#!/usr/bin/env python3
"""Derive the jungle terrain texture set (Assets/Art/Textures/terrain_jungle) from sets already in the project.

No downloads: every output is a recolour / repack of an existing Poly Haven set in Assets/Art/Textures.
  * <layer>_BaseColor.png  only for recoloured layers (grass -> tropical green, moss, basalt, sand)
  * <layer>_MaskMap.png    for every layer: R metallic 0, G AO (from the set's DRY mask), B height (the set's
                           _Height.png, needed by TerrainLit height blending), A smoothness (dry mask, scaled)
Normals are not copied: the terrain layers bind the source set's _Normal.png directly.
Run from the project root:  python3 "Assets/Art/Terrain/Source~/derive_terrain_textures.py"
"""
import os
import numpy as np
from PIL import Image

SRC = "Assets/Art/Textures"
OUT = "Assets/Art/Textures/terrain_jungle"


def load_rgb(path):
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1); mn = rgb.min(-1); d = mx - mn
    h = np.zeros_like(mx)
    m = d > 1e-6
    rm = m & (mx == r); gm = m & (mx == g) & ~rm; bm = m & ~rm & ~gm
    h[rm] = ((g - b)[rm] / d[rm]) % 6.0
    h[gm] = ((b - r)[gm] / d[gm]) + 2.0
    h[bm] = ((r - g)[bm] / d[bm]) + 4.0
    h = h / 6.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0.0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    h6 = (h % 1.0) * 6.0
    i = np.floor(h6).astype(np.int32) % 6
    f = h6 - np.floor(h6)
    p = v * (1 - s); q = v * (1 - s * f); t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,), np.float32)
    for k, (a, b, c) in enumerate([(v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q)]):
        sel = i == k
        out[sel, 0] = a[sel]; out[sel, 1] = b[sel]; out[sel, 2] = c[sel]
    return out


def flatten(rgb, sigma_frac=0.12, keep=0.25):
    """Remove the low-frequency light/colour drift across a tiling texture (the checkerboard seen from the air):
    divide by a wrap-around Gaussian blur, keep a little of it, renormalise to the original mean."""
    from PIL import ImageFilter
    h, w, _ = rgb.shape
    pad = h // 4
    padded = np.pad(rgb, ((pad, pad), (pad, pad), (0, 0)), mode="wrap")
    img = Image.fromarray((np.clip(padded, 0, 1) * 255).astype(np.uint8), "RGB")
    blur = np.asarray(img.filter(ImageFilter.GaussianBlur(radius=h * sigma_frac)), dtype=np.float32)[pad:pad + h, pad:pad + w] / 255.0
    mean = rgb.reshape(-1, 3).mean(0)
    ratio = mean / np.maximum(blur, 1e-3)
    out = rgb * (ratio * (1 - keep) + keep)
    return np.clip(out, 0, 1)


def save_rgb(arr, path):
    Image.fromarray((np.clip(arr, 0, 1) * 255 + 0.5).astype(np.uint8), "RGB").save(path, optimize=True)


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1)
    return t * t * (3 - 2 * t)


def recolour_grass(rgb):
    """Straw/leaf-litter grass -> wet tropical green; keeps the original hue spread and a few brown leaves."""
    h, s, v = rgb_to_hsv(rgb)
    deg = h * 360.0
    # how "straw" a pixel is: hues 15..60 deg move fully, already-green hues (> 75) move a little
    move = 1.0 - smoothstep(55.0, 85.0, deg)
    # keep ~1 in 8 dark brown leaves brown (hue variation kept): darkest saturated reds stay
    keep = smoothstep(0.30, 0.18, v) * smoothstep(0.35, 0.55, s) * (deg < 40)
    shift = (58.0 * move + 14.0 * (1 - move)) * (1 - 0.85 * keep)
    deg2 = deg + shift
    s2 = np.clip(s * (1.18 + 0.15 * move), 0, 1)
    v2 = v * (0.70 - 0.08 * move)
    return hsv_to_rgb(deg2 / 360.0, s2, v2)


def recolour_moss(rgb, target_lum=0.105):
    """Orange-brown ground moss (moss_ground) -> damp green jungle moss; keeps its hue spread and detail and is
    scaled to a mean linear luminance of ~0.1 (the other ground layers sit at 0.08-0.12)."""
    h, s, v = rgb_to_hsv(rgb)
    deg = h * 360.0
    # move the set's orange/olive hues (20..70 deg) to yellow-green (~78..105 deg), keep the spread
    deg2 = 78.0 + (deg - 38.0) * 0.6
    deg2 = np.where(deg > 150.0, deg, deg2)
    out = hsv_to_rgb(deg2 / 360.0, np.clip(s * 0.72, 0, 1), v)
    lin = np.where(out <= 0.04045, out / 12.92, ((out + 0.055) / 1.055) ** 2.4)
    lum = float((lin @ np.array([0.2126, 0.7152, 0.0722], np.float32)).mean())
    k = (target_lum / max(lum, 1e-4)) ** (1 / 2.2)
    return np.clip(out * k, 0, 1)


def recolour_basalt(rgb):
    """Warm brown cliff -> dark grey-brown volcanic rock (keeps its detail)."""
    lum = rgb @ np.array([0.30, 0.55, 0.15], np.float32)
    grey = np.stack([lum * 0.98, lum * 0.97, lum * 0.95], -1)
    out = grey * 0.78 + rgb * 0.22
    return out * 0.78


