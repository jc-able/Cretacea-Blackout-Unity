# Round 3 hand-in: zone1 (Visitor Center ceilings, room dressing, plaza islands, kiosk back, night exterior, roofs)

Branch `r3/zone1` from `5c57ee7`. Clone `~/CretaceaUnityClones/zone1`. Captures: `Screenshots/handin_r3/` (each PNG = same
view, top before / bottom after), index in `Screenshots/handin_r3/INDEX.md`. Raw sets: `Screenshots/r3_before`, `r3_e` (final).

## Files touched (all mine)
- `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs`: `CeilingFill()` + ceiling light layer, battery cleanup,
  `TreeIsland()` rewrite + uplight, kiosk back/sides, `DressSecurityWestExterior()`, staff-exit plaster + streaks,
  `DressRoomsRound3()` (+ helpers Whiteboard, WallPhone, KeyCabinet, Mat, BoxStack, ChairStack, Scuffs).
- `Assets/Editor/LookPass/LookLights.Zone1.cs`: rules below.
- `Assets/Editor/LookPass/LookFixtures.cs`: room battery units hung at 2.45 m and aimed 60 deg down/out; `_CeilFill` skipped (no
  fixture: a virtual bounce); `Light_TreeUp` -> floor can.
- `Assets/Editor/LookPass/LookRoofs.cs`: Zone 1 only (non-metal caps under `Zone_01_VisitorCenter`): gravel ballast, paver
  walkway, skylights, vent stacks, roof hatch. Other zones unchanged.
- New materials `MAT_Z1_KerbRound`, `MAT_Z1_Mulch`, `MAT_Z1_RoofGravel`, `MAT_Z1_SkylightGlass`; changed `MAT_Z1_SagePlaster`.
- Helpers `Tools/agent/z1.sh`, `Tools/agent/z1shots.sh` (pinned to this clone).

## Goals
| # | Finding | Status | What changed / evidence |
|---|---|---|---|
| 1 | Charcoal ceilings with blotchy halos (major) | **fixed** | One shadowless upward spot 7 m under every <= 6 m ceiling cell (`CeilingFill`, 90 deg, inner 66 deg, 600 cd, 12.5 m), on a new ceiling-only light layer (bit 7, set on the ceiling slabs in `TagWashReceivers`), in VCMain (dark in the blackout). Every room with washes except the control room (already fine), plus the corridor (7 m cells). Companion troffers (Grid / Break_Main_n / GiftShop_n) are now Shadow.None, so they no longer carry a floor bounce each (the per-troffer ovals). Measured ceiling median sRGB before -> after: office 41 -> 74, lockers 42 -> 78, break 39 -> 69, gift 20 -> 73 (centre no longer black, the source-less oval gone), corridor 28 -> 55; walls 56-117 unchanged within noise. A first version (12 m deep, 24 m range) overflowed HDRP's light lists (blocky patches): the 7 m / 12.5 m version shows none in 01-05. Designed with the lead's bounce 0.95 in mind: only each room's shadowed main troffer keeps a bounce, so the extra light lands round one fixture per room. NOT tested with `BounceShare` 0.95 locally (time). |
| 2 | Rooms read as empty greybox halls (major) | **partly** | `DressRoomsRound3`: security office (2 whiteboards with writing, key cabinet, wall phone, switch, door mat, 3 box stacks, bin, cable run + drop, scuffs), break room (roster + menu boards, clock, wall phone, extinguisher, door mat, 4.6x2.8 m rug, credenza with kettle/papers, 2 chair stacks, bin, box stack, plant, scuffs), gift shop (door mat, 3 stock stacks, bin, utility cart, clock, extinguisher, card spinner, plush bin, scuffs), feed prep (hook rail with hanging meat, hose reel, 3 tubs, drain, scuffs). All within 0.9 m of a wall, away from door lines. Not done: partitions / half-height walls / extra furniture groups in the middle of the big rooms (would touch walking space); the lockers/records placeholder boxes (minor). |
| 3 | Blackout battery units (blocker support) | **done** | One unit per room, by the exit: removed Security_Batt_1, Control 2 -> 1 (by the door), Records_Batt_1, duplicate Cold/Vet units; Break unit moved off the door head. Room/corridor units hang at 2.45 m, light 0.35 m off the wall, aimed 60 deg down and out with a 120 deg cone (top edge level: nothing reaches the ceiling junction), rule without Throw.Down so they carry no bounce or wall fill (300 cd). 06/07: the remaining bright ceiling patch is the flashlight bounce (lead). |
| 4 | Green peeling plaster = giant crackle wallpaper (major) | **fixed** | Staff exit uses the set's white-paint variant (no red-brown blotches) tinted sage, 1.25 m tile, normal 0.45; 9 leak streaks + 3 scuffs at irregular spacing (08). |
| 5 | Plaza tree islands read as blockout (major) | **fixed** | Round precast kerb (two 20-sided cylinders turned half a facet + coping lip) in mid-grey concrete (`MAT_Z1_KerbRound`), mounded leaf-mulch bed (`MAT_Z1_Mulch`), tree + 10 ground-cover clumps (scale 1.2-2.0). The 14 old kerb boxes stay as invisible colliders (route and NavMesh unchanged). Warm can uplight per island (Plaza group) for dusk/night (09, 10, 15). |
| 6 | Back of the ticket kiosk (major) | **done** | Back: framed steel door with handle, lock, kick plate and STAFF ONLY plate, louvred vent, framed poster, trim, kick band, downpipe, gutter, rafter ends, top sign printed on both sides. Sides: framed park-map board with a yellow price strip (11, 12: intro_05 shows the side board; the back faces the axis). |
| 7 | First night exterior (major) | **done** | West wall of the security office: fascia band + gutter, 2 downpipes with shoes and brackets, condenser on a pad with line set, meter box with HIGH VOLTAGE plate and conduit to the wall pack, drip streaks, 1.5 m concrete apron with puddles, two more warm wall packs (scallops). 13 (after = free camera at the shot start; the intro frame grid missed the shot). Not done: rain on the glass, camera re-blocking (StoryDirector is not mine). |
| 8 | 'Closing time' aerial: blank roofs, black planters (major) | **done** | Zone 1 roofs: gravel ballast, walkway, 1-4 skylights, vents, hatch (14). Planters: lighter kerbs + planting + uplights (15). |

