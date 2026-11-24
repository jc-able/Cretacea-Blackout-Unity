#!/bin/bash
# Free-camera captures of one atmosphere preset from fixed outdoor viewpoints (Play mode must be running).
#   presetshots.sh <tag> <GoldenDay|DuskTransition|NightCalm|NightStormBlackout|DawnCoast> [out_dir]
source "$(dirname "$0")/ucli.sh"
TAG="$1"; PRESET="$2"; OUT="${3:-Screenshots/look9}"
mkdir -p "$UNITY_PROJECT_PATH/$OUT"
ev "Cretacea.Core.AtmosphereController.Instance.SetPreset(Cretacea.Core.AtmospherePreset.$PRESET, 0f); Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return \"preset\";" >/dev/null
/bin/sleep 4
cap() { unity command cretacea capture --output_path "$OUT/${TAG}_$1.png" --pos $2 --look_at $3 --fov 60 --width 1280 --height 720 --warmup_frames 24 2>&1 | grep -o '"meanLuma":[0-9.]*' | sed "s/^/${TAG}_$1 /"; }
cap aerial -90,38,-70 0,2,15
cap plaza -31,1.7,-60 -31,6,0
cap valley 128,3,0 220,6,20
cap yard 445,2.6,-30 500,3,-50
cap coast 536,2.6,-46 620,2,-40
