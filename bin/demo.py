import atexit
import click
import gym
import logging
import orrb
import orrb.utils as utils
import os
import subprocess
import sys
import tempfile

import numpy as np

from google.protobuf import json_format
from queue import Queue

import orrb.protos.RendererConfig_pb2 as renderer_config_pb2


@click.group()
def cli():
    pass


def _save_model_xml(sim):
    with tempfile.NamedTemporaryFile(delete=False, suffix='.xml', mode='w') as f:
        f.write(sim.model.get_xml())
    return f.name


def _save_mapping(sim):
    with tempfile.NamedTemporaryFile(delete=False, suffix='.mapping', mode='w') as f:
        for joint_name in sim.model.joint_names:
            joint_id = sim.model.joint_name2id(joint_name)
            f.write(f'{joint_name}, {sim.model.jnt_qposadr[joint_id]}\n')
    return f.name


def _run_interactive(asset_basedir, model_xml_path, model_mapping_path, renderer_config_path,
                     model_state_path):
    renderer_executable = orrb.get_renderer_executable(orrb.get_renderer_version())
    renderer = subprocess.Popen([renderer_executable,
                                 '-logFile', '-screen-width', '1200', '-screen-height', '800',
                                 '--main.mode=Interactive',
                                 f'--main.model_xml_path={model_xml_path}',
                                 f'--main.model_mapping_path={model_mapping_path}',
                                 f'--main.renderer_config_path={renderer_config_path}',
                                 f'--main.model_state_path={model_state_path}',
                                 f'--main.asset_basedir={asset_basedir}'],
                                 cwd=os.path.dirname(renderer_executable))
    renderer.wait()

   
def _resolve_gym_sim(gym_environment):
    environment = gym.make(gym_environment)
    environment.reset()
    sim = None

    try:
        sim = environment.sim
    except:
        logging.warning(f'No sim in environment: {gym_environment}.')

    if not sim:
        try:
            sim = environment.unwrapped.sim
        except:
            logging.warning(f'No sim in unwrapped environment: {gym_environment}.')
    return sim


@cli.command()
@click.argument('gym_environment', default=None)
@click.option('--asset-basedir', default=utils.package_relative_path('assets/stls'))
def gymenv(gym_environment, asset_basedir):
    sim = _resolve_gym_sim(gym_environment)
    assert sim, 'This environment does not seem to have a MuJoCo simulation.'

    model_xml_path = _save_model_xml(sim)
    model_mapping_path = _save_mapping(sim)
    renderer_config_path = utils.package_relative_path('assets/gym.renderer_config.json')
    model_state_path = utils.package_relative_path('assets/states')
    _run_interactive(asset_basedir, model_xml_path, model_mapping_path, renderer_config_path,
                     model_state_path)


@cli.command()
@click.argument('asset_basedir', default=utils.package_relative_path('assets'))
@click.argument('model_xml_path', default='dactyl.xml')
@click.argument('model_mapping_path', default='dactyl.mapping')
@click.argument('renderer_config_path', default='dactyl.renderer_config.json')
@click.argument('model_state_path', default='states')
def interactive(asset_basedir, model_xml_path, model_mapping_path, renderer_config_path,
                model_state_path):
    _run_interactive(asset_basedir, model_xml_path, model_mapping_path, renderer_config_path,
                     model_state_path)


def _load_states():
    return np.loadtxt(utils.package_relative_path('assets/states/qpos.csv'), delimiter=',')


def _build_renderer(num_gpus, num_workers, base_port, mpi_rank, mpi_size,
                    render_depth, render_segmentation, render_normals, cameras):
    config = orrb.RemoteRendererConfig()
    config.camera_names = cameras
    config.image_width = 200
    config.image_height = 200
    config.renderer_version = orrb.get_renderer_version()
    config.model_xml_path = 'dactyl.xml'
    config.model_mapping_path = 'dactyl.mapping'
    config.renderer_config_path = 'dactyl.renderer_config.json'
    config.asset_basedir = utils.package_relative_path('assets')
    config.render_depth = render_depth
    config.render_normals = render_normals
    config.render_segmentation = render_segmentation

    server_configs = utils.build_server_configs(num_gpus, num_workers, base_port, mpi_rank,
                                                mpi_size)

    return config, orrb.RemoteRenderer('OrrbRenderer0', server_configs, config)


def _load_renderer_config(config_path):
    config = renderer_config_pb2.RendererConfig()
    with open(utils.package_relative_path(config_path), 'r') as f:
        json_format.Parse(f.read(), config)
    return config


