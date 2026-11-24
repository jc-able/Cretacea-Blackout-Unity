# Package: TERRAIN, VEGETATION, WATER  (branch `look/terrain`)

**Problem.** The island reads as a desert: a flat brown basin ringed by smooth brown domes, ~165 trees on one square
kilometre, straw-coloured "grass", an unlit exposure-hacked ocean. It must become a lush volcanic jungle island with
real water. This is the single biggest visual change in the game (prologue, every outdoor night scene, the ending).
Read `docs/maps_2026-11-20/digest_zone4_terrain.md` and `notes_zone4_terrain.md` first.

**You own:** `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs`, new files
`Assets/Editor/LookPass/LookTerrain*.cs` / `LookVegetation*.cs`, `Assets/Art/Terrain/*`, new texture sets you derive
locally under `Assets/Art/Textures/<new_set>/`, new prefabs under `Assets/Prefabs/Foliage/`, new runtime scripts under
`Assets/Scripts/Rendering/` (water, wind), and in `Assets/Scripts/Player/PlayerController.cs` ONLY the
`terrainLayerSurfaces` mapping. Generated terrain data (`CretaceaTerrain.asset`) is rebuilt by the build: do not commit it.

## Goals, in priority order
1. **Ground that reads as jungle.** New terrain layers (max 8): lush grass, jungle floor (leaf litter), moss, mud,
   gravel road, mossy rock, sand. No green or sand set exists: derive them from existing sets with Python/PIL
   (e.g. recolour `grass_ground` straw → tropical green with hue variation kept; `mud`/`gravel_road` → wet sand),
   reusing the set's Normal/Mask/Height. Bind the **dry** `_MaskMap` by exact file name (the old binder picked
   `_MaskMap_Wet` for every layer). Paint by rules: rock only where slope > ~38° broken up with noise; delete the
   "y > 18/22 ⇒ rock" and "x > 546 ⇒ rock" clauses; mud on stream banks and worn paths; gravel on Service Road 3;
   sand at the waterline; macro colour variation so 14 m tiling is not visible on hills. Keep
   `PlayerController.terrainLayerSurfaces` in step with the layer order (footstep sounds).
2. **Shape.** Inside the play area the terrain must stay **exactly** as it is: do not change
   `EvaluateWorldElevation` for X −125…560, Z −215…195 (zones 1–3 hard-code heights there; plateaus at
   Zone4:341-344). Outside it: turn the ring into believable volcanic jungle hills (domain-warped fBm, ridges,
   gullies, a dominant peak or two; most slopes under 40° so trees stand; silhouettes that read against the sky).
   No sheer cuts at the world edge: the island must end in coast or be backed by more hills/sea in every direction
   a player or cutscene camera can see. East coast: a real shoreline under the overlook (beach or rocks), seabed
   well below the water.
3. **Vegetation, 20–50× denser, and cheaper to draw.** Build LODGroup prefabs from the existing `_LOD1/_LOD2` FBX
   files (LOD0 → LOD1 → LOD2 → culled; small plants cull early), GPU instancing on. Jungle on the hills (far trees
   use the cheapest LOD), dense belts around the valley floor, right behind the service-path walls and outside the
   yard walls (shrink `FoliageExclusions` to the real footprints + ~2 m), understory (banana_plant, monstera_clump,
   tree_fern, shrubs, ferns, fallen_log, root_cluster, rocks), ground cover near the walkable route. Deterministic
   (seeded). Trunk colliders only for trees next to walkable ground; drop the carving `NavMeshObstacle`s (the
   NavMesh is baked from colliders). Never block Service Road 3, the paths, plateaus, the stream ford/bridge, the
   intro and ending camera lines, or the 7 `TallGrassZoneVolume` stealth patches (keep them, make them look like
   tall grass).
   **Budget:** today a frame draws 12.1 M triangles from 4,577 renderers with no LODs. Target ≤ 4 M triangles in any
   gameplay view and ≥ 60 fps at 1280×720 in your clone's Play mode
   (`unity command diagnostics get_performance`). Report the numbers.
4. **Water.** HDRP Water System (already enabled in the HDRP assets): the sea as a `WaterSurface` (OceanSeaLake,
   infinite) at sea level with foam at the shore; the valley stream as a River surface or a lit ribbon that follows
   `EvaluateStreamX`; the Titan Meadows lake if cheap. Remove the old ocean grid and the 29 stream cubes' renderers
   but **keep** their Trigger-layer colliders with `SurfaceTag` Water (water footsteps), the rescue boat, all
   anchors and triggers. The water must look right in all five presets (it is lit, so it should).
5. Wind sway for nearby plants if you find a cheap, robust way (optional).

## Viewpoints to capture (before you change anything, then after)
- Prologue eye-level: `look_preview --preset GoldenDay`, camera (−42, 2.0, −92) looking at (−30, 3, 0); and
  (−60, 3, −150) looking at (−40, 4, −40).
- Aerials (day): (−250, 220, −260) → (0, 0, 0); (750, 220, −300) → (400, 0, −30); (150, 140, −200) → (150, 0, 0).
- Valley route at night: Play mode, `jump_chapter --chapter Substation`, and a free camera at (160, 2.2, 6)
  looking at (200, 2, 2) with `look_preview --preset NightStormBlackout`.
- Ending: Play mode `jump_chapter --chapter DawnCoast` twice (the first jump plays the ending cutscene), and a
  free camera at (543, 2.7, −46) looking at (900, −10, −46) with `look_preview --preset DawnCoast`.
