#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Bake Haven's four crypts into fixed placements for ModernUO."""

from collections import deque
import json
from pathlib import Path
import random

from generate_dungeon import Room, build_walls, generate, rectangle, write_decoration


OUTPUT = Path(__file__).resolve().parents[1] / "data/Sanctuary/HavenCrypts"
CRYPTS = [(27, 3600), (118, 3690), (218, 3780), (451, 3870)]
STONE_PAVERS = (0x051B, 0x051C, 0x051D, 0x051E)
SARCOPHAGUS = (
    (0x1C63, 0x1C64, 0x1C65),
    (0x1C62, 0x1C61, 0x1C60),
)
RITUAL_FLOOR = (
    (0x120E, 0x120F, 0x1210),
    (0x1215, 0x1216, 0x1211),
    (0x1214, 0x1213, 0x1212),
)
STONE_BENCH = ((0x1208, 0x1209, 0x1207),)
ARRIVAL_SIZE = 8


def place_art(left, top, art, z=0):
    return [(left + x, top + y, z, graphic)
            for y, row in enumerate(art) for x, graphic in enumerate(row)]


def dress(room, theme):
    left, top, right, bottom = room.left, room.top, room.right, room.bottom
    x, y = room.center
    if theme == "tombs":
        return (place_art(left + 3, top + 3, SARCOPHAGUS)
                + place_art(right - 6, bottom - 5, SARCOPHAGUS)
                + [(left + 2, y, 0, 0x241D), (right - 3, y, 0, 0x241E)])
    if theme == "ossuary":
        return [
            (left + 1, top + 1, 0, 0x10DD), (left + 2, top + 1, 0, 0x10DC),
            (left + 1, top + 2, 0, 0x10D9), (left + 2, top + 2, 0, 0x10D4),
            (right - 2, bottom - 2, 0, 0x10DD), (right - 3, bottom - 2, 0, 0x10D7),
            (x, y, 0, 0x1B09), (x + 1, y, 0, 0x1B0E), (x, y + 1, 0, 0x1B10),
        ]
    return (place_art(x - 1, top + 3, RITUAL_FLOOR, z=1)
            + place_art(x - 1, y + 2, STONE_BENCH)
            + [(x - 3, top + 4, 0, 0x0B26), (x + 3, top + 4, 0, 0x0B26),
               (x, bottom - 3, 0, 0x0E31)])


def reachable(cells, entry):
    reached = {entry}
    pending = deque([entry])
    while pending:
        x, y = pending.popleft()
        for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if point in cells and point not in reached:
                pending.append(point)
                reached.add(point)
    return reached


def compact_arrival(room, corridors):
    for corridor in corridors:
        left, right = min(x for x, _ in corridor), max(x for x, _ in corridor) + 1
        top, bottom = min(y for _, y in corridor), max(y for _, y in corridor) + 1
        if (room.bottom == top or room.top == bottom) and room.left <= left < right <= room.right:
            x = max(room.left, min((left + right - ARRIVAL_SIZE) // 2, room.right - ARRIVAL_SIZE))
            y = room.bottom - ARRIVAL_SIZE if room.bottom == top else room.top
            return Room(x, y, x + ARRIVAL_SIZE, y + ARRIVAL_SIZE)
        if (room.right == left or room.left == right) and room.top <= top < bottom <= room.bottom:
            x = room.right - ARRIVAL_SIZE if room.right == left else room.left
            y = max(room.top, min((top + bottom - ARRIVAL_SIZE) // 2, room.bottom - ARRIVAL_SIZE))
            return Room(x, y, x + ARRIVAL_SIZE, y + ARRIVAL_SIZE)
    raise ValueError("Arrival room has no connecting corridor")


def wall_lights(room, walls):
    return [(x, room.top - 1, 15, 0x0A07)
            for x in (room.left + 1, room.right - 2) if (x, room.top - 1) in walls] + [
            (room.left - 1, y, 15, 0x0A0C)
            for y in (room.top + 1, room.bottom - 2) if (room.left - 1, y) in walls]


def build(number, seed, origin_y):
    rng = random.Random(seed + 10000)
    rooms, corridors, floors, walls, (entrance, _) = generate(
        seed, slot_size=28, room_size=(14, 19), corridor_width=4)
    original_arrival = rooms[entrance]
    spawn_points = [
        ((min(x for x, _ in corridor) + max(x for x, _ in corridor)) // 2,
         (min(y for _, y in corridor) + max(y for _, y in corridor)) // 2)
        for corridor in corridors
    ]
    scenery = []
    chest_points = []
    blocked = set()
    themes = ["tombs", "ossuary", "chapel"]
    rng.shuffle(themes)
    for index, room in enumerate(rooms):
        if index == entrance:
            # Keep the landing floor clear except for the return rope.
            for point in sorted(room.cells()):
                floors[point] = rng.choice(STONE_PAVERS)
        else:
            theme = themes.pop()
            props = dress(room, theme)
            scenery.extend(props)
            reserved = {(x, y) for x, y, _, _ in props}
            blocked.update(reserved)
            inner = rectangle(room.left + 1, room.top + 1, room.right - 1, room.bottom - 1)
            chest, first, second = rng.sample(sorted(inner - reserved), 3)
            chest_points.append(chest)
            spawn_points.extend((first, second))
            if theme == "chapel":
                for point in sorted(room.cells()):
                    floors[point] = rng.choice(STONE_PAVERS)

        if index != entrance:
            scenery.extend(wall_lights(room, walls))

    # Trim after the original placement rolls so the other rooms retain their
    # exact floor art, decorations, and encounter positions.
    arrival = compact_arrival(original_arrival, corridors)
    entry = arrival.center
    removed_floors = {point: floors.pop(point) for point in sorted(original_arrival.cells() - arrival.cells())}
    new_walls = build_walls(floors.keys())
    removed_walls = {point: tuple(g for g in graphics if g not in new_walls.get(point, ()))
                     for point, graphics in walls.items()}
    new_lights = wall_lights(arrival, new_walls)
    removed_lights = sorted(set(wall_lights(original_arrival, walls)) - set(new_lights))
    scenery.extend(new_lights)

    paths = floors.keys() - blocked - set(chest_points)
    walkable = reachable(paths, entry)
    if walkable != paths or not set(spawn_points) <= walkable:
        raise ValueError(f"Crypt {number} has blocked or disconnected floors")

    origin = (7024, origin_y, 0)

    def world(point):
        return [origin[0] + point[0], origin[1] + point[1], origin[2]]

    cfg = f"crypt-{number}.cfg"
    retired = f"retired-arrival-{number}.cfg"
    write_decoration(OUTPUT / cfg, seed, origin, floors, new_walls, entry, None, scenery)
    write_decoration(OUTPUT / retired, seed, origin, removed_floors, removed_walls,
                     original_arrival.center, None, removed_lights)
    return {
        "file": cfg,
        "origin": origin,
        "entry": world(entry),
        "arrivalStart": world((arrival.left, arrival.top)),
        "arrivalEnd": world((arrival.right, arrival.bottom)),
        "previousArrival": {
            "file": retired,
            "entry": world(original_arrival.center),
            "start": world((original_arrival.left, original_arrival.top)),
            "end": world((original_arrival.right, original_arrival.bottom)),
        },
        "spawns": [world(point) for point in spawn_points],
        "chests": [world(point) for point in chest_points],
    }


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    entries = [build(number, seed, y) for number, (seed, y) in enumerate(CRYPTS, 1)]
    (OUTPUT / "crypts.json").write_text(json.dumps(entries, separators=(",", ":"),
                                                      indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
