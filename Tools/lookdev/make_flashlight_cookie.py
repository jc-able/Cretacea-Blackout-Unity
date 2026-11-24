#!/usr/bin/env python3
"""Flashlight cookie (Assets/Art/VFX/flashlight_cookie.png): a real torch beam profile, no hard disc.

The texture is mapped across the spot cone in tangent space (edge of the texture = outer cone angle, 38 degrees).
Profile: tight hot spot, wide soft corona, a dim spill that fades to nothing at the edge, and a little irregularity
so the beam does not look computer-perfect. Imported linear (see CretaceaPbrImporter: sRGB off, no mips, clamp).
"""
import sys
import numpy as np
from PIL import Image

N = 512
OUT = sys.argv[1] if len(sys.argv) > 1 else "Assets/Art/VFX/flashlight_cookie.png"

y, x = np.mgrid[0:N, 0:N].astype(np.float64)
cx = cy = (N - 1) / 2.0
dx, dy = (x - cx) / (N / 2.0), (y - cy) / (N / 2.0)
r = np.sqrt(dx * dx + dy * dy)
a = np.arctan2(dy, dx)

def smoothstep(e0, e1, v):
    t = np.clip((v - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)

hot = np.exp(-(r / 0.135) ** 2)                               # ~6 degree half width
corona = 0.19 * np.exp(-(r / 0.36) ** 2)                      # glow out to ~18 degrees
spill = 0.062 * np.exp(-(r / 0.52) ** 2)                      # dim field: one smooth bell, no knee, ~0 at the edge (0.046 until round 3: with the bounce cut to its real size the ground at the player's feet went black)
ring = 0.0                                                    # (a reflector ring read as a hard disc edge in corridors)

# low-frequency irregularity (reflector facets, lens dirt): +-4 %, deterministic
rng = np.random.default_rng(7)
irr = np.zeros_like(r)
for k in range(1, 6):
    irr += rng.uniform(-1, 1) * np.cos(k * a + rng.uniform(0, 6.28)) * np.exp(-((r - rng.uniform(0.2, 0.8)) / 0.3) ** 2)
irr = 1.0 + 0.04 * irr / max(1e-6, np.abs(irr).max())

# Review 3: the corona and the spill lit the ceiling over the beam as a wide fan (white tiles, grazing light). A
# torch held at the hip is shaded upward by the hand and the bezel: the soft field is 45 % weaker straight up and
# untouched level and below, so the floor at the player's feet keeps its light. Row 0 of the image is the top.
up = np.clip(-dy / np.maximum(r, 1e-6), 0.0, 1.0)           # 0 level or below, 1 straight up
shade = 1.0 - 0.45 * smoothstep(0.15, 0.85, up)
beam = (hot + (corona + spill) * shade + ring) * irr
beam *= 1.0 - smoothstep(0.955, 1.0, r)                       # guarantee zero at the border (clamp sampling)
beam /= beam.max()

# 16-bit greyscale: the spill lives at 0.5-5 % of the peak, where 8 bits give visible steps on a wall
img = (np.clip(beam, 0, 1) * 65535.0 + 0.5).astype(np.uint16)
Image.fromarray(img, "I;16").save(OUT)
for deg in (0, 5, 10, 15, 20, 25, 30, 35, 37.5):
    rr = np.tan(np.radians(deg)) / np.tan(np.radians(38.0))
    px = int(round(cx + rr * (N / 2.0)))
    print(f"{deg:5.1f} deg  r={rr:.3f}  level={beam[int(cy), min(px, N - 1)]:.3f}")
print("saved", OUT)
