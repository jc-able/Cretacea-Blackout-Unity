# Hand-in: Zone 1, the Visitor Center (branch `look/zone1`)

Captures: `Screenshots/handin/` (16 files + `INDEX.md`; 01-08 are before/after pairs from the same camera).

## What changed and why

### 1. Right material on every surface
- **Ceilings** -> `CeilingTiles`: security, lockers, control, server, records, corridor, break room, gift shop, staff exit.
  Feed prep wing ceiling -> `PlasterClean` (it was wall tile). Storage ceiling -> `ConcreteBoardform` (it was the wall-concrete texture).
- **Interior linings.** The kit gives a wall one material on both faces, so the exterior walls showed raw boardform inside the offices
  and wall tile on the outside of the wing. A new `Liner()` helper puts a 2-3 cm lining on the inside face, with holes that match each
  wall's openings, plus its own skirting: drywall in security, lockers, control and gift; plaster in server and records;
  green plaster in the staff exit; white tile in the feed prep wing (west, north, south, east); boardform on the lobby face of the corridor wall.
  The outside of the break room south wall, the staff exit north wall and the wing south and east walls is now boardform.
- **Corridor:** drywall with a blue-grey wainscot band under the steel dado rail. Office partitions are drywall.
- **Floors:** the lobby is `ConcreteFloorWet` (palette), feed prep is `FloorTileWet`, the dock pad is `AsphaltWet` (it was wall concrete),
  the plaza is `paving_stone` at 1.25 m tiles (new `MAT_Z1_PlazaPaving`), with kerbs on every edge.
- Zone materials are made inline with `palette.Surface(...)` (`MAT_Z1_*`: wainscot, feature green, wood veneer, laminate, cabinet paint,
  black plastic, paper, cork, vending red, kerb concrete, binders, seat fabric, plaza paving; plus two emissives: vending glow, screen glow).

### 2. Placeholders and defects fixed
- **Gift shop:** new west exterior wall, three double-sided gondolas and two wall bays stocked with boxed goods, a checkout counter with
  register and back shelf, display tables behind the south window, rex banner, raptor poster, park logos. A glazed shopfront in the
  lobby west wall (window, not walkable) with the welcome sign over it makes the shop visible from the lobby. 4 ceiling fixtures, a battery unit, a reflection probe.
- **Mezzanine:** the glass slab is now two balustrade runs (posts every ~2 m, glass panels, steel cap), open at the stair head
  (X -17.95..-15.05). The NavMesh now reaches the mezzanine (`lobby->mezz` path: Partial at base, Complete now). There is a fascia,
  four columns under the deck, a sloped soffit under the stair flight (no floating stepped blocks), benches and planters up top.
- **Dock canopy:** columns on the east edge and at the open SW corner, perimeter beams, rafters, a fascia, and a concrete upstand on top
  of the wing wall, so the west edge bears on the building.
  **Viewing gallery:** columns on the three open sides, edge beams, benches, two sign boards, 6 downlights.
- **Furniture** built from parts instead of slabs: CCTV desk (top kept at 0.82 m under the flashlight, radio and memo), terminal desk,
  radio desk under the west window, supervisor desk, records desk, server desk, break tables with plastic chairs, a kitchenette
  (sink, microwave, coffee maker, upper cabinets, fridge), a vending machine (glass front, lit interior on VCMain, snack trays, keypad),
  a reception desk (moved to (-42,-25), off the broken-pane puddle and off the entrance axis), stainless prep tables with under-shelves
  (same solid collider footprint as before), a vet exam table, vet cabinets with sink, locker benches.
  Clutter: monitors, CRTs, keyboards, phones, binders, paper stacks, mugs, a desk lamp, a key cabinet, a wall clock, cork boards.
