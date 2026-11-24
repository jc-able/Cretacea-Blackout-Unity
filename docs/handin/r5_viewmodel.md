# Round 5 hand-in: the first-person hand and flashlight (`r5/viewmodel`)

Evidence: `Screenshots/handin_r5/` (see its `INDEX.md`): before/after stills and nine motion clips (MP4 at 12 fps,
each frame about 0.08-0.15 s of game time apart, captured with `Time.timeScale` slowed so the Game-view captures
land close together) and a contact sheet (8 frames, cropped to the lower right) for each.

## Goal 1: a hand that looks like a shipped horror game's: done
- The old `flashlight_fps_rig.fbx` (lumpy mitten, torch hidden in the fist) is no longer used. The held model is now
  the paid Tripo scan `~/CretaceaBlackout/assets/models/viewmodel/flashlight_hand/flashlight_hand.glb` (read-only
  source), converted headless in Blender by `Tools/agent/r5vm/build_vm.py` into
  `Assets/Art/Props/Flashlight/ViewmodelHand/flashlight_hand_vm.fbx` (15,038 triangles, no decimation needed):
  - scaled 0.74 (0.46 m as generated -> a 0.34 m tactical torch and a hand of real size at 68° FOV);
  - origin at the grip on the barrel axis, an empty `Beam_Muzzle` at the lens;
  - split into five submeshes by geometry (a radius profile of the barrel measured outside the hand and interpolated
    under the fingers): leather glove, torch body, lens glass, a small LED disc at the lens centre, and a new
    tapered, oval jacket sleeve (28 x 9 rings, folds under the cuff) that continues the cuff 0.42 m down and back,
    so the forearm leaves the bottom-right of the frame and nothing ends in mid-air;
  - submeshes sorted into slot order before export (the FBX writer orders materials by first use; the first build
    had the LED material on the torch and the glass on the glove).
- Pose (`CretaceaWorldAssembler` player-rig block): grip at the existing sway pivot +(0.04, 0.02, 0.03), torch
  aimed 2° up and 4° in toward the crosshair, rolled 22° so the forearm comes in from the lower right and the back of
  the hand and the knuckles face the eye. Whole barrel, head and tail cap visible. Handedness: the hand reads as a
  right hand gripping from below in the Game view (no mirror applied); see `01_after_office_lit.png`.
- The rig is built in code (`FlashlightViewmodel` -> `VM_HandTorch`), not from `PF_FlashlightFpsRig.prefab`; the
  old prefab stays as the fallback if the new model is missing.

## Goal 2: materials: done (one shared texture atlas)
- `LookMaterials.EnsureViewmodelHandMaterials()` (flashlight section; also run by `look_pass`) creates/retunes
  `Assets/Art/Materials/Props/Flashlight/M_VM_{Glove,TorchBody,LensGlass,LED,Sleeve}.mat`:
  - glove: scan base colour (tint 0.86), scan normal x1.35, HDRP mask map converted from the scan's ORM
    (`T_VMHand_Mask.png`: R metal, G AO, A 1-roughness), smoothness 0.18-0.50, no metal: worn black leather with
    sheen on the knuckles and the creases/seams of the scan;
  - torch body: same atlas, metal up to 0.92 where the scan marks metal, smoothness 0.30-0.74: anodised body,
    knurled grip and tail cap read as separate finishes;
  - lens glass: near-black, smoothness 0.97; LED: emissive 60 nits, driven at runtime by the beam
    (`FlashlightController.UpdateLedGlow`, a MaterialPropertyBlock: off with the beam, dips with the flicker);
  - sleeve: navy jacket (0.075, 0.088, 0.115), woven normal (carpet_office normal at 3x), gloss 0.18.
  - all: decals off, SSR off.
- The beam leaves from the lens: `FlashlightSpotLight` is parented to the model at `Beam_Muzzle`, aimed down the
  torch axis, so it follows every sway, bob and pull-back (the volumetric shaft visibly starts at the head in
  `m_outdoor`).
- Rendering layer 5 and shadow casting off are still applied to every viewmodel renderer by the existing
  `ConfigureHdrpLights` (unchanged), so the 18 cd viewmodel fill still lights the glove in the blackout
  (`02_after_corridor_dark.png`).

