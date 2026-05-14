"""Streaming popup window for Shorty AI prompts.

Pure helpers live at module scope so tests can import them without
triggering Tk window creation.
"""
import json
import os
import time


def parse_chunk(line: str) -> str | None:
    """Extract content from one SSE line. Returns None for terminator/empty/malformed."""
    if not line:
        return None
    if line.startswith("data: "):
        line = line[6:]
    if line.strip() == "[DONE]":
        return None
    try:
        data = json.loads(line)
    except json.JSONDecodeError:
        return None
    try:
        return data["choices"][0]["delta"].get("content")
    except (KeyError, IndexError, TypeError):
        return None


def _settings_dir() -> str:
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    return os.path.join(base, "FlowLauncher", "Settings", "Plugins", "Shorty AI")


def _log_path() -> str:
    base = _settings_dir()
    os.makedirs(base, exist_ok=True)
    return os.path.join(base, "popup.log")


def _log_error(msg: str) -> None:
    """Append a line to popup.log; truncate first if file > 1MB."""
    path = _log_path()
    try:
        if os.path.isfile(path) and os.path.getsize(path) > 1_000_000:
            open(path, "w", encoding="utf-8").close()
        with open(path, "a", encoding="utf-8") as f:
            f.write(f"{time.strftime('%Y-%m-%d %H:%M:%S')}  {msg}\n")
    except OSError:
        pass


def _load_settings(path: str) -> dict:
    """Read Flow's Settings.json. Returns {} if missing or unreadable."""
    try:
        with open(path, "r", encoding="utf-8") as f:
            return json.load(f) or {}
    except (OSError, json.JSONDecodeError):
        return {}


def _load_preset(name: str) -> dict:
    """Read presets.json and return the named preset, or {} if missing."""
    presets_path = os.path.join(_settings_dir(), "presets.json")
    try:
        with open(presets_path, "r", encoding="utf-8") as f:
            data = json.load(f)
        return data.get(name, {})
    except (OSError, json.JSONDecodeError):
        return {}
