#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Register the painted Thing roof to the two UO tile axes and encode static art."""

from pathlib import Path

from PIL import Image

from build_succubus_bolt import encode_static

ART = Path(__file__).resolve().parents[1] / "art/world/dagger-isle"
GRAPHICS = range(59987, 59991)
WIDTH, HEIGHT = 656, 720
TILE = 22
EAVE_X, EAVE_Y = WIDTH / 2 + 8, HEIGHT - 27
LENGTH, BREADTH = 13, 8
# Painted junctions of the left eave, near gable and right eave respectively.
WEST, FRONT, EAST = (56, 489), (954, 958), (1462, 757)


def main():
    # Centre the near gable on the front corner tile. Undo the source camera's
    # foreshortening to put the eaves exactly on the 45-degree tile axes.
    coefficients = []
    for west, front, east in zip(WEST, FRONT, EAST):
        along = (front - west) / (LENGTH * TILE)
        across = (east - front) / (BREADTH * TILE)
        dx, dy = (along + across) / 2, (along - across) / 2
        coefficients.extend((dx, dy, front - dx * EAVE_X - dy * EAVE_Y))

    with Image.open(ART / "thing-roof-source.png") as source:
        roof = source.transform((WIDTH, HEIGHT), Image.Transform.AFFINE, coefficients,
                                Image.Resampling.BICUBIC)
    pixels = list(roof.getdata())
    counts = [sum(pixels[y * WIDTH + x][3] >= 150 for y in range(HEIGHT)) for x in range(WIDTH)]
    total = sum(counts)
    boundaries = [0]
    running = 0
    for x, count in enumerate(counts):
        running += count
        if len(boundaries) < len(GRAPHICS) and running >= total * len(boundaries) / len(GRAPHICS):
            boundaries.append(x + 1)
    boundaries.append(WIDTH)
    for column, graphic in enumerate(GRAPHICS):
        start, end = boundaries[column:column + 2]
        # Each slice uses the same anchor. Splitting the raster keeps the
        # per-row word offsets in the legacy .art format within 16 bits.
        colors = [
            max(1, (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3))
            if start <= index % WIDTH < end and a >= 150 else 0
            for index, (r, g, b, a) in enumerate(pixels)
        ]
        (ART / "statics" / f"{graphic}.art").write_bytes(encode_static(WIDTH, HEIGHT, colors))


if __name__ == "__main__":
    main()
