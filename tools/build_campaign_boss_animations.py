#!/usr/bin/env python3
"""Pack the painted campaign turntables using the native succubus MUL encoder."""
from functools import lru_cache
from math import cos, pi, sin
from statistics import median
from pathlib import Path
from struct import pack

from PIL import Image

from build_succubus_animations import ACTIONS, encode_frames


ART = Path(__file__).resolve().parents[1] / 'art' / 'campaign-bosses'
CELL = 256
GROUND = (128, 195)
EMBER = ((255, 153, 26), (255, 219, 66))
# Native scale per sheet pixel, row breaks, and horizontal ground pivots.
# Ground pivots stay under the body while the whip, staff and sword extend sideways.
SHEETS = {
    'leoric': (.49, (0, 265, 511, 778, 1040, 1254), (
        (141, 390, 627, 869, 1122), (141, 384, 618, 873, 1126),
        (132, 383, 613, 872, 1126), (132, 374, 617, 867, 1125),
        (135, 392, 631, 874, 1125))),
    'lazarus': (.50, (0, 250, 501, 751, 1015, 1254), (
        (137, 389, 632, 877, 1123), (135, 389, 632, 877, 1123),
        (127, 386, 627, 879, 1126), (127, 381, 626, 879, 1126),
        (125, 380, 627, 877, 1123))),
    'diablo': (.72, (0, 249, 498, 767, 1011), (
        (142, 393, 650, 909, 1130), (135, 392, 657, 910, 1130),
        (127, 387, 633, 877, 1160), (134, 390, 642, 898, 1130))),
}
# The spell and a few side-on weapon/death poses extend past nominal cell boundaries.
CUTS = {('leoric', 3): (0, 251, 493, 753, 1004, 1254),
        ('leoric', 4): (0, 251, 520, 753, 1004, 1254),
        ('lazarus', 3): (0, 251, 476, 715, 995, 1254),
        ('diablo', 0): (0, 252, 504, 759, 1010, 1262),
        ('diablo', 1): (0, 252, 499, 773, 1018, 1262),
        ('diablo', 2): (0, 252, 505, 777, 1018, 1262),
        ('diablo', 3): (0, 235, 488, 767, 1014, 1262)}

# (painted key, horizontal shift, vertical lift, change in height). The MUL format
# has a fixed ground origin even for a raised spell or a falling body.
SEQUENCES = {
    'attack': [(0, 0, 0, 0), (2, 1, 0, -2), (2, 2, 0, 1),
               (3, -2, 0, 0), (3, -1, 0, -2), (0, 0, 0, -2)],
    'hit': [(0, 0, 0, 0), (0, 3, 0, -5), (0, 2, 0, -3), (0, 0, 0, 0)],
    'death': [(0, 0, 0, 0), (0, 1, 0, -7), (3, 1, 0, -15),
              (4, 2, 4, 6), (4, 1, 1, 2), (4, 0, 0, 0)],
}


def resize_key(tile, size, body, direction):
    key = tile.resize(size, Image.Resampling.LANCZOS)
    if body == 'diablo' and direction < 3:
        # Carry the painted eye highlights through the resample before RGB555 packing.
        eyes = Image.new('L', tile.size)
        eyes.putdata([255 if a > 180 and r > 175 and g > 110 and g > 2*b and g > .57*r else 0
                      for r, g, b, a in tile.getdata()])
        coverage = eyes.resize(size, Image.Resampling.BOX)
        for threshold, color in zip((24, 105), EMBER):
            key.paste((*color, 255), (0, 0, *size), coverage.point(lambda a: 255 if a >= threshold else 0))
    return key


