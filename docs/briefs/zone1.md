# Package: ZONE 1, THE VISITOR CENTER  (branch `look/zone1`)

**Problem.** Most of the game's interiors are here and they read as grey-box blockouts: rooms without ceilings worth
looking at, bare-box furniture, an empty gift shop with a missing wall, stairs that dead-end, a floating canopy,
blank plaza, blackout rooms that are either pitch black or flooded red.
Read `docs/maps_2026-11-20/digest_zone1.md` then `notes_zone1.md` (room table with coordinates, ASCII plan, per-room
issues and dressing ideas). The kit, lamps-with-bodies, signs and probes are being fixed by another agent at kit
level: do not build fixture bodies or sign textures yourself; place lights and signs through the existing kit calls.

**You own:** `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs` and `Assets/Editor/LookPass/LookLights.Zone1.cs`.
Zone-specific materials: create them inline with `palette.Surface(...)` (see the Look Bible). Do not edit the kit,
the palette or other zones.

## Goals, in priority order
1. **Right material on every surface** (Look Bible §5): ceilings → `CeilingTiles` (offices, corridor, break room,
   gift shop, staff exit), office and corridor walls → `PlasterClean`/`DrywallPaint` with a darker dado or skirting
   where it helps, service areas → `PlasterPeeling90s`/`ConcreteBoardform`, lobby floor → `ConcreteFloorWet`
   (polished, puddles under the broken skylight panes), feed prep → `LabTileWhite` + `FloorTileWet`, plaza →
   `PavingStone` with kerbs. No surface may keep a material that does not match what it is.
2. **Fix every placeholder and defect in the digest:** gift shop west wall + shelves, counter, logo/banner dressing;
   mezzanine rail gap at the stair head (or a proper landing); dock canopy and gallery roof supports; bare-box
   furniture rebuilt as legged tables/counters/reception desk/vending machine with clutter (monitors, binders,
   chairs); CCTV bank flush to the wall; keeper note on the counter; corridor cable tray clear of the lights;
   puddles under the broken panes; `lobby_shutter_sparks` anchor + a control box by the shutter; ceiling inset
   slots closed (no light or rain leaks along walls); plaza kerbs, planters, benches, bins, lamp posts, a welcome
   canopy with `lobby_welcome` / `park_logo` over the entrance; a viewing rail built as posts + glass, not a slab.
3. **Blackout you can read.** Every room the player can enter has at least one battery emergency unit: warm white
   (3000–3500 K), low (name them `Light_<Room>_Batt`), and keep red for beacons and exit accents. Add rules for
   your new lights in `LookLights.Zone1.cs` (pools ≈ 35 lux, see Look Bible §4). Lockers, Server, Break, Gift Shop,
   Vet Clinic and Wing S currently have none. Power-on rooms must look like lit rooms; blackout rooms like rooms
   under emergency light — never a saturated monochrome.
4. **Lived-in detail** on the player's route (security office → corridor → records annex → lobby → feed prep →
   loading dock): posters and signs from `Assets/Art/UI/Signs`, wall-mounted extinguishers, bins, wet-floor sign,
   lockers, archive shelves, crates, decals (`DecalGrimeLeak`, `DecalCracks`, `DecalCautionStripes`,
   `DecalBloodSmear` sparingly). Nothing floating, nothing intersecting, nothing blocking a door swing or the
   walkable route (the route notes are in `docs/maps_2026-11-20/notes_story_flow.md`).

## Verify (Play mode)
`jump_chapter Night` (walk the office and corridor with the lights on: use
`unity command script eval --code 'Cretacea.Player.PlayerController.Instance.Teleport(new UnityEngine.Vector3(x,y,z), yaw, pitch); return "ok";'`),
`jump_chapter Corridor` (blackout), and free-camera views of every room and the plaza in both power states.
Prove the route still works: doors open, no new collider in a doorway
(`unity command cretacea verify_playmode_doors`).
