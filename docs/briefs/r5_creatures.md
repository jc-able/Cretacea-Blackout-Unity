# Round 5 package: every creature and person in motion

Clone: `~/CretaceaUnityClones/creatures`. Branch `r5/creatures`.
You own: `Assets/Scripts/AI/**`, `Assets/Animations/Dinosaurs/**`, `Assets/Prefabs/Dinosaurs/**`,
`Assets/Prefabs/Characters/**`, `Assets/Editor/DinoAnimatorBuilder.cs` (and other editor builders of creature or
character prefabs), import settings and clips of `Assets/Art/Dinosaurs/**` and `Assets/Art/Characters/**`. Not
`Assets/Scripts/Story/**` (the playthrough package owns it): if a scripted beat needs a new position, path or timing,
write it under "Requests for the lead" with exact values.

The owner asked to fix "all animations" so it looks like a professional AAA game. Inventory: T. rex (Idle_Heavy,
Walk_Stomp, Charge_Run, Roar_Massive, Look_Scan, Bite_Snap, Fence_Bash), raptor (Idle_Alert, Stalk_Walk, Sprint_Run,
Sniff_Inspect, Roar_Call, Bite_Lunge, Turn_Left, Turn_Right), brachiosaurus (Idle_Graze, Walk_Majestic,
Head_Lift_Call), triceratops (Idle_Alert, Walk_Heavy, Snort_Threat): clips authored procedurally in Blender by the
first AI (`Assets/Art/Dinosaurs/dino_manifest.json`); `DinoControllerBase` (NavMesh locomotion, stride-speed scaling,
scripted moves), `DinoProceduralRig` (look-at, jaw, foot IK, pelvis); park visitors and the guide (`ParkVisitor`,
`ParkVisitorCrowd`, idle and walk clips).
Goals, in order:
1. **Watch every one in motion first** (motion captures, see r5_common.md): each species walking, running, turning
   and idling on flat ground and on a slope (spawn them with `DinoSpawner`), and every scripted beat: the opening
   (brachiosaurus and trikes in shots 1-3, the T. rex at the gallery window, the crowd), the chapter 1 blackout
   raptors and the lobby raptor, the service-path raptor crossing, the T. rex reveal and its patrol in the valley,
   the chase after auxiliary power, the T. rex at the yard wall. Write down every defect you see: foot sliding,
   moonwalking, T-poses, popping between clips, floating or sinking, sliding sideways, spinning on the spot, a run
   played at walking speed, stiff robot cycles, heads or tails that never move, clipping through walls, fences,
   glass, each other or the camera.
2. **Fix them**: animator transitions and blend trees (speed-driven walk-run blend, turn in place, exit times), clip
   import settings (loop, root motion, compression), locomotion in `DinoControllerBase` (acceleration, turning rate
   that matches the body, stride matching so feet plant), `DinoProceduralRig` (foot IK that plants, pelvis on slopes,
   breathing, tail and neck follow-through, look-at), visitor walk speed and idle variety. If a clip itself is bad
   (a stiff or broken cycle), you may rework it with headless Blender from the FBX and re-import, keeping the bone
   names and clip names.
3. The T. rex is the star: its walk must carry weight (a dip on each footfall, head counter-motion, tail swing), its
   roar must read, and nothing may slide.
Evidence: before/after motion clips for each species and each beat you changed; perf at the valley entry with the
T. rex in view before and after.
