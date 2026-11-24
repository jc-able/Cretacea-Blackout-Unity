"""Tileable reptile-scale detail map for HDRP (R: albedo 0.5 neutral, G: normal Y, B: smoothness, A: normal X)."""
import os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))  # project root
import numpy as np
from PIL import Image
rng = np.random.default_rng(7)
N = 512; cells = 22
pts = rng.random((cells * cells, 2))
# jittered grid for even scales
g = np.stack(np.meshgrid(np.arange(cells), np.arange(cells)), -1).reshape(-1, 2)
pts = (g + 0.15 + 0.7 * rng.random((cells * cells, 2))) / cells
yy, xx = np.mgrid[0:N, 0:N] / N
d1 = np.full((N, N), 9.0); d2 = np.full((N, N), 9.0)
for p in pts:
    dx = np.abs(xx - p[0]); dx = np.minimum(dx, 1 - dx)
    dy = np.abs(yy - p[1]); dy = np.minimum(dy, 1 - dy)
    dd = np.sqrt(dx * dx + dy * dy)
    m = dd < d1
    d2 = np.where(m, d1, np.minimum(d2, dd)); d1 = np.where(m, dd, d1)
edge = (d2 - d1) * cells          # 0 at cell borders
h = np.sqrt(np.clip(edge / 0.55, 0, 1))   # domed scales
h = h + 0.08 * rng.standard_normal((N, N)) * 0.2
gx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * N / cells * 0.9
gy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * N / cells * 0.9
n = np.stack([-gx, gy, np.ones_like(h)], -1); n /= np.linalg.norm(n, axis=-1, keepdims=True)
alb = 0.5 + 0.06 * (h - 0.5)            # grooves a touch darker
smooth = 0.5 - 0.10 * (h - 0.6) + 0.0   # grooves (low h) slightly smoother/wetter
out = np.stack([alb, n[..., 1] * 0.5 + 0.5, np.clip(smooth, 0, 1), n[..., 0] * 0.5 + 0.5], -1)
Image.fromarray((np.clip(out, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGBA').save(
    ROOT + '/Assets/Art/Materials/Dinosaurs/T_DinoScaleDetail_mask.png')
print('detail ok', h.min(), h.max())
