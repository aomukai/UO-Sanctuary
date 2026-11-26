"""Build alternating walk cycles from the Valkyrie and werebear idle artwork."""

from math import cos, pi, sin, tau

from PIL import Image, ImageChops, ImageDraw, ImageEnhance


# The polygons follow the visible seams in the 128-pixel idle poses. The
# profile's far leg is occluded, so it borrows the near leg's artwork.
LEG_OUTLINES = {
    "valkyrie": (
        (((54, 73), (64, 74), (64, 111), (53, 111), (53, 103)),
         ((64, 74), (72, 73), (76, 83), (80, 111), (65, 111), (63, 85))),
        (((54, 73), (62, 73), (63, 84), (63, 110), (55, 111), (55, 95)),
         ((62, 73), (68, 73), (71, 81), (80, 103), (78, 110), (68, 110), (66, 86))),
        (((58, 75), (67, 75), (72, 83), (71, 110), (55, 110)),
         None),
        (((54, 72), (65, 74), (64, 96), (62, 108), (54, 108)),
         ((65, 74), (74, 73), (77, 103), (73, 106), (63, 106), (64, 96))),
        (((54, 71), (63, 73), (62, 103), (50, 103), (51, 86)),
         ((63, 73), (72, 72), (74, 102), (66, 103), (64, 88))),
    ),
    "werebear": (
        (((48, 75), (56, 74), (60, 79), (60, 98), (64, 111), (38, 111), (40, 92), (45, 87)),
         ((71, 75), (79, 76), (83, 84), (85, 95), (91, 109), (91, 112), (66, 112), (64, 101), (71, 88))),
        (((46, 74), (54, 75), (62, 82), (60, 95), (61, 108), (35, 108), (36, 99), (44, 92)),
         ((67, 76), (76, 76), (79, 83), (80, 90), (81, 95), (88, 100), (89, 112),
          (61, 112), (62, 100), (70, 92), (65, 84))),
        (((56, 85), (71, 83), (77, 89), (76, 105), (42, 109), (42, 98), (55, 96)),
         None),
        (((51, 74), (61, 73), (65, 79), (65, 93), (64, 105), (36, 106), (37, 96), (51, 91)),
         ((67, 82), (78, 83), (86, 93), (86, 104), (65, 104), (65, 96))),
        (((51, 70), (60, 71), (62, 83), (60, 94), (63, 102), (38, 103), (38, 93), (45, 85)),
         ((67, 70), (75, 68), (80, 78), (89, 94), (89, 102), (65, 103), (64, 92), (66, 82))),
    ),
}

# Project the werebear's stride onto the five ClassicUO screen facings.
WEREBEAR_STRIDE = ((0, 5), (-6, 4), (-10, 0), (-6, -4), (0, -5))
WEREBEAR_FOOT_INSET = (4, 3, 0, 3, 4)
WEREBEAR_KNEES = ((87, 87), (87, 88), (91, 88), (86, 91), (84, 84))

# Kneecap and ankle rows on the registered artwork, in image-left / image-right
# order. In profile the second leg is the depth-offset copy of the first.
VALKYRIE_JOINTS = (
    ((86, 102), (86, 102)),
    ((87, 101), (86, 101)),
    ((86, 97), (83, 94)),
    ((84, 96), (83, 95)),
    ((82, 94), (82, 94)),
)
VALKYRIE_STRIDE = ((0, 5), (-5, 4), (-8, 0), (-5, -4), (0, -5))
VALKYRIE_FOOT_INSET = (6, 4, 0, 4, 5)

