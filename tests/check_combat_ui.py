"""Exercise the actual mouse combat flow against the disposable integration server."""
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import statistics
from PIL import ImageGrab

ROOT = Path(__file__).resolve().parents[1]


def exercise(window, click, capture):
    if os.environ.get("SANCTUARY_TEST_GEM_SOCKETS_UI") == "1":
        from check_gem_socket_ui import exercise as gem_socket_exercise
        gem_socket_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_RUNES_UI") == "1":
        from check_rune_ui import exercise as rune_exercise
        rune_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_RUNEBARK_UI") == "1":
        from check_runebark_ui import exercise as runebark_exercise
        runebark_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_MOULDS_UI") == "1":
        from check_mould_ui import exercise as mould_exercise
        mould_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_SOCKETS_UI") == "1":
        from check_socket_ui import exercise as socket_exercise
        socket_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_GEAR_DROPS_UI") == "1":
        from check_gear_drop_ui import exercise as gear_exercise
        gear_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_IMBUING_UI") == "1":
        from check_imbuing_ui import exercise as imbuing_exercise
        imbuing_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_SALVAGE_UI") == "1":
        from check_salvage_ui import exercise as salvage_exercise
        salvage_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_WARDROBE_UI") == "1":
        from check_wardrobe_ui import exercise as wardrobe_exercise
        wardrobe_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_RECOGNITION_UI") == "1":
        from check_recognition_ui import exercise as recognition_exercise
        recognition_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_REPAIR_UI") == "1":
        from check_repair_ui import exercise as repair_exercise
        repair_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_MARKET_UI") == "1":
        from check_market_ui import exercise as market_exercise
        market_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_EQUIPMENT_CRAFTING_UI") == "1":
        from check_equipment_crafting_ui import exercise as equipment_exercise
        equipment_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_PROCESSING_UI") == "1":
        from check_processing_ui import exercise as processing_exercise
        processing_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_CRAFTING_UI") == "1":
        from check_crafting_ui import exercise as crafting_exercise
        crafting_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_PROFESSIONS_UI") == "1":
        from check_professions_ui import exercise as profession_exercise
        profession_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_ECONOMY_UI") == "1":
        from check_economy_ui import exercise as economy_exercise
        economy_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_BELT_UI") == "1":
        from check_belt_ui import exercise as belt_exercise
        belt_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_MOVEMENT_UI") == "1":
        click(335, 367)
        from check_movement_ui import exercise as movement_exercise
        movement_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_RESURRECTION_UI") == "1":
        from check_resurrection_ui import exercise as resurrection_exercise
        resurrection_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_PETS_UI") == "1":
        click(335, 367)
        from check_pets_ui import exercise as pets_exercise
        pets_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_STATS_UI") == "1":
        from check_stats_ui import exercise as stats_exercise
        stats_exercise(window, click, capture)
        return
    if os.environ.get("SANCTUARY_TEST_LOOT_ONLY") == "1":
        click(335, 367)
        from check_loot_ui import exercise as loot_exercise
        loot_exercise(window, click, capture)
        return
    serverlog = Path(os.environ.get("SANCTUARY_TEST_LOG", ROOT / ".local/logs/britain-baseline-check.log"))
    statefile = Path(os.environ["SANCTUARY_TEST_UI_STATE"])
    console = int(os.environ["SANCTUARY_TEST_CONSOLE_FD"])

    def command(name):
        offset = serverlog.stat().st_size
        os.write(console, ("combat-ui-" + name + "\n").encode())
        deadline = time.monotonic() + 5
        while time.monotonic() < deadline:
            output = serverlog.read_text(errors="replace")[offset:]
            if f"combat-ui-{name}: failed" in output:
                raise AssertionError(output[-2000:])
            if f"combat-ui-{name}: passed" in output:
                return json.loads(statefile.read_text())
            time.sleep(0.05)
        raise AssertionError(f"Missing combat-ui-{name}; inspect {serverlog}")

    geometry = dict(line.split("=", 1) for line in subprocess.check_output(
        ["xdotool", "getwindowgeometry", "--shell", window], text=True).splitlines() if "=" in line)
    origin_x, origin_y = int(geometry["X"]), int(geometry["Y"])

    def project(state, entity, sprite=True):
        a, b = state["player"], state[entity]
        dx, dy, dz = b["x"] - a["x"], b["y"] - a["y"], b["z"] - a["z"]
        x = 310 + 22 * (dx - dy)
        y = 250 + 22 * (dx + dy) - 4 * dz - ((60 if entity in ("enemy", "second") else 55) if sprite else 0)
        if sprite and entity in ("enemy", "second"):
            # Hostile skeletons are red in the default profile. Pick a solid skull
            # pixel within its projected area rather than a transparent sprite gap.
            im = ImageGrab.grab(xdisplay=os.environ["DISPLAY"])
            points = []
            for yy in range(y - 10, y + 11):
                for xx in range(x - 16, x + 17):
                    r, g, b = im.getpixel((origin_x + xx, origin_y + yy))[:3]
                    if r > 55 and r > 2 * g and r > 1.8 * b:
                        points.append((xx, yy))
            assert points, f"No visible hostile skull near {(x, y)}"
            mid_x = statistics.median(p[0] for p in points)
            mid_y = statistics.median(p[1] for p in points)
            return min(points, key=lambda p: (p[0] - mid_x) ** 2 + (p[1] - mid_y) ** 2)
        return x, y

    def body_gap(state, entity):
        # Pick visible terrain between the red ribs/limbs, proving the click does
        # not rely on an opaque skeleton pixel or on the old skull-pixel workaround.
        subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
        time.sleep(0.2)
        skull_x, skull_y = project(state, entity)
        im = ImageGrab.grab(xdisplay=os.environ["DISPLAY"])
        def pixel(x, y):
            return im.getpixel((origin_x + x, origin_y + y))[:3]
        def red(x, y):
            r, g, b = pixel(x, y)
            return r > 35 and r > 2 * g and r > 1.8 * b
        candidates = []
        for y in range(skull_y + 14, skull_y + 43):
            xs = [x for x in range(skull_x - 18, skull_x + 19) if red(x, y)]
            if len(xs) < 2:
                continue
            for x in range(min(xs) + 1, max(xs)):
                r, g, b = pixel(x, y)
                if g > 12 and g >= r * 0.8 and not red(x, y):
                    candidates.append((x, y))
        assert candidates, "No transparent body gap found for the skeleton picking test"
        return min(candidates, key=lambda p: (p[0] - skull_x) ** 2 + (p[1] - skull_y - 28) ** 2)

    def right_click(point):
        subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, point)], check=True)
        time.sleep(0.2)  # Let world picking resolve the new hover before the button event.
        subprocess.run(["xdotool", "click", "3"], check=True)

    def overlap_checks():
        subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80", "keydown", "Shift_L"], check=True)
        state = command("overlap-setup")
        # Isolate each sprite to identify pixels that belong to the front/back
        # creature and transparent gaps, rather than guessing from their geometry.
        def sample(phase):
            command("overlap-" + phase)
            time.sleep(0.5)
            images = []
            for _ in range(3):
                images.append(ImageGrab.grab(xdisplay=os.environ["DISPLAY"]))
                time.sleep(0.06)
            return images
        blank = sample("none")
        back_images = sample("back")
        front_images = sample("front")
        both_images = sample("both")
        def rgb(im, p):
            return im.getpixel((origin_x + p[0], origin_y + p[1]))[:3]
        def red(rgb):
            r, g, b = rgb
            return r > 35 and r > 2 * g and r > 1.8 * b
        region = [(x, y) for x in range(345, 435) for y in range(255, 365)]
        def body_pixel(images, p):
            return all(red(rgb(im, p)) and max(abs(a - b) for a, b in zip(rgb(im, p), rgb(blank[0], p))) > 5 for im in images)
        back = {p for p in region if body_pixel(back_images, p)}
        front = {p for p in region if body_pixel(front_images, p)}
        assert back and front, "Missing isolated overlapping creature pixels"
        def bounds(points):
            return min(p[0] for p in points), min(p[1] for p in points), max(p[0] for p in points), max(p[1] for p in points)
        bb, fb = bounds(back), bounds(front)
        def interior(p, b):
            return b[0] + 2 <= p[0] <= b[2] - 2 and b[1] + 2 <= p[1] <= b[3] - 2
        def clear(images, p):
            return all(max(abs(a - b) for a, b in zip(rgb(im, p), rgb(blank[0], p))) <= 2 for im in images)
        rear_visible = [p for p in back if interior(p, fb) and clear(front_images, p)
                        and all(red(rgb(im, p)) for im in both_images)]
        opaque_overlap = list(back & front)
        double_gap = [p for p in region if interior(p, fb) and interior(p, bb)
                      and clear(back_images, p) and clear(front_images, p) and clear(both_images, p)]
        assert rear_visible and opaque_overlap and double_gap, f"Missing overlap cases: rear={len(rear_visible)}, opaque={len(opaque_overlap)}, gaps={len(double_gap)}, bounds={bb}/{fb}"
        def choose(points):
            return min(points, key=lambda p: (p[0] - 387) ** 2 + (p[1] - 320) ** 2)
        print("Overlap test pixels:", choose(rear_visible), choose(opaque_overlap), choose(double_gap), flush=True)
        capture("combat-overlap-masks-both.png")
        for pixels, check, screenshot in (
            (rear_visible, "overlap-pick-back", "combat-overlap-rear-pixel.png"),
            (opaque_overlap, "overlap-pick-front", "combat-overlap-front-pixel.png"),
            (double_gap, "overlap-pick-front", "combat-overlap-gap.png"),
        ):
            subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, choose(pixels))], check=True)
            time.sleep(0.2)
            subprocess.run(["xdotool", "mousedown", "1"], check=True)
            time.sleep(0.2)
            command(check)
            capture(screenshot)
            subprocess.run(["xdotool", "mouseup", "1"], check=True)
            time.sleep(0.6)
        capture("combat-overlapping-selection.png")
        time.sleep(0.6)
        subprocess.run(["xdotool", "mousedown", "3"], check=True)
        time.sleep(0.2)
        command("overlap-held-front")
        subprocess.run(["xdotool", "mouseup", "3"], check=True)
        time.sleep(0.1)
        command("overlap-reset")
        subprocess.run(["xdotool", "keyup", "Shift_L", "mousemove", "--window", window, "590", "80"], check=True)
        time.sleep(0.5)
        print("Actual client: rear visible pixels beat front fallback, opaque overlap chooses front, and double gaps choose front fallback.", flush=True)

    if os.environ.get("SANCTUARY_TEST_TIMING_ONLY") == "1":
        click(335, 367)
        from check_timing_ui import exercise as timing_exercise
        timing_exercise(window, click, capture, project)
        from check_loot_ui import exercise as loot_exercise
        loot_exercise(window, click, capture)
        return
    state = command("setup")
    command("run")
    capture("movement-paperdoll-run.png")
    click(320, 317)  # The restored paperdoll toggle now selects Walk.
    command("walk")
    capture("movement-paperdoll-walk.png")
    # Holding Tab must toggle once, independently of key repeat or key release.
    subprocess.run(["xdotool", "keydown", "Tab"], check=True)
    time.sleep(1.1)
    command("run")
    subprocess.run(["xdotool", "keyup", "Tab"], check=True)
    time.sleep(0.2)
    command("run")
    click(320, 317)
    command("walk")
    # Save Desktop writes the chosen mode to this disposable character profile.
    subprocess.run(["xdotool", "key", "ctrl+alt+shift+s"], check=True)
    time.sleep(0.3)
    click(335, 367)  # Minimize the paperdoll, preserving world input beneath it.
    command("movement-mark")
    subprocess.run(["xdotool", "mousemove", "--window", window, "250", "310"], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(1.3)
    command("walk-step")
    subprocess.run(["xdotool", "key", "Tab"], check=True)
    time.sleep(1.1)
    command("run-step")
    subprocess.run(["xdotool", "keydown", "Shift_L", "mouseup", "1"], check=True)
    time.sleep(0.3)
    command("movement-reset")
    subprocess.run(["xdotool", "keyup", "Shift_L"], check=True)
    overlap_checks()
    time.sleep(1)
    state = command("stationary-setup")
    subprocess.run(["xdotool", "mousemove", "--window", window, "250", "310", "keydown", "Shift_L"], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(1.9)
    command("stationary-empty")
    subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, project(state, "enemy"))], check=True)
    time.sleep(0.4)
    command("stationary-far")
    subprocess.run(["xdotool", "keyup", "Shift_L"], check=True)
    time.sleep(1)
    command("stationary-stopped")
    subprocess.run(["xdotool", "mouseup", "1"], check=True)
    time.sleep(0.3)
    command("stationary-stopped")
    state = command("stationary-near")
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
    time.sleep(1)
    subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, project(state, "enemy")), "keydown", "Shift_L"], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(1.9)
    command("stationary-hit")
    capture("combat-stationary-melee.png")
    subprocess.run(["xdotool", "mousemove", "--window", window, "250", "310"], check=True)
    time.sleep(0.3)
    command("stationary-miss-mark")
    time.sleep(1.6)
    command("stationary-miss")
    subprocess.run(["xdotool", "mouseup", "1", "keyup", "Shift_L"], check=True)
    time.sleep(1)
    state = command("stationary-reset")
    print("Actual client: Shift-left empty/repeated swings, cursor-selected melee, distant harmless swings, and Shift/mouse release without chase passed.", flush=True)
    time.sleep(1)
    capture("combat-ready.png")
    click(*body_gap(state, "enemy"))
    time.sleep(5)
    capture("combat-autoattack.png")
    state = command("approach")
    click(250, 310)  # Ground near the player: cancel and walk away.
    time.sleep(0.4)
    command("stop")
    time.sleep(2)
    state = command("stopped")
    capture("combat-ground-stop.png")
    command("shift-mark")
    subprocess.run(["xdotool", "keydown", "--window", window, "Shift_L"], check=True)
    click(250, 310)
    time.sleep(1)
    command("shift")
    subprocess.run(["xdotool", "keyup", "--window", window, "Shift_L"], check=True)
    command("movement-mark")
    subprocess.run(["xdotool", "mousemove", "--window", window, "250", "310"], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "1"], check=True)
    time.sleep(2.4)
    command("movement-held")
    subprocess.run(["xdotool", "keydown", "Shift_L"], check=True)
    time.sleep(0.4)
    command("shift-mark")
    time.sleep(0.8)
    command("shift")
    subprocess.run(["xdotool", "keyup", "Shift_L"], check=True)
    time.sleep(1)
    command("movement-resumed")
    capture("combat-held-ground.png")
    command("cast-transition-mark")
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
    time.sleep(.15)
    subprocess.run(["xdotool", "mousedown", "3"], check=True)
    time.sleep(.4)
    command("cast-transition-still")
    time.sleep(1.3)
    command("cast-transition")
    subprocess.run(["xdotool", "mouseup", "3"], check=True)
    print("Actual client: right-held casting stops a left-held moving route after the committed step.", flush=True)
    subprocess.run(["xdotool", "keydown", "Shift_L", "mouseup", "1"], check=True)
    time.sleep(0.3)
    command("movement-reset")
    subprocess.run(["xdotool", "keyup", "Shift_L"], check=True)
    time.sleep(1)
    state = command("teeth-mark")
    point = project(state, "enemy")
    right_click(point)
    for frame in range(3):
        time.sleep(0.05)
        capture(f"combat-teeth-missile-{frame}.png")
    time.sleep(0.35)
    command("teeth")
    capture("combat-teeth.png")
    time.sleep(1)
    state = command("hold-mark")
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
    time.sleep(0.3)
    subprocess.run(["xdotool", "mousemove", "--window", window, *map(str, project(state, "enemy"))], check=True)
    time.sleep(0.2)
    subprocess.run(["xdotool", "mousedown", "3"], check=True)
    time.sleep(0.2)
    command("held-target")
    subprocess.run(["xdotool", "mousemove", "--window", window, "590", "80"], check=True)
    time.sleep(2.05)
    capture("combat-held-teeth.png")
    subprocess.run(["xdotool", "mouseup", "3"], check=True)
    time.sleep(0.1)
    command("held")
    time.sleep(1.1)
    command("released")
    command("far-mark")
    right_click((590, 80))
    time.sleep(0.2)
    command("far")
    state = command("unequip")
    time.sleep(1)
    point = project(state, "enemy")
    right_click(point)
    time.sleep(0.6)
    command("unavailable")
    state = command("death-setup")
    time.sleep(1)
    click(*project(state, "enemy"))
    time.sleep(2.3)
    state = command("death")
    capture("combat-target-dead.png")
    click(*project(state, "friendly"))
    time.sleep(0.8)
    command("friendly")
    capture("combat-friendly.png")
    from check_timing_ui import exercise as timing_exercise
    timing_exercise(window, click, capture, project)
    from check_loot_ui import exercise as loot_exercise
    loot_exercise(window, click, capture)
    print("Actual client: transparent-body click/target lock, approach/repeat, ground cancellation/continuous movement, Shift hold/resume, reagent-free held Teeth/cadence/release, far screen aim, weapon removal, death stop/no retarget, and friendly interaction passed.", flush=True)


if __name__ == "__main__":
    from check_creation_ui import check
    check(sys.argv[1], combat=True)
