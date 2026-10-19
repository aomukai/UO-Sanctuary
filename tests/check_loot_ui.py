"""Actual ClassicUO combined corpse grid, five-tile reach, and bladed harvesting."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def exercise(window, click, capture):
    serverlog = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    statefile = Path(os.environ["SANCTUARY_TEST_UI_STATE"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    def command(name):
        offset = serverlog.stat().st_size
        os.write(console, ("loot-ui-" + name + "\n").encode())
        until = time.monotonic() + 5
        while time.monotonic() < until:
            output = serverlog.read_text(errors="replace")[offset:]
            if f"loot-ui-{name}: failed" in output:
                raise AssertionError(output[-2000:])
            if f"loot-ui-{name}: passed" in output:
                return json.loads(statefile.read_text())
            time.sleep(0.05)
        raise AssertionError(f"Missing loot-ui-{name}; inspect {serverlog}")
    state = command("setup")
    time.sleep(1)
    def body_point(entity):
        a, b = state["player"], state[entity]
        return (310 + 22 * (b["x"] - a["x"] - b["y"] + a["y"]),
                250 + 22 * (b["x"] - a["x"] + b["y"] - a["y"]) - 4 * (b["z"] - a["z"]))
    capture("loot-bodies-full.png")
    click(*body_point("second"))
    command("opened")
    time.sleep(0.7)
    capture("loot-nearby-grid.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "145", "180"], check=True)
    time.sleep(1.2)
    capture("loot-item-tooltip.png")
    # One click takes each whole stack; the remaining icon reflows into cell one.
    click(145, 180)
    command("first-taken")
    time.sleep(0.6)
    click(145, 180)
    command("both-taken")
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
    time.sleep(0.7)
    capture("loot-empty-grey-bodies.png")
    command("blade")
    time.sleep(0.2)
    x, y = body_point("first")
    click(x - 1, y - 20)  # The chicken corpse sprite lies above its tile's ground origin.
    capture("loot-bladed-target.png")
    state = command("harvested")
    time.sleep(0.7)
    capture("loot-harvest-materials.png")
    # Chicken harvesting produces meat and feathers (both whole-stack pickups).
    click(145, 180)
    time.sleep(0.6)
    click(145, 180)
    time.sleep(0.6)
    command("harvest-looted")
    capture("loot-harvested-body-removed.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "110", "110", "click", "3"], check=True)
    time.sleep(0.3)
    command("closed")
    command("final")
    print("Actual client: five-tile combined grid, manual generated-gold and whole-stack transfer, grey empty usable bodies, bladed harvesting, delete only after final harvested item, and grid close passed.", flush=True)
