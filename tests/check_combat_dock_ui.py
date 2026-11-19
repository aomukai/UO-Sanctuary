"""Exercise the combined combat dock, window fitting, and avatar portrait in the SDL client."""
from collections import Counter
import json
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from PIL import ImageGrab


def exercise(window, click, profile):
    log = Path(os.environ["SANCTUARY_TEST_LOG"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])

    def command(name):
        offset = log.stat().st_size
        os.write(console, (f"dock-ui-{name}\n").encode())
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            output = log.read_text(errors="replace")[offset:]
            assert f"dock-ui-{name}: failed" not in output, output
            if f"dock-ui-{name}: passed" in output:
                return
            time.sleep(0.05)
        raise AssertionError(f"Missing dock-ui-{name}")

    def geometry():
        result = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
        bounds = dict(line.split("=", 1) for line in result.splitlines() if "=" in line)
        return tuple(int(bounds[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))

    def capture(name, crop=None):
        wx, wy, width, height = geometry()
        x, y, w, h = crop or (0, 0, width, height)
        image = ImageGrab.grab(bbox=(wx + x, wy + y, wx + x + w, wy + y + h), xdisplay=os.environ["DISPLAY"])
        image.save(log.parent / f"{name}.png")
        return image

    def move(x, y):
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)

    def key(value):
        subprocess.run(["xdotool", "key", "--window", window, value], check=True)

    def drag(start, end):
        move(*start)
        time.sleep(0.2)
        subprocess.run(["xdotool", "mousedown", "1"], check=True)
        for step in range(1, 9):
            move(*(a + (b - a) * step // 8 for a, b in zip(start, end)))
            time.sleep(0.08)
        subprocess.run(["xdotool", "mouseup", "1"], check=True)
        time.sleep(0.35)

    def desktop():
        key("ctrl+alt+shift+s")
        time.sleep(0.5)
        return ET.parse(profile / "gumps.xml")

    command("setup")
    time.sleep(0.9)
    click(605, 101)  # Close the character sheet so the dock and world receive input.
    _, _, width, height = geometry()
    step = max(23, min(40, (width - 60) // 18))
    dock_width, dock_y = step * 18 + 48, height - 96
    dock_x = (width - dock_width) // 2
    belt_x = dock_x + 20 + step * 14
    click(belt_x + 4 * step + 12, dock_y + 35)
    command("belt")
    capture("combat-dock-filled", (dock_x, dock_y, dock_width, 86))
    move(width // 2, height // 2)
    key("1")
    time.sleep(0.35)
    command("use")
    click(belt_x + 4 * step + 12, dock_y + 14)
    capture("combat-dock-expanded", (dock_x, dock_y - 108, dock_width, 194))
    click(belt_x + 4 * step + 12, dock_y + 14)

    before = capture("experience-before")
    command("experience")
    time.sleep(0.65)
    after = capture("experience-after")
    assert before.crop((dock_x + 220, dock_y + 77, dock_x + 470, dock_y + 82)).tobytes() != after.crop(
        (dock_x + 220, dock_y + 77, dock_x + 470, dock_y + 82)).tobytes(), "XP strip did not update"

    click(dock_x + (dock_width - 238) // 2 + 14, dock_y + 60)  # Character icon.
    assert desktop().find(".//gump[@type='9']") is not None, "Character icon did not open the sheet"
    click(605, 101)
    drag((width // 2, height // 2 - 19), (width // 2 + 86, height // 2 - 103))
    bars = desktop().findall(".//gump[@type='4']")
    assert len(bars) == 1, "Dragging the avatar did not open a health gump"
    bar_x, bar_y = (int(bars[0].attrib[field]) for field in ("x", "y"))
    capture("avatar-portrait", (bar_x, bar_y, 226, 101))
    selected_portrait = None
    for appearance in ("human-m", "human-f", "elf-m", "elf-f", "gargoyle-m", "gargoyle-f"):
        command(appearance)
        time.sleep(0.45)
        portrait = capture(f"portrait-{appearance}", (bar_x, bar_y, 226, 101)).crop((12, 29, 64, 89))
        colors = Counter(portrait.getdata())
        assert portrait.width * portrait.height - colors.most_common(1)[0][1] > 280, f"Missing {appearance} portrait"
        if selected_portrait is None:
            selected_portrait = portrait.tobytes()
        assert portrait.tobytes() == selected_portrait, "Changing the world sprite changed the selected portrait"
    command("elf-f")
    drag((bar_x + 40, bar_y + 8), (bar_x + 110, bar_y + 8))
    moved = desktop().find(".//gump[@type='4']")
    assert int(moved.attrib["x"]) != bar_x, "The opened health gump could not be moved"
    move(width - 7, height // 2)
    time.sleep(0.3)
    capture("combat-hud")

    drag((dock_x + 70, dock_y + 61), (dock_x + 70, dock_y - 79))
    desktop()
    saved = json.loads((profile / "profile.json").read_text())
    assert "combat_dock_offset" in saved and saved["combat_dock_offset"], saved
    assert "-140" in str(saved["combat_dock_offset"]), saved["combat_dock_offset"]

    subprocess.run(["xdotool", "windowsize", "--sync", window, "900", "640"], check=True)
    time.sleep(0.85)
    _, _, width, height = geometry()
    assert (width, height) == (900, 640), (width, height)
    image = capture("combat-dock-resized")
    colors = image.crop((width - 12, height - 12, width - 3, height - 3)).getdata()
    assert any(any(pixel[:3]) for pixel in colors), "Resized game had a black margin"
    print("Actual client: dock menus, belt, live XP, avatar dragging, portraits, saved dock position, and full-window resize passed.", flush=True)