- **CCTV bank** flush on the wall lining (z 33.805, face 33.847). **Status wall** map is now a framed emissive picture flush on the partition.
- **Keeper note** on the prep counter at (2.0, 0.91, 13.5). **Manual** sits on the desk (0.822). **Hanging meat** raised to the hooks.
- **Corridor:** the conduit runs tight to the north wall; the cable tray is a real tray on hangers at z 21.42, y 2.95, clear of the fixtures.
- **Puddles** straight under the three broken panes ((-34.2,-16.1), (-27.8,-16.1), (-27.8,-22.4)), plus crack decals and glass shards (all in the `lobby_damage` group).
- **Shutter:** `lobby_shutter_sparks` anchor at (-28.6,3.6,-31.4) (the assembler no longer falls back), a `gate_control_box` with conduit to
  a motor, a head box and side guide rails, and a caution stripe on the floor.
- **Ceiling slots closed:** the old 0.45 m ceiling insets (corridor, break, gift, feed prep) are gone. Ceilings are full size and tucked 12 mm
  under the wall tops (`TuckCeiling`). Rain blockers stay full size.
- **Plaza:** paving, kerbs, darker strips along the facade and the entrance axis, six raised planting beds (a palm, banana, tree fern and
  monstera in each), 8 lamp posts with lights, park benches and bins, a park-map board, a fire-assembly sign, an entrance canopy on two
  columns with downlights, the lit `lobby_welcome` sign on top and `park_logo` on the facade. The south viewing rail is posts plus glass.

### 3. Blackout you can read
- Every room the player can enter has a warm battery unit (`Light_<Room>_Batt`, VCEmergency, 3200-3300 K, about 35 lux pools): Security,
  Lockers, Control (x2), Server, Records, Corridor (x4), Break, StaffExit, GiftShop, Lobby (x4, high on the walls), Prep, Cold, Vet, WingS, Dock.
- The red room floods are gone (Security/Control/StaffExit/Lobby/Prep `_Emerg` red points became battery units). Red is left only as
  small accents: the five corridor beacons (130 cd), one at the staff exit door (60 cd), one at the jammed shutter control (140 cd).
- Power-on: added fixtures where rooms had none (prep hall x4, cold, vet, wing south, staff exit, server white main; gift x4, break x3).
  Each main ceiling fixture has a low unshadowed `_Fill` point 1 m under the ceiling standing in for floor bounce, so ceilings and upper
  walls read instead of going black. The server room's blue light is now a small rack glow, not the room light.

### 4. Lived-in detail on the route
Posters and signs from `Assets/Art/UI/Signs` (raptor_poster, rex_banner, fire_assembly, staff_only, records, feed_prep, map_park_modern,
talon_ridge_warning, park_logo, lobby_welcome), wall-mounted extinguishers on brackets (moved clear of door swings), bins, a drinking
fountain, notice boards, archive boxes, crates, pallets, drums, a dumpster, a hand truck, steel bollards outside the roller door
(outside its 3 m width), decals (grime, cracks, caution stripes, one blood smear in feed prep, the existing one in the vet clinic).

### Performance pass
`ConsolidateStaticPrimitives()` runs at the end of `Build()` and merges every static, single-material Cube/Quad on the Environment layer
into one mesh per material (1974 primitives -> 41 meshes). Colliders stay on their original objects. It leaves alone anything a component
references (light-group emissives, toggled diffusers, CCTV screens, the flashlight LED), anything under a LightGroupMember, WorldAnchor,
DoorController, DocumentPickup, SimpleInteractable, FlashlightPickup or Animator, glass (no shadows) and the rain blockers (own layer).

## Files touched
- `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs`
- `Assets/Editor/LookPass/LookLights.Zone1.cs`
- New generated materials `Assets/Art/Materials/World/MAT_Z1_*.mat` (+ .meta), created by the builder through the palette.

