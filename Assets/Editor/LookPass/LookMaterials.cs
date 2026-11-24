using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Editor.WorldBuilder;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Material side of the Look Pass.
    ///  1. Rebuilds the world palette (exact texture sets, world-space mapping, gloss ranges).
    ///  2. Gives every imported model (props, vehicles, rocks, plants, the fossil) a real material: the FBX importer
    ///     only bound base colour + normal, so nothing had roughness or metal. Plants also get alpha clipping,
    ///     two-sided leaves and light transmission.
    ///  3. Swaps moving parts (door leaves) to UV-mapped copies of the world-mapped materials so textures do not
    ///     swim through a door as it swings.
    ///  4. Fixes the first-person arm (its tint made the glove albedo ~0.006, so only grey specular showed).
    /// </summary>
    public static class LookMaterials
    {
        public const string ModelMaterialsFolder = "Assets/Art/Materials/Models";
        public const string MovingFolder = "Assets/Art/Materials/World/Moving";
        private const string FoliageProfilePath = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/FoliageDiffusionProfile.asset";

        private static readonly string[] ModelRoots =
        {
            "Assets/Art/Props",
            "Assets/Art/Foliage",
            "Assets/Art/Dinosaurs/trex_skeleton",
        };

        private enum Kind { Prop, Rock, PlantCards, PlantSolid, Fossil }

        public static string Apply()
        {
            var palette = new WorldMaterialPalette();     // rewrites Assets/Art/Materials/World/*.mat in place
            int models = RemapModelMaterials(out int materials);
            int scene = SwapSceneModelMaterials();
            int moving = AssignMovingVariants();
            string arm = FixViewmodelArm();
            AssetDatabase.SaveAssets();
            return $"palette rebuilt; model materials={materials} on {models} models; scene renderers re-pointed={scene}; moving variants={moving}; {arm}";
        }

        // ------------------------------------------------------------------------------------------------------------
        // MODELS
        // ------------------------------------------------------------------------------------------------------------
        private static Kind KindFor(string path, bool baseHasAlpha)
        {
            string p = path.Replace('\\', '/');
            if (p.Contains("/trex_skeleton/")) return Kind.Fossil;
            if (p.Contains("/Foliage/Rocks/")) return Kind.Rock;
            if (p.Contains("/Foliage/"))
            {
                if (p.Contains("dead_tree_trunk") || p.Contains("root_cluster") || p.Contains("tree_stump") || p.Contains("fallen_log")) return Kind.Rock;
                return baseHasAlpha ? Kind.PlantCards : Kind.PlantSolid;
            }
            return Kind.Prop;
        }

        private static int RemapModelMaterials(out int materialCount)
        {
            materialCount = 0;
            int modelCount = 0;
            LookAtmosphereProfiles.EnsureFolder(ModelMaterialsFolder);
            var foliageProfile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(FoliageProfilePath);
            var built = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

            string[] guids = AssetDatabase.FindAssets("t:Model", ModelRoots);
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                    if (path.Contains("/Flashlight/")) continue;                 // hand-authored external materials
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null) continue;

                    string dir = Path.GetDirectoryName(path).Replace('\\', '/');
                    string texDir = dir + "/Textures";
                    bool changed = false;
                    var existing = importer.GetExternalObjectMap();
                    // embedded materials still inside the FBX (a model that was remapped earlier has none left)
                    foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().ToArray())
                    {
                        if (embedded == null || string.IsNullOrEmpty(embedded.name)) continue;
                        var src = new AssetImporter.SourceAssetIdentifier(embedded);
                        Material mat = BuildModelMaterial(path, texDir, src.name, foliageProfile, built);
                        if (mat == null) continue;
                        materialCount++;
                        if (!existing.TryGetValue(src, out UnityEngine.Object mapped) || mapped != mat)
                        {
                            importer.AddRemap(src, mat);
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        EditorUtility.SetDirty(importer);
                        importer.SaveAndReimport();
                        modelCount++;
                    }
                }
            }
            return modelCount;
        }

        /// <summary>Texture stem for a source material: "M_crate" -> crate, "Velociraptor_Mat" -> Velociraptor.</summary>
        private static string StemFor(string materialName)
        {
            string s = materialName;
            if (s.StartsWith("M_", StringComparison.Ordinal)) s = s.Substring(2);
            if (s.EndsWith("_Mat", StringComparison.Ordinal)) s = s.Substring(0, s.Length - 4);
            return s;
        }

        private static string FirstExisting(params string[] paths)
        {
            foreach (string p in paths) if (File.Exists(p)) return p;
            return null;
        }

        private static Material BuildModelMaterial(string modelPath, string texDir, string sourceName, DiffusionProfileSettings foliageProfile, Dictionary<string, Material> built)
        {
            string stem = StemFor(sourceName);
            string key = texDir + "|" + stem;
            if (built.TryGetValue(key, out Material done)) return done;

            string basePath = FirstExisting($"{texDir}/{stem}_BaseColor.png", $"{texDir}/{stem}_Albedo.png");
            string normalPath = FirstExisting($"{texDir}/{stem}_Normal.png");
            string maskPath = FirstExisting($"{texDir}/{stem}_MaskMap.png");
            if (basePath == null)
            {
                // secondary slots ("pachira_aquatica_01_m2") reuse the model's main set; untextured slots keep the FBX material
                string modelStem = Path.GetFileNameWithoutExtension(modelPath);
                int lod = modelStem.IndexOf("_LOD", StringComparison.Ordinal);
                if (lod > 0) modelStem = modelStem.Substring(0, lod);
                basePath = FirstExisting($"{texDir}/{modelStem}_BaseColor.png", $"{texDir}/{modelStem}_Albedo.png");
                if (basePath == null || !stem.StartsWith(modelStem, StringComparison.OrdinalIgnoreCase)) { built[key] = null; return null; }
                normalPath = FirstExisting($"{texDir}/{modelStem}_Normal.png");
                maskPath = FirstExisting($"{texDir}/{modelStem}_MaskMap.png");
            }

            var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            bool hasAlpha = baseTex != null && GraphicsFormatHasAlpha(basePath);
            Kind kind = KindFor(modelPath, hasAlpha);

            string sub = kind == Kind.Prop ? "Props" : (kind == Kind.Rock ? "Rocks" : (kind == Kind.Fossil ? "Props" : "Plants"));
            string folder = $"{ModelMaterialsFolder}/{sub}";
            LookAtmosphereProfiles.EnsureFolder(folder);
            string matPath = $"{folder}/MAT_{stem}.mat";

            Material mat = CretaceaPbrImporter.CreateOrUpdateHdrpLitMaterial(matPath, basePath, maskPath, normalPath, null, 0f, 0.4f, 1.0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_AORemapMin", 0.2f);
            mat.SetFloat("_AORemapMax", 1f);

            switch (kind)
            {
                case Kind.Prop:
                    mat.SetFloat("_SmoothnessRemapMin", 0.05f);
                    mat.SetFloat("_SmoothnessRemapMax", 0.92f);
                    mat.SetFloat("_MetallicRemapMin", 0f);
                    mat.SetFloat("_MetallicRemapMax", 1f);
                    break;
                case Kind.Fossil:
                    mat.SetFloat("_SmoothnessRemapMin", 0.05f);
                    mat.SetFloat("_SmoothnessRemapMax", 0.38f);
                    mat.SetFloat("_MetallicRemapMax", 0f);
                    mat.SetColor("_BaseColor", new Color(1.0f, 0.97f, 0.92f));
                    break;
                case Kind.Rock:
                    mat.SetFloat("_SmoothnessRemapMin", 0.05f);
                    mat.SetFloat("_SmoothnessRemapMax", 0.72f);     // rain-wet rock and bark
                    mat.SetFloat("_MetallicRemapMax", 0f);
                    mat.SetColor("_BaseColor", new Color(0.86f, 0.86f, 0.84f));
                    break;
                case Kind.PlantCards:
                case Kind.PlantSolid:
                    mat.SetFloat("_SmoothnessRemapMin", 0.10f);
                    mat.SetFloat("_SmoothnessRemapMax", 0.36f);     // waxy leaves; at 0.62 wet ground cover sparkled like frost under the moon
                    mat.SetFloat("_MetallicRemapMax", 0f);
                    mat.SetFloat("_DoubleSidedEnable", 1f);
                    mat.SetFloat("_DoubleSidedNormalMode", 0f);     // flip
                    if (kind == Kind.PlantCards)
                    {
                        HDMaterial.SetAlphaClipping(mat, true);
                        HDMaterial.SetAlphaCutoff(mat, 0.42f);
                        mat.SetFloat("_AlphaCutoffShadow", 0.42f);
                        mat.SetFloat("_UseShadowThreshold", 1f);
                    }
                    // Leaves let light through, cards or modelled fronds alike. Round 4: the solid palm crowns had no
                    // transmission and crushed to black against a bright sky (GoldenDay_plaza_eye).
                    if (foliageProfile != null)
                    {
                        HDMaterial.SetMaterialType(mat, MaterialId.LitTranslucent);
                        HDMaterial.SetDiffusionProfile(mat, foliageProfile);
                        mat.SetFloat("_Thickness", kind == Kind.PlantCards ? 0.35f : 0.5f);
                    }
                    break;
            }

            mat.enableInstancing = true;
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            built[key] = mat;
            return mat;
        }

        private static bool GraphicsFormatHasAlpha(string texturePath)
        {
            var ti = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            return ti != null && ti.DoesSourceTextureHaveAlpha();
        }

        /// <summary>
        /// Scene instances built before the remap still point at the FBX-embedded materials. Re-point them by name
        /// (M_crate -> MAT_crate) so the open scene matches what a fresh world build produces.
        /// </summary>
        private static int SwapSceneModelMaterials()
        {
            var lookup = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ModelMaterialsFolder }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m != null && m.name.StartsWith("MAT_", StringComparison.Ordinal)) lookup[m.name.Substring(4)] = m;
            }

            int swapped = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                Material[] mats = r.sharedMaterials;
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null) continue;
                    string path = AssetDatabase.GetAssetPath(m);
                    if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;    // only FBX-embedded materials
                    if (lookup.TryGetValue(StemFor(m.name), out Material replacement) && replacement != m)
                    {
                        mats[i] = replacement;
                        any = true;
                    }
                }
                if (any)
                {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                    swapped++;
                }
            }
            return swapped;
        }

        // ------------------------------------------------------------------------------------------------------------
        // MOVING PARTS
        // ------------------------------------------------------------------------------------------------------------
        private static int AssignMovingVariants()
        {
            GameObject world = GameObject.Find("World");
            if (world == null) return 0;
            LookAtmosphereProfiles.EnsureFolder(MovingFolder);
            var cache = new Dictionary<Material, Material>();
            int count = 0;
            foreach (MeshRenderer mr in world.GetComponentsInChildren<MeshRenderer>(true))
            {
                bool moves = mr.GetComponentInParent<Animator>() != null
                             || mr.GetComponentInParent<Rigidbody>() != null
                             || mr.gameObject.layer == Cretacea.Core.GameLayers.Door;
                if (!moves) continue;
                Material[] mats = mr.sharedMaterials;
                bool any = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !m.HasProperty("_UVBase")) continue;
                    if (m.GetFloat("_UVBase") < 4f)
                    {
                        // already a UV0 copy from an earlier run: refresh it from its world-mapped source
                        if (!m.name.EndsWith("_UV0", StringComparison.Ordinal)) continue;
                        var source = AssetDatabase.LoadAssetAtPath<Material>($"{WorldMaterialPalette.MaterialsOutputFolder}/{m.name.Substring(0, m.name.Length - 4)}.mat");
                        if (source == null) continue;
                        m = source;
                    }
                    if (!cache.TryGetValue(m, out Material variant))
                    {
                        string path = $"{MovingFolder}/{m.name}_UV0.mat";
                        variant = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (variant == null)
                        {
                            variant = new Material(m) { name = m.name + "_UV0" };
                            AssetDatabase.CreateAsset(variant, path);
                        }
                        else
                        {
                            variant.CopyPropertiesFromMaterial(m);
                        }
                        variant.SetFloat("_UVBase", 0f);
                        variant.SetVector("_UVMappingMask", new Vector4(1f, 0f, 0f, 0f));
                        variant.SetFloat("_InvTilingScale", 1f);
                        HDMaterial.ValidateMaterial(variant);
                        EditorUtility.SetDirty(variant);
                        cache[m] = variant;
                    }
                    mats[i] = variant;
                    any = true;
                }
                if (any)
                {
                    mr.sharedMaterials = mats;
                    EditorUtility.SetDirty(mr);
                    count++;
                }
            }
            return count;
        }

        // ------------------------------------------------------------------------------------------------------------
        // FIRST-PERSON ARM
        // ------------------------------------------------------------------------------------------------------------
        private static string FixViewmodelArm()
        {
            const string armPath = "Assets/Art/Materials/Props/Flashlight/M_FPS_GlovedArm.mat";
            const string sleevePath = "Assets/Art/Materials/Props/Flashlight/M_FPS_JacketSleeveExt.mat";
            var arm = AssetDatabase.LoadAssetAtPath<Material>(armPath);
            var sleeve = AssetDatabase.LoadAssetAtPath<Material>(sleevePath);
            if (arm != null)
            {
                // the texture is already a black glove; the old 0.15 tint left ~0.6 % albedo and a grey plastic look
                arm.SetColor("_BaseColor", new Color(0.27f, 0.27f, 0.29f, 1f));   // dark tactical glove: no glow in ambient light, but not a black fist either
                arm.SetFloat("_SmoothnessRemapMin", 0.06f);
                arm.SetFloat("_SmoothnessRemapMax", 0.30f);
                arm.SetFloat("_MetallicRemapMax", 0f);
                HDMaterial.ValidateMaterial(arm);
                EditorUtility.SetDirty(arm);
            }
            if (sleeve != null)
            {
                sleeve.SetColor("_BaseColor", new Color(0.055f, 0.065f, 0.085f, 1f));
                sleeve.SetFloat("_Smoothness", 0.28f);
                sleeve.SetFloat("_Metallic", 0f);
                HDMaterial.ValidateMaterial(sleeve);
                EditorUtility.SetDirty(sleeve);
            }
            // Nothing in the hand takes decals: the hazard stripes painted across the E-0 threshold were projected onto
            // the glove and the torch (review 3).
            int noDecals = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Art/Materials/Props/Flashlight" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null || !m.HasProperty("_SupportDecals")) continue;
                if (m.GetFloat("_SupportDecals") != 0f)
                {
                    m.SetFloat("_SupportDecals", 0f);
                    HDMaterial.ValidateMaterial(m);
                    EditorUtility.SetDirty(m);
                }
                noDecals++;
            }
            Material[] hand = EnsureViewmodelHandMaterials();
            return "arm materials " + (arm != null ? "ok" : "missing") + $", hand+torch {hand.Length} materials, {noDecals} hand materials without decals";
        }

        // ------------------------------------------------------------------------------------------------------------
        // FIRST-PERSON HAND AND TORCH (round 5: the scanned gloved hand round a tactical torch)
        // ------------------------------------------------------------------------------------------------------------
        public const string ViewmodelHandFolder = "Assets/Art/Props/Flashlight/ViewmodelHand";
        public const string ViewmodelHandModel = ViewmodelHandFolder + "/flashlight_hand_vm.fbx";
        /// <summary>The same torch without the hand, for the security-office desk pickup (submeshes: body, glass, LED, rubber).</summary>
        public const string ViewmodelPickupModel = ViewmodelHandFolder + "/flashlight_pickup_vm.fbx";
        private const string HandMatFolder = "Assets/Art/Materials/Props/Flashlight";

        /// <summary>
        /// Creates (or re-tunes) the six materials of the held hand-and-torch model, in its submesh order:
        /// leather glove, anodised aluminium head, lens glass, LED, jacket sleeve, matte rubber grip and tail cap.
        /// One texture atlas (base colour, normal, an HDRP mask map converted from the scan's ORM) is shared by glove
        /// and torch; each gets its own gloss and metal range. Nothing here takes decals.
        /// </summary>
        public static Material[] EnsureViewmodelHandMaterials()
        {
            var baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(ViewmodelHandFolder + "/T_VMHand_BaseColor.jpg");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(ViewmodelHandFolder + "/T_VMHand_Normal.jpg");
            var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(ViewmodelHandFolder + "/T_VMHand_Mask.png");
            var clothN = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Textures/carpet_office/carpet_office_Normal.png");

            // leather: the scan is worn black leather with its creases and seams painted in; kept dark (albedo ~0.025), the sheen does the work
            // (round 5 fix: a softer normal and a narrow, low gloss range; the strong normal and 0.5 gloss read as foil)
            Material glove = HandMat("M_VM_Glove", baseMap, normal, mask, new Color(0.70f, 0.69f, 0.68f, 1f), 0.85f,
                smoothMin: 0.12f, smoothMax: 0.36f, metalMax: 0f, aoMin: 0.2f);
            // black anodised aluminium head: dark, satin (a tint above 1 and 0.74 gloss read as polished chrome)
            Material body = HandMat("M_VM_TorchBody", baseMap, normal, mask, new Color(0.45f, 0.45f, 0.47f, 1f), 1.0f,
                smoothMin: 0.22f, smoothMax: 0.55f, metalMax: 0.85f);
            // matte black rubber grip and knurled tail cap: no metal, low gloss
            Material rubber = HandMat("M_VM_Rubber", baseMap, normal, mask, new Color(0.35f, 0.35f, 0.35f, 1f), 1.0f,
                smoothMin: 0.12f, smoothMax: 0.28f, metalMax: 0f);
            Material glass = HandMat("M_VM_LensGlass", null, null, null, new Color(0.035f, 0.038f, 0.045f, 1f), 1f,
                smoothMin: 0.97f, smoothMax: 0.97f, metalMax: 0f);
            Material led = HandMat("M_VM_LED", null, null, null, new Color(0.9f, 0.92f, 0.95f, 1f), 1f,
                smoothMin: 0.8f, smoothMax: 0.8f, metalMax: 0f);
            led.SetColor("_EmissiveColor", new Color(1f, 0.97f, 0.92f) * 60f);   // nits; the controller dims it with the beam
            led.SetColor("_EmissiveColorLDR", new Color(1f, 0.97f, 0.92f));
            led.SetFloat("_EmissiveIntensity", 60f);
            led.SetFloat("_UseEmissiveIntensity", 0f);
            HDMaterial.ValidateMaterial(led);
            // the guard's navy nylon-cotton jacket sleeve: a woven normal at a fine scale, low gloss
            Material sleeve = HandMat("M_VM_Sleeve", null, clothN, null, new Color(0.075f, 0.088f, 0.115f, 1f), 0.55f,
                smoothMin: 0.18f, smoothMax: 0.18f, metalMax: 0f);
            sleeve.SetTextureScale("_BaseColorMap", new Vector2(3f, 3f));
            HDMaterial.ValidateMaterial(sleeve);
            EditorUtility.SetDirty(led);
            EditorUtility.SetDirty(sleeve);
            return new[] { glove, body, glass, led, sleeve, rubber };
        }

        /// <summary>The desk pickup's materials in its submesh order (body, glass, LED, rubber): the held torch's own.</summary>
        public static Material[] ViewmodelPickupMaterials()
        {
            Material[] h = EnsureViewmodelHandMaterials();
            return new[] { h[1], h[2], h[3], h[5] };
        }

        private static Material HandMat(string name, Texture2D baseMap, Texture2D normal, Texture2D mask, Color tint, float normalScale,
            float smoothMin, float smoothMax, float metalMax, float aoMin = 0.35f)
        {
            string path = HandMatFolder + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("HDRP/Lit")) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseColorMap", baseMap);
            m.SetColor("_BaseColor", tint);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalScale", normalScale);
            m.SetTexture("_MaskMap", mask);
            if (mask != null)
            {
                m.SetFloat("_SmoothnessRemapMin", smoothMin);
                m.SetFloat("_SmoothnessRemapMax", smoothMax);
                m.SetFloat("_MetallicRemapMin", 0f);
                m.SetFloat("_MetallicRemapMax", metalMax);
                m.SetFloat("_AORemapMin", aoMin);
                m.SetFloat("_AORemapMax", 1f);
            }
            else
            {
                m.SetFloat("_Smoothness", smoothMax);
                m.SetFloat("_Metallic", metalMax);
            }
            m.SetFloat("_SupportDecals", 0f);
            m.SetFloat("_ReceivesSSR", 0f);
            HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
