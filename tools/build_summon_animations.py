#!/usr/bin/env python3
"""Register original summon poses on a ground pivot and pack ClassicUO animations."""

from pathlib import Path
from statistics import median
from struct import pack

from PIL import Image, ImageChops

from build_succubus_animations import ACTIONS, encode_frames
from summon_walk_cycles import LEG_OUTLINES, walk_cycle


ART = Path(__file__).resolve().parents[1] / "art" / "summon-bodies"
CELL = 128
GROUND_X, GROUND_Y = 64, 104
DIRECTIONS = 5
# Keep this order in sync with SummonBodyAppearance. Hats add height above the same feet.
BODIES = (
    ("sword-and-shield", 65), ("damaged-chainmail", 65),
    ("mage-hat", 76), ("mage-staff", 76), ("mage-robe", 76),
    ("blood-golem", 83), ("iron-golem", 80), ("fire-golem", 86),
    ("valkyrie", 81),
)
POSE_ROWS = {"valkyrie": (0, 289, 558, 821, 1049, 1312)}
FOOT_OFFSETS = (6, 6, 4, 1, -3)
FORWARD = ((0, 2), (-2, 1), (-2, 0), (-2, -1), (0, -2))
OPAQUE = 160


def silhouette(image):
    return image.getchannel("A").point(lambda opacity: 255 if opacity >= OPAQUE else 0)


def divisions(mask, horizontal):
    # Favor the empty gutter over a nominal boundary. Weapon tips can cross it.
    span = mask.height if horizontal else mask.width
    edges = [0]
    for division in range(1, 5):
        nominal = round(span * division / 5)

        def score(edge):
            if horizontal:
                stripe = mask.crop((0, edge - 1, mask.width, edge + 1))
            else:
                stripe = mask.crop((edge - 1, 0, edge + 1, mask.height))
            return stripe.histogram()[255] + 2 * abs(edge - nominal)

        edges.append(min(range(nominal - 22, nominal + 23), key=score))
    return [*edges, span]


def cells(sheet, row_edges=None):
    rows = row_edges or divisions(silhouette(sheet), horizontal=True)
    for top, bottom in zip(rows, rows[1:]):
        strip = sheet.crop((0, top, sheet.width, bottom))
        columns = divisions(silhouette(strip), horizontal=False)
        yield [
            strip.crop((left, 0, right, strip.height))
            for left, right in zip(columns, columns[1:])
        ]


def trimmed(image):
    mask = silhouette(image)
    bounds = mask.getbbox()
    if bounds is None:
        raise ValueError("A summon pose is empty")
    return image.crop(bounds)


def torso_center(sprite):
    top, bottom = round(sprite.height * .37), round(sprite.height * .65)
    band = silhouette(sprite).crop((0, top, sprite.width, bottom))
    profile = list(band.resize((sprite.width, 1), Image.Resampling.BOX).getdata())
    weight_sum = sum(profile)
    if not weight_sum:
        return sprite.width / 2
    return sum(x * weight for x, weight in enumerate(profile)) / weight_sum


def anchored(source, scale, direction, fallen=False):
    size = (round(source.width * scale), round(source.height * scale))
    sprite = source.resize(size, Image.Resampling.LANCZOS)
    center = sprite.width / 2 if fallen else torso_center(sprite)
    left = GROUND_X - round(center)
    top = GROUND_Y + FOOT_OFFSETS[direction] - sprite.height
    if not (0 <= left < left + sprite.width <= CELL and 0 <= top < top + sprite.height <= CELL):
        raise ValueError(f"Summon pose exceeds the animation canvas: {size=} {left=} {top=}")
    canvas = Image.new("RGBA", (CELL, CELL))
    canvas.paste(sprite, (left, top))
    return canvas


