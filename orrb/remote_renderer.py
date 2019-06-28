import grpc
import logging
import os
import platform
import socket
import subprocess
import time

import google.protobuf.json_format as json_format
import numpy as np

from copy import deepcopy
from queue import Queue

from orrb.queue_executor import QueueExecutorABC, QueueWorkerABC

import orrb.protos.RendererConfig_pb2 as renderer_config_pb2
import orrb.protos.RenderService_pb2 as render_service_pb2
import orrb.protos.RenderService_pb2_grpc as render_service_pb2_grpc
from orrb.utils import (
    read_depth_image,
    read_normals_image,
    read_rgba_image,
    read_segmentation_image,
)

# Uncompressed batches of images tend to be big, default for grpc is just 4MB.
DEFAULT_GRPC_MESSAGE_SIZE = 256 * 1024 * 1024


def _get_server_bind_host():
    if platform.system() == 'Linux':
        return socket.gethostname()
    else:
        return 'localhost'


def _create_render_service_stub(port, message_size=DEFAULT_GRPC_MESSAGE_SIZE, timeout=10.0):
    channel = grpc.insecure_channel('%s:%d' % (_get_server_bind_host(), port),
                                    [('grpc.max_receive_message_length', message_size)])
    grpc.channel_ready_future(channel).result(timeout=timeout)
    return render_service_pb2_grpc.RenderServiceStub(channel)


def _build_render_batch_request(workload, config):
    request = render_service_pb2.RenderBatchRequest()
    request.width = config.image_width
    request.height = config.image_height

    if 'seed' in workload:
        request.batch_seed = workload['seed'] % (1 << 31)  # Truncate to fit int32
        use_entry_seeds = False

    if 'seeds' in workload:
        seeds = workload['seeds']
        use_entry_seeds = True

    request.use_entry_seeds = use_entry_seeds
    request.render_alpha = config.render_alpha
    request.render_depth = config.render_depth
    request.render_normals = config.render_normals
    request.render_segmentation = config.render_segmentation

    for i, qpos in enumerate(workload['qpos']):
        entry = request.entries.add()
        entry.qpos[:] = qpos
        if use_entry_seeds:
            entry.seed = seeds[i]

    for camera_name in config.camera_names:
        request.camera_names.append(camera_name)

    return request, len(workload['qpos'])


