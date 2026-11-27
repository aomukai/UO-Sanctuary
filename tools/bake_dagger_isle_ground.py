#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Bake one north-up material overlay for the Dagger Isle terrain mesh."""
from pathlib import Path
from random import Random

from PIL import Image, ImageChops, ImageDraw, ImageEnhance, ImageFilter, ImageOps

ART = Path(__file__).resolve().parents[1] / "art/world/dagger-isle"
ORIGIN = (3976, 546)
SCALE = 32
SIZE = (64 * SCALE, 48 * SCALE)


def pixel(x, y):
    return (round((x - ORIGIN[0]) * SCALE), round((y - ORIGIN[1]) * SCALE))


def bake():
    material = Image.open(ART / "materials.png").convert("RGB")
    half = material.width // 2

    def cover(quadrant):
        left = quadrant * half
        patch = material.crop((left + 6, 6, left + half - 6, half - 6)).resize(
            (320, 320), Image.Resampling.LANCZOS)
        result = Image.new("RGB", SIZE)
        for row, y in enumerate(range(0, SIZE[1], patch.height)):
            strip = ImageOps.flip(patch) if row % 2 else patch
            for col, x in enumerate(range(0, SIZE[0], patch.width)):
                result.paste(ImageOps.mirror(strip) if col % 2 else strip, (x, y))
        return result

    rng = Random(3980572)

    def noise(spacing):
        size = tuple(max(2, side // spacing) for side in SIZE)
        return Image.frombytes("L", size, rng.randbytes(size[0] * size[1])).resize(
            SIZE, Image.Resampling.BICUBIC)

    coarse = noise(63)
    grain = Image.blend(noise(14), noise(2), .36)
    drift = Image.blend(coarse, grain, .39)
    road = Image.new("L", SIZE)
    brush = ImageDraw.Draw(road)

    def trail(points, radius):
        coords = [pixel(*point) for point in points]
        r = round(radius * SCALE)
        brush.line(coords, fill=255, width=2 * r, joint="curve")
        for x, y in coords:
            brush.ellipse((x-r, y-r, x+r, y+r), fill=255)

    # A wide landing, then a worn lane which curves into the assembly porch.
    trail([(3980,572.3), (3985,572), (3990,571.8), (3995,572.2), (4000,572.7),
           (4005,572.2), (4011,571.7), (4016,571.6), (4019,570.4), (4021,568.4),
           (4021.5,566)], 1.45)
    trail([(3984.8,572), (3987.2,571.3), (3989.1,570.5)], 2.25)
    trail([(3987,572), (3989,570.2), (3990.5,568), (3990.5,566)], 1.15)
    trail([(4000.4,572.4), (4002.3,571.2), (4003.5,569.4), (4003.5,566)], 1.0)
    trail([(4000,572.3), (3999.2,574.8), (3999.2,578.4), (3999.5,581.5),
           (3998.7,584.3), (3997.4,585.4), (3995.8,585.5), (3994.5,584)], .85)
    trail([(3999.2,575), (4001.1,576.1), (4004.4,578.5)], .6)

    wear = road.filter(ImageFilter.GaussianBlur(16))
    wear = ImageChops.add(wear, grain.point(lambda v: (v-128)*.58+128), offset=-128)
    wear = wear.point(lambda v: max(0, min(255, (v-25)*1.28)))

    wear = ImageChops.multiply(wear, road.filter(ImageFilter.GaussianBlur(27)).point(lambda v: min(255, v*3)))

    # A soft irregular perimeter lets the original snowy coast and hillside show through.
    pasture = Image.new("L", SIZE)
    outline = [(3982,553), (3994,551), (4014,551), (4034,552), (4036,566),
               (4034,583), (4028,587), (4014,587), (4001,585), (3990,587),
               (3986,580), (3982,574), (3980,567)]
    ImageDraw.Draw(pasture).polygon([pixel(*p) for p in outline], fill=255)
    pasture = pasture.filter(ImageFilter.GaussianBlur(48))
    grass = cover(0)
    earth = ImageEnhance.Color(cover(1)).enhance(1.18)
    earth = ImageEnhance.Brightness(earth).enhance(1.10)
    # Snow settles in broad drifts and small pockets between the painted grass tufts.
    exposure = ImageChops.add(pasture.point(lambda v: v*.55), drift, offset=-60)
    exposure = ImageChops.subtract(exposure, ImageOps.grayscale(grass).point(lambda v: v*.25))
    exposure = exposure.point(lambda v: max(0, min(255, (v-129)*5)))
    exposure = ImageChops.multiply(exposure, pasture)
    opacity = ImageChops.lighter(exposure, wear)
    rgb = Image.composite(earth, grass, wear)
    # ClassicUO uses premultiplied alpha for world sprites.
    rgb = Image.composite(rgb, Image.new("RGB", SIZE), opacity)
    rgb.putalpha(opacity)
    rgb.save(ART / "village-ground.png")


if __name__ == "__main__":
    bake()
