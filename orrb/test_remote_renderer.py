import pytest
import numpy as np
import orrb
import os
import imageio
import platform

from orrb.utils import render_depth, render_normals, render_segmentation


# Run `GENERATE_IMAGES=1 pytest` to generate all images regardless of assertion errors.


def _package_relative_path(path):
    return os.path.normpath(
        os.path.join(os.path.abspath(os.path.dirname(__file__)), '..', path))


def _load_states(max_count=None):
    states = np.loadtxt(_package_relative_path('assets/states/qpos.csv'), delimiter=',')
    if max_count:
        return states[:max_count]
    else:
        return states


def _build_renderer():
    config = orrb.RemoteRendererConfig()
    config.camera_names = ['vision_cam_left', 'vision_cam_top', 'vision_cam_right']
    config.image_width = 200
    config.image_height = 200
    config.renderer_version = orrb.get_renderer_version()
    config.model_xml_path = 'dactyl.xml'
    config.model_mapping_path = 'dactyl.mapping'
    config.renderer_config_path = 'dactyl.renderer_config.json'
    config.asset_basedir = _package_relative_path('assets')
    config.render_depth = True
    config.render_normals = True
    config.render_segmentation = True

    return config, orrb.RemoteRenderer('OrrbRenderer0', [[0, 7000]], config)


def _build_batch(states):
    return {'qpos': states, 'seeds': np.arange(len(states))}


def _generate_test_image_name(test_name, camera_name, image_id):
    return f'test_{test_name}_{camera_name}_{image_id}'

# The Golden Images were generated on a 2018 MBP with a Radeon Pro 560X, the error
# thresholds were adjusted to pass on: a 2017 ThinkPad Laptop with Ubuntu and a NVidia
# Quadro M2000M, and Skylake based Ubuntu GCE VM with a V100.

@pytest.mark.skipif(platform.system() not in ['Darwin', 'Linux'],
                    reason="Right now we only support MacOS and Linux")
def test_remote_renderer():
    config, renderer = _build_renderer()
    renderer.start()

    image_count = 5

    states = _load_states(max_count=image_count)

    batch = _build_batch(states)

    # Burn first batch, first frame has some AO artifacts.
    # TODO(maciekcc): investigate and fix on renderer side.
    _ = renderer.render_batch(batch)

    result = renderer.render_batch(batch)

    golden_dir = _package_relative_path('assets/test_images')
    generate_images = bool(os.getenv('GENERATE_IMAGES'))

    for camera in config.camera_names:
        for suffix in ['', '_depth', '_normals', '_segmentation']:
            key = camera + suffix
            assert key in result.keys()
            assert len(result[key]) == image_count

            for i in range(image_count):
                image = result[key][i]
                if suffix == '_depth':
                    assert image.shape == (config.image_width, config.image_height)
                    assert image.dtype == np.float32
                    image = render_depth(image)
                elif suffix == '_normals':
                    assert image.shape == (config.image_width, config.image_height, 3)
                    assert image.dtype == np.float32
                    image = render_normals(image)
                elif suffix == '_segmentation':
                    assert image.shape == (config.image_width, config.image_height)
                    assert image.dtype == np.uint8
                    image = render_segmentation(image)
                else:
                    assert image.shape == (config.image_width, config.image_height, 3)
                    assert image.dtype == np.uint8

                name = _generate_test_image_name('remote_renderer', key, i)
                imageio.imwrite(os.path.join('/tmp', f'{name}_new.png'), image)

                golden_path = os.path.join(golden_dir, f'{name}.png')
                if not generate_images or (generate_images and os.path.exists(golden_path)):
                    golden_image = imageio.imread(golden_path)
                    imageio.imwrite(os.path.join('/tmp', f'{name}_old.png'), golden_image)

                    diff = np.abs(image.astype(float) - golden_image.astype(float))
                    imageio.imwrite(os.path.join('/tmp', f'{name}_diff.png'), diff.astype(np.uint8))

                    error = np.mean(diff)
                    error_90 = np.percentile(diff, 90)

                    if not generate_images:
                        assert error < 5.0, f'Image different than golden: {name}, error: {error}'
                        assert error_90 < 15.0, (f'Image different than golden: {name}, error_90: '
                                                 f'{error_90}')
                    else:
                        print(f'Image different than golden: {name}, error: {error}')

    renderer.shutdown()
