"""Drive the New Haven journal and Iolo's companion dialogue in ClassicUO."""
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from check_portrait_dialogue_ui import client_capture, client_image


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
    if os.environ.get("SANCTUARY_TEST_HAVEN_SERVICES_UI") == "1":
        exercise_services(window, click, command)
        return
    if os.environ.get("SANCTUARY_TEST_SUCCUBUS_ONLY") == "1":
        exercise_succubus(window, command)
        return
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
    click(406, 339)  # Summons remains available after recruitment.
    click(406, 419)  # Equipment stays available after following a keyword.
    client_capture(window, "iolo-equipment.png")
    key("ctrl+alt+shift+s")
    bars = ET.parse(profile / "gumps.xml").findall(".//gump[@type='9']")
    assert len(bars) == 1, "Equipment did not open Iolo's paperdoll"
    command("joined")
    key("j")
    assert client_capture(window, area=journal_area) == entry, "Repeating the conversation changed the first journal entry"
    key("j")
    if os.environ.get("SANCTUARY_TEST_BRITAIN_STORY_UI") == "1":
        exercise_britain(window, click, command, key)
        return
    command("bark-stocked")
    time.sleep(7.2)
    command("bark-composed")
    client_capture(window, "iolo-composed-bark.png")
    command("bark-urgent")
    client_capture(window, "iolo-urgent-bark.png")
    command("bark-cleanup")
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
    command("iolo-ghost-setup")
    time.sleep(0.5)
    client_capture(window, "iolo-before-death.png")
    command("iolo-died")
    time.sleep(0.5)
    client_capture(window, "iolo-ghost.png")
    click(298, 179)  # Iolo's spirit follows the Avatar into the healer's house.
    client_capture(window, "iolo-ghost-click.png")
    time.sleep(0.55)
    click(362, 188)  # Ask Merryn about Iolo.
    client_capture(window, "iolo-healer-offer.png")
    command("iolo-healer-offer")
    click(374, 235)
    command("iolo-healer-declined")
    time.sleep(0.55)
    click(362, 188)
    command("iolo-healer-offer")
    click(210, 235)
    command("iolo-healer-resurrected")
    for approach, point in (("shaft-start", (342, 211)), ("start", (386, 167))):
        command(f"mine-{approach}")
        time.sleep(0.8)
        client_capture(window, f"mine-{approach}.png")
        click(*point)
        time.sleep(1.1)
        client_capture(window, "mine-inside.png")
        command("mine-entered")
    command("mountain-mine-start")
    time.sleep(0.8)
    client_capture(window, "mountain-mine-approach.png")
    click(254, 175)
    time.sleep(1.1)
    client_capture(window, "mountain-mine-inside.png")
    command("mountain-mine-entered")
    command("mountain-mine-minion")
    command("mountain-minion-follows", timeout=12)
    command("rope-start")
    time.sleep(0.7)
    subprocess.run(["xdotool", "mousemove", "--window", window, "338", "209"], check=True)
    time.sleep(0.2)
    client_capture(window, "mine-rope-with-corpse.png")
    subprocess.run(["xdotool", "click", "--repeat", "2", "1"], check=True)
    time.sleep(1)
    command("rope-arrived")
    command("krulg-start")
    command("krulg-attacks", timeout=12)
    command("assist")
    command("assist-impact", timeout=9)
    command("miner-setup")
    time.sleep(0.6)
    client_capture(window, "mine-camp.png")
    click(320, 129)
    client_capture(window, "mine-camp-dialogue.png")
    click(132, 419)
    command("departure")
    time.sleep(0.7)
    client_capture(window, "iolo-after-moongate.png")
    click(132, 419)
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
    exercise_succubus(window, command)
    command("mine-topics")
    exercise_tower(window, click, command, key)
    print("Actual client: Iolo, mine, healer, summon, and tower flows passed.", flush=True)


def exercise_tower(window, click, command, key):
    command("tower-start")
    time.sleep(0.8)
    key("Escape")
    click(480, 334)  # Graphics → Circle of transparency.
    click(487, 403)  # Apply.
    opaque = client_capture(window, "tower-opaque.png", area=(194, 183, 165, 145))
    key("Escape")
    click(480, 334)
    click(487, 403)
    revealed = client_capture(window, "tower-revealed.png", area=(194, 183, 165, 145))
    assert revealed != opaque, "Changing transparency had no effect at the tower"
    click(266, 271)
    time.sleep(2)
    command("tower-entered")


