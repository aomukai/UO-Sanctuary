#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-or-later
"""Register the approved illustration to the existing Diablo cave, offline."""
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

ART = Path(__file__).resolve().parents[1] / "art/world/diablo-ritual"
SIZE = 1536  # Half gameplay resolution, 11 px per projected tile axis.
MARGIN = 160
# (plan x, plan y, painting x, painting y). Plan coordinates use a 750 px
# proof of the centered, 81-tile cave. Source landmarks sit on the ground
# at the base of walls and on the upper lips of the lava banks.
LANDMARKS = [
    # Center and ends of the four line-of-sight shelves.
    (375, 375, 627, 627), (505, 205, 820, 318), (547, 244, 902, 398),
    (505, 544, 826, 910), (545, 505, 911, 829), (205, 505, 339, 831),
    (244, 545, 409, 913), (177, 293, 288, 495), (218, 252, 366, 412),
    # Outer wall feet, arrival landing and bridge corners.
    (31, 402, 65, 705), (38, 383, 69, 650),
    (390, 33, 643, 200), (422, 33, 682, 207), (712, 322, 1182, 611),
    (714, 422, 1181, 710), (426, 708, 690, 1109), (322, 708, 557, 1110),
    (166, 548, 323, 930), (550, 588, 918, 919),
    # The long north and south aprons have fewer painted pixels per tile.
    # Pin successive bands of paving so wall faces keep their thickness.
    (400, 72, 656, 219), (385, 116, 640, 247), (367, 162, 615, 276),
    (380, 199, 629, 303), (344, 114, 572, 236), (329, 158, 553, 271),
    (310, 198, 526, 306), (449, 123, 731, 251), (460, 171, 752, 295),
    (400, 578, 655, 966), (397, 635, 651, 1028), (377, 685, 625, 1089),
    (288, 625, 487, 1022), (474, 625, 802, 1020),
    # Back lava opening: outer bank, then platform lip.
    (283, 254, 454, 409), (327, 231, 517, 355), (355, 230, 593, 332),
    (399, 235, 670, 329), (438, 242, 733, 355), (467, 251, 796, 397),
    (444, 278, 751, 440), (405, 263, 679, 427), (375, 253, 626, 421),
    (342, 269, 557, 446), (307, 278, 492, 451),
    # Right opening, clockwise around the shore.
    (505, 273, 842, 449), (535, 304, 896, 489), (558, 339, 945, 548),
    (568, 369, 972, 614), (564, 409, 961, 674), (531, 448, 923, 734),
    (503, 475, 874, 799), (483, 453, 817, 775), (512, 412, 842, 711),
    (499, 396, 825, 675), (501, 360, 834, 601), (514, 338, 863, 557),
    (500, 326, 829, 534), (478, 299, 798, 495),
    # Bottom opening.
    (283, 489, 454, 848), (305, 472, 504, 789), (343, 481, 562, 807),
    (378, 486, 633, 817), (412, 480, 703, 808), (444, 471, 751, 790),
    (470, 497, 789, 842), (431, 506, 726, 875), (382, 517, 635, 897),
    (342, 517, 568, 894), (309, 505, 506, 875),
    # Left opening.
    (246, 273, 393, 450), (272, 299, 433, 492), (245, 326, 410, 530),
    (225, 338, 382, 558), (249, 357, 407, 599), (249, 395, 402, 669),
    (235, 413, 390, 697), (268, 446, 433, 751), (246, 476, 394, 792),
    (209, 445, 342, 752), (194, 410, 306, 690), (194, 366, 293, 626),
    (194, 328, 315, 553), (205, 306, 339, 501),
    # Outer faces follow the wall feet, including their extension above or
    # below the walkable diamond. Leave a margin for the highest rear pillars.
    (388, -24, 634, 24), (419, -24, 688, 25), (268, 101, 423, 168),
    (560, 140, 919, 238), (726, 291, 1206, 554), (727, 424, 1206, 737),
    (732, 366, 1210, 662), (20, 373, 46, 612), (19, 416, 46, 732),
    (169, 556, 315, 948), (322, 740, 554, 1180), (422, 740, 688, 1184),
    (380, 752, 627, 1220), (540, 620, 880, 981),
    (140, 185, 217, 304), (194, 129, 309, 221),
    (-80, -80, -100, -100), (830, -80, 1354, -100),
    (830, 830, 1354, 1354), (-80, 830, -100, 1354),
]
# Locate bridge boundaries in actual tile coordinates; both banks are narrower
# in the illustration than the existing five-tile crossing.
unit = 11 * 750 / SIZE
for tile_x, tile_y, sx, sy in [
    (2.5, 4.5, 259, 320), (2.5, -4.5, 296, 287), (5, 0, 295, 314),
    (7.5, 4.5, 284, 347), (7.5, -4.5, 335, 301),
    (7.5, 2.5, 294, 348), (7.5, -2.5, 331, 311),
    (11.5, 2.5, 349, 386), (11.5, -2.5, 388, 353),
    (9.5, 0, 341, 347), (12, 0, 374, 371),
    (7.5, 5.5, 277, 375), (11.5, 5.5, 314, 405), (9.5, 4, 309, 382),
    (7.5, -5.5, 340, 293), (11.5, -5.5, 397, 325), (9.5, -4, 368, 320),
]:
    LANDMARKS.append((375 + (tile_x - 40 - tile_y) * unit,
                      375 + (tile_x - 40 + tile_y) * unit, sx, sy))


