#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Bake separate ground textures and a blood decal for the isometric Diablo cave."""

import json
import math
from pathlib import Path
from random import Random

from PIL import Image, ImageDraw

from build_dragon_hoard import pack_ground_section
from build_succubus_bolt import encode_static

ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art/world/diablo-ritual"
DATA = ROOT / "data/Sanctuary/diablo-ritual.json"
FIRST_BLOOD = 59920
FIRST_CARPET = 59760
BLOOD_END = 59960
LAVA_BARRIER = 59992
CARPET_DRAW_OFFSET = 240
ISO = 22
SQUASH = 0.76
ROOT2 = math.sqrt(2)
BLOOD_RADIUS = 15
BLOOD_WIDTH, BLOOD_HEIGHT = 1008, 790
TIPS = [(math.cos(a), math.sin(a)) for a in (math.radians(-90 + i * 72) for i in range(5))]


def ritual_plane(x, y):
    return ((x - y) / ROOT2, (x + y) / (ROOT2 * SQUASH))


def ground_plane(u, v):
    return ((u + v * SQUASH) / ROOT2, (v * SQUASH - u) / ROOT2)


def platform_edge(x, y, u, v):
    angle = math.atan2(v, u)
    profile = 19.1 + 1.8 * max(0, math.cos(5 * (angle + math.pi / 2))) ** 4
    vent_edge = max(16.7 + .45 * math.cos(v) - abs(u), abs(v) - 6)
    platform = min(profile - math.hypot(u, v), vent_edge)
    return max(platform, 2.5 - min(abs(x), abs(y)))


def platform(x, y):
    return platform_edge(x, y, *ritual_plane(x, y)) >= 0


def moat_edge(u, v):
    return 24.5 + .7 * math.cos(math.atan2(v, u) * 7) - math.hypot(u, v)


def terrain():
    stone, lava = set(), set()
    for y in range(-25, 26):
        for x in range(-25, 26):
            u, v = ritual_plane(x, y)
            if moat_edge(u, v) < 0:
                continue
            (stone if platform(x, y) else lava).add((x, y))
    return stone, lava


