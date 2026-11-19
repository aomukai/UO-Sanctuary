#!/usr/bin/env python3
"""Rasterize IM Fell English and Japanese Noto Serif into a ClassicUO Unicode font."""

import argparse
from pathlib import Path
import struct

from PIL import Image, ImageDraw, ImageFont


def japanese_characters():
    for plane in (b"", b"\x8f"):
        for row in range(0xA1, 0xFF):
            for column in range(0xA1, 0xFF):
                text = (plane + bytes((row, column))).decode("euc_jis_2004", errors="ignore")
                if len(text) == 1 and ord(text) <= 0xFFFF:
                    yield text


def write_font(latin_font, japanese_font, output):
    latin = set(map(chr, range(0x21, 0x7F))) | set(map(chr, range(0xA1, 0x100))) | set("Œœ€…‘’“”–—")
    japanese = set(japanese_characters()) | set(map(chr, range(0xFF61, 0xFFA0))) | {"ẞ"}
    data = bytearray(4 * 65536)
    for char in sorted(latin | japanese):
        font = latin_font if char in latin else japanese_font
        left, _, right, _ = font.getbbox(char, anchor="ls")
        width = max(1, round(font.getlength(char)) - 1, right - min(left, 0))
        image = Image.new("L", (width, 23))
        ImageDraw.Draw(image).text((-min(left, 0), 18), char, font=font, fill=255, anchor="ls")
        bitmap = image.point(lambda alpha: 255 if alpha >= 92 else 0).convert("1", dither=Image.Dither.NONE)
        struct.pack_into("<I", data, 4 * ord(char), len(data))
        data.extend(bytes((0, 0, bitmap.width, bitmap.height)))
        data.extend(bitmap.tobytes())
    output.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("latin", type=Path, help="IM Fell English Roman TTF")
    parser.add_argument("japanese", type=Path, help="Noto Serif Japanese OTF or TTC")
    parser.add_argument("output", type=Path)
    parser.add_argument("--japanese-index", type=int, default=0, help="Face index if using a TTC")
    args = parser.parse_args()
    write_font(ImageFont.truetype(str(args.latin), 17),
               ImageFont.truetype(str(args.japanese), 18, index=args.japanese_index), args.output)


if __name__ == "__main__":
    main()
