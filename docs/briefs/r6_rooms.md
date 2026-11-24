# Round 6 package: interiors — the first room, the gift shop, the break room, the lobby

Clone: `~/CretaceaUnityClones/zone1`. Branch `r6/rooms`.
You own: `Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs` EXCEPT the loading dock, door placements and the
positions of story interactables (flashlight, terminal, documents, doors: do not move them); a NEW file
`Assets/Editor/WorldBuilder/Zone1_RoomDressing.cs` if you prefer (hook it with one call from the Zone 1 builder);
in `Assets/Editor/LookPass/LookLights.Zone1.cs` and `LookLights.Kit.cs` only the `Light_Security_Main` rules and
rules for lamps you add inside rooms you dress; your own `MAT_Z1_*` materials; `WorldMaterialPalette.cs` only by
appending new materials at the end; `LookRoofs.cs` for zone 1. Not `Assets/Scripts/Story/**`.

Read `docs/review4/interiors.md` and `docs/review4/intro.md` (intro_19, intro_20), then look at
`~/CretaceaUnityClones/vfx/Screenshots/review4/rooms/` (office_on, giftshop_on, breakroom_on, lobby_on, lockers_on)
and `review4/intro/intro_20.png`. Capture positions are in `Tools/lookdev/capture_set.sh` (rooms part); the first
playable frame comes from `capture_set.sh <dir> intro` (frame 20).

Goals, in order:
1. **Major — the first playable frame is a sparse, evenly lit beige room** (intro_20). The CCTV bank glows now (base
   commit). Dim the four `Light_Security_Main` troffers by about 40 % so the desk pendant's pool and the screens carry
   the contrast; dress the north wall round the bank (a laminated park map, a shift roster, a key cabinet, a radio
   charger shelf: things a night guard uses); move `WallClock` about 1.2 m left so the green pendant shade no longer
   hangs in front of the bank, and set its hands to 23:05 (the card says 23:05, the clock reads 2:00); make the
   water cooler stop reading as a face.
2. **Major — the gift shop is a half-empty set** (giftshop_on: a dozen cartons on two tables, back wall bare grey over
   x 50–340 and 410–700). Wall shelving full of merchandise (plush dinosaurs, mugs, T-shirts on racks, books, toy
   figures, snow globes), a register counter with a till and signage, price signs, a floor display; fix the bright
   ceiling band at the back-wall junction if you can.
3. **The break room has not changed since review 3** (front half bare tile): a kitchenette run (sink, microwave,
   kettle, fridge), a vending machine, a table with chairs, a notice board, a floor mat; contact darkening under every
   furniture foot (soft AO blob decals, or contact shadows on the room's lamps).
4. **The lobby's two long walls carry one poster**: exhibit panels, banners and posters between the doors.
5. If time: blank plates in the lockers room (lockers_on minor); the feed prep steel table as brushed steel
   (smoothness 0.55–0.65 with a brushed normal, top 30–50 luma; `12_ch3_feed_prep` minor).

Constraints: nothing may block a door, a path, a crouch route or an interactable; flat wall props without colliders;
keep draw calls modest (merge static props where the builders already merge). Evidence: same-view before/after of
intro_20 (or the Night checkpoint start view), office_on, giftshop_on, breakroom_on, lobby_on; perf in the office
(chapter 1 is the heaviest chapter) before and after.
