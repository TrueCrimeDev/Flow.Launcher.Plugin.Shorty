"""Preset storage for Shorty AI system prompts."""
import os


def _settings_dir() -> str:
    base = os.environ.get("APPDATA") or os.path.expanduser("~")
    return os.path.join(base, "FlowLauncher", "Settings", "Plugins", "Shorty AI")


def path() -> str:
    """Absolute path to presets.json. Does not create the file or dir."""
    return os.path.join(_settings_dir(), "presets.json")
