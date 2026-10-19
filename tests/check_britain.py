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
        (folder / "Data/Sanctuary").symlink_to(ROOT / "data/Sanctuary", target_is_directory=True)
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
        if creation:
            config["settings"]["accountHandler.enableAutoAccountCreation"] = "True"
            config["settings"]["accountHandler.maxAccountsPerIP"] = "50"
        config_path.write_text(json.dumps(config))
        logfile = ROOT / ".local/logs/britain-baseline-check.log"
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
                    if creation:
                        command = ["creation-check", "creation-reload-check", "creation-migration-check", "creation-migration-check"][iteration]
                        session.send(command)
                        result = session.expect(command + r": (passed|failed)").group(1)
                        assert result == "passed", f"{command} failed; inspect {logfile}"
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
                            final = "class-names-ui-check" if os.environ.get("SANCTUARY_TEST_CLASS_NAMES_UI") == "1" else "economy-ui-final" if os.environ.get("SANCTUARY_TEST_ECONOMY_UI") == "1" else "belt-ui-final" if os.environ.get("SANCTUARY_TEST_BELT_UI") == "1" else "movement-ui-final" if os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1" else "resurrection-ui-final" if os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") == "1" else "pet-ui-final" if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1" else "stat-ui-final" if os.environ.get("SANCTUARY_TEST_STATS_UI") == "1" else "loot-ui-closed" if os.environ.get("SANCTUARY_TEST_LOOT_ONLY") == "1" or os.environ.get("SANCTUARY_TEST_TIMING_ONLY") == "1" else "combat-ui-final" if combat_ui else "creation-ui-check"
                            session.send(final)
                            assert session.expect(final + r": (passed|failed)").group(1) == "passed"
                    if creation and os.environ.get("SANCTUARY_TEST_COMBAT_UI") == "1" and iteration > 0 and os.environ.get("SANCTUARY_TEST_ECONOMY_UI") != "1" and os.environ.get("SANCTUARY_TEST_BELT_UI") != "1" and os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") != "1" and os.environ.get("SANCTUARY_TEST_STATS_UI") != "1" and os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") != "1" and os.environ.get("SANCTUARY_TEST_PETS_UI") != "1":
                        session.send("loot-ui-reload")
                        assert session.expect(r"loot-ui-reload: (passed|failed)").group(1) == "passed"
                    session.send("save")
                    session.expect("Writing world save snapshot done")
                    session.send("shutdown")
                    session.expect("Shutting down", timeout=20)
                    session.process.wait(timeout=20)
                    assert session.process.returncode == 0, session.process.returncode
                finally:
                    session.close()
                print(f"Boot {iteration + 1}: 8 doors, 3 spawners, 6 residents; save and shutdown passed.")
                if creation and iteration in (1, 2):
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
