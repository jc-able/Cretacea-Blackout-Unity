# Round 4 package: Visitor Center light and fixtures (lobby colour, emergency heads, battery units, loading dock, plaza lamps)

Clone: `~/CretaceaUnityClones/kit`. Branch `r4/lights`.
You own: `Assets/Editor/LookPass/LookLights.Zone1.cs`, `LookLights.Kit.cs`, `LookFixtures.cs`, and in
`Assets/Editor/WorldBuilder/Zone1_VisitorCenterBuilder.cs` ONLY the loading dock (`Dock_Pad` and what stands in it)
and the `Light_*` placements and aims. Another package (r4/rooms) dresses the rooms in the same file: do not touch
room furniture or wall dressing, so the two branches merge. The core lamp table (`LookLights.cs`), the atmosphere
profiles and the flashlight are the lead's.

Read `docs/review3/interiors.md` and `interiors_check.md`, then `intro_check.md` (intro_16, intro_17) and
`night_a_check.md` (13_ch3_loading_dock).
1. **Major — the lobby is one amber-brown from floor to wall** (`rooms/lobby_on`: frame R/G 1.26, B/G 0.79; the other
   lit rooms are 1.11-1.13 / 0.86-0.90). `Lobby_Grazer` (13,000 cd) and `Lobby_Main` (6,000 cd) are both 3600 K and
   the wet concrete floor mirrors them. Make `Lobby_Main` a neutral key (4000-4300 K), let cool moonlight through the
   glass roof reach the floor in the lit state too (6500-7500 K, 8-12 lux, with the roof-beam shadows), keep
   3000-3300 K for accents on the skeleton, banners and reception. Target: frame R/G about 1.12-1.16 with a visible
   warm-cool contrast.
2. **Major — a red emergency head floods the near corridor wall crimson** (`rooms/corridor_dark`: 20.7 % of the frame
   is red-dominant; the lens clips to pink-white). `Light_Corr_Emerg_n` sit 0.35 m off the north wall at 2.75 m with a
   150 degree cone. Narrow them to 70 degrees or less, straight down, so red stays a floor pool, and bring the lens
   glow (`LookFixtures.Emergency`, 70 nits) down until the lens reads red, not pink-white.
3. **Major — the battery emergency unit reads as two orange eyes floating on the wall**
   (`extra/office_dark_exit_wall_off`, `_on`): the body (`LookFixtures.TwinHeadBody`) is off-white on beige and sits
   above its own cone. Give it a light-grey body with a dark front band and dark head cans, a 3-5 cm green charge LED,
   heads angled down so the lenses are not two discs facing the room, and enough spill on the body that the housing
   reads in the blackout and in the torch.
4. **Major — the loading dock is nearly black** (`13_ch3_loading_dock`: 57 % of the frame below 16/255; the lead
   raised `Light_Dock_Batt` to 1000 cd after the capture). Check it in your clone, then: place or aim the battery unit
   over the walk to the Service Road 3 exit so the player crosses one warm pool (60-100/255); change the dock shell
   from rust-red corrugated metal to galvanized or dark painted metal so the ceiling returns torch light; a dry
   concrete floor under the roof with wet strips only along the open edges; ceiling panels that are not flat grey
   rectangles.
5. Minors: the night plaza is an even mauve-grey sheet with pinprick lamp heads and no warm pools under the posts
   (`intro_16`); the three wall packs of the first night exterior read red and maroon, not tungsten amber (`intro_17`:
   halos 63-71/34-38/30-34); the street-lamp lens is a flat terracotta rectangle offset in its housing
   (`DuskTransition_plaza_eye` x 790-880, y 55-90); a bright ceiling band at the gift-shop back wall with no fixture
   in it (`rooms/giftshop_on`); stair-step shadows in the office's back-right corner (`rooms/office_on` x 720-735) and
   on the right wall of the first gameplay frame (`intro_20` x 1165-1220): find the lamp and fix its shadow bias,
   resolution or filter; a detached plant-leaf shadow floating on the wall above the pot (`rooms/office_on`);
   with the flashlight off the blackout walls sit at 10-14/255, under the 15-30 target
   (`rooms/corridor_dark_noflash`); the desk lamp in the security office has no light of its own (a small warm pool
   on the desk, no shadows).
Report `perf_here.sh` in the security office (the heaviest view of the game: 73 fps in the built player) before and
after: it must not get slower. Run `route_test.py` before hand-in.
