# Round 4 hand-in: Tyrant Valley at night (`r4/valley`)

All views are Play-mode game views from the Paddock checkpoint (blackout storm), captured with
`Tools/agent/r4v_caps.sh` (same poses before and after). Measurements: `Tools/agent/vstats.py` (luma outside the
objective box and the caption band). Captures and pairs: `Screenshots/handin_r4/` (see `INDEX.md`).
Take-over note: the previous engineer left no source edits (only regenerated `.mat`/`.meta` files and two
"before" captures); everything below is new in this branch.

## 1. Major: the T. rex reveal shows no T. rex: fixed
- `StoryDirector.TrexStreamRevealRoutine` now stages the crossing: unless the animal is already in the player's
  view within 90 m, it is placed on the road ~46 m ahead of the trigger (174, 4) and walks at its stride speed
  (2.6 m/s) across the open gap straight down Service Road 3 (clear sky between the two central groves, bearings
  85 to 69 degrees from the trigger) to the far verge (170, 16). The three original strikes (0, 0.45, 2.05 s; the
  VO line at 0.45 s) are kept and still come from behind the animal. At 4.6 s it stops, turns its head to the
  player, and a flickering three-stroke strike (4.9, 5.16, 5.46 s) holds it in light; then it walks into the grass
  to (166, 40), patrol point 0 is set and the normal AI takes over (same 9-point loop; the stealth section is
  unchanged). While scripted it does not perceive the player; after release the player is behind it and outside its
  150-degree cone. The routine bails out cleanly if the animal is despawned or forced into another state.
  The patrol array is built by a new helper `BuildValleyTrexPatrol()` (same points, same start indices).
- New `Assets/Scripts/AI/TrexMoonRim.cs` (added by `TrexAI.Start`): a shadowless 1700 cd spot 11 m toward the
  moon (azimuth 115, elevation 29) aimed at the body, on its own light layer (bit 9) that only the animal's
  renderers listen to, so the ground never shows a moving pool. Night presets only. It also raises the eyes'
  retro-reflection (`DinoEyeShine.RetroGain`, new public property) to 4 so the torch finds them at 45 m.
- Proof: `08_after_18_reveal_route` (route_test shot 18, final build: silhouette against the sky in the gap; this run
  missed the strike, capture latency varies 0.1-0.4 s), `08b` (the same beat on the 2.05 s strike, first staging build),
  `09_after_18_reveal_hold` (stopped and facing the player: moon rim on the hide, amber eyeshine in the torch) vs
  `07_before_18_reveal` (empty road). `route_test.py` shot 18 now waits 1.9 s (lands on the 2.05 s strike).
- Not done: a capture of the stream itself reading between strikes (the stream is ~80 m out behind the animal; the
  bridge flashers mark the crossing, see 2). The stream mist band over it is the lead's (minor 4b).

## 2. Major: nothing bright between strikes: improved, target partly met
New `Assets/Editor/WorldBuilder/ValleyNightDressing.cs` (one hook line in `CretaceaWorldAssembler` after Zone 2):
- Aviation obstruction mast (23 m, galvanised two-section pole, rungs, conduit, battery cabinet, solar panel)
  outside Substation 3's NW fence corner (247, 21): two steady red lamps (top and 12 m), 0.3 m lenses at 18 nits,
  900 cd red point lights with volumetric 1.2 for a small halo, no shadows. Straight down the road from the valley
  entry (x ~660 in the yaw-78 view, above the treeline gap).
- Amber battery flashers (post, hazard board, battery box, 18-nit lens, 120 cd soft pulse) on both kerbs at the
  west end of the stream bridge (x 189.5): the stream crossing reads as a goal 60 m out.
- Retro-reflective delineator posts every 13 m on both verges from the gate to x 186 (amber north, white south)
  and four large curve chevrons (0.75 x 0.9 m) on the outside of the right-hand bend at (160, 8). All driven by
  the new `Assets/Scripts/Rendering/TorchReflectorGroup.cs`: one component, emission = flashlight lux at the
  reflector x 4.5 x facing, capped at 18 nits, 0.25 nits idle; no per-post Update, MaterialPropertyBlock only.
- Lamp table: new `Assets/Editor/LookPass/LookLights.Valley.cs` (`ValleyRules`), applied by the builder through
  `LookLights.ApplyValleyRules` (lights are named `StatusLight_Valley*` so the fixtures step leaves my heads alone).
- Numbers (`18` pose, torch on, between strikes): >150/255 0.00 % -> 0.15 %, >100 0.06 % -> 0.32 %,
  p99.9 96 -> 237 (`01`/`02`). Torch off (`13`/`14`): p99.9 113 -> 116, the red beacon and flashers are visible
  but red/amber lenses at signal luminance cannot reach luma 150 (pure red maxes at luma 54). The brief's 0.3 %
  above 150 is NOT reached; something above 120 in every valley frame is.

## 3. Major: the valley road looks dry: fixed
`LookWeather.PlaceValleyRoadWater` (called at the end of `PlacePuddles`; nothing else in the file changed):
- One puddle per 15-25 m stretch along the Service Road 3 polyline from the gate to the stream bank (x 191), in a
  wheel rut (±0.95 m) at the lowest of 18 nearby rut points, 2.2-4.8 m long, laid along the road, trying every low
  spot and a smaller size before giving up (5 placed). Atlas puddle material, so they mirror the flash like the rest.