def _add_auxiliary_stream(batch_dataset, batch_size, stream, dtype):
    data = np.array(stream.data, dtype=dtype)
    data_length = len(data)
    if data_length % batch_size == 0:
        data = data.reshape((batch_size, len(data) // batch_size))
        batch_dataset[stream.name] = data
    else:
        logging.warning(f'Error in "{stream.name}" len: {len(data)} batch: {batch_size}.')


def _convert_render_batch_response(response, config, batch_size):
    h, w = config.image_height, config.image_width
    batch_dataset = dict()
    for stream in response.streams:
        stream_rgb_dataset = []
        stream_depth_dataset = []
        stream_normals_dataset = []
        stream_segmentation_dataset = []
        for entry in stream.entries:
            if entry.image_data:
                image = read_rgba_image(entry.image_data, w, h)
                stream_rgb_dataset.append(image)
            if entry.depth_data:
                image = read_depth_image(entry.depth_data, w, h)
                stream_depth_dataset.append(image)
            if entry.normals_data:
                image = read_normals_image(entry.normals_data, w, h)
                stream_normals_dataset.append(image)
            if entry.segmentation_data:
                image = read_segmentation_image(entry.segmentation_data, w, h)
                stream_segmentation_dataset.append(image)

        batch_dataset[stream.name] = np.array(stream_rgb_dataset)
        if stream_depth_dataset:
            batch_dataset['%s_depth' % stream.name] = np.array(stream_depth_dataset)
        if stream_normals_dataset:
            batch_dataset['%s_normals' % stream.name] = np.array(stream_normals_dataset)
        if stream_segmentation_dataset:
            batch_dataset['%s_segmentation' % stream.name] = np.array(stream_segmentation_dataset)

    for float_stream in response.auxiliary_float_streams:
        _add_auxiliary_stream(batch_dataset, batch_size, float_stream, float)

    for int_stream in response.auxiliary_int_streams:
        _add_auxiliary_stream(batch_dataset, batch_size, int_stream, int)

    for bool_stream in response.auxiliary_bool_streams:
        _add_auxiliary_stream(batch_dataset, batch_size, bool_stream, bool)

    return batch_dataset


def _build_update_request(renderer_config):
    request = render_service_pb2.UpdateRequest()

    for component in renderer_config.components:
        component_copy = request.components.add()
        component_copy.CopyFrom(component)

    return request


class _WorkloadWithConfig:
    def __init__(self, renderer_config_stamp, renderer_config, workload):
        self.renderer_config_stamp = renderer_config_stamp
        self.renderer_config = renderer_config
        self.workload = workload


class _RemoteRendererWorker(QueueWorkerABC):

    def __init__(self, input_queue, device, port, base_config):
        super().__init__(input_queue)
        self.device = device
        self.port = port
        self.base_config = base_config
        self.server_process = None
        self.client_stub = None
        self.renderer_config_stamp = 0

    def on_run(self):
        if self.base_config.spawn_servers:
            command, args, environment = self._server_commandline()
            logging.info('Starting render server: %s args: %s env: %s.' % (
                command, args, environment if environment else '-'))

            environment_copy = os.environ.copy()
            if environment:
                environment_copy.update(environment)

            if os.getenv('ORRB_DETACHED'):
                print('\nManually start: %s' % ' '.join([command] + args))
                input('\nPress enter when ready.')
            else:
                self.server_process = subprocess.Popen([command] + args,
                                                       stdout=subprocess.DEVNULL,
                                                       stderr=subprocess.DEVNULL,
                                                       cwd=os.path.dirname(command),
                                                       env=environment_copy)
        for i in range(10):
            try:
                self.client_stub = _create_render_service_stub(self.port)
                return
            except:
                logging.warning(f'... cannot create client stub, retrying. If this persists '
                                f'check /tmp/StandaloneRenderer.{self.port}.log.')
                time.sleep(1 + i)
        raise RendererError('Could not create remote render server.')

    def on_shutdown(self):
        if self.server_process:
            self.server_process.terminate()
            self.server_process.kill()
            self.server_process.wait()

    def process(self, workload_with_config):
        if workload_with_config.renderer_config_stamp != self.renderer_config_stamp:
            self.renderer_config_stamp = workload_with_config.renderer_config_stamp
            self.client_stub.Update(_build_update_request(workload_with_config.renderer_config))

        actual_workload = workload_with_config.workload
        request, batch_size = _build_render_batch_request(actual_workload, self.base_config)
        response = self.client_stub.RenderBatch(request)
        actual_workload.update(
            _convert_render_batch_response(response, self.base_config, batch_size))
        return actual_workload

    def _server_commandline(self):
        assert self.base_config.renderer_local_binary
        assert self.base_config.model_xml_path
        assert self.base_config.model_mapping_path
        assert self.base_config.renderer_config_path
        commandline_args = ['-logFile', '/tmp/StandaloneRenderer.%d.log' % self.port,
                            '--render_server.host=%s' % _get_server_bind_host(),
                            '--render_server.port=%d' % self.port,
                            '--render_server.workers_count=%d' % self.base_config.workers_count,
                            '--render_server.queues_count=%d' % self.base_config.queues_count,
                            '--render_server.streams_count=%d' % self.base_config.streams_count,
                            '--main.mode=Server',
                            '--main.model_xml_path=%s' % self.base_config.model_xml_path,
                            '--main.model_mapping_path=%s' % self.base_config.model_mapping_path,
                            '--main.renderer_config_path=%s' % (
                                self.base_config.renderer_config_path),
                            '--main.asset_basedir=%s' % self.base_config.asset_basedir,
                            '--main.parent_pid=%d' % os.getpid()]
        display = os.getenv('ORRB_DISPLAY', '0')
        environment = {'DISPLAY': ':%s.%d' % (display, self.device)}
        return (self.base_config.renderer_local_binary, commandline_args, environment)


def _resolve_path(base_dir, path):
    if os.path.isabs(path):
        return path
    else:
        return os.path.normpath(os.path.join(os.path.abspath(base_dir), path))


def _load_renderer_config(base_dir, renderer_config_path):
    resolved_renderer_config_path = _resolve_path(base_dir, renderer_config_path)
    with open(resolved_renderer_config_path, 'r') as f:
        config = f.read()
        logging.info(f'Renderer config: {resolved_renderer_config_path}\n\n{config}')
        return json_format.Parse(config, renderer_config_pb2.RendererConfig())


class RendererError(Exception):
    pass


class RemoteRendererConfig:

    def __init__(self):
        self.camera_names = []
        self.render_alpha = False
        self.render_depth = False
        self.render_normals = False
        self.render_segmentation = False
        self.image_width = 100
        self.image_height = 100
        self.spawn_servers = True
        self.renderer_local_binary = None
        self.renderer_version = None
        self.renderer_config_path = None
        self.model_xml_path = None
        self.model_mapping_path = None
        self.asset_basedir = "."

        # Tune these params according to the request load placed upon each render server.
        self.workers_count = 4
        self.queues_count = 4
        self.streams_count = 4


class RemoteRenderer(QueueExecutorABC):

    def __init__(self, name, server_configs, base_config):
        """Main class for managing remote renderer workers and configs.

        :param name: A name for this renderer in str.
        :param server_configs: A list of (device, port), where device could be the GPU id.
        :param base_config: A orrb.RemoteRendererConfig object.
        """
        self.renderer_config_stamp = 0
        self.renderer_config = _load_renderer_config(base_config.asset_basedir,
                                                     base_config.renderer_config_path)
        self.local_config = deepcopy(base_config)
        self.sync_queue = Queue()

        if self.local_config.renderer_local_binary is None:
            self.local_config.renderer_local_binary = get_renderer_executable(
                self.local_config.renderer_version)

        super().__init__(name, server_configs, self.local_config)

    def mutable_renderer_config(self):
        return self.renderer_config

    def create_workers(self, input_queue, server_configs, base_config):
        assert len(server_configs) > 0

        if os.getenv('ORRB_MINIMAL'):
            minimal_setup_size = int(os.getenv('ORRB_MINIMAL'))
            server_configs = server_configs[:minimal_setup_size]

        workers = []
        for (device, port) in server_configs:
            workers.append(_RemoteRendererWorker(input_queue, device, port, base_config))
        return workers

    def execute(self, workload, destination):
        assert False, "Executor low level API is hidden by the renderer."

    def render_batch_async(self, workload, destination):
        workload_with_config = _WorkloadWithConfig(self.renderer_config_stamp,
                                                   self.renderer_config,
                                                   workload)
        super().execute(workload_with_config, destination)

    def render_batch(self, workload):
        self.render_batch_async(workload, self.sync_queue)
        result = self.sync_queue.get()
        self.sync_queue.task_done()
        return result

    def update(self, renderer_config):
        self.renderer_config_stamp += 1
        self.renderer_config = deepcopy(renderer_config)


def get_renderer_executable(version):
    version_override = os.getenv('ORRB_VERSION_OVERRIDE')
    if version_override is not None:
        logging.warning(f'Overriding ORRB version with: {version_override}')
        version = version_override

    if version is None:
        raise ValueError('Cannot resolve renderer binary, version is None.')

    orb_binaries_dir = os.getenv('ORRB_BINARIES_DIR')
    if orb_binaries_dir is None:
        raise ValueError('Point ORRB_BINARIES_DIR to the directory with built renderers.')

    package = 'StandaloneRenderer-%s-%s-%s' % (platform.system(), platform.machine(), version)
    package_executable = os.path.join(orb_binaries_dir, package, 'run.sh')
    if not os.path.exists(package_executable):
        raise ValueError('Missing run.sh in package: %s (%s).' % (package, orb_binaries_dir))
    return package_executable
