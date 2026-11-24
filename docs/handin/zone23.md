# Hand-in: Zones 2 + 3 (service paths, Talon Ridge, Substation 3, Old Yard) — branch `look/zone23`

Base 941668e. Captures: `Screenshots/handin/` (16 files + INDEX.md; the folder is git-ignored, it stays in the clone).

## What changed and why

All changes are in the two zone builders (+ a helper class `Zone23Dressing` that lives in
`Zone2_ServicePathsBuilder.cs`) and the zone lamp table. Every dressing piece is collider-free unless noted, so the
walkable route and the NavMesh are what the gameplay builders made.

**Shared helper `Zone23Dressing`** (internal static class): precast panel wall builder (4 m panels with recessed
joints, one continuous invisible collider, coping, plinth, outside pilasters, inside kerb + drain channel with a
grating decal, grime/moss/crack decals, hanging vines, hero jungle against the outside face kept clear of the lanes);
wall/floor decal helper (pivot centred); unlit bolted sign plates (texture reads unmirrored); LOD'd plant placement
seated on `EvaluateWorldElevation` (top level = `_LOD1`, then `_LOD2`, culled; understory casts no shadows);
lamp_post with a pole-only collider; zone materials via `palette.Surface(...)` (`MAT_Z23_*`).

1. **Service path walls** — the 14 single-cube walls are now precast panel walls (three concrete tones), coping, pilasters,
   kerb + drain, grime under the coping, moss at the foot, vines, jungle behind; L-corner notches closed by end
   extensions; road segments top out 0/4/8/12/16 mm apart (no coplanar overlaps) and grey concrete turning slabs
   (2 cm, no collider) cover the bends; extra puddles, oil stains, cracks, raptor prints across the corner-2 crossing;
   a palm fallen over the Seg4_NE coping opposite the "crashes through the jungle above" sound (75,4,9) with its root
   plate torn out; hazard paint on the Paddock 4 gate posts and threshold, `paddock4_gate`, `high_voltage` and
   `service_road_3` plates; edge clutter in the dead-end Branch 1 (no colliders).
2. **Talon Ridge** — `TalonPen_Ground` removed (the terrain plateau is the floor); fence on a 1 m boardform plinth
   (collider) with cap, 4 standoff insulator runs + cranked outriggers with 3 wires on the pen side, warning plates
   every 14 m; the sliding `talon_gate` keeps its collider/DoorController but the solid panel no longer renders: bars,
   rails, stiles and braces ride on the sliding leaf (Door layer, so the Look Pass gives them UV0 copies) with a warning
   plate; feeding cage + hanging carcass on a gantry framed by the gate; rock outcrops and boulders; jungle from 58 m
   back; two cold flood masts on the fence (`Light_Talon_FloodFence_0/1`, Talon group).
3. **Substation 3** — pad material cracked concrete with 6 m joints and stains; aprons `GravelGround`; two kit-built
   transformers (tank, radiator banks, conservator, porcelain bushing stacks, plates) on a gravel bed with kerbs; a
   lattice bus gantry (two portals, suspension insulators, sagging conductors, droppers to the bushings, incoming
   line); brick control hut with mono-pitch roof, gutter, door, louvre, AC unit, plates and a battery bulkhead;
   flush cable trench with grating from the transformers to the switch cabinet; open troughs carrying the three red
   breaker conduits (the conduits themselves are untouched); real gates in both fence gaps (heavy posts with
   colliders at the gap ends, chainlink leaves swung back against the outside of the fence); HV plates on all sides,
   `substation_3` plate at the west gate; fuel-corner clutter; a service truck outside the west apron; jungle outside
   the fence. The six interact stations, conduits, anchors and the sub_blocker ring are unchanged.
