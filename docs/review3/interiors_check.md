# Review 3 second opinion (Nov 20) — segment `interiors` (first critic 6/10, second critic 6/10)

Visitor Center interiors, power on and blackout (Play mode, HUD on)

The second critic looked at the same captures, tried to refute each finding of the first critic, corrected severities and added what was missed.

## Confirmed (14)

- **[major] rooms/records_dark.png** (lighting): The torch still lights the ceiling as brightly as the wall it points at. In records_dark the brightest 32 px block is ceiling tile in front of the back wall (x 512-576, y 240-288, luma 168), level with the brick under the crosshair (164). The fan at x 500-800, y 150-300 averages 109 (review 2: 113, when its peak block was 231 against a brick pool of 189). office_dark is better: the beam pool is the brightest block there (189), with the ceiling at the junction at 153 (about 81 %). The room is 58 % as bright as the lit office (42.6 vs 73.8; review 2: 79 %), and unlit walls sit at 27-28. Review 2 status: PARTLY FIXED.
  - Why it is real: At full frame the eye goes to the lit stained tiles over the RECORDS / ARCHIVE wall, not to the brick under the crosshair. The block values were re-measured and reproduce the critic's records numbers exactly.
  - Fix: The patch comes from the torch's bounce light. In the captured build (HEAD) it is a shadowless point light parked up to 3.2 m in front of the wall but only 1.2 m under the tiles (BounceShare 0.006, BounceCeilingGap 1.2). It lights the ceiling above it from about 1.3 m and the wall from about 3 m, so the tiles get roughly 3x the lux, at about 2.5x the brick's albedo. Steps:
- The working tree already moves to 0.004 / 1.8 m (uncommitted). That should cut the tile patch to about a third.
- Better still: make the bounce a wide spot (~160 deg) at the hit point, aimed along hit.normal. Like a real wall pool, it then reaches the ceiling only at grazing angles.
- Make HotSpotMaxLux albedo-aware, so dark brick gets about 1.5x the lux.
- Lower IndoorDiffuseBlackout from 3.0 to about 1.5, so SSGI stops tripling the pool's bounce onto the tiles.
Target: wall pool 150-200, ceiling at most half of that. Re-capture records_dark.
- **[major] route/12_ch3_feed_prep.png** (exposure): The torch still clips on white tile. 1,364 px are at 250 or above inside x 608-693, y 342-401, and the inner 40x30 px averages 247. The core is ringed by bloom, while the two doorways sit at 8 and 11. Grout now reads round the edge of the spot but not in its centre. extra/feedprep_white_tile.png shows the same view (1,321 px). Review 2 status: PARTLY FIXED, not 'still there': clipped pixels halved (2,712 to 1,364; at 240 or above, 5,237 to 2,284).
  - Why it is real: In a side-by-side zoom with review 2's frame, the washed area is clearly smaller, but a featureless white centre with a glow ring remains. At full frame it still reads as a white disc on a dim wall.
  - Fix: Scale the hot-spot budget by the albedo of the surface hit: about 35-40 lux on LabTileWhite and PlasterClean, 90 on concrete, about 130 on brick and dark paint. The Look Pass can bake a material-to-albedo table that FlashlightController looks up from the hit renderer. The uncommitted HotSpotMaxLux change (90 to 80) trims only 11 % and will not stop the clip. Target: core at most 215/255 with grout visible. Re-capture this exact view.
- **[major] rooms/corridor_dark.png** (colour): A red emergency head beside the camera floods the near right wall bay crimson (x 1000-1250, y 50-500, R/G 2.3-2.6). 20.7 % of the frame is red-dominant (R > 1.6G), against 0.5 % in review 2's frame from the same spot. This is the 'black-and-red night' that criterion 1 rules out, and it breaks this segment's 'red only as small emergency pools'. The lens on the left wall still clips to pink-white (max 255,227,247 at x 458, y 200). Severity raised from minor: the wash covers a fifth of the frame and is new this round.
  - Why it is real: In the full frame the right fifth of the picture is a dark crimson wall. Measured over the frame and against review 2's capture. The left-wall lens at 4x zoom is a pink-white blob with a red fringe.
  - Fix: Light_Corr_Emerg_n sit 0.35 m off the north (left) wall at 2.75 m with a 150 deg Throw.Down cone, so each head lights the opposite wall bay across the 2.4 m corridor.
