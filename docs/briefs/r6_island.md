# Round 6 package: the island from the air and at the ending

Clone: `~/CretaceaUnityClones/terrain`. Branch `r6/island`.
You own the same files as round 4 (`docs/briefs/r4_island.md`): `Zone4_TerrainJungleAndDawnCoastBuilder.cs`,
`LookTerrain*.cs`, `LookVegetation*.cs`, `InstancedFoliageRenderer.cs`, `WaterWeatherDriver.cs`, `Assets/Art/Terrain/**`,
`Assets/Art/Textures/terrain_jungle/**`, `Assets/Prefabs/Foliage/**`; plus ONLY the dawn profile block of
`Assets/Editor/LookPass/LookAtmosphereProfiles.cs`. Do NOT change the sea colours the lead set in
`LookTerrainWater.cs` (refraction, scattering, ambient/height/displacement/tip/body scattering). The ending crane
camera lives in the story code: ask for a re-aim under "Requests for the lead" with exact positions.

Read `docs/review4/night_b.md` (28_ending_1/2/3), `docs/review4/intro.md` (intro_14, intro_02) and
`docs/review4/daylight.md`, then look at `~/CretaceaUnityClones/vfx/Screenshots/review4/route/28_ending_*`,
`review4/intro/intro_14.png`, `review4/presets/GoldenDay_aerial_sw.png`, `DawnCoast_aerial_sw.png`,
`DuskTransition_meadow_eye.png`. Preset views: `capture_set.sh <dir> presets` (the free-camera positions are also listed in
`Tools/lookdev/lead/atmo_lab.sh`: read it for positions only, it targets the lead's clone, never run it); the ending
frames come from `route_test.py` (shots 28_ending_1–3).

Goals, in order:
1. **Major — the ending crane shot looks like a model** (28_ending_2): one even carpet of identical saturated crowns,
   a far ridge as crisp as the near canopy, a row of pale trunks at one height along the wall. Vary crown size, hue
   and value; add emergent trees and palms above the canopy; put understory in front of the trunk row; give the far
   ridge aerial perspective from the global dawn fog (fogMaxHeight 60 → about 300 m, mean free path about
   1200–1500 m) and check that 28_ending_1/3 and the coast lookout do not go milky (a little haze on the sea horizon
   is fine). If the sun stays behind the crane (front-lit, 28_ending_2 minor), request a re-aim.
2. **Major — glittering specks on the backlit hill in the CLOSING TIME aerial** (intro_14): cream specks and pale
   dashes on the shadowed slope. Likely the crown caps beyond the dusk shadow distance, lit as if facing the sun with
   no shadow (`LookVegetationImpostors` cap, `InstancedFoliageRenderer.capElevation`). Darken or shadow-fake the caps
   beyond the shadow distance, or fade them by view elevation.
3. **No beach from the air** (GoldenDay_aerial_sw minor): grass and trees run straight into dark water along the dock
   and the east coast. A sand/shingle band with a wet-sand line and shore foam, visible in GoldenDay_aerial_sw and
   DawnCoast_aerial_sw, without moving the coast road, the dock, the lookout or the ending boat's water.
4. Only if 1–3 are done: a bounded attempt at the dusk-hill mosaic (DuskTransition_meadow_eye: rounded crown normals,
   a dithered soft edge, a fade by view elevation); keep it only if the same-view frames clearly improve and the
   meadow GPU time holds. The brown dawn trees (DawnCoast_aerial_sw, hue 49) if cheap.

Evidence: same-view before/after of 28_ending_1/2/3, intro_14, GoldenDay_aerial_sw, DawnCoast_aerial_sw,
DawnCoast_coast_eye, DuskTransition_meadow_eye; perf at the meadow before and after.
