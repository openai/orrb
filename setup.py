#!/usr/bin/env python
import setuptools
from os.path import join
import os
from orrb.version import __version__


def read_requirements_file(filename):
    req_file_path = os.path.join(os.path.dirname(os.path.realpath(__file__)), filename)
    with open(req_file_path) as f:
        ret = [line.strip() for line in f]
    return ret


setuptools.setup(
    name='orrb',
    version=__version__,  # noqa
    description='OpenAI Remote Rendering Backend',
    url='https://github.com/openai/orrb',
    author='OpenAI Robotics Team',
    author_email='robotics@openai.com',
    packages=['orrb', 'orrb.protos'],
    package_data={'orrb.protos': ['RendererConfig.proto',
                                  'RenderService.proto',
                                  'Makefile']},
    install_requires=read_requirements_file('requirements.txt'),
)
