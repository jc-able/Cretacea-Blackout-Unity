#!/bin/bash
# Review capture set (Play mode must be running, saves sandboxed). Writes 1280x720 PNGs into <out_dir>/{rooms,presets,ui,intro}.
#   capture_set.sh <out_dir> [rooms] [presets] [ui] [extra] [intro]      (no part named = rooms presets ui intro)
# The story route itself is captured by route_test.py --out <out_dir>/route.
source "$(dirname "$0")/ucli.sh"
OUT="${1:?out dir}"; shift
PARTS="$*"; [ -z "$PARTS" ] && PARTS="rooms presets ui intro"
T="$(dirname "$0")/nightlab.sh"
cd "$UNITY_PROJECT_PATH" || exit 1
mkdir -p "$OUT/rooms" "$OUT/presets" "$OUT/ui" "$OUT/intro"
has() { case " $PARTS " in *" $1 "*) return 0;; esac; return 1; }
start() { ev "Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.$1, true); return \"ok\";" >/dev/null; /bin/sleep 5; }
# A free camera far from the player: the weather (rain, rain curtains, roof map) is moved to the camera first and
# given three seconds to fill in, or the storm frames show the curtains from outside as vertical seams.
anchor() { ev "var go = UnityEngine.GameObject.Find(\"__CaptureAnchor\"); if (go == null) go = new UnityEngine.GameObject(\"__CaptureAnchor\"); var p = \"$1\".Split(','); go.transform.position = new UnityEngine.Vector3(float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture), float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture)); Cretacea.Rendering.StormRainOcclusionController.CaptureAnchor = go.transform; return \"anchored\";" >/dev/null; }
unanchor() { ev 'Cretacea.Rendering.StormRainOcclusionController.CaptureAnchor = null; var go = UnityEngine.GameObject.Find("__CaptureAnchor"); if (go != null) UnityEngine.Object.Destroy(go); return "released";' >/dev/null; }
free() { anchor $2; /bin/sleep 3; unity command cretacea capture --output_path "$OUT/presets/$1.png" --pos $2 --look_at $3 --fov ${4:-60} --width 1280 --height 720 --warmup_frames 24 >/dev/null 2>&1; }

if has rooms; then
  start Night
  $T office_on      - -66.5 0.1 25.5 35 -6 off "$OUT/rooms" >/dev/null
  $T lockers_on     - -53 0.1 25 0 0 off "$OUT/rooms" >/dev/null
  $T corridor_on    - -52 0.1 20.5 90 0 off "$OUT/rooms" >/dev/null
  $T breakroom_on   - -61 0.1 18.5 180 0 off "$OUT/rooms" >/dev/null
  $T giftshop_on    - -61 0.1 -22 0 0 off "$OUT/rooms" >/dev/null
  $T lobby_on       - -31 0.7 -22 0 5 off "$OUT/rooms" >/dev/null
  start Records
  $T office_dark    - -66.5 0.1 25.5 35 -6 on "$OUT/rooms" >/dev/null
  $T corridor_dark  - -52 0.1 20.5 90 0 on "$OUT/rooms" >/dev/null
  $T corridor_dark_noflash - -40 0.1 20.5 90 0 off "$OUT/rooms" >/dev/null
  $T lobby_dark     - -31 0.7 -8 0 -8 on "$OUT/rooms" >/dev/null
  $T breakroom_dark - -61 0.1 18.5 180 0 on "$OUT/rooms" >/dev/null
  $T records_dark   - -8 0.1 22 60 -5 on "$OUT/rooms" >/dev/null
fi

