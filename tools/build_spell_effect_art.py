#!/usr/bin/env python3
"""Pack the generated spell sprite sheets into ClassicUO loose statics."""

from pathlib import Path
from itertools import islice

from PIL import Image, ImageEnhance, ImageFilter, ImageOps

from build_succubus_bolt import encode_static


ROOT = Path(__file__).resolve().parents[1]
ART = ROOT / "art" / "spell-effects"


def frame(cell, scale, origin, canvas_size, bottom_padding, alpha_threshold=90, outline=None):
    image = cell.resize(tuple(round(size * scale) for size in cell.size), Image.Resampling.LANCZOS)
    width, height = canvas_size
    pivot = (width // 2, height - bottom_padding)
    offset = tuple(anchor - round(source * scale) for anchor, source in zip(pivot, origin))
    canvas = Image.new("RGBA", canvas_size)
    canvas.paste(image, offset)
    if outline is not None:
        mask = canvas.getchannel("A").point(lambda a: 255 if a >= alpha_threshold else 0)
        bordered = Image.new("RGBA", canvas_size)
        bordered.paste(outline, (0, 0, width, height), mask.filter(ImageFilter.MaxFilter(3)))
        bordered.paste(canvas, (0, 0), mask)
        canvas = bordered
    pixels = [
        max(1, (red >> 3) << 10 | (green >> 3) << 5 | (blue >> 3)) if alpha >= alpha_threshold else 0
        for red, green, blue, alpha in canvas.getdata()
    ]
    if not any(pixels):
        raise ValueError("Spell frame has no opaque art after packing")
    return encode_static(width, height, pixels)


def bone_spear():
    with Image.open(ART / "bone-spear" / "sheet.png") as sheet:
        if sheet.size != (1269, 1240) or sheet.mode != "RGBA":
            raise ValueError("Expected the approved 4-by-4 RGBA sheet")
        columns = (0, 320, 635, 950, 1269)
        rows = (0, 313, 622, 941, 1240)
        cells = [
            sheet.crop((columns[col], rows[row], columns[col + 1], rows[row + 1]))
            for row in range(4) for col in range(4)
        ]

    for index, cell in enumerate(cells[:8]):
        left, top, right, bottom = cell.getchannel("A").point(lambda a: 255 if a >= 96 else 0).getbbox()
        cells[index] = frame(cell, 0.16, ((left + right) / 2, (top + bottom) / 2), (56, 62), 40)

    # Align the shatter on the original impact point through expansion and fade.
    contact_source = cells[8]
    anchors = ((158, 150), (149, 156), (149, 157), (149, 154),
               (158, 145), (149, 141), (149, 142), (149, 138))
    for index, (cell, origin) in enumerate(zip(cells[8:], anchors), 8):
        cells[index] = frame(cell, 0.31, origin, (56, 62), 40,
                             alpha_threshold=40 if index == 15 else 90)

    # The contact cue uses just the tight bright core of the initial burst.
    cells.append(frame(contact_source.crop((126, 126, 178, 177)), 0.24, (25, 25), (56, 62), 40))
    return cells


def meteor():
    with Image.open(ART / "meteor" / "sheet.png") as sheet:
        if sheet.size != (1263, 1246) or sheet.mode != "RGBA":
            raise ValueError("Expected the generated Meteor RGBA sheet")
        recipes = (
            (0.44, (158, 164), (136, 98), 22),  # Warning ring ground center.
            (0.26, (245, 224), (120, 95), 22),  # Falling rock's leading tip.
            (0.41, (155, 185), (136, 108), 24),  # Impact origin stays grounded.
            (0.16, (155, 196), (48, 50), 22),   # One fire patch per live field.
        )
        for row, (scale, origin, size, padding) in enumerate(recipes):
            for column in range(4):
                box = tuple(round(n * s / 4) for n, s in zip(
                    (column, row, column + 1, row + 1), sheet.size * 2
                ))
                yield frame(sheet.crop(box), scale, origin, size, padding, alpha_threshold=128)


def load_grid(name, expected_size, rows=4, columns=4, filename="sheet.png"):
    with Image.open(ART / name / filename) as sheet:
        if sheet.mode != "RGBA" or sheet.size != expected_size:
            raise ValueError(f"Unexpected {name} RGBA sprite sheet")
        return [
            [sheet.crop(tuple(round(n * s / count) for n, s, count in zip(
                (column, row, column + 1, row + 1), sheet.size * 2, (columns, rows) * 2
            ))) for column in range(columns)]
            for row in range(rows)
        ]


def blizzard():
    fall, impact, contact, frost = load_grid("blizzard", (1292, 1217))
    tips = ((203, 219), (213, 241), (220, 265), (231, 291))
    for image, origin, elevation in zip(fall, tips, (100, 80, 60, 40)):
        yield frame(image, 0.17, origin, (56, 146), elevation, alpha_threshold=165)
    for image in impact:
        yield frame(image, 0.17, (160, 269), (60, 65), 22, alpha_threshold=170)
    for image, origin in zip(contact, ((161, 171), (163, 169), (158, 165), (170, 166))):
        yield frame(image, 0.13, origin, (42, 56), 36, alpha_threshold=154)
    for image in frost:
        yield frame(image, 0.15, (158, 176), (52, 45), 22, alpha_threshold=168)


def frozen_orb():
    core, bolts, burst, contact = load_grid("frozen-orb", (1269, 1240))
    for image, origin in zip(core, ((171, 173), (161, 165), (152, 173), (172, 170))):
        yield frame(image, 0.15, origin, (56, 72), 41, alpha_threshold=176)
    for facing in range(8):
        for image, origin in zip(bolts, ((161, 177), (165, 176), (153, 178), (161, 179))):
            centered = Image.new("RGBA", (384, 384))
            centered.paste(image, (192 - origin[0], 192 - origin[1]))
            rotated = centered.rotate(-45 * facing, resample=Image.Resampling.BICUBIC)
            yield frame(rotated, 0.084, (192, 192), (36, 56), 39, alpha_threshold=168)
    for image, origin in zip(burst, ((175, 151), (160, 149), (153, 149), (162, 149))):
        yield frame(image, 0.21, origin, (76, 84), 41, alpha_threshold=168)
    for image, origin, threshold in zip(contact,
            ((172, 153), (171, 151), (134, 151), (148, 144)), (149, 149, 149, 94)):
        yield frame(image, 0.12, origin, (32, 54), 37, alpha_threshold=threshold)


def strafe():
    with Image.open(ART / "strafe" / "sheet.png") as sheet:
        if sheet.size != (2172, 724) or sheet.mode != "RGBA":
            raise ValueError("Expected the Strafe arrow and crossbow bolt sheet")
        for column, (target_width, bounds) in enumerate((
                (30, (135, 290, 1026, 421)), (24, (259, 285, 844, 428)))):
            left, top, right, bottom = bounds
            x = column * (sheet.width // 2)
            source = sheet.crop((x + left, top, x + right, bottom))
            center = 512
            centered = Image.new("RGBA", (2 * center, 2 * center))
            centered.paste(source, (center - source.width // 2, center - source.height // 2))
            for facing in range(8):
                rotated = centered.rotate(-45 * facing, resample=Image.Resampling.BICUBIC)
                yield frame(rotated, target_width / source.width, (center, center), (48, 58), 38,
                            alpha_threshold=142)


def directional_flight(images, anchors, scale, size, padding, alpha_threshold, center=256):
    centered_frames = []
    for image, (x, y) in zip(images, anchors):
        centered = Image.new("RGBA", (2 * center, 2 * center))
        centered.paste(image, (center - x, center - y))
        centered_frames.append(centered)
    for facing in range(8):
        for centered in centered_frames:
            rotated = centered.rotate(-45 * facing, resample=Image.Resampling.BICUBIC)
            yield frame(rotated, scale, (center, center), size, padding, alpha_threshold=alpha_threshold)


def directional_missile(name, flight_anchors, flight_scale, impact_anchors, impact_scale, size,
                        flight_padding=40, impact_padding=40, impact_size=None, alpha_threshold=150):
    flight, impacts = load_grid(name, (1774, 887), rows=2)
    yield from directional_flight(flight, flight_anchors, flight_scale, size, flight_padding, alpha_threshold)
    for image, origin in zip(impacts, impact_anchors):
        yield frame(image, impact_scale, origin, impact_size or size, impact_padding, alpha_threshold=alpha_threshold)


def teeth():
    return directional_missile(
        "teeth", ((233, 247),) * 4, 0.087,
        ((223, 231), (222, 229), (219, 229), (229, 239)), 0.20, (40, 60)
    )


def bone_spirit():
    return directional_missile(
        "bone-spirit", ((260, 227), (267, 223), (268, 220), (272, 222)), 0.16,
        ((230, 221), (235, 221), (223, 221), (222, 223)), 0.20, (62, 76)
    )


def abyss():
    opening, field, pull, collapse = load_grid("abyss", (1448, 1086))
    for images, anchors, threshold in (
        (opening, ((182, 155), (181, 155), (182, 154), (182, 154)), 190),
        (field, ((183, 150), (183, 151), (183, 151), (181, 152)), 190),
        (pull, ((181, 151), (180, 151), (180, 150), (179, 149)), 190),
        (collapse, ((181, 151), (181, 151), (182, 151), (178, 151)), 175),
    ):
        for image, anchor in zip(images, anchors):
            yield frame(image, 0.34, anchor, (110, 76), 32, alpha_threshold=threshold)


def charged_bolt():
    return directional_missile(
        "charged-bolt", ((232, 242), (233, 236), (224, 230), (222, 230)), 0.078,
        ((227, 220), (225, 223), (219, 223), (222, 223)), 0.13, (56, 66), flight_padding=29
    )


def lightning():
    return directional_missile(
        "lightning", ((221, 234),) * 4, 0.155,
        ((225, 220), (221, 210), (219, 211), (220, 223)), 0.18, (68, 78)
    )


def chain_lightning():
    return directional_missile(
        "chain-lightning", ((219, 225), (219, 222), (219, 223), (221, 224)), 0.143,
        ((220, 219), (220, 224), (222, 222), (226, 222)), 0.147, (68, 76)
    )


def static_field():
    rings, impacts = load_grid("static-field", (1774, 887), rows=2)
    # GameEffectView adds back the vertical space below this ground pivot.
    for scale in (0.36, 0.53, 0.70):
        for image, origin in zip(rings, ((220, 230), (216, 225), (223, 228), (221, 227))):
            yield frame(image, scale, origin, (310, 214), 116, alpha_threshold=170)
    for image, origin in zip(impacts, ((220, 214), (219, 216), (218, 217), (220, 217))):
        yield frame(image, 0.128, origin, (54, 60), 29, alpha_threshold=162)


def thunder_storm():
    charge, strike = load_grid("thunder-storm", (1774, 887), rows=2)
    for image, origin in zip(charge, ((226, 197), (224, 195), (222, 190), (224, 193))):
        yield frame(image, 0.155, origin, (76, 112), 86, alpha_threshold=176)
    for image, origin in zip(strike, ((224, 397), (219, 397), (220, 397), (219, 397))):
        yield frame(image, 0.30, origin, (98, 157), 29, alpha_threshold=173)


def ice_bolt():
    return directional_missile(
        "ice-bolt", ((240, 228),) * 4, 0.080,
        ((220, 223), (219, 224), (218, 222), (221, 221)), 0.16, (44, 62)
    )


def ice_blast():
    return directional_missile(
        "ice-blast", ((250, 221),) * 4, 0.128,
        ((223, 220), (222, 219), (220, 218), (221, 221)), 0.18, (62, 72)
    )


def glacial_spike():
    return directional_missile(
        "glacial-spike", ((240, 218),) * 4, 0.132,
        ((217, 334), (215, 342), (216, 342), (212, 341)), 0.30, (66, 76),
        impact_padding=39, impact_size=(140, 146), alpha_threshold=185
    )


def frost_nova():
    rings, impacts = load_grid("frost-nova", (1774, 887), rows=2)
    for image, origin in zip(rings, ((218, 224), (208, 225), (204, 225), (212, 226))):
        yield frame(image, 0.69, origin, (310, 214), 116, alpha_threshold=198)
    for image, origin in zip(impacts, ((218, 222), (219, 222), (220, 224), (221, 222))):
        yield frame(image, 0.15, origin, (48, 60), 32, alpha_threshold=176)


def cold_status():
    chill, freeze = load_grid("cold-status", (1774, 887), rows=2)
    for image in chill:
        # Anchor the open space on the soles and trim the fringe of the mist.
        yield frame(image, 0.142, (220, 265), (62, 62), 36, alpha_threshold=207)
    for image in freeze:
        yield frame(image, 0.183, (221, 282), (70, 73), 36, alpha_threshold=186)
    # Reuse the Ice Blast fragments at ground level for the dying and thawing ice.
    _, cracks = load_grid("ice-blast", (1774, 887), rows=2)
    anchors = ((223, 220), (222, 219), (220, 218), (221, 221))
    for scale, size in ((0.27, (76, 77)), (0.14, (44, 57))):
        for image, anchor in zip(cracks, anchors):
            yield frame(image, scale, anchor, size, 38, alpha_threshold=176)


def fire_bolt():
    return directional_missile(
        "fire-bolt", ((222, 228),) * 4, 0.164,
        ((225, 220), (218, 221), (222, 220), (218, 222)), 0.21, (54, 64),
        alpha_threshold=163
    )


def inferno():
    return directional_missile(
        "inferno", ((227, 231), (230, 231), (230, 230), (230, 232)), 0.150,
        ((224, 305), (220, 305), (215, 305), (218, 305)), 0.15, (70, 72),
        impact_padding=34, alpha_threshold=162
    )


def blaze():
    for images, scale, anchors in zip(
        load_grid("blaze", (1448, 1086), rows=3),
        (0.245, 0.225, 0.225),
        (((180, 330), (178, 331), (179, 331), (173, 331)),
         ((178, 320), (178, 320), (177, 320), (180, 320)),
         ((175, 320), (176, 319), (176, 319), (176, 320))),
    ):
        for image, origin in zip(images, anchors):
            yield frame(image, scale, origin, (66, 88), 33, alpha_threshold=176)


def fire_wall():
    rows = load_grid("fire-wall", (1448, 1086), rows=3)
    anchors = (((183, 318), (179, 318), (173, 318), (174, 318)),
               ((177, 324), (177, 324), (176, 324), (173, 324)),
               ((178, 323), (178, 323), (178, 323), (175, 323)))
    for images, pivots in zip(rows, anchors):
        for image, pivot in zip(images, pivots):
            yield frame(image, 0.276, pivot, (72, 125), 33, alpha_threshold=175)


def ward_rows(rows, stages):
    for images, (origin, scale, padding) in zip(rows, stages):
        for image in images:
            yield frame(image, scale, origin, (116, 110), padding, alpha_threshold=184)


def frozen_armor():
    with Image.open(ART / "frozen-armor" / "sheet.png") as sheet:
        if sheet.size != (1454, 1082) or sheet.mode != "RGBA":
            raise ValueError("Unexpected Frozen Armor RGBA sheet")
        # The generated rows have uneven vertical gutters.
        columns = (25, 374, 723, 1072, 1454)
        rows = (0, 320, 585, 865, 1082)
        cells = [[sheet.crop((columns[c], rows[r], columns[c + 1], rows[r + 1]))
                  for c in range(4)] for r in range(4)]
    return ward_rows(cells, (((176, 254), 0.25, 38), ((175, 204), 0.254, 38),
                             ((171, 195), 0.267, 38), ((173, 165), 0.247, 38)))


def shiver_armor():
    return ward_rows(load_grid("shiver-armor", (1448, 1086)), (
        ((181, 262), 0.28, 45), ((180, 261), 0.28, 45),
        ((180, 261), 0.20, 34), ((181, 232), 0.275, 45)
    ))


def chilling_armor():
    return ward_rows(load_grid("chilling-armor", (1448, 1086)), (
        ((181, 253), 0.25, 43), ((181, 251), 0.25, 43),
        ((182, 250), 0.267, 43), ((181, 247), 0.248, 43)
    ))


def energy_shield():
    cast, ward, catches, fade = load_grid("energy-shield", (1448, 1086))
    yield from ward_rows((cast, ward), (((182, 247), 0.34, 32), ((181, 247), 0.34, 32)))
    # The source hit frames include the shield. Keep the flash and the outer
    # edge on the struck side, then turn that fragment about the shield center.
    center = 192
    for facing in range(8):
        for image in catches:
            right_edge = image.crop((247, 151, 344, 231))
            centered = Image.new("RGBA", (2 * center, 2 * center))
            centered.paste(right_edge, (center + 247 - 181, center + 151 - 188))
            turned = centered.rotate(-45 * facing, resample=Image.Resampling.BICUBIC)
            turned = turned.resize((turned.width, 3 * turned.height // 4), Image.Resampling.LANCZOS)
            yield frame(turned, 0.34, (center, 3 * center // 4), (116, 110), 52, alpha_threshold=178)
    for image in fade:
        yield frame(image, 0.34, (181, 247), (116, 110), 32, alpha_threshold=165)


def nova():
    # The image has three equal rows, with the ring centered below its row midpoint.
    rings, contacts, casts = load_grid("nova", (1536, 1024), rows=3)
    for image, origin, scale in zip(rings, ((192, 204), (190, 202), (189, 198), (192, 194)),
                                    (0.36, 0.55, 0.73, 0.96)):
        yield frame(image, scale, origin, (360, 230), 116, alpha_threshold=195)
    for image, origin in zip(contacts, ((194, 181), (197, 193), (189, 191), (185, 181))):
        yield frame(image, 0.18, origin, (60, 64), 48, alpha_threshold=193)
    for image, origin in zip(casts, ((190, 266), (192, 266), (183, 266), (181, 266))):
        yield frame(image, 0.18, origin, (58, 103), 35, alpha_threshold=194)


def telekinesis():
    with Image.open(ART / "telekinesis" / "sheet.png") as sheet:
        if sheet.size != (1774, 887) or sheet.mode != "RGBA":
            raise ValueError("Unexpected Telekinesis RGBA sheet")
        boundaries = (0, 315, 595, 887)
        rows = [[sheet.crop((round(sheet.width * c / 4), top, round(sheet.width * (c + 1) / 4), bottom))
                 for c in range(4)] for top, bottom in zip(boundaries, boundaries[1:])]
    casts, contacts, objects = rows
    for image, origin in zip(casts, ((228, 260), (221, 260), (219, 260), (216, 260))):
        yield frame(image, 0.20, origin, (90, 100), 35, alpha_threshold=196)
    for image, origin in zip(contacts, ((215, 137), (220, 145), (215, 142), (221, 141))):
        yield frame(image, 0.19, origin, (90, 108), 54, alpha_threshold=196)
    for image, origin in zip(objects, ((222, 162), (219, 159), (222, 164), (221, 156))):
        yield frame(image, 0.16, origin, (84, 78), 34, alpha_threshold=196)


def teleport():
    departure, arrival = load_grid("teleport", (1774, 887), rows=2)
    anchors = (((225, 367), (226, 365), (222, 365), (221, 365)),
               ((223, 369), (220, 366), (220, 368), (218, 369)))
    for animation, origins in zip((departure, arrival), anchors):
        for image, origin in zip(animation, origins):
            yield frame(image, 0.24, origin, (100, 142), 34, alpha_threshold=198)


def enchant():
    with Image.open(ART / "enchant" / "sheet.png") as sheet:
        if sheet.size != (1261, 1247) or sheet.mode != "RGBA":
            raise ValueError("Unexpected Enchant RGBA sheet")
        boundaries = (0, 350, 613, 920, 1247)
        rows = [[sheet.crop((round(sheet.width * c / 4), top, round(sheet.width * (c + 1) / 4), bottom))
                 for c in range(4)] for top, bottom in zip(boundaries, boundaries[1:])]
    for stage, (origins, scale, padding, thresholds) in enumerate((
        (((166, 268), (159, 268), (157, 268), (152, 268)), 0.23, 35, (185,) * 4),
        (((162, 122), (153, 118), (160, 120), (153, 121)), 0.23, 52, (194,) * 4),
        (((162, 154), (163, 147), (160, 149), (147, 153)), 0.14, 54, (185,) * 4),
        (((162, 201), (156, 201), (157, 201), (153, 201)), 0.17, 52, (172, 172, 172, 130)),
    )):
        for image, origin, alpha in zip(rows[stage], origins, thresholds):
            yield frame(image, scale, origin, (86, 116), padding, alpha_threshold=alpha)


def hydra_head():
    with Image.open(ART / "hydra" / "sheet.png") as sheet:
        if sheet.size != (1312, 1199) or sheet.mode != "RGBA":
            raise ValueError("Unexpected Hydra head RGBA sheet")
        stages = (
            (0, 300, (0, 328, 656, 984, 1312), ((184, 265), (173, 265), (153, 265), (142, 265))),
            (300, 605, (0, 328, 656, 984, 1312), ((180, 265), (164, 265), (153, 265), (145, 265))),
            (605, 910, (0, 328, 668, 995, 1312), ((181, 256), (154, 257), (112, 257), (134, 257))),
            (910, 1199, (0, 328, 656, 984, 1312), ((174, 249), (164, 249), (155, 249), (148, 249))),
        )
        cells = [(sheet.crop((left, top, right, bottom)), anchor)
                 for top, bottom, columns, anchors in stages
                 for left, right, anchor in zip(columns, columns[1:], anchors)]
    for image, origin in cells:
        yield frame(image, 0.19, origin, (112, 108), 22, alpha_threshold=178)
    for image, origin in cells:
        yield frame(image.transpose(Image.Transpose.FLIP_LEFT_RIGHT), 0.19,
                    (image.width - origin[0], origin[1]), (112, 108), 22, alpha_threshold=178)


def hydra_bolt():
    return directional_missile(
        "hydra-bolt", ((332, 226), (331, 225), (330, 224), (333, 224)), 0.087,
        ((221, 222), (219, 218), (235, 219), (222, 222)), 0.131, (72, 86),
        flight_padding=52, impact_padding=52, alpha_threshold=162
    )


def amazon_arcane_arrows():
    rows = load_grid("amazon-arcane-arrows", (1261, 1247))
    for row, centers, scale in zip(rows, (
        ((159, 169), (160, 170), (161, 169), (161, 169)),
        ((159, 168), (158, 168), (161, 169), (159, 168)),
        ((161, 157), (162, 157), (161, 158), (160, 156)),
        ((159, 154), (161, 154), (159, 155), (160, 152)),
    ), (0.129, 0.123, 0.141, 0.127)):
        yield from directional_flight(row, centers, scale, (52, 62), 39, 181)


def amazon_arrow_impact():
    rows = load_grid("amazon-arrow-impact", (1448, 1086), rows=3)
    for row, anchors, scale in zip(rows, (
        ((168, 207), (171, 208), (166, 208), (166, 210)),
        ((180, 174), (175, 173), (174, 174), (170, 173)),
        ((131, 177), (136, 178), (129, 174), (129, 174)),
    ), (0.145, 0.151, 0.136)):
        for image, anchor in zip(row, anchors):
            yield frame(image, scale, anchor, (66, 73), 46, alpha_threshold=172)


def elemental_arrow_flight(name, pivots):
    rows = load_grid(name, (1024, 1536), rows=3, columns=2)
    for row, anchors in zip(rows, pivots):
        for image, anchor, scale in zip(row, anchors, (0.094, 0.087)):
            yield from directional_flight([image], [anchor], scale, (54, 62), 39, 207)


def grid_effects(name, expected_size, pivots, scales, size, padding, alpha_threshold):
    for row, anchors, scale in zip(load_grid(name, expected_size, rows=len(pivots)), pivots, scales):
        for image, anchor in zip(row, anchors):
            yield frame(image, scale, anchor, size, padding, alpha_threshold=alpha_threshold)


def javelin_flight():
    rows = load_grid("javelin-flight", (1261, 1247), rows=2, columns=2)
    for row, anchors in zip(rows, (((309, 347), (320, 348)), ((307, 318), (318, 315)))):
        for image, anchor in zip(row, anchors):
            yield from directional_flight([image], [anchor], 0.076, (62, 68), 39, 182, center=352)


def amazon_marks():
    rows = load_grid("amazon-marks", (1536, 1024))
    specifications = (
        (0.23, 28, 0, 170, ((214, 172), (187, 173), (182, 171), (182, 173))),
        (0.136, 77, 35, 178, ((345, 154), (326, 154), (320, 154), (310, 154))),
        (0.24, 27, 0, 151, ((196, 158), (185, 153), (184, 151), (183, 151))),
        (0.136, 77, 0, 176, ((58, 127), (52, 126), (48, 124), (40, 127))),
    )
    for row, (scale, padding, trim_top, threshold, anchors) in zip(rows, specifications):
        for image, (x, y) in zip(row, anchors):
            cell = image.crop((0, trim_top, image.width, image.height))
            yield frame(cell, scale, (x, y - trim_top), (112, 113), padding, alpha_threshold=threshold)


def write_group(name, first_graphic, frames):
    target = ART / name / "statics"
    target.mkdir(exist_ok=True)
    for graphic, data in enumerate(frames, first_graphic):
        (target / f"{graphic}.art").write_bytes(data)


def holy_ring(cell, size, canvas_size, bottom_padding, threshold, brightness=1.0):
    alpha = cell.getchannel("A").point(lambda value: 255 if value >= threshold else 0)
    ring = cell.crop(alpha.getbbox()).resize(size, Image.Resampling.LANCZOS)
    ring = ImageEnhance.Brightness(ring).enhance(brightness)
    return frame(ring, 1, (ring.width // 2, ring.height // 2), canvas_size, bottom_padding, threshold)


def barbarian_corpse_effects():
    for name, first, layout in (
        ("barbarian-find-potion", 64813, (
            ((194, 140), (190, 137), (192, 134), (186, 132)),
            ((194, 134), (194, 135), (193, 137), (192, 138)),
            ((194, 208), (192, 208), (191, 211), (191, 211)),
            ((193, 131), (192, 131), (191, 131), (191, 131)),
        )),
        ("barbarian-find-item", 64829, (
            ((197, 134), (194, 137), (194, 136), (193, 133)),
            ((167, 206), (167, 206), (163, 206), (163, 206)),
            ((193, 132), (193, 132), (193, 132), (191, 132)),
            ((194, 145), (191, 145), (193, 146), (193, 146)),
        )),
    ):
        rows = load_grid(name, (1536, 1024))
        for index, (cells, pivots, spec) in enumerate(zip(rows, layout, (
            (.228, 43, (210, 210, 209, 198)),
            (.203, 23, (210, 209, 210, 205)),
            (.285, 32, (213, 211, 210, 167)),
            (.210, 22, (211, 209, 193, 146)),
        ))):
            scale, padding, cutoffs = spec
            write_group(name, first + index * 4, (
                frame(cell, scale, pivot, (76, 89), padding, cutoff)
                for cell, pivot, cutoff in zip(cells, pivots, cutoffs)
            ))

    name = "barbarian-grim-ward"
    rise, body, fall, mark = load_grid(name, (1536, 1024))
    for index, scale in enumerate((.241, .306, .368)):
        for first, cells, pivots in (
            (64845, rise, ((191, 228), (191, 228), (192, 229), (190, 229))),
            (64857, body, ((193, 236), (191, 236), (193, 236), (192, 236))),
            (64869, fall, ((192, 241), (192, 241), (192, 242), (192, 242))),
        ):
            write_group(name, first + index * 4, (
                frame(cell, scale, pivot, (76, 111), 21, 207)
                for cell, pivot in zip(cells, pivots)
            ))
    write_group(name, 64881, (
        frame(cell, .183, pivot, (54, 72), 49, 207)
        for cell, pivot in zip(mark, ((194, 126), (193, 126), (198, 126), (193, 126)))
    ))
    with Image.open(ART / name / "pulse-sheet.png") as sheet:
        if sheet.mode != "RGBA" or sheet.size != (1536, 1024):
            raise ValueError("Unexpected Grim Ward pulse sheet")
        pulse = [sheet.crop((column * 384, 0, (column + 1) * 384, 512)) for column in range(4)]
        write_group(name, 64885, (
            holy_ring(cell, (234, 120), (240, 132), 66, 194, brightness)
            for cell, brightness in zip(pulse, (.85, 1.04, 1.09, .78))
        ))


def barbarian_strikes():
    for name, first, layout in (
        ("barbarian-bash", 62000, (
            (.187, 40, 206, ((152, 222), (186, 195), (194, 190), (189, 187))),
            (.202, 29, 214, ((155, 247), (156, 246), (155, 246), (146, 247))),
            (.167, 20, 200, ((157, 268), (154, 268), (154, 268), (149, 268))),
        )),
        ("barbarian-stun", 62012, (
            (.193, 43, 198, ((210, 181), (205, 173), (204, 171), (197, 168))),
            (.195, 38, 205, ((200, 171), (194, 171), (195, 170), (191, 169))),
            (.163, 56, 193, ((193, 255), (193, 264), (193, 261), (193, 258))),
        )),
        ("barbarian-concentrate", 62024, (
            (.197, 41, 194, ((171, 174), (178, 178), (194, 175), (195, 174))),
            (.194, 37, 182, ((194, 170), (193, 170), (202, 171), (193, 170))),
            (.207, 42, 205, ((191, 167), (193, 166), (192, 167), (192, 165))),
        )),
        ("barbarian-berserk", 62036, (
            (.186, 43, 215, ((195, 176), (192, 174), (192, 173), (193, 174))),
            (.179, 37, 218, ((196, 191), (197, 193), (194, 193), (192, 192))),
            (.177, 44, 216, ((194, 174), (193, 174), (192, 174), (192, 174))),
        )),
    ):
        rows = load_grid(name, (1536, 1024), rows=3)
        for index, (cells, spec) in enumerate(zip(rows, layout)):
            scale, padding, threshold, pivots = spec
            write_group(name, first + index * 4, (
                frame(cell, scale, pivot, (82, 98), padding, threshold)
                for cell, pivot in zip(cells, pivots)
            ))
    _, _, ring = load_grid("barbarian-berserk", (1536, 1024), rows=3)
    write_group("barbarian-berserk", 62048, (
        frame(ImageEnhance.Brightness(cell).enhance(brightness), scale, (193, 174), (82, 98), 44, threshold)
        for cell, brightness, scale, threshold in zip(ring, (.95, .71, .47, .29),
                                                       (.177, .187, .197, .205), (216, 225, 234, 243))
    ))


def barbarian_motion():
    leap = "barbarian-leap"
    attack = "barbarian-leap-attack"
    spin = "barbarian-whirlwind"
    takeoff, flight, landing, settle = load_grid(leap, (1536, 1024))
    attack_ring, area, swing, impact = load_grid(attack, (1536, 1024))
    spinning, wake, strike, fade = load_grid(spin, (1536, 1024))

    for name, graphic, frames, thresholds in (
        (leap, 62056, landing, (205, 207, 201, 179)),
        (attack, 62096, attack_ring, (200, 206, 202, 194)),
    ):
        write_group(name, graphic, (
            holy_ring(cell, (234, 120), (240, 132), 66, threshold, brightness)
            for cell, threshold, brightness in zip(frames, thresholds, (.89, 1.08, 1.0, .83))
        ))

    write_group(leap, 62064, directional_flight(flight,
                ((243, 158), (276, 155), (281, 153), (286, 153)), .194, (54, 59), 32, 184))

    for name, graphic, frames, scale, padding, cutoffs, pivots in (
        (leap, 62052, takeoff, .219, 22, (209, 210, 205, 193),
         ((195, 241), (187, 242), (191, 241), (193, 241))),
        (leap, 62060, settle, .218, 22, (212, 210, 199, 181),
         ((192, 192), (192, 191), (192, 191), (192, 191))),
        (attack, 62100, area, .191, 26, (211, 208, 201, 172),
         ((194, 207), (192, 207), (193, 207), (192, 207))),
        (attack, 62104, swing, .211, 42, (207, 208, 208, 187),
         ((192, 170), (191, 154), (194, 142), (192, 133))),
        (attack, 62108, impact, .215, 36, (207, 207, 208, 191),
         ((195, 130), (189, 130), (191, 129), (194, 129))),
        (spin, 62112, spinning, .245, 36, (214, 214, 214, 214),
         ((194, 150), (193, 150), (191, 150), (192, 150))),
        (spin, 62116, wake, .168, 22, (212, 208, 197, 161),
         ((195, 183), (196, 183), (195, 183), (193, 183))),
        (spin, 62120, strike, .208, 36, (210, 214, 201, 184),
         ((197, 130), (197, 128), (194, 128), (192, 128))),
        (spin, 62124, fade, .205, 26, (212, 203, 185, 102),
         ((193, 126), (193, 126), (193, 124), (193, 125))),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (96, 83), padding, cutoff)
            for cell, pivot, cutoff in zip(frames, pivots, cutoffs)
        ))


def barbarian_paired():
    swing = "barbarian-double-swing"
    frenzy = "barbarian-frenzy"
    throwing = "barbarian-double-throw"
    lead, follow, lead_impact, follow_impact = load_grid(swing, (1536, 1024))
    rush_lead, rush_follow, speed, fade = load_grid(frenzy, (1536, 1024))
    knife, axe, javelin, impact = load_grid(throwing, (1536, 1024))
    glaive, pilum = load_grid(throwing, (1536, 1024), rows=2, filename="glaive-pilum.png")

    frenzy_impact = []
    for cell in follow_impact:
        sparks = ImageOps.colorize(cell.convert("L"), "#402026", "#fff3be", "#e94f26")
        sparks.putalpha(cell.getchannel("A"))
        frenzy_impact.append(sparks)

    center = ((192, 128),) * 4
    for name, graphic, cells, scale, padding, cutoffs, pivots in (
        (swing, 62128, lead, .195, 40, (202, 208, 212, 168), ((186, 203), (193, 164), (193, 152), (193, 153))),
        (swing, 62132, follow, .195, 36, (200, 207, 213, 160), ((190, 101), (193, 106), (194, 118), (193, 117))),
        (swing, 62136, lead_impact, .166, 34, (204, 209, 200, 145), center),
        (swing, 62140, follow_impact, .171, 37, (202, 207, 193, 149), center),
        (frenzy, 62144, rush_lead, .175, 40, (202, 209, 213, 201), ((192, 158),) * 4),
        (frenzy, 62148, rush_follow, .175, 36, (202, 209, 213, 202), center),
        (frenzy, 62152, speed, .195, 16, (214, 214, 216, 214), ((192, 136),) * 4),
        (frenzy, 62156, fade, .192, 16, (210, 208, 198, 167), ((192, 132),) * 4),
        (frenzy, 62160, frenzy_impact, .166, 35, (202, 207, 193, 149), center),
        (throwing, 62164, impact, .152, 30, (207, 211, 207, 179), center),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (88, 76), padding, cutoff)
            for cell, pivot, cutoff in zip(cells, pivots, cutoffs)
        ))

    for graphic, cells, scale, pivots in (
        (62168, knife, .128, ((193, 145), (197, 140), (192, 140), (187, 143))),
        (62176, axe, .149, ((194, 137), (197, 123), (196, 131), (189, 134))),
    ):
        # The sheet depicts half a rotation. The turned frames complete the spin.
        write_group(throwing, graphic, (
            frame(cell.transpose(Image.Transpose.ROTATE_180) if reverse else cell, scale,
                  (cell.width - x, cell.height - y) if reverse else (x, y), (54, 46), 8, 199)
            for reverse in (False, True)
            for cell, (x, y) in zip(cells, pivots)
        ))
    for graphic, cells, scale, pivots in (
        (62184, javelin, .134, ((195, 126),) * 4),
        (62216, glaive, .153, ((207, 299), (204, 299), (199, 299), (194, 299))),
        (62248, pilum, .144, ((207, 241), (204, 241), (199, 241), (194, 241))),
    ):
        write_group(throwing, graphic, directional_flight(cells, pivots, scale, (62, 54), 8, 198))


def assassin_martial_arts():
    name = "assassin-martial-arts"
    tiger, tiger_hit, cobra, cobra_hit = load_grid(name, (1536, 1024), filename="tiger-cobra.png")
    claws = load_grid(name, (1536, 1024), filename="elemental-claws.png")
    hits = load_grid(name, (1536, 1024), filename="martial-elemental-bursts.png")
    ground = load_grid(name, (1536, 1024), filename="martial-ground.png")
    orbs = load_grid(name, (1536, 1024), rows=3, columns=6, filename="martial-charge-orbs.png")

    for family, cells in enumerate(zip(*orbs)):
        for tier in range(3):
            def mote(cell):
                mask = cell.getchannel("A").point(lambda alpha: 255 if alpha >= 205 else 0)
                left, top, right, bottom = mask.getbbox()
                scale = (10.5 + tier) / max(right - left, bottom - top)
                pivot = ((left + right) / 2, (top + bottom) / 2)
                return frame(cell, scale, pivot, (24, 24), 12, 179)

            write_group(name, 62280 + family * 9 + tier * 3, (mote(cell) for cell in cells))

    for first, rows, scale, padding, pivots, cutoffs in (
        (62336, (tiger, cobra, *claws), .171, 36, ((192, 151), (192, 145), (190, 135), (195, 140)),
         (202, 208, 210, 172)),
        (62360, (tiger_hit, cobra_hit, *hits), .154, 33, ((190, 154), (192, 150), (194, 144), (190, 144)),
         (199, 208, 207, 159)),
    ):
        for family, cells in enumerate(rows):
            thresholds = (*cutoffs[:3], 83 if first == 62336 else 128) if family < 2 else cutoffs
            write_group(name, first + family * 4, (
                frame(cell, scale, pivot, (78, 78), padding, cutoff)
                for cell, pivot, cutoff in zip(cells, pivots, thresholds)
            ))

    for family, cells in enumerate(ground[:3]):
        write_group(name, 62384 + family * 4, (
            holy_ring(cell, (234, 123), (240, 132), 66, threshold, brightness)
            for cell, threshold, brightness in zip(cells, (207, 211, 211, 179), (.9, 1.08, 1.0, .86))
        ))
    write_group(name, 62396, (
        frame(cell, .135, (193, 191), (38, 38), 13, 204) for cell in ground[3]
    ))

    for graphic, source, first in ((62400, "blaze", 65492), (62404, "meteor", 64932)):
        # Give each class field its own client lease even on overlapping tiles.
        write_group(name, graphic, ((ART / source / "statics" / f"{i}.art").read_bytes() for i in range(first, first + 4)))


def assassin_dragon_finishers():
    talon, claw, tail, flight = ("assassin-dragon-" + skill for skill in ("talon", "claw", "tail", "flight"))
    kick, kick_hit, skid, block = load_grid(talon, (1536, 1024))
    lead, follow, lead_hit, follow_hit = load_grid(claw, (1536, 1024))
    tail_kick, tail_hit, blast, embers = load_grid(tail, (1536, 1024))
    leave, arrive, flight_kick, flight_hit = load_grid(flight, (1536, 1024))

    for name, graphic, cells, scale, pivot, padding in (
        (talon, 62408, tuple(ImageOps.mirror(cell) for cell in kick), .162, (168, 166), 28),
        (claw, 62440, lead, .168, (172, 141), 38),
        (claw, 62472, follow, .168, (167, 111), 38),
        (tail, 62504, tail_kick, .194, (161, 146), 24),
        (flight, 62536, flight_kick, .177, (161, 134), 32),
    ):
        write_group(name, graphic, directional_flight(cells, (pivot,) * 4, scale, (92, 96), padding, 166))

    for name, graphic, cells, scale, pivot, padding, cutoffs in (
        (talon, 62568, kick_hit, .154, (175, 140), 31, (205, 210, 206, 178)),
        (claw, 62572, lead_hit, .16, (182, 128), 36, (207, 210, 204, 164)),
        (claw, 62576, follow_hit, .16, (181, 119), 36, (207, 210, 204, 164)),
        (tail, 62580, tail_hit, .158, (141, 165), 28, (207, 212, 205, 177)),
        (flight, 62584, flight_hit, .186, (207, 176), 23, (209, 207, 180, 80)),
        (talon, 62588, block, .16, (184, 124), 34, (202, 207, 197, 173)),
        (talon, 62592, skid, .154, (182, 181), 16, (205, 208, 205, 170)),
        (tail, 62600, embers, .167, (164, 174), 18, (202, 207, 200, 147)),
        (flight, 62604, leave, .204, (199, 127), 34, (207, 207, 198, 158)),
        (flight, 62608, arrive, .19, (193, 160), 19, (212, 207, 196, 168)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (88, 87), padding, cutoff) for cell, cutoff in zip(cells, cutoffs)
        ))

    write_group(tail, 62596, (
        holy_ring(cell, (236, 124), (240, 132), 66, cutoff, brightness)
        for cell, cutoff, brightness in zip(blast, (208, 215, 215, 179), (.89, 1.09, 1.0, .82))
    ))


def assassin_blade_skills():
    sentinel, fury, shield = ("assassin-blade-" + skill for skill in ("sentinel", "fury", "shield"))
    rise, body, cut, crumble = load_grid(sentinel, (1536, 1024))
    release, travel, contact, spent = load_grid(fury, (1536, 1024))
    raise_ring, ring, shield_hit, drop_ring = load_grid(shield, (1536, 1024))

    for name, graphic, cells, scale, pivots, padding, cutoffs in (
        (sentinel, 62612, rise, .197, ((193, 196), (193, 208), (194, 205), (194, 205)), 22, (203, 211, 214, 214)),
        (sentinel, 62616, body, .207, ((195, 137), (194, 120), (193, 127), (193, 120)), 39, (214,) * 4),
        (sentinel, 62620, cut, .163, ((196, 135),) * 4, 35, (207, 211, 204, 176)),
        (sentinel, 62624, crumble, .201, ((193, 132),) * 4, 39, (212, 213, 205, 147)),
        (fury, 62628, release, .145, ((191, 145),) * 4, 47, (212, 210, 209, 203)),
        (fury, 62664, contact, .166, ((193, 133),) * 4, 36, (214, 211, 204, 193)),
        (fury, 62668, spent, .126, ((210, 122),) * 4, 25, (214, 208, 202, 195)),
        (shield, 62672, raise_ring, .236, ((193, 133),) * 4, 45, (205, 208, 212, 212)),
        (shield, 62676, ring, .236, ((193, 127),) * 4, 45, (208,) * 4),
        (shield, 62680, shield_hit, .172, ((198, 128),) * 4, 36, (214, 211, 204, 105)),
        (shield, 62684, drop_ring, .236, ((193, 127),) * 4, 45, (210, 208, 197, 96)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (88, 86), padding, cutoff) for cell, pivot, cutoff in zip(cells, pivots, cutoffs)
        ))
    write_group(fury, 62632, directional_flight(
        travel, ((208, 129), (212, 128), (196, 124), (204, 131)), .112, (52, 62), 22, 190
    ))


def assassin_shadow_skills():
    tempo, venom, cloak, psychic = ("assassin-" + family for family in
                                   ("speed-fade", "venom", "cloak-shadows", "psychic-force"))
    haste, haste_loop, fade, fade_loop = load_grid(tempo, (1536, 1024))
    coat, coat_loop, _, coat_end = load_grid(venom, (1536, 1024))
    cloak_wave, mantle, mask, cloak_end = load_grid(cloak, (1536, 1024))
    hammer, hammer_hit, mind_wave, mind_hit = load_grid(psychic, (1536, 1024))
    for cell in haste_loop:
        cell.paste((0, 0, 0, 0), (0, 0, cell.width, 112))
    for cell in hammer_hit:
        cell.paste((0, 0, 0, 0), (0, 0, cell.width, 30))

    for name, graphic, cells, scale, pivot, padding, cutoffs in (
        (tempo, 62688, haste, .172, (193, 239), 24, (197, 203, 196, 167)),
        (tempo, 62692, haste_loop, .167, (190, 172), 25, (186,) * 4),
        (tempo, 62700, fade, .181, (188, 142), 49, (192, 195, 183, 160)),
        (tempo, 62704, fade_loop, .169, (191, 134), 43, (181,) * 4),
        (venom, 62712, coat, .185, (192, 142), 46, (206, 210, 210, 191)),
        (venom, 62716, coat_loop, .104, (267, 136), 39, (201,) * 4),
        (venom, 62720, coat_end, .153, (191, 147), 43, (202, 202, 183, 146)),
        (cloak, 62728, mantle, .199, (189, 151), 49, (193,) * 4),
        (cloak, 62732, mask, .166, (195, 127), 71, (194,) * 4),
        (cloak, 62736, [ImageEnhance.Color(cell).enhance(.35) for cell in mask],
         .133, (195, 127), 67, (202,) * 4),
        (cloak, 62740, cloak_end, .169, (190, 132), 46, (191, 192, 175, 124)),
        (psychic, 62744, hammer, .151, (188, 167), 47, (205, 209, 205, 172)),
        (psychic, 62748, hammer_hit, .206, (189, 222), 29, (206, 205, 206, 183)),
        (psychic, 62756, mind_hit, .192, (191, 131), 37, (204, 206, 199, 159)),
        (psychic, 62760, mind_hit, .113, (191, 131), 48, (204, 203, 176, 72)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (91, 93), padding, cutoff) for cell, cutoff in zip(cells, cutoffs)
        ))

    for graphic, cells, pivot, padding in (
        (62696, haste_loop, (189, 170), 26),
        (62708, fade_loop, (192, 130), 43),
    ):
        write_group(tempo, graphic, (
            frame(ImageEnhance.Brightness(cell).enhance(light), scale, pivot, (91, 93), padding, alpha)
            for cell, light, scale, alpha in zip(cells, (.91, .82, .65, .51), (.167, .148, .128, .103), (179, 185, 187, 145))
        ))

    for name, graphic, cells, cutoffs, brightnesses in (
        (cloak, 62724, cloak_wave, (182, 202, 207, 197), (1.08, 1.04, .88, .79)),
        (psychic, 62752, mind_wave, (183, 200, 202, 193), (1.01, 1.08, .92, .79)),
    ):
        write_group(name, graphic, (
            holy_ring(cell, (236, 126), (240, 132), 66, cutoff, brightness)
            for cell, cutoff, brightness in zip(cells, cutoffs, brightnesses)
        ))


def assassin_lob_skills():
    fire, shock = "assassin-fire-blast", "assassin-shock-web"
    ember, bomb, fuse, burst = load_grid(fire, (1536, 1024))
    shard, web, shock_hit, dissipate = load_grid(shock, (1536, 1024))
    # The chest-height flash uses the bright forks from the ground contact at a smaller scale.
    for name, graphic, cells, scale, pivots, padding, cutoffs in (
        (fire, 62764, ember, .162, ((195, 147), (178, 144), (166, 146), (180, 147)),
         46, (201, 206, 198, 171)),
        (fire, 62772, fuse, .188, ((198, 168), (197, 168), (194, 169), (191, 170)),
         21, (213, 211, 208, 212)),
        (shock, 62780, shock_hit, .091, ((193, 160), (194, 160), (194, 161), (194, 168)),
         45, (202, 200, 187, 162)),
        (shock, 62788, web, .175, ((193, 131), (193, 131), (193, 131), (193, 129)),
         22, (207, 205, 203, 200)),
        (shock, 62792, shock_hit, .169, ((193, 178), (193, 176), (194, 176), (194, 174)),
         23, (207, 207, 201, 183)),
        (shock, 62796, dissipate, .176, ((193, 130), (193, 131), (193, 134), (193, 130)),
         22, (205, 202, 197, 164)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (90, 88), padding, cutoff) for cell, pivot, cutoff in zip(cells, pivots, cutoffs)
        ))

    for name, graphic, cells, pivots, scale, cutoff in (
        (fire, 62768, bomb, ((204, 131), (213, 130), (227, 129), (231, 130)), .181, 211),
        (shock, 62784, shard, ((240, 143), (214, 136), (218, 132), (226, 141)), .126, 200),
    ):
        poses = []
        for phase, (cell, (x, y)) in enumerate(zip(cells, pivots)):
            centered = Image.new("RGBA", (512, 512))
            centered.paste(cell, (256-x, 256-y))
            poses.append(centered.rotate(90 * phase))
        write_group(name, graphic, (frame(cell, scale, (256, 256), (60, 64), 22, cutoff) for cell in poses))

    write_group(fire, 62776, (
        holy_ring(cell, (236, 126), (240, 132), 66, cutoff, light)
        for cell, cutoff, light in zip(burst, (208, 213, 210, 191), (1.02, 1.08, .99, .82))
    ))


def assassin_sentry_skills():
    fire = "assassin-fire-sentries"
    electric = "assassin-lightning-sentries"
    attacks = "assassin-sentry-attacks"
    with Image.open(ART / fire / "sheet.png") as sheet:
        wake, inferno, sparks, fire_debris = (
            [sheet.crop((x, top, x + 384, bottom)) for x in range(0, 1536, 384)]
            for top, bottom in ((0, 277), (277, 579), (579, 823), (823, 1024))
        )
    charged, lightning_sentry, death, electric_debris = load_grid(electric, (1536, 1024))
    for name, graphic, model, foot, debris, debris_foot in (
        (fire, 61800, wake, (207, 266), fire_debris, (200, 143)),
        (fire, 61812, inferno, (209, 283), fire_debris, (200, 143)),
        (electric, 61824, charged, (191, 232), electric_debris, (190, 199)),
        (electric, 61836, lightning_sentry, (192, 233), electric_debris, (190, 199)),
        (electric, 61848, death, (191, 234), electric_debris, (190, 199)),
    ):
        size, padding = (66, 82), 25
        # Each phase uses the same foot pivot, including when the base clears the ground.
        rise = [frame(cell, scale, foot, size, padding, 213)
                for cell, scale in zip(model, (.05, .092, .136, .178))]
        idle = [frame(cell, .19, foot, size, padding, 213) for cell in model]
        rubble = [frame(ImageEnhance.Brightness(cell).enhance(light), .19, debris_foot, size, padding, 214)
                  for cell, light in zip(debris, (1.01, .9, .76, .58))]
        write_group(name, graphic, rise + idle + rubble)

    with Image.open(ART / attacks / "sheet.png") as sheet:
        def row(top, bottom, edges):
            return [sheet.crop((left, top, right, bottom)) for left, right in zip(edges, edges[1:])]

        fire_wave = row(60, 251, (0, 346, 724, 1129, 1536))
        fire_stream = row(325, 475, (0, 326, 712, 1116, 1536))
        charge = row(524, 723, (0, 384, 768, 1152, 1536))
        explosion = row(747, 986, (0, 376, 718, 1108, 1536))

    for graphic, cells, tips, scale, padding in (
        (61860, fire_wave, ((248, 151), (288, 146), (328, 138), (343, 144)), .164, 24),
        (61892, [ImageOps.mirror(cell) for cell in fire_stream],
         ((257, 82), (325, 80), (367, 81), (382, 81)), .162, 35),
    ):
        write_group(attacks, graphic, directional_flight(cells, tips, scale, (122, 129), padding, 215))

    charge_centers = ((165, 144), (152, 147), (141, 151), (177, 141))
    write_group(attacks, 61924, (frame(cell, .155, center, (66, 62), 28, 213)
                                for cell, center in zip(charge, charge_centers)))
    write_group(attacks, 61928, (
        holy_ring(cell, (236, 126), (240, 132), 66, cutoff, brightness)
        for cell, cutoff, brightness in zip(explosion, (216, 216, 216, 210), (1.17, 1.12, 1.03, .91))
    ))
    write_group(fire, 61932, (frame(cell, .145, (201, 211), (60, 68), 40, 213) for cell in sparks))
    write_group(attacks, 61936, (frame(cell, .135, center, (66, 68), 38, 213)
                                for cell, center in zip(charge, charge_centers)))
    # The existing original lightning sprite gets fractional positioning on sentry paths.
    write_group(attacks, 61940, islice(lightning(), 32))


def assassin_shadow_summons():
    # Each style shares the humanoid's planted foot pivot across arrival, veil and departure.
    for name, graphic, recipes in (
        ("shadow-warrior", 61600, (
            (.19, (193, 149), 48, (210, 210, 213, 198)),
            (.23, (194, 207), 25, (207, 193, 200, 176)),
            (.205, (192, 180), 25, (148, 159, 159, 148)),
            (.214, (194, 192), 25, (217, 214, 204, 153)),
        )),
        ("shadow-master", 61616, (
            (.19, (193, 139), 48, (211, 213, 216, 203)),
            (.225, (194, 215), 25, (212, 213, 214, 212)),
            (.196, (191, 149), 25, (182, 177, 181, 176)),
            (.216, (193, 186), 25, (211, 212, 207, 181)),
        )),
    ):
        rows = load_grid(name, (1536, 1024))
        write_group(name, graphic, (
            frame(cell, scale, pivot, (96, 96), padding, cutoff)
            for cells, (scale, pivot, padding, cutoffs) in zip(rows, recipes)
            for cell, cutoff in zip(cells, cutoffs)
        ))


def warlock_demon_summons():
    for name, graphic, recipes in (
        ("warlock-goatman", 61400, (
            (.194, (193, 145), 47, (213, 213, 215, 210)),
            (.22, (194, 203), 24, (216, 215, 213, 207)),
            (.20, (206, 172), 37, (214, 217, 216, 206)),
            (.216, (190, 154), 24, (215, 212, 205, 184)),
        )),
        ("warlock-tainted", 61416, (
            (.194, (192, 147), 48, (210, 214, 211, 177)),
            (.22, (192, 179), 24, (208, 212, 213, 161)),
            (.225, (193, 145), 24, (194, 199, 201, 191)),
            (.216, (191, 157), 24, (208, 203, 188, 159)),
        )),
        ("warlock-defiler", 61432, (
            (.194, (193, 145), 48, (211, 212, 212, 204)),
            (.22, (195, 196), 24, (198, 207, 215, 199)),
            (.20, (192, 123), 46, (209, 218, 219, 209)),
            (.216, (190, 204), 24, (211, 188, 112, 125)),
        )),
    ):
        rows = load_grid(name, (1536, 1024))
        write_group(name, graphic, (
            frame(cell, scale, pivot, (96, 96), padding, cutoff)
            for cells, (scale, pivot, padding, cutoffs) in zip(rows, recipes)
            for cell, cutoff in zip(cells, cutoffs)
        ))

    # The same link shape expands and dims when damage actually transfers.
    marks = load_grid("warlock-defiler", (1536, 1024))[2]
    write_group("warlock-defiler", 61448, (
        frame(ImageEnhance.Brightness(marks[index]).enhance(light),
              scale, (192, 123), (96, 96), 42, cutoff)
        for index, scale, light, cutoff in (
            (1, .22, 1.06, 217), (2, .29, 1.08, 216), (2, .32, .73, 224), (3, .34, .76, 213)
        )
    ))


def warlock_rituals():
    death = "warlock-death-mark"
    boil = "warlock-blood-boil"
    consume = "warlock-consume"
    bind = "warlock-bind-demon"
    death_cast, departure, arrival, glyph = load_grid(death, (1536, 1024))
    boil_cast, ring, hit, boil_source = load_grid(boil, (1536, 1024))
    consume_cast, consume_source, consume_trail, consume_ward = load_grid(consume, (1536, 1024))
    bind_cast, bind_trail, bind_seal, bind_success = load_grid(bind, (1536, 1024))
    for name, graphic, row, scale, pivot, padding, cutoffs in (
        (death, 61200, death_cast, .181, (194, 132), 46, (217, 216, 218, 196)),
        (death, 61204, departure, .211, (198, 198), 24, (211, 214, 215, 181)),
        (death, 61208, arrival, .203, (197, 174), 24, (171, 209, 215, 208)),
        (death, 61212, glyph, .117, (191, 124), 55, (214, 216, 215, 212)),
        (boil, 61220, boil_cast, .167, (191, 138), 47, (214, 212, 215, 197)),
        (boil, 61228, hit, .151, (174, 177), 38, (208, 211, 213, 196)),
        (boil, 61232, boil_source, .194, (179, 128), 33, (211, 204, 192, 90)),
        (consume, 61000, consume_cast, .148, (195, 136), 48, (217, 215, 213, 30)),
        (consume, 61004, consume_source, .194, (195, 201), 24, (213, 211, 208, 190)),
        (consume, 61040, consume_ward, .161, (194, 127), 25, (211, 211, 209, 208)),
        (bind, 61300, bind_cast, .151, (195, 147), 46, (216, 216, 218, 215)),
        (bind, 61336, bind_seal, .171, (193, 195), 24, (213, 215, 214, 213)),
        (bind, 61340, bind_success, .184, (190, 141), 33, (217, 218, 215, 209)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (98, 106), padding, cutoff)
            for cell, cutoff in zip(row, cutoffs)
        ))
    write_group(boil, 61224, (
        holy_ring(cell, (234, 124), (240, 132), 66, cutoff, light)
        for cell, cutoff, light in zip(ring, (209, 213, 215, 211), (1.08, 1.08, 1.03, .95))
    ))
    write_group(consume, 61008, directional_flight(consume_trail,
        ((347, 132), (331, 128), (324, 132), (317, 130)), .136, (98, 106), 26, 205))
    write_group(bind, 61304, directional_flight(bind_trail,
        ((332, 124), (325, 123), (326, 125), (300, 121)), .113, (98, 106), 27, 190))
    write_group(consume, 61044, (
        frame(ImageEnhance.Brightness(cell).enhance(light), scale, (194, 127), (98, 106), 25, cutoff)
        for cell, scale, cutoff, light in zip(consume_ward, (.16, .13, .096, .064), (208, 205, 194, 174),
                                             (1.02, .90, .79, .68))
    ))


def warlock_engorge():
    name = "warlock-engorge"
    with Image.open(ART / name / "sheet.png") as sheet:
        cast, source, trail, heal = (
            [sheet.crop((x, top, x + 384, bottom)) for x in range(0, 1536, 384)]
            for top, bottom in ((0, 271), (271, 539), (539, 706), (706, 1024))
        )
    for graphic, row, scale, pivot, padding, cutoffs in (
        (61100, cast, .176, (193, 156), 47, (213, 211, 213, 211)),
        (61104, source, .195, (195, 172), 24, (215, 209, 181, 155)),
        (61140, heal, .190, (193, 222), 38, (214, 209, 215, 200)),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, (98, 106), padding, cutoff)
            for cell, cutoff in zip(row, cutoffs)
        ))
    write_group(name, 61108, directional_flight(trail,
        ((348, 85), (310, 86), (250, 86), (222, 86)), .131, (98, 106), 28, 186))
    write_group(name, 61144, (
        frame(ImageEnhance.Brightness(cell.crop((0, 191, 384, 291))).enhance(.86),
              .163, (193, 44), (98, 106), 25, cutoff)
        for cell, cutoff in zip(heal, (204, 203, 199, 184))
    ))


