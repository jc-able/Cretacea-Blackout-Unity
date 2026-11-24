#!/usr/bin/env python3
"""Mean sRGB of named rectangles in a 1280x720 capture:  region_stats.py file.png name=x0,y0,x1,y1 [...]"""
import sys
import numpy as np
from PIL import Image
im = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(np.float32)
out = []
for spec in sys.argv[2:]:
    name, box = spec.split("=")
    x0, y0, x1, y1 = [int(v) for v in box.split(",")]
    r = im[y0:y1, x0:x1].reshape(-1, 3)
    m = r.mean(0)
    lum = 0.2126 * r[:, 0] + 0.7152 * r[:, 1] + 0.0722 * r[:, 2]
    out.append(f"{name} {m[0]:.0f}/{m[1]:.0f}/{m[2]:.0f} (luma {lum.mean():.0f}, p99 {np.percentile(lum, 99):.0f})")
print(sys.argv[1].split("/")[-1][:30].ljust(30), " | ".join(out))
