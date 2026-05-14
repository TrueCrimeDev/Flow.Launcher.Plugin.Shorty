"""Shorty AI — Flow Launcher AI assistant plugin."""
import json
import os
import sys

parent_folder_path = os.path.abspath(os.path.dirname(__file__))
sys.path.append(parent_folder_path)
sys.path.append(os.path.join(parent_folder_path, "lib"))
sys.path.append(os.path.join(parent_folder_path, "plugin"))

from pyflowlauncher import Plugin, Result, send_results

import presets

ICON = "Images\\icon.png"

plugin = Plugin()


def _settings_path() -> str:
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    return os.path.join(
        base, "FlowLauncher", "Settings", "Plugins", "Shorty AI", "Settings.json"
    )


def _load_settings() -> dict:
    """Read Flow's plugin settings file. Returns {} if missing/corrupt."""
    try:
        with open(_settings_path(), "r", encoding="utf-8") as f:
            return json.load(f) or {}
    except (OSError, json.JSONDecodeError):
        return {}


def _resolved_settings() -> dict:
    """Settings with defaults applied so callers never see None."""
    s = _load_settings()
    return {
        "api_key":        (s.get("api_key") or "").strip(),
        "base_url":       (s.get("base_url") or "https://api.openai.com/v1").rstrip("/"),
        "default_model":  (s.get("default_model") or "gpt-4o-mini").strip(),
        "default_preset": (s.get("default_preset") or "default").strip(),
        "request_timeout": s.get("request_timeout") or "60",
    }


def _admin_row_presets() -> Result:
    return Result(
        Title="ai :presets",
        SubTitle=f"Open {presets.path()}",
        IcoPath=ICON,
        JsonRPCAction={"method": "open_presets_file", "parameters": []},
    )


@plugin.on_method
def query(q: str):
    q = (q or "").strip()
    s = _resolved_settings()

    if q.startswith(":"):
        cmd_token = q[1:].split(maxsplit=1)[0].lower() if q[1:].strip() else ""
        if cmd_token == "presets":
            return send_results([_admin_row_presets()])
        return send_results([
            Result(
                Title=f"Unknown admin command: {q}",
                SubTitle="Try :presets",
                IcoPath=ICON,
            )
        ])

    preset_name, prompt = presets.match(q, default=s["default_preset"])

    if not prompt:
        return send_results([
            Result(
                Title=f"Ask [{preset_name}]: …",
                SubTitle=f"Type a prompt then press Enter (model: {s['default_model']})",
                IcoPath=ICON,
            ),
            _admin_row_presets(),
        ])

    return send_results([
        Result(
            Title=f"Ask [{preset_name}]: {prompt}",
            SubTitle=f"Press Enter to send to {s['default_model']}",
            IcoPath=ICON,
            JsonRPCAction={"method": "ask", "parameters": [preset_name, prompt]},
        ),
        _admin_row_presets(),
    ])


if __name__ == "__main__":
    plugin.run()