4. **Old Yard** — slab split into 7 pieces around the Warehouse B and workshop footprints (no coplanar floors), grey
   cracked concrete with 6 m joints; asphalt drives (squeeze gap -> south drive -> exit court -> E-0, north drive,
   WH B aprons) with yellow dashes, white edges, stop bar, parking bays; `e0_exit_decal` stencil and hazard threshold
   at E-0; 12 puddles, oil stains, cracks; perimeter walls get coping, outside pilasters, joints, plinth, grime, moss,
   weeds at the inside foot and jungle outside (squeeze-gap approach and coast road kept clear); workshop exterior walls
   painted brick, ceiling tiles, pitched corrugated roof with eaves, gables, frieze, fascia, gutters, downpipes, high
   windows / meter box / vents / AC units on the long south face; the floating `Porch_Canopy` is now a canopy on two
   posts over `workshop_door_e`; desk and workbench built with legs, chair, lockers; electrical closet dressed
   (panelboards, switch cabinet, rack, cable tray); `SouthDockBuilding` is a shed with three roller doors (one jammed
   half open), raised dock with bumpers and hazard edge, steps, ramp and canopy; 7 kit-built containers, pallet
   stacks, drums, crates, a second utility truck, a parked jeep, debris, 4 AuxYard drive lamps; `warehouse_b` plate
   and `warehouse_b_decal` stencil.
5. **Seating** — watchtowers seated on the lowest terrain under their footprint (they floated 0.3–4.3 m); feeding cage
   and its blood decal seated; jeep crash site: concrete pad removed, jeep seated on the terrain, skid furrows,
   flattened bollard, torn fence panel, cans; culvert bridge: gravel embankments with boardform wing walls at both
   deck ends (the deck stood 1.5 m above the cut banks; collider, walkable), guardrail posts every 1.8 m + mid rail;
   AuxRoute bollards 3.4 m off Road 3's centre line, alternating sides (the last one south of the squeeze gap),
   seated on the terrain (they floated up to 0.8 m), cube caps replaced by a glowing collar (still the paired emissive).
6. **Blackout lighting** — battery bulkheads: `Light_PathsCornerBulkhead_Batt`, `Light_PathsDiagBulkhead_Batt`
   (VCExit, on in the blackout), `Light_SubHutBulkhead_Batt` (SubBattery), `Light_YardGapBulkhead_Batt`,
   `Light_WorkshopWestBulkhead_Batt`, `Light_WorkshopDoorEBulkhead_Batt` (YardBattery). Green stays the AuxRoute,
   amber the service points, red only on the beacons.
7. **Unused art now used** — signs `talon_ridge_warning`, `paddock4_gate`, `high_voltage`, `substation_3`,
   `service_road_3`, `warehouse_b`, `warehouse_b_decal`, `e0_exit_decal` (floor stencil); decals `moss_patch`,
   `grime_leak_2/3`, `cracks_2`, `grating`, `footprint_raptor`, `footprint_trex` (Road 3 east of the substation),
   `hazard_stripes`, `DecalGrimeLeak`, `DecalCracks`, `DecalCautionStripes`.

Gameplay geometry fix (worth knowing): the six path lamp_posts used an automatic bounds box collider that spanned the
2.3 m double arm, i.e. an invisible wall across half the lane under lamps 4 and 5; they now have a pole-only collider.

## Files touched
- `Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs`
- `Assets/Editor/WorldBuilder/Zone3_SubstationAndYardBuilder.cs`
- `Assets/Editor/LookPass/LookLights.Zone23.cs`
- generated by the builders (committed like the other palette output): `Assets/Art/Materials/World/MAT_Z23_*.mat`

## Lamp rules added (`LookLights.Zone23.cs`)
| Regex | cd | K | range | vol | shadow | spot |
|---|---|---|---|---|---|---|
| `^Light_YardGapBulkhead_Batt$` | 480 | 2600 | 13 | 0.80 | Low | 120 |
| `^Light_.*Bulkhead_Batt$` | 320 | 2600 | 10 | 0.75 | Low | 120 |
| `^Light_Talon_FloodFence` | 16000 | 6200 | 55 | 1.00 | Low | 80 |
| `^Light_PathLamp_Yard` | 2200 | 2300 | 17 | 0.85 | Low | 100 |
`look_pass`: lights calibrated 110, unmatched 0.