def transform(image, dx=0, dy=0, height=1.0, shear=0):
    # Windup and recovery tilt relative to the standing feet, so no moving ground origin.
    return image.transform(image.size, Image.Transform.AFFINE,
                           (1, shear, -dx - shear * GROUND_Y,
                            0, 1 / height, GROUND_Y - (GROUND_Y + dy) / height),
                           Image.Resampling.BICUBIC)


def transition(first, second, amount):
    # Blend the shared interior. Use just one silhouette so a weapon or foot never doubles.
    pose = first if amount < .5 else second
    shared = ImageChops.darker(silhouette(first), silhouette(second))
    image = Image.composite(Image.blend(first, second, amount), pose, shared)
    image.putalpha(pose.getchannel("A"))
    return image


def walking(idle, left, right, direction):
    advance_x, advance_y = FORWARD[direction]
    frames = []
    for stride in (left, right):
        gather = transform(idle, dy=-1, height=1.015)
        reach = transform(stride, -advance_x / 2, -advance_y / 2, .98)
        plant = transform(stride, advance_x / 2, advance_y / 2)
        frames.extend((gather, transition(gather, reach, .56), plant,
                       transform(stride, dy=-1, height=.985), transition(stride, idle, .66)))
    return frames


def motion(poses, direction, caster):
    idle, stride_left, stride_right, action, corpse = poses
    advance_x, advance_y = FORWARD[direction]
    windup = transform(idle, -advance_x, -advance_y, .97, advance_x * .011)
    recoil = transform(idle, advance_x / 2, advance_y / 2, 1.01, -advance_x * .006)
    hit = transform(idle, -advance_x, -advance_y, .94, advance_x * .015)
    fall = transform(action, advance_x, advance_y + 1, .58)
    crouch = transform(idle, dy=2, height=.8, shear=-advance_x * .02)
    attack = [windup, transition(windup, action, .35), action, recoil]
    cast = [windup, transform(windup, dy=-1), transition(windup, action, .60), action]
    return {
        "idle": [idle],
        "walk": walking(idle, stride_left, stride_right, direction),
        "attack": cast if caster else attack,
        "cast": cast if caster else attack,
        "hit": [hit, transition(hit, idle, .35), recoil, idle],
        "death": [hit, crouch, fall, corpse],
    }


def encode(sequence):
    strip = Image.new("RGB", (len(sequence) * CELL, CELL))
    mask = Image.new("L", strip.size)
    for frame, image in enumerate(sequence):
        at = (frame * CELL, 0)
        frame_mask = silhouette(image)
        strip.paste(image.convert("RGB"), at, frame_mask)
        mask.paste(frame_mask, at)
    return encode_frames(strip, mask, CELL, GROUND_X, GROUND_Y)


def animations(name, height, *, art=None):
    art = art or ART
    with Image.open(art / f"{name}.png") as sheet:
        poses = [[trimmed(cell) for cell in row] for row in cells(sheet, POSE_ROWS.get(name))]
    scale = height / median(sprite.height for sprite in poses[0])
    for direction in range(DIRECTIONS):
        sequence = [anchored(row[direction], scale, direction, fallen=index == 4)
                    for index, row in enumerate(poses)]
        moves = motion(sequence, direction, caster=name.startswith("mage-"))
        if name in LEG_OUTLINES:
            moves["walk"] = walk_cycle(sequence[0], name, direction)
        yield moves


def write_archive(art, basename, bodies):
    index = bytearray()
    data = bytearray()
    for name, height in bodies:
        blocks = {}
        for direction, moves in enumerate(animations(name, height, art=art)):
            for action, sequence in moves.items():
                block = encode(sequence)
                blocks[action, direction] = pack("<III", len(data), len(block), 0)
                data.extend(block)
        for group, action in enumerate(ACTIONS):
            for direction in range(DIRECTIONS):
                index.extend(blocks["cast" if group == 12 else action, direction])
    (art / f"{basename}.mul").write_bytes(data)
    (art / f"{basename}.idx").write_bytes(index)


def main():
    write_archive(ART, "summon-bodies", BODIES)


if __name__ == "__main__":
    main()