def radial(x, y):
    squared = x * x + y * y
    return squared * math.log(squared) if squared else 0.0


def registration():
    """Fit the two thin-plate splines that map floor pixels back to the painting."""
    # Normalized coordinates keep the linear system well conditioned.
    controls = [(x / 750, y / 750, sx, sy) for x, y, sx, sy in LANDMARKS]
    rows = [[radial(x - u, y - v) for u, v, _, _ in controls] + [1, x, y, sx, sy]
            for x, y, sx, sy in controls]
    for column in ([1] * len(controls), [x for x, _, _, _ in controls], [y for _, y, _, _ in controls]):
        rows.append(column + [0, 0, 0, 0, 0])
    for i in range(len(rows)):
        pivot = max(range(i, len(rows)), key=lambda j: abs(rows[j][i]))
        rows[i], rows[pivot] = rows[pivot], rows[i]
        row = [value / rows[i][i] for value in rows[i]]
        rows[i] = row
        for j, other in enumerate(rows):
            if j != i:
                factor = other[i]
                rows[j] = [a - factor * b for a, b in zip(other, row)]
    weights = [row[-2:] for row in rows]

    def sample(px, py):
        x, y = (px - MARGIN) / SIZE, (py - MARGIN) / SIZE
        sx, sy = [a + b * x + c * y for a, b, c in zip(*weights[-3:])]
        for (u, v, _, _), (wx, wy) in zip(controls, weights):
            k = radial(x - u, y - v)
            sx += wx * k
            sy += wy * k
        return sx, sy
    return sample


def main():
    with Image.open(ART / "proposals/volcanic-chamber-base.png") as painting:
        assert painting.size == (1254, 1254)
        step = 8
        extent = SIZE + 2 * MARGIN
        sample = registration()
        coordinates = range(0, extent + 1, step)
        points = {(x, y): sample(x, y) for x in coordinates for y in coordinates}
        mesh = []
        for y in range(0, extent, step):
            for x in range(0, extent, step):
                right, bottom = x + step, y + step
                corners = ((x, y), (x, bottom), (right, bottom), (right, y))
                mesh.append(((x, y, right, bottom), sum((points[p] for p in corners), ())))
        # Strip the studio backdrop, retaining dark crevices enclosed by the silhouette.
        mask = painting.convert("L").point(lambda value: 255 if value >= 30 else 0)
        mask = mask.filter(ImageFilter.MaxFilter(7))
        ImageDraw.floodfill(mask, (0, 0), 128)
        mask = mask.point(lambda value: 0 if value == 128 else 255).filter(ImageFilter.GaussianBlur(2))
        scene = Image.composite(painting.convert("RGB"), Image.new("RGB", painting.size), mask)
        floor = scene.transform((extent, extent), Image.Transform.MESH, mesh, Image.Resampling.BICUBIC)
    with Image.open(ART / "uo-bloody-pentagram.png") as original:
        assert original.size == (211, 169)
        seal = original.resize((original.width * 2, original.height * 2), Image.Resampling.NEAREST)
        floor.paste(seal, ((extent - seal.width) // 2, (extent - seal.height) // 2), seal)
    floor.save(ART / "volcanic-chamber.png")


if __name__ == "__main__":
    main()
