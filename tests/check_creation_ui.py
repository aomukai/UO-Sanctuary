#!/usr/bin/env python3
"""Drive the real ClassicUO creation UI in Xvfb against a disposable test server."""
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys
import tempfile
import time
import xml.etree.ElementTree as ET
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]


def check(port, combat=False):
    logs = ROOT / ".local/logs"
    logfile = logs / "creation-ui-client.log"
    with tempfile.TemporaryDirectory(prefix="sanctuary-creation-ui-", dir="/dev/shm") as temp:
        folder = Path(temp)
        username = "combatui" if combat else "classui"
        character = "CombatUI" if combat else "ClassUI"
        password = secrets.token_hex(8).encode()
        machine = socket.gethostname().encode()
        cipher = "1-" + bytes(value ^ machine[i % len(machine)] for i, value in enumerate(password)).hex().upper()
        config = {
            "ip": "127.0.0.1", "port": int(port), "username": username, "password": cipher,
            "ultimaonlinedirectory": str(ROOT / ".local/uo-data"), "clientversion": "7.0.117.1",
            "plugins": [], "autologin": True, "saveaccount": True, "reconnect": False,
            "profilespath": str(folder / "Profiles"),
        }
        server_config = json.loads((ROOT / ".local/server/Configuration/modernuo.json").read_text())
        server_name = server_config["settings"]["serverListing.serverName"]
        profile = folder / "Profiles" / username / server_name / character
        profile.mkdir(parents=True)
        if combat:
            # Legacy AlwaysRun must never override the explicit Diablo Walk choice.
            (profile / "profile.json").write_text(json.dumps({"always_run": True, "activate_chat_after_enter": False, "activate_chat_additional_buttons": True}))
        # A disposable Save Desktop macro lets us inspect final UI state without
        # changing the production client or depending on a window manager.
        (profile / "macros.xml").write_text(
            '<macros><macro name="Save test desktop" key="115" alt="True" ctrl="True" shift="True">'
            '<actions><action code="32" subcode="0" submenutype="0"/></actions></macro></macros>')
        if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1":
            macros = profile / "macros.xml"
            macros.write_text(macros.read_text().replace("</macros>",
                '<macro name="Raise Skeleton" key="113" alt="False" ctrl="False" shift="False">'
                '<actions><action code="1" subcode="0" submenutype="0" text="[Cast RaiseSkeleton"/></actions></macro></macros>'))
        settings = folder / "settings.json"
        settings.write_text(json.dumps(config))
        settings.chmod(0o600)
        env = {**os.environ, "LD_LIBRARY_PATH": str(ROOT / ".local/client"),
               "DOTNET_ROOT": str(ROOT / ".tools/dotnet"), "SDL_AUDIODRIVER": "dummy",
               "LIBGL_ALWAYS_SOFTWARE": "1"}
        if os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1" or os.environ.get("SANCTUARY_TEST_TREASURE_UI") == "1":
            env["SANCTUARY_TRACE_MOVEMENT"] = "1"
        client_assembly = Path(os.environ.get("SANCTUARY_TEST_CLIENT_DLL", ROOT / "upstream/ClassicUO/bin/Debug/net10.0/cuo.dll"))
        with logfile.open("w") as output:
            process = subprocess.Popen(
                [str(ROOT / ".tools/dotnet/dotnet"),
                 str(client_assembly),
                 "-settings", str(settings), "-skiploginscreen"],
                cwd=folder, env=env, stdout=output, stderr=subprocess.STDOUT,
            )
        try:
            def wait_for(needle, timeout=25):
                deadline = time.monotonic() + timeout
                while time.monotonic() < deadline:
                    if needle in logfile.read_text(errors="replace"):
                        return
                    if process.poll() is not None:
                        raise AssertionError(f"Client exited; inspect {logfile}")
                    time.sleep(0.1)
                raise AssertionError(f"Client did not reach {needle!r}; inspect {logfile}")

            wait_for("Login step: CharacterCreation")
            time.sleep(0.5)
            result = subprocess.check_output(["xdotool", "search", "--onlyvisible", "--pid", str(process.pid)], text=True)
            window = result.splitlines()[-1]

            def click(x, y):
                subprocess.run(["xdotool", "mousemove", "--window", window, str(x), str(y)], check=True)
                time.sleep(0.15)
                subprocess.run(["xdotool", "click", "1"], check=True)
                time.sleep(0.25)

            def capture(name):
                ImageGrab.grab(xdisplay=os.environ["DISPLAY"]).save(logs / name)

            if os.environ.get("SANCTUARY_TEST_CLASS_NAMES_UI") == "1":
                from check_class_names_ui import exercise
                exercise(window, click, capture, character, wait_for)
                return

            capture("creation-classes.png")
            click(210, 205)  # Amazon, then back to the class selector.
            capture("creation-appearance-amazon.png")
            click(596, 455)
            capture("creation-back.png")
            click(410 if os.environ.get("SANCTUARY_TEST_TELEPORT_UI") == "1" else 210 if combat else 410, 205 if os.environ.get("SANCTUARY_TEST_TELEPORT_UI") == "1" else 260)  # Necromancer or Paladin.
            click(320, 82)
            subprocess.run(["xdotool", "key", "--window", window, "ctrl+a"], check=True)
            subprocess.run(["xdotool", "type", "--window", window, "--delay", "60", character], check=True)
            capture("creation-appearance-paladin.png")
            click(615, 455)
            capture("creation-submitted.png")
            wait_for("Login step: CharacterCreationDone")
            # World initialization follows the protocol transition; leave time for its first frame.
            time.sleep(1)
            capture("creation-world.png")
            print("Actual client: class selector, appearance, Back/reselect, and world entry passed.", flush=True)
            if combat:
                os.environ["SANCTUARY_TEST_CLIENT_PID"] = str(process.pid)
                os.environ["SANCTUARY_TEST_PROFILE"] = str(profile)
                from check_combat_ui import exercise
                exercise(window, click, capture)
                if os.environ.get("SANCTUARY_TEST_ESCROW_UI") == "1" or os.environ.get("SANCTUARY_TEST_INSURANCE_UI") == "1" or os.environ.get("SANCTUARY_TEST_HOUSING_UI") == "1" or os.environ.get("SANCTUARY_TEST_ALLIED_OVERLAP_UI") == "1" or os.environ.get("SANCTUARY_TEST_INSCRIPTION_UI") == "1" or os.environ.get("SANCTUARY_TEST_TELEPORT_UI") == "1" or os.environ.get("SANCTUARY_TEST_SURVEYS_UI") == "1" or os.environ.get("SANCTUARY_TEST_CARPENTRY_UI") == "1" or os.environ.get("SANCTUARY_TEST_MEALS_UI") == "1" or os.environ.get("SANCTUARY_TEST_COMBAT_POTIONS_UI") == "1" or os.environ.get("SANCTUARY_TEST_ALCHEMY_UI") == "1" or os.environ.get("SANCTUARY_TEST_GEM_SOCKETS_UI") == "1" or os.environ.get("SANCTUARY_TEST_RUNES_UI") == "1" or os.environ.get("SANCTUARY_TEST_RUNEBARK_UI") == "1" or os.environ.get("SANCTUARY_TEST_MOULDS_UI") == "1" or os.environ.get("SANCTUARY_TEST_SOCKETS_UI") == "1" or os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") == "1" or os.environ.get("SANCTUARY_TEST_IMBUING_UI") == "1" or os.environ.get("SANCTUARY_TEST_SALVAGE_UI") == "1" or os.environ.get("SANCTUARY_TEST_WARDROBE_UI") == "1" or os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") == "1" or os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1" or os.environ.get("SANCTUARY_TEST_MARKET_UI") == "1" or os.environ.get("SANCTUARY_TEST_CRAFTING_UI") == "1" or os.environ.get("SANCTUARY_TEST_PROFESSIONS_UI") == "1" or os.environ.get("SANCTUARY_TEST_ECONOMY_UI") == "1" or os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1" or os.environ.get("SANCTUARY_TEST_BELT_UI") == "1":
                    return
                if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1":
                    from check_pets_ui import command
                    process.terminate()
                    process.wait(timeout=10)
                    time.sleep(0.7)
                    command("offline")
                    with logfile.open("w") as output:
                        process = subprocess.Popen(
                            [str(ROOT / ".tools/dotnet/dotnet"),
                             str(client_assembly),
                             "-settings", str(settings), "-skiploginscreen"],
                            cwd=folder, env=env, stdout=output, stderr=subprocess.STDOUT,
                        )
                    wait_for("Login step: EnteringBritania")
                    time.sleep(2)
                    command("online")
                    capture("pets-after-relog.png")
                    command("final")
                    print("Actual client: disconnect/relogin retained guardian identity, health and guard order.", flush=True)
                if os.environ.get("SANCTUARY_TEST_EQUIPMENT_CRAFTING_UI") == "1" or os.environ.get("SANCTUARY_TEST_PROCESSING_UI") == "1" or os.environ.get("SANCTUARY_TEST_PETS_UI") == "1" or os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") == "1" or os.environ.get("SANCTUARY_TEST_STATS_UI") == "1" or os.environ.get("SANCTUARY_TEST_LOOT_ONLY") == "1" or os.environ.get("SANCTUARY_TEST_TIMING_ONLY") == "1":
                    return
                saved = json.loads((profile / "profile.json").read_text())
                assert saved["diablo_run"] is False, "Walk preference was not saved to the character profile"
                print("Actual client: Run/Walk preference persisted in the character profile.", flush=True)
                return
            console = os.environ.get("SANCTUARY_TEST_CONSOLE_FD")
            if console:
                os.write(int(console), b"creation-ui-level\n")
                time.sleep(0.4)
                click(320, 263)  # Paperdoll Class.
                capture("talents-defense.png")
                click(240, 94)
                capture("talents-offense.png")
                click(436, 94)
                capture("talents-combat.png")
                click(44, 94)
                click(420, 346)  # Learn Prayer, costing the awarded point.
                capture("talents-learned.png")
                click(590, 41)  # Close the Class window.
                click(320, 236)  # Paperdoll Skills.
                capture("trade-skills.png")

                def drag(start, end):
                    subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, start)], check=True)
                    subprocess.run(["xdotool", "mousedown", "1"], check=True)
                    time.sleep(0.2)
                    # Cross several rows while held, as the old group-drop code ran on hover.
                    for step in range(1, 9):
                        point = [int(start[i] + (end[i] - start[i]) * step / 8) for i in range(2)]
                        subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, point)], check=True)
                        time.sleep(0.06)
                    subprocess.run(["xdotool", "mouseup", "1"], check=True)
                    time.sleep(0.3)

                drag((170, 335), (170, 100))  # Cartography toward Gathering.
                drag((170, 251), (500, 430))  # Clickable Imbuing out of the menu.
                capture("skills-after-drag.png")
                # Save through the client's existing Save Desktop macro.
                subprocess.run(["xdotool", "key", "--window", window, "ctrl+alt+shift+s"], check=True)
                wait_for("Saving done!")
                expected = {
                    "Gathering": {18, 44, 45},
                    "Crafting": {0, 7, 8, 11, 13, 23, 34, 37, 56},
                    "Treasure Hunting": {12},
                }
                groups = list((folder / "Profiles").rglob("skillsgroups.xml"))
                assert groups, "Skills groups were not saved"
                for file in groups:
                    actual = {group.attrib["name"]: {int(item.attrib["id"]) for item in group.findall(".//skill")}
                              for group in ET.parse(file).findall("group")}
                    assert actual == expected, actual
                    desktop = ET.parse(file.with_name("gumps.xml"))
                    assert not desktop.findall(".//gump[@type='16']"), "Dragging created a floating skill button"
                print("Actual client: Class tabs, talent learning, and fixed 13-skill groups passed; dragging neither regrouped skills nor created shortcuts.", flush=True)
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == "__main__":
    check(sys.argv[1])
