#!/usr/bin/env python3
"""Pack both supplied flare sheets as ClassicUO loose statics."""

from itertools import groupby
from pathlib import Path
from struct import pack

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
FIRST_GRAPHIC = 65000  # FlareGraphics.RedFlight.
FLIGHT_FRAMES = 16
BURST_FRAMES = 7
MINIMUM_BOTTOM_MARGIN = 36


def extract_frames(sheet, size, rows, count, key, origin):
    cells = []
    bounds = []
    for index in range(count):
        row, column = divmod(index, 8)
        x, y = 1 + column * (size + 1), rows[row]
        cell = sheet.crop((x, y, x + size, y + size))
        mask = Image.new("L", cell.size)
        mask.putdata([255 if color != key else 0 for color in cell.getdata()])
        extent = mask.getbbox()
        if extent is None:
            raise ValueError(f"No flare art in frame {index} at {(x, y)}")
        cells.append(cell)
        bounds.append(extent)

    cx, cy = origin
    left = min(box[0] for box in bounds)
    right = max(box[2] for box in bounds)
    top = min(box[1] for box in bounds)
    bottom = max(box[3] for box in bounds)
    # Keep the bright core aligned as the flare changes shape. The client
    # centers effect art horizontally and anchors it at the bottom.
    half_width = max(cx - left, right - cx)
    above = cy - top
    below = max(bottom - cy, MINIMUM_BOTTOM_MARGIN)
    width, height = 2 * half_width, above + below
    for cell in cells:
        canvas = Image.new("RGB", (width, height), key)
        canvas.paste(cell, (half_width - cx, above - cy))
        colors = [
            0 if (red, green, blue) == key
            else max(1, (red >> 3) << 10 | (green >> 3) << 5 | (blue >> 3))
            for red, green, blue in canvas.getdata()
        ]
        yield encode_static(width, height, colors)


def encode_static(width, height, colors):
    offsets = []
    rows = []
    for y in range(height):
        offsets.append(len(rows))
        x = 0
        end = 0
        for opaque, run in groupby(colors[y * width:(y + 1) * width], bool):
            pixels = list(run)
            if opaque:
                rows.extend((x - end, len(pixels), *pixels))
                end = x + len(pixels)
            x += len(pixels)
        rows.extend((0, 0))
    return pack(f"<Ihh{len(offsets) + len(rows)}H", 0, width, height, *offsets, *rows)


def main():
    with Image.open(ROOT / "flare_red.png") as source:
        red = source.convert("RGB")
    with Image.open(ROOT / "flare_blue.png") as source:
        blue = source.convert("RGB")
    if red.size != (1033, 416) or blue.size != (904, 324):
        raise ValueError("Expected the supplied red and blue flare sheets")

    red_key, blue_key = (82, 79, 96), (121, 76, 68)
    sequences = (
        extract_frames(red, 128, (15, 144), FLIGHT_FRAMES, red_key, (71, 76)),
        extract_frames(blue, 96, (1, 98), FLIGHT_FRAMES, blue_key, (54, 58)),
        extract_frames(red, 128, (287,), BURST_FRAMES, red_key, (64, 81)),
        extract_frames(blue, 128, (195,), BURST_FRAMES, blue_key, (64, 81)),
    )
    target = ROOT / "art" / "succubus" / "statics"
    target.mkdir(parents=True, exist_ok=True)
    graphic = FIRST_GRAPHIC
    for sequence in sequences:
        for frame in sequence:
            (target / f"{graphic}.art").write_bytes(frame)
            graphic += 1


if __name__ == "__main__":
    main()
