import cv2
import click
import grpc
import numpy as np

import orrb.protos.RenderService_pb2 as render_service_pb2
import orrb.protos.RenderService_pb2_grpc as render_service_pb2_grpc
from orrb.remote_renderer import DEFAULT_GRPC_MESSAGE_SIZE
from orrb.utils import (
    read_depth_image,
    read_normals_image,
    read_rgba_image,
    read_segmentation_image,
    render_depth,
    render_normals,
    render_segmentation,
    )


@click.command()
@click.option('--host', '-h', default='localhost')
@click.option('--port', '-p', default=6666)
@click.option('--batch-size', '-b', default=1)
@click.option('--image-size', '-s', default=200)
@click.option('--render-depth', type=bool, default=True)
@click.option('--render-normals', type=bool, default=True)
@click.option('--render-segmentation', type=bool, default=True)
def main(host, port, batch_size, image_size, render_depth, render_normals, render_segmentation):
    """
    Utility to query a running render service and render results to screen.

    You can run the Editor in 'server' mode and use this utility to connect to it.
    """
    client_stub = _create_render_service_stub(host, port)

    seed = 0
    while True:
        request = _build_render_batch_request(
            image_size, batch_size, render_depth, render_normals, render_segmentation, seed)
        response = client_stub.RenderBatch(request)
        image = _handle_response(request, response)
        cv2.imshow("images", cv2.cvtColor(image, cv2.COLOR_BGR2RGB))
        if cv2.waitKey(0) == 27:
            break
        seed += batch_size


def _create_render_service_stub(host, port,
                                message_size=DEFAULT_GRPC_MESSAGE_SIZE,
                                timeout=10.0):
    channel = grpc.insecure_channel(
        '%s:%d' % (host, port),
        [('grpc.max_receive_message_length', DEFAULT_GRPC_MESSAGE_SIZE)])
    grpc.channel_ready_future(channel).result(timeout=timeout)
    return render_service_pb2_grpc.RenderServiceStub(channel)


def _build_render_batch_request(image_size, batch_size,
                                render_depth, render_normals, render_segmentation, seed):
    request = render_service_pb2.RenderBatchRequest()
    request.width = image_size
    request.height = image_size

    request.batch_seed = seed

    request.use_entry_seeds = False
    request.render_alpha = False
    request.render_depth = render_depth
    request.render_normals = render_segmentation
    request.render_segmentation = render_normals

    for i in range(batch_size):
        entry = request.entries.add()
        entry.qpos[:] = np.zeros(100)

    for camera_name in ['vision_cam_top', 'vision_cam_left', 'vision_cam_right']:
        request.camera_names.append(camera_name)

    return request


def _handle_response(request, response):
    w, h = request.width, request.height
    columns = []
    for stream in response.streams:
        images = []
        for entry in stream.entries:
            mode_images = []
            for mode in ['image', 'depth', 'normals', 'segmentation']:
                mode_data = getattr(entry, f'{mode}_data')
                if not mode_data:
                    continue

                if mode == 'depth':
                    image = read_depth_image(mode_data, w, h)
                    image = render_depth(image)
                elif mode == 'normals':
                    image = read_normals_image(mode_data, w, h)
                    image = render_normals(image)
                elif mode == 'segmentation':
                    image = read_segmentation_image(mode_data, w, h)
                    image = render_segmentation(image)
                else:
                    image = read_rgba_image(mode_data, w, h)

                mode_images.append(image)
            images.append(np.vstack(mode_images))
        columns.append(np.vstack(images))

    image = np.hstack(columns)
    return image


if __name__ == '__main__':
    main()
