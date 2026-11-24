using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Rendering;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Look Pass step "weather": everything the storm leaves in the generated world, placed under one root
    /// ("[Weather]", rebuilt on every run, so the step is idempotent).
    ///   - puddle decals in the open on roads and slabs (faded by WetnessDriver with the wetness)
    ///   - low ground mist in Tyrant Valley and along the stream, thin haze in the lobby and the warehouse
    ///     (LocalVolumetricFog with a generated, slowly scrolling 3D noise)
    ///   - the electrical sparks restyled (self-lit streaks + SparkGlow arc light)
    /// Generated textures live in Assets/Art/VFX, the materials in Assets/Settings/Rendering and Assets/VFX.
    /// </summary>
    public static class LookWeather
    {
        public const string RootName = "[Weather]";
        private const string PuddleAtlasPath = "Assets/Art/VFX/wx_puddle_atlas.png";
        private const string MistNoisePath = "Assets/Art/VFX/wx_mist_noise3d.asset";
        // The palette's DecalPuddleWet (MAT_WetPuddleDecal.mat) is shared with the main menu's puddles: the atlas
        // puddles of the world get their own material, the shared one stays as authored.
        private const string PuddleMatPath = "Assets/Settings/Rendering/MAT_WetPuddleDecal_Atlas.mat";
        private const string RippleNormalPath = "Assets/Art/VFX/rain_ripples_normal.png";
        private const string PuddleNormalPath = "Assets/Art/VFX/wx_puddle_ripple_normal.png";
        // RGB copy of the atlas alpha: masks the lightning flash mirrored in the water to the puddle's own shape
        private const string PuddleMaskPath = "Assets/Art/VFX/wx_puddle_mask.png";
        private const float PuddleNormalStrength = 0.3f;
        private const string SparkMatPath = "Assets/VFX/MAT_WxSparks.mat";
        private const string BoltMatPath = "Assets/VFX/Resources/WxLightningBolt.mat";
        private const string CurtainMatPath = "Assets/VFX/Resources/WxRainCurtain.mat";
        private const string RainShader = "Cretacea/HDRP/RainParticle";
        private const string StreakMatPath = "Assets/Settings/Rendering/MAT_RainStreaks.mat";

        [MenuItem("Cretacea/Look/Place Weather (puddles, mist, sparks)")]
        public static void RunMenu() => Debug.Log("[Look] " + Apply());

        public static string Apply()
        {
            EnsureAssets();
            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);

            RoofOcclusionMap.Collect(1 << 13);
            int puddles = PlacePuddles(root.transform);
            int mist = PlaceMist(root.transform);
            int sparks = StyleSparks();
            NormalizeBuilderPuddles();
            return $"weather: {puddles} puddles, {mist} mist/haze volumes, {sparks} spark systems";
        }

        // ------------------------------------------------------------------------------------------------ assets
        public static void EnsureAssets()
        {
            if (!File.Exists(PuddleAtlasPath)) WritePuddleAtlas();
            if (AssetDatabase.LoadAssetAtPath<Texture3D>(MistNoisePath) == null) WriteMistNoise();
            if (!File.Exists(PuddleNormalPath)) WritePuddleNormal();
            if (!File.Exists(PuddleMaskPath)) WritePuddleMask();
            ConfigurePuddleMaterial();
            EnsureSparkMaterial();
            EnsureBoltMaterial();
            ConfigureStreakMaterial();
            EnsureCurtainMaterial();
        }

        // Distant rain curtains (StormRainOcclusionController loads it from Resources, so builds keep the shader).
        private static void EnsureCurtainMaterial()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CurtainMatPath));
            Material m = AssetDatabase.LoadAssetAtPath<Material>(CurtainMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Cretacea/HDRP/RainCurtain")) { name = "WxRainCurtain" };
                AssetDatabase.CreateAsset(m, CurtainMatPath);
            }
            m.shader = Shader.Find("Cretacea/HDRP/RainCurtain");
            m.SetColor("_Color", new Color(0.78f, 0.84f, 0.95f, 1f));
            m.SetFloat("_Opacity", 1f);
            m.SetVector("_RingOpacity", new Vector4(0.18f, 0.35f, 0.5f, 0f));   // near 55 m, mid 110 m, far 180 m
            m.SetFloat("_Scatter", 0.09f);
            m.SetFloat("_MoonWeight", 0.6f);
            m.SetFloat("_FlashWeight", 1f);
            m.SetFloat("_DepthFade", 18f);
            EditorUtility.SetDirty(m);
        }

        // The falling rain (review 1: "rain reads as snow"): faint, thin streaks lit only by the light that reaches
        // them. The moon and sky give a blue-grey hint (sky weight 0.3), lamps and the flashlight make them bright,
        // capped at about scene white at storm exposure (22 nits at EV 4 = 19 nits white).
        private static void ConfigureStreakMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(StreakMatPath);
            if (m == null) return;
            if (m.shader == null || m.shader.name != RainShader) m.shader = Shader.Find(RainShader);
            m.SetFloat("_Mode", 0f);
            m.SetColor("_Color", new Color(0.82f, 0.88f, 0.96f, 0.5f));
            m.SetFloat("_Scatter", 0.1f);
            m.SetFloat("_Brightness", 1f);
            m.SetFloat("_MaxNits", 35f);
            m.SetFloat("_ExposedCap", 0.3f);
            m.SetFloat("_SkyLight", 0.3f);
            m.SetFloat("_FlashLight", 1f);
            m.SetFloat("_Softness", 1.5f);
            m.SetFloat("_Occlusion", 0.06f);
            m.SetFloat("_CoverageDistance", 8f);
            m.SetVector("_NearFade", new Vector4(0.45f, 1.5f, 0f, 0f));
            EditorUtility.SetDirty(m);
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        private static float Fbm(float x, float y, int seed)
        {
            float v = 0f, a = 0.5f, f = 1f;
            for (int o = 0; o < 4; o++)
            {
                v += a * (Mathf.PerlinNoise(x * f + seed * 17.13f, y * f + seed * 5.71f) - 0.5f);
                a *= 0.5f; f *= 2.03f;
            }
            return v;
        }

        // 2 x 2 atlas of irregular puddle shapes: RGB = still-water tint, A = water depth / coverage.
        private static void WritePuddleAtlas()
        {
            const int cell = 256, res = cell * 2;
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false, true);
            var px = new Color32[res * res];
            for (int cy = 0; cy < 2; cy++)
            for (int cx = 0; cx < 2; cx++)
            {
                int seed = cy * 2 + cx + 3;
                for (int y = 0; y < cell; y++)
                for (int x = 0; x < cell; x++)
                {
                    float u = (x + 0.5f) / cell * 2f - 1f, v = (y + 0.5f) / cell * 2f - 1f;
                    // elongated blob with a ragged, fractal shore and a few satellite pools
                    float ang = Mathf.Atan2(v, u);
                    float r = Mathf.Sqrt(u * u * 0.85f + v * v * 1.25f);
                    float shore = 0.62f + 0.22f * Fbm(Mathf.Cos(ang) * 1.3f + 4f, Mathf.Sin(ang) * 1.3f + 4f, seed) * 2f
                                + 0.06f * Fbm(u * 6f, v * 6f, seed + 11) * 2f;
                    float main = Mathf.Clamp01((shore - r) / 0.10f);
                    float sat = Mathf.Clamp01((Fbm(u * 3.2f, v * 3.2f, seed + 29) * 2f + 0.15f - Mathf.Max(0f, r - 0.55f) * 1.2f) / 0.12f);
                    float edge = Mathf.Clamp01((1f - r) / 0.15f);
                    float a = Mathf.Clamp01(Mathf.Max(main, sat * 0.85f)) * edge;
                    a = Mathf.SmoothStep(0f, 1f, a);
                    px[(cy * cell + y) * res + cx * cell + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(PuddleAtlasPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PuddleAtlasPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PuddleAtlasPath);
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        // Tiling 3D value-noise (fbm) for the mist: alpha = density multiplier, RGB = white.
        private static void WriteMistNoise()
        {
            const int n = 32;
            var tex = new Texture3D(n, n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, name = "wx_mist_noise3d" };
            var lattice = new float[8 * 8 * 8];
            for (int i = 0; i < lattice.Length; i++) lattice[i] = Hash(i, i * 7 + 3, 91);
            var lattice2 = new float[16 * 16 * 16];
            for (int i = 0; i < lattice2.Length; i++) lattice2[i] = Hash(i, i * 13 + 1, 37);
            float Sample(float[] lat, int L, float x, float y, float z)
            {
                int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y), z0 = Mathf.FloorToInt(z);
                float fx = x - x0, fy = y - y0, fz = z - z0;
                fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy); fz = fz * fz * (3 - 2 * fz);
                float V(int a, int b, int c) => lat[((a % L + L) % L) + ((b % L + L) % L) * L + ((c % L + L) % L) * L * L];
                float c00 = Mathf.Lerp(V(x0, y0, z0), V(x0 + 1, y0, z0), fx);
                float c10 = Mathf.Lerp(V(x0, y0 + 1, z0), V(x0 + 1, y0 + 1, z0), fx);
                float c01 = Mathf.Lerp(V(x0, y0, z0 + 1), V(x0 + 1, y0, z0 + 1), fx);
                float c11 = Mathf.Lerp(V(x0, y0 + 1, z0 + 1), V(x0 + 1, y0 + 1, z0 + 1), fx);
                return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
            }
            var cols = new Color32[n * n * n];
            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = 0.65f * Sample(lattice, 8, x / 4f, y / 4f, z / 4f) + 0.35f * Sample(lattice2, 16, x / 2f, y / 2f, z / 2f);
                v = Mathf.Clamp01((v - 0.28f) / 0.5f);
                v = Mathf.Lerp(0.15f, 1f, v * v * (3f - 2f * v));
                cols[x + y * n + z * n * n] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(v * 255f));
            }
            tex.SetPixels32(cols);
            tex.Apply();
            AssetDatabase.CreateAsset(tex, MistNoisePath);
        }

        // The ripple normal flattened to 30 % (HDRP decals have no normal scale): lamps and the beam glint on it
        // without the puddle reading as a rippled sheet of tin.
        private static void WritePuddleNormal()
        {
            if (!File.Exists(RippleNormalPath)) return;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!src.LoadImage(File.ReadAllBytes(RippleNormalPath))) { Object.DestroyImmediate(src); return; }
            Color32[] px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float x = (px[i].r / 255f * 2f - 1f) * PuddleNormalStrength;
                float y = (px[i].g / 255f * 2f - 1f) * PuddleNormalStrength;
                float z = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y));
                px[i] = new Color32((byte)Mathf.RoundToInt((x * 0.5f + 0.5f) * 255f), (byte)Mathf.RoundToInt((y * 0.5f + 0.5f) * 255f),
                                    (byte)Mathf.RoundToInt((z * 0.5f + 0.5f) * 255f), 255);
            }
            src.SetPixels32(px);
            src.Apply();
            File.WriteAllBytes(PuddleNormalPath, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(PuddleNormalPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PuddleNormalPath);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        private static void WritePuddleMask()
        {
            if (!File.Exists(PuddleAtlasPath)) return;
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!src.LoadImage(File.ReadAllBytes(PuddleAtlasPath))) { Object.DestroyImmediate(src); return; }
            Color32[] px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++) { byte a = px[i].a; px[i] = new Color32(a, a, a, 255); }
            src.SetPixels32(px);
            src.Apply();
            File.WriteAllBytes(PuddleMaskPath, src.EncodeToPNG());
            Object.DestroyImmediate(src);
            AssetDatabase.ImportAsset(PuddleMaskPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PuddleMaskPath);
            imp.sRGBTexture = false;
            imp.alphaSource = TextureImporterAlphaSource.None;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        private static void ConfigurePuddleMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(PuddleMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("HDRP/Decal")) { name = "MAT_WetPuddleDecal_Atlas" };
                AssetDatabase.CreateAsset(m, PuddleMatPath);
            }
            if (m.shader == null || m.shader.name != "HDRP/Decal") m.shader = Shader.Find("HDRP/Decal");
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(PuddleAtlasPath);
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(PuddleNormalPath);
            // Still water: DARKER than the wet ground around it (it hides the slab's texture and its diffuse), a near
            // mirror for the lamps and the sky, with a faint ripple normal. Every channel follows the atlas shape:
            // blend source 0 = base-colour alpha (1 would be "mask-map blue x fade" = the whole projector box, which
            // drew the hard light rectangles of review 1).
            m.SetTexture("_BaseColorMap", atlas);
            m.SetColor("_BaseColor", new Color(0.15f, 0.16f, 0.17f, 0.92f));
            m.SetTexture("_NormalMap", normal);
            m.SetTexture("_MaskMap", null);
            m.SetFloat("_DecalBlend", 1f);
            m.SetFloat("_NormalBlendSrc", 0f);
            m.SetFloat("_MaskBlendSrc", 0f);
            m.SetFloat("_Smoothness", 0.97f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AO", 1f);
            m.SetFloat("_AffectAlbedo", 1f);
            m.SetFloat("_AffectNormal", 1f);
            m.SetFloat("_AffectMetal", 1f);
            m.SetFloat("_AffectSmoothness", 1f);
            m.SetFloat("_AffectAO", 0f);
            // the sky's flash mirrored in the water: WetnessDriver drives _EmissiveColor on a runtime copy; the mask
            // keeps it inside the puddle shape
            m.SetFloat("_AffectEmission", 1f);
            m.SetColor("_EmissiveColor", Color.black);
            m.SetTexture("_EmissiveColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PuddleMaskPath));
            m.SetFloat("_DecalMaskMapBlueScale", 1f);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
        }

        private static void EnsureSparkMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(SparkMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find(RainShader)) { name = "MAT_WxSparks" };
                AssetDatabase.CreateAsset(m, SparkMatPath);
            }
            m.shader = Shader.Find(RainShader);
            m.SetFloat("_Mode", 3f);
            m.SetColor("_Color", new Color(1f, 0.78f, 0.45f, 1f));
            m.SetFloat("_EmissionNits", 2600f);
            m.SetFloat("_MaxNits", 4000f);
            m.SetFloat("_UseRoofOcclusion", 0f);
            m.SetFloat("_Occlusion", 0f);
            m.SetFloat("_Softness", 1.4f);
            m.SetVector("_NearFade", new Vector4(0.05f, 0.2f, 0f, 0f));
            EditorUtility.SetDirty(m);
        }

        private static void EnsureBoltMaterial()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BoltMatPath));
            Material m = AssetDatabase.LoadAssetAtPath<Material>(BoltMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("Cretacea/HDRP/LightningBolt")) { name = "WxLightningBolt" };
                AssetDatabase.CreateAsset(m, BoltMatPath);
            }
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------------------------------------ puddles
        private static bool IsPuddleSurface(Renderer r)
        {
            if (r == null) return false;
            Material m = r.sharedMaterial;
            if (m == null) return false;
            string n = m.name;
            return n.Contains("Asphalt") || n.Contains("ConcreteCracked") || n.Contains("ConcreteExterior")
                || n.Contains("PavingStone") || n.Contains("PlazaPaving") || n.Contains("PlazaStrip") || n.Contains("GravelGround") || n.Contains("MudGround") || n.Contains("ConcreteFloorWet");
        }

        /// <summary>
        /// A terrain hit on the gravel or mud of a road (review 2: 'the moon never lights wet ground in the valley':
        /// Service Road 3 is a terrain layer, so it never got a puddle and looked dry in a downpour).
        /// </summary>
        private static bool IsTerrainRoad(RaycastHit hit)
        {
            Terrain t = hit.collider != null ? hit.collider.GetComponent<Terrain>() : null;
            TerrainData td = t != null ? t.terrainData : null;
            if (td == null || td.alphamapLayers == 0) return false;
            Vector3 local = hit.point - t.transform.position;
            int ax = Mathf.Clamp(Mathf.RoundToInt(local.x / td.size.x * (td.alphamapWidth - 1)), 0, td.alphamapWidth - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(local.z / td.size.z * (td.alphamapHeight - 1)), 0, td.alphamapHeight - 1);
            float[,,] w = td.GetAlphamaps(ax, az, 1, 1);
            TerrainLayer[] layers = td.terrainLayers;
            float road = 0f;
            for (int i = 0; i < layers.Length && i < td.alphamapLayers; i++)
            {
                string n = layers[i] != null ? layers[i].name : "";
                if (n.Contains("Gravel") || n.Contains("Mud")) road += w[0, 0, i];
            }
            return road > 0.55f;
        }

        private static bool IsPlazaSurface(Renderer r)
        {
            Material m = r != null ? r.sharedMaterial : null;
            return m != null && (m.name.Contains("PlazaPaving") || m.name.Contains("PlazaStrip"));
        }

        private struct PuddleCandidate
        {
            public Vector3 point;
            public float w, h, yaw, fade, order;
            public int cell;
        }

        private const int PuddleMask = (1 << 0) | (1 << 3) | (1 << 4);
        private const int PuddleCap = 400;          // HDRP maxDecalsOnScreen is 512; drawDistance 120 m keeps few on screen
        private const int PuddlesPerCell = 30;      // per 100 x 100 m cell, so no part of the map is starved

        // The footprint must lie on the same flat collider (8 edge rays within 3 cm) and be clear of anything
        // standing on it up to 0.8 m (walls, kerbs, props), so the decal box can never reach a wall.
        private static bool FootprintClear(RaycastHit hit, float w, float h, float yaw)
        {
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                // the 4 corners and 4 edge midpoints of the rectangle
                Vector2 e = new Vector2(Mathf.Clamp(Mathf.Cos(a) * 1.5f, -1f, 1f) * w * 0.5f, Mathf.Clamp(Mathf.Sin(a) * 1.5f, -1f, 1f) * h * 0.5f);
                Vector3 pt = hit.point + rot * new Vector3(e.x, 0f, e.y);
                if (!Physics.Raycast(pt + Vector3.up * 1.5f, Vector3.down, out RaycastHit eh, 3f, PuddleMask, QueryTriggerInteraction.Ignore)) return false;
                // a slab is flat to 3 cm; a rutted terrain road gets 10 cm (the decal box reaches 17 cm down)
                float tolerance = hit.collider is TerrainCollider ? 0.10f : 0.03f;
                if (eh.collider != hit.collider || Mathf.Abs(eh.point.y - hit.point.y) > tolerance) return false;
            }
            return !Physics.CheckBox(hit.point + Vector3.up * 0.45f, new Vector3(w * 0.5f, 0.35f, h * 0.5f), rot, PuddleMask, QueryTriggerInteraction.Ignore);
        }

        private static void ConfigurePuddleDecal(DecalProjector d)
        {
            // a shallow box from 17 cm under to 7.5 cm over the ground, and faded on anything steeper than 20-40
            // degrees: the puddle stays on the slab, never on a wall base, a kerb or a dinosaur's leg
            d.pivot = new Vector3(0f, 0f, 0.1f);
            d.size = new Vector3(d.size.x, d.size.y, 0.25f);
            d.startAngleFade = 20f;
            d.endAngleFade = 40f;
        }

        private static int PlacePuddles(Transform root)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PuddleMatPath);
            if (mat == null) return 0;
            var parent = new GameObject("Puddles").transform;
            parent.SetParent(root, false);

            // 1. every acceptable spot on the map (no early exit: a cap hit in the scan order starved the east)
            const float step = 2.5f;
            var cands = new List<PuddleCandidate>(1024);
            var placed = new List<Vector3>(1024);

            // 0. the hero view (NightCalm_plaza_eye, storm plaza): a cluster of 4-6 puddles on the paving in front of the
            // player, clear of the lamp pole, so the far lamps, bollards and the lobby windows throw reflections into
            // standing water (fix round: "no distinct puddle shape shows in the plaza")
            int hero = 0;
            for (int k = 0; k < 40 && hero < 6; k++)
            {
                float hx = Mathf.Lerp(-50f, -34f, Hash(k, 71, 21)), hz = Mathf.Lerp(-86f, -70f, Hash(k, 71, 22));
                if (!Physics.Raycast(new Vector3(hx, 60f, hz), Vector3.down, out RaycastHit hh, 120f, PuddleMask, QueryTriggerInteraction.Ignore)) continue;
                if (hh.normal.y < 0.985f || !IsPlazaSurface(hh.collider.GetComponent<Renderer>())) continue;
                if (RoofOcclusionMap.IsCovered(hh.point, 0.05f)) continue;
                bool near = false;
                foreach (Vector3 p in placed) if ((p - hh.point).sqrMagnitude < 3.2f * 3.2f) { near = true; break; }
                if (near) continue;
                float hs = Mathf.Lerp(1.6f, 2.8f, Hash(k, 71, 23));
                float hw = hs * Mathf.Lerp(1.1f, 1.7f, Hash(k, 71, 24)), hhgt = hs;
                float hyaw = Hash(k, 71, 25) * 360f;
                // clear of the pole and the bollards by a margin (a wider probe box than the footprint)
                if (Physics.CheckBox(hh.point + Vector3.up * 0.6f, new Vector3(hw * 0.5f + 0.6f, 0.5f, hhgt * 0.5f + 0.6f), Quaternion.Euler(0f, hyaw, 0f), PuddleMask, QueryTriggerInteraction.Ignore)) continue;
                if (!FootprintClear(hh, hw, hhgt, hyaw)) continue;
                placed.Add(hh.point);
                cands.Add(new PuddleCandidate
                {
                    point = hh.point, w = hw, h = hhgt, yaw = hyaw,
                    fade = Mathf.Lerp(0.85f, 1f, Hash(k, 71, 26)),
                    cell = hero % 4,
                    order = -1f + hero * 0.01f   // first in the cap order
                });
                hero++;
            }

            for (float x = -95f; x <= 565f; x += step)
            for (float z = -140f; z <= 95f; z += step)
            {
                int ix = Mathf.RoundToInt(x / step), iz = Mathf.RoundToInt(z / step);
                float jx = (Hash(ix, iz, 5) - 0.5f) * step, jz = (Hash(ix, iz, 6) - 0.5f) * step;
                Vector3 o = new Vector3(x + jx, 60f, z + jz);
                // clusters: puddles gather where the noise says the slab sags
                float field = Mathf.PerlinNoise(o.x * 0.085f + 31.7f, o.z * 0.085f + 12.9f) * 0.75f + Mathf.PerlinNoise(o.x * 0.31f, o.z * 0.31f) * 0.25f;
                // the plaza paving (the hero exterior) holds water more readily: field >= 0.5 there, 0.6 elsewhere
                if (field < 0.5f || Hash(ix, iz, 9) > 0.55f) continue;
                if (!Physics.Raycast(o, Vector3.down, out RaycastHit hit, 120f, PuddleMask, QueryTriggerInteraction.Ignore)) continue;
                bool terrainRoad = hit.collider is TerrainCollider && hit.normal.y >= 0.975f && IsTerrainRoad(hit);
                if (hit.normal.y < 0.985f && !terrainRoad) continue;
                Renderer r = hit.collider.GetComponent<Renderer>();
                if (!terrainRoad && !IsPuddleSurface(r)) continue;
                if (field < 0.6f && !IsPlazaSurface(r)) continue;
                if (RoofOcclusionMap.IsCovered(hit.point, 0.05f)) continue;
                bool tooClose = false;
                foreach (Vector3 p in placed) if ((p - hit.point).sqrMagnitude < 2.6f * 2.6f) { tooClose = true; break; }
                if (tooClose) continue;

                float size = Mathf.Lerp(1.4f, 3.6f, Mathf.InverseLerp(0.6f, 0.85f, field)) * Mathf.Lerp(0.8f, 1.2f, Hash(ix, iz, 8));
                float w = size * Mathf.Lerp(1.0f, 1.6f, Hash(ix, iz, 10)), h = size;
                float yaw = Hash(ix, iz, 7) * 360f;
                if (!FootprintClear(hit, w, h, yaw))
                {
                    w *= 0.7f; h *= 0.7f;
                    if (!FootprintClear(hit, w, h, yaw)) continue;
                }
                placed.Add(hit.point);
                cands.Add(new PuddleCandidate
                {
                    point = hit.point, w = w, h = h, yaw = yaw,
                    fade = Mathf.Lerp(0.75f, 1f, Hash(ix, iz, 12)),
                    cell = Mathf.FloorToInt(Hash(ix, iz, 11) * 4f) % 4,
                    order = Hash(ix, iz, 13)
                });
            }

            // 2. keep up to PuddlesPerCell per 100 x 100 m cell in hash order, then the global cap
            cands.Sort((a, b) => a.order.CompareTo(b.order));
            var perCell = new Dictionary<Vector2Int, int>();
            int count = 0;
            foreach (PuddleCandidate c in cands)
            {
                if (count >= PuddleCap) break;
                var key = new Vector2Int(Mathf.FloorToInt(c.point.x / 100f), Mathf.FloorToInt(c.point.z / 100f));
                perCell.TryGetValue(key, out int inCell);
                if (inCell >= PuddlesPerCell) continue;
                perCell[key] = inCell + 1;

                var go = new GameObject($"WxPuddle_{count:000}");
                go.transform.SetParent(parent, false);
                go.transform.position = c.point + Vector3.up * 0.05f;
                go.transform.rotation = Quaternion.Euler(90f, c.yaw, 0f);
                var d = go.AddComponent<DecalProjector>();
                d.material = mat;
                d.size = new Vector3(c.w, c.h, 0.25f);
                ConfigurePuddleDecal(d);
                d.uvScale = new Vector2(0.5f, 0.5f);
                d.uvBias = new Vector2((c.cell % 2) * 0.5f, (c.cell / 2) * 0.5f);
                d.fadeFactor = c.fade;
                d.drawDistance = 120f;
                count++;
            }
            Debug.Log($"[Look] weather: {cands.Count} puddle spots accepted ({hero} in the hero plaza view), {count} placed in {perCell.Count} cells");
            count += PlaceValleyRoadWater(parent, mat, placed);
            return count;
        }

        // ------------------------------------------------------------------------------------------------ valley road
        // Review 3: "in a downpour the valley road looks dry" (the noise field above left the walked stretch of
        // Service Road 3 empty). Along the road's own polyline, from the paddock gate to the stream: a puddle every
        // 15-25 m in a wheel rut at the lowest point nearby (2-5 m long, laid along the road), and two dark glossy rut
        // strips (smoothness 0.8) the whole way. The names contain "Puddle", so WetnessDriver fades them in with the
        // soak like every other puddle. The pebble field (terrain gravel, smoothness 0.42) stays as it is.
        private static readonly Vector2[] ValleyRoadXZ =
        {
            new Vector2(91f, -3.5f), new Vector2(120f, 0f), new Vector2(160f, 8f), new Vector2(200f, 4f)
        };
        private const float ValleyRoadEndX = 191f;      // the stream bank starts at about x 193 on the road
        private const float RutOffset = 0.95f;          // half a service truck's track
        private const string RutMatPath = "Assets/Settings/Rendering/MAT_WetRutDecal.mat";
        private const string RutMaskPath = "Assets/Art/VFX/wx_rut_mask_v2.png";

        private static bool RoadPointAt(float s, out Vector3 p, out Vector3 fwd)
        {
            for (int i = 0; i < ValleyRoadXZ.Length - 1; i++)
            {
                Vector2 a = ValleyRoadXZ[i], b = ValleyRoadXZ[i + 1];
                float len = Vector2.Distance(a, b);
                if (s <= len || i == ValleyRoadXZ.Length - 2)
                {
                    Vector2 q = Vector2.Lerp(a, b, Mathf.Clamp01(s / len));
                    Vector2 d = (b - a) / len;
                    p = new Vector3(q.x, 0f, q.y);
                    fwd = new Vector3(d.x, 0f, d.y);
                    return q.x <= ValleyRoadEndX;
                }
                s -= len;
            }
            p = fwd = Vector3.zero;
            return false;
        }

        private static bool TerrainHit(Vector3 xz, out RaycastHit hit)
        {
            if (!Physics.Raycast(new Vector3(xz.x, 80f, xz.z), Vector3.down, out hit, 160f, PuddleMask, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider is TerrainCollider;
        }

        private static int PlaceValleyRoadWater(Transform parent, Material puddleMat, List<Vector3> placed)
        {
            int n = 0;
            // 1. puddles: one per 15-25 m stretch, in the lower of the two ruts at the lowest of 9 nearby stations
            float s = 6f;
            for (int k = 0; k < 40; k++)
            {
                float stretch = Mathf.Lerp(15f, 25f, Hash(k, 5, 61));
                float centre = s + stretch * 0.5f;
                s += stretch;
                if (!RoadPointAt(centre, out _, out _)) break;
                var spots = new List<(RaycastHit hit, Vector3 fwd)>(18);
                for (int j = -4; j <= 4; j++)
                {
                    if (!RoadPointAt(centre + j * 1.5f, out Vector3 c, out Vector3 f)) continue;
                    Vector3 side = new Vector3(f.z, 0f, -f.x);
                    for (int r = -1; r <= 1; r += 2)
                    {
                        Vector3 q = c + side * (RutOffset * r + (Hash(k, j + 9, 62 + r) - 0.5f) * 0.3f);
                        if (TerrainHit(q, out RaycastHit h) && h.normal.y >= 0.94f) spots.Add((h, f));
                    }
                }
                spots.Sort((x, y) => x.hit.point.y.CompareTo(y.hit.point.y));   // lowest first: water gathers there
                float len = Mathf.Lerp(2.2f, 4.8f, Hash(k, 7, 63));
                float wid = Mathf.Lerp(0.9f, 1.6f, Hash(k, 7, 64));
                float yaw = 0f;
                RaycastHit best = default; bool found = false;
                for (int t = 0; t < spots.Count * 2 && !found; t++)
                {
                    var sp = spots[t % spots.Count];
                    if (t == spots.Count) { wid *= 0.7f; len *= 0.7f; }   // second pass: a smaller puddle
                    yaw = Mathf.Atan2(sp.fwd.x, sp.fwd.z) * Mathf.Rad2Deg + (Hash(k, 7, 65) - 0.5f) * 14f;
                    if (FootprintClear(sp.hit, wid, len, yaw)) { best = sp.hit; found = true; }
                }
                if (!found) continue;
                var go = new GameObject($"WxPuddle_Road3_{n:00}");
                go.transform.SetParent(parent, false);
                go.transform.position = best.point + Vector3.up * 0.05f;
                go.transform.rotation = Quaternion.Euler(90f, yaw, 0f);
                var d = go.AddComponent<DecalProjector>();
                d.material = puddleMat;
                d.size = new Vector3(wid, len, 0.25f);
                ConfigurePuddleDecal(d);
                int cell = Mathf.FloorToInt(Hash(k, 7, 66) * 4f) % 4;
                d.uvScale = new Vector2(0.5f, 0.5f);
                d.uvBias = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
                d.fadeFactor = Mathf.Lerp(0.85f, 1f, Hash(k, 7, 67));
                d.drawDistance = 120f;
                placed.Add(best.point);
                n++;
            }

            // 2. the two wheel ruts: dark, glossy, broken strips the whole way (8 m pieces that follow the bends)
            Material rut = EnsureRutMaterial();
            int ruts = 0;
            if (rut != null)
            {
                const float piece = 8f;
                for (int r = -1; r <= 1; r += 2)
                for (int k = 0; k < 30; k++)
                {
                    float sc = 2f + piece * (k + 0.5f) + (r > 0 ? 3.0f : 0f);
                    if (!RoadPointAt(sc, out Vector3 c, out Vector3 f)) break;
                    Vector3 side = new Vector3(f.z, 0f, -f.x);
                    Vector3 q = c + side * (RutOffset * r + (Hash(k, r + 3, 71) - 0.5f) * 0.12f);
                    if (!TerrainHit(q, out RaycastHit h) || h.normal.y < 0.94f) continue;
                    var go = new GameObject($"WxPuddleRut_{(r < 0 ? "L" : "R")}{k:00}");
                    go.transform.SetParent(parent, false);
                    go.transform.position = h.point + Vector3.up * 0.05f;
                    go.transform.rotation = Quaternion.Euler(90f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f);
                    var d = go.AddComponent<DecalProjector>();
                    d.material = rut;
                    d.size = new Vector3(Mathf.Lerp(0.55f, 0.75f, Hash(k, r + 3, 72)), piece + 1.2f, 0.25f);
                    ConfigurePuddleDecal(d);
                    d.uvScale = new Vector2(1f, 1f);
                    d.uvBias = new Vector2(0f, 0f);
                    // every other piece runs the mask backwards, so neighbouring pieces never repeat
                    if ((k & 1) == 1) { d.uvScale = new Vector2(1f, -1f); d.uvBias = new Vector2(0f, 1f); }
                    d.fadeFactor = Mathf.Lerp(0.8f, 1f, Hash(k, r + 3, 73));
                    d.drawDistance = 90f;
                    ruts++;
                }
            }
            Debug.Log($"[Look] weather: valley road {n} puddles + {ruts} rut strips");
            return n + ruts;
        }

        private static Material EnsureRutMaterial()
        {
            if (!File.Exists(RutMaskPath)) WriteRutMask();
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(RutMaskPath);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(RutMatPath);
            if (m == null)
            {
                m = new Material(Shader.Find("HDRP/Decal")) { name = "MAT_WetRutDecal" };
                AssetDatabase.CreateAsset(m, RutMatPath);
            }
            if (m.shader == null || m.shader.name != "HDRP/Decal") m.shader = Shader.Find("HDRP/Decal");
            // churned mud holding a film of water: darker than the wet gravel, smoothness 0.8, no normal, shaped by
            // the mask's alpha (blend source 0) so no box edge ever shows
            m.SetTexture("_BaseColorMap", mask);
            m.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.155f, 0.6f));
            m.SetTexture("_NormalMap", null);
            m.SetTexture("_MaskMap", null);
            m.SetFloat("_DecalBlend", 1f);
            m.SetFloat("_NormalBlendSrc", 0f);
            m.SetFloat("_MaskBlendSrc", 0f);
            m.SetFloat("_Smoothness", 0.8f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_AO", 1f);
            m.SetFloat("_AffectAlbedo", 1f);
            m.SetFloat("_AffectNormal", 0f);
            m.SetFloat("_AffectMetal", 1f);
            m.SetFloat("_AffectSmoothness", 1f);
            m.SetFloat("_AffectAO", 0f);
            m.SetFloat("_AffectEmission", 0f);
            m.SetFloat("_DecalMaskMapBlueScale", 1f);
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        // 64 x 512 mask: a soft trough across the width, broken along the length where the water has drained.
        private static void WriteRutMask()
        {
            const int w = 64, h = 512;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                float along = Mathf.PerlinNoise(v * 3f, 3.7f) * 0.75f + Mathf.PerlinNoise(v * 17f, 8.1f) * 0.25f;
                float keep = Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 0.5f, along)));   // mostly continuous, thinner where it drained
                float ends = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.08f, v)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.92f, v));   // pieces overlap 1.2 m
                float wander = (Mathf.PerlinNoise(v * 6f, 1.3f) - 0.5f) * 0.25f;
                float halfW = Mathf.Lerp(0.18f, 0.36f, Mathf.PerlinNoise(v * 14f, 5.5f));
                for (int x = 0; x < w; x++)
                {
                    float u = x / (float)(w - 1) - 0.5f - wander;
                    float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(halfW * 0.45f, halfW, Mathf.Abs(u)));
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(across * keep * ends) * 255f);
                    px[y * w + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(RutMaskPath));
            File.WriteAllBytes(RutMaskPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RutMaskPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (TextureImporter)AssetImporter.GetAtPath(RutMaskPath);
            imp.sRGBTexture = true;
            imp.alphaSource = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
            imp.SaveAndReimport();
        }

        // The zone builders' own puddles (PathPuddle_*, SubPad_Puddle*, Lobby_SkylightPuddle_*) use the shared
        // palette material: move them to the atlas material, give each one atlas cell and the same shallow,
        // angle-faded box as the placed puddles.
        private static void NormalizeBuilderPuddles()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(PuddleMatPath);
            int i = 0;
            foreach (DecalProjector d in Object.FindObjectsByType<DecalProjector>(FindObjectsInactive.Include))
            {
                if (d.name.StartsWith("WxPuddle") || d.name.IndexOf("Puddle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (mat != null) d.material = mat;
                int cell = i++ % 4;
                d.uvScale = new Vector2(0.5f, 0.5f);
                d.uvBias = new Vector2((cell % 2) * 0.5f, (cell / 2) * 0.5f);
                ConfigurePuddleDecal(d);
            }
        }

        // ------------------------------------------------------------------------------------------------ mist
        private static LocalVolumetricFog AddFog(Transform parent, string name, Vector3 center, Vector3 size, Color albedo, float mfp, Vector3 fade, Vector3 scroll, float tiling,
            LocalVolumetricFogBlendingMode blend = LocalVolumetricFogBlendingMode.Additive)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var f = go.AddComponent<LocalVolumetricFog>();
            var p = f.parameters;
            p.albedo = albedo;
            p.meanFreePath = mfp;
            p.size = size;
            p.blendingMode = blend;
            p.falloffMode = LocalVolumetricFogFalloffMode.Exponential;
            p.positiveFade = fade;
            p.negativeFade = new Vector3(fade.x, Mathf.Min(0.3f, fade.y), fade.z);
            p.distanceFadeStart = 200f;
            p.distanceFadeEnd = 320f;
            p.maskMode = LocalVolumetricFogMaskMode.Texture;
            p.volumeMask = AssetDatabase.LoadAssetAtPath<Texture3D>(MistNoisePath);
            p.textureScrollingSpeed = scroll;
            p.textureTiling = Vector3.one * tiling;
            f.parameters = p;
            return f;
        }

        private static int PlaceMist(Transform root)
        {
            var parent = new GameObject("MistAndHaze").transform;
            parent.SetParent(root, false);
            parent.gameObject.AddComponent<Cretacea.Rendering.GroundMistDriver>();   // mist follows the weather
            int n = 0;
            Color mistAlbedo = new Color(0.86f, 0.9f, 0.95f);

            // Along the stream: one ribbon of low mist per pair of water strips, hugging the water.
            var strips = new List<Transform>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
                if (t.name.StartsWith("StreamWater_")) strips.Add(t);
            strips.Sort((a, b) => a.position.z.CompareTo(b.position.z));
            float minWater = float.MaxValue;
            for (int i = 0; i < strips.Count; i += 2)
            {
                Vector3 a = strips[i].position;
                Vector3 b = strips[Mathf.Min(i + 1, strips.Count - 1)].position;
                Vector3 c = (a + b) * 0.5f;
                minWater = Mathf.Min(minWater, Mathf.Min(a.y, b.y));
                float len = Mathf.Abs(b.z - a.z) + 16f;
                float yaw = Mathf.Atan2(b.x - a.x, Mathf.Max(0.1f, b.z - a.z)) * Mathf.Rad2Deg;
                // Max blending: the overlapping ribbons do not add up into bright bands
                // Review 3: at a 2 m mean free path with the top fading over 85 % of the box, the ribbon was a near-white
                // slab with a flat lid across the valley. Thinner, fading over its whole height, with finer wisps.
                var f = AddFog(parent, $"StreamMist_{n:00}", c + Vector3.up * 1.1f, new Vector3(22f, 2.2f, len), mistAlbedo, 6f,
                    new Vector3(0.35f, 1.0f, 0.2f), new Vector3(0.25f, 0.02f, 0.12f), 0.14f, LocalVolumetricFogBlendingMode.Max);
                f.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                n++;
            }

            // Tyrant Valley floor: low mist that hugs the ground in the low parts of the valley. The valley floor is not
            // flat (about -1.5 to +2 m) and lies well above the stream bed, so one box at the stream's level was buried
            // under the terrain: the floor is tiled into cells, each sitting on its own ground height, and only the
            // low cells get mist. Max blending, so the overlaps and the stream ribbons never add up into bands.
            if (strips.Count > 0)
            {
                Vector3 lo = strips[0].position, hi = strips[strips.Count - 1].position;
                float cx = 0f; foreach (Transform t in strips) cx += t.position.x; cx /= strips.Count;
                const float cellX = 34f, cellZ = 46f, spanX = 170f;
                float spanZ = Mathf.Abs(hi.z - lo.z) + 60f, z0 = (lo.z + hi.z) * 0.5f - spanZ * 0.5f, x0 = cx - spanX * 0.5f;
                int nx = Mathf.CeilToInt(spanX / cellX), nz = Mathf.CeilToInt(spanZ / cellZ);
                var floors = new float[nx * nz];
                var all = new List<float>(nx * nz);
                for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    // 25th percentile of a 5 x 5 sample of the ground in the cell
                    var hs = new List<float>(25);
                    for (int sz = 0; sz < 5; sz++)
                    for (int sx = 0; sx < 5; sx++)
                    {
                        Vector3 o = new Vector3(x0 + (ix + (sx + 0.5f) / 5f) * cellX, 80f, z0 + (iz + (sz + 0.5f) / 5f) * cellZ);
                        if (Physics.Raycast(o, Vector3.down, out RaycastHit h, 200f, PuddleMask, QueryTriggerInteraction.Ignore) && h.collider is TerrainCollider)
                            hs.Add(h.point.y);
                    }
                    if (hs.Count < 8) { floors[iz * nx + ix] = float.NaN; continue; }
                    hs.Sort();
                    floors[iz * nx + ix] = hs[hs.Count / 4];
                    all.Add(hs[hs.Count / 4]);
                }
                all.Sort();
                float valleyLow = all.Count > 0 ? all[all.Count / 2] + 0.6f : minWater + 3f;   // the lower half of the valley, plus a little
                for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float f = floors[iz * nx + ix];
                    if (float.IsNaN(f) || f > valleyLow) continue;
                    // dense at the ground and gone below eye height (box from floor-0.5 to floor+1.3, top 45 % faded), so
                    // the player looks over it and the flashlight beam is not swallowed (measured: band +42 % towards the
                    // moon with the flashlight off, beam centre +24 % with it on)
                    var c = new Vector3(x0 + (ix + 0.5f) * cellX, f + 0.4f, z0 + (iz + 0.5f) * cellZ);
                    var fog = AddFog(parent, $"ValleyGroundMist_{n:00}", c, new Vector3(cellX + 8f, 1.8f, cellZ + 8f), mistAlbedo, 4f,
                        new Vector3(0.12f, 1.0f, 0.12f), new Vector3(0.35f, 0.03f, 0.18f), 0.03f, LocalVolumetricFogBlendingMode.Max);   // top faded over 80 %: no flat lid
                    var fp = fog.parameters; fp.distanceFadeStart = 110f; fp.distanceFadeEnd = 180f; fog.parameters = fp;
                    n++;
                }
            }

            // Big interiors: a thin, still haze that makes the lamps and the beam read in the air.
            Color hazeAlbedo = new Color(0.82f, 0.82f, 0.8f);
            GameObject lobby = GameObject.Find("Lobby_Floor");
            if (lobby != null)
            {
                Vector3 p = lobby.transform.position;
                AddFog(parent, "LobbyHaze", p + new Vector3(0f, 5.4f, 0f), new Vector3(37f, 10.6f, 50f), hazeAlbedo, 45f,
                    new Vector3(0.03f, 0.08f, 0.03f), new Vector3(0.02f, 0.01f, 0.015f), 0.06f);
                n++;
            }
            GameObject wh = GameObject.Find("WarehouseB_Floor");
            if (wh != null)
            {
                Vector3 p = wh.transform.position;
                AddFog(parent, "WarehouseHaze", p + new Vector3(0f, 3.9f, 0f), new Vector3(41f, 7.6f, 31f), hazeAlbedo, 40f,
                    new Vector3(0.03f, 0.1f, 0.03f), new Vector3(0.02f, 0.01f, 0.015f), 0.07f);
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------ sparks
        private static int StyleSparks()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(SparkMatPath);
            int n = 0;
            foreach (ParticleSystem ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include))
            {
                if (ps.name.IndexOf("Spark", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r == null) continue;
                if (mat != null) r.sharedMaterial = mat;
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.035f;
                r.lengthScale = 1.2f;
                r.maxParticleSize = 0.05f;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
                {
                    ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
                    ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX
                });
                var main = ps.main;
                main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.018f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.93f, 0.75f, 1f), new Color(1f, 0.6f, 0.22f, 1f));
                var col = ps.collision;
                col.enabled = true;
                col.type = ParticleSystemCollisionType.World;
                col.mode = ParticleSystemCollisionMode.Collision3D;
                col.dampen = 0.55f;
                col.bounce = 0.35f;
                col.lifetimeLoss = 0.25f;
                col.quality = ParticleSystemCollisionQuality.Low;
                col.collidesWith = (1 << 0) | (1 << 3) | (1 << 4);
                // a system the builder left with no emission at all (PathLampSparksVfx: rate 0, no bursts) never shows
                // a spark when the story plays it: give it an intermittent arc, a burst every couple of seconds
                var em = ps.emission;
                if (em.rateOverTime.constantMax <= 0.001f && em.burstCount == 0)
                {
                    em.enabled = true;
                    var burst = new ParticleSystem.Burst(0.05f, 14, 28, 0, 1.7f) { probability = 0.75f };
                    em.SetBursts(new[] { burst });
                    var mainLoop = ps.main;
                    mainLoop.loop = true;
                }
                if (ps.GetComponent<SparkGlow>() == null) ps.gameObject.AddComponent<SparkGlow>();
                n++;
            }
            return n;
        }
    }
}
