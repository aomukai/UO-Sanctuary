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
from PIL import ImageChops, ImageGrab

ROOT = Path(os.environ.get("SANCTUARY_TEST_RUNTIME_ROOT", Path(__file__).resolve().parents[1]))


def check(port, combat=False):
    logs = Path(os.environ.get("SANCTUARY_TEST_LOG_DIR", ROOT / ".local/logs"))
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

            capture("creation-appearance.png")
            if os.environ.get("SANCTUARY_TEST_CREATION_RESIZE_UI") == "1":
                from check_portrait_dialogue_ui import exercise_resize
                exercise_resize(window, click)
            portraits = os.environ.get("SANCTUARY_TEST_PORTRAITS_UI") == "1"
            if portraits:
                from check_portrait_dialogue_ui import appearance
                selected_portrait = appearance(window, click, capture)
            click(540, 408)
            capture("creation-name.png")
            click(88, 408)
            capture("creation-back.png")
            click(540, 408)
            click(310, 194)
            subprocess.run(["xdotool", "key", "--window", window, "ctrl+a"], check=True)
            subprocess.run(["xdotool", "type", "--window", window, "--delay", "60", character], check=True)
            click(540, 408)
            capture("creation-classes.png")
            if os.environ.get("SANCTUARY_TEST_IOLO_UI") == "1":
                click(310, 313)  # Witch/Warlock.
            elif os.environ.get("SANCTUARY_TEST_TELEPORT_UI") == "1":
                click(310, 112)  # Sorceress/Sorcerer.
            else:
                click(136 if combat else 310, 180)  # Necromancer or Paladin.
            click(540, 408)
            capture("creation-submitted.png")
            wait_for("Login step: CharacterCreationDone")
            # World initialization follows the protocol transition; leave time for its first frame.
            time.sleep(2)
            if portraits:
                from check_portrait_dialogue_ui import arrival
                arrival(window, click, profile, selected_portrait)
                return
            click(130, 419)  # Dismiss the private New Haven arrival greeting.
            capture("creation-world.png")
            if os.environ.get("SANCTUARY_TEST_IOLO_UI") == "1":
                from check_iolo_ui import exercise
                exercise(window, click, profile)
                return
            print("Actual client: appearance, name, class, and world entry passed.", flush=True)
            if os.environ.get("SANCTUARY_TEST_DOCK_UI") == "1":
                from check_combat_dock_ui import exercise
                exercise(window, click, profile)
                return
            if os.environ.get("SANCTUARY_TEST_MENUS_UI") == "1":
                from check_sanctuary_menus_ui import exercise
                exercise(window, click, profile)
                return
            if os.environ.get("SANCTUARY_TEST_SOCIAL_GUMPS_UI") == "1":
                from check_social_gumps_ui import exercise
                exercise(window, click)
                return
            if combat:
                os.environ["SANCTUARY_TEST_CLIENT_PID"] = str(process.pid)
                os.environ["SANCTUARY_TEST_PROFILE"] = str(profile)
                from check_combat_ui import exercise
                exercise(window, click, capture)
                if os.environ.get("SANCTUARY_TEST_MERCENARY_CARE_UI") == "1" or os.environ.get("SANCTUARY_TEST_GAMBLING_UI") == "1" or os.environ.get("SANCTUARY_TEST_ESCROW_UI") == "1" or os.environ.get("SANCTUARY_TEST_INSURANCE_UI") == "1" or os.environ.get("SANCTUARY_TEST_HOUSING_UI") == "1" or os.environ.get("SANCTUARY_TEST_ALLIED_OVERLAP_UI") == "1" or os.environ.get("SANCTUARY_TEST_INSCRIPTION_UI") == "1" or os.environ.get("SANCTUARY_TEST_TELEPORT_UI") == "1" or os.environ.get("SANCTUARY_TEST_SURVEYS_UI") == "1" or os.environ.get("SANCTUARY_TEST_CARPENTRY_UI") == "1" or os.environ.get("SANCTUARY_TEST_MEALS_UI") == "1" or os.environ.get("SANCTUARY_TEST_COMBAT_POTIONS_UI") == "1" or os.environ.get("SANCTUARY_TEST_ALCHEMY_UI") == "1" or os.environ.get("SANCTUARY_TEST_GEM_SOCKETS_UI") == "1" or os.environ.get("SANCTUARY_TEST_RUNES_UI") == "1" or os.environ.get("SANCTUARY_TEST_RUNEBARK_UI") == "1" or os.environ.get("SANCTUARY_TEST_MOULDS_UI") == "1" or os.environ.get("SANCTUARY_TEST_SOCKETS_UI") == "1" or os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") == "1" or os.environ.get("SANCTUARY_TEST_IMBUING_UI") == "1" or os.environ.get("SANCTUARY_TEST_SALVAGE_UI") == "1" or os.environ.get("SANCTUARY_TEST_WARDROBE_UI") == "1" or os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") == "1" or os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1" or os.environ.get("SANCTUARY_TEST_MARKET_UI") == "1" or os.environ.get("SANCTUARY_TEST_CRAFTING_UI") == "1" or os.environ.get("SANCTUARY_TEST_PROFESSIONS_UI") == "1" or os.environ.get("SANCTUARY_TEST_ECONOMY_UI") == "1" or os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1" or os.environ.get("SANCTUARY_TEST_BELT_UI") == "1":
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
                click(150, 453)  # Paperdoll Class.
                capture("paladin-talent-trees.png")
                click(77, 150)  # Draft Prayer.
                click(551, 442)  # Apply the allocation.
                capture("talents-learned.png")
                click(568, 61)  # Close the Class window.
                managed_skills = os.environ.get("SANCTUARY_TEST_TRADE_SKILLS_UI") == "1"
                if managed_skills:
                    log = Path(os.environ["SANCTUARY_TEST_LOG"])

                    def command(name):
                        offset = log.stat().st_size
                        os.write(int(console), (f"skills-ui-{name}\n").encode())
                        deadline = time.monotonic() + 5
                        while time.monotonic() < deadline:
                            output = log.read_text(errors="replace")[offset:]
                            assert f"skills-ui-{name}: failed" not in output, output
                            if f"skills-ui-{name}: passed" in output:
                                return
                            time.sleep(0.05)
                        raise AssertionError(f"Missing skills-ui-{name}")

                    command("setup")

                click(208, 453)  # Paperdoll Skills.
                geometry = subprocess.check_output(["xdotool", "getwindowgeometry", "--shell", window], text=True)
                bounds = dict(line.split("=", 1) for line in geometry.splitlines() if "=" in line)
                wx, wy, ww, wh = (int(bounds[key]) for key in ("X", "Y", "WIDTH", "HEIGHT"))
                x, y = max(0, (ww - 532) // 2), max(0, (wh - 364) // 2)

                def capture_sheet(name):
                    time.sleep(0.9)  # Give the rendered client time for one snapshot.
                    sheet = ImageGrab.grab(bbox=(wx + x, wy + y, wx + x + 532, wy + y + 364),
                                           xdisplay=os.environ["DISPLAY"])
                    sheet.save(logs / name)
                    return sheet

                first = capture_sheet("trade-skills.png")
                if managed_skills:
                    command("choose")
                    chosen = capture_sheet("trade-skills-chosen.png")
                    command("unlock")
                    unlocked = capture_sheet("trade-skills-unlocked.png")
                    command("switch")
                    switched = capture_sheet("trade-skills-switched.png")

                    def changed(before, after, area):
                        return ImageChops.difference(before.crop(area), after.crop(area)).getbbox() is not None

                    assert changed(first, chosen, (26, 92, 244, 180)), "Confirmed craft did not join active skills"
                    assert changed(chosen, unlocked, (26, 307, 500, 329)), "Wardrobe hint did not update"
                    assert changed(unlocked, switched, (26, 92, 244, 180)), "Active craft did not refresh after switching"
                else:
                    assert first.getpixel((25, 95)) != first.getpixel((25, 171)), "Gathering rows did not render"

                # Saving the desktop confirms the normal Skills action opened one full gump.
                subprocess.run(["xdotool", "key", "--window", window, "ctrl+alt+shift+s"], check=True)
                wait_for("Saving done!")
                desktop = ET.parse(profile / "gumps.xml")
                skills = desktop.findall(".//gump[@type='10']")
                assert len(skills) == 1 and int(skills[0].attrib["x"]) == x and int(skills[0].attrib["y"]) == y
                assert not desktop.findall(".//gump[@type='16']"), "The sheet created a skill shortcut"
                click(x + 477, y + 25)
                print("Actual client: Class allocation and the unified Skills sheet passed.", flush=True)
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
