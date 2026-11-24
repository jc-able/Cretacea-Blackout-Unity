#!/usr/bin/env python3
"""Clothing recolours for the opening crowd (review 2 fix round): per visitor model, a few albedo copies where only the
clothing pixels change colour (keyed by hue/saturation/value so skin, hair, shoes and the khaki shorts stay as they are).
Writes Assets/Prefabs/Characters/Resources/VisitorVariants/T_visitor_<model>_v<n>.jpg (1024 px).
Run: python3 Tools/agent/visitor_variants.py   (then Tools/agent/visitor_variants_mats.cs builds the materials)."""
import colorsys, os, sys
import numpy as np
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets/Prefabs/Characters/Resources/VisitorVariants")
SIZE = 1024


def hsv(img):
    a = img.astype(np.float32) / 255.0
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(-1); mn = a.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-5
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6
    h[gm] = ((b - r)[gm] / d[gm]) + 2
    h[bm] = ((r - g)[bm] / d[bm]) + 4
    h = h * 60.0
    s = np.where(mx > 1e-5, d / np.maximum(mx, 1e-5), 0)
    return h, s, mx


def soft(x, lo, hi):
    return np.clip((x - lo) / max(hi - lo, 1e-5), 0, 1)


def hue_dist(h, c):
    d = np.abs(h - c) % 360
    return np.minimum(d, 360 - d)


def shift_hue(img, mask, dh, sat_mul=1.0, val_mul=1.0):
    a = img.astype(np.float32) / 255.0
    h, s, v = hsv(img)
    h2 = (h + dh) % 360
    s2 = np.clip(s * sat_mul, 0, 1); v2 = np.clip(v * val_mul, 0, 1)
    c = v2 * s2; x = c * (1 - np.abs((h2 / 60.0) % 2 - 1)); m = v2 - c
    k = (h2 // 60).astype(int) % 6
    rgb = np.zeros_like(a)
    for i, (rr, gg, bb) in enumerate([(c, x, 0), (x, c, 0), (0, c, x), (0, x, c), (x, 0, c), (c, 0, x)]):
        sel = k == i
        for ch, val in enumerate((rr, gg, bb)):
            rgb[..., ch][sel] = (val[sel] if isinstance(val, np.ndarray) else val) + m[sel]
    out = a * (1 - mask[..., None]) + rgb * mask[..., None]
    return (np.clip(out, 0, 1) * 255).astype(np.uint8)


def tint(img, mask, color):
    """Multiply light, unsaturated cloth by a colour (white dress -> pastel, grey hoodie -> coloured)."""
    a = img.astype(np.float32) / 255.0
    t = np.array(color, np.float32)
    out = a * (1 - mask[..., None]) + (a * t) * mask[..., None]
    return (np.clip(out, 0, 1) * 255).astype(np.uint8)


def main():
    os.makedirs(OUT, exist_ok=True)
    src = lambda m: os.path.join(ROOT, f"Assets/Art/Characters/visitor_{m}/Textures/visitor_{m}_BaseColor.png")
    made = []
    for model in ("man", "woman", "teen", "child"):
        img = np.array(Image.open(src(model)).convert("RGB").resize((SIZE, SIZE), Image.LANCZOS))
        h, s, v = hsv(img)
        variants = []
        if model == "man":      # royal-blue polo and cap -> other polo colours
            mask = soft(s, 0.35, 0.5) * (1 - soft(hue_dist(h, 218), 22, 34)) * soft(v, 0.18, 0.3)
            variants = [shift_hue(img, mask, 150, 0.95, 0.95),   # brick red
                        shift_hue(img, mask, -95, 0.5, 0.7),     # forest green
                        shift_hue(img, mask, 185, 0.6, 1.05)]    # mustard
        elif model == "woman":  # white/cream dress -> pastel and mid tones
            mask = (1 - soft(s, 0.14, 0.24)) * soft(v, 0.5, 0.62)
            variants = [tint(img, mask, (0.62, 0.78, 0.95)),     # sky blue
                        tint(img, mask, (0.80, 0.68, 0.90)),     # lilac
                        tint(img, mask, (0.66, 0.80, 0.60))]     # sage
        elif model == "teen":   # light-grey hoodie -> coloured hoodie
            mask = (1 - soft(s, 0.10, 0.2)) * soft(v, 0.45, 0.58)
            variants = [tint(img, mask, (0.62, 0.22, 0.24)),     # maroon
                        tint(img, mask, (0.28, 0.50, 0.36)),     # bottle green
                        tint(img, mask, (0.95, 0.74, 0.30))]     # mustard
        elif model == "child":  # red jacket -> yellow / green / purple
            mask = soft(s, 0.5, 0.65) * (1 - soft(hue_dist(h, 358), 14, 24)) * soft(v, 0.2, 0.32)
            variants = [shift_hue(img, mask, 48, 0.95, 1.08),    # yellow
                        shift_hue(img, mask, 130, 0.55, 0.75),   # green
                        shift_hue(img, mask, 275, 0.55, 0.8)]    # purple
        print(model, "cloth fraction %.2f" % float(mask.mean()))
        for i, im in enumerate(variants):
            p = os.path.join(OUT, f"T_visitor_{model}_v{i + 1}.jpg")
            Image.fromarray(im).save(p, quality=92)
            made.append(p)
    # contact sheet for review
    sheet = Image.new("RGB", (4 * 256, 4 * 256))
    for r, model in enumerate(("man", "woman", "teen", "child")):
        sheet.paste(Image.open(src(model)).convert("RGB").resize((256, 256)), (0, r * 256))
        for i in range(3):
            sheet.paste(Image.open(os.path.join(OUT, f"T_visitor_{model}_v{i + 1}.jpg")).resize((256, 256)), ((i + 1) * 256, r * 256))
    sheet.save(os.path.join(ROOT, "Tools/agent/scratch/variants_sheet.png"))
    print("\n".join(made))


if __name__ == "__main__":
    main()
