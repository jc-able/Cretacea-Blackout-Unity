#!/usr/bin/env python3
"""Before/after contact sheet from two capture sets with the same file names.
   sheet_pairs.py <before_dir> <after_dir> <out.jpg> rel/path.png[=caption] ...      (one row per pair: before | after)"""
import sys
from PIL import Image, ImageDraw, ImageFont

before_dir, after_dir, out = sys.argv[1:4]
items = [a.split("=", 1) if "=" in a else [a, a.rsplit("/", 1)[-1].rsplit(".", 1)[0]] for a in sys.argv[4:]]
W, H, PAD, BAR = 800, 450, 10, 34
try:
    font = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 20)
    small = ImageFont.truetype("/System/Library/Fonts/Helvetica.ttc", 16)
except OSError:
    font = small = ImageFont.load_default()
sheet = Image.new("RGB", (PAD * 3 + W * 2, PAD + (H + BAR + PAD) * len(items)), (18, 18, 20))
d = ImageDraw.Draw(sheet)
for i, (rel, cap) in enumerate(items):
    y = PAD + i * (H + BAR + PAD)
    for j, (root, tag) in enumerate(((before_dir, "BEFORE"), (after_dir, "AFTER"))):
        x = PAD + j * (W + PAD)
        try:
            im = Image.open(f"{root}/{rel}").convert("RGB").resize((W, H), Image.LANCZOS)
            sheet.paste(im, (x, y + BAR))
        except OSError:
            d.rectangle([x, y + BAR, x + W, y + BAR + H], fill=(40, 20, 20))
            d.text((x + 12, y + BAR + 12), "missing: " + rel, fill=(230, 160, 160), font=small)
        d.text((x + 4, y + 6), f"{tag}  ·  {cap}", fill=(235, 235, 235) if j else (170, 170, 170), font=font)
sheet.save(out, quality=88)
print("saved", out, sheet.size)
