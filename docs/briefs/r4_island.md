# Round 4 package: island (tree impostors, valley woodland, tall grass, ending tree belt, pond, rock)

Clone: `~/CretaceaUnityClones/terrain`. Branch `r4/island`.
You own: `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`, `Assets/Editor/LookPass/LookTerrain*.cs`,
`LookVegetation*.cs`, `Assets/Scripts/Rendering/{InstancedFoliageRenderer,WaterWeatherDriver}.cs`, `Assets/Art/Terrain/**`,
`Assets/Art/Textures/terrain_jungle/**`, `Assets/Prefabs/Foliage/**`.

Read `docs/review3/daylight.md` and `daylight_check.md`, then the vegetation and water items of `night_a_check.md`,
`night_b_check.md` (28_ending_2, the boat) and `intro_check.md` (intro_02, intro_03).
1. **Major — from eye level the impostor crowns read as stacked flat ledges** (`DuskTransition_meadow_eye` hill, the
   right edges of `DawnCoast_valley_eye` and `DuskTransition_valley_eye`, the far tree line in `GoldenDay_valley_eye`).
   State now: the cap is an 8-triangle umbrella (`LookVegetationImpostors.SaveMesh`: centre +3 % of crown height, rim
   inset 0.88, droop 13 %). Look at those frames at full size in your clone. Accept only if no straight or domed shelf
   shows from eye level AND the aerials stay as green as now (`GoldenDay_aerial_sw` far canopy luminance at least 0.30,
   hue at least 80 degrees). If shelves remain: smaller, lower cap hidden by the leaf cards, or a cap that fades by
   view elevation, or more card planes; your choice, proven in captures from the air and from eye level.
2. **Major — a band of dying woodland from the air** (`GoldenDay_valley_air` y 140-330, the meadow in
   `GoldenDay_aerial_sw`, brown at dawn in `DawnCoast_aerial_sw`): the play-area trees (`ScatterPlayAreaTrees`,
   `PF_jungle_tree` LODGroup) switch to the impostor below 10 % screen height (`LookVegetationPrefabs.cs`, Screen
   {0.70, 0.36, 0.10, 0.0035}); the impostor casts no ground shadow; its crown is grey-olive (0.356/0.367/0.239) on a
   pale pole, on a lawn of 0.335/0.432/0.188. Fix all three: a later switch (about 0.05; check the triangle budget), a
   ground shadow at the impostor level (a shadows-only crown shape or a soft blob under each crown), crown G/R at
   least 1.15 and hue at least 75 degrees at the switch, the trunk at about 0.6 under the crown, no brown trees at dawn.
3. **Major — the tall grass reads as a planted crop** (`GoldenDay_valley_eye`; maize rows at dusk and dawn; these are
   the night stealth stands): one saturated emerald blade card at one height with a straight front along the gravel.
   Add two more tall prototypes picked by noise (a dry seed-head variant with tan tips, about 20-25 %; a darker,
   bluer green, about 25 %), pull the main material back to about 0.45 saturation with lighter tips, and start the
   falloff about 2.5 m from the gravel so the outer metre breaks into tufts. Stealth must keep working: change how the
   stands look, not where the tall-grass zones and their triggers are.
4. **Major — the ending crane shot reads as a diorama** (`28_ending_2`): one pale-trunked umbrella tree at one height
   repeats across the whole frame between the yard wall and the hills, and the hills are an even carpet. Vary scale
   (about 9-22 m), species (palms, tree ferns, big shrubs), tilt and setback, and darken trunks with distance. (The
   lead adds dawn haze on the far ridge; the zone23 package dresses the warehouse roof and the wall.)
5. Minors, cheapest first: the lawn is a golf course (low-frequency tint and noise on the meadow terrain paint, more
   clumps: `GoldenDay_valley_air`, `NightCalm_meadow_eye`); big trees grow out of the Titan Meadows pond and the pond
   is a smooth ellipse that reads as a crater (`GoldenDay_aerial_sw`, `NightStormBlackout_aerial_sw`): clear the
   water, break the bank, add reeds; lone umbrella trees are the focal silhouettes at the end of the valley road (`18`):
   group them; saturated orange slab rocks on the pond bank (`intro_02`, `intro_03`) and bald orange headland shelves
   (`GoldenDay_aerial_sw`): dark volcanic rock with moss; a row of identical pale buttress trunks (`intro_02`, `intro_03`);
   the palm crown crushing to black in strong light (`GoldenDay_plaza_eye`); the rescue boat sits on the water with no
   foam at the hull and no wake (`28_ending_1`, `28_ending_3`); no shoreline from the air (sand, surf line, pale
   shallows) if it is cheap.
Report `perf_here.sh` in the valley and the meadow before and after. Run `route_test.py` before hand-in.
