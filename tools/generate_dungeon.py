#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Generate a fixed four-room Hythloth layout for ModernUO's decoration loader."""

import argparse
from collections import defaultdict
from dataclasses import dataclass
from pathlib import Path
import random
import secrets


# Static art IDs found in Trammel Hythloth. Cave floors are walkable at their base Z.
CAVE_FLOORS = range(0x053B, 0x0540)
WALL_ALONG_X = 0x0241
WALL_ALONG_Y = 0x0242
WALL_CORNER_NW = 0x0243
WALL_CORNER_SE = 0x0244
SLOT_SIZE = 16
MAP_WIDTH = 7168  # Current Felucca and Trammel client data
MAP_HEIGHT = 4096
SIZE = SLOT_SIZE * 2


@dataclass(frozen=True)
class Room:
    left: int
    top: int
    right: int  # Exclusive
    bottom: int  # Exclusive

    @property
    def center(self):
        return (self.left + self.right) // 2, (self.top + self.bottom) // 2

    def cells(self):
        return rectangle(self.left, self.top, self.right, self.bottom)


def rectangle(left, top, right, bottom):
    return {(x, y) for y in range(top, bottom) for x in range(left, right)}


def generate(seed, *, slot_size=SLOT_SIZE, room_size=(7, 10), corridor_width=2):
    rng = random.Random(seed)
    rooms = []
    for col, row in ((0, 0), (1, 0), (0, 1), (1, 1)):
        left = col * slot_size + rng.randint(1, 4)
        top = row * slot_size + rng.randint(1, 4)
        rooms.append(Room(left, top, left + rng.randint(*room_size), top + rng.randint(*room_size)))

    # Drop one side of the cycle. The other three connections form a single route.
    links = [(0, 1), (2, 3), (0, 2), (1, 3)]
    ends = links.pop(rng.randrange(len(links)))
    corridors = []
    for first, second in links:
        a, b = rooms[first], rooms[second]
        # Align the openings along the overlap and leave a tile of room on either side.
        if second - first == 1:
            y = rng.randint(max(a.top, b.top) + 1, min(a.bottom, b.bottom) - corridor_width - 1)
            corridors.append(rectangle(a.right, y, b.left, y + corridor_width))
        else:
            x = rng.randint(max(a.left, b.left) + 1, min(a.right, b.right) - corridor_width - 1)
            corridors.append(rectangle(x, a.bottom, x + corridor_width, b.top))

    floor = set().union(*corridors, *(room.cells() for room in rooms))
    floors = {point: rng.choice(CAVE_FLOORS) for point in sorted(floor)}
    return rooms, corridors, floors, build_walls(floor), ends


def build_walls(floor):
    # Include diagonal neighbors so a diagonal move cannot slip between wall tiles.
    boundary = {
        (x + dx, y + dy) for x, y in floor for dx in (-1, 0, 1) for dy in (-1, 0, 1)
    } - floor
    return {point: wall_art(point, boundary) for point in sorted(boundary)}


def wall_art(point, boundary):
    x, y = point
    west = (x - 1, y) in boundary
    east = (x + 1, y) in boundary
    north = (x, y - 1) in boundary
    south = (x, y + 1) in boundary
    if west and north and not (east or south):
        return (WALL_CORNER_NW,)
    if east and south and not (west or north):
        return (WALL_CORNER_SE,)
    if (west or east) and (north or south):
        return WALL_ALONG_X, WALL_ALONG_Y
    if north or south:
        return (WALL_ALONG_Y,)
    return (WALL_ALONG_X,)


