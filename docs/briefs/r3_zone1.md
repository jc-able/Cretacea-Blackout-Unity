# Round 3 package: Visitor Center interiors, plaza details

Clone: `~/CretaceaUnityClones/zone1`. Branch `r3/zone1`.
You own: `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs`, `Assets/Editor/LookPass/LookLights.Zone1.cs`,
`Assets/Editor/LookPass/LookFixtures.cs`, `LookRoofs.cs` (zone 1), your `MAT_Z1_*` materials.

Read `docs/review2/interiors.md`, the plaza/building items of `docs/review2/daylight.md` and `docs/review2/intro.md`.
The lead is changing, in parallel: flashlight bounce (much weaker, kept away from ceilings), no bounce or wall fill on
`_Batt` lamps, a higher minimum exposure inside blackout rooms, a stronger ceiling bounce for lit main lamps
(share 0.45 → 0.95), a cooler indoor grade at night. Do not duplicate those; your part:
1. **Major — charcoal ceilings with blotchy halos over evenly lit walls** (`office_on`, `lockers_on`, `breakroom_on`,
   `corridor_on`, `giftshop_on`): between fixtures the tiles are sRGB 34–46 while walls are 90–135; each troffer has a
   bright oval and corners go near-black; one bright oval in the gift shop has no fixture in it. A white drop ceiling
   must read as an evenly lit white plane with fixtures in it (ceiling albedo, wash placement and count, no source-less
   ovals). After the lead's merge the bounce share is higher: design for that (you can test by editing
   `LookLights.BounceShare` locally to 0.95, but do not commit that file).
2. **Major — rooms read as empty greybox halls**: bare floors, blank walls, every surface spotless. Add life the cheap
   way: floor scuff/stain decals, cable runs, wall-mounted things (clocks, extinguishers, notice boards with paper,
   switches, vents), bins, boxes, stacked chairs, a few more furniture groups per room, rugs or floor mats at doors.
   The break room, gift shop, security office and feed prep are the worst.
3. **Blocker support — blackout rooms**: keep ONE battery unit per room, by the exit, aimed down the wall (the lead
   removes their bounce); no bright patch at the ceiling junction.
4. **Major — green peeling-plaster walls read as giant crackle wallpaper** (`08_ch1_staff_exit_collapse`): crack cells
   are person-sized and the same blotches repeat in a grid: fix the scale/variation where you use that material.
5. **Major — plaza tree islands read as blockout** (`GoldenDay_plaza_eye`): a ring of straight, near-black glossy box
   segments around a flat brown soil disc with four small plants; the kerb crushes to black. Round stone kerbs, mulch
   or planting that fills the bed, a tree that fits.
6. **Major — the back of the ticket kiosk** in the facade glide (`intro_05`, `intro_07`): a plain dark-green box with a
   flat plank lid and a blank black plate: give it a real back (door, vent, sign, awning return) or turn it.
7. **Major — the first night exterior** (`intro_17`): a flat elevation of a featureless dark wall with one window and a
   wall pack: add a door or pipes, a sign, trim, a rain gutter and downpipe, drip stains, a puddle with the window's
   glow, so the shot has something to look at.
8. **Major — the 'Closing time' aerial** (`intro_14`–`16`): wings are flat boxes with blank roofs; planters become
   black discs at dusk: roof detail (units, skylights, parapet, gravel texture), lit planters.
Run `route_test.py` and `verify_playmode_doors` before hand-in.
