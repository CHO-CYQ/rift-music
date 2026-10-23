"""Owned test directories inherit the workspace ACL on restricted Windows tokens."""
from contextlib import contextmanager
from pathlib import Path
import shutil
import tempfile
import uuid

@contextmanager
def TemporaryDirectory():
    root=Path(tempfile.gettempdir()).resolve()
    target=root/('rift-python-test-'+uuid.uuid4().hex)
    target.mkdir()  # Python 3.14's mode-700 temp ACL excludes restricted process tokens.
    try:yield str(target)
    finally:
        resolved=target.resolve()
        if resolved.parent!=root or not resolved.name.startswith('rift-python-test-'):raise ValueError('Unsafe test cleanup target')
        shutil.rmtree(resolved)
