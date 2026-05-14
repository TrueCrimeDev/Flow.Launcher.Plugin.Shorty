"""Tests for main.query()."""
import json
import os

import pytest


@pytest.fixture
def settings_file(tmp_appdata):
    """Seed Settings.json so main.query can read it."""
    plugin_dir = os.path.join(
        str(tmp_appdata), "FlowLauncher", "Settings", "Plugins", "Shorty"
    )
    os.makedirs(plugin_dir, exist_ok=True)
    path = os.path.join(plugin_dir, "Settings.json")
    with open(path, "w", encoding="utf-8") as f:
        json.dump({
            "api_key": "sk-test",
            "base_url": "https://api.openai.com/v1",
            "default_model": "gpt-4o-mini",
            "default_preset": "default",
            "request_timeout": "60",
        }, f)
    return path


@pytest.fixture
def main_module(settings_file):
    """Import main fresh per test so module-level state is clean."""
    import importlib
    import main
    importlib.reload(main)
    return main


def _result_dicts(payload):
    """send_results returns {'result': [rows], ...}. Pull out the rows."""
    parsed = json.loads(payload) if isinstance(payload, str) else payload
    return parsed.get("result", parsed)


def test_query_with_prompt_returns_sentinel_then_admin(main_module):
    payload = main_module.query("hello world")
    rows = _result_dicts(payload)
    assert len(rows) == 2
    assert rows[0]["Title"].startswith("Ask [default]: hello world")
    assert rows[0]["JsonRPCAction"]["method"] == "ask"
    assert rows[0]["JsonRPCAction"]["parameters"] == ["default", "hello world"]
    assert rows[1]["Title"] == "hey :presets"


def test_query_with_preset_token_routes(main_module):
    payload = main_module.query("code regex for emails")
    rows = _result_dicts(payload)
    assert rows[0]["Title"].startswith("Ask [code]: regex for emails")
    assert rows[0]["JsonRPCAction"]["parameters"] == ["code", "regex for emails"]


def test_query_empty_returns_disabled_sentinel_plus_admin(main_module):
    payload = main_module.query("")
    rows = _result_dicts(payload)
    assert len(rows) == 2
    assert "…" in rows[0]["Title"]
    assert rows[0].get("JsonRPCAction") is None


def test_query_admin_presets_returns_only_that_row(main_module):
    payload = main_module.query(":presets")
    rows = _result_dicts(payload)
    assert len(rows) == 1
    assert rows[0]["Title"] == "hey :presets"
    assert rows[0]["JsonRPCAction"]["method"] == "open_presets_file"


def test_query_unknown_admin_command_returns_help_row(main_module):
    payload = main_module.query(":nope")
    rows = _result_dicts(payload)
    assert len(rows) == 1
    assert "Unknown admin command" in rows[0]["Title"]


def test_ask_spawns_popup_subprocess(main_module, monkeypatch, tmp_appdata):
    captured = {}

    def fake_popen(cmd, **kwargs):
        captured["cmd"] = cmd
        captured["kwargs"] = kwargs
        class _Fake:
            pid = 12345
        return _Fake()

    monkeypatch.setattr("subprocess.Popen", fake_popen)
    main_module.ask("code", "regex for emails")

    assert captured["cmd"][1].endswith("popup.py")
    assert captured["cmd"][2] == "code"
    assert captured["cmd"][3] == "regex for emails"
    assert captured["cmd"][4].endswith("Settings.json")
    flags = captured["kwargs"].get("creationflags", 0)
    if os.name == "nt":
        assert flags != 0


def test_open_presets_file_creates_then_opens(main_module, monkeypatch, tmp_appdata):
    opened = {}
    monkeypatch.setattr("os.startfile", lambda p: opened.setdefault("path", p), raising=False)
    monkeypatch.setattr(
        "subprocess.Popen",
        lambda cmd, **kw: opened.setdefault("path", cmd[-1]),
    )

    main_module.open_presets_file()

    import presets
    assert opened["path"] == presets.path()
    assert os.path.isfile(presets.path())
