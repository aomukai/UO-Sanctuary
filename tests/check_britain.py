#!/usr/bin/env python3
"""Integration check using an isolated, disposable world and the installed assets."""
import json
import os
from pathlib import Path
import pty
import re
import shutil
import socket
import struct
import subprocess
import tempfile
import termios
import time

ROOT = Path(__file__).resolve().parents[1]
SERVER = Path(os.environ.get("SANCTUARY_TEST_SERVER", ROOT / ".local/server"))
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")
STATUS = re.compile(r"Britain baseline status: doors=(\d+), spawners=(\d+), residents=(\d+), "
                    r"bankers=(\d+), provisioners=(\d+); serials=([\d,]+)")


class Session:
    def __init__(self, folder, log):
        self.master, slave = pty.openpty()
        flags = termios.tcgetattr(slave)
        flags[3] &= ~(termios.ECHO | termios.ECHONL)
        termios.tcsetattr(slave, termios.TCSANOW, flags)
        self.offset = os.fstat(log.fileno()).st_size
        self.process = subprocess.Popen(
            [str(ROOT / ".tools/dotnet/dotnet"), str(folder / "ModernUO.dll")],
            cwd=folder, stdin=slave, stdout=log, stderr=subprocess.STDOUT, start_new_session=True,
            env={**os.environ, "DOTNET_ROOT": str(ROOT / ".tools/dotnet")},
        )
        os.close(slave)
        self.buffer = ""
        self.log = log

    def expect(self, pattern, timeout=60):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            match = re.search(pattern, self.buffer)
            if match:
                self.buffer = self.buffer[match.end():]
                return match
            content = Path(self.log.name).read_bytes()
            chunk = content[self.offset:].decode(errors="replace")
            self.offset = len(content)
            self.buffer += ANSI.sub("", chunk)
            if not chunk and self.process.poll() is not None:
                break
            time.sleep(0.1)
        raise AssertionError(f"Server did not reach {pattern!r}. Last output:\n{self.buffer[-3500:]}")

    def send(self, command):
        os.write(self.master, (command + "\n").encode())

    def close(self):
        if self.process.poll() is None:
            self.process.terminate()
            try:
                self.process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait()
        os.close(self.master)


