"""Tests for presets.py."""
import os

import presets


def test_settings_dir_uses_appdata(tmp_appdata):
    expected = os.path.join(
        str(tmp_appdata), "FlowLauncher", "Settings", "Plugins", "Shorty AI"
    )
    assert presets._settings_dir() == expected


def test_path_is_presets_json_in_settings_dir(tmp_appdata):
    p = presets.path()
    assert p.endswith(os.path.join("Shorty AI", "presets.json"))
    assert str(tmp_appdata) in p