## Lamp rules (LookLights.Zone1.cs)
- NEW `_CeilFill(_\d+)?$` 600 cd, 5000 K, 12.5 m, no shadow, angle kept from the builder (90/66), first in the table.
- NEW `^Light_TreeUp` 520 cd, 3000 K, 9 m, 46 deg. NEW `^Light_SecWest_WallPack_\d$` 420 cd, 2700 K, 8 m.
- CHANGED VC/corridor `_Batt`: 300 cd, 120 deg, no Throw.Down (aim kept, no bounce). Companions `..._Grid\d+`,
  `^Light_GiftShop_\d`, `^Light_Break_Main_\d`: Shadow.Low -> None.

## Verification
- `unity recompile`: 0 errors, 0 warnings. Final `build_all_scenes` WITH NavMesh: status ok, both NavMeshes baked, warnings [].
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid.
- `route_test.py --project <clone> --out Screenshots/route_r3`: **45/45**, "no new console errors" (291 s).
- Console after the final build and Play sessions: 0 errors; the only warnings are "Failed to create agent because there is no
  valid NavMesh", all stamped before the NavMesh build (the no-NavMesh capture sessions).
- Perf (hidden editor, 3 other clone editors running, so CPU numbers are not comparable): office_on GPU 9.3 ms with the ceiling
  fills vs 8.3 ms with all 56 fills disabled in the same session; gift shop 5.7 ms GPU; plaza GoldenDay 7.3 ms GPU. No
  before/after at base was measured (time).
- Looked at: every capture in handin_r3 (contact sheets), plus intermediate sets r3_a/b/c/d.

## Requests for the lead
1. With `BounceShare` 0.95 please glance at office_on / breakroom_on: the main troffer's bounce oval will be brighter; if it
   reads as a hot spot, the `_CeilFill` candela (600) can drop to ~450.
2. office_dark / records_dark: the ceiling patch at the wall junction is the flashlight bounce (your fix in flight).
3. The 23:04 shot would gain from the low 30-40 deg angle the critic asked for (StoryDirector shot 6), now that the wall has
   things on it, and rain streaks in the wall packs' cones.

## Not verified / rough edges
- Not tested with the lead's bounce 0.95, cooler indoor grade or blackout exposure floor.
- Kiosk back seen only at intro resolution (the back faces the axis; the side board is what shot 2 shows close up).
- Dressing is wall-side only; the break room and gift shop centres are still open floor.
- Feed prep and records not re-captured after the dressing (route test walked them: 45/45).