## Verification (done in this clone)
- Compile 0 errors / 0 warnings. Final `build_all_scenes` with NavMesh: status ok, raptor + T. rex NavMesh baked.
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid.
- Play mode smoke test after the final build: `jump_chapter` ServicePaths, Substation, OldYard, Escape, Night,
  Substation-after-aux: 0 console errors, 0 warnings (one HDRP "ScriptableRuntimeReflectionSystemSettings.system is
  assigned more than once" warning appears when Play mode is entered a second time in the same session: engine-level).
- Route: a 0.3 m player capsule swept (0.5 m steps) along the waypoints dock exit -> Paddock 4 gate, paddock entry ->
  Road 3 -> culvert bridge -> substation west gap, gap -> each of the six stations, east gap -> AuxRoute -> squeeze gap
  -> workshop corridor, workshop_door_e -> exit court -> E-0 box -> gate: no blocking collider from this package. The
  only hits are pre-existing or method artefacts: `ServiceGate_GroundedLog` overlaps the capsule at (94.5, 1.24, -2.2)
  right beside `paddock_entry` (unchanged), `BulkheadPost_1` beside the substation west line (unchanged, walk round),
  DinoBlocker/lintel/ceiling hits of the downward ground ray.
- Triangles in Play mode (`diagnostics get_performance`, main camera incl. shadows): ServicePaths spawn 3.52 M /
  555 draws, Substation 6.8–7.5 M (Zone 4 terrain/scatter is ~4.7 M of it; toggling this package's substation dressing
  made no measurable difference), OldYard 3.05 M, Escape 0.71 M. The first version of the path jungle cost +4.15 M at
  the ServicePaths spawn; after LOD1-as-top, no understory/vine shadows and thinning it costs ~+1.3–1.6 M.

## Requests for the lead / other packages
- **Palette (lead):** `MAT_ChainlinkFence` reads as a very coarse expanded-metal mesh (diamonds ~0.5 m) on every fence
  and gate here; a ~1 m tile with the finer `chainlink_detail_*` maps would read as chainlink.
- **Kit (fixtures):** bodies for this package's new lamps by name family: six `Light_*Bulkhead_Batt` (wall
  bulkheads; they sit 0.1–0.2 m off the wall face, spot aimed down/out), `Light_Talon_FloodFence_0/1` (flood heads on
  the 9.2 m `TalonFloodMast_*` poles), `Light_PathLamp_Yard_0..3` (on `lamp_post` FBX like the path lamps; light on the
  pole axis at +4.1 m). The AuxRoute caps are now flattened cylinder collars (`AuxGreenCap_i`, still the paired
  emissive); if LookFixtures builds its own lens there, drop one of the two.
- **Kit (roofs):** the workshop has its own pitched roof (eaves at Y 5.15, frieze from the wall head 4.5 to 5.27 on both
  long sides). A shell roof cap/parapet up to ~0.5 m high stays hidden under it; anything taller pokes through.
  Rain-blocker slabs still show as white roofs in capture cameras (WH B, dock canopy, door canopy).
- **Terrain:** the Talon pen floor is now the terrain (X -90..20, Z 42..128): a mud/forest-floor splat there and scatter
  outside the fence lines would finish it. Hand-placed hero plants of this package stand within ~1–9 m of the walls and
  fences; your scatter can come right up to them (lanes, pads and the yard interior must stay clear as before).
- **Lead (tooling):** running `cretacea look_pass` on a live editor left HDRP spamming
  `RTHandleSystem` assertion errors in the next Play session (2000 in a few seconds) until
  `EditorUtility.RequestScriptReload()`; `build_all_scenes` alone did not.

## Not verified / rough edges
- Not merged with the kit, terrain or zone1 branches: fixture bodies, sign art and the new jungle are not in my captures.
- Frame times are not a 60 fps proof: five hidden editors share this Mac's GPU, so cpu/gpu ms varied 29–110 ms per frame
  between identical runs.
- Route checked with a capsule sweep and the door/NavMesh verifier, not by walking the character controller end to end.
  The bridge embankments make the deck walkable from Road 3; the stream bed under the deck is unchanged.
