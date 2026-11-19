"""Drive the New Haven journal and Iolo's companion dialogue in ClassicUO."""
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from check_portrait_dialogue_ui import client_capture


def exercise(window, click, profile):
    log = Path(os.environ["SANCTUARY_TEST_LOG"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])

    def command(suffix, timeout=5):
        name = f"iolo-story-{suffix}"
        offset = log.stat().st_size
        os.write(console, f"{name}\n".encode())
        for _ in range(timeout * 20):
            output = log.read_text(errors="replace")[offset:]
            assert f"{name}: failed" not in output, output
            if f"{name}: passed" in output:
                return
            time.sleep(0.05)
        raise AssertionError(f"The server did not complete {name}")

    def key(value):
        subprocess.run(["xdotool", "key", "--window", window, value], check=True)
        time.sleep(0.3)

    click(605, 101)  # Close the initial character sheet.
    key("j")
    journal_area = (49, 153, 244, 205)
    empty_journal = client_capture(window, area=journal_area)
    key("j")
    command("setup")
    time.sleep(0.65)
    client_capture(window, "harvest-inn.png")

    click(342, 230)  # Iolo at the stool beside the south table.
    client_capture(window, "iolo-first-meeting.png")
    command("met")
    line_area = (262, 85, 340, 195)
    responses = {client_capture(window, area=line_area)}
    for y in (339, 379, 419):
        click(406, y)
        response = client_capture(window, area=line_area)
        assert response not in responses, "An Iolo keyword did not deliver a distinct answer"
        responses.add(response)
    click(132, 419)

    key("j")
    client_capture(window, "iolo-journal.png")
    entry = client_capture(window, area=journal_area)
    assert entry != empty_journal, "Meeting Iolo did not add a visible journal entry"
    key("j")
    click(342, 230)
    assert client_capture(window, area=line_area) not in responses, "The early return did not use Iolo's busy greeting"
    click(132, 419)
    command("met")

    command("late")
    click(342, 230)
    client_capture(window, "iolo-ready.png")
    click(406, 339)  # Travel Alone leaves Iolo at the inn and the gate open.
    command("solo")
    client_capture(window, "iolo-solo.png")
    click(406, 419)  # Travel Together stays available after declining.
    client_capture(window, "iolo-joins.png")
    click(406, 299)  # The first keyword stays available after recruitment.
    click(406, 419)  # Equipment stays available after following a keyword.
    client_capture(window, "iolo-equipment.png")
    key("ctrl+alt+shift+s")
    bars = ET.parse(profile / "gumps.xml").findall(".//gump[@type='9']")
    assert len(bars) == 1, "Equipment did not open Iolo's paperdoll"
    command("joined")
    key("j")
    assert client_capture(window, area=journal_area) == entry, "Repeating the conversation changed the first journal entry"
    key("j")
    command("doorway")
    time.sleep(0.65)
    client_capture(window, "iolo-open-door.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "334", "169"], check=True)
    time.sleep(0.15)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(1.6)
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    client_capture(window, "iolo-crossed-door.png")
    command("crossed-doorway")
    command("departure")
    command("ghost")
    time.sleep(0.7)
    client_capture(window, "elin-ghost.png")
    click(132, 419)  # End Elin's conversation before going to the healer.
    command("resurrect")
    time.sleep(0.5)
    client_capture(window, "healer-resurrection.png")
    click(374, 235)  # Not yet.
    command("resurrection-declined")
    click(210, 235)
    command("resurrected")
    command("bandit-combat")
    command("bandit-impact", timeout=22)
    command("town-summon")
    time.sleep(1.2)
    client_capture(window, "town-minion-stored.png")
    command("town-summon-return")
    time.sleep(0.4)
    client_capture(window, "town-minion-returned.png")
    print("Actual client: Iolo dialogue and doorway, healer rescue, bandit combat, and town summons passed.", flush=True)
