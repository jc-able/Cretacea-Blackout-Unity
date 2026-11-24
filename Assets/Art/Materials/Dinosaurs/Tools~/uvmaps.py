import os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))  # project root
import numpy as np, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from meshlib import load
def raster(sp, R=1024):
    v, n, uv, bw, t, bones = load(sp)
    pos = np.zeros((R, R, 3), np.float32); bone = np.full((R, R), -1, np.int16); nrm = np.zeros((R,R,3), np.float32)
    P = np.stack([uv[:,0]*R - 0.5, (1-uv[:,1])*R - 0.5], 1)
    for tri in t:
        a, b, c = P[tri]
        x0, y0 = np.floor(np.minimum(np.minimum(a, b), c)).astype(int); x1, y1 = np.ceil(np.maximum(np.maximum(a, b), c)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, R-1), min(y1, R-1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1+1), np.arange(y0, y1+1))
        d = (b[1]-c[1])*(a[0]-c[0]) + (c[0]-b[0])*(a[1]-c[1])
        if abs(d) < 1e-12: continue
        w0 = ((b[1]-c[1])*(xs-c[0]) + (c[0]-b[0])*(ys-c[1]))/d
        w1 = ((c[1]-a[1])*(xs-c[0]) + (a[0]-c[0])*(ys-c[1]))/d
        w2 = 1-w0-w1
        m = (w0 >= -0.02) & (w1 >= -0.02) & (w2 >= -0.02)
        if not m.any(): continue
        pp = w0[m,None]*v[tri[0]] + w1[m,None]*v[tri[1]] + w2[m,None]*v[tri[2]]
        nn = w0[m,None]*n[tri[0]] + w1[m,None]*n[tri[1]] + w2[m,None]*n[tri[2]]
        pos[ys[m], xs[m]] = pp; nrm[ys[m], xs[m]] = nn
        wts = [bw['w0'][tri].argmax()]
        bone[ys[m], xs[m]] = bw['i0'][tri[0]]
    return pos, bone, nrm, bones
if __name__ == '__main__':
    for sp in sys.argv[1:]:
        pos, bone, nrm, bones = raster(sp)
        np.savez_compressed(ROOT + f'/Screenshots/_work/mesh/{sp}_uv.npz', pos=pos, bone=bone, nrm=nrm, bones=np.array(bones))
        print(sp, 'covered', (bone >= 0).mean())
