"""Tests for presets.py."""
import json
import os

import presets


def test_settings_dir_uses_appdata(tmp_appdata):
    expected = os.path.join(
        str(tmp_appdata), "FlowLauncher", "Settings", "Plugins", "Shorty"
    )
    assert presets._settings_dir() == expected


def test_path_is_presets_json_in_settings_dir(tmp_appdata):
    p = presets.path()
    assert p.endswith(os.path.join("Shorty", "presets.json"))
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


def test_match_finds_named_preset(tmp_appdata):
    name, prompt = presets.match("code regex for emails")
    assert name == "code"
    assert prompt == "regex for emails"


def test_match_is_case_insensitive(tmp_appdata):
    name, prompt = presets.match("CODE foo")
    assert name == "code"
    assert prompt == "foo"


def test_match_falls_through_to_default(tmp_appdata):
    name, prompt = presets.match("regex for emails")
    assert name == "default"
    assert prompt == "regex for emails"


def test_match_empty_query_returns_default(tmp_appdata):
    name, prompt = presets.match("")
    assert name == "default"
    assert prompt == ""


def test_match_single_token_that_is_a_preset(tmp_appdata):
    name, prompt = presets.match("short")
    assert name == "short"
    assert prompt == ""


def test_match_uses_caller_default(tmp_appdata):
    name, _ = presets.match("hello", default="short")
    assert name == "short"
