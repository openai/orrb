**Status:** Archive (code is provided as-is, no updates expected)

# ORRB
OpenAI Remote Rendering Backend

![OpenAI Remote Rendering Backend](/docs/orrb.png?raw=true "Orrb")

ORRB is a high performance rendering tool that can be used as a source of synthetic image data in training of ML models.

## Paper
More technical details are available in [ORRB Technical Report](https://arxiv.org/abs/1906.11633). Please cite this as:
```
@inproceedings{orrb2019,
  author = {{Maciek Chociej}, {Peter Welinder}, {Lilian Weng}},
  title = {ORRB: OpenAI Remote Rendering Backend},
  year = {2019},
  month = june,
  booktitle = {eprint arXiv},
  eprint = {arXiv:1906.11633},
  url = {https://arxiv.org/abs/1906.11633}
}
```

## License
This project is released under the MIT License. Please review the [License file](LICENSE.md) for more details.

# Installation

This code runs on Mac OS X and Linux with Python 3.6.5.  The Linux version requires a running X11 server with OpenGL support.

First, install [`pyenv`](https://github.com/pyenv/pyenv#installation).

Then prepare a clean python environment & activate it:

```
pyenv virtualenv 3.6.5 orrb
pyenv activate orrb
```

Clone the repository:

```
git clone https://github.com/openai/orrb.git
cd orrb
```

Install prerequisites:

```
pip install -r requirements.txt
```

Install pre-built binaries:

```
cd bin
./install-prebuilt.sh
cd ..
```

You should see something similar to:

```
  Execute or add this to your .bashrc/.bashprofile.
  export ORRB_BINARIES_DIR="/Users/mac/code/orrb/bin"
```

Add the required environment variable:

```
export ORRB_BINARIES_DIR="/Users/mac/code/orrb/bin"
```

Finally install the python package (in the root orrb directory):

```
pip install -e .
```

# Demos

## Interactive

This demo opens the renderer in interactive mode. You can cycle through the available cameras and manually tweak and run the configured randomizers.

```
python bin/demo.py interactive
```

![OpenAI Remote Rendering Backend](/docs/interactive.png?raw=true "Orrb")

## Batch

This demo will render batches of images and output them in a simple OpenCV window.

```
python bin/demo.py batch
```

## Batch (with config updates)

This demo will render batches of images updating the renderer config once in a while.

```
python bin/demo.py batch-update
```

## Gym

This demo will open the gym environment in interactive renderer mode. The `asset-basedir` needs to be passed so that the renderer knows where to look for the `.stl` files (unfortunately the MuJoCo XML loses that information).

```
OPENAI_GYM_PATH=$(python -c "import gym; print(gym.__path__[0])")
python bin/demo.py gymenv FetchSlide-v1 --asset-basedir=$OPENAI_GYM_PATH/envs/robotics/assets/fetch/
```

## Benchmark

This script spawns multiple render servers and executes batch render requests as fast as possible.

For example, in order to run on GPUs 0 through 3 with 2 workers per gpu (8 render servers in total) execute:

```
python bin/benchmark.py --num-gpus=4 --workers-per-gpu=2
```

This demo supports MPI and will distribute the workload / render servers between MPI processes, e.g.:

```
mpirun -n 8 bin/benchmark.py --num-gpus=8 --workers-per-gpu=4
```

will spawn 8 processes that talk to 32 render servers running across 8 GPUs.

## Keras

We have provided a sample application that uses ORRB and Keras to train a simple vision predictor. It takes the example environment (with the hand and the cube), and a sample batch of states. The training set is constructed
by augmenting this example batch with: randomized block rotation, perturbed block position and perturbed robot joint positions. The unaugmented sample batch (with rendering randomizer seed constant at 0) is used as the test set.
We use a simple convolutional network with the Adam optimizer to predict the screen position of the block. To run the demo execute:

```
python bin/demo_keras.py
```

# Linux

In order to run this on Linux, you need a X11 display. In order to run on different GPUs configure a screen per gpu (internally Orrb uses the `:ORRB_DISPLAY.GPU_NUM` notation to assign different GPUs to different render servers).
Use the `ORRB_DISPLAY` environment variable to specify which display to use (default is `:0`). We use the NVidia virtual frame buffers and None display device mode to run headless in datacenters, see [Remote Visualization on Tesla GPUs](https://www.nvidia.com/content/dam/en-zz/Solutions/Data-Center/tesla-product-literature/remote-viz-tesla-gpus.pdf) for more details.

# Building the Unity binaries

The `unity` directory contains the Standalone Renderer project. In `Build` directory you will find a script that builds the binaries:

```
cd unity/Builds
./build.sh 20181009
```

The parameter is the version id, that can be used to distinguish between builds in the client python code. Note that you cannot run the Unity Editor while building.

The script requires that the `UNITY_CMD` environment variable is set, you will need to point it to where Unity is installed, e.g.:

```
export UNITY_CMD="/Applications/Unity/Unity.app/Contents/MacOS/Unity"
```

# Useful Environment Variables

- `ORRB_DETACHED=1`: A flag that makes the job wait for you to start the server manually (in commandline or from the Unity Editor).
- `ORRB_MINIMAL=<int>`: A flag that defines the maximum number of local renderers running at the same time.
- `ORRB_BINARIES_DIR=<path>` : A flag that determines where the renderer binaries are located.
- `ORRB_DISPLAY=<int>` : A flag that defines which X11 display should be used for the render servers.
- `UNITY_CMD=<path>` : Used by the build script, points to the Unity Editor binary.

# External code

- GRPC - [grpc.io](https://grpc.io/)
- Protobuf - [developers.google.com/protocol-buffers](https://developers.google.com/protocol-buffers/)
- Stl Importer / Exporter - [git](https://github.com/karl-/pb_Stl)
- MIConvexHull - [git](https://designengrlab.github.io/MIConvexHull/)
- FBX Exporter - [git](https://github.com/KellanHiggins/UnityFBXExporter)
- PostProcessingV2 - [git](https://github.com/Unity-Technologies/PostProcessing)
