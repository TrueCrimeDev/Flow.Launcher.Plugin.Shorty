"""Tests for presets.py."""
import json
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


def test_load_seeds_defaults_when_file_missing(tmp_appdata):
    data = presets.load()
    assert "default" in data
    assert "short" in data
    assert "code" in data
    assert "translate" in data
    assert os.path.isfile(presets.path())


def test_load_reads_existing_file(tmp_appdata):
    os.makedirs(presets._settings_dir(), exist_ok=True)
    custom = {"pirate": {"system_prompt": "Arr."}}
    with open(presets.path(), "w", encoding="utf-8") as f:
        json.dump(custom, f)
    data = presets.load()
    assert data == custom


def test_load_falls_back_when_file_corrupt(tmp_appdata):
    os.makedirs(presets._settings_dir(), exist_ok=True)
    with open(presets.path(), "w", encoding="utf-8") as f:
        f.write("not valid json {")
    data = presets.load()
    assert "default" in data


def test_load_falls_back_when_file_is_empty_object(tmp_appdata):
    os.makedirs(presets._settings_dir(), exist_ok=True)
    with open(presets.path(), "w", encoding="utf-8") as f:
        f.write("{}")
    data = presets.load()
    assert "default" in data