- Narrow the cone to 70 deg or less, aimed straight down. The foot of the opposite wall is 40 deg off vertical, so the red stays a floor pool.
- The pink core comes from LookFixtures.Emergency, which makes the builder's beacon_red glow at 70 nits (GlowModel(existing, LensColor(l), 70f)). That is 3-5x the bible's 14-22 nits for signal colours. Use 10-15 nits and a deeper red.
- **[major] extra/office_dark_exit_wall_off.png** (fixtures): The only lit fixture in the blackout room reads as two orange eyes floating on the wall: lenses at x 506 and 528, y 314, clipped to 255, above their warm scallop. No housing shows, and there is still none when the beam lights that wall (office_dark_exit_wall_on.png). It breaks 'no lamp without a fixture', and in a dinosaur horror game it reads as a pair of eyes. New in round 3.
  - Why it is real: Zoomed both frames: only two glowing discs and a pool under them, with no box, cans or LED, even with the beam on that wall. In corridor_dark, by contrast, the corridor unit's housing does read where the beam hits it.
  - Fix: LookFixtures.TwinHeadBody does build a body (a 0.36 x 0.13 x 0.09 m box, a grille, cans and a 1.2 cm EmissiveGreen LED), but in PlasterClean. That is off-white on beige drywall, and the box sits above its own 120 deg cone (aimed 60 deg down), so it gets no light and has no contrast. The LED is about 1 px at this distance. Changes:
- A light-grey ABS body with a dark-grey front band and dark-grey head cans.
- A 3-5 cm charge LED at about 10 nits.
- A faint shadowless 1-3 cd point light about 10 cm in front of the box (or emissive lens bezels) so the outline reads.
- Lenses under about 40 nits so they stop clipping.
- **[major] rooms/lockers_on.png** (geometry): Placeholder primitives in the centre of frame: three pairs of boots are black boxes, the duffel is a black capsule, and the towel on the bench is a flat cream slab (x 470-810, y 440-515). Also confirmed:
- a blank cream box among printed stock on the gift-shop shelf (giftshop_on x 115-225, y 534-612)
- a plain grey cube on the records side table (records_dark x 750-795, y 435-478)
- a blank cream box under the feed-prep table (12_ch3 x 0-100, y 540-590)
- the new break-room 'fridge', a tan box with two bar handles that reads as a cardboard carton (breakroom_on x 0-70, y 350-460)
Review 2 status: STILL THERE (only the break-room floor cube is gone).
  - Why it is real: Zoomed 3x: the boots are untextured black blocks and the bag a smooth black capsule, dead centre of the room's view. The other items are confirmed at 2-3x.
  - Fix: The working tree (uncommitted, after these captures) already rebuilds Lock_Duffel_n and Lock_Boot as multi-part groups in Zone1_VisitorCenterBuilder, so re-capture lockers_on. Still open:
- Lock_Towel as a folded towel with thickness and a hem
- Rec_FicheReader as a reader with a hood and a slanted screen
- printed labels on the blank stock boxes
- the fridge in white or almond enamel (smoothness about 0.6), with a door gap, rounded edges and a kick grille
- **[major] rooms/lockers_on.png** (dressing): Half the spaces are still bare halls:
- Lockers: identical to review 2 apart from the ceiling (a thumbnail comparison was checked). Three locker banks, two benches, blank beige walls, bare floor.
- Lobby: identical to review 2. One poster, a vast empty polished floor, two benches, two plants, a bin.
- Staff corridor: 80 m of blank wall.
- Feed prep route view: two black doorways, two steel tables and a bin. None of the new sinks, prep tables, hooks or freezer door is in view.
The office, break room and gift shop now read as working rooms. Review 2 status: PARTLY FIXED.
  - Why it is real: Visible at first glance in lockers_on, lobby_on, corridor_on and 12_ch3. The review 2 / review 3 thumbnails of lockers and lobby differ only in the ceiling and a thin trim line.
  - Fix: Lockers, corridor and feed prep: as critic 1, placed on the walls these cameras face. For the lockers that means the far wall above the lockers and both side walls.
Lobby: the builder already has a reception desk (BuildReceptionDesk at z -25, behind this camera) and two east-wall banners. Dress the north half the player faces instead: stanchions and a plaque round the skeleton, a donor wall or mural under the mezzanine, info kiosks, floor signage.
Then a grime decal pass on skirting and floors.
- **[minor] rooms/giftshop_on.png** (lighting): A bright band on the ceiling at the back-wall junction (x 480-900, y 215-260, mean 111, peak 208) holds only a vent, against a ceiling centre of 58-62. Only two troffers are in view, both at the frame edges, and none hangs over the merchandise tables. Review 2 status: PARTLY FIXED (the black centre is gone: 13-14 to 58-62).
  - Why it is real: Measured. At full frame it reads as light spilling from nowhere near the back wall.
  - Fix: Re-aim the gift-shop _Wash units at the wall below the cornice, or drop them to about 250 cd. Add a centre row of troffers over the tables.
