# Round 4 hand-in: r4/rooms (Visitor Center rooms: dressing, placeholder props, first gameplay frame)

Branch `r4/rooms`, base `3987803`. Take-over of a package started by another engineer (cut off by a tool restart).
Kept from their work: the water-cooler rebuild (one recessed tap panel with two small white push taps, steel drip grille,
shouldered ribbed bottle, cup tube) and their helper drafts in `Tools/agent/r4_helpers.cs.txt` (keyboard, phone, logbook,
desk lamp, world text in the park's OFL fonts, notices, plates), which I finished, fixed and wired in.

Befores = `~/CretaceaUnityClones/_refs/final/**` (the lead's starting point). Afters = `Screenshots/handin_r4/` (see INDEX.md).

## Goal 1 (major) — bare halls: DONE for the four named views
`DressRoomsRound4` (new, called after `DressRoomsRound3`):
- **Lockers** (`lockers_on`): far wall above the locker run gets a STAFF LOCKERS plate, a wall clock, the park badge and a
  typed Facilities notice (they first sat 2 cm behind `Lining_Lockers_N`: the liner face is z 33.847, fixed); west wall:
  a mirror over a shelf with toiletries, a paper-towel dispenser and a green FIRST AID cabinet; east wall south of the
  locker bank: three hanging jackets on the existing hooks (replace the flat yellow `RainJacket` slab), a cork board and a
  SHOWERS sheet; floor: rubber mats in front of both banks, a slotted drain with grime, a wet-floor sign (prop model).
- **Staff corridor** (`corridor_on`): a water main (green) and a red sprinkler feed with hangers every 2.5 m and brass
  sprinkler heads along the south side of the ceiling; door plates on the latch side of all eight doors; two cork boards;
  three typed notices (radio check, storm drill, master keys); three red extinguisher cabinets with glass doors and labels;
  the utility cart (prop model, collider) parked tight to the south wall at x -38.6 (2.3 m clear); floor wear and
  wainscot scuff decals.
- **Lobby** (`lobby_on`, north half): brass stanchions with a velvet rope round the T. rex plinth, a brass plaque lectern
  facing the entrance, a dark granite inlay band round the exhibit and two inlay lines on the entrance axis, a rubber
  entrance mat; under the mezzanine a green FOUNDING FRIENDS donor wall (24 brass name plates) left of the corridor link and
  a 5 x 3 m park-map mural right of it; two banners on the north wall above the mezzanine; two museum display cases
  (sickle claw, horn core; granite plinths with brass labels, colliders) north of the skeleton; a round information desk
  with an "i INFORMATION" sign (collider) at (-23.4, 12.6), off the walking line.
- **Feed prep** (`route/12_ch3_feed_prep`): the route camera faces the south wall, so the sinks/hooks/freezer are brought
  to it: a second double sink counter between the two doors (kept 1.6 m from door_cold's swing), knife rail and a WASH
  HANDS sheet, a meat rail with four hooks and two hanging quarters (prop model) right of it, a blue COLD STORE -18 C plate
  over door_cold and a digital thermometer beside it.

## Goal 2 (major) — placeholder primitives: DONE
- Locker towel: a folded three-layer towel with a rolled fold edge and a woven stripe (`FoldedTowel`).
- Records fiche reader: base, film stage with glass, column, hood with a slanted bezel and lit screen, vents, two knobs.
- Gift-shop blank boxes: every wall-shelf and gondola item now carries a product print (small items used to skip it; the
  extra prints draw no random numbers, so the shelf layout is unchanged); every stock carton in `BoxStack` is printed.
- Feed-prep under-table "cream box": lidded HDPE food tubs with rim, handles and marker labels ("FISH 20kg").
- Break-room fridge: white enamel body with two proud doors and a gap, rounded top edge, dark pull handles, kick grille,
  badge, notes held by magnets.

## Goal 3 (major) — first gameplay frame: DONE (models), light untouched
- Keyboard: beige 1990s board with five key rows, modifiers, F-row and numpad; beige mouse on a felt pad.
- Desk phone: sloped beige body, handset in its cradle, 12-key pad, line buttons, coiled cord.
- Logbook: open green ring binder, ruled pages, two pages of real handwriting (Caveat), a pen.
- Water cooler: as described above (kept from the previous engineer).
- Whiteboards (all four): real marker handwriting (Kalam): the security board is the night-shift checklist, the shift board
  a duty roster, break-room boards a fridge notice and the menu.
- Desk lamp: balanced-arm lamp, round weighted base, twin arms with springs, conical shade with rim and bulb, cable. Its
  light (`Light_Security_DeskTask`) is not touched (r4/lights owns it).

## Goal 4 (minors)
- Control-room monitors: DONE. The four khaki feeds are now a cool emissive night-camera picture (`MAT_Z1_CctvFeed`) with a
  camera ID and a time stamp on each; the overlay dies with the screens in the blackout (child of the toggled object).
- Blank grey panel beside the far corridor door: NOT DONE (could not identify the object in the time box; the new door
  plates and notices now sit along that wall).
- Desk pendant canopy on the troffer lens, gallery glass/sign, main-building roof landmark: NOT DONE (time box; the pendant
  and troffer grid also touch r4/lights' `Light_*` / LookFixtures area).

## Goal 5 (minor) — flashlight pickup after it is taken: DONE
`FlashlightPickup.Update` hides the desk torch (and its LED light) as soon as `GameState.HasFlashlight` is true by any path
(Continue, jump_chapter, checkpoints, the capture tools). `ResetForChapter1` runs after `SetHasFlashlight(false)`, so a
chapter-1 restart still shows it.

## Files touched
- `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs` (no loading-dock or `Light_*` lines changed)
- `Assets/Scripts/Interact/FlashlightPickup.cs` (Update only)
- New materials (generated by the builder): `MAT_Z1_BeigeABS`, `Keycap`, `KeycapDark`, `FridgeEnamel`, `Towel`, `JacketRed`,
  `JacketNavy`, `JacketYellow`, `RubberMat`, `Brass`, `FirstAidGreen`, `FeltBoard`, `Mirror`, `VelvetRope`, `FossilBone`,
  `TubHDPE`, `CctvFeed`, and `Text/MAT_Z1_Ink_*` (TMP HDRP-lit SDF per font and ink colour).
- No lamp rules added, no new lights.

## Requests for the lead
- intro_20 caption: "[THUNDER CRACKS OVERHEAD]" still covers the desk in the first gameplay seconds (StoryDirector timing).
- The flashlight hot spot still clips on the feed-prep white tile (FlashlightController budget, look system).
- Lobby colour (sepia, 3600 K lamps) is a lamp-table change (r4/lights).

## Verification
See the Verification section appended below (route test, doors, console, perf).

### Verification (done, on the final build with NavMesh)
- `unity recompile`: 0 errors, 0 warnings. `build_all_scenes` (NavMesh baked): no warnings, both NavMeshes baked.
- `route_test.py`: **45 / 45** (`Screenshots/route_r4`), "no new console errors during the run".
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, flashlight OK, raptor and T. rex NavMeshes valid.
- Play-mode capture runs (Night and Records checkpoints): console errors 0 (the only listed error is a stale compile error
  from a mid-edit recompile, since fixed). Warnings seen: HDRP "ScriptableRuntimeReflectionSystemSettings.system is
  assigned more than once" (engine, not from this package) and "Failed to create agent because there is no valid NavMesh"
  from the intermediate captures taken on scenes built with `--bake_navmesh false` (gone on the NavMesh build: route test clean).
- Looked at: lockers, corridor, lobby, lobby north, feed prep, office, desk close-up, control room, office blackout desk crop
  (torch gone), all in Play mode.

### Not verified / rough edges
- Performance not measured (`perf_here.sh` not run in the time box). The package adds about 1,500 small primitives,
  which `ConsolidateStaticPrimitives` merges per material and 16 m cell, about 90 TextMeshPro labels (no shadows), no lights.
- The phone and the 'Kalam' whiteboard text were seen only at room distance; the gift-shop and break-room afters were not
  inspected closely (captured in handin_r4 16-19).
- The lobby is still lit sepia (lamp table) and its north half is 25-30 m from the review camera, so the new dressing reads
  small in `lobby_on`; banners hung from the roof trusses would fill the upper half further.
- The control-room CCTV overlay text is lit (not emissive) TMP: legible in the lit room, gone with the screens in the blackout.