- Not inspected up close: the floor stencil orientation (`Stencil_E0`), the drain/trench grating decals, the dock steps
  and ramp, the culvert embankments (seen only at a distance), the Paddock 4 plates.
- The yard is still a big open lot from the air (clutter covers the NE quadrant, the south strip and the court edges).
- The workshop's south high windows are an exterior skin only (no openings inside).

---

## Fix round (art-director review of b35af75)

Same three owned files. Generated palette output added: `MAT_Z23_YardConcreteDark/Light`, `MAT_Z23_PalmBark`,
`MAT_Z23_PoleTimber` (+ updated `MAT_Z23_Container*`, `Coping`, `KerbConcrete`, `DrainVoid`, `YardConcrete`).
Captures: `Screenshots/handin/` (16 files, INDEX.md); round-1 set kept in `Screenshots/handin_round1/`.

| # | Finding (severity) | What I did | Evidence |
|---|---|---|---|
| 1 | Workshop gable wedge in both doorways (high) | `GableSkew` cube trick replaced by `Zone23Dressing.Prism`: a real triangular prism (flat-shaded mesh, clockwise winding) standing on the wall head at (412/462, wallTop, -16); nothing below the wall top, no collider. | 16 tiles 1-4: west entry and workshop_door_e clear from outside and inside |
| 2 | Troughs hide the red/green conduits (high) | Trough sides cut to the bed (0.086 m, centre 0.043), bed switched to the light kerb concrete so the line reads against it, saddle-clamp feet every 1.2 m. Conduits untouched. | 08 tile 1-2 (red line on the trough), tile 3 (green after the transfer). Note: the conduit's fault red is deliberately dim (`SubstationStation.cs:346`, x0.6; green is x4.5), so the blackout line is dark red, not bright; the baseline looked brighter mainly because its orange pad pulled exposure. Not my file. |
| 3 | Bulkheads too dim, hut lamp into its wall (high) | Hut lamp euler (60,180,0), 0.25 m off the face. All six bulkheads 0.25 m off their faces. Rules: `^Light_Paths\w*Bulkhead_Batt$` 1000 cd, `YardGap` 800 cd, other `*Bulkhead_Batt` 560 cd, all 80 deg cones (50-60 deg pitch keeps the cone off its own wall). Raycast along each axis in the built scene: PathsCorner 37 lux, PathsDiag 36, SubHut 53, YardGap 35, WorkshopWest 39, DoorE 48 lux at the pool centre (target 35-130). | 04, 08 tile 2, 12 tiles 2-4 |
| 4 | Old Yard empty tan lot, tiled slab (high) | `SlabConcrete` tint (1.21,1.39,1.71) -> neutral ~0.12 grey. Every 6 m bay outside the building footprints gets one of three tones (3 mm overlays, joints stay on top, different tile sizes break the repeat) and ~1 in 4 bays a 3-6 m oil/grime stain. New off-route clutter: container row + stacked container at x 424-444 z -48, a fifth container at (452.5,-40.5), scrap/vehicle pile at x 437-460 z -58..-70 (dead truck, stripped jeep, debris, logs, drums, kit-built scrap heap), pallet/drum/crate rows at z -34.6..-35.3 south of the drive (gaps at the lamps), kit-built flatbed trailer with pallets between the workshop and WH B, cart + drum by the workshop's NW corner. All clutter has simple colliders; none is on the route lines. | 14, 15 tiles 2-3 |
| 5 | Talon gate slides through plinth/pylon (medium) | `GateBars` at local z -0.75 (outside track at z 41.25; pylon face 41.4, plinth cap from 41.69), the leaf's `TopBeam` renderer disabled and a 0.2 m top rail added to the bars, ground rail with sleepers x -41.2..-20.6 at z 41.25, yellow end-stop post with footing and buffer at x -20.4. Door, collider and anchor unchanged. The open position (x -31.5..-21.5) clears pylon E and the SouthE plinth by the same 0.15 m+. | 06, 07 tiles 1-2 (scripted beat itself not played) |
| 6 | Capture set skips required views (medium) | Added: OldYard blackout (gap, west entry, door E), Escape after aux (court, E-0), Substation after aux (pad, transformers), close-ups of workshop doors + canopy, dock, E-0 stencil, drain and trench gratings, Paddock 4 + Service Road 3 plates, hut, bollards, watchtower, bridge end, jeep site. Route: a CharacterController walk (0.2 m Move steps + gravity, real 0.35 m step offset) from Seg1 to the Paddock 4 gate, paddock_entry -> Road 3 -> bridge -> substation gap, to each of the six stations and back, east gap -> AuxRoute -> squeeze gap -> west entry -> workshop corridor -> workshop_door_e (opened) -> court -> E-0 box -> gate: **0 stuck points** on the final build. Embankment ends: Road 3 crosses a side slope there (SE corner 0.39 m below the embankment top, NE 0.9 m above), so I added sloped gravel aprons (2.6 m run, 0.55 m drop, collider) off both end faces; the 0.39 m step (> 0.35 m step offset) is gone. | 11 tile 3, all sheets |
| 7 | 60 fps unproven; micro-renderers and decals (medium) | Partly done, partly **rejected with evidence**. Done: weathering decals now fade at 35 m (`Zone23Dressing.Decal` default; E-0 stencil 70 m, warehouse stencil 80 m); plants within 12 m of a service-path lane use the full mesh as LOD0 down to 35 % screen height, then `_LOD1`, `_LOD2`, cull. Rejected: mesh-combining the static primitives. This project runs HDRP with the GPU Resident Drawer in `InstancedDrawing` mode and the SRP Batcher on, so the thousands of identical Cube/Cylinder renderers sharing a material are already instanced; merging them into unique meshes made it worse. A/B at 1080p in Play (same code otherwise): no combine 551 / 785 / 641 draws (ServicePaths / Substation / OldYard); whole-group combine 674 / 883 / 735; combine bucketed in 10 m cells 845 / 1389 / 1175, for renderers 9069 -> 6412 / 6976. The combine code was removed. Final build, 1080p, Play: ServicePaths 559 draws / 135 setpass / 3.86 M tris; Substation 793 / 163 / 8.03 M; OldYard 640 / 120 / 4.14 M; Escape 258 / 99 / 0.72 M; Night 787 / 271 / 3.06 M. The near-lane LOD0 costs ~+0.3 M tris at the ServicePaths spawn (3.54 -> 3.86 M) and ~+8 draws. | numbers from `diagnostics get_performance` |
| 8 | Containers: blue renders black, red flat (medium) | Blue = `metal_painted_worn` grey_BaseColor x (0.55,0.75,1.15); green = green_BaseColor x 1.0; red = rusty corrugated x (1.25,0.85,0.78). 3 cm ribs every 0.3 m on both long sides and across the blind end. | 15 tile 3, 16 tile 3 |
| 9 | Blue drain, pasted bend slabs, orange coping, palm (medium) | `DrainVoid` tint 0.16, smoothness 0.25-0.45 (also the joints); grating decal confirmed rendering (03 tile 6). `BuildBendSlab` calls and method deleted (road tops stay 4 mm apart). `Coping` on concrete_polished x 1.75, `Kerb` on concrete_polished x 1.6 (neutral). Path puddle decals faded 0.82-0.88 -> 0.58-0.62 (they read as black blotches). Palm: 17 m palm at 24 deg lean whose trunk rises out of a 0.6 m broken notch in Seg4_NE's coping (`NotchCoping` splits the coping box and adds two broken stubs), crown over the lane; three coping chunks on the kerb/drain, an impact scuff under the notch, a 2.3 m snapped bark trunk with splinters on the lane edge (no colliders). | 02, 03 tiles 1-3 |
| 10 | Wrong sign text on the Talon gate and fence (medium) | Fence plates and `Sign_TalonGate` now `high_voltage` (0.55 x 0.75 on the fence, 0.8 x 1.1 on the gate). `talon_ridge_warning` ("DO NOT TAP ON GLASS") is no longer used by this package. | 06, 07 |
| 11 | Floating / unsupported pieces (medium) | Incoming line ends on a timber terminal pole at Ground(257,-21): footing, galvanised crossarm with braces, three pin-insulator stacks, jumpers into a cable riser down the pole, stay wire to an anchor block, HV plate; the three phases sag onto the crossarm. `Sign_Paddock4Gate` and `Sign_HighVoltage_P4` at x 90.912 (backing flush with the leaf face 90.93); `Sign_ServiceRoad3` computed flush on Wall_Seg5_N's lane face; `FlattenedBollard` grounded at GroundMin - 0.03 (lies on the slope); Talon flood masts start 0.15 m below the terrain with a 0.7 m footing. | 03 tiles 4-5, 10 tile 2, 11 tile 4 |

