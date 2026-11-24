#!/bin/bash
# Flashlight lab 2: five dark-room poses, region stats + the budget the controller is using.
#   fl_lab2.sh <out_tag> [noplay]     (noplay = Play mode is already running at the Records checkpoint)
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
T=Tools/lookdev/nightlab.sh; O=Screenshots/fl_lab/$1; mkdir -p $O
PY=/opt/homebrew/bin/python3
if [ "$2" != "noplay" ]; then
  unity command editor.playmode start >/dev/null 2>&1; /bin/sleep 12
  sandbox_saves >/dev/null
  ev 'Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.Records, true); return "ok";' >/dev/null; /bin/sleep 6
fi
dbg() { ev 'return UnityEngine.Object.FindAnyObjectByType<Cretacea.Player.FlashlightController>().DebugBudget();' | tail -1; }
one() { # name x y z yaw pitch regions...
  local n=$1; shift; $T $n - $1 $2 $3 $4 $5 on $O >/dev/null; shift 5
  # look twice: the first look at a material starts its albedo measurement
  /bin/sleep 1.2; gv "$UNITY_PROJECT_PATH/$O/$n.png" 1 1280 720 >/dev/null
  echo "$($PY Tools/lookdev/region_stats.py "$O/$n.png" "$@")  [$(dbg)]"
}
one records_dark  -8 0.1 22 60 -5      ceil=512,240,576,288 fan=500,150,800,300 pool=608,328,672,392 hand=820,560,980,700 room=0,0,1280,720
one office_dark   -66.5 0.1 25.5 35 -6 ceil=560,200,720,260 pool=600,330,680,400 hand=820,560,980,700 room=0,0,1280,720
one feedprep_tile -2.0 0.06 12.0 180 0 core=608,342,693,401 hand=820,560,980,700 room=0,0,1280,720
one lobby_dark    -31 0.7 -8 0 -8      wedge=680,430,780,500 pool=600,320,680,400 hand=820,560,980,700 room=0,0,1280,720
one corridor_dark -52 0.1 20.5 90 0    pool=600,320,680,400 hand=820,560,980,700 room=0,0,1280,720
$PY - "$O/feedprep_tile.png" <<'PYEOF'
import sys, numpy as np
from PIL import Image
a=np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(float); L=0.2126*a[...,0]+0.7152*a[...,1]+0.0722*a[...,2]
print("feedprep_tile: px >=250:", int((L>=250).sum()), " >=240:", int((L>=240).sum()), " max:", int(L.max()))
PYEOF
echo "== $(date +%T) lab2 $1 done"
