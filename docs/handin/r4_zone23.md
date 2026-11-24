# Round 4 hand-in: zone23 (service paths, E-0 gate, ending warehouse and wall, substation, coast road)

Branch `r4/zone23`, base `3987803`. Took over a cut-off session: its working tree held only regenerated scene/material
files, three procedural textures (`Assets/Art/UI/Signs/paddock4_arrow.png`, `Assets/Art/Decals/z23_door_stencil.png`,
`z23_gate_stencil.png`, made by `Tools/agent/make_r4_art.py`) and a set of before captures (`Screenshots/r4_before`). No
code had been changed. I kept the two textures I use, deleted the unused gate stencil and wrote all the code below.
Captures: `Screenshots/handin_r4/` (16 files + INDEX.md). Camera script: `Tools/agent/r4beats.py`, runner `r4run.sh`.

## Goal 1 (major): service path, frame 15 — done
- Right wall (`Wall_Seg3_S`): a steel pump-room door on a flood sill (frame, green leaf with stiffener panel, louvred
  vent, kick plate, lever, hinges, hasp and padlock, white "PUMP RM P-2 / KEEP CLEAR" stencil, rust run), a HV sign
  beside it. The wall detail keeps the door span clear (`Zone23Dressing.DoorSpans`, seed 6) and the conduit hops over
  the door head.
- Back wall (`Wall_Seg4_NE`, the low fence-topped panel): a 1.9 x 0.95 m amber PADDOCK 4 arrow sign on four stand-offs.
- Left wall (`Wall_Seg3_N`): ferns on the low cap and the full coping, vines hanging over both, tree fern, banana and
  monstera pressing on the fence.
- A trench drain with steel edges and a grating across the lane at the corner (walk-over, no collider).
- Every path wall: a strong runoff streak under each coping joint of the full-height runs (`JointRunoff_*`).
- Panel tints lowered: PanelC 1.42-1.48 -> 1.0-1.04, PanelA 1.10 -> 0.95, coping 1.75 -> 1.15.
- Proof: 01/02. Measured on frame 15 (mean RGB): left wall 35 -> 19, back wall 83/77/73 -> 76/70/66, right wall 18 -> 12.
  The wet look of the walls themselves is the lead's WetnessDriver change (not in this branch).

## Goal 2 (major): closed E-0 gate — done
- The chain-link leaf boxes keep their colliders but draw nothing (no halftone dots). Each leaf is a welded palisade:
  18 bars 5 cm every 21.5 cm, 10 cm stiles, a rail over a solid 1 m kick plate with hazard-stripe decals on both faces
  (children of the hinge, they swing with the leaf), a brace. All collider-free children of the hinges.
- A signal head on each leaf's yard face, outboard of the E-0 lightbox: the red lens faces the yard while closed; the
  green lens faces the leaf's own hinge, which is the yard side once the leaf has swung out 95 degrees. A deep cheek on
  the red lens hides it from the yard when open. No runtime script was needed (I own no runtime code).
- Hinge posts in the jambs and a swing-gate motor housing on the wall's yard face beside each (grey box, hood, vent,
  yellow label, arm bracket, conduit into the slab).
- Proof: 03/04 (closed), 05/06 (open: green shows, red hidden), 07/08 (from the yard drive at 40 m: an empty doorway
  before, a barred screen with two red lamps after; 60 m view in `r4_final2/y3_gate_60m.png` reads the same).
- Not done: the 1080p re-capture the critic asked for (I only captured 720p).

## Goal 3 (major): ending crane shot — partly done
- `LookRoofs.PitchedShedRoof`: metal sheds over 900 m² (Warehouse B) get a 6-degree duo-pitch grey sheet roof instead
  of the flat black cap: two slopes, gable infills, ridge capping, five ridge vents, three skylight strips and four
  rust patches per slope, eaves gutters, four downpipes per side, rain stains under the gutters (decals to 220 m).
- Yard perimeter, inside faces of the west and north walls (what the crane looks across): a cast tone per 4 m bay,
  a shallow pier every 8 m, an algae band and runoff streaks (decals to 200 m), a security fence on the coping (posts
  with cranked arms, top rail, mid wire, three barbed wires).
- Proof: 09/10. The roof now reads as a pitched sheet roof with skylights. The far wall reads as precast bays with piers,
  but at that distance the stains and the fence are faint: it is better, not a hero wall. Trees and haze are not mine.

