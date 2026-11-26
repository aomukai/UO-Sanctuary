"""Spend all four class attributes through the Sanctuary character sheet."""
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]

def talent_frame(width, height):
    frame_width = max(612, min(width - 20, 960))
    frame_height = max(440, min(height - 24, 544))
    return min(10, max(0, width - frame_width)), min(27, max(0, height - frame_height)), frame_width, frame_height

def exercise(window, click, capture):
    log = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])
    profile = Path(os.environ["SANCTUARY_TEST_PROFILE"])
    geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
    bounds = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
    window_x, window_y, window_width, window_height = (int(bounds[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))

    def capture_client(name):
        area = (window_x, window_y, window_x + window_width, window_y + window_height)
        image = ImageGrab.grab(bbox=area, xdisplay=os.environ["DISPLAY"])
        image.save(log.parent / name)
        return image

    def saved_gumps():
        subprocess.run(["xdotool", "key", "--window", window, "ctrl+alt+shift+s"], check=True)
        time.sleep(0.5)
        return [g for file in profile.rglob("gumps.xml") for g in ET.parse(file).findall("gump")]

    def drag(start, end):
        def move(x, y):
            subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)

        move(*start)
        time.sleep(0.2)
        subprocess.run(["xdotool", "mousedown", "1"], check=True)
        time.sleep(0.2)
        for step in range(1, 9):
            move(*(int(a + (b - a) * step / 8) for a, b in zip(start, end)))
            time.sleep(0.08)
        time.sleep(0.15)
        subprocess.run(["xdotool", "mouseup", "1"], check=True)
        time.sleep(0.4)

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
    def hover(x, y):
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
        time.sleep(1.0)

    def right_click(x, y):
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y), "click", "3"], check=True)
        time.sleep(0.35)

    command("setup")
    time.sleep(1)
    capture("level-up-notification.png")
    click(190, 114)  # New Talents notification button.
    command("talents")
    capture("level-up-talents.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "500", "200", "click", "3"], check=True)
    time.sleep(0.4)
    click(60, 114)  # The Stats notification brings the character sheet forward.
    time.sleep(0.6)
    capture("stats-unspent.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "610", "41"], check=True)
    time.sleep(0.2)
    capture_client("sanctuary-character-sheet.png")
    # Save the actual origin of the unified sheet.
    gumps = saved_gumps()
    sheet = next((g for g in gumps if g.attrib.get("type") == "9"), None)
    assert sheet is not None, f"No character sheet in saved desktop: {[g.attrib for g in gumps]}"
    assert all(g.attrib.get("type") != "12" for g in gumps), "An extra legacy status window opened"
    x, y = int(sheet.attrib["x"]), int(sheet.attrib["y"])
    command("matchup")
    time.sleep(0.65)
    hover(x + 505, y + 118)
    capture_client("attack-chance.png")
    hover(x + 505, y + 137)
    capture_client("defense-chance.png")
    for row in range(4):
        click(x + 392, y + 103 + 19 * row)
    command("four")
    click(x + 392, y + 160)
    for _ in range(3):
        click(x + 392, y + 103)
    time.sleep(0.6)
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "50"], check=True)
    time.sleep(0.5)
    capture("stats-spent.png")
    command("final")

    drag((x + 31, y + 242), (x + 56, y + 381))
    capture_client("character-sheet-stow.png")
    command("stowed")
    click(x + 56, y + 381)  # Backpack from the character sheet.
    pack = next(g for g in saved_gumps() if g.attrib.get("type") == "2")
    pack_x, pack_y = int(pack.attrib["x"]), int(pack.attrib["y"])
    capture_client("character-sheet-backpack.png")
    # Start on an opaque part of the small diagonal dagger sprite.
    drag((pack_x + 68, pack_y + 69), (x + 568, y + 346))
    capture_client("character-sheet-equipped.png")
    command("equipped")
    subprocess.run(["xdotool", "mousemove", "--window", window, str(pack_x + 111), str(pack_y + 110)], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "click", "3"], check=True)
    click(x + 250, y + 30)  # Bring the character sheet forward after the backpack.

    click(x + 247, y + 381)
    book_x = max(0, (window_width - 604) // 2)
    book_y = max(0, (window_height - 404) // 2)
    book = capture_client("sanctuary-journal.png")
    red, green, blue = book.getpixel((book_x + 122, book_y + 250))[:3]
    assert red > 200 and green > 170 and blue > 110, "Journal parchment did not render"
    click(book_x + 385, book_y + 159)  # Honesty emblem.
    filtered = capture_client("sanctuary-journal-filter.png")
    assert any(book.getpixel((xx, yy)) != filtered.getpixel((xx, yy))
               for xx in range(book_x + 30, book_x + 170)
               for yy in range(book_y + 85, book_y + 108)), "Selecting Honesty did not update the journal heading"
    click(book_x + 556, book_y + 39)
    command("catalog")
    command("batch")
    click(x + 122, y + 381)  # Class from the character sheet.
    command("talents")
    menu_x, menu_y, menu_width, menu_height = talent_frame(window_width, window_height)
    skeleton = (menu_x + 67, menu_y + 125)
    mastery = (menu_x + 118, menu_y + 125)
    apply = (menu_x + menu_width - 69, menu_y + menu_height - 24)
    close = (menu_x + menu_width - 55, menu_y + 34)
    hover(*skeleton)
    capture_client("talent-milestones.png")
    click(*skeleton)
    click(*mastery)
    command("draft")
    right_click(*skeleton)  # Keep the draft valid while a drafted talent depends on this rank.
    right_click(*mastery)
    right_click(*skeleton)
    click(*apply)  # No pending points to apply.
    command("draft")

    click(*skeleton)
    click(*close)  # Close discards the draft.
    command("draft")
    click(x + 122, y + 381)
    command("talents")
    click(*skeleton)
    hover(*mastery)
    capture_client("talent-mastery-comparison.png")
    hover(*skeleton)
    capture_client("talent-raise-comparison.png")
    click(*mastery)
    command("draft")
    time.sleep(0.4)
    capture_client("talent-preview.png")
    click(*apply)
    time.sleep(0.7)
    command("spent")
    hover(*mastery)
    capture_client("talent-applied.png")
    print("Actual client: verified character stats, journal, talent draft, undo, discard and Apply.", flush=True)