def check():
    creation = os.environ.get("SANCTUARY_TEST_CREATION") == "1"
    professions = os.environ.get("SANCTUARY_TEST_PROFESSIONS") == "1"
    with tempfile.TemporaryDirectory(prefix="sanctuary-britain-check-", dir="/dev/shm") as temp:
        folder = Path(temp)
        for entry in SERVER.iterdir():
            if entry.is_file() and entry.suffix in (".dll", ".json", ".config"):
                shutil.copy2(entry, folder / entry.name)
        (folder / "runtimes").symlink_to(SERVER / "runtimes", target_is_directory=True)
        shutil.copytree(ROOT / ".local/server/Configuration", folder / "Configuration")
        for name in ("Assemblies", "Data"):
            destination = folder / name
            destination.mkdir()
            for entry in (SERVER / name).iterdir():
                if entry.name not in ("Sanctuary.Content.dll", "Sanctuary", "assemblies.json"):
                    (destination / entry.name).symlink_to(entry, target_is_directory=entry.is_dir())
        (folder / "Assemblies/Sanctuary.Content.dll").symlink_to(ROOT / ".local/content/Sanctuary.Content.dll")
        shutil.copytree(ROOT / "data/Sanctuary", folder / "Data/Sanctuary")
        assemblies = json.loads((SERVER / "Data/assemblies.json").read_text())
        if "Sanctuary.Content.dll" not in assemblies:
            assemblies.append("Sanctuary.Content.dll")
        if creation:
            probe = ROOT / ".local/integration/Sanctuary.Integration.dll"
            (folder / "Assemblies/Sanctuary.Integration.dll").symlink_to(probe)
            assemblies.append("Sanctuary.Integration.dll")
        (folder / "Data/assemblies.json").write_text(json.dumps(assemblies))
        config_path = folder / "Configuration/modernuo.json"
        config = json.loads(config_path.read_text())
        with socket.socket() as reservation:
            reservation.bind(("127.0.0.1", 0))
            port = reservation.getsockname()[1]
        with socket.socket(type=socket.SOCK_DGRAM) as reservation:
            reservation.bind(("127.0.0.1", 0))
            ping_port = reservation.getsockname()[1]
        config["listeners"] = [f"127.0.0.1:{port}"]
        config["settings"].update({"autosave.enabled": "False", "pingServer.enabled": "True",
                                    "pingServer.port": str(ping_port),
                                    "sanctuary.graveyard.enabled": "False", "world.savePath": "Saves", "crashGuard.restartServer": "False"})
        config["settings"]["sanctuary.professions.enabled"] = "True" if professions else "False"
        if professions:
            # Registered facilities exist only in this disposable fixture world.
            facility_path = folder / "Data/Sanctuary/Balance/craft-facilities.json"
            sites = json.loads(facility_path.read_text()) + [{"Skill": skill, "Map": "Trammel", "X": x, "Y": y, "Z": 0, "ItemID": 0xB4A}
                     for skill in (0, 7, 8, 11, 12, 13, 23, 34, 37, 56)
                     for x, y in ((5550, 1150), (5450, 1160))]
            facility_path.write_text(json.dumps(sites))
        if creation:
            config["settings"]["accountHandler.enableAutoAccountCreation"] = "True"
            config["settings"]["accountHandler.maxAccountsPerIP"] = "50"
        config_path.write_text(json.dumps(config))
        logfile = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
        with logfile.open("w") as log:
            signatures = []
            for iteration in range(4 if creation else 2):
                session = Session(folder, log)
                try:
                    if iteration == 0 or not creation:
                        session.expect(r"create the owner account now\? \(y/n\):")
                        session.send("n")
                    session.expect(f"Listening: 127.0.0.1:{port}")
                    session.send("sanctuary-status")
                    status = session.expect(STATUS).groups()
                    assert status[:5] == ("8", "3", "6", "2", "1"), status
                    signatures.append(status[5])
                    session.send("sanctuary-status")
                    assert session.expect(STATUS).groups() == status
                    if creation and iteration == 0 and os.environ.get("SANCTUARY_RECIPE_AUDIT"):
                        session.send("equipment-recipe-audit")
                        assert session.expect(r"equipment-recipe-audit: (passed|failed)").group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_DEATH_GOLD") == "1" and (professions or iteration < 2):
                        death_command = "death-gold-check" if iteration == 0 else "death-gold-reload-check"
                        session.send(death_command)
                        assert session.expect(death_command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_MOULDS") == "1":
                        command = "mould-check" if iteration == 0 else "mould-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_SOCKETS") == "1":
                        command = "socket-check" if iteration == 0 else "socket-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_DIFFICULTIES") == "1":
                        command = "difficulty-check" if iteration == 0 else "difficulty-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_GEAR_DROPS") == "1":
                        command = "gear-drop-check" if iteration == 0 else "gear-drop-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_IDENTIFICATION") == "1":
                        command = "identification-check" if iteration == 0 else "identification-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_IMBUING") == "1":
                        command = "imbuing-check" if iteration == 0 else "imbuing-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_SALVAGE") == "1":
                        command = "salvage-check" if iteration == 0 else "salvage-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_WARDROBE") == "1":
                        command = "wardrobe-check" if iteration == 0 else "wardrobe-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_RECOGNITION") == "1":
                        command = "recognition-check" if iteration == 0 else "recognition-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=60).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_REPAIRS") == "1":
                        repair_command = "repair-check" if iteration == 0 else "repair-reload-check"
                        session.send(repair_command)
                        assert session.expect(repair_command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_MARKETS") == "1":
                        market_command = "market-check" if iteration == 0 else "market-reload-check"
                        session.send(market_command)
                        assert session.expect(market_command + r": (passed|failed)", timeout=90).group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_REWARD_LIFE") == "1":
                        reward_command = "reward-life-check" if iteration == 0 else "reward-life-reload-check"
                        session.send(reward_command)
                        assert session.expect(reward_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                    if professions:
                        if os.environ.get("SANCTUARY_TEST_EFFECTIVE_GEAR") == "1":
                            effective_command = "effective-gear-check" if iteration == 0 else "effective-gear-reload-check"
                            session.send(effective_command)
                            assert session.expect(effective_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                        command = "profession-check" if iteration == 0 else "profession-reload-check"
                        session.send(command)
                        assert session.expect(command + r": (passed|failed)", timeout=45).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_PROCESSING") == "1":
                            process_command = "processing-check" if iteration == 0 else "processing-reload-check"
                            session.send(process_command)
                            assert session.expect(process_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                            if iteration == 0:
                                session.send("processing-spin-check")
                                assert session.expect(r"processing-spin-check: (passed|failed)", timeout=15).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_EQUIPMENT_CRAFTING") == "1":
                            equipment_command = "equipment-crafting-check" if iteration == 0 else "equipment-crafting-reload-check"
                            session.send(equipment_command)
                            assert session.expect(equipment_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_CRAFTING") == "1":
                            craft_command = "crafting-check" if iteration == 0 else "crafting-reload-check"
                            session.send(craft_command)
                            assert session.expect(craft_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_FIRST_LOOP") == "1":
                            session.send("first-loop-catalogue-check")
                            assert session.expect(r"first-loop-catalogue-check: (passed|failed)").group(1) == "passed"
                        if iteration == 0 and (os.environ.get("SANCTUARY_TEST_MOULDS_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_SOCKETS_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_IMBUING_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_SALVAGE_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_WARDROBE_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_PROFESSIONS_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_CRAFTING_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_PROCESSING_UI") == "1"
                                               or os.environ.get("SANCTUARY_TEST_EQUIPMENT_CRAFTING_UI") == "1"):
                            subprocess.run(["xvfb-run", "-a", "python3", str(ROOT / "tests/check_combat_ui.py"), str(port)],
                                check=True, pass_fds=(session.master,),
                                env={**os.environ, "SANCTUARY_TEST_CONSOLE_FD": str(session.master)})
                            ui_final = "mould-ui-final" if os.environ.get("SANCTUARY_TEST_MOULDS_UI") == "1" else "socket-ui-final" if os.environ.get("SANCTUARY_TEST_SOCKETS_UI") == "1" else "gear-drop-ui-final" if os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") == "1" else "imbuing-ui-final" if os.environ.get("SANCTUARY_TEST_IMBUING_UI") == "1" else "salvage-ui-final" if os.environ.get("SANCTUARY_TEST_SALVAGE_UI") == "1" else "wardrobe-ui-final" if os.environ.get("SANCTUARY_TEST_WARDROBE_UI") == "1" else "recognition-ui-final" if os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") == "1" else "repair-ui-final" if os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1" else "equipment-crafting-ui-final" if os.environ.get("SANCTUARY_TEST_EQUIPMENT_CRAFTING_UI") == "1" else "processing-ui-final" if os.environ.get("SANCTUARY_TEST_PROCESSING_UI") == "1" else "crafting-ui-final" if os.environ.get("SANCTUARY_TEST_CRAFTING_UI") == "1" else "profession-ui-final"
                            session.send(ui_final)
                            assert session.expect(ui_final + r": (passed|failed)").group(1) == "passed"
                    if creation and not professions:
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_PROCESSING") == "1":
                            session.send("processing-comparison-check")
                            assert session.expect(r"processing-comparison-check: (passed|failed)").group(1) == "passed"
                        command = ["creation-check", "creation-reload-check", "creation-migration-check", "creation-migration-check"][iteration]
                        session.send(command)
                        result = session.expect(command + r": (passed|failed)").group(1)
                        assert result == "passed", f"{command} failed; inspect {logfile}"
                        if os.environ.get("SANCTUARY_TEST_ITEM_STATE") == "1":
                            state_command = ["item-state-check", "item-state-reload-check",
                                             "item-state-legacy-reload-check", "item-state-legacy-reload-check"][iteration]
                            session.send(state_command)
                            assert session.expect(state_command + r": (passed|failed)").group(1) == "passed"
                        if iteration < 2 and os.environ.get("SANCTUARY_TEST_CLASS_NAMES") == "1":
                            name_command = "class-names-check" if iteration == 0 else "class-names-reload-check"
                            session.send(name_command)
                            assert session.expect(name_command + r": (passed|failed)").group(1) == "passed"
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_FIRST_LOOP") == "1":
                            session.send("first-loop-catalogue-check")
                            assert session.expect(r"first-loop-catalogue-check: (passed|failed)").group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_CREATURE_XP") == "1":
                            # Later boots deliberately rewrite the class save into legacy fixtures
                            # containing only their seven original characters.
                            xp_command = ["creature-xp-check", "creature-xp-reload-check",
                                          "creature-xp-claims-check", "creature-xp-claims-check"][iteration]
                            session.send(xp_command)
                            assert session.expect(xp_command + r": (passed|failed)", timeout=45).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_BELT") == "1":
                            belt_command = "belt-check" if iteration == 0 else "belt-reload-check"
                            session.send(belt_command)
                            assert session.expect(belt_command + r": (passed|failed)", timeout=30).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_ECONOMY") == "1":
                            economy_command = "economy-check" if iteration == 0 else "economy-reload-check"
                            session.send(economy_command)
                            assert session.expect(economy_command + r": (passed|failed)", timeout=30).group(1) == "passed"
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_CREATURE_PROFILES") == "1":
                            session.send("creature-profile-check")
                            assert session.expect(r"creature-profile-check: (passed|failed)", timeout=30).group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_PETS") == "1" and iteration < 2:
                            pet_command = "pet-check" if iteration == 0 else "pet-reload-check"
                            session.send(pet_command)
                            assert session.expect(pet_command + r": (passed|failed)").group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_RESURRECTION") == "1":
                            healer_command = "resurrection-check" if iteration == 0 else "resurrection-reload-check"
                            session.send(healer_command)
                            assert session.expect(healer_command + r": (passed|failed)").group(1) == "passed"
                        if os.environ.get("SANCTUARY_TEST_MONSTERS") == "1" and iteration < 2:
                            if iteration == 0:
                                if os.environ.get("SANCTUARY_TEST_APPROACH") == "1":
                                    session.send("approach-check")
                                    assert session.expect(r"approach-check: (passed|failed)", timeout=30).group(1) == "passed"
                                session.send("normal-monster-check")
                                assert session.expect(r"normal-monster-check: (passed|failed)", timeout=30).group(1) == "passed"
                            monster_command = "monster-check" if iteration == 0 else "monster-reload-check"
                            session.send(monster_command)
                            assert session.expect(monster_command + r": (passed|failed)", timeout=60).group(1) == "passed"
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_COMBAT") == "1":
                            session.send("combat-check")
                            assert session.expect(r"combat-check: (passed|failed)").group(1) == "passed"
                        if iteration == 0 and os.environ.get("SANCTUARY_TEST_UI") == "1":
                            combat_ui = os.environ.get("SANCTUARY_TEST_COMBAT_UI") == "1"
                            script = "check_combat_ui.py" if combat_ui else "check_creation_ui.py"
                            subprocess.run(["xvfb-run", "-a", "python3", str(ROOT / "tests" / script),
                                            str(port)], check=True, pass_fds=(session.master,),
                                           env={**os.environ, "SANCTUARY_TEST_CONSOLE_FD": str(session.master)})
                            final = "repair-ui-final" if os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1" else "market-ui-final" if os.environ.get("SANCTUARY_TEST_MARKET_UI") == "1" else "class-names-ui-check" if os.environ.get("SANCTUARY_TEST_CLASS_NAMES_UI") == "1" else "economy-ui-final" if os.environ.get("SANCTUARY_TEST_ECONOMY_UI") == "1" else "belt-ui-final" if os.environ.get("SANCTUARY_TEST_BELT_UI") == "1" else "movement-ui-final" if os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1" else "resurrection-ui-final" if os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") == "1" else "pet-ui-final" if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1" else "stat-ui-final" if os.environ.get("SANCTUARY_TEST_STATS_UI") == "1" else "loot-ui-closed" if os.environ.get("SANCTUARY_TEST_LOOT_ONLY") == "1" or os.environ.get("SANCTUARY_TEST_TIMING_ONLY") == "1" else "combat-ui-final" if combat_ui else "creation-ui-check"
                            session.send(final)
                            assert session.expect(final + r": (passed|failed)").group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_COMBAT_UI") == "1" and iteration > 0 and os.environ.get("SANCTUARY_TEST_MOULDS_UI") != "1" and os.environ.get("SANCTUARY_TEST_SOCKETS_UI") != "1" and os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") != "1" and os.environ.get("SANCTUARY_TEST_IMBUING_UI") != "1" and os.environ.get("SANCTUARY_TEST_SALVAGE_UI") != "1" and os.environ.get("SANCTUARY_TEST_WARDROBE_UI") != "1" and os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") != "1" and os.environ.get("SANCTUARY_TEST_REPAIR_UI") != "1" and os.environ.get("SANCTUARY_TEST_MARKET_UI") != "1" and os.environ.get("SANCTUARY_TEST_ECONOMY_UI") != "1" and os.environ.get("SANCTUARY_TEST_BELT_UI") != "1" and os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") != "1" and os.environ.get("SANCTUARY_TEST_STATS_UI") != "1" and os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") != "1" and os.environ.get("SANCTUARY_TEST_PETS_UI") != "1":
                        session.send("loot-ui-reload")
                        assert session.expect(r"loot-ui-reload: (passed|failed)").group(1) == "passed"
                    if professions and iteration == 0 and os.environ.get("SANCTUARY_TEST_PROCESSING") == "1":
                        session.send("processing-pending-check")
                        assert session.expect(r"processing-pending-check: (passed|failed)").group(1) == "passed"
                    session.send("save")
                    session.expect("Writing world save snapshot done")
                    if creation and iteration == 1 and os.environ.get("SANCTUARY_TEST_ITEM_STATE") == "1":
                        session.send("item-state-write-legacy")
                        assert session.expect(r"item-state-write-legacy: (passed|failed)").group(1) == "passed"
                    session.send("shutdown")
                    session.expect("Shutting down", timeout=20)
                    session.process.wait(timeout=20)
                    assert session.process.returncode == 0, session.process.returncode
                finally:
                    session.close()
                print(f"Boot {iteration + 1}: 8 doors, 3 spawners, 6 residents; save and shutdown passed.")
                if creation and not professions and iteration in (1, 2):
                    serials = json.loads((folder / "creation-checks.json").read_text())[:7]
                    legacy = struct.pack("<ii", 1 if iteration == 1 else 0, 7)
                    for index, serial in enumerate(serials):
                        legacy += struct.pack("<IBiq", serial, 101 + index, 1, 0)
                        if iteration == 1:
                            legacy += struct.pack("<i", 0)
                    (folder / "Saves/SanctuaryCharacters/SanctuaryCharacters.bin").write_bytes(legacy)
            assert len(set(signatures)) == 1, "Door/spawner/NPC identities changed after restart"
        print(f"Save/restart preserved all door, spawner, and resident serials. Log: {logfile}")


if __name__ == "__main__":
    check()