def warlock_fire():
    ring = "warlock-ring-of-fire"
    wave = "warlock-flame-wave"
    apocalypse = "warlock-apocalypse"
    ring_cast, ring_travel, ring_contact = load_grid(ring, (1254, 1254), rows=3)
    wave_cast, wave_front, wave_field = load_grid(wave, (1254, 1254), rows=3)
    warning, eruption, apocalypse_contact = load_grid(apocalypse, (1254, 1254), rows=3)
    for name, graphic, row, scale, pivots, canvas, padding, threshold in (
        (ring, 61500, ring_cast, .23, ((179, 336), (164, 336), (142, 336), (125, 336)), (88, 80), 21, 136),
        (ring, 61504, ring_travel, .105, ((175, 267), (167, 271), (153, 273), (125, 274)), (50, 59), 22, 144),
        (ring, 61508, ring_contact, .145, ((171, 174), (153, 158), (139, 151), (134, 151)), (60, 87), 38, 136),
        (ring, 61512, ring_travel, .174, ((175, 267), (167, 271), (153, 273), (125, 274)), (72, 70), 22, 155),
        (wave, 61516, wave_cast, .219, ((158, 335), (155, 335), (149, 335), (147, 335)), (84, 89), 23, 145),
        (wave, 61520, wave_front, .174, ((155, 285), (153, 286), (150, 289), (148, 292)), (65, 74), 22, 145),
        (wave, 61524, wave_field, .120, ((157, 278), (158, 279), (153, 279), (152, 278)), (49, 60), 20, 148),
        (apocalypse, 61536, eruption, .34, ((167, 349), (158, 354), (155, 356), (153, 358)), (126, 160), 26, 147),
        (apocalypse, 61540, apocalypse_contact, .18, ((162, 252), (151, 253), (145, 245), (157, 243)), (64, 83), 33, 132),
    ):
        write_group(name, graphic, (
            frame(cell, scale, pivot, canvas, padding, threshold)
            for cell, pivot in zip(row, pivots)
        ))
    write_group(wave, 61528, (
        frame(ImageEnhance.Brightness(cell).enhance(light), scale, (151, 279), (56, 65), 21, cutoff)
        for cell, scale, light, cutoff in zip(wave_field, (.19, .16, .14, .105),
                                             (1.05, .91, .79, .70), (146, 142, 140, 126))
    ))
    write_group(apocalypse, 61532, (
        holy_ring(cell, (240, 132), (240, 132), 70, cutoff, light)
        for cell, cutoff, light in zip(warning, (138, 142, 143, 139), (.91, 1.0, 1.12, 1.03))
    ))


