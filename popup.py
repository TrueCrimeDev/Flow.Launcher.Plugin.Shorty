"""Streaming popup window for Shorty AI prompts.

Pure helpers live at module scope so tests can import them without
triggering Tk window creation.
"""
import json


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
