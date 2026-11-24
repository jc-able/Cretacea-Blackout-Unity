using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Far LOD for trees: two crossed quads whose texture is the tree itself, rasterised on the CPU from its LOD1 mesh
    /// (orthographic front and side views, albedo from the material's base map, alpha tested like the leaves). Mip levels
    /// keep the alpha-tested coverage of the full-size image, so distant hillsides stay as dense as near ones.
    /// 4 triangles per tree instead of 100-340 for the old vertex-clustered blob.
    /// </summary>
    public static class LookVegetationImpostors
    {
        private const int ViewW = 256, ViewH = 512, AtlasW = ViewW * 3;
        private const float Cutoff = 0.5f;
        /// <summary>Crossed cards first (2 quads = 8 vertices / 12 indices), then the crown cap (CapRing rim points).
        /// Fix round (r5 critic): the 45 deg cards reused the 0/90 deg views, so every crown showed doubled silhouettes
        /// (8-spoke stars from the air, a fence of slabs from eye level): back to two cards.</summary>
        private const int CardVerts = 8, CardIndices = 12, CapRing = 12;
        // bark tone of the prefab impostor (open valley, lawn: matches the pale mesh trunks next to it) and of the dense
        // hill copy (under a closed canopy brighter trunks read as pale streaks), both in linear
        private const float BarkOpen = 1.0f, BarkDense = 0.45f;     // (fix round: x0.80 still left the far trunks darker than the sunlit LOD2 trunks)
        // leaf texels never brighter / more saturated than this before the self-shade (review 2 fix round: one global
        // x1.34 / x1.22 boost made the palm and tree-fern caps neon lime, value 0.71 at saturation 0.75)
        private const float LeafMaxValue = 0.50f, LeafMaxSat = 0.62f;
        /// <summary>Hill copies of the impostor: hue shift (deg), value factor, mirrored atlas.</summary>
        private static readonly (float hue, float val, bool mirror)[] Variants = { (0f, 1.0f, false), (-6f, 0.88f, false), (6f, 1.08f, true) };
        public const int VariantCount = 3;

        /// <summary>
        /// Extra leaf value / saturation of the dense hill copies: a closed canopy is seen from above, sunlit leaf tops
        /// with no self-shadowing crown under them, so it is matched to the lawn (canopy/lawn 0.85-1.0) instead of to a
        /// single LOD2 tree that shades itself.
        /// </summary>
        private static (float val, float sat) DenseBoost(string stem) => stem == "jungle_tree" ? (1.49f, 1.28f) : (1f, 1f);

        /// <summary>
        /// Leaf value / saturation factors per species, so each impostor matches its own mesh LOD in the same light
        /// (jungle_tree's leaf atlas is dark, 0.31-0.36; the palm and tree-fern atlases are already bright).
        /// </summary>
        private static (float val, float sat) LeafFactors(string stem)
        {
            switch (stem)
            {
                case "jungle_tree": return (0.90f, 0.95f);     // measured (forced LOD2 vs impostor, same view): x1.10 was 24 % brighter
                case "tree_fern": return (0.88f, 1.05f);
                default: return (1.0f, 1.0f);      // palm_tree, palm_tree_b and anything else
            }
        }

        private sealed class MatSample
        {
            public Color32[] px; public int w, h; public Color tint; public float cutoff;
        }

        private static readonly Dictionary<Texture, (Color32[] px, int w, int h)> TexCache = new Dictionary<Texture, (Color32[], int, int)>();

        private static (Color32[] px, int w, int h) Readback(Texture tex)
        {
            if (TexCache.TryGetValue(tex, out var c)) return c;
            int w = Mathf.Min(tex.width, 1024), h = Mathf.Min(tex.height, 1024);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t2 = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            t2.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t2.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var res = (t2.GetPixels32(), w, h);
            Object.DestroyImmediate(t2);
            TexCache[tex] = res;
            return res;
        }

        private static MatSample Sample(Material m)
        {
            var s = new MatSample { tint = Color.white, cutoff = -1f };
            if (m == null) return s;
            if (m.HasProperty("_BaseColor")) s.tint = m.GetColor("_BaseColor");
            if (m.HasProperty("_AlphaCutoffEnable") && m.GetFloat("_AlphaCutoffEnable") > 0.5f) s.cutoff = m.HasProperty("_AlphaCutoff") ? m.GetFloat("_AlphaCutoff") : 0.5f;
            Texture tex = m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
            if (tex != null) { var r = Readback(tex); s.px = r.px; s.w = r.w; s.h = r.h; }
            return s;
        }

        /// <summary>Builds (or rebuilds in place) the impostor mesh + material of a tree from its LOD1 renderers.</summary>
        public static Renderer Build(string stem, Transform root, Renderer[] source, string meshFolder, string matFolder)
        {
            // gather triangles in prefab-root space
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); var triMat = new List<int>();
            var mats = new List<MatSample>();
            Material leafMat = null;
            foreach (Renderer r in source)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh m = mf.sharedMesh;
                Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                int baseIndex = verts.Count;
                Vector3[] v = m.vertices; Vector2[] uv = m.uv; Vector3[] vn = m.normals;
                for (int i = 0; i < v.Length; i++)
                {
                    verts.Add(toRoot.MultiplyPoint3x4(v[i]));
                    norms.Add(vn.Length == v.Length ? toRoot.MultiplyVector(vn[i]).normalized : Vector3.up);
                    uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                }
                Material[] sm = r.sharedMaterials;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    Material mat = s < sm.Length ? sm[s] : null;
                    MatSample ms = Sample(mat);
                    if (ms.cutoff > 0f && leafMat == null) leafMat = mat;
                    int mi = mats.Count; mats.Add(ms);
                    int[] t = m.GetTriangles(s);
                    for (int i = 0; i < t.Length; i++) tris.Add(baseIndex + t[i]);
                    for (int i = 0; i < t.Length / 3; i++) triMat.Add(mi);
                }
            }
            if (verts.Count == 0) return null;
            var b = new Bounds(verts[0], Vector3.zero);
            foreach (Vector3 p in verts) b.Encapsulate(p);

            var px = new Color[AtlasW * ViewH];
            var nrm = new Vector3[AtlasW * ViewH];
            var depth = new float[AtlasW * ViewH];
            for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;
            var bark = new bool[AtlasW * ViewH];
            var lf = LeafFactors(stem);
            Rasterise(verts, norms, uvs, tris, triMat, mats, b, px, nrm, depth, bark, lf, 0);
            Rasterise(verts, norms, uvs, tris, triMat, mats, b, px, nrm, depth, bark, lf, 1);
            Rasterise(verts, norms, uvs, tris, triMat, mats, b, px, nrm, depth, bark, lf, 2);
            // the crown cap sits at the mean height of the crown's top surface (seen from above)
            double capSum = 0; int capN = 0;
            for (int y = 0; y < ViewW; y++)
                for (int x = 0; x < ViewW; x++)
                {
                    int i = y * AtlasW + 2 * ViewW + x;
                    if (px[i].a > 0f) { capSum += -depth[i]; capN++; }
                }
            float capY = capN > 0 ? (float)(capSum / capN) : Mathf.Lerp(b.min.y, b.max.y, 0.8f);
            Color[] normalPx = BuildCrownNormals(px, nrm);
            Material template = leafMat != null ? leafMat : source[0].sharedMaterial;

            // hill copies first (they start from the undilated atlas): darker bark, hue / value variants, one mirrored
            for (int vi = 0; vi < Variants.Length; vi++)
            {
                Color[] vpx = MakeVariant(px, bark, Variants[vi].hue, Variants[vi].val, Variants[vi].mirror, DenseBoost(stem));
                Color[] vn = Variants[vi].mirror ? MirrorNormals(normalPx) : normalPx;
                Dilate(vpx);
                Texture2D vt = SaveTexture($"{meshFolder}/{stem}_Impostor_V{vi}_Tex.asset", vpx);
                Texture2D vnt = Variants[vi].mirror ? SaveNormalTexture($"{meshFolder}/{stem}_Impostor_V{vi}_Normal.asset", vn) : null;
                s_pendingVariants.Add((vi, vt, vnt));
            }

            OpenValleyGrade(px, bark, stem);
            PaintGroundBlob(px);
            Dilate(px);
            Texture2D tex = SaveTexture($"{meshFolder}/{stem}_Impostor_Tex.asset", px);
            Texture2D ntex = SaveNormalTexture($"{meshFolder}/{stem}_Impostor_Normal.asset", normalPx);
            Mesh mesh = SaveMesh($"{meshFolder}/{stem}_Impostor.asset", b, capY);
            Material imat = SaveMaterial($"{matFolder}/MAT_{stem}_Impostor.mat", template, tex, ntex);
            foreach (var pv in s_pendingVariants)
                SaveMaterial($"{matFolder}/MAT_{stem}_Impostor_V{pv.vi}.mat", template, pv.tex, pv.ntex != null ? pv.ntex : ntex);
            s_pendingVariants.Clear();

            var go = new GameObject(stem + "_Impostor");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = imat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            mr.staticShadowCaster = true;
            return mr;
        }

        /// <summary>
        /// view 0: looking along +Z (image = X,Y); view 1: looking along +X (image = Z,Y); view 2: looking straight down
        /// (image = X,Z, a ViewW x ViewW square in the third atlas column) for the horizontal crown cap.
        /// </summary>
        private static void Rasterise(List<Vector3> v, List<Vector3> vn, List<Vector2> uv, List<int> tris, List<int> triMat, List<MatSample> mats, Bounds b, Color[] px, Vector3[] nrm, float[] depth, bool[] bark, (float val, float sat) lf, int view)
        {
            float minU = view == 1 ? b.min.z : b.min.x, sizeU = Mathf.Max(0.01f, view == 1 ? b.size.z : b.size.x);
            float minV = view == 2 ? b.min.z : b.min.y, sizeV = Mathf.Max(0.01f, view == 2 ? b.size.z : b.size.y);
            int ox = view * ViewW;
            int rows = view == 2 ? ViewW : ViewH;
            var sp = new Vector3[3];
            var st = new Vector2[3];
            var sn = new Vector3[3];
            for (int t = 0; t < tris.Count / 3; t++)
            {
                MatSample ms = mats[triMat[t]];
                for (int k = 0; k < 3; k++)
                {
                    Vector3 p = v[tris[t * 3 + k]];
                    float u = view == 1 ? p.z : p.x;
                    float vv = view == 2 ? p.z : p.y;
                    float d = view == 0 ? p.z : (view == 1 ? p.x : -p.y);
                    sp[k] = new Vector3((u - minU) / sizeU * ViewW, (vv - minV) / sizeV * rows, d);
                    st[k] = uv[tris[t * 3 + k]];
                    sn[k] = vn[tris[t * 3 + k]];
                }
                float area = (sp[1].x - sp[0].x) * (sp[2].y - sp[0].y) - (sp[2].x - sp[0].x) * (sp[1].y - sp[0].y);
                if (Mathf.Abs(area) < 1e-6f) continue;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(sp[0].x, Mathf.Min(sp[1].x, sp[2].x))));
                int x1 = Mathf.Min(ViewW - 1, Mathf.CeilToInt(Mathf.Max(sp[0].x, Mathf.Max(sp[1].x, sp[2].x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(sp[0].y, Mathf.Min(sp[1].y, sp[2].y))));
                int y1 = Mathf.Min(rows - 1, Mathf.CeilToInt(Mathf.Max(sp[0].y, Mathf.Max(sp[1].y, sp[2].y))));
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float fx = x + 0.5f, fy = y + 0.5f;
                        float w0 = ((sp[1].x - fx) * (sp[2].y - fy) - (sp[2].x - fx) * (sp[1].y - fy)) / area;
                        float w1 = ((sp[2].x - fx) * (sp[0].y - fy) - (sp[0].x - fx) * (sp[2].y - fy)) / area;
                        float w2 = 1f - w0 - w1;
                        if (w0 < -0.02f || w1 < -0.02f || w2 < -0.02f) continue;
                        float z = w0 * sp[0].z + w1 * sp[1].z + w2 * sp[2].z;
                        int idx = y * AtlasW + ox + x;
                        if (z >= depth[idx]) continue;
                        Color c = ms.tint;
                        if (ms.px != null)
                        {
                            Vector2 tuv = st[0] * w0 + st[1] * w1 + st[2] * w2;
                            int tx = (int)(Mathf.Repeat(tuv.x, 1f) * ms.w) % ms.w, ty = (int)(Mathf.Repeat(tuv.y, 1f) * ms.h) % ms.h;
                            Color32 s = ms.px[ty * ms.w + tx];
                            if (ms.cutoff > 0f && s.a / 255f < ms.cutoff) continue;
                            c = new Color(s.r / 255f * ms.tint.r, s.g / 255f * ms.tint.g, s.b / 255f * ms.tint.b, 1f);
                        }
                        // light baked self-shading only (review 2: the old x0.40 'canopy grade' made the far forest
                        // a burnt khaki mass, darker than the mesh LODs next to it): the far side and the underside
                        // of the crown a little darker, lighting and haze do the rest
                        float shade;
                        if (view == 2)
                            // the cap faces the sun and casts no shadow: kept below the lawn's brightness
                            shade = Mathf.Lerp(0.85f, 0.70f, Mathf.InverseLerp(-b.max.y, -b.min.y, z));
                        else
                        {
                            float deep = Mathf.InverseLerp(view == 0 ? b.min.z : b.min.x, view == 0 ? b.max.z : b.max.x, z);
                            shade = Mathf.Lerp(1.0f, 0.88f, deep) * Mathf.Lerp(0.90f, 1.0f, fy / rows);
                        }
                        // leaf or bark: alpha-tested leaf cards are leaves; the solid tropical atlases hold both, so
                        // their texels are told apart by colour (green-yellow and saturated = leaf)
                        float cmax = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), cmin = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                        bool leafTexel = ms.cutoff > 0f || (c.g >= cmax - 1e-4f && c.g >= c.r * 0.92f && cmax > 0.02f && (cmax - cmin) / cmax > 0.18f);
                        // trunks sit in the crown's shade (brighter trunks left pale vertical streaks on the far hills)
                        if (!leafTexel) { Color lin = c.linear * BarkOpen; c = lin.gamma; }
                        else
                        {
                            // a quad has no leaf translucency and no sky-lit outer leaves over shaded inner ones, so the
                            // crown is matched to the mesh LODs next to it (measured in GoldenDay_valley_air: mesh crowns
                            // luminance 0.30-0.34 / saturation 0.47, the plain bake 0.22 / 0.31)
                            Color.RGBToHSV(c, out float hh, out float ss, out float vv);
                            // and nudged towards a blue-green (105 deg) so an orange 5 deg sun leaves the hill olive, not tan
                            if (hh > 0.06f && hh < 0.40f) hh += (0.29f - hh) * 0.18f;
                            c = Color.HSVToRGB(hh, Mathf.Min(LeafMaxSat, ss * lf.sat), Mathf.Min(LeafMaxValue, vv * lf.val));
                        }
                        px[idx] = new Color(Mathf.Clamp01(c.r * shade), Mathf.Clamp01(c.g * shade), Mathf.Clamp01(c.b * shade), 1f);
                        depth[idx] = z;
                        bark[idx] = !leafTexel;
                        // surface normal in the quad's tangent frame (x = along the image, y = up the image, z = towards
                        // the viewer; leaves are two-sided, so z is always taken towards the viewer)
                        Vector3 n = (sn[0] * w0 + sn[1] * w1 + sn[2] * w2).normalized;
                        nrm[idx] = view == 0 ? new Vector3(n.x, n.y, Mathf.Abs(n.z)) : (view == 1 ? new Vector3(n.z, n.y, Mathf.Abs(n.x)) : new Vector3(n.x, n.z, Mathf.Abs(n.y)));
                    }
                }
            }
        }

        /// <summary>
        /// Round 4 (review 3, GoldenDay_valley_air: the play-area trees past the LOD switch were grey-olive crowns,
        /// 0.356/0.367/0.239, G/R 1.03, on pale poles, less green than the lawn under them): the open-valley prefab
        /// impostor only (the hill copies are made before this) gets its leaves pulled to the mesh LOD's green and its
        /// trunk into the crown's shade (linear x0.6).
        /// </summary>
        private static void OpenValleyGrade(Color[] px, bool[] bark, string stem)
        {
            if (stem != "jungle_tree") return;
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a <= 0f) continue;
                Color c = px[i];
                if (bark[i]) { Color lin = c.linear * 0.6f; px[i] = new Color(lin.gamma.r, lin.gamma.g, lin.gamma.b, c.a); continue; }
                Color.RGBToHSV(c, out float hh, out float ss, out float vv);
                // round 5 (r4 critic: crown clusters G/R 1.10, hue 77 from the air): pulled harder towards 97 deg
                // (fix round, r5 critic: the k-means crown clusters did not move, the atlas was already at hue 98, G/R 1.8
                // linear, and the warm sun takes 25 deg off it): towards 110 deg, more saturation
                if (hh > 0.04f && hh < 0.40f) hh += (0.305f - hh) * 0.85f;   // towards 110 deg: an orange low sun leaves it green
                px[i] = Color.HSVToRGB(hh, Mathf.Min(0.74f, ss * 1.45f), Mathf.Min(0.62f, vv * 1.06f));
                px[i].a = c.a;
            }
        }

        /// <summary>
        /// Shadows-only copy of the impostor's crown cap for the play-area LODGroup (review 3: the impostor cast no
        /// ground shadow, so the trees past the switch stood without the shadow pools of the mesh trees in front).
        /// Crown-shaped (the alpha-tested top view), no crossed-card shadows.
        /// </summary>
        public static Renderer BuildShadowCrown(string stem, Transform root, Renderer impostor, string meshFolder)
        {
            var mf = impostor != null ? impostor.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return null;
            Mesh src = mf.sharedMesh;
            int[] all = src.triangles;
            if (all.Length <= CardIndices) return null;
            // the crossed cards are the first CardVerts vertices / CardIndices indices; the rest is the cap
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var nrm = new List<Vector3>();
            Vector3[] sv = src.vertices; Vector2[] su = src.uv; Vector3[] sn = src.normals;
            for (int k = CardVerts; k < sv.Length; k++) { verts.Add(sv[k]); uvs.Add(su[k]); nrm.Add(sn[k]); }
            var tris = new List<int>();
            for (int k = CardIndices; k < all.Length; k++) tris.Add(all[k] - CardVerts);
            string path = $"{meshFolder}/{stem}_ImpostorShadow.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            else mesh.Clear();
            mesh.SetVertices(verts); mesh.SetNormals(nrm); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            var go = new GameObject(stem + "_ImpostorShadow");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = impostor.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            mr.receiveShadows = false;
            mr.staticShadowCaster = false;
            return mr;
        }

        /// <summary>
        /// Round 5 (r4 critic: the valley band from the air is 260-520 m out, past the 260 m sun shadow distance, so the
        /// impostor trees there stood without shadow pools): a soft dark ground patch under each crown, painted into the
        /// spare upper half of the atlas's third column. Alpha-tested like the leaves, so its soft edge is a dither whose
        /// density falls off with the radius; the mip coverage pass keeps it as a soft disc far away.
        /// </summary>
        private static void PaintGroundBlob(Color[] px)
        {
            var shade = new Color(BlobColor.r, BlobColor.g, BlobColor.b, 1f);
            int half = ViewW / 2;
            for (int y = ViewW; y < ViewH; y++)
                for (int x = 0; x < ViewW; x++)
                {
                    float dx = (x + 0.5f - half) / half, dy = (y - ViewW + 0.5f - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    // lobed outline, so it is not a perfect disc
                    float ang = Mathf.Atan2(dy, dx);
                    r *= 1f + 0.10f * Mathf.Sin(ang * 3f + 0.7f) + 0.06f * Mathf.Sin(ang * 5f + 2.1f);
                    float density = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.95f, r));
                    float hsh = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f) % 1f;
                    int i = y * AtlasW + 2 * ViewW + x;
                    // darkest under the crown centre
                    float k = Mathf.Lerp(0.75f, 1.15f, Mathf.InverseLerp(0f, 0.9f, r));
                    px[i] = hsh < density ? new Color(shade.r * k, shade.g * k, shade.b * k, 1f) : new Color(0f, 0f, 0f, 0f);
                }
        }

        /// <summary>sRGB albedo of the ground patch: the valley lawn in shade (lit by the sun like the lawn next to it).</summary>
        private static readonly Color BlobColor = new Color(0.085f, 0.105f, 0.045f);

        /// <summary>
        /// The ground patch renderer for the play-area LODGroup's impostor level: one quad just over the tree's base,
        /// 0.8 of the crown footprint, same material as the impostor (instanced with it), no shadows.
        /// </summary>
        public static Renderer BuildGroundBlob(string stem, Transform root, Renderer impostor, string meshFolder)
        {
            var mf = impostor != null ? impostor.GetComponent<MeshFilter>() : null;
            if (mf == null || mf.sharedMesh == null) return null;
            Bounds b = mf.sharedMesh.bounds;
            float rx = b.extents.x * 0.8f, rz = b.extents.z * 0.8f, y = b.min.y + 0.25f;
            Vector3 c = b.center;
            const float u0 = 2f / 3f, u1 = 1f, v0 = 0.5f, v1 = 1f;
            var verts = new List<Vector3> { new Vector3(c.x - rx, y, c.z - rz), new Vector3(c.x + rx, y, c.z - rz), new Vector3(c.x + rx, y, c.z + rz), new Vector3(c.x - rx, y, c.z + rz) };
            var uvs = new List<Vector2> { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            var nrm = new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            var tris = new List<int> { 0, 2, 1, 0, 3, 2 };
            string path = $"{meshFolder}/{stem}_ImpostorBlob.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            else mesh.Clear();
            mesh.SetVertices(verts); mesh.SetNormals(nrm); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            var go = new GameObject(stem + "_ImpostorBlob");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = impostor.sharedMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return mr;
        }

        private static readonly List<(int vi, Texture2D tex, Texture2D ntex)> s_pendingVariants = new List<(int, Texture2D, Texture2D)>();

        /// <summary>
        /// A hill copy of the atlas: bark from the open-valley tone down to the dense tone, leaf texels shifted in hue
        /// and value (rows of identical crown stamps were the 'scale' pattern on the dusk hill), optionally mirrored.
        /// </summary>
        private static Color[] MakeVariant(Color[] src, bool[] bark, float hueDeg, float val, bool mirror, (float val, float sat) boost)
        {
            var o = new Color[src.Length];
            float barkK = BarkDense / BarkOpen;
            for (int y = 0; y < ViewH; y++)
                for (int x = 0; x < AtlasW; x++)
                {
                    int i = y * AtlasW + x;
                    Color c = src[i];
                    if (c.a > 0f)
                    {
                        if (bark[i]) { Color lin = c.linear * barkK; c = lin.gamma; c.a = 1f; }
                        else
                        {
                            Color.RGBToHSV(c, out float h, out float s, out float v);
                            // the hill sits 300-900 m away under a 5-27 deg warm sun: pulled 30 % further towards 110 deg so
                            // it stays green at dusk (measured hue 51-62 there, the review's check is >= 70)
                            if (h > 0.06f && h < 0.40f) h += (110f / 360f - h) * 0.30f;
                            s = boost.sat > 1f ? Mathf.Min(LeafMaxSat, s * boost.sat) : s;
                            v = boost.val > 1f ? Mathf.Min(LeafMaxValue, v * boost.val) : v;
                            c = Color.HSVToRGB(Mathf.Repeat(h + hueDeg / 360f, 1f), s, Mathf.Clamp01(v * val));
                            c.a = 1f;
                        }
                    }
                    int col = x / ViewW, lx = x % ViewW;
                    int dx = mirror ? col * ViewW + (ViewW - 1 - lx) : x;
                    o[y * AtlasW + dx] = c;
                }
            SoftenHillCrowns(o);
            return o;
        }

        /// <summary>
        /// Fix round (r5 critic, goal 1): the hill read as a mosaic of hard-edged lit leaf clumps with dark holes between
        /// them. For the two side views of the hill copies: the coverage is blurred (about 3 px) and re-thresholded with
        /// a dither, so pinholes between leaf clumps close, specks go and the crown edge breaks up instead of standing as
        /// a crisp cut-out; then the leaf colour is pulled 45 % towards its local mean, so one crown is one soft mass
        /// instead of a speckle of sunlit and black leaves.
        /// </summary>
        private static void SoftenHillCrowns(Color[] o)
        {
            var filled = (Color[])o.Clone();
            Dilate(filled);
            for (int view = 0; view < 2; view++)
            {
                int ox = view * ViewW;
                var a = new float[ViewW * ViewH];
                var r = new float[ViewW * ViewH]; var g = new float[ViewW * ViewH]; var bl = new float[ViewW * ViewH];
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++)
                    {
                        Color c = o[y * AtlasW + ox + x];
                        float k = c.a > 0f ? 1f : 0f;
                        a[y * ViewW + x] = k; r[y * ViewW + x] = c.r * k; g[y * ViewW + x] = c.g * k; bl[y * ViewW + x] = c.b * k;
                    }
                float[] ab = BoxBlur(BoxBlur(a, 2), 2);
                float[] am = BoxBlur(a, 4), rm = BoxBlur(r, 4), gm = BoxBlur(g, 4), bm = BoxBlur(bl, 4);
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++)
                    {
                        int j = y * ViewW + x, i = y * AtlasW + ox + x;
                        float hsh = Mathf.Abs(Mathf.Sin(x * 12.9898f + y * 78.233f + view * 3.7f) * 43758.5453f) % 1f;
                        bool keep = ab[j] > 0.38f + 0.24f * hsh;
                        Color c = o[i].a > 0f ? o[i] : filled[i];
                        if (!keep) { o[i] = new Color(c.r, c.g, c.b, 0f); continue; }
                        if (c.r + c.g + c.b <= 0f) { o[i] = new Color(0f, 0f, 0f, 0f); continue; }   // nothing to fill it with
                        if (am[j] > 0.05f)
                        {
                            var mean = new Color(rm[j] / am[j], gm[j] / am[j], bm[j] / am[j]);
                            c = Color.Lerp(c, mean, 0.45f);
                        }
                        o[i] = new Color(c.r, c.g, c.b, 1f);
                    }
            }
        }

        private static Color[] MirrorNormals(Color[] src)
        {
            var o = new Color[src.Length];
            for (int y = 0; y < ViewH; y++)
                for (int x = 0; x < AtlasW; x++)
                {
                    Color c = src[y * AtlasW + x];
                    int col = x / ViewW, lx = x % ViewW;
                    o[y * AtlasW + col * ViewW + (ViewW - 1 - lx)] = new Color(1f - c.r, c.g, c.b, c.a);
                }
            return o;
        }

        /// <summary>The impostor's four cards without the crown cap (null if not baked).</summary>
        public static Mesh CardsMesh(string meshFolder, string stem) => AssetDatabase.LoadAssetAtPath<Mesh>($"{meshFolder}/{stem}_ImpostorCards.asset");

        /// <summary>Hill (dense) impostor material of a species, variant 0..VariantCount-1; null if not baked.</summary>
        public static Material DenseVariant(string matFolder, string stem, int variant) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{matFolder}/MAT_{stem}_Impostor_V{variant}.mat");

        /// <summary>
        /// Tangent-space normals that describe a round crown instead of a flat card: the outline of every leaf mass
        /// bends away from the viewer (gradient of the blurred coverage), the whole crown bulges round its dense centre
        /// and leans to the sky, and a share of the real leaf normals keeps the texture. Without this each card was lit
        /// on one half and black on the other at a low sun.
        /// </summary>
        private static Color[] BuildCrownNormals(Color[] px, Vector3[] nrm)
        {
            var outPx = new Color[px.Length];
            var flat = new Color(0.5f, 0.5f, 1f, 1f);
            for (int view = 0; view < 2; view++)
            {
                int ox = view * ViewW;
                var a = new float[ViewW * ViewH];
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++) a[y * ViewW + x] = px[y * AtlasW + ox + x].a > 0f ? 1f : 0f;
                float[] blur = BoxBlur(BoxBlur(BoxBlur(a, 10), 10), 10);
                // crown centre: weighted by the blurred density squared (the dense crown, not the thin trunk)
                double sw = 0, sx = 0, sy = 0;
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++)
                    {
                        float wv = blur[y * ViewW + x]; wv *= wv;
                        sw += wv; sx += wv * x; sy += wv * y;
                    }
                float cx = sw > 0 ? (float)(sx / sw) : ViewW * 0.5f, cy = sw > 0 ? (float)(sy / sw) : ViewH * 0.6f;
                double vx = 0, vy = 0;
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++)
                    {
                        float wv = blur[y * ViewW + x]; wv *= wv;
                        vx += wv * (x - cx) * (x - cx); vy += wv * (y - cy) * (y - cy);
                    }
                float rx = Mathf.Max(8f, 1.7f * Mathf.Sqrt((float)(vx / System.Math.Max(sw, 1e-6)))), ry = Mathf.Max(8f, 1.7f * Mathf.Sqrt((float)(vy / System.Math.Max(sw, 1e-6))));
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < ViewW; x++)
                    {
                        int i = y * AtlasW + ox + x;
                        if (px[i].a <= 0f) { outPx[i] = flat; continue; }
                        float bl = blur[y * ViewW + Mathf.Max(0, x - 1)], br = blur[y * ViewW + Mathf.Min(ViewW - 1, x + 1)];
                        float bd = blur[Mathf.Max(0, y - 1) * ViewW + x], bu = blur[Mathf.Min(ViewH - 1, y + 1) * ViewW + x];
                        var g = new Vector2(bl - br, bd - bu) * 14f;
                        var sph = new Vector2((x - cx) / rx, (y - cy) / ry) * 1.1f;
                        Vector3 shape = new Vector3(g.x + sph.x, g.y + sph.y + 0.30f, 1f).normalized;
                        Vector3 leaf = nrm[i].sqrMagnitude > 0.01f ? nrm[i] : Vector3.forward;
                        Vector3 n = (shape * 0.72f + leaf * 0.28f).normalized;
                        if (n.z < 0.15f) { n.z = 0.15f; n.Normalize(); }
                        outPx[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                    }
            }
            // top view: the crown seen from above faces the sky; a share of the leaf normals keeps the texture
            for (int y = 0; y < ViewH; y++)
                for (int x = 0; x < ViewW; x++)
                {
                    int i = y * AtlasW + 2 * ViewW + x;
                    if (y >= ViewW || px[i].a <= 0f) { outPx[i] = flat; continue; }
                    Vector3 leaf = nrm[i].sqrMagnitude > 0.01f ? nrm[i] : Vector3.forward;
                    Vector3 n = (Vector3.forward * 0.65f + leaf * 0.35f).normalized;
                    outPx[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            return outPx;
        }

        private static float[] BoxBlur(float[] src, int r)
        {
            var tmp = new float[src.Length];
            var dst = new float[src.Length];
            for (int y = 0; y < ViewH; y++)
            {
                float acc = 0f; int n = 0;
                for (int x = -r; x < ViewW; x++)
                {
                    int xa = x + r, xr = x - r - 1;
                    if (xa < ViewW) { acc += src[y * ViewW + xa]; n++; }
                    if (xr >= 0) { acc -= src[y * ViewW + xr]; n--; }
                    if (x >= 0) tmp[y * ViewW + x] = acc / Mathf.Max(1, n);
                }
            }
            for (int x = 0; x < ViewW; x++)
            {
                float acc = 0f; int n = 0;
                for (int y = -r; y < ViewH; y++)
                {
                    int ya = y + r, yr = y - r - 1;
                    if (ya < ViewH) { acc += tmp[ya * ViewW + x]; n++; }
                    if (yr >= 0) { acc -= tmp[yr * ViewW + x]; n--; }
                    if (y >= 0) dst[y * ViewW + x] = acc / Mathf.Max(1, n);
                }
            }
            return dst;
        }

        private static Texture2D SaveNormalTexture(string path, Color[] px)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = tex == null;
            if (isNew) tex = new Texture2D(AtlasW, ViewH, TextureFormat.RGBA32, true, true);
            else if (tex.width != AtlasW || tex.height != ViewH) tex.Reinitialize(AtlasW, ViewH, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.SetPixels(px, 0);
            tex.Apply(true, false);
            if (isNew) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }

        /// <summary>Fill transparent texels with their neighbours' colour (alpha stays 0) so mips have no dark fringes.</summary>
        private static void Dilate(Color[] px)
        {
            var src = (Color[])px.Clone();
            for (int pass = 0; pass < 6; pass++)
            {
                for (int y = 0; y < ViewH; y++)
                    for (int x = 0; x < AtlasW; x++)
                    {
                        int i = y * AtlasW + x;
                        if (src[i].a > 0f) continue;
                        Color sum = Color.clear; int n = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= AtlasW || yy >= ViewH) continue;
                                Color c = src[yy * AtlasW + xx];
                                if (c.r + c.g + c.b > 0f) { sum += c; n++; }
                            }
                        if (n > 0) px[i] = new Color(sum.r / n, sum.g / n, sum.b / n, 0f);
                    }
                System.Array.Copy(px, src, px.Length);
            }
        }

        private static float Coverage(Color[] c, float scale)
        {
            int n = 0;
            for (int i = 0; i < c.Length; i++) if (c[i].a * scale >= Cutoff) n++;
            return n / (float)c.Length;
        }

        private static Texture2D SaveTexture(string path, Color[] px)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = tex == null;
            if (isNew) tex = new Texture2D(AtlasW, ViewH, TextureFormat.RGBA32, true, false);
            else if (tex.width != AtlasW || tex.height != ViewH) tex.Reinitialize(AtlasW, ViewH, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.SetPixels(px, 0);
            tex.Apply(true, false);
            // keep alpha-tested coverage in every mip (otherwise far impostors thin out and vanish)
            float target = Coverage(px, 1f);
            for (int m = 1; m < tex.mipmapCount; m++)
            {
                Color[] mip = tex.GetPixels(m);
                float lo = 0.5f, hi = 8f;
                for (int it = 0; it < 14; it++) { float mid = (lo + hi) * 0.5f; if (Coverage(mip, mid) < target) lo = mid; else hi = mid; }
                for (int i = 0; i < mip.Length; i++) mip[i].a = Mathf.Clamp01(mip[i].a * hi);
                tex.SetPixels(mip, m);
            }
            tex.Apply(false, false);
            if (isNew) AssetDatabase.CreateAsset(tex, path);
            else EditorUtility.SetDirty(tex);
            return tex;
        }

        private static Mesh SaveMesh(string path, Bounds b, float capY)
        {
            Vector3 c = b.center;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            // vertex normals = the quad's front side (clockwise winding); the crown normal map does the shading
            void Quad(Vector3 a0, Vector3 a1, float u0, float u1, Vector3 front)
            {
                int i = v.Count;
                v.Add(a0 + Vector3.up * b.min.y); v.Add(a1 + Vector3.up * b.min.y); v.Add(a1 + Vector3.up * b.max.y); v.Add(a0 + Vector3.up * b.max.y);
                n.Add(front); n.Add(front); n.Add(front); n.Add(front);
                uv.Add(new Vector2(u0, 0f)); uv.Add(new Vector2(u1, 0f)); uv.Add(new Vector2(u1, 1f)); uv.Add(new Vector2(u0, 1f));
                t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2);
            }
            const float third = 1f / 3f;
            Quad(new Vector3(b.min.x, 0f, c.z), new Vector3(b.max.x, 0f, c.z), 0f, third, Vector3.back);
            Quad(new Vector3(c.x, 0f, b.min.z), new Vector3(c.x, 0f, b.max.z), third, 2f * third, Vector3.right);
            // horizontal crown cap (review 2: seen from 30-60 deg above, the crossed quads collapsed into thin X
            // shapes over the dark ground): the top view, image = (X, Z), in the third atlas column, lower half
            // Review 3: as one flat quad the cap showed edge-on from eye level as a plate ('pagoda' shelves on every
            // forest edge); as an 8-sided umbrella at 88 % of the footprint its straight rim still read as a row of
            // lit ledges on the dusk hill. Round 4: a ragged 16-point rim at 72-88 % of the footprint still read as a
            // mosaic of lit plates on the dusk hill. Round 5: a steep, small cone sunk deep into the four leaf cards
            // (centre 10 % under the crown top, rim at 55-70 % of the footprint and 30-40 % of the crown height lower,
            // 10 points): from eye level the cards' leaves hide its rim, from the air it still closes the crown top.
            {
                int i = v.Count;
                float vMax = ViewW / (float)ViewH;
                Vector3 ext = b.extents;
                float h = b.size.y;
                Vector2 UvAt(float sx, float sz) => new Vector2(2f * third + third * (0.5f + 0.5f * sx), vMax * (0.5f + 0.5f * sz));
                // fix round (r5 critic): rim back out to 70-85 % of the footprint, droop 18-26 % of the crown height,
                // centre 4 % under the crown top, rim normals lean 0.15 (the deep 55-70 % cone left the crown top open
                // between the cards and, with the 45 deg cards, gave pyramid tents from the air)
                v.Add(new Vector3(c.x, capY - h * 0.04f, c.z)); n.Add(Vector3.up); uv.Add(UvAt(0f, 0f));
                const int ring = CapRing;
                for (int k = 0; k < ring; k++)
                {
                    float ang = (k + 0.37f) * Mathf.PI * 2f / ring;
                    // fixed pseudo-random rim (same for every bake)
                    float hsh = Mathf.Abs(Mathf.Sin(k * 12.9898f + 4.1f) * 43758.5453f) % 1f;
                    float rad = 0.70f + 0.15f * hsh;
                    float droop = h * (0.18f + 0.08f * ((k % 2 == 0) ? hsh : 1f - hsh));
                    float sx = Mathf.Cos(ang) * rad, sz = Mathf.Sin(ang) * rad;
                    v.Add(new Vector3(c.x + sx * ext.x, capY - droop, c.z + sz * ext.z));
                    n.Add((Vector3.up + new Vector3(sx, 0f, sz) * 0.15f).normalized);
                    // the rim samples the top view a little further out than it sits, so the cap shows the whole crown top
                    uv.Add(UvAt(Mathf.Clamp(sx * 1.12f, -1f, 1f), Mathf.Clamp(sz * 1.12f, -1f, 1f)));
                }
                for (int k = 0; k < ring; k++)
                {
                    int r0 = i + 1 + k, r1 = i + 1 + (k + 1) % ring;
                    t.Add(i); t.Add(r1); t.Add(r0);
                }
            }
            // round 5: the same cards without the cap, for the hill renderer's eye-level cells (the cap is only drawn
            // where the camera looks down on the crowns: InstancedFoliageRenderer.capElevation)
            {
                string cpath = path.Replace("_Impostor.asset", "_ImpostorCards.asset");
                var cm = AssetDatabase.LoadAssetAtPath<Mesh>(cpath);
                bool cNew = cm == null;
                if (cNew) cm = new Mesh();
                else cm.Clear();
                cm.SetVertices(v.GetRange(0, CardVerts)); cm.SetNormals(n.GetRange(0, CardVerts)); cm.SetUVs(0, uv.GetRange(0, CardVerts)); cm.SetTriangles(t.GetRange(0, CardIndices), 0);
                cm.RecalculateTangents();
                cm.RecalculateBounds();
                if (cNew) AssetDatabase.CreateAsset(cm, cpath);
                else EditorUtility.SetDirty(cm);
            }
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh();
            else mesh.Clear();
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (isNew) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material SaveMaterial(string path, Material template, Texture2D tex, Texture2D normalTex)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) { mat = new Material(template); AssetDatabase.CreateAsset(mat, path); }
            else mat.CopyPropertiesFromMaterial(template);
            mat.SetTexture("_BaseColorMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normalTex);
            if (mat.HasProperty("_NormalScale")) mat.SetFloat("_NormalScale", 1f);
            if (mat.HasProperty("_NormalMapSpace")) mat.SetFloat("_NormalMapSpace", 0f);
            // seen from behind, the crown normal keeps its sideways and upward lean and turns towards the viewer
            if (mat.HasProperty("_DoubleSidedNormalMode")) mat.SetFloat("_DoubleSidedNormalMode", 1f);
            if (mat.HasProperty("_MaskMap")) mat.SetTexture("_MaskMap", null);
            // plain Lit, rough: no translucent glow when back-lit, no grey sky sheen on the up-facing quad normals
            HDMaterial.SetMaterialType(mat, MaterialId.LitStandard);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.0f);
            if (mat.HasProperty("_SmoothnessRemapMax")) mat.SetFloat("_SmoothnessRemapMax", 0.05f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.SetAlphaClipping(mat, true);
            HDMaterial.SetAlphaCutoff(mat, Cutoff);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
