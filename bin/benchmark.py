import atexit
import click
import logging
import orrb
import orrb.utils as utils
import os
import sys
import time

import numpy as np

from mpi4py import MPI
from queue import Queue


def _load_states():
    return np.loadtxt(utils.package_relative_path('assets/states/qpos.csv'), delimiter=',')


def _build_renderer(num_gpus, num_workers, base_port, mpi_rank, mpi_size,
                    render_depth, render_segmentation, render_normals):
    config = orrb.RemoteRendererConfig()
    config.camera_names = ['vision_cam_left', 'vision_cam_top', 'vision_cam_right']
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


@click.command()
@click.option('--num-gpus', type=int, default=1)
@click.option('--workers-per-gpu', type=int, default=1)
@click.option('--iterations', type=int, default=20)
@click.option('--base-port', type=int, default=7000)
@click.option('--render-depth', type=bool, default=False)
@click.option('--render-normals', type=bool, default=False)
@click.option('--render-segmentation', type=bool, default=False)
def main(num_gpus, workers_per_gpu, iterations, base_port,
         render_depth, render_normals, render_segmentation):
    mpi_comm = MPI.COMM_WORLD

    mpi_rank = mpi_comm.Get_rank()
    mpi_size = mpi_comm.Get_size()

    config, renderer = _build_renderer(num_gpus, workers_per_gpu, base_port, mpi_rank, mpi_size,
                                       render_depth, render_normals, render_segmentation)

    states = _load_states()
    queue = Queue()

    cameras_count = len(config.camera_names)
    batch_size = len(states)
    seed = mpi_rank * 11713

    all_workers = num_gpus * workers_per_gpu
    assert all_workers % mpi_size == 0
    local_workers = all_workers // mpi_size

    renderer.start()

    atexit.register(utils.renderer_closer, renderer)

    sleep_time = 5.0 + all_workers * 0.5
    logging.info(f'Sleeping for: {sleep_time}s.')
    time.sleep(sleep_time)

    logging.info(f'Queueing {iterations} iterations + 1 warmup, on {local_workers} local workers.')
    for _ in range((iterations + 1) * local_workers):
        renderer.render_batch_async(utils.build_batch(states, seed), queue)
        seed += batch_size

    logging.info('Warmup pass.')

    for _ in range(local_workers):
        result = queue.get()
        queue.task_done()

    logging.info('Warmup done. Waiting on a barrier.')
    mpi_comm.Barrier()

    logging.info('Starting benchmark.')
    start_time = time.time()

    for _ in range(iterations * local_workers):
        result = queue.get()
        queue.task_done()

    logging.info('Done. Waiting on a barrier.')
    mpi_comm.Barrier()

    delta_time = time.time() - start_time
    total_frames = all_workers * iterations * batch_size * cameras_count
    fps = float(total_frames) / delta_time
    if mpi_rank is 0:
        logging.info(f'{total_frames} frames in {delta_time}s : {fps} fps.')


if __name__ == '__main__':
    utils.setup_logging()
    main()
