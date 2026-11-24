# Round 2 package: island (jungle, terrain, sea, river, the ending view)

Clone: `~/CretaceaUnityClones/terrain`. Branch `fix/island`.
You own: `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`, `Assets/Editor/LookPass/LookTerrain*.cs`,
`LookVegetation*.cs`, `Assets/Scripts/Rendering/{InstancedFoliageRenderer,WaterWeatherDriver}.cs`, `Assets/Art/Terrain/**`,
`Assets/Art/Textures/terrain_jungle/**`, `Assets/Prefabs/Foliage/**`.

Findings to fix (read `docs/review1/daylight.md`, `night_a.md`, `night_b.md`; yours are vegetation, water, terrain and
the coast/ending geometry):
1. **BLOCKER — hill jungle renders as flat mustard cardboard blocks at dusk and dawn** (`DuskTransition_plaza_eye`,
   `DuskTransition_meadow_eye`, `DawnCoast_valley_eye`, the three aerials): each far-tree card is lit on one half and
   dark on the other, no crowns, no aerial haze; from above trees read as tan paper cut-outs with trunk streaks.
   Fix the impostor/instanced canopy shading for low sun (normals that describe a round crown, not a flat card;
   translucency/ambient so the dark side is not black; albedo that stays green when lit by an orange sun), and the
   bare slope with yellow speckles between them.
2. **BLOCKER — the rescue boat in the last shot of the game is a stack of bare boxes** (`28_ending_3`): box hull,
   stepped box deckhouse, stick mast, a grey slab sticking out. Build a believable small coast-guard cutter from code
   (lofted hull with bow flare and sheer, deck, wheelhouse with windows, mast with radar and lights, rails, an orange
   diagonal stripe, running lights, a wake) or from existing assets; it must hold up at the ending camera's distance.
3. **Major — the overlook rail in the ending view is primitive boxes** (`GoldenDay_coast_eye`, `DawnCoast_coast_eye`):
   square sections with a plank texture tiling like bricks, plain square posts with no base plate. It is the
   silhouette of the final shot.
4. **Major — checkerboard lawn** (all aerials): the grass texture repeats as light and dark squares; Titan Meadows at
   eye level is a flat lawn with evenly scattered single palms ("golf course"). At night the meadow is one flat dark
   carpet (`NightCalm_meadow_eye`). Break the tiling (macro variation, distance blend), add clumps, height variation,
   shrubs and uneven ground cover.
5. **Major — the river reads as a dry ditch** (`GoldenDay_valley_air`): no visible water surface, reflection, foam or
   reeds. **The sea from the overlook is a flat featureless navy plane** (`GoldenDay_coast_eye`, captured before the
   lead thinned the day fog: re-check first): swell, whitecaps, cloud reflections, a clean horizon.
6. **Major — the run to the coast ends in nothing** (`27_escape_coast_road`, shared with the outdoor-zones package:
   you own what lies beyond the road: the view of the sea, the shore, a marker light or beacon at the overlook).
7. **Major — crane shot back at the island** (`28_ending_2`): forest edge is a row of identical straight trunks; no
   mist although the caption says "misty island".
8. Minors in those files about vegetation, terrain and water. Also: the Talon pen floor is still bare dirt (zone 2
   asked for forest floor and scatter there).
Do not change play-area heights, colliders, triggers or NavMesh-relevant ground. Performance: the hill jungle is
drawn by `InstancedFoliageRenderer`; keep triangle counts where they are (report `perf_here.sh` in the valley).