def necromancer_curses():
    cells = [cell for row in load_grid("necromancer-curses", (1448, 1086), rows=3) for cell in row]
    for cell in cells[:10]:
        left, top, right, bottom = cell.getchannel("A").point(lambda a: 255 if a >= 185 else 0).getbbox()
        anchor = ((left + right) / 2, (top + bottom) / 2)
        scale = 29 / max(right - left, bottom - top)
        cell = ImageEnhance.Brightness(cell).enhance(1.24)
        for size, padding in ((1.0, 64), (1.04, 65), (1.0, 66), (0.96, 65)):
            yield frame(cell, scale * size, anchor, (44, 85), padding, 185, outline=(30, 19, 29, 255))

    ring, life = cells[10], cells[11]
    for scale in (0.125, 0.19, 0.245, 0.27):
        yield frame(ring, scale, (180, 177), (108, 98), 22, 185)
    for scale, padding in ((0.103, 64), (0.086, 68), (0.06, 71), (0.033, 74)):
        yield frame(ring, scale, (180, 177), (51, 90), padding, 185)
    for scale, padding in ((0.133, 33), (0.143, 43), (0.097, 56), (0.053, 67)):
        yield frame(life, scale, (179, 180), (57, 93), padding, 185)
    for scale, padding in ((0.165, 36), (0.123, 37), (0.088, 40), (0.051, 42)):
        yield frame(cells[4], scale, (181, 183), (68, 85), padding, 185)


