"""Pipeline: (1) unity command script eval --file Tools~/dumpmesh.cs  (2) python3 uvmaps.py <Species...>  (3) python3 hide.py <Species...>  (4) python3 detail.py  (5) cretacea build_dino_prefabs.
Derive hide maps for the dinosaurs: MaskMap (R metal 0, G cavity AO, B detail mask, A smoothness), a
subsurface mask (skin 1, eyes/teeth/horn/claw 0), an eyeshine mask (pupils only) and a night-safe albedo (linear
luminance soft-capped near 0.25; the bible's night albedos are 0.05-0.30, the raptor's cream chest was 0.43) from
the source albedo, normal and roughness maps plus the mesh (UV-space position/bone rasters from uvmaps.py).
Eyes are located PER SIDE (the meshes are not mirror-exact: the raptor's -X iris sits 4 cm from the mirrored +X
centre, the brachiosaur's head is 12 cm off-axis). Output 2048 px PNGs next to the source textures."""
import os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))  # project root
import numpy as np, sys, json
from PIL import Image
from imglib import box, gauss, dilate
A = ROOT + '/Assets/Art/Dinosaurs/'
W = ROOT + '/Screenshots/_work/'
folders = {'Velociraptor':'raptor','Tyrannosaurus':'trex','Triceratops':'triceratops','Brachiosaurus':'sauropod'}
# eye centres (mesh space, +X side) located from the head side-view sheet; radius in metres. The -X eye starts from
# the mirrored centre, is projected onto the -X head surface and refined by the iris colour rule (IRIS) when it finds one.
EYES = {'Velociraptor': ((0.078,-1.351,1.918), 0.022), 'Tyrannosaurus': ((0.302,-4.266,4.946), 0.07),
        'Triceratops': ((0.333,-2.859,2.348), 0.056), 'Brachiosaurus': ((0.349,-9.127,15.857), 0.05)}
# iris colour rule per species (albedo 0..1 sRGB): raptor yellow iris, brachiosaur black eye; None = keep the guess
IRIS = {'Velociraptor': lambda al, lum: (al[..., 0] > 0.5) & (al[..., 1] > 0.4) & (al[..., 2] < 0.3),
        'Brachiosaurus': lambda al, lum: lum < 0.02,
        'Tyrannosaurus': None, 'Triceratops': None}
R = 2048
def srgb_to_lin(c): return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
def lin_to_srgb(c): return np.where(c <= 0.0031308, c * 12.92, 1.055 * np.power(np.maximum(c, 0), 1 / 2.4) - 0.055)
def find_eye(pos, headjaw, al, lum, sp, ec, er, sx):
    """Centre of the eye on side sx (+1/-1), mesh space."""
    c = np.array([ec[0] * sx, ec[1], ec[2]], np.float32)
    side = headjaw & (np.sign(pos[..., 0]) == sx)
    rule = IRIS.get(sp)
    if rule is not None:   # 1. the iris colour near the mirrored guess
        m = side & rule(al, lum) & (np.linalg.norm(pos - c, axis=2) < 4.0 * er)
        if m.sum() >= 8:
            P = pos[m]; cc = np.median(P, 0)
            for _ in range(4):   # densest blob around the median
                k = np.linalg.norm(P - cc, axis=1) < 1.2 * er
                if k.sum() >= 4: cc = P[k].mean(0)
            return cc.astype(np.float32)
    if not (side & (np.linalg.norm(pos - c, axis=2) < er)).any():
        # 2. mirrored guess is off the surface: the outermost head texel at the same height/length (y, z)
        yz = np.hypot(pos[..., 1] - c[1], pos[..., 2] - c[2])
        ring = side & (yz < er)
        if ring.any():
            P = pos[ring]
            c = P[np.argmax(P[:, 0] * sx)].astype(np.float32)
    return c
def load_img(p, mode, size=R):
    return np.asarray(Image.open(p).convert(mode).resize((size, size), Image.LANCZOS)).astype(np.float32) / 255.0
