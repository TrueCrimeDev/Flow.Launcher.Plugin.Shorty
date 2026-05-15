"""Streaming popup window for Shorty prompts.

Pure helpers live at module scope so tests can import them without
triggering Tk window creation.
"""
import json
import os
import queue
import sys
import threading
import time

# Ensure plugin's bundled deps in lib/ are importable. Required because Flow's
# embeddable Python doesn't auto-add this directory, and main.py's sys.path
# tweaks don't carry over to the popup subprocess.
_PLUGIN_DIR = os.path.abspath(os.path.dirname(__file__))
sys.path.insert(0, os.path.join(_PLUGIN_DIR, "lib"))

import requests

# tkinter is imported lazily inside PopupApp so this module can be
# imported (for unit tests of pure helpers) on systems where tkinter
# isn't installed. Production target is Windows, where tkinter ships
# with Python by default.


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
    return os.path.join(base, "FlowLauncher", "Settings", "Plugins", "Shorty")


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


# Color palette from CLAUDE.md global design system.
BG = "#0f0f0f"
PANEL_BG = "#121212"
BORDER = "#303030"
SUBTLE = "#232323"
HOVER_BG = "#252525"
FG = "#ffffff"
DIM = "#a0a0a0"
ERROR = "#DC3545"
FONT_BODY = ("Cascadia Mono", 11)
FONT_HEADER = ("Segoe UI", 10)


class PopupApp:
    def __init__(self, preset_name: str, prompt: str, settings_path: str):
        import tkinter as tk
        self._tk = tk
        from tkinter import ttk
        self._ttk = ttk

        self.preset_name = preset_name
        self.prompt = prompt
        self.settings = _load_settings(settings_path)
        self.preset = _load_preset(preset_name)
        self.queue: "queue.Queue" = queue.Queue()

        self.root = tk.Tk()
        self.root.title(f"Shorty — {preset_name}")
        self.root.geometry("640x440")
        self.root.configure(bg=BG)
        self.root.bind("<Escape>", lambda e: self.root.destroy())
        self._build_ui()

    def _build_ui(self) -> None:
        tk = self._tk
        ttk = self._ttk
        header = tk.Label(
            self.root,
            text=self.prompt or "(no prompt)",
            bg=BG, fg=DIM,
            font=FONT_HEADER,
            anchor="w", justify="left",
            wraplength=600, padx=12, pady=10,
        )
        header.pack(fill="x")

        body = tk.Frame(self.root, bg=BG)
        body.pack(fill="both", expand=True, padx=12, pady=(0, 8))

        self.text = tk.Text(
            body,
            bg=PANEL_BG, fg=FG,
            insertbackground=FG,
            font=FONT_BODY,
            wrap="word", relief="flat",
            highlightthickness=1,
            highlightbackground=BORDER, highlightcolor=BORDER,
            padx=10, pady=8,
        )
        scroll = ttk.Scrollbar(body, command=self.text.yview)
        self.text.configure(yscrollcommand=scroll.set)
        self.text.tag_configure("error", foreground=ERROR)
        self.text.pack(side="left", fill="both", expand=True)
        scroll.pack(side="right", fill="y")

        footer = tk.Frame(self.root, bg=BG)
        footer.pack(fill="x", padx=12, pady=(0, 12))

        self.copy_btn = self._mk_button(footer, "Copy", self._copy)
        self.copy_btn.configure(state="disabled")
        self.regen_btn = self._mk_button(footer, "Regenerate", self._regenerate)
        self.regen_btn.configure(state="disabled")
        close_btn = self._mk_button(footer, "Close", self.root.destroy)

        close_btn.pack(side="right")
        self.regen_btn.pack(side="right", padx=(0, 8))
        self.copy_btn.pack(side="right", padx=(0, 8))

    def _mk_button(self, parent, label: str, cmd):
        return self._tk.Button(
            parent, text=label, command=cmd,
            bg=SUBTLE, fg=FG,
            activebackground=HOVER_BG, activeforeground=FG,
            disabledforeground="#4a4a4a",
            relief="flat", padx=14, pady=6, borderwidth=0,
        )

    def run(self) -> None:
        if not (self.settings.get("api_key") or "").strip():
            self._show_error(
                "No API key configured. Open Flow Launcher → Settings → "
                "Plugins → Shorty and paste your key."
            )
            self.root.mainloop()
            return
        self._start_stream()
        self.root.after(30, self._drain)
        self.root.mainloop()

    def _show_error(self, msg: str) -> None:
        self.text.insert("end", msg, ("error",))

    def _start_stream(self) -> None:
        self.text.configure(state="normal")
        self.text.delete("1.0", "end")
        self.copy_btn.configure(state="disabled")
        self.regen_btn.configure(state="disabled")
        self.queue = queue.Queue()
        threading.Thread(target=self._stream, daemon=True).start()

    def _stream(self) -> None:
        base_url = (self.settings.get("base_url") or "https://api.openai.com/v1").rstrip("/")
        api_key = self.settings.get("api_key", "")
        model = self.preset.get("model") or self.settings.get("default_model") or "gpt-5.5-instant"
        try:
            timeout = float(self.settings.get("request_timeout") or 60)
        except (TypeError, ValueError):
            timeout = 60.0

        messages = []
        system = self.preset.get("system_prompt", "")
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": self.prompt})

        try:
            r = requests.post(
                f"{base_url}/chat/completions",
                headers={
                    "Authorization": f"Bearer {api_key}",
                    "Content-Type": "application/json",
                },
                json={"model": model, "stream": True, "messages": messages},
                stream=True,
                timeout=timeout,
            )
            if r.status_code >= 400:
                body = r.text
                try:
                    body = r.json().get("error", {}).get("message", body) or body
                except (ValueError, AttributeError):
                    pass
                self.queue.put(("error", f"Error {r.status_code}: {body}"))
                return
            for raw in r.iter_lines(decode_unicode=True):
                if raw is None:
                    continue
                content = parse_chunk(raw)
                if content:
                    self.queue.put(("token", content))
        except requests.exceptions.RequestException as e:
            _log_error(f"transport: {type(e).__name__}: {e}")
            self.queue.put(("error", f"Error: {type(e).__name__}: {e}"))
        except Exception as e:
            _log_error(f"unexpected: {type(e).__name__}: {e}")
            self.queue.put(("error", f"Error: {type(e).__name__}: {e}"))
        finally:
            self.queue.put(("done", None))

    def _drain(self) -> None:
        done = False
        try:
            while True:
                kind, payload = self.queue.get_nowait()
                if kind == "token":
                    self.text.insert("end", payload)
                    self.text.see("end")
                elif kind == "error":
                    self.text.insert("end", payload, ("error",))
                    self.text.see("end")
                elif kind == "done":
                    done = True
        except queue.Empty:
            pass
        if done:
            self.copy_btn.configure(state="normal")
            self.regen_btn.configure(state="normal")
            return
        self.root.after(30, self._drain)

    def _copy(self) -> None:
        text = self.text.get("1.0", "end").rstrip("\n")
        self.root.clipboard_clear()
        self.root.clipboard_append(text)
        original = self.copy_btn["text"]
        self.copy_btn.configure(text="Copied!")
        self.root.after(1000, lambda: self.copy_btn.configure(text=original))

    def _regenerate(self) -> None:
        self._start_stream()
        self.root.after(30, self._drain)


def main() -> None:
    if len(sys.argv) < 4:
        sys.exit("Usage: popup.py <preset_name> <prompt> <settings_json_path>")
    PopupApp(sys.argv[1], sys.argv[2], sys.argv[3]).run()


if __name__ == "__main__":
    main()
