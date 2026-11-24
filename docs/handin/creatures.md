# Hand-in: CREATURES AND PEOPLE (branch `look/creatures`)

> The **Fix round** section at the end supersedes the sections below where they differ (foot lock, eye masks, eyeshine
> 40 -> 8 nits, wet albedo 0.9 -> 0.82, Mesh LOD removed, controllers moved, StoryDirector scope).

Captures: `Screenshots/handin/` (16 numbered views + INDEX.md, 168 animation frames + contact sheets; the folder is git-ignored).

## 1. Animation (goal 1)

Measured first (edit-mode clip sampling + an edit-mode locomotion simulator that steps the Animator and the rig at 60 Hz,
generator kept in `Assets/Art/Materials/Dinosaurs/Tools~/skate.cs`):

- The authored clips are clean: lowest skinned vertex = 0.000 m in every clip, loops seamless (end-vs-start 0.0 deg),
  stance-foot speeds match the manifest stride speeds within 1 %.
- **The defects were in the runtime layer**, now fixed in `DinoProceduralRig.cs`:
  1. **Broken two-bone IK.** The old solver mixed cached lengths with a mis-signed swing: asked to move a foot 13 cm it
     missed by 21-26 cm, so every foot correction moved the foot the wrong way. Replaced with an analytic solver
     (0.0000 m error on all 12 dinosaur legs). The foot keeps its world rotation through the solve (toes stay flat).
  2. **Hover.** The IK lifted each foot to its bind-pose height + a 2 cm clearance. Trike/brachio bind feet sit 5-7 cm
     higher than their idle feet, so they floated. The IK now only corrects the difference between the real ground under
     each foot and the root's plane (planted height tracked per clip). Play-mode probe on flat ground: lowest vertex gap
     0.000 m (raptor, T. rex).
  3. **Foot locking.** The procedural quadruped walks drift (trike/brachio front feet move back at 0.95-1.6 m/s against a
     1.3 m/s body). Planted feet are now pinned in world space and the leg IK absorbs the drift.
     Simulated stance slip, before -> after: raptor walk 6 % -> 2 %, stalk 5 % -> 0 %; T. rex walk/stalk/run 1 % -> 0 %;
     brachio 33-35 % -> 0 %; trike 38-40 % -> 18-22 %.
  4. **Dislocated jaw on roars.** The roar/bite clips already open the jaw 35-44 deg and the procedural pulse added up to
     34 deg on top (~75 deg). The pulse now only adds what the clip is not doing. Play-mode probe during Roar_Massive:
     total jaw angle 42 deg (max 42).
  5. **Stiff tails on turns.** Tail now trails body yaw (under-damped spring, max 34 deg spread over the tail bones).
- **Playback speed vs. locomotion speed** (`DinoAnimatorBuilder`): stalk speeds are 0.68-0.77 x the walk stride, but the
  blend tree mixed 30 % idle in and the 0.72 floor stopped playback slowing down, so stalking raptors skated. The blend
  tree now holds a pure walk from 0.55 x stride, `minAnimSpeedScale` 0.55; trike/brachio run on their walk clip, so their
  `maxAnimSpeedScale` is raised to cover their run speed (2.54 / 1.53).
- **Builder bug:** rebuilding deletes/recreates `AC_<Species>.controller` with the same GUID; prefabs whose text came out
  byte-identical were not re-imported and kept a dangling controller (Animator with no controller = frozen bind pose
  until an editor restart). The builder now force-reimports each prefab after saving.
- Mesh LOD: `generateMeshLods` on the dinosaur FBX importers (5 levels inside the mesh, still one SkinnedMeshRenderer).

Remaining animation defects (cannot be fixed here):
- Walk-to-run blend at mid speeds (raptor 4 m/s, T. rex 4.8 m/s) still slips 20-44 % for a few frames while
  accelerating: the walk and run cycles have different lengths and `DinoConfig.EvaluateExpectedClipStrideSpeed` (not
  mine) assumes a linear blend. A sync'd 2D blend or matched cycle counts in Blender would fix it.
