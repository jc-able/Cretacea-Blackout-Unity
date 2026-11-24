import os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))  # project root
import numpy as np
def box(img, r):
    if r < 1: return img.copy()
    pad = np.pad(img, ((r+1, r), (r+1, r)) + ((0,0),)*(img.ndim-2), mode='edge').astype(np.float64)
    c = pad.cumsum(0).cumsum(1)
    k = 2*r+1
    out = c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]
    return (out / (k*k)).astype(np.float32)
def gauss(img, r):
    r3 = max(1, int(round(r/1.7)))
    return box(box(box(img, r3), r3), r3)
def label(mask):
    """4-connected labelling via union-find on runs (pure numpy/python, fine for sparse masks)."""
    ys, xs = np.nonzero(mask)
    idx = -np.ones(mask.shape, np.int64); idx[ys, xs] = np.arange(len(ys))
    parent = np.arange(len(ys))
    def find(a):
        while parent[a] != a:
            parent[a] = parent[parent[a]]; a = parent[a]
        return a
    for dy, dx in ((0,1),(1,0)):
        a = idx[:mask.shape[0]-dy, :mask.shape[1]-dx]; b = idx[dy:, dx:]
        m = (a >= 0) & (b >= 0)
        for p, q in zip(a[m], b[m]):
            rp, rq = find(p), find(q)
            if rp != rq: parent[rq] = rp
    roots = np.array([find(i) for i in range(len(ys))])
    uniq, lab = np.unique(roots, return_inverse=True)
    out = np.zeros(mask.shape, np.int32); out[ys, xs] = lab + 1
    return out, len(uniq)
def dilate(mask, r):
    return box(mask.astype(np.float32), r) > 1e-6
def erode(mask, r):
    return box(mask.astype(np.float32), r) > 1 - 1e-6