def bone_armor_explosion():
    armor, broken, explosion = load_grid("bone-armor-explosion", (1448, 1086), rows=3)
    for cell, center in zip(armor, ((183, 190), (185, 190), (181, 190), (176, 190))):
        yield frame(cell, 0.216, center, (80, 80), 53, 167)
    for cell, center in zip(broken, ((183, 179), (181, 178), (178, 181), (175, 182))):
        yield frame(cell, 0.25, center, (90, 91), 57, 134)
    for cell, center in zip(explosion, ((178, 207), (180, 223), (179, 227), (177, 233))):
        yield frame(cell, 0.44, center, (170, 142), 25, 149)


def bone_barriers():
    wall, prison, connector, rubble = load_grid("bone-barriers", (2172, 724), rows=1)[0]
    for cell, anchor in ((wall, 283), (prison, 258)):
        for growth in (0.19, 0.43, 0.72, 1.0):
            rising = cell.resize((cell.width, round(cell.height * growth)), Image.Resampling.LANCZOS)
            yield frame(rising, 0.116, (anchor, round(596 * growth)), (66, 88), 24, 172)
    yield frame(connector, 0.103, (266, 596), (62, 60), 24, 171)
    yield frame(rubble, 0.101, (258, 599), (60, 59), 23, 170)