def exercise_britain(window, click, command, key):
    def scene(suffix):
        command(f"britain-{suffix}")
        time.sleep(0.45)

    scene("bridge")
    client_capture(window, "britain-bridge.png")
    scene("geoffrey")
    client_capture(window, "britain-meeting-geoffrey.png")
    click(132, 419)
    scene("throne")
    audience = set()
    for page in range(5):
        portrait = client_capture(window, f"britain-audience-{page}.png", area=(43, 76, 150, 150))
        audience.add(portrait)
        click(406, 419)
    assert len(audience) == 2, "The audience did not alternate between Iolo and Lord British"
    scene("retired")
    key("j")
    client_capture(window, "britain-journal.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "200", "290", "click", "--repeat", "10", "5"], check=True)
    time.sleep(0.35)
    client_capture(window, "britain-journal-retirement.png")
    key("j")
    scene("college")
    client_capture(window, "britain-college.png")
    click(406, 379)
    client_capture(window, "britain-free-identification.png")
    scene("free-id")
    scene("recover")
    click(406, 419)
    click(132, 419)
    scene("runebook")
    client_capture(window, "britain-runebook.png")
    click(284, 268)
    scene("runebook-arrived")
    scene("wolfgang-offer")
    click(406, 339)
    client_capture(window, "britain-wolfgang-introduction.png")
    click(406, 379)
    client_capture(window, "britain-geoffreys-advice.png")
    click(406, 419)
    client_capture(window, "britain-wolfgang-joins.png")
    click(132, 419)
    scene("wolfgang-joined")
    client_capture(window, "britannia-wolfgang-bark.png")
    time.sleep(5.0)
    scene("wolfgang-win")
    time.sleep(5.4)
    scene("wolfgang-trust")
    click(406, 419)
    client_capture(window, "britain-wolfgangs-past.png")
    click(132, 419)
    scene("runebook-followers")
    click(284, 268)
    scene("runebook-party-arrived")
    if os.environ.get("SANCTUARY_VERIFY_BRITANNIA_WORLD") == "1":
        scene("world-and-pack")
        client_capture(window, "wolfgang-backpack.png")
    print("Actual client: Britain audience, journal, runebook, free identification, and Wolfgang passed.", flush=True)


def exercise_services(window, click, command):
    def mouse(x, y, count=1):
        subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
        time.sleep(0.15)
        subprocess.run(["xdotool", "click", "--repeat", str(count), "1"], check=True)
        time.sleep(0.45)

    command("npc-speech")
    command("scholar-setup")
    time.sleep(0.6)
    client_capture(window, "scholar-location.png")
    click(362, 151)
    client_capture(window, "scholar-dialogue.png")
    click(406, 379)
    client_capture(window, "scholar-appraisal.png")
    mouse(213, 92, 2)
    mouse(213, 160, 2)
    client_capture(window, "scholar-selected.png")
    click(422, 326)
    client_capture(window, "scholar-payment.png")
    command("scholar-paid")

    command("training-setup")
    time.sleep(0.6)
    client_capture(window, "trainer-location.png")
    click(341, 126)
    client_capture(window, "trainer-dialogue.png")
    click(406, 339)
    client_capture(window, "trainer-confirm.png")
    click(399, 324)
    command("training-cancelled")
    click(341, 126)
    click(406, 339)
    click(136, 326)
    client_capture(window, "trainer-amount.png")
    click(390, 205)  # Increase the offer by 10 gold.
    click(255, 205)  # Decrease it again.
    click(316, 205)
    subprocess.run(["xdotool", "key", "--window", window, "Home", "shift+End", "9", "0", "0"], check=True)
    click(136, 265)
    client_capture(window, "trainer-paid.png")
    command("training-paid")

    command("gate-ui-setup")
    time.sleep(0.6)
    client_capture(window, "moongate-britannia.png")
    click(350, 220)  # Select Britain by its full row.
    click(450, 428)
    command("gate-ui-arrived")
    client_capture(window, "britain-arrival.png")


def exercise_succubus(window, command):
    def right_click():
        subprocess.run(["xdotool", "mousedown", "3"], check=True)
        time.sleep(0.12)
        subprocess.run(["xdotool", "mouseup", "3"], check=True)
        time.sleep(1.1)

    command("succubus-start")
    time.sleep(0.5)
    subprocess.run(["xdotool", "mousemove", "--window", window, "386", "282"], check=True)
    time.sleep(0.2)
    client_capture(window, "succubus-start.png")
    right_click()
    command("succubus-first")
    time.sleep(0.6)
    mana_icon = (43, 403, 26, 25)
    affordable = client_image(window, area=mana_icon).convert("RGB")
    command("succubus-low-mana")
    time.sleep(0.6)
    client_capture(window, "succubus-low-mana-screen.png")
    low = client_image(window, "succubus-low-mana.png", area=mana_icon).convert("RGB")
    red = lambda img: sum(r > 1.3 * g and r > 1.3 * b for r, g, b in img.getdata())
    assert red(low) > red(affordable) + 30, "The Succubus icon did not turn red below the exact mana cost"
    command("succubus-refill")
    time.sleep(0.6)
    assert red(client_image(window, area=mana_icon).convert("RGB")) == red(affordable), "The hotbar icon stayed red after refilling mana"
    client_capture(window, "succubus-friendly-aim.png")
    right_click()
    command("succubus-second")