# Review 4: the UI set shot after the presets showed the blackout Staff Corridor lit like a powered hall (walls
# 187-211/255); a normal checkpoint start and a DawnCoast snap both give the dark corridor (walls ~40), so the
# free-camera preset captures leave something behind. The UI set is shot before them.
if has ui; then
  start Records
  $T hud_corridor - -40 0.1 20.5 90 0 on "$OUT/ui" >/dev/null
  ev 'Cretacea.UI.MenuController.Instance.ShowPauseMenu(); return "pause";' >/dev/null; /bin/sleep 1.2
  gv "$OUT/ui/pause.png" 1 1280 720 >/dev/null
  ev 'Cretacea.UI.MenuController.Instance.OpenSettings(null); return "settings";' >/dev/null; /bin/sleep 1.2
  gv "$OUT/ui/settings.png" 1 1280 720 >/dev/null
  ev 'Cretacea.UI.MenuController.Instance.OpenControls(null); return "controls";' >/dev/null; /bin/sleep 1.2
  gv "$OUT/ui/controls.png" 1 1280 720 >/dev/null
  ev 'Cretacea.UI.MenuController.Instance.ResumeGame(); return "resume";' >/dev/null; /bin/sleep 0.8
  for TAB in 0 1 2; do
    ev "Cretacea.UI.JournalController.Instance.OpenJournal($TAB); return \"journal\";" >/dev/null; /bin/sleep 1.2
    gv "$OUT/ui/journal_tab$TAB.png" 1 1280 720 >/dev/null
  done
  ev 'Cretacea.UI.JournalController.Instance.CloseJournal(); return "close";' >/dev/null; /bin/sleep 0.6
  ev 'Cretacea.Core.WorldRegistry.Instance.GetInteractable("doc_manual").OnInteract(null); return "doc";' >/dev/null; /bin/sleep 1.5
  gv "$OUT/ui/document_reader.png" 1 1280 720 >/dev/null
  ev 'Cretacea.UI.MenuController.Instance.CloseDocumentModal(); return "closed";' >/dev/null
fi

if has presets; then
  start Night
  for PRESET in GoldenDay DuskTransition NightCalm NightStormBlackout DawnCoast; do
    # the storm is the blackout: shoot it with the game's blackout light groups (review 3 saw a powered plaza)
    if [ "$PRESET" = "NightStormBlackout" ]; then start Records; fi
    ev "Cretacea.Core.AtmosphereController.Instance.SetPreset(Cretacea.Core.AtmospherePreset.$PRESET, 0f); Cretacea.AI.DinoSpawner.Instance.DespawnAll(); return \"p\";" >/dev/null
    /bin/sleep 5
    free ${PRESET}_plaza_eye  -42,2.0,-92  -30,3,0
    free ${PRESET}_meadow_eye -60,3,-150   -40,4,-40
    free ${PRESET}_aerial_sw  -250,220,-260 0,0,0
    free ${PRESET}_valley_eye 128,3,0      220,6,20
    free ${PRESET}_valley_air 150,140,-200 150,0,0
    free ${PRESET}_yard_eye   445,2.6,-30  500,3,-50
    free ${PRESET}_coast_eye  543,2.7,-46  900,-10,-46
  done
  unanchor
fi

# Lightning as the player sees it (the glow is placed for the player's camera): a strike held at its peak, game view.
if has extra; then
  mkdir -p "$OUT/extra"
  start Yard
  $T yard_before_strike - 445 terrain -30 110 2 off "$OUT/extra" >/dev/null
  ev 'Cretacea.Core.AtmosphereController.Instance.HoldLightningForCapture(1f, 200f, 55f); return "held";' >/dev/null; /bin/sleep 1.2
  gv "$OUT/extra/yard_strike.png" 1 1280 720 >/dev/null
  ev 'Cretacea.Core.AtmosphereController.Instance.HoldLightningForCapture(0f); return "released";' >/dev/null
  start Paddock
  $T valley_road_flashlight - 128 terrain 0 78 -10 on "$OUT/extra" >/dev/null
  ev 'Cretacea.Core.AtmosphereController.Instance.HoldLightningForCapture(1f, 250f, 50f); return "held";' >/dev/null; /bin/sleep 1.2
  gv "$OUT/extra/valley_strike.png" 1 1280 720 >/dev/null
  ev 'Cretacea.Core.AtmosphereController.Instance.HoldLightningForCapture(0f); return "released";' >/dev/null
  start Records
  $T office_dark_exit_wall_off - -63 0.06 30 180 0 off "$OUT/extra" >/dev/null
  $T office_dark_exit_wall_on  - -63 0.06 30 180 0 on "$OUT/extra" >/dev/null
  $T feedprep_white_tile - -2.0 0.06 12.0 180 0 on "$OUT/extra" >/dev/null
fi

if has intro; then
  unity command cretacea jump_chapter --chapter Intro >/dev/null 2>&1
  for i in $(seq -w 1 20); do /bin/sleep 2.4; gv "$OUT/intro/intro_$i.png" 1 1280 720 >/dev/null; done
fi
echo "capture set written to $OUT ($PARTS)"
