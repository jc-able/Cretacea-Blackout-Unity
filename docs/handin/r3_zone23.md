# Round 3 hand-in: zone23 (service paths, substation lamp post, aux flood head, containers, yard slab, coast road)

Branch `r3/zone23` on base `5c57ee7`. Captures: `Screenshots/handin_r3/` (16 files, `INDEX.md`). Every claim marked
**verified** was checked in a capture I looked at.

## Results
- Final full rebuild with NavMesh: both NavMeshes baked, build warnings `[]`.
- `route_test.py`: **45/45** (final build). The baseline run on the base commit in this clone was 44/45 (ch4 player fell
  through the terrain at (128, -663, 0)); that baseline build had no NavMesh baked. The final run passes that check.
- `verify_playmode_doors`: **18/18 doors, 5/5 documents, both NavMeshes valid**.
- Console: 0 errors. Recompile: 0 errors, 0 warnings. The one console warning after stopping Play mode is HDRP's
  "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once". It does not come from these changes.
- Perf (`perf_here.sh`, editor Play mode, 8 samples, GPU median): before paths15 6.8 / substation 5.3 / coast 5.2 ms.
  First after-run: 6.6 / 9.9 / 6.4 ms. The substation rise came from 369 new flood-head renderers, so I merged each
  head (lens cells into one paired renderer, body parts into one mesh per material) and shortened the beacon range.
  Final after-run: 5.4 / 4.7 / 3.8 ms. Other agents were running on the machine, so these numbers swing ±2 ms between
  runs. Read them as "no visible regression", not as a measured speed-up.

## What changed, per goal
1. **Service path canyon (major): verified** (01-04). In `Zone2_ServicePathsBuilder.cs` (`PanelWall` with the new
   `WallStyle.Detail`):
   - Low panels: 2-3 panels per wall (explicit table `LowPanelTable`, chosen so views 14, 15, 16 and the diagonal each
     get one) are a 2.2 m wall with a cap, a chain-link panel in a galvanised tube frame up to 5 m, and barbed wire on
     arms. The jungle shows through. The invisible full-height wall collider is unchanged, and the coping is split per
     full-height run. Panels with wall fixtures (the three battery bulkheads, the blood smear) stay full height. Seed 1
     (dock approach) stays closed. On seed 7 (the fallen palm) only the end panel at the Branch 2 mouth is low.
   - On every panel: form-tie holes (3x2 or 3x4), lifting anchors, a weep hole, an algae line under the coping, and
     ferns rooted at the kerb.
   - Per wall: a conduit at 1.45-1.8 m with saddle clamps, junction boxes and ground drops. Even seeds get a cable tray
     at 3.7 m with cables and brackets; odd seeds get downpipes with scuppers, shoes and stain decals. Each wall also
     gets one sign (staff only, Paddock 4, high voltage, Talon Ridge, Service Road 3) and barbed wire on the full-height
     coping.
   - All of it is collider-free. Primitives are merged per wall (`R3Detail`).
2. **Substation post lamp (major): verified** (05/06; also `Screenshots/r3_a/s2_postlamp_close.png`). The
   `lamp_bulkhead` lantern turns to face the compound and its wall plate sits on the pole face (the plate is at model
   z -0.146; I measured the mesh). Two U-bolt clamp bands, a pole cap, a conduit down the pole and a junction box were
   added. The ring is gone. The glow is now a frosted sleeve (r 7 cm) between the glass and the cage bars, so the cage
   reads lit. The light sits at the cage centre. The name stays `Light_SubPostLamp_i`, so the fixture pass skips it.
3. **Aux flood head (major): verified** (07-09). New `Zone23Dressing.FloodHead` builds an LED area flood: housing,
   bezel, 4x3 LED cells at **9 nits** behind a dark reflector grid, visor, 11 back fins, U-yoke with pivot bolts, and
   a cable gland. Each mast gets a galvanised crossarm with **two heads** (so several floods snap on in view), one lamp
   per head, aimed at the pad (±4 m spread, away from the fences). The lights are renamed `Light_SubMastFlood_m`
   (shadows, as before) and `Light_SubMastFloodB_m` (no shadow, no bounce). The new names are not a `LookFixtures`
   family, so the white-card head is no longer built. Lamp rules: 7000 cd each (14000 per mast, as before), 5000 K,
   **volumetric 0.9** (was 1.8).
