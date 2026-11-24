# Review 3 (Nov 20, after round 3) — segment `interiors` (score 6/10)

Real progress, but not amazing yet. The lit rooms finally have white drop ceilings, and the blackout now reads as a blackout: neutral grey, with warm battery islands and a beam that leads the eye. lobby_dark and the staff-exit collapse are genuine horror frames. What holds the segment back is greybox that did not change (black-box boots and a capsule bag in the middle of the locker room; an empty lobby, locker room and corridor), plus a white-tile hot spot that still clips, a flashlight that lights the records ceiling as brightly as the wall, and battery units that read as two floating eyes.

Strengths:
- Fixed since review 2 (charcoal ceilings): every lit room now has a light drop ceiling, sRGB 60-95 next to walls at 100-145. In review 2 they were 26-48, and the gift-shop centre was 14. The halos around each troffer are gone.
- Fixed (blackout sepia): the amber-olive cast is gone. office_dark measures R/G 1.04 and B/G 0.97, breakroom_dark 1.04/0.99, records_dark 1.09/0.92. The blackout is grey-blue with warm battery islands.
- Mostly fixed (blackout too bright): office_dark is 58 % as bright as the lit office (it was 79 %). Unlit walls sit at 24-27/255 and the battery pool is about 109 on the floor. The beam hot spot is the brightest thing in office_dark (189), breakroom_dark (185) and the exit-wall view (193), and it shows texture: CCTV static, whiteboard, door frame, dado rail.
- Fixed (staff-exit wallpaper): the wall is plain sage paint with no repeat. With the hanging tiles, rebar, rubble and rain falling through the hole, 08 is now a real horror beat.
- Fixed: furniture now sits on the floor. Under the break-room tables the floor reads 70 against 95 beside them, and under the locker bench 51-56 against 84. The corridor's wall/ceiling junction reads (right wall top 112, was 91), and the security-desk pendant now dies with the power.
- lobby_dark is the best frame in the segment: moonlit roof-grid shadows on the board-form wall, cold sky through the glass, and warm bone in the beam with a soft shaft. It has the Jurassic Park mood.
- The security office now reads as a working room: a second desk pair with monitors, a cork board, lockers, a water cooler and a plant. The CCTV wall turning to static in the blackout is a good story touch.
- Colour in the lit rooms is natural and neutral-warm (R/G 1.11-1.13, B/G 0.86-0.89) with a full tonal range. Only lamp lenses clip.
- With the flashlight off in the corridor, the red heads make small red pools (R/G 1.49 under the head) and red covers only 1.4 % of the frame. The ceiling stain decals in records_dark show what a grime pass adds.

## Findings

- **[major] records_dark.png** (lighting): The flashlight still paints a wide fan of light across the ceiling. In records_dark the brightest 32 px block in the frame is on the ceiling (x 512-576, y 240-272, luma 168), brighter than the beam on the brick under the crosshair (164). The fan (x 500-800, y 150-300) averages 109, about what review 2 measured (113); only the clipped patch is gone. In office_dark the fan covers x 400-850, y 100-330 at a mean of 138 (peak 233), so the beam reads as a soft glow running up onto the tiles rather than a pool on the wall. Review 2 status for 'blackout too bright / ceiling glow': PARTLY FIXED. The room is now 58 % as bright as the lit one (was 79 %), unlit walls are 24-27/255, and the beam leads in office_dark, breakroom_dark and the exit-wall view. This ceiling glow is what is left.
  - Likely cause: FlashlightController.cs parks the bounce point light (12,000 cd x BounceShare 0.006 x sqrt(adapt), about 30-35 cd) only BounceCeilingGap 1.2 m under the tiles. That puts about 20-25 lux on a pale ceiling. Meanwhile HotSpotMaxLux caps the darker painted brick (BrickOld) at 90 lux, a cap on illuminance, not on brightness. AtmosphereController.IndoorDiffuseBlackout = 3.0 then triples the screen-space GI, which already contains the beam's real bounce, so the bounce is counted twice and then multiplied by three.
  - Suggested fix: Keep the pool on the wall the brightest thing in frame:
