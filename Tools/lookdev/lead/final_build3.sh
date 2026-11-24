#!/bin/bash
# Final player build on the verification clone + two self-test runs (saves sandboxed).
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
S=/private/tmp/claude-501/-Users-jc-able--claude-spares-spare-jxQboM/9d5b6ec1-be2e-4d6b-8f6a-8a96d2ed0c5e/scratchpad/unity/buildtest_final
echo "== $(date +%T) rebuild scenes"; unity command cretacea build_all_scenes --timeout 2400 2>&1 | grep -v hint | tail -1
OLD=$(unity command build status 2>/dev/null | grep -E '^buildId' | head -1)
echo "== $(date +%T) player build (previous: $OLD)"
unity command build run --target StandaloneOSX --output-path Builds/CretaceaBlackout.app --confirm true 2>&1 | grep -v hint | head -3
/bin/sleep 20
i=0
until { ST="$(unity command build status 2>/dev/null)"; echo "$ST" | grep -qE '^status: completed' && ! echo "$ST" | grep -qF "$OLD"; } || [ $i -ge 400 ]; do /bin/sleep 5; i=$((i+1)); done
unity command build status 2>&1 | grep -v hint | grep -E "^(status|buildId|result|totalSizeBytes|buildTimeMs|totalWarnings|totalErrors|buildEndedAt)"
for run in 1 2; do
  rm -rf $S$run; mkdir -p $S$run/save
  echo "== $(date +%T) smoke run $run"
  CRETACEA_SAVE_DIR="$S$run/save" "$UNITY_PROJECT_PATH/Builds/CretaceaBlackout.app/Contents/MacOS/CretaceaBlackoutUnity" -cretaceaSmoke "$S$run" -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 > $S$run/stdout.log 2>&1
  cat $S$run/report.txt
done
echo "== $(date +%T) done"