# Forward travel, ankle lift, knee travel, and knee lift for one boot.
# The two halves cover heel contact through toe-off, then swing and recovery.
# The knee leads the ankle substantially at passing so the shin folds back.
VALKYRIE_STEP = (
    (1.00, 0, .48, 0),      # Contact
    (.67, 0, .65, 0),      # Down
    (.33, 0, .47, 0),
    (0, 0, .18, 0),        # Passing (weight on this foot)
    (-.33, 0, .02, 0),     # Up
    (-.67, 1, -.20, 0),    # Heel lift
    (-1.00, 2, -.40, 0),   # Toe-off
    (-.79, 5, -.07, 1),
    (-.43, 7, .46, 3),
    (.04, 8, .77, 3),      # Passing (this foot in swing)
    (.52, 5, .80, 2),
    (.86, 2, .62, 1),      # Reach
)
VALKYRIE_BOB = (0, 2, 1, 0, -1, -1)
# Belt pivot, lateral travel in screen pixels, pelvic tilt, and the spear's inner edge.
VALKYRIE_TORSO = (
    (64, 73, 3.2, .17, 52),
    (63, 73, 2.8, .15, 54),
    (64, 74, 2.1, .09, 54),
    (63, 72, 2.6, .15, 54),
    (64, 72, 3.1, .17, 74),
)
VALKYRIE_HIP_X = ((58, 68), (58, 67), (63, 66), (59, 68), (58, 68))


def leg_layer(sprite, outline):
    selection = Image.new("L", sprite.size)
    ImageDraw.Draw(selection).polygon(outline, fill=255)
    layer = sprite.copy()
    layer.putalpha(ImageChops.multiply(sprite.getchannel("A"), selection))
    return layer


def pose_werebear_leg(sprite, knee, inward, travel, lift, stride_x, stride_y, bob):
    _, top, _, bottom = sprite.getbbox()
    ankle = bottom - 4
    foot_dx = round(inward + travel * stride_x)
    foot_dy = round(travel * stride_y - lift)
    knee_dx = round(inward * .5 + (travel * .38 + lift * .10) * stride_x)
    knee_dy = round((foot_dy + bob) * .45 - lift * .32)

    joints = ((top, 0, bob), (knee, knee_dx, knee_dy),
              (ankle, foot_dx, foot_dy), (bottom, foot_dx, foot_dy))
    return warp_leg(sprite, joints)


def warp_leg(sprite, joints):
    # Adjacent strips meet on the same knee and ankle coordinates.
    width = sprite.width
    mesh = [
        ((0, upper_y + upper_dy, width, lower_y + lower_dy),
         (-upper_dx, upper_y, -lower_dx, lower_y, width - lower_dx, lower_y, width - upper_dx, upper_y))
        for (upper_y, upper_dx, upper_dy), (lower_y, lower_dx, lower_dy) in zip(joints, joints[1:])
    ]
    return sprite.transform(sprite.size, Image.Transform.MESH, mesh, Image.Resampling.BICUBIC)


def valkyrie_body_pose(direction, frame, bob):
    waist_x, waist_y, sway, tilt, spear_edge = VALKYRIE_TORSO[direction]
    # Weight peaks over the planted foot at passing. The shoulders follow a frame later.
    weight = .5 * sin(tau * (frame - .5) / len(VALKYRIE_STEP))
    counter = sin(tau * (frame - 1.5) / len(VALKYRIE_STEP))
    spear_side = -1 if direction == 4 else 1

    def displacement(x, y):
        # Keep the spear and the outside of the gripping hand on the same rigid path.
        grip = max(0, min(1, (x - spear_edge) * spear_side / 5))
        pelvis = max(0, min(1, (y - waist_y + 12) / 10))
        shoulders = max(0, 1 - abs(y - (waist_y - 22)) / 13)
        dx = grip * (-sway * weight * pelvis + 1.1 * counter * shoulders)
        dy = grip * (x - waist_x) * tilt * (weight * pelvis - .55 * counter * shoulders)
        return dx, bob + dy

    return displacement


def pose_valkyrie_torso(torso, displacement):
    # Sample a small inverse mesh so the waist rolls while the chest rotates
    # back over it. The same displacement places the thigh roots at the hem.
    left, top, right, bottom = torso.getbbox()
    columns = [*range(left - 5, right + 5, 3), right + 5]
    rows = [*range(top - 5, bottom + 5, 3), bottom + 5]
    vertices = {}
    for y in rows:
        for x in columns:
            src_x, src_y = x, y
            for _ in range(4):
                dx, dy = displacement(src_x, src_y)
                src_x, src_y = x - dx, y - dy
            vertices[x, y] = src_x, src_y
    mesh = [
        ((x, y, right_x, bottom_y),
         (*vertices[x, y], *vertices[x, bottom_y], *vertices[right_x, bottom_y], *vertices[right_x, y]))
        for y, bottom_y in zip(rows, rows[1:])
        for x, right_x in zip(columns, columns[1:])
    ]
    return torso.transform(torso.size, Image.Transform.MESH, mesh, Image.Resampling.BICUBIC)