## Goal 3: living animation: done (procedural, spring-damper)
New `Assets/Scripts/Player/ViewmodelMotion.cs`, added at runtime to the sway pivot by
`FlashlightController.UpdateViewmodelMotionAndPullback` (the wall sphere-cast stays there). Damped springs with
sub-steps (stable at any frame rate) drive one carry-pose layer and a softer look-sway layer:
- idle breathing 0.23 Hz (faster and deeper after a sprint, when exhausted, or with a predator near): `m01_idle_click`;
- gait: figure-eight bob with roll and yaw whose phase follows distance walked (stride 1.45 m walking, 2.3 m
  sprinting), weighted in/out smoothly: `m_walk_dark`, `m_sprint`;
- a dip and nod on every footstep (`GameEvents.OnPlayerFootstep`, scaled for walk/sprint/crouch);
- look sway from the camera's angular speed: lags, trails and banks into the turn (`m_walk_dark` frames 22-34);
- sprint carry (low, rolled in across the body), crouch (drawn in to the chest): `m_sprint`, `m_crouch`;
- the thumb press on the switch: a squeeze-and-click kick of the whole grip on every toggle (`m01_idle_click`
  frames 12 and 26). The scan has no finger bones, so the thumb itself does not move (see rough edges);
- equip raise from below the frame (existing `_equipProgress`, now through the springs): `m_equip`;
- lowered almost out of frame while a document or the field tablet is open: `m_document`;
- lowered and turned toward the work while an interaction is held (detected from
  `PlayerInteractor.CurrentHoldProgress01` rising): not captured in a clip;
- fine tremor (Perlin, 9-13 Hz, squared with proximity) and faster breathing when `SetNearestPredatorDistance`
  reports a predator: `m_raptor`;
- wall pull-back: the existing sphere cast, now through the springs: `m_walk_dark` last frames, `m_outdoor`.

## Goal 4: the desk pickup matches the held torch: NOT done
Out of time. The desk still shows `flashlight_pickup.fbx` via `PF_FlashlightPickup.prefab`. Plan: export a
torch-only mesh from the same scan (submeshes 1-3 plus a barrel patch under the fingers) and swap the visual in
`TryUpgradeDeskFlashlightPickup`.