def painted_keys(body, direction):
    scale, rows, pivots = SHEETS[body]
    with Image.open(ART / f'{body}.png') as sheet:
        keys = []
        for row, (top, bottom) in enumerate(zip(rows, rows[1:])):
            cols = CUTS.get((body, row), (0, 251, 502, 753, 1004, 1254))
            left, right = cols[direction:direction + 2]
            tile = sheet.crop((left, top, right, bottom))
            bounds = tile.getchannel('A').point(lambda a: 255 if a >= 120 else 0).getbbox()
            tile = tile.crop(bounds)
            size = tuple(round(v * scale) for v in tile.size)
            key = resize_key(tile, size, body, direction)
            pivot = round((pivots[row][direction] - left - bounds[0]) * scale)
            keys.append((key, pivot))
        if body == 'diablo':
            with Image.open(ART / 'diablo-death.png') as corpse:
                cuts = (0, 426, 840, 1319, 1767, corpse.width)
                left, right = cuts[direction:direction + 2]
                tile = corpse.crop((left, 0, right, corpse.height))
                tile = tile.crop(tile.getchannel('A').point(lambda a: 255 if a >= 120 else 0).getbbox())
                img = tile.resize(tuple(round(v * .46) for v in tile.size), Image.Resampling.LANCZOS)
                keys.append((img, img.width // 2))
        return keys


@lru_cache(maxsize=1)
def flame_loop():
    # Row two is the sustained four-frame Sanctuary Fire Wall loop.
    with Image.open(ART.parent / 'spell-effects' / 'fire-wall' / 'sheet.png') as source:
        w, h = source.width // 4, source.height // 3
        return [source.crop((i*w, h, (i+1)*w, 2*h)).resize((77, 83), Image.Resampling.LANCZOS)
                for i in range(4)]


def burn(img, direction, phase):
    # Ground the flames on either wing and the armour. Keep the face uncovered.
    sites = (
        ((.22, .62), (.80, .70), (.52, .48)), ((.21, .64), (.83, .65), (.58, .45)),
        ((.27, .58), (.76, .72), (.54, .39)), ((.22, .55), (.79, .57), (.51, .39)),
        ((.19, .57), (.83, .56), (.53, .42)),
    )
    for i, (x, y) in enumerate(sites[direction]):
        fire = flame_loop()[(phase + i) % 4]
        img.alpha_composite(fire, (round(img.width*x) - 38, round(img.height*y) - 71))


def chest_center(image):
    # Cloth, tails and extended weapons often make the silhouette's midpoint
    # a poor ground pivot. Register the torso against the original standing key.
    band = image.getchannel('A').crop((0, image.height * 3 // 8, image.width, image.height * 5 // 8))
    weights = list(band.resize((band.width, 1), Image.Resampling.BOX).getdata())
    return sum(i * weight for i, weight in enumerate(weights)) / sum(weights)


WALK_ROWS = {'leoric': (0, 201, 402, 604, 803, 1006), 'lazarus': (0, 201, 402, 604, 805, 1006),
             'diablo': (0, 201, 402, 593, 793, 1006)}


def gait(body, direction, stand):
    idle, pivot = stand
    with Image.open(ART / f'{body}-walk.png') as sheet:
        top, bottom = WALK_ROWS[body][direction:direction + 2]
        poses = []
        for col in range(8):
            cell = sheet.crop((round(col * sheet.width / 8), top, round((col+1) * sheet.width / 8), bottom))
            bounds = cell.getchannel('A').point(lambda a: 255 if a >= 120 else 0).getbbox()
            poses.append((cell.crop(bounds), bounds[3]))
    scale = idle.height / median(im.height for im, _ in poses)
    baseline = median(foot for _, foot in poses)
    offset = pivot - chest_center(idle)
    frames = []
    for im, foot in poses:
        key = resize_key(im, tuple(round(v * scale) for v in im.size), body, direction)
        frames.append((key, round(chest_center(key) + offset), round((baseline - foot) * scale)))
    return frames


def turn_key(body, direction, stand, next_stand):
    row = list(SHEETS).index(body)
    edges = (0, 362, 720, 1086)
    with Image.open(ART / 'turns.png') as sheet:
        cell = sheet.crop((direction*362, edges[row], (direction+1)*362, edges[row+1]))
    tile = cell.crop(cell.getchannel('A').point(lambda a: 255 if a >= 120 else 0).getbbox())
    height = round((stand[0].height + next_stand[0].height) / 2)
    key = resize_key(tile, (round(tile.width * height / tile.height), height), body, direction)
    delta = sum(pivot - chest_center(im) for im, pivot in (stand, next_stand)) / 2
    return [(key, round(chest_center(key) + delta), 0)]


def diablo_stances():
    # Half a revolution in 22.5-degree increments. Register between the two
    # hooves so a projecting wing or tail never moves the demon off his tile.
    hoof_centers = (209, 608, 1016, 176, 580, 1017, 180, 603, 1043)
    poses = []
    with Image.open(ART / 'diablo-stand.png') as sheet:
        for heading, hoof_center in enumerate(hoof_centers):
            row, col = divmod(heading, 3)
            left, top = col * sheet.width // 3, row * sheet.height // 3
            cell = sheet.crop((left, top, (col+1)*sheet.width//3, (row+1)*sheet.height//3))
            bounds = cell.getchannel('A').point(lambda a: 255 if a >= 120 else 0).getbbox()
            tile = cell.crop(bounds)
            scale = .49
            key = resize_key(tile, tuple(round(v*scale) for v in tile.size), 'diablo', heading // 2)
            poses.append((key, round((hoof_center - left - bounds[0]) * scale)))
    return poses


def breathing(stand, direction):
    # Only this small patch of ribs/side expands. Head, wings and ground pivot are invariant.
    image, pivot = stand
    bounds = ((.33,.35,.67,.51), (.20,.36,.55,.52), (.09,.38,.40,.52),
              (.15,.35,.37,.51), (.36,.28,.65,.49))[direction]
    left, top, right, bottom = (round(v * dim) for v, dim in zip(bounds, image.size*2))
    ribs = image.crop((left, top, right, bottom))
    w, h = ribs.size
    frames = []
    for i in range(12):
        swell = (1 - cos(2*pi*i/12)) / 2
        def source(x, y):
            weight = sin(pi*x/w) * sin(pi*y/h)
            return (x - swell*2.3*weight*(2*x/w-1), y + swell*2.0*weight)
        mesh = []
        for y in range(0, h, 5):
            for x in range(0, w, 5):
                r, b = min(w, x+5), min(h, y+5)
                quad = source(x,y) + source(x,b) + source(r,b) + source(r,y)
                mesh.append(((x,y,r,b), quad))
        frame = image.copy()
        frame.paste(ribs.transform(ribs.size, Image.Transform.MESH, mesh, Image.Resampling.BICUBIC), (left,top))
        frames.append((frame, pivot, 0))
    return frames


def pack_motion(body, direction, frames):
    strip = Image.new('RGBA', (CELL * len(frames), CELL))
    for i, (im, pivot, lift) in enumerate(frames):
        x, y = GROUND[0]-pivot, GROUND[1]-im.height-lift
        if not (0 <= x <= CELL-im.width and 0 <= y <= CELL-im.height):
            raise ValueError(f'Clipped motion frame {body}/{direction}/{i}')
        strip.paste(im, (i*CELL+x, y))
    return pack_strip(strip, EMBER if body == 'diablo' and direction < 3 else ())


def encode(body, direction, action, keys):
    sequence = SEQUENCES['attack' if action == 'cast' else action]
    burning = body == 'diablo' and action == 'death'
    if burning:
        sequence = sequence + [(4, 0, 0, 0)] * 4
    strip = Image.new('RGBA', (CELL * len(sequence), CELL))
    for frame, (pose, shift, lift, height_change) in enumerate(sequence):
        key, pivot = keys[pose]
        if action == 'cast' and body == 'lazarus':
            lift += (0, 3, 6, 7, 5, 2)[frame]
        img = key.resize((key.width, key.height + height_change), Image.Resampling.LANCZOS)
        if burning and frame >= len(SEQUENCES['death']):
            burn(img, direction, frame - len(SEQUENCES['death']))
        if pose == 4:
            lift -= min(54, round(img.height * .37))
        # Down/up attacks extend in depth, with no sidestep at the pivot.
        x = GROUND[0] - pivot + (shift if direction in (1, 2, 3) else 0)
        y = GROUND[1] - img.height - lift
        if x < 0 or y < 0 or x + img.width > CELL or y + img.height > CELL:
            raise ValueError(f'Clipped {body}/{action}/{direction}/{frame}')
        strip.paste(img, (frame * CELL + x, y))
    colors = EMBER if body == 'diablo' and direction < 3 and not burning else ()
    return pack_strip(strip, colors)


def pack_strip(strip, colors):
    mask = strip.getchannel('A').point(lambda a: 255 if a >= 100 else 0)
    rgb = Image.new('RGB', strip.size)
    rgb.paste(strip.convert('RGB'), mask=mask)
    return encode_frames(rgb, mask, CELL, *GROUND, reserved_colors=colors)


def main():
    data = bytearray()
    index = bytearray()
    for body in SHEETS:
        records = {}
        directions = [painted_keys(body, d) for d in range(5)]
        stances = diablo_stances() if body == 'diablo' else None
        for direction, keys in enumerate(directions):
            # The painted walk was registered to the original stride sheet.
            walk = gait(body, direction, keys[0])
            if stances:
                keys[0] = stances[2 * direction]
                turn = [(*stances[min(2 * direction + 1, 8)], 0)]
            elif direction < 4:
                turn = turn_key(body, direction, keys[0], directions[direction+1][0])
            else:
                turn = [(*keys[0], 0)]
            idle = keys[0]
            motion = {
                'walk': walk,
                'idle': breathing(idle, direction) if body == 'diablo' else [(*idle, 0)],
                'turn': turn,
            }
            for action in (*motion, *SEQUENCES, 'cast'):
                if action == 'cast' and body != 'lazarus':
                    records[action, direction] = records['attack', direction]
                    continue
                block = pack_motion(body, direction, motion[action]) if action in motion else encode(body, direction, action, keys)
                records[action, direction] = pack('<III', len(data), len(block), 0)
                data.extend(block)
        for group, action in enumerate(ACTIONS):
            for direction in range(5):
                index.extend(records[{8: 'turn', 12: 'cast'}.get(group, action), direction])
    (ART / 'campaign-bosses.mul').write_bytes(data)
    (ART / 'campaign-bosses.idx').write_bytes(index)


if __name__ == '__main__':
    main()
