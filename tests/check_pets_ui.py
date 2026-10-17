"""Learn Raise Skeleton, bind both mouse buttons, summon, inspect stats and cast by hotkey."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def command(name):
    log = ROOT / ".local/logs/britain-baseline-check.log"
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    offset = log.stat().st_size
    os.write(console, ("pet-ui-" + name + "\n").encode())
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        output = log.read_text(errors="replace")[offset:]
        if f"pet-ui-{name}: failed" in output:
            raise AssertionError(output[-2500:])
        if f"pet-ui-{name}: passed" in output:
            return json.loads(Path(os.environ["SANCTUARY_TEST_UI_STATE"]).read_text())
        time.sleep(0.05)
    raise AssertionError(f"No result for pet-ui-{name}")

def exercise(window, click, capture):
    def mouse(point, button=3):
        subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, point)], check=True)
        time.sleep(0.25)
        subprocess.run(["xdotool", "click", str(button)], check=True)
        time.sleep(1.2)
    def point(state, name):
        a, b = state["player"], state[name]
        return (310 + 22 * (b["x"] - a["x"] - b["y"] + a["y"]),
                250 + 22 * (b["x"] - a["x"] + b["y"] - a["y"]) - 4 * (b["z"] - a["z"]))
    state = command("setup")
    time.sleep(1)
    capture("pets-earned-skill-point.png")
    # A press must allocate once, without depending on a later mouse-up/focus event.
    subprocess.run(["xdotool", "mousemove", "--window", window, "500", "342"], check=True)
    time.sleep(.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(.3)
    command("learned")
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    click(42, 433)  # Mouse skills.
    time.sleep(0.5)
    capture("pets-mouse-bindings.png")
    click(405, 267)  # Raise Skeleton -> Right.
    click(315, 231)  # Teeth -> Left.
    state = command("bound")
    time.sleep(0.7)
    mouse(point(state, "hovered"))
    capture("pets-hovered-corpse-raised.png")
    command("hovered")
    command("fight-setup")
    time.sleep(1.8)
    capture("pets-guarding-combat.png")
    command("fought")
    command("class")
    time.sleep(0.5)
    click(225, 433)
    time.sleep(0.5)
    capture("pets-combat-stats.png")
    command("pets")
    command("reset")
    subprocess.run(["xdotool", "mousemove", "--window", window, "570", "80", "key", "q"], check=True)
    time.sleep(1.2)
    capture("pets-hotkey-nearest-corpse.png")
    command("nearest")
    state = command("left-setup")
    time.sleep(0.6)
    mouse(point(state, "near"), 1)
    capture("pets-left-mouse-summon.png")
    command("left")
    command("relog-prepare")
    print("Actual client: earned skill learning, both mouse bindings, hovered/nearest corpse raising, hotkey macro, left/right casting and pet stats passed.", flush=True)