- Trike front feet: ~20 % residual slip (the lock releases when the clip's drift exceeds 28 % of leg length).
- Skin stretching on the raptor throat / T. rex limbs (manifest deformation audit 43-76) needs re-weighting in Blender.
- The procedural quadruped clips have short hind-foot stance (feet flat only ~15 % of the cycle): reads slightly
  "tip-toe" up close; needs re-authoring.

## 2. Hide (goal 2)

Derived maps (generator `Assets/Art/Materials/Dinosaurs/Tools~/` : dumpmesh.cs -> uvmaps.py -> hide.py -> detail.py):
- `<Species>_Hide_MaskMap.png` (2048): R metallic 0; G cavity/fold AO from multi-scale normal-map curvature + albedo
  valleys (mean 0.91-0.93, p5 0.65-0.74); B detail mask (0 on eyes/teeth/horn/claws); A smoothness with real variation
  (scales 0.25-0.35, folds up to 0.55-0.6, keratin 0.5, eyes 0.93).
- `<Species>_SSS_Mask.png`: skin 1, eyes/teeth/horn/claws 0 (keratin found by brightness/chroma on head/jaw/toe/hand
  texels; eyes located on each head from a texture-space side view and mirrored).
- `<Species>_EyeShine_Emissive.png`: eye-only emissive mask for the eyeshine.
- `Assets/Art/Materials/Dinosaurs/T_DinoScaleDetail_mask.png`: tileable 22-scale Voronoi detail map (HDRP detail layout),
  tiling 13-18 so one scale is ~1.1 cm on the raptor, ~3 cm on the big species.
- `BuildOrUpdateHdrpMaterial` binds all of it so a rebuild keeps it: smoothness/AO remap 0-1 (values live in the mask),
  metallic remap 0/0, specular occlusion from AO, detail scales 0.35/0.45/0.5, `_SubsurfaceMaskMap` + subsurface 0.55,
  thickness 0.85, emissive map = eye mask with colour black, `HDMaterial.ValidateMaterial`. SSS_DinoSkin stays bound.

## 3. Wet in the rain (goal 3) - `Assets/Scripts/AI/DinoWetness.cs` (on every dinosaur prefab)
MaterialPropertyBlock: smoothness floor 0 -> 0.38 and base colour x0.9 (sRGB) with `_GlobalWetness`; soaks in 4 s,
dries in 45 s; a 0.5 s upward raycast detects shelter. Verified in Play mode: storm outdoors 1.00 within 4 s; raptor
teleported into the security office stays 0.00; GoldenDay spawns 0.08.

## 4. Eyes in the dark (goal 4) - `Assets/Scripts/AI/DinoEyeShine.cs`
Eyeshine luminance = flashlight lux at the head (candela / d^2 x cone falloff) x 1.2 / pi x head-facing factor, capped at
40 nits, occlusion-tested (ignores the player's own capsule), amber (1.0, 0.78, 0.36), only on the eye texels.
Verified: 0 behind a wall (correct), 40 nits at 6.5 m in the beam, 20-29 nits on the T. rex at 10-14 m.

## 5. People in the prologue (goal 5)
- `cretacea build_character_prefabs` (in DinoAnimatorBuilder.cs) builds `Assets/Prefabs/Characters/PF_<name>.prefab` +
  `AC_<name>.controller` for park_guide and the 4 visitors: states Idle / LookAround / Phone / Clap / Cheer / Wave / Walk
  from the embedded clips, each looping by a self-transition (the FBX clips import without loop time and I did not touch
  their import settings). Model turned +90 deg (the Tripo rigs walk toward -X), scaled to 1.80/1.68/1.74/1.25/1.80 m from
  the idle pose, walk speed measured from the planted foot (0.66-1.06 m/s).
- `ParkVisitor.cs` (stand facing a target / stroll back and forth at the clip's own speed, ground raycast, no colliders)
  and `ParkVisitorCrowd.cs` (16 people: 8 strolling the facade z -38..-41, 5 at the viewing rail z -94, 3 in the gallery
  x 52.5 z -49..-55; Godot positions with z mirrored) on `Assets/Prefabs/Characters/Resources/PF_OpeningCrowd.prefab`.
- `StoryDirector.cs`: the opening instantiates `Resources/PF_OpeningCrowd` (StoryDirector's assembly does not reference
  Cretacea.AI, so it only instantiates/destroys a GameObject); `FinishOpeningCutscene` and `StopAllStoryRoutines`
  (chapter jumps) destroy it. Verified: 16 visitors during the opening, 0 after it.

## Files touched
- `Assets/Editor/DinoAnimatorBuilder.cs` (materials, playback limits, blend tree, prefab reimport fix, Mesh LOD, wetness/eyeshine components, character builder)
- `Assets/Scripts/AI/DinoProceduralRig.cs`; new `DinoWetness.cs`, `DinoEyeShine.cs`, `ParkVisitor.cs`, `ParkVisitorCrowd.cs`
- `Assets/Scripts/Story/StoryDirector.cs` (opening crowd spawn/despawn only)
- `Assets/Art/Materials/Dinosaurs/*` (4 materials, detail map, `Tools~/` generators), derived maps in `Assets/Art/Dinosaurs/*/Textures/`
- `Assets/Prefabs/Dinosaurs/*`, new `Assets/Prefabs/Characters/*`, `Assets/ScriptableObjects/AI/DinoConfig_*` (anim speed limits)
- Generated by my builder outside the listed folders: `Assets/Animations/Dinosaurs/AC_*.controller` (rebuilt as before)
  and `Assets/Art/Dinosaurs/*/<Species>.fbx.meta` (2 lines each: Mesh LOD on). Please confirm these are OK to keep.
- No lamp rules added.

## Requests for the lead
1. **Flashlight on wet hide at 3 m reads pale cream** (09): the raptor's light belly + wet sheen at ~800 lux. Not
   clipped (0.3 % pixels), texture visible, but a softer near-field falloff or a slightly lower hot spot would help.
2. **StormRainOcclusionController NRE** (`Assets/Scripts/Rendering/StormRainOcclusionController.cs:190`,
   VolumeComponent.Override) logs after every script reload in edit mode. Pre-existing, not from this package.
3. Opening trikes are spawned 7 m apart along a 7.6 m body: trike_0's tail overlaps trike_1 (StoryDirector
   `SpawnOpeningShowcaseDinos`, outside my part). Spacing 10 m would fix it.
4. Optional: let Cretacea.Story reference Cretacea.AI if you prefer a typed call over `Resources.Load("PF_OpeningCrowd")`.
5. A yard raptor in OldYard reported wetness 0 in the storm (the shelter probe found something overhead: a canopy or
   roof collider). Not checked visually.

## Not verified / rough edges
- No frame-by-frame look at the runtime rig on slopes (verified by numbers on flat ground and in the meadow: mean low
  vertex gap -0.01..-0.05 m on the sloped meadow, up to -0.11 m for one trike toe on a slope).
- Eyeshine judged on one 8.5 m view (clear amber glint) and numbers; in 08/09 the head is turned so the glint is small.
- Brachiosaurus eye position estimated (r 5 cm); never seen up close.
- Detail-map tiling follows the atlas UVs, so scale direction changes across UV islands (subtle at 0.45 normal scale).
- 60 fps not measured (16 visitors are 5 skinned meshes of ~40 bones, CullUpdateTransforms; dinosaurs now have Mesh LOD).
- Console: no errors or warnings from the Play-mode smoke test (Intro, Night, Corridor, ServicePaths, Substation, OldYard,
  Escape). The ~190 "Failed to create agent" warnings in the editor log came from my edit-mode test scripts
  instantiating prefabs off the NavMesh, not from the game.

---

# Fix round (art-director review of 1aeb8a1)

Captures: `Screenshots/handin/01-16_*.png` + `INDEX.md` (round-1 files moved to `_round1/`); Play-mode frame sequences of the runtime rig in
`Screenshots/handin/play/` (`<Species>_<clip>_<side|front|top>_NN.png`, 8 frames per view per clip, contact sheets
`sheet_<Species>_<clip>.jpg`; 27 clips/scenarios). Generator: `Assets/Art/Materials/Dinosaurs/Tools~/playcap.cs` + `playcap.py`
(Play mode, real scene: Animator + `DinoProceduralRig.Tick` stepped at 60 Hz with the AI/NavMeshAgent off, skinning forced per render).

## Findings and what I did

| # | Finding (severity) | Verdict | What changed | Evidence |
|---|---|---|---|---|
| 1 | Foot lock pops feet on fast turns (high) | **confirmed, fixed** (and a second cause found) | `DinoProceduralRig`: `LimbIkChain.NeedsLift`; a drift/turn no longer releases a planted foot that then re-locks at the animated spot next frame. While planted the pin is held on a **soft leash** (past 28 % of leg length it eases toward the animated foot, hard limit 40 %); only a warp (> 55 %) releases it, then `NeedsLift` blocks re-locking until the next lift and the pin slides out (`MoveTowards` 2 m/s x scale) while the weight fades over 0.2 s. No new locks while the body yaws > 90 deg/s (yaw rate computed at the top of `Tick`). A landing foot re-locks only once the old pin has faded. Lift-off fade 0.10 s, longer when the pin is far from the animated foot (visible catch-up <= max(3.5, 1.5 x body speed) m/s). **Second cause:** the planted/lifted thresholds were 0.05/0.09 x scale, but the trike's feet lift only 14-17 cm and the brachiosaur's 8-17 cm (below even the "planted" threshold for the brachio), so their feet stayed locked 99 % of the time and were released by drift, which caused the 45-128 cm pops on straight walks. The thresholds now follow each foot's measured lift height in the current clip (`MaxLift`, slow decay). | Edit-mode sims, 60 Hz, flat ground. `Tools~/skate_turn.cs` (straight 1.5 s, 180 deg turn at the species turn rate and x1.35, straight 1.5 s). **Largest one-frame jump of a planted foot, before -> after:** raptor 20-28 cm -> 0.7-5.1 cm; T. rex 56-70 cm -> 2.5-4.3 cm; trike 34-52 cm -> 2.9-6.1 cm; brachio 112-131 cm -> 5.6-7.5 cm. `Tools~/skate.cs` (straight lines) stance slip at stalk/walk/mid(walk-run blend)/run, 1aeb8a1 -> now: raptor 0/2/21/9 % -> 0/2/6/9 %; T. rex 0/0/44/0 % -> 1/0/6/0 %; trike 21/22/20/18 % -> 0/0/0/0 %; brachio 0/0/0/0 % -> 7/3/3/11 % (its old 0 % came from feet that never unlocked and popped 112-131 cm instead). Play mode: `play/sheet_*_turn.jpg` for all four species (side + top), 16. |
| 2 | Raptor = blown-out cream ghost in the flashlight (high) | **partly fixed**, rest is the flashlight near field | `DinoWetness` keeps the material's `_BaseColor` and multiplies it (was overwritten with grey); wet albedo x0.82 (was 0.9). `hide.py` writes `<Species>_Hide_Albedo.png` (linear luminance soft-capped near 0.25: `(0.18 + 0.07 tanh((L-0.18)/0.07))/L` above 0.18; keratin/eyes keep up to ~0.35), bound as `_BaseColorMap` by `BuildOrUpdateHdrpMaterial`. Albedo p99 (linear): raptor 0.45 -> 0.28, trike 0.52 -> 0.34, brachio 0.44 -> 0.25, T. rex 0.33 -> 0.25. | Same 3 m frame, same pose, storm, flashlight 7200 cd (800 lux at 3 m): body pixels > 230/255 **18.8 % -> 8.1 %**, p90 244 -> 223 (10). The chest top still clips: 800 lux on a 0.25 albedo is ~2-6 x display white at storm EV 3-4.7, so it needs the near-field flashlight change (request 1). |
| 3 | Raptor/brachio eye treatment covers one eye (high) | **confirmed, fixed** | `hide.py` finds each eye per side: iris colour rule near the mirrored guess (raptor yellow iris R>0.5 G>0.4 B<0.3, brachio black eye), else projects the guess onto that side's head surface. Smooth wet eyeball (0.93) inside 0.7-0.9 x radius, eyeshine mask only inside 0.3-0.45 x radius (pupil), AO 1, SSS 0. | Eye centres now: raptor +X (0.077,-1.349,1.923), -X (-0.090,-1.390,1.914) (the critic's measurement); brachio +X (0.372,-9.079,15.859), -X (-0.259,-9.043,15.859) (head is 12 cm off-axis). Eye texels +X/-X: raptor 412/356 (was 180/0), brachio 84/124 (was n/0). 11: both raptor eyes glint in the storm. |
| 4 | Eyeshine clips white, glows on the eyelid, ignores the cookie (medium) | **confirmed, fixed** | `DinoEyeShine`: retroGain 1.2 -> 0.6, maxNits 40 -> **8** (at the suggested 15 the 6.5 m glint still showed a white core at storm EV ~3), cone `r = angle/(spotAngle/2)`: 1 inside 0.3, smooth to 0.12 at the edge, 0 outside; pupil-only mask; the flashlight spot is cached once and re-queried every 2 s only while missing (no per-second `GetComponentsInChildren`). | 11 (both eyes, small amber points), 09 (6.5 m), 13 (T. rex 11 m: 5.2 nits). |
| 5 | Strollers walk through each other in shot 2 (medium) | **confirmed, fixed** | `ParkVisitorCrowd.Layout`: westbound on z -38.2 / -39.1, eastbound on z -40.2 / -41.1 (>= 0.9 m between directions), >= 25 m between starts on a lane. | 01, 02 (shot 2 at 13.3 s and 14.9 s): separate silhouettes. |
| 6 | Shot 3 shows only a cut-off arm (medium) | **confirmed, fixed** | Rail group inside the camera's travel (x -44 -> -41), 2.5-3.6 m ahead: woman -42.0, guide -42.8, teen -43.6 at the rail (z -94.4), man filming -41.6 and child -41.0 just in front. | 02 (shot 3 start / mid / end): 3-5 people framed for the whole glide; at the very end two heads touch the bottom edge. |
| 7 | Visitor walk re-blends every 2.29 s (medium) | **confirmed, fixed** | `BuildCharacterPrefab` copies each chosen clip to `Assets/Prefabs/Characters/Clips/<name>_<state>.anim` with loop time on (loop pose for Walk) and uses the copies; the self-transitions are gone (FBX import settings untouched). | Controllers have 7 states, 0 transitions; 35 looping clip copies. |
| 8 | Visitor materials plastic (medium) | **confirmed, fixed** | New `Tools~/charmask.py` -> `Assets/Art/Materials/Characters/T_<name>_Mask_Look.png` (curvature + albedo-valley AO, mean 0.95, p5 0.78-0.82; skin ~0.40, cloth 0.12-0.30, hair 0.30; metallic 0), bound by the new `BuildOrUpdateCharacterMaterial` (remap 0-1, metallic remap 0/0, specular occlusion from AO; without the map it falls back to the critic's remaps). | 06-08: matte cloth, soft skin sheen. |
| 9 | Opening trikes overlap (medium) | **fixed** (one line, actor spawning of the opening) | `StoryDirector.SpawnOpeningShowcaseDinos`: `trikeBase + (i*10, 0, -i*6)` (~11.7 m apart), ground sampling kept. | 02 (shot 3 end): two trikes apart; no overlap. |
| 10 | Undisclosed T. rex AI value change (medium) | **confirmed, reverted** | The builder now copies every non-reference serialized value of the previous prefab's AI component onto the re-added one (`CopyTunedAiValues`). PF_Tyrannosaurus restored from b234ad4 and rebuilt: `substationOrbitRadius 26.5`, `fenceClearanceProbeDistance 3.2` again. `fenceBashStandoffMeters 7.4` / `substationFenceHalfExtents (18,17)` are now written explicitly, but they equal the code defaults the game already used (the old prefab had no such fields), so behaviour is unchanged. | `git diff b234ad4 -- Assets/Prefabs/Dinosaurs/PF_Tyrannosaurus.prefab`: TrexAI only gains those two explicit default fields. |
| 11 | Crowd strolls through "Closing time" and the night (low) | **fixed** | `StoryDirector.OpeningCutsceneRoutine` despawns the crowd right before the "Closing time" title card (at the cut). `ParkVisitorCrowd` also removes itself as soon as the atmosphere leaves GoldenDay or the cutscene ends (skip, chapter jump), so the extra despawn calls I had added to `StopAllStoryRoutines` / `FinishOpeningCutscene` are reverted. | Play mode: 16 visitors at 15.5 s, "crowd gone, preset DuskTransition" at 40.5 s; 05. |
| 12 | Raptor teeth read as black spikes (low) | **partly a lighting effect** | `hide.py`: AO on teeth/claws floored at 0.85 (no cavity darkening between teeth) and keratin keeps up to ~0.35 albedo. | 14: the lit lower tooth row reads as bone; the upper row sits in the snout's own shadow with the sun behind the head, which is lighting, not material (teeth albedo lum 0.59 sRGB, AO mean 0.95/0.91 on head/jaw keratin). |

Ownership fixes (the review's out-of-scope list):
- `Assets/Animations/Dinosaurs/AC_*.controller`: **reverted** to b234ad4. The builder now writes the dinosaur controllers to
  `Assets/Prefabs/Dinosaurs/Controllers/AC_<Species>.controller` (inside my folder; nothing else references the old ones).
- `Assets/Art/Dinosaurs/*/<Species>.fbx.meta`: **reverted** (Mesh LOD off again) and the builder no longer sets Mesh LOD.
  Goal 6 is now a request (2 below).
- `StoryDirector.cs`: only the opening's actor spawning remains: the crowd spawn helper, the despawn before "Closing time",
  and the trike spacing. `StopAllStoryRoutines` / `FinishOpeningCutscene` are back to b234ad4.
- `PF_Tyrannosaurus.prefab`: AI values restored (finding 10).

Goal notes:
- Goal 1: Play-mode side + front sequences now exist for all 21 clips plus walk/run/turn/look scenarios (raptor 10,
  T. rex 9, trike 5, brachio 4). Looked at: raptor turn (feet planted, tail swings with the turn), raptor look-at sweep
  -150..150 deg (smooth, no snap), T. rex roar front (head rears, jaw ~42 deg, not dislocated), T. rex turn, brachio walk
  (feet on the ground, no hover). Look-at code checked: exponential smoothing on weight and angles, the > 145 deg case fades
  to 0 instead of flipping across +/-180.
- Goal 3: 12 = same frame, same pose (time frozen), dry vs wet: mean RGB 111/102/90 -> 87/81/71, p99 237 -> 227, plus the
  smoothness sheen. OldYard `yard_raptor_a` (450, 1, 16) reads 0 because it stands under a `Ceiling` collider 6.2 m above
  it (a roofed building): correct "dries indoors"; `yard_raptor_b` in the open reads 1.00.

## Requests for the lead (fix round)
1. Flashlight near field (`FlashlightController`, not mine): 7200 cd gives 800 lux at 3 m, which still clips the top of a
   0.25-albedo chest at storm EV 3-4.7 (10). A near-field falloff (e.g. a softer cookie centre or intensity easing in
   under ~4 m) would finish finding 2.
2. Dinosaur LODs: set `generateMeshLods: 1`, `maximumMeshLod: 4` on the four dinosaur FBX importers
   (`Assets/Art/Dinosaurs/*/<Species>.fbx.meta`, 2 lines each); one SkinnedMeshRenderer stays, the rebuild keeps it.
3. Please confirm the new controller location `Assets/Prefabs/Dinosaurs/Controllers/` (the handover controllers in
   `Assets/Animations/Dinosaurs/` are now unused and can be deleted by their owner).

## Still not fixed / not verified (fix round)
- Turning still slides planted feet at roughly the body's rotation speed x foot radius (raptor 1.5-2.3 m/s, T. rex 2.6-3.5 m/s
  during a full-rate 180 deg turn): without turn-in-place stepping clips a pinned foot has to give way. It eases (no pops).
- Walk-run blend at mid speeds: steady-state slip is now 6 % (raptor 4.05 m/s, T. rex 4.8 m/s), but the cycles still have
  different lengths, so a few frames while accelerating can slip more. Needs synced cycles in the clips or a sync'd blend
  in `DinoConfig.EvaluateExpectedClipStrideSpeed` (not mine).
- The numbers in finding 1 come from the edit-mode simulators; in Play mode I looked at frame sequences (8 frames per
  view), not a video, so a single-frame pop between samples would not show in the captures.
- Hide skin stretching (raptor throat, T. rex limbs) and the quadruped "tip-toe" hind stance need re-authoring in Blender.
- The flashlight hot spot still clips the raptor's chest at 3 m (request 1).
- 60 fps not measured.

## Files touched (fix round)
- `Assets/Scripts/AI/DinoProceduralRig.cs` (foot lock: NeedsLift, soft leash, adaptive lift thresholds, bounded lift-off fade, yaw-rate gate)
- `Assets/Scripts/AI/DinoWetness.cs` (keeps the material tint, wet x0.82), `DinoEyeShine.cs` (8 nits, gain 0.6, cookie-aware cone, cached light)
- `Assets/Scripts/AI/ParkVisitorCrowd.cs` (lanes, rail positions, self-removal at dusk / cutscene end)
- `Assets/Scripts/Story/StoryDirector.cs` (opening only: despawn before "Closing time", trike spacing; my extra despawns elsewhere reverted)
- `Assets/Editor/DinoAnimatorBuilder.cs` (controllers to `Assets/Prefabs/Dinosaurs/Controllers`, no Mesh LOD, derived albedo, AI value copy, looping visitor clips, visitor materials)
- `Assets/Art/Materials/Dinosaurs/Tools~/`: `hide.py` (per-side eyes, pupil mask, albedo cap, keratin AO), new `charmask.py`, `skate_turn.cs`, `playcap.cs`, `playcap.py`; `meshlib.py`/`uvmaps.py` f-string path bug fixed
- Regenerated: `Assets/Art/Dinosaurs/*/Textures/<Species>_{Hide_MaskMap,SSS_Mask,EyeShine_Emissive,Hide_Albedo}.png`,
  `Assets/Art/Materials/Dinosaurs/MAT_*.mat`, `Assets/Art/Materials/Characters/MAT_*.mat` + new `T_<name>_Mask_Look.png`,
  `Assets/Prefabs/Dinosaurs/*` (+ `Controllers/`), `Assets/Prefabs/Characters/*` (+ `Clips/`)
- Reverted to b234ad4: `Assets/Animations/Dinosaurs/AC_*.controller`, `Assets/Art/Dinosaurs/*/<Species>.fbx.meta`
- No lamp rules added. Final `build_all_scenes` with NavMesh: OK (raptor + T. rex NavMesh baked, no warnings); Play-mode smoke
  test Intro / Night / Corridor / ServicePaths / Substation / OldYard / Escape: 0 console errors, 0 warnings.

