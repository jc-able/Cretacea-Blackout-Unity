#!/usr/bin/env python3
"""
Review 2 (daylight): every plant atlas is yellow-olive (leaves hue 55-60 deg, red = green), so the island read khaki.
The tropical plants use ONE solid atlas for bark and leaves, so a material tint would turn the bark green too.
This writes a copy of each atlas with only the leaf texels shifted to a tropical green (hue ~95-108 deg):
leaf texels are picked by colour (hue 32-95 deg and saturation above ~0.2); bark (brown, grey, pale) is untouched.

    python3 green_leaf_atlases.py <project folder>

Output: Assets/Prefabs/Foliage/Textures/<stem>_BaseColor_Leaf.png (2048 px, what the importer used anyway).
LookVegetationPrefabs binds them to green copies of the plant materials.
"""
import os
import sys

import numpy as np
from PIL import Image

STEMS = {
    # stem: (target hue deg, pull 0..1, value gain)
    # (fix round: targets 100-108 deg; 94-104 left the plants at hue ~71 after the mip/light mix)
    "jungle_tree": (106.0, 0.88, 1.04),
    "palm_tree": (100.0, 0.85, 1.03),
    "palm_tree_b": (102.0, 0.85, 1.03),
    "tree_fern": (108.0, 0.85, 1.0),
    "banana_plant": (100.0, 0.85, 1.02),
    "monstera_clump": (104.0, 0.75, 1.0),
}


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def rgb_to_hsv(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(-1); mn = a.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6.0
    h[gm] = (b - r)[gm] / d[gm] + 2.0
    h[bm] = (r - g)[bm] / d[bm] + 4.0
    h = h * 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    h = (h % 360.0) / 60.0
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,), dtype=np.float32)
    for k, (R, G, B) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k
        out[sel, 0] = R[sel]; out[sel, 1] = G[sel]; out[sel, 2] = B[sel]
    return out


def main(project):
    out_dir = os.path.join(project, "Assets/Prefabs/Foliage/Textures")
    os.makedirs(out_dir, exist_ok=True)
    for stem, (target, pull, gain) in STEMS.items():
        src = os.path.join(project, f"Assets/Art/Foliage/Tropical/{stem}/Textures/{stem}_BaseColor.png")
        if not os.path.exists(src):
            print("missing", src); continue
        im = Image.open(src)
        alpha = im.getchannel("A") if im.mode == "RGBA" else None
        rgb = im.convert("RGB").resize((2048, 2048), Image.LANCZOS)
        a = np.asarray(rgb).astype(np.float32) / 255.0
        h, s, v = rgb_to_hsv(a)
        # sunlit leaf highlights are pale (saturation 0.08-0.16): bright texels get a lower saturation gate, otherwise
        # they stayed yellow (fix round: banana sunlit top 15 % measured hue 59, R = G)
        sat_gate = smooth(0.16, 0.30, s) + (smooth(0.08, 0.18, s) - smooth(0.16, 0.30, s)) * smooth(0.40, 0.50, v)
        leaf = smooth(30.0, 42.0, h) * (1.0 - smooth(110.0, 140.0, h)) * np.maximum(sat_gate, 0.0) * smooth(0.04, 0.10, v)
        nh = h + (target - h) * pull * leaf
        ns = np.clip(s * (1.0 + 0.06 * leaf), 0, 1)
        nv = np.clip(v * (1.0 + (gain - 1.0) * leaf), 0, 1)
        o = hsv_to_rgb(nh, ns, nv)
        img = Image.fromarray((np.clip(o, 0, 1) * 255.0 + 0.5).astype(np.uint8), "RGB")
        if alpha is not None:
            img.putalpha(alpha.resize((2048, 2048), Image.LANCZOS))
        dst = os.path.join(out_dir, f"{stem}_BaseColor_Leaf.png")
        img.save(dst, optimize=True)
        print(f"{stem}: leaf share {leaf.mean():.2f} -> {dst}")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.getcwd())
