#!/usr/bin/env python3
"""Pack Sanctuary's generated raven poses using the Succubus MUL encoder."""

from math import cos, pi, sin
from struct import pack

from PIL import Image

from build_succubus_animations import ACTIONS, ROOT, encode_frames


ART = ROOT / "art" / "raven"
CELL_WIDTH, CELL_HEIGHT = 160, 112
ANCHOR = (80, 91)
# The source rows face north through south. Native animation directions start south.
ROW_BOUNDS = (
    (0, 250, 501, 750, 994, 1254),
    (0, 250, 501, 752, 1003, 1254),
    (0, 250, 501, 756, 1003, 1254),
    (0, 250, 501, 752, 1003, 1254),
)
FLIGHT = (0, 1, 2, 1)
DIVE = (0, 1, 3, 3, 3, 3, 2, 2, 1, 0)
FLOCK_POSITIONS = ((0, 0), (-19, 12), (19, 12), (-30, 25), (30, 25))


def load_poses():
    with Image.open(ART / "sheet.png") as source:
        sheet = source.convert("RGBA")
    if sheet.size != (1254, 1254):
        raise ValueError("Expected the 1254 × 1254 Raven source sheet")

    for direction in range(5):
        poses = []
        row = 4 - direction
        for column, bounds in enumerate(ROW_BOUNDS):
            box = (column * sheet.width // 4, bounds[row],
                   (column + 1) * sheet.width // 4, bounds[row + 1])
            pose = sheet.crop(box)
            trimmed = pose.getchannel("A").point(lambda alpha: 255 if alpha >= 96 else 0).getbbox()
            if trimmed is None:
                raise ValueError(f"Missing pose {column} facing {direction}")
            pose = pose.crop(trimmed)
            poses.append(pose.resize((round(pose.width * 0.135), round(pose.height * 0.135)),
                                     Image.Resampling.LANCZOS))
        yield poses


def render_strip(poses, direction, count, dive):
    sequence = DIVE if dive else FLIGHT
    strip = Image.new("RGBA", (CELL_WIDTH * len(sequence), CELL_HEIGHT))
    angle = direction * pi / 4
    facing_x, facing_y = -sin(angle), cos(angle)
    for frame, pose_index in enumerate(sequence):
        positions = []
        for bird, (side, trail) in enumerate(FLOCK_POSITIONS[:count]):
            if dive:
                pose = poses[pose_index]
                thrust = (0, 2, 6, 10, 13, 11, 8, 5, 2, 0)[frame]
                drop = (0, 0, 2, 7, 11, 10, 7, 4, 1, 0)[frame]
            else:
                phase = (frame + bird) % len(FLIGHT)
                pose = poses[FLIGHT[phase]]
                thrust, drop = 0, (0, 1, 3, 1)[phase]
            x = round(ANCHOR[0] + facing_y * side + facing_x * (thrust - trail))
            y = round(ANCHOR[1] - 32 - bird * 2 + drop
                      + (facing_y * (thrust - trail) - facing_x * side) * 0.6)
            positions.append((y, x, pose))
        for y, x, pose in sorted(positions, key=lambda item: item[0]):
            strip.alpha_composite(pose, (frame * CELL_WIDTH + x - pose.width // 2, y - pose.height // 2))
    return strip


def main():
    poses_by_direction = list(load_poses())
    data = bytearray()
    index = bytearray()
    for count in range(1, len(FLOCK_POSITIONS) + 1):
        entries = {}
        for direction, poses in enumerate(poses_by_direction):
            for dive in (False, True):
                strip = render_strip(poses, direction, count, dive)
                mask = strip.getchannel("A").point(lambda alpha: 255 if alpha >= 100 else 0)
                block = encode_frames(strip.convert("RGB"), mask, CELL_WIDTH, *ANCHOR)
                entries[dive, direction] = pack("<III", len(data), len(block), 0)
                data.extend(block)
        for action in ACTIONS:
            for direction in range(5):
                index.extend(entries[action == "attack", direction])
    (ART / "raven.idx").write_bytes(index)
    (ART / "raven.mul").write_bytes(data)


if __name__ == "__main__":
    main()