- **[minor] rooms/corridor_dark_noflash.png** (exposure): With the flashlight off, walls away from the pools sit at or under the bottom of the bible's 15-30 target:
- corridor right wall 13.9 (std 2.8; review 2: 7.8)
- office exit wall right of the door 10.4 (std 0.6, no texture) in office_dark_exit_wall_off
- break-room back wall 13.4, even with the beam on
Review 2 status: PARTLY FIXED.
  - Why it is real: Measured. The corridor is close to target and reads as moody. The exit wall is a flat dark plane with no texture.
  - Fix: Give enclosed blackout rooms a dim cool base of 2-4 lux at about 7000 K. Prefer an ambient term in Look_Indoor (a probe-ambient floor under NightStormBlackout) over more punctual lights: the zone1 hand-in already hit HDRP's per-tile light-list cap in this office with one extra fill. The base also lets IndoorDiffuseBlackout come down from 3.0.
- **[minor] rooms/office_on.png** (lighting): A stair-step shadow runs down the back-right corner (x 720-735, y 345-400) of the game's first room. It is faint at full frame and clear at 5x. Review 2 status: STILL THERE.
  - Why it is real: Zoomed 5x: a regular stepped edge on the left face of the corner.
  - Fix: Light_Security_Main is Shadow.Medium with the 176 deg TrofferCone. Give the shadowed main a cone of 120 deg or less, or a 1024 shadow map.
- **[minor] rooms/office_on.png** (lighting): A blurred, detached shadow of the plant's leaves floats on the wall above the pot (x 120-190, y 440-500), as if lit from below. It was not in review 2's frame.
  - Why it is real: Visible at full frame as a grey smudge over the plant. At 4x zoom it shows distinct leaf shapes, offset up and left of the plant.
  - Fix: The floor bounces and wall fills are shadowless (LookLights.EnsureBounce / EnsureWallFill: shadows None). The likely cause is screen-space GI: Look_Indoor now runs SSGI at 1.0 instead of 0.20, and the alpha-clipped leaves block the lit floor's bounce onto the wall. Check a settled frame. If the shadow stays, raise SSGI denoising or blur in the indoor profile, or shorten its ray length. A history reset on cuts helps only if it fades after a few seconds.
- **[minor] route/03_ch1_control_room_lit.png** (story): Four of six monitors show a flat khaki terrain texture with no glow (x 0-300 and 970-1260, y 455-530). This is the room where the radio says to log the camera feeds. The big night window has no reflection of the lit room and reads as an open hole. Review 2 status: STILL THERE.
  - Why it is real: Zoomed 2x: grass and dirt texture fields next to two lit map screens.
  - Fix: As critic 1: a CCTV UI on the screens (camera ID, timestamp, scanlines, a night paddock view), and the room probe reflected on the glass at full strength.
- **[minor] rooms/office_dark.png** (exposure): In the blackout frames the glove and torch are a featureless black cut-out at the black lift. The hand region has a median of 6 in office_dark, records_dark and corridor_dark and 8 in breakroom_dark, against 27 in office_on. This is not a regression in office_dark (review 2: 7). Review 2's breakroom_dark glove at 38 came from the old oversized bounce.
  - Why it is real: Measured. At full frame it is a black blob in the bottom-right eighth of every blackout frame.
  - Fix: As critic 1: put the viewmodel's rendering layer in the bounce, or add a 1-2 lux camera-relative cool fill on the viewmodel layer, so the glove holds 20-30/255.
- **[minor] rooms/office_dark.png** (story): The flashlight pickup that office_on asks the player to take (a small torch at the desk-lamp base with an amber LED and a warm pool, x 535-545, y 486-498) is still on the desk and glowing in office_dark, while the HUD shows the flashlight equipped.
  - Why it is real: The same object and glow show in the office_on and office_dark desk crops. The desk lamp itself and the pendant are now dark.
  - Fix: Hide the pickup when HasFlashlight becomes true, including after Continue or jump_chapter.
- **[minor] rooms/corridor_on.png** (geometry): A blank light-grey panel, about 1 x 1.4 m, hangs on the left wall beside the far door (x 560-588, y 337-385), next to a printed door sign.
  - Why it is real: Zoomed 5x: a plain grey rectangle. Small here, but the player walks right past it.
  - Fix: Print it (an evacuation plan, staff notices) or remove it.

## Missed by the first critic (3)

