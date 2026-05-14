"""Shared pytest fixtures for Shorty AI."""
import os
import sys

import pytest

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if ROOT not in sys.path:
    sys.path.insert(0, ROOT)


@pytest.fixture
def tmp_appdata(tmp_path, monkeypatch):
    """Redirect APPDATA so file-system writes land in tmp_path."""
    monkeypatch.setenv("APPDATA", str(tmp_path))
    return tmp_path