@cli.command()
@click.option('--cameras', default='vision_cam_top,vision_cam_left,vision_cam_right')
def batch_update(render_depth, render_normals, render_segmentation, cameras):
    cameras = cameras.split(',')
    config, renderer = _build_renderer(1, 1, 7000, 0, 1, False, False, False, cameras)
    renderer.start()

    atexit.register(utils.renderer_closer, renderer)

    configs = [_load_renderer_config('assets/update_1.renderer_config.json'),
               _load_renderer_config('assets/update_2.renderer_config.json')]

    def updating_batch_iterator():
        states = _load_states()[0:3]
        iteration = 0
        while True:
            if iteration % 5 == 0:
                renderer.update(configs[iteration % 2])

            iteration += len(states)
            result = renderer.render_batch(utils.build_batch(states, iteration))

            for i in range(len(states)):
                yield {cam_name: result[cam_name][i] for cam_name in config.camera_names}

    writer = utils.ScreenWriter()

    for result in updating_batch_iterator():
        if writer.write(result):
            break


def _add_marker(image, coordinates):
    x = int(coordinates[0] * image.shape[0])
    y = int(coordinates[1] * image.shape[1])

    for ix in range(x-7, x+7):
        image[y, ix, 0:3] = 255 - image[y, ix, 0:3]

    for iy in range(y-7, y+7):
        image[iy, x, 0:3] = 255 - image[iy, x, 0:3]


def _add_bbox(image, bounds):
    min_x = int(bounds[0] * image.shape[0])
    max_x = int(bounds[1] * image.shape[0])
    min_y = int(bounds[2] * image.shape[1])
    max_y = int(bounds[3] * image.shape[1])

    for ix in range(min_x, max_x):
        for bound_y in [min_y, max_y]:
            image[bound_y, ix, 0:3] = 255 - image[bound_y, ix, 0:3]

    for iy in range(min_y, max_y):
        for bound_x in [min_x, max_x]:
            image[iy, bound_x, 0:3] = 255 - image[iy, bound_x, 0:3]


@cli.command()
@click.argument('num_gpus', type=int, default=1)
@click.argument('num_workers', type=int, default=2)
@click.argument('base_port', type=int, default=7000)
@click.option('--render-info/--no-render-info', default=True)
@click.option('--render-depth/--no-render-depth', default=False)
@click.option('--render-normals/--no-render-normals', default=False)
@click.option('--render-segmentation/--no-render-segmentation', default=False)
@click.option('--cameras', default='vision_cam_top,vision_cam_left,vision_cam_right')
def batch(num_gpus, num_workers, base_port, render_info, render_depth, render_normals,
          render_segmentation, cameras):
    cameras = cameras.split(',')
    config, renderer = _build_renderer(num_gpus, num_workers, base_port, 0, 1,
                                       render_depth, render_normals, render_segmentation,
                                       cameras)
    renderer.start()
    
    atexit.register(utils.renderer_closer, renderer)

    def batch_iterator():
        queue = Queue()
        seed = 0
        states = _load_states()
        
        # Preload couple of batches.
        for _ in range(5):
            renderer.render_batch_async(utils.build_batch(states, seed), queue)
            seed += len(states)

        while True:
            seed += len(states)
            renderer.render_batch_async(utils.build_batch(states, seed), queue)
            result = queue.get()
            queue.task_done()

            for i in range(len(states)):
                images = {}
                for cam_name in config.camera_names:
                    images[cam_name] = result[cam_name][i]

                    depth_name = f'{cam_name}_depth'
                    if depth_name in result:
                        images[depth_name] = utils.render_depth(result[depth_name][i])

                    normals_name = f'{cam_name}_normals'
                    if normals_name in result:
                        images[normals_name] = utils.render_normals(result[normals_name][i])

                    segmentation_name = f'{cam_name}_segmentation'
                    if segmentation_name in result:
                        images[segmentation_name] = utils.render_segmentation(
                            result[segmentation_name][i])

                if render_info:
                    for key in result.keys():
                        if key.startswith('tracker'):
                            for camera in config.camera_names:
                                if key.endswith(camera):
                                    _add_marker(images[camera], result[key][i])
                                if key.endswith(f"{camera}_bbox"):
                                    _add_bbox(images[camera], result[key][i])
                yield images

    writer = utils.ScreenWriter(print_numbers=render_info)
   
    for result in batch_iterator():
        if writer.write(result):
            break


if __name__ == '__main__':
    utils.setup_logging()
    cli()
