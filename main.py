"""Shorty AI — Flow Launcher AI assistant plugin."""
import os
import sys

parent_folder_path = os.path.abspath(os.path.dirname(__file__))
sys.path.append(parent_folder_path)
sys.path.append(os.path.join(parent_folder_path, "lib"))
sys.path.append(os.path.join(parent_folder_path, "plugin"))

from pyflowlauncher import Plugin, Result, send_results

ICON = "Images\\icon.png"

plugin = Plugin()


@plugin.on_method
def query(q: str):
    return send_results([
        Result(
            Title="Shorty AI",
            SubTitle="Plugin loaded. Type a prompt to get started.",
            IcoPath=ICON,
        )
    ])


if __name__ == "__main__":
    plugin.run()
