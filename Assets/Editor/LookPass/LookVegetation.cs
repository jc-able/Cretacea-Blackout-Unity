using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cretacea.Core;
using Z4 = Cretacea.Editor.WorldBuilder.Zone4_TerrainJungleAndDawnCoastBuilder;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Jungle scatter (deterministic, seeded):
    ///  * inside and next to the play area: LODGroup prefab GameObjects (GPU Resident Drawer instances them) -
    ///    tree belts behind walls and around clearings, understory, logs and rocks; trunk colliders on trees so the
    ///    baked NavMesh and the player respect them; nothing on roads, paths, plateaus, camera lines or the stream;
    ///  * the rest of the island: terrain tree instances of the same prefabs (cheap far LOD), on every tile.
    /// </summary>
    public static class LookVegetation
    {
        /// <summary>GameObject vegetation reaches this far outside the play area; terrain trees start here.</summary>
        public const float GoMargin = 50f;      // the play bounds ring is 46 m out: everything reachable is a GameObject

        private static readonly string[] TreeStems = { "jungle_tree", "palm_tree", "palm_tree_b", "tree_fern" };

        // camera lines of the intro and ending cutscenes (from StoryDirector / Zone 4 anchors): keep them clear
        private static readonly (Vector2 a, Vector2 b, float r)[] CameraLines =
        {
            (new Vector2(-176f, -252f), new Vector2(-36f, -42f), 9f),     // opening glide over Titan Meadows
            (new Vector2(-160f, -234f), new Vector2(-138f, -209f), 16f),  // ... which starts low over the lake: keep its first seconds open
            (new Vector2(-92f, -150f), new Vector2(-120f, -190f), 9f),
            (new Vector2(-44f, -92f), new Vector2(-40f, -150f), 7f),      // VC terrace -> sauropods
            (new Vector2(-10f, -150f), new Vector2(-36f, -42f), 8f),      // high crane shot
            (new Vector2(-20f, -136f), new Vector2(-40f, -30f), 8f),
            (new Vector2(44f, -47f), new Vector2(66f, -54f), 6f),         // T. rex reveal
            (new Vector2(-96f, 27f), new Vector2(-72f, 28.5f), 7f),       // opening shot 6: push-in on the Security Office west window
            (new Vector2(543f, -46f), new Vector2(900f, -46f), 26f),      // dawn ending, looking east
        };

        // Titan Meadows stays a meadow for the herbivores: only scattered trees
        private static readonly Vector3 MeadowCentre = new Vector3(-55f, 0f, -175f);
        private const float MeadowRadius = 70f;

        // real footprints + ~2 m: compounds whose interior must stay clear (walls are handled by physics checks)
        private static readonly Vector4[] Footprints =
        {
            new Vector4(-104f, 58f, -98f, -30f),    // plaza + viewing gallery
            new Vector4(-74f, 24f, -34f, 36f),      // Visitor Center wings
            new Vector4(-92f, 22f, 39f, 133f),      // Talon Ridge raptor pen (clear sightline)
            new Vector4(20f, 94f, -13f, 19f),       // service paths main route
            new Vector4(36f, 68f, 14f, 36f),        // service paths north branches
            new Vector4(87f, 98f, -9f, 4f),         // paddock 4 gate
            new Vector4(97f, 112f, -14f, -1f),      // overturned jeep
            new Vector4(241f, 295f, -19f, 19f),     // Substation 3 compound
            new Vector4(395f, 509f, -98f, 53f),     // Old Maintenance Yard
            new Vector4(503f, 556f, -55f, -37f),    // coast road + overlook
        };

        private static int s_structMask;
        private static readonly Collider[] Hits = new Collider[16];

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Fbm(float x, float z, float s, float ox, float oz)
        {
            return Mathf.PerlinNoise(x * s + ox, z * s + oz) * 0.6f + Mathf.PerlinNoise(x * s * 2.3f + ox + 9.1f, z * s * 2.3f + oz + 3.3f) * 0.4f;
        }

        private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool InFootprint(float x, float z, float pad)
        {
            foreach (Vector4 r in Footprints)
                if (x >= r.x - pad && x <= r.y + pad && z >= r.z - pad && z <= r.w + pad) return true;
            return false;
        }

        // the opening showcase herd (StoryDirector.SpawnOpeningShowcaseDinos): meadow_sauropod_a/b and the three
        // triceratops at meadow_trike + i * (10, 0, -6). Groves and clumps must not grow through them.
        private static readonly Vector3[] IntroDinos =
        {
            new Vector3(-40f, 0f, -150f), new Vector3(-95f, 0f, -185f),
            new Vector3(-18f, 0f, -142f), new Vector3(-8f, 0f, -148f), new Vector3(2f, 0f, -154f),
        };

        private static bool NearIntroDino(float x, float z, float pad)
        {
            foreach (Vector3 d in IntroDinos)
            {
                float dx = x - d.x, dz = z - d.z, rr = 14f + pad;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        private static bool OnCameraLine(float x, float z, float pad)
        {
            var p = new Vector2(x, z);
            foreach (var l in CameraLines)
                if (DistToSegment(p, l.a, l.b) < l.r + pad) return true;
            return false;
        }

        /// <summary>True when any zone 1-3 collider (not the terrain, not triggers) is inside the box.</summary>
        private static bool NearStructure(Vector3 groundPos, float radius, float yMin, float yMax)
        {
            Vector3 c = groundPos + Vector3.up * ((yMin + yMax) * 0.5f);
            Vector3 half = new Vector3(radius, (yMax - yMin) * 0.5f, radius);
            int n = Physics.OverlapBoxNonAlloc(c, half, Hits, Quaternion.identity, s_structMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (Hits[i] is TerrainCollider) continue;
                if (Hits[i].GetComponentInParent<LODGroup>() != null && Hits[i].transform.IsChildOf(s_root)) continue;   // our own plants
                return true;
            }
            return false;
        }

        private static Transform s_root;

        /// <summary>Forest density 0..1 at a point (clumps, clearings, valley meadows, belts).</summary>
        private static float ForestDensity(float x, float z, float y, bool baseMeadow = false)
        {
            // groves and clearings (sharp clumps): no evenly spaced park trees
            float clump = SS(0.36f, 0.58f, Fbm(x, z, 0.011f, 4.2f, 8.8f));
            float d = 0.07f + 0.93f * clump;
            // Tyrant Valley floor: open grassland near the road, jungle at the valley sides
            if (x > 95f && x < 400f)
            {
                float road = Z4.DistanceToRoad3Internal(x, z);
                d *= Mathf.Lerp(0.12f, 1f, SS(14f, 55f, road));
            }
            // Titan Meadows: open meadow with a few trees
            float md = Vector2.Distance(new Vector2(x, z), new Vector2(MeadowCentre.x, MeadowCentre.z));
            // (trees stand in a few groves, not as evenly spaced single palms: review 1 "golf course")
            float grove = SS(0.52f, 0.62f, Fbm(x, z, 0.035f, 17.3f, 5.1f));
            float inner = baseMeadow ? 0.06f : 0.012f + 0.95f * grove;      // (8ed04fa: 0.06 everywhere in the meadow)
            d *= Mathf.Lerp(inner, 1f, SS(MeadowRadius * 0.7f, MeadowRadius * 1.25f, md));
            // fewer trees on the beach band
            d *= SS(-37.5f, -34f, y);
            return Mathf.Clamp01(d);
        }

        public static string Scatter(Transform parent, Terrain mainTerrain, List<Terrain> tiles, (float x, float z, float r)[] stealthPatches = null)
        {
            s_stealthPatches = stealthPatches ?? System.Array.Empty<(float, float, float)>();
            s_structMask = GameLayers.Mask(GameLayers.Default) | GameLayers.Mask(GameLayers.Environment) | GameLayers.Mask(GameLayers.Door);
            Physics.SyncTransforms();
            var root = new GameObject("IslaTormenta_JungleScatter");
            root.transform.SetParent(parent, false);
            s_root = root.transform;

            int unified = UnifyLeafGreens();
            int trees = ScatterPlayAreaTrees(root.transform);
            int under = ScatterUnderstory(root.transform);
            under += ScatterYardBelt(root.transform);
            int rocks = ScatterRocks(root.transform);
            int terrainTrees = ScatterTerrainTrees(tiles);
            rocks += ScatterCoastRocks(root.transform);
            rocks += ScatterLakeShore(root.transform);
            rocks += ScatterCliffDressing(root.transform);
            int pond = s_pondTrees.Count;
            foreach (GameObject g in s_pondTrees) if (g != null) Object.DestroyImmediate(g);
            s_pondTrees.Clear();
            return $"jungle: {trees} trees ({pond} objects removed from the pond after the scatter), {under} understory, {rocks} rocks/logs as GameObjects; {terrainTrees} hill trees; {unified} zone 1-3 plant renderers on the leaf-green materials";
        }

        private static readonly string[] SolidTropicalStems = { "jungle_tree", "palm_tree", "palm_tree_b", "tree_fern", "banana_plant", "monstera_clump" };

        /// <summary>
        /// Fix round ('two greens per species'): the plants the zone 1-3 builders place (plaza and Visitor Center palms,
        /// service-path dressing) use the yellow-olive MAT_&lt;stem&gt; atlases, the island's plants the leaf-green copies.
        /// Zones 1-3 are built before zone 4, so their renderers are switched to the same green copies here (material
        /// only: no object, collider or transform changes). LookMaterials only swaps FBX-embedded materials, so it keeps it.
        /// </summary>
        private static int UnifyLeafGreens()
        {
            var map = new Dictionary<Material, Material>();
            foreach (string stem in SolidTropicalStems)
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Art/Materials/Models/Plants/MAT_{stem}.mat");
                if (src == null)
                {
                    string[] g = AssetDatabase.FindAssets($"MAT_{stem} t:Material", new[] { "Assets/Art/Materials/Models" });
                    foreach (string id in g)
                    {
                        var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));
                        if (m != null && m.name == $"MAT_{stem}") { src = m; break; }
                    }
                }
                if (src == null) continue;
                Material green = LookVegetationPrefabs.GreenLeafMaterial(src, stem);
                if (green != null && green != src) map[src] = green;
            }
            if (map.Count == 0) return 0;
            int n = 0;
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                Material[] sm = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < sm.Length; i++)
                    if (sm[i] != null && map.TryGetValue(sm[i], out Material g)) { sm[i] = g; any = true; }
                if (!any) continue;
                r.sharedMaterials = sm;
                n++;
            }
            return n;
        }

        private static float GroundY(float x, float z) => Z4.EvaluateWorldElevation(x, z);

        private static float SlopeDeg(float x, float z)
        {
            float gx = (GroundY(x + 1.5f, z) - GroundY(x - 1.5f, z)) / 3f;
            float gz = (GroundY(x, z + 1.5f) - GroundY(x, z - 1.5f)) / 3f;
            return Mathf.Atan(Mathf.Sqrt(gx * gx + gz * gz)) * Mathf.Rad2Deg;
        }

        /// <summary>The 7 TallGrassZoneVolume stealth patches (x, z, radius): no tree, rock or log may block them.</summary>
        private static (float x, float z, float r)[] s_stealthPatches = System.Array.Empty<(float, float, float)>();

        private static bool InStealthPatch(float x, float z, float pad)
        {
            foreach (var p in s_stealthPatches)
            {
                float dx = x - p.x, dz = z - p.z, rr = p.r + 2f + pad;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        private static bool CommonReject(float x, float z, float pad)
        {
            if (InFootprint(x, z, pad)) return true;
            if (InStealthPatch(x, z, pad)) return true;
            if (OnCameraLine(x, z, pad)) return true;
            if (x > 86f && x < 406f && Z4.DistanceToRoad3Internal(x, z) < 5.5f + pad) return true;
            if (x > 150f && x < 260f && Mathf.Abs(z) < 200f && Mathf.Abs(x - Z4.EvaluateStreamX(z)) < 5.5f + pad) return true;
            return false;
        }

        private static GameObject Place(string stem, Transform parent, Vector3 pos, float yaw, float scale, string name, float tiltDeg = 0f, float tiltDir = 0f)
        {
            GameObject pf = LookVegetationPrefabs.Get(stem);
            if (pf == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
            // instances keep the prefab's name and flags: every per-instance override is serialized ~35,000 times
            Quaternion rot = Quaternion.AngleAxis(tiltDeg, Quaternion.Euler(0f, tiltDir, 0f) * Vector3.right) * Quaternion.Euler(0f, yaw, 0f);
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ------------------------------------------------------------------------------------------------------------
        // TREES (play area + margin)
        // ------------------------------------------------------------------------------------------------------------
        private struct TreePick { public bool ok; public int s; public float scale; }

        /// <summary>The 8ed04fa acceptance test for one candidate (species, size and every reject) at a given density.</summary>
        private static TreePick PickTree(float x, float z, Vector3 ground, float density, double r3, double r4, double r5)
        {
            var t = new TreePick();
            if (r3 > density * 0.72f) return t;
            // species: palms on low open ground and the coast side, jungle trees in the belts
            float palmBias = SS(0.2f, 0.8f, 1f - density) * 0.35f + (x > 420f ? 0.25f : 0f);
            int s;
            if (r4 < 0.38 - palmBias * 0.5) s = 0;
            else if (r4 < 0.62) s = 1;
            else if (r4 < 0.80) s = 2;
            else s = 3;
            float scale = s == 0 ? Mathf.Lerp(0.62f, 1.05f, (float)r5) : (s == 3 ? Mathf.Lerp(0.8f, 1.5f, (float)r5) : Mathf.Lerp(0.85f, 1.35f, (float)r5));
            float crown = (s == 0 ? 6.5f : (s == 3 ? 1.6f : 3.0f)) * scale;
            float trunkPad = s == 3 ? 1.2f : 2.0f;
            if (CommonReject(x, z, trunkPad)) return t;
            if (NearStructure(ground, trunkPad + 0.5f, -0.5f, 3.5f)) return t;
            // crowns of big trees must not cut into tall buildings
            if (s == 0 && NearStructure(ground, crown * 0.6f, 6f * scale, 15f * scale)) return t;
            if (s != 3 && NearStructure(ground, crown * 0.5f, 4.5f * scale, 9f * scale)) return t;
            t.ok = true; t.s = s; t.scale = scale;
            return t;
        }

        /// <summary>
        /// Trees in and next to the play area. The NavMesh is baked from their trunk colliders, so outside Titan Meadows
        /// every trunk is exactly where 8ed04fa put it (same random stream, same species, same world-space collider);
        /// in the meadow the trees stand in groves and keep clear of the opening showcase herd. Only the visual size
        /// of the broad jungle trees changes (0.7-1.6, review 1), with the collider scaled back to its old world size.
        /// </summary>
        /// <summary>True when a tree here would stand in the pond or hang its crown over it (ground under the lake level
        /// + 0.3 m at the trunk or anywhere 3.5 m around it).</summary>
        private static bool InPond(float x, float z)
        {
            if (Vector2.Distance(new Vector2(x, z), Z4.LakeCentre) > 90f) return false;
            float lim = Z4.LakeLevel + 0.3f;
            if (GroundY(x, z) < lim) return true;
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f;
                if (GroundY(x + Mathf.Cos(a) * 3.5f, z + Mathf.Sin(a) * 3.5f) < lim) return true;
            }
            return false;
        }

        /// <summary>
        /// Round 5 fix round (r5 critic): the flat ground-shade quad of the impostor level clips into the slope on one side
        /// and floats on the other where the ground leans more than 4 deg: switched off there (a renderer flag, no
        /// structural prefab override).
        /// </summary>
        private static void DropBlobOnSlope(GameObject tree, float slope)
        {
            if (slope <= 4f || tree == null) return;
            foreach (Renderer r in tree.GetComponentsInChildren<Renderer>(true))
                if (r.name.EndsWith("_ImpostorBlob")) r.enabled = false;
        }

        /// <summary>Round 5 fix round: ground here is under the Titan Meadows pond's water (+0.2 m), so a plant would float on it.</summary>
        private static bool InLakeWater(float x, float z, float y) =>
            y < Z4.LakeLevel + 0.2f && Vector2.Distance(new Vector2(x, z), Z4.LakeCentre) < 95f;

        private static int ScatterPlayAreaTrees(Transform root)
        {
            var parent = new GameObject("Trees").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(90421);
            const float cell = 6.0f;
            int placed = 0;
            // Trees standing in the Titan Meadows pond are placed like every other tree and removed after the scatter:
            // later cells test their clearance against the colliders of earlier trees (NearStructure), and that test
            // decides how many numbers the stream consumes, so skipping a pond tree re-rolled every tree after it
            // (round 4 critic: 995 -> 937 jungle trees, the staged views changed).
            var pondCells = new List<(int from, int to)>();
            float x0 = Z4.PlayMinX - GoMargin, x1 = Z4.PlayMaxX + GoMargin, z0 = Z4.PlayMinZ - GoMargin, z1 = Z4.PlayMaxZ + GoMargin;
            for (float cz = z0; cz < z1; cz += cell)
            {
                for (float cx = x0; cx < x1; cx += cell)
                {
                    if (pondCells.Count > 0 && pondCells[pondCells.Count - 1].to < 0) pondCells[pondCells.Count - 1] = (pondCells[pondCells.Count - 1].from, parent.childCount);
                    double r1 = rng.NextDouble(), r2 = rng.NextDouble(), r3 = rng.NextDouble(), r4 = rng.NextDouble(), r5 = rng.NextDouble();
                    float x = cx + (float)r1 * cell, z = cz + (float)r2 * cell;
                    if (Z4.DistanceOutsidePlayArea(x, z) > GoMargin) continue;
                    float y = GroundY(x, z);
                    if (y < -36f) continue;
                    float slope = SlopeDeg(x, z);
                    if (slope > 34f) continue;

                    // belts: right behind walls and fences the jungle closes in
                    var ground = new Vector3(x, y, z);
                    bool nearWall = NearStructure(ground, 9f, 0.3f, 6f);
                    float baseDensity = ForestDensity(x, z, y, true), density = ForestDensity(x, z, y, false);
                    if (nearWall) { baseDensity = Mathf.Max(baseDensity, 0.75f); density = Mathf.Max(density, 0.75f); }
                    TreePick b = PickTree(x, z, ground, baseDensity, r3, r4, r5);
                    TreePick m = Mathf.Approximately(baseDensity, density) ? b : PickTree(x, z, ground, density, r3, r4, r5);
                    if (m.ok && NearIntroDino(x, z, 0f)) m.ok = false;

                    // the base consumed three more numbers for every tree it placed: keep the stream identical
                    double ry = 0, rt = 0, rd = 0;
                    if (b.ok) { ry = rng.NextDouble(); rt = rng.NextDouble(); rd = rng.NextDouble(); }
                    if (!m.ok) continue;
                    // review 3: big trees stood in the Titan Meadows pond (trunks out of the water, crowns over half of it).
                    if (InPond(x, z)) pondCells.Add((parent.childCount, -1));   // closed at the start of the next cell
                    if (!b.ok || b.s != m.s)
                    {
                        // a new meadow grove tree: its own hash stream
                        var h = new System.Random(unchecked((int)(cx * 7919f) * 31 + (int)(cz * 104729f) + 17));
                        ry = h.NextDouble(); rt = h.NextDouble(); rd = h.NextDouble();
                    }
                    int s = m.s;
                    float scale = m.scale;
                    string stem = TreeStems[s];

                    // sink on slopes so the base never floats
                    float sink = 0.15f + Mathf.Tan(slope * Mathf.Deg2Rad) * (s == 0 ? 1.4f : 0.6f);
                    float yaw = (float)(ry * 360.0);
                    float tilt = (float)rt * (s == 1 || s == 2 ? 7f : 3f);
                    float vis = scale;
                    // (not round the Old Yard: the crane shot of the ending looks over it, and taller trees showed
                    // more bare trunk above the yard wall)
                    if (s == 0 && OutsideYard(x, z) > 60f)
                    {
                        float big = Mathf.Lerp(0.7f, 1.6f, (float)r5);
                        if (big <= scale || (!NearStructure(ground, 6.5f * big * 0.6f, 6f * big, 15f * big) && !NearStructure(ground, 6.5f * big * 0.5f, 4.5f * big, 9f * big))) vis = big;
                    }
                    if (s == 0)
                    {
                        // review 2 (night_a 17/18): the valley was one umbrella tree, cloned, upright and evenly spaced.
                        // Broad jungle trees now get their own crown width and depth (independent of the height), a
                        // 4-8 deg lean and an understory clump; the trunk collider stays upright on a holder at exactly
                        // its old world position and size (the NavMesh is baked from it)
                        var hv = new System.Random(unchecked((int)(x * 7919f) * 31 + (int)(z * 104729f) + 911));
                        float wide = Mathf.Lerp(0.75f, 1.35f, (float)hv.NextDouble());
                        float deep = wide * Mathf.Lerp(0.82f, 1.15f, (float)hv.NextDouble());
                        float lean = Mathf.Lerp(4f, 8f, (float)hv.NextDouble());
                        // fix round: an independent height factor (same crown on a taller or shorter trunk)
                        float tall = Mathf.Lerp(0.85f, 1.15f, (float)hv.NextDouble());
                        float leanDir = (float)(rd * 360.0);
                        // round 4 (review 3, 28_ending_2: one pale-trunked umbrella tree at one height repeated across
                        // the crane shot between the yard wall and the hills): round the Old Yard the visible tree varies
                        // in species (palms, tree ferns), height (about 9-22 m) and lean, and the trunks darken with
                        // distance from the ending camera. Visual only: the holder keeps the jungle tree's trunk collider.
                        float oy = OutsideYard(x, z);
                        // round 5 (r4 critic): the belt only where the crane shot looks (west of the ending camera at
                        // (540, -46), within 140 m of its axis); a 130 m ring re-dressed chapter 5 round Substation 3
                        // (fix round, r5 critic: x 380-540 covered only the first 160 m of the shot; the belt is now the
                        // crane camera's view wedge, 290 m deep and widening with the distance like the frustum)
                        bool belt = oy > 6f && x > 250f && x < 540f && Mathf.Abs(z + 46f) < 0.6f * (540f - x) + 40f;
                        string beltStem = stem;
                        bool darkBark = false;
                        if (belt)
                        {
                            var hb = new System.Random(unchecked((int)(x * 4993f) * 17 + (int)(z * 7727f) + 2029));
                            double pick = hb.NextDouble(), size = hb.NextDouble(), lean2 = hb.NextDouble();
                            if (oy > 14f)
                            {
                                if (pick < 0.20) beltStem = "palm_tree";
                                else if (pick < 0.32) beltStem = "palm_tree_b";
                                else if (pick < 0.44) beltStem = "tree_fern";
                            }
                            bool fern = beltStem == "tree_fern";
                            // palms stand in the crown layer (1.3-1.8: their crowns reach the jungle crowns), ferns under it
                            float k2 = beltStem == "jungle_tree" ? Mathf.Lerp(0.62f, 1.5f, (float)size)
                                     : (fern ? Mathf.Lerp(1.6f, 2.4f, (float)size) : Mathf.Lerp(1.3f, 1.8f, (float)size));
                            float cand = k2;
                            if (beltStem == "jungle_tree")
                            {
                                if (oy > 14f && !NearStructure(ground, 6.5f * cand * 0.6f, 6f * cand, 15f * cand)) vis = cand;
                                tall = Mathf.Lerp(0.8f, 1.25f, (float)lean2); lean = Mathf.Lerp(3f, 11f, (float)hb.NextDouble());
                                // jungle trees capped at about 22 m (vis x tall 1.22 of the 18 m tree); fix round: the cap
                                // scales the whole tree down (clamping only the height pancaked the crown and its cards)
                                if (vis * tall > 1.22f) vis = 1.22f / tall;
                            }
                            else
                            {
                                // palms and tree ferns: their own crown clearance and their own (thin) bodies
                                wide = deep = tall = 1f;
                                vis = !NearStructure(ground, (fern ? 1.6f : 3.0f) * cand * 0.5f, 4.5f * cand, 9f * cand) ? cand : scale;
                            }
                            float camD = Vector2.Distance(new Vector2(x, z), new Vector2(540f, -46f));
                            darkBark = beltStem == "jungle_tree" && (camD > 60f || hb.NextDouble() < SS(30f, 60f, camD) * 0.9f + 0.1f);
                        }
                        bool beltPlant = belt && beltStem != "jungle_tree";
                        // fix round: the clearance test uses the real crown (wider, taller, leaned), not the upright one
                        float reach = 6.5f * vis * Mathf.Max(wide, deep);
                        Vector3 leanOff = Quaternion.Euler(0f, leanDir, 0f) * Vector3.forward * (Mathf.Sin(lean * Mathf.Deg2Rad) * 12f * vis * tall);
                        if (!beltPlant && (wide > 1f || deep > 1f || tall > 1f || lean > tilt) && NearStructure(ground + leanOff, reach * 0.6f, 6f * vis, 15f * vis * Mathf.Max(1f, tall)))
                        { wide = deep = 1f; lean = tilt; tall = 1f; leanOff = Vector3.zero; }
                        var holder = new GameObject($"Tree_{stem}_{placed:0000}");
                        holder.layer = Cretacea.Core.GameLayers.Environment;
                        holder.transform.SetParent(parent, false);
                        holder.transform.position = new Vector3(x, y - sink, z);
                        GameObject vt = Place(beltStem, holder.transform, new Vector3(x, y - sink, z), yaw, vis, beltStem, beltStem == "jungle_tree" ? lean : lean * 0.5f, leanDir);
                        if (vt == null) { Object.DestroyImmediate(holder); continue; }
                        if (beltStem == "jungle_tree") vt.transform.localScale = new Vector3(vis * wide, vis * tall, vis * deep);
                        if (darkBark) DarkenBark(vt);
                        DropBlobOnSlope(vt, slope);
                        var hc = holder.AddComponent<CapsuleCollider>();
                        hc.direction = 1;
                        // (fix round: a belt palm / fern keeps no upright 0.55 m jungle-trunk wall round its thin, leaning trunk)
                        hc.radius = beltPlant ? Mathf.Min(0.55f * scale, (beltStem == "tree_fern" ? 0.22f : 0.30f) * vis) : 0.55f * scale;
                        hc.height = 7f * scale;
                        hc.center = new Vector3(0f, hc.height * 0.5f + sink, 0f);
                        // the visible trunk is wider and leans: a player-only body that follows it (IgnoreRaycast
                        // collides with the player but is outside the NavMesh layer mask, so the NavMesh is unchanged)
                        if (beltPlant)
                            AddPlayerBody(holder.transform, new Vector3(x, y - sink, z), lean * 0.5f, leanDir, (beltStem == "tree_fern" ? 0.22f : 0.30f) * vis, 4f * vis, sink);
                        else
                            AddPlayerBody(holder.transform, new Vector3(x, y - sink, z), lean, leanDir, 0.55f * vis * Mathf.Max(wide, deep), 7f * vis * tall, sink);
                        placed++;
                        // understory, companions, roots and vines only where the player sees trees at foot level
                        if (SeenAtFootLevel(x, z))
                        {
                            placed += TreeUnderstory(parent, x, z, vis, hv);
                            placed += TreeCompanions(parent, holder.transform, x, y - sink, z, vis, wide, deep, tall, lean, leanDir, hv);
                        }
                        continue;
                    }
                    GameObject t = Place(stem, parent, new Vector3(x, y - sink, z), yaw, vis, $"Tree_{stem}_{placed:0000}", tilt, (float)(rd * 360.0));
                    if (t == null) continue;
                    DropBlobOnSlope(t, slope);

                    // trunk collider (the NavMesh is baked from colliders; no carving obstacles)
                    var cap = t.AddComponent<CapsuleCollider>();
                    cap.direction = 1;
                    float k = scale / vis;      // local -> world: same world radius, height and base as before
                    cap.radius = (s == 0 ? 0.55f : (s == 3 ? 0.22f : 0.30f)) * k;
                    cap.height = (s == 0 ? 7f : 4f) * k;
                    cap.center = new Vector3(0f, cap.height * 0.5f + sink / vis, 0f);
                    placed++;
                }
            }
            if (pondCells.Count > 0 && pondCells[pondCells.Count - 1].to < 0) pondCells[pondCells.Count - 1] = (pondCells[pondCells.Count - 1].from, parent.childCount);
            // removed only at the very end of Scatter: the scatters after this one test against these colliders too
            s_pondTrees.Clear();
            foreach (var (from, to) in pondCells)
                for (int i = from; i < to && i < parent.childCount; i++) s_pondTrees.Add(parent.GetChild(i).gameObject);
            return placed;
        }

        private static readonly List<GameObject> s_pondTrees = new List<GameObject>();

        private static readonly (string stem, float min, float max)[] TreeBaseStems =
        {
            ("banana_plant", 0.8f, 1.3f), ("tree_fern", 0.55f, 0.9f), ("monstera_clump", 0.9f, 1.5f), ("fern_02", 1.2f, 2.0f),
        };

        /// <summary>
        /// 0-3 understory plants round the foot of a broad jungle tree (no colliders, like all understory), so trees
        /// stand in clumps of vegetation instead of alone on mown lawn. Uses its own hash stream (the tree stream and
        /// every trunk position stay as they were).
        /// </summary>
        private static int TreeUnderstory(Transform parent, float x, float z, float vis, System.Random hv)
        {
            // (fix round: 0-3 per tree everywhere added ~1,300 LODGroups; 0-2, and only where seen at foot level)
            int n = Mathf.FloorToInt((float)hv.NextDouble() * 2.4f);
            int placed = 0;
            for (int i = 0; i < n; i++)
            {
                double a = hv.NextDouble() * System.Math.PI * 2.0, rr = hv.NextDouble(), pk = hv.NextDouble(), sc = hv.NextDouble(), yw = hv.NextDouble();
                float dist = Mathf.Lerp(1.6f, 4.2f, (float)rr) * Mathf.Max(0.8f, vis);
                float px = x + Mathf.Cos((float)a) * dist, pz = z + Mathf.Sin((float)a) * dist;
                if (CommonReject(px, pz, 0.8f) || NearIntroDino(px, pz, 0f)) continue;
                float py = GroundY(px, pz);
                if (py < Z4.LakeLevel + 0.15f) continue;      // fix round: plants stood in the pond
                if (NearStructure(new Vector3(px, py, pz), 0.9f, -0.3f, 2.5f)) continue;
                var u = TreeBaseStems[Mathf.Min(TreeBaseStems.Length - 1, (int)(pk * TreeBaseStems.Length))];
                float slope = SlopeDeg(px, pz);
                if (slope > 34f) continue;
                if (Place(u.stem, parent, new Vector3(px, py - 0.08f - Mathf.Tan(slope * Mathf.Deg2Rad) * 0.3f, pz), (float)(yw * 360.0), Mathf.Lerp(u.min, u.max, (float)sc), $"TreeBase_{u.stem}") != null) placed++;
            }
            return placed;
        }

        /// <summary>Within 40 m of the valley road, a camera line or a stealth patch (where trees are seen from the ground).</summary>
        private static bool SeenAtFootLevel(float x, float z)
        {
            if (x > 86f && x < 406f && Z4.DistanceToRoad3Internal(x, z) < 40f) return true;
            return OnCameraLine(x, z, 40f) || InStealthPatch(x, z, 38f);
        }

        /// <summary>A collider on the IgnoreRaycast layer: blocks the player, ignored by the NavMesh bake and raycasts.</summary>
        private static void AddPlayerBody(Transform holder, Vector3 basePos, float lean, float leanDir, float radius, float height, float sink)
        {
            var body = new GameObject("PlayerBody");
            body.layer = GameLayers.IgnoreRaycast;
            body.transform.SetParent(holder, false);
            body.transform.SetPositionAndRotation(basePos, Quaternion.AngleAxis(lean, Quaternion.Euler(0f, leanDir, 0f) * Vector3.right));
            var c = body.AddComponent<CapsuleCollider>();
            c.direction = 1;
            c.radius = radius;
            c.height = Mathf.Max(height, radius * 2f + 0.1f);
            c.center = new Vector3(0f, c.height * 0.5f + sink * 0.5f, 0f);
        }

        private static readonly string[] CompanionStems = { "palm_tree", "palm_tree_b", "tree_fern" };

        /// <summary>
        /// Fix round (review 2 'one umbrella tree in rows'): each broad jungle tree seen from the ground gets 1-2 smaller
        /// companions of another species 4-9 m away (a grove instead of a lone kapok), a root cluster at half the trunks
        /// and hanging vines in 30 % of the crowns. Its own hash stream; trunk positions and the NavMesh are unchanged
        /// (companions get player-only bodies, no NavMesh colliders).
        /// </summary>
        private static int TreeCompanions(Transform parent, Transform holder, float x, float baseY, float z, float vis, float wide, float deep, float tall, float lean, float leanDir, System.Random hv)
        {
            int placed = 0;
            int n = 1 + (hv.NextDouble() < 0.45 ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                double a = hv.NextDouble() * System.Math.PI * 2.0, rr = hv.NextDouble(), pk = hv.NextDouble(), sc = hv.NextDouble(), yw = hv.NextDouble(), tl = hv.NextDouble();
                float dist = Mathf.Lerp(4f, 9f, (float)rr);
                float px = x + Mathf.Cos((float)a) * dist, pz = z + Mathf.Sin((float)a) * dist;
                string cs = CompanionStems[Mathf.Min(CompanionStems.Length - 1, (int)(pk * CompanionStems.Length))];
                float scale = Mathf.Lerp(0.9f, 1.3f, (float)sc) * (cs == "tree_fern" ? 0.75f : 1f);
                if (CommonReject(px, pz, 2f) || NearIntroDino(px, pz, 0f)) continue;
                float py = GroundY(px, pz);
                if (py < Z4.LakeLevel + 0.4f || py < -36f) continue;
                float slope = SlopeDeg(px, pz);
                if (slope > 30f) continue;
                var g = new Vector3(px, py, pz);
                if (NearStructure(g, 2.5f, -0.5f, 3.5f)) continue;
                if (NearStructure(g, 3f * scale * 0.5f, 4.5f * scale, 9f * scale)) continue;
                float sink = 0.15f + Mathf.Tan(slope * Mathf.Deg2Rad) * 0.6f;
                float cTilt = Mathf.Lerp(2f, 9f, (float)tl);
                float cDir = (float)(yw * 720.0 % 360.0);
                var go = Place(cs, parent, new Vector3(px, py - sink, pz), (float)(yw * 360.0), scale, $"TreeMate_{cs}", cTilt, cDir);
                if (go == null) continue;
                AddPlayerBody(CompanionHolder(parent, go), new Vector3(px, py - sink, pz), cTilt, cDir, (cs == "tree_fern" ? 0.22f : 0.30f) * scale, 4f * scale, sink);
                placed++;
            }
            // root cluster round the foot of half the trunks
            if (hv.NextDouble() < 0.5)
            {
                float rs = Mathf.Lerp(0.55f, 0.8f, (float)hv.NextDouble()) * vis * Mathf.Max(wide, deep);
                if (Place("root_cluster_01", holder, new Vector3(x, baseY + 0.05f, z), (float)(hv.NextDouble() * 360.0), rs, "TreeRoots") != null) placed++;
            }
            // hanging vines from 30 % of the crowns (under the crown, following the lean)
            if (hv.NextDouble() < 0.3)
            {
                int nv = 1 + (hv.NextDouble() < 0.5 ? 1 : 0);
                for (int i = 0; i < nv; i++)
                {
                    double a = hv.NextDouble() * System.Math.PI * 2.0, rr = hv.NextDouble(), vs = hv.NextDouble();
                    float vsc = Mathf.Lerp(1.0f, 1.6f, (float)vs) * Mathf.Max(0.8f, vis);
                    float top = 10f * vis * tall;
                    float r = Mathf.Lerp(2.0f, 4.5f, (float)rr) * vis * Mathf.Lerp(wide, deep, 0.5f);
                    Vector3 lo = Quaternion.Euler(0f, leanDir, 0f) * Vector3.forward * (Mathf.Sin(lean * Mathf.Deg2Rad) * top);
                    var vp = new Vector3(x + Mathf.Cos((float)a) * r, baseY + top - 2.6f * vsc, z + Mathf.Sin((float)a) * r) + lo;
                    if (vp.y < GroundY(vp.x, vp.z) + 2.4f) continue;     // keep the tips above head height
                    if (Place("hanging_vines", holder, vp, (float)(a * Mathf.Rad2Deg), vsc, "TreeVines") != null) placed++;
                }
            }
            return placed;
        }

        // the companion is a prefab instance: its player body goes on a small holder next to it (no prefab overrides)
        private static Transform CompanionHolder(Transform parent, GameObject plant)
        {
            var h = new GameObject(plant.name + "_Body");
            h.layer = GameLayers.IgnoreRaycast;
            h.transform.SetParent(parent, false);
            return h.transform;
        }

        // ------------------------------------------------------------------------------------------------------------
        // UNDERSTORY (play area + margin)
        // ------------------------------------------------------------------------------------------------------------
        private static readonly (string stem, float min, float max, float weight)[] Understory =
        {
            ("banana_plant", 0.8f, 1.35f, 1.4f),
            ("monstera_clump", 0.9f, 1.8f, 1.6f),
            ("shrub_02", 0.8f, 1.5f, 1.4f),
            ("pachira_aquatica_01", 1.2f, 2.2f, 0.7f),
            ("fern_02", 1.2f, 2.4f, 2.4f),
            ("anthurium_botany_01", 1.0f, 1.8f, 0.8f),
            ("calathea_orbifolia_01", 1.4f, 2.6f, 0.6f),
            ("tree_fern", 0.55f, 0.9f, 0.8f),
        };

        private static int ScatterUnderstory(Transform root)
        {
            var parent = new GameObject("Understory").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(77177);
            float total = 0f;
            foreach (var u in Understory) total += u.weight;
            const float cell = 3.4f;
            int placed = 0;
            float x0 = Z4.PlayMinX - GoMargin, x1 = Z4.PlayMaxX + GoMargin, z0 = Z4.PlayMinZ - GoMargin, z1 = Z4.PlayMaxZ + GoMargin;
            for (float cz = z0; cz < z1; cz += cell)
            {
                for (float cx = x0; cx < x1; cx += cell)
                {
                    double r1 = rng.NextDouble(), r2 = rng.NextDouble(), r3 = rng.NextDouble(), r4 = rng.NextDouble(), r5 = rng.NextDouble(), r6 = rng.NextDouble();
                    float x = cx + (float)r1 * cell, z = cz + (float)r2 * cell;
                    if (Z4.DistanceOutsidePlayArea(x, z) > GoMargin) continue;
                    float y = GroundY(x, z);
                    if (y < -37f) continue;
                    if (InLakeWater(x, z, y)) continue;   // r5 fix round: shrubs and ferns floated on the pond (all six numbers are drawn above: the stream is unchanged)
                    float slope = SlopeDeg(x, z);
                    if (slope > 38f) continue;
                    float density = ForestDensity(x, z, y);
                    var ground = new Vector3(x, y, z);
                    bool nearWall = NearStructure(ground, 5f, 0.2f, 3f);
                    if (nearWall) density = Mathf.Max(density, 0.85f);
                    // forest edges are lush: boost where the density field changes
                    float edge = Mathf.Abs(ForestDensity(x + 6f, z, y) - ForestDensity(x - 6f, z, y)) + Mathf.Abs(ForestDensity(x, z + 6f, y) - ForestDensity(x, z - 6f, y));
                    density = Mathf.Clamp01(density * 0.8f + edge * 1.2f);
                    // Titan Meadows: shrub and fern clumps in the open grass (uneven ground cover, not a mown lawn)
                    float mdist = Vector2.Distance(new Vector2(x, z), new Vector2(MeadowCentre.x, MeadowCentre.z));
                    float meadowClump = SS(0.48f, 0.58f, Fbm(x, z, 0.06f, 3.7f, 9.9f)) * (1f - SS(MeadowRadius * 0.9f, MeadowRadius * 1.3f, mdist));
                    if (meadowClump > 0.05f && r3 < meadowClump * 0.75f && !NearIntroDino(x, z, 0f))
                    {
                        // a clump of 3 shrubs / ferns / bananas round the cell (own hash rng: the main stream stays as it was)
                        placed += MeadowClump(parent, x, z, cx, cz);
                        continue;
                    }
                    if (r3 > density * 0.55f) continue;

                    double pick = r4 * total; int k = 0;
                    for (; k < Understory.Length - 1; k++) { pick -= Understory[k].weight; if (pick < 0) break; }
                    var u = Understory[k];
                    float scale = Mathf.Lerp(u.min, u.max, (float)r5);
                    float pad = 0.6f;
                    if (CommonReject(x, z, pad)) continue;
                    if (NearIntroDino(x, z, 0f)) continue;
                    if (NearStructure(ground, 0.9f, -0.3f, 2.5f)) continue;
                    GameObject go = Place(u.stem, parent, new Vector3(x, y - 0.08f - Mathf.Tan(slope * Mathf.Deg2Rad) * 0.3f, z), (float)(r6 * 360.0), scale, $"Plant_{u.stem}_{placed:00000}");
                    if (go != null) placed++;
                }
            }
            return placed;
        }

        private static readonly (string stem, float min, float max)[] MeadowStems =
        {
            // light species first (fern_02 is 6 k triangles at LOD0, shrub_02 18 k): the meadow is the opening shot
            ("fern_02", 1.6f, 2.8f), ("banana_plant", 0.9f, 1.4f), ("fern_02", 1.4f, 2.4f), ("shrub_02", 1.2f, 2.2f), ("fern_02", 1.8f, 3.0f), ("banana_plant", 1.1f, 1.6f),
        };

        /// <summary>Titan Meadows: three jittered plants per accepted cell, so the open grass breaks into thickets.</summary>
        private static int MeadowClump(Transform parent, float x, float z, float cx, float cz)
        {
            var rng = new System.Random(unchecked((int)(cx * 7919f) * 31 + (int)(cz * 104729f)));
            int n = 0;
            const int count = 3;
            for (int i = 0; i < count; i++)
            {
                float px = x + ((float)rng.NextDouble() - 0.5f) * 3.8f, pz = z + ((float)rng.NextDouble() - 0.5f) * 3.8f;
                var spec = MeadowStems[(int)(rng.NextDouble() * MeadowStems.Length) % MeadowStems.Length];
                float scale = Mathf.Lerp(spec.min, spec.max, (float)rng.NextDouble());
                float yaw = (float)(rng.NextDouble() * 360.0);
                if (CommonReject(px, pz, 0.6f) || NearIntroDino(px, pz, 0f)) continue;
                float py = GroundY(px, pz);
                if (py < -37f) continue;
                if (InLakeWater(px, pz, py)) continue;   // r5 fix round: meadow clumps floated on the pond
                float slope = SlopeDeg(px, pz);
                if (slope > 30f) continue;
                if (NearStructure(new Vector3(px, py, pz), 0.9f, -0.3f, 2.5f)) continue;
                if (Place(spec.stem, parent, new Vector3(px, py - 0.08f - Mathf.Tan(slope * Mathf.Deg2Rad) * 0.3f, pz), yaw, scale, "MeadowPlant") != null) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------------------------------------------------
        // OLD YARD BELT: the crane shot of the ending looks west over the yard; outside its walls the trunks of the
        // belt read as a fence. Dense understory hides them and leaning tree ferns / palms break the crown line.
        // ------------------------------------------------------------------------------------------------------------
        private static readonly Collider[] TrunkHits = new Collider[16];

        private static bool NearOwnTrunk(Vector3 ground, float radius)
        {
            int n = Physics.OverlapSphereNonAlloc(ground + Vector3.up * 1.5f, radius, TrunkHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (TrunkHits[i] is CapsuleCollider && TrunkHits[i].transform.IsChildOf(s_root)) return true;
            return false;
        }

        private static readonly Vector4 OldYard = new Vector4(395f, 509f, -98f, 53f);

        private static Material s_darkBark;

        /// <summary>
        /// Round 4: a copy of the jungle tree's green atlas material with the bark texels at half their linear value
        /// (the solid atlas holds bark and leaves; texels are told apart by colour as in LookVegetationImpostors), for
        /// the trees of the ending crane shot that stand far from the camera (pale trunks read as a planted diorama).
        /// </summary>
        private static void DarkenBark(GameObject tree)
        {
            foreach (Renderer r in tree.GetComponentsInChildren<Renderer>(true))
            {
                Material[] sm = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < sm.Length; i++)
                {
                    if (sm[i] == null || sm[i].name != "MAT_jungle_tree_Leaf") continue;
                    Material d = DarkBarkMaterial(sm[i]);
                    if (d == null) return;
                    sm[i] = d; any = true;
                }
                if (any) r.sharedMaterials = sm;
            }
        }

        private static Material DarkBarkMaterial(Material src)
        {
            if (s_darkBark != null) return s_darkBark;
            var srcTex = src.GetTexture("_BaseColorMap") as Texture2D;
            if (srcTex == null) return null;
            int w = Mathf.Min(srcTex.width, 2048), h = Mathf.Min(srcTex.height, 2048);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(srcTex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var rd = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            rd.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            rd.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            Color[] px = rd.GetPixels();
            Object.DestroyImmediate(rd);
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                float cmax = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), cmin = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                bool leaf = c.g >= cmax - 1e-4f && c.g >= c.r * 0.92f && cmax > 0.02f && (cmax - cmin) / cmax > 0.18f;
                if (leaf) continue;
                Color lin = c.linear * 0.5f;
                Color g = lin.gamma;
                px[i] = new Color(g.r, g.g, g.b, c.a);
            }
            const string texPath = LookVegetationPrefabs.LeafTextureFolder + "/jungle_tree_DarkBark_Tex.asset";
            // round 5 (r4 critic): BC7 without a CPU copy (was 22 MB RGBA32, readable), copied into the existing asset
            var nt = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { wrapMode = srcTex.wrapMode, filterMode = FilterMode.Trilinear };
            nt.SetPixels(px);
            nt.Apply(true, false);
            EditorUtility.CompressTexture(nt, TextureFormat.BC7, TextureCompressionQuality.Normal);
            nt.name = System.IO.Path.GetFileNameWithoutExtension(texPath);      // CopySerialized copies the name too
            nt.Apply(false, true);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex == null) { AssetDatabase.CreateAsset(nt, texPath); tex = nt; }
            else { EditorUtility.CopySerialized(nt, tex); Object.DestroyImmediate(nt); EditorUtility.SetDirty(tex); }
            string path = LookVegetationPrefabs.LeafMaterialFolder + "/MAT_jungle_tree_Leaf_DarkBark.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src) { name = "MAT_jungle_tree_Leaf_DarkBark" }; AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            m.SetTexture("_BaseColorMap", tex);
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            s_darkBark = m;
            return m;
        }

        private static float OutsideYard(float x, float z)
        {
            float dx = Mathf.Max(OldYard.x - x, x - OldYard.y, 0f), dz = Mathf.Max(OldYard.z - z, z - OldYard.w, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static int ScatterYardBelt(Transform root)
        {
            var parent = new GameObject("OldYardBelt").transform;
            parent.SetParent(root, false);
            Physics.SyncTransforms();      // the play-area trunks placed above
            var rng = new System.Random(39509);
            string[] under = { "banana_plant", "monstera_clump", "pachira_aquatica_01", "banana_plant", "shrub_02" };
            int placed = 0;
            const float cell = 3.6f;
            for (float cz = OldYard.z - 30f; cz < OldYard.w + 30f; cz += cell)
            {
                for (float cx = OldYard.x - 55f; cx < OldYard.y + 30f; cx += cell)
                {
                    double r1 = rng.NextDouble(), r2 = rng.NextDouble(), r3 = rng.NextDouble(), r4 = rng.NextDouble(), r5 = rng.NextDouble(), r6 = rng.NextDouble(), r7 = rng.NextDouble();
                    float x = cx + (float)r1 * cell, z = cz + (float)r2 * cell;
                    // the belt reaches 30 m out, 55 m on the west side the crane shot looks across
                    float o = OutsideYard(x, z);
                    float reach = x < OldYard.x ? 55f : 30f;
                    if (o < 1.5f || o > reach) continue;
                    o *= 30f / reach;
                    if (Z4.DistanceOutsidePlayArea(x, z) > GoMargin) continue;
                    float y = GroundY(x, z);
                    if (y < -37f) continue;
                    float slope = SlopeDeg(x, z);
                    if (slope > 36f) continue;
                    var ground = new Vector3(x, y, z);
                    // thickest right outside the walls, thinning over 30 m
                    float p = 0.9f * (1f - SS(14f, 30f, o));
                    if (r3 < 0.40f * (1f - SS(18f, 30f, o)))
                    {
                        // a leaning tree fern or slim palm (thin trunk collider like the others)
                        bool fern = r4 < 0.6;
                        string stem = fern ? "tree_fern" : "palm_tree_b";
                        float scale = fern ? Mathf.Lerp(1.0f, 1.8f, (float)r5) : Mathf.Lerp(0.7f, 1.6f, (float)r5);
                        float pad = fern ? 1.2f : 2.0f;
                        if (CommonReject(x, z, pad)) continue;
                        if (NearStructure(ground, pad + 0.5f, -0.5f, 3.5f)) continue;
                        if (NearStructure(ground, (fern ? 1.6f : 3.0f) * scale * 0.5f, 4.5f * scale, 9f * scale)) continue;
                        if (NearOwnTrunk(ground, 1.6f)) continue;
                        float sink = 0.15f + Mathf.Tan(slope * Mathf.Deg2Rad) * 0.6f;
                        GameObject t = Place(stem, parent, new Vector3(x, y - sink, z), (float)(r6 * 360.0), scale, "YardBeltTree", 4f + (float)r7 * 5f, (float)(r1 * 360.0 + r2 * 77.0));
                        if (t == null) continue;
                        var cap = t.AddComponent<CapsuleCollider>();
                        cap.direction = 1;
                        cap.radius = fern ? 0.22f : 0.30f;
                        cap.height = 4f;
                        cap.center = new Vector3(0f, 2f + sink / scale, 0f);
                        Physics.SyncTransforms();
                        placed++;
                        continue;
                    }
                    if (r3 > p) continue;
                    string u = under[(int)(r4 * under.Length) % under.Length];
                    float us = u == "pachira_aquatica_01" ? Mathf.Lerp(2.4f, 3.4f, (float)r5) : (u == "banana_plant" ? Mathf.Lerp(1.4f, 2.0f, (float)r5) : Mathf.Lerp(1.4f, 2.2f, (float)r5));
                    if (CommonReject(x, z, 0.6f)) continue;
                    if (NearStructure(ground, 0.9f, -0.3f, 2.5f)) continue;
                    if (Place(u, parent, new Vector3(x, y - 0.08f - Mathf.Tan(slope * Mathf.Deg2Rad) * 0.3f, z), (float)(r6 * 360.0), us, "YardBeltPlant") != null) placed++;
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------------------------------------------------
        // ROCKS + DEADWOOD (play area + margin)
        // ------------------------------------------------------------------------------------------------------------
        private static readonly (string stem, float min, float max, bool collider)[] Rocks =
        {
            ("namaqualand_boulder_02", 0.9f, 1.8f, true),
            ("namaqualand_boulder_03", 0.8f, 1.6f, true),
            ("namaqualand_boulder_04", 0.7f, 1.4f, true),
            ("rock_moss_set_01", 0.9f, 1.7f, true),
            ("rock_moss_set_02", 0.9f, 1.8f, true),
            ("rock_face_02", 0.8f, 1.5f, true),
            ("fallen_log", 0.7f, 1.1f, true),
            ("root_cluster_01", 0.8f, 1.3f, true),
            ("tree_stump_01", 0.9f, 1.5f, true),
            ("dead_tree_trunk", 1.6f, 3.0f, false),
        };

        private static int ScatterRocks(Transform root)
        {
            var parent = new GameObject("RocksAndDeadwood").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(31337);
            int placed = 0;
            const float cell = 11f;
            float x0 = Z4.PlayMinX - GoMargin, x1 = Z4.PlayMaxX + GoMargin, z0 = Z4.PlayMinZ - GoMargin, z1 = Z4.PlayMaxZ + GoMargin;
            for (float cz = z0; cz < z1; cz += cell)
            {
                for (float cx = x0; cx < x1; cx += cell)
                {
                    double r1 = rng.NextDouble(), r2 = rng.NextDouble(), r3 = rng.NextDouble(), r4 = rng.NextDouble(), r5 = rng.NextDouble(), r6 = rng.NextDouble();
                    float x = cx + (float)r1 * cell, z = cz + (float)r2 * cell;
                    if (Z4.DistanceOutsidePlayArea(x, z) > GoMargin) continue;
                    float y = GroundY(x, z);
                    if (y < -37f) continue;
                    float slope = SlopeDeg(x, z);
                    float density = 0.25f + 0.5f * ForestDensity(x, z, y, true) + 0.4f * SS(20f, 40f, slope);
                    if (r3 > density * 0.5f) continue;
                    var spec = Rocks[(int)(r4 * Rocks.Length) % Rocks.Length];
                    float scale = Mathf.Lerp(spec.min, spec.max, (float)r5);
                    if (CommonReject(x, z, 2.5f)) continue;
                    var ground = new Vector3(x, y, z);
                    if (NearStructure(ground, 3.0f * scale, -0.3f, 2.5f)) continue;
                    GameObject go = Place(spec.stem, parent, new Vector3(x, y - 0.25f * scale - Mathf.Tan(slope * Mathf.Deg2Rad) * 1.0f * scale, z), (float)(r6 * 360.0), scale, $"Rock_{spec.stem}_{placed:000}",
                        spec.stem == "fallen_log" || spec.stem == "dead_tree_trunk" ? slope * 0.6f : 0f, (float)(r6 * 360.0));
                    if (go == null) continue;
                    if (spec.collider)
                    {
                        // one simple box per rock / log (NavMesh hole, player blocker); sized from the prefab bounds
                        var lg = go.GetComponent<LODGroup>();
                        var rs = lg.GetLODs()[0].renderers;
                        Bounds b = rs[0].bounds;
                        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                        var box = go.AddComponent<BoxCollider>();
                        box.center = go.transform.InverseTransformPoint(b.center);
                        Vector3 sz = go.transform.InverseTransformVector(b.size);
                        box.size = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z)) * 0.8f;
                    }
                    placed++;
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------------------------------------------------
        // LAKE SHORE: boulders and deadwood on the Titan Meadows waterline (breaks its round outline)
        // ------------------------------------------------------------------------------------------------------------
        private static int ScatterLakeShore(Transform root)
        {
            var parent = new GameObject("LakeShore").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(6006);
            string[] stems = { "rock_moss_set_01", "namaqualand_boulder_03", "rock_moss_set_02", "namaqualand_boulder_04", "root_cluster_01", "namaqualand_boulder_02" };
            int placed = 0;
            // walk round the rim in irregular steps: clusters of 2-4 rocks, gaps, palms and ferns leaning over the water
            float ang = 0.3f;
            while (ang < Mathf.PI * 2f + 0.3f)
            {
                ang += 0.18f + (float)rng.NextDouble() * 0.55f;
                float a = ang + ((float)rng.NextDouble() - 0.5f) * 0.8f;     // +-0.4 rad
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float rr = WaterlineRadius(dir);
                double roll = rng.NextDouble();
                if (roll < 0.30) continue;                                    // open muddy shore
                if (roll < 0.52)
                {
                    Vector2 q = Z4.LakeCentre + dir * (rr + 2.5f + (float)rng.NextDouble() * 5f);
                    if (OnCameraLine(q.x, q.y, 3f)) continue;
                    string tstem = rng.NextDouble() < 0.55 ? "palm_tree" : "tree_fern";
                    float lean = tstem == "palm_tree" ? 8f + (float)rng.NextDouble() * 8f : 4f;
                    float leanDir = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;    // towards the lake
                    if (Place(tstem, parent, new Vector3(q.x, GroundY(q.x, q.y) - 0.25f, q.y), (float)(rng.NextDouble() * 360.0), Mathf.Lerp(0.85f, 1.3f, (float)rng.NextDouble()), $"Lake_{tstem}_{placed:00}", lean, leanDir) != null) placed++;
                    continue;
                }
                int cluster = 2 + (int)(rng.NextDouble() * 3.0);
                Vector2 tangent = new Vector2(-dir.y, dir.x);
                for (int c = 0; c < cluster; c++)
                {
                    Vector2 p = Z4.LakeCentre + dir * (rr + ((float)rng.NextDouble() - 0.55f) * 3f) + tangent * (((float)rng.NextDouble() - 0.5f) * 5f);
                    if (OnCameraLine(p.x, p.y, 1f)) continue;
                    string stem = stems[(int)(rng.NextDouble() * stems.Length) % stems.Length];
                    float scale = Mathf.Lerp(0.6f, 1.9f, (float)(rng.NextDouble() * rng.NextDouble()));
                    if (Place(stem, parent, new Vector3(p.x, GroundY(p.x, p.y) - 0.3f * scale, p.y), (float)(rng.NextDouble() * 360.0), scale, $"Lake_{stem}_{placed:00}", (float)rng.NextDouble() * 10f, (float)(rng.NextDouble() * 360.0)) != null) placed++;
                }
            }
            // two fallen trees lying half in the water
            foreach (float fa in new[] { 2.2f, 4.9f })
            {
                var dir = new Vector2(Mathf.Cos(fa), Mathf.Sin(fa));
                float rr = WaterlineRadius(dir);
                Vector2 p = Z4.LakeCentre + dir * (rr - 1.5f);
                if (OnCameraLine(p.x, p.y, 1f)) continue;
                float yaw = -Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f + 25f;
                if (Place("fallen_log", parent, new Vector3(p.x, Z4.LakeLevel - 0.55f, p.y), yaw, 1.25f, $"Lake_fallen_log_{placed:00}", 6f, yaw) != null) placed++;
                if (Place("dead_tree_trunk", parent, new Vector3(p.x + dir.x * 4f, Z4.LakeLevel - 0.7f, p.y + dir.y * 4f), yaw + 70f, 2.2f, $"Lake_dead_trunk_{placed:00}", 74f, yaw) != null) placed++;
            }
            placed += ScatterReeds(parent);
            return placed;
        }

        /// <summary>
        /// Round 5 fix round (r5 critic: 'no reeds anywhere'): reed beds in the shallow water, 0-0.4 m deep, in patches
        /// round about half of the rim (tall grass clumps standing in the water break the smooth waterline). Own random
        /// stream, after every other lake-shore draw, so the rocks and palms stay where they were.
        /// </summary>
        private static int ScatterReeds(Transform parent)
        {
            var rng = new System.Random(9119);
            int placed = 0;
            for (float a = 0f; a < Mathf.PI * 2f; a += 0.035f + (float)rng.NextDouble() * 0.05f)
            {
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float patch = Fbm(Mathf.Cos(a) * 40f, Mathf.Sin(a) * 40f, 0.05f, 2.3f, 7.7f);
                double r0 = rng.NextDouble(), r1 = rng.NextDouble(), r2 = rng.NextDouble(), r3 = rng.NextDouble(), r4 = rng.NextDouble();
                if (patch < 0.47f) continue;
                float rr = WaterlineRadius(dir);
                Vector2 tangent = new Vector2(-dir.y, dir.x);
                Vector2 p = Z4.LakeCentre + dir * (rr - 0.3f - (float)r0 * 3.2f) + tangent * (((float)r1 - 0.5f) * 1.5f);
                float gy = GroundY(p.x, p.y);
                float depth = Z4.LakeLevel - gy;
                if (depth < 0f || depth > 0.4f) continue;
                if (OnCameraLine(p.x, p.y, 1f)) continue;
                float scale = Mathf.Lerp(1.7f, 2.8f, (float)r2);
                if (Place("grass_medium_02", parent, new Vector3(p.x, gy - 0.05f, p.y), (float)(r3 * 360.0), scale, "Lake_reeds", (float)r4 * 7f, (float)(r3 * 720.0 % 360.0)) != null) placed++;
            }
            return placed;
        }

        private static float WaterlineRadius(Vector2 dir)
        {
            float rr = 3f;
            for (; rr < 70f; rr += 0.5f)
            {
                Vector2 q = Z4.LakeCentre + dir * rr;
                if (GroundY(q.x, q.y) > Z4.LakeLevel) break;
            }
            return rr;
        }

        // ------------------------------------------------------------------------------------------------------------
        // FROZEN HEADLAND STEP at x = 520 (zone 4's legacy heights add the headland term with no fade): rock faces
        // ------------------------------------------------------------------------------------------------------------
        private static int ScatterCliffDressing(Transform root)
        {
            var parent = new GameObject("HeadlandStepRocks").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(5205);
            int placed = 0;
            foreach (var range in new[] { (a: 46f, b: 270f), (a: -300f, b: -140f) })
            {
                for (float z = range.a; z <= range.b; z += 5.5f + (float)rng.NextDouble() * 3f)
                {
                    float lo = GroundY(516.5f, z), hi = GroundY(523.5f, z);
                    float h = hi - lo;
                    if (h < 1.6f) continue;
                    bool big = h > 9f;
                    string stem = big ? "mountainside" : (rng.NextDouble() < 0.6 ? "rock_face_01" : "rock_face_02");
                    float baseH = big ? 10.5f : (stem == "rock_face_01" ? 3.57f : 2.47f);
                    float scale = Mathf.Clamp((h + 2.5f) / baseH * Mathf.Lerp(0.95f, 1.2f, (float)rng.NextDouble()), 1.0f, big ? 4.5f : 5.5f);
                    float x = 519.2f + ((float)rng.NextDouble() - 0.5f) * 1.6f;
                    float yaw = -90f + ((float)rng.NextDouble() - 0.5f) * 36f;
                    if (Place(stem, parent, new Vector3(x, lo - 2f, z), yaw, scale, $"StepRock_{stem}_{placed:000}", ((float)rng.NextDouble() - 0.5f) * 8f, yaw) != null) placed++;
                    // a smaller boulder at the foot breaks the line
                    if (rng.NextDouble() < 0.45)
                    {
                        float zb = z + ((float)rng.NextDouble() - 0.5f) * 4f;
                        if (Place("namaqualand_boulder_03", parent, new Vector3(516.4f - (float)rng.NextDouble() * 1.5f, GroundY(516f, zb) - 0.4f, zb), (float)(rng.NextDouble() * 360.0), Mathf.Lerp(1.0f, 1.8f, (float)rng.NextDouble()), $"StepRock_foot_{placed:000}") != null) placed++;
                    }
                    // ferns and shrubs along the top edge soften the silhouette
                    if (rng.NextDouble() < 0.7)
                    {
                        float zt = z + ((float)rng.NextDouble() - 0.5f) * 3f, xt = 524.5f + (float)rng.NextDouble() * 4f;
                        string top = rng.NextDouble() < 0.5 ? "tree_fern" : "shrub_02";
                        if (Place(top, parent, new Vector3(xt, GroundY(xt, zt) - 0.2f, zt), (float)(rng.NextDouble() * 360.0), Mathf.Lerp(1.0f, 1.6f, (float)rng.NextDouble()), $"StepTop_{top}_{placed:000}") != null) placed++;
                    }
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------------------------------------------------
        // COAST: rocky shelves on the headlands, boulders along the waterline all round the island
        // ------------------------------------------------------------------------------------------------------------
        private static int ScatterCoastRocks(Transform root)
        {
            var parent = new GameObject("CoastRocks").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(4242);
            var centre = new Vector2(150f, -20f);
            int placed = 0;
            const int rays = 260;
            string[] boulders = { "namaqualand_boulder_03", "namaqualand_boulder_04", "rock_face_01", "rock_face_02", "namaqualand_boulder_02" };
            for (int i = 0; i < rays; i++)
            {
                float ang = (i + (float)rng.NextDouble() * 0.9f) / rays * Mathf.PI * 2f;     // irregular spacing, no rows
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                // march out to the waterline (CoastDistance crosses zero)
                float lo = 200f, hi = 1300f;
                if (LookTerrainShape.CoastDistance(centre.x + dir.x * hi, centre.y + dir.y * hi) > 0f) continue;
                for (int it = 0; it < 24; it++)
                {
                    float mid = (lo + hi) * 0.5f;
                    var p = centre + dir * mid;
                    if (LookTerrainShape.CoastDistance(p.x, p.y) > 0f) lo = mid; else hi = mid;
                }
                Vector2 shore = centre + dir * lo;
                double r = rng.NextDouble();
                bool headland = shore.x > 600f && (shore.y > 60f || shore.y < -150f);
                if (!headland && r > 0.5) continue;
                if (headland && r > 0.8) continue;      // gaps between the rock groups
                int cluster = headland ? 1 + (int)(rng.NextDouble() * 4.0) : 1 + (int)(rng.NextDouble() * 2.5);
                for (int c = 0; c < cluster; c++)
                {
                    float along = ((float)rng.NextDouble() - 0.5f) * 18f;
                    float across = ((float)rng.NextDouble() - 0.4f) * 12f;
                    Vector2 tangent = new Vector2(-dir.y, dir.x);
                    Vector2 p2 = shore + tangent * along + dir * across;
                    float y = GroundY(p2.x, p2.y);
                    if (y > LookTerrainShape.SeaLevel + 4f || y < LookTerrainShape.SeaLevel - 6f) continue;
                    string stem = boulders[(int)(rng.NextDouble() * boulders.Length) % boulders.Length];
                    // mostly small, a few big: scale distribution skewed low
                    double sr = rng.NextDouble(); sr *= sr;
                    float scale = headland ? Mathf.Lerp(1.4f, 4.4f, (float)sr) : Mathf.Lerp(0.9f, 2.8f, (float)sr);
                    float yaw = (float)(rng.NextDouble() * 360.0);
                    // sunk deeper and tilted: half-buried boulders, not planks lying on the sand
                    if (Place(stem, parent, new Vector3(p2.x, y - 0.8f * scale, p2.y), yaw, scale, $"Coast_{stem}_{placed:000}", 8f + (float)rng.NextDouble() * 22f, (float)(rng.NextDouble() * 360.0)) != null) placed++;
                }
                // a long weathered shelf under each headland stretch
                if (headland && rng.NextDouble() < 0.18)
                {
                    float yaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg + ((float)rng.NextDouble() - 0.5f) * 40f;   // long side roughly along the shore
                    Vector2 p3 = shore + dir * 3f;
                    if (Place("coastal_cliff_01", parent, new Vector3(p3.x, LookTerrainShape.SeaLevel - 5.0f - (float)rng.NextDouble() * 2f, p3.y), yaw, Mathf.Lerp(0.4f, 0.75f, (float)rng.NextDouble()), $"Coast_Shelf_{placed:000}", (float)rng.NextDouble() * 10f, yaw + 90f) != null) placed++;
                }
            }
            return placed;
        }

        // ------------------------------------------------------------------------------------------------------------
        // HILL JUNGLE (rest of the island, every tile): instanced, not GameObjects
        // ------------------------------------------------------------------------------------------------------------
        private static readonly string[] HillStems = { "jungle_tree", "palm_tree", "palm_tree_b", "tree_fern", "banana_plant", "pachira_aquatica_01", "shrub_02", "monstera_clump" };
        private const string HillDataPath = "Assets/Art/Terrain/HillJungleInstances.bytes";

        private static void WriteInstance(System.IO.BinaryWriter bw, int proto, Vector3 pos, Quaternion rot, float scale)
        {
            bw.Write(proto);
            bw.Write(pos.x); bw.Write(pos.y); bw.Write(pos.z);
            bw.Write(rot.x); bw.Write(rot.y); bw.Write(rot.z); bw.Write(rot.w);
            bw.Write(scale);
        }

        /// <summary>
        /// The hill jungle beyond the reachable area: a closed canopy of clumped trees (2-3 per dense 4.5 m cell) over a
        /// mid layer of tree ferns, bananas, pachira and shrubs. Nobody can reach it (LookTerrainBounds), so it needs no
        /// colliders and is drawn by Cretacea.Rendering.InstancedFoliageRenderer from a baked instance list (LOD meshes,
        /// impostors far away) instead of tens of thousands of GameObjects.
        /// </summary>
        private static int ScatterTerrainTrees(List<Terrain> tiles)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
            foreach (Terrain t in tiles)
            {
                TerrainData td = t.terrainData;
                td.treeInstances = new TreeInstance[0];
                td.treePrototypes = new TreePrototype[0];
                t.drawTreesAndFoliage = true;
                EditorUtility.SetDirty(td);
                Vector3 o = t.transform.position;
                minX = Mathf.Min(minX, o.x); minZ = Mathf.Min(minZ, o.z);
                maxX = Mathf.Max(maxX, o.x + td.size.x); maxZ = Mathf.Max(maxZ, o.z + td.size.z);
            }
            var protoIndex = new Dictionary<string, int>();
            var protoVariants = new Dictionary<string, int>();
            var prototypes = new List<Cretacea.Rendering.InstancedFoliageRenderer.Prototype>();
            foreach (string stem in HillStems)
            {
                bool tree = stem == "jungle_tree" || stem == "palm_tree" || stem == "palm_tree_b" || stem == "tree_fern";
                // trees: one prototype per dense impostor variant (hue/value/mirror), picked per instance by a position
                // hash (review 2 fix round: one bake per species stamped the far canopy in rows of identical crowns)
                int nv = tree ? LookVegetationImpostors.VariantCount : 1;
                int first = prototypes.Count;
                for (int v = 0; v < nv; v++)
                {
                    var lods = tree ? LookVegetationPrefabs.InstancedLods(stem, 0.35f, 1f, v) : LookVegetationPrefabs.InstancedLods(stem, 0.45f, 0.45f);
                    if (lods == null) break;
                    prototypes.Add(new Cretacea.Rendering.InstancedFoliageRenderer.Prototype { name = nv > 1 ? $"{stem}_V{v}" : stem, lods = lods });
                }
                if (prototypes.Count == first) continue;
                protoIndex[stem] = first;
                protoVariants[stem] = prototypes.Count - first;
            }
            int P(string stem) => protoIndex.TryGetValue(stem, out int i) ? i : -1;
            // variant by position hash (keeps the random streams, and with them every position, as they were)
            int PV(string stem, float x, float z)
            {
                int pi0 = P(stem);
                if (pi0 < 0) return -1;
                int nvar = protoVariants[stem];
                uint h = (uint)(Mathf.FloorToInt(x * 7.31f) * 73856093) ^ (uint)(Mathf.FloorToInt(z * 5.17f) * 19349663);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return pi0 + (int)(h % (uint)nvar);
            }

            var ms = new System.IO.MemoryStream();
            var bw = new System.IO.BinaryWriter(ms);
            int count = 0, canopy = 0;
            // --- canopy -------------------------------------------------------------------------------------------
            var rng = new System.Random(5150);
            const float cell = 4.5f;
            for (float cz = minZ; cz < maxZ; cz += cell)
            {
                for (float cx = minX; cx < maxX; cx += cell)
                {
                    double r0 = rng.NextDouble();
                    float mx = cx + cell * 0.5f, mz = cz + cell * 0.5f;
                    if (Z4.DistanceOutsidePlayArea(mx, mz) <= GoMargin - 2f) continue;
                    float my = GroundY(mx, mz);
                    if (my < -37.5f || my > 270f) continue;
                    float slopeC = SlopeDeg(mx, mz);
                    // (review 2: the old 36-45 deg falloff and the 44 deg cut left bald S-shaped strips on the dusk hill)
                    float density = ForestDensity(mx, mz, my) * (1f - SS(44f, 55f, slopeC) * 0.5f);
                    density = Mathf.Max(density, 0.85f * SS(-32f, 6f, my) * (1f - SS(44f, 55f, slopeC) * 0.8f));    // jungle closes over the hills
                    density *= 1f - 0.25f * SS(95f, 175f, my);      // slight thinning towards the summits (0.55 left speckled clearings)
                    float clump = SS(0.36f, 0.66f, Fbm(mx, mz, 0.017f, 21.5f, 3.7f));
                    float expected = density * (0.35f + 2.3f * clump);
                    int n = Mathf.FloorToInt(expected + (float)r0);
                    for (int k = 0; k < n; k++)
                    {
                        double r1 = rng.NextDouble(), r2 = rng.NextDouble(), r4 = rng.NextDouble(), r5 = rng.NextDouble(), r6 = rng.NextDouble();
                        float x = cx + (float)r1 * cell, z = cz + (float)r2 * cell;
                        if (Z4.DistanceOutsidePlayArea(x, z) <= GoMargin) continue;
                        float y = GroundY(x, z);
                        if (y < -37.5f) continue;
                        float slope = SlopeDeg(x, z);
                        if (slope > 52f) continue;
                        if (OnCameraLine(x, z, 2f)) continue;
                        // species by height: palms by the coast, jungle trees on the hills, tree ferns in between
                        float coast = SS(-18f, -37f, y);
                        string stem;
                        float sc;
                        if (r4 < coast * 0.8) { stem = r5 < 0.5 ? "palm_tree" : "palm_tree_b"; sc = Mathf.Lerp(0.85f, 1.4f, (float)r6); }
                        else if (r4 < 0.46) { stem = "jungle_tree"; sc = Mathf.Lerp(0.9f, 1.6f, (float)r6); }
                        // palms fill the band between the mid layer and the high crowns (hides the long trunks)
                        else if (r4 < 0.72) { stem = "palm_tree"; sc = Mathf.Lerp(0.85f, 1.35f, (float)r6); }
                        else if (r4 < 0.85) { stem = "palm_tree_b"; sc = Mathf.Lerp(0.85f, 1.35f, (float)r6); }
                        else { stem = "tree_fern"; sc = Mathf.Lerp(0.9f, 1.6f, (float)r6); }
                        int pi = PV(stem, x, z);
                        if (pi < 0) continue;
                        float sink = 0.2f + Mathf.Tan(slope * Mathf.Deg2Rad) * 1.2f;
                        Quaternion rot = Quaternion.AngleAxis((float)r5 * 5f, Quaternion.Euler(0f, (float)(r1 * 360.0), 0f) * Vector3.right) * Quaternion.Euler(0f, (float)(r6 * 360.0 + r2 * 97.0), 0f);
                        WriteInstance(bw, pi, new Vector3(x, y - sink, z), rot, sc);
                        count++; canopy++;
                    }
                }
            }
            // --- mid layer under the canopy (hides the bare trunks) and on steep faces up to 50 deg ----------------
            var rng2 = new System.Random(8123);
            const float cell2 = 3.2f;
            string[] mid = { "tree_fern", "banana_plant", "pachira_aquatica_01", "shrub_02", "monstera_clump" };
            float[] midW = { 0.30f, 0.22f, 0.16f, 0.20f, 0.12f };
            for (float cz = minZ; cz < maxZ; cz += cell2)
            {
                for (float cx = minX; cx < maxX; cx += cell2)
                {
                    double r1 = rng2.NextDouble(), r2 = rng2.NextDouble(), r3 = rng2.NextDouble(), r4 = rng2.NextDouble(), r5 = rng2.NextDouble();
                    float x = cx + (float)r1 * cell2, z = cz + (float)r2 * cell2;
                    if (Z4.DistanceOutsidePlayArea(x, z) <= GoMargin) continue;
                    float y = GroundY(x, z);
                    if (y < -36.5f || y > 240f) continue;
                    float slope = SlopeDeg(x, z);
                    if (slope > 58f) continue;
                    float density = Mathf.Max(ForestDensity(x, z, y), 0.8f * SS(-30f, 6f, y)) * (1f - 0.3f * SS(95f, 175f, y));
                    // steep faces (thin canopy there) get ferns and shrubs so they are not bald scars
                    float steep = SS(40f, 48f, slope) * (1f - SS(55f, 58f, slope));
                    float p = density * 0.42f + steep * 0.55f;
                    if (r3 > p) continue;
                    if (OnCameraLine(x, z, 1f)) continue;
                    double pick = r4; int k = 0;
                    for (; k < mid.Length - 1; k++) { pick -= midW[k]; if (pick < 0) break; }
                    if (steep > 0.5f) k = r4 < 0.55 ? 0 : 3;
                    int pi = PV(mid[k], x, z);
                    if (pi < 0) continue;
                    float sc = k == 0 ? Mathf.Lerp(0.7f, 1.3f, (float)r5) : (k == 2 ? Mathf.Lerp(1.6f, 2.8f, (float)r5) : Mathf.Lerp(1.0f, 1.9f, (float)r5));
                    float sink = 0.1f + Mathf.Tan(slope * Mathf.Deg2Rad) * 0.5f;
                    WriteInstance(bw, pi, new Vector3(x, y - sink, z), Quaternion.Euler(0f, (float)(r5 * 360.0 + r1 * 131.0), 0f), sc);
                    count++;
                }
            }
            bw.Flush();
            System.IO.File.WriteAllBytes(HillDataPath, ms.ToArray());
            AssetDatabase.ImportAsset(HillDataPath, ImportAssetOptions.ForceUpdate);
            var data = AssetDatabase.LoadAssetAtPath<TextAsset>(HillDataPath);

            var go = new GameObject("HillJungle_Instanced");
            go.transform.SetParent(s_root, false);
            go.layer = GameLayers.Environment;
            var r = go.AddComponent<Cretacea.Rendering.InstancedFoliageRenderer>();
            r.prototypes = prototypes.ToArray();
            r.instanceData = data;
            r.renderLayer = GameLayers.Environment;
            r.cellSize = 64f;
            Debug.Log($"[Zone4] hill jungle: {canopy} canopy trees + {count - canopy} mid-layer plants (instanced)");
            return count;
        }
    }
}
