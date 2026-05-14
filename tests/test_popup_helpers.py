"""Tests for popup helpers (log path, settings load, preset load)."""
import json
import os

import popup


def test_log_path_under_appdata(tmp_appdata):
    p = popup._log_path()
    assert p.endswith(os.path.join("Shorty", "popup.log"))
    assert str(tmp_appdata) in p
    assert os.path.isdir(os.path.dirname(p))  # _log_path creates parent


def test_log_error_appends_then_truncates_at_1mb(tmp_appdata):
    popup._log_error("first")
    p = popup._log_path()
    assert "first" in open(p, encoding="utf-8").read()

    # Pre-fill above the cap
    with open(p, "w", encoding="utf-8") as f:
        f.write("x" * 1_100_000)
    popup._log_error("after rotate")
    contents = open(p, encoding="utf-8").read()
    assert "after rotate" in contents
    assert "x" * 1000 not in contents  # rotated, not appended


def test_load_settings_returns_dict(tmp_appdata):
    settings_path = os.path.join(str(tmp_appdata), "Settings.json")
    with open(settings_path, "w", encoding="utf-8") as f:
        json.dump({"api_key": "sk-abc", "base_url": "https://x"}, f)
    s = popup._load_settings(settings_path)
    assert s["api_key"] == "sk-abc"


def test_load_settings_returns_empty_when_missing(tmp_appdata):
    s = popup._load_settings(os.path.join(str(tmp_appdata), "missing.json"))
    assert s == {}


def test_load_settings_returns_empty_when_corrupt(tmp_appdata):
    p = os.path.join(str(tmp_appdata), "bad.json")
    with open(p, "w", encoding="utf-8") as f:
        f.write("{")
    assert popup._load_settings(p) == {}


def test_load_preset_returns_named_preset(tmp_appdata):
    plugin_dir = os.path.join(
        str(tmp_appdata), "FlowLauncher", "Settings", "Plugins", "Shorty"
    )
    os.makedirs(plugin_dir, exist_ok=True)
    with open(os.path.join(plugin_dir, "presets.json"), "w", encoding="utf-8") as f:
        json.dump({"pirate": {"system_prompt": "Arr."}}, f)
    p = popup._load_preset("pirate")
    assert p == {"system_prompt": "Arr."}


def test_load_preset_returns_empty_when_unknown(tmp_appdata):
    plugin_dir = os.path.join(
        str(tmp_appdata), "FlowLauncher", "Settings", "Plugins", "Shorty"
    )
    os.makedirs(plugin_dir, exist_ok=True)
    with open(os.path.join(plugin_dir, "presets.json"), "w", encoding="utf-8") as f:
        json.dump({}, f)
    assert popup._load_preset("anything") == {}