def bone_armor_catch():
    _, broken, _ = load_grid("bone-armor-explosion", (1448, 1086), rows=3)
    spark = broken[0].crop((71, 130, 136, 204))
    return frame(spark, 0.29, (83, 36), (65, 75), 51, 143)


def necromancer_poison():
    wisps, afflicted, bursts = load_grid("necromancer-poison", (1448, 1086), rows=3)
    yield from directional_flight(wisps, ((269, 239), (289, 233), (287, 229), (284, 221)),
                                  0.098, (62, 71), 26, 158)
    for cell in afflicted:
        yield frame(cell, 0.159, (119, 210), (61, 88), 50, 161)
    # Isolated ImageGen sprites expand or disperse at their game-event pivot.
    phases = (
        ((180, 175), (54, 68), 48, (0.155, 0.135, 0.091, 0.052), (157, 174, 191, 208)),
        ((182, 216), (156, 111), 24, (0.16, 0.34, 0.397, 0.251), (157, 174, 191, 208)),
        ((181, 185), (131, 94), 24, (0.145, 0.216, 0.29, 0.38), (157, 174, 191, 208)),
        ((145, 195), (68, 85), 52, (0.145, 0.128, 0.104, 0.065), (172, 165, 154, 137)),
    )
    for sprite, (anchor, size, padding, scales, thresholds) in zip(bursts, phases):
        for scale, threshold in zip(scales, thresholds):
            yield frame(sprite, scale, anchor, size, padding, threshold)


