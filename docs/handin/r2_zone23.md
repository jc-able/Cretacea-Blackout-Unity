# Round 2 hand-in: zone23 (service paths, Talon pen, Substation 3, Old Yard, Exit E-0, coast road)

Branch `fix/zone23` from 8ed04fa. Captures: `Screenshots/handin_r2/` (8 before/after pairs, `INDEX.md`). Before set =
route_test at the base commit in this clone (`Screenshots/before/`), after set = route_test on the final build
(`Screenshots/after/`); close-ups and the E-0 sign at 20 m in `Screenshots/r2final/` (13b, 16b, 25b).
I also read the 05:45 addendum and `docs/review1/night_a_check.md` / `night_b_check.md`.

## Findings: what changed
1. **Chain-link (major)**: verified still opaque at base in 16, 20, 21, 27 (the lead's blended material improved
   distance, but kit spans are 8-14 cm double-sided boxes = two wire layers + edge stripes). Now:
   - `Zone23Dressing.ThinChainlinkBox`: the box keeps its collider, its renderer is off, and one plane goes through the
     middle (UVs in m/1.8, so the UV0 copy used on moving door leaves keeps 5.6 cm diamonds).
   - `UpgradeChainlinkSpan`: every kit fence span in zones 2-3 (Talon, branches, substation) plus the coast road fences
     (Zone 4 builds them later, so the upgrade runs from `LookRoofs.Apply`) gets round galvanised posts with caps,
     a round top rail, a mid rail on tall spans, a tension wire. Square kit posts/rails hidden (they had no colliders).
   - The panels use `MAT_Z23_ChainlinkSeeThrough`, a copy of `MAT_ChainlinkFence` rebuilt from it on every build with
     base-colour alpha 0.52 (the floodlit two-layer veil read as a tan board). If the lead prefers one material, the same
     alpha change can go into `WorldMaterialPalette.BuildChainlinkMaterial` and the copy can go.
   - Substation gate leaves swung ~115 deg instead of ~165 deg so they no longer overlap the fence (two layers).
   - E-0 gate leaves (kit `BuildFramedDoor`) thinned the same way; colliders and door motion unchanged.
2. **Glowing rods (major)**: conduits are galvanised steel (`BuildSubstationInteractStation`); the state shows on
   small LEDs riding on clamps every 2.4 m and on a new **mimic panel** on the switch cabinet's north face (6 LEDs:
   breakers 1-3, fuel, generator, transfer), all driven by the existing `SubstationStation.powerConduitRenderers`
   (dim fault red -> green). The diagonal transfer conduit now runs at floor level in troughs; trough beds are dark;
   the lever arms are red paint, not emissive.
3. **E-0 sign (major)**: new artwork `Assets/Art/UI/Signs/e0_exit_lightbox.png` (2048x352, opaque, solid DIN letters,
   escape-green field, straight-ahead arrow instead of the sideways one; generator `Tools/agent/make_e0_lightbox.py`).
   `LookSigns`: stem `e0_exit_lightbox` is back-lit, opaque (no alpha test), emission map = the artwork, trilinear +
   aniso 8 import, letters at 3.5 nits (`E0LetterNits`; measured: 16 nits = white bar at 20 m, 9 nits = mint field).
   Crisp and readable at 20 m (`r2final/25b_e0_sign_20m.png`). The floor stencil decal still uses the spray art.
4. **Escape-green neon (major)**: deleted the `AuxGreenCap` primitive (with it LookFixtures added a dome on top of the
   collar = the stacked discs); one collar lens per bollard. Zone23 lamp rules: `Light_AuxRouteGreen` 110 cd, 7.5 m,
   cyan-green filter (0.42, 1, 0.70); `Light_E0Gate_GreenOverhead` desaturated filter.
5. **Loading dock (major)**: Zone 1 builds the dock, so `Zone23Dressing.DressServiceDockApproach` (called from the Zone 2
   build, which runs after Zone 1) hides the renderers of Zone 1's box `Dumpster` (collider kept) and builds a front-load
   skip in its place (tapered body, rim, ribs, fork pockets, two lids one ajar, casters, rust decals), plus a pallet
   stack with crate, a pallet truck, a hose reel on the SE canopy column, a wheelie bin, stains, a SERVICE ROAD 3 sign on
   the column in the path mouth, a downpipe, hazard band and grime on the light wall end. The dock exposure is the lead's.
