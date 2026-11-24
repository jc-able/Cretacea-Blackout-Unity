#!/bin/bash
# Full pass on the verification clone: rebuild, doors, capture set, route test.   lead_pass.sh <out_tag>
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
TAG=${1:-lead}; O=Screenshots/$TAG
unity command editor.playmode stop >/dev/null 2>&1; /bin/sleep 2
echo "== $(date +%T) recompile"; unity recompile "$P" --format json 2>&1 | grep -v hint | grep -o '"summary":{[^}]*}' | head -1
unity status --until-ready --timeout 900 2>&1 | grep -v hint | grep vfx | cut -c1-60
for try in 1 2 3 4 5 6; do R=$(unity command cretacea look_pass --steps profiles 2>&1 | grep -v hint | tail -1 | cut -c1-80); case "$R" in *regenerated*) break;; esac; /bin/sleep 5; done
echo "== $(date +%T) build ($R)"; unity command cretacea build_all_scenes --timeout 2400 2>&1 | grep -v hint | tail -2
echo "== $(date +%T) doors"; unity command cretacea verify_playmode_doors --timeout 600 2>&1 | grep -v hint | grep -E "documentsFound|navMesh|\"doors|passed|failed" | head -8
rm -rf $O; mkdir -p $O
echo "== $(date +%T) play + intro"; unity command editor.playmode start 2>&1 | grep -v hint | tail -1; /bin/sleep 15
sandbox_saves >/dev/null
bash Tools/lookdev/capture_set.sh $O intro 2>&1 | tail -1
echo "== $(date +%T) rooms presets ui extra"; bash Tools/lookdev/capture_set.sh $O rooms presets ui extra 2>&1 | tail -1
echo "== $(date +%T) route"; python3 Tools/lookdev/route_test.py --out $O/route 2>&1 | tail -5
unity command editor.playmode stop 2>&1 | grep -v hint | tail -1
echo "== $(date +%T) done; files: $(find $O -name '*.png' | wc -l)"
