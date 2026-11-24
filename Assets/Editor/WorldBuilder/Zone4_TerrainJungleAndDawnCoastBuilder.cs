using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Builds Zone 4: the island terrain (main 1024 m tile + 8 neighbour tiles, all with colliders, NavMesh on the main tile only; heights inside
    /// the play area unchanged, volcanic jungle hills and a coast all round outside it), the 8-layer jungle splat
    /// (LookTerrainLayers), the HDRP Water sea / stream / lake (LookTerrainWater), the 7 tall-grass stealth patches,
    /// the jungle (LODGroup prefabs + seeded scatter + terrain-detail ground cover: LookVegetation*) and the dawn coast.
    /// Terrain layer order = PlayerController.terrainLayerSurfaces (footsteps).
    /// </summary>
    public static class Zone4_TerrainJungleAndDawnCoastBuilder
    {
        public const string TerrainFolder = "Assets/Art/Terrain";
        public const string TerrainDataAssetPath = TerrainFolder + "/CretaceaTerrain.asset";

        // Main terrain tile: X = -320..+704 (1024 m), Z = -440..+584 (1024 m), Y = -100..+280 (380 m: a seabed well
        // below the sea at Y -40 and the volcano). Eight neighbour tiles of the same size carry the rest of the island
        // and the seabed (TerrainCollider kept, NavMeshModifier ignoreFromBuild: the NavMesh covers the main tile alone).
        private const float TerrainOriginX = -320f;
        private const float TerrainOriginY = -100f;
        private const float TerrainOriginZ = -440f;
        private const float TerrainSizeXZ = 1024f;
        private const float TerrainHeightRange = 380f;
        private const int HeightmapRes = 513;
        private const int AlphamapRes = 512;

        // Service Road 3 Polyline (Unity X, Z) for Gravel splatmap painting & foliage exclusion
        private static readonly Vector2[] ServiceRoad3Points =
        {
            new(91f, -3.5f), new(120f, 0f), new(160f, 8f), new(200f, 4f),
            new(240f, 0f), new(250f, 0f), new(286f, 0f), new(310f, -8f),
            new(360f, -20f), new(401f, -22.8f)
        };

        // 7 Tyrant Valley Tall-Grass Stealth Concealment Patches (Unity X, Z, radius m)
        private static readonly (float x, float z, float radius)[] TallGrassPatches =
        {
            (140f, -16f, 17f),
            (152f, +26f, 14f),
            (226f, +32f, 15f),
            (232f, -24f, 13f),
            (320f, +10f, 18f),
            (352f, -44f, 15f),
            (118f, +20f, 10f)
        };

        // Foliage exclusion: real footprints + ~2 m and physics checks against zones 1-3 (LookVegetation.Footprints).

        public static GameObject Build(Transform worldRoot, WorldMaterialPalette palette)
        {
            var root = new GameObject("Zone_04_TerrainJungleAndDawnCoast");
            if (worldRoot != null) root.transform.SetParent(worldRoot, false);

            Terrain terrain = BuildUnifiedTerrain(root.transform, palette);
            // invisible ring through the jungle outside the play area (the outer hills are walkable): blocks the player
            // and cuts the baked dinosaur NavMesh off from the outer island
            Debug.Log("[Zone4] " + LookTerrainBounds.Build(root.transform));
            BuildTyrantValleyStreamAndGrassPatches(root.transform, terrain, palette);
            // the coast road and rails exist before the jungle is scattered, so plants avoid them too
            BuildDawnCoastAndOceanVista(root.transform, palette);
            BuildExclusionGuardedRainforest(root.transform, terrain, palette);

            return root;
        }

        // =========================================================================
        // UNIFIED 1024x1024 SCULPTED UNITY TERRAIN + 4 PBR TERRAIN LAYERS
        // =========================================================================

        /// <summary>All terrain tiles of the last build: [0] is the main (collider, NavMesh) tile, then the 8 neighbours.</summary>
        internal static readonly List<Terrain> TerrainTiles = new List<Terrain>();

        private static Terrain BuildUnifiedTerrain(Transform parent, WorldMaterialPalette p)
        {
            WorldMaterialPalette.EnsureFolder(TerrainFolder);
            TerrainTiles.Clear();
            TerrainLayer[] layers = LookTerrainLayers.BuildLayers(TerrainFolder);
            Material tMat = EnsureTerrainMaterial();

            Terrain main = BuildTerrainTile(parent, 0, 0, layers, tMat);
            main.gameObject.AddComponent<Cretacea.Rendering.TerrainWetnessDriver>();   // rain-wet ground at runtime (all tiles)
            TerrainTiles.Add(main);
            var grid = new Terrain[3, 3];
            grid[1, 1] = main;
            for (int gz = -1; gz <= 1; gz++)
            {
                for (int gx = -1; gx <= 1; gx++)
                {
                    if (gx == 0 && gz == 0) continue;
                    Terrain t = BuildTerrainTile(parent, gx, gz, layers, tMat);
                    grid[gx + 1, gz + 1] = t;
                    TerrainTiles.Add(t);
                }
            }
            for (int gz = 0; gz < 3; gz++)
            {
                for (int gx = 0; gx < 3; gx++)
                {
                    Terrain left = gx > 0 ? grid[gx - 1, gz] : null;
                    Terrain right = gx < 2 ? grid[gx + 1, gz] : null;
                    Terrain top = gz < 2 ? grid[gx, gz + 1] : null;
                    Terrain bottom = gz > 0 ? grid[gx, gz - 1] : null;
                    grid[gx, gz].SetNeighbors(left, top, right, bottom);
                }
            }
            return main;
        }

        private static Material EnsureTerrainMaterial()
        {
            Shader hdrpTerrainLit = Shader.Find("HDRP/TerrainLit");
            if (hdrpTerrainLit == null) return null;
            const string matPath = TerrainFolder + "/MAT_CretaceaTerrain.mat";
            Material tMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (tMat == null)
            {
                tMat = new Material(hdrpTerrainLit) { name = "MAT_CretaceaTerrain" };
                AssetDatabase.CreateAsset(tMat, matPath);
            }
            // 8 layers: TerrainLit height blending only works with 4, so it stays off
            tMat.SetFloat("_EnableHeightBlend", 0f);
            tMat.DisableKeyword("_TERRAIN_BLEND_HEIGHT");
            tMat.enableInstancing = true;
            EditorUtility.SetDirty(tMat);
            return tMat;
        }

        /// <summary>One 1024 m tile; (gx, gz) = (0, 0) is the main play tile, the others are its neighbours (collider, no NavMesh).</summary>
        private static Terrain BuildTerrainTile(Transform parent, int gx, int gz, TerrainLayer[] layers, Material tMat)
        {
            bool isMain = gx == 0 && gz == 0;
            string assetPath = isMain ? TerrainDataAssetPath : $"{TerrainFolder}/CretaceaTerrain_{TileSuffix(gx, gz)}.asset";
            TerrainData td = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
            if (td == null)
            {
                td = new TerrainData();
                AssetDatabase.CreateAsset(td, assetPath);
            }
            float ox = TerrainOriginX + gx * TerrainSizeXZ;
            float oz = TerrainOriginZ + gz * TerrainSizeXZ;

            td.heightmapResolution = HeightmapRes;
            td.alphamapResolution = AlphamapRes;
            td.baseMapResolution = 1024;
            td.size = new Vector3(TerrainSizeXZ, TerrainHeightRange, TerrainSizeXZ);
            td.terrainLayers = layers;

            float[,] heights = new float[HeightmapRes, HeightmapRes];
            for (int iz = 0; iz < HeightmapRes; iz++)
            {
                float worldZ = oz + (iz / (float)(HeightmapRes - 1)) * TerrainSizeXZ;
                for (int ix = 0; ix < HeightmapRes; ix++)
                {
                    float worldX = ox + (ix / (float)(HeightmapRes - 1)) * TerrainSizeXZ;
                    float worldY = EvaluateWorldElevation(worldX, worldZ);
                    heights[iz, ix] = Mathf.Clamp01((worldY - TerrainOriginY) / TerrainHeightRange);
                }
            }
            td.SetHeights(0, 0, heights);

            float[,,] alpha = PaintJungleSplat(td, ox, oz, TerrainSizeXZ, AlphamapRes, structureMask: isMain);
            td.SetAlphamaps(0, 0, alpha);
            EditorUtility.SetDirty(td);

            GameObject terrainGo = Terrain.CreateTerrainGameObject(td);
            terrainGo.name = isMain ? "IslaTormenta_UnifiedTerrain" : $"IslaTormenta_OuterTerrain_{TileSuffix(gx, gz)}";
            terrainGo.transform.SetParent(parent, false);
            terrainGo.transform.position = new Vector3(ox, TerrainOriginY, oz);
            terrainGo.layer = GameLayers.Environment;
            if (!isMain)
            {
                // solid ground everywhere (nothing falls through), but left out of the NavMesh bake: the dinosaur
                // NavMesh stays on the main tile
                var mod = terrainGo.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
                mod.ignoreFromBuild = true;
            }

            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = isMain ? 4f : 6f;
            terrain.basemapDistance = 600f;
            terrain.groupingID = 0;
            terrain.allowAutoConnect = true;
            if (tMat != null) terrain.materialTemplate = tMat;
            return terrain;
        }

        private static string TileSuffix(int gx, int gz)
        {
            string ns = gz > 0 ? "N" : (gz < 0 ? "S" : "");
            string ew = gx > 0 ? "E" : (gx < 0 ? "W" : "");
            return ns + ew;
        }

        /// <summary>Titan Meadows lake: water level and the centre of the hollow it fills.</summary>
        internal const float LakeLevel = -7.65f;      // the meadow_sauropod_b anchor (-95, -185) stands at -7.59: on dry ground
        internal static readonly Vector2 LakeCentre = new Vector2(-62f, -205f);

        /// <summary>Play area that zones 1-3 built by hand (heights there are frozen).</summary>
        internal const float PlayMinX = -125f, PlayMaxX = 560f, PlayMinZ = -215f, PlayMaxZ = 195f;

        internal static float DistanceOutsidePlayArea(float x, float z)
        {
            float dx = x < PlayMinX ? PlayMinX - x : (x > PlayMaxX ? x - PlayMaxX : 0f);
            float dz = z < PlayMinZ ? PlayMinZ - z : (z > PlayMaxZ ? z - PlayMaxZ : 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Main tile, alphamap resolution: 1 = a zone 1-3 collider within ~1 m, 0.55 = within ~2.6 m.</summary>
        internal static float[,] MainStructureMap;

        private static float[,,] PaintJungleSplat(TerrainData td, float originX, float originZ, float size, int res, bool structureMask)
        {
            float[,,] alpha = new float[res, res, LookTerrainLayers.LayerCount];
            float[] w = new float[LookTerrainLayers.LayerCount];
            if (structureMask) MainStructureMap = new float[res, res];
            int structMask = GameLayers.Mask(GameLayers.Default) | GameLayers.Mask(GameLayers.Environment) | GameLayers.Mask(GameLayers.Door);
            if (structureMask) Physics.SyncTransforms();
            var hits = new Collider[8];
            for (int iz = 0; iz < res; iz++)
            {
                float normZ = iz / (float)(res - 1);
                float worldZ = originZ + normZ * size;
                for (int ix = 0; ix < res; ix++)
                {
                    float normX = ix / (float)(res - 1);
                    float worldX = originX + normX * size;
                    float worldY = EvaluateWorldElevation(worldX, worldZ);
                    float outside = DistanceOutsidePlayArea(worldX, worldZ);

                    var t = new LookTerrainLayers.Texel
                    {
                        X = worldX, Z = worldZ, Y = worldY,
                        Slope = td.GetSteepness(normX, normZ),
                        PlayArea = 1f - SmoothStepRange(0f, 60f, outside)
                    };

                    if (worldX > 155f && worldX < 255f && Mathf.Abs(worldZ) < 200f)
                    {
                        float streamDist = Mathf.Abs(worldX - EvaluateStreamX(worldZ));
                        t.StreamBank = 1f - SmoothStepRange(5.5f, 9.5f, streamDist + (Mathf.PerlinNoise(worldX * 0.2f, worldZ * 0.2f) - 0.5f) * 2.5f);
                    }

                    bool insideSubPad = worldX >= 249.5f && worldX <= 286.5f && Mathf.Abs(worldZ) <= 17.5f;
                    if (!insideSubPad && worldX >= 86f && worldX <= 406f)
                    {
                        float roadDist = DistanceToRoad3(worldX, worldZ);
                        t.RoadGravel = 1f - SmoothStepRange(2.4f, 4.6f, roadDist + (Mathf.PerlinNoise(worldX * 0.35f, worldZ * 0.35f) - 0.5f) * 0.9f);
                    }

                    float lakeD = Vector2.Distance(new Vector2(worldX, worldZ), LakeCentre);
                    if (lakeD < 75f) t.LakeShore = 1f - SmoothStepRange(0.2f, 0.9f, Mathf.Abs(worldY - LakeLevel - 0.2f));

                    for (int g = 0; g < TallGrassPatches.Length; g++)
                    {
                        var patch = TallGrassPatches[g];
                        float d = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(patch.x, patch.z));
                        t.TallGrass = Mathf.Max(t.TallGrass, 1f - SmoothStepRange(patch.radius - 3f, patch.radius + 2f, d));
                    }

                    // worn ground around anything zones 1-3 built (slabs, walls, fences, bollards)
                    if (structureMask && outside < 1f)
                    {
                        Vector3 c = new Vector3(worldX, worldY + 0.55f, worldZ);
                        if (Physics.OverlapBoxNonAlloc(c, new Vector3(1.1f, 0.6f, 1.1f), hits, Quaternion.identity, structMask, QueryTriggerInteraction.Ignore) > 0) t.NearStructure = 1f;
                        else if (Physics.OverlapBoxNonAlloc(c, new Vector3(2.6f, 0.6f, 2.6f), hits, Quaternion.identity, structMask, QueryTriggerInteraction.Ignore) > 0) t.NearStructure = 0.55f;
                    }

                    if (structureMask) MainStructureMap[iz, ix] = t.NearStructure;
                    LookTerrainLayers.Paint(in t, w);
                    float sum = 0f;
                    for (int l = 0; l < w.Length; l++) sum += w[l];
                    if (sum < 1e-4f) { w[LookTerrainLayers.Grass] = 1f; sum = 1f; }
                    for (int l = 0; l < w.Length; l++) alpha[iz, ix, l] = w[l] / sum;
                }
            }
            return alpha;
        }

        /// <summary>
        /// Continuous C1-smooth Isla Tormenta elevation function:
        /// - Cradles Visitor Center / Plaza / Paths at Y = -0.04 m
        /// - Rolls smoothly through Titan Meadows (Y ~ -6.2 m) & Tyrant Valley (Y ~ -1.6..+2.1 m) with carved stream
        /// - Elevates Substation 3 pad cleanly to Y = 2.26 m & Old Yard to Y = 0.96 m
        /// - Frames North, West, South with dramatic 60-115 m volcanic mountain ridges
        /// - OPENS WIDE TO THE EAST (X > 546, Z: -240..+160) dropping smoothly to the ocean seabed (Y = -42 m)!
        /// </summary>
        public static float EvaluateWorldElevation(float x, float z)
        {
            // Inside the play area this is EXACTLY the elevation zones 1-3 were built against; outside it blends
            // over 70 m into the volcanic island (LookTerrainShape).
            float outside = DistanceOutsidePlayArea(x, z);
            float legacy = outside < 70f ? EvaluateLegacyElevation(x, z) : 0f;
            if (outside <= 0f) return legacy;
            float outer = LookTerrainShape.OuterElevation(x, z, outside);
            float y = Mathf.Lerp(legacy, outer, SmoothStepRange(0f, 70f, outside));
            return Mathf.Clamp(y, TerrainOriginY + 1f, TerrainOriginY + TerrainHeightRange - 2f);
        }

        /// <summary>The original Zone 4 elevation (unchanged; only used inside and next to the play area).</summary>
        private static float EvaluateLegacyElevation(float x, float z)
        {
            // Base natural rolling valley undulation
            float gentleRoll = (Mathf.PerlinNoise((x + 500f) * 0.0085f, (z + 500f) * 0.0085f) - 0.48f) * 3.6f;
            float detail = (Mathf.PerlinNoise((x + 120f) * 0.032f, (z + 120f) * 0.032f) - 0.5f) * 0.65f;
            float y = 0.6f + gentleRoll + detail;

            // Meandering Tyrant Valley stream trough (X: 165..245)
            if (x > 160f && x < 248f && Mathf.Abs(z) < 190f)
            {
                float sDist = Mathf.Abs(x - EvaluateStreamX(z));
                float cut = (1f - SmoothStepRange(3.5f, 15.0f, sDist)) * 2.5f;
                y -= cut;
            }

            // Titan Meadows south basin (Opening Cutscene vista, Z < -102, X < 160)
            if (z < -96f && x < 180f)
            {
                float meadowBlend = SmoothStepRange(-96f, -135f, z) * (1f - SmoothStepRange(110f, 180f, x));
                y = Mathf.Lerp(y, -6.2f + gentleRoll * 0.6f, meadowBlend);
                // Scenic lake hollow in Titan Meadows
                float lakeDist = Vector2.Distance(new Vector2(x, z), new Vector2(-65f, -210f));
                // round 5 (r4 critic: a smooth ellipse with an even ring read as a crater from the air): a ragged shore
                // of bays and spits. It only ever moves the shore inwards (raises the bowl), so the ground round the lake,
                // the sauropod anchor at (-95, -185) included, stays as dry as before.
                float lakeAng = Mathf.Atan2(z + 210f, x + 65f);
                // (fix round, r5 critic 'still an ellipse from the air': larger, lower-frequency bays, x20 -> x35, 0.055 -> 0.03)
                float rag = Mathf.PerlinNoise(x * 0.03f + 3.1f, z * 0.03f + 7.7f) * 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(lakeAng * 3f + 1.3f));
                lakeDist += Mathf.Max(0f, rag - 0.38f) * 35f;
                if (lakeDist < 55f)
                {
                    y -= (1f - SmoothStepRange(25f, 55f, lakeDist)) * 2.8f;
                }
            }

            // Surrounding Volcanic Mountain Ring (North, West, South — STRICTLY OPEN TO THE EAST X > 520, Z: -210..+120!)
            float northWall = SmoothStepRange(175f, 340f, z);
            float westWall = SmoothStepRange(-155f, -295f, x);
            float southWall = SmoothStepRange(-255f, -410f, z);
            float ring = Mathf.Clamp01(Mathf.Max(northWall, Mathf.Max(westWall, southWall)));
            if (ring > 0.001f)
            {
                float peakNoise = 0.55f + 0.75f * Mathf.PerlinNoise(x * 0.014f + 4.1f, z * 0.014f + 1.9f);
                y += ring * ring * 105f * peakNoise;
            }

            // Dramatic flanking coastal headlands far North-East & South-East while keeping the Eastern Ocean Vista 100% OPEN!
            if (x > 520f)
            {
                float eastDrop = SmoothStepRange(546f, 625f, x);
                float coastalBluffY = Mathf.Lerp(1.0f, -42.0f, eastDrop);

                // Keep central dawn ocean corridor (Z: -130..+40) completely unobstructed!
                float headlandN = SmoothStepRange(45f, 160f, z);
                float headlandS = SmoothStepRange(-145f, -260f, z);
                float headland = Mathf.Max(headlandN, headlandS) * (1f - SmoothStepRange(610f, 680f, x));
                y = Mathf.Lerp(y, coastalBluffY, SmoothStepRange(520f, 550f, x)) + headland * 38f;
            }

            // Smooth architectural plateaus cradling every built zone cleanly (zero clipping, zero floating slabs!)
            y = BlendPlateau(y, x, z, -104f, 92f, -96f, 130f, -0.05f, margin: 18f);      // Visitor Center + Plaza + Paths + Talon
            y = BlendPlateau(y, x, z, 248f, 288f, -19f, 19f, 2.26f, margin: 20f);         // Substation 3 Pad (Y = 2.3)
            y = BlendPlateau(y, x, z, 398f, 508f, -97f, 52f, 0.96f, margin: 18f);         // Old Maintenance Yard (Y = 1.0)
            y = BlendPlateau(y, x, z, 505f, 547f, -51f, -41f, 0.96f, margin: 8f);         // Coast Road Overlook (Y = 1.0)

            return Mathf.Clamp(y, -42f, 136f);
        }

        /// <summary>
        /// GLSL-style smoothstep(edge0, edge1, x): maps x cleanly from [edge0..edge1] -> [0..1]
        /// (supports both ascending and descending edges!). Fixes P0-1 Unity Mathf.SmoothStep range misuse.
        /// </summary>
        private static float SmoothStepRange(float edge0, float edge1, float x)
        {
            float denom = edge1 - edge0;
            if (Mathf.Abs(denom) < 1e-5f) return x >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((x - edge0) / denom);
            return t * t * (3f - 2f * t);
        }

        private static float BlendPlateau(float currentY, float x, float z, float xMin, float xMax, float zMin, float zMax, float targetY, float margin)
        {
            float dx = x < xMin ? (xMin - x) : (x > xMax ? (x - xMax) : 0f);
            float dz = z < zMin ? (zMin - z) : (z > zMax ? (z - zMax) : 0f);
            float dist = Mathf.Sqrt(dx * dx + dz * dz);
            if (dist >= margin) return currentY;
            float blend = 1f - SmoothStepRange(0f, margin, dist);
            return Mathf.Lerp(currentY, targetY, blend);
        }

        public static float EvaluateStreamX(float z) => 205f + 18f * Mathf.Sin(-z / 45f) + 6f * Mathf.Sin(-z / 13f);

        internal static float DistanceToRoad3Internal(float x, float z) => DistanceToRoad3(x, z);

        private static float DistanceToRoad3(float x, float z)
        {
            Vector2 pt = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i < ServiceRoad3Points.Length - 1; i++)
            {
                // Skip Substation concrete pad gap (segment 5 -> 6)
                if (i == 5) continue;
                Vector2 a = ServiceRoad3Points[i];
                Vector2 b = ServiceRoad3Points[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                float d = Vector2.Distance(pt, a + ab * t);
                if (d < best) best = d;
            }
            return best;
        }

        // =========================================================================
        // TYRANT VALLEY STREAM (WATER FOOTSTEPS!) & 7 TALL-GRASS STEALTH PATCHES
        // =========================================================================

        private static void BuildTyrantValleyStreamAndGrassPatches(Transform parent, Terrain terrain, WorldMaterialPalette p)
        {
            var hydro = new GameObject("TyrantValley_StreamAndStealthGrass");
            hydro.transform.SetParent(parent, false);

            // Shallow Stream Ribbon running across Tyrant Valley (Z = -160 .. +160) with Water trigger for footsteps!
            Material streamMat = p.GlassArchitectural;   // renderer removed below; the box only carries the trigger
            for (int i = -14; i <= 14; i++)
            {
                float z0 = i * 11f;
                float x0 = EvaluateStreamX(z0);
                float y0 = SampleTerrainY(terrain, x0, z0) + 0.22f;

                GameObject waterStrip = WorldArchitectureKit.CreateBox(hydro.transform, $"StreamWater_{i + 14}", new Vector3(x0, y0, z0), new Vector3(9.0f, 0.12f, 12.2f), streamMat, addCollider: true);
                waterStrip.layer = GameLayers.Trigger;
                BoxCollider bc = waterStrip.GetComponent<BoxCollider>();
                if (bc != null)
                {
                    bc.isTrigger = true;
                    bc.size = new Vector3(1f, 14f, 1f);
                }
                WorldArchitectureKit.ApplySurface(waterStrip, SurfaceType.Water, p);
                // footstep trigger only: the visible water is the River surface below
                var stripRenderer = waterStrip.GetComponent<MeshRenderer>();
                if (stripRenderer != null) Object.DestroyImmediate(stripRenderer);
                var stripFilter = waterStrip.GetComponent<MeshFilter>();
                if (stripFilter != null) Object.DestroyImmediate(stripFilter);
            }
            LookTerrainWater.BuildStream(hydro.transform, -176f, 176f);
            LookTerrainWater.BuildLake(hydro.transform, LakeLevel);

            // 7 Tall-Grass Concealment Patches (TallGrassZoneVolume + Instanced Poly Haven Grass/Ferns!)
            for (int i = 0; i < TallGrassPatches.Length; i++)
            {
                var patch = TallGrassPatches[i];
                float cy = SampleTerrainY(terrain, patch.x, patch.z);

                var patchGo = new GameObject($"TallGrassPatch_{i + 1}");
                patchGo.transform.SetParent(hydro.transform, false);
                patchGo.transform.position = new Vector3(patch.x, cy + 0.7f, patch.z);
                patchGo.layer = GameLayers.Trigger;

                SphereCollider sc = patchGo.AddComponent<SphereCollider>();
                sc.isTrigger = true;
                sc.radius = patch.radius;
                patchGo.AddComponent<TallGrassZoneVolume>();

                // the visible tall grass is painted as terrain details (LookVegetationDetails)
            }
        }

        // =========================================================================
        // EXCLUSION-GUARDED TROPICAL RAINFOREST (TRUNK COLLIDERS + NAVMESH CARVE!)
        // =========================================================================

        private static void BuildExclusionGuardedRainforest(Transform parent, Terrain terrain, WorldMaterialPalette p)
        {
            var forest = new GameObject("IslaTormenta_TropicalRainforest");
            forest.transform.SetParent(parent, false);

            // LODGroup prefabs from the LOD0/LOD1/LOD2 FBX files, then the seeded jungle scatter (GameObjects in the play
            // area with trunk colliders, terrain trees everywhere else). The NavMesh is baked from colliders afterwards,
            // so there are no carving NavMeshObstacles.
            Debug.Log("[Zone4] " + LookVegetationPrefabs.BuildAll());
            Debug.Log("[Zone4] " + LookVegetation.Scatter(forest.transform, terrain, TerrainTiles, TallGrassPatches));
            Debug.Log("[Zone4] " + LookVegetationDetails.Paint(terrain, MainStructureMap, TallGrassPatches));

            // Opening Cutscene Titan Meadows Herbivore Anchors
            WorldArchitectureKit.BuildWorldAnchor(forest.transform, "meadow_sauropod_a", new Vector3(-40f, SampleTerrainY(terrain, -40f, -150f), -150f));
            WorldArchitectureKit.BuildWorldAnchor(forest.transform, "meadow_sauropod_b", new Vector3(-95f, SampleTerrainY(terrain, -95f, -185f), -185f));
            WorldArchitectureKit.BuildWorldAnchor(forest.transform, "meadow_trike", new Vector3(-18f, SampleTerrainY(terrain, -18f, -142f), -142f));
        }

        // =========================================================================
        // COAST ROAD & UNOBSTRUCTED DAWN OCEAN SUNRISE VISTA (X: 505 .. 3000)
        // =========================================================================

        private static void BuildDawnCoastAndOceanVista(Transform parent, WorldMaterialPalette p)
        {
            var coast = new GameObject("DawnCoast_And_OceanVista");
            coast.transform.SetParent(parent, false);
            const float roadY = 1.0f;

            // Rain-slick Fenced Coast Road running East from Service Exit E-0 (X: 505..547, Z: -49.8..-42.2)
            GameObject road = WorldArchitectureKit.CreateBox(coast.transform, "CoastRoad_Asphalt", new Vector3(526f, roadY - 0.18f, -46f), new Vector3(42f, 0.36f, 7.6f), p.AsphaltWet, addCollider: true);
            WorldArchitectureKit.ApplySurface(road, SurfaceType.Concrete, p);
            WorldArchitectureKit.BuildChainlinkFenceSpan(coast.transform, "CoastRoad_FenceN", new Vector3(505f, 0f, -42.1f), new Vector3(546f, 0f, -42.1f), roadY, 2.2f, p, addDinoBlocker: false);
            WorldArchitectureKit.BuildChainlinkFenceSpan(coast.transform, "CoastRoad_FenceS", new Vector3(505f, 0f, -49.9f), new Vector3(546f, 0f, -49.9f), roadY, 2.2f, p, addDinoBlocker: false);

            // Coastal Cliff Overlook Timber & Steel Rail at X = 546.5 (unobstructed sea view straight ahead +X!)
            // the top-rail box stays as the invisible collider that stops the player at the cliff edge; the visible rail is
            // a galvanised pipe guard rail on bolted base plates with a green route marker lamp (LookTerrainCoast)
            GameObject railCollider = WorldArchitectureKit.CreateBox(coast.transform, "Overlook_TopRail", new Vector3(546.5f, roadY + 1.05f, -46f), new Vector3(0.14f, 0.10f, 8.0f), p.WoodPlanks, addCollider: true);
            Object.DestroyImmediate(railCollider.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(railCollider.GetComponent<MeshFilter>());
            LookTerrainCoast.BuildOverlookRail(coast.transform, p, 546.5f, roadY, -49.9f, -42.1f);

            // Dawn Ending Anchors & Trigger (including elevated crane-shot peak clearing Old Yard east wall!)
            WorldArchitectureKit.BuildWorldAnchor(coast.transform, "coast", new Vector3(543.0f, roadY, -46.0f), yawDeg: 90f);
            WorldArchitectureKit.BuildWorldAnchor(coast.transform, "ending_view", new Vector3(543.0f, roadY + 0.05f, -46.0f), yawDeg: 90f);
            WorldArchitectureKit.BuildWorldAnchor(coast.transform, "dawn_coast", new Vector3(543.0f, roadY + 0.05f, -46.0f), yawDeg: 90f);
            WorldArchitectureKit.BuildWorldAnchor(coast.transform, "dawn_crane_peak", new Vector3(536.0f, roadY + 14.2f, -42.0f), yawDeg: -85f);
            WorldArchitectureKit.BuildStoryTrigger(coast.transform, "coast_ending", new Vector3(536.5f, roadY + 2.5f, -46.0f), new Vector3(4.5f, 5.0f, 12.0f), oneShot: true);

            // (the headland rock shelves and waterline boulders are placed on the real shoreline by LookVegetation)

            // The sea: HDRP Water System ocean (lit by the scene, correct in every preset)
            LookTerrainWater.BuildSea(coast.transform);

            // The Coast Guard rescue cutter out on the sunlit eastern bay (the last shot of the game): a lofted 25 m hull
            // with the racing stripe, deckhouse, wheelhouse, mast and radar, rails, tender and wake (LookTerrainCoast),
            // running across the ending camera's view towards the island
            var evac = new GameObject("DistantEvacBeacon_Bay");
            evac.transform.SetParent(coast.transform, false);
            evac.transform.SetPositionAndRotation(LookTerrainCoast.CutterPosition + Vector3.up * LookTerrainShape.SeaLevel, Quaternion.Euler(0f, LookTerrainCoast.CutterYaw, 0f));
            var (mastLocal, searchLocal) = LookTerrainCoast.BuildCutter(evac.transform, p);
            // the low dawn sun stands behind the cutter: its 60 m shadow lay on the sea towards the camera as a hard
            // black band (HDRP water darkens its scattering in shadow). At 230 m nobody misses the boat's own shadow.
            foreach (Renderer r in evac.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Masthead warm rescue strobe + sweeping forward searchlight beam aimed back toward Isla Tormenta!
            Vector3 mastWorld = evac.transform.TransformPoint(mastLocal);
            Vector3 searchOrigin = evac.transform.TransformPoint(searchLocal);
            Vector3 searchDir = (new Vector3(548f, 4f, -46f) - searchOrigin).normalized;     // the beam crosses the end of the coast road
            WorldArchitectureKit.BuildHdrpLight(evac.transform, "Light_EvacBeacon_White", mastWorld, Vector3.zero, LightType.Point, new Color(1.0f, 0.90f, 0.68f), 85000f, 260f, LightGroupId.VCExit, volumetricDimmer: 1.5f, pulsingEmergency: true);
            WorldArchitectureKit.BuildHdrpLight(evac.transform, "Light_EvacSearchlight", searchOrigin, Quaternion.LookRotation(searchDir).eulerAngles, LightType.Spot, new Color(1.0f, 0.95f, 0.84f), 140000f, 260f, LightGroupId.VCExit, volumetricDimmer: 1.8f, spotOuterAngle: 28f);
        }

        private static float SampleTerrainY(Terrain terrain, float x, float z)
        {
            if (terrain != null && terrain.terrainData != null)
            {
                return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            }
            return EvaluateWorldElevation(x, z);
        }
    }
}
