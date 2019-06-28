import logging
import numpy as np
import os
import sys

from mpi4py import MPI


def read_depth_image(data, w, h):
    image = np.frombuffer(data, dtype=np.float32)
    image = image.reshape(h, w)
    return image


def read_normals_image(data, w, h):
    image = np.frombuffer(data, dtype=np.float32)
    image = image.reshape(h, w, 3)
    return image


def read_rgba_image(data, w, h):
    image = np.frombuffer(data, dtype=np.uint8)
    image = image.reshape(h, w, -1)
    return image


def read_segmentation_image(data, w, h):
    image = np.frombuffer(data, dtype=np.uint8)
    return image.reshape(h, w)


def render_depth(image):
    image = np.expand_dims(image, -1)
    image = np.tile(image * 255, [1, 1, 3])
    return image.astype(np.uint8)


def render_normals(image):
    return (image * 255).astype(np.uint8)


def render_segmentation(image):
    n_categories = image.max() + 1
    assert n_categories < 8, f"only 8 categories supported, got {n_categories}"

    # set all colors to the 0, 255 extremes. so we only support 8 colors.
    assert image.ndim == 2
    new_image = np.zeros(list(image.shape) + [3], dtype=np.uint8)
    for i in range(n_categories):
        color = np.array([int(b) for b in '{0:03b}'.format(i)[::-1]]) * 255
        new_image[image == i, ...] = color

    return new_image


def package_relative_path(path):
    return os.path.normpath(
        os.path.join(os.path.abspath(os.path.dirname(__file__)), '..', path))


def renderer_closer(renderer):
    renderer.shutdown()


def build_server_configs(num_gpus, num_workers, base_port, mpi_rank, mpi_size):
    server_configs = []
    for worker in range(num_workers):
        for gpu in range(num_gpus):
            server_configs.append([gpu, base_port])
            base_port += 1
    assert len(server_configs) % mpi_size == 0
    chunk = len(server_configs) // mpi_size
    return server_configs[mpi_rank * chunk:(mpi_rank + 1) * chunk]


def build_batch(states, seed): 
    return {'qpos': states, 'seed': seed} 


def setup_logging():
    mpi_comm = MPI.COMM_WORLD

    mpi_rank = mpi_comm.Get_rank()
    mpi_size = mpi_comm.Get_size()

    logger_handler = logging.StreamHandler(sys.stderr)
    if mpi_size > 1:
        logger_handler.setFormatter(logging.Formatter(
            fmt=f'%(asctime)s.%(msecs)03d <{mpi_rank}> %(levelname)s: %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'))
    else:
        logger_handler.setFormatter(logging.Formatter(
            fmt=f'%(asctime)s.%(msecs)03d %(levelname)s: %(message)s',
            datefmt='%Y-%m-%d %H:%M:%S'))
    logging.getLogger().setLevel(logging.DEBUG if os.getenv('DEBUG', 0) == '1' else logging.INFO)
    logging.getLogger().handlers.clear()
    logging.getLogger().addHandler(logger_handler)


class ScreenWriter:
    """
    Renders images to the screen.
    """

    def __init__(self, window_name="image", history_size=6, print_numbers=True):
        import cv2
        self.cv2 = cv2
        self.window_name = window_name
        self.history_size = history_size
        self.history = []
        self.count = 0
        self.print_numbers = print_numbers

    def write(self, image, timeout=0):
        """ Returns True if user pressed escape. """
        if isinstance(image, list):
            image = np.vstack(np.flipud(image))
        elif isinstance(image, dict):
            images = []
            for key, img in image.items():
                if (isinstance(img, np.ndarray) and
                        img.ndim in (3, 4) and
                        img.shape[-1] in (1, 3) and
                        img.dtype == np.uint8):
                    images.append(np.flipud(img))
            assert all((images[0].shape == img.shape) for img in images)
            image = np.vstack(images)

        if self.print_numbers:
            self.cv2.putText(image, f'{self.count}', (10, 20), self.cv2.FONT_HERSHEY_DUPLEX, 0.5,
                             (224, 224, 224))
        self.count += 1

        self.history.append(image)
        if len(self.history) > self.history_size:
            self.history.pop(0)
        image = np.hstack(self.history)

        self.cv2.imshow(self.window_name, self.cv2.cvtColor(image, self.cv2.COLOR_BGR2RGB))
        res = self.cv2.waitKey(timeout)
        return (res == 27)
