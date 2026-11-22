#!/usr/bin/env python3
"""Pack the generated bonfire sheet into ClassicUO loose statics."""

from pathlib import Path

from PIL import Image

from build_spell_effect_art import frame


ART = Path(__file__).resolve().parents[1] / "art" / "world" / "ettin-camp"
FIRST_GRAPHIC = 65512  # CampGraphics.Bonfire.


def main():
    destination = ART / "statics"
    destination.mkdir(exist_ok=True)
    with Image.open(ART / "bonfire-atlas.png") as sheet:
        if sheet.mode != "RGBA" or sheet.size != (1774, 887):
            raise ValueError("Expected the four by two bonfire sheet")
        for index in range(8):
            row, column = divmod(index, 4)
            cell = sheet.crop((column * sheet.width // 4, row * sheet.height // 2,
                               (column + 1) * sheet.width // 4, (row + 1) * sheet.height // 2))
            # Keep the base of the logs aligned to the same ground point in every frame.
            packed = frame(cell, 0.22, (220, 387), (78, 102), 9, alpha_threshold=148)
            (destination / f"{FIRST_GRAPHIC + index}.art").write_bytes(packed)


if __name__ == "__main__":
    main()
