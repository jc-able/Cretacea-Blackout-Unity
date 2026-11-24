# Zone 4 map: terrain, jungle vegetation, stream, dawn coast, ocean, backdrop

Read-only map of `Assets/Editor/WorldBuilder/Zone4_TerrainJungleAndDawnCoastBuilder.cs` (Z4) and its assets.
Paths are relative to `~/CretaceaBlackoutUnity/` unless absolute. "not verified" = not checked.

TL;DR (why it looks like a desert):
1. 44 % of the map is a 58-136 m smooth-dome ring painted 100 % warm-brown rock by the `y > 22` rule (Zone4:152-156); the east strip x > 546 is forced >= 85 % rock (Zone4:155).
2. The "grass" layer texture is tan/straw (mean sRGB 0.59/0.52/0.35), every layer binds the WET mask (smoothness 0.68-0.89), no green or sand texture exists.
3. 165 trees on 32 ha (~5/ha, 20 m apart), nothing on the ridge, no ground cover; the "3,600 vegetation objects" are 225 grass tufts x 4-21 sub-meshes. Plant materials lack mask maps and (very likely) alpha clipping.
4. Ocean + stream use an unlit custom shader scaled x2600 by exposure: blown-out at dawn, ~1000x too bright at night; the stream is 29 wobbling cubes; HDRP Water is disabled in the HDRP assets.

## 0. Entry points, call order, idempotency

