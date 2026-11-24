using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// The dawn coast dressing that the ending camera frames: the Coast Guard rescue cutter in the bay (a lofted hull
    /// built from code: bow flare, sheer, boot-top, the slanted orange-and-blue racing stripe, deckhouse, raked
    /// wheelhouse windows, mast with radar and running lights, rails, a rescue tender and a foam wake on the HDRP sea).
    /// Materials are plain painted HDRP Lit surfaces in Assets/Art/Terrain/Coast (a 25 m boat seen from 175 m).
    /// </summary>
    public static class LookTerrainCoast
    {
        public const string Folder = "Assets/Art/Terrain/Coast";

        // ------------------------------------------------------------------------------------------------------------
        // MATERIALS
        // ------------------------------------------------------------------------------------------------------------
        private static Material Paint(string name, Color srgb, float smoothness, float metallic = 0f)
        {
            WorldMaterialPalette.EnsureFolder(Folder);
            string path = $"{Folder}/MAT_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("HDRP/Lit")) { name = "MAT_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", srgb);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material s_hullWhite, s_bottom, s_orange, s_blue, s_deck, s_glass, s_grey, s_black, s_tender;

        private static void EnsureMaterials()
        {
            s_hullWhite = Paint("Boat_HullWhite", new Color(0.86f, 0.87f, 0.86f), 0.62f);
            s_bottom = Paint("Boat_Antifoul", new Color(0.17f, 0.07f, 0.06f), 0.35f);
            s_orange = Paint("Boat_RacingOrange", new Color(0.93f, 0.33f, 0.07f), 0.55f);
            s_blue = Paint("Boat_RacingBlue", new Color(0.07f, 0.17f, 0.42f), 0.55f);
            s_deck = Paint("Boat_DeckGrey", new Color(0.36f, 0.38f, 0.38f), 0.25f);
            s_glass = Paint("Boat_BridgeGlass", new Color(0.03f, 0.04f, 0.05f), 0.95f);
            s_grey = Paint("Boat_MastGrey", new Color(0.62f, 0.63f, 0.63f), 0.5f, 0.4f);
            s_black = Paint("Boat_Black", new Color(0.05f, 0.05f, 0.05f), 0.4f);
            s_tender = Paint("Boat_TenderOrange", new Color(0.95f, 0.40f, 0.08f), 0.4f);
        }

        // ------------------------------------------------------------------------------------------------------------
        // HULL (local: +X bow, +Y up with the waterline at 0, +Z port)
        // ------------------------------------------------------------------------------------------------------------
        private const float HullLength = 25f, HalfBeam = 3.25f;
        private const int Stations = 160;

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float StationX(float t) => -HullLength * 0.5f + HullLength * t;

        /// <summary>The topsides' station lines lean forward like the racing stripe (x + 0.55 y, faded out at both
        /// ends) so the stripe edges follow mesh edges: a clean slanted band instead of a staircase.</summary>
        private const float StripeLean = 0.55f;
        private static float Shear(float t, float y) => StripeLean * (y - 1.2f) * SS(0.05f, 0.3f, t) * SS(0.97f, 0.8f, t);
        /// <summary>Deck height: the sheer rises from a low working stern to a high flared bow.</summary>
        private static float Sheer(float t) => 2.05f + 0.35f * t + 1.25f * Mathf.Pow(SS(0.5f, 1f, t), 1.6f);
        private static float DeckHalf(float t)
        {
            float bow = t > 0.52f ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((t - 0.52f) / 0.48f, 2f))) : 1f;
            float stern = 0.93f + 0.07f * SS(0f, 0.18f, t);
            return HalfBeam * bow * stern;
        }
        private static float Keel(float t) => -1.35f + 0.25f * SS(0.15f, 0f, t) + 1.15f * Mathf.Pow(SS(0.72f, 1f, t), 1.4f);

        /// <summary>Port half-section at station t: keel -> deadrise bottom -> chine -> flared topsides -> deck edge.</summary>
        private static Vector2[] Section(float t)
        {
            float hb = DeckHalf(t), ys = Sheer(t), yk = Keel(t);
            float chineY = Mathf.Lerp(-0.25f, 0.35f, SS(0.6f, 1f, t));
            float chineZ = hb * Mathf.Lerp(0.93f, 0.55f, SS(0.55f, 1f, t));       // narrow, deep V forward: the bow flares out above it
            float deadMid = Mathf.Lerp(yk, chineY, 0.55f + 0.2f * SS(0.6f, 1f, t));
            var p = new List<Vector2>
            {
                new Vector2(0f, yk),
                new Vector2(chineZ * 0.5f, deadMid),
                new Vector2(chineZ, chineY),
            };
            const int sideSeg = 7;
            for (int i = 1; i <= sideSeg; i++)
            {
                float s = i / (float)sideSeg;
                // topsides bulge slightly out then come in a little at the gunwale (flare forward, mild tumblehome aft)
                float z = Mathf.Lerp(chineZ, hb, Mathf.Pow(s, 0.65f));
                float y = Mathf.Lerp(chineY, ys, s);
                p.Add(new Vector2(z, y));
            }
            return p.ToArray();
        }

        private static Mesh BuildHullMesh()
        {
            var v = new List<Vector3>();
            int sec = Section(0f).Length;
            // [0] white topsides, [1] antifouling below the boot-top, [2] orange stripe, [3] blue stripe, [4] deck, [5] black boot-top
            var tris = new List<int>[6];
            for (int i = 0; i < tris.Length; i++) tris[i] = new List<int>();

            for (int side = 0; side < 2; side++)
            {
                float zs = side == 0 ? 1f : -1f;
                int baseV = v.Count;
                for (int i = 0; i <= Stations; i++)
                {
                    float t = i / (float)Stations;
                    Vector2[] s = Section(t);
                    for (int k = 0; k < sec; k++) v.Add(new Vector3(StationX(t) + Shear(t, s[k].y), s[k].y, s[k].x * zs));
                }
                for (int i = 0; i < Stations; i++)
                {
                    for (int k = 0; k < sec - 1; k++)
                    {
                        int a = baseV + i * sec + k, b = a + 1, c = a + sec, d = c + 1;
                        Vector3 cen = (v[a] + v[b] + v[c] + v[d]) * 0.25f;
                        int m = HullPaint(cen);
                        // outward winding (clockwise seen from outside); the starboard half is mirrored
                        if (side == 0) { tris[m].Add(a); tris[m].Add(c); tris[m].Add(b); tris[m].Add(b); tris[m].Add(c); tris[m].Add(d); }
                        else { tris[m].Add(a); tris[m].Add(b); tris[m].Add(c); tris[m].Add(b); tris[m].Add(d); tris[m].Add(c); }
                    }
                }
            }
            // deck (own vertices: hard edge at the gunwale)
            int deckBase = v.Count;
            for (int i = 0; i <= Stations; i++)
            {
                float t = i / (float)Stations;
                float x = StationX(t), y = Sheer(t) - 0.02f, hb = DeckHalf(t);
                x += Shear(t, y + 0.02f);
                v.Add(new Vector3(x, y, hb)); v.Add(new Vector3(x, y + 0.05f, 0f)); v.Add(new Vector3(x, y, -hb));
            }
            for (int i = 0; i < Stations; i++)
            {
                int a = deckBase + i * 3;
                for (int k = 0; k < 2; k++)
                {
                    int p0 = a + k, p1 = a + k + 1, q0 = p0 + 3, q1 = p1 + 3;
                    tris[4].Add(p0); tris[4].Add(q0); tris[4].Add(p1);
                    tris[4].Add(p1); tris[4].Add(q0); tris[4].Add(q1);
                }
            }
            // transom: a flat stern face (fan from its centre)
            int tb = v.Count;
            Vector2[] st = Section(0f);
            v.Add(new Vector3(StationX(0f), (Sheer(0f) + Keel(0f)) * 0.5f, 0f));
            for (int k = 0; k < sec; k++) v.Add(new Vector3(StationX(0f), st[k].y, st[k].x));
            for (int k = sec - 1; k >= 0; k--) v.Add(new Vector3(StationX(0f), st[k].y, -st[k].x));
            int ring = sec * 2;
            for (int k = 0; k < ring - 1; k++)
            {
                int a = tb + 1 + k, b = tb + 2 + k;
                int m = v[a].y < 0.32f ? 1 : 0;
                tris[m].Add(tb); tris[m].Add(a); tris[m].Add(b);
            }
            var mesh = new Mesh { name = "CoastGuardCutter_Hull", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) mesh.SetTriangles(tris[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Hull paint by position: antifouling below the boot-top, black boot-top, white topsides with the
        /// slanted Coast Guard racing stripe (wide orange, narrow blue) a third of the way back from the bow.</summary>
        private static int HullPaint(Vector3 c)
        {
            if (c.y < 0.18f) return 1;
            if (c.y < 0.42f) return 5;
            float s = c.x - StripeLean * c.y;      // leans forward towards the top
            if (s > 4.2f && s < 6.0f) return 2;
            if (s > 6.25f && s < 6.75f) return 3;
            return 0;
        }

        private static Mesh SaveMesh(Mesh m, string file)
        {
            string path = $"{Folder}/{file}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(m, path); return m; }
            existing.Clear();
            existing.indexFormat = m.indexFormat;
            existing.SetVertices(m.vertices);
            existing.subMeshCount = m.subMeshCount;
            for (int i = 0; i < m.subMeshCount; i++) existing.SetTriangles(m.GetTriangles(i), i);
            existing.SetNormals(m.normals);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(m);
            return existing;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 localPos, Quaternion localRot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            go.layer = GameLayers.Environment;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return go;
        }

        private static Mesh Builtin(string file) => Resources.GetBuiltinResource<Mesh>(file);

        /// <summary>A part made of a built-in primitive (no collider).</summary>
        private static GameObject Part(Transform parent, string name, string primitive, Material mat, Vector3 pos, Vector3 scale, Vector3? euler = null)
        {
            return MeshObject(parent, name, Builtin(primitive), new[] { mat }, pos, Quaternion.Euler(euler ?? Vector3.zero), scale);
        }

        /// <summary>A thin round bar between two local points (rails, stays, mast members).</summary>
        private static GameObject Bar(Transform parent, string name, Material mat, Vector3 a, Vector3 b, float diameter)
        {
            Vector3 mid = (a + b) * 0.5f, dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-3f) return null;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
            var go = MeshObject(parent, name, Builtin("Cylinder.fbx"), new[] { mat }, mid, rot, new Vector3(diameter, len * 0.5f, diameter));
            go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A box tapered towards its top (deckhouse and wheelhouse with tumblehome and raked fronts).</summary>
        private static Mesh Frustum(string name, float x0, float x1, float w0, float y0, float y1, float topX0, float topX1, float w1)
        {
            Vector3[] c =
            {
                new Vector3(x0, y0, -w0), new Vector3(x1, y0, -w0), new Vector3(x1, y0, w0), new Vector3(x0, y0, w0),
                new Vector3(topX0, y1, -w1), new Vector3(topX1, y1, -w1), new Vector3(topX1, y1, w1), new Vector3(topX0, y1, w1),
            };
            int[][] faces =
            {
                new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 }, new[] { 4, 5, 6, 7 }, new[] { 3, 2, 1, 0 },
            };
            var v = new List<Vector3>(); var t = new List<int>();
            foreach (int[] f in faces)
            {
                int b = v.Count;
                foreach (int k in f) v.Add(c[k]);
                t.Add(b); t.Add(b + 2); t.Add(b + 1); t.Add(b); t.Add(b + 3); t.Add(b + 2);
            }
            var m = new Mesh { name = name };
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------------------------------------------------
        // THE CUTTER
        // ------------------------------------------------------------------------------------------------------------
        /// <summary>Builds the rescue cutter under <paramref name="root"/> (local +X = bow, waterline at local y 0).
        /// Returns the masthead and searchlight positions in local space for the lights the caller places.</summary>
        public static (Vector3 masthead, Vector3 searchlight) BuildCutter(Transform root, WorldMaterialPalette p)
        {
            EnsureMaterials();
            Mesh hull = SaveMesh(BuildHullMesh(), "CoastGuardCutter_Hull");
            MeshObject(root, "Cutter_Hull", hull, new[] { s_hullWhite, s_bottom, s_orange, s_blue, s_deck, s_black }, Vector3.zero, Quaternion.identity, Vector3.one);

            float deckY = Sheer(0.42f);
            // deckhouse (two levels) with tumblehome; the wheelhouse front is raked forward (reverse rake windows)
            Mesh house = SaveMesh(Frustum("Cutter_Deckhouse", -5.4f, 3.6f, 2.55f, deckY - 0.1f, deckY + 2.0f, -5.2f, 3.2f, 2.35f), "CoastGuardCutter_Deckhouse");
            MeshObject(root, "Cutter_Deckhouse", house, new[] { s_hullWhite }, Vector3.zero, Quaternion.identity, Vector3.one);
            float whY = deckY + 2.0f;
            Mesh wheel = SaveMesh(Frustum("Cutter_Wheelhouse", -2.6f, 2.6f, 2.25f, whY, whY + 1.9f, -2.5f, 3.15f, 2.05f), "CoastGuardCutter_Wheelhouse");
            MeshObject(root, "Cutter_Wheelhouse", wheel, new[] { s_hullWhite }, Vector3.zero, Quaternion.identity, Vector3.one);
            // roof with an overhanging brow
            Part(root, "Cutter_WheelhouseRoof", "Cube.fbx", s_hullWhite, new Vector3(0.45f, whY + 1.96f, 0f), new Vector3(6.2f, 0.14f, 4.5f));

            // bridge windows: a dark band on the raked front (three panes) and down both sides
            float wy0 = whY + 0.85f, wy1 = whY + 1.65f;
            for (int i = 0; i < 3; i++)
            {
                float zc = (i - 1) * 1.33f;
                Vector3 bottom = new Vector3(Mathf.Lerp(2.6f, 3.15f, 0.85f / 1.9f), wy0, zc), top = new Vector3(Mathf.Lerp(2.6f, 3.15f, 1.65f / 1.9f), wy1, zc);
                Vector3 mid = (bottom + top) * 0.5f + Vector3.right * 0.03f;
                float rake = Mathf.Atan2(top.x - bottom.x, top.y - bottom.y) * Mathf.Rad2Deg;
                Part(root, $"Cutter_BridgeWindowFront_{i}", "Cube.fbx", s_glass, mid, new Vector3(0.03f, (top - bottom).magnitude, 1.18f), new Vector3(0f, 0f, -rake));
            }
            foreach (float zs in new[] { 1f, -1f })
            {
                Part(root, "Cutter_BridgeWindowSide", "Cube.fbx", s_glass, new Vector3(0.6f, (wy0 + wy1) * 0.5f, zs * 2.17f), new Vector3(3.6f, 0.78f, 0.03f));
                // portholes on the lower deckhouse
                for (int k = 0; k < 4; k++)
                    Part(root, "Cutter_Porthole", "Cylinder.fbx", s_glass, new Vector3(-3.8f + k * 1.6f, deckY + 1.15f, zs * 2.47f), new Vector3(0.42f, 0.02f, 0.42f), new Vector3(90f, 0f, 0f));
                // running lights on the wheelhouse wings: red port, green starboard
                Part(root, zs > 0 ? "Cutter_NavLight_Port" : "Cutter_NavLight_Stbd", "Cube.fbx", zs > 0 ? p.EmissiveRed : p.EmissiveGreen, new Vector3(2.2f, whY + 1.2f, zs * 2.2f), new Vector3(0.28f, 0.22f, 0.12f));
                // life-raft canisters on the deckhouse side deck
                for (int k = 0; k < 2; k++)
                    Part(root, "Cutter_LifeRaft", "Capsule.fbx", s_hullWhite, new Vector3(-4.4f + k * 1.3f, deckY + 2.35f, zs * 1.7f), new Vector3(0.62f, 0.55f, 0.62f), new Vector3(0f, 0f, 90f));
            }

            // mast on the wheelhouse roof: tapered pole, yard, stays, open-array radar, radome, masthead lights
            float roofY = whY + 2.03f;
            Vector3 mastBase = new Vector3(0.2f, roofY, 0f), mastTop = new Vector3(-0.15f, roofY + 5.2f, 0f);
            Bar(root, "Cutter_Mast", s_grey, mastBase, mastTop, 0.24f);
            Bar(root, "Cutter_MastStayP", s_grey, new Vector3(-1.4f, roofY, 1.6f), mastTop + Vector3.down * 2.1f, 0.08f);
            Bar(root, "Cutter_MastStayS", s_grey, new Vector3(-1.4f, roofY, -1.6f), mastTop + Vector3.down * 2.1f, 0.08f);
            Bar(root, "Cutter_Yard", s_grey, new Vector3(-0.05f, roofY + 3.6f, 1.7f), new Vector3(-0.05f, roofY + 3.6f, -1.7f), 0.1f);
            Part(root, "Cutter_RadarPedestal", "Cylinder.fbx", s_grey, new Vector3(-0.05f, roofY + 2.95f, 0f), new Vector3(0.5f, 0.12f, 0.5f));
            Part(root, "Cutter_RadarArray", "Cube.fbx", s_black, new Vector3(-0.05f, roofY + 3.2f, 0f), new Vector3(0.28f, 0.26f, 3.0f), new Vector3(0f, 18f, 0f));
            Part(root, "Cutter_Radome", "Sphere.fbx", s_hullWhite, new Vector3(-1.5f, roofY + 0.55f, 0f), new Vector3(1.0f, 0.75f, 1.0f));
            Part(root, "Cutter_MastheadLight", "Sphere.fbx", p.EmissiveWarmWhite, mastTop + Vector3.up * 0.15f, Vector3.one * 0.3f);
            Part(root, "Cutter_BlueStrobe", "Sphere.fbx", p.EmissiveCyan, new Vector3(-0.05f, roofY + 3.75f, 1.6f), Vector3.one * 0.22f);
            Part(root, "Cutter_BlueStrobe", "Sphere.fbx", p.EmissiveCyan, new Vector3(-0.05f, roofY + 3.75f, -1.6f), Vector3.one * 0.22f);
            // searchlight on the wheelhouse roof brow
            Vector3 search = new Vector3(2.9f, roofY + 0.3f, 0.9f);
            Part(root, "Cutter_Searchlight", "Cylinder.fbx", s_grey, search, new Vector3(0.45f, 0.25f, 0.45f), new Vector3(0f, 0f, 90f));
            // exhaust stack aft of the wheelhouse
            Mesh stack = SaveMesh(Frustum("Cutter_Stack", -4.4f, -3.0f, 0.55f, whY - 0.1f, whY + 1.5f, -4.7f, -3.5f, 0.45f), "CoastGuardCutter_Stack");
            MeshObject(root, "Cutter_Stack", stack, new[] { s_hullWhite }, Vector3.zero, Quaternion.identity, Vector3.one);
            Part(root, "Cutter_StackCap", "Cube.fbx", s_black, new Vector3(-4.1f, whY + 1.52f, 0f), new Vector3(1.25f, 0.1f, 0.95f));

            // dark rubbing strake along the gunwale (gives the white topsides a sheer line)
            for (int side = 0; side < 2; side++)
            {
                float zs = side == 0 ? 1f : -1f;
                Vector3 prev = Vector3.zero;
                for (int i = 0; i <= 24; i++)
                {
                    float t = Mathf.Lerp(0.01f, 0.985f, i / 24f);
                    float y = Sheer(t) - 0.12f;
                    var q = new Vector3(StationX(t) + Shear(t, y), y, (DeckHalf(t) + 0.04f) * zs);
                    if (i > 0) Bar(root, "Cutter_RubRail", s_black, prev, q, 0.16f);
                    prev = q;
                }
            }

            // rails along the foredeck and the aft working deck: stanchions every ~1.4 m, two rails
            BuildRails(root);

            // rescue tender (orange RHIB) on the aft deck under a davit
            float aftY = Sheer(0.12f);
            Part(root, "Cutter_TenderTubes", "Capsule.fbx", s_tender, new Vector3(-9.2f, aftY + 0.4f, 0.4f), new Vector3(0.8f, 1.9f, 1.6f), new Vector3(0f, 0f, 90f));
            Part(root, "Cutter_TenderConsole", "Cube.fbx", s_black, new Vector3(-8.9f, aftY + 0.85f, 0.4f), new Vector3(0.5f, 0.4f, 0.45f));
            Bar(root, "Cutter_DavitPost", s_grey, new Vector3(-8.0f, aftY, -1.8f), new Vector3(-8.0f, aftY + 2.6f, -1.8f), 0.2f);
            Bar(root, "Cutter_DavitArm", s_grey, new Vector3(-8.0f, aftY + 2.6f, -1.8f), new Vector3(-9.1f, aftY + 2.4f, 0.4f), 0.14f);

            // anchor, bow name panel (dark), hull number in white on the bow is too small at this distance
            Part(root, "Cutter_Anchor", "Cube.fbx", s_black, new Vector3(10.6f, Sheer(0.92f) - 0.6f, 0.62f), new Vector3(0.5f, 0.45f, 0.08f));
            return (mastTop + Vector3.up * 0.4f, search + Vector3.right * 0.3f);
        }


        private static void BuildRails(Transform root)
        {
            var rails = new GameObject("Cutter_Rails").transform;
            rails.SetParent(root, false);
            foreach (float zs in new[] { 1f, -1f })
            {
                foreach (var span in new[] { (a: 0.05f, b: 0.27f), (a: 0.66f, b: 0.97f) })
                {
                    Vector3 prevTop = Vector3.zero, prevMid = Vector3.zero;
                    int n = Mathf.Max(2, Mathf.RoundToInt((span.b - span.a) * HullLength / 1.4f));
                    bool have = false;
                    for (int i = 0; i <= n; i++)
                    {
                        float t = Mathf.Lerp(span.a, span.b, i / (float)n);
                        float y = Sheer(t);
                        var foot = new Vector3(StationX(t) + Shear(t, y), y, (DeckHalf(t) - 0.12f) * zs);
                        if (Mathf.Abs(foot.z) < 0.3f) continue;
                        Vector3 top = foot + Vector3.up * 1.0f, mid = foot + Vector3.up * 0.5f;
                        Bar(rails, "Stanchion", s_grey, foot, top, 0.05f);
                        if (have)
                        {
                            Bar(rails, "TopRail", s_grey, prevTop, top, 0.05f);
                            Bar(rails, "MidRail", s_grey, prevMid, mid, 0.035f);
                        }
                        prevTop = top; prevMid = mid; have = true;
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------
        // OVERLOOK RAIL (the silhouette of the final shot) + the green route marker at the end of the coast road
        // ------------------------------------------------------------------------------------------------------------
        /// <summary>Galvanised pipe guard rail across the end of the coast road at x = <paramref name="x"/>: round posts on
        /// bolted base plates, a top rail with rounded ends and a mid rail, and a green escape-route marker lamp on a
        /// short mast at the north end (AuxRoute group, so it is lit from aux power to the dawn).</summary>
        public static void BuildOverlookRail(Transform parent, WorldMaterialPalette p, float x, float roadY, float z0, float z1)
        {
            EnsureMaterials();
            var root = new GameObject("Overlook_GuardRail").transform;
            root.SetParent(parent, false);
            Material steel = p.BrushedSteel;
            int posts = 5;
            for (int i = 0; i < posts; i++)
            {
                float z = Mathf.Lerp(z0 + 0.1f, z1 - 0.1f, i / (float)(posts - 1));
                var foot = new Vector3(x, roadY, z);
                var post = Bar(root, $"OverlookPost_{i}", steel, foot, foot + Vector3.up * 1.12f, 0.076f);
                post.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                Part(root, "PostCap", "Sphere.fbx", steel, foot + Vector3.up * 1.12f, Vector3.one * 0.082f);
                Part(root, "BasePlate", "Cube.fbx", steel, foot + Vector3.up * 0.008f, new Vector3(0.22f, 0.016f, 0.22f));
                for (int b = 0; b < 4; b++)
                {
                    float bx = (b % 2 == 0 ? -1f : 1f) * 0.075f, bz = (b < 2 ? -1f : 1f) * 0.075f;
                    Part(root, "Bolt", "Cylinder.fbx", s_black, foot + new Vector3(bx, 0.024f, bz), new Vector3(0.03f, 0.008f, 0.03f));
                }
            }
            foreach (var (h, d, name) in new[] { (1.05f, 0.060f, "Overlook_TopRailPipe"), (0.58f, 0.048f, "Overlook_MidRailPipe") })
            {
                var a = new Vector3(x, roadY + h, z0); var b = new Vector3(x, roadY + h, z1);
                var rail = Bar(root, name, steel, a, b, d);
                rail.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                // rounded returns down to the end posts
                Part(root, "RailEnd", "Sphere.fbx", steel, a, Vector3.one * d * 1.05f);
                Part(root, "RailEnd", "Sphere.fbx", steel, b, Vector3.one * d * 1.05f);
            }
            // green route marker: a 3.6 m mast (the lamp clears the 3.2 m fence top) on the asphalt in the north corner,
            // inside the fence. The lamp head is the fixture pass's dome (LookFixtures: ^Light_AuxRouteGreen without a
            // bollard -> Beacon), so the mast carries no housing of its own; it lights last in the aux-power sequence
            // and pulses like the other escape-route signals.
            var mFoot = new Vector3(x - 0.45f, roadY, z1 - 0.35f);
            var mast = Bar(root, "OverlookMarker_Mast", p.PaintedMetalGreen != null ? p.PaintedMetalGreen : steel, mFoot, mFoot + Vector3.up * 3.6f, 0.1f);
            mast.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            Part(root, "OverlookMarker_Plate", "Cube.fbx", steel, mFoot + Vector3.up * 0.01f, new Vector3(0.28f, 0.02f, 0.28f));
            WorldArchitectureKit.BuildHdrpLight(root, "Light_AuxRouteGreen_Overlook", mFoot + Vector3.up * 3.7f, Vector3.zero, LightType.Point,
                new Color(0.30f, 1.0f, 0.50f), 170f, 9f, LightGroupId.AuxRoute, volumetricDimmer: 0.5f, sequenceOrder: 999, pulsingEmergency: true);
        }

        // ------------------------------------------------------------------------------------------------------------
        // WAKE (painted into the island's shore-foam water decal by LookTerrainWater)
        // ------------------------------------------------------------------------------------------------------------
        /// <summary>Where the cutter lies in the bay (the builder places it here; the shore-foam water decal paints its
        /// wake: HDRP has one 1024 water-decal atlas and the island's shore foam fills it).</summary>
        public static readonly Vector3 CutterPosition = new Vector3(770f, 0f, -10f);    // ~227 m out: clear of the ending's letterbox
        public const float CutterYaw = -112f;

        /// <summary>Wake foam 0..1 at a world point (bow wave along the hull, Kelvin arms at 19.5 deg, churned trail).</summary>
        public static float WakeFoam(float wx, float wz)
        {
            Vector3 l = Quaternion.Euler(0f, -CutterYaw, 0f) * new Vector3(wx - CutterPosition.x, 0f, wz - CutterPosition.z);
            float x = l.x, z = l.z;
            float bowX = HullLength * 0.5f, sternX = -HullLength * 0.5f;
            if (x > bowX + 4f || x < sternX - 80f || Mathf.Abs(z) > 30f) return 0f;
            float n = Mathf.PerlinNoise(wx * 0.21f + 5.1f, wz * 0.33f + 2.7f) * 0.6f + Mathf.PerlinNoise(wx * 0.6f, wz * 0.8f + 9.3f) * 0.4f;
            float foam = 0f;
            if (x < sternX + 1f)
            {
                float d = sternX + 1f - x, w = 3.2f + d * 0.12f;
                foam = Mathf.Max(foam, Mathf.Exp(-(z * z) / (w * w)) * Mathf.Clamp01(1f - d / 75f) * (0.6f + 0.5f * n));
            }
            if (x < bowX)
            {
                float d = bowX - x, arm = d * 0.354f, w = 2.5f + d * 0.04f;
                float a = Mathf.Abs(z) - arm;
                foam = Mathf.Max(foam, Mathf.Exp(-(a * a) / (w * w)) * Mathf.Clamp01(1f - d / 80f) * 0.8f * SS(0.2f, 0.55f, n + 0.15f));
            }
            // bow wave: only along the forward third of the hull, broken by noise, never ahead of the stem (a solid
            // band round the bow read as a flat tan slab at 2 m per decal texel)
            if (x > sternX && x < bowX - 1f)
            {
                float t = Mathf.Clamp01((x - sternX) / HullLength);
                if (t > 0.7f)
                {
                    float a = Mathf.Abs(z) - (DeckHalf(t) * 0.95f + 0.8f);
                    foam = Mathf.Max(foam, Mathf.Exp(-(a * a) / 2.5f) * 0.5f * SS(0.7f, 0.9f, t) * (0.5f + 0.7f * n));
                }
            }
            return Mathf.Min(0.65f, foam);
        }
    }
}
