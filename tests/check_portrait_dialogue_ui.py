"""Exercise the portrait picker and New Haven's private dialogue in ClassicUO."""
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from PIL import Image, ImageChops, ImageGrab, ImageStat


def client_geometry(window):
    geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
    dimensions = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
    return tuple(int(dimensions[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))


def client_image(window, name=None, area=None):
    wx, wy, width, height = client_geometry(window)
    x, y, w, h = area or (0, 0, width, height)
    image = ImageGrab.grab(bbox=(wx + x, wy + y, wx + x + w, wy + y + h), xdisplay=os.environ["DISPLAY"])
    if name:
        image.save(Path(os.environ["SANCTUARY_TEST_LOG_DIR"]) / name)
    return image


def client_capture(window, name=None, area=None):
    return client_image(window, name, area).tobytes()


def creator_projection(width, height):
    scale = min(width / 640, height / 480)
    left, top = (width - 640 * scale) / 2, (height - 480 * scale) / 2

    def project(x, y):
        return round(left + x * scale), round(top + y * scale)

    return project


def assert_same_preview(expected, actual):
    size = (32, 32)
    images = [image.convert("RGB").resize(size, Image.Resampling.BILINEAR) for image in (expected, actual)]
    difference = ImageStat.Stat(ImageChops.difference(*images)).mean
    assert max(difference) < 8, f"The scaled portrait differed from the selection: {difference}"


def exercise_resize(window, click):
    _, _, original_width, original_height = client_geometry(window)

    def resize(width, height):
        subprocess.run(["xdotool", "windowmove", window, "0", "0"], check=True)
        subprocess.run(["xdotool", "windowsize", "--sync", window, str(width), str(height)], check=True, timeout=10)
        time.sleep(0.6)
        assert client_geometry(window)[2:] == (width, height), "Client window did not resize"

    preview = (408, 100, 182, 182)
    default_face = client_image(window, area=preview)

    def scaled_preview(project):
        x, y, w, h = preview
        left, top = project(x, y)
        right, bottom = project(x + w, y + h)
        return client_image(window, area=(left, top, right - left, bottom - top))

    resize(1200, 720)
    wide = creator_projection(1200, 720)
    client_capture(window, "creation-wide.png")
    before_click = scaled_preview(wide)
    assert_same_preview(default_face, before_click)

    click(*wide(339, 378))  # Next
    click(*wide(81, 196))   # First portrait on page two
    chosen = scaled_preview(wide)
    assert chosen.tobytes() != before_click.tobytes(), "Clicks missed the enlarged portrait gallery"

    resize(960, 768)
    tall = creator_projection(960, 768)
    assert_same_preview(chosen, scaled_preview(tall))
    click(*tall(540, 418))  # Continue
    assert scaled_preview(tall).tobytes() != chosen.tobytes(), "Continue did not open the name page"
    click(*tall(88, 408))   # Back
    assert_same_preview(chosen, scaled_preview(tall))

    resize(original_width, original_height)
    assert_same_preview(chosen, client_image(window, area=preview))
    click(82, 377)  # Previous
    click(81, 196)
    assert client_image(window, area=preview).tobytes() == default_face.tobytes(), "Windowed gallery clicks missed"
    print("Actual client: aspect-preserving creator scaling, portrait clicks, page navigation, and restore passed.", flush=True)


def appearance(window, click, capture):
    face = (408, 100, 182, 182)
    click(146, 199)
    previous = client_capture(window, area=face)
    click(325, 145)
    click(195, 145)
    assert client_capture(window, area=face) == previous, "Switching sex lost the selected portrait"

    all_tiles = set()
    for race_x in (182, 265, 345):
        click(race_x, 108)
        for sex_x in (194, 325):
            click(sex_x, 145)
            tiles = set()
            for page, count in enumerate((15, 15, 15, 11)):
                if page:
                    click(339, 378)
                view = client_image(window)
                for i in range(count):
                    x, y = 55 + i % 5 * 66, 174 + i // 5 * 64
                    image = view.crop((x, y, x + 57, y + 56))
                    assert len(image.getcolors(3192)) > 20, "A template thumbnail is empty"
                    tiles.add(image.tobytes())
            assert len(tiles) == 56, "The race and sex group did not draw 56 distinct portraits"
            all_tiles.update(tiles)
    assert len(all_tiles) == 336, "The gallery drew duplicates across groups"
    click(82, 329)  # Gargoyle female, page 4, slot 11. Tests a template ID above 255.
    client_capture(window, "portrait-creator.png")
    return client_image(window, area=face)


def arrival(window, click, profile, expected_portrait):
    log = Path(os.environ["SANCTUARY_TEST_LOG"])

    def command(suffix):
        name = f"portrait-dialogue-{suffix}"
        offset = log.stat().st_size
        os.write(int(os.environ["SANCTUARY_TEST_CONSOLE_FD"]), f"{name}\n".encode())
        for _ in range(100):
            output = log.read_text(errors="replace")[offset:]
            assert f"{name}: failed" not in output, output
            if f"{name}: passed" in output:
                return
            time.sleep(0.05)
        raise AssertionError(f"Server did not complete {name}")

    command("check")
    line = (260, 85, 325, 90)
    greeting = client_capture(window, "new-haven-dialogue.png")
    assert_same_portrait(expected_portrait, client_image(window, area=(34, 322, 56, 56)), "dialogue")
    lines = {client_capture(window, area=line)}
    for y in (339, 379, 419):
        click(400, y)  # Iolo, New Haven, Bountiful Harvest Inn.
        rendered = client_capture(window, area=line)
        assert rendered not in lines, "A topic did not produce its own answer"
        lines.add(rendered)
    reply = client_capture(window, "new-haven-reply.png")
    assert greeting != reply, "The dialogue did not repaint"
    click(130, 419)
    assert reply != client_capture(window, "new-haven-arrival.png"), "Farewell did not dismiss the conversation"
    click(605, 101)  # Close the initial character sheet.
    client_capture(window, "new-haven-world.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "318", "191"], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    for x, y in ((331, 181), (341, 169), (365, 151), (403, 132)):
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
        time.sleep(0.12)
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    time.sleep(0.35)
    client_capture(window, "avatar-health.png")
    subprocess.run(["xdotool", "key", "ctrl+alt+shift+s"], check=True)
    time.sleep(0.65)
    bars = ET.parse(profile / "gumps.xml").findall(".//gump[@type='4']")
    assert len(bars) == 1, "A health gump did not open for the avatar"
    x, y = int(bars[0].attrib["x"]), int(bars[0].attrib["y"])
    assert_same_portrait(expected_portrait, client_image(window, area=(x + 11, y + 28, 54, 62)), "health")

    # Dismiss the test health bar and reopen the greeting with one click on Elin.
    subprocess.run(["xdotool", "mousemove", "--window", window, str(x + 25), str(y + 10), "click", "3"], check=True)
    click(366, 152)
    time.sleep(0.4)
    assert client_capture(window, area=line) in lines, "A single click did not open Elin's dialogue"
    blackletter = client_capture(window, "new-haven-blackletter.png", area=line)

    def choose_typeface(x, name):
        subprocess.run(["xdotool", "key", "Escape"], check=True)
        time.sleep(0.35)
        click(x, 353)  # Options → Graphics → Dialogue font.
        click(487, 403)  # Apply.
        return client_capture(window, f"new-haven-{name}.png", area=line)

    classic = choose_typeface(423, "classic")
    book = choose_typeface(492, "book")
    assert len({blackletter, classic, book}) == 3, "The three typefaces did not redraw the active dialogue distinctly"
    assert choose_typeface(339, "blackletter-restored") == blackletter, "Returning to Blackletter did not restore the default face"
    click(130, 419)

    command("banker")
    time.sleep(0.3)
    click(366, 152)
    time.sleep(0.55)
    client_capture(window, "npc-bank-click.png")
    command("bank-opened")
    time.sleep(0.3)
    click(366, 152)
    time.sleep(0.55)
    client_capture(window, "npc-vendor-click.png")
    command("vendor-opened")
    print("Actual client: portrait continuity, one-click NPC services, three dialogue topics, and font switching passed.", flush=True)


def assert_same_portrait(expected, actual, label):
    # The selected tall atlas tile fills the height. Compare its inner pixels, away from the frame.
    def face_pixels(image, size):
        w, h = image.size
        width = round(h * 0.6)
        left = (w - width) // 2
        return image.crop((left, 0, left + width, h)).resize(size, Image.Resampling.BILINEAR).convert("RGB")

    size = (11, 16)
    reference = face_pixels(expected, size)
    actual = face_pixels(actual, size)
    error = ImageStat.Stat(ImageChops.difference(reference, actual.convert("RGB"))).mean
    assert max(error) < 9, f"The portrait in {label} differed from the selection: {error}"
