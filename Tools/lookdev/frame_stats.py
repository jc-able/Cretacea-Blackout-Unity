#!/usr/bin/env python3
"""Per-frame numbers the reviews use: clipped %, near-black %, mean luma, red/blue balance of mid-tones.
   frame_stats.py file.png [file.png ...]"""
import sys
import numpy as np
from PIL import Image
for f in sys.argv[1:]:
    im = np.asarray(Image.open(f).convert("RGB")).astype(np.float32)
    lum = 0.2126 * im[..., 0] + 0.7152 * im[..., 1] + 0.0722 * im[..., 2]
    clip = 100.0 * (im.max(-1) >= 250).mean()
    black = 100.0 * (lum < 8).mean()
    mid = (lum > 40) & (lum < 200)
    rb = (im[..., 0][mid].mean() / max(1.0, im[..., 2][mid].mean())) if mid.any() else 0.0
    dark = lum < 40
    rb_dark = (im[..., 0][dark].mean() / max(1.0, im[..., 2][dark].mean())) if dark.any() else 0.0
    print(f"{f.split('/')[-1][:36]:36s} mean {lum.mean():5.1f}  median {np.median(lum):5.1f}  clipped {clip:4.1f}%  <8/255 {black:4.1f}%  R/B mid {rb:4.2f} dark {rb_dark:4.2f}")
