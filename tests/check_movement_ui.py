"""Hold movement towards enclosed ground: route searches must not freeze the client."""
import json
import os
from pathlib import Path
import re
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def exercise(window, click, capture):
    log = ROOT / ".local/logs/britain-baseline-check.log"
    client_log = ROOT / ".local/logs/creation-ui-client.log"
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    def command(name):
        offset = log.stat().st_size
        os.write(console, ("movement-ui-" + name + "\n").encode())
        deadline = time.monotonic() + 6
        while time.monotonic() < deadline:
            text = log.read_text(errors="replace")[offset:]
            if f"movement-ui-{name}: failed" in text:
                raise AssertionError(text[-2500:])
            if f"movement-ui-{name}: passed" in text:
                return json.loads(Path(os.environ["SANCTUARY_TEST_UI_STATE"]).read_text())
            time.sleep(0.05)
        raise AssertionError(f"Missing movement-ui-{name}")
    state = command("setup")
    time.sleep(1)
    a, b = state["player"], state["goal"]
    x = 310 + 22 * (b["x"] - a["x"] - b["y"] + a["y"])
    y = 250 + 22 * (b["x"] - a["x"] + b["y"] - a["y"]) - 4 * (b["z"] - a["z"])
    subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
    time.sleep(0.25)
    offset = client_log.stat().st_size
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(3)
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    time.sleep(1)
    text = client_log.read_text(errors="replace")[offset:]
    durations = [float(value) for value in re.findall(r"Movement path: ms=([\d.]+)", text)]
    assert durations, "No completed movement route: client may be stuck in pathfinding"
    print(f"Held movement: {len(durations)} routes; worst synchronous search {max(durations):.2f} ms", flush=True)
    assert max(durations) < 100, f"Movement path froze the client for {max(durations):.2f} ms"
    command("moved")
    state = command("open-setup")
    time.sleep(.5)
    def aim(state):
        a, b = state["player"], state["goal"]
        return 310 + 22 * (b["x"] - a["x"] - b["y"] + a["y"]), 250 + 22 * (b["x"] - a["x"] + b["y"] - a["y"])
    x, y = aim(state)
    subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
    time.sleep(.2)
    subprocess.run(["xdotool", "click", "1"], check=True)
    time.sleep(2)
    command("clicked")
    state = command("open-setup")
    time.sleep(.6)
    x, y = aim(state)
    subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
    time.sleep(.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(.35)
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    time.sleep(.6)
    command("hold-mark")
    time.sleep(1)
    command("held-stop")
    print("Short click completes its route; held movement stops on release.", flush=True)
    state = command("gear-setup")
    time.sleep(.7)
    x, y = aim(state)
    subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
    time.sleep(.2)
    subprocess.run(["xdotool", "click", "1"], check=True)
    time.sleep(2.8)
    state = command("gear-check")
    import statistics
    intervals = [b - a for a, b in zip(state["stepTimes"], state["stepTimes"][1:])]
    print(f"Actual client: heavy armour/shield penalty {state['penalty']}%; median accepted running step {statistics.median(intervals):.0f} ms.", flush=True)
    capture("movement-enclosed-target.png")
    command("final")