## Lamp rules added (`LookLights.Zone1.cs`, checked before the core table)
`_Fill$` 80 cd (builder colour, no shadow, no volumetric) | `^Light_Lobby_Batt` 900 cd 3200 K | `^Light_Dock_Batt` 520 cd |
`_Batt` 340 cd 3300 K, 150 deg down | `^Light_Corr_Emerg` 130 cd red | `^Light_StaffExit_Emerg$` 60 cd red | `^Light_Lobby_Emerg$` 140 cd red |
`^Light_(Prep|Vet|WingS)_Main` 1300 cd 4300 K | `^Light_Cold_Main` 1100 cd 5000 K | `^Light_StaffExit_Main` 1000 cd | `^Light_GiftShop` 1300 cd
(4 fixtures now) | `^Light_Server_Main` 900 cd | `^Light_Server_Blue` 110 cd | `^Light_Break_Main` 1250 cd (3 fixtures) |
`^Light_PlazaLamp` 1900 cd 2700 K, low shadows | `^Light_Canopy_Down` / `^Light_Gallery_Down` 800 cd 3000 K.
`cretacea look_pass` on the final scene: lights calibrated=169, unmatched=0.

## Requests for the lead / other packages
- **Kit (fixture bodies):** none of my new lights has a body (by brief). Battery units, the `_Fill` points (should stay invisible),
  the gift/break/prep/vet/cold/wing/staff-exit/server ceiling fixtures, plaza lamp heads (they sit inside the `lamp_post` FBX head at y 4.25),
  canopy and gallery downlights need the kit's lamp bodies.
- **Kit (signs):** door and wayfinding signs are still the blank green boxes; seen from the lobby, `Sign_ToStaffExit` (55-nit green) blooms
  into a large green blob on the wet floor.
- **Palette:** `CorrugatedMetal` and `RustedIron` share the rust-orange texture; the entrance shutter and the dock canopy underside read
  rust-red. A grey painted corrugated variant would suit the shutter better.
- **Merge note:** `ConsolidateStaticPrimitives()` also merges the kit's static wall/trim cubes inside Zone 1. If the kit adds new
  per-object components to fixtures or signs, they are protected automatically only when a component references the renderer or sits
  on an ancestor.

## Verified (I looked at the capture or ran the check)
- 0 compile errors and 0 warnings. Final `build_all_scenes` with NavMesh: status ok, `warnings: []`.
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, raptor and T. rex NavMesh valid.
- Play-mode smoke test: `jump_chapter Night`, `Corridor`, `ServicePaths`, with 0 console errors. One warning appeared on entering Play
  mode: "ScriptableRuntimeReflectionSystemSettings.system is assigned more than once". It is engine-internal, comes from no Zone 1 code,
  and also appeared during the door check.
- Route capsule-clearance checks (corridor spawn, fountain, lobby axis, lobby to staff exit, shutter, prep aisle, wing south, dock exit,
  records desk, stair head): all clear. NavMesh leg paths are the same as base, except the mezzanine, which is now reachable.
- I looked at captures of every room in both power states (play mode) and at the plaza, canopy and gallery at night and by day.

## Not verified / rough edges
- **Frame rate is not proven.** Five editors were running on this Mac at once, and one hidden batch editor read anywhere from 15 to 66
  fps in the same spot from minute to minute. In one back-to-back pair before the merge pass, the Night chapter read about 26 fps vs 48
  at base in the lobby and about 21 vs 32 in the corridor. The blackout chapter and the plaza read about the same as base. The merge
  pass came after that pair. A later A/B (lights on and off in one session) showed no clear cost from the new lights. The 60 fps target
  needs a measurement in a windowed editor on a quiet machine.
- The Intro is a cinematic, so I could not teleport the player there. The golden-day plaza and gallery were judged from the cinematic
  frames and edit-mode captures.
- The `_Fill` bounce points are a stand-in for the bounce SSGI is not giving here. If GI gets better they can be dropped from one rule.
- The mezzanine is reachable but nothing on the story route uses it.

---

# Fix round (art-director review of 6bea769)

Captures: `Screenshots/handin/` (16 files + `INDEX.md`, replaced). The round-1 set is kept in `Screenshots/handin_round1/`.
Everything below is in the two owned files plus the generated `MAT_Z1_*` materials.

## Findings and what I did

