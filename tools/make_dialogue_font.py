#!/usr/bin/env python3
"""Rasterize Latin and Japanese faces into a ClassicUO Unicode font."""

import argparse
from pathlib import Path
import struct
import subprocess

from PIL import Image, ImageDraw, ImageFont


def japanese_characters():
    for plane in (b"", b"\x8f"):
        for row in range(0xA1, 0xFF):
            for column in range(0xA1, 0xFF):
                text = (plane + bytes((row, column))).decode("euc_jis_2004", errors="ignore")
                if len(text) == 1 and ord(text) <= 0xFFFF:
                    yield text


def font_characters(path, index=0):
    ranges = subprocess.check_output(["fc-query", "--index", str(index), "--format", "%{charset}", path], text=True)
    codes = set()
    for span in ranges.split():
        first, _, last = span.partition("-")
        codes.update(range(int(first, 16), int(last or first, 16) + 1))
    return codes


def stored_glyph(data, code):
    offset = struct.unpack_from("<I", data, 4 * code)[0]
    if not offset:
        return b""
    _, _, width, height = data[offset:offset + 4]
    return data[offset:offset + 4 + ((width + 7) // 8) * height]


def write_font(latin_font, japanese_font, output, fallback=None, latin_codes=None, japanese_codes=None):
    latin = set(map(chr, range(0x21, 0x7F))) | set(map(chr, range(0xA1, 0x100))) | set("Œœ€…‘’“”–—")
    japanese = set(japanese_characters()) | set(map(chr, range(0xFF61, 0xFFA0))) | {"ẞ"}
    if fallback is not None:
        latin.update(map(chr, range(0x100, 0x250)))
        latin.update(map(chr, range(0x1E00, 0x1F00)))
        inherited = {chr(code) for code, (offset,) in enumerate(struct.iter_unpack("<I", fallback[:4 * 65536])) if offset}
    else:
        inherited = set()
    data = bytearray(4 * 65536)
    for char in sorted(latin | japanese | inherited):
        code = ord(char)
        font, coverage = (latin_font, latin_codes) if char in latin else (japanese_font, japanese_codes)
        if coverage is not None and code not in coverage:
            glyph = stored_glyph(fallback, code)
        else:
            left, _, right, _ = font.getbbox(char, anchor="ls")
            width = max(1, round(font.getlength(char)) - 1, right - min(left, 0))
            image = Image.new("L", (width, 23))
            ImageDraw.Draw(image).text((-min(left, 0), 18), char, font=font, fill=255, anchor="ls")
            bitmap = image.point(lambda alpha: 255 if alpha >= 92 else 0).convert("1", dither=Image.Dither.NONE)
            glyph = bytes((0, 0, bitmap.width, bitmap.height)) + bitmap.tobytes()
        if glyph:
            struct.pack_into("<I", data, 4 * code, len(data))
            data.extend(glyph)
    output.write_bytes(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("latin", type=Path, help="Latin font")
    parser.add_argument("japanese", type=Path, help="Japanese font")
    parser.add_argument("output", type=Path)
    parser.add_argument("--japanese-index", type=int, default=0, help="Face index if using a TTC")
    parser.add_argument("--fallback-mul", type=Path, help="Copy missing glyphs from this bitmap font. Requires fc-query.")
    args = parser.parse_args()
    fallback = args.fallback_mul
    write_font(ImageFont.truetype(str(args.latin), 17),
               ImageFont.truetype(str(args.japanese), 18, index=args.japanese_index), args.output,
               fallback.read_bytes() if fallback else None,
               font_characters(args.latin) if fallback else None,
               font_characters(args.japanese, args.japanese_index) if fallback else None)


if __name__ == "__main__":
    main()
