"""Builds Assets/Resources/UI/Keys/key_atlas.png + key_atlas.json (inline keycap sprites for the UI).
Run from the project root: python3 docs/handin/ui_tools/make_key_atlas.py
Every key is drawn here with one 40 px legend (round 4: E, F, C and Shift no longer come from the old
Assets/Art/UI/HUD/key_*.png art, whose legends were smaller; WASD, MOUSE, CTRL and the sticks joined the atlas so the
controls page has one keycap style) (grey 2 px border, dark slate face, darker base step, white Barlow Condensed Bold legend).
Gamepad face buttons (A B X Y) are round; LB/RB/View/Menu are caps like the keyboard keys."""
import json, os
from PIL import Image, ImageDraw, ImageFont

ROOT = os.getcwd()
OUT = os.path.join(ROOT, "Assets/Resources/UI/Keys")
FONT = os.path.join(ROOT, "Assets/Art/UI/Fonts/BarlowCondensed/BarlowCondensed-Bold.ttf")
ART = os.path.join(ROOT, "Assets/Art/UI/HUD")
H = 64            # key height in the atlas (same as the project art)
SS = 4            # supersampling
RIM, BASE, BORDER, FACE = (14, 16, 18, 235), (60, 63, 67, 254), (103, 106, 109, 254), (63, 68, 73, 255)

def font(px):
    return ImageFont.truetype(FONT, px * SS)

def text_w(label, px, spacing):
    f = font(px)
    return sum(f.getlength(ch) for ch in label) / SS + spacing * (len(label) - 1)

def draw_label(d, cx, cy, label, px, spacing):
    f = font(px)
    total = sum(f.getlength(ch) for ch in label) + spacing * SS * (len(label) - 1)
    x = cx * SS - total / 2
    for ch in label:
        d.text((x, cy * SS), ch, font=f, fill=(255, 255, 255, 255), anchor="lm")
        x += f.getlength(ch) + spacing * SS

def cap(label, min_w=None, px=None):
    px = px or 40          # one legend size for every key: multi-letter caps grow wider, never smaller type
    spacing = 0 if len(label) == 1 else 1.0
    w = max(min_w or H, int(round(text_w(label, px, spacing) + 24)))
    im = Image.new("RGBA", (w * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = SS
    d.rounded_rectangle((0, 2 * s, w * s - 1, 62 * s), radius=10 * s, fill=RIM)
    d.rounded_rectangle((1 * s, 3 * s, w * s - 1 - 1 * s, 58 * s), radius=9 * s, fill=BASE)
    d.rounded_rectangle((1 * s, 3 * s, w * s - 1 - 1 * s, 52 * s), radius=9 * s, fill=BORDER)
    d.rounded_rectangle((4 * s, 6 * s, w * s - 1 - 4 * s, 49 * s), radius=7 * s, fill=FACE)
    draw_label(d, w / 2, 28, label, px, spacing)
    return im.resize((w, H), Image.LANCZOS)

def round_button(label):
    w = H
    im = Image.new("RGBA", (w * SS, H * SS), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = SS
    d.ellipse((2 * s, 2 * s, 62 * s, 62 * s), fill=RIM)
    d.ellipse((3 * s, 3 * s, 61 * s, 59 * s), fill=BORDER)
    d.ellipse((6 * s, 6 * s, 58 * s, 56 * s), fill=FACE)
    draw_label(d, w / 2, 31, label, 36, 0)
    return im.resize((w, H), Image.LANCZOS)

keys = {}
for name, label in (("e", "E"), ("f", "F"), ("c", "C"), ("shift", "SHIFT"), ("ctrl", "CTRL"), ("wasd", "WASD"),
                    ("mouse", "MOUSE"), ("l stick", "L STICK"), ("r stick", "R STICK"), ("tab", "TAB"), ("esc", "ESC"), ("enter", "ENTER"), ("space", "SPACE"),
                    ("lb", "LB"), ("rb", "RB"), ("l3", "L3"), ("view", "VIEW"), ("menu", "MENU"),
                    ("q", "Q"), ("r", "R"), ("m", "M"), ("j", "J"), ("p", "P")):
    keys[name] = cap(label, min_w=96 if name == "space" else None)
for name in ("a", "b", "x", "y"):
    keys["pad_" + name] = round_button(name.upper())

# Shelf-pack into a 1024-wide atlas, 2 px gutter (transparent), rows of 64 + 8.
W = 1024
x, y, rects = 4, 4, []
for name, im in keys.items():
    if x + im.width + 4 > W:
        x, y = 4, y + H + 8
    rects.append((name, x, y, im))
    x += im.width + 8
atlas_h = 1
while atlas_h < y + H + 4:
    atlas_h *= 2
atlas = Image.new("RGBA", (W, atlas_h), (0, 0, 0, 0))
out = []
for name, rx, ry, im in rects:
    atlas.alpha_composite(im, (rx, ry))
    out.append({"name": name, "x": rx, "y": atlas_h - ry - im.height, "w": im.width, "h": im.height})
os.makedirs(OUT, exist_ok=True)
atlas.save(os.path.join(OUT, "key_atlas.png"))
with open(os.path.join(OUT, "key_atlas.json"), "w") as fh:
    json.dump({"height": H, "keys": out}, fh, indent=1)
print(W, atlas_h, len(out), "keys")
