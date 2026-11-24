# Round 4 package: Visitor Center rooms (dressing, props, the first gameplay frame)

Clone: `~/CretaceaUnityClones/zone1`. Branch `r4/rooms`.
You own: `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs` EXCEPT the loading dock and every `Light_*`
placement or aim (another package, r4/lights, edits those parts of the same file: stay out of them so the two
branches merge), `LookRoofs.cs` (zone 1), your `MAT_Z1_*` materials, and `Assets/Scripts/Interact/FlashlightPickup.cs`
(only to hide the pickup once taken, goal 5).

Read `docs/review3/interiors.md` and `interiors_check.md`, then `intro_check.md` (intro_11, intro_19, intro_20).
1. **Major — half the spaces are still bare halls** (`rooms/lockers_on`, `rooms/lobby_on`, `rooms/corridor_on`,
   `route/12_ch3_feed_prep`): lockers = three locker banks, two benches, blank beige walls, bare floor; lobby = one
   poster, a vast empty polished floor, two benches, two plants, a bin; staff corridor = 80 m of blank wall; the feed
   prep route view shows two black doorways, two tables and a bin. Dress the walls and floors these cameras face
   (positions in `Tools/lookdev/capture_set.sh` and `route_test.py`). Lobby: the north half the player faces
   (stanchions and a plaque round the skeleton, a donor wall or mural under the mezzanine, exhibit cases, a ticket
   or information point, floor inlay or mats, banners). Lockers: the far wall above the lockers and both side walls
   (notice board, clock, hooks with jackets, a mirror, a first-aid cabinet, a wet-floor sign, a floor drain, mats).
   Corridor: door surrounds, a pin board, extinguisher cabinets, a trolley, pipes or a cable tray at the ceiling,
   floor scuffs, room signs. Feed prep: bring the sinks, hooks and the freezer door into the route view.
2. **Major — placeholder primitives in frame**: the towel on the locker bench is a flat cream slab; the fiche reader
   on the records side table is a plain grey cube (`records_dark` x 750-795); a blank cream box among printed stock
   on the gift-shop shelf (`giftshop_on` x 115-225) and under the feed-prep table; the break-room fridge is a tan
   box with two bar handles; check every room view for more.
3. **Major — the first gameplay frame** (`intro_20`, `intro_19`, `rooms/office_on`): the keyboard, phone and logbook
   are black slabs; the water cooler's blue and red tap cubes over the black drip tray read as a cartoon face (also
   through the window in `intro_18`); the whiteboard's writing is nine bars; the desk lamp is rounder now but still
   primitives. Build these as recognisable objects (key rows, handset and cord, ring binder, bottle and taps, real
   handwriting or a duty roster). The desk lamp's own light belongs to r4/lights: leave its `Light_*` alone.
4. Minors: a blank light-grey panel beside the far corridor door (`corridor_on` x 560-588); four of six control-room
   monitors show a flat khaki texture with no glow (`03_ch1_control_room_lit`): give them camera-feed pictures and
   screen glow; the desk pendant's canopy sits on the troffer lens (`intro_19`): move one of them; the gallery glass
   shows no reflection, tint or smudge and the gallery is bare (`intro_11`): a faint tint and smudges, a rail sign,
   an interpretive panel; the park's main building is a low beige box with no landmark silhouette
   (`GoldenDay_meadow_eye`): a roof feature that reads from the meadow and the plaza (a lantern roof, a mast with
   the park badge, a canopy) without touching anything the player walks through.
5. Minor — the flashlight pickup stays on the desk, glowing, after the player has taken it (`rooms/office_dark`):
   hide it once it is taken.
Run `route_test.py` and `verify_playmode_doors` before hand-in: every prop on a floor needs to stay out of the
walking route (the route test walks it).
