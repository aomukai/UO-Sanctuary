#!/usr/bin/env python3
"""Extract the four molten channels and their reflected light from the registered painting."""
from pathlib import Path

from PIL import Image, ImageFilter

ART = Path(__file__).resolve().parents[1] / 'art/world/diablo-ritual'
# Native painting coordinates, clockwise from the rear opening.
CHANNELS = ((710, 593, 1145, 755), (1114, 676, 1360, 1145),
            (708, 1098, 1162, 1260), (501, 686, 748, 1157))


def main():
    with Image.open(ART / 'volcanic-chamber.png') as source:
        for i, box in enumerate(CHANNELS):
            stone = source.crop(box).convert('RGB')
            # Saturated orange identifies the melt. Black cooling crust and deep red blood
            # never reach the green-channel threshold. Scatter light a short way up the lip.
            heat = Image.new('L', stone.size)
            heat.putdata([max(0, min(255, (r-72)*3, (g-31)*5, (r-b-48)*4))
                          for r, g, b in stone.getdata()])
            spill = heat.filter(ImageFilter.GaussianBlur(7))
            glow = Image.new('RGBA', stone.size)
            pixels = []
            for j, ((r,g,b), hot, near) in enumerate(zip(stone.getdata(), heat.getdata(), spill.getdata())):
                x, y = j % stone.width, j // stone.width
                edge = min(1, min(x, y, stone.width-1-x, stone.height-1-y) / 12)
                alpha = round(max(hot * .94, near * .49) * edge)
                pixels.append((min(255, r + 105), min(245, g + 79), min(100, b + 27), alpha) if alpha else (0,0,0,0))
            glow.putdata(pixels)
            glow.save(ART / f'lava-glow-{i}.png')


if __name__ == '__main__':
    main()