## Goal 4 (minors)
- Transfer lever — done: the animated arm sat inside the transfer pedestal model (that is why the pose never changed).
  It is now a knife-switch lever with an A/B plate on the pedestal's west face that swings 90 degrees on the throw
  (11 -> 12). No clunk/spark (needs runtime code) and no second mast.
- Bollard collar — done: AuxRoute collars get their own lens (0.25, 1.0, 0.45) at 7 nits, swapped in `LookRoofs.Apply`
  after `LookFixtures` builds them (the palette green stays for LEDs). 13 -> 14: no white core.
- Substation foreground slab (frame 19) — NOT fixed. I tried splitting the pad's renderer into 6 x 6 tiles (each judged
  by its own roof probe in `WetnessDriver.Build`): the near slab stayed exactly the same (bottom rows mean 64/43/34 before,
  65/43/34 after), so the cause is not the single probe at the pad centre. I reverted it. Frame 16 documents it.
- Coast road sea view — not attempted (time). What blocks it, per review 3: the road runs straight between grass verges
  to the overlook and the storm haze puts the far sea at the sky's value; a lit surf line or the boat lights would need
  Zone 4 / look-system work.

## Goal 5 (minor): knocked-down bollard — done
`DressFlattenedBollard`: a base flange with torn bolt holes, a yellow reflective band, a smashed lens with a dark hole,
rim shards and shards in the gravel, the concrete footing it was torn from with sheared bolts, a torn cable from the
footing to the flange, a scrape decal. Proof: 15 (crop, before | after).

## Checks
- Compile: 0 errors, 0 warnings. Console: 0 errors after builds and every Play session.
- Final build with NavMesh (both NavMeshes baked).
- `route_test.py`: 45/45 on the final build (`Tools/agent/route_r4.log`, 306 s); second run after the last
  collider-free gate change (red-lens cheek): 45/45 again (`Tools/agent/route_r4b.log`, 303 s).
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid (`Tools/agent/doors_r4.log`).
- Perf (`perf_here.sh`, yard view 24, 1280x720 game view): before gpu 6.4 ms / cpu 31.2 ms, 1.57 M tris; after gpu
  4.9 ms / cpu 23.5 ms, 1.57 M tris (CPU numbers in the hidden editor are noisy; no regression).

## Files
- `Assets/Editor/WorldBuilder/Zone2_ServicePathsBuilder.cs` (DressCorner2Faces, DoorSpans, joint runoff, tints, DressFlattenedBollard)
- `Assets/Editor/WorldBuilder/Zone3_SubstationAndYardBuilder.cs` (E-0 leaves, signals, motors; wall inside faces and fence; transfer lever)
- `Assets/Editor/LookPass/LookRoofs.cs` (PitchedShedRoof, route bollard collar lens)
- New art: `Assets/Art/UI/Signs/paddock4_arrow.png`, `Assets/Art/Decals/z23_door_stencil.png` (procedural, `Tools/agent/make_r4_art.py`)
- New materials: `MAT_Z23_GateSignalRed/Green`, `MAT_Z23_BollardCollarGreen`, `MAT_Z23_RoofSheetGrey`, `MAT_Z23_RoofSkylightGRP`,
  `MAT_Z23_RoofRustPatch`, `MAT_Z23_DecalDoorStencil`, `MAT_Z23_SignPaddock4Arrow`, `MAT_Z23_ReflectorWhite`
- No lamp rules added.

## Requests for the lead
1. Frame 19 slab: please look at `WetnessDriver` for `Substation_ConcretePad` (36 x 34 m, ConcreteCracked, not merged):
   splitting it did not change the near rows, so the cause is elsewhere (wet clone of ConcreteCracked too weak, or
   the RoofOcclusionMap covering the west end).
2. Transfer lever payoff: a clunk and a spark at the end of the hold, and the SubMast floods in the StoryDirector light
   ripple (runtime code).
3. Coast road: a lit surf line / boat lights on the road axis (Zone 4 / look system).

## Known rough edges
- The perimeter fence and the wall stains are faint from the crane; the bay tones can read as a patchwork up close.
- The hinge post slightly overlaps the leaf's hinge stile (both dark/galvanised, not noticeable in the captures).
- 1080p gate capture not taken.
