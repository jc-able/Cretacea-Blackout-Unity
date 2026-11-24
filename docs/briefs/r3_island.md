# Round 3 package: island (far jungle, greens, valley trees)

Clone: `~/CretaceaUnityClones/terrain`. Branch `r3/island`.
You own: `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`, `Assets/Editor/LookPass/LookTerrain*.cs`,
`LookVegetation*.cs`, `Assets/Scripts/Rendering/{InstancedFoliageRenderer,WaterWeatherDriver}.cs`, `Assets/Art/Terrain/**`,
`Assets/Art/Textures/terrain_jungle/**`, `Assets/Prefabs/Foliage/**`.

Read `docs/review2/daylight.md` (and its `_check.md` if present) and the vegetation items of `night_a.md`.
1. **BLOCKER — from the air the jungle is a burnt, dead forest** (`GoldenDay_aerial_sw`, `GoldenDay_valley_air`,
   `DawnCoast_aerial_sw`): far trees are dark khaki X-shaped sticks with black gaps (far canopy RGB 0.18/0.18/0.14 next
   to a lawn at luminance 0.40). **BLOCKER — at dusk the hill is a sandstone mountain** (`DuskTransition_meadow_eye`,
   `DuskTransition_plaza_eye`): stacked beige tree cards, no green at all. **Major — in full sun the hill behind the
   Visitor Center is a near-black olive mass** (luminance 0.15–0.18, darker than shaded leaves 10 m away; distance
   runs backwards) and there is **an obvious LOD seam** in `GoldenDay_valley_air` (lit green trees within ~60 m, dark
   grey-brown sticks beyond).
   The far forest must read as a closed, lit, green canopy from every angle and in every preset. Do what it takes:
   a canopy ground layer under the hill trees (the texture set `Assets/Art/Textures/backdrop_jungle_aerial` exists for
   exactly this) so gaps are green and not black; impostors with a horizontal crown cap for views from above;
   impostor albedo, saturation and brightness matched to the near LOD in the same light (measure both in one capture);
   shading that does not turn beige under a 5° sun; a cross-fade or matched lighting at the LOD switch.
2. **Major — the day's greens are yellow-olive, not tropical** (tall grass RGB 0.297/0.299/0.065, hue 60°; fronds and
   banana leaves hue 55°: red equals green on every plant). Shift the plant, grass-card and terrain-grass albedo toward
   a real tropical green (hue about 95–120°), keep some variety. (The lead keeps the day white balance neutral.)
3. **Major — the valley has one canopy tree, cloned** (`17_ch4_valley_entry`, `18_ch4_trex_reveal`): 5–8 identical
   umbrella silhouettes, upright, evenly spaced. Mix species, sizes, leans and groupings along the valley path.
4. **Major — the foreground tree islands on the plaza and the kerbs** are the Visitor Center package's; skip them.
5. Check that no ground mist or water effect of yours shows as a flat slab in GoldenDay Play-mode views
   (`GoldenDay_valley_eye` showed a 2 m tall brown-grey slab with a hard top edge at the foot of the kapok trees: find
   out what it is in a Play-mode capture; if it is yours, fix it; if not, report it).
Keep triangle counts (report `perf_here.sh` in the valley and the meadow before/after).
