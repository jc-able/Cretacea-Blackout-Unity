#!/bin/bash
# Night look lab (Play mode must be running): jump to a spot, flashlight on/off, capture with the HUD.
#   nightlab.sh <name> <checkpoint> <x> <y|terrain> <z> <yaw> <pitch> <flash:on|off> [out_dir]
source "$(dirname "$0")/ucli.sh"
NAME="$1"; CP="$2"; X="$3"; Y="$4"; Z="$5"; YAW="$6"; PITCH="$7"; FL="$8"; OUT="${9:-Screenshots/look9}"
mkdir -p "$UNITY_PROJECT_PATH/$OUT"
if [ "$CP" != "-" ]; then
  ev "Cretacea.Story.StoryDirector.Instance.StartFromCheckpoint(Cretacea.Core.CheckpointId.$CP, true); return \"cp\";" >/dev/null
  /bin/sleep 3
fi
if [ "$Y" = "terrain" ]; then
  # the ground under the point: a ray from above (Terrain.activeTerrain is an arbitrary tile: the island has nine)
  Y=$(ev "UnityEngine.RaycastHit h; return UnityEngine.Physics.Raycast(new UnityEngine.Vector3(${X}f, 120f, ${Z}f), UnityEngine.Vector3.down, out h, 400f, Cretacea.Core.GameLayers.EnvironmentAndDoorMask, UnityEngine.QueryTriggerInteraction.Ignore) ? (h.point.y + 0.1f).ToString(\"F2\") : \"1.0\";" | tail -1)
fi
B=false; [ "$FL" = "on" ] && B=true
ev "Cretacea.AI.DinoSpawner.Instance.DespawnAll(); var fl = UnityEngine.Object.FindAnyObjectByType<Cretacea.Player.FlashlightController>(); fl.SetHasFlashlight(true, false, false); fl.SetBeamEnabled($B, true); Cretacea.Player.PlayerController.Instance.Teleport(new UnityEngine.Vector3(${X}f, ${Y}f, ${Z}f), ${YAW}f, ${PITCH}f); return \"ok\";" >/dev/null
/bin/sleep 3.5
gv "$UNITY_PROJECT_PATH/$OUT/$NAME.png" 1 1280 720
