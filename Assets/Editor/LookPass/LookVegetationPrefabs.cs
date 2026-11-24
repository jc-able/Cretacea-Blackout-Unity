using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Builds LODGroup prefabs (Assets/Prefabs/Foliage/PF_&lt;stem&gt;.prefab) from the plant and rock FBX files and
    /// their *_LOD1 / *_LOD2 siblings: LOD0 -> LOD1 -> LOD2 -> (generated far LOD for trees) -> culled.
    /// The far LOD is a vertex-clustered copy of the last authored LOD (~5-10 % of its triangles), so a hillside of
    /// thousands of trees stays inside the triangle budget. Pivot = base of the model; GPU instancing comes from the
    /// LookMaterials plant/rock materials (enableInstancing). Rebuilt in place on every world build (stable GUIDs).
    /// </summary>
    public static class LookVegetationPrefabs
    {
        public const string Folder = "Assets/Prefabs/Foliage";
        public const string MeshFolder = Folder + "/Meshes";

        public enum Kind { Tree, Plant, Small, Rock, Log }

        public sealed class Spec
        {
            public string Stem;
            public Kind Kind;
            public float[] Screen;      // screen-relative transition heights for LOD0.. (last = cull)
            public Vector3Int[] FarGrids;   // clustering grid of each generated far LOD (none = authored LODs only)
            public bool Impostor;           // last LOD = crossed-quad impostor rendered from LOD1 (LookVegetationImpostors)
            public bool FarLod => FarGrids != null && FarGrids.Length > 0;
            public Spec(string stem, Kind kind, float[] screen, params Vector3Int[] farGrids)
            { Stem = stem; Kind = kind; Screen = screen; FarGrids = farGrids; }
            public Spec WithImpostor() { Impostor = true; return this; }
        }

        // Transition heights: trees keep LOD0 only very close (the far LOD carries hillsides); small plants cull early.
        public static readonly Spec[] Specs =
        {
            // trees: LOD0 -> LOD1 -> LOD2 -> impostor (2 crossed quads) -> culled
            new Spec("jungle_tree", Kind.Tree, new[] { 0.70f, 0.36f, 0.10f, 0.0035f }).WithImpostor(),    // round 4: 0.05 was tried and kept the skeletal pale-trunked LOD2 twice as far (worse band from the air)
            new Spec("palm_tree", Kind.Tree, new[] { 0.62f, 0.32f, 0.10f, 0.004f }).WithImpostor(),
            new Spec("palm_tree_b", Kind.Tree, new[] { 0.62f, 0.32f, 0.10f, 0.004f }).WithImpostor(),
            new Spec("tree_fern", Kind.Tree, new[] { 0.55f, 0.26f, 0.09f, 0.007f }).WithImpostor(),
            new Spec("banana_plant", Kind.Plant, new[] { 0.45f, 0.12f, 0.02f }, new Vector3Int(4, 4, 4)),
            new Spec("monstera_clump", Kind.Plant, new[] { 0.50f, 0.035f }),
            new Spec("shrub_02", Kind.Plant, new[] { 0.50f, 0.035f }),
            new Spec("pachira_aquatica_01", Kind.Plant, new[] { 0.50f, 0.035f }),
            new Spec("fern_02", Kind.Small, new[] { 0.25f, 0.06f, 0.035f }),
            new Spec("anthurium_botany_01", Kind.Small, new[] { 0.25f, 0.07f, 0.04f }),
            new Spec("calathea_orbifolia_01", Kind.Small, new[] { 0.25f, 0.07f, 0.045f }),
            new Spec("grass_medium_02", Kind.Small, new[] { 0.20f, 0.05f, 0.035f }),
            new Spec("fallen_log", Kind.Log, new[] { 0.45f, 0.12f, 0.02f }, new Vector3Int(3, 3, 9)),
            new Spec("root_cluster_01", Kind.Log, new[] { 0.45f, 0.12f, 0.025f }, new Vector3Int(7, 3, 5)),
            new Spec("hanging_vines", Kind.Plant, new[] { 0.40f, 0.03f }),      // fix round: vines in the valley crowns
            new Spec("tree_stump_01", Kind.Log, new[] { 0.45f, 0.12f, 0.03f }, new Vector3Int(5, 3, 5)),
            new Spec("dead_tree_trunk", Kind.Log, new[] { 0.45f, 0.10f, 0.025f }, new Vector3Int(9, 3, 3)),
            new Spec("namaqualand_boulder_02", Kind.Rock, new[] { 0.50f, 0.18f, 0.05f, 0.016f }),
            new Spec("namaqualand_boulder_03", Kind.Rock, new[] { 0.50f, 0.18f, 0.05f, 0.014f }),
            new Spec("namaqualand_boulder_04", Kind.Rock, new[] { 0.50f, 0.18f, 0.05f, 0.014f }),
            new Spec("rock_face_01", Kind.Rock, new[] { 0.50f, 0.18f, 0.05f, 0.010f }),
            new Spec("rock_face_02", Kind.Rock, new[] { 0.50f, 0.18f, 0.05f, 0.014f }),
            new Spec("rock_moss_set_01", Kind.Rock, new[] { 0.55f, 0.03f }),
            new Spec("rock_moss_set_02", Kind.Rock, new[] { 0.55f, 0.035f }),
            new Spec("mountainside", Kind.Rock, new[] { 0.30f, 0.10f, 0.02f, 0.003f }),
            new Spec("coastal_cliff_01", Kind.Rock, new[] { 0.40f, 0.12f, 0.03f, 0.002f }),
        };

        private static readonly Dictionary<string, GameObject> Built = new Dictionary<string, GameObject>();

        public static GameObject Get(string stem) => Built.TryGetValue(stem, out GameObject go) ? go : null;

        public static string BuildAll()
        {
            Built.Clear();
            WorldMaterialPalette.EnsureFolder(Folder);
            WorldMaterialPalette.EnsureFolder(MeshFolder);
            int ok = 0;
            var missing = new List<string>();
            foreach (Spec s in Specs)
            {
                GameObject pf = BuildPrefab(s);
                if (pf != null) { Built[s.Stem] = pf; ok++; }
                else missing.Add(s.Stem);
            }
            return $"foliage prefabs {ok}/{Specs.Length}" + (missing.Count > 0 ? " missing: " + string.Join(",", missing) : "");
        }

        private static string FindModel(string fileStem)
        {
            foreach (string guid in AssetDatabase.FindAssets(fileStem + " t:Model", new[] { "Assets/Art/Foliage" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p) == fileStem) return p;
            }
            return null;
        }

        private static GameObject BuildPrefab(Spec s)
        {
            string lod0Path = FindModel(s.Stem);
            if (lod0Path == null) return null;
            var lodPaths = new List<string> { lod0Path };
            for (int i = 1; i <= 2; i++)
            {
                string p = FindModel($"{s.Stem}_LOD{i}");
                if (p != null) lodPaths.Add(p);
            }
            // boulder-style sets whose LOD files are not reduced are skipped by the spec list; keep what exists

            var root = new GameObject("PF_" + s.Stem);
            var lods = new List<LOD>();
            float minY = 0f;
            bool haveMinY = false;
            Renderer[] lastRenderers = null;
            int farCount = s.FarLod ? s.FarGrids.Length : 0;
            int authored = Mathf.Min(lodPaths.Count, s.Screen.Length - farCount - (s.Impostor ? 1 : 0));
            Renderer[] impostorSource = null;
            for (int i = 0; i < authored; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(lodPaths[i]);
                GameObject inst = Object.Instantiate(asset, root.transform, false);
                inst.name = $"{s.Stem}_LOD{i}";
                Renderer[] rs = inst.GetComponentsInChildren<Renderer>(true);
                if (!haveMinY)
                {
                    Bounds b = rs[0].bounds;
                    foreach (Renderer r in rs) b.Encapsulate(r.bounds);
                    minY = b.min.y;
                    haveMinY = true;
                }
                inst.transform.localPosition = new Vector3(0f, -minY, 0f);
                foreach (Renderer r in rs) { ConfigureRenderer(r, s.Kind, i); TintLeaves(r, s.Stem); if (s.Kind == Kind.Rock || s.Stem == "root_cluster_01") BasaltRock(r, s.Stem); }
                lods.Add(new LOD(s.Screen[i], rs));
                lastRenderers = rs;
                if (i == 1 || impostorSource == null) impostorSource = rs;
            }

            for (int f = 0; f < farCount && lastRenderers != null && lods.Count < s.Screen.Length; f++)
            {
                Renderer far = BuildFarLod(s, root.transform, lastRenderers, s.FarGrids[f], f == 0 ? "_FarLOD" : $"_FarLOD{f + 1}", lods.Count);
                if (far != null) lods.Add(new LOD(s.Screen[lods.Count], new[] { far }));
            }

            if (s.Impostor && impostorSource != null && lods.Count < s.Screen.Length)
            {
                Renderer imp = LookVegetationImpostors.Build(s.Stem, root.transform, impostorSource, MeshFolder, Folder);
                if (imp != null)
                {
                    // review 3: the impostor cast no ground shadow, so the switch read as a line where the forest died
                    Renderer shadow = LookVegetationImpostors.BuildShadowCrown(s.Stem, root.transform, imp, MeshFolder);
                    // round 5: and a soft ground patch, which shows at any range (the band is past the shadow distance)
                    Renderer blob = LookVegetationImpostors.BuildGroundBlob(s.Stem, root.transform, imp, MeshFolder);
                    var impRs = new List<Renderer> { imp };
                    if (shadow != null) impRs.Add(shadow);
                    if (blob != null) impRs.Add(blob);
                    lods.Add(new LOD(s.Screen[lods.Count], impRs.ToArray()));
                }
            }

            // transitions must decrease strictly
            for (int i = 0; i < lods.Count; i++)
            {
                LOD l = lods[i];
                l.screenRelativeTransitionHeight = s.Screen[Mathf.Min(i, s.Screen.Length - 1)];
                if (i == lods.Count - 1) l.screenRelativeTransitionHeight = s.Screen[s.Screen.Length - 1];
                lods[i] = l;
            }

            LODGroup group = root.AddComponent<LODGroup>();
            // no cross-fade: measured in Play (Substation) it took draw calls from 1,145 to 3,416, because the GPU
            // Resident Drawer stops instancing LODGroups that cross-fade
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(lods.ToArray());
            group.RecalculateBounds();

            root.layer = Cretacea.Core.GameLayers.Environment;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Cretacea.Core.GameLayers.Environment;

            string path = $"{Folder}/PF_{s.Stem}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return saved;
        }

        /// <summary>
        /// LOD levels of a built prefab for Cretacea.Rendering.InstancedFoliageRenderer: each level's renderers combined
        /// into one mesh in prefab space (one submesh per renderer submesh), its materials, and the distance (at scale 1,
        /// 70 deg vertical FOV) up to which the LODGroup would show it.
        /// </summary>
        /// <param name="meshLodScale">multiplies the distances of every level but the last (the hill jungle is never
        /// close to the player: switching to the impostor early keeps tens of thousands of trees in the triangle budget)</param>
        /// <param name="lastScale">multiplies the distance of the last level (its cull distance)</param>
        /// <param name="impostorVariant">0..LookVegetationImpostors.VariantCount-1: the last level uses that dense hill copy
        /// of the impostor (dark bark, hue/value variant) instead of the prefab's open-valley impostor; -1 keeps it</param>
        public static Cretacea.Rendering.InstancedFoliageRenderer.LodLevel[] InstancedLods(string stem, float meshLodScale = 1f, float lastScale = 1f, int impostorVariant = -1)
        {
            GameObject pf = Get(stem);
            if (pf == null) return null;
            LODGroup g = pf.GetComponent<LODGroup>();
            LOD[] lods = g.GetLODs();
            float size = g.size;
            float k = 2f * Mathf.Tan(35f * Mathf.Deg2Rad);
            var outLods = new Cretacea.Rendering.InstancedFoliageRenderer.LodLevel[lods.Length];
            for (int i = 0; i < lods.Length; i++)
            {
                var combine = new List<CombineInstance>();
                var mats = new List<Material>();
                bool shadows = false;
                foreach (Renderer r in lods[i].renderers)
                {
                    var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                    if (mf == null || mf.sharedMesh == null) continue;
                    // the impostor's shadow crown is for the play-area trees only (tens of thousands of hill trees
                    // casting it would cost a shadow pass the far jungle does not need)
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                    // nor the ground patch (round 5): under a closed hill canopy it is never seen
                    if (r.name.EndsWith("_ImpostorBlob")) continue;
                    Matrix4x4 toRoot = pf.transform.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    Material[] sm = r.sharedMaterials;
                    for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++)
                    {
                        combine.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = toRoot });
                        mats.Add(sub < sm.Length ? sm[sub] : sm[sm.Length - 1]);
                    }
                    shadows |= r.shadowCastingMode != ShadowCastingMode.Off;
                }
                string path = $"{MeshFolder}/{stem}_Inst_LOD{i}.asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool isNew = mesh == null;
                if (isNew) mesh = new Mesh { name = $"{stem}_Inst_LOD{i}" };
                else mesh.Clear();
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(combine.ToArray(), false, true, false);
                mesh.RecalculateBounds();
                if (isNew) AssetDatabase.CreateAsset(mesh, path);
                else EditorUtility.SetDirty(mesh);
                if (impostorVariant >= 0 && i == lods.Length - 1)
                {
                    Material dense = LookVegetationImpostors.DenseVariant(Folder, stem, impostorVariant);
                    if (dense != null)
                        for (int mi = 0; mi < mats.Count; mi++)
                            if (mats[mi] != null && mats[mi].name.EndsWith("_Impostor")) mats[mi] = dense;
                }
                // round 5: the impostor level also gets a capless copy for the cells seen from eye level
                Mesh capless = null;
                Mesh cards = i == lods.Length - 1 ? LookVegetationImpostors.CardsMesh(MeshFolder, stem) : null;
                if (cards != null)
                {
                    var cc = new List<CombineInstance>();
                    foreach (CombineInstance ci in combine)
                        cc.Add(ci.mesh != null && ci.mesh.name == stem + "_Impostor" || (ci.mesh != null && AssetDatabase.GetAssetPath(ci.mesh).EndsWith($"/{stem}_Impostor.asset"))
                            ? new CombineInstance { mesh = cards, subMeshIndex = 0, transform = ci.transform } : ci);
                    string cpath = $"{MeshFolder}/{stem}_Inst_LOD{i}_Cards.asset";
                    capless = AssetDatabase.LoadAssetAtPath<Mesh>(cpath);
                    bool cNew = capless == null;
                    if (cNew) capless = new Mesh { name = $"{stem}_Inst_LOD{i}_Cards" };
                    else capless.Clear();
                    capless.indexFormat = IndexFormat.UInt32;
                    capless.CombineMeshes(cc.ToArray(), false, true, false);
                    capless.RecalculateBounds();
                    if (cNew) AssetDatabase.CreateAsset(capless, cpath);
                    else EditorUtility.SetDirty(capless);
                }
                outLods[i] = new Cretacea.Rendering.InstancedFoliageRenderer.LodLevel
                {
                    capless = capless,
                    mesh = mesh,
                    materials = mats.ToArray(),
                    maxDistance = size / (k * Mathf.Max(1e-4f, lods[i].screenRelativeTransitionHeight)) * (i == lods.Length - 1 ? lastScale : meshLodScale),
                    castShadows = shadows,
                };
            }
            return outLods;
        }

        // ------------------------------------------------------------------------------------------------------------
        // LEAF GREEN (review 2 daylight: every plant measured hue 55-60 deg, red = green, so the island read khaki)
        // ------------------------------------------------------------------------------------------------------------
        public const string LeafMaterialFolder = Folder + "/Materials";

        /// <summary>
        /// sRGB tint of the leaf (alpha-tested) materials per species: the atlases are yellow-olive, the tint pulls them
        /// to a tropical green (hue ~80-100 deg). A little variety per species: palms stay a touch yellower, the jungle
        /// tree and the tree fern deeper and bluer.
        /// </summary>
        public static Color LeafTint(string stem)
        {
            switch (stem)
            {
                // (r5 fix round, critic: valley crowns G/R 1.11 at hue 72 from the air, dawn trees brown at G/R 0.9:
                // the low warm sun adds the red, so the four tree species lose about 15 % of their leaf red)
                case "jungle_tree": return new Color(0.60f, 1.0f, 0.80f);
                case "tree_fern": return new Color(0.60f, 1.0f, 0.84f);
                case "palm_tree": return new Color(0.66f, 1.0f, 0.80f);
                case "palm_tree_b": return new Color(0.64f, 1.0f, 0.80f);
                case "banana_plant": return new Color(0.80f, 1.0f, 0.80f);
                case "grass_medium_02": return new Color(0.74f, 1.0f, 0.78f);
                default: return new Color(0.76f, 1.0f, 0.82f);
            }
        }

        private static bool IsLeafMaterial(Material m) =>
            m != null && m.HasProperty("_AlphaCutoffEnable") && m.GetFloat("_AlphaCutoffEnable") > 0.5f;

        /// <summary>
        /// Green copy of a leaf material (Assets/Prefabs/Foliage/Materials/MAT_&lt;name&gt;_Leaf.mat), refreshed from the
        /// LookMaterials original on every build. Bark and solid materials are left as they are.
        /// </summary>
        public static Material GreenLeafMaterial(Material src, string stem)
        {
            if (src == null || src.name.EndsWith("_Leaf")) return src;
            if (!IsLeafMaterial(src)) return GreenAtlasMaterial(src, stem);
            WorldMaterialPalette.EnsureFolder(LeafMaterialFolder);
            string path = $"{LeafMaterialFolder}/{src.name}_{stem}_Leaf.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src) { name = $"{src.name}_{stem}_Leaf" }; AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            Color baseTint = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
            Color t = LeafTint(stem);
            m.SetColor("_BaseColor", new Color(baseTint.r * t.r, baseTint.g * t.g, baseTint.b * t.b, baseTint.a));
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        public const string LeafTextureFolder = Folder + "/Textures";

        /// <summary>
        /// Solid plants (bark and leaves in one atlas, no alpha): a copy of the material bound to the leaf-green atlas
        /// written by Assets/Art/Terrain/Source~/green_leaf_atlases.py (only the leaf texels are shifted, so the bark
        /// keeps its colour). Without that atlas the material is left as it is.
        /// </summary>
        private static Material GreenAtlasMaterial(Material src, string stem)
        {
            Texture baseTex = src.HasProperty("_BaseColorMap") ? src.GetTexture("_BaseColorMap") : null;
            if (baseTex == null) return src;
            string texPath = $"{LeafTextureFolder}/{baseTex.name}_Leaf.png";
            if (!File.Exists(texPath)) return src;
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            var srcTi = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(baseTex)) as TextureImporter;
            if (ti != null && srcTi != null && (ti.maxTextureSize != srcTi.maxTextureSize || !ti.sRGBTexture || ti.mipmapEnabled != srcTi.mipmapEnabled))
            {
                ti.sRGBTexture = true;
                ti.mipmapEnabled = srcTi.mipmapEnabled;
                ti.maxTextureSize = srcTi.maxTextureSize;
                ti.textureCompression = srcTi.textureCompression;
                ti.SaveAndReimport();
            }
            var leafTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (leafTex == null) return src;
            WorldMaterialPalette.EnsureFolder(LeafMaterialFolder);
            string path = $"{LeafMaterialFolder}/{src.name}_Leaf.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(src) { name = $"{src.name}_Leaf" }; AssetDatabase.CreateAsset(m, path); }
            else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
            m.SetTexture("_BaseColorMap", leafTex);
            // r5 fix round: the solid tree atlases (bark and leaves in one texture, mesh LODs 0-2 and the impostor
            // bake; the palms and the tree fern are atlas plants too) get the same red cut as the alpha-leaf species (it is the brown crown of the dawn meadow and the
            // valley band); on the bark it reads as a slightly cooler, darker trunk
            if ((stem == "jungle_tree" || stem == "palm_tree" || stem == "palm_tree_b" || stem == "tree_fern") && src.HasProperty("_BaseColor"))
            {
                Color bc = src.GetColor("_BaseColor");
                m.SetColor("_BaseColor", new Color(bc.r * 0.85f, bc.g, bc.b * 0.94f, bc.a));
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Round 4 (review 3, intro_02/intro_03 and GoldenDay_aerial_sw: saturated orange slab rocks on the pond bank
        /// and bald orange headland shelves on a volcanic jungle island): the desert sandstone sets (namaqualand
        /// boulders, rock faces) get a copy of their material tinted to a cool dark basalt grey.
        /// </summary>
        private static void BasaltRock(Renderer r, string stem)
        {
            // round 5 (r4 critic: the orange slab on the pond bank and the bald orange headland shelves): the
            // mountainside rock too, and the root cluster as dark wet bark
            bool roots = stem == "root_cluster_01";
            if (!stem.StartsWith("namaqualand") && !stem.StartsWith("rock_face") && stem != "mountainside" && !roots) return;
            Material[] sm = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < sm.Length; i++)
            {
                Material src = sm[i];
                if (src == null || src.name.EndsWith("_Basalt")) continue;
                WorldMaterialPalette.EnsureFolder(LeafMaterialFolder);
                string path = $"{LeafMaterialFolder}/{src.name}_Basalt.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(src) { name = $"{src.name}_Basalt" }; AssetDatabase.CreateAsset(m, path); }
                else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); }
                Color bc = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                // orange sandstone ~0.53/0.45/0.31 -> about 0.27/0.28/0.28: dark, slightly cool volcanic grey
                m.SetColor("_BaseColor", roots ? new Color(bc.r * 0.60f, bc.g * 0.55f, bc.b * 0.50f, bc.a) : new Color(bc.r * 0.52f, bc.g * 0.62f, bc.b * 0.90f, bc.a));
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                sm[i] = m; changed = true;
            }
            if (changed) r.sharedMaterials = sm;
        }

        private static void TintLeaves(Renderer r, string stem)
        {
            Material[] sm = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < sm.Length; i++)
            {
                Material g = GreenLeafMaterial(sm[i], stem);
                if (g != sm[i]) { sm[i] = g; changed = true; }
            }
            if (changed) r.sharedMaterials = sm;
        }

        private static void ConfigureRenderer(Renderer r, Kind kind, int lod)
        {
            ShadowCastingMode shadows;
            switch (kind)
            {
                case Kind.Tree: shadows = lod <= 2 ? ShadowCastingMode.On : ShadowCastingMode.Off; break;
                case Kind.Plant: shadows = lod == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off; break;
                case Kind.Small: shadows = ShadowCastingMode.Off; break;
                default: shadows = lod <= 1 ? ShadowCastingMode.On : ShadowCastingMode.Off; break;
            }
            r.shadowCastingMode = shadows;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            r.allowOcclusionWhenDynamic = true;
            r.staticShadowCaster = true;    // what the Look Pass sets on every static World renderer: no per-instance override
            if (r is MeshRenderer mr) mr.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
        }

        // ------------------------------------------------------------------------------------------------------------
        // FAR LOD: vertex clustering of the last authored LOD
        // ------------------------------------------------------------------------------------------------------------
        private static Renderer BuildFarLod(Spec s, Transform root, Renderer[] source, Vector3Int grid, string suffix, int lodIndex)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Material mat = null;
            foreach (Renderer r in source)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Mesh m = mf.sharedMesh;
                if (mat == null) mat = r.sharedMaterial;
                Matrix4x4 toRoot = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                int baseIndex = verts.Count;
                Vector3[] v = m.vertices; Vector3[] n = m.normals; Vector2[] uv = m.uv;
                for (int i = 0; i < v.Length; i++)
                {
                    verts.Add(toRoot.MultiplyPoint3x4(v[i]));
                    norms.Add(n.Length == v.Length ? toRoot.MultiplyVector(n[i]).normalized : Vector3.up);
                    uvs.Add(uv.Length == v.Length ? uv[i] : Vector2.zero);
                }
                int[] t = m.triangles;
                for (int i = 0; i < t.Length; i++) tris.Add(baseIndex + t[i]);
            }
            if (verts.Count == 0) return null;

            var bounds = new Bounds(verts[0], Vector3.zero);
            foreach (Vector3 p in verts) bounds.Encapsulate(p);
            Vector3 cell = new Vector3(bounds.size.x / grid.x, bounds.size.y / grid.y, bounds.size.z / grid.z);
            cell = Vector3.Max(cell, Vector3.one * 0.05f);

            // cluster key: spatial cell + a coarse UV cell (keeps unrelated atlas islands apart)
            var keyToCluster = new Dictionary<long, int>();
            var remap = new int[verts.Count];
            var sumP = new List<Vector3>(); var sumN = new List<Vector3>(); var count = new List<int>(); var repUv = new List<Vector2>();
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i] - bounds.min;
                int cx = Mathf.Clamp((int)(p.x / cell.x), 0, 255), cy = Mathf.Clamp((int)(p.y / cell.y), 0, 255), cz = Mathf.Clamp((int)(p.z / cell.z), 0, 255);
                int ux = Mathf.Clamp((int)(Mathf.Repeat(uvs[i].x, 1f) * 4f), 0, 3), uy = Mathf.Clamp((int)(Mathf.Repeat(uvs[i].y, 1f) * 4f), 0, 3);
                long key = (long)cx | ((long)cy << 8) | ((long)cz << 16) | ((long)ux << 24) | ((long)uy << 26);
                if (!keyToCluster.TryGetValue(key, out int c))
                {
                    c = sumP.Count; keyToCluster[key] = c;
                    sumP.Add(Vector3.zero); sumN.Add(Vector3.zero); count.Add(0); repUv.Add(uvs[i]);
                }
                sumP[c] += verts[i]; sumN[c] += norms[i]; count[c]++;
                remap[i] = c;
            }
            var outV = new Vector3[sumP.Count]; var outN = new Vector3[sumP.Count];
            for (int c = 0; c < sumP.Count; c++)
            {
                outV[c] = sumP[c] / count[c];
                outN[c] = sumN[c].sqrMagnitude > 1e-8f ? sumN[c].normalized : Vector3.up;
            }
            var outT = new List<int>();
            var seen = new HashSet<long>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                int a = remap[tris[i]], b = remap[tris[i + 1]], c = remap[tris[i + 2]];
                if (a == b || b == c || a == c) continue;
                // de-duplicate (order independent)
                int lo = Mathf.Min(a, Mathf.Min(b, c)), hi = Mathf.Max(a, Mathf.Max(b, c)), mid = a + b + c - lo - hi;
                long k = (long)lo | ((long)mid << 21) | ((long)hi << 42);
                if (!seen.Add(k)) continue;
                outT.Add(a); outT.Add(b); outT.Add(c);
            }

            string meshPath = $"{MeshFolder}/{s.Stem}{suffix}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            bool isNew = mesh == null;
            if (isNew) mesh = new Mesh { name = s.Stem + suffix };
            else mesh.Clear();
            mesh.vertices = outV;
            mesh.normals = outN;
            mesh.uv = repUv.ToArray();
            mesh.triangles = outT.ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            if (isNew) AssetDatabase.CreateAsset(mesh, meshPath);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject(s.Stem + suffix);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            ConfigureRenderer(mr, s.Kind, Mathf.Max(3, lodIndex));
            return mr;
        }
    }
}
