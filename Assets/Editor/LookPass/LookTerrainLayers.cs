using UnityEditor;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Terrain look: the eight jungle terrain layers and the rule-based splat painter.
    ///
    /// Layer order (PlayerController.terrainLayerSurfaces must follow it for footstep sounds):
    ///   0 Mud, 1 Grass (tropical green), 2 Gravel, 3 RockyCliff (basalt), 4 JungleFloor (leaf litter),
    ///   5 MossyRock, 6 Moss, 7 Sand.
    /// Textures are bound by exact file name; the DRY mask maps come from Assets/Art/Textures/terrain_jungle
    /// (derived by Assets/Art/Terrain/Source~/derive_terrain_textures.py: G = AO, B = height, A = smoothness).
    /// </summary>
    public static class LookTerrainLayers
    {
        public const int Mud = 0, Grass = 1, Gravel = 2, Rock = 3, Floor = 4, MossyRock = 5, Moss = 6, Sand = 7;
        public const int LayerCount = 8;

        private const string Tex = "Assets/Art/Textures";
        private const string Derived = Tex + "/terrain_jungle";

        private struct LayerDef
        {
            public string Name, BaseColor, Normal, Mask;
            public float Tile;
            public Color Tint;      // TerrainLayer.diffuseRemapMax (TerrainLit "Color Tint")
            public float NormalScale;
        }

        private static readonly LayerDef[] Defs =
        {
            new LayerDef { Name = "TL_Mud", BaseColor = Derived + "/tj_mud_BaseColor.png", Normal = Tex + "/mud/mud_Normal.png", Mask = Derived + "/tj_mud_MaskMap.png", Tile = 7f, Tint = new Color(0.92f, 0.90f, 0.86f), NormalScale = 1f },
            new LayerDef { Name = "TL_Grass", BaseColor = Derived + "/tj_grass_BaseColor.png", Normal = Tex + "/grass_ground/grass_ground_Normal.png", Mask = Derived + "/tj_grass_MaskMap.png", Tile = 7.5f, Tint = new Color(0.74f, 1.0f, 0.94f), NormalScale = 1f },   // review 2: hue 60 deg (R = G) read khaki; fix round: less saturated, hue 90-100
            new LayerDef { Name = "TL_Gravel", BaseColor = Derived + "/tj_gravel_BaseColor.png", Normal = Tex + "/gravel_road/gravel_road_Normal.png", Mask = Derived + "/tj_gravel_MaskMap.png", Tile = 5f, Tint = new Color(0.95f, 0.93f, 0.90f), NormalScale = 1f },
            new LayerDef { Name = "TL_RockyCliff", BaseColor = Derived + "/tj_basalt_BaseColor.png", Normal = Tex + "/rock_cliff/rock_cliff_Normal.png", Mask = Derived + "/tj_basalt_MaskMap.png", Tile = 15f, Tint = Color.white, NormalScale = 1.2f },
            new LayerDef { Name = "TL_JungleFloor", BaseColor = Derived + "/tj_floor_BaseColor.png", Normal = Tex + "/forest_floor/forest_floor_Normal.png", Mask = Derived + "/tj_floor_MaskMap.png", Tile = 5f, Tint = new Color(0.78f, 0.80f, 0.74f), NormalScale = 1f },
            new LayerDef { Name = "TL_MossyRock", BaseColor = Derived + "/tj_mossyrock_BaseColor.png", Normal = Tex + "/rock_mossy/rock_mossy_Normal.png", Mask = Derived + "/tj_mossyrock_MaskMap.png", Tile = 9f, Tint = new Color(0.58f, 0.63f, 0.52f), NormalScale = 1.1f },
            new LayerDef { Name = "TL_Moss", BaseColor = Derived + "/tj_moss_BaseColor.png", Normal = Tex + "/moss_ground/moss_ground_Normal.png", Mask = Derived + "/tj_moss_MaskMap.png", Tile = 6f, Tint = new Color(0.74f, 1.0f, 0.94f), NormalScale = 1f },
            new LayerDef { Name = "TL_Sand", BaseColor = Derived + "/tj_sand_BaseColor.png", Normal = Tex + "/mud/mud_Normal.png", Mask = Derived + "/tj_sand_MaskMap.png", Tile = 11f, Tint = Color.white, NormalScale = 0.6f },
        };

        /// <summary>Footstep surface for each layer index (kept in step with PlayerController.terrainLayerSurfaces).</summary>
        public static readonly SurfaceType[] Surfaces =
        {
            SurfaceType.Mud, SurfaceType.Grass, SurfaceType.Gravel, SurfaceType.Concrete,
            SurfaceType.Grass, SurfaceType.Concrete, SurfaceType.Grass, SurfaceType.Gravel
        };

        public static TerrainLayer[] BuildLayers(string folder)
        {
            var layers = new TerrainLayer[Defs.Length];
            for (int i = 0; i < Defs.Length; i++)
            {
                LayerDef d = Defs[i];
                string path = $"{folder}/{d.Name}.terrainlayer";
                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null)
                {
                    layer = new TerrainLayer { name = d.Name };
                    AssetDatabase.CreateAsset(layer, path);
                }
                layer.diffuseTexture = Load(d.BaseColor);
                layer.normalMapTexture = Load(d.Normal);
                layer.maskMapTexture = Load(d.Mask);
                layer.normalScale = d.NormalScale;
                layer.tileSize = new Vector2(d.Tile, d.Tile);
                layer.tileOffset = new Vector2(i * 1.37f, i * 2.71f);      // de-align the tiling grids of the layers
                layer.metallic = 0f;
                layer.smoothness = 0.3f;
                layer.diffuseRemapMin = Vector4.zero;
                layer.diffuseRemapMax = new Vector4(d.Tint.r, d.Tint.g, d.Tint.b, 1f);
                layer.maskMapRemapMin = Vector4.zero;
                layer.maskMapRemapMax = Vector4.one;
                EditorUtility.SetDirty(layer);
                layers[i] = layer;
            }
            return layers;
        }

        private static Texture2D Load(string path)
        {
            var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (t == null) Debug.LogWarning("[Terrain] missing texture " + path);
            return t;
        }

        // ------------------------------------------------------------------------------------------------------------
        // SPLAT
        // ------------------------------------------------------------------------------------------------------------

        /// <summary>Everything the painter needs to know about one alphamap texel.</summary>
        public struct Texel
        {
            public float X, Z, Y, Slope;
            public float NearStructure;     // 0..1, 1 = right next to a wall / slab / fence (worn ground)
            public float RoadGravel;        // 0..1 Service Road 3
            public float StreamBank;        // 0..1 stream banks
            public float TallGrass;         // 0..1 inside a stealth tall-grass patch
            public float PlayArea;          // 0..1, 1 inside the hand-built play area
            public float LakeShore;         // 0..1 at the Titan Meadows lake waterline
        }

        private static float N(float x, float z, float scale, float ox, float oz) => Mathf.PerlinNoise(x * scale + ox, z * scale + oz);

        private static float Fbm(float x, float z, float scale, float ox, float oz)
        {
            return N(x, z, scale, ox, oz) * 0.55f + N(x, z, scale * 2.13f, ox + 17.3f, oz + 3.1f) * 0.30f + N(x, z, scale * 4.7f, ox + 5.9f, oz + 41.7f) * 0.15f;
        }

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>Paints one texel into w[0..7] (not normalised).</summary>
        public static void Paint(in Texel t, float[] w)
        {
            for (int i = 0; i < LayerCount; i++) w[i] = 0f;
            float x = t.X, z = t.Z, y = t.Y;

            // macro variation fields (break the 5-22 m tiling at three scales)
            float macro = Fbm(x, z, 0.0065f, 11.2f, 7.7f);         // ~150 m
            float mid = Fbm(x, z, 0.021f, 3.3f, 19.1f);            // ~50 m
            float fine = N(x, z, 0.11f, 7.1f, 2.9f);               // ~9 m
            float highland = SS(6f, 30f, y) * (1f - t.PlayArea * 0.6f);   // hills and slopes outside the valley

            // --- ground cover -------------------------------------------------------------------------------------
            // valley: lush grass broken by damp moss swales, leaf-litter drifts and bare patches at 4-60 m (one texture
            // tiled every 7.5 m read as a mown checkerboard lawn from the air); hills: dark moss and leaf litter under
            // the canopy, so the gaps between the far crowns read as shade, not as sunlit lawn speckles
            float swale = Fbm(x, z, 0.043f, 31.7f, 12.4f);          // ~25 m
            float speck = N(x, z, 0.26f, 13.3f, 27.1f);             // ~4 m
            float grass = Mathf.Lerp(0.85f, 0.12f, highland);
            // (review 2: brown leaf litter under the far crowns read as black-brown gaps from the air: moss green now)
            float floor = Mathf.Lerp(0.14f, 0.16f, highland);
            float moss = Mathf.Lerp(0.22f, 0.85f, highland);
            grass *= 0.35f + 1.0f * SS(0.28f, 0.72f, macro) + 0.35f * fine + 0.25f * speck;
            floor *= 0.30f + 1.3f * SS(0.40f, 0.72f, mid) + 0.45f * SS(0.55f, 0.85f, speck);
            moss *= 0.30f + 1.5f * SS(0.42f, 0.72f, 1f - macro * 0.5f - swale * 0.5f) + 0.3f * (1f - speck);
            // tall-grass stealth patches read as deep grass
            grass = Mathf.Lerp(grass, 1.2f, t.TallGrass);
            floor *= 1f - t.TallGrass * 0.8f;
            w[Grass] = grass; w[Floor] = floor; w[Moss] = moss;

            // --- Titan Meadows: macro hue and brightness swings (lush green grass, olive moss hollows, dry tan
            // leaf-litter drifts at ~40 m) and a muddy game trail from the lake towards the plaza, so the first shot
            // of the game is a grazed meadow, not one mown lawn
            float md = Vector2.Distance(new Vector2(x, z), new Vector2(-55f, -175f));
            float inMeadow = 1f - SS(60f, 95f, md);
            if (inMeadow > 0f)
            {
                float hue = Fbm(x, z, 0.024f, 41.3f, 8.6f);
                float dry = Fbm(x, z, 0.031f, 2.9f, 55.1f);
                w[Grass] *= 1f - inMeadow * 0.45f * SS(0.35f, 0.65f, 1f - hue);
                w[Moss] += inMeadow * 0.85f * SS(0.40f, 0.68f, 1f - hue) * (0.6f + 0.4f * fine);
                w[Floor] += inMeadow * 0.90f * SS(0.52f, 0.74f, dry) * (0.5f + 0.5f * speck);
                float trail = TrailDistance(x, z) + (fine - 0.5f) * 1.2f;
                float worn = (1f - SS(0.5f, 1.4f, trail)) * inMeadow;
                float edge = (1f - SS(1.2f, 2.6f, trail)) * inMeadow * 0.5f;
                if (worn + edge > 0f)
                {
                    Scale(w, 1f - Mathf.Clamp01(worn + edge * 0.6f));
                    w[Mud] += worn * (0.8f + 0.2f * speck);
                    w[Floor] += edge * 0.6f;
                }
            }

            // --- Talon Ridge raptor pen (the terrain is its floor): trampled forest floor, moss and mud, little grass
            float pen = SS(0f, 4f, Mathf.Min(Mathf.Min(x + 90f, 20f - x), Mathf.Min(z - 42f, 128f - z)));
            if (pen > 0f)
            {
                Scale(w, 1f - pen * 0.85f);
                w[Floor] += pen * (0.55f + 0.35f * mid);
                w[Moss] += pen * 0.30f * (0.5f + fine);
                w[Mud] += pen * 0.22f * SS(0.45f, 0.75f, speck * 0.6f + macro * 0.4f);
            }

            // --- worn ground: mud around slabs, walls and fences; stream banks (wet mud at the water, moss above) -------
            float mudAmt = Mathf.Max(t.NearStructure * (0.55f + 0.45f * fine), t.StreamBank * (0.85f + 0.1f * fine));
            w[Moss] += t.StreamBank * (1f - t.StreamBank) * 1.2f;      // a mossy fringe where the bank meets the grass
            // small natural mud patches in the valley floor
            mudAmt = Mathf.Max(mudAmt, SS(0.72f, 0.86f, mid) * 0.6f * (1f - highland));
            if (mudAmt > 0f) Scale(w, 1f - mudAmt);
            w[Mud] += mudAmt;

            // --- lake shore: wet mud and a little sand at the waterline
            if (t.LakeShore > 0f)
            {
                float k = t.LakeShore * (0.65f + 0.35f * fine);
                Scale(w, 1f - k);
                w[Mud] += k * 0.7f;
                w[Sand] += k * 0.3f;
            }

            // --- gravel road -------------------------------------------------------------------------------------
            if (t.RoadGravel > 0f) { Scale(w, 1f - t.RoadGravel); w[Gravel] += t.RoadGravel; }

            // --- beach / waterline -----------------------------------------------------------------------------------
            const float sea = -40f;
            float beach = SS(sea + 3.4f, sea + 1.6f, y + (fine - 0.5f) * 1.2f) * (1f - SS(24f, 36f, t.Slope));
            float underwater = SS(sea - 0.5f, sea - 3f, y);
            beach = Mathf.Max(beach, underwater);
            if (beach > 0f)
            {
                Scale(w, 1f - beach);
                w[Sand] += beach * (0.75f + 0.25f * fine);
                w[Mud] += beach * underwater * 0.25f * mid;
            }

            // --- rock on steep slopes only (broken up with noise: outcrops, not bands) ---------------------------------
            float slopeN = t.Slope + (mid - 0.5f) * 14f + (fine - 0.5f) * 6f;
            float rock = SS(41f, 53f, slopeN);
            if (rock > 0f)
            {
                Scale(w, 1f - rock);
                // the steepest faces are dark basalt (sunlit mossy rock read as pale scars on the hills)
                float basalt = SS(38f, 48f, slopeN) * (0.75f + 0.25f * macro) + SS(sea + 8f, sea + 1f, y) * 0.8f;
                basalt = Mathf.Clamp01(basalt);
                w[Rock] += rock * basalt;
                w[MossyRock] += rock * (1f - basalt);
            }
        }

        private static readonly Vector2[] Trail =
        {
            new Vector2(-104f, -226f), new Vector2(-84f, -214f), new Vector2(-70f, -196f), new Vector2(-64f, -178f),
            new Vector2(-52f, -164f), new Vector2(-34f, -158f), new Vector2(-20f, -146f), new Vector2(-14f, -128f),
        };

        private static float TrailDistance(float x, float z)
        {
            var p = new Vector2(x, z);
            float best = float.MaxValue;
            for (int i = 0; i + 1 < Trail.Length; i++)
            {
                Vector2 a = Trail[i], ab = Trail[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        private static void Scale(float[] w, float k)
        {
            for (int i = 0; i < w.Length; i++) w[i] *= k;
        }
    }
}
