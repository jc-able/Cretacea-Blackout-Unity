using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using Cretacea.Core;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Round 2 dressing helpers for Zones 2 + 3 (review 1 findings): single-layer chain-link panels with tube frames,
    /// conduits with status LEDs instead of glowing rods, tapered meshes for skips, and kit-built dock and road props.
    /// Everything here is collider-free; where a gameplay box is replaced visually its collider is kept as it was.
    /// </summary>
    internal static partial class Zone23Dressing
    {
        // ------------------------------------------------------------------ chain-link
        /// <summary>Real chain-link repeats every 1.8 m (32 diamonds across the texture, 5.6 cm diamonds).</summary>
        public const float ChainlinkTile = 1.8f;
        public const string ChainlinkPanelName = "ChainlinkPanel";

        /// <summary>
        /// A chain-link box (double-sided transparent material on a cube: two wire layers plus stripes on its edges)
        /// becomes one plane through its middle. The box keeps its collider; only its renderer is switched off.
        /// The plane's UVs are in metres / 1.8, so the UV-mapped copy that moving door leaves get keeps the diamond size.
        /// </summary>
        public static MeshRenderer ThinChainlinkBox(GameObject box)
        {
            if (box == null) return null;
            MeshRenderer mr = box.GetComponent<MeshRenderer>();
            if (mr == null) return null;
            Transform existing = box.transform.Find(ChainlinkPanelName);
            if (existing != null) return existing.GetComponent<MeshRenderer>();

            Vector3 s = box.transform.localScale;
            Vector3 lossy = box.transform.lossyScale;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
            int a0 = thin == 0 ? 2 : 0;            // horizontal-ish axis of the panel
            int a1 = thin == 1 ? 2 : 1;            // vertical-ish axis
            var go = new GameObject(ChainlinkPanelName);
            go.layer = box.layer;
            go.transform.SetParent(box.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = PanelMesh(thin, a0, a1, Mathf.Abs(lossy[a0]) / ChainlinkTile, Mathf.Abs(lossy[a1]) / ChainlinkTile);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ChainlinkSeeThrough() ?? mr.sharedMaterial;
            r.shadowCastingMode = ShadowCastingMode.On;
            mr.enabled = false;
            return r;
        }

        private static Material s_chainlink;

        /// <summary>
        /// The zone 2-3 panels use a copy of the palette's chain-link with 52 % of its wire opacity (base colour alpha):
        /// at 5-30 m, where the wires are thinner than a pixel, the palette material averages to a ~29 % veil per layer
        /// and a floodlit fence read as a tan plywood board (review 1). Rebuilt from MAT_ChainlinkFence on every build.
        /// </summary>
        public static Material ChainlinkSeeThrough()
        {
            if (s_chainlink != null) return s_chainlink;
            string folder = WorldMaterialPalette.MaterialsOutputFolder;
            var src = AssetDatabase.LoadAssetAtPath<Material>(folder + "/MAT_ChainlinkFence.mat");
            if (src == null) return null;
            string path = folder + "/MAT_Z23_ChainlinkSeeThrough.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src) { name = "MAT_Z23_ChainlinkSeeThrough" }; AssetDatabase.CreateAsset(m, path); }
            else m.CopyPropertiesFromMaterial(src);
            Color c = src.GetColor("_BaseColor");
            m.SetColor("_BaseColor", new Color(c.r * 0.92f, c.g * 0.92f, c.b * 0.92f, 0.52f));
            Texture2D fade = ChainlinkFadeTexture(src.GetTexture("_BaseColorMap") as Texture2D);
            if (fade != null) m.SetTexture("_BaseColorMap", fade);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            s_chainlink = m;
            return m;
        }

        /// <summary>
        /// Fix round (far fences still read as lit boards): a copy of the chain-link albedo whose small mips fade out.
        /// Mips 0-2 (wires resolved, up to ~8 m) keep their alpha; mip 3 keeps 55 %, mip 4 and smaller 30 %, capped at
        /// 0.12, so a fence past ~10-15 m is a faint veil you see the valley through instead of a floodlit panel. Built once
        /// from Assets/Art/Decals/chainlink_albedo.png (DXT5, 1024 px), rebuilt when the source is newer.
        /// </summary>
        public static Texture2D ChainlinkFadeTexture(Texture2D src)
        {
            if (src == null) return null;
            string srcPath = AssetDatabase.GetAssetPath(src);
            if (string.IsNullOrEmpty(srcPath) || !srcPath.EndsWith(".png")) return null;
            string path = WorldMaterialPalette.MaterialsOutputFolder + "/TEX_Z23_ChainlinkFade.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            string full = System.IO.Path.GetFullPath(srcPath), outFull = System.IO.Path.GetFullPath(path);
            if (existing != null && System.IO.File.Exists(outFull) && System.IO.File.GetLastWriteTimeUtc(outFull) >= System.IO.File.GetLastWriteTimeUtc(full)) return existing;
            var tmp = new Texture2D(2, 2, TextureFormat.RGBA32, true, false);
            if (!tmp.LoadImage(System.IO.File.ReadAllBytes(full), false)) { Object.DestroyImmediate(tmp); return existing; }
            tmp.Apply(true, false);
            var dst = new Texture2D(tmp.width, tmp.height, TextureFormat.RGBA32, true, false) { name = "TEX_Z23_ChainlinkFade", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            for (int mip = 0; mip < tmp.mipmapCount; mip++)
            {
                Color32[] px = tmp.GetPixels32(mip);
                float k = mip <= 2 ? 1f : (mip == 3 ? 0.55f : 0.30f);
                if (k < 1f)
                    for (int i = 0; i < px.Length; i++)
                    {
                        float a = px[i].a / 255f * k;
                        if (mip >= 4) a = Mathf.Min(a, 0.12f);
                        px[i].a = (byte)Mathf.RoundToInt(a * 255f);
                    }
                dst.SetPixels32(px, mip);
            }
            dst.Apply(false, false);
            EditorUtility.CompressTexture(dst, TextureFormat.DXT5, TextureCompressionQuality.Best);
            Object.DestroyImmediate(tmp);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(dst, path);
            return dst;
        }

        /// <summary>Call at the start of a build so the material is refreshed from the palette.</summary>
        public static void ResetCaches() { s_chainlink = null; s_corrugated.Clear(); }

        private static Mesh PanelMesh(int thin, int a0, int a1, float uMax, float vMax)
        {
            var v = new Vector3[4];
            var uv = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                float p0 = (i == 1 || i == 2) ? 0.5f : -0.5f;
                float p1 = i >= 2 ? 0.5f : -0.5f;
                Vector3 q = Vector3.zero;
                q[a0] = p0; q[a1] = p1;
                v[i] = q;
                uv[i] = new Vector2((p0 + 0.5f) * uMax, (p1 + 0.5f) * vMax);
            }
            Vector3 n = Vector3.zero; n[thin] = 1f;
            var m = new Mesh { name = "Z23_ChainlinkPanel" };
            m.vertices = v;
            m.uv = uv;
            m.normals = new[] { n, n, n, n };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>
        /// Upgrades a kit fence span (BuildChainlinkFenceSpan): one wire plane instead of the double box, round
        /// galvanised posts with caps, round top / bottom rails and a tension wire. Square kit posts and rails are hidden
        /// (they had no colliders); the span's mesh collider box stays.
        /// </summary>
        public static void UpgradeChainlinkSpan(Transform span, Material galv, Material darkSteel)
        {
            if (span == null || span.Find("MeshSpan") == null || span.Find("Z23_Upgraded") != null) return;
            new GameObject("Z23_Upgraded").transform.SetParent(span, false);
            Transform mesh = span.Find("MeshSpan");
            ThinChainlinkBox(mesh.gameObject);
            float h = mesh.localScale.y, len = mesh.localScale.z;
            foreach (Transform c in span)
            {
                if (c.name.StartsWith("Post_") || c.name == "TopRail" || c.name == "MidRail")
                {
                    var r = c.GetComponent<MeshRenderer>();
                    if (r != null) r.enabled = false;
                }
            }
            Vector3 w(float y, float z) => span.TransformPoint(new Vector3(0f, y, z));
            int posts = 0;
            foreach (Transform c in span) if (c.name.StartsWith("Post_")) posts++;
            posts = Mathf.Max(2, posts);
            for (int i = 0; i < posts; i++)
            {
                float z = i / (float)(posts - 1) * len;
                bool end = i == 0 || i == posts - 1;
                float rad = end ? 0.055f : 0.042f;
                Cyl(span, $"Z23Post_{i}", w(-0.05f, z), w(h + 0.12f, z), rad, galv);
                Cyl(span, $"Z23PostCap_{i}", w(h + 0.12f, z), w(h + 0.17f, z), rad + 0.012f, darkSteel);
            }
            Cyl(span, "Z23TopRail", w(h - 0.05f, 0f), w(h - 0.05f, len), 0.024f, galv);
            Cyl(span, "Z23BottomWire", w(0.08f, 0f), w(0.08f, len), 0.006f, darkSteel);
            if (h > 3f) Cyl(span, "Z23MidRail", w(h * 0.5f, 0f), w(h * 0.5f, len), 0.02f, galv);
            foreach (Transform c in span)
                if (c.name.StartsWith("Z23")) { var r = c.GetComponent<MeshRenderer>(); if (r != null) r.shadowCastingMode = ShadowCastingMode.Off; }
            // one mesh per material for the span's posts, caps and rails (review 2: 226 post/cap cylinders)
            MergeStatic(span, t => t.parent == span && t.name.StartsWith("Z23"));
        }

        /// <summary>A round tube frame around a thin chain-link box (stiles, top and bottom rails, optional mid rail and
        /// diagonal brace), following the box's own rotation.</summary>
        public static void TubeFrame(Transform parent, GameObject box, Material m, float r, bool midRail, bool brace)
        {
            Transform t = box.transform;
            Vector3 s = t.localScale;
            int thin = s.x <= s.y && s.x <= s.z ? 0 : (s.y <= s.z ? 1 : 2);
            int a0 = thin == 0 ? 2 : 0, a1 = thin == 1 ? 2 : 1;
            Vector3 P(float u, float v) { Vector3 q = Vector3.zero; q[a0] = u; q[a1] = v; return t.TransformPoint(q); }
            Vector3 bl = P(-0.5f, -0.5f), br = P(0.5f, -0.5f), tl = P(-0.5f, 0.5f), tr = P(0.5f, 0.5f);
            string n = box.name;
            Cyl(parent, n + "_StileA", bl, tl, r, m);
            Cyl(parent, n + "_StileB", br, tr, r, m);
            Cyl(parent, n + "_TopTube", tl, tr, r, m);
            Cyl(parent, n + "_BottomTube", bl, br, r, m);
            if (midRail) Cyl(parent, n + "_MidTube", (bl + tl) * 0.5f, (br + tr) * 0.5f, r * 0.8f, m);
            if (brace) Cyl(parent, n + "_Brace", bl, (br + tr) * 0.5f, r * 0.7f, m);
        }

        /// <summary>Hides a gameplay box's renderer (keeps its collider) and puts a round post in its place.</summary>
        public static void RoundPost(Transform parent, GameObject box, float radius, Material m, Material cap)
        {
            if (box == null) return;
            var mr = box.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
            // From the transform, not Collider.bounds: with auto-sync-transforms off, a collider created this frame still
            // reports its creation-time unit cube at the origin (fix round: posts floated / vanished, 9 poles at 0,0,0).
            Vector3 c = box.transform.position;
            float hy = Mathf.Abs(box.transform.lossyScale.y) * 0.5f;
            Vector3 lo = c - Vector3.up * hy, hi = c + Vector3.up * hy;
            Cyl(parent, box.name + "_Round", lo, hi, radius, m);
            if (cap != null) Cyl(parent, box.name + "_Cap", hi, hi + Vector3.up * 0.05f, radius + 0.02f, cap);
        }

        /// <summary>Every kit fence span under root.</summary>
        public static int UpgradeChainlinkSpans(Transform root, WorldMaterialPalette p)
        {
            if (root == null) return 0;
            var spans = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "MeshSpan" && t.parent != null) spans.Add(t.parent);
            foreach (Transform s in spans) UpgradeChainlinkSpan(s, GalvSteel(p), p.DarkPaintedMetal);
            return spans.Count;
        }

        // ------------------------------------------------------------------ merging (perf, fix round)
        private static readonly HashSet<string> MergeableMeshes = new HashSet<string> { "Cube", "Cylinder", "Quad", "Sphere" };

        /// <summary>
        /// Performance (review 2: ~900 new GameObjects): merges the collider-free primitive dressing under root into one
        /// mesh per material and shadow mode. Skipped: anything with a collider, extra components or children, anything
        /// under a LODGroup or referenced by a component (light-group emissives, station LEDs, door leaves), emissive
        /// materials, transparent chain-link panels and names in keepNames. Returns the number of renderers merged.
        /// </summary>
        public static int MergeStatic(Transform root, System.Func<Transform, bool> include = null)
        {
            if (root == null) return 0;
            var protectedGos = new HashSet<GameObject>();
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                var so = new SerializedObject(mb);
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                    Object o = it.objectReferenceValue;
                    GameObject g = o is GameObject go ? go : (o is Component c ? c.gameObject : null);
                    if (g != null) protectedGos.Add(g);
                }
                foreach (Transform t in mb.GetComponentsInChildren<Transform>(true)) protectedGos.Add(t.gameObject);
            }
            foreach (LODGroup lg in root.GetComponentsInChildren<LODGroup>(true))
                foreach (Transform t in lg.GetComponentsInChildren<Transform>(true)) protectedGos.Add(t.gameObject);

            var groups = new Dictionary<(Material, ShadowCastingMode), List<MeshRenderer>>();
            foreach (MeshRenderer mr in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                GameObject g = mr.gameObject;
                if (!mr.enabled || protectedGos.Contains(g) || g.transform.childCount > 0) continue;
                MeshFilter mf = g.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || !MergeableMeshes.Contains(mf.sharedMesh.name)) continue;
                if (g.GetComponents<Component>().Length != 3) continue;           // Transform + MeshFilter + MeshRenderer only
                if (mr.sharedMaterials.Length != 1 || mr.sharedMaterial == null) continue;
                string mn = mr.sharedMaterial.name;
                if (mn.Contains("Emissive") || mn.Contains("Chainlink") || mn.Contains("Glass") || mn.Contains("Hidden")) continue;
                if (g.name.IndexOf("Ceil", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;   // LookRoofs keys on "Ceil"
                if (mr.sharedMaterial.HasProperty("_SurfaceType") && mr.sharedMaterial.GetFloat("_SurfaceType") > 0.5f) continue;
                if (include != null && !include(g.transform)) continue;
                var key = (mr.sharedMaterial, mr.shadowCastingMode);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
                list.Add(mr);
            }
            int merged = 0;
            Matrix4x4 toRoot = root.worldToLocalMatrix;
            foreach (var kv in groups)
            {
                if (kv.Value.Count < 2) continue;
                var combine = new List<CombineInstance>();
                int verts = 0, chunk = 0;
                void Flush()
                {
                    if (combine.Count == 0) return;
                    var mesh = new Mesh { name = $"Z23_Merged_{kv.Key.Item1.name}_{chunk}", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(combine.ToArray(), true, true);
                    mesh.RecalculateBounds();
                    var go = new GameObject(mesh.name) { layer = GameLayers.Environment };
                    go.transform.SetParent(root, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>();
                    r.sharedMaterial = kv.Key.Item1;
                    r.shadowCastingMode = kv.Key.Item2;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
                    combine.Clear(); verts = 0; chunk++;
                }
                foreach (MeshRenderer mr in kv.Value)
                {
                    Mesh m = mr.GetComponent<MeshFilter>().sharedMesh;
                    if (verts + m.vertexCount > 60000) Flush();
                    combine.Add(new CombineInstance { mesh = m, transform = toRoot * mr.localToWorldMatrix });
                    verts += m.vertexCount;
                }
                Flush();
                foreach (MeshRenderer mr in kv.Value) { Object.DestroyImmediate(mr.gameObject); merged++; }
            }
            return merged;
        }

        // ------------------------------------------------------------------ conduits
        public static Material ConduitGrey(WorldMaterialPalette p) => GalvSteel(p);

        /// <summary>
        /// Small status LEDs riding on a conduit (one on a saddle clamp every ~2.4 m). They use the palette's red
        /// emissive; the SubstationStation drives their emission (dim fault red -> powered green) instead of a glowing
        /// tube. Returns their renderers.
        /// </summary>
        public static List<Renderer> ConduitLeds(Transform parent, string name, Vector3[] path, float conduitRadius, WorldMaterialPalette p, float spacing = 2.4f)
        {
            var leds = new List<Renderer>();
            if (path == null || path.Length < 2) return leds;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            float acc = spacing * 0.35f;
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 a = path[i], b = path[i + 1];
                Vector3 d = b - a; float len = d.magnitude; if (len < 0.05f) continue; d /= len;
                Vector3 side = Vector3.Cross(Vector3.up, d); if (side.sqrMagnitude < 0.01f) side = Vector3.right; side.Normalize();
                Vector3 up = Vector3.Cross(d, side).normalized; if (up.y < 0f) up = -up;
                Quaternion rot = Quaternion.LookRotation(d, up);
                for (float t = acc; t < len - 0.1f; t += spacing)
                {
                    Vector3 c = a + d * t;
                    Box(root.transform, "LedClamp", c, new Vector3(conduitRadius * 2.6f, conduitRadius * 2.6f, 0.035f), p.BrushedSteel, rot);
                    var led = Box(root.transform, "LedMarker", c + up * (conduitRadius * 1.3f + 0.006f), new Vector3(0.022f, 0.012f, 0.022f), p.EmissiveRed, rot);
                    var lr = led.GetComponent<MeshRenderer>();
                    lr.shadowCastingMode = ShadowCastingMode.Off;
                    leds.Add(lr);
                }
                acc = Mathf.Repeat(acc - len, spacing);
                if (acc < 0.1f) acc += spacing;
            }
            return leds;
        }

        // ------------------------------------------------------------------ meshes
        /// <summary>A box tapered in X and Z (bottom smaller than top), centred on worldPos at its base. Flat-shaded,
        /// triplanar materials need no UVs.</summary>
        public static GameObject Frustum(Transform parent, string name, Vector3 worldBase, Quaternion rot, Vector2 bottom, Vector2 top, float h, Material mat)
        {
            float bx = bottom.x * 0.5f, bz = bottom.y * 0.5f, tx = top.x * 0.5f, tz = top.y * 0.5f;
            Vector3[] c =
            {
                new(-bx, 0, -bz), new(bx, 0, -bz), new(bx, 0, bz), new(-bx, 0, bz),
                new(-tx, h, -tz), new(tx, h, -tz), new(tx, h, tz), new(-tx, h, tz),
            };
            int[][] faces = { new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 }, new[] { 3, 2, 1, 0 } };
            var v = new List<Vector3>(); var tri = new List<int>(); var uv = new List<Vector2>();
            foreach (int[] f in faces)
            {
                int o = v.Count;
                for (int k = 0; k < 4; k++) { v.Add(c[f[k]]); uv.Add(new Vector2(k == 1 || k == 2 ? 1 : 0, k >= 2 ? 1 : 0)); }
                tri.AddRange(new[] { o, o + 2, o + 1, o, o + 3, o + 2 });
            }
            var m = new Mesh { name = "Z23_" + name };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            var go = new GameObject(name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldBase, rot);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        /// <summary>World position of a local offset under a yawed frame.</summary>
        public static Vector3 At(Vector3 origin, Quaternion rot, float x, float y, float z) => origin + rot * new Vector3(x, y, z);

        // ------------------------------------------------------------------ dock and coast road (filled in below)
        /// <summary>
        /// The loading dock where the service path starts (review 1: the torch landed on a pale-mint box and a blank
        /// wall). Zone 1 builds the dock; this replaces the look of its box 'Dumpster' (its collider stays) with a
        /// front-load skip, and dresses the path mouth: pallets, a hose reel, a pallet truck, a route sign, a downpipe.
        /// </summary>
        public static void DressServiceDockApproach(Transform worldRoot, Transform zone2Root, WorldMaterialPalette p)
        {
            var g = new GameObject("Z23_DockApproachDressing");
            g.transform.SetParent(zone2Root, false);
            Transform bin = null;
            if (worldRoot != null)
                foreach (Transform t in worldRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Dumpster" && t.parent != null && t.parent.name == "CoveredLoadingDock") { bin = t; break; }
            Vector3 c = bin != null ? bin.position : new Vector3(20.6f, 0f, -12.6f);
            Quaternion r = bin != null ? bin.rotation : Quaternion.Euler(0f, 90f, 0f);
            // Zone 1 merges its box dumpster into its static mesh before this runs, so it cannot be hidden from here (and
            // it is zone 1 geometry): the skip is built a little larger than the box and encloses it (fix round: the
            // "pale mint skip" in review 2 was the old box showing through a tapered skip). Request: zone 1 drops the box.
            BuildSkip(g.transform, c, r, p);

            // pallet stack, crate and a coiled hose by the south-east column, clear of the route (z -11.3 .. -6.7)
            float py = 0f;
            for (int i = 0; i < 4; i++)
            {
                var pal = p.InstantiatePropOrFallback("pallet", g.transform, new Vector3(16.9f, py, -14.7f), new Vector3(0f, 90f + (i % 2) * 4f, 0f), new Vector3(1.2f, 0.15f, 1.0f), grounded: true, addCollider: false);
                py += 0.145f;
            }
            p.InstantiatePropOrFallback("crate", g.transform, new Vector3(16.8f, py, -14.6f), new Vector3(0f, 12f, 0f), new Vector3(0.8f, 0.7f, 0.8f), grounded: true, addCollider: false);
            HoseReel(g.transform, new Vector3(21.43f, 0f, -17.6f), Quaternion.Euler(0f, -90f, 0f), p);   // on the south-east canopy column
            PalletTruck(g.transform, new Vector3(18.9f, 0f, -14.3f), Quaternion.Euler(0f, 205f, 0f), p);
            p.InstantiatePropOrFallback("trash_bin", g.transform, new Vector3(20.8f, 0f, -13.95f), new Vector3(0f, 30f, 0f), new Vector3(0.55f, 0.95f, 0.55f), grounded: true, addCollider: false);
            Decal(g.transform, "SkipOilStain", new Vector3(20.3f, 0.05f, -12.9f), Vector3.up, new Vector3(2.6f, 2.0f, 0.3f), Grime(p, 1), 0.75f, rollDeg: 30f);
            Decal(g.transform, "SkipLaneStripes", new Vector3(19.0f, 0.05f, -12.6f), Vector3.up, new Vector3(0.4f, 2.4f, 0.3f), p.DecalCautionStripes, 0.8f);

            // route sign on the canopy column in the path mouth, a downpipe and hazard band on the wall end
            Material road3 = SignMat(p, "MAT_Z23_SignServiceRoad3", "service_road_3");
            SignPlate(g.transform, "Sign_ServiceRoad3_Dock", new Vector3(21.43f, 2.25f, -10.0f), Vector3.left, 0.78f, 0.39f, road3, p.DarkPaintedMetal);
            Cyl(g.transform, "WallEnd_Downpipe", new Vector3(21.35f, 0.05f, -6.42f), new Vector3(21.35f, 4.95f, -6.42f), 0.05f, p.DarkPaintedMetal);
            Cyl(g.transform, "WallEnd_DownpipeShoe", new Vector3(21.35f, 0.05f, -6.42f), new Vector3(21.1f, 0.05f, -6.42f), 0.05f, p.DarkPaintedMetal);
            Decal(g.transform, "WallEnd_Hazard", new Vector3(21.3f, 0.6f, -6.7f), Vector3.left, new Vector3(0.36f, 1.2f, 0.4f), p.DecalCautionStripes, 0.9f);
            Decal(g.transform, "WallEnd_Grime", new Vector3(21.3f, 3.6f, -6.7f), Vector3.left, new Vector3(0.36f, 2.4f, 0.4f), Grime(p, 2), 0.8f);

            // fix round: the long pale south face of the path's north wall (Wall_Seg1_N, face z -6.9, from x 21.5 east)
            // read as a blank slab: rain streaks and grime, a staff-only plate, a conduit with a junction box, a hose bib
            const float fz = -6.9f;
            float[] gx = { 22.6f, 25.1f, 27.4f, 30.2f, 32.6f };
            for (int i = 0; i < gx.Length; i++)
                Decal(g.transform, $"WallN_Streaks_{i}", new Vector3(gx[i], 3.4f - (i % 2) * 0.5f, fz - 0.2f), Vector3.back, new Vector3(1.3f + 0.3f * (i % 3), 2.8f, 0.5f), Grime(p, i + 3), 0.85f);
            Decal(g.transform, "WallN_FootGrime", new Vector3(26.5f, 0.5f, fz - 0.2f), Vector3.back, new Vector3(9.5f, 1.0f, 0.5f), Grime(p, 1), 0.8f);
            SignPlate(g.transform, "Sign_StaffOnly_ServicePath", new Vector3(22.9f, 1.75f, fz - 0.012f), Vector3.back, 0.6f, 0.3f, SignMat(p, "MAT_Z23_SignStaffOnly", "staff_only"), p.DarkPaintedMetal);
            Material galv = GalvSteel(p);
            Cyl(g.transform, "WallN_Conduit", new Vector3(21.7f, 3.3f, fz - 0.04f), new Vector3(33.5f, 3.3f, fz - 0.04f), 0.022f, galv);
            for (float x = 22.4f; x < 33.4f; x += 1.5f)
                Box(g.transform, "WallN_ConduitClamp", new Vector3(x, 3.3f, fz - 0.03f), new Vector3(0.04f, 0.07f, 0.06f), galv, Quaternion.identity);
            Box(g.transform, "WallN_JunctionBox", new Vector3(24.4f, 1.45f, fz - 0.07f), new Vector3(0.34f, 0.44f, 0.14f), Kerb(p), Quaternion.identity);
            Box(g.transform, "WallN_JunctionBoxLid", new Vector3(24.4f, 1.45f, fz - 0.145f), new Vector3(0.3f, 0.4f, 0.012f), p.DarkPaintedMetal, Quaternion.identity);
            Cyl(g.transform, "WallN_JunctionRiser", new Vector3(24.4f, 1.67f, fz - 0.04f), new Vector3(24.4f, 3.3f, fz - 0.04f), 0.022f, galv);
            Decal(g.transform, "WallN_JunctionHazard", new Vector3(24.4f, 1.45f, fz - 0.35f), Vector3.back, new Vector3(0.2f, 0.12f, 0.3f), p.DecalCautionStripes, 0.9f);
            Cyl(g.transform, "WallN_HoseBibPipe", new Vector3(28.6f, 0.05f, fz - 0.04f), new Vector3(28.6f, 0.8f, fz - 0.04f), 0.016f, p.BrushedSteel);
            Box(g.transform, "WallN_HoseBibValve", new Vector3(28.6f, 0.82f, fz - 0.08f), new Vector3(0.06f, 0.06f, 0.08f), PaintRed(p), Quaternion.identity);

            // the jammed roller door at x = 8 (behind the dock checkpoint): rubber bumpers either side and horizontal
            // ribs on its leaf (children of the leaf, so they follow it if it ever moves); collider-free
            for (int s = -1; s <= 1; s += 2)
            {
                Box(g.transform, "DockBumper", new Vector3(8.33f, 0.55f, -11.0f + s * 1.95f), new Vector3(0.14f, 0.5f, 0.26f), Rubber(p), Quaternion.identity);
                Box(g.transform, "DockBumperPlate", new Vector3(8.24f, 0.55f, -11.0f + s * 1.95f), new Vector3(0.03f, 0.6f, 0.34f), p.DarkPaintedMetal, Quaternion.identity);
            }
            Transform leaf = null;
            if (worldRoot != null)
                foreach (Transform t in worldRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == "ShutterCorrugated" && t.parent != null && t.parent.name == "JammedShutter" && Mathf.Abs(t.position.x - 8f) < 0.5f) { leaf = t; break; }
            if (leaf != null)
            {
                Bounds lb = leaf.GetComponent<Renderer>() != null ? leaf.GetComponent<Renderer>().bounds : new Bounds(leaf.position, Vector3.one);
                float faceX = lb.max.x + 0.012f;
                for (float yy = lb.min.y + 0.12f; yy < lb.max.y - 0.05f; yy += 0.16f)
                {
                    GameObject rib = Box(g.transform, "RollerRib", new Vector3(faceX, yy, lb.center.z), new Vector3(0.024f, 0.035f, lb.size.z - 0.04f), RollerSteel(p), Quaternion.identity);
                    rib.transform.SetParent(leaf, true);
                }
                Box(g.transform, "RollerBottomBar", new Vector3(faceX + 0.01f, lb.min.y + 0.03f, lb.center.z), new Vector3(0.05f, 0.06f, lb.size.z), p.DarkPaintedMetal, Quaternion.identity).transform.SetParent(leaf, true);
            }
            MergeStatic(g.transform);
        }

        /// <summary>Front-load skip, ~1.9 x 1.1 x 1.2 m: tapered steel body on casters, top rim, side ribs, fork
        /// pockets, two black lids (one ajar), grime. Base centre at c.</summary>
        public static void BuildSkip(Transform parent, Vector3 c, Quaternion r, WorldMaterialPalette p)
        {
            var g = new GameObject("Skip");
            g.transform.SetParent(parent, false);
            g.transform.SetPositionAndRotation(c, r);
            Material body = SkipGreen(p), lid = Rubber(p), steel = p.DarkPaintedMetal;
            // 2.1 x 1.3 m at the rim, 2.0 x 1.2 at the floor, 1.36 m tall: big enough to enclose a 1.9 x 1.1 x 1.25 box
            const float L = 2.1f, D = 1.3f, H = 1.36f, lift = 0.0f;
            Frustum(g.transform, "SkipBody", At(c, r, 0f, lift, 0f), r, new Vector2(L - 0.1f, D - 0.1f), new Vector2(L, D), H - lift, body);
            for (int s = -1; s <= 1; s += 2)
                Box(g.transform, "Skid", At(c, r, 0f, 0.05f, s * (D * 0.5f - 0.1f)), new Vector3(L - 0.06f, 0.1f, 0.12f), steel, r);
            Box(g.transform, "RimFront", At(c, r, 0f, H, -D * 0.5f - 0.02f), new Vector3(L + 0.06f, 0.07f, 0.05f), body, r);
            Box(g.transform, "RimBack", At(c, r, 0f, H, D * 0.5f + 0.02f), new Vector3(L + 0.06f, 0.07f, 0.05f), body, r);
            Box(g.transform, "RimL", At(c, r, -L * 0.5f - 0.02f, H, 0f), new Vector3(0.05f, 0.07f, D + 0.06f), body, r);
            Box(g.transform, "RimR", At(c, r, L * 0.5f + 0.02f, H, 0f), new Vector3(0.05f, 0.07f, D + 0.06f), body, r);
            for (int s = -1; s <= 1; s += 2)
            {
                for (int k = 0; k < 4; k++)
                {
                    float x = -0.72f + k * 0.48f;
                    Box(g.transform, "SideRib", At(c, r, x, (H + 0.12f) * 0.5f, s * (D * 0.5f - 0.015f)), new Vector3(0.05f, H - 0.16f, 0.06f), body, r * Quaternion.Euler(s * -2.1f, 0f, 0f));
                }
                // fork pocket channels on the short ends
                Box(g.transform, "ForkPocket", At(c, r, s * (L * 0.5f + 0.02f), 0.72f, 0f), new Vector3(0.12f, 0.16f, D + 0.04f), steel, r);
                Box(g.transform, "ForkPocketLip", At(c, r, s * (L * 0.5f + 0.07f), 0.72f, 0f), new Vector3(0.02f, 0.2f, D + 0.06f), steel, r);
            }
            // lids hinged at the back: left closed, right ajar (12 deg)
            Box(g.transform, "LidL", At(c, r, -L * 0.25f, H + 0.05f, 0.02f), new Vector3(L * 0.5f - 0.02f, 0.035f, D + 0.08f), lid, r * Quaternion.Euler(-3f, 0f, 0f));
            Quaternion ajar = r * Quaternion.Euler(-13f, 0f, 0f);
            Vector3 hinge = At(c, r, L * 0.25f, H + 0.06f, D * 0.5f + 0.04f);
            Box(g.transform, "LidR", hinge + ajar * new Vector3(0f, 0f, -(D + 0.08f) * 0.5f), new Vector3(L * 0.5f - 0.02f, 0.035f, D + 0.08f), lid, ajar);
            Box(g.transform, "LidHandle", hinge + ajar * new Vector3(0f, 0.03f, -(D + 0.06f)), new Vector3(0.3f, 0.03f, 0.03f), lid, ajar);
            for (int s = -1; s <= 1; s += 2)
                Decal(g.transform, "SkipCornerHazard", At(c, r, s * (L * 0.5f - 0.18f), H - 0.3f, -D * 0.5f), r * Vector3.back, new Vector3(0.3f, 0.5f, 0.3f), p.DecalCautionStripes, 0.9f);
            Decal(g.transform, "SkipRust", At(c, r, 0.3f, 0.55f, -D * 0.5f), r * Vector3.back, new Vector3(1.2f, 0.9f, 0.3f), Grime(p, 0), 0.85f, rollDeg: 180f);
            Decal(g.transform, "SkipRustBack", At(c, r, -0.4f, 0.5f, D * 0.5f), r * Vector3.forward, new Vector3(1.0f, 0.8f, 0.3f), Grime(p, 2), 0.8f, rollDeg: 180f);
            foreach (MeshRenderer mr in g.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = ShadowCastingMode.On;
        }

        /// <summary>Wall-mounted hose reel: bracket, drum and a few loops of black hose, the end dropped to the floor.</summary>
        public static void HoseReel(Transform parent, Vector3 wallFoot, Quaternion r, WorldMaterialPalette p)
        {
            var g = new GameObject("HoseReel");
            g.transform.SetParent(parent, false);
            Vector3 hub = At(wallFoot, r, 0f, 1.25f, 0.2f);
            Box(g.transform, "Bracket", At(wallFoot, r, 0f, 1.25f, 0.03f), new Vector3(0.36f, 0.5f, 0.05f), PaintRed(p), r);
            Cyl(g.transform, "Axle", At(wallFoot, r, 0f, 1.25f, 0.03f), hub + r * Vector3.forward * 0.12f, 0.03f, p.BrushedSteel);
            Cyl(g.transform, "DrumBack", hub - r * Vector3.forward * 0.1f, hub - r * Vector3.forward * 0.085f, 0.3f, PaintRed(p));
            Cyl(g.transform, "DrumFront", hub + r * Vector3.forward * 0.085f, hub + r * Vector3.forward * 0.1f, 0.3f, PaintRed(p));
            Cyl(g.transform, "HoseCoil", hub - r * Vector3.forward * 0.08f, hub + r * Vector3.forward * 0.08f, 0.22f, Rubber(p));
            Vector3 a = hub + r * new Vector3(0.18f, -0.14f, 0.05f), b = At(wallFoot, r, 0.35f, 0.03f, 0.45f), e = At(wallFoot, r, -0.1f, 0.03f, 0.9f);
            Cyl(g.transform, "HoseDrop", a, b, 0.022f, Rubber(p));
            Cyl(g.transform, "HoseFloor", b, e, 0.022f, Rubber(p));
            Cyl(g.transform, "Nozzle", e, e + r * new Vector3(-0.18f, 0f, 0.05f), 0.03f, p.BrushedSteel);
        }

        /// <summary>Hand pallet truck: forks, a hydraulic body and a tiller handle.</summary>
        public static void PalletTruck(Transform parent, Vector3 c, Quaternion r, WorldMaterialPalette p)
        {
            var g = new GameObject("PalletTruck");
            g.transform.SetParent(parent, false);
            Material y = PaintYellow(p);
            for (int s = -1; s <= 1; s += 2)
                Box(g.transform, "Fork", At(c, r, s * 0.19f, 0.045f, 0.55f), new Vector3(0.16f, 0.07f, 1.15f), y, r);
            Box(g.transform, "Body", At(c, r, 0f, 0.2f, -0.08f), new Vector3(0.52f, 0.32f, 0.22f), y, r);
            Cyl(g.transform, "Pump", At(c, r, 0f, 0.3f, -0.12f), At(c, r, 0f, 0.62f, -0.16f), 0.05f, p.DarkPaintedMetal);
            Cyl(g.transform, "Tiller", At(c, r, 0f, 0.6f, -0.16f), At(c, r, 0f, 1.2f, -0.42f), 0.022f, p.DarkPaintedMetal);
            Cyl(g.transform, "Grip", At(c, r, -0.15f, 1.2f, -0.42f), At(c, r, 0.15f, 1.2f, -0.42f), 0.026f, Rubber(p));
            Cyl(g.transform, "SteerWheel", At(c, r, -0.07f, 0.09f, -0.12f), At(c, r, 0.07f, 0.09f, -0.12f), 0.09f, Rubber(p));
        }

        /// <summary>
        /// The run from E-0 to the overlook (road X 505-547, Z -49.8 .. -42.2, top Y 1.0): review 1 called it a fenced
        /// concrete chute with nothing at the end. Kerbs with drains, a dashed centre line, reflector posts every 6 m,
        /// three low sodium road lamps, an assembly-point sign and a burning red flare at the rail as the goal.
        /// Collider-free except the lamp poles (at the road edge, out of the running line).
        /// </summary>
        public static void DressCoastRoad(Transform zone3Root, WorldMaterialPalette p)
        {
            var g = new GameObject("Z23_CoastRoadDressing");
            g.transform.SetParent(zone3Root, false);
            const float y = 1.0f, x0 = 506.0f, x1 = 545.6f, zN = -42.55f, zS = -49.45f;
            Material kerb = Kerb(p), paint = Coping(p);
            foreach (float z in new[] { zN, zS })
            {
                Box(g.transform, "CoastKerb", new Vector3((x0 + x1) * 0.5f, y + 0.03f, z), new Vector3(x1 - x0, 0.06f, 0.3f), kerb, Quaternion.identity);
                float side = z > -46f ? -1f : 1f;
                Box(g.transform, "CoastEdgeLine", new Vector3((x0 + x1) * 0.5f, y + 0.003f, z + side * 0.45f), new Vector3(x1 - x0, 0.006f, 0.1f), paint, Quaternion.identity);
                for (float x = x0 + 4f; x < x1 - 2f; x += 8f)
                {
                    Box(g.transform, "CoastDrain", new Vector3(x, y + 0.002f, z + side * 0.42f), new Vector3(0.9f, 0.006f, 0.42f), DrainVoid(p), Quaternion.identity);
                    var dg = Decal(g.transform, "CoastDrainGrate", new Vector3(x, y + 0.05f, z + side * 0.42f), Vector3.up, new Vector3(0.9f, 0.42f, 0.2f), Grating(p), 1f);
                    dg.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right);
                    dg.uvScale = new Vector2(1f, 2.1f);
                }
            }
            for (float x = x0 + 1.5f; x < x1 - 4f; x += 4.5f)
                Box(g.transform, "CoastCentreDash", new Vector3(x + 1.2f, y + 0.003f, -46f), new Vector3(2.4f, 0.006f, 0.12f), paint, Quaternion.identity);
            Box(g.transform, "CoastStopBar", new Vector3(x1 - 2.2f, y + 0.003f, -46f), new Vector3(0.35f, 0.006f, 6.6f), paint, Quaternion.identity);
            Decal(g.transform, "CoastRoadStain", new Vector3(520f, y + 0.05f, -45f), Vector3.up, new Vector3(4.0f, 3.0f, 0.3f), Grime(p, 0), 0.7f, rollDeg: 40f);
            Decal(g.transform, "CoastRoadCracks", new Vector3(531f, y + 0.05f, -47.4f), Vector3.up, new Vector3(3.0f, 3.0f, 0.3f), Cracks2(p), 0.75f, rollDeg: 110f);

            // reflector posts (delineators) every 6 m, alternating sides: white post, black band, amber reflector
            int k = 0;
            for (float x = x0 + 2f; x < x1 - 1f; x += 6f, k++)
            {
                float z = (k % 2 == 0) ? zN - 0.15f : zS + 0.15f;
                float face = z > -46f ? -1f : 1f;
                Box(g.transform, "Delineator", new Vector3(x, y + 0.5f, z), new Vector3(0.1f, 1.0f, 0.1f), paint, Quaternion.identity);
                Box(g.transform, "DelineatorBand", new Vector3(x, y + 0.9f, z), new Vector3(0.104f, 0.12f, 0.104f), p.DarkPaintedMetal, Quaternion.identity);
                // reflectors face along the road (toward the runner, -X) and back (+X): edge-on to the runner they
                // were sub-pixel (fix round)
                Box(g.transform, "DelineatorReflector", new Vector3(x - 0.053f, y + 0.78f, z), new Vector3(0.006f, 0.1f, 0.05f), Reflector(p), Quaternion.identity);
                Box(g.transform, "DelineatorReflectorBack", new Vector3(x + 0.053f, y + 0.78f, z), new Vector3(0.006f, 0.1f, 0.05f), Reflector(p), Quaternion.identity);
            }

            // three low sodium road lamps at the road edge (AuxYard: on once E-0 has power)
            float[] lampX = { 512f, 526f, 540f };
            for (int i = 0; i < lampX.Length; i++)
            {
                float z = i % 2 == 0 ? zN + 0.05f : zS - 0.05f;
                LampPost(p, g.transform, new Vector3(lampX[i], y, z), z > -46f ? 180f : 0f);
                WorldArchitectureKit.BuildHdrpLight(g.transform, $"Light_PathLamp_Coast_{i}", new Vector3(lampX[i], y + 4.1f, z), new Vector3(90f, 0f, 0f), LightType.Spot,
                    new Color(1.0f, 0.82f, 0.56f), 1600f, 15f, LightGroupId.AuxYard, volumetricDimmer: 0.85f, castShadows: false, spotOuterAngle: 100f);
            }

            // the goal: a back-lit escape-green assembly-point light box at the rail and a burning red road flare in front
            // of it (fix round: the 420 cd flare tinted the rail pink with no visible source; the sign was a dark plate)
            Material assembly = BacklitSign(p, "MAT_Z23_SignAssemblyLit", "fire_assembly", 10f);
            // round 3: a human-scale light box (1.15 m) on two posts, readable from the gate 40 m back (was 0.7 m on one post);
            // fix round: the posts stand behind the box (box back at 545.085) instead of through the lit face
            for (int sgn = -1; sgn <= 1; sgn += 2)
                Cyl(g.transform, "AssemblySignPost", new Vector3(545.13f, y, -43.6f + sgn * 0.5f), new Vector3(545.13f, y + 2.65f, -43.6f + sgn * 0.5f), 0.04f, GalvSteel(p));
            Box(g.transform, "AssemblySignBox", new Vector3(545.03f, y + 2.0f, -43.6f), new Vector3(0.11f, 1.25f, 1.25f), p.DarkPaintedMetal, Quaternion.identity);
            Box(g.transform, "AssemblySignHood", new Vector3(544.93f, y + 2.66f, -43.6f), new Vector3(0.24f, 0.03f, 1.29f), p.DarkPaintedMetal, Quaternion.identity);
            SignPlate(g.transform, "Sign_EvacAssembly_Coast", new Vector3(544.968f, y + 2.0f, -43.6f), Vector3.left, 1.15f, 1.15f, assembly, null);
            Vector3 fl = new Vector3(543.4f, y + 0.02f, -46.6f);
            Vector3 tipA = fl + new Vector3(0.24f, 0.05f, 0.04f), tipB = fl + new Vector3(0.27f, 0.057f, 0.045f);
            Cyl(g.transform, "RoadFlare", fl, tipA, 0.018f, PaintRed(p));
            Cyl(g.transform, "RoadFlareCap", tipA, tipB, 0.02f, p.DarkPaintedMetal);
            // the burning core: a 4 cm ball of ~200 nits at the tip (the source the light comes from)
            GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "RoadFlareCore";
            core.layer = GameLayers.Environment;
            Object.DestroyImmediate(core.GetComponent<Collider>());
            core.transform.SetParent(g.transform, false);
            core.transform.position = tipB + new Vector3(0.012f, 0.004f, 0f);
            core.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
            var coreR = core.GetComponent<MeshRenderer>();
            coreR.sharedMaterial = p.GetOrBuildEmissive("MAT_Z23_FlareCore", new Color(1.0f, 0.36f, 0.18f), 200f);
            coreR.shadowCastingMode = ShadowCastingMode.Off;
            FlareSparks(g.transform, core.transform.position);
            // Light_Evac* is skipped by LookLights.Calibrate, so these builder values are the ones that apply
            WorldArchitectureKit.BuildHdrpLight(g.transform, "Light_EvacFlare_Road", core.transform.position + new Vector3(0f, 0.18f, 0f), Vector3.zero, LightType.Point,
                new Color(1.0f, 0.24f, 0.10f), 150f, 6f, LightGroupId.AuxYard, volumetricDimmer: 0.9f, castShadows: false, pairedEmissives: new Renderer[] { coreR });
            MergeStatic(g.transform);
        }

        /// <summary>A light box sign: the artwork is its own emission map (white figure and green field glow).</summary>
        public static Material BacklitSign(WorldMaterialPalette p, string name, string stem, float nits)
        {
            Material m = SignMat(p, name, stem);
            if (m == null) return null;
            Texture tex = m.HasProperty("_BaseColorMap") ? m.GetTexture("_BaseColorMap") : null;
            m.SetTexture("_EmissiveColorMap", tex);
            m.SetColor("_EmissiveColorLDR", Color.white);
            m.SetColor("_EmissiveColor", Color.white * nits);
            m.SetFloat("_EmissiveIntensity", nits);
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetFloat("_EmissiveExposureWeight", 1f);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A trickle of sparks off the flare tip. The name contains "Spark", so LookWeather.StyleSparks gives it
        /// the spark material, stretch rendering, collisions and the SparkGlow it gives every spark system.</summary>
        private static void FlareSparks(Transform parent, Vector3 at)
        {
            var go = new GameObject("FlareSparksVfx");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(-90f, 0f, 0f));
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.45f, 1f), new Color(1f, 0.35f, 0.15f, 1f));
            main.gravityModifier = 0.9f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 22f;
            var sh = ps.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 30f;
            sh.radius = 0.01f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ materials (round 2)
        public static Material SkipGreen(WorldMaterialPalette p) => Surf(p, "MAT_Z23_SkipGreen", "metal_painted_worn", "green_BaseColor", "MaskMap", 1.3f, new Color(0.30f, 0.40f, 0.28f), 0.20f, 0.55f, 0.4f);
        public static Material PaintRed(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PaintRed", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.8f, new Color(1.55f, 0.36f, 0.30f), 0.25f, 0.60f, 0.3f);
        public static Material PaintYellow(WorldMaterialPalette p) => Surf(p, "MAT_Z23_PaintYellow", "metal_painted_worn", "grey_BaseColor", "MaskMap", 0.8f, new Color(1.6f, 1.25f, 0.35f), 0.25f, 0.60f, 0.3f);
        public static Material Rubber(WorldMaterialPalette p) => Surf(p, "MAT_Z23_Rubber", "asphalt_wet", "BaseColor", "MaskMap", 0.6f, new Color(0.35f, 0.35f, 0.35f), 0.15f, 0.35f);
        public static Material RollerSteel(WorldMaterialPalette p) => Surf(p, "MAT_Z23_RollerDoorSteel", "metal_corrugated_rusty", "BaseColor", "MaskMap", 1.6f, new Color(0.95f, 0.98f, 1.0f), 0.20f, 0.55f, 0.6f);
        public static Material Reflector(WorldMaterialPalette p) => p.GetOrBuildEmissive("MAT_Z23_ReflectorAmber", new Color(1.0f, 0.55f, 0.15f), 3f);
        /// <summary>Frosted lens under a post bulkhead: a warm glow of ~25 nits (the housing itself stays dark).</summary>
        public static Material BulkheadLens(WorldMaterialPalette p) => p.GetOrBuildEmissive("MAT_Z23_BulkheadLens", new Color(1.0f, 0.72f, 0.45f), 25f);
        public static Material ReflectorWhite(WorldMaterialPalette p) => p.GetOrBuildEmissive("MAT_Z23_ReflectorWhite", new Color(0.9f, 0.92f, 0.95f), 1.6f);

        // ------------------------------------------------------------------ round 3: coast road view
        /// <summary>
        /// Review 2 (27_escape_coast_road): both sides of the run to the overlook were 2.2 m chain-link screens and the end
        /// showed no coast. Zone 4 builds the two fence spans after zones 2-3, so this runs from the look pass
        /// (LookRoofs.Apply; everything it makes is under a "LookRoof_" root that the pass clears and rebuilds): the
        /// fences keep their colliders but are not drawn, a galvanised W-beam guard rail with a hand rail at 1.1 m lines
        /// both sides instead, and a rotating navigation light on a mast at the cliff edge sweeps out over the sea.
        /// </summary>
        public static string OpenCoastRoad(Transform world, WorldMaterialPalette p, string rootName)
        {
            Transform fn = FindDeep(world, "CoastRoad_FenceN"), fs = FindDeep(world, "CoastRoad_FenceS");
            if (fn == null || fs == null) return "coast road: fences not found";
            int hidden = 0;
            foreach (Transform f in new[] { fn, fs })
                foreach (MeshRenderer mr in f.GetComponentsInChildren<MeshRenderer>(true))
                    if (mr.enabled) { mr.enabled = false; EditorUtility.SetDirty(mr); hidden++; }
            var g = new GameObject(rootName);
            g.transform.SetParent(world, false);
            g.layer = GameLayers.Environment;
            Transform t = g.transform;
            const float y = 1.0f, x0 = 505.4f, x1 = 546.2f;
            Material galv = GalvSteel(p);
            foreach (float z in new[] { -42.15f, -49.85f })
            {
                float inward = z > -46f ? -1f : 1f;                  // toward the road
                float zb = z + inward * 0.17f;                       // beam face
                Box(t, "GuardBeam", new Vector3((x0 + x1) * 0.5f, y + 0.68f, zb), new Vector3(x1 - x0, 0.31f, 0.05f), galv, Quaternion.identity);
                Box(t, "GuardBeamValley", new Vector3((x0 + x1) * 0.5f, y + 0.68f, zb + inward * 0.026f), new Vector3(x1 - x0, 0.07f, 0.004f), DrainVoid(p), Quaternion.identity);
                Box(t, "GuardBeamLip", new Vector3((x0 + x1) * 0.5f, y + 0.835f, zb + inward * 0.03f), new Vector3(x1 - x0, 0.012f, 0.03f), galv, Quaternion.identity);
                Box(t, "GuardBeamLip", new Vector3((x0 + x1) * 0.5f, y + 0.525f, zb + inward * 0.03f), new Vector3(x1 - x0, 0.012f, 0.03f), galv, Quaternion.identity);
                Cyl(t, "HandRail", new Vector3(x0, y + 1.1f, z), new Vector3(x1, y + 1.1f, z), 0.024f, galv);
                for (float x = x0 + 0.1f; x <= x1; x += 2.0f)
                {
                    Box(t, "GuardPost", new Vector3(x, y + 0.55f, z), new Vector3(0.1f, 1.1f, 0.15f), galv, Quaternion.identity);
                    Box(t, "GuardBlockout", new Vector3(x, y + 0.68f, z + inward * 0.11f), new Vector3(0.08f, 0.2f, 0.08f), p.DarkPaintedMetal, Quaternion.identity);
                    Cyl(t, "HandRailCap", new Vector3(x, y + 1.08f, z), new Vector3(x, y + 1.13f, z), 0.04f, galv);
                }
            }

            // a coastal navigation light at the cliff edge right of the road end: 5.5 m galvanised mast on a footing,
            // a lantern with a slowly rotating white beam (LightGroupMember rotation) aimed a few degrees down over the sea
            Vector3 mb = new Vector3(548.6f, 0f, -51.2f);
            if (Physics.Raycast(mb + Vector3.up * 30f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)) mb.y = hit.point.y;
            else mb.y = 0.6f;
            Box(t, "NavLightFooting", mb + Vector3.up * 0.15f, new Vector3(0.9f, 0.5f, 0.9f), Coping(p), Quaternion.identity);
            Cyl(t, "NavLightMast", mb + Vector3.up * 0.4f, mb + Vector3.up * 5.5f, 0.09f, galv);
            Box(t, "NavLightPlatform", mb + Vector3.up * 5.5f, new Vector3(0.9f, 0.05f, 0.9f), p.DarkPaintedMetal, Quaternion.identity);
            for (int k = 0; k < 4; k++)
            {
                Vector3 o = Quaternion.Euler(0f, 45f + 90f * k, 0f) * new Vector3(0.6f, 0f, 0f);
                Cyl(t, "NavLightRailPost", mb + o + Vector3.up * 5.5f, mb + o + Vector3.up * 6.15f, 0.015f, galv);
            }
            Cyl(t, "NavLightLanternBase", mb + Vector3.up * 5.52f, mb + Vector3.up * 5.75f, 0.22f, p.DarkPaintedMetal);
            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "NavLightLens"; lens.layer = GameLayers.Environment;
            Object.DestroyImmediate(lens.GetComponent<Collider>());
            lens.transform.SetParent(t, false);
            lens.transform.position = mb + Vector3.up * 5.95f;
            lens.transform.localScale = new Vector3(0.36f, 0.2f, 0.36f);
            var lensR = lens.GetComponent<MeshRenderer>();
            lensR.sharedMaterial = p.GetOrBuildEmissive("MAT_Z23_NavLightLens", new Color(1.0f, 0.95f, 0.85f), 22f);
            lensR.shadowCastingMode = ShadowCastingMode.Off;
            Cyl(t, "NavLightRoof", mb + Vector3.up * 6.15f, mb + Vector3.up * 6.3f, 0.25f, p.DarkPaintedMetal);
            // fix round: level beam (a tilted spot turning about its own up axis dipped and rose 4 deg per turn), 30 deg/s
            // (12 s a turn; the component default is 140), and a name in the lamp table without "Beacon" (no fixture
            // body). This runs after LookLights.Calibrate, so the rule is applied here.
            Light beam = WorldArchitectureKit.BuildHdrpLight(t, "Light_CoastNavLantern", mb + Vector3.up * 5.95f, new Vector3(0f, 70f, 0f), LightType.Spot,
                new Color(1.0f, 0.95f, 0.86f), 26000f, 90f, LightGroupId.AuxYard, volumetricDimmer: 0.8f, castShadows: false, spotOuterAngle: 9f,
                pairedEmissives: new Renderer[] { lensR }, rotatingBeacon: true);
            if (beam != null)
            {
                Cretacea.Editor.Look.LookLights.ApplyRuleTo(beam);
                foreach (LightGroupMember lgm in beam.GetComponentsInParent<LightGroupMember>(true))
                {
                    var so = new SerializedObject(lgm);
                    SerializedProperty sp = so.FindProperty("rotationSpeedDegPerSec");
                    if (sp != null) { sp.floatValue = 30f; so.ApplyModifiedPropertiesWithoutUndo(); }
                }
            }
            MergeStatic(t);
            return $"coast road: {hidden} fence renderers hidden, guard rails + nav light ({(beam != null ? "lit" : "no light")})";
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform c in root.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return null;
        }

        // ------------------------------------------------------------------ round 3: LED flood head
        /// <summary>Flood lens: cool white at ~9 nits (review 2: the 25-nit lens clipped to a white card at storm
        /// exposure, so the housing, fins and lens structure were lost).</summary>
        public static Material FloodLens(WorldMaterialPalette p) => p.GetOrBuildEmissive("MAT_Z23_FloodLensLed", new Color(0.94f, 0.95f, 1.0f), 4f);

        /// <summary>
        /// An LED area flood head (~0.56 x 0.42 m) centred on pos, its lens looking along aim: die-cast housing, a bank of
        /// vertical cooling fins on the back, a bezel, a lens split by a dark reflector grid into 4 x 3 LED cells, a visor,
        /// a U-yoke whose pivot bolts sit on the housing sides, and a cable gland. Collider-free. Returns the lens cells.
        /// </summary>
        public static List<Renderer> FloodHead(Transform parent, string name, Vector3 pos, Vector3 aim, WorldMaterialPalette p)
        {
            var root = new GameObject(name);
            root.layer = GameLayers.Environment;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim.normalized, Vector3.up));
            Transform t = root.transform;
            Material body = p.DarkPaintedMetal, fin = GalvSteel(p), lensM = FloodLens(p);
            const float w = 0.56f, h = 0.42f, d = 0.10f;
            GameObject B(string n, Vector3 lp, Vector3 size, Material m)
            {
                GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                g.name = n; g.layer = GameLayers.Environment;
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.SetParent(t, false);
                g.transform.localPosition = lp; g.transform.localScale = size;
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                return g;
            }
            B("Housing", new Vector3(0f, 0f, -d * 0.5f), new Vector3(w, h, d), body);
            // bezel: a 4 cm galvanised frame standing 1.5 cm proud of the lens (fix round: the 2.5 cm dark frame was lost
            // in the halo; a light metal frame reads against it)
            const float bz = 0.04f;
            B("BezelT", new Vector3(0f, h * 0.5f - bz * 0.5f, 0.0075f), new Vector3(w, bz, 0.015f), fin);
            B("BezelB", new Vector3(0f, -h * 0.5f + bz * 0.5f, 0.0075f), new Vector3(w, bz, 0.015f), fin);
            B("BezelL", new Vector3(-w * 0.5f + bz * 0.5f, 0f, 0.0075f), new Vector3(bz, h, 0.015f), fin);
            B("BezelR", new Vector3(w * 0.5f - bz * 0.5f, 0f, 0.0075f), new Vector3(bz, h, 0.015f), fin);
            // LED cells: each cell of the 4 x 3 reflector grid is a dark reflector with 3 x 3 LED dots on it (fix round:
            // a flat lit cell clips to a white card; dots over a dark ground still read as dots when they clip)
            var cells = new List<Renderer>();
            float iw = w - 2f * bz, ih = h - 2f * bz;
            const int nx = 4, ny = 3; const float bar = 0.016f, dot = 0.026f;
            float cw = (iw - bar * (nx - 1)) / nx, ch = (ih - bar * (ny - 1)) / ny;
            for (int ix = 0; ix < nx; ix++)
                for (int iy = 0; iy < ny; iy++)
                {
                    float x = -iw * 0.5f + cw * 0.5f + ix * (cw + bar), y = -ih * 0.5f + ch * 0.5f + iy * (ch + bar);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            GameObject cell = B($"LedDot_{ix}{iy}", new Vector3(x + dx * cw * 0.31f, y + dy * ch * 0.31f, 0.004f), new Vector3(dot, dot, 0.006f), lensM);
                            cell.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                            cells.Add(cell.GetComponent<MeshRenderer>());
                        }
                }
            for (int ix = 1; ix < nx; ix++) B("GridV", new Vector3(-iw * 0.5f + ix * (cw + bar) - bar * 0.5f, 0f, 0.004f), new Vector3(bar, ih, 0.012f), fin);
            for (int iy = 1; iy < ny; iy++) B("GridH", new Vector3(0f, -ih * 0.5f + iy * (ch + bar) - bar * 0.5f, 0.004f), new Vector3(iw, bar, 0.012f), fin);
            // visor over the lens, tipped down 12 degrees
            GameObject visor = B("Visor", new Vector3(0f, h * 0.5f + 0.004f, 0.05f), new Vector3(w + 0.02f, 0.008f, 0.12f), body);
            visor.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            // cooling fins on the back: 11 vertical blades on a back plate
            B("FinBase", new Vector3(0f, 0f, -d - 0.006f), new Vector3(w - 0.04f, h - 0.04f, 0.012f), fin);
            for (int k = 0; k < 11; k++)
                B("Fin", new Vector3(-w * 0.5f + 0.04f + k * (w - 0.08f) / 10f, 0f, -d - 0.045f), new Vector3(0.008f, h - 0.06f, 0.07f), fin);
            // U-yoke: side arms on pivot bolts at the housing sides, a bottom bar behind the fins carrying the mount
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                B("YokeArm", new Vector3(sgn * (w * 0.5f + 0.02f), -0.06f, -0.06f), new Vector3(0.012f, 0.22f, 0.05f), body);
                GameObject bolt = B("PivotBolt", new Vector3(sgn * (w * 0.5f + 0.03f), 0f, -0.06f), new Vector3(0.012f, 0.05f, 0.05f), fin);
                bolt.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            B("YokeBar", new Vector3(0f, -0.17f, -0.06f), new Vector3(w + 0.052f, 0.03f, 0.05f), body);
            B("YokeStem", new Vector3(0f, -0.17f, -0.16f), new Vector3(0.06f, 0.03f, 0.17f), body);
            B("CableGland", new Vector3(0.18f, -h * 0.5f - 0.015f, -0.05f), new Vector3(0.03f, 0.03f, 0.03f), fin);
            // perf: the 108 LED dots become one lens renderer (the lamp's paired emissive) and the ~30 body parts one mesh per
            // material (369 renderers on the 8 heads cost ~0.7 ms GPU in the substation view)
            var combine = new List<CombineInstance>();
            Matrix4x4 toRoot = t.worldToLocalMatrix;
            foreach (Renderer c in cells) combine.Add(new CombineInstance { mesh = c.GetComponent<MeshFilter>().sharedMesh, transform = toRoot * c.transform.localToWorldMatrix });
            var lensMesh = new Mesh { name = "Z23_FloodLedCells" };
            lensMesh.CombineMeshes(combine.ToArray(), true, true);
            lensMesh.RecalculateBounds();
            foreach (Renderer c in cells) Object.DestroyImmediate(c.gameObject);
            var lensGo = new GameObject("LedCells") { layer = GameLayers.Environment };
            lensGo.transform.SetParent(t, false);
            lensGo.AddComponent<MeshFilter>().sharedMesh = lensMesh;
            var lensR = lensGo.AddComponent<MeshRenderer>();
            lensR.sharedMaterial = lensM;
            lensR.shadowCastingMode = ShadowCastingMode.Off;
            MergeStatic(t);
            return new List<Renderer> { lensR };
        }

        // ------------------------------------------------------------------ round 3: corrugated steel
        private static readonly Dictionary<string, Mesh> s_corrugated = new Dictionary<string, Mesh>();

        /// <summary>
        /// A trapezoid-corrugated steel sheet (ISO container side profile): 'width' along local x, 'height' along local
        /// y, the profile pressed back along -z from z = 0 (outer flats) to z = -depth (inner flats). Flat-shaded, so
        /// every sloped web catches the light differently (review 2: the box ribs read as planks). Single-sided, facing +z.
        /// Pitch 0.278 m: outer flat 0.07, web 0.068, inner flat 0.072, web 0.068.
        /// </summary>
        public static Mesh CorrugatedSheet(float width, float height, float depth = 0.036f, float pitch = 0.278f)
        {
            string key = $"{width:F3}_{height:F3}_{depth:F3}_{pitch:F3}";
            if (s_corrugated.TryGetValue(key, out Mesh cached) && cached != null) return cached;
            float of = pitch * 0.252f, web = pitch * 0.245f, inf = pitch * 0.258f;
            var xs = new List<float>(); var zs = new List<float>();
            float x = -width * 0.5f; int phase = 0;
            xs.Add(x); zs.Add(0f);
            float[] seg = { of, web, inf, web };
            float[] zEnd = { 0f, -depth, -depth, 0f };
            while (x < width * 0.5f - 1e-4f)
            {
                float step = Mathf.Min(seg[phase], width * 0.5f - x);
                float z0 = zs[zs.Count - 1];
                float z1 = zEnd[phase];
                // a clipped web ends part-way down
                if (step < seg[phase] - 1e-4f && (phase == 1 || phase == 3)) z1 = Mathf.Lerp(z0, zEnd[phase], step / seg[phase]);
                x += step; xs.Add(x); zs.Add(z1);
                phase = (phase + 1) % 4;
            }
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            for (int i = 0; i < xs.Count - 1; i++)
            {
                int o = v.Count;
                v.Add(new Vector3(xs[i], 0f, zs[i])); v.Add(new Vector3(xs[i + 1], 0f, zs[i + 1]));
                v.Add(new Vector3(xs[i + 1], height, zs[i + 1])); v.Add(new Vector3(xs[i], height, zs[i]));
                uv.Add(new Vector2(xs[i], 0f)); uv.Add(new Vector2(xs[i + 1], 0f)); uv.Add(new Vector2(xs[i + 1], height)); uv.Add(new Vector2(xs[i], height));
                tri.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });   // clockwise seen from +z: the front face looks along +z
            }
            var m = new Mesh { name = "Z23_Corrugated_" + key };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
            s_corrugated[key] = m;
            return m;
        }

        /// <summary>Places a corrugated sheet: its centre at local 'centre' under parent, outer face looking along 'outward'
        /// (parent-local), height along parent-local up.</summary>
        public static GameObject CorrugatedPanel(Transform parent, string name, Vector3 localCentre, Vector3 localOutward, float width, float height, Material mat, float depth = 0.036f)
        {
            var go = new GameObject(name);
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localCentre - Vector3.up * (height * 0.5f);
            go.transform.localRotation = Quaternion.LookRotation(localOutward, Vector3.up);
            go.AddComponent<MeshFilter>().sharedMesh = CorrugatedSheet(width, height, depth);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}
