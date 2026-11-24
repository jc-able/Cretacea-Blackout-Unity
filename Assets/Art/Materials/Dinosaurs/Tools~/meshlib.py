import os
ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../..'))  # project root
import numpy as np, struct
def load(sp):
    b = open(ROOT + f'/Screenshots/_work/mesh/{sp}.bin','rb').read()
    nv, nt = struct.unpack_from('ii', b, 0); o = 8
    v = np.frombuffer(b, np.float32, nv*3, o).reshape(-1,3); o += nv*12
    n = np.frombuffer(b, np.float32, nv*3, o).reshape(-1,3); o += nv*12
    uv = np.frombuffer(b, np.float32, nv*2, o).reshape(-1,2); o += nv*8
    bw = np.frombuffer(b, np.dtype([('i0','i4'),('w0','f4'),('i1','i4'),('w1','f4')]), nv, o); o += nv*16
    t = np.frombuffer(b, np.int32, nt, o).reshape(-1,3)
    bones = open(ROOT + f'/Screenshots/_work/mesh/{sp}_bones.txt').read().split('\n')
    return v, n, uv, bw, t, bones
def components(v, t):
    # weld by position, union-find over triangles
    key = np.round(v*1e4).astype(np.int64)
    _, weld = np.unique(key, axis=0, return_inverse=True); weld = weld.ravel()
    parent = np.arange(weld.max()+1)
    def find(x):
        while parent[x] != x:
            parent[x] = parent[parent[x]]; x = parent[x]
        return x
    for a,b,c in weld[t]:
        ra, rb, rc = find(a), find(b), find(c)
        parent[rb] = ra; parent[find(rc)] = ra
    roots = np.array([find(x) for x in range(len(parent))])
    return roots[weld]
