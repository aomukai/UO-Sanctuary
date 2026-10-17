"""Spend all four class attributes through the actual expanded ClassicUO status window."""
import os
from pathlib import Path
import subprocess
import time

ROOT = Path(__file__).resolve().parents[1]

def exercise(window, click, capture):
    log = ROOT / ".local/logs/britain-baseline-check.log"
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    def command(name):
        offset = log.stat().st_size
        os.write(console, ("stat-ui-" + name + "\n").encode())
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            output = log.read_text(errors="replace")[offset:]
            if f"stat-ui-{name}: failed" in output:
                raise AssertionError(output[-2500:])
            if f"stat-ui-{name}: passed" in output:
                return
            time.sleep(0.05)
        raise AssertionError(f"Missing stat-ui-{name}")
    command("setup")
    time.sleep(1)
    capture("level-up-notification.png")
    click(190, 114)  # New Talents notification button.
    command("talents")
    capture("level-up-talents.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "500", "200", "click", "3"], check=True)
    time.sleep(0.4)
    click(60, 114)  # New Stats notification button opens expanded status.
    time.sleep(0.6)
    capture("stats-unspent.png")
    # The expanded status is constrained to the viewport; save its actual origin.
    subprocess.run(["xdotool", "key", "--window", window, "ctrl+alt+shift+s"], check=True)
    time.sleep(0.5)
    import xml.etree.ElementTree as ET
    profile_root = Path(os.environ["SANCTUARY_TEST_PROFILE"])
    from pprint import pformat
    gumps = [g for f in profile_root.rglob("gumps.xml") for g in ET.parse(f).findall("gump")]
    status = next((g for g in gumps if g.attrib.get("type") == "12"), None)
    assert status is not None, "No expanded status in saved desktop: " + pformat([g.attrib for g in gumps])
    x, y = int(status.attrib["x"]), int(status.attrib["y"])
    for row in range(4):
        click(x + 90, y + 82 + 28 * row)
    command("four")
    click(x + 90, y + 166)
    for _ in range(3):
        click(x + 90, y + 82)
    time.sleep(0.6)
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "50"], check=True)
    time.sleep(0.5)
    capture("stats-spent.png")
    command("final")
    click(190, 114)
    command("talents")
    click(460, 340)  # Learn the selected Raise Skeleton rank.
    time.sleep(0.7)
    command("spent")
    subprocess.run(["xdotool", "mousemove", "--window", window, "500", "200", "click", "3"], check=True)
    time.sleep(0.6)
    capture("level-up-points-spent.png")
    print("Actual client: level-up buttons opened stats/talents, spent all attribute and talent points, updated resource pools, and rejected overspending.", flush=True)
