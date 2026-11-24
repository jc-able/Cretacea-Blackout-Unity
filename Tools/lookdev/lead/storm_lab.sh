#!/bin/bash
# Storm valley lab: sky against tree line, fog veil. Play mode at the Paddock checkpoint.
export UNITY_PROJECT_PATH=$HOME/CretaceaUnityClones/vfx
cd $UNITY_PROJECT_PATH && source Tools/lookdev/ucli.sh
T=Tools/lookdev/nightlab.sh; O=Screenshots/storm_lab; mkdir -p $O
PY=/opt/homebrew/bin/python3
st() { $PY Tools/lookdev/region_stats.py "$O/$1.png" sky_top=200,20,1080,120 sky_low=200,200,1080,280 trees=200,300,1080,380 ground=200,500,1080,700; }
setlook() { # sunDimmer fogMul ambientDimmer
  ev "var p = UnityEngine.Resources.Load<UnityEngine.Rendering.VolumeProfile>(\"Atmosphere/Look_NightStormBlackout\"); UnityEngine.Rendering.HighDefinition.VolumetricClouds c; p.TryGet(out c); UnityEngine.Rendering.HighDefinition.Fog f; p.TryGet(out f); c.sunLightDimmer.value = ${1}f; c.ambientLightProbeDimmer.value = ${3}f; f.color.value = new UnityEngine.Color(0.34f, 0.42f, 0.56f) * ${2}f; return c.sunLightDimmer.value + \" \" + c.ambientLightProbeDimmer.value + \" \" + f.color.value;" | tail -1
}
if [ "$1" != "noplay" ]; then
  unity command editor.playmode start >/dev/null 2>&1; /bin/sleep 12
  sandbox_saves >/dev/null
  ev 'Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.Paddock, true); return "ok";' >/dev/null; /bin/sleep 8
fi
ev 'var p = UnityEngine.Resources.Load<UnityEngine.Rendering.VolumeProfile>("Atmosphere/Look_NightStormBlackout"); UnityEngine.Rendering.HighDefinition.VolumetricClouds c; p.TryGet(out c); return "now: sun " + c.sunLightDimmer.value + " ambient " + c.ambientLightProbeDimmer.value + " (override " + c.ambientLightProbeDimmer.overrideState + ")";' | tail -1
$T base - 128 terrain 0 78 2 off $O >/dev/null; st base
setlook 0.4 2.3 1.0; /bin/sleep 3; gv "$PWD/$O/fog23.png" 1 1280 720 >/dev/null; st fog23
setlook 0.8 2.3 1.0; /bin/sleep 3; gv "$PWD/$O/sun08_fog23.png" 1 1280 720 >/dev/null; st sun08_fog23
setlook 1.5 2.3 1.0; /bin/sleep 3; gv "$PWD/$O/sun15_fog23.png" 1 1280 720 >/dev/null; st sun15_fog23
setlook 0.8 2.3 2.0; /bin/sleep 3; gv "$PWD/$O/sun08_amb2_fog23.png" 1 1280 720 >/dev/null; st sun08_amb2_fog23
echo "== $(date +%T) storm lab done (Play still running)"