def blood_art():
    rng = Random(548)
    size = (BLOOD_WIDTH, BLOOD_HEIGHT)
    mask = Image.new("L", size)
    pen = ImageDraw.Draw(mask)
    scale = ISO * ROOT2
    cx, cy = size[0] / 2, size[1] / 2

    def stamp(x, y, width, value):
        pen.ellipse((x - width, y - width * .82, x + width, y + width * .82), fill=value)

    def paint_path(points, base_width, gap_chance):
        width = base_width
        for rx, ry in points:
            width = width * .81 + rng.uniform(.25, 1.65) * base_width * .19
            stroke = width * (1.0 + .36 * math.sin(rx * 1.5 + ry * .9) + .22 * math.cos(rx * 2.6 - ry * 1.3))
            x, y = cx + rx * scale + rng.gauss(0, 1.8), cy + ry * scale * SQUASH + rng.gauss(0, 1.7)
            if rng.random() < gap_chance or (width < 3.8 and rng.random() < .4):
                continue
            stamp(x, y, max(1, stroke + rng.uniform(-2.6, 2.6)), rng.randrange(190, 256))
            if rng.random() < .22:
                x += rng.gauss(0, stroke * 1.55)
                y += rng.gauss(0, stroke * 1.55)
                stamp(x, y, rng.uniform(0.6, 2.9), rng.randrange(125, 242))

    ring = []
    for i in range(940):
        theta = 2 * math.pi * i / 940
        radius = BLOOD_RADIUS + .09 * math.cos(theta * 9) + .12 * math.sin(theta * 17)
        ring.append((radius * math.cos(theta), radius * math.sin(theta)))
    paint_path(ring, 8.2, .16)
    for i, (x, y) in enumerate(TIPS):
        ax, ay = TIPS[(i + 2) % 5]
        points = []
        for step in range(370):
            t = step / 369
            skew = .12 * math.sin(t * 10 + i) + .07 * math.sin(t * 28 + i * 3)
            if .17 < (t + i * .14) % .41 < .194:
                continue
            points.append((BLOOD_RADIUS * (x * (1 - t) + ax * t) + skew,
                           BLOOD_RADIUS * (y * (1 - t) + ay * t) - skew))
        paint_path(points, 5.3, .15)
    for u, v in TIPS:
        x, y = cx + u * BLOOD_RADIUS * scale, cy + v * BLOOD_RADIUS * scale * SQUASH
        for _ in range(38):
            stamp(x + rng.gauss(0, 11), y + rng.gauss(0, 8), rng.uniform(1.0, 6.3), rng.randrange(160, 249))

    # The ClassicUO art is binary alpha. Sparse dark marks preserve the texture of dried blood.
    output = Image.new("RGBA", size)
    mask_data, pixels = mask.load(), output.load()
    for y in range(size[1]):
        for x in range(size[0]):
            alpha = mask_data[x, y]
            if alpha < 125:
                continue
            wx, wy = ((x - cx) / scale, (y - cy) / (scale * SQUASH))
            gx, gy = ground_plane(wx, wy)
            if not platform(round(gx), round(gy)) or rng.randrange(17) == 0:
                continue
            fleck = rng.randrange(-12, 15)
            strength = max(40, int(alpha * .48) + fleck)
            pixels[x, y] = (strength, max(5, strength // 11), max(5, strength // 9), 255)
    return output


def pack_overlay(art, first, limit, offset=(0, 0), anchored=False):
    sections = []
    for top in range(0, art.height, 148):
        for left in range(0, art.width, 148):
            cell = art.crop((left, top, min(left + 148, art.width), min(top + 148, art.height)))
            bounds = cell.getchannel("A").getbbox()
            if bounds is None:
                continue
            sprite = cell.crop(bounds)
            x = left + bounds[0] - art.width // 2 + offset[0]
            y = top + bounds[1] - art.height // 2 + offset[1]
            if first < FIRST_BLOOD:
                cx, cy = x + sprite.width / 2, y + sprite.height / 2
                tile_x, tile_y = round((cy + cx) / 44), round((cy - cx) / 44)
                if anchored:
                    # Keep the large artwork's anchors within view of the platform.
                    tile_x = max(-18, min(18, tile_x))
                    tile_y = max(-18, min(18, tile_y))
                packed, (dx, dy) = pack_ground_section(sprite, x, y,
                    draw_offset=CARPET_DRAW_OFFSET, anchor=(tile_x, tile_y))
            else:
                packed, (dx, dy) = pack_ground_section(sprite, x, y)
            graphic = first + len(sections)
            if graphic >= limit:
                raise ValueError("Overlay exceeded the assigned art slots")
            (ART / f"statics/{graphic}.art").write_bytes(packed)
            sections.append({"Graphic": graphic, "X": dx, "Y": dy})
    return sections


def swatch(source, liquid):
    half = source.width // 2
    left = 0 if liquid else half
    field = source.crop((left + 4, 4, left + half - 4, source.height // 2 - 4)).resize((176, 176))
    wrapped = Image.new("RGB", field.size)
    src, dst = field.load(), wrapped.load()
    for y in range(field.height):
        for x in range(field.width):
            # Wrap both axes across the outer eighth without rotating adjacent tiles.
            w = max(0, 1 - min(x, 176 - x) / 22) * .5
            h = max(0, 1 - min(y, 176 - y) / 22) * .5
            colors = (src[x, y], src[175 - x, y], src[x, 175 - y], src[175 - x, 175 - y])
            mixed = [round(sum(p * q for p, q in zip(c, ((1 - w) * (1 - h), w * (1 - h), (1 - w) * h, w * h))))
                     for c in zip(*colors)]
            if liquid:
                mixed = [min(255, round(mixed[0] * 1.18)), min(255, round(mixed[1] * 1.05)), mixed[2]]
            else:
                mixed = [int(value * .68) for value in mixed]
            dst[x, y] = tuple(mixed)
    return dst


def render_carpet(stone, lava, size, trench=False):
    overlay = Image.new("RGBA", size)
    pixels = overlay.load()
    for sy in range(size[1]):
        v = (sy + .5 - size[1] / 2) / (ISO * ROOT2 * SQUASH)
        for sx in range(size[0]):
            u = (sx + .5 - size[0] / 2) / (ISO * ROOT2)
            x, y = ground_plane(u, v)
            if trench:
                dx = abs(x) - 1.38
                dy = abs(y) - .88
                outer = .67 - math.hypot(max(dx, 0), max(dy, 0))
                shore = -.64
            else:
                outer = moat_edge(u, v)
                shore = platform_edge(x, y, u, v)
            scatter = (sx * 73856093 ^ sy * 19349663) & 255
            if outer < 0:
                # Let exposed native floor show between increasingly sparse
                # patches of ash, gravel and stone toward the outside edge.
                apron = .95 if trench else 2.25
                apron += .33 * math.sin(1.86 * x + .42 * y) + .21 * math.sin(1.38 * y - .8 * x)
                fade = -outer / apron
                if fade >= 1 or scatter >= 256 * (1 - fade) ** 1.6:
                    continue
                rock = stone[int(x * 33) % 176, int(y * 33) % 176]
                gravel = (110, 100, 77)
                weathering = min(1, .21 + fade * .83)
                grain = int((scatter - 120) / 14)
                pixels[sx, sy] = (*(round(r * (1 - weathering) + g * weathering) + grain
                                    for r, g in zip(rock, gravel)), 255)
                continue

            # The same contours drive the art and collision. Shading creates
            # depth along curved and diagonal shores between world tile centers.
            if shore >= 0:
                scale = .76 + .09 * math.sin(x * .7 + y * .21)
                scale *= .7 + min(shore, .68) * .44
                tx, ty = int(x * 30 + 7 * math.cos(y * .7)), int(y * 30 + 6 * math.cos(x))
                color = stone[tx % 176, ty % 176]
            else:
                tx, ty = int(x * 24 + 15 * math.cos(y * .45)), int(y * 24 + 13 * math.sin(x * .42))
                color = lava[tx % 176, ty % 176]
                edge = min(-shore, outer)
                molten = min(1.0, edge * 1.05)
                scale = .21 + .69 * molten
                if .12 <= edge <= .32:
                    color = (max(color[0], 130), color[1] // 2, color[2] // 2)
            pixels[sx, sy] = (*(round(channel * scale) for channel in color), 255)
    return overlay


def main():
    folder = ART / "statics"
    folder.mkdir(exist_ok=True)
    blood = blood_art()
    blood.save(ART / "blood-overlay.png", optimize=True)
    sections = pack_overlay(blood, FIRST_BLOOD, BLOOD_END)
    with Image.open(ART / "materials.png") as source:
        source = source.convert("RGB")
        stone_tex, lava_tex = swatch(source, False), swatch(source, True)
        carpet = render_carpet(stone_tex, lava_tex, (1760, 1340))
        carpet.save(ART / "arena-carpet.png", optimize=True)
        floor = pack_overlay(carpet, FIRST_CARPET, FIRST_BLOOD, anchored=True)
        trench = render_carpet(stone_tex, lava_tex, (210, 210), trench=True)
        for y in (-4, 4):
            x = -30.5
            offset = (round(ISO * (x - y)), round(ISO * (x + y)))
            floor.extend(pack_overlay(trench, FIRST_CARPET + len(floor), FIRST_BLOOD, offset))
    (folder / f"{LAVA_BARRIER}.art").write_bytes(encode_static(1, 1, [0]))
    for start, end in ((FIRST_CARPET + len(floor), FIRST_BLOOD),
                       (FIRST_BLOOD + len(sections), LAVA_BARRIER)):
        for graphic in range(start, end):
            (folder / f"{graphic}.art").unlink(missing_ok=True)

    stone, lava = terrain()
    def coordinates(cells):
        return [[x, y] for x, y in sorted(cells)]

    DATA.write_text(json.dumps({
        "Sections": floor + sections,
        "Stone": coordinates(stone),
        "Lava": coordinates(lava),
    }, separators=(",", ":")) + "\n")


if __name__ == "__main__":
    main()