6. **Old Yard from the crane (major)**: perimeter walls are grey precast concrete (`PanelB`) instead of tan; outside
   faces get rain/grime streaks and hanging vines on the east and south walls; the exit court gets a parked truck and
   jeep in the painted bays, two skips (with colliders), pallet stacks, drums and a flatbed trailer carrying a fuel tank.
   The sprint line drive -> E-0 and the control box stay clear (route_test 38/38). The crane framing, mist and the
   repeated tree are not mine (CameraDirector / atmosphere / vegetation).
7. **Coast road (major)**: `DressCoastRoad`: kerbs, edge lines, drains with gratings every 8 m, dashed centre line,
   stop bar, stains/cracks, reflector posts every 6 m (small amber reflector, 1.2 nits), three low sodium lamps
   (`Light_PathLamp_Coast_*`, AuxYard group, no shadows, PathLamp fixture), an assembly-point sign at the rail and a
   burning red road flare (`Light_EvacFlare_Road`, 420 cd, skipped by fixtures). Fences see-through (item 1).
8. **Paddock 4 gate (major)** and minors: leaves are wire planes in round tube frames with mid rail, brace, hinges and
   a drop bolt; posts round; header a pipe; the emissive bottom lip is a dark pipe with three small amber tape strips.
   Substation bulkhead posts and flood masts render as round galvanised poles with base plates (box colliders kept).
   E-0 control-box beacon 190 cd / 5 m (the orange wash). New battery bulkhead on the east wall of the north run
   (`Light_PathsEastBulkhead_Batt`) for the black right third of 14.

## Lamp rules added (`LookLights.Zone23.cs`)
`^Light_AuxRouteGreen` 110 cd; `^Light_E0Gate_GreenOverhead` 900 cd filter (0.38,1,0.62); `^Light_E0ControlBox_Beacon`
190 cd 5 m; `^Light_PathLamp_Coast` 1600 cd 2400 K no shadow; `^Light_EvacFlare` 420 cd red. The new path bulkhead
matches the existing `^Light_Paths\w*Bulkhead_Batt$` rule.

## Files touched
`Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs`, `Zone3_SubstationAndYardBuilder.cs`, new
`Zone23Dressing.cs` (the existing `Zone23Dressing` class in the Zone 2 file is now `partial`),
`Assets/Editor/LookPass/LookSigns.cs`, `LookLights.Zone23.cs`, `LookRoofs.cs` (coast fence hook only),
`Assets/Art/UI/Signs/e0_exit_lightbox.png`, generated `MAT_Z23_*` materials, `MAT_Sign_e0_exit_lightbox_Lit.mat`,
`MAT_SignE0.mat`, helpers in `Tools/agent/` (`zone_shots.py`, `perf_clone.sh`, `make_e0_lightbox.py`).

## Verification
- 0 compile errors, 0 warnings; build warnings `[]`; NavMesh baked (raptor + T. rex).
- `route_test.py`: 38/38 at base and 38/38 after, no new console errors. `verify_playmode_doors`: 18/18 doors,
  5/5 documents, both NavMeshes valid.
