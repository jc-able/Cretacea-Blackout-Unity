"""Back-lit E-0 header artwork: solid white letters on an escape-green field, made for emission (no alpha holes).
    python3 Tools/agent/make_e0_lightbox.py  ->  Assets/Art/UI/Signs/e0_exit_lightbox.png (2048 x 352, opaque)
Face size on the gate is 4.2 x 0.72 m, so one letter stroke (~26 px) is ~5 cm: crisp at 20 m with trilinear mips."""
import os, random
from PIL import Image, ImageDraw, ImageFont, ImageFilter

W, H = 2048, 352
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "..", "Assets", "Art", "UI", "Signs", "e0_exit_lightbox.png")
din = "/System/Library/Fonts/Supplemental/DIN Alternate Bold.ttf"

field = (26, 142, 78)          # sRGB escape green, not a primary (linear ~0.010/0.27/0.08)
white = (236, 242, 236)
img = Image.new("RGB", (W, H), field)
d = ImageDraw.Draw(img)

# subtle diffuser falloff: brighter centre, darker corners (a light box with two tubes)
grad = Image.new("L", (W, H), 0)
gd = ImageDraw.Draw(grad)
for i in range(60):
    t = i / 59.0
    gd.rounded_rectangle([int(t * 60), int(t * 40), W - int(t * 60), H - int(t * 40)], radius=40, fill=int(150 + 105 * t))
grad = grad.filter(ImageFilter.GaussianBlur(30))
dark = Image.new("RGB", (W, H), (14, 86, 46))
img = Image.composite(img, dark, grad)
d = ImageDraw.Draw(img)

# inner white keyline
d.rounded_rectangle([18, 18, W - 19, H - 19], radius=26, outline=white, width=10)

# E-0 gate plate on the left: white block, green letters
px0, py0, px1, py1 = 46, 46, 520, H - 47
d.rounded_rectangle([px0, py0, px1, py1], radius=18, outline=white, width=12)   # outline only: a big white block blooms
f_big = ImageFont.truetype(din, 236)
tw = d.textlength("E-0", font=f_big)
bb = d.textbbox((0, 0), "E-0", font=f_big)
d.text(((px0 + px1) / 2 - tw / 2, (py0 + py1) / 2 - (bb[1] + bb[3]) / 2), "E-0", font=f_big, fill=white)

# SERVICE EXIT
f_txt = ImageFont.truetype(din, 178)
txt = "SERVICE EXIT"
bb = d.textbbox((0, 0), txt, font=f_txt)
tx0 = 590
d.text((tx0, H / 2 - (bb[1] + bb[3]) / 2), txt, font=f_txt, fill=white)

# straight-ahead arrow (through the gate) on the right
ax = W - 170
ay0, ay1 = 70, H - 70
shaft = 34
d.rectangle([ax - shaft, ay0 + 96, ax + shaft, ay1], fill=white)
d.polygon([(ax, ay0), (ax - 104, ay0 + 120), (ax + 104, ay0 + 120)], fill=white)

# a little dust on the diffuser so it is not a flat vector panel
rnd = random.Random(7)
dust = Image.new("L", (W, H), 0)
dd = ImageDraw.Draw(dust)
for _ in range(900):
    x, y = rnd.randrange(W), rnd.randrange(H)
    r = rnd.choice([1, 1, 2, 3])
    dd.ellipse([x - r, y - r, x + r, y + r], fill=rnd.randrange(20, 70))
for _ in range(14):   # drip streaks from the top edge
    x = rnd.randrange(40, W - 40)
    dd.line([(x, 20), (x + rnd.randrange(-6, 6), 20 + rnd.randrange(30, 110))], fill=rnd.randrange(30, 60), width=rnd.choice([3, 4, 6]))
dust = dust.filter(ImageFilter.GaussianBlur(1.2))
img = Image.composite(Image.new("RGB", (W, H), (30, 40, 30)), img, dust)

img.save(out)
print(out, img.size)