def up(a, size=R):  # nearest upsample of 1024 rasters
    f = size // a.shape[0]
    return np.repeat(np.repeat(a, f, 0), f, 1)
def build(sp, eye_override=None):
    tex = A + folders[sp] + '/Textures/' + sp
    al = load_img(tex + '_Albedo.png', 'RGB'); nm = load_img(tex + '_Normal.png', 'RGB'); rough = load_img(tex + '_Roughness.png', 'L')
    d = np.load(W + f'mesh/{sp}_uv.npz'); pos = up(d['pos']); bone = up(d['bone']); bones = list(d['bones'])
    cover = bone >= 0
    lum = al @ np.array([0.2126, 0.7152, 0.0722], np.float32); chroma = al.max(2) - al.min(2)
    # --- curvature from the OpenGL normal map (divergence), multi-scale ---
    nx = nm[..., 0] * 2 - 1; ny = nm[..., 1] * 2 - 1
    def curv(r):
        bx, by = (gauss(nx, r), gauss(ny, r)) if r > 0 else (nx, ny)
        dx = np.zeros_like(bx); dy = np.zeros_like(by)
        dx[:, 1:-1] = (bx[:, 2:] - bx[:, :-2]) * 0.5
        dy[1:-1, :] = -(by[2:, :] - by[:-2, :]) * 0.5   # image rows go down, OpenGL green goes up
        return dx + dy
    c_fine, c_mid, c_big = curv(0), curv(3), curv(10)
    def norm(c):
        s = np.percentile(np.abs(c[cover]), 95) + 1e-6
        return np.clip(c / s, -1.5, 1.5)
    c_fine, c_mid, c_big = norm(c_fine), norm(c_mid), norm(c_big)
    # seams: kill curvature spikes near chart borders
    inner = ~dilate(~cover, 3)
    c_fine *= inner; c_mid *= inner; c_big *= inner
    cav = np.clip(-(0.45 * c_fine + 0.35 * c_mid + 0.20 * c_big), 0, 1.5)
    ridge = np.clip(0.6 * c_fine + 0.4 * c_mid, 0, 1.5)
    # large-scale occlusion: darker albedo valleys relative to their neighbourhood
    lum_lo = gauss(lum, 24); valley = np.clip((lum_lo - lum) / (lum_lo + 0.05), 0, 1)
    ao = 1.0 - 0.34 * cav - 0.18 * np.clip(-c_big, 0, 1.5) - 0.22 * valley
    ao = np.clip(ao, 0.32, 1.0)
    # --- regions ---
    bi = {b: i for i, b in enumerate(bones)}
    def on(*names): return np.isin(bone, [bi[n] for n in names if n in bi])
    headjaw = on('Head', 'Jaw')
    extremity = on('Toe_L', 'Toe_R', 'Hand_L', 'Hand_R', 'LeftFoot', 'RightFoot', 'LeftFrontFoot', 'RightFrontFoot')
    lum_loc = gauss(lum, 16)
    keratin_hj = headjaw & (((lum > np.maximum(0.36, lum_loc * 1.35)) & (chroma < 0.32)) | ((lum > 0.46) & (chroma < 0.38)))
    claws = extremity & (((lum > np.maximum(0.34, lum_loc * 1.4)) & (chroma < 0.3)) | (lum < lum_loc * 0.45))
    keratin = keratin_hj | claws
    eyes = np.zeros(bone.shape, np.float32); pupil = np.zeros(bone.shape, np.float32)
    ec, er = eye_override or EYES[sp]
    centres = []
    if ec is not None:
        for sx in (1, -1):
            c = find_eye(pos, headjaw, al, lum, sp, ec, er, sx); centres.append(c)
            dist = np.linalg.norm(pos - c, axis=2)
            eyes = np.maximum(eyes, np.clip((er * 0.9 - dist) / (er * 0.2), 0, 1) * headjaw)    # smooth wet eyeball, inside 0.7-0.9 er
            pupil = np.maximum(pupil, np.clip((er * 0.45 - dist) / (er * 0.15), 0, 1) * headjaw)  # eyeshine: pupil only
    ker = gauss(keratin.astype(np.float32), 1.5) * (1 - eyes)
    # --- smoothness ---
    s_orig = 1 - rough; s_dev = s_orig - np.median(s_orig[cover])
    s = 0.30 + 0.22 * np.clip(cav, 0, 1) + 0.08 * np.clip(ridge, 0, 1) + 0.35 * s_dev - 0.06 * np.clip(lum - 0.35, 0, 1)
    s = np.clip(s, 0.16, 0.62)
    s = s * (1 - ker) + (0.50 + 0.10 * np.clip(lum - 0.4, 0, 1)) * ker
    s = s * (1 - eyes) + 0.93 * eyes
    ao = np.maximum(ao, 0.85 * ker)          # teeth/claws: no cavity darkening between them (read as bone, not black)
    ao = ao * (1 - eyes) + 1.0 * eyes
    detail = np.clip(1 - ker - eyes, 0, 1)
    sss = np.clip(1 - 1.15 * ker - eyes, 0, 1)
    out = np.stack([np.zeros_like(ao), ao, detail, s], -1)
    Image.fromarray((out * 255 + 0.5).astype(np.uint8), 'RGBA').save(tex + '_Hide_MaskMap.png', optimize=True)
    Image.fromarray((sss * 255 + 0.5).astype(np.uint8), 'L').save(tex + '_SSS_Mask.png', optimize=True)
    shine = pupil ** 1.5                               # retro-reflective eyeshine footprint: a small point per eye
    # night-safe albedo: soft cap of linear luminance near 0.25 (keratin and eyes keep a little more, max ~0.35)
    lin = srgb_to_lin(al); L = lin @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    knee, room = 0.18, 0.07
    capped = np.where(L > knee, (knee + room * np.tanh((L - knee) / room)) / np.maximum(L, 1e-6), 1.0)
    kroom = 0.17; kcap = np.where(L > knee, (knee + kroom * np.tanh((L - knee) / kroom)) / np.maximum(L, 1e-6), 1.0)
    keep = np.clip(ker + eyes, 0, 1)
    scale_ = capped * (1 - keep) + kcap * keep
    al_out = lin_to_srgb(lin * scale_[..., None])
    Image.fromarray((np.clip(al_out, 0, 1) * 255 + 0.5).astype(np.uint8), 'RGB').save(tex + '_Hide_Albedo.png', optimize=True)
    Lout = srgb_to_lin(al_out) @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    Image.fromarray((np.repeat(shine[..., None], 3, -1) * 255 + 0.5).astype(np.uint8), 'RGB').save(tex + '_EyeShine_Emissive.png', optimize=True)
    st = dict(species=sp, ao_mean=float(ao[cover].mean()), ao_p5=float(np.percentile(ao[cover], 5)),
              s_mean=float(s[cover].mean()), s_p5=float(np.percentile(s[cover], 5)), s_p95=float(np.percentile(s[cover], 95)),
              keratin_pct=float(ker[cover].mean() * 100), sss_mean=float(sss[cover].mean()),
              eye_px_pos=int(((eyes > 0.5) & (pos[..., 0] > 0)).sum()), eye_px_neg=int(((eyes > 0.5) & (pos[..., 0] < 0)).sum()),
              pupil_px_pos=int(((pupil > 0.5) & (pos[..., 0] > 0)).sum()), pupil_px_neg=int(((pupil > 0.5) & (pos[..., 0] < 0)).sum()),
              eye_centres=[[round(float(x), 3) for x in c] for c in centres],
              albedo_lin_p99_before=float(np.percentile(L[cover], 99)), albedo_lin_p99_after=float(np.percentile(Lout[cover], 99)))
    print(json.dumps({k: (round(v, 3) if isinstance(v, float) else v) for k, v in st.items()}))
if __name__ == '__main__':
    for sp in sys.argv[1:]: build(sp)