- Called once from `CretaceaWorldAssembler.BuildMainWorldScene` (Assets/Editor/WorldBuilder/CretaceaWorldAssembler.cs:283), AFTER Zones 1-3 (lines 274-281) and BEFORE the NavMesh bake (line 352, `BuildAndBakeNavMeshes`). Scene is `EditorSceneManager.NewScene(EmptyScene)` (line 270) => every GameObject below is recreated on each `cretacea build_all_scenes`; hand edits in MainWorld.unity are lost.
- `Zone4.Build(worldRoot, palette)` (Zone4:62-73) = 4 steps in order: `BuildUnifiedTerrain` (79) -> `BuildTyrantValleyStreamAndGrassPatches` (395) -> `BuildExclusionGuardedRainforest` (458) -> `BuildDawnCoastAndOceanVista` (540). Root GO `Zone_04_TerrainJungleAndDawnCoast` = 3,639 transforms in the live scene (env.txt "ROOT World").
- Persistent ASSETS (survive rebuilds, are UPDATED in place, not recreated): `Assets/Art/Terrain/CretaceaTerrain.asset` (TerrainData; heights + splat fully rewritten every build, Zone4:83-108,172), `TL_*.terrainlayer` (tileSize/smoothness/metallic and texture refs re-assigned every build, Zone4:212-277), `MAT_CretaceaTerrain.mat` (created only if missing, Zone4:188-195; never re-configured => hand edits to this material DO survive), `Assets/Settings/Rendering/DawnOceanGrid.asset` (mesh cleared + rebuilt every build, Zone4:615-677). `MAT_DawnOcean.mat` is only loaded (Zone4:401,572), never written => hand edits survive.
- Terrain-layer hand edits do NOT survive: `CreateOrUpdateTerrainLayer` overwrites `tileSize`, `smoothness`, `metallic` (Zone4:222-224) and re-binds textures by filename search; normalScale, diffuseRemap, maskMapRemap are NOT touched (survive).
- Randomness is deterministic: `System.Random(90421)` for trees+rocks (Zone4:463), `System.Random(199600 + i*31)` per grass patch (Zone4:436). Noise = `Mathf.PerlinNoise` (deterministic). Same build => same layout.
- Helpers used: `WorldMaterialPalette.InstantiatePropOrFallback(stem, parent, pos, euler, targetSize, grounded, addCollider, useMeshCollider, fallbackMat, customName)` (WorldMaterialPalette.cs:351-406) = `PrefabUtility.InstantiatePrefab` of the LOD0 FBX found by filename stem under Assets/Art/{Props,Foliage,Rocks,Dinosaurs,Characters} (FindModelAsset 471-503, prefers `t:Prefab`, then `t:Model`; exact stem match), uniform scale so the LARGEST bounds axis = max(targetSize) (NormalizeScaleAndGround 408-429), bottom of bounds put at pos.y if grounded, layer set to Environment recursively (404). Missing model => grey/green cube proxy (379-402). `WorldArchitectureKit.CreateBox` (WorldArchitectureKit.cs:926-952) = Unity cube primitive, localScale = size, layer Environment, BoxCollider unless addCollider=false.
- No LODGroup is ever created (env.txt: `lodGroups=0`); the `*_LOD1.fbx/_LOD2.fbx` files are never referenced. Zone 4 code sets no static flags on its props/boxes (StaticEditorFlags appear only in WorldArchitectureKit.cs:222-225,273,468,473 for walls/floors; the terrain GO's own flags were not checked).
- RNG coupling: trees and boulders share one `System.Random(90421)` stream (Zone4:463, 469-528), so changing the number/order of rng calls in the tree loop also moves every boulder.

## 1. Height function, terrain object, key coordinates

Terrain object (Zone4:18-28, 90-93, 175-196; confirmed by env.txt TERRAIN line):
- GO `IslaTormenta_UnifiedTerrain`, position (-320, -42, -440), size 1024 x 180 x 1024 => X -320..704, Z -440..584, Y -42..138. Layer Environment.
- heightmapResolution 513 (2.0 m per sample), alphamapResolution 512 (2.0 m per texel), baseMapResolution 512, `drawInstanced=true`, `heightmapPixelError=4`, `basemapDistance=450` (Zone4:182-184). No trees, no details (treeProtos=0, detailProtos=0, detailRes=0), terrainHoles off in HDRP asset.
- Height samples are clamped to [-42, 136] (Zone4:346).

`EvaluateWorldElevation(x,z)` (Zone4:288-347), applied in this order:
1. Base: `y = 0.6 + (Perlin((x+500)*0.0085,(z+500)*0.0085)-0.48)*3.6 + (Perlin((x+120)*0.032,(z+120)*0.032)-0.5)*0.65` (291-293) => +-1.8 m rolls, ~118 m and ~31 m wavelengths.
2. Stream trough (296-301): only for x 160..248 and |z| < 190; centreline `EvaluateStreamX(z) = 205 + 18 sin(-z/45) + 6 sin(-z/13)` (371) => X 181..229; depth `2.5*(1 - smoothstep(3.5,15, dist))` (full 2.5 m within 3.5 m of centre, 0 at 15 m).
3. Titan Meadows south basin (304-314): z < -96 and x < 180; blend `smoothstep(-96,-135,z)*(1-smoothstep(110,180,x))` towards `-6.2 + gentleRoll*0.6`; lake hollow centre (-65,-210), r 55 m, extra -2.8 m (full inside 25 m). NOTE: no water object is placed in this lake hollow (none in Zone4) - not verified whether another zone adds one (grep found none).
4. Volcanic ring (317-325): `ring = max(smoothstep(175,340,z), smoothstep(-155,-295,x), smoothstep(-255,-410,z))`; `y += ring^2 * 105 * (0.55 + 0.75*Perlin(x*0.014+4.1, z*0.014+1.9))` => crest 58-136 m (clamped 136). ONE low-frequency Perlin (~71 m) is the only relief on the ridge: smooth domes, no ridgelines/erosion/terracing. No east wall.
5. East coast (328-338), x > 520: `y = lerp(y, lerp(1, -42, smoothstep(546,625,x)), smoothstep(520,550,x)) + headland*38`, headland = max(smoothstep(45,160,z), smoothstep(-145,-260,z)) * (1 - smoothstep(610,680,x)). The lerp REPLACES the ring for x > 550, so the N/S ring meets the coast as a 38 m headland. Central corridor z -130..+40 stays open.
6. Plateaus `BlendPlateau(y,x,z,xMin,xMax,zMin,zMax,targetY,margin)` (361-369; smoothstep falloff over `margin` m outside the rect), Zone4:341-344:
   - Visitor Center + Plaza + Paths + Talon: X -104..92, Z -96..130 -> Y -0.05, margin 18.
   - Substation 3 pad: X 248..288, Z -19..19 -> Y 2.26, margin 20.
   - Old Maintenance Yard: X 398..508, Z -97..52 -> Y 0.96, margin 18.
   - Coast Road overlook: X 505..547, Z -51..-41 -> Y 0.96, margin 8.

Key coordinates (Unity world):
- Shoreline: terrain crosses the ocean level Y=-40 at about x = 614 in the open corridor (bluff smoothstep 546->625); headlands push it to about x 650-680 for z > 45 / z < -145 (computed from 330-337, not measured). Seabed east of x~625 is flat at Y -42 = only 2 m under the water (Zone4:331 vs 575). Terrain ends at x = 704; beyond that only the ocean plane.
- Coast road asphalt box centre (526, 0.82, -46), 42 x 0.36 x 7.6 m (Zone4:547); fences z -42.1 / -49.9 from x 505 to 546 (549-550); overlook rail x 546.5 (553-558).
- Ending anchors: `coast` (543, 1.0, -46) yaw 90; `ending_view` + `dawn_coast` (543, 1.05, -46) yaw 90 (looking +X = east); `dawn_crane_peak` (536, 15.2, -42) yaw -85; StoryTrigger `coast_ending` centre (536.5, 3.5, -46) size 4.5 x 5 x 12 (Zone4:561-565).
- Ocean plane centre (1700, -40, -46) (Zone4:575). Evac boat root (715, -39.4, -30) yaw -32 (Zone4:586).
- Stream: centreline X = EvaluateStreamX(z), water strips z -154..+154 (Zone4:402-417).
- Tall-grass patch centres (x, z, r): (140,-16,17) (152,26,14) (226,32,15) (232,-24,13) (320,10,18) (352,-44,15) (118,20,10) (Zone4:39-48).
- Service Road 3 polyline (91,-3.5)->(120,0)->(160,8)->(200,4)->(240,0)->(250,0) [gap = Substation pad] (286,0)->(310,-8)->(360,-20)->(401,-22.8) (Zone4:31-36; gap skip at 380).
- Titan Meadows herbivore anchors: meadow_sauropod_a (-40, h, -150), meadow_sauropod_b (-95, h, -185), meadow_trike (-18, h, -142) (Zone4:531-533).

## 2. Splat painting, terrain layers, TerrainLit material

Splat loop (Zone4:110-172), per 2 m alphamap texel, layer order 0=Mud 1=Grass 2=Gravel 3=RockyCliff:
- Start Mud 0.15, Grass 0.85 everywhere (124-127) => even "grass" areas carry 15 % mud.
- Stream banks (130-136): x 155..250 and |x - streamX| < 12 m => mud up to 0.95 (linear to 0 at 12 m), grass x (1 - bank*0.85).
- Gravel (139-149): x 90..402, outside the Substation pad (x 249.5..286.5, |z| <= 17.5), distance to Road 3 polyline < 4.5 m => gravel = 1 - smoothstep(2.6, 4.5, d). This is the ONLY path painting; the Zone 2 service paths / Visitor Center paths / Old Yard aprons / coast road get no mud or gravel strip.
- Rock (152-162): if `slope > 22 deg || x > 546 || y > 18`: rock = clamp01((slope-20)/18); x > 546 => rock >= 0.85; y > 22 => rock >= clamp01((y-22)/28) (100 % above 50 m). Other layers scaled by (1-rock). Slope comes from `td.GetSteepness` (121) right after SetHeights.
- No sand layer, no beach, no wet shoreline, no forest-floor layer under trees, no noise/macro variation in weights (pure function of slope/height/position).

Why rock = 59 % (lead's measure): I re-implemented Zone4:288-347 + 110-170 in numpy (classic Perlin stand-in for Mathf.PerlinNoise) and got mud 6.6 / grass 34.7 / rock 58.7 % vs measured 6.5 / 34.3 / 59.0 => the rules alone explain it. Area facts from that replica: y > 22 m = 44.2 % of the terrain, y > 50 m (pure rock) = 35.8 %, slope > 22 deg = 53.5 %, x > 546 strip = 15.4 % (Zone4:155 forces >= 85 % rock there, including the flat seabed under the ocean). Ring slopes: mean 43.5 deg, p90 59 deg; crest p50 86 m, p90 117 m, max 136 m. The flat play basin (-8 < y < 6, x < 546) is 37 % of the map and only ~1 % rock: the basin looks like desert because of the GRASS TEXTURE COLOUR and missing ground cover, not because of rock.

Terrain layers (`BuildTerrainLayers` Zone4:201-210 -> `CreateOrUpdateTerrainLayer` 212-278; values verified in Assets/Art/Terrain/TL_*.terrainlayer):
| idx | asset | tile m | smoothness field | bound diffuse / normal / mask | mean sRGB of diffuse | mask A (smoothness) mean |
|---|---|---|---|---|---|---|
| 0 | TL_Mud | 8 | 0.55 | Textures/mud/mud_BaseColor, mud_Normal, **mud_MaskMap_Wet** | 0.33/0.29/0.23 | 0.89 (dry map: 0.48) |
| 1 | TL_Grass | 10 | 0.35 | grass_ground_BaseColor, _Normal, **_MaskMap_Wet** | 0.59/0.52/0.35 (tan/straw, NOT green) | 0.68 (dry 0.32) |
| 2 | TL_Gravel | 7 | 0.45 | gravel_road_BaseColor, _Normal, **_MaskMap_Wet** | 0.31/0.31/0.30 | 0.71 (dry 0.22) |
| 3 | TL_RockyCliff | 14 | 0.30 | rock_cliff_BaseColor, _Normal, **_MaskMap_Wet** | 0.51/0.41/0.32 (warm brown) | 0.74 (dry 0.18) |
- Texture binding is a substring search over every Texture2D in Assets/Art/Textures (Zone4:229-245): hints mud/dirt/soil, grass/forest_floor/moss_ground, gravel/pebble/dirt_road, rock_cliff/basalt/rock_face/rock. The mask assignment (Zone4:240) has no "first wins" guard and no `_wet` filter, so the LAST matching `*maskmap*` wins = `*_MaskMap_Wet.png` for all 4 layers (GUIDs resolved: TL_Grass mask 19b359c0... = grass_ground_MaskMap_Wet.png etc.). Diffuse has a `== null` guard so the dry `_BaseColor` wins over `_BaseColor_Wet`. Result: the whole terrain is permanently "wet" (smoothness 0.68-0.89) in every preset, including GoldenDay.
- All mask maps have B = 0.00 (packer writes R=Metallic, G=AO, B=0, A=Smoothness; CretaceaPbrImporter.cs:319-320) => HDRP TerrainLit height-based blending has no height data. Each set ships a `*_Height.png` (e.g. Assets/Art/Textures/grass_ground/grass_ground_Height.png) that could be packed into B.
- normalScale 1, diffuseRemap 0..1 (no tint), maskMapRemap 0..1, smoothnessSource 1 (TL_*.terrainlayer). Layer `smoothness` fields are ignored when a mask map is bound (HDRP TerrainLit_Splatmap.hlsl:97 lerps by `_LayerHasMask`).
- 2K textures (all BaseColor 2048x2048 RGB). Tile 7-14 m on 1024 m => obvious repetition from the air; no macro/detail variation.

`MAT_CretaceaTerrain.mat` (HDRP/TerrainLit, Assets/Art/Terrain/MAT_CretaceaTerrain.mat): keywords only `_TERRAIN_INSTANCED_PERPIXEL_NORMAL`; no `_EnableHeightBlend` => `_TERRAIN_BLEND_HEIGHT` off (linear splat blending, mushy transitions); MOTIONVECTORS pass disabled; queue 2225. 4 layers only (one splat texture; a 5th-8th layer needs `_TERRAIN_8_LAYERS` = second control map, TerrainLit.shader:60).

Gameplay coupling of the splat: footsteps read the dominant layer index via `PlayerController.SampleTerrainDominantSurface` (Assets/Scripts/Player/PlayerController.cs:529-552) and map it through `terrainLayerSurfaces = {Mud, Grass, Gravel, Concrete}` (PlayerController.cs:53-59); an index past the array returns Grass (551). Re-ordering layers changes footstep sounds; new layers 4+ need that array extended.

## 3. Vegetation placement

Everything is one GameObject per plant (`PrefabUtility.InstantiatePrefab` of the LOD0 FBX), no Terrain trees/details, no LODGroup, no GPU-instanced scatter, no wind. Shadows: never changed => every foliage renderer casts shadows (default On). Layer: Environment (WorldMaterialPalette.cs:404).

A. Rainforest trees, `BuildExclusionGuardedRainforest` (Zone4:458-507):
- Up to 420 attempts, cap 165 trees; candidate x = U(-160, 528), z = U(-260, 210) (Zone4:471-472) = a 688 x 470 m rectangle (32.3 ha) that never reaches the ridge (ring starts z 175 / z -255 / x -155) => 0 trees on 44 % of the terrain.
- Rejects: `IsExcludedFromFoliage` rects (474; rects at Zone4:51-60), Road 3 < 7.5 m (475), stream centre < 8.5 m (476), terrain y < -7.5 or > 36 (479).
- Stems round-robin `{jungle_tree, palm_tree, palm_tree_b, tree_fern}` (464, 481) => ~41 each. targetSize (5.6, 10.5, 5.6)*s, tree_fern (3.2, 4.0, 3.2)*s, s = U(0.85, 1.40) (482-485); yaw U(0,360); no tilt, no colour variation.
- Trees with x 88..410 and |z| < 165 get a trunk CapsuleCollider (centre y 3, r 0.42, h 6) + carving NavMeshObstacle capsule (r 0.52, h 5) (491-504). NOTE the NavMesh bake already collects PhysicsColliders on Default|Environment|Door (CretaceaWorldAssembler.cs:1262-1270, `useGeometry = PhysicsColliders`), so the carving obstacles duplicate the baked holes and cost runtime carving.
- Replay of the RNG (System.Random(90421) re-implemented with the .NET legacy algorithm; assumes Unity's Random = .NET legacy, not verified in scene): 165 trees placed after 214 attempts (41 rejected by rects, 3 road, 5 stream); 72 in Tyrant Valley collider zone, 28 in Titan Meadows, 65 elsewhere; ~5.1 trees per hectare, median nearest-neighbour distance ~20 m. A tropical forest is several hundred stems/ha => this is 50-100x too sparse, hence "sparse palms on brown ground". Live renderer counts agree: M_jungle_tree 49, M_palm_tree 48, M_palm_tree_b 41, M_tree_fern 41 (materials.txt; extras come from Zone 2).

B. Stealth tall grass, `BuildTyrantValleyStreamAndGrassPatches` (Zone4:419-451):
- 7 trigger spheres (`TallGrassZoneVolume`, layer Trigger, centre y +0.7) with radius 10-18 m (Zone4:425-433).
- Tufts per patch = round(r * 2.2) (437) => 225 total over ~4,800 m2 (sum of pi r^2) = one tuft per ~21 m2. Stems cycle fern_02 / grass_medium_01 / grass_bermuda_01 (447), targetSize (1.8, 1.35, 1.8) (449; uniform scale so the largest axis = 1.8 m, so flat clumps end up far lower than 1.35 m), yaw random, grounded, no collider.
- These tufts ARE the "~3,600 vegetation GameObjects": grass_bermuda_01.fbx has 21 sub-meshes, grass_medium_01.fbx 17, fern_02.fbx 4 (my FBX parse, section 6) => live renderers M_grass_bermuda_01 1,533, M_grass_medium_01 1,258, M_fern_02 312 (materials.txt) = ~3,100 of Zone 4's 3,639 transforms. grass_medium_01 is 24,730 tris per tuft (~1.8 M tris for ~75 tufts).
- Visual gap: the stealth mechanic (PlayerController visibility x0.18-0.50 in grass, PlayerController.cs:568-572; trigger component Assets/Scripts/Core/TallGrassZoneVolume.cs:9) has almost no visible grass to sell it.

C. Valley boulders (Zone4:509-528): 36 rocks, x U(95,520), z U(-170,170), skip rects and Road 3 < 9.5 m; stems round-robin boulder_01 / rock_face_01 / namaqualand_boulder_03; size s = U(1.6, 4.4), targetSize (1.4s, s, 1.3s); placed at terrain y - 0.35, NOT grounded (`grounded:false`) so the pivot, not the bounds bottom, sits 0.35 m under ground; BoxCollider (addCollider) + carving NavMeshObstacle box (523-527). Replay: all 36 placed in 41 attempts. No rocks on the ridge, no cliff meshes on steep slopes.

D. Other vegetation outside Zone 4: MainWorld - Zone2_ServicePathsBuilder.cs:124,127-128,159,201,264 (palms/jungle trees along paths, hanging_vines x2, fallen_log x2, Talon pen trees). MainMenu.unity only - CretaceaWorldAssembler.cs:1387-1430 `Menu_StormJungleExterior` (6 palms, 3 pachira, 2 fern_02, shrub_01, coastal_cliff_01, namaqualand_boulder_02 + RockyCliff box "ridges").

E. Material state of the plants (materials.txt; FBX meta `materialImportMode: 2`, `materialLocation: 1`, `externalObjects: {}` e.g. Assets/Art/Foliage/PolyHaven/grass_medium_01/grass_medium_01.fbx.meta:7-12):
- Embedded FBX materials `M_<stem>` (HDRP/Lit, Standard matid=1, surf=0 opaque), base colour + normal only, NO mask map although every set ships `<stem>_MaskMap.png` + `_MaskMap_Wet.png` next to the FBX; smoothness constants from FBX (grass_bermuda 0.62, grass_medium 0.56 = plastic-shiny grass; palm 0.19, fern 0.06).
- Poly Haven grass/fern/shrub/moss/anthurium/calathea BaseColor PNGs are RGBA (leaf cut-outs in alpha), but HDRP's FBX postprocessor enables alpha clipping only when the FBX material is transparent (FBXMaterialDescriptionPostprocessor.cs:59-77 in the HDRP package) and these imported as surf=0 => alpha cutout is most likely OFF => blades/fronds render as opaque cards. Not verified visually (no close-up shot of a tuft). Nothing in Assets/Editor post-processes FBX materials (CretaceaPbrImporter.cs only has OnPreprocessTexture/OnPreprocessModel, 121-259).
- No Translucent material type / diffusion profile, no double-sided flag, no wind. Tripo tropical trees (jungle_tree etc.) are single-mesh opaque RGB 4K textures, so cut-out is not their issue; their issue is sparse count and flat smoothness.

## 4. Stream water + dawn ocean + boat/beacon

Stream (Zone4:400-417):
- 29 axis-aligned Unity CUBES `StreamWater_0..28` (i = -14..14), centre (EvaluateStreamX(z0), terrainY(centre) + 0.22, z0 = i*11), size 9.0 x 0.12 x 12.2 m, NOT rotated to follow the meander (the centreline swings +-24 m in X, so consecutive 12.2 m slabs step sideways; banks are cut only 2.5 m deep, Zone4:299). Range z -154..+154 (the trough itself runs |z| < 190).
- Material = `MAT_DawnOcean` (the ocean shader!) with fallback `p.GlassArchitectural` (401). Layer Trigger (409); BoxCollider kept as trigger, local size (1,14,1) => world 9 x 1.68 x 12.2 (410-415); SurfaceTag Water (416) for footsteps. Not in the NavMesh (trigger layer).
- The ocean shader displaces vertices with 0.55 m Gerstner waves in world XZ (CretaceaOcean.shader:99-138,171-190): on a 24-vertex 0.12 m-thick cube the corners bob up to +-0.55 m, so each slab tilts/warps independently and the overlapping slabs z-fight. It also applies the dawn haze and `_LuminanceScale` (below) => the stream has no flow direction, no bank foam, no transparency, and its brightness is tied to the dawn exposure.
- Culvert bridge carrying Road 3 over the stream is built by Zone 3 at (201, ?, +4) (Zone3_SubstationAndYardBuilder.cs:58-66); `trex_drink` anchor (192, 0, 40) (Zone3:55). Zone 3 runs BEFORE the terrain exists and uses hard-coded Y values in the valley (e.g. feeding cage y 0.2 at (150, 62), Zone3:71).

Ocean (Zone4:571-581, 615-677):
- GO `DawnCoast_HDRPOcean`, layer Water, position (1700, -40, -46); mesh `Assets/Settings/Rendering/DawnOceanGrid.asset` = 2400 x 3400 m flat grid, 72 x 72 quads (10,368 tris), X spacing concentrated west with pow(vx, 1.35) (645) => covers X 500..2900, Z -1746..1654; UV = (shapedX*16, vz*22). shadowCastingMode Off, no collider. Ocean surface Y -40 vs seabed Y -42 => 2 m deep over the whole submerged terrain (x ~614..704); beyond x 704 there is no seabed at all (sky depth).
- `MAT_DawnOcean` (Assets/Settings/Rendering/MAT_DawnOcean.mat) -> shader `Cretacea/HDRP/DawnOcean` (Assets/Shaders/CretaceaOcean.shader, 266 lines): single `ForwardOnly` pass, `Queue=Transparent-10`, ZWrite On, Blend One Zero (opaque output), Cull Back (17-52). It is effectively UNLIT: it never reads HDRP lights, sky, fog, shadows, reflection probes or SSR. Lighting is faked from material constants: sky reflection = lerp(_HorizonSkyColor (0.95,0.62,0.36), _ZenithSkyColor (0.14,0.24,0.42)) (214-216), body = lerp(_DeepColor (0.025,0.095,0.145), _ShallowColor (0.075,0.28,0.32)) (219-221), GGX sun glint with `_SunDirection` (0.35,0.16,0.92) = mostly +Z/north, not the +X ending view (204-228), custom horizon haze to `_OceanHazeColor` (0.85,0.64,0.46) between `_HazeStart` 160 m and `_HazeEnd` 900 m (255-256), then `col *= _LuminanceScale * GetCurrentExposureMultiplier()` with `_LuminanceScale = 2600` (259).
- Consequence: brightness depends only on the camera exposure. Presets' fixed EV (Assets/Settings/Atmosphere/Preset_*.asset `fixedExposure`): BlackoutStorm 0.6 (line 128), InteriorEmergency 1, PowerRestored 1.2, DawnCoast 8.9 (477), OpeningDusk 11.2 (567). Exposure multiplier 1/(1.2*2^EV) => x2600 gives ~1430x at EV 0.6, ~4.5x at EV 8.9, ~0.9x at EV 11.2. So the haze colour (0.85,0.64,0.46) is ~(3.9, 2.9, 2.1) at DawnCoast = the blown white band in shots_base/play_DawnCoast_5s.jpg, and anything using MAT_DawnOcean (ocean AND the 29 stream slabs) would be ~1000x over-bright in night presets (calculated, not seen in a shot). No script modulates the material (grep for `_LuminanceScale`/`DawnOcean` in Assets: only Zone4).
- Foam: crest foam from wave crest + value noise (234-235) and depth-contact foam `1 - (sceneEyeDepth - waterEyeDepth)/_FoamDistance(3.2)` (238-249). With only 2 m of water over the flat seabed, the view-ray thickness is a few metres over the whole x ~614..704 band => a wide uniform foam/colour band that stops dead at the terrain edge x = 704 (seen as the hard white coastline outline in shots_base/aerial_east_oblique.jpg).
- HDRP Water is NOT available right now: `supportWater: 0` in all quality assets (Assets/Settings/HDRP High Fidelity.asset:234, HDRP Balanced.asset:236, HDRP Performant.asset:235); env.txt `water=False`. The default volume profile already has `WaterRendering enable=True` (env.txt DEFAULT PROFILE).

Evacuation boat `DistantEvacBeacon_Bay` (Zone4:584-612): root (715, -39.4, -30) yaw -32 (outside the terrain, over open water); 15 primitive cubes (hull DarkPaintedMetal, deckhouse PlasterClean, stripes EmissiveAmber, bridge windows DarkGranitePlinth, foam collar = PlasterClean box 32 x 0.18 x 9.6 m) (589-605). It does not bob with the Gerstner waves (static boxes; waves move the water +-0.55 m around it). Lights: `Light_EvacBeacon_White` point 85,000 cd range 260 m, volumetric dimmer 1.5, pulsing (611); `Light_EvacSearchlight` spot 140,000 cd range 260 m, outer 28 deg, aimed at (560, -18, -46) (610-612); both LightGroupId.VCExit, shadows None (lights.txt rows 104-105: pos (716.5,-26.5,-29.0) and (719.7,-32.6,-27.0)).

## 5. Rocks / cliffs / backdrop meshes

- Only two cliff meshes: `DawnHeadland_NE` = coastal_cliff_01 at (585, -38, 48) yaw -110 targetSize (85, 52, 75); `DawnHeadland_SE` = coastal_cliff_01 at (590, -38, -140) yaw -65 targetSize (92, 56, 78); `grounded:false`, no collider (Zone4:568-569). coastal_cliff_01 is a 92 x 10.3 x 11 m cliff STRIP (Godot docs: ~/CretaceaBlackout/assets_src/polyhaven/REPORT.md:106); uniform scale to max(target) keeps it ~10 m tall. The terrain surface there is about y -20 (NE) and -24 (SE) (computed from Zone4:330-337), so a 10 m strip based at y -38 is buried 8-14 m under the bluff => effectively invisible (computed; consistent with the two tiny dark specks at the shore in shots_base/aerial_east_oblique.jpg). The visible "headlands" are the terrain's +38 m bumps (Zone4:334-337), painted >= 85 % rock.
- Valley boulders: 36, see section 3.C (Zone4:509-528). Nothing else breaks up the ridge: no rock outcrops, no cliff meshes on the 43 deg slopes, no scree, no vegetation above y 36.
- No backdrop at all beyond the terrain: the terrain ends with a sheer vertical cut at x -320, z -440, z 584 (ring at full 58-136 m height there) and only the ocean plane exists outside (X 500..2900). Visible in shots_base/aerial_east_oblique.jpg (straight wall at the north-east corner with ocean beyond). From the play basin (y ~0) the ring crest hides the cut; from high cameras (dawn_crane_peak y 15.2, aerials) it can show. Not verified per camera.
- Unused backdrop textures: `Assets/Art/Textures/backdrop_jungle_aerial/*` (Poly Haven rocky_terrain_02, 90 m scan, mean sRGB 0.31/0.31/0.11 = the only green-olive ground texture in the project) and `backdrop_cliff_vegetated/*` (coast_sand_rocks_02, 15 m) - referenced by no C# (grep "backdrop" hits only UI code).
- What the Godot original had and the port dropped (~/CretaceaBlackout/scripts/world/zone_backdrop.gd): two distant ring meshes textured with backdrop_jungle_aerial tinted (0.42, 0.5, 0.38) at uv 0.004 (radius 600-1300 m / 170 m high and 1200-2300 m / 360 m high, lines 87-90), and `_jungle_walls` MultiMesh belts of jungle_tree/palm_tree at 1 tree per 110 m2 (~90 trees/ha, scale 1.0-1.6, sway 0.15) closing sightlines around the VC and paths (lines 292-303; Godot coordinates, Z sign vs Unity not verified).

## 6. Asset inventory (Foliage, Rocks, Textures, HDRIs)

Triangle / sub-mesh counts below come from parsing the binary FBX `PolygonVertexIndex` arrays myself (FBX 7.4); they match the Godot tris column for the Tripo models (~/CretaceaBlackout/docs/ASSET_SOURCES.md:47-54). Every folder has `Textures/<stem>_BaseColor.png, _Normal.png, _MaskMap.png, _MaskMap_Wet.png` (pachira also `_m2_*`). Tropical = Tripo v3.1 generated, 4096 RGB BaseColor; PolyHaven + Rocks = 2048. "used" = referenced by builder code.

| model (Assets/Art/Foliage/...) | sub-meshes | tris LOD0 / LOD1 / LOD2 | BaseColor | used |
|---|---|---|---|---|
| Tropical/jungle_tree | 1 | 19,476 / 8,179 / 3,505 | 4K RGB | Zone4 trees, Zone2 |
| Tropical/palm_tree | 1 | 11,750 / 4,935 / 2,115 | 4K RGB | Zone4, Zone2, MainMenu x6 |
| Tropical/palm_tree_b | 1 | 11,901 / 4,998 / 2,142 | 4K RGB | Zone4 |
| Tropical/tree_fern | 1 | 11,765 / 4,941 / 2,117 | 4K RGB | Zone4 |
| Tropical/banana_plant | 1 | 11,515 / 4,836 / - | 4K RGB | UNUSED |
| Tropical/monstera_clump | 1 | 11,800 / 4,956 / - | 4K RGB | UNUSED |
| Tropical/hanging_vines | 1 | 9,587 / 4,026 / - | 4K RGB | Zone2 x2 |
| Tropical/fallen_log | 1 | 7,453 / 3,130 / - | 2K RGB | Zone2 x2 |
| PolyHaven/grass_bermuda_01 | 21 | 941 / - / - | 2K RGBA | Zone4 tufts |
| PolyHaven/grass_medium_01 | 17 | 24,730 / 12,362 / - | 2K RGBA | Zone4 tufts |
| PolyHaven/grass_medium_02 | 5 | 7,842 / 3,917 / - | 2K RGBA | UNUSED |
| PolyHaven/fern_02 | 4 | 6,232 / 2,801 / - | 2K RGBA | Zone4 tufts, MainMenu x2 |
| PolyHaven/shrub_01 | 1 | 19,999 / 6,999 / - | 2K RGBA | MainMenu x1 |
| PolyHaven/shrub_02 / _03 / _04 | 4 / 4 / 1 | 17,996 / 8,287 / 17,999 (LOD1 7,196 / 3,726 / 7,199) | 2K RGBA | UNUSED |
| PolyHaven/pachira_aquatica_01 | 8 (2 mats) | 19,992 / 6,992 / - | 2K RGB + m2 RGBA | MainMenu x3 |
| PolyHaven/anthurium_botany_01 | 6 | 17,996 / 7,195 / - | 2K RGBA | UNUSED |
| PolyHaven/calathea_orbifolia_01 | 5 | 16,688 / 7,505 / - | 2K RGBA | UNUSED |
| PolyHaven/moss_01 | 12 | 204 / - / - | 2K RGBA | UNUSED |
| PolyHaven/root_cluster_01 | 1 | 15,999 / 5,599 / - | 2K RGB | UNUSED |
| PolyHaven/dead_tree_trunk | 1 | 13,999 / 4,899 / - | 2K RGB | UNUSED |
| PolyHaven/tree_stump_01 | 1 | 12,000 / 4,200 / - | 2K RGB | UNUSED |
| Rocks/boulder_01 | 1 | 26,613 / 26,161 / 26,161 (LODs not reduced) | 2K RGB | Zone4 boulders |
| Rocks/rock_face_01 | 1 | 14,000 / 4,900 / 2,100 | 2K RGB | Zone4 boulders |
| Rocks/namaqualand_boulder_03 | 1 | 11,999 / 3,599 / 1,439 | 2K RGB | Zone4 boulders |
| Rocks/namaqualand_boulder_02 / _04 | 1 | 13,999 / 12,000 (LOD1 4,199 / 3,600; LOD2 1,679 / 1,439) | 2K RGB | _02 MainMenu x1; _04 UNUSED |
| Rocks/rock_face_02 | 1 | 15,999 / 5,598 / 2,399 | 2K RGB | UNUSED |
| Rocks/rock_moss_set_01 / _02 | 6 / 7 | 23,995 / 23,994 (LOD1 8,391 / 8,393) | 2K RGB | UNUSED |
| Rocks/mountainside | 1 | 28,000 / 9,800 / 3,920 | 2K RGB | UNUSED |
| Rocks/coastal_cliff_01 | 1 | 36,000 / 10,800 / 4,320 | 2K RGB | Zone4 headlands x2 (buried), MainMenu x1 |
- Godot sources also list island_tree_01/02/03 (Poly Haven) that were NOT ported (~/CretaceaBlackout/docs/ASSET_SOURCES.md, Poly Haven table).
- FBX import: `materialImportMode 2`, `materialLocation 1` (embedded), `isReadable 1`, `meshCompression 1`, no LOD screen percentages (grass_medium_01.fbx.meta:7-12,32-44). `CretaceaPbrImporter.OnPreprocessModel` sets readable/Low compression for env paths (CretaceaPbrImporter.cs:226-259).

Ground textures in Assets/Art/Textures (each: BaseColor, BaseColor_Wet, Height, MaskMap(B=0), MaskMap_Wet, Normal; 2048 RGB; mean sRGB / linear albedo measured by me): grass_ground (Poly Haven leafy_grass) 0.59/0.52/0.35, 0.217; mud (brown_mud_03) 0.33/0.29/0.23, 0.070; gravel_road (gravel_stones) 0.31/0.31/0.30, 0.077; rock_cliff (rock_face_03) 0.51/0.41/0.32, 0.153; forest_floor (brown_mud_leaves_01) 0.40/0.33/0.20, 0.087; moss_ground (forest_leaves_02, dry leaves) 0.56/0.44/0.23, 0.169; rock_mossy (mossy_rock) 0.42/0.42/0.34, 0.138; bark_tropical (palm_tree_bark, 4K); backdrop_jungle_aerial 0.31/0.31/0.11; backdrop_cliff_vegetated 0.29/0.27/0.18. No sand / beach / green jungle-floor texture exists. Source mapping: ~/CretaceaBlackout/docs/ASSET_SOURCES.md:74-100.

HDRIs (Assets/Art/HDRIs, 4K .hdr imported as cubemaps, maxTextureSize 2048): approaching_storm, belfast_sunset_puresky, kloppenheim_02_puresky, kloppenheim_07_puresky, qwantani_moonrise_puresky. Their GUIDs are referenced by nothing under Assets/Settings or Assets/Scenes (grep) => unused; skies are Gradient/PhysicallyBased (env.txt).

## 7. Upgrade hooks (lush repaint, trees/details/instancing, volcanic hills, HDRP Water)

All edits go in Zone4 (and the assets it updates); nothing in MainWorld.unity survives `cretacea build_all_scenes`.

(a) Lush repaint
- Splat rules = Zone4:110-172 only. Concrete changes: (1) delete the `worldY > 18` trigger and the `worldY > 22` ramp (152, 156) - they alone paint 44 % of the map rock; (2) delete `worldX > 546 => rock >= 0.85` (155) and paint by height instead: wet sand / beach for y in about [-41, -36], wet dark rock only for slope > ~38 deg on the bluff; (3) raise the slope rule from 22/20..38 deg (152-154) to roughly 35..50 deg and break it with noise so rock appears as outcrops, not bands; (4) default ground = jungle floor/moss + grass mix with Perlin patchiness instead of the flat 0.15/0.85 (124-125); (5) mud strips along ALL paths, not just Road 3 (139-149): add polylines for the Zone 2 service paths / Visitor Center plaza edges / Old Yard aprons, or derive a mask from the zone colliders (below).
- Layers = `BuildTerrainLayers` (201-210). Up to 8 layers render in one HDRP TerrainLit pass (`_TERRAIN_8_LAYERS`, `_NORMALMAP`, `_MASKMAP` are terrain built-in keywords, set by the engine: TerrainLit.shader:59-62). Candidate sources already in Assets/Art/Textures: forest_floor, moss_ground, rock_mossy, backdrop_jungle_aerial (only olive-green set; meant for 90 m tiling). There is NO sand and NO green grass texture: a green jungle-floor/grass and a sand set must be imported, or grass_ground tinted via `TerrainLayer.diffuseRemapMax` (TerrainLit "Color Tint", TerrainLitGUI.cs:218-222; Zone4 never writes it, so set it in code next to Zone4:222-224).
- Keep footsteps consistent: layer index -> SurfaceType array `terrainLayerSurfaces` (PlayerController.cs:53-59) must be re-ordered/extended to match the new layer list (index past the array => Grass, 551).
- Fix the texture binder (Zone4:229-245): skip paths containing `_wet` for both mask and diffuse (or bind explicit paths) so dry masks are used; wetness should come from a runtime/global control, not permanently baked into the layers.
- Height blending: set `_EnableHeightBlend = 1` and `_HeightTransition` (~0.2-0.4) on MAT_CretaceaTerrain (TerrainLit.shader:5-6; material created only when missing, Zone4:188-195, so set it in code there). Needs height in mask B: `CretaceaPbrImporter.PackMaskMap(out, metallic, ao, roughness, smoothness, orm, ...)` (CretaceaPbrImporter.cs:428-436) writes B = 0, so add a height input (each set has `<set>_Height.png`).
- Repetition: tile sizes 7-14 m (205-208) on a 1024 m terrain; use larger tiles for ridge layers (30-90 m) plus a detail/macro layer; terrain basemapDistance 450 (184).

(b) Dense vegetation (10-50x)
- Today: 165 trees in a 32 ha rectangle (5/ha) + 225 tufts (section 3). Target examples: 1,500-8,000 trees, ground cover by terrain details.
- Terrain trees: on `td` before `Terrain.CreateTerrainGameObject` (Zone4:175) set `td.treePrototypes` (prefab per species; the FBX can be used directly but a prefab with LODGroup LOD0/1/2 from the `_LOD1/_LOD2.fbx` files is better - none exist yet) and `td.SetTreeInstances(TreeInstance[], snapToHeightmap:true)` (positions normalised 0..1 = (x+320)/1024, (y-(-42))/180, (z+440)/1024). Trunk colliders come from a CapsuleCollider on the prototype prefab (TerrainCollider tree colliders); whether NavMeshSurface PhysicsColliders collection includes terrain-tree colliders is not verified.
- Terrain details (grass/ferns/small plants): `td.SetDetailResolution(N, 32)`, `td.detailPrototypes = {...}` with `usePrototypeMesh = true`, `useInstancing = true` (HDRP renders instanced detail meshes; materials need GPU instancing on), `td.SetDetailLayer(0,0,i,int[,])`. The Poly Haven grass FBXs are 17-21 separate sub-meshes (section 6), so combine each into one mesh asset first; grass_medium_01 (24.7k tris) is too heavy for detail use - prefer grass_bermuda_01 (941 tris) / grass_medium_02 / fern_02 LOD1 (2.8k).
- Or keep GameObjects: GPU Resident Drawer is ON in the active asset (`gpuResidentDrawerSettings.mode: 1`, Assets/Settings/HDRP High Fidelity.asset:526-531; `m_BrgStripping: 2` keep-all, ProjectSettings/GraphicsSettings.asset:58), so many MeshRenderers that share mesh+material are instanced via BRG; still needs LODGroups and shadow culling for 1000s of trees (not verified at runtime).
- Foliage materials must be fixed first (section 3.E): extract/override the embedded `M_<stem>` materials (or post-process them in an AssetPostprocessor `OnPostprocessMaterial`) to set `_AlphaCutoffEnable=1` + `_AlphaCutoff ~0.4-0.5` + `_ALPHATEST_ON` for RGBA leaf textures, double-sided (`_DoubleSidedEnable`), bind `<stem>_MaskMap.png`, Translucent material type with a foliage diffusion profile. Use the HDRP helpers `HDMaterial.SetAlphaClipping / SetAlphaCutoff / SetDiffusionProfile / ValidateMaterial` (HDRP package Runtime/Material/MaterialExtension.cs:495,504,513,369) so keywords/passes are rebuilt; the only similar code in the project is the chain-link material (WorldMaterialPalette.cs:287-319: `_AlphaCutoffEnable`, `_AlphaCutoff 0.4`, `_ALPHATEST_ON`, `_DoubleSidedEnable`).
- Exclusion data available inside Zone4 at scatter time: `FoliageExclusions` rects (Zone4:51-60) + `IsExcludedFromFoliage` (679-687); `ServiceRoad3Points` + `DistanceToRoad3` (31-36, 373-389); `EvaluateStreamX(z)` (371); `TallGrassPatches` (39-48); plateau rects (341-344); `td.GetSteepness(nx,nz)` and `EvaluateWorldElevation` for slope/height; `SampleTerrainY` (689-696). Zones 1-3 are already built when Zone4 runs (CretaceaWorldAssembler.cs:274-283), so their colliders can serve as a generic building/path mask with `Physics.CheckBox/CheckCapsule` (call `Physics.SyncTransforms()` first; the assembler only syncs at CretaceaWorldAssembler.cs:351, after Zone4) - exclude the terrain's own collider (same Environment layer). The NavMesh is baked after Zone4 (CretaceaWorldAssembler.cs:352) from PhysicsColliders on Default|Environment|Door (1262-1270), so it cannot be a placement mask during Zone4, but every trunk collider is automatically baked as a hole: drop the per-tree carving `NavMeshObstacle`s (Zone4:498-503, 523-527) when density goes up.
- Sampling region: widen `Mathf.Lerp(-160,528)/(-260,210)` (471-472) to the whole terrain and replace the `y > 36` reject (479) with a slope limit; raise the attempt budget (469) - it is 420 for 165 trees.

(c) Ridge -> jungle-covered volcanic hills
- `EvaluateWorldElevation` ring block (Zone4:316-325): today ring^2 * 105 * one Perlin (0.014) => smooth 43 deg domes. Replace with domain-warped fBm/ridged noise, a couple of volcanic cones/calderas, gullies, and gentler lower slopes (<= ~35 deg where trees must stand). Keep the eastern opening (328-338) and plateaus (341-344) - Zones 1-3 place props at hard-coded Y (e.g. Zone3_SubstationAndYardBuilder.cs:55,71,322) and do NOT sample the terrain.
- Extend the hills past the terrain edges or add an outer ring backdrop (Godot had 600-1300 m and 1200-2300 m ring meshes, zone_backdrop.gd:87-90) so the sheer edge at x -320 / z -440 / z 584 never shows.
- Height range: clamp is [-42, 136] (346) with origin -42 / range 180 (23, 26). Heightmap is 2 m/sample (513); 1025 would give 1 m (blueprint asked for 1025).

(d) HDRP Water (ocean + stream)
- Prerequisite: `supportWater: 1` in Assets/Settings/HDRP High Fidelity.asset:234 (and Balanced:236 / Performant:235); the Water frame setting must be on for cameras (HDRP global settings, not verified); WaterRendering override is already enabled in the default profile (env.txt).
- Ocean: replace Zone4:571-581 (grid mesh + MAT_DawnOcean) with a GO carrying `UnityEngine.Rendering.HighDefinition.WaterSurface` (`surfaceType = OceanSeaLake`, `geometryType = Infinite` (WaterSurface.cs:149,154), y -40, `foam = true`, tune `largeWindSpeed`, `refractionColor`, `absorptionDistance` (default 5 m), `scatteringColor`). It is lit by the scene and exposure-correct in every preset. Also deepen the seabed: Zone4:331 lerps to -42 = only 2 m under the water; lower it (and TerrainOriginY/HeightRange at 23/26 plus the clamp at 346) so depth colour and shore foam read, and add a sand band at the waterline.
- Stream: replace the 29 cubes (Zone4:402-417) with a `WaterSurface` `surfaceType = River`, `geometryType = Custom` + `meshRenderers` = a ribbon mesh following `EvaluateStreamX` (or several rotated Quad surfaces); keep invisible trigger boxes (Trigger layer + `SurfaceTag` Water, 409-416) for footsteps. Whether a sloped custom mesh is supported is not verified.
- If MAT_DawnOcean stays anywhere: `_LuminanceScale 2600` (MAT_DawnOcean.mat) is only right near EV 9-11; make the shader read HDRP lighting/sky/fog or drive `_LuminanceScale`, `_SunDirection`, sky colours from the active atmosphere preset; and never use it for the stream.
- Boat (Zone4:584-612) floats at y -39.4 static; with HDRP water it can sample height via the water search API (not set up). Its 85,000 cd / 140,000 cd lights are tuned for dawn exposure (EV 8.9).

## 8. Open questions / not verified

- Alpha clipping on the Poly Haven plant materials is inferred (surf=0 in materials.txt + HDRP FBX postprocessor logic), not looked at in a close-up. Quick check: select `grass_medium_01.fbx` > embedded `M_grass_medium_01` and read `_AlphaCutoffEnable`.
- Stream slabs glowing in night presets is calculated (MAT_DawnOcean x2600 x exposure 0.55 at EV 0.6; the shader also skips HDRP fog), not seen in a screenshot.
- coastal_cliff_01 headlands buried: calculated from the height function + REPORT.md size; the FBX pivot location was not checked.
- RNG replay assumes Unity's `System.Random(seed)` = the .NET legacy subtractive generator; terrain Y in the replay ignores Perlin, so y-band rejects may differ slightly.
- My splat/area numbers use a classic Perlin stand-in for `Mathf.PerlinNoise`; totals matched the live splat shares within 0.5 %.
- HDRP-specific: terrain tree billboards are not available in HDRP (use LODGroup prefabs); detail meshes need instancing-enabled materials; whether NavMeshSurface (PhysicsColliders) includes terrain-tree colliders; whether `WaterSurface` River accepts a sloped custom mesh; where the Water frame setting lives in this project's HDRP global settings - all not verified here.
- Titan Meadows lake hollow (-65, -210), r 55 m, 2.8 m deep (Zone4:309-313) has no water object anywhere in the builders.
- Unused but present: 5 HDRIs (no reference in any .asset/.unity/.mat/.prefab/.cs), backdrop textures, 12 plant and 5 rock models (section 6). Godot's island_tree_01/02/03 were not ported.
- Previous agent blueprint is stale on terrain details: it says heightmapResolution 1025 and "HDRP Water / animated wet-ripple shader" for the stream, plus `SG_FoliageWindTrample.shadergraph` / `FoliageTrampleDriver.cs` (prev_agent_unity_aaa_blueprint.md:102,106,248-249); the code uses 513, cubes with the ocean shader, and no wind/trample shader exists (Assets/Shaders has only 5 custom shaders; no WindZone anywhere).
