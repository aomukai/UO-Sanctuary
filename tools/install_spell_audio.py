#!/usr/bin/env python3
"""Render the selected Sonniss cues for the local ClassicUO Sounds folder."""

import argparse
from array import array
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import wave


ROOT = Path(__file__).resolve().parents[1]
SAMPLE_RATE = 22050


def render(source, cue):
    fade_out_start = cue["duration"] - cue["fadeOut"]
    filters = ",".join((
        f"highpass=f={cue['highpass']}",
        f"afade=t=in:d={cue['fadeIn']}",
        f"afade=t=out:st={fade_out_start:.6f}:d={cue['fadeOut']}",
    ))
    raw = subprocess.check_output([
        "ffmpeg", "-hide_banner", "-loglevel", "error", "-ss", str(cue["start"]), "-i", str(source),
        "-t", str(cue["duration"]), "-af", filters, "-ac", "1", "-ar", str(SAMPLE_RATE),
        "-c:a", "pcm_f32le", "-f", "f32le", "-",
    ])
    samples = array("f")
    samples.frombytes(raw)
    if sys.byteorder != "little":
        samples.byteswap()
    peak = max(map(abs, samples), default=0)
    if abs(len(samples) - round(cue["duration"] * SAMPLE_RATE)) > 1 or peak < 0.0001:
        raise ValueError(f"Unexpected duration or silence in {source.name}")
    scale = 32767 * 10 ** (cue["peakDb"] / 20) / peak
    result = array("h", (round(sample * scale) for sample in samples))
    if sys.byteorder != "little":
        result.byteswap()
    return result.tobytes()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", type=Path, default=ROOT / ".local/spell-effects/sonniss/sources")
    parser.add_argument("--uo-data", type=Path, default=ROOT / ".local/uo-data")
    args = parser.parse_args()
    spec = json.loads((ROOT / "art/spell-effects/sonniss-cues.json").read_text())
    sound_ids = dict(re.findall(r"public const int (\w+) = (\d+);",
                                (ROOT / "shared/SpellEffectSounds.cs").read_text()))
    if not args.uo_data.is_dir():
        parser.error(f"UO data directory does not exist: {args.uo_data}")
    sources = {}
    for name, source in spec["sources"].items():
        path = args.sources / source["filename"]
        if not path.is_file() or hashlib.sha256(path.read_bytes()).hexdigest() != source["sha256"]:
            parser.error(f"Expected the original Sonniss 2019 part {source['part']} recording at {path}")
        sources[name] = path
    cues = {sound_ids[name]: render(sources[cue["source"]], cue) for name, cue in spec["cues"].items()}
    output = args.uo_data / "Sounds"
    output.mkdir(exist_ok=True)
    for index, samples in cues.items():
        path = output / f"{index}.wav"
        temporary = path.with_suffix(".wav.pending")
        with wave.open(str(temporary), "wb") as stream:
            stream.setparams((1, 2, SAMPLE_RATE, 0, "NONE", "not compressed"))
            stream.writeframes(samples)
        temporary.replace(path)
    print(f"Installed {len(cues)} spell cues in {output}")


if __name__ == "__main__":
    main()
