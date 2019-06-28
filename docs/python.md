# Python Integration

Using ORRB from python is quite simple. Start with importing the package:

``` python
import orrb
```

Then you need to create the actual ORRB renderer. The renderer will read the config files you need
to provide, and will start one or a number of render server instances that process your render requests.
This snippet shows how to prepare a `orrb.RemoteRendererConfig`:

``` python
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
```

Please note that the config file paths need to be either absolute or relative to the `asset_basedir`.
Short description of fields:

- `camera_names` - a list of strings with camera names that will be producing images (the render response will
contain a `numpy` `array` with a batch of images for each used camera.
- `image_width`, `image_height` - rendered image output size.
- `renderer_version` - id used to pick the appropriate renderer binary version from your `ORRB_BINARIES_DIR`.
- `model_xml_path` - path to the MuJoCo XML scene file.
- `model_mapping_path` - path to the mapping file, the mapping file contains qpos indexes for joints.
- `renderer_config_path` - path to the renderer config with all the renderer component properties.
- `asset_basedir` - base dir for all other relative paths.
- `render_depth` - should depth channel be produced.
- `render_normals` - should normals map be produced.
- `render_segmentation` - should segmentation map be produced.

The `servers_config` is a list that contains pairs of `[gpu_number, worker_port]`. The `orrb.utils.build_server_configs`
is a convenience method that prepares a list assigning equal number of workers on each gpu and consequent ports.
It also supports sharding that might be useful in a MPI setup.

Finally a named `orrb.RemoteRenderer` is created. All there is to be done is to start it - when the actual render
server instances will be brought up.

``` python
renderer.start()
```

The renderer has two modes of operation, you can synchronously request rendering with:

``` python
result = renderer.render_batch(batch)
```

or queue it for asynchronous rendering and then get the result back from a queue:

``` python
result_queue = queue.Queue()

renderer.render_batch_async(batch, queue)

result = queue.get()
queue.task_done()
```

The request `batch` is a dictionary that should contain at least two keys: `qpos` a numpy array with the joint states,
and `seed` the initial batch random number generator seed. You can alternatively pass `seeds` a numpy array with a
individual seed for each `qpos` state.

The response is a dictionary containing the request values, and all the produced image, depth, normal, segmentation and
auxiliary tensors.

In order to stop and clean up the renderer run:

``` python
renderer.shutdown()
```

which will close the render server instances and block until done.