def recolour_sand(rgb):
    """Brown mud -> damp volcanic beach sand: lower contrast, tan-grey."""
    lum = rgb @ np.array([0.30, 0.55, 0.15], np.float32)
    mean = lum.mean()
    lum2 = mean + (lum - mean) * 0.55
    tint = np.array([1.00, 0.90, 0.72], np.float32)
    return np.clip(lum2[..., None] * tint * (0.56 / max(mean, 1e-3)), 0, 1) * 0.92


def flatten_channel(ch, sigma_frac=0.05):
    """Divide one channel by its wrap-around blur (keeps detail, removes per-tile light/dark drift)."""
    rgb = np.repeat(ch[..., None], 3, -1)
    return flatten(rgb, sigma_frac, 0.0)[..., 0]


def write_mask(src_set, layer, smooth_scale=1.0, smooth_add=0.0, flat_ao=False):
    mask = np.asarray(Image.open(f"{SRC}/{src_set}/{src_set}_MaskMap.png").convert("RGBA"), dtype=np.float32) / 255.0
    hpath = f"{SRC}/{src_set}/{src_set}_Height.png"
    height = np.asarray(Image.open(hpath).convert("L"), dtype=np.float32) / 255.0 if os.path.exists(hpath) else np.full(mask.shape[:2], 0.5, np.float32)
    if height.shape != mask.shape[:2]:
        height = np.asarray(Image.fromarray((height * 255).astype(np.uint8)).resize(mask.shape[1::-1], Image.BILINEAR), dtype=np.float32) / 255.0
    # stretch height to 0..1 so TerrainLit height blending has contrast to work with
    lo, hi = np.percentile(height, 1), np.percentile(height, 99)
    height = np.clip((height - lo) / max(hi - lo, 1e-3), 0, 1)
    out = np.zeros_like(mask)
    out[..., 0] = 0.0
    out[..., 1] = flatten_channel(mask[..., 1]) if flat_ao else mask[..., 1]
    out[..., 2] = height
    out[..., 3] = np.clip(mask[..., 3] * smooth_scale + smooth_add, 0, 1)
    # masks at 1024: AO / height / smoothness need less resolution than colour (halves the repo footprint)
    img = Image.fromarray((out * 255 + 0.5).astype(np.uint8), "RGBA").resize((1024, 1024), Image.LANCZOS)
    img.save(f"{OUT}/{layer}_MaskMap.png", optimize=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    # every layer's colour is flattened (no low-frequency drift) so 5-22 m tiling does not show from the air
    # grass and moss cover the meadows seen from the air: also remove the mid-frequency blotches (~1/4 tile) that
    # repeat every 6-7.5 m as a light/dark grid (review 1 "checkerboard lawn"); the splat painter adds the variation
    save_rgb(flatten(flatten(recolour_grass(load_rgb(f"{SRC}/grass_ground/grass_ground_BaseColor.png"))), 0.03, 0.2), f"{OUT}/tj_grass_BaseColor.png")
    save_rgb(flatten(flatten(recolour_moss(load_rgb(f"{SRC}/moss_ground/moss_ground_BaseColor.png"))), 0.03, 0.2), f"{OUT}/tj_moss_BaseColor.png")
    save_rgb(flatten(recolour_basalt(load_rgb(f"{SRC}/rock_cliff/rock_cliff_BaseColor.png")), 0.12, 0.4), f"{OUT}/tj_basalt_BaseColor.png")
    save_rgb(flatten(recolour_sand(load_rgb(f"{SRC}/mud/mud_BaseColor.png"))), f"{OUT}/tj_sand_BaseColor.png")
    save_rgb(flatten(load_rgb(f"{SRC}/mud/mud_BaseColor.png")), f"{OUT}/tj_mud_BaseColor.png")
    save_rgb(flatten(load_rgb(f"{SRC}/forest_floor/forest_floor_BaseColor.png")), f"{OUT}/tj_floor_BaseColor.png")
    save_rgb(flatten(load_rgb(f"{SRC}/gravel_road/gravel_road_BaseColor.png")), f"{OUT}/tj_gravel_BaseColor.png")
    save_rgb(flatten(load_rgb(f"{SRC}/rock_mossy/rock_mossy_BaseColor.png"), 0.12, 0.4), f"{OUT}/tj_mossyrock_BaseColor.png")
    # masks for all eight layers (layer name -> source set)
    write_mask("mud", "tj_mud", 1.0, 0.05)
    write_mask("grass_ground", "tj_grass", 0.9, 0.05, flat_ao=True)
    write_mask("gravel_road", "tj_gravel", 1.0, 0.05)
    write_mask("rock_cliff", "tj_basalt", 1.2, 0.08)
    write_mask("forest_floor", "tj_floor", 1.6, 0.12)
    write_mask("rock_mossy", "tj_mossyrock", 1.1, 0.05)
    write_mask("moss_ground", "tj_moss", 1.4, 0.08, flat_ao=True)
    write_mask("mud", "tj_sand", 0.55, 0.0)
    print("wrote", sorted(os.listdir(OUT)))


if __name__ == "__main__":
    main()
