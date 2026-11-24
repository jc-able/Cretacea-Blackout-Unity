using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Editor.WorldBuilder;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Roofs and exteriors. Room shells use their ceiling slab as the roof, so from outside and above every building
    /// read as a plaster box. This pass caps every flat roofed ceiling with a bitumen roof, a parapet with a metal
    /// coping on the edges that face outside (shared edges between rooms get none), and a few rooftop units on big
    /// roofs. It also gives every RainBlocker proxy (layer 13) a fully clipped material, so the slabs can never show
    /// in a colour camera or a reflection probe even when the camera forgets to mask the layer.
    /// Generated objects are named LookRoof_* and rebuilt from scratch on every run.
    /// </summary>
    public static class LookRoofs
    {
        public const string Prefix = "LookRoof_";
        private const string HiddenMatPath = "Assets/Art/Materials/World/Look/MAT_RainBlockerHidden.mat";
        private const float CapThickness = 0.14f;
        private const float Overhang = 0.12f;
        private const float ParapetHeight = 0.55f;
        private const float ParapetThickness = 0.2f;

        private struct Cap
        {
            public Rect xz;          // world XZ rectangle of the cap
            public float top;        // world Y of the roof surface
            public Transform parent;
            public string name;
            public bool metal;       // a corrugated-metal shed: metal fascia, no concrete parapet
        }

        public static Material RainBlockerMaterial()
        {
            Material m = AssetDatabase.LoadAssetAtPath<Material>(HiddenMatPath);
            if (m != null) return m;
            Shader unlit = Shader.Find("HDRP/Unlit");
            if (unlit == null) return null;
            LookSigns.EnsureFolder("Assets/Art/Materials/World/Look");
            m = new Material(unlit) { name = "MAT_RainBlockerHidden" };
            m.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 0f));
            m.SetFloat("_AlphaCutoffEnable", 1f);
            m.SetFloat("_AlphaCutoff", 1f);
            HDMaterial.ValidateMaterial(m);
            AssetDatabase.CreateAsset(m, HiddenMatPath);
            return m;
        }

        public static string Apply(WorldMaterialPalette p)
        {
            GameObject world = GameObject.Find("World");
            if (world == null) return "roofs: no World root";

            // 1. RainBlocker proxies: hidden material
            Material hidden = RainBlockerMaterial();
            int blockers = 0;
            foreach (MeshRenderer mr in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.layer != GameLayers.RainBlocker) continue;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                if (hidden != null && mr.sharedMaterial != hidden) { mr.sharedMaterial = hidden; EditorUtility.SetDirty(mr); }
                blockers++;
            }

            // 2. clear the previous run
            var old = new List<GameObject>();
            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(Prefix)) old.Add(t.gameObject);
            foreach (GameObject g in old) if (g != null) Object.DestroyImmediate(g);

            // 3. find flat roofed ceilings
            Physics.SyncTransforms();
            var caps = new List<Cap>();
            foreach (MeshRenderer mr in world.GetComponentsInChildren<MeshRenderer>(false))
            {
                if (!IsRoofCeiling(mr)) continue;
                Bounds b = mr.bounds;
                if (SomethingAbove(mr, b)) continue;
                caps.Add(new Cap
                {
                    xz = Rect.MinMaxRect(b.min.x - Overhang, b.min.z - Overhang, b.max.x + Overhang, b.max.z + Overhang),
                    top = b.max.y + CapThickness,
                    parent = mr.transform.parent,
                    name = mr.transform.parent != null ? mr.transform.parent.name : mr.name,
                    metal = IsMetalShed(mr),
                });
            }

            // 4. build caps, parapets and rooftop units
            int parapets = 0, units = 0;
            for (int i = 0; i < caps.Count; i++)
            {
                Cap c = caps[i];
                var root = new GameObject($"{Prefix}{c.name}");
                root.layer = GameLayers.Environment;
                root.transform.SetParent(c.parent, true);
                root.transform.position = new Vector3(c.xz.center.x, c.top, c.xz.center.y);
                root.transform.rotation = Quaternion.identity;

                // round 4 (review 3, 28_ending_2: 'a flat-roofed orange box with a black roof and one vent'): the big
                // sheet-metal warehouse gets a low-pitched sheet roof with gables, skylights, ridge vents, gutters,
                // downpipes and rain stains instead of the flat bitumen cap
                if (c.metal && c.xz.width * c.xz.height > 900f)
                {
                    units += PitchedShedRoof(root.transform, c, p);
                    continue;
                }
                bool z1 = InZone1(c.parent) && !c.metal;
                Box(root.transform, "Cap", new Vector3(c.xz.center.x, c.top - CapThickness * 0.5f, c.xz.center.y),
                    new Vector3(c.xz.width, CapThickness, c.xz.height), z1 ? Zone1Gravel(p) : p.RoofBitumen, shadows: true);

                parapets += BuildParapets(root.transform, c, caps, i, p, z1);
                if (!z1) units += BuildRooftopUnits(root.transform, c, p);   // Zone 1 builds its own, placed clear of the walkway (fix round)
                if (z1) units += Zone1RoofDetail(root.transform, c, p);
            }

            // round 3: zones 2-3 own the coast road dressing, but zone 4 builds its fences last, so the opened view is
            // made here (its root carries the Prefix, so step 2 clears it on the next run)
            string coast = Cretacea.Editor.WorldBuilder.Zone23Dressing.OpenCoastRoad(world.transform, p, Prefix + "CoastRoadOpen");
            // round 4 (review 3, 22_ch5_green_route: the nearest route bollard's collar clipped to a mint-white band, 40 %
            // of its box): the AuxRoute collars get their own deeper green lens at 7 nits instead of the palette green at
            // 14 (shared with LEDs, so it stays); runs after LookFixtures built the collars
            Material collarLens = p.GetOrBuildEmissive("MAT_Z23_BollardCollarGreen", new Color(0.25f, 1.0f, 0.45f), 7f);
            int collars = 0;
            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Fixture_Light_AuxRouteGreen")) continue;
                Transform col = t.Find("Collar");
                if (col != null && col.TryGetComponent(out MeshRenderer cr)) { cr.sharedMaterial = collarLens; EditorUtility.SetDirty(cr); collars++; }
            }
            coast += $"; route collars={collars}";
            return $"roofs: caps={caps.Count} parapet runs={parapets} rooftop units={units}; rain blockers hidden={blockers}; {coast}";
        }

        /// <summary>A shed whose ceiling or walls are corrugated / painted sheet metal (warehouse, workshop).</summary>
        private static bool IsMetalShed(MeshRenderer ceiling)
        {
            bool Metal(Renderer r) => r != null && r.sharedMaterial != null
                && (r.sharedMaterial.name.Contains("Corrugated") || r.sharedMaterial.name.Contains("RustedIron"));
            if (Metal(ceiling)) return true;
            Transform shell = ceiling.transform.parent;
            if (shell == null) return false;
            int walls = 0, metal = 0;
            foreach (Transform t in shell)
            {
                if (t.name.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (MeshRenderer r in t.GetComponentsInChildren<MeshRenderer>(false)) { walls++; if (Metal(r)) metal++; }
            }
            return walls > 0 && metal * 2 >= walls;
        }

        private static bool IsRoofCeiling(MeshRenderer mr)
        {
            string n = mr.name;
            if (n.IndexOf("Ceil", System.StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (n.StartsWith(Prefix) || n.StartsWith("Fixture_")) return false;
            if (mr.gameObject.layer != GameLayers.Environment) return false;
            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") return false;
            Vector3 e = mr.transform.rotation.eulerAngles;
            if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 1f || Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 1f) return false;
            Bounds b = mr.bounds;
            return b.size.y < 0.6f && b.size.x * b.size.z > 3f;
        }

        /// <summary>True when the ceiling is covered by something solid (an upper floor, a skylight roof, a canopy):
        /// rays dropped from 4 m above its centre and corners hit something else first.</summary>
        private static bool SomethingAbove(MeshRenderer ceiling, Bounds b)
        {
            Collider own = ceiling.GetComponent<Collider>();
            int mask = ~((1 << GameLayers.RainBlocker) | (1 << GameLayers.Viewmodel) | (1 << 9) | (1 << 2));
            Vector3[] pts =
            {
                b.center,
                new Vector3(b.min.x + 0.4f, 0f, b.min.z + 0.4f), new Vector3(b.max.x - 0.4f, 0f, b.min.z + 0.4f),
                new Vector3(b.min.x + 0.4f, 0f, b.max.z - 0.4f), new Vector3(b.max.x - 0.4f, 0f, b.max.z - 0.4f),
            };
            int covered = 0;
            foreach (Vector3 q in pts)
            {
                Vector3 o = new Vector3(q.x, b.max.y + 4f, q.z);
                if (Physics.Raycast(o, Vector3.down, out RaycastHit hit, 4.5f, mask, QueryTriggerInteraction.Ignore)
                    && hit.collider != own && hit.point.y > b.max.y + 0.05f)
                    covered++;
            }
            return covered >= 3;
        }

        private static int BuildParapets(Transform root, Cap c, List<Cap> caps, int self, WorldMaterialPalette p, bool z1 = false)
        {
            int runs = 0;
            // edges: (start, end, outward normal)
            var edges = new[]
            {
                (new Vector2(c.xz.xMin, c.xz.yMin), new Vector2(c.xz.xMax, c.xz.yMin), new Vector2(0f, -1f)),
                (new Vector2(c.xz.xMin, c.xz.yMax), new Vector2(c.xz.xMax, c.xz.yMax), new Vector2(0f, 1f)),
                (new Vector2(c.xz.xMin, c.xz.yMin), new Vector2(c.xz.xMin, c.xz.yMax), new Vector2(-1f, 0f)),
                (new Vector2(c.xz.xMax, c.xz.yMin), new Vector2(c.xz.xMax, c.xz.yMax), new Vector2(1f, 0f)),
            };
            foreach (var (a, bEnd, nrm) in edges)
            {
                float len = Vector2.Distance(a, bEnd);
                int samples = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
                float runStart = -1f;
                for (int s = 0; s <= samples; s++)
                {
                    float t = s / (float)samples;
                    bool exterior = false;
                    if (s < samples)
                    {
                        float tm = (s + 0.5f) / samples;
                        Vector2 e = Vector2.Lerp(a, bEnd, tm);
                        // no parapet towards a neighbour roof: touching, or across a narrow strip (< 2.5 m) between
                        // two buildings, which parapets on both sides would fence in like a trench
                        exterior = !Covered(e + nrm * 0.35f, c.top, caps, self) && !Covered(e + nrm * 1.4f, c.top, caps, self)
                                   && !Covered(e + nrm * 2.5f, c.top, caps, self);
                    }
                    if (exterior && runStart < 0f) runStart = t;
                    if (!exterior && runStart >= 0f)
                    {
                        BuildParapetRun(root, Vector2.Lerp(a, bEnd, runStart), Vector2.Lerp(a, bEnd, t), nrm, c.top, p, c.metal, z1);
                        runs++;
                        runStart = -1f;
                    }
                }
            }
            return runs;
        }

        private static bool Covered(Vector2 q, float top, List<Cap> caps, int self)
        {
            for (int i = 0; i < caps.Count; i++)
            {
                if (i == self) continue;
                if (caps[i].top < top - 1.2f) continue;      // a much lower neighbour does not hide our edge
                if (caps[i].xz.Contains(q)) return true;
            }
            return false;
        }

        private static void BuildParapetRun(Transform root, Vector2 a, Vector2 b, Vector2 nrm, float top, WorldMaterialPalette p, bool metal = false, bool z1 = false)
        {
            float len = Vector2.Distance(a, b);
            if (len < 0.3f) return;
            if (metal)
            {
                // sheet-metal shed: a low corrugated fascia with a dark capping, not a concrete parapet
                Vector2 fm = (a + b) * 0.5f - nrm * 0.03f;
                bool ax = Mathf.Abs(nrm.y) > 0.5f;
                Box(root, "Fascia", new Vector3(fm.x, top + 0.09f, fm.y), ax ? new Vector3(len, 0.18f, 0.06f) : new Vector3(0.06f, 0.18f, len), p.CorrugatedMetal, shadows: true);
                Box(root, "Capping", new Vector3(fm.x, top + 0.19f, fm.y), ax ? new Vector3(len + 0.04f, 0.03f, 0.12f) : new Vector3(0.12f, 0.03f, len + 0.04f), p.DarkPaintedMetal, shadows: false);
                return;
            }
            Vector2 mid = (a + b) * 0.5f - nrm * (ParapetThickness * 0.5f);
            bool alongX = Mathf.Abs(nrm.y) > 0.5f;
            Vector3 size = alongX ? new Vector3(len, ParapetHeight, ParapetThickness) : new Vector3(ParapetThickness, ParapetHeight, len);
            Box(root, "Parapet", new Vector3(mid.x, top + ParapetHeight * 0.5f, mid.y), size, p.ConcreteExterior, shadows: true);
            // Zone 1 (fix round): a 0.15 m pale precast coping band that outlines every wing from the air
            float copeH = z1 ? 0.15f : 0.05f;
            Vector3 cope = alongX ? new Vector3(len + 0.06f, copeH, ParapetThickness + 0.06f) : new Vector3(ParapetThickness + 0.06f, copeH, len + 0.06f);
            Box(root, "Coping", new Vector3(mid.x, top + ParapetHeight + copeH * 0.5f, mid.y), cope, z1 ? Zone1Coping(p) : p.DarkPaintedMetal, shadows: true);
        }

        /// <summary>Rooftop HVAC units on roofs larger than 60 m^2, placed deterministically from the roof name.</summary>
        private static int BuildRooftopUnits(Transform root, Cap c, WorldMaterialPalette p)
        {
            float area = c.xz.width * c.xz.height;
            // only on real buildings (room shells have a Floor next to their Ceiling), never on canopies
            if (c.parent == null || c.parent.Find("Floor") == null) return 0;
            if (area < 90f || c.xz.width < 4f || c.xz.height < 4f) return 0;
            int count = Mathf.Clamp(Mathf.FloorToInt(area / 260f), 1, 2);
            var rng = new System.Random(c.name.GetHashCode() & 0x7fffffff);
            int built = 0;
            for (int i = 0; i < count; i++)
            {
                float u = 0.2f + 0.6f * (float)rng.NextDouble();
                float v = 0.25f + 0.5f * (float)rng.NextDouble();
                Vector3 basePos = new Vector3(Mathf.Lerp(c.xz.xMin + 1.5f, c.xz.xMax - 1.5f, u), c.top, Mathf.Lerp(c.xz.yMin + 1.5f, c.xz.yMax - 1.5f, v));
                var unit = new GameObject("RooftopUnit");
                unit.layer = GameLayers.Environment;
                unit.transform.SetParent(root, false);
                unit.transform.position = basePos;
                unit.transform.rotation = Quaternion.Euler(0f, rng.Next(0, 2) * 90f, 0f);
                Box(unit.transform, "Curb", new Vector3(0f, 0.08f, 0f), new Vector3(1.9f, 0.16f, 1.3f), p.DarkPaintedMetal, shadows: true, local: true);
                Box(unit.transform, "Cabinet", new Vector3(0f, 0.66f, 0f), new Vector3(1.75f, 1.0f, 1.15f), p.BrushedSteel, shadows: true, local: true);
                Box(unit.transform, "Louvre", new Vector3(0f, 0.6f, -0.585f), new Vector3(1.5f, 0.7f, 0.02f), p.DarkPaintedMetal, shadows: false, local: true);   // dark grille, not rust-orange sheet
                GameObject fan = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fan.name = "FanShroud";
                Object.DestroyImmediate(fan.GetComponent<Collider>());
                fan.layer = GameLayers.Environment;
                fan.transform.SetParent(unit.transform, false);
                fan.transform.localPosition = new Vector3(0.35f, 1.22f, 0f);
                fan.transform.localScale = new Vector3(0.8f, 0.06f, 0.8f);
                fan.GetComponent<MeshRenderer>().sharedMaterial = p.DarkPaintedMetal;
                // duct into the roof
                // duct from the cabinet's side down into the roof (sits on the roof, touches the cabinet)
                Box(unit.transform, "Duct", new Vector3(-0.6f, 0.2f, 0.85f), new Vector3(0.4f, 0.4f, 0.6f), p.BrushedSteel, shadows: true, local: true);
                Box(unit.transform, "DuctFlashing", new Vector3(-0.6f, 0.015f, 1.0f), new Vector3(0.5f, 0.03f, 0.4f), p.DarkPaintedMetal, shadows: false, local: true);
                built++;
            }
            return built;
        }

        // ---- Zones 2-3 big sheds, round 4 ------------------------------------------------------------------------------
        private static Material s_roofSheet, s_roofGrp, s_roofRust;

        private static Material RoofSheet(WorldMaterialPalette p)
        {
            if (s_roofSheet == null)
                s_roofSheet = p.Surface("MAT_Z23_RoofSheetGrey", "metal_painted_worn", "grey_BaseColor", "MaskMap", 2.6f, new Color(0.66f, 0.70f, 0.72f), 0.22f, 0.55f, 0.4f, 1.0f);
            return s_roofSheet != null ? s_roofSheet : p.CorrugatedMetal;
        }

        private static Material RoofGrp(WorldMaterialPalette p)
        {
            if (s_roofGrp == null)
                s_roofGrp = p.Surface("MAT_Z23_RoofSkylightGRP", "tiles_white", "BaseColor", "MaskMap", 1.2f, new Color(0.86f, 0.88f, 0.78f), 0.55f, 0.75f, 0f, 0.3f);
            return s_roofGrp != null ? s_roofGrp : p.PlasterClean;
        }

        private static Material RoofRust(WorldMaterialPalette p)
        {
            if (s_roofRust == null)
                s_roofRust = p.Surface("MAT_Z23_RoofRustPatch", "metal_corrugated_rusty", "BaseColor", "MaskMap", 2.0f, new Color(0.62f, 0.5f, 0.42f), 0.12f, 0.35f, 0.3f, 1.0f);
            return s_roofRust != null ? s_roofRust : p.RustedIron;
        }

        /// <summary>A 6-degree duo-pitch sheet roof over a flat shed ceiling: two slopes, gable infills over the end walls,
        /// a ridge capping with five ridge vents, three skylight strips and rust patches per slope, eaves gutters,
        /// downpipes to the ground and rain stains on the walls under the gutters. Collider-free.</summary>
        private static int PitchedShedRoof(Transform root, Cap c, WorldMaterialPalette p)
        {
            bool alongX = c.xz.width >= c.xz.height;                       // the ridge runs along the long side
            float L = alongX ? c.xz.width : c.xz.height, span = alongX ? c.xz.height : c.xz.width;
            float hs = span * 0.5f;
            const float pitch = 6f;
            float rise = hs * Mathf.Tan(pitch * Mathf.Deg2Rad);
            float eave = c.top - CapThickness;                              // the ceiling top (= wall head)
            Vector3 ctr = new Vector3(c.xz.center.x, eave, c.xz.center.y);
            Vector3 ax = alongX ? Vector3.right : Vector3.forward;          // along the ridge
            Vector3 ac = alongX ? Vector3.forward : Vector3.right;          // across, towards the + slope
            Material sheet = RoofSheet(p), grp = RoofGrp(p), rust = RoofRust(p), dark = p.DarkPaintedMetal, wall = p.CorrugatedMetal;
            float slopeLen = hs / Mathf.Cos(pitch * Mathf.Deg2Rad) + 0.25f;

            foreach (float sd in new[] { -1f, 1f })
            {
                // local frame of the slope: x along the ridge, z down the slope towards the eave
                Quaternion sr = Quaternion.LookRotation(Vector3.ProjectOnPlane(ac * sd, Vector3.up).normalized * Mathf.Cos(pitch * Mathf.Deg2Rad) - Vector3.up * Mathf.Sin(pitch * Mathf.Deg2Rad), Vector3.up);
                Vector3 mid = ctr + ac * (sd * hs * 0.5f) + Vector3.up * (rise * 0.5f + 0.06f);
                GameObject slab = Box(root, "RoofSlope", mid, new Vector3(L + 0.3f, 0.1f, slopeLen), sheet, shadows: true);
                slab.transform.rotation = sr;
                Vector3 down = sr * Vector3.forward, up = sr * Vector3.up;
                // skylight strips (eave to near the ridge) and rust patches
                for (int k = 0; k < 3; k++)
                {
                    float along = (k - 1) * L * 0.3f + L * 0.04f * sd;
                    GameObject sk = Box(root, "RoofSkylight", mid + ax * along + up * 0.075f - down * 0.4f, new Vector3(1.1f, 0.02f, slopeLen * 0.72f), grp, shadows: false);
                    sk.transform.rotation = sr;
                }
                for (int k = 0; k < 4; k++)
                {
                    float h = Mathf.Repeat(Mathf.Sin((k + 1) * 12.9898f + sd * 78.233f + c.xz.x) * 43758.55f, 1f);
                    float along = (h - 0.5f) * L * 0.85f;
                    GameObject rp = Box(root, "RoofRust", mid + ax * along + up * 0.064f + down * (slopeLen * (0.15f + 0.25f * h)), new Vector3(1.6f + 2.4f * h, 0.012f, 1.2f + 2.2f * (1f - h)), rust, shadows: false);
                    rp.transform.rotation = sr;
                }
                // eaves gutter and downpipes on this side
                Vector3 eavePt = ctr + ac * (sd * (hs + 0.05f)) + Vector3.up * 0.0f;
                GameObject gut = Box(root, "Gutter", eavePt + Vector3.up * -0.02f, alongX ? new Vector3(L + 0.2f, 0.16f, 0.18f) : new Vector3(0.18f, 0.16f, L + 0.2f), dark, shadows: true);
                float wallFace = sd * (hs - Overhang + 0.22f);
                for (int k = 0; k < 4; k++)
                {
                    float along = (k / 3f - 0.5f) * (L - 3.4f);
                    // keep clear of the door openings (the warehouse doors are near the middle of its south and east walls)
                    if (Mathf.Abs(along + (alongX ? c.xz.center.x : c.xz.center.y) - (alongX ? 432.5f : 21f)) < 4.5f) along += 6f;
                    Vector3 top = ctr + ax * along + ac * wallFace;
                    float ground = eave - 8f;
                    if (Physics.Raycast(top + Vector3.down * 0.5f + ac * sd * 0.1f, Vector3.down, out RaycastHit hit, 14f, ~((1 << GameLayers.RainBlocker) | (1 << GameLayers.Viewmodel)), QueryTriggerInteraction.Ignore))
                        ground = hit.point.y;
                    float len = top.y - ground;
                    Cyl(root, "Downpipe", top + Vector3.down * (len * 0.5f) + ac * sd * 0.06f, new Vector3(0.11f, len * 0.5f, 0.11f), dark);
                    Box(root, "DownpipeShoe", new Vector3(top.x, ground + 0.12f, top.z) + ac * sd * 0.2f, alongX ? new Vector3(0.11f, 0.11f, 0.32f) : new Vector3(0.32f, 0.11f, 0.11f), dark, shadows: false);
                    Cretacea.Editor.WorldBuilder.Zone23Dressing.Decal(root, "DownpipeStain", top + Vector3.down * 2.4f + ac * sd * 0.05f, ac * sd, new Vector3(1.1f, 4.2f, 0.5f),
                        Cretacea.Editor.WorldBuilder.Zone23Dressing.RunoffStrong(p), 0.85f, drawDistance: 220f);
                }
                // rain stains under the gutter along the wall, every 3.5 m
                for (float a = -L * 0.5f + 1.8f; a < L * 0.5f - 1.5f; a += 3.5f)
                {
                    float h = Mathf.Repeat(Mathf.Sin(a * 4.13f + sd * 9.7f) * 3571.3f, 1f);
                    if (h < 0.3f) continue;
                    Vector3 sp = ctr + ax * (a + h) + ac * (wallFace + sd * 0.03f) + Vector3.down * (1.0f + 1.0f * h);
                    Cretacea.Editor.WorldBuilder.Zone23Dressing.Decal(root, "WallRainStain", sp, ac * sd, new Vector3(0.9f + h, 2.0f + 2.0f * h, 0.5f),
                        Cretacea.Editor.WorldBuilder.Zone23Dressing.RunoffStrong(p), 0.75f, drawDistance: 220f);
                }
            }
            // gable infills over the end walls (the wall sheet carried up to the ridge) with a dark verge trim
            foreach (float se in new[] { -1f, 1f })
            {
                Vector3 g = ctr + ax * (se * (L * 0.5f - Overhang - 0.15f));
                GameObject gable = Cretacea.Editor.WorldBuilder.Zone23Dressing.Prism(root, "RoofGable", g, 0.3f, -hs + Overhang, hs - Overhang, 0f, rise + 0.02f, wall);
                gable.transform.rotation = alongX ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
                GameObjectUtility.SetStaticEditorFlags(gable, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }
            // ridge capping and five ridge vents
            Box(root, "RidgeCap", ctr + Vector3.up * (rise + 0.14f), alongX ? new Vector3(L + 0.35f, 0.08f, 0.6f) : new Vector3(0.6f, 0.08f, L + 0.35f), dark, shadows: true);
            for (int k = 0; k < 5; k++)
            {
                Vector3 vp = ctr + ax * ((k - 2) * L * 0.18f) + Vector3.up * (rise + 0.42f);
                Box(root, "RidgeVent", vp, alongX ? new Vector3(1.4f, 0.5f, 0.9f) : new Vector3(0.9f, 0.5f, 1.4f), p.BrushedSteel, shadows: true);
                Box(root, "RidgeVentHood", vp + Vector3.up * 0.3f, alongX ? new Vector3(1.6f, 0.08f, 1.2f) : new Vector3(1.2f, 0.08f, 1.6f), dark, shadows: true);
                Box(root, "RidgeVentLouvre", vp + ac * 0.46f, alongX ? new Vector3(1.2f, 0.32f, 0.02f) : new Vector3(0.02f, 0.32f, 1.2f), dark, shadows: false);
                Box(root, "RidgeVentLouvre", vp - ac * 0.46f, alongX ? new Vector3(1.2f, 0.32f, 0.02f) : new Vector3(0.02f, 0.32f, 1.2f), dark, shadows: false);
            }
            return 5;
        }

        // ---- Zone 1 (Visitor Center) roofs, round 3 -------------------------------------------------------------------
        // Review 2 (intro_14-16 'Closing time' aerial, GoldenDay_valley_air): "the wings are flat boxes with blank roofs".
        // Visitor Center roofs get a pale gravel ballast instead of plain bitumen, a membrane walkway of pavers, framed
        // skylights, vent stacks and a roof hatch, all deterministic from the roof name.

        private static Material s_gravel, s_skyGlass, s_coping;

        private static Material Zone1Coping(WorldMaterialPalette p)
        {
            if (s_coping == null)
                s_coping = p.Surface("MAT_Z1_RoofCoping", "concrete_wall", "BaseColor", "MaskMap", 3.2f, new Color(1.45f, 1.45f, 1.42f), 0.15f, 0.6f, 0f, 0.8f);
            return s_coping != null ? s_coping : p.ConcreteExterior;
        }

        /// <summary>Zone 1 rooftop HVAC unit (fix round: the old 0.5 m details read as specks at 70 m): 0.15 m plinth, a
        /// 2.2 x 1.3 x 1.1 m dark cabinet with louvre bands, a top fan disc with a guard ring, a duct into the roof.</summary>
        private static void Zone1Hvac(Transform root, Vector3 basePos, float yaw, WorldMaterialPalette p)
        {
            var unit = new GameObject("RooftopUnit_Z1");
            unit.layer = GameLayers.Environment;
            unit.transform.SetParent(root, false);
            unit.transform.position = basePos;
            unit.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(unit.transform, "Plinth", new Vector3(0f, 0.075f, 0f), new Vector3(2.4f, 0.15f, 1.5f), p.ConcreteExterior, shadows: true, local: true);
            Box(unit.transform, "Cabinet", new Vector3(0f, 0.7f, 0f), new Vector3(2.2f, 1.1f, 1.3f), p.DarkPaintedMetal, shadows: true, local: true);
            Box(unit.transform, "TopPanel", new Vector3(0f, 1.265f, 0f), new Vector3(2.24f, 0.03f, 1.34f), p.BrushedSteel, shadows: false, local: true);
            for (int s = 0; s < 2; s++)
                Box(unit.transform, $"Louvre_{s}", new Vector3(-0.35f, 0.65f, (s == 0 ? -1f : 1f) * 0.655f), new Vector3(1.3f, 0.7f, 0.02f), p.BrushedSteel, shadows: false, local: true);
            CylLocal(unit.transform, "FanRing", new Vector3(0.55f, 1.33f, 0f), new Vector3(0.95f, 0.05f, 0.95f), p.BrushedSteel);
            CylLocal(unit.transform, "FanDisc", new Vector3(0.55f, 1.36f, 0f), new Vector3(0.8f, 0.02f, 0.8f), p.DarkPaintedMetal);
            CylLocal(unit.transform, "FanHub", new Vector3(0.55f, 1.38f, 0f), new Vector3(0.18f, 0.02f, 0.18f), p.BrushedSteel);
            Box(unit.transform, "Duct", new Vector3(-1.35f, 0.35f, 0f), new Vector3(0.5f, 0.5f, 0.6f), p.BrushedSteel, shadows: true, local: true);
            Box(unit.transform, "DuctFlashing", new Vector3(-1.4f, 0.02f, 0f), new Vector3(0.7f, 0.04f, 0.8f), p.DarkPaintedMetal, shadows: false, local: true);
        }

        private static bool Clear(List<Rect> taken, Rect r)
        {
            foreach (Rect t in taken) if (t.Overlaps(r)) return false;
            return true;
        }

        private static bool InZone1(Transform t)
        {
            for (; t != null; t = t.parent) if (t.name == "Zone_01_VisitorCenter") return true;
            return false;
        }

        private static Material Zone1Gravel(WorldMaterialPalette p)
        {
            if (s_gravel == null)
                s_gravel = p.Surface("MAT_Z1_RoofGravel", "gravel_road", "BaseColor", "MaskMap", 1.8f, new Color(1.12f, 1.12f, 1.10f), 0.05f, 0.35f, 0f, 1.0f);
            return s_gravel != null ? s_gravel : p.RoofBitumen;
        }

        private static Material SkylightGlass(WorldMaterialPalette p)
        {
            if (s_skyGlass == null)
                s_skyGlass = p.Surface("MAT_Z1_SkylightGlass", "concrete_polished", "BaseColor", "MaskMap", 2.0f, new Color(0.30f, 0.38f, 0.42f), 0.90f, 0.96f, 0f, 0.1f);
            return s_skyGlass;
        }

        private static int Zone1RoofDetail(Transform root, Cap c, WorldMaterialPalette p)
        {
            float w = c.xz.width, d = c.xz.height;
            if (w < 3f || d < 3f) return 0;
            var rng = new System.Random((c.name.GetHashCode() ^ 0x5bd1e995) & 0x7fffffff);
            bool alongX = w >= d;
            int built = 0;
            // paver walkway along the long axis (a darker strip that breaks the gravel field)
            float len = (alongX ? w : d) - 1.2f;
            Vector3 wc = new Vector3(c.xz.center.x, c.top + 0.02f, c.xz.center.y) + (alongX ? new Vector3(0f, 0f, d * 0.18f) : new Vector3(w * 0.18f, 0f, 0f));
            Box(root, "Walkway", wc, alongX ? new Vector3(len, 0.04f, 0.6f) : new Vector3(0.6f, 0.04f, len), p.ConcreteExterior, shadows: false);
            // keep-out rectangles (x, z), each grown by 1 m: walkway, skylights, hatch, HVAC units; vents test against them
            var taken = new List<Rect>();
            taken.Add(alongX ? new Rect(wc.x - len * 0.5f - 1f, wc.z - 1.3f, len + 2f, 2.6f) : new Rect(wc.x - 1.3f, wc.z - len * 0.5f - 1f, 2.6f, len + 2f));
            // skylights: one per ~70 m2, up to 4, in a row on the other side of the walkway
            int sky = Mathf.Clamp(Mathf.FloorToInt(w * d / 70f), (w * d > 40f) ? 1 : 0, 4);
            for (int i = 0; i < sky; i++)
            {
                float u = (i + 0.5f) / sky;
                Vector3 sp = alongX
                    ? new Vector3(Mathf.Lerp(c.xz.xMin + 1.6f, c.xz.xMax - 1.6f, u), c.top, c.xz.center.y - d * 0.15f)
                    : new Vector3(c.xz.center.x - w * 0.15f, c.top, Mathf.Lerp(c.xz.yMin + 1.6f, c.xz.yMax - 1.6f, u));
                // fix round: 2.4 x 1.6 m on a 0.45 m curb (1.5 m units were specks from the aerial)
                Vector3 ssz = alongX ? new Vector3(2.4f, 0f, 1.6f) : new Vector3(1.6f, 0f, 2.4f);
                Box(root, "SkylightCurb", sp + new Vector3(0f, 0.225f, 0f), new Vector3(ssz.x, 0.45f, ssz.z), p.ConcreteExterior, shadows: true);
                Box(root, "SkylightGlass", sp + new Vector3(0f, 0.52f, 0f), new Vector3(ssz.x - 0.16f, 0.14f, ssz.z - 0.16f), SkylightGlass(p), shadows: false);
                Box(root, "SkylightFrameA", sp + new Vector3(0f, 0.6f, 0f), new Vector3(ssz.x - 0.1f, 0.04f, 0.06f), p.BrushedSteel, shadows: false);
                Box(root, "SkylightFrameB", sp + new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.04f, ssz.z - 0.1f), p.BrushedSteel, shadows: false);
                taken.Add(new Rect(sp.x - ssz.x * 0.5f - 1f, sp.z - ssz.z * 0.5f - 1f, ssz.x + 2f, ssz.z + 2f));
                built++;
            }
            // roof hatch with a ladder rail (placed before the vents so they keep clear of it)
            if (w * d > 60f)
            {
                Vector3 hp = new Vector3(c.xz.xMax - 1.4f, c.top, c.xz.yMax - 1.4f);
                Box(root, "HatchCurb", hp + new Vector3(0f, 0.2f, 0f), new Vector3(0.95f, 0.4f, 0.95f), p.PaintedMetalYellow, shadows: true);
                Box(root, "HatchLid", hp + new Vector3(0f, 0.42f, 0f), new Vector3(1.0f, 0.05f, 1.0f), p.DarkPaintedMetal, shadows: false);
                taken.Add(new Rect(hp.x - 1.5f, hp.z - 1.5f, 3f, 3f));
                built++;
            }
            // HVAC units: one per cap over 60 m2, two over 200 m2, on free gravel at least 1.3 m from the roof edge
            int hvac = w * d > 200f ? 2 : (w * d > 60f ? 1 : 0);
            for (int i = 0, tries = 0; i < hvac && tries < 40; tries++)
            {
                bool rot = rng.Next(2) == 0;
                float hx = rot ? 0.75f : 1.45f, hz = rot ? 1.45f : 0.75f;   // half footprint incl. plinth and duct
                if (c.xz.width < 2f * (hx + 1.3f) || c.xz.height < 2f * (hz + 1.3f)) continue;
                Vector3 up = new Vector3(Mathf.Lerp(c.xz.xMin + 1.3f + hx, c.xz.xMax - 1.3f - hx, (float)rng.NextDouble()), c.top,
                                         Mathf.Lerp(c.xz.yMin + 1.3f + hz, c.xz.yMax - 1.3f - hz, (float)rng.NextDouble()));
                Rect fp = new Rect(up.x - hx, up.z - hz, 2f * hx, 2f * hz);
                if (!Clear(taken, fp)) continue;
                Zone1Hvac(root, up, rot ? 90f : 0f, p);
                taken.Add(new Rect(fp.x - 1f, fp.y - 1f, fp.width + 2f, fp.height + 2f));
                i++; built++;
            }
            // vent stacks and a gooseneck or two, each at least 1 m clear of the walkway, skylights, hatch and units
            int vents = Mathf.Clamp(Mathf.FloorToInt(w * d / 45f), 1, 5);
            for (int i = 0, tries = 0; i < vents && tries < 60; tries++)
            {
                Vector3 vp = new Vector3(Mathf.Lerp(c.xz.xMin + 0.9f, c.xz.xMax - 0.9f, (float)rng.NextDouble()), c.top, Mathf.Lerp(c.xz.yMin + 0.9f, c.xz.yMax - 0.9f, (float)rng.NextDouble()));
                if (!Clear(taken, new Rect(vp.x - 0.2f, vp.z - 0.2f, 0.4f, 0.4f))) continue;
                i++;
                float h = 0.45f + (float)rng.NextDouble() * 0.5f;
                Cyl(root, "VentStack", vp + new Vector3(0f, h * 0.5f, 0f), new Vector3(0.16f, h * 0.5f, 0.16f), p.BrushedSteel);
                Cyl(root, "VentCap", vp + new Vector3(0f, h + 0.05f, 0f), new Vector3(0.32f, 0.04f, 0.32f), p.DarkPaintedMetal);
                Box(root, "VentFlashing", vp + new Vector3(0f, 0.015f, 0f), new Vector3(0.4f, 0.03f, 0.4f), p.DarkPaintedMetal, shadows: false);
                built++;
            }
            return built;
        }

        /// <summary>Cylinder at a local position under a rotated parent.</summary>
        private static void CylLocal(Transform parent, string name, Vector3 localPos, Vector3 localScale, Material mat)
            => Cyl(parent, name, parent.TransformPoint(localPos), localScale, mat);

        private static void Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.layer = GameLayers.Environment;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat, bool shadows, bool local = false)
        {
            GameObject go = WorldArchitectureKit.CreateBox(parent, name, Vector3.zero, size, mat, addCollider: false);
            if (local) go.transform.localPosition = pos; else go.transform.position = pos;
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }
    }
}
