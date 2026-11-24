"""Exercise Sanctuary window shortcuts and options in the running client."""
import json
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from PIL import ImageGrab


def exercise(window, click, profile):
    geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
    bounds = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
    wx, wy, width, height = (int(bounds[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))
    logs = Path(os.environ["SANCTUARY_TEST_LOG_DIR"])

    def key(*values):
        subprocess.run(["xdotool", "key", "--window", window, *values], check=True)
        time.sleep(0.3)

    def desktop():
        key("ctrl+alt+shift+s")
        return {node.attrib["type"] for node in ET.parse(profile / "gumps.xml").findall("gump")}

    def capture(name=None, area=None):
        x, y, w, h = area or (0, 0, width, height)
        shot = ImageGrab.grab(bbox=(wx + x, wy + y, wx + x + w, wy + y + h), xdisplay=os.environ["DISPLAY"])
        if name:
            shot.save(logs / f"{name}.png")
        return shot

    def profile_values():
        return json.loads((profile / "profile.json").read_text())

    assert "9" in desktop(), "Expected the initial character sheet"
    key("c")
    assert "9" not in desktop(), "C did not close the initial character sheet"
    for shortcut, gump in (("b", "2"), ("c", "9")):
        key(shortcut)
        assert gump in desktop(), f"{shortcut} did not open gump {gump}"
        key(shortcut)
        assert gump not in desktop(), f"{shortcut} did not close gump {gump}"

    journal_x, journal_y = (width - 604) // 2, (height - 404) // 2
    journal_area = (journal_x + 108, journal_y + 239, 10, 10)
    key("j")
    journal_shot = capture(area=journal_area)
    red, green, blue = journal_shot.getpixel((5, 5))[:3]
    assert red > 200 and green > 170 and blue > 110, "J did not open the Sanctuary journal"
    journal = journal_shot.tobytes()
    key("j")
    assert capture(area=journal_area).tobytes() != journal, "J did not close the journal"

    initial_zoom = profile_values()["world_map_zoom_index"]
    key("m")
    assert "18" in desktop() and "8" not in desktop(), "M did not open the world map"
    subprocess.run(["xdotool", "mousemove", "--window", window, "290", "260"], check=True)
    time.sleep(0.4)
    subprocess.run(["xdotool", "click", "4"], check=True)
    time.sleep(0.35)
    capture("sanctuary-world-map")
    key("m")
    zoom = profile_values()["world_map_zoom_index"]
    assert zoom == initial_zoom + 1, f"Map zoom did not save the wheel change from {initial_zoom}: stored {zoom}"
    assert "world_map_position" in profile_values(), "Closing the map did not save its position"
    assert "18" not in desktop(), "M did not close the map"

    ox, oy = (width - 466) // 2, (height - 375) // 2
    options_area = (ox + 3, oy + 3, 16, 16)
    key("Escape")
    capture("options-graphics")
    options = capture(area=options_area).tobytes()
    key("Escape")
    assert capture(area=options_area).tobytes() != options, "Escape did not close Options"

    key("Return", "b", "c", "j", "m", "t", "k", "Escape")
    capture("menu-keys-during-chat")
    assert capture(area=journal_area).tobytes() != journal, "J opened the journal during chat"
    assert capture(area=options_area).tobytes() != options, "Escape opened Options during chat"
    key("Return")
    assert desktop().isdisjoint(("2", "9", "10", "18")), "A letter shortcut opened a gump during chat"

    talent_area = (37, 42, 130, 18)
    before_talents = capture(area=talent_area).tobytes()
    key("t")
    talents = capture("talents-shortcut", area=talent_area).tobytes()
    assert talents != before_talents, "T did not open class talents"
    key("t")
    assert capture(area=talent_area).tobytes() != talents, "T did not close class talents"
    key("k")
    assert "10" in desktop(), "K did not open Skills"
    key("k")
    assert "10" not in desktop(), "K did not close Skills"

    step = max(23, min(40, (width - 60) // 18))
    dock_width = 18 * step + 48
    menu_x = (width - dock_width) // 2 + (dock_width - 238) // 2
    click(menu_x + 4 * 34 + 14, height - 36)
    capture("sanctuary-help")
    click(ox + 403, (height - 320) // 2 + 286)

    key("Escape")
    click(ox + 56, oy + 351)
    click(ox + 403, (height - 320) // 2 + 286)
    assert capture(area=options_area).tobytes() == options, "Help did not return to Options"

    click(ox + 178, oy + 57)
    click(ox + 285, oy + 136)
    capture("options-sound")
    click(ox + 283, oy + 57)
    click(ox + 393, oy + 106)
    capture("options-music")
    click(ox + 388, oy + 57)
    click(ox + 393, oy + 106)
    key("c")  # The existing Character key cannot also bind the Backpack.
    key("Escape")
    assert capture(area=options_area).tobytes() == options, "Escape closed Options while capturing a key"
    click(ox + 393, oy + 106)
    key("i")
    capture("options-hotkeys")
    click(ox + 400, oy + 351)

    values = profile_values()
    assert 48 <= values["sound_volume"] <= 52, values["sound_volume"]
    assert values["enable_music"] is False
    assert values["sanctuary_menu_keys"] == "icjmtkof"
    key("b")
    assert "2" not in desktop(), "The old backpack shortcut was still active"
    key("i")
    assert "2" in desktop(), "The new backpack shortcut did not open it"
    key("i")
    assert "2" not in desktop(), "The new backpack shortcut did not close it"
    key("Escape")
    prompt_area = (width // 2 - 55, height // 2 - 30, 110, 60)
    before_logout = capture(area=prompt_area).tobytes()
    click(ox + 234, oy + 351)
    assert capture("options-logout-confirmation", area=prompt_area).tobytes() != before_logout, "Log Out did not show confirmation"
    print("Actual client: window toggles, map persistence, chat guard, Options, Help, logout confirmation, and key rebinding passed.", flush=True)
