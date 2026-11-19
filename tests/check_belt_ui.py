"""Real SDL client: authoritative belt cells, held-key repeat and mandatory Enter-chat."""
import json
import os
from pathlib import Path
import subprocess
import time
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]


def command(name):
    log = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    offset = log.stat().st_size
    os.write(int(os.environ["SANCTUARY_TEST_CONSOLE_FD"]), ("belt-ui-" + name + "\n").encode())
    deadline = time.monotonic() + 6
    while time.monotonic() < deadline:
        output = log.read_text(errors="replace")[offset:]
        if f"belt-ui-{name}: failed" in output:
            raise AssertionError(output[-2500:])
        if f"belt-ui-{name}: passed" in output:
            return json.loads(Path(os.environ["SANCTUARY_TEST_UI_STATE"]).read_text())
        time.sleep(.05)
    raise AssertionError(f"Missing belt-ui-{name}; inspect {log}")


def exercise(window, click, capture):
    def key(name):
        subprocess.run(["xdotool", "key", "--window", window, name], check=True)
        time.sleep(.15)

    click(538, 101)  # Minimize paperdoll so world and belt receive input.
    state = command("setup")
    time.sleep(.7)
    capture("belt-empty.png")
    geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
    size = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
    width, height = int(size["WIDTH"]), int(size["HEIGHT"])
    step = max(23, min(40, (width - 60) // 18))
    dock_x, dock_y = (width - (18 * step + 48)) // 2, height - 96
    belt_x = dock_x + 20 + 14 * step
    controls_x = belt_x + 4 * step + 12
    click(controls_x, dock_y + 35)  # Fill (+).
    time.sleep(.5)
    filled = command("state")
    assert len([s for s in filled["Cells"] if s]) == 16, filled
    capture("belt-filled.png")
    key("1")
    after = command("state")
    assert len([s for s in after["Cells"] if s]) == 15 and after["Cells"][0] == filled["Cells"][4], after
    command("hurt")
    subprocess.run(["xdotool", "keydown", "--window", window, "2"], check=True)
    time.sleep(1.1)
    subprocess.run(["xdotool", "keyup", "--window", window, "2"], check=True)
    held = command("state")
    assert len([s for s in held["Cells"] if s]) == 14, "Held key repeated potion consumption"
    command("hurt")
    key("3")
    key("4")
    before_chat = command("state")
    assert len([s for s in before_chat["Cells"] if s]) == 12, before_chat
    key("Return")
    subprocess.run(["xdotool", "type", "--window", window, "--delay", "100", "1234"], check=True)
    capture("belt-chat-digits.png")
    key("Return")
    chatted = command("chat")
    assert chatted["Cells"] == before_chat["Cells"], "Chat digits consumed potions"
    key("ctrl+alt+shift+s")  # Native Save Desktop macro, preserving the existing bindings.
    saved = json.loads((Path(os.environ["SANCTUARY_TEST_PROFILE"]) / "profile.json").read_text())
    assert saved["activate_chat_after_enter"] is True and saved["activate_chat_additional_buttons"] is False, saved
    timing = command("recovery")
    key("1")
    recovered = command("state")
    assert len([s for s in recovered["Cells"] if s]) == 11 and recovered["Recovery"] == timing["Recovery"] and recovered["NextAction"] == timing["NextAction"], recovered
    click(controls_x, dock_y + 35)  # Refill, then use four columns in one rapid burst.
    command("hurt")
    subprocess.run(["xdotool", "key", "--window", window, "--delay", "10", "1", "2", "3", "4"], check=True)
    time.sleep(.25)
    burst = command("state")
    assert len([s for s in burst["Cells"] if s]) == 12, "Different columns suppressed one another"
    subprocess.run(["xdotool", "mousemove", "--window", window, str(belt_x + step // 2), str(dock_y + 26), "click", "3"], check=True)
    time.sleep(.25)
    returned = command("state")
    assert len([s for s in returned["Cells"] if s]) == 11, "Right-click did not return one potion"
    click(controls_x, dock_y + 14)  # Expand all four rows.
    time.sleep(.3)
    capture("belt-expanded.png")
    command("final")
    print("Actual client: four-column fill/use/compaction, held-key repeat suppression, Enter-only chat from legacy false profile, saved enforcement and drinking during recovery passed.", flush=True)