4. **Shipping containers (major): verified** (10-12). `BuildContainer` now uses trapezoid-corrugated sheets
   (`Zone23Dressing.CorrugatedSheet`, ISO profile, 0.278 m pitch, 36 mm deep, flat-shaded) on both sides, the blind
   end and the two door leaves (22 mm pressings), with hinge blocks. The gameplay box keeps its collider but is not
   drawn; a slimmer core carries the roof. The box ribs are gone. The roof-rust decal is now 0.2 m deep with angle fade
   25-50°, so it no longer runs down the sides.
5. **Yard slab dry in the downpour (major): verified** (13/14). Root cause: the 3 mm cast-bay overlays were merged
   into one mesh spanning the whole yard. `WetnessDriver` probes roof cover once at the bounds centre, which landed
   under the workshop roof, so all bays stayed dry and matte. Now the bays stay separate renderers (167 bays, all get
   the wet-floor clone; checked at runtime) with their own ground-only materials (`MAT_Z23_YardPavingDark/Light`).
   Four standing-water decals sit at the entry where they mirror the workshop wall lamp. `DockPlatform` uses the yard
   slab material instead of the board-form wall material.
6. **Coast road shows no coast (major): partly done, verified** (15/16). Zone 4 builds the two fences after zones 2-3,
   so `Zone23Dressing.OpenCoastRoad` runs from `LookRoofs.Apply` (my file; its root is `LookRoof_CoastRoadOpen`,
   which the pass clears and rebuilds). It does three things:
   - The fence renderers are hidden. The colliders stay, so the run is still bounded at 2.2 m.
   - A galvanised W-beam guard rail with blockouts, posts every 2 m and a 1.1 m hand rail lines both sides, so the view
     opens.
   - A rotating coastal navigation light stands on a 5.5 m mast at the cliff edge (`Light_EvacNavBeacon_Coast`,
     AuxYard, 26000 cd, 9° beam, volumetric 1.4, range 90 m).

   The assembly-point light box is now 1.15 m on two posts with a hood (was 0.7 m). **Not achieved: the sea horizon.**
   From the road the sea is 41 m below and 80+ m east, hidden by the road edge, and the storm fog (110 m mean free path,
   the lead's) merges sea and sky. In frame 16 the nav lantern shows as a point at the road end; I did not capture the
   beam mid-sweep.
7. **Minors**:
   - The overturned jeep sits on its lowest corner, sunk 0.18 m (code; not re-captured).
   - `Light_ServiceGate_Beacon` volumetric 0.95 → 0.40 through a zone rule (not re-captured; the 16 frame looks the same
     at this size).

## Files touched
- `Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs`: wall detail, low panels, paving materials, jeep seat
- `Assets/Editor/WorldBuilder/Zone3_SubstationAndYardBuilder.cs`: post lamp, flood masts, containers, yard bays and
  puddles, dock platform material
- `Assets/Editor/WorldBuilder/Zone23Dressing.cs`: `CorrugatedSheet/Panel`, `FloodHead`, `FloodLens`, `OpenCoastRoad`,
  bigger assembly sign
- `Assets/Editor/LookPass/LookLights.Zone23.cs`: rules `^Light_SubMastFloodB`, `^Light_SubMastFlood` (replaces
  `^Light_AuxSubFlood`), `^Light_ServiceGate_Beacon`
- `Assets/Editor/LookPass/LookRoofs.cs`: calls `OpenCoastRoad` at the end of `Apply`
- New materials: `MAT_Z23_FloodLensLed`, `MAT_Z23_NavLightLens`, `MAT_Z23_YardPavingDark`, `MAT_Z23_YardPavingLight`,
  `MAT_Z23_SignStaffOnlyPlate`
- Helpers: `Tools/agent/beats.py` (area beat replays with captures), `Tools/agent/perf_r3z.sh`

## Requests for the lead
- `LookLights.cs` core rule `^Light_AuxSubFlood` no longer matches anything (the lights were renamed). It can go.
- `LookFixtures.Build` still lists `AuxSubFlood` in its flood regex. It is harmless now; drop it if you like.
- Coast horizon: the storm fog over the sea (110 m mean free path, constant colour) hides the sea from the coast road.
  A thinner fog or a brighter horizon band toward the east in `NightStormBlackout` would let the sea read behind the
  new rails. A lit surf line under the cliff (zone 4 / `LookTerrainCoast`) would also help.
- Chain-link: the fence-topped low panels on the service path use `Zone23Dressing.ChainlinkSeeThrough()` (a copy of
  the palette material, rebuilt every build), so your new chain-link material carries over.
- `WetnessDriver`: a merged mesh's single roof probe at its bounds centre can still misclassify other zone-wide merges.
  I worked around it for the yard bays only.
- Zone 4 owner: the coast fences are now hidden from the look pass. If zone 4 replaces them with rails itself, delete
  `OpenCoastRoad`.

## Not verified / rough edges
- I did not watch the nav-light beam sweeping in motion, and did not capture it mid-sweep.
- The coping barbed wire (4.5 mm strands) barely shows at 720p. I did not check it in a close capture.
- The jeep re-seat and the gate-beacon haze change were not re-captured.
- Transfer-lever feedback (minor) and the green bollard collar (palette `EmissiveGreen`) are outside my files.
- The yard asphalt still looks wet in GoldenDay. That is palette `AsphaltWet`, as noted in review 2.

## Fix round (after the art director's review of commit 96906f5)
Captures: `Screenshots/handin_r3/` (refreshed set, `INDEX.md`; the old 01-16 stay as the "before" frames). Every
claim marked **verified** was checked in a capture I looked at. Frames are from the final build (NavMesh baked).

| Finding | What I did | Status |
|---|---|---|
| Service path still a canyon of blank slabs; detail sub-pixel (high) | Root cause of the invisible weathering: the palette decals run at `_DecalBlend` 0.5 on grime/moss textures whose alpha averages 14-24 %, i.e. ~5 % darkening on a lit wall. New `MAT_Z23_DecalRunoff` / `MAT_Z23_DecalAlgaeLine` (copies of `grime_leak_3` / `moss_patch` with alpha x2.6 / x1.9, `z23_*_strong.png`, full opacity). Every full panel gets a 0.6 m algae line at height-0.3 (opacity 0.9); ~70 % get a 0.9-1.6 x 3.4 m runoff streak from the coping (0.8). `PanelA`/`PanelB` tints pushed apart (A x1.12, B x0.85). One eye-level object every two panels under the conduit, fed by a drop from it: red hose-reel cabinet, yellow emergency phone or grey isolator with a red handle (skipped on the bulkhead / blood-smear panels). No wall packs (no new lights). | streaks + algae **verified** close (`close_wall`); 14/15/16 + diagonal re-shot. The near panels' algae sits above the frame top in 15 (the coping is at 5 m, camera level). |
| Coast road shows no coast (high) | Nav light rebuilt as the sea cue (see the nav-light row), beam strengthened to 60000 cd / 12 deg / volumetric 3.0 through its lamp rule. Requests below (zone 4 terrain lip / surf line, lead horizon band). | partly: see captures 27 / sweep; the sea itself is still fogged out |
| Flood heads clipped white grids under a halo; B lamps cast no shadow (medium) | `FloodLens` 9 -> 4 nits. Each of the 4x3 cells is now 3x3 LED dots (26 mm) over the dark housing face, between galvanised grid bars. Bezel 2.5 cm dark -> 4 cm galvanised. `^Light_SubMastFloodB` Shadow.None -> Shadow.Low (cached, 256 px). | **verified** in 21: the heads read as framed dot grids; upper-200-row mean 53.8 (was 59.9 in the old 08; 36.8 with the floods off in 20) |
| Wall-top fences vanish past 10 m; barbed wire sub-pixel (medium) | `LowPanelFence`: the panel renderer uses `p.ChainlinkFence` (the lead's material, not the far-fading copy), tube frame r 0.024 -> 0.038 plus two intermediate posts (r 0.03) and the mid rail; barbed strands r 0.004 -> 0.008 in DarkPaintedMetal, arms 0.016. Coping wire on full panels 0.0045 -> 0.008 dark. | **verified** in 15: the end-wall fence now shows posts, rails and mesh at ~12 m |
| Containers: black door ends in an unsettled frame (medium) | No code change needed. Re-shot after `jump_chapter Intro` + `SetPreset(GoldenDay, 0)` + 25 s settle. The same door-end box (x870-925, y300-460) measures p50 10 / p95 24 (was 1 / 2 in the storm-leftover frame); door leaves, lock bars and corner posts read in the door-end view. | **verified** (`GoldenDay_yard_eye`, `container_doorend`, `container_close`) |
| Post lamp blasts the pole (medium) | `Light_SubPostLamp_i` is a 150 deg spot aimed out and 20 deg down (`LookRotation(outDir - 0.36 up)`), rule spot angle 150. The pole is `DarkPaintedMetal`. The lantern is scaled at yaw 0 and then turned (`SetPositionAndRotation`), so every post gets the same scale and its plate sits on the pole. | **verified** in 20/21: the pole under the lantern stays dark |
| Nav light 140 deg/s, wobble, outside the table (medium) | Built level (`Euler(0,70,0)`), renamed `Light_CoastNavLantern` (no "Beacon": no fixture body), `rotationSpeedDegPerSec` 30 via SerializedObject, new Zone23 rule, applied in `OpenCoastRoad` with `LookLights.ApplyRuleTo` (it runs after Calibrate). | **verified** in Play mode by query: yaw 234 -> 311 deg in ~2.5 s, pitch 0, 26000 cd/vol 0.8 before the final retune (60000/3.0) |
| Assembly-sign posts through the lit face (low) | Posts moved behind the box to x 545.13 (box back 545.085 + r 0.04). | **verified** (`27b_overlook_sign`: clean face) |
| Jeep re-seat sampling misses the ends; radio moves (low) | Corners sampled in the jeep frame (`Euler(0,-24,0) * (+/-1.0, 0, +/-2.2)` plus the centre); the radio anchor and DocumentPickup keep the pre-round-3 seat height. 16 and 17 re-shot. | route test + doors check below |
| Perf where renderers were added (low) | See perf below. | |

### Fix-round results
- Final full rebuild with NavMesh: both NavMeshes baked, build warnings `[]`; recompile 0 errors / 0 warnings.
- `route_test.py`: **45/45**, "no new errors during the run" (`Tools/agent/route_fx.log`).
- `verify_playmode_doors`: **18/18 doors, 5/5 documents, both NavMeshes valid**, saved in `Tools/agent/doors_fx.log`.
- Console after the Play sessions: 0 errors; the one warning is HDRP's "ScriptableRuntimeReflectionSystemSettings.system
  is assigned more than once" (present before these changes).
- Perf (`Tools/agent/perf_fx.sh`, editor Play mode, GPU median of 8): paths15 6.8 ms, substation 6.1, coast road 4.5,
  yard 3.8 (new spot: the 167 separate yard bays). Earlier runs: base 6.8 / 5.3 / 5.2, first hand-in 5.4 / 4.7 / 3.8.
  Other agents share the GPU (±2 ms between runs). The substation now has four more cached Shadow.Low lamps and 864 LED
  dots merged into 8 meshes; I read the numbers as no visible regression, not as a measurement of those costs.

### Disputed / not done
- **Scope note on `OpenCoastRoad` hiding the zone-4 fences**: brief item 6 asks me to "lower or stop the fence toward the
  overlook", so I kept it. It only disables the fence renderers from my look-pass step; the zone-4 colliders stay (the
  run stays bounded). Zone 4 should own this: replace `CoastRoad_FenceN/S` by a 1.1 m rail collider or keep the
  colliders and delete `OpenCoastRoad`'s hiding loop. The 2.2 m collider above a 1.1 m rail is an invisible wall
  above the hand rail, the normal arrangement at a cliff edge; I did not move it (gameplay geometry).
- **Sea horizon**: still not visible from route 27 (storm fog merges sea and sky). The beam now reads as the sea cue
  (27_fx). Requests: zone 4: drop the terrain lip east of x 547 or add a lit surf line under the cliff; lead: a horizon
  band or thinner fog over the sea sector in `NightStormBlackout`.
- **Bay stencils** ("P4-0n") need new text textures; I used cabinets instead. No wall-pack lamps (no new lights).
- **Jungle tree row along the yard wall** (28_ending_2, not a finding in my list): not attempted.
- The radio document was not picked up by hand; the doors check finds 5/5 documents and the route test passes.

### Files touched in the fix round
`Zone2_ServicePathsBuilder.cs`, `Zone3_SubstationAndYardBuilder.cs`, `Zone23Dressing.cs`, `LookLights.Zone23.cs`;
new `Assets/Art/Decals/z23_runoff_strong.png`, `z23_algae_strong.png` (alpha-boosted copies of `grime_leak_3` /
`moss_patch`), new materials `MAT_Z23_DecalRunoff`, `MAT_Z23_DecalAlgaeLine`; helpers `Tools/agent/beats_fx.py`,
`perf_fx.sh`, `q_decal*.cs`, `q_nav.cs`.
