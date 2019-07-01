"""
This is a simple, sample application that shows how to use the renderer as a training
data source. It uses Keras. The training data is constructed from the example state batch.
In order to provide some variety to the training set we augment it: randomize the cube
rotation, perturb its position and perturb the robot's joints.
"""

import click
import logging
import numpy as np
import orrb
import sys


try:
    import tensorflow as maybe_tf
except:
    logging.error('You need tensorflow installed to make this demo work. Try: '
                  '\'pip install tensorflow\'.')
    sys.exit(1)


try:
    import keras as maybe_keras
except:
    logging.error('You need keras installed to make this demo work. Try: '
                  '\'pip install keras\'.')
    sys.exit(1)


from keras.layers import Dense, Flatten, Input, Conv2D, BatchNormalization, Activation, add as Add
from keras.models import Model
from keras.optimizers import SGD, Adam
from keras.regularizers import l2
from queue import Queue


def _build_renderer(num_gpus, num_workers, base_port):
    config = orrb.RemoteRendererConfig()
    config.camera_names = ['vision_cam_left']
    config.image_width = 200
    config.image_height = 200
    config.renderer_version = orrb.get_renderer_version()
    config.model_xml_path = 'dactyl.xml'
    config.model_mapping_path = 'dactyl.mapping'
    config.renderer_config_path = 'demo_keras.renderer_config.json'
    config.asset_basedir = orrb.utils.package_relative_path('assets')
    config.render_depth = False
    config.render_normals = False
    config.render_segmentation = False

    server_configs = orrb.utils.build_server_configs(num_gpus, num_workers, base_port, mpi_rank=0,
                                                     mpi_size=1)
    return config, orrb.RemoteRenderer('OrrbRenderer0', server_configs, config)


def _load_states():
    return np.loadtxt(orrb.utils.package_relative_path('assets/states/qpos.csv'), delimiter=',')


def _build_model(lr, reg_weight):
    """Build a simple convolutional model to predict screen (x, y) from a 200x200x3 image."""
    inputs = Input(shape=(200, 200, 3))
    
    x = Conv2D(32, (5, 5), activation='relu', input_shape=(200, 200, 3),
               kernel_regularizer=l2(reg_weight))(inputs)
    x = Conv2D(32, (3, 3), activation='relu', kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    x = Conv2D(32, (3, 3), activation='relu', strides=(2, 2), kernel_regularizer=l2(reg_weight))(x)
    
    x = Flatten()(x)
    x = Dense(64, activation='relu', kernel_regularizer=l2(reg_weight))(x)
    x = Dense(64, activation='relu', kernel_regularizer=l2(reg_weight))(x)
    x = Dense(2)(x)

    optimizer = Adam(lr=lr)
    
    model = Model(inputs=inputs, outputs=x)
    model.compile(loss='mean_squared_error', optimizer=optimizer)

    return model


def _random_quaternion(batch_size):
    result = []
    for _ in range(batch_size):
        norm = 0
        while norm < 0.00001:
            axis = np.random.rand(3) - 0.5
            norm = np.linalg.norm(axis)
        axis /= norm
        angle = (np.random.rand(1)[0] * 2 * np.pi) - np.pi
        axis *= np.sin(0.5 * angle)
        result.append([axis[0], axis[1], axis[2], np.cos(0.5 * angle)])
    return result


NUM_JOINT_STATES = 24


def _augment_states(base_states):
    batch_size = len(base_states)
    states = np.copy(base_states)
    # Randomize joints +/- 0.05 radians.
    states[:,0:NUM_JOINT_STATES] += (np.random.rand(batch_size, NUM_JOINT_STATES) - 0.5) / 10.0
    # Randomize block position +/- 1cm.
    states[:,NUM_JOINT_STATES:NUM_JOINT_STATES + 3] += (np.random.rand(batch_size, 3) - 0.5) / 50.0
    # Randomly rotate the block.
    states[:,NUM_JOINT_STATES + 3:NUM_JOINT_STATES + 7] = _random_quaternion(batch_size)
    return states


def _schedule_batches(count, renderer, queue, states, seed):
    for _ in range(count):
        # Augment the states, so that we have more training variety.
        states = _augment_states(states)
        batch = orrb.utils.build_batch(states, seed)
        renderer.render_batch_async(batch, queue)
        seed += len(states)
    return seed


def _add_marker(image, coordinates):
    """Put a little cross in the image at the provided coordinates."""
    x = max(7, min(image.shape[0] - 8, int(coordinates[0] * image.shape[0])))
    y = max(7, min(image.shape[1] - 8, int(coordinates[1] * image.shape[1])))

    for ix in range(x-7, x+7):
        image[y, ix, 0:3] = 255 - image[y, ix, 0:3]

    for iy in range(y-7, y+7):
        image[iy, x, 0:3] = 255 - image[iy, x, 0:3]


@click.command()
@click.option('--epochs', type=int, default=20)
@click.option('--iterations', type=int, default=200)
@click.option('--lr', type=float, default=0.001)
@click.option('--gpus', type=int, default=1)
@click.option('--workers-per-gpu', type=int, default=2)
@click.option('--base-port', type=int, default=7777)
@click.option('--show/--no-show', default=True)
@click.option('--reg-weight', type=float, default=0.0001)
def main(epochs, iterations, lr, gpus, workers_per_gpu, base_port, show, reg_weight):
    config, renderer = _build_renderer(gpus, workers_per_gpu, base_port)
    base_states = _load_states()
    model = _build_model(lr, reg_weight)
    queue = Queue()
    seed = 0
    
    logging.getLogger().setLevel(logging.INFO)

    writer = orrb.utils.ScreenWriter()

    logging.info('Starting renderer.')
    renderer.start()

    logging.info('Preloading some data.')
    seed = _schedule_batches(16, renderer, queue, base_states, seed)

    logging.info('Starting training.')
    for epoch in range(epochs):
        logging.info(f'Epoch: {epoch}.')
        for iteration in range(iterations):

            batch = queue.get()
            queue.task_done()
            seed = _schedule_batches(1, renderer, queue, base_states, seed)

            images = batch['vision_cam_left']
            cube_position = batch['tracker_cube_X_vision_cam_left']

            loss = model.train_on_batch(images, cube_position)
            logging.info(f'Iteration: {iteration} loss: {loss}')

        logging.info(f'Evaluating.')

        # Use the base_states as the test set, do not augment, use the same seed.
        batch = orrb.utils.build_batch(base_states, 0)
        rendered_batch = renderer.render_batch(batch)

        images = rendered_batch['vision_cam_left']
        cube_position = rendered_batch['tracker_cube_X_vision_cam_left']

        loss = model.evaluate(images, cube_position)
        logging.info(f'Evaluation loss: {loss}.')

        # Show the predicted cube positions on a number of rendered samples.
        if show:
            predictions = model.predict(images)
            for i in range(len(base_states)):
                _add_marker(images[i], predictions[i])
                writer.write(images[i], timeout=200)


if __name__ == '__main__':
    main()