### Lamp rules now in `LookLights.Zone23.cs`
| Regex | cd | K | range | vol | shadow | spot |
|---|---|---|---|---|---|---|
| `^Light_Paths\w*Bulkhead_Batt$` | 1000 | 2600 | 14 | 0.80 | Low | 80 |
| `^Light_YardGapBulkhead_Batt$` | 800 | 2600 | 14 | 0.80 | Low | 80 |
| `^Light_.*Bulkhead_Batt$` | 560 | 2600 | 12 | 0.75 | Low | 80 |
| `^Light_Talon_FloodFence` | 16000 | 6200 | 55 | 1.00 | Low | 80 |
| `^Light_PathLamp_Yard` | 2200 | 2300 | 17 | 0.85 | Low | 100 |

### Verification (fix round, this clone)
- Compile 0 errors / 0 warnings. Final `build_all_scenes` with NavMesh: status ok, raptor + T. rex NavMesh baked.
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid.
- Play smoke test on the final build: Substation, ServicePaths, OldYard, Escape, Night: 0 errors, 0 warnings. (Re-entering
  Play a second time in one session logs the known engine warning "ScriptableRuntimeReflectionSystemSettings.system is
  assigned more than once".)
- Right after a full `build_all_scenes` the live GameView logged 4-5 HDRP-internal `NullReferenceException`s in
  `ReflectionProbeTextureCache.NewRender` (the log shows the pipeline being disposed and recreated during the build, i.e.
  the Look Pass touching HDRP settings; Look Bible §7). After `EditorUtility.RequestScriptReload()` they do not recur.
  Not from builder code; worth a look by the lead (the build could request the reload itself).
- CharacterController walk dock -> E-0 (see finding 6): 0 stuck points.

### Requests for the lead / other packages (fix round)
- **zone1 / lead:** `talon_ridge_warning` reads "PADDOCK 2 – TALON RIDGE / DO NOT TAP ON GLASS": it belongs on the
  Control Room's viewing glass, where its text makes sense; this package no longer uses it.
- **Lead (perf):** the critic's mesh-combine advice does not apply with the GPU Resident Drawer on (numbers above). A
  60 fps proof needs a run with only one editor on the GPU: here 5 hidden editors share it, and even the 0.72 M-triangle
  Escape view measured 57-69 ms per frame.
- **Lead (tooling):** see the HDRP NRE note above; a `RequestScriptReload()` at the end of `build_all_scenes` would avoid it.
- **Terrain:** the Talon pen floor still reads as bare dirt under dark trees and warm under the floods; a mud/forest-floor
  splat and scatter there would finish the hero view (unchanged request from round 1).

### Not verified / rough edges (fix round)
- The scripted Talon gate beat (StoryDirector turning the player to talon_gate_view and sliding the leaf) was not played;
  the clearance is from geometry (open leaf x -31.5..-21.5 at z 41.25 vs pylon E face z 41.4 and plinth cap 41.69).
- 60 fps at 1080p is not proven (shared GPU, see above).
- Blackout tiles taken with `cretacea capture` during Play include the player's flashlight from the player's position.
- The yard's far south strip (z -95..-80 between the S containers and the dock) is still fairly open; the SW/centre
  quarters the critic named are filled.
- The fallen palm is a leaning palm resting in the notch (a straight trunk rooted outside cannot drop into the lane);
  the crash reads from the notch, chunks and snapped trunk piece rather than a crown on the ground.