- **[major] rooms/lobby_on.png** (colour): The lobby is one amber-brown from floor to wall. The frame measures R/G 1.26 / B/G 0.79, the walls 1.31-1.35 / 0.73-0.76 and the floor 1.25 / 0.77. The only cool area is the near-black roof glass (luma 35). The other lit rooms are 1.11-1.13 / 0.86-0.90, and review 2 measured exactly the same here (1.26 / 0.78), so nothing changed. The showpiece hall with the T. rex reads as a sepia photo, which hits the owner's 'colours are off' and breaks 'one palette' with the rest of the Visitor Center. Critic 1 quoted the R/G figure inside the dressing finding but gave no colour finding or fix. lobby_dark (R/G 1.04, moonlit roof-beam shadows) shows what a cool counterpoint does for this room.
  - Fix: Lobby_Grazer (13,000 cd) and Lobby_Main (6,000 cd) are both 3600 K and light every surface, and the ConcreteFloorWet floor mirrors them. Changes:
- Let cool moonlight through the glass roof reach the floor in the lit state too: 6500-7500 K, 8-12 lux, with the roof-beam shadows.
- Take Lobby_Main to 4000-4300 K as a neutral key.
- Keep 3000-3300 K for accents on the skeleton, banners and reception.
Target: frame R/G about 1.15, with a visibly cool floor.
- **[minor] rooms/lobby_dark.png** (lighting): The brightest thing in the frame is the air in front of the lens: a white volumetric wedge from the torch (16 px blocks up to 207 at x 744-776, y 480-504). The area x 680-780, y 430-500 measures 153, up from 113 in review 2. Review 2's brightest block was the surface at the crosshair (164). In a dry indoor hall the wedge reads as smoke and pulls the eye to the hand instead of the scene.
  - Fix: The beam went from 7,200 to 12,000 cd this round (FlashlightController.BeamCandela), while BeamVolumetric stayed at 0.42. At long throw _adapt stays near 1, so the shaft got about 1.7x stronger. Set BeamVolumetric to about 0.25, or scale it down by the indoor weight when enclosed, and keep the outdoor rain shaft.
- **[minor] rooms/breakroom_on.png** (lighting): Critic 1 gave no status for review 2's 'furniture floats' major (from the second opinion). It is PARTLY FIXED. Under the centre break-room table the floor median is 75-77, against 99 beside it (review 2: 72-84 vs 94-98). Chair legs and the credenza show almost no contact shadow, so the furniture still sits lightly on the floor.
  - Fix: Turn contact shadows on for the room mains (length about 0.5 m). The companions are Shadow.Low, and LookLights sets useContactShadow = r.shadow != Shadow.Low, so they get none. Add soft AO blob decals under tables, chairs, benches and counters.

## Dropped (3)

- Finding 1, office_dark evidence: 'the fan covers x 400-850, y 100-330 at a mean of 138, so the beam reads as a soft glow rather than a pool on the wall'.
  - Why not: That box measures 89, not 138. In office_dark the beam pool is the brightest block in the frame (189 at x 624, y 352) and the ceiling junction is 153, so the pool reads with a glow above it. The finding stands on records_dark, where the critic's numbers reproduce.
- Finding 5, lobby fix: 'add a reception desk and two hanging banners'.
  - Why not: Both already exist in Zone1_VisitorCenterBuilder: BuildReceptionDesk at z -25, behind this camera, and Lobby_Banner_RexE / RaptorE on the east wall (the RaptorE one is the poster in frame). The kept fix sends the dressing to the north half the player faces.
- Review 2 majors and minors that critic 1 gave no status: charcoal ceilings, blackout amber-olive sepia, staff-exit crackle wallpaper, the corridor_on dark top band, and the desk lamp and pendant burning in the blackout.
  - Why not: All are FIXED in round 3 and nothing remains to carry forward:
- Ceilings: lit tile ceilings everywhere. Corridor ceiling 73 against walls of 140; gift-shop centre 13 to 58-62; the office, lockers and break room read as white drop ceilings.
- Blackout colour: 08 R/G 1.20 to 1.11; office_dark about 1.05-1.09; lobby_dark 1.04; breakroom_dark 1.0-1.17.
- Staff exit: plain sage paint with sparse leak and crack decals.
- corridor_on: the top-right wall went from 12-43 to 138.
- Office: desk lamp and pendant are dark in the blackout.
Other strengths: neutral colour and full range in the lit rooms; the office, break room and gift shop read as working rooms; the beam leads in office_dark, breakroom_dark, 08 and the exit-wall view; lobby_dark (cool moonlight, roof-beam shadows) is the best blackout frame.