if __name__ == "__main__":
    write_group("bone-spear", 64900, bone_spear())  # SpellEffectGraphics.BoneSpearFlight.
    write_group("meteor", 64920, meteor())          # SpellEffectGraphics.MeteorWarning.
    write_group("blizzard", 64936, blizzard())      # SpellEffectGraphics.BlizzardFall.
    write_group("frozen-orb", 64952, frozen_orb())   # SpellEffectGraphics.FrozenOrb.
    write_group("strafe", 65046, strafe())          # SpellEffectGraphics.StrafeArrow.
    write_group("teeth", 65062, teeth())            # SpellEffectGraphics.TeethFlight.
    write_group("bone-spirit", 65098, bone_spirit())  # SpellEffectGraphics.BoneSpiritFlight.
    write_group("abyss", 65134, abyss())             # SpellEffectGraphics.AbyssOpen.
    write_group("charged-bolt", 65150, charged_bolt())        # SpellEffectGraphics.ChargedBoltFlight.
    write_group("lightning", 65186, lightning())              # SpellEffectGraphics.LightningFlight.
    write_group("chain-lightning", 65222, chain_lightning())  # SpellEffectGraphics.ChainLightningFlight.
    write_group("static-field", 65258, static_field())        # SpellEffectGraphics.StaticFieldSmall.
    write_group("thunder-storm", 65274, thunder_storm())      # SpellEffectGraphics.ThunderStormCharge.
    write_group("ice-bolt", 65282, ice_bolt())        # SpellEffectGraphics.IceBoltFlight.
    write_group("ice-blast", 65318, ice_blast())      # SpellEffectGraphics.IceBlastFlight.
    write_group("glacial-spike", 65354, glacial_spike())  # SpellEffectGraphics.GlacialSpikeFlight.
    write_group("frost-nova", 65390, frost_nova())    # SpellEffectGraphics.FrostNovaRing.
    write_group("cold-status", 65400, cold_status()) # SpellEffectGraphics.Chilled.
    write_group("fire-bolt", 65416, fire_bolt())    # SpellEffectGraphics.FireBoltFlight.
    write_group("inferno", 65452, inferno())       # SpellEffectGraphics.InfernoFlight.
    write_group("blaze", 65488, blaze())           # SpellEffectGraphics.BlazeCast.
    write_group("fire-wall", 65500, fire_wall())   # SpellEffectGraphics.FireWallIgnite.
    write_group("frozen-armor", 62800, frozen_armor())      # SpellEffectGraphics.FrozenArmorCast.
    write_group("shiver-armor", 62816, shiver_armor())      # SpellEffectGraphics.ShiverArmorCast.
    write_group("chilling-armor", 62832, chilling_armor()) # SpellEffectGraphics.ChillingArmorCast.
    write_group("energy-shield", 62848, energy_shield())   # SpellEffectGraphics.EnergyShieldCast.
    write_group("nova", 62900, nova())                     # SpellEffectGraphics.NovaRing.
    write_group("telekinesis", 62912, telekinesis())       # SpellEffectGraphics.TelekinesisCast.
    write_group("teleport", 62924, teleport())            # SpellEffectGraphics.TeleportDeparture.
    write_group("enchant", 62932, enchant())              # SpellEffectGraphics.EnchantCast.
    write_group("hydra", 62948, hydra_head())              # SpellEffectGraphics.HydraHeadRight.
    write_group("hydra-bolt", 62984, hydra_bolt())          # SpellEffectGraphics.HydraBoltFlight.
    write_group("amazon-arcane-arrows", 63020, amazon_arcane_arrows())  # SpellEffectGraphics.MagicArrowFlight.
    write_group("amazon-arrow-impact", 63148, amazon_arrow_impact())   # SpellEffectGraphics.ArrowContact.
    write_group("amazon-fire-flight", 63160, elemental_arrow_flight("amazon-fire-flight", (
        ((286, 271), (290, 273)), ((292, 242), (286, 242)), ((295, 245), (292, 244)),
    )))
    write_group("amazon-cold-flight", 63208, elemental_arrow_flight("amazon-cold-flight", (
        ((279, 288), (289, 288)), ((279, 244), (290, 244)), ((285, 239), (286, 238)),
    )))
    write_group("amazon-fire-impact", 63256, grid_effects("amazon-fire-impact", (1448, 1086), (
        ((180, 137), (180, 137), (180, 137), (180, 137)),
        ((181, 140), (177, 139), (180, 136), (177, 141)),
        ((182, 186), (176, 186), (179, 187), (177, 187)),
        ((180, 175), (180, 175), (180, 175), (180, 175)),
    ), (0.25, 0.31, 0.32, 0.19), (116, 105), 55, 195))
    write_group("amazon-cold-impact", 63272, grid_effects("amazon-cold-impact", (1448, 1086), (
        ((178, 234), (183, 233), (183, 233), (183, 233)),
        ((181, 157), (181, 157), (181, 157), (181, 157)),
        ((175, 119), (164, 118), (177, 118), (174, 119)),
    ), (0.20, 0.215, 0.28), (116, 105), 55, 221))
    write_group("javelin-flight", 63284, javelin_flight())
    write_group("javelin-venom", 63316, grid_effects("javelin-venom", (1263, 1246), (
        ((176, 183), (169, 182), (163, 178), (151, 184)),
        ((165, 199), (165, 198), (164, 200), (165, 199)),
        ((168, 190), (158, 183), (157, 184), (151, 176)),
        ((166, 161), (165, 162), (161, 161), (161, 162)),
    ), (0.205, 0.235, 0.29, 0.228), (105, 89), 48, 162))
    write_group("amazon-thrust-effects", 63332, grid_effects("amazon-thrust-effects", (1448, 1086), (
        ((206, 148), (184, 148), (189, 147), (189, 147)),
        ((197, 132), (188, 132), (190, 133), (189, 131)),
        ((180, 133), (168, 134), (174, 134), (177, 132)),
        ((191, 123), (177, 123), (185, 122), (175, 123)),
    ), (0.22, 0.20, 0.23, 0.215), (84, 85), 56, 120))
    write_group("impale-slow", 63348, grid_effects("impale-slow", (2172, 724), (
        ((272, 391), (272, 391), (272, 391), (272, 391)),
    ), (0.115,), (60, 58), 28, 176))
    write_group("amazon-marks", 63352, amazon_marks())
    write_group("decoy-mirror", 63368, grid_effects("decoy-mirror", (1448, 1086), (
        ((183, 299), (180, 299), (176, 307), (176, 299)),
        ((186, 302), (179, 305), (172, 301), (182, 303)),
        ((185, 306), (187, 301), (180, 305), (181, 302)),
    ), (0.255, 0.245, 0.26), (110, 130), 26, 207))
    write_group("valkyrie-gold", 63380, grid_effects("valkyrie-gold", (1448, 1086), (
        ((198, 284), (186, 297), (181, 295), (177, 292)),
        ((203, 259), (191, 258), (179, 258), (159, 257)),
        ((204, 243), (187, 244), (177, 245), (171, 242)),
    ), (0.247, 0.152, 0.235), (112, 112), 27, 183))
    write_group("amazon-avoidance", 63392, grid_effects("amazon-avoidance", (1448, 1086), (
        ((199, 316), (199, 316), (199, 316), (199, 316)),
        ((196, 275), (196, 275), (196, 275), (196, 275)),
        ((188, 275), (188, 275), (188, 275), (188, 275)),
    ), (0.185, 0.20, 0.19), (84, 86), 18, 177))
    write_group("necromancer-curses", 63404, necromancer_curses())
    write_group("bone-armor-explosion", 63460, bone_armor_explosion())
    write_group("bone-barriers", 63472, bone_barriers())
    write_group("bone-armor-explosion", 63482, [bone_armor_catch()])
    write_group("necromancer-poison", 63483, necromancer_poison())
    # Reuse the poison javelin cloud art with IDs for short-lived moving samples.
    write_group("necromancer-poison", 63535, (
        (ART / "javelin-venom" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63320, 63324)
    ))
    write_group("undead-summons", 63539, grid_effects("undead-summons", (1254, 1254), (
        ((163, 280), (157, 277), (152, 279), (149, 274)),
        ((165, 280), (149, 279), (151, 280), (145, 277)),
        ((168, 268), (147, 271), (150, 274), (145, 269)),
        ((164, 254), (159, 253), (152, 251), (148, 242)),
    ), (0.345, 0.36, 0.36, 0.295), (122, 128), 26, 184))
    write_group("golem-arrivals", 63555, grid_effects("golem-arrivals", (1254, 1254), (
        ((156, 256), (156, 258), (150, 258), (156, 255)),
        ((155, 256), (153, 255), (152, 258), (152, 253)),
        ((166, 258), (151, 257), (149, 256), (161, 254)),
        ((151, 273), (150, 274), (151, 278), (160, 268)),
    ), (0.37, 0.37, 0.37, 0.37), (132, 134), 24, 190))
    write_group("golem-combat", 63571, grid_effects("golem-combat", (1254, 1254), (
        ((154, 225), (146, 230), (158, 243), (141, 233)),
        ((150, 253), (149, 253), (154, 247), (138, 249)),
        ((147, 150), (143, 152), (150, 159), (152, 158)),
        ((156, 218), (151, 217), (150, 215), (146, 215)),
    ), (0.26, 0.31, 0.22, 0.36), (116, 108), 28, 190))
    write_group("golem-fire", 63587, grid_effects("golem-fire", (1774, 887), (
        ((224, 344), (225, 335), (222, 322), (221, 338)),
        ((224, 350), (213, 343), (205, 348), (221, 347)),
    ), (0.30, 0.37), (162, 136), 25, 190))
    # The golem slow and Impale can coexist and expire independently.
    write_group("golem-combat", 63595, (
        (ART / "impale-slow" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63348, 63352)
    ))
    write_group("open-wounds", 63599, grid_effects("open-wounds", (1774, 887), (
        ((225, 212), (225, 212), (225, 212), (225, 212)),
        ((225, 224), (225, 224), (225, 224), (225, 224)),
    ), (0.176, 0.176), (62, 100), 57, 170))
    write_group("druid-form-transitions", 63607, grid_effects("druid-form-transitions", (1254, 1254), (
        ((154, 280), (158, 280), (156, 280), (157, 280)),
        ((158, 280), (157, 280), (163, 280), (154, 280)),
        ((154, 281), (158, 281), (157, 281), (156, 281)),
        ((154, 280), (152, 280), (154, 280), (156, 279)),
    ), (0.37, 0.34, 0.42, 0.39), (150, 144), 24, 204))
    charge_rows = load_grid("druid-form-charges", (1254, 1254))
    write_group("druid-form-charges", 63623, (
        frame(cell, scale, anchor, (92, 96), 36, 202)
        for row, scale, anchors in (
            (charge_rows[0], 0.27, ((161, 253), (162, 251), (160, 250), (156, 248))),
            (charge_rows[1], 0.26, ((157, 218), (153, 219), (151, 210), (150, 204))),
        )
        for cell, anchor in zip(row, anchors)
    ))
    write_group("druid-form-charges", 63631, (
        frame(cell, scale, anchor, (30, 32), 16, 205)
        for row, anchors in (
            (charge_rows[2], ((157, 161), (160, 158), (158, 157), (158, 159))),
            (charge_rows[3], ((160, 140), (161, 137), (158, 135), (157, 143))),
        )
        for scale in (0.134, 0.168, 0.20)
        for cell, anchor in zip(row, anchors)
    ))
    write_group("druid-melee", 63655, grid_effects("druid-melee", (1254, 1254), (
        ((156, 218), (161, 223), (158, 218), (159, 224)),
        ((165, 192), (161, 188), (156, 185), (157, 183)),
        ((168, 242), (164, 246), (159, 247), (159, 239)),
        ((157, 241), (155, 243), (157, 236), (158, 232)),
    ), (0.335, 0.31, 0.25, 0.28), (106, 108), 29, 196))
    wave, impact, stun, release = load_grid("druid-shock-wave", (1254, 1254))
    write_group("druid-shock-wave", 63671, directional_flight(
        wave, ((190, 175), (183, 173), (180, 166), (179, 171)), 0.205, (72, 78), 23, 211
    ))
    write_group("druid-shock-wave", 63703, (
        frame(cell, scale, anchor, size, padding, 211)
        for row, scale, anchors, size, padding in (
            (impact, 0.29, ((157, 209), (153, 212), (155, 213), (157, 211)), (100, 100), 31),
            (stun, 0.168, ((158, 161), (154, 151), (157, 161), (158, 159)), (46, 84), 58),
            (release, 0.41, ((156, 165), (157, 165), (155, 163), (157, 164)), (140, 115), 22),
        )
        for cell, anchor in zip(row, anchors)
    ))
    bite, carrier, spread, fade = load_grid("druid-rabies", (1254, 1254))
    write_group("druid-rabies", 63715, (
        frame(cell, scale, anchor, (90, 90), 41, threshold)
        for row, scale, anchors, thresholds in (
            (bite, 0.30, ((172, 213), (160, 210), (151, 208), (145, 204)), (217,) * 4),
            (carrier, 0.187, ((163, 156), (152, 158), (146, 154), (147, 155)), (217,) * 4),
            (spread, 0.215, ((163, 159), (150, 159), (150, 151), (140, 155)), (217,) * 4),
            (fade, 0.19, ((166, 132), (161, 139), (152, 136), (151, 131)), (217, 217, 217, 165)),
        )
        for cell, anchor, threshold in zip(row, anchors, thresholds)
    ))
    release, fire, opening, cracks = load_grid("druid-ground-fire", (1261, 1247))
    write_group("druid-ground-fire", 63731, (
        frame(cell, 0.36, origin, (112, 124), 24, 205)
        for cell, origin in zip(release, ((160, 289), (155, 285), (156, 287), (152, 283)))
    ))
    write_group("druid-ground-fire", 63735, directional_flight(
        fire, ((190, 209), (222, 207), (250, 211), (255, 214)), 0.19, (78, 76), 22, 195
    ))
    write_group("druid-ground-fire", 63767, (
        frame(cell, 0.36, origin, (114, 115), 24, 205)
        for cell, origin in zip(opening, ((151, 225), (155, 221), (154, 220), (153, 223)))
    ))
    write_group("druid-ground-fire", 63771, (
        frame(ImageOps.mirror(cell) if mirrored else cell, 0.23, (cell.width // 2, 190), (72, 69), 22, 205)
        for mirrored in (False, True)
        for cell in cracks
    ))
    formation, roll, burst, contact = load_grid("druid-boulder", (1293, 1216))
    write_group("druid-boulder", 63779, (
        frame(cell, scale, origin, size, padding, 203)
        for row, scale, origins, size, padding in (
            (formation, 0.28, ((151, 274), (161, 270), (157, 271), (155, 271)), (94, 96), 24),
            (roll, 0.28, ((163, 281), (161, 281), (160, 282), (157, 282)), (94, 96), 24),
            (burst, 0.34, ((164, 252), (156, 252), (168, 252), (162, 253)), (124, 126), 25),
            (contact, 0.225, ((152, 231), (157, 231), (155, 231), (157, 231)), (70, 73), 34),
        )
        for cell, origin in zip(row, origins)
    ))
    # Keep the low flame bed on the exact cells used by the mechanical trails.
    write_group("druid-ground-fire", 63795, (
        (ART / "blaze" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(65492, 65496)
    ))
    twister, tornado, release, contact = load_grid("druid-wind-missiles", (1254, 1254))
    twister_pivots = ((165, 211), (157, 212), (157, 212), (153, 213))
    tornado_pivots = ((161, 282), (165, 281), (151, 283), (148, 283))
    release_pivots = ((164, 180), (160, 180), (158, 182), (156, 183))
    write_group("druid-wind-missiles", 63799, (
        frame(cell, scale, origin, size, padding, 191)
        for row, scale, pivots, size, padding in (
            (twister, 0.235, twister_pivots, (69, 65), 23),
            (tornado, 0.323, tornado_pivots, (100, 120), 23),
            (release, 0.33, release_pivots, (122, 82), 22),
            (contact, 0.24, ((167, 158), (155, 166), (158, 163), (151, 156)), (79, 72), 42),
        )
        for cell, origin in zip(row, pivots)
    ))
    write_group("druid-wind-missiles", 63815, (
        frame(cell, scale * factor, origin, size, 23, threshold)
        for row, scale, pivots, size in (
            (twister, 0.235, twister_pivots, (69, 65)),
            (tornado, 0.323, tornado_pivots, (100, 120)),
        )
        for cell, origin, factor, threshold in zip(row, pivots, (0.88, 0.67, 0.45, 0.24), (198, 206, 218, 230))
    ))
    write_group("druid-wind-missiles", 63823, (
        frame(cell, 0.29, origin, (107, 73), 22, 210)
        for cell, origin in zip(release, release_pivots)
    ))
    opening, storm, fade, wisps = load_grid("druid-hurricane", (1254, 1254))
    write_group("druid-hurricane", 63827, (
        frame(cell, scale, origin, size, 22, 209)
        for row, scale, origins, size in (
            (opening, 0.35, ((172, 216), (155, 217), (158, 215), (157, 213)), (124, 102)),
            (storm, 0.365, ((155, 176), (157, 171), (156, 173), (155, 175)), (116, 83)),
            (fade, 0.365, ((163, 182), (154, 183), (155, 179), (152, 169)), (122, 91)),
        )
        for cell, origin in zip(row, origins)
    ))
    write_group("druid-hurricane", 63839, directional_flight(
        wisps, ((163, 158), (169, 155), (155, 156), (174, 154)), 0.161, (62, 68), 22, 206
    ))
    arctic_release, arctic_stream, arctic_fade, arctic_hit = load_grid("druid-arctic-blast", (1254, 1254))
    write_group("druid-arctic-blast", 63871, directional_flight(
        arctic_release, ((112, 173), (77, 174), (54, 172), (36, 168)), 0.158, (96, 95), 42, 216
    ))
    write_group("druid-arctic-blast", 63903, directional_flight(
        arctic_stream, ((207, 156), (204, 155), (209, 156), (210, 155)), 0.144, (68, 79), 40, 216
    ))
    write_group("druid-arctic-blast", 63935, (
        frame(cell, scale, (157, 151), (62, 77), 39, threshold)
        for cell, scale, threshold in zip(arctic_fade, (0.168, 0.149, 0.132, 0.091), (209, 218, 226, 235))
    ))
    write_group("druid-arctic-blast", 63939, (
        frame(cell, 0.188, pivot, (62, 82), 38, 207)
        for cell, pivot in zip(arctic_hit, ((157, 160), (157, 168), (149, 168), (153, 163)))
    ))
    write_group("druid-cyclone-armor", 63943, grid_effects("druid-cyclone-armor", (1254, 1254), (
        ((153, 261), (159, 260), (153, 258), (155, 260)),
        ((155, 253), (157, 255), (152, 252), (153, 252)),
        ((156, 250), (154, 250), (156, 249), (157, 249)),
        ((156, 250), (154, 252), (155, 249), (152, 245)),
    ), (0.31, 0.31, 0.342, 0.35), (114, 125), 25, 217))
    opening, vent, debris, cooling = load_grid("druid-volcano", (1278, 1230))
    write_group("druid-volcano", 63959, (
        frame(cell, scale, pivot, size, 23, 198)
        for row, pivots, scales, size in (
            (opening, ((159, 242), (158, 242), (159, 242), (161, 242)), (0.34,) * 4, (112, 124)),
            (vent, ((159, 239), (160, 239), (160, 239), (161, 239)), (0.34,) * 4, (112, 124)),
            (debris, ((158, 158), (161, 153), (156, 156), (153, 155)), (0.245,) * 4, (54, 63)),
            (cooling, ((159, 222), (159, 222), (160, 222), (161, 222)), (0.34, 0.32, 0.28, 0.23), (112, 124)),
        )
        for cell, pivot, scale in zip(row, pivots, scales)
    ))
    write_group("druid-volcano", 63975, (
        (ART / "druid-boulder" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63791, 63795)
    ))
    release, aura, warning, rock = load_grid("druid-armageddon", (1269, 1240))
    write_group("druid-armageddon", 63979, (
        frame(cell, scale, pivot, size, 22, threshold)
        for row, scale, pivots, size, threshold in (
            (release, 0.34, ((153, 263), (157, 263), (155, 262), (157, 263)), (118, 122), 200),
            (aura, 0.28, ((152, 203), (157, 201), (158, 202), (157, 201)), (86, 65), 218),
            (warning, 0.19, ((155, 219), (159, 220), (157, 218), (161, 218)), (60, 54), 209),
        )
        for cell, pivot in zip(row, pivots)
    ))
    fall_pivots = ((141, 239), (144, 239), (145, 242), (143, 248))
    write_group("druid-armageddon", 63991, (
        frame(rock[step % 4], 0.204, fall_pivots[step % 4], (66, 182), 24 + (9 - step) * 11, 196)
        for step in range(10)
    ))
    # Each Armageddon fall uses a smaller version of the authored Meteor impact.
    meteor_impact = load_grid("meteor", (1263, 1246))[2]
    write_group("druid-armageddon", 64001, (
        frame(cell, 0.269, (155, 185), (92, 88), 24, 164)
        for cell in meteor_impact
    ))
    animals = load_grid("druid-animal-arrivals", (1278, 1230))
    animal_pivots = (
        ((156, 253), (155, 253), (154, 251), (157, 246)),
        ((156, 245), (157, 247), (157, 245), (159, 242)),
        ((156, 225), (157, 226), (158, 224), (157, 220)),
        ((153, 237), (154, 238), (158, 237), (158, 234)),
    )
    animal_scales = (0.265, 0.318, 0.34, 0.415)
    write_group("druid-animal-arrivals", 64005, (
        frame(cell, scale, pivot, (132, 139), 23, threshold)
        for row, pivots, scale, threshold in zip(animals, animal_pivots, animal_scales, (202, 217, 201, 207))
        for cell, pivot in zip(row, pivots)
    ))
    write_group("druid-animal-arrivals", 64021, (
        frame(cell, scale * shrink, pivot, (132, 139), 23, threshold)
        for row, pivots, scale in zip(animals, animal_pivots, animal_scales)
        for cell, pivot, shrink, threshold in zip(
            reversed(row), reversed(pivots), (0.99, 0.82, 0.68, 0.51), (204, 215, 224, 226)
        )
    ))
    write_group("druid-animal-contacts", 64037, grid_effects("druid-animal-contacts", (1262, 1246), (
        ((160, 174), (163, 166), (157, 158), (154, 161)),
        ((159, 159), (159, 158), (157, 163), (152, 159)),
        ((156, 150), (156, 150), (156, 150), (154, 150)),
        ((156, 156), (159, 162), (155, 172), (152, 173)),
    ), (0.19, 0.224, 0.226, 0.265), (90, 91), 39, 194))
    # Recut Dire Wolf's warm arrival wisps as a smaller aura beneath the wolf.
    write_group("druid-animal-contacts", 64053, (
        frame(animals[2][i], 0.22, animal_pivots[2][i], (70, 77), 23, 222)
        for i in (1, 2, 3, 2)
    ))
    write_group("druid-animal-contacts", 64057, (
        (ART / "necromancer-curses" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63428, 63432)
    ))
    write_group("druid-animal-contacts", 64061, (
        (ART / "druid-form-charges" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63623, 63627)
    ))
    oak, heart, barbs, return_spikes = load_grid("druid-spirit-arrivals", (1254, 1254))
    write_group("druid-spirit-arrivals", 64065, (
        frame(cell, scale, pivot, (118, 132), 24, threshold)
        for row, scale, pivots, threshold in (
            (oak, 0.36, ((156, 267), (157, 270), (155, 268), (152, 261)), 202),
            (heart, 0.342, ((157, 257), (158, 262), (159, 262), (155, 243)), 200),
            (barbs, 0.35, ((157, 263), (155, 263), (157, 260), (155, 245)), 196),
        )
        for cell, pivot in zip(row, pivots)
    ))
    life, force, thorns, departure = load_grid("druid-spirit-auras", (1269, 1240))
    write_group("druid-spirit-auras", 64077, (
        frame(cell, 0.229, pivot, (77, 68), 21, 212)
        for row, pivots in (
            (life, ((170, 185), (164, 182), (156, 185), (146, 186))),
            (force, ((169, 168), (163, 167), (156, 163), (147, 168))),
            (thorns, ((171, 151), (165, 148), (153, 152), (147, 151))),
        )
        for cell, pivot in zip(row, pivots)
    ))
    write_group("druid-spirit-auras", 64089, (
        frame(cell, scale, pivot, (110, 108), 23, threshold)
        for cell, scale, pivot, threshold in zip(
            departure, (0.31, 0.31, 0.27, 0.22), ((166, 170), (159, 170), (155, 170), (139, 171)), (202, 211, 225, 232)
        )
    ))
    write_group("druid-spirit-arrivals", 64093, (
        frame(cell, 0.29, pivot, (87, 94), 38, 191)
        for cell, pivot in zip(return_spikes, ((157, 248), (155, 252), (151, 240), (155, 235)))
    ))
    creeper, carrion, solar, thorn = load_grid("druid-vine-arrivals", (1254, 1254))
    vine_rows = (
        (creeper, ((150, 272), (148, 273), (155, 273), (154, 277)), 0.326),
        (carrion, ((156, 277), (157, 276), (152, 275), (154, 277)), 0.326),
        (solar, ((155, 275), (155, 276), (154, 275), (155, 274)), 0.336),
    )
    write_group("druid-vine-arrivals", 64097, (
        frame(cell, scale, pivot, (116, 128), 22, 207)
        for row, pivots, scale in vine_rows
        for cell, pivot in zip(row, pivots)
    ))
    write_group("druid-vine-arrivals", 64109, (
        frame(cell, scale * shrink, pivot, (116, 128), 22, threshold)
        for row, pivots, scale in vine_rows
        for cell, pivot, shrink, threshold in zip(
            (row[2], row[1], row[3], row[0]), (pivots[2], pivots[1], pivots[3], pivots[0]),
            (0.81, 0.74, 0.67, 0.49), (208, 215, 220, 226)
        )
    ))
    write_group("druid-vine-arrivals", 64121, directional_flight(
        thorn, ((215, 183), (231, 172), (244, 178), (245, 171)), 0.167, (86, 84), 22, 213
    ))
    mat, corpse, life, mana = load_grid("druid-vine-ground", (1448, 1086))
    mat_pivots = ((176, 220), (181, 219), (182, 217), (177, 219))
    write_group("druid-vine-ground", 64153, (
        frame(cell, 0.108, pivot, (43, 48), 21, 204)
        for cell, pivot in zip(mat, mat_pivots)
    ))
    write_group("druid-vine-ground", 64157, (
        frame(ImageEnhance.Brightness(cell).enhance(brightness), scale, pivot, (43, 48), 21, threshold)
        for cell, pivot, brightness, scale, threshold in zip(
            mat, mat_pivots, (0.84, 0.73, 0.64, 0.56), (0.103, 0.086, 0.069, 0.052), (207, 213, 219, 224)
        )
    ))
    write_group("druid-vine-ground", 64161, (
        frame(cell, scale, pivot, (95, 106), 24, threshold)
        for row, scale, pivots, threshold in (
            (corpse, 0.236, ((178, 210), (182, 208), (181, 211), (179, 209)), 203),
            (life, 0.311, ((178, 218), (177, 215), (179, 215), (177, 209)), 206),
            (mana, 0.307, ((180, 222), (181, 222), (176, 220), (173, 216)), 206),
        )
        for cell, pivot in zip(row, pivots)
    ))
    write_group("druid-vine-ground", 64173, (
        (ART / "druid-rabies" / "statics" / f"{graphic}.art").read_bytes()
        for graphic in range(63723, 63727)
    ))

    fire, frost, storm, slow = load_grid("paladin-holy-auras", (1254, 1254))
    # The client maps this 240 by 132 painted footprint to the live pulse radius.
    write_group("paladin-holy-auras", 64177, (
        holy_ring(cell, (240, 132), (248, 140), 70, threshold, brightness)
        for row in (fire, frost, storm)
        for cell, threshold, brightness in zip(row, (219, 221, 218, 203), (1.0, 1.0, 1.0, 0.73))
    ))
    write_group("paladin-holy-auras", 64189, (
        holy_ring(row[index], (43, 25), (52, 31), 14, 207, brightness)
        for row in (fire, frost, storm)
        for index, brightness in zip((0, 1, 2, 1), (0.66, 0.70, 0.73, 0.69))
    ))
    write_group("paladin-holy-auras", 64201, (
        holy_ring(cell, (30, 15), (38, 22), 11, 205, 0.95) for cell in slow
    ))
    contacts = load_grid("paladin-holy-contacts", (1448, 1086), rows=3)
    contact_pivots = (
        ((215, 218), (185, 228), (170, 229), (155, 218)),
        ((212, 204), (185, 215), (166, 204), (153, 175)),
        ((215, 169), (167, 152), (165, 152), (130, 157)),
    )
    write_group("paladin-holy-contacts", 64205, (
        frame(cell, 0.181, pivot, (64, 78), 49, 203)
        for row, pivots in zip(contacts, contact_pivots) for cell, pivot in zip(row, pivots)
    ))

    bolt, holy_hit, bolt_heal, bolt_fade = load_grid("paladin-holy-bolt", (1448, 1086))
    bolt_centers = ((224, 156), (222, 155), (225, 158), (223, 157))
    write_group("paladin-holy-bolt", 64217, directional_flight(
        bolt, bolt_centers, 0.192, (72, 71), 27, 208
    ))
    heaven_bolts = []
    for cell in bolt:
        cool = ImageOps.colorize(cell.convert("L"), "#3c496a", "#f4f6ff")
        cool.putalpha(cell.getchannel("A"))
        heaven_bolts.append(cool)
    write_group("paladin-holy-bolt", 64249, directional_flight(
        heaven_bolts, bolt_centers, 0.155, (58, 60), 23, 201
    ))

    hammer, hammer_hit, lightning, warning = load_grid("paladin-hammer-heavens", (1254, 1254))
    write_group("paladin-hammer-heavens", 64281, (
        frame(cell, 0.166, center, (63, 63), 27, 227)
        for cell, center in zip(hammer, ((163, 179), (156, 156), (150, 174), (162, 154)))
    ))

    hit_centers = ((182, 144), (179, 143), (180, 144), (176, 143))
    write_group("paladin-holy-bolt", 64285, (
        frame(cell, 0.145, center, (56, 68), 43, threshold)
        for cell, center, threshold in zip(holy_hit, hit_centers, (221, 219, 214, 186))
    ))
    write_group("paladin-holy-bolt", 64289, (
        frame(cell.crop((0, 15, cell.width, cell.height)), 0.204, (181, 181), (72, 69), 23, threshold)
        for cell, threshold in zip(bolt_heal, (213, 206, 208, 176))
    ))
    write_group("paladin-holy-bolt", 64293, (
        frame(cell, 0.204, center, (76, 83), 49, threshold)
        for cell, center, threshold in zip(holy_hit, hit_centers, (223, 222, 220, 191))
    ))
    write_group("paladin-holy-bolt", 64297, (
        frame(cell, 0.206, (180, 136), (55, 56), 23, threshold)
        for cell, threshold in zip(bolt_fade, (211, 205, 186, 113))
    ))

    hammer_contacts = ((109, 181), (103, 184), (112, 183), (116, 182))
    for graphic, scale, size, bottom in ((64301, 0.154, (58, 66), 38), (64305, 0.208, (80, 92), 52),
                                          (64309, 0.140, (54, 57), 26)):
        write_group("paladin-hammer-heavens", graphic, (
            frame(cell, scale, center, size, bottom, threshold)
            for cell, center, threshold in zip(hammer_hit, hammer_contacts, (230, 232, 229, 214))
        ))
    write_group("paladin-hammer-heavens", 64313, (
        frame(cell, 0.216, (156, 156), (70, 50), 14, threshold)
        for cell, threshold in zip(warning, (183, 218, 220, 221))
    ))
    write_group("paladin-hammer-heavens", 64317, (
        frame(cell, 0.46, (156, 267), (128, 167), 31, threshold)
        for cell, threshold in zip(lightning, (187, 203, 201, 213))
    ))
    write_group("paladin-hammer-heavens", 64321, (
        frame(ImageEnhance.Brightness(cell).enhance(brightness), scale, (155, 156), (133, 80), 31, threshold)
        for cell, brightness, scale, threshold in zip(reversed(warning), (1.10, 1.06, 0.91, 0.73),
                                                      (0.32, 0.40, 0.45, 0.46), (215, 213, 217, 192))
    ))

    wards = load_grid("paladin-resistance-wards", (1254, 1254))
    # Different ring sizes keep independently stacked wards readable at the same feet.
    ward_frames = []
    for row, size, canvas, padding, threshold, palette in zip(wards,
            ((44, 24), (54, 28), (64, 32), (76, 37)), ((50, 31), (60, 35), (70, 39), (82, 44)),
            (15, 17, 19, 22), (185, 187, 175, 183), (
                ("#39230d", "#ffcd82", "#da5828"), ("#15344f", "#aeeeff", "#368da6"),
                ("#2b1c49", "#dcc5ff", "#9458de"), ("#21321c", "#fff3b2", "#80a06a")
            )):
        for cell in row:
            tint = ImageOps.colorize(cell.convert("L"), *palette)
            tint.putalpha(cell.getchannel("A"))
            ward_frames.append(holy_ring(Image.blend(cell, tint, 0.74), size, canvas, padding, threshold))
    write_group("paladin-resistance-wards", 64325, ward_frames)

    prayer, cleanse, meditation, mana = load_grid("paladin-recovery-auras", (1264, 1244))
    redeem_ring, corpse, thread, receipt = load_grid("paladin-redemption", (1268, 1240))
    # The same quiet recovery motif has a distinct palette and footprint for each aura.
    for graphic, row, size, palette in (
        (64341, meditation, (36, 16), ("#613a16", "#f8c282", "#b77040")),
        (64345, meditation, (44, 19), ("#47255d", "#c9b2e7", "#8f66ad")),
        (64349, meditation, (52, 22), ("#142c48", "#aeeafb", "#4283b5")),
        (64353, redeem_ring, (58, 25), ("#38422b", "#e1d895", "#8e995d")),
    ):
        ring_frames = []
        for cell in row:
            colored = ImageOps.colorize(cell.convert("L"), *palette)
            colored.putalpha(cell.getchannel("A"))
            width, height = size
            ring_frames.append(holy_ring(colored, size, (width + 6, height + 8), (height + 8) // 2, 195))
        write_group("paladin-recovery-auras", graphic, ring_frames)

    write_group("paladin-recovery-auras", 64357, (
        frame(cell, 0.202, pivot, (74, 87), 19, threshold)
        for cell, pivot, threshold in zip(prayer, ((173, 267), (161, 269), (143, 273), (142, 261)),
                                          (195, 195, 200, 179))
    ))
    write_group("paladin-recovery-auras", 64361, (
        frame(cell, 0.202, pivot, (71, 88), 23, threshold)
        for cell, pivot, threshold in zip(cleanse, ((162, 229), (153, 235), (154, 234), (148, 223)),
                                          (199, 201, 203, 175))
    ))
    for graphic, scale, canvas in ((64365, 0.157, (42, 57)), (64377, 0.215, (57, 71))):
        write_group("paladin-recovery-auras", graphic, (
            frame(cell, scale, pivot, canvas, 24, threshold)
            for cell, pivot, threshold in zip(mana, ((164, 208), (157, 195), (152, 190), (149, 173)),
                                              (204, 203, 204, 183))
        ))
    write_group("paladin-redemption", 64369, (
        frame(cell, 0.244, pivot, (73, 91), 19, threshold)
        for cell, pivot, threshold in zip(corpse, ((162, 252), (156, 252), (155, 253), (148, 251)),
                                          (215, 207, 211, 199))
    ))
    write_group("paladin-redemption", 64373, (
        frame(cell, 0.239, pivot, (58, 78), 23, threshold)
        for cell, pivot, threshold in zip(receipt, ((156, 182), (157, 182), (157, 148), (154, 150)),
                                          (214, 210, 213, 202))
    ))
    write_group("paladin-redemption", 64381, directional_flight(
        thread, ((205, 145), (215, 145), (195, 145), (188, 145)), 0.115, (44, 48), 20, 196
    ))

    offense = load_grid("paladin-offense-auras", (1254, 1254))
    offense_frames = []
    for row, size, padding, threshold, brightness, palette in zip(offense,
            ((48, 30), (56, 28), (64, 37), (76, 36)), (14, 16, 16, 22), (188, 185, 215, 221),
            (1.22, 1.26, 1.0, 1.0), (
                ("#552410", "#ffca78", "#d37a33"), ("#163e49", "#9aefde", "#48b6ae"),
                ("#2c1845", "#d4c2ff", "#936cd4"), ("#511728", "#ffc17d", "#cb484e")
            )):
        for cell in row:
            tint = ImageOps.colorize(cell.convert("L"), *palette)
            tint.putalpha(cell.getchannel("A"))
            width, height = size
            offense_frames.append(holy_ring(Image.blend(cell, tint, 0.65), size,
                                             (width + 6, height + 8), padding, threshold, brightness))
    write_group("paladin-offense-auras", 64413, offense_frames)

    guard, = load_grid("paladin-concentration", (2172, 724), rows=1)
    write_group("paladin-concentration", 64429, (
        frame(cell, 0.122, pivot, (69, 79), 41, threshold)
        for cell, pivot, threshold in zip(guard, ((271, 431), (268, 443), (266, 421), (268, 421)), (215, 210, 219, 126))
    ))

    defense = load_grid("paladin-defense-auras", (1261, 1247))
    defense_frames = []
    for row, size, padding, threshold, palette in zip(defense[:3],
            ((56, 34), (68, 32), (72, 38)), (15, 14, 18), (213, 222, 216), (
                ("#18263e", "#d4e9ff", "#718cc4"), ("#143e2b", "#d6f6af", "#589c59"),
                ("#3d153b", "#ffb29e", "#a74f75")
            )):
        for cell in row:
            tint = ImageOps.colorize(cell.convert("L"), *palette)
            tint.putalpha(cell.getchannel("A"))
            width, height = size
            defense_frames.append(holy_ring(Image.blend(cell, tint, 0.62), size,
                                             (width + 6, height + 8), padding, threshold, 1.18))
    for cell, pivot in zip(defense[3], ((156, 207), (156, 205), (155, 197), (155, 190))):
        tint = ImageOps.colorize(cell.convert("L"), "#501044", "#ffe1c5", "#ec8caa")
        tint.putalpha(cell.getchannel("A"))
        cell = ImageEnhance.Brightness(Image.blend(cell, tint, 0.58)).enhance(1.25)
        defense_frames.append(frame(cell, 0.252, pivot, (65, 70), 26, 218))
    write_group("paladin-defense-auras", 64433, defense_frames)

    shield = load_grid("holy-shield", (1312, 1199))
    write_group("holy-shield", 64449, (
        frame(ImageEnhance.Brightness(cell).enhance(1.13), scale, pivot, (92, 88), 45, threshold)
        for row, scale, threshold, pivots in zip(shield, (0.268, 0.265, 0.268, 0.265), (228, 225, 216, 199), (
            ((165, 123), (165, 132), (165, 150), (155, 169)),
            ((164, 147), (164, 147), (164, 147), (164, 147)),
            ((164, 147), (164, 147), (164, 147), (164, 147)),
            ((164, 147), (164, 147), (164, 147), (164, 147)),
        ))
        for cell, pivot in zip(row, pivots)
    ))

    sanctified, wave, strike = load_grid("paladin-sanctuary", (1774, 887), rows=3)
    write_group("paladin-sanctuary", 64465, (
        holy_ring(cell, (60, 29), (66, 36), 14, 219, 1.14) for cell in sanctified
    ))
    write_group("paladin-sanctuary", 64469, (
        holy_ring(cell, (240, 132), (248, 140), 70, threshold, brightness)
        for cell, threshold, brightness in zip(wave, (225, 223, 220, 213), (1.0, 1.0, 0.92, 0.77))
    ))
    write_group("paladin-sanctuary", 64473, (
        frame(cell, 0.218, center, (76, 82), 47, threshold)
        for cell, center, threshold in zip(strike, ((221, 145), (217, 145), (217, 148), (219, 157)),
                                           (225, 228, 225, 206))
    ))

    conviction, mark, snap = load_grid("paladin-conviction", (1448, 1086), rows=3)
    write_group("paladin-conviction", 64477, (
        holy_ring(cell, (66, 30), (72, 38), 14, 227, 1.17) for cell in conviction
    ))
    write_group("paladin-conviction", 64481, (
        frame(ImageEnhance.Brightness(cell).enhance(1.19), scale, center, (50, 82), 43, threshold)
        for row, scale, threshold, pivots in (
            (mark, 0.141, 225, ((183, 174), (182, 173), (184, 174), (179, 174))),
            (snap, 0.172, 221, ((177, 178), (177, 177), (181, 176), (180, 178))),
        )
        for cell, center in zip(row, pivots)
    ))

    write_group("paladin-sacrifice", 64489, grid_effects(
        "paladin-sacrifice", (1536, 1024), (
            ((195, 211), (193, 210), (191, 210), (191, 209)),
            ((191, 180), (190, 180), (193, 183), (192, 179)),
            ((155, 220), (126, 225), (109, 226), (109, 225)),
        ), (0.182, 0.192, 0.198), (90, 88), 39, 222
    ))
    write_group("paladin-smite", 64501, grid_effects(
        "paladin-smite", (1774, 887), (
            ((221, 250), (220, 246), (222, 246), (217, 248)),
            ((221, 231), (220, 242), (215, 240), (220, 222)),
        ), (0.172, 0.181), (84, 86), 38, 226
    ))
    write_group("paladin-vengeance", 64509, grid_effects(
        "paladin-vengeance", (1774, 887), (
            ((222, 251), (223, 251), (222, 250), (223, 249)),
            ((229, 247), (222, 248), (220, 249), (221, 248)),
        ), (0.176, 0.20), (85, 86), 37, 222
    ))

    write_group("paladin-zeal", 64517, grid_effects(
        "paladin-zeal", (1774, 887), (
            ((220, 286), (222, 281), (226, 284), (225, 285)),
            ((224, 217), (228, 220), (223, 218), (222, 217)),
        ), (0.165, 0.167), (78, 80), 36, 215
    ))
    launch, steps, charge_hit = load_grid("paladin-charge", (1448, 1086), rows=3)
    write_group("paladin-charge", 64525, (
        frame(cell, 0.202, pivot, (75, 72), 31, 224)
        for cell, pivot in zip(launch, ((184, 270), (181, 270), (182, 270), (177, 270)))
    ))
    write_group("paladin-charge", 64529, (
        frame(cell, 0.105, pivot, (48, 31), 13, threshold)
        for cell, pivot, threshold in zip(steps, ((186, 233), (183, 233), (173, 233), (175, 233)),
                                          (218, 192, 158, 100))
    ))
    write_group("paladin-charge", 64533, (
        frame(cell.crop((0, 0, right, cell.height)), 0.252, pivot, (78, 92), 32, 215)
        for cell, pivot, right in zip(charge_hit, ((151, 244), (143, 248), (143, 249), (139, 247)),
                                      (208, 220, 213, 212))
    ))

    conversion = load_grid("paladin-conversion", (1263, 1246))
    write_group("paladin-conversion", 64537, (
        frame(cell, scale, pivot, (63, 79), 38, 221)
        for row, scale, pivots in zip(conversion, (0.24, 0.185, 0.135, 0.145), (
            ((172, 205), (162, 202), (153, 201), (138, 202)),
            ((180, 183), (163, 183), (148, 183), (134, 183)),
            ((178, 173), (158, 173), (145, 176), (120, 173)),
            ((180, 159), (158, 154), (147, 154), (129, 151)),
        ))
        for cell, pivot in zip(row, pivots)
    ))

    howl_cast, howl_wave, howl_mark = load_grid("barbarian-howl", (1448, 1086), rows=3)
    taunt_cast, taunt_mark = load_grid("barbarian-taunt", (1774, 887), rows=2)
    battle_cast, battle_wave, battle_mark = load_grid("barbarian-battle-cry", (1448, 1086), rows=3)
    war_cast, war_wave, war_hit = load_grid("barbarian-war-cry", (1536, 1024), rows=3)
    for group, graphic, cells, pivots, scale, padding, threshold in (
        ("barbarian-howl", 64553, howl_cast, ((172, 220), (172, 213), (163, 212), (163, 211)), .229, 43, 197),
        ("barbarian-taunt", 64557, taunt_cast, ((221, 239), (222, 239), (223, 239), (221, 239)), .182, 44, 222),
        ("barbarian-battle-cry", 64561, battle_cast, ((180, 205), (180, 204), (176, 201), (172, 199)), .199, 43, 225),
        ("barbarian-war-cry", 64565, war_cast, ((195, 230), (195, 211), (194, 204), (191, 204)), .217, 43, 214),
        ("barbarian-howl", 64569, howl_mark, ((181, 136), (181, 134), (181, 136), (176, 134)), .137, 53, 221),
        ("barbarian-taunt", 64573, taunt_mark, ((221, 219), (221, 220), (222, 219), (223, 220)), .113, 52, 229),
        ("barbarian-battle-cry", 64577, battle_mark, ((180, 181), (180, 180), (180, 181), (181, 181)), .143, 51, 228),
        ("barbarian-war-cry", 64581, war_hit, ((195, 182), (187, 182), (179, 180), (180, 181)), .205, 46, 216),
    ):
        write_group(group, graphic, (
            frame(cell, scale, pivot, (79, 89), padding, threshold) for cell, pivot in zip(cells, pivots)
        ))
    for group, graphic, cells, pivots, scale, threshold in (
        ("barbarian-howl", 64585, howl_wave, ((205, 181), (204, 183), (218, 181), (229, 181)), .131, 200),
        ("barbarian-battle-cry", 64617, battle_wave, ((183, 180), (215, 180), (222, 179), (224, 177)), .145, 212),
        ("barbarian-war-cry", 64649, war_wave, ((217, 174), (248, 172), (248, 172), (246, 171)), .165, 213),
    ):
        write_group(group, graphic, directional_flight(cells, pivots, scale, (53, 58), 29, threshold))

    for name, cast_id, ward_id, fade_id, wave_id, cast_pivots, ward_pivots, wave_pivots, ward_scale, ward_height, threshold in (
        ("barbarian-shout", 64681, 64693, 64705, 64717,
         ((193, 184), (191, 184), (193, 181), (193, 183)),
         ((192, 182), (192, 187), (192, 188), (190, 181)),
         ((238, 177), (253, 177), (271, 176), (268, 174)), .184, 13, 216),
        ("barbarian-battle-orders", 64685, 64697, 64709, 64749,
         ((197, 208), (190, 194), (180, 191), (175, 191)),
         ((205, 174), (199, 181), (180, 187), (178, 182)),
         ((252, 185), (268, 186), (272, 190), (264, 182)), .159, 29, 207),
        ("barbarian-battle-command", 64689, 64701, 64713, 64781,
         ((192, 204), (193, 204), (190, 204), (193, 204)),
         ((193, 184), (192, 188), (192, 195), (193, 191)),
         ((243, 207), (255, 207), (280, 207), (265, 207)), .138, 45, 205),
    ):
        cast, wave, ward = load_grid(name, (1536, 1024), rows=3)
        if name == "barbarian-battle-command":
            # Remove the bright point from the peak cast cell above this wave.
            wave[2] = wave[2].copy()
            wave[2].paste((0, 0, 0, 0), (0, 0, wave[2].width, 45))
        write_group(name, cast_id, (
            frame(cell, .205, pivot, (79, 89), 44, threshold) for cell, pivot in zip(cast, cast_pivots)
        ))
        write_group(name, ward_id, (
            frame(cell, ward_scale, pivot, (64, 66), ward_height, threshold)
            for cell, pivot in zip(ward, ward_pivots)
        ))
        write_group(name, fade_id, (
            frame(ImageEnhance.Brightness(ward[pose]).enhance(light), ward_scale, ward_pivots[pose],
                  (64, 66), ward_height, cutoff)
            for pose, light, cutoff in ((2, .94, threshold), (1, .82, threshold + 7),
                                         (0, .64, threshold + 13), (0, .39, threshold + 21))
        ))
        write_group(name, wave_id, directional_flight(wave, wave_pivots, .147, (59, 65), 31, threshold))

    barbarian_corpse_effects()
    barbarian_strikes()
    barbarian_motion()
    barbarian_paired()
    assassin_martial_arts()
    assassin_dragon_finishers()
    assassin_blade_skills()
    assassin_shadow_skills()
    assassin_lob_skills()
    assassin_sentry_skills()
    assassin_shadow_summons()
    warlock_demon_summons()
    warlock_rituals()
    warlock_engorge()
    warlock_fire()
