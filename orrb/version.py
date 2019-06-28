__all__ = ['__version__']


def get_renderer_version():
    return '20190514'


def get_version():
    """ Returns the version as a human-format string. """
    # version format:
    # ('major', 'minor', 'patch')
    version_info = (0, 1, 12)

    return '%d.%d.%d' % version_info

__version__ = get_version()