def valkyrie_walk(torso, first, second, direction):
    stride_x, stride_y = VALKYRIE_STRIDE[direction]
    inset = VALKYRIE_FOOT_INSET[direction]
    leg_joints = tuple(zip((first, second), VALKYRIE_JOINTS[direction]))
    half = len(VALKYRIE_BOB)
    frames = []
    for i in range(len(VALKYRIE_STEP)):
        body_pose = valkyrie_body_pose(direction, i, VALKYRIE_BOB[i % half])
        legs = []
        for side, (leg, (knee, ankle)) in enumerate(leg_joints):
            travel, lift, knee_travel, knee_lift = VALKYRIE_STEP[(i + side * half) % len(VALKYRIE_STEP)]
            inward = inset if side == 0 else -inset
            _, top, _, bottom = leg.getbbox()
            hip_x, hip_y = (round(offset) for offset in body_pose(VALKYRIE_HIP_X[direction][side], top))
            knee_dx = round((hip_x + inward) / 2 + knee_travel * stride_x)
            knee_dy = round(hip_y / 2 + knee_travel * stride_y - knee_lift)
            foot_dx = round(inward + travel * stride_x)
            foot_dy = round(travel * stride_y - lift)
            # Keep the toe closer to the floor during heel lift and toe-off.
            toe_off = int(travel < -.6 and lift > 0)
            joints = ((top, hip_x, hip_y), (knee, knee_dx, knee_dy),
                      (ankle, foot_dx, foot_dy), (bottom, foot_dx, foot_dy + toe_off))
            legs.append(warp_leg(leg, joints))
        frame = Image.new("RGBA", torso.size)
        for leg in legs if direction == 1 else reversed(legs):
            frame.alpha_composite(leg)
        frame.alpha_composite(pose_valkyrie_torso(torso, body_pose))
        frames.append(frame)
    return frames


def werebear_walk(torso, first, second, direction):
    stride_x, stride_y = WEREBEAR_STRIDE[direction]
    inset = WEREBEAR_FOOT_INSET[direction]
    first_knee, second_knee = WEREBEAR_KNEES[direction]
    far, near = first, second
    far_knee, near_knee = first_knee, second_knee
    if direction != 1:
        far, near = near, far
        far_knee, near_knee = near_knee, far_knee
        inset = -inset
    half = 6
    max_lift = 6
    # The planted foot moves backward relative to the body. The other
    # foot lifts and passes it. Each leg repeats that motion half a cycle later.
    stance = [(1 - 2 * i / half, 0) for i in range(half)]
    swing = [(-cos(pi * i / half), max_lift * sin(pi * i / half)) for i in range(half)]
    phases = stance + swing

    frames = []
    for i, (travel, lift) in enumerate(phases):
        other_travel, other_lift = phases[(i + half) % len(phases)]
        bob = -1 if 1 <= i % half < half - 1 else 0
        frame = Image.new("RGBA", torso.size)
        frame.alpha_composite(pose_werebear_leg(far, far_knee, inset, travel, lift, stride_x, stride_y, bob))
        frame.alpha_composite(pose_werebear_leg(near, near_knee, -inset, other_travel, other_lift, stride_x, stride_y, bob))
        frame.alpha_composite(torso, (0, bob))
        frames.append(frame)
    return frames


def walk_cycle(sprite, name, direction):
    first_outline, second_outline = LEG_OUTLINES[name][direction]
    first = leg_layer(sprite, first_outline)
    sprite_alpha = sprite.getchannel("A")
    if second_outline is None:
        second = ImageChops.offset(ImageEnhance.Brightness(first).enhance(.77), 3, -3)
        leg_alpha = first.getchannel("A")
    else:
        second = leg_layer(sprite, second_outline)
        leg_alpha = ImageChops.lighter(first.getchannel("A"), second.getchannel("A"))
    torso = sprite.copy()
    torso.putalpha(ImageChops.subtract(sprite_alpha, leg_alpha))
    if name == "valkyrie":
        return valkyrie_walk(torso, first, second, direction)
    return werebear_walk(torso, first, second, direction)