## Checks
- Compile: 0 errors, 0 warnings. Scene builds: `warnings: []`.
- Final build with NavMesh: both NavMeshes baked, `warnings: []`. `route_test.py`: 45 / 45 in 269 s (no new console
  errors). `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, flashlight OK, both NavMeshes valid.
  Play-mode smoke: jumped Corridor, ServicePaths, Substation, OldYard, Escape: 0 console errors.
- Perf, security office, Play mode in the hidden editor (editor-bound CPU, batch mode): hand shown gpu 8.9 ms,
  7.73 M tris; hand hidden gpu 9.7 ms, 6.93 M tris (no measurable GPU cost; the old rig was not measured before
  it was replaced).

## Files
- `Assets/Scripts/Player/ViewmodelMotion.cs` (new), `Assets/Scripts/Player/FlashlightController.cs` (motion method
  and its new helpers only: `EnsureViewmodelMotion`, `ApplySpringViewmodelMotion`, `UpdateLedGlow`).
- `Assets/Editor/WorldBuilder/CretaceaWorldAssembler.cs` (player-rig block), `Assets/Editor/LookPass/LookMaterials.cs`
  (flashlight section).
- `Assets/Art/Props/Flashlight/ViewmodelHand/*` (model + textures), `Assets/Art/Materials/Props/Flashlight/M_VM_*.mat`.
- Tools: `Tools/agent/r5vm/build_vm.py` (Blender conversion), `clip.py` (motion clips), `pose.sh`.

## Rough edges
- The fingers and thumb are one rigid scan: no finger animation; the switch press is a whole-grip kick.
- A few small torch-classified patches on the inside of the fingers take the torch finish (slightly glossier).
- In the brightest lit rooms the torch body reads as grey anodised metal rather than black.
- `m_outdoor` was meant as a walk in the rain; the Dock checkpoint spawn faced a wall, so it shows the outdoor
  beam shaft and the pull-back instead. Rain on the hand was not verified.
- The hold-interaction lowering is implemented but not captured.

## Requests for the lead
- `PF_FlashlightFpsRig.prefab`, `flashlight_fps_rig.fbx`, `AC_FlashlightFpsRig.controller` and
  `ViewmodelGripOffset.cs` are now unused by the generated scene (fallback only); delete them when convenient.
- The desk pickup (goal 4) as planned above.

## Fix round (after the independent review)
All motion evidence was re-shot in **real time** (`Tools/agent/r5vm/clip2.py`: timeScale 1, `Time.captureFramerate`
30, every frame grabbed in the engine from `Application.onBeforeRender`, MP4 at 30 fps) with real Input System
keys/mouse (the hidden editor is never focused, so the recorder sets the runtime `InputSettings` to ignore focus and
re-enables the keyboard/mouse; both are reset afterwards and are not an asset). `Screenshots/handin_r5/INDEX.md`
lists every file.

| Finding | What I did | Evidence |
|---|---|---|
| Predator tremor never fires in game (high) | New `Assets/Scripts/Player/PredatorProximityProbe.cs`, added to the player by `FlashlightController.EnsureViewmodelMotion`: every 0.25 s an `OverlapSphereNonAlloc` (14 m, Dinosaur layer) finds the nearest non-herbivore `IDinoController` (idle/patrolling ones count 1.5x as far) and calls `SetNearestPredatorDistance` (99 when none, only on change; 99 on disable). | `m_raptor`: a real raptor spawned 11 m down the corridor walks in (no eval of the distance); the probe logged 99 -> 10.3 -> 8.9 -> 6.2 -> 2.9 -> 1.1 m. |
| Gait bob ~2x too fast, out of step (high) | Reproduced from the code (stride 1.45 m per 2 pi with cos 2 phase). `ViewmodelMotion.Tick`: stride = Lerp(Lerp(3.1, 4.3, sprint), 2.3, crouch) = 2x PlayerController's step stride; `OnFootstep` phase-locks the bob's low point (phase pi/2 + n pi) to each step, eased over 0.1 s. | Measured in Play mode at 60 fps (`fix/gait_probe.cs`): walk 2.70 m/s, 1.72 bob lows/s, every footstep within 0.017 s (one frame) of a bob low; sprint 5.70 m/s, 2.58 steps/s = 2.58 lows/s, all within one frame. `m_walk_dark`, `m_sprint`. |
| Look sway in world space, leads (high) | Delta now in the camera's frame (`Inverse(last) * current`), pitch term negated so looking down trails up (`p.y = -sway.x * 0.0012` unchanged). | `fix/sway_probe.cs`, 31 deg mouse pitch-down at yaw 0/90/180/270: hand pitch -2.8/-2.4/-2.5/-2.8 deg (nose up, behind the camera), hand y up, same at every heading. `m_look_yaw0/90/180`. |
| Torch renders as chrome; tail reads as a lens (high) | `LookMaterials`: TorchBody tint 0.45, smoothness 0.22-0.55, metal 0.85. `build_vm.py`: new slot 5 `M_VM_Rubber` (tint 0.35, metal 0, smoothness 0.12-0.28) on the grip from just ahead of the fingers back to the knurled tail cap; the head stays anodised. The scan painted the recessed tail switch light grey, so its 166 faces now take the dark knurl texel. | `01_after_office_lit`, `02_after_corridor_dark`, `04_before_after_crops.jpg`: matte black torch, dark tail. |
| Glove reads as foil; thumb press barely visible (medium) | Glove normal 0.85 (was 1.35), smoothness 0.12-0.36, tint 0.70, AO remap min 0.2. Pose roll 22 -> 34 deg. `Click()` kick raised to posVel (0, -0.18, -0.6), rotVel pitch 260/220. No thumb bone (not done: the scan has no finger topology and a weighted bone would tear the single shell). | `m01_idle_click` frames 15-24 (F pressed at 15). The glove still shows the scan's sculpted creases; it reads as dull leather now, not shiny foil, but the fingers are still not separately readable. |
| Beam inherits every hand jitter (medium) | The spot is re-parented at runtime to the rig parent (the camera pivot). Each frame (`UpdateStableBeam`) its position is the lens, its aim is the rest aim times `ViewmodelMotion.BeamRotation` (carry pose + look sway only, no tremor/gait/breath), eased at 12/s in the rig frame so camera turns are not delayed. | `fix/beam_probe.cs` with a predator at 2 m standing still: torch model 0.072 deg/frame of shake, spot 0.000 deg/frame, spot exactly at the lens. |
| Light origin behind a wall when flush (medium) | Reproduced: pressed against the corridor wall (0.38 m ahead) the lens was 0.69 m from the eye, past the wall. Fix: `UpdateMuzzleTuck` linecasts eye -> REST-pose lens and adds the missing pull-back (up to 1.6x the wall pose), plus a last-resort `Linecast` that keeps the light 3 cm on this side of a wall. (A first version moved the light back toward the eye, which put it behind the lens and lit the head as a white blob; the tuck replaced that.) | Re-measured flush: lens 0.60 m from the eye, not behind the wall, spot exactly at the lens. `m_wall` (full sheet). |
| Desk pickup is a different torch (medium) | `build_vm.py` also writes `flashlight_pickup_vm.fbx`: the same scan without glove and sleeve, the hand's holes closed by a lathe of the measured barrel profile, grip span in rubber, centred. `CretaceaWorldAssembler.TryUpgradeDeskFlashlightPickup` -> new `SwapDeskTorchForHeldModel` hides the prefab's old renderers and places it with the held torch's own materials. `FlashlightPickup.cs` unchanged (not needed). | `03a_desk_before_oldmodel` vs `03b_desk_after` (same view); `m_pickup_equip`: picked up with the real E key, desk empty, objective advances, the same torch rises in the hand. A few finger-contact patches remain as small rubber flaps on the grip. |
| Motion evidence slowed/staged; no perf baseline (medium) | All clips re-shot in real time (see top). Document opened through `DocumentPickup.OnInteract` and paged with E; tablet with Tab; E-0 gate held with E (the box was unlocked by eval, the hold is real input); outdoor walk at the Dock spawn facing open ground (heading 165, 60 m clear). Perf: same session, same view, old rig instantiated at runtime under the sway pivot (new hidden) instead of a stash-and-rebuild. | `perf_here.sh`, security office, beam on, 8 samples x2: new hand gpu 9.3 / 9.6 ms, old rig (24.0 k tris vs 15.0 k) gpu 10.5 / 8.4 ms; cpu 28-29 ms in both (hidden editor bound). No measurable difference; the spread between runs (about 2 ms) is larger than any change. |
| Hold lowering fires spuriously; scripted toggles kick the hand (low) | Hold now also needs `CretaceaInput.InteractHeld`. `SetBeamEnabled` arms the click kick only when `!silent`. | `m_hold` (lowers only while E is held). |

Not done / rough edges after this round:
- No finger or thumb animation (no bones in the scan); the switch press is a stronger whole-grip squeeze and nod.
- The glove's sculpted creases still read busier than a real leather glove; fingers are not separately readable.
- Pressed flush against a wall the torch now tucks almost out of the frame (the light stays correct).
- The office "after" still grants the torch by eval for the same-view comparison with the old still (the in-flow
  version is `m_pickup_equip`).
- Process: once I saved `FlashlightController.cs` while Play mode was still running (the route test had left it
  running); that reload threw 8 Input System errors in that session. I stopped Play, recompiled and re-ran
  everything in fresh sessions (0 errors), and re-ran `verify_playmode_doors` and the route test afterwards.

Fix-round checks: compile 0 errors / 0 warnings; scene build with NavMesh `warnings: []`, both NavMeshes baked;
`verify_playmode_doors` passed (flashlight OK, both NavMeshes valid, no missing documents;
`Tools/agent/r5vm/fix/doors.log`); `route_test.py` 45 / 45 in 256 s with no new console errors
(`Tools/agent/r5vm/fix/route.log`); fresh Play session with all clips: 0 console errors (one HDRP
"ScriptableRuntimeReflectionSystemSettings assigned more than once" warning on Play start, an engine message that
comes before any of this code runs).

Fix-round files: `ViewmodelMotion.cs`, `FlashlightController.cs` (motion helpers, plus the one-line `!silent` gate on
the click kick in `SetBeamEnabled`), new `PredatorProximityProbe.cs`, `LookMaterials.cs` (flashlight section: rubber,
retune, `ViewmodelPickupModel` / `ViewmodelPickupMaterials`), `CretaceaWorldAssembler.cs` (pose roll, desk swap),
`flashlight_hand_vm.fbx`, new `flashlight_pickup_vm.fbx`, `M_VM_*.mat` + new `M_VM_Rubber.mat`, `Tools/agent/r5vm/`
(`build_vm.py`, `clip2.py`, `import_pickup.cs`, `fix/*` probes).