## Fix round (after the art director's review of this hand-in)
Same files as above plus the new `MAT_Z1_RoofCoping`. Captures: `Screenshots/handin_r3/` (refreshed, new INDEX.md; the round 3
set is kept in `Screenshots/handin_r3_prev/`); raw fix-round sets `Screenshots/fx/a` (first pass), `fx/b95` (bounce 0.95 test),
`fx/d` (final build). Measurements: `Tools/agent/ceilstat.py` (ceiling band of the frame, lens blocks excluded).

| Finding | What I did | Evidence |
|---|---|---|
| Rooms still read as empty greybox halls (high) | `DressRoomCentresFixRound`: break room west-wall kitchenette (fridge with notes, 4.1 m base run with double sink, tap, dish rack, microwave, coffee machine, tiled splashback, wall cabinets), two lunch groups for four in the open south half, two armchairs + a plant round the lounge sofa, the rug moved under the lounge set (no slab mid-floor), extinguisher moved off the kitchenette wall; gift shop round two-tier plush display in the open north-west floor + two 3.2 m gondola runs in the south half; security office second desk pair face to face east of the spawn (monitors, keyboards, paper, chairs); feed prep double sink on the west wall, two steel prep tables north-west (cutting board, cleaver, scale), walk-in freezer door with latch, hinges, plate and thermostat on the north wall. Every group keeps >= 1.5 m aisles and stays off the door-to-door lines (checked against a collider dump of each room, `Tools/agent/fx_rooms.cs`). | 01-05; route test 45/45 (it teleports, so it does not walk the new colliders: the aisles were laid out from the collider dump, not walked) |
| Blackout battery unit never shown; junction patch brightest (high) | Captured every blackout room facing its exit, beam off and on. No code change needed: with the beam off the brightest thing in each frame is the kept unit's pool by the exit (office 124, records 119, break room 117 /255) and the brightest 32 px block in the ceiling band is 16-31 /255; from review camera 06 with the beam off the ceiling band max is 16. With the beam on the brightest block is the beam's hot spot at the crosshair (227-240); the ceiling glow (107 office, 140 records) appears only with the beam, i.e. it is the flashlight bounce (lead's file), not a battery unit. | 06, 07, 08 |
| Ceilings not tested at bounce 0.95 (medium) | Set `LookLights.BounceShare = 0.95f` locally, rebuilt, recaptured, reverted (not committed; `git diff` of LookLights.cs is empty). Ceiling median / max-block-to-median, 0.45 -> 0.95: office 61/1.93 -> 62/1.93, break room 60/1.38 -> 60/1.45, gift shop 58/2.62 -> 58/2.74 (that max is the perimeter-wash band at the back junction, present at both), corridor 97/1.58 -> 98/1.59. No oval grows past ~1.3x locally; nothing to change. Also: every companion troffer and `Light_Break_Main_n` / `Light_GiftShop_n` now have no bounce at all (see the low finding), so only one bounce per room is left. | `Screenshots/fx/a` vs `fx/b95` |
| Z-fighting ring in every tree island; uplight buried (medium) | Mulch top 0.475 (1 cm proud of the 0.465 coping), mound y 0.41; `Light_TreeUp` sits 5 cm above the bed surface computed at its radius, so the floor can's lens shows; aim unchanged (trunk/crown). | 10, 11 |
| Staff-exit wall still a repeating crackle wallpaper (medium) | `MAT_Z1_SagePlaster` = plain sage paint on the drywall set (3 m tile, exactly the critic's values). Decay only in decals: 9 leak streaks 1.2-2.0 m wide at fade 0.9-1.0, 7 crack decals per wall at random height (0.9-2.85 m), size (0.8-1.8 m) and roll, irregular spacing (1.9-4.3 m), scuffs at fade 0.6. Fixed the stale "DressStaffExitWalls" comment. | 09 (in the blackout the decals read faintly; the repeat is gone) |
| 23:04 exterior still dark and flat; dressing leaves the frame (medium) | Interest moved next to the window: dummy steel door (frame, closer, lever, kick plate, step) with a printed SECURITY plaque at z 31.3, condenser to z 32.9 in light enamel with a slatted grille, fan hub and rating plate (no longer a black cube), meter box (steel) + conduit to z 26.3, `Puddle_Window` under the sill (face-0.9, fade 1.0, it now catches the window glow), drip under the door head, the wall itself on `ConcreteExterior`. Tried a cool moon fill on the wall (wall 13 -> 29-54 /255) and REMOVED it: it made HDRP drop lights in blocks on that wall (light-list overflow; with the ceiling fills disabled at runtime the blocks vanished: 14 lights already reach that wall plus the office fills). Without it the wall between the scallops stays ~13 /255. Shot 6 ends 1.67 m from the wall inside the window (StoryDirector, not mine): see requests. | 12, 13 (free camera on the glide path at 0 / 2.5 / 4.5 s) |
| 56 shadowless ceiling fills: GPU, light lists, light through walls (medium) | Fill rects kept in `s_ceilFillRects`; `TagWashReceivers` gives the ceiling-only layer only to slabs centred inside one (control room, mezzanine deck, canopies no longer get it). `_CeilFill` 850 cd / 10 m; corridor fills 1500 cd (its ceiling was ~17 % of the walls); gift shop in 7.5 m cells (9 fills, not 16); feed prep gets 9 fills (its ceiling was black: median 11 -> 51). Total 58 fills. Perf in one session, fills on vs off (hidden editor, other clone editors running, CPU-bound at 20-30 ms, so treat GPU as +-1 ms): office 11.0 vs 9.0 ms GPU, gift 4.6 vs 4.1, control 7.8 vs 7.0, lobby 4.5 vs 4.3. A base-commit A/B was not run (time). | `Tools/agent/fx_final.log`; 16 |
| Aerial roofs still flat slabs (medium) | `LookRoofs` Zone 1: 1 HVAC unit per cap over 60 m2, 2 over 200 m2 (0.15 m plinth, 2.2 x 1.3 x 1.1 m dark cabinet with louvre bands, fan ring/disc/hub, duct), replacing the generic units on Zone 1 caps; skylights 2.4 x 1.6 m on 0.45 m concrete curbs; 0.15 m pale coping band (`MAT_Z1_RoofCoping`) on Zone 1 parapets; walkway, skylights, hatch and units are keep-out rects grown by 1 m, vents and units retry until clear. Other zones unchanged. | 14, 15 (the intro grid frames 16/17 are the dusk and night aerials of this build) |
| Shadow.None on companions, GiftShop_n, Break_Main_n (low) | Rules back to Shadow.Low (range and cached shadow as at base); `ThinCompanionBounce` now removes every companion's bounce and fill; new `StripZone1SecondaryBounces` removes Bounce/Bounce_Fill from `Light_(GiftShop|Break_Main)_n` after calibration. | 01, 05 |
| Remaining primitives and small rule breaks (low) | The finding text reached me truncated after "The ki..."; not addressed beyond the above. | - |

### Verification (fix round)
- `unity recompile`: 0 errors, 0 warnings. Final `build_all_scenes` WITH NavMesh: status ok, both NavMeshes baked, warnings [].
- `verify_playmode_doors`: passed, 18/18 doors, 5/5 documents, both NavMeshes valid.
- `route_test.py --out Screenshots/route_fx2`: 45/45, no new console errors (255 s). (An earlier run on the same code gave 44/45: the player
  fell through at the Paddock, x 128, chapter 4, far from Zone 1; the rerun passed.)
- Play-mode smoke (Intro, Night, Corridor): console 0 errors. The console history holds ArgumentNullExceptions from
  `LightGroupMember.ApplyFactorImmediate` stamped 15:56:32: I recompiled while Play mode was still on, and a hot reload drops the
  member's MaterialPropertyBlock (`_cached` survives the reload, `_mpb` does not). Not from this code; worth hardening (see requests).

### Requests for the lead (fix round)
1. Blackout ceiling glow: with the beam off the junction is dark (16 /255); the glow is the flashlight bounce.
2. StoryDirector shot 6 ends 1.67 m from the wall at FOV 38 (frame ~2 m wide, inside the 3 m window). Ending at ~4 m (or the low
   30-40 deg angle review 2 asked for) keeps the door, puddle, meter box and condenser in frame.
3. The office's west wall is near HDRP's light-list cap (14 lights reach it plus the office ceiling fills): any new light there drops
   out in blocks. A bigger per-cell list or fewer office companions would give room for a moon fill on that wall.
4. `LightGroupMember.CacheDefaults`: also re-create `_mpb` when it is null (hot reload in Play mode).