- Raise BounceCeilingGap to at least 1.8 m and cut BounceShare to about 0.003 indoors (SSGI already carries the real bounce).
- Lower IndoorDiffuseBlackout to about 1.5 and get the unlit-wall level from a dim cool base instead (see the flashlight-off finding).
- Make the hot-spot budget depend on the albedo of the surface hit, so dark brick gets about 1.5x the lux.
Target: wall pool 150-200/255, ceiling fan at most half of that.
- **[major] 12_ch3_feed_prep.png** (exposure): The flashlight still clips on white tile. 1,364 px in the core are at 250 or above (x 608-693, y 342-401, mean 245; review 2: 251). extra/feedprep_white_tile.png is the same (1,321 px, mean 244). The grout only just survives inside a flat white disc ringed by glow, while both doorways sit at 8-11/255. That is still the 'white disc on black' the criteria rule out. Review 2 status: STILL THERE.
  - Likely cause: HotSpotMaxLux = 90 lux in FlashlightController.cs ignores albedo. Its own comment expects about 215/255 on white tile, but LabTileWhite (tint 0.95, smoothness up to 0.92, so a specular sheen on top) lands at about 250 at the EV 3.4 indoor exposure floor.
  - Suggested fix: Scale the hot-spot budget by the albedo of the surface hit: about 35-40 lux on LabTileWhite and PlasterClean, 90 on concrete, about 130 on brick and dark paint. The Look Pass can bake a material-to-mean-albedo table that the flashlight looks up from hit.collider's renderer. Target: core at most 215/255 with readable grout. Re-capture this exact view.
- **[major] office_dark_exit_wall_off.png** (geometry): The battery emergency unit is the only lit fixture in a blackout room, and it reads as two glowing eyes floating on the wall above its pool (x 506 and 528, y 314). No housing shows, even with the beam on that wall (extra/office_dark_exit_wall_on.png): just two orange discs in a red-brown haze. In a dinosaur horror game a player sees a pair of eyes, not a lamp, and it breaks 'no lamp without a fixture'. This is new in round 3.
  - Likely cause: LookFixtures.TwinHeadBody builds the BatteryBox and both head cans in s_p.PlasterClean, the wall's own material, and the round 3 aim (60 deg down, cone top edge level) never lights the body. The green ChargeLED is a 12 mm cube, invisible at room distance, and the lenses are hot enough to bloom into discs.
  - Suggested fix: Give the unit a look of its own:
