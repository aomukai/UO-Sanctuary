"""Measure held dagger/halberd/IAS/FCR actions through the actual ClassicUO client."""
import json
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def exercise(window, click, capture, project):
    log = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    statefile = Path(os.environ["SANCTUARY_TEST_UI_STATE"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    def command(name):
        offset = log.stat().st_size
        os.write(console, ("timing-ui-" + name + "\n").encode())
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            output = log.read_text(errors="replace")[offset:]
            if f"timing-ui-{name}: failed" in output:
                raise AssertionError(output[-2000:])
            if f"timing-ui-{name}: passed" in output:
                return json.loads(statefile.read_text())
            time.sleep(0.02)
        raise AssertionError(f"Missing timing-ui-{name}")
    state = command("setup")
    time.sleep(1)
    for mode, button in (("dagger", 3), ("halberd", 3), ("ias", 3), ("fcr", 3)):
        state = command(mode)
        subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
        time.sleep(1)
        point = project(state, "enemy")
        point = (point[0], point[1] + 25)
        subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, point), "keydown", "Shift_L"], check=True)
        time.sleep(0.2)
        command("mark")
        started = time.monotonic()
        subprocess.run(["xdotool", "mousedown", str(button)], check=True)
        if mode == "dagger":
            time.sleep(0.07)
            command("windup")
        remainder = (2.08 if mode == "fcr" else 1.9) - (time.monotonic() - started)
        if remainder > 0:
            time.sleep(remainder)
        capture("timing-" + mode + "-held.png")
        command(mode + "-hits")
        command("stop")
        subprocess.run(["xdotool", "mouseup", str(button), "keyup", "Shift_L"], check=True)
    command("finish")
    print("Actual client: delayed first hit, dagger/halberd cadence, equipment IAS and equipment FCR passed.", flush=True)
