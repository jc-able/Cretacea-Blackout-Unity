using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Z4 = Cretacea.Editor.WorldBuilder.Zone4_TerrainJungleAndDawnCoastBuilder;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// HDRP Water System surfaces: the sea (infinite ocean at Y -40, lit by the scene in every preset), the Tyrant
    /// Valley stream (River surface on a ribbon mesh that follows EvaluateStreamX) and the Titan Meadows lake.
    /// Replaces the unlit exposure-scaled ocean grid and the stream cubes' renderers.
    /// </summary>
    public static class LookTerrainWater
    {
        public const string MeshFolder = "Assets/Art/Terrain/Meshes";

        public static WaterSurface BuildSea(Transform parent)
        {
            var go = new GameObject("IslaTormenta_Sea");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(400f, LookTerrainShape.SeaLevel, -46f);
            go.layer = GameLayers.Water;
            WaterSurface w = go.AddComponent<WaterSurface>();
            w.surfaceType = WaterSurfaceType.OceanSeaLake;
            w.geometryType = WaterGeometryType.Infinite;
            w.timeMultiplier = 1f;
            // a moderate tropical swell; WaterWeatherDriver raises it with the storm at runtime
            w.repetitionSize = 500f;
            w.largeWindSpeed = 26f;
            w.largeChaos = 0.85f;
            w.largeOrientationValue = 250f;     // swell rolls in from the east towards the overlook
            w.ripples = true;
            w.ripplesWindSpeed = 10f;
            w.ripplesChaos = 0.9f;
            // tropical water: clear teal over the long sandy shelf (LookTerrainShape), deep blue-green further out
            w.refractionColor = new Color(0.12f, 0.62f, 0.62f);
            w.maxRefractionDistance = 2.5f;
            w.absorptionDistance = 14f;
            // (review 2: 0.15 / 0.15 left the sea a dark navy plane far below the sky; the last shot read warm grey)
            // review 4: still a slate 'northern' sea under a blue sky: a brighter turquoise body that shows through
            w.scatteringColor = new Color(0.04f, 0.33f, 0.54f);
            w.ambientScattering = 0.6f;
            w.heightScattering = 0.5f;
            w.displacementScattering = 0.35f;
            w.directLightTipScattering = 0.6f;
            w.directLightBodyScattering = 0.6f;
            w.startSmoothness = 0.95f;
            w.endSmoothness = 0.92f;      // the far sea mirrors the sky and the clouds
            w.foam = true;
            w.simulationFoamAmount = 0.5f;      // first whitecaps on the swell
            w.foamSmoothness = 0.3f;
            w.foamColor = new Color(0.88f, 0.91f, 0.91f);
            w.foamResolution = WaterSurface.WaterDecalRegionResolution.Resolution2048;
            w.foamTextureTiling = 0.18f;
            w.foamPersistenceMultiplier = 0.6f;
            w.decalRegionSize = new Vector2(ShoreFoamRegion, ShoreFoamRegion);
            w.caustics = true;
            w.causticsIntensity = 0.4f;
            w.tessellation = true;
            w.underWater = false;
            go.AddComponent<Cretacea.Rendering.WaterWeatherDriver>();
            BuildShoreFoam(go.transform);
            return w;
        }

        // ------------------------------------------------------------------------------------------------------------
        // SHORE FOAM: one water decal over the whole island, texture = a band along the waterline broken by noise
        // ------------------------------------------------------------------------------------------------------------
        private const float ShoreFoamRegion = 2048f;
        private static readonly Vector2 ShoreFoamCentre = new Vector2(20f, 50f);   // the island spans x -720..760, z -730..830
        private const int ShoreFoamRes = 1024;                                     // HDRP water decal atlas is 1024
        private const string DecalShaderPath = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/ShaderGraph/Water Decal Sample.shadergraph";

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D BuildShoreFoamTexture()
        {
            string path = "Assets/Art/Terrain/ShoreFoamMask.asset";
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = tex == null || tex.width != ShoreFoamRes;
            if (isNew) tex = new Texture2D(ShoreFoamRes, ShoreFoamRes, TextureFormat.RGBA32, true, true) { name = "ShoreFoamMask" };
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[ShoreFoamRes * ShoreFoamRes];
            float cell = ShoreFoamRegion / ShoreFoamRes;
            for (int iz = 0; iz < ShoreFoamRes; iz++)
            {
                float z = ShoreFoamCentre.y - ShoreFoamRegion * 0.5f + (iz + 0.5f) * cell;
                for (int ix = 0; ix < ShoreFoamRes; ix++)
                {
                    float x = ShoreFoamCentre.x - ShoreFoamRegion * 0.5f + (ix + 0.5f) * cell;
                    float d = LookTerrainShape.CoastDistance(x, z);     // + land, - sea; the waterline is d ~ 0
                    float foam = 0f;
                    if (d > -40f && d < 6f)
                    {
                        float n = Mathf.PerlinNoise(x * 0.045f + 3.3f, z * 0.045f + 8.1f) * 0.6f + Mathf.PerlinNoise(x * 0.16f + 1.7f, z * 0.16f + 4.4f) * 0.4f;
                        float sea = -d;
                        // wash right at the waterline (runs a few metres up the beach), fading out over ~10 m
                        float wash = (1f - SS(2f, 14f + 8f * n, sea)) * (1f - SS(1f, 5f, d));
                        // a broken surf line further out where the swell breaks on the shelf
                        float surf = SS(14f, 20f, sea) * (1f - SS(24f, 34f, sea)) * SS(0.4f, 0.65f, n) * 0.8f;
                        foam = Mathf.Clamp01(wash * (0.35f + 0.9f * SS(0.3f, 0.7f, n)) + surf);
                    }
                    foam = Mathf.Max(foam, LookTerrainCoast.WakeFoam(x, z));     // the rescue cutter's wake
                    byte r = (byte)Mathf.RoundToInt(foam * 255f), g = (byte)Mathf.RoundToInt(foam * 0.8f * 255f);
                    px[iz * ShoreFoamRes + ix] = new Color32(r, g, 0, 255);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(true, false);
            if (isNew) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }

        private static void BuildShoreFoam(Transform sea)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(DecalShaderPath);
            if (shader == null) { Debug.LogWarning("[Water] Water Decal Sample shader graph not found: no shore foam"); return; }
            const string matPath = "Assets/Art/Terrain/MAT_ShoreFoamDecal.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader) { name = "MAT_ShoreFoamDecal" }; AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            // _TYPE enum keyword: 0 Sphere, 1 Box, 2 Bow Wave, 3 Shore Wave, 4 Texture
            mat.SetFloat("_TYPE", 4f);
            foreach (string k in new[] { "_TYPE_SPHERE", "_TYPE_BOX", "_TYPE_BOW_WAVE", "_TYPE_SHORE_WAVE" }) mat.DisableKeyword(k);
            mat.EnableKeyword("_TYPE_TEXTURE");
            mat.SetTexture("_Foam_Texture", BuildShoreFoamTexture());
            mat.SetFloat("_AffectDeformation", 0f);
            mat.SetFloat("_AffectFoam", 1f);
            EditorUtility.SetDirty(mat);

            var go = new GameObject("IslaTormenta_ShoreFoamDecal");
            go.transform.SetParent(sea, false);
            go.transform.position = new Vector3(ShoreFoamCentre.x, LookTerrainShape.SeaLevel, ShoreFoamCentre.y);
            go.layer = GameLayers.Water;
            var decal = go.AddComponent<WaterDecal>();
            decal.scaleMode = DecalScaleMode.ScaleInvariant;
            decal.regionSize = new Vector2(ShoreFoamRegion, ShoreFoamRegion);
            decal.resolution = new Vector2Int(ShoreFoamRes, ShoreFoamRes);
            decal.material = mat;
            decal.surfaceFoamDimmer = 0.8f;
            decal.deepFoamDimmer = 1f;
        }

        /// <summary>River surface on a ribbon that follows the meandering stream trough.</summary>
        public static WaterSurface BuildStream(Transform parent, float zMin, float zMax)
        {
            WorldMaterialPalette.EnsureFolder(MeshFolder);
            const float step = 2f;
            const float halfWidth = 9f;     // wide enough that the banks (~1.1 m above the bed) bury the edges
            int rows = Mathf.CeilToInt((zMax - zMin) / step) + 1;
            const int cols = 7;
            var verts = new Vector3[rows * cols];
            var uvs = new Vector2[rows * cols];
            var tris = new int[(rows - 1) * (cols - 1) * 6];
            // smoothed water level: a little above the trough bottom, monotone-smoothed so it never steps up/down hard
            var level = new float[rows];
            for (int r = 0; r < rows; r++)
            {
                float z = zMin + r * step;
                float bottom = float.MaxValue;
                for (int k = -2; k <= 2; k++)
                {
                    float x = Z4.EvaluateStreamX(z) + k * 1.2f;
                    bottom = Mathf.Min(bottom, Z4.EvaluateWorldElevation(x, z));
                }
                // a full stream, 0.25 m below the lower bank top (review 2: +0.5 m over the bed still read as a ditch)
                float sx = Z4.EvaluateStreamX(z);
                float bank = Mathf.Min(Z4.EvaluateWorldElevation(sx - 6.5f, z), Z4.EvaluateWorldElevation(sx + 6.5f, z));
                level[r] = Mathf.Clamp(bank - 0.25f, bottom + 0.35f, bottom + 0.95f);
            }
            for (int pass = 0; pass < 6; pass++)
                for (int r = 1; r < rows - 1; r++) level[r] = (level[r - 1] + level[r] * 2f + level[r + 1]) * 0.25f;

            Vector3 origin = new Vector3(Z4.EvaluateStreamX(0f), level[rows / 2], 0f);
            for (int r = 0; r < rows; r++)
            {
                float z = zMin + r * step;
                float cx = Z4.EvaluateStreamX(z);
                float dxdz = (Z4.EvaluateStreamX(z + 0.5f) - Z4.EvaluateStreamX(z - 0.5f));
                Vector2 side = new Vector2(1f, -dxdz).normalized;   // perpendicular to the flow in XZ
                for (int c = 0; c < cols; c++)
                {
                    float u = c / (float)(cols - 1) * 2f - 1f;
                    Vector3 p = new Vector3(cx + side.x * u * halfWidth, level[r], z + side.y * u * halfWidth);
                    verts[r * cols + c] = p - origin;
                    uvs[r * cols + c] = new Vector2(u * 0.5f + 0.5f, (z - zMin) / 12f);
                }
            }
            int t = 0;
            for (int r = 0; r < rows - 1; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    int i = r * cols + c;
                    tris[t++] = i; tris[t++] = i + cols; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + cols; tris[t++] = i + cols + 1;
                }

            string meshPath = MeshFolder + "/StreamRibbon.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = "StreamRibbon" };
            else mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, meshPath);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject("TyrantValley_StreamWater");
            go.transform.SetParent(parent, false);
            go.transform.position = origin;
            go.layer = GameLayers.Water;
            var meshGo = new GameObject("StreamRibbonMesh");
            meshGo.transform.SetParent(go.transform, false);
            meshGo.layer = GameLayers.Water;
            meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = meshGo.AddComponent<MeshRenderer>();
            mr.enabled = false;                 // the water system draws the mesh with its own material
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            WaterSurface w = go.AddComponent<WaterSurface>();
            w.surfaceType = WaterSurfaceType.River;
            w.geometryType = WaterGeometryType.Custom;
            w.meshRenderers.Add(mr);
            w.largeWindSpeed = 2.5f;
            w.largeChaos = 0.6f;
            w.largeOrientationValue = 90f;
            w.ripples = true;
            w.ripplesWindSpeed = 3f;
            // dark tannin-stained jungle water: the bed fades within ~1 m, the surface reads by its reflections
            // clear green-brown jungle water: the bed shows in the shallows, the deep run reflects the sky
            w.refractionColor = new Color(0.20f, 0.30f, 0.26f);
            w.maxRefractionDistance = 0.6f;
            w.absorptionDistance = 1.6f;
            w.scatteringColor = new Color(0.10f, 0.20f, 0.16f);     // lighter green body: it reads as water from the air
            w.ambientScattering = 0.35f;
            w.startSmoothness = 0.96f;
            w.endSmoothness = 0.93f;
            w.foam = true;
            w.simulationFoamAmount = 0.6f;
            w.caustics = false;
            w.tessellation = false;
            w.underWater = false;
            return w;
        }

        /// <summary>Small lake in the Titan Meadows hollow (centre (-65, -210)).</summary>
        public static WaterSurface BuildLake(Transform parent, float level)
        {
            var go = new GameObject("TitanMeadows_Lake");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-65f, level, -210f);
            go.transform.localScale = new Vector3(96f, 1f, 96f);
            go.layer = GameLayers.Water;
            WaterSurface w = go.AddComponent<WaterSurface>();
            w.surfaceType = WaterSurfaceType.Pool;
            w.geometryType = WaterGeometryType.Quad;
            // murky meadow water: wind ripples break the sky reflection, short absorption hides the bowl
            w.ripples = true;
            w.ripplesWindSpeed = 7f;
            w.ripplesChaos = 0.9f;
            w.refractionColor = new Color(0.30f, 0.36f, 0.22f);
            w.maxRefractionDistance = 0.5f;
            w.absorptionDistance = 2.4f;
            w.scatteringColor = new Color(0.10f, 0.16f, 0.09f);
            w.ambientScattering = 0.45f;
            w.startSmoothness = 0.88f;
            w.endSmoothness = 0.80f;
            w.caustics = false;
            w.tessellation = false;
            w.underWater = false;
            return w;
        }
    }
}
