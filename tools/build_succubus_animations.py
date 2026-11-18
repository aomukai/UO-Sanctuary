#!/usr/bin/env python3
"""Convert the supplied succubus sheet to ClassicUO's monster animation format."""

from itertools import groupby
from pathlib import Path
from struct import pack

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
CELL = 128
ROW_STRIDE = CELL + 1
ANCHOR_X, ANCHOR_Y = 64, 114
CHROMA_KEY = (255, 0, 255)

# Each strip starts with the south-facing view, then turns clockwise through
# southwest, west, northwest and north. ClassicUO mirrors the other three views.
STRIPS = {
    "death": (0, 7, 24),
    "attack": (0, 1045, 16),
    "hit": (2049, 1045, 7),
    "idle": (0, 2083, 14),
    "walk": (1793, 2083, 8),
}

# Ordered like ClassicUO's HighAnimationGroup, with the supplied sequences
# shared by slots for which the sheet has no dedicated animation.
ACTIONS = (
    "walk", "idle", "death", "death",                    # Walk, Stand, Die1–2
    "attack", "attack", "attack",                       # Attack1–3
    "idle", "idle", "idle", "hit", "attack",             # Misc1–3, Stumble, SlapGround
    "attack", "hit", "idle", "hit", "hit",               # Cast, GetHit1, Misc4, GetHit2–3
    "idle", "idle", "walk", "idle", "death",             # Fidget1–2, Fly, Land, DieInFlight
)


def encode_strip(sheet, strip, direction):
    left, top, count = STRIPS[strip]
    top += direction * ROW_STRIDE
    strip_image = sheet.crop((left, top, left + count * CELL, top + CELL))
    indexed = strip_image.quantize(colors=256, dither=Image.Dither.NONE)
    colors = indexed.getpalette()
    palette = [
        (colors[i] >> 3) << 10 | (colors[i + 1] >> 3) << 5 | (colors[i + 2] >> 3)
        for i in range(0, len(colors), 3)
    ]

    frames = []
    for frame in range(count):
        box = (frame * CELL, 0, (frame + 1) * CELL, CELL)
        mask = Image.new("L", (CELL, CELL))
        mask.putdata([0 if color == CHROMA_KEY else 255 for color in strip_image.crop(box).getdata()])
        bounds = mask.getbbox()
        if bounds is None:
            raise ValueError(f"Empty {strip} frame {frame} in direction {direction}")

        x0, y0, x1, y1 = bounds
        width, height = x1 - x0, y1 - y0
        encoded = bytearray(pack("<hhhh", ANCHOR_X - x0, ANCHOR_Y - y1, width, height))
        pixels = indexed.crop(box).load()
        opaque = mask.load()
        for y in range(y0, y1):
            for present, run in groupby(range(x0, x1), key=lambda x: opaque[x, y]):
                if present:
                    start, *rest = run
                    length = 1 + len(rest)
                    # MUL runs store signed offsets relative to the common ground anchor.
                    header = ((start - ANCHOR_X) & 0x3FF) << 22 | ((y - ANCHOR_Y) & 0x3FF) << 12 | length
                    encoded.extend(pack("<I", header))
                    encoded.extend(pixels[x, y] for x in range(start, start + length))
        encoded.extend(pack("<I", 0x7FFF7FFF))
        frames.append(encoded)

    offsets = []
    offset = 4 * (1 + count)
    for frame in frames:
        offsets.append(offset)
        offset += len(frame)
    palette.extend([0] * (256 - len(palette)))
    return pack("<256H", *palette) + pack(f"<I{count}I", count, *offsets) + b"".join(frames)


def main():
    with Image.open(ROOT / "succubus.png") as source:
        if source.size != (3072, 3114):
            raise ValueError("Expected the supplied 3072 × 3114 succubus sheet")
        sheet = source.convert("RGB")

    data = bytearray()
    entries = {}
    for strip in STRIPS:
        for direction in range(5):
            block = encode_strip(sheet, strip, direction)
            entries[strip, direction] = pack("<III", len(data), len(block), 0)
            data.extend(block)

    target = ROOT / "art" / "succubus"
    target.mkdir(parents=True, exist_ok=True)
    (target / "succubus.idx").write_bytes(b"".join(
        entries[strip, direction] for strip in ACTIONS for direction in range(5)
    ))
    (target / "succubus.mul").write_bytes(data)


if __name__ == "__main__":
    main()
