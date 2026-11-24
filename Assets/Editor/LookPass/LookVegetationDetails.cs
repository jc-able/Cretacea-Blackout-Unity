using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Z4 = Cretacea.Editor.WorldBuilder.Zone4_TerrainJungleAndDawnCoastBuilder;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Ground cover as instanced terrain details on the main tile: grass tufts, tall grass (the 7 stealth patches and
    /// meadow edges) and fern fans. The meshes are a few crossed cards cut from the Poly Haven grass / fern atlases
    /// (6-16 triangles each, instead of the 941-24,730 triangle source tufts), so thousands of them cost almost nothing.
    /// </summary>
    public static class LookVegetationDetails
    {
        private const string Folder = LookVegetationPrefabs.Folder;
        private const string MeshFolder = LookVegetationPrefabs.MeshFolder;

        // sprite rectangles (u0, v0, u1, v1) measured on grass_bermuda_01_BaseColor / fern_02_BaseColor alpha
        private static readonly Vector4[] TuftSprites =
        {
            new Vector4(0.684f, 0.762f, 0.945f, 0.891f), new Vector4(0.043f, 0.711f, 0.309f, 0.840f),
            new Vector4(0.051f, 0.457f, 0.184f, 0.578f), new Vector4(0.281f, 0.461f, 0.457f, 0.594f),
            new Vector4(0.539f, 0.453f, 0.715f, 0.637f), new Vector4(0.781f, 0.453f, 0.938f, 0.676f),
        };
        private static readonly Vector4[] TallSprites =
        {
            new Vector4(0.355f, 0.688f, 0.469f, 0.969f), new Vector4(0.535f, 0.770f, 0.664f, 0.969f),
            new Vector4(0.047f, 0.012f, 0.168f, 0.316f), new Vector4(0.234f, 0.016f, 0.387f, 0.367f),
            new Vector4(0.426f, 0.008f, 0.613f, 0.312f), new Vector4(0.699f, 0.008f, 0.863f, 0.332f),
        };
        private static readonly Vector4[] FernSprites =
        {
            new Vector4(0.086f, 0.051f, 0.234f, 0.891f), new Vector4(0.312f, 0.234f, 0.457f, 0.977f),
            new Vector4(0.508f, 0.500f, 0.660f, 0.973f), new Vector4(0.715f, 0.105f, 0.836f, 0.891f),
            new Vector4(0.512f, 0.035f, 0.629f, 0.488f),
        };

        public const int ProtoTuft = 0, ProtoTall = 1, ProtoFern = 2, ProtoShort = 3, ProtoTallDry = 4, ProtoTallDark = 5;

        /// <summary>Crossed vertical cards; each card picks a sprite and keeps its aspect ratio.</summary>
        private static Mesh CardClump(string name, Vector4[] sprites, int cards, float height, float spread, float lean, int seed)
        {
            var rng = new System.Random(seed);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int c = 0; c < cards; c++)
            {
                Vector4 s = sprites[(c + seed) % sprites.Length];
                // the atlas is square, so the sprite aspect in UV equals the aspect in pixels
                float aspect = (s.z - s.x) / Mathf.Max(0.01f, s.w - s.y);
                float h = height * (0.8f + 0.4f * (float)rng.NextDouble());
                float w = h * aspect;
                float ang = c * Mathf.PI / cards + (float)rng.NextDouble() * 0.5f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                Vector3 off = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * spread;
                // lean outward (fern fans): top moves along the card's normal
                Vector3 normal = Vector3.Cross(dir, Vector3.up).normalized * (c % 2 == 0 ? 1f : -1f);
                Vector3 top = Vector3.up * h + normal * (h * lean);
                int b = v.Count;
                v.Add(off - dir * w * 0.5f); v.Add(off + dir * w * 0.5f); v.Add(off + dir * w * 0.5f + top); v.Add(off - dir * w * 0.5f + top);
                uv.Add(new Vector2(s.x, s.y)); uv.Add(new Vector2(s.z, s.y)); uv.Add(new Vector2(s.z, s.w)); uv.Add(new Vector2(s.x, s.w));
                // soft "up" normals: cards light like the ground they stand on, no black back faces
                Vector3 nn = (Vector3.up * 0.75f + normal * 0.25f).normalized;
                n.Add(nn); n.Add(nn); n.Add(Vector3.up); n.Add(Vector3.up);
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
            string path = $"{MeshFolder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = name };
            else mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject CardPrefab(string name, Mesh mesh, string materialPath)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = true;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, $"{Folder}/{name}.prefab");
            Object.DestroyImmediate(go);
            return saved;
        }

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// The ground-cover cards get their own copy of the grass material: the shared one (smoothness up to 0.62) lit the
        /// blade tips silver-white under the night sky (review 1 night_a: NightCalm_valley_eye, where chapter 4 tells the
        /// player to hide). Rough blades with specular occlusion read as dark grass at night.
        /// </summary>
        private static string DetailGrassMaterial()
        {
            const string src = "Assets/Art/Materials/Models/Plants/MAT_grass_bermuda_01.mat";
            const string dst = "Assets/Art/Terrain/MAT_detail_grass.mat";
            var s = AssetDatabase.LoadAssetAtPath<Material>(src);
            if (s == null) return src;
            var m = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (m == null) { m = new Material(s) { name = "MAT_detail_grass" }; AssetDatabase.CreateAsset(m, dst); }
            else { m.shader = s.shader; m.CopyPropertiesFromMaterial(s); }
            m.SetFloat("_SmoothnessRemapMin", 0.05f);
            m.SetFloat("_SmoothnessRemapMax", 0.32f);
            m.SetFloat("_Smoothness", 0.3f);
            m.SetFloat("_SpecularOcclusionMode", 1f);
            // review 2: tall grass measured RGB 0.297/0.299/0.065 (hue 60 deg): pulled to a tropical green
            Color bc = s.HasProperty("_BaseColor") ? s.GetColor("_BaseColor") : Color.white;
            m.SetColor("_BaseColor", new Color(bc.r * 0.72f, bc.g * 1.04f, bc.b * 1.2f, bc.a));   // fix round: hue 82 sat 0.83 -> hue 90-100 sat 0.60-0.68 (blue lifts the floor of the yellow-green; x1.45 went teal, hue 117)
            EditorUtility.SetDirty(m);
            return dst;
        }

        /// <summary>
        /// Round 4 (review 3, GoldenDay_valley_eye: the tall grass read as a planted crop, one saturated emerald blade
        /// card at one height): three tall-grass looks, each a recoloured copy of the grass atlas on its own material.
        /// 0 = the main stand, pulled back to about 0.45 saturation with lighter tips; 1 = dry seed heads with straw
        /// tips; 2 = a darker, bluer green. Only the tall sprites' texels change; t runs from the blade base to the tip.
        /// </summary>
        private static string TallGrassMaterial(string baseMat, int look)
        {
            string[] names = { "MAT_detail_grass_tall", "MAT_detail_grass_tall_dry", "MAT_detail_grass_tall_dark" };
            string dst = $"Assets/Art/Terrain/{names[look]}.mat";
            var s = AssetDatabase.LoadAssetAtPath<Material>(baseMat);
            if (s == null) return baseMat;
            var src = s.GetTexture("_BaseColorMap") as Texture2D;
            if (src == null) return baseMat;
            int w = Mathf.Min(src.width, 2048), h = Mathf.Min(src.height, 2048);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var rd = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            rd.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            rd.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = rd.GetPixels();
            Object.DestroyImmediate(rd);
            foreach (Vector4 sp in TallSprites)
            {
                int x0 = Mathf.FloorToInt(sp.x * w), x1 = Mathf.CeilToInt(sp.z * w), y0 = Mathf.FloorToInt(sp.y * h), y1 = Mathf.CeilToInt(sp.w * h);
                for (int y = Mathf.Max(0, y0); y < Mathf.Min(h, y1); y++)
                {
                    float t = Mathf.InverseLerp(y0, y1, y);
                    for (int x = Mathf.Max(0, x0); x < Mathf.Min(w, x1); x++)
                    {
                        int i = y * w + x;
                        Color c = px[i];
                        if (c.a < 0.02f) continue;
                        Color.RGBToHSV(c, out float hh, out float ss, out float vv);
                        if (look == 0)
                        {
                            float tip = SS(0.55f, 1f, t);
                            // r5 fix round: the material tint is neutral now, so the texels carry the colour: a plain
                            // mid green (hue pulled to 97 deg) at x0.38 saturation, lighter, straw-tinted tips
                            // (measured: a 0.5 pull left the stand straw-yellow at hue 57 in GoldenDay_valley_eye: 0.85 to 97 deg)
                            hh = Mathf.Lerp(Mathf.Lerp(hh, 0.27f, 0.85f), 0.17f, 0.30f * tip);
                            ss *= 0.38f * (1f - 0.30f * tip);
                            vv = Mathf.Min(1f, vv * (1f + 0.35f * tip));
                        }
                        else if (look == 1)
                        {
                            // round 5 (r4 critic: saturated gold over whole blades read as dying maize): only the
                            // top of the blade turns to a pale, desaturated seed head
                            float tip = SS(0.65f, 0.95f, t);
                            hh = Mathf.Lerp(Mathf.Lerp(hh, 0.16f, 0.4f), 0.10f, tip);
                            ss = Mathf.Lerp(ss * 0.5f, 0.30f, tip);
                            vv = Mathf.Min(1f, Mathf.Lerp(vv, Mathf.Max(vv * 1.3f, 0.62f), tip));
                        }
                        else
                        {
                            hh = Mathf.Lerp(Mathf.Repeat(hh + 0.035f, 1f), 0.29f, 0.8f);
                            vv *= 0.72f;
                            ss *= 0.42f;      // r5 fix round: neutral tint, the saturation from the texels
                        }
                        Color o = Color.HSVToRGB(hh, Mathf.Clamp01(ss), Mathf.Clamp01(vv));
                        o.a = c.a;
                        px[i] = o;
                    }
                }
            }
            string texPath = $"Assets/Art/Terrain/{names[look]}_Tex.asset";
            // round 5 (r4 critic: three 22 MB uncompressed readable atlases): built fresh, BC7, no CPU copy, and
            // copied into the existing asset so its GUID (and the material's reference) stays
            var nt = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            nt.SetPixels(px);
            nt.Apply(true, false);
            EditorUtility.CompressTexture(nt, TextureFormat.BC7, TextureCompressionQuality.Normal);
            nt.name = System.IO.Path.GetFileNameWithoutExtension(texPath);      // CopySerialized copies the name too
            nt.Apply(false, true);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { AssetDatabase.CreateAsset(nt, texPath); tex = nt; }
            else { EditorUtility.CopySerialized(nt, tex); Object.DestroyImmediate(nt); EditorUtility.SetDirty(tex); }

            var m = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (m == null) { m = new Material(s) { name = names[look] }; AssetDatabase.CreateAsset(m, dst); }
            else { m.shader = s.shader; m.CopyPropertiesFromMaterial(s); }
            m.SetTexture("_BaseColorMap", tex);
            // the shared grass tint (x0.72 red, x1.2 blue) would turn straw green: the dry stand gets a near-neutral one
            if (look != 1)
                // r5 fix round: the (0.84, 1, 1.12) tint turned the stand a cold grey-teal (its blue lift); a neutral tint
                // (critic's suggestion) left it khaki-olive (measured hue 60, G/R 1.00 in GoldenDay_valley_eye): a red cut
                // with no blue lift gives a plain green, the texels keep the low saturation
                m.SetColor("_BaseColor", new Color(0.76f, 1.0f, 0.84f, 1f));
            else
            {
                var g = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Models/Plants/MAT_grass_bermuda_01.mat");
                Color bc = g != null && g.HasProperty("_BaseColor") ? g.GetColor("_BaseColor") : Color.white;
                m.SetColor("_BaseColor", new Color(bc.r * 0.94f, bc.g, bc.b * 1.0f, bc.a));
            }
            EditorUtility.SetDirty(m);
            return dst;
        }

        /// <summary>Green copy of the fern material for the ground-cover fern cards (same reason as the grass).</summary>
        private static string DetailFernMaterial()
        {
            const string src = "Assets/Art/Materials/Models/Plants/MAT_fern_02.mat";
            const string dst = "Assets/Art/Terrain/MAT_detail_fern.mat";
            var s = AssetDatabase.LoadAssetAtPath<Material>(src);
            if (s == null) return src;
            var m = AssetDatabase.LoadAssetAtPath<Material>(dst);
            if (m == null) { m = new Material(s) { name = "MAT_detail_fern" }; AssetDatabase.CreateAsset(m, dst); }
            else { m.shader = s.shader; m.CopyPropertiesFromMaterial(s); }
            Color bc = s.HasProperty("_BaseColor") ? s.GetColor("_BaseColor") : Color.white;
            m.SetColor("_BaseColor", new Color(bc.r * 0.76f, bc.g, bc.b * 0.84f, bc.a));
            EditorUtility.SetDirty(m);
            return dst;
        }

        /// <summary>Builds the card prefabs and paints the detail layers of the main tile.</summary>
        public static string Paint(Terrain main, float[,] structureMap, (float x, float z, float r)[] tallGrassPatches)
        {
            string grassMat = DetailGrassMaterial();
            string fernMat = DetailFernMaterial();
            GameObject tuft = CardPrefab("PF_detail_grass_tuft", CardClump("detail_grass_tuft", TuftSprites, 3, 0.34f, 0.10f, 0.0f, 3), grassMat);
            GameObject tall = CardPrefab("PF_detail_grass_tall", CardClump("detail_grass_tall", TallSprites, 4, 0.95f, 0.18f, 0.08f, 5), TallGrassMaterial(grassMat, 0));
            // round 4: dry seed heads stand a little taller, the dark green a little lower (one height read as a crop)
            GameObject tallDry = CardPrefab("PF_detail_grass_tall_dry", CardClump("detail_grass_tall_dry", TallSprites, 4, 1.15f, 0.22f, 0.12f, 13), TallGrassMaterial(grassMat, 1));
            GameObject tallDark = CardPrefab("PF_detail_grass_tall_dark", CardClump("detail_grass_tall_dark", TallSprites, 5, 0.82f, 0.20f, 0.06f, 17), TallGrassMaterial(grassMat, 2));
            GameObject fern = CardPrefab("PF_detail_fern", CardClump("detail_fern", FernSprites, 6, 0.85f, 0.05f, 0.55f, 7), fernMat);
            // short, dense lawn-like tufts that fill the ground between the taller ones (3 cards, 0.15-0.45 m)
            GameObject shortTuft = CardPrefab("PF_detail_grass_short", CardClump("detail_grass_short", TuftSprites, 3, 0.24f, 0.20f, 0.05f, 11), grassMat);

            TerrainData td = main.terrainData;
            td.detailPrototypes = new[]
            {
                new DetailPrototype { prototype = tuft, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.7f, maxHeight = 1.5f, noiseSpread = 0.35f, alignToGround = 0.6f, positionJitter = 1f, density = 1f },
                new DetailPrototype { prototype = tall, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    // (r5 fix round: a wide height range in large clumps, so the stand is not a flat-topped hedge)
                    minWidth = 0.7f, maxWidth = 1.4f, minHeight = 0.5f, maxHeight = 1.9f, noiseSpread = 0.2f, alignToGround = 0.2f, positionJitter = 1f, density = 1f },
                new DetailPrototype { prototype = fern, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f, maxWidth = 1.6f, minHeight = 0.8f, maxHeight = 1.5f, noiseSpread = 0.5f, alignToGround = 0.5f, positionJitter = 1f, density = 1f },
                new DetailPrototype { prototype = shortTuft, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f, maxWidth = 1.6f, minHeight = 0.7f, maxHeight = 2.0f, noiseSpread = 0.4f, alignToGround = 0.8f, positionJitter = 1f, density = 1f },
                new DetailPrototype { prototype = tallDry, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.7f, maxWidth = 1.3f, minHeight = 0.55f, maxHeight = 1.3f, noiseSpread = 0.7f, alignToGround = 0.2f, positionJitter = 1f, density = 1f },
                new DetailPrototype { prototype = tallDark, usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.45f, maxHeight = 1.7f, noiseSpread = 0.25f, alignToGround = 0.2f, positionJitter = 1f, density = 1f },
            };
            const int res = 1024;
            td.SetDetailResolution(res, 32);
            td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            var tuftMap = new int[res, res]; var tallMap = new int[res, res]; var fernMap = new int[res, res]; var shortMap = new int[res, res];
            var dryMap = new int[res, res]; var darkMap = new int[res, res];
            Vector3 o = main.transform.position; Vector3 size = td.size;
            int amRes = td.alphamapResolution;
            float[,,] splat = td.GetAlphamaps(0, 0, amRes, amRes);
            // fresh structure mask (everything zones 1-4 built, incl. the coast road): no grass through slabs or roads
            Physics.SyncTransforms();
            int mask = Cretacea.Core.GameLayers.Mask(Cretacea.Core.GameLayers.Default) | Cretacea.Core.GameLayers.Mask(Cretacea.Core.GameLayers.Environment) | Cretacea.Core.GameLayers.Mask(Cretacea.Core.GameLayers.Door);
            var hits = new Collider[8];
            var blocked = new bool[amRes, amRes];
            for (int az = 0; az < amRes; az++)
            {
                float wz = o.z + (az + 0.5f) / amRes * size.z;
                for (int ax = 0; ax < amRes; ax++)
                {
                    float wx = o.x + (ax + 0.5f) / amRes * size.x;
                    if (Z4.DistanceOutsidePlayArea(wx, wz) > 2f) continue;
                    float wy = Z4.EvaluateWorldElevation(wx, wz);
                    int n = Physics.OverlapBoxNonAlloc(new Vector3(wx, wy + 0.5f, wz), new Vector3(1.2f, 0.6f, 1.2f), hits, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < n; i++)
                    {
                        if (hits[i] is TerrainCollider || hits[i].GetComponentInParent<LODGroup>() != null) continue;
                        blocked[az, ax] = true;
                        break;
                    }
                }
            }
            // round 4: road and bank proximity (bare ground blurred over +-2 cells, ~4 m), so the tall stands start to
            // thin about 2.5 m from the gravel instead of standing at full height to a straight front
            var bareBlur = new float[amRes, amRes];
            {
                var tmp = new float[amRes, amRes];
                for (int az = 0; az < amRes; az++)
                    for (int ax = 0; ax < amRes; ax++)
                    {
                        float sum = 0f; int cnt = 0;
                        for (int d = -2; d <= 2; d++)
                        {
                            int xx = ax + d; if (xx < 0 || xx >= amRes) continue;
                            sum += splat[az, xx, LookTerrainLayers.Gravel] + splat[az, xx, LookTerrainLayers.Mud] + splat[az, xx, LookTerrainLayers.Sand];
                            cnt++;
                        }
                        tmp[az, ax] = sum / cnt;
                    }
                for (int az = 0; az < amRes; az++)
                    for (int ax = 0; ax < amRes; ax++)
                    {
                        float sum = 0f; int cnt = 0;
                        for (int d = -2; d <= 2; d++)
                        {
                            int zz = az + d; if (zz < 0 || zz >= amRes) continue;
                            sum += tmp[zz, ax]; cnt++;
                        }
                        bareBlur[az, ax] = sum / cnt;
                    }
            }
            long counts = 0;
            for (int iz = 0; iz < res; iz++)
            {
                float z = o.z + (iz + 0.5f) / res * size.z;
                int az = Mathf.Clamp((int)((iz + 0.5f) / res * amRes), 0, amRes - 1);
                for (int ix = 0; ix < res; ix++)
                {
                    float x = o.x + (ix + 0.5f) / res * size.x;
                    float outside = Z4.DistanceOutsidePlayArea(x, z);
                    if (outside > 90f) continue;
                    int ax = Mathf.Clamp((int)((ix + 0.5f) / res * amRes), 0, amRes - 1);
                    if (blocked[az, ax]) continue;      // never through slabs, floors or roads
                    float grass = splat[az, ax, LookTerrainLayers.Grass];
                    float moss = splat[az, ax, LookTerrainLayers.Moss];
                    float floorW = splat[az, ax, LookTerrainLayers.Floor];
                    float bare = splat[az, ax, LookTerrainLayers.Gravel] + splat[az, ax, LookTerrainLayers.Mud] + splat[az, ax, LookTerrainLayers.Sand]
                                 + splat[az, ax, LookTerrainLayers.Rock] + splat[az, ax, LookTerrainLayers.MossyRock];
                    // reeds and sedge along the Tyrant Valley stream (they stand in the wet bank mud too)
                    float streamReed = 0f;
                    if (x > 150f && x < 262f && Mathf.Abs(z) < 178f)
                    {
                        float sd = Mathf.Abs(x - Z4.EvaluateStreamX(z));
                        float rn = Mathf.PerlinNoise(x * 0.17f + 2.3f, z * 0.17f + 6.1f);
                        streamReed = SS(5.2f, 6.4f, sd) * (1f - SS(8.5f, 11f, sd + (rn - 0.5f) * 3f)) * SS(0.28f, 0.6f, rn);   // the water reaches ~6 m from the centre line
                    }
                    if (bare > 0.55f)
                    {
                        if (streamReed > 0f) tallMap[iz, ix] = Mathf.FloorToInt(streamReed * 6f + Mathf.Abs(Mathf.Sin(ix * 12.9898f + iz * 78.233f) * 43758.5453f) % 1f);
                        continue;
                    }
                    if (structureMap != null && structureMap[az, ax] > 0.2f) grass *= 0.35f;

                    float n1 = Mathf.PerlinNoise(x * 0.09f + 3.1f, z * 0.09f + 7.7f);
                    float n2 = Mathf.PerlinNoise(x * 0.023f + 11.3f, z * 0.023f + 1.9f);
                    float h = Mathf.Abs(Mathf.Sin(ix * 12.9898f + iz * 78.233f) * 43758.5453f) % 1f;   // per-cell hash

                    // short tufts over grass (patchy), fading out at distance from the play area
                    float tuftD = (grass * 1.1f + moss * 0.5f) * (0.35f + 0.9f * SS(0.3f, 0.75f, n1 * 0.6f + n2 * 0.4f)) * (1f - SS(40f, 90f, outside));
                    tuftD *= 0.55f + 0.45f * SS(0.15f, 0.55f, n2 + (n1 - 0.5f) * 0.5f);    // bald and thick swathes
                    // the dawn ending looks east over the overlook rail: keep its foreground low
                    bool endingView = x > 538f && Mathf.Abs(z + 46f) < 26f;
                    if (endingView) tuftD *= 0.45f;
                    int nt = Mathf.FloorToInt(tuftD * 6.0f + h);
                    tuftMap[iz, ix] = nt;
                    // short tufts carpet the grass between them (denser where the taller ones thin out)
                    float shortD = (grass * 1.2f + moss * 0.4f + floorW * 0.25f) * (0.55f + 0.6f * (1f - SS(0.35f, 0.8f, n1))) * (1f - SS(40f, 80f, outside));
                    shortMap[iz, ix] = Mathf.FloorToInt(shortD * (endingView ? 2f : 5f) + h);

                    // tall grass: the stealth patches, plus clumps along meadow and forest edges
                    float tallD = 0f;
                    float edgeN = Mathf.PerlinNoise(x * 0.35f + 9.1f, z * 0.35f + 4.7f) * 0.65f + Mathf.PerlinNoise(x * 1.1f + 2.2f, z * 1.1f + 8.8f) * 0.35f;
                    foreach (var p in tallGrassPatches)
                    {
                        // the patch outline wanders +-2 m and fades over ~2 m (a sharp circle read as a hedge wall)
                        float d = Vector2.Distance(new Vector2(x, z), new Vector2(p.x, p.z)) + (edgeN - 0.5f) * 4f;
                        tallD = Mathf.Max(tallD, 1f - SS(p.r - 2.0f, p.r + 1.0f, d));
                    }
                    float patchD = tallD;
                    if (tallD > 0f) tuftMap[iz, ix] = Mathf.FloorToInt(nt * 0.5f);
                    // irregular density inside the patches (no planted-crop look): noise thins and thickens them
                    tallD *= 0.55f + 0.6f * SS(0.25f, 0.75f, Mathf.PerlinNoise(x * 0.21f + 5.5f, z * 0.21f + 2.2f));
                    tallD = Mathf.Max(tallD, grass * SS(0.68f, 0.80f, n2) * 0.55f);
                    // Titan Meadows: noise-gated blobs of tall grass across the open meadow (uneven cover, not a lawn)
                    float md = Vector2.Distance(new Vector2(x, z), new Vector2(-55f, -175f));
                    if (md < 95f)
                    {
                        float blob = SS(0.50f, 0.62f, Mathf.PerlinNoise(x * 0.075f + 21.7f, z * 0.075f + 13.9f) * 0.7f + n1 * 0.3f);
                        tallD = Mathf.Max(tallD, blob * (grass + moss * 0.5f) * 0.75f * (1f - SS(70f, 95f, md)));
                    }
                    // tall grass thins out over the last ~2 m towards a gravel road or a bare bank (it stood at full
                    // height right to the road edge: two hedge walls along the valley road)
                    tallD *= 1f - SS(0.06f, 0.45f, bare + (edgeN - 0.5f) * 0.3f) * 0.85f;
                    // round 4: and from ~2.5 m out the stand already breaks up (wandering front, outer metre in tufts)
                    // (r5 fix round: the front wanders 1.5-3 m instead of following the gravel edge)
                    float nearRoad = SS(0.04f, 0.45f, bareBlur[az, ax] + (edgeN - 0.5f) * 0.6f);
                    if (nearRoad > 0.15f && h < 0.6f) tuftMap[iz, ix] += 1;
                    tallD *= 1f - nearRoad * 0.9f;      // round 5: no straight front along the gravel
                    // fringe of every tall stand: part of the tall cards become tufts, so the height steps down
                    if (tallD > 0.05f && tallD < 0.55f && h < 0.5f) { tuftMap[iz, ix] += 2; tallD *= 0.5f; }
                    // reeds round the Titan Meadows lake break up its round shoreline
                    float lakeD = Vector2.Distance(new Vector2(x, z), Z4.LakeCentre);
                    if (lakeD < 75f)
                    {
                        float gy = Z4.EvaluateWorldElevation(x, z);
                        float reed = 1f - SS(0.15f, 0.75f, Mathf.Abs(gy - Z4.LakeLevel - 0.25f));
                        reed *= SS(0.25f, 0.55f, n1 * 0.7f + n2 * 0.3f);
                        // dense reed beds on about a third of the rim (the north-west side), sparse elsewhere
                        float la = Mathf.Atan2(z - Z4.LakeCentre.y, x - Z4.LakeCentre.x);
                        float bed = Mathf.Cos(la - 2.3f);
                        reed *= Mathf.Lerp(0.12f, 1f, SS(0.35f, 0.65f, bed));
                        tallD = Mathf.Max(tallD, reed);
                    }
                    tallD = Mathf.Max(tallD, streamReed);
                    if (endingView) tallD = 0f;
                    int nl = Mathf.FloorToInt(tallD * 7f + h);
                    // round 4: the stand mixes three looks, chosen by noise (dry seed heads ~22 %, dark green ~25 %),
                    // in drifts rather than per blade. Reeds by the water stay the main look.
                    float dryF = SS(0.40f, 0.75f, Mathf.PerlinNoise(x * 0.11f + 31.7f, z * 0.11f + 8.3f)) * 0.55f;
                    float darkF = SS(0.38f, 0.72f, Mathf.PerlinNoise(x * 0.085f + 12.9f, z * 0.085f + 44.1f)) * 0.55f;
                    if (streamReed > 0f || lakeD < 75f) { dryF *= 0.3f; }
                    float h2 = Mathf.Abs(Mathf.Sin(ix * 39.346f + iz * 11.135f) * 24634.6345f) % 1f;
                    int nDry = Mathf.FloorToInt(nl * dryF + h2);
                    nDry = Mathf.Min(nDry, nl);
                    int nDark = Mathf.Min(nl - nDry, Mathf.FloorToInt(nl * darkF + (1f - h2)));
                    dryMap[iz, ix] = nDry;
                    darkMap[iz, ix] = nDark;
                    tallMap[iz, ix] = nl - nDry - nDark;

                    // ferns: forest floor and moss, banks
                    float fernD = (floorW * 0.8f + moss * 0.6f) * SS(0.45f, 0.75f, n1) * 0.9f;
                    // Talon pen: ferns and ground cover over the trampled floor (details only: no colliders, the
                    // Control Room sightline and the raptor NavMesh stay as they are)
                    bool inPen = x > -88f && x < 18f && z > 44f && z < 126f;
                    if (inPen) fernD = Mathf.Max(fernD, (0.35f + 0.6f * SS(0.35f, 0.7f, n1)) * (0.5f + floorW));
                    fernD = Mathf.Max(fernD, patchD * SS(0.55f, 0.8f, n1) * 0.7f);     // ferns mixed into the stealth patches
                    fernMap[iz, ix] = endingView ? 0 : Mathf.FloorToInt(fernD * 1.3f + h * 0.9f);
                    counts += tuftMap[iz, ix] + tallMap[iz, ix] + fernMap[iz, ix] + shortMap[iz, ix] + dryMap[iz, ix] + darkMap[iz, ix];
                }
            }
            td.SetDetailLayer(0, 0, ProtoTuft, tuftMap);
            td.SetDetailLayer(0, 0, ProtoTall, tallMap);
            td.SetDetailLayer(0, 0, ProtoFern, fernMap);
            td.SetDetailLayer(0, 0, ProtoShort, shortMap);
            td.SetDetailLayer(0, 0, ProtoTallDry, dryMap);
            td.SetDetailLayer(0, 0, ProtoTallDark, darkMap);
            main.detailObjectDistance = 60f;     // ground cover is the biggest triangle cost (~1.8 M at 80 m); 70 m cost +0.25 M tris in the valley
            main.detailObjectDensity = 1f;
            EditorUtility.SetDirty(td);
            return $"details: {counts} instances (tufts, short tufts, tall grass, ferns)";
        }
    }
}
