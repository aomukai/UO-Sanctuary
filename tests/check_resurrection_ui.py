"""Run as a ghost to the graveyard healer and accept resurrection in ClassicUO."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def exercise(window, click, capture):
    log = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    def command(name):
        offset = log.stat().st_size
        os.write(console, ("resurrection-ui-" + name + "\n").encode())
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            output = log.read_text(errors="replace")[offset:]
            if f"resurrection-ui-{name}: failed" in output:
                raise AssertionError(output[-2500:])
            if f"resurrection-ui-{name}: passed" in output:
                return json.loads(Path(os.environ["SANCTUARY_TEST_UI_STATE"]).read_text())
            time.sleep(0.05)
        raise AssertionError(f"No result for resurrection-ui-{name}")
    click(335, 367)  # Minimize paperdoll.
    state = command("setup")
    time.sleep(1.2)
    state = command("die")
    time.sleep(5)
    capture("resurrection-ghost.png")
    player, healer = state["player"], state["healer"]
    dx, dy = healer["x"] - player["x"], healer["y"] - player["y"]
    dz = healer["z"] - player["z"]
    point = (354 + 22 * (dx - dy), 250 + 22 * (dx + dy) - 4 * dz + 8)
    subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, point)], check=True)
    time.sleep(0.3)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    try:
        time.sleep(2.2)
    finally:
        subprocess.run(["xdotool", "mouseup", "1"], check=True)
    capture("resurrection-healer-offer.png")
    command("offered")
    click(180, 240)  # Native ResurrectGump at (100, 0), Continue button.
    time.sleep(0.5)
    capture("resurrection-full-life.png")
    command("final")
    time.sleep(3)
    state = command("recovery-setup")
    time.sleep(0.8)
    a, b = state["player"], state["corpse"]
    point = (310 + 22 * (b["x"] - a["x"] - b["y"] + a["y"]),
             250 + 22 * (b["x"] - a["x"] + b["y"] - a["y"]) - 4 * (b["z"] - a["z"]))
    click(point[0], point[1] + 12)
    capture("resurrection-corpse-recovered.png")
    command("recovered")
    if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1":
        from check_pets_ui import exercise as pets_exercise
        pets_exercise(window, click, capture)
    print("Actual client: held left-click moved the ghost to the healer; native prompt resurrected at full health and retained the corpse.", flush=True)
