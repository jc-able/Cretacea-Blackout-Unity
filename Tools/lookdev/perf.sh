#!/bin/bash
# Frame-time samples at fixed spots (Play mode running). perf.sh [samples=6]
# Prints the median GPU / CPU frame time and triangles per spot. The editor's Game view size and other apps affect it.
source "$(dirname "$0")/ucli.sh"
N="${1:-6}"
T="$(dirname "$0")/nightlab.sh"
cd "$UNITY_PROJECT_PATH" || exit 1
spot() { # name checkpoint x y z yaw pitch flash
  $T _perf "$2" $3 $4 $5 $6 $7 $8 Screenshots/_perf >/dev/null
  /bin/sleep 2
  : > /tmp/_perf_$$.txt
  for i in $(seq 1 $N); do unity command diagnostics get_performance 2>/dev/null | grep -E "^render|^frameTiming" | tr '\n' ' ' >> /tmp/_perf_$$.txt; echo >> /tmp/_perf_$$.txt; /bin/sleep 0.7; done
  /opt/homebrew/bin/python3 - "$1" /tmp/_perf_$$.txt <<'PY'
import sys,re,statistics
name,p=sys.argv[1],sys.argv[2]
g=[];c=[];m=[];t=[];d=[]
for line in open(p):
    a=re.search(r'"gpuFrameTimeMs":([0-9.]+)',line); b=re.search(r'"cpuFrameTimeMs":([0-9.]+)',line); mm=re.search(r'"cpuMainThreadFrameTimeMs":([0-9.]+)',line); tr=re.search(r'"triangles":(\d+)',line); dc=re.search(r'"drawCalls":(\d+)',line)
    if a and b and tr:
        g.append(float(a.group(1))); c.append(float(b.group(1))); m.append(float(mm.group(1))); t.append(int(tr.group(1))); d.append(int(dc.group(1)))
if g:
    med=statistics.median
    worst=max(med(g),med(c))
    print(f"{name:14s} gpu {med(g):5.1f} ms  cpu {med(c):5.1f} ms (main {med(m):4.1f})  ~{1000/worst:5.1f} fps  tris {med(t)/1e6:5.2f} M  draws {int(med(d))}")
else:
    print(name,"no samples")
PY
  rm -f /tmp/_perf_$$.txt
}
spot office_on   Night      -66.5 0.1 25.5 35 -6 off
spot lobby_on    -          -31 0.7 -22 0 5 off
spot corridor    Records    -40 0.1 20.5 90 0 on
spot lobby_dark  -          -31 0.7 -8 0 -8 on
spot path        Dock       62 0.1 5 100 0 on
spot valley      Paddock    128 terrain 0 70 4 on
spot substation  Substation 252.5 2.4 0 90 -3 on
spot yard        Yard       445 1.1 -30 110 0 on
