"""Preset storage for Shorty AI system prompts."""
import json
import os

DEFAULTS = {
    "default":   {"system_prompt": "You are a concise, helpful assistant."},
    "short":     {"system_prompt": "Reply in one sentence. No preamble."},
    "code":      {"system_prompt": "Reply with code only, no commentary. Use markdown fences only if multiple files."},
    "translate": {"system_prompt": "Translate the user's input to English. Output the translation only."},
}


def _settings_dir() -> str:
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    return os.path.join(base, "FlowLauncher", "Settings", "Plugins", "Shorty AI")


def path() -> str:
    """Absolute path to presets.json. Does not create the file or dir."""
    return os.path.join(_settings_dir(), "presets.json")


def load() -> dict:
    """Load presets. Seeds defaults if file missing; falls back to defaults if corrupt."""
    p = path()
    os.makedirs(os.path.dirname(p), exist_ok=True)
    if not os.path.isfile(p):
        with open(p, "w", encoding="utf-8") as f:
            json.dump(DEFAULTS, f, indent=2)
        return dict(DEFAULTS)
    try:
        with open(p, "r", encoding="utf-8") as f:
            data = json.load(f)
    except (OSError, json.JSONDecodeError):
        return dict(DEFAULTS)
    if not isinstance(data, dict) or not data:
        return dict(DEFAULTS)
    return data


def match(query: str, default: str = "default") -> tuple[str, str]:
    """Return (preset_name, remainder).

    If the first whitespace-delimited token matches a preset name
    (case-insensitive), strip it and return the rest as the prompt.
    Otherwise return (default, query) unchanged.
    """
    q = (query or "").strip()
    if not q:
        return (default, "")
    parts = q.split(maxsplit=1)
    first = parts[0].lower()
    for name in load():
        if name.lower() == first:
            return (name, parts[1] if len(parts) > 1 else "")
    return (default, q)
