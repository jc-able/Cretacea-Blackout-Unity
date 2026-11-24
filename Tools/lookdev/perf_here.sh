#!/bin/bash
# Median frame time at the current camera (Play mode). perf_here.sh <label> [samples=6]
source "$(dirname "$0")/ucli.sh"
N="${2:-6}"; F=/tmp/_perfh_$$.txt; : > $F
for i in $(seq 1 $N); do unity command diagnostics get_performance 2>/dev/null | grep -E "^render|^frameTiming" | tr '\n' ' ' >> $F; echo >> $F; /bin/sleep 0.6; done
/opt/homebrew/bin/python3 - "$1" $F <<'PY'
import sys,re,statistics
name,p=sys.argv[1],sys.argv[2]
g=[];c=[];t=[]
for line in open(p):
    a=re.search(r'"gpuFrameTimeMs":([0-9.]+)',line); b=re.search(r'"cpuFrameTimeMs":([0-9.]+)',line); tr=re.search(r'"triangles":(\d+)',line)
    if a and b and tr: g.append(float(a.group(1))); c.append(float(b.group(1))); t.append(int(tr.group(1)))
med=statistics.median
print(f"{name:26s} gpu {med(g):5.1f} ms  cpu {med(c):5.1f} ms  tris {med(t)/1e6:5.2f} M") if g else print(name,"no samples")
PY
rm -f $F
