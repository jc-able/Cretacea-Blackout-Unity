#!/bin/bash
# Motion capture set for reviews (Play mode is started here; saves sandboxed): frame sequences in slowed time,
# assembled into MP4 clips and contact sheets.   motion_set.sh <out_dir> [segment ...]
# Segments: hand_idle intro_trex raptor_cross trex_reveal trex_walk_side visitors   (default: all)
source "$(dirname "$0")/ucli.sh"
OUT="${1:?out dir}"; shift
SEGS="$*"; [ -z "$SEGS" ] && SEGS="hand_idle intro_trex raptor_cross trex_reveal trex_walk_side visitors"
cd "$UNITY_PROJECT_PATH" || exit 1
mkdir -p "$OUT"
PY=/opt/homebrew/bin/python3; FF=/opt/homebrew/bin/ffmpeg
has() { case " $SEGS " in *" $1 "*) return 0;; esac; return 1; }
ts() { ev "UnityEngine.Time.timeScale = ${1}f; return \"ts\";" >/dev/null; }
start() { ev "Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.$1, true); return \"ok\";" >/dev/null; /bin/sleep ${2:-6}; }
tp() { ev "Cretacea.Player.PlayerController.Instance.Teleport(new UnityEngine.Vector3(${1}f, ${2}f, ${3}f), ${4}f, ${5}f); return \"ok\";" >/dev/null; }
torch() { ev "var fl = UnityEngine.Object.FindAnyObjectByType<Cretacea.Player.FlashlightController>(); fl.SetHasFlashlight(true, false, false); fl.SetBeamEnabled($1, true); return \"ok\";" >/dev/null; }
clip() { # name frames [free-camera C# expression returning "px,py,pz|lx,ly,lz"]
  local name=$1 n=$2 cam="$3" d="$OUT/$1"; rm -rf "$d"; mkdir -p "$d"
  for i in $(seq -w 1 $n); do
    if [ -z "$cam" ]; then gv "$PWD/$d/f_$i.png" 1 1280 720 >/dev/null
    else
      local pl=$(ev "$cam" | tail -1); local p=${pl%%|*} l=${pl##*|}
      unity command cretacea capture --output_path "$d/f_$i.png" --pos $p --look_at $l --fov 50 --width 1280 --height 720 --warmup_frames 2 >/dev/null 2>&1
    fi
  done
  $FF -y -loglevel error -framerate 8 -pattern_type glob -i "$d/f_*.png" -c:v libx264 -pix_fmt yuv420p "$OUT/$name.mp4"
  $PY - "$d" "$OUT/${name}_sheet.jpg" <<'PYEOF'
import sys, glob
from PIL import Image, ImageDraw
fs=sorted(glob.glob(sys.argv[1]+"/f_*.png"))
if not fs: sys.exit(0)
pick=[fs[round(i*(len(fs)-1)/7)] for i in range(8)] if len(fs)>=8 else fs
W,H=480,270; s=Image.new("RGB",(W*4,H*2)); d=ImageDraw.Draw(s)
for k,f in enumerate(pick):
    r,c=divmod(k,4); s.paste(Image.open(f).convert("RGB").resize((W,H)),(c*W,r*H)); d.text((c*W+4,r*H+3),f.split("/")[-1],fill=(255,255,0))
s.save(sys.argv[2],quality=85)
PYEOF
  echo "  $name: $(ls $d | wc -l | tr -d ' ') frames -> $OUT/$name.mp4"
}
unity command editor.playmode start >/dev/null 2>&1; /bin/sleep 12
sandbox_saves >/dev/null

if has hand_idle; then          # the hand at rest in the lit office (breathing, idle sway), beam on
  start Night; ev 'Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return "d";' >/dev/null
  tp -66.5 0.1 25.5 35 -6; torch true; /bin/sleep 2; ts 1
  clip hand_idle 24
fi
if has intro_trex; then         # opening: the T. rex walks to the gallery window and roars (slowed x0.5)
  unity command cretacea jump_chapter --chapter Intro >/dev/null 2>&1; /bin/sleep 31; ts 0.5
  clip intro_trex 32; ts 1
fi
if has raptor_cross; then       # service path: the raptor crosses the junction (slowed x0.25)
  start Dock; ev 'Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return "d";' >/dev/null
  tp 55.0 0.06 14.0 90 0; torch true; /bin/sleep 5; ts 0.25      # 5 s: let the exposure leave the dark dock first
  ev 'Cretacea.Core.GameEvents.RaiseStoryTriggerEntered("paths_corner2"); return "ok";' >/dev/null
  clip raptor_cross 16; ts 1
fi
if has trex_reveal; then        # valley: the T. rex reveal from the trigger point (slowed x0.5)
  start Paddock 8; tp 128 1.2 0 78 2; torch false; /bin/sleep 5; ts 0.5
  ev 'Cretacea.Core.GameEvents.RaiseStoryTriggerEntered("trex_reveal"); return "ok";' >/dev/null
  clip trex_reveal 32; ts 1
fi
if has trex_walk_side; then     # the T. rex walking its patrol, free camera beside it (normal speed, sampled)
  start Paddock 8
  ev 'var t = Cretacea.AI.DinoSpawner.Instance; return "ok";' >/dev/null
  clip trex_walk_side 24 'UnityEngine.Transform t = null; foreach (var d in UnityEngine.Object.FindObjectsByType<Cretacea.AI.TrexAI>()) { t = d.transform; break; } if (t == null) return "150,8,0|190,4,40"; var p = t.position + t.right * 16f + UnityEngine.Vector3.up * 3.5f - t.forward * 3f; var l = t.position + UnityEngine.Vector3.up * 3f; System.Func<UnityEngine.Vector3,string> f = v => v.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + v.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "," + v.z.ToString("F2", System.Globalization.CultureInfo.InvariantCulture); return f(p) + "|" + f(l);'
fi
if has visitors; then           # opening glide past the crowd on the plaza (normal speed)
  unity command cretacea jump_chapter --chapter Intro >/dev/null 2>&1; /bin/sleep 9.5; ts 1
  clip visitors 24
fi
ts 1
unity command editor.playmode stop >/dev/null 2>&1
echo "motion set written to $OUT ($SEGS)"