- An off-white ABS body with a darker front band and a printed EMERGENCY label.
- Dark grey head cans.
- A 3-5 cm green charge LED at about 10 nits.
- A 2-3 cd shadowless point light a few cm in front of the box (or emissive lens rims) so the box silhouette shows.
Keep the lenses under about 40 nits.
- **[major] lockers_on.png** (geometry): Placeholder primitives are still in frame. In lockers_on the three pairs of boots under the centre bench are black boxes and the bag on the bench is a black capsule (x 524-716, y 444-512, dead centre of the room's view). Others:
- a blank cream box on the gift-shop shelf (giftshop_on x 115-225, y 534-612)
- the plain grey cube on the records side table (records_dark x 745-800, y 432-480)
- a blank white box on the feed-prep table's lower shelf (12_ch3 x 0-100, y 540-590)
- the new break-room fridge, a tan box with two bar handles that reads as a cardboard carton (breakroom_on x 0-70, y 350-460).
Review 2 status: STILL THERE. Only the break-room floor cube is gone, and the round 3 hand-in lists the rest as not addressed.
  - Likely cause: Zone1_VisitorCenterBuilder.cs l.2318-2324: Lock_Duffel_* is PrimitiveType.Capsule and Lock_BootL/R_* are 0.11x0.14x0.27 Boxes in mBlack. l.2365: Rec_FicheReader is a 0.45x0.5x0.5 Box. The fridge bodies (l.954 and l.2669) are plain boxes in mLaminate and mCabinet.
  - Suggested fix: Swap in models from Assets/Art, or build real shapes: boots with sole, heel and shaft; a canvas duffel with straps and a zip; a microfiche reader with a hood and a slanted screen; printed stock boxes. Make the fridges white or almond enamel (smoothness about 0.6) with a door gap, rounded corners and a kick grille.
- **[major] lockers_on.png** (geometry): Half of the spaces are still empty greybox halls.
- Locker room: unchanged from review 2 apart from the ceiling. Three locker banks, two benches, bare beige walls, bare floor.
- Lobby (lobby_on): unchanged. One poster across two long board-form walls, a vast empty polished floor with two benches, two plants and a bin, and one amber tone from floor to wall (R/G 1.26-1.31).
- Staff corridor: unchanged over its 80 m.
- Feed prep: the route view (12_ch3) is an empty tiled hall with two black doorways and one steel table. None of the new sinks, prep tables or freezer door is in it.
The break room is better (two more lunch groups, a credenza, chair stacks), but the front half of its frame is still bare floor. The office now reads as a working room. Review 2 status: PARTLY FIXED.
  - Likely cause: Per the r3_zone1 hand-in, DressRoomsRound3 and DressRoomCentresFixRound only dressed the office, break room, gift shop and feed prep. The lockers, lobby and corridor were not touched.
  - Suggested fix: - Locker room: towels and kit bags on the benches, a mirror-and-sink run, a notice board with rosters, a clock, a laundry cart, a few open lockers with clutter.
- Lobby: a reception desk, two hanging banners, a mural or donor wall, rope stanchions round the skeleton, info kiosks, and cool moonlight from the roof on the floor.
- Feed prep: put the sinks, prep tables, hooks and freezer door on the walls the route camera faces.
- Corridor: door number plates, notice boards, conduit runs.
- Then a grime decal pass on skirting and floors (the ceiling stains in records_dark show it works).
- **[minor] giftshop_on.png** (lighting): A bright oval on the ceiling just in front of the back wall (x 480-900, y 215-260, mean 111, peak 208) still contains nothing but a vent. The ceiling centre beside it is at 60. Review 2 status: PARTLY FIXED. The black centre is gone, but the oval with no source is still there. With only two troffers in view, the merchandise tables are lit by nothing the player can see.
  - Likely cause: The perimeter wash rule '_Wash(_\d)?$' in LookLights.Zone1.cs (450 cd, 150 deg) is aimed up at the wall/ceiling junction and still lands on the tiles.
  - Suggested fix: Aim the washes at the wall under the cornice, or drop them to about 250 cd, and add a centre row of troffers over the tables.
- **[minor] corridor_dark.png** (lighting): The red emergency lens still clips to pink-white (core 255,227,247 at x 458, y 200). A head next to the camera also floods the whole near right wall bay dark crimson (x 1000-1250, y 50-500, R/G 2.1-2.5); 12 % of the frame is red-dominant. In corridor_dark_noflash the pool under a head is right (small, R/G 1.49). Next to the camera it becomes a wall-sized wash. Review 2 status: the pink lens is STILL THERE; the red pool now exists but is too wide.
  - Likely cause: Light_Corr_Emerg (LookLights.Zone1.cs l.78) uses a 150 deg Throw.Down cone from 2.4 m or more up, which covers a whole bay. The lens emission is too hot for EV 3.4.
  - Suggested fix: Use a cone of about 90-100 deg aimed steeply down, so the pool stays about 1.5 m wide under each head. Make the lens about 8-12 nits in a deeper red so the core stays red.
- **[minor] corridor_dark_noflash.png** (exposure): With the flashlight off, walls away from the battery pools still sit near the black lift. The corridor's right wall is at 13-14/255 (std 2; review 2: 7.5-8.6). In extra/office_dark_exit_wall_off.png the exit wall right of the door is at 10.6 with std 0.7, with no texture at all. The bible's target is 15-30. Review 2 status: PARTLY FIXED (pixels at 10 or below fell from 31.8 % to 11.7 % in the corridor).
  - Likely cause: Nothing lights a wall that does not see a lit surface. The cool base review 2 asked for was never added, and the 3.0 screen-space GI multiplier only amplifies what is already on screen.
  - Suggested fix: Add a dim cool base in enclosed blackout rooms: 2-4 lux at about 7000 K, shadowless, in the VCEmergency group. Alternatively, give Look_Indoor a small probe-ambient floor under NightStormBlackout. This also lets IndoorDiffuseBlackout come down from 3.0.
- **[minor] office_on.png** (lighting): The jagged stair-step shadow in the back-right corner of the game's first room is still there (x 720-734, y 354-400). Review 2 status: STILL THERE.
  - Likely cause: The troffers use a 176 deg TrofferCone with cached shadow maps. The companion troffers (Fixture_Lamp_Light_*_Grid, Shadow.Low = 256 px map, LookLights.Zone1.cs l.60) are left out of the 1024 px resolution bump in LookLights.cs, which skips Shadow.Low.
  - Suggested fix: Set the companions to Shadow.None and let the main troffer keep the shadows, or use cones of at most 120 deg on any troffer that casts shadows.
- **[minor] office_on.png** (lighting): New since review 2: a blurred, detached shadow of the plant's leaves floats on the wall above the pot (x 120-190, y 440-500), as if the plant were lit from below. No lamp in the room sits under the plant, and review 2's frame had no such shadow.
  - Likely cause: Probably stale screen-space GI/AO history after the capture's teleport, or a contact shadow from a floor-level light (the troffer bounce 1.4 m under the floor, or a _CeilFill).
  - Suggested fix: Reset SSGI/SSAO history on teleports and camera cuts, as CameraDirector already does for exposure and TAA. Make sure no under-floor bounce or ceiling fill has contact shadows. Recheck in a settled frame.
- **[minor] 03_ch1_control_room_lit.png** (story): Four of six monitors still show a flat khaki terrain texture (x 0-300 and 970-1260, y 455-530), in the room where the radio tells the player to log the camera feeds. The big night window still shows no reflection of the lit room and reads as an open hole. Review 2 status: STILL THERE.
  - Suggested fix: Put a CCTV UI on those screens: camera ID, timestamp, scanlines, a night paddock view. Let the window glass reflect the room probe at full strength.
- **[minor] office_dark.png** (exposure): In every blackout frame the glove and torch are a featureless black cut-out at the black lift: the hand region has a median of 6/255 in office_dark and 8 in breakroom_dark, against 37 in review 2's breakroom_dark. It leaves a dark hole in the bottom-right eighth of the frame.
  - Suggested fix: Let the viewmodel catch some light: include its rendering layer in the flashlight bounce, or add a faint camera-relative cool fill on the viewmodel layer (about 1-2 lux). The glove should hold 20-30/255 so its shape reads.
- **[minor] office_dark.png** (story): An amber charging LED and a small warm pool still glow on the desk by the lamp base (x 535-545, y 486-498) while the player is holding the flashlight. It reads as a second torch left on the desk.
  - Likely cause: FlashlightPickup only hides itself on interact, or in OnEnable when GameState.HasFlashlight is already true. After a chapter jump, and possibly after a Continue, it stays active. This may be the capture path only.
  - Suggested fix: Hide the pickup whenever HasFlashlight becomes true (subscribe to the change), and check the desk after a Continue from chapter 2.
- **[minor] corridor_on.png** (geometry): A blank light-grey panel, about 1 x 1.4 m, hangs on the left wall beside the far door (x 560-588, y 337-385). Criterion 2 rules out blank sign plates.
  - Suggested fix: Print it (staff notices, an evacuation plan) or remove it.