| # | Finding | What I did | Evidence |
|---|---|---|---|
| 1 | Desk clutter floats in the Vet Clinic / Cold Storage (world/local mix-up) | Confirmed in a capture (4 groups hanging in the Vet Clinic). `PaperStack`/`BinderRow` place through `Group()` in world space, so the five calls now pass world points: DeskBinders (-66.62,0.82,31.52), Timesheets (-61.5,0.80,25.95), LooseFiles (-3.2,0.82,27.25), Receipts (-56.4,1.0,-14.48), VisitorLog via `t.TransformPoint(...)`. | 01, 15 |
| 2 | `_Fill` points: a glowing blob on every lit ceiling | All 22 `_Fill` points removed (`CeilingLamp` has no fill any more, the corridor fills are gone, the `_Fill$` rule is gone). Ceilings get a soft upward `_Wash` instead: a 160 deg spot 1.2 m off the floor, 90 cd, unshadowed, no volumetric (~30 lux on the tiles, broad, no hot spot), 1-2 per room (Security, Lockers, Control x2, Records, Server, Break, Gift, Prep, Corridor x4) and a 380 cd one under the dock canopy so its beams and rafters are not lost in black. | 03, 04, 05, 09 |
| 3 | 60 fps not proven; A/B showed a large drop | Cut cost in the lamp table: `_Batt` unshadowed (Low only for the 4 big lobby units), plaza lamps, canopy and gallery downlights unshadowed, only one fixture per room shadowed (the extra troffers, gift/break/prep fixtures: no shadow, no volumetric), corridor tubes Low on even / none on odd, cold-room blue no shadow. Zone 1 in Night now has 28 shadowed lights (VCMain 24, VCExit 3, DockMain 1; was ~58), 124 lights in all (the 22 fills are gone). Re-measured base vs branch in one editor at 1080p (numbers below): the machine is shared and the readings are noise-dominated, so 60 fps is still NOT proven. | perf section |
| 4 | 90-degree props get box colliders turned 90 degrees | New `RefitPropBoxColliders()` runs before the merge pass: every BoxCollider the palette added to a prop instance (`IsAddedComponentOverride`) is refit from the meshes' bounds in the prop's own space. Lamp posts get a CapsuleCollider round the pole instead (r 0.2 m, at the FBX's measured pole position local (-0.06,-0.69), 0.8 m from the pivot). Checked: collider bounds now equal mesh bounds for park_bench (yaw 90/270), lockers (90/180), archive_shelf, and the lamp capsule sits on the pole. Covers the utility truck and the cold-storage shelves too. NavMesh re-baked, doors re-verified. | numbers in Verified |
| 5 | Canopy column on the lobby-raptor mark; flee line through PlantingBed_3 | Canopy columns removed: the slab is 3.0 m deep and hangs from two 80 mm tie rods to plates on the solid facade above the shutter bay; welcome sign on the new front edge; downlights moved under the slab. PlantingBed_3 moved from (-4,-52) to (6,-50), 4.4 m clear of the flee line. Ran the beat in Play mode (Corridor chapter, walked into the lobby): the raptor walked (-58,-38) -> (-36.7,-36.06) in a straight line, held, then fled to the south-east; the raptor's NavMesh path for both legs is Complete and straight (22 m and 52 m = the straight-line distances). | 07, 14, 16 |
| 6 | Status bezel 22 m outside; lobby shards in the gift shop / plaza | Bezel now `Box(cr.transform, ..., (-26.125,1.85,28), (0.05,2.3,4.6))` on the partition face; the map sits on the bezel face (x -26.15). Shards offset by the `dmg` origin (`gx + 31, gz + 12`). Grepped every `Box()`/`CreateBox()` into a parent that is not at the origin: only these two were wrong (everything else is a Group with local offsets by design). | 02, 06 |
| 7 | Offices read as one dim amber pool | Troffer grids: Security 4 (-68.5/-61.5, z 25/31), Lockers 2 (z 25/31), Control 6 (x -44/-37/-30, z 25/31), Records 4 (x -13.5/-4.5, z 25/31), plus the washes. Rules: extras `^Light_(Security|Lockers|Control|Records)_Main_\d` 950 cd 4000 K, no shadow, no fog; the first fixture of each room 1000 cd 4000 K Medium shadow (neutral fluorescent white instead of 3200-4500 K). | 03, 04, 09 |
| 8 | Drywall orange-sepia; rust shutter the loudest colour | New `MAT_Z1_OfficePaint` (drywall_paint, tint (1.10,1.20,1.32), albedo ~0.42/0.39/0.35) on every office/corridor wall, partition and lining that used `DrywallPaint` (16 uses). Shutter now `MAT_Z1_ShutterPaint` (grey painted metal). Plaza strips were the light-yellow kerb concrete: now `MAT_Z1_PlazaStrip` (neutral grey). | 03, 05, 06, 14 |
| 9 | Status map stays lit in the blackout | Fail-safe: a printed (non-emissive) copy 3 mm behind; the glowing picture is a `toggleObject` of the statusRoot member (SetActive(false) when VCMain dies); emissive dropped 24 -> 8 nits. Checked in Play mode: lit in Night, off in the Corridor chapter. The console screens are the control_console FBX's own material (not changed). | 08 |
| 10 | Puddles cannot be seen | The palette's puddle decal has a tiling noise mask (square edges, no contrast on the already glossy floor), so each puddle is now 4 overlapping flat ellipses of `MAT_Z1_StandingWater` (dark tint 0.50, smoothness 0.96-0.98, 2-3 mm proud, no collider, no shadow) with a grime ring decal, ~4-4.6 m across, under the three broken panes. | 06 |
| 11 | Gift-shop stock and vending snacks (text cut off in the review) | What I found: the vending cabinet was one solid box whose front face sat 5 mm behind the glass, so the trays, snacks and lit panel were hidden inside it; the stock was plain coloured boxes. The cabinet is now a shell round a recess (body, stile, keypad panel, base, head); the back panel is 14 nits (60 bloomed white); every other snack and ~55% of the bigger boxes on the gondolas and wall bays get printed box art (park_logo, raptor_poster, rex_banner, map_park_modern quads, the existing `MAT_Z1_Print_*`). | 10 |
| - | Corridor white orb (from finding 2) | It was not volumetric (turning fog off changed nothing): the 170-nit `EmissiveCoolWhite` diffuser bloomed. The corridor diffusers now use `MAT_Z1_CorridorTube` at 70 nits; corridor lights also get volumetric 0.12. | 05 |
| - | Cold storage blue flood (seen in my blackout captures) | `Light_Cold_Blue` (a bare 420 cd point under the ceiling) is now a small 45 cd downward cool pool. | 13 |
| - | Dark blackout rooms | Second battery units in Security (over the CCTV/radio corner) and Records (over the desk). | 11 |
| - | Outside-ownership note | `MAT_Z1_ScreenGlow` (dead) deleted. The other `MAT_Z1_*` are generated by the allowed palette calls. Scenes, atmosphere `.meta` files and the TMP fallback font are left uncommitted. | |

## Lamp rules now in `LookLights.Zone1.cs` (first match wins)
`^Light_Dock_Wash$` 380 cd | `_Wash` 90 cd, 160 deg up, no shadow | `^Light_(Security|Lockers|Control|Records)_Main_\d` 950 cd 4000 K, none |
`^Light_(Security_Main|Lockers_Main|Control_Main[LR]|Records_Main)$` 1000 cd 4000 K Medium | `^Light_Corr_Main_[13579]$` 1150 cd vol 0.12 none |
`^Light_Corr_Main_\d+$` 1150 cd vol 0.12 Low | `^Light_Lobby_Batt` 900 cd Low | `^Light_Dock_Batt` 520 cd none | `_Batt` 340 cd none |
red accents unchanged | `^Light_Prep_Main_0$` Medium, other prep none | Vet/WingS/Cold Low | `^Light_Cold_Blue` 45 cd down | StaffExit, Server, first Gift and Break fixture Medium,
the other Gift/Break fixtures none | `^Light_PlazaLamp`, `^Light_Canopy_Down`, `^Light_Gallery_Down` no shadows.
`cretacea look_pass`: lights calibrated=174, unmatched=0.

## Performance (measured, not proven)
One editor (mine), Play mode, game view 1920x1080, 10 s averages (frames / realtime) at fixed spots, base (941668e builder +
lamp table, rebuilt in the same editor) and branch back to back:

| Spot | Base fps | Branch fps |
|---|---|---|
| Night, lobby (-31,0,-8) yaw 180 | 17.6 | 18.1, 17.2 |
| Night, corridor (-45,0,20.6) yaw -90 | 20.2 | 14.5, 17.8, 18.6 |
| Night, plaza (-32,0,-60) yaw 0 | 24.1 | 18.3 |
| Corridor chapter, lobby, flashlight on | 20.5 | 18.4 |

Five other agents' Unity editors were running on this Mac at 28-67% CPU each. The readings are noise: in the branch corridor,
switching OFF every local light's volumetric and then every local shadow at runtime gave 14.4 and 13.5 fps, i.e. lower than
with them on. GPU frame time read 50-90 ms everywhere, base included. So these numbers cannot prove or disprove 60 fps; they
do not show the 34-45% drop of round 1 either. The branch readings were taken before the last cut (no fog on the secondary
fixtures). The 60 fps check needs a quiet machine and a windowed editor.

## Verified (I ran it or looked at the capture)
- 0 compile errors, 0 warnings. Final `build_all_scenes` with NavMesh: status ok, `warnings: []`, 18 doors, 5 documents.
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, raptor and T. rex NavMesh valid.
- NavMesh legs (raptor agent), same as base except the mezzanine (Complete now, Partial at base): lobby-raptor in/flee Complete
  22/52 m, plaza axis and both bench rows Complete, lobby->staff exit Complete; security->records, records->lobby, prep->dock,
  cold store Partial at base and branch alike (closed doors).
- Play-mode smoke test: `jump_chapter Night`, `Corridor`, `ServicePaths`: 0 console errors, 0 warnings. In one of six Play
  sessions (entered right after an edit-mode `look_preview` + capture) HDRP logged once "ScriptableRuntimeReflectionSystemSettings.system
  is assigned more than once" (engine-internal, HDRuntimeReflectionSystem re-registered; no Zone 1 code touches it). It did not
  appear in the base session or in the other branch sessions, including the final smoke test.
- Captures I looked at: power on (play Night) Records, Lockers, Server, Control, Corridor, Break, Gift, Feed prep, Security, lobby;
  blackout (play Corridor) Security, Lockers, Records, Server, Control, Corridor, Break, Gift, Staff exit, Feed prep, Cold, Vet,
  Wing S, Dock, lobby; plus plaza, canopy (night and day), mezzanine, desk clutter, raptor beat. NOT captured with power on this
  round: Staff exit, Cold, Vet, Wing S, Dock.

## Not verified / rough edges
- 60 fps (see above).
- The status map's console neighbours (control_console FBX screens) still glow in the blackout: their material belongs to the
  model, not to Zone 1.
- Ceiling washes are still a stand-in for bounce light; the ceilings read dim-grey, not black, with a broad soft brightening.
- Blackout rooms are dark outside their battery pools (by design ~35 lux pools); with the flashlight off, Lockers and Control
  read mostly as silhouettes from the door.
- The wing raptor killed the player during my first Cold Storage/Wing S blackout shots; for capture 13 I deactivated it at runtime.
- The break-room quad in capture 10 still shows the old solid vending cabinet (captured before that fix); the close-up next to it
  is the fixed one.

## Requests for the lead
- Palette `AttachCleanCollider`: size the BoxCollider from `MeshFilter.sharedMesh.bounds` in local space (my refit pass works
  around it for Zone 1 only; other zones' rotated props have the same 90-degree boxes).
- Palette `EmissiveCoolWhite` at 170 nits blooms into a round orb seen down a corridor; Zone 1 uses its own 70-nit tube.
- Palette `DrywallPaint` is beige (red/blue 1.8); a neutral variant would help every zone.
- Kit lamp bodies for the new troffers, washes (should stay invisible) and battery units.
