"""Render the original Ultima Online MIDI soundtrack into the local client data."""

import hashlib
from io import BytesIO
from pathlib import Path
import shutil
import subprocess
import tempfile
from urllib.request import Request, urlopen
from zipfile import ZipFile


SOURCE = "https://www.uoguide.com/images2/UO_MIDI_Music.zip"
SOURCE_SHA256 = "9924fd04d323924da26b9d0a99651b5e543e0e937d69250380443b5f9f5b90a9"
TRACKS = """
    OLDULT01 CREATE1 DRAGFLIT OLDULT02 OLDULT03 OLDULT04 OLDULT05 OLDULT06
    STONES2 BRITAIN1 BRITAIN2 BUCSDEN JHELOM LBCASTLE LINELLE MAGINCIA MINOC
    OCLLO SAMLETHE SERPENTS SKARABRA TRINSIC VESPER WIND YEW CAVE01 DUNGEON9
    FOREST_A INTOWN01 JUNGLE_A MOUNTN_A PLAINS_A SAILING SWAMP_A TAVERN01
    TAVERN02 TAVERN03 TAVERN04 COMBAT1 COMBAT2 COMBAT3 APPROACH DEATH VICTORY
    BTCASTLE NUJELM DUNGEON2 COVE MOONGLOW
""".split()
DEFAULT_SOUNDFONT = Path("/usr/share/sounds/sf2/default-GM.sf2")


def install(data_directory, archive=None, soundfont=None):
    fluidsynth, ffmpeg = shutil.which("fluidsynth"), shutil.which("ffmpeg")
    soundfont = Path(soundfont).expanduser().resolve() if soundfont else DEFAULT_SOUNDFONT
    if not (fluidsynth and ffmpeg and soundfont.is_file()):
        raise ValueError("Classic music needs FluidSynth, FFmpeg and a General MIDI soundfont. Use --soundfont to select one.")

    if archive:
        source = Path(archive).expanduser().read_bytes()
    else:
        request = Request(SOURCE, headers={"User-Agent": "Sanctuary Classic Client Data Import"})
        with urlopen(request, timeout=30) as response:
            source = response.read(2_000_000)
        if hashlib.sha256(source).hexdigest() != SOURCE_SHA256:
            raise ValueError("The classic MIDI archive differs from the verified download. Supply a local archive with --archive.")

    destination = data_directory / "Music" / "Classic"
    destination.mkdir(parents=True, exist_ok=True)
    installed = 0
    with ZipFile(BytesIO(source)) as tracks, tempfile.TemporaryDirectory(dir=destination) as work:
        work = Path(work)
        entries = {Path(entry.filename).name.upper(): entry for entry in tracks.infolist() if not entry.is_dir()}
        for index, name in enumerate(TRACKS):
            output = destination / f"{index}.mp3"
            entry = entries.get(f"{name}.MID")
            if output.exists() or entry is None:
                continue
            if entry.file_size > 1_000_000:
                raise ValueError(f"Unexpectedly large MIDI file: {name}.")
            midi_bytes = tracks.read(entry)
            if not midi_bytes.startswith(b"MThd"):
                raise ValueError(f"Invalid MIDI file: {name}.")
            midi, wav, mp3 = (work / f"{index}.{extension}" for extension in ("mid", "wav", "mp3"))
            midi.write_bytes(midi_bytes)
            subprocess.run([fluidsynth, "-niq", "-r", "44100", "-F", wav, soundfont, midi],
                           check=True, stdout=subprocess.DEVNULL)
            subprocess.run([ffmpeg, "-hide_banner", "-loglevel", "error", "-i", wav, "-ac", "2", "-ar", "44100",
                            "-q:a", "4", mp3], check=True)
            mp3.replace(output)
            midi.unlink()
            wav.unlink()
            installed += 1

        # ClassicUO uses Stones for the original login cue and index 78 for modern clients.
        stones, login = destination / "8.mp3", destination / "78.mp3"
        if stones.exists() and not login.exists():
            pending = work / login.name
            shutil.copyfile(stones, pending)
            pending.replace(login)
            installed += 1
    print(f"Installed {installed} classic recordings in {destination}.")
