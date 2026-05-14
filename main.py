"""Shorty — Flow Launcher AI assistant plugin."""
import json
import os
import subprocess
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
        base, "FlowLauncher", "Settings", "Plugins", "Shorty", "Settings.json"
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
        "default_model":  (s.get("default_model") or "gpt-5.5-instant").strip(),
        "default_preset": (s.get("default_preset") or "default").strip(),
        "request_timeout": s.get("request_timeout") or "60",
    }


def _action_keyword() -> str:
    """Read the keyword from plugin.json so the admin row label tracks it."""
    try:
        with open(os.path.join(parent_folder_path, "plugin.json"), "r", encoding="utf-8") as f:
            return (json.load(f).get("ActionKeyword") or "hey").strip() or "hey"
    except (OSError, json.JSONDecodeError):
        return "hey"


def _admin_row_presets() -> Result:
    return Result(
        Title=f"{_action_keyword()} :presets",
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


# Windows process creation flags. Defined as ints so the module imports
# cleanly on non-Windows dev machines where subprocess doesn't expose
# them as attrs.
DETACHED_PROCESS = 0x00000008
CREATE_NO_WINDOW = 0x08000000


@plugin.on_method
def ask(preset_name: str, prompt: str):
    """Spawn the popup subprocess and return immediately."""
    py = sys.executable
    pyw = py.replace("python.exe", "pythonw.exe")
    if not os.path.isfile(pyw):
        pyw = py
    popup_path = os.path.join(parent_folder_path, "popup.py")
    settings_json = _settings_path()

    kwargs = {"close_fds": True}
    if os.name == "nt":
        kwargs["creationflags"] = DETACHED_PROCESS | CREATE_NO_WINDOW

    subprocess.Popen(
        [pyw, popup_path, preset_name, prompt, settings_json],
        **kwargs,
    )


@plugin.on_method
def open_presets_file():
    """Open presets.json in the OS default editor (Windows: os.startfile)."""
    presets.load()  # ensure file exists before opening
    if os.name == "nt":
        os.startfile(presets.path())  # type: ignore[attr-defined]
    else:
        # Dev convenience for non-Windows. Production target is Windows.
        subprocess.Popen(["xdg-open", presets.path()])


if __name__ == "__main__":
    plugin.run()