- Perf (clone editor, hidden, 1280x720 game view, median of 8; `Tools/agent/perf_clone.sh` because
  `Tools/lookdev/perf_here.sh` sources `ucli.sh`, which targets the owner's project): substation floods gpu 9.2 -> 9.3 ms,
  exit court 5.1 -> 5.5 ms (tris 1.04 -> 1.27 M), coast road 4.6 -> 5.3 ms. CPU 16-20 ms both (hidden-editor overhead,
  noisy). No new shadow-casting lights.

## Requests for the lead
- `Tools/lookdev/ucli.sh` hardcodes `UNITY_PROJECT_PATH=$HOME/CretaceaBlackoutUnity`, so `perf_here.sh` run from a clone
  measures the owner's editor. Please make it use the clone (or warn in the brief).
- `LookProbes`: the `CoastRoad` probe (centre X 545, 40 m) does not cover the road X 505-547; re-centre to about X 526
  so the asphalt reflection is continuous (the blue band is much weaker now but this is the cause named by the critic).
- `LookFixtures.Bulkhead` glows the whole lamp_bulkhead FBX at 60 nits (the orange "faucet" in 20/21): glass only, ~25 nits.
- Chain-link: consider moving the 0.52 alpha into the palette material and dropping `MAT_Z23_ChainlinkSeeThrough`.
- Puddle decals as light-grey parallelograms (19, 23) and the rain-as-snow finding are look-system items, not changed here.

## Not verified / rough edges
- The crane shot still shows the yard (framing and mist belong to CameraDirector / atmosphere); I dressed what it sees.
- The open substation gate leaves now stand out from the fence at ~115 deg; I checked 21 but not every angle.
- Reflectors and the flare are emissive stand-ins (no retro-reflection in HDRP).
- `docs/briefs/r2_common.md` was modified in this clone by someone else (addendum) and `docs/review1/*_check.md` are
  untracked; I did not commit them.

## Fix round (art-director review of the hand-in above)
Branch `fix/zone23`, on top of 5b15368. Captures: `Screenshots/handin/` (16 files + `INDEX.md`; "before" = the reviewed
hand-in captures from 5b15368, "after" = this round's final build with NavMesh). Working captures in `Screenshots/fix/`.

### Findings and what I did
1. **RoundPost stale bounds (high) - fixed.** `Zone23Dressing.RoundPost` now takes the post from the box's transform
   (`position ± lossyScale.y/2`) instead of `Collider.bounds` (auto-sync-transforms is off, so a fresh collider reports
   the unit cube at the origin). Verified in the built scene: GatePost_N/S_Round at (91, 2.5, -0.9/-7.1) 5 m tall,
   FloodMast_0_Round at (252, 6.3, 15) 8 m, BulkheadPost_0_Round at (279.5, 3.75, -5.6); **0** `_Round`/`_Cap` renderers
   within 2 m of (0,0,0). Paddock 4 posts visible again in `07_gate_after`; substation lamp poles and flood masts stand
   on the pad (`11_floods_after`).
2. **Old Yard crane shot (high) - partly fixed, crane framing requested.** I projected every yard object into the
   crane camera (`Tools/agent/q_proj.cs`) to dress only what it sees (x < ~484; nearer the east wall the ground is hidden):
   - court: forklift (kit-built, collider block) in a painted yellow hatched loading box, three cable drums and three
     tyre stacks by the parked truck, a drum + tyres by the trailer, oil and tyre-mark decals, ten patched-asphalt repairs
     on the court and drive; the big court puddle ("mirror") shrunk from 6 x 3.4 m to 2.6 x 1.6 m. All > 6 m off the
     sprint line (466,-28) -> (505,-46).
   - the lone grey container C5 is green with a blue C6 stacked across it; every container gets roof and door-end rust
     decals drawn to 130 m (castings, lock bars and ribs were already there).
   - perimeter walls: a pilaster with a cap at every 4 m joint (was every 8 m); on the east and south outside faces each
     4 m bay gets a different cast tone and a horizontal precast joint at 2.5 m. The 133 `GrimeOut` decals are deleted.
   Measured: wall patch in the crane frame (x 60-330, y 600-700) R/B 5.41 -> 5.30, std 19.6 -> 20.1: the tan is the
   low dawn sun on concrete; the bays and pilasters now read on the right half of the wall (`15_ending_after`). The
   crane camera itself (CameraDirector) and valley mist are not mine: see Requests.
3. **Dock (medium) - fixed.** Cause found: Zone 1 merges its box dumpster into `Merged_StaticDressing` before Zone 2
   runs, so the old "hide the Dumpster renderer" did nothing and the pale-mint box showed through the tapered skip (that is
   why the skip measured the box's colour). The skip is now 2.1 x 1.3 x 1.36 m on skids, encloses the box, tint
   (0.30, 0.40, 0.28), hazard corners; the code no longer touches Zone 1 renderers. The long pale south face of
   Wall_Seg1_N (x 21.5 ->) gets five rain-streak decals, a grimy foot, a STAFF ONLY plate, a conduit on clamps with a
   junction box and riser, a hose bib. The jammed roller door gets two rubber bumpers and horizontal ribs + bottom bar
   (children of the shutter leaf, collider-free). `02/04/05_dock_*_after`.
4. **Far chain-link (medium) - fixed.** `TEX_Z23_ChainlinkFade` (built from `Assets/Art/Decals/chainlink_albedo.png`,
   DXT5): mips 0-2 unchanged, mip 3 alpha x0.55, mips 4+ x0.30 capped at 0.12. `MAT_Z23_ChainlinkSeeThrough` uses it, so
   fences past ~10-15 m become a faint veil per pixel (no LOD pop). The substation gate leaves (a 3 cm palette-material
   box = two layers, the "grey sheet") are thinned to one plane. Talon pen now captured (`16_talon_fence_after`: near
   fence with round posts, far fences faint, trees visible through both).
5. **Coast-road goal (medium) - fixed.** Flare 150 cd / 6 m in the builder (Light_Evac* is skipped by Calibrate, so the
   dead `^Light_EvacFlare` rule is deleted), a 4 cm burning core at 200 nits paired with the light, and `FlareSparksVfx`
   (22 sparks/s; its name makes LookWeather style it like every spark system). The assembly sign is a back-lit light box
   (artwork as emission, 10 nits) in a dark housing: the end of the road reads as a green sign over a small red flare
   (`13_coast_after`).
6. **Climax floods / bulkhead (medium) - fixed in my files.** New rule `^Light_AuxSubFlood` 14000 cd **5000 K**, 48 m,
   volumetric 1.8, Shadow.High, 88 deg (checked before the core 3300 K rule). The three substation post lamps are renamed
   `Light_SubPostLamp_*` (rule 520 cd 2400 K, same as the core SubBulkhead rule) so LookFixtures no longer glows the whole
   lamp_bulkhead FBX; the housing is dark with shadows off and a 17 cm frosted lens under it at 25 nits is the paired
   emissive. Visible cones still depend on the storm volumetric fog density (look system): the flood head reads as a
   cool-white source, the cone is faint.
7. **Breaker conduit "tripwire" (medium) - fixed.** Troughs: 3 cm bed, 12 cm sides; conduits at SubY+0.065 inside them;
   chequer-plate `TroughLid` (0.26 x 0.012) over the walkway crossings (x 258 run z -6..9, and the transfer riser run at the
   east gate). Clamp LEDs on hidden runs dropped for the breakers; each breaker shows its state on an LED box on the
   exposed riser (local -0.22, 0.5 m), plus the mimic LED. Circuit state on the pylons: a small indicator box with LED on
   each of the 4 bus-gantry posts (north face, 1.7 m), passed to the transfer lever's `powerConduitRenderers` (red -> green).
8. **Perf (medium) - objects cut, numbers noisy.** `Zone23Dressing.MergeStatic` merges collider-free, unreferenced
   primitive dressing into one mesh per material/shadow mode (skips colliders, LODGroups, anything a component references,
   emissives, chain-link, glass, names with "Ceil"). Applied per chain-link span (posts, caps, rails), per container,
   per perimeter-wall dressing, yard ground, exit court, forklift, bus gantry, substation dressing, dock and coast dressing.
   Built scene: transforms 120 144 -> 117 870, MeshRenderers 91 115 -> 88 893, Zone 3 objects 5 396 -> 3 297,
   decals 804 -> 745 (in spite of the new props). Perf at 1920x1080 game-view rendering resolution, 30 samples, hidden
   batch editor (no visible window is possible for a clone editor), base = 5b15368 code, same build steps:

   | view | base gpu / cpu ms | after gpu / cpu ms | draws base -> after | batches |
   |---|---|---|---|---|
   | substation floods | 11.1 / 16.1 and 17.1 / 23.6 (two runs) | 21.3 / 24.6 and 12.6 / 19.9 | 1520 -> 1752 | 83 -> 77 |
   | exit court | 7.1 / 14.5 and 10.1 / 21.1 | 8.6 / 20.1 and 8.5 / 17.0 | 899 -> 1042 | 45 -> 46 |
   | coast road | 7.2 / 13.1 and 10.7 / 16.6 | 8.2 / 18.2 and 9.0 / 17.0 | 688 -> 729 | 18 -> 19 |

   GPU varies by up to 50 % between identical runs in this editor; the draw counts rose in the substation (+15 %) and the
   court (+16 %, the new props and 30 decals). Not proven: 60 fps at 1080p in the owner's visible editor.
9. **Delineators (low) - fixed.** Reflectors on the -X face (toward the runner) and on +X, 6 x 100 x 50 mm, amber 3 nits.
10. **Zone 1 `_Batt` rule (low) - confirmed, request.** `LookLights.Zone1.cs` has the unanchored `"_Batt"` rule and
    AllRules yields Zone1Rules first, so every zone 2-3 `*_Batt` lamp is 340 cd aimed down. Not fixable in my files
    without renaming lamps; the earlier hand-in claim ("matches the existing rule") was wrong. See Requests.
11. **Mimic labels / StatusLED (low) - fixed.** The six blank label strips are deleted (no text art exists for them);
    the 14 cm glowing cube on each station is a 4 cm domed LED (flattened sphere) in a dark bezel.
12. **route_test fall-through at ch4 (low) - confirmed pre-existing, not mine.** See Requests.

### Ownership flags from the review
- `LookRoofs.cs` coast-fence step: **reverted** (LookRoofs is mine for zones 2-3 only). Request below. In this build the
  coast fences still read see-through (`13_coast_after`), from the lead's blended palette material.
- Dock: no Zone 1 renderers are touched any more; the skip encloses Zone 1's box; dressing objects live under
  `Zone_02.../Z23_DockApproachDressing` (brief item 5 asks for the dock dressing). The roller-door ribs are parented to
  Zone 1's shutter leaf so they move with it.
- RoundPost side effect at (0,0,0): fixed (item 1).
- `Assets/Art/UI/Signs/e0_exit_lightbox.png`: kept, **needs the lead's OK** (request). Without it the E-0 header falls
  back to the shredded spray art.
- Flare + assembly sign: on the road end (x 543-545, my dressing), not at the overlook. Request: agree with island.

### Requests for the lead / other packages
- **Lead / island (CameraDirector):** raise the `PlayDawnCoastCraneShot` peak (now overlook + (-7, 14.2, 4) = y 15) to
  ~25-30 m and aim it over the canopy toward the valley, and add valley mist; at 15 m the frame is half yard wall and
  props within ~20 m of the wall are hidden behind it.
- **Zone 1 or lead:** anchor the Zone 1 rule to `^Light_(Lobby|Dock|Corr|Security|Records|Prep|Vet|Cold|Break|StaffExit)\w*_Batt`
  or yield Zone23Rules before Zone1Rules in `LookLights.AllRules`; then the zone 2-3 battery bulkheads get their 1000/800/560 cd
  angled rules (re-capture 14 and the substation hut).
- **Zone 1:** drop the box `CoveredLoadingDock/Dumpster` (now hidden inside the skip); and `LookFixtures.Bulkhead`
  glass-only at ~25 nits for the remaining bulkheads.
- **Island:** call `Zone23Dressing.UpgradeChainlinkSpans(fenceRoot, palette)` on `CoastRoad_FenceN/S` after building them
  (the step I removed from LookRoofs), and agree the overlook marker with my flare/assembly sign.
- **Lead:** OK for `e0_exit_lightbox.png`; `route_test.py` `alive(ch4)` passes with the player at y = -610 (fell through
  the world after the T. rex reveal teleport, in both base and after runs): add a y check; the teleport target needs a look.
- Still open from before: `LookProbes` CoastRoad probe centre (X 545 -> ~526).

### Files touched this round
`Assets/Editor/WorldBuilder/Zone23Dressing.cs`, `Zone3_SubstationAndYardBuilder.cs`,
`Assets/Editor/LookPass/LookLights.Zone23.cs`, `LookRoofs.cs` (revert only), generated materials `MAT_Z23_AsphaltPatch`,
`MAT_Z23_BulkheadLens`, `MAT_Z23_FlareCore`, `MAT_Z23_RollerDoorSteel`, `MAT_Z23_SignAssemblyLit`, `MAT_Z23_SignStaffOnly`,
`TEX_Z23_ChainlinkFade.asset`, updated `MAT_Z23_ChainlinkSeeThrough(_UV0)`, `MAT_Z23_ReflectorAmber`, `MAT_Z23_SkipGreen`;
helpers `Tools/agent/zone_shots.py` (talon, dock door, gantry views; `PERF_N`), `perf_clone.sh` (draws, batches),
`env.sh`, `q_*.cs` (scene queries).

### Verification (fix round)
- 0 compile errors, 0 warnings; `build_all_scenes` warnings `[]`; final build with NavMesh (raptor + T. rex baked).
- `route_test.py --project <clone>`: **38/38**, no new console errors (`Tools/agent/route_fix.log`, 264 s). In this run
  the ch4 alive check reported the player at (128, 1.0, 0) (on the ground); the earlier runs had y -610 / -670.
- `verify_playmode_doors`: 18/18 doors, 5/5 documents, both NavMeshes valid. Console: 0 errors (the only warnings are
  "Failed to create agent because there is no valid NavMesh" from the no-NavMesh capture sessions, and one Unity
  `ScriptableRuntimeReflectionSystemSettings` notice).
- Looked at: dock 13/13b/13c, gate 16, substation 19/20/21/21b, Talon 03b/03c, coast 27, crane shot 28 (Play) and the
  crane peak in edit mode (`Screenshots/fix/crane_peak_a/b/c.png`, zoomed comparison `crane_cmp_ac.jpg`, `ending_cmp.jpg`).
- Not verified: 60 fps in the owner's visible editor at 1080p; visible flood cones (depend on the storm fog); the roller
  door ribs if the shutter ever animates; the 14 frame lighting with the `_Batt` rule fixed (needs the Zone 1 change).
