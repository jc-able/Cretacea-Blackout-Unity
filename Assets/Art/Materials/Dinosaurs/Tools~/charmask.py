"""Derived look mask for the prologue people: python3 charmask.py  (then `cretacea build_character_prefabs`).
Source masks (Assets/Art/Characters/<name>/Textures/<name>_MaskMap.png) have a blank AO channel (G ~1.0), metallic
leaks up to 0.71 and one smoothness (~0.47) for skin and cloth alike, so the people read as glossy toys next to the
matte hides. Output: Assets/Art/Materials/Characters/T_<name>_Mask_Look.png (HDRP mask: R metallic 0, G cavity AO from
normal-map curvature + albedo valleys, B 0, A smoothness: skin ~0.40, cloth matte 0.12-0.30, hair 0.30)."""
import os, sys, json
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from imglib import gauss
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))
NAMES = ['park_guide', 'visitor_man', 'visitor_woman', 'visitor_teen', 'visitor_child']
R = 2048
def load(p, mode):
    return np.asarray(Image.open(p).convert(mode).resize((R, R), Image.LANCZOS)).astype(np.float32) / 255.0
for n in (sys.argv[1:] or NAMES):
    t = f'{ROOT}/Assets/Art/Characters/{n}/Textures/{n}_'
    al = load(t + 'BaseColor.png', 'RGB'); nm = load(t + 'Normal.png', 'RGB'); mk = load(t + 'MaskMap.png', 'RGBA')
    nx = nm[..., 0] * 2 - 1; ny = nm[..., 1] * 2 - 1
    def curv(r):
        bx, by = (gauss(nx, r), gauss(ny, r)) if r > 0 else (nx, ny)
        dx = np.zeros_like(bx); dy = np.zeros_like(by)
        dx[:, 1:-1] = (bx[:, 2:] - bx[:, :-2]) * 0.5
        dy[1:-1, :] = -(by[2:, :] - by[:-2, :]) * 0.5
        c = dx + dy
        return np.clip(c / (np.percentile(np.abs(c), 95) + 1e-6), -1.5, 1.5)
    cav = np.clip(-(0.5 * curv(1) + 0.3 * curv(4) + 0.2 * curv(12)), 0, 1.5)
    lum = al @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    lum_lo = gauss(lum, 24); valley = np.clip((lum_lo - lum) / (lum_lo + 0.05), 0, 1)
    ao = np.clip(1.0 - 0.38 * cav - 0.25 * valley, 0.35, 1.0)
    r, g, b = al[..., 0], al[..., 1], al[..., 2]
    chroma = al.max(2) - al.min(2)
    skin = ((r > g) & (g > b) & (r > 0.25) & (chroma > 0.06) & (chroma < 0.45) & ((r - b) > 0.08) & (r > 1.18 * g) & (r < 1.9 * g)).astype(np.float32)
    skin = gauss(skin, 2)
    hair = ((lum < 0.12) & (chroma < 0.12)).astype(np.float32) * (1 - skin)
    s_src = mk[..., 3]
    cloth = np.clip(0.12 + 0.35 * (s_src - 0.25), 0.12, 0.30)
    s = cloth * (1 - skin - hair) + 0.40 * skin + 0.30 * hair
    s = s * (0.85 + 0.15 * ao)                      # creases a touch duller
    out = np.stack([np.zeros_like(ao), ao, np.zeros_like(ao), np.clip(s, 0, 1)], -1)
    dst = f'{ROOT}/Assets/Art/Materials/Characters/T_{n}_Mask_Look.png'
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGBA').save(dst, optimize=True)
    print(json.dumps(dict(name=n, ao_mean=round(float(ao.mean()), 3), ao_p5=round(float(np.percentile(ao, 5)), 3),
                          skin_pct=round(float(skin.mean() * 100), 1), s_p5=round(float(np.percentile(s, 5)), 2),
                          s_p50=round(float(np.percentile(s, 50)), 2), s_p95=round(float(np.percentile(s, 95)), 2))))
