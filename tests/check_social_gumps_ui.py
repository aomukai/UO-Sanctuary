"""Exercise the real Party and Guild windows against a disposable ModernUO world."""
import os
from pathlib import Path
import subprocess
import time

from PIL import ImageGrab


def exercise(window, click):
    log = Path(os.environ["SANCTUARY_TEST_LOG"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
    bounds = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
    wx, wy = int(bounds["X"]), int(bounds["Y"])

    def command(name):
        offset = log.stat().st_size
        os.write(console, (f"social-ui-{name}\n").encode())
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            output = log.read_text(errors="replace")[offset:]
            assert f"social-ui-{name}: failed" not in output, output
            if f"social-ui-{name}: passed" in output:
                return
            time.sleep(0.05)
        raise AssertionError(f"Missing social-ui-{name}")

    def screenshot(name, x, y, width, height):
        subprocess.run(["xdotool", "mousemove", "--window", window, "626", "12"], check=True)
        time.sleep(0.3)
        image = ImageGrab.grab(bbox=(wx + x, wy + y, wx + x + width, wy + y + height), xdisplay=os.environ["DISPLAY"])
        image.save(log.parent / f"{name}.png")

    def type_text(value):
        subprocess.run(["xdotool", "key", "--window", window, "ctrl+a"], check=True)
        subprocess.run(["xdotool", "type", "--window", window, value], check=True)

    click(343, 453)
    screenshot("party-empty", 60, 144, 520, 192)
    click(136, 294)  # Invite member.
    command("invite")
    command("party")  # The native roster update must refresh the already-open window.
    screenshot("party-members", 60, 144, 520, 226)
    click(183, 342)  # Draft a change to the permission.
    click(438, 342)  # Cancel.
    command("no-loot")
    click(343, 453)
    click(183, 325)  # Reopened and centered now that it has members.
    click(518, 325)  # Apply.
    command("loot")
    click(343, 453)
    click(470, 291)  # Disband.
    command("left")
    click(520, 278)  # Close the now-empty party window.

    command("fund")
    click(405, 453)
    screenshot("guild-create", 10, 27, 500, 300)
    click(232, 199)
    type_text("Lanternwatch")
    click(234, 228)
    type_text("LW")
    screenshot("guild-registration-filled", 10, 27, 500, 300)
    click(429, 258)
    command("created")

    click(405, 453)
    command("guild")
    screenshot("guild-overview", 10, 27, 600, 440)
    click(325, 81)  # Click the full Roster tab.
    command("roster")
    screenshot("guild-roster", 10, 27, 600, 440)
    click(478, 81)
    command("diplomacy")
    screenshot("guild-diplomacy", 10, 27, 600, 440)
    click(473, 216)
    command("search")
    screenshot("guild-search", 10, 27, 600, 440)
    click(95, 237)  # Show All Guilds.
    click(505, 411)  # OK.
    command("diplomacy")
    click(557, 47)  # Close.
    command("closed")
    print("Actual client: party invite, loot cancel and apply, disband, guild registration and guild navigation passed.", flush=True)
