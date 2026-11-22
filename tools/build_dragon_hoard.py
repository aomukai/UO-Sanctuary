#!/usr/bin/env python3
"""Pack the dragon hoard into ClassicUO loose art and a relative placement template."""

import json
from pathlib import Path

from PIL import Image, ImageChops

from build_spell_effect_art import frame
from build_succubus_bolt import encode_static


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art" / "world" / "dragon-hoard"
GRAPHIC = 65520  # WorldDecorationGraphics.DragonHoard.
FIRST_GROUND_GRAPHIC = 65521  # WorldDecorationGraphics.DragonHoardGroundFirst.
MASK_ORIGIN = (-484, -360)  # Screen pixels from the nest's ground anchor.
SECTION_WIDTH, SECTION_HEIGHT = 192, 224
GROUND_DRAW_OFFSET = 112  # WorldDecorationGraphics.DragonHoardGroundDrawOffset.
ALPHA_THRESHOLD = 112


def sprite_from(path, threshold):
    with Image.open(path) as source:
        bounds = source.getchannel("A").point(lambda alpha: 255 if alpha >= threshold else 0).getbbox()
        if bounds is None:
            raise ValueError(f"{path.name} has no opaque art.")
        return source.crop(bounds)


def pack_nest(destination):
    sprite = sprite_from(ART / "cindermaw-hoard-weathered.png", 128)
    width = 370
    scale = width / sprite.width
    height = round(sprite.height * scale)
    # Place the foreground edge just in front of the anchor tile.
    origin = (sprite.width / 2, (height - 14) / scale)
    canvas = (width, height + 8)

    (destination / f"{GRAPHIC}.art").write_bytes(frame(sprite, scale, origin, canvas, 22, alpha_threshold=128))


def pack_ground_section(section, left, top):
    # Map each section to a nearby tile, padding the bitmap to preserve pixel-perfect alignment.
    target_x = left + section.width / 2
    target_y = top + section.height - GROUND_DRAW_OFFSET
    dx = round((target_y + target_x) / 44)
    dy = round((target_y - target_x) / 44)
    anchor_x, anchor_y = 22 * (dx - dy), 22 * (dx + dy)

    half_width = max(anchor_x - left, left + section.width - anchor_x)
    width, height = half_width * 2, anchor_y + GROUND_DRAW_OFFSET - top + 22
    canvas = Image.new("RGBA", (width, height))
    canvas.paste(section, (left - anchor_x + half_width, 0))
    pixels = [max(1, (r >> 3) << 10 | (g >> 3) << 5 | (b >> 3)) if a >= ALPHA_THRESHOLD else 0
              for r, g, b, a in canvas.getdata()]
    return encode_static(width, height, pixels), (dx, dy)


def pack_ground(destination):
    with Image.open(ART / "clearing-mask.png") as image:
        mask = image.convert("L")
    if mask.size != (SECTION_WIDTH * 4, SECTION_HEIGHT * 3):
        raise ValueError("Unexpected hoard clearing mask dimensions.")

    bounds = mask.getbbox()
    sprite = sprite_from(ART / "cindermaw-hoard-ground.png", ALPHA_THRESHOLD)
    sprite = sprite.resize((bounds[2] - bounds[0], bounds[3] - bounds[1]), Image.Resampling.LANCZOS)
    floor = Image.new("RGBA", mask.size)
    floor.paste(sprite, bounds[:2])
    # The lower slope needs more scatter than the thinner edge of the main sprite.
    slope = sprite_from(ART / "cindermaw-hoard-slope.png", ALPHA_THRESHOLD)
    slope = slope.resize((416, 254), Image.Resampling.LANCZOS)
    floor.alpha_composite(slope, (94, 374))
    floor.putalpha(ImageChops.multiply(floor.getchannel("A"), mask))

    sections = [{"graphic": GRAPHIC, "x": 0, "y": 0}]
    for row in range(3):
        for column in range(4):
            graphic = FIRST_GROUND_GRAPHIC + row * 4 + column
            left, top = column * SECTION_WIDTH, row * SECTION_HEIGHT
            section = floor.crop((left, top, left + SECTION_WIDTH, top + SECTION_HEIGHT))
            if section.getchannel("A").getextrema()[1] < ALPHA_THRESHOLD:
                continue
            packed, (x, y) = pack_ground_section(section, left + MASK_ORIGIN[0], top + MASK_ORIGIN[1])
            (destination / f"{graphic}.art").write_bytes(packed)
            sections.append({"graphic": graphic, "x": x, "y": y})
    template = ROOT / "data" / "Sanctuary" / "dragon-hoard-template.json"
    template.write_text(json.dumps(sections, indent=2) + "\n", encoding="utf-8")


def main():
    destination = ART / "statics"
    destination.mkdir(exist_ok=True)
    pack_nest(destination)
    pack_ground(destination)


if __name__ == "__main__":
    main()
