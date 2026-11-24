# Package: ZONES 2 + 3, SERVICE PATHS, TALON RIDGE, SUBSTATION 3, OLD YARD  (branch `look/zone23`)

**Problem.** The outdoor route is where the player spends the storm: today it is 14 bare concrete slabs lining a
lane in an empty field, a raptor pen that is a flat rock-textured box, a substation that is an orange pad with
invisible fences, and a yard that is one 110 × 146 m slab with four bare walls.
Read `docs/maps_2026-11-20/digest_zone23.md` then `notes_zone23.md` (area tables, route waypoints, ASCII plans,
placeholder list, dressing ideas). Jungle scatter, terrain and water are being rebuilt by the terrain agent, lamp
bodies and signs by the kit agent: place hero plants by hand only right against your structures, place lights and
signs through the existing kit calls, and do not build lamp bodies.

**You own:** `Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs`,
`Assets/Editor/WorldBuilder/Zone3_SubstationAndYardBuilder.cs`, `Assets/Editor/LookPass/LookLights.Zone23.cs`.
Zone-specific materials: `palette.Surface(...)` inline. Terrain heights: read them with
`Zone4_TerrainJungleAndDawnCoastBuilder.EvaluateWorldElevation(x, z)` (do not edit that file).

## Goals, in priority order
1. **Service path walls** as built structures: 4 m panels with joints, a coping, pilasters, a kerb and drain at the
   foot, grime streaks under the coping, moss at the base, hanging vines, hazard markings at the gate; a fallen palm
   over the wall where the story says something crashes through the jungle; road surface `AsphaltWet` /
   `ConcreteCracked` with puddles; no coplanar overlaps at the bends (z-fighting).
2. **Talon Ridge raptor pen** (the Control Room's hero view): remove the flat ground box so terrain shows, fence on
   a concrete plinth with insulators and warning signs, a barred gate, feeding cage + hanging meat, rocks, floodlit
   as a cold cage in the dark.
3. **Substation 3** as a substation: transformers with insulator stacks, a bus gantry with cables, a small control
   hut, gravel aprons (`GravelGround`), a cable trench with grating, real gates in the fence gaps, high-voltage
   signs; the six interact stations stay exactly where they are and stay reachable.
4. **Old Maintenance Yard:** break the slab into lanes and cracked panels with joints, puddles and stencils; fill
   the empty quarters with believable 1990s yard clutter (pallets, crates, drums, a second truck, containers built
   from the kit, debris); workshop with an exterior skin and a pitched corrugated roof, canopy with posts over its
   door, a loading dock that looks like one, furniture with legs; wall copings; weeds at wall bases.
5. **Everything sits on the ground.** Seat valley props with `EvaluateWorldElevation`; bridge abutments, guardrail
   posts, watchtowers, AuxRoute bollards off the road centre line.
6. **Light the route for a blackout:** battery bulkheads at the yard entry and other dead-black gaps the player must
   cross (name `Light_<Place>_Batt`), rules in `LookLights.Zone23.cs` (pools ≈ 35–130 lux, Look Bible §4). The
   green AuxRoute stays the escape language; amber for service points; red only as beacons.
7. Use the unused sign textures and decals listed in the digest.

## Verify (Play mode)
`jump_chapter ServicePaths`, `Substation`, `OldYard`, `Escape`; free-camera views of each area in
`NightStormBlackout`, and the yard + substation again after aux power (after `Substation`, call the transfer:
see `notes_story_flow.md` for `WorldRegistry.Instance.GetInteractable("substation_TransferLever")`). Prove the route
still works: no new collider on the walkable route, all stations and the E-0 gate box reachable
(`unity command cretacea verify_playmode_doors`, and teleport-walk the waypoints from the notes).