- Two dark glossy rut strips (smoothness 0.8, alpha 0.6, generated soft mask `Assets/Art/VFX/wx_rut_mask_v2.png`,
  material `MAT_WetRutDecal`) in 9.2 m overlapping pieces the whole way (24 decals). Named `WxPuddleRut_*` so
  `WetnessDriver` fades them in with the soak. Pebble field (terrain gravel 0.42) untouched.
- Proof: `03`/`04` (`valley_road_flashlight` at pitch +10: two wet tyre tracks run down the road, a puddle in the
  far rut) and `05`/`06` (held strike: the puddles mirror the lit sky, the ruts sheen).

## 4. Minors
- a. Shot 15: NOT fixed, timing left at 2.5 s. Without a NavMesh the raptor steers straight across the junction and
  is in frame 0.3 s after the trigger (`12`), gone by 0.6 s. With the NavMesh baked (as in every route_test run) the
  agent path from raptor_cross_a (64, 30) to _b (64, -6) leaves the junction: measured at (68.6, 35.2) at ~1 s and
  (78.9, 20.6) at ~1.6 s, and the 0.3 s shot in the route run showed an empty junction. No fixed delay catches it;
  the fix is in `StoryDirector.TriggerServicePathRaptorCrossing` (not my function): request below. Shot 18's wait
  is now 1.9 + 3.3 s (was 2.2 + 3), so the rest of the run keeps its timing.
- b. The flat near-white mist band with a straight top: it is the stream mist, `LookWeather.PlaceMist`:
  `StreamMist_*` LocalVolumetricFog boxes 22 x 2.2 m, mean free path 2 m (very dense), Max blending, fade only 0.85
  of the box height, so a 2.2 m slab with a flat lid lies along the stream (and the valley floor cells beside it).
  Not my file region: request below.
- c. Grass teal-mint under lightning: confirmed visually in `06` (report only): the lightning colour
  (0.78, 0.87, 1.0) in `AtmosphereController` on top of the storm's blue shadow lift.

## Performance (`perf_here.sh` at the valley entry, 93.6, -2.2, yaw 90)
before: gpu 5.0 ms, cpu 20.8 ms, 2.30 M tris; after: gpu 6.8 ms, cpu 22.5 ms, 2.25 M tris (editor batch mode,
noisy; a mid run measured gpu 5.9 / cpu 18.1). Added: 4 shadowless lights, ~70 small renderers, 29 decals,
one MonoBehaviour for all reflectors and one per T. rex.

## Files
- `Assets/Scripts/Story/StoryDirector.cs` (only `SpawnValleyTrex` patrol set-up -> `BuildValleyTrexPatrol`, the
  reveal constants/helpers directly above `TrexStreamRevealRoutine`, and that routine)
- `Assets/Scripts/AI/TrexMoonRim.cs` (new), `Assets/Scripts/AI/TrexAI.cs` (one line in `Start`),
  `Assets/Scripts/AI/DinoEyeShine.cs` (public `RetroGain` property)
- `Assets/Scripts/Rendering/TorchReflectorGroup.cs` (new)
- `Assets/Editor/WorldBuilder/ValleyNightDressing.cs` (new), `CretaceaWorldAssembler.cs` (one hook line)
- `Assets/Editor/LookPass/LookLights.Valley.cs` (new), `Assets/Editor/LookPass/LookWeather.cs` (valley road water)
- `Assets/Art/VFX/wx_rut_mask_v2.png`, `Assets/Settings/Rendering/MAT_WetRutDecal.mat`, palette emissives
  `MAT_Valley_*` (generated)
- `Tools/lookdev/route_test.py` (timing of shot 18 only)

## Requests for the lead
1. `LookLights.cs` `AllRules()`: add `foreach (Rule r in ValleyRules) yield return r;` so `look_pass` calibrates
   the four `StatusLight_Valley*` lamps itself and stops listing them as unmatched (they are calibrated by the
   builder today, with the same table).
2. Stream mist (`LookWeather.PlaceMist`, `StreamMist_*`): mean free path 2 m -> ~8 m, vertical fade over the whole
   2.2 m (positiveFade.y 1.0) and a little more noise tiling, so the band loses its flat top.
3. Lightning colour (0.78, 0.87, 1.0) -> about (0.88, 0.92, 1.0) and storm smhShadows blue 1.06 -> 1.02 (the teal
   grass under strikes, minor c).
4. If you want the full 0.3 % above 150 between strikes in `18`: a warm-white battery lantern in view (a 3000 K
   lamp on the obstruction mast's cabinet or at the bridge) would do it; I kept to signal colours and torch
   reflectors as the brief asked.
5. `TriggerServicePathRaptorCrossing`: force the direct (straight-line) scripted move for the crossing, e.g. a
   ScriptMoveTo option that skips the NavMesh path, or put an intermediate target at the junction (64, 14), so the
   raptor really crosses the lane in front of the player; then shot 15 can be taken at ~0.3-0.5 s.
6. No vegetation lane needed: the reveal gap (170-175, 0-20 seen from 128, 0) is clear.

## Verified / not verified
- Verified in captures: T. rex in the gap on a strike and holding with rim and eyeshine; delineators, chevrons,
  beacon and flashers; wet ruts and puddles in the torch and under a strike; raptor inside the junction at 0.3 s (without NavMesh only).
- `route_test.py`: 45/45 passed (final build with NavMesh, 261 s, no new console errors).
  `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid. 0 compile errors / warnings.
- Not verified: the reveal when the player enters the trigger from an unusual angle (the routine skips the staging
  if the animal is already visible within 90 m); the built player.