def write_decoration(path, seed, origin, floors, walls, entry, treasure, scenery=()):
    def world(point):
        return origin[0] + point[0], origin[1] + point[1], origin[2]

    placements = defaultdict(list)
    for point, graphic in floors.items():
        placements[graphic].append(world(point))
    for point, graphics in walls.items():
        for graphic in graphics:
            placements[graphic].append(world(point))
    for x, y, z, graphic in scenery:
        placements[graphic].append((origin[0] + x, origin[1] + y, origin[2] + z))

    lines = [
        f"# Hythloth layout generated with seed {seed}",
        f"# Entry: {world(entry)}",
    ]
    if treasure is not None:
        lines.append(f"# Treasure marker: {world(treasure)}")
    lines.append("")
    for graphic, points in sorted(placements.items()):
        lines.append(f"Static 0x{graphic:04X}")
        lines.extend(f"{x} {y} {z}" for x, y, z in points)
        lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def write_plan(path, seed, rooms, corridors, walls, entry, treasure):
    cells = 18
    inset = 24
    width = SIZE * cells + 2 * inset
    top = 76
    height = width + top
    lines = [
        f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" '
        f'viewBox="0 0 {width} {height}" role="img" aria-labelledby="title">',
        f'<title id="title">Hythloth dungeon plan, seed {seed}</title>',
        '<rect width="100%" height="100%" fill="#161b20"/>',
        '<g font-family="sans-serif" fill="#ece7dc">',
        f'<text x="{inset}" y="38" font-size="24" font-weight="bold">Hythloth trial</text>',
        f'<text x="{inset}" y="61" font-size="13" fill="#b7babd">Seed {seed} · Each square is one tile</text>',
    ]
    for tiles, fill in ((walls, "#51555b"), (corridors, "#82796f"),
                        (set().union(*(room.cells() for room in rooms)), "#a29a87")):
        for x, y in sorted(tiles):
            px, py = inset + x * cells, top + y * cells
            lines.append(f'<rect x="{px}" y="{py}" width="{cells}" height="{cells}" '
                         f'fill="{fill}" stroke="#161b20" stroke-width="0.5"/>')
    for (x, y), label, fill in ((entry, "Entry", "#b9dbc1"), (treasure, "Treasure", "#f1d29a")):
        px, py = inset + x * cells + cells // 2, top + y * cells + cells // 2
        lines.append(f'<circle cx="{px}" cy="{py}" r="6" fill="{fill}" stroke="#272827"/>')
        lines.append(f'<text x="{px}" y="{py + 22}" text-anchor="middle" '
                     f'font-size="13" font-weight="bold" fill="#161b20">{label}</text>')
    lines.extend(("</g>", "</svg>"))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path, help="New directory for layout.cfg and plan.svg")
    parser.add_argument("--seed", type=int, help="Integer seed. Omit to pick a fresh seed.")
    parser.add_argument("--origin", type=int, nargs=3, metavar=("X", "Y", "Z"), required=True,
                        help="Northwest corner of a clear 32×32 area on Felucca or Trammel")
    args = parser.parse_args()
    x, y, z = args.origin
    if not (0 <= x <= MAP_WIDTH - SIZE and 0 <= y <= MAP_HEIGHT - SIZE and -128 <= z <= 105):
        parser.error("origin must fit the 32×32 footprint and 22-Z walls on Felucca or Trammel")
    seed = args.seed if args.seed is not None else secrets.randbits(32)
    rooms, corridors, floors, walls, (first, last) = generate(seed)
    entry, treasure = rooms[first].center, rooms[last].center
    try:
        args.output.mkdir(parents=True)
    except FileExistsError:
        parser.error(f"output already exists: {args.output}")
    write_decoration(args.output / "layout.cfg", seed, args.origin, floors, walls, entry, treasure)
    write_plan(args.output / "plan.svg", seed, rooms, set().union(*corridors), walls, entry, treasure)
    print(f"Wrote seed {seed} to {args.output}")
    print(f"Entry at ({x + entry[0]}, {y + entry[1]}, {z})")
    print(f"Treasure marker at ({x + treasure[0]}, {y + treasure[1]}, {z})")


if __name__ == "__main__":
    main()
