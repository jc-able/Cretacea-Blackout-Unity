#!/bin/bash
# Atmosphere lab on the verification clone: day and dawn free-camera views after a profile change.
#   atmo_lab.sh <tag> [presets...]      (default presets: GoldenDay DawnCoast)
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
TAG=$1; shift; PRESETS="$*"; [ -z "$PRESETS" ] && PRESETS="GoldenDay DawnCoast"
O=Screenshots/atmo_lab/$TAG; mkdir -p $O
unity command editor.playmode stop >/dev/null 2>&1; /bin/sleep 2
unity recompile "$PWD" --format json 2>&1 | grep -v hint | /opt/homebrew/bin/python3 -c "
import sys,json
d=json.load(sys.stdin)['data']; print('compile errors',d['summary']['errors'],'warnings',d['summary']['warnings'])
for e in d.get('errors',[])[:6]: print(' E',str(e)[:300])"
for try in 1 2 3 4 5 6; do R=$(unity command cretacea look_pass --steps profiles 2>&1 | grep -v hint | tail -1 | cut -c1-80); echo "profiles: $R"; case "$R" in *regenerated*) break;; esac; /bin/sleep 5; done
unity command editor.playmode start >/dev/null 2>&1; /bin/sleep 12
sandbox_saves >/dev/null
ev 'Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.Night, true); return "ok";' >/dev/null; /bin/sleep 5
free() { unity command cretacea capture --output_path "$O/$1.png" --pos $2 --look_at $3 --fov ${4:-60} --width 1280 --height 720 --warmup_frames 24 >/dev/null 2>&1; }
for PRESET in $PRESETS; do
  ev "Cretacea.Core.AtmosphereController.Instance.SetPreset(Cretacea.Core.AtmospherePreset.$PRESET, 0f); Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return \"p\";" >/dev/null
  /bin/sleep 6
  free ${PRESET}_plaza_eye  -42,2.0,-92  -30,3,0
  free ${PRESET}_meadow_eye -60,3,-150   -40,4,-40
  free ${PRESET}_aerial_sw  -250,220,-260 0,0,0
  free ${PRESET}_valley_eye 128,3,0      220,6,20
  free ${PRESET}_coast_eye  543,2.7,-46  900,-10,-46
  free ${PRESET}_crane      533,28,-42   310,10,-15 55
done
unity command editor.playmode stop >/dev/null 2>&1
/opt/homebrew/bin/python3 - "$O" $PRESETS <<'PYEOF'
import sys, os
from PIL import Image, ImageDraw
o=sys.argv[1]; presets=sys.argv[2:]
views=["plaza_eye","meadow_eye","aerial_sw","valley_eye","coast_eye","crane"]
W,H=640,360
s=Image.new("RGB",(W*3,H*2*len(presets))); d=ImageDraw.Draw(s)
for pi,p in enumerate(presets):
    for vi,v in enumerate(views):
        r,c=divmod(vi,3); f=f"{o}/{p}_{v}.png"
        if not os.path.exists(f): continue
        im=Image.open(f).convert("RGB").resize((W,H)); s.paste(im,(c*W,(pi*2+r)*H)); d.rectangle((c*W,(pi*2+r)*H,c*W+220,(pi*2+r)*H+14),fill=(0,0,0)); d.text((c*W+3,(pi*2+r)*H+1),f"{p}_{v}",fill=(255,255,0))
s.save(f"{o}/sheet.jpg",quality=85); print("sheet", s.size)
PYEOF
echo "== $(date +%T) atmo lab $TAG done"
