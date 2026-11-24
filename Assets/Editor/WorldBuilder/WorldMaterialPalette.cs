using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Editor;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Central HDRP/Lit material & prop/model instantiation palette for all procedural Zone Builders.
    /// - Automatically creates/updates physical HDRP/Lit materials in Assets/Art/Materials/World/ backed by
    ///   packed MaskMaps via CretaceaPbrImporter.CreateOrUpdateHdrpLitMaterial(...) when textures exist.
    /// - Resolves calibrated fallback HDRP materials when running before full texture sync.
    /// - Instantiates real 4K FBXs from Assets/Art/Props, Foliage, Dinosaurs, Characters, Rocks with
    ///   automatic bottom-pivot grounding (Y = 0) and solid Environment colliders (zero floating props!).
    /// </summary>
    public sealed class WorldMaterialPalette
    {
        public const string MaterialsOutputFolder = "Assets/Art/Materials/World";
        public const string TexturesRootFolder = "Assets/Art/Textures";
        public const string PhysicsMaterialsFolder = "Assets/Settings/Physics";

        private static readonly string[] PropSearchRoots =
        {
            "Assets/Art/Props",
            "Assets/Art/Models/Props",
            "Assets/Art/Foliage",
            "Assets/Art/Models/Foliage",
            "Assets/Art/Rocks",
            "Assets/Art/Models/Rocks",
            "Assets/Art/Dinosaurs",
            "Assets/Art/Models/Dinosaurs",
            "Assets/Art/Characters",
            "Assets/Art/Models/Characters"
        };

        private readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _assetCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<SurfaceType, PhysicsMaterial> _physicsCache = new Dictionary<SurfaceType, PhysicsMaterial>();

        // Canonical Architectural & Industrial Materials
        public Material ConcreteFloor { get; private set; }
        public Material ConcreteFloorWet { get; private set; }
        public Material ConcreteExterior { get; private set; }
        public Material ConcreteBoardform { get; private set; }
        public Material AsphaltWet { get; private set; }
        public Material PlasterClean { get; private set; }
        public Material PlasterPeeling90s { get; private set; }
        public Material BrickOld { get; private set; }
        public Material LabTileWhite { get; private set; }
        public Material FloorTileClean { get; private set; }
        public Material FloorTileWet { get; private set; }
        public Material CarpetDark { get; private set; }
        public Material BrushedSteel { get; private set; }
        public Material DarkPaintedMetal { get; private set; }
        public Material RustedIron { get; private set; }
        public Material CorrugatedMetal { get; private set; }
        /// <summary>Grey painted corrugated sheet with rust only in the low spots (shutters, canopies, shed walls).</summary>
        public Material CorrugatedPainted { get; private set; }
        public Material WoodPlanks { get; private set; }
        public Material DarkGranitePlinth { get; private set; }
        public Material ChainlinkFence { get; private set; }
        public Material RockyCliff { get; private set; }
        public Material FoliageDark { get; private set; }
        public Material GlassArchitectural { get; private set; }
        public Material CctvScreenLive { get; private set; }

        // Added by the Look Pass (use these instead of stretching a wall texture over a floor or ceiling)
        public Material CeilingTiles { get; private set; }
        public Material DrywallPaint { get; private set; }
        public Material PavingStone { get; private set; }
        public Material GravelGround { get; private set; }
        public Material MudGround { get; private set; }
        public Material ConcreteCracked { get; private set; }
        public Material BrickPainted { get; private set; }
        public Material PaintedMetalGreen { get; private set; }
        public Material PaintedMetalYellow { get; private set; }
        public Material RoofBitumen { get; private set; }

        // Emissive Indicator & Fixture Materials
        public Material EmissiveRed { get; private set; }
        public Material EmissiveAmber { get; private set; }
        public Material EmissiveGreen { get; private set; }
        public Material EmissiveCyan { get; private set; }
        public Material EmissiveWarmWhite { get; private set; }
        public Material EmissiveCoolWhite { get; private set; }

        // Decal Materials
        public Material DecalCautionStripes { get; private set; }
        public Material DecalPuddleWet { get; private set; }
        public Material DecalBloodSmear { get; private set; }
        public Material DecalGrimeLeak { get; private set; }
        public Material DecalCracks { get; private set; }

        public WorldMaterialPalette()
        {
            EnsureFolder(MaterialsOutputFolder);
            BuildAllCoreMaterials();
        }

        private void BuildAllCoreMaterials()
        {
            // Every architectural material is mapped in WORLD SPACE (triplanar) at a real-world tile size, because the
            // kit builds everything from scaled cubes: with plain UVs a 40 m wall showed ONE stretched texture copy.
            // Columns: name, texture set, base-colour variant, mask variant, metres per texture repeat, tint,
            // smoothness remap (min, max), metallic remap max, normal strength.
            ConcreteFloor      = Surface("MAT_ConcreteFloor",      "concrete_polished",       "BaseColor",       "MaskMap",     3.0f, Tint(1.55f), 0.30f, 0.86f, 0f, 1.0f);
            ConcreteFloorWet   = Surface("MAT_ConcreteFloorWet",   "concrete_polished",       "BaseColor_Wet",   "MaskMap_Wet", 3.0f, Tint(1.55f), 0.62f, 0.97f, 0f, 0.9f);
            ConcreteExterior   = Surface("MAT_ConcreteExterior",   "concrete_wall",           "BaseColor",       "MaskMap",     3.2f, Tint(0.92f), 0.18f, 0.78f, 0f, 1.0f);
            ConcreteBoardform  = Surface("MAT_ConcreteBoardform",  "concrete_wall_boardform", "BaseColor",       "MaskMap",     3.0f, Tint(1.25f), 0.15f, 0.72f, 0f, 1.0f);
            ConcreteCracked    = Surface("MAT_ConcreteCracked",    "concrete_cracked_ext",    "BaseColor",       "MaskMap",     3.5f, Tint(1.9f),  0.25f, 0.85f, 0f, 1.0f);
            AsphaltWet         = Surface("MAT_AsphaltWet",         "asphalt_wet",             "BaseColor_Wet",   "MaskMap_Wet", 4.0f, Tint(1.0f),  0.45f, 0.96f, 0f, 1.0f);
            // Clean means clean: the white variant of the peeling-plaster set still peeled and showed black flake
            // blots on every "clean" ceiling. Painted drywall with a cool tint that cancels its beige.
            PlasterClean       = Surface("MAT_PlasterClean",       "drywall_paint",           "BaseColor",       "MaskMap",     3.0f, new Color(1.16f, 1.24f, 1.40f), 0.10f, 0.42f, 0f, 0.5f);
            PlasterPeeling90s  = Surface("MAT_PlasterPeeling90s",  "plaster_painted_peeling", "BaseColor",       "MaskMap",     3.0f, Tint(1.1f),  0.08f, 0.50f, 0f, 1.0f);
            DrywallPaint       = Surface("MAT_DrywallPaint",       "drywall_paint",           "BaseColor",       "MaskMap",     3.0f, Tint(1.2f),  0.10f, 0.42f, 0f, 0.8f);
            CeilingTiles       = Surface("MAT_CeilingTiles",       "ceiling_tiles",           "BaseColor",       "MaskMap",     1.2f, Tint(1.0f),  0.05f, 0.30f, 0f, 1.0f);
            BrickOld           = Surface("MAT_BrickOld",           "brick_painted",           "BaseColor",       "MaskMap",     2.6f, Tint(0.9f),  0.10f, 0.60f, 0f, 1.0f);
            BrickPainted       = Surface("MAT_BrickPainted",       "brick_painted",           "BaseColor",       "MaskMap",     2.6f, Tint(1.0f),  0.10f, 0.60f, 0f, 1.0f);
            LabTileWhite       = Surface("MAT_LabTileWhite",       "tiles_white",             "BaseColor",       "MaskMap",     2.0f, Tint(0.95f), 0.35f, 0.92f, 0f, 1.0f);
            FloorTileClean     = Surface("MAT_FloorTileClean",     "floor_tiles_industrial",  "BaseColor",       "MaskMap",     2.4f, Tint(1.5f),  0.25f, 0.85f, 0f, 1.0f);
            FloorTileWet       = Surface("MAT_FloorTileWet",       "floor_tiles_industrial",  "BaseColor_Wet",   "MaskMap_Wet", 2.4f, Tint(1.5f),  0.60f, 0.97f, 0f, 0.9f);
            CarpetDark         = Surface("MAT_CarpetDark",         "carpet_office",           "BaseColor",       "MaskMap",     2.0f, new Color(1.5f, 1.55f, 1.7f), 0.0f, 0.22f, 0f, 1.0f);
            BrushedSteel       = Surface("MAT_BrushedSteel",       "steel_brushed",           "BaseColor",       "MaskMap",     1.4f, Tint(0.85f), 0.35f, 0.88f, 1f, 1.0f);
            DarkPaintedMetal   = Surface("MAT_DarkPaintedMetal",   "metal_painted_worn",      "grey_BaseColor",  "MaskMap",     1.6f, new Color(0.50f, 0.53f, 0.58f), 0.25f, 0.78f, 0.35f, 1.0f);
            PaintedMetalGreen  = Surface("MAT_PaintedMetalGreen",  "metal_painted_worn",      "green_BaseColor", "MaskMap",     1.6f, Tint(1.25f), 0.25f, 0.78f, 0.35f, 1.0f);
            PaintedMetalYellow = Surface("MAT_PaintedMetalYellow", "metal_painted_worn",      "BaseColor",       "MaskMap",     1.6f, Tint(0.9f),  0.25f, 0.78f, 0.35f, 1.0f);
            RustedIron         = Surface("MAT_RustedIron",         "metal_rusty",             "BaseColor",       "MaskMap",     1.6f, Tint(1.5f),  0.10f, 0.70f, 0.6f, 1.0f);
            CorrugatedMetal    = Surface("MAT_CorrugatedMetal",    "metal_corrugated_rusty",  "BaseColor",       "MaskMap",     2.2f, Tint(1.9f),  0.15f, 0.75f, 0.7f, 1.0f);
            CorrugatedPainted  = Surface("MAT_CorrugatedPainted",  "metal_corrugated_rusty",  "BaseColor_Painted", "MaskMap",   2.2f, Tint(1.0f),  0.20f, 0.62f, 0.25f, 1.0f);
            WoodPlanks         = Surface("MAT_WoodPlanks",         "wood_planks_weathered",   "BaseColor",       "MaskMap",     2.2f, Tint(1.9f),  0.10f, 0.62f, 0f, 1.0f);
            DarkGranitePlinth  = Surface("MAT_DarkGranitePlinth",  "concrete_polished",       "BaseColor",       "MaskMap",     2.0f, Tint(0.55f), 0.55f, 0.96f, 0f, 0.5f);
            RockyCliff         = Surface("MAT_RockyCliff",         "rock_mossy",              "BaseColor",       "MaskMap",     6.0f, Tint(1.0f),  0.10f, 0.70f, 0f, 1.2f);
            FoliageDark        = Surface("MAT_FoliageDark",        "moss_ground",             "BaseColor",       "MaskMap",     3.0f, new Color(0.55f, 0.85f, 0.45f), 0.05f, 0.50f, 0f, 1.0f);
            PavingStone        = Surface("MAT_PavingStone",        "paving_stone",            "BaseColor",       "MaskMap",     3.0f, Tint(1.0f),  0.25f, 0.80f, 0f, 1.0f);   // wet paving is a sheen, not a mirror (review 2)
            GravelGround       = Surface("MAT_GravelGround",       "gravel_road",             "BaseColor",       "MaskMap",     3.0f, Tint(1.5f),  0.20f, 0.85f, 0f, 1.0f);
            MudGround          = Surface("MAT_MudGround",          "mud",                     "BaseColor_Wet",   "MaskMap_Wet", 3.5f, Tint(1.3f),  0.40f, 0.95f, 0f, 1.0f);
            RoofBitumen        = Surface("MAT_RoofBitumen",        "asphalt_wet",             "BaseColor",       "MaskMap",     5.0f, Tint(0.8f),  0.20f, 0.80f, 0f, 0.8f);

            ChainlinkFence = BuildChainlinkMaterial();
            GlassArchitectural = BuildGlassMaterial();
            CctvScreenLive = LoadOrBuildCctvMaterial();

            EmissiveRed = GetOrBuildEmissive("MAT_EmissiveRed", new Color(1.0f, 0.10f, 0.06f), 20f);
            EmissiveAmber = GetOrBuildEmissive("MAT_EmissiveAmber", new Color(1.0f, 0.55f, 0.12f), 22f);
            EmissiveGreen = GetOrBuildEmissive("MAT_EmissiveGreen", new Color(0.45f, 1.0f, 0.65f), 14f);   // pale core, green halo: not a neon puck
            EmissiveCyan = GetOrBuildEmissive("MAT_EmissiveCyan", new Color(0.24f, 0.85f, 1.0f), 26f);
            EmissiveWarmWhite = GetOrBuildEmissive("MAT_EmissiveWarmWhite", new Color(1.0f, 0.86f, 0.66f), 90f);
            EmissiveCoolWhite = GetOrBuildEmissive("MAT_EmissiveCoolWhite", new Color(0.86f, 0.93f, 1.0f), 90f);

            DecalCautionStripes = CreateDecalMaterial("MAT_DecalCautionStripes", "hazard_stripes", null, new Color(0.92f, 0.72f, 0.14f, 0.92f));
            DecalPuddleWet = LoadExistingOrCreateDecal("Assets/Settings/Rendering/MAT_WetPuddleDecal.mat", "MAT_DecalPuddleWet", "puddle_mask", "rain_ripples_normal", new Color(0.12f, 0.15f, 0.18f, 0.72f));
            DecalBloodSmear = CreateDecalMaterial("MAT_DecalBloodSmear", "blood_smear", null, new Color(0.32f, 0.03f, 0.03f, 0.88f));
            DecalGrimeLeak = CreateDecalMaterial("MAT_DecalGrimeLeak", "grime_leak_1", null, new Color(0.12f, 0.13f, 0.11f, 0.65f));
            DecalCracks = CreateDecalMaterial("MAT_DecalCracks", "cracks_1", "cracks_1_normal", new Color(0.16f, 0.16f, 0.16f, 0.78f));
        }

        private static Color Tint(float v) => new Color(v, v, v, 1f);

        /// <summary>
        /// One architectural surface: an exact texture set (no fuzzy matching), mapped in world space so the texture
        /// repeats every <paramref name="tileMeters"/> metres on any box of any size, with its own gloss range.
        /// </summary>
        public Material Surface(string matName, string setFolder, string baseVariant, string maskVariant, float tileMeters,
            Color tint, float smoothMin, float smoothMax, float metallicMax, float normalScale)
        {
            if (_matCache.TryGetValue(matName, out Material cached) && cached != null) return cached;

            string dir = $"{TexturesRootFolder}/{setFolder}";
            string basePath = $"{dir}/{setFolder}_{baseVariant}.png";
            string maskPath = $"{dir}/{setFolder}_{maskVariant}.png";
            string normalPath = $"{dir}/{setFolder}_Normal.png";
            if (!File.Exists(basePath)) { Debug.LogWarning($"[Palette] {matName}: missing {basePath}"); basePath = null; }
            if (!File.Exists(maskPath)) maskPath = File.Exists($"{dir}/{setFolder}_MaskMap.png") ? $"{dir}/{setFolder}_MaskMap.png" : null;
            if (!File.Exists(normalPath)) normalPath = null;

            string matPath = $"{MaterialsOutputFolder}/{matName}.mat";
            Material mat = CretaceaPbrImporter.CreateOrUpdateHdrpLitMaterial(
                matPath,
                baseColorAssetPath: basePath,
                maskMapAssetPath: maskPath,
                normalAssetPath: normalPath,
                metallic: 0f,
                smoothness: smoothMax,
                normalScale: normalScale);
            if (mat == null) return null;

            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_NormalScale", normalScale);
            mat.SetFloat("_SmoothnessRemapMin", smoothMin);
            mat.SetFloat("_SmoothnessRemapMax", smoothMax);
            mat.SetFloat("_MetallicRemapMin", 0f);
            mat.SetFloat("_MetallicRemapMax", metallicMax);
            mat.SetFloat("_AORemapMin", 0.25f);
            mat.SetFloat("_AORemapMax", 1f);
            ApplyWorldMapping(mat, tileMeters);
            EditorUtility.SetDirty(mat);
            _matCache[matName] = mat;
            return mat;
        }

        /// <summary>HDRP Lit world-space triplanar mapping: one texture repeat every tileMeters metres.</summary>
        public static void ApplyWorldMapping(Material mat, float tileMeters)
        {
            float scale = 1f / Mathf.Max(0.05f, tileMeters);
            mat.SetFloat("_UVBase", 5f);                 // UVBaseMapping.Triplanar
            mat.SetFloat("_ObjectSpaceUVMapping", 0f);   // world space
            mat.SetFloat("_TexWorldScale", scale);
            mat.SetVector("_UVMappingMask", new Vector4(0f, 0f, 0f, 0f));
            mat.SetFloat("_InvTilingScale", 2f / scale);
            mat.SetTextureScale("_BaseColorMap", Vector2.one);
            HDMaterial.ValidateMaterial(mat);
        }

        public Material GetOrBuildPbr(string matName, string[] folderHints, Color fallbackTint, float metallic, float smoothness)
        {
            if (_matCache.TryGetValue(matName, out Material cached) && cached != null) return cached;

            string matPath = $"{MaterialsOutputFolder}/{matName}.mat";
            string basePath = null, maskPath = null, normalPath = null;
            FindTextureSet(folderHints, out basePath, out maskPath, out normalPath);

            Material mat = CretaceaPbrImporter.CreateOrUpdateHdrpLitMaterial(
                matPath,
                baseColorAssetPath: basePath,
                maskMapAssetPath: maskPath,
                normalAssetPath: normalPath,
                metallic: metallic,
                smoothness: smoothness,
                normalScale: 1.0f);

            if (mat != null)
            {
                if (string.IsNullOrEmpty(basePath) && mat.HasProperty("_BaseColor"))
                {
                    mat.SetColor("_BaseColor", fallbackTint);
                }
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                EditorUtility.SetDirty(mat);
                _matCache[matName] = mat;
            }
            return mat;
        }

        public Material GetOrBuildEmissive(string matName, Color color, float intensity)
        {
            if (_matCache.TryGetValue(matName, out Material cached) && cached != null) return cached;

            string matPath = $"{MaterialsOutputFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                mat = new Material(hdrpLit != null ? hdrpLit : Shader.Find("Standard")) { name = matName };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color * 0.25f);
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", color * intensity);
            if (mat.HasProperty("_EmissiveColorLDR")) mat.SetColor("_EmissiveColorLDR", color);
            if (mat.HasProperty("_EmissiveIntensity")) mat.SetFloat("_EmissiveIntensity", intensity);
            if (mat.HasProperty("_UseEmissiveIntensity")) mat.SetFloat("_UseEmissiveIntensity", 1f);
            if (mat.HasProperty("_EmissiveExposureWeight")) mat.SetFloat("_EmissiveExposureWeight", 1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);

            _matCache[matName] = mat;
            return mat;
        }

        public Material CreateTextureQuadMaterial(string matName, string textureStem, bool emissive = false, float emissiveIntensity = 1.6f, Color? fallbackTint = null)
        {
            if (_matCache.TryGetValue(matName, out Material cached) && cached != null) return cached;

            string matPath = $"{MaterialsOutputFolder}/{matName}.mat";
            string texPath = FindSingleTextureByStem(textureStem);
            Material mat = CretaceaPbrImporter.CreateOrUpdateHdrpLitMaterial(
                matPath,
                baseColorAssetPath: texPath,
                emissiveAssetPath: emissive ? texPath : null,
                metallic: 0.05f,
                smoothness: 0.45f);

            if (mat != null)
            {
                if (string.IsNullOrEmpty(texPath) && fallbackTint.HasValue && mat.HasProperty("_BaseColor"))
                    mat.SetColor("_BaseColor", fallbackTint.Value);

                if (emissive)
                {
                    Color emCol = (fallbackTint ?? Color.white) * emissiveIntensity;
                    if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", emCol);
                    if (mat.HasProperty("_EmissiveIntensity")) mat.SetFloat("_EmissiveIntensity", emissiveIntensity);
                }
                EditorUtility.SetDirty(mat);
                _matCache[matName] = mat;
            }
            return mat;
        }

        public Material CreateDecalMaterial(string matName, string albedoStem, string normalStem, Color fallbackTint)
        {
            if (_matCache.TryGetValue(matName, out Material cached) && cached != null) return cached;

            string matPath = $"{MaterialsOutputFolder}/{matName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader decalShader = Shader.Find("HDRP/Decal") ?? Shader.Find("HDRP/Lit");
                mat = new Material(decalShader) { name = matName };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            string albPath = FindSingleTextureByStem(albedoStem);
            string nrmPath = !string.IsNullOrEmpty(normalStem) ? FindSingleTextureByStem(normalStem) : null;

            if (!string.IsNullOrEmpty(albPath) && mat.HasProperty("_BaseColorMap"))
                mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(albPath));
            if (!string.IsNullOrEmpty(nrmPath) && mat.HasProperty("_NormalMap"))
                mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(nrmPath));
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", string.IsNullOrEmpty(albPath) ? fallbackTint : Color.white);

            EditorUtility.SetDirty(mat);
            _matCache[matName] = mat;
            return mat;
        }

        private Material LoadExistingOrCreateDecal(string existingPath, string fallbackName, string albStem, string nrmStem, Color tint)
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(existingPath);
            if (existing != null) return existing;
            return CreateDecalMaterial(fallbackName, albStem, nrmStem, tint);
        }

        private Material BuildGlassMaterial()
        {
            const string matPath = MaterialsOutputFolder + "/MAT_GlassArchitectural.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                mat = new Material(hdrpLit != null ? hdrpLit : Shader.Find("Standard")) { name = "MAT_GlassArchitectural" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            // Configure clean HDRP Transparent Lit glass with subtle rain-streaked specular sheen
            if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f); // Transparent
            if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f);
            if (mat.HasProperty("_AlphaDstBlend")) mat.SetFloat("_AlphaDstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.62f, 0.78f, 0.82f, 0.24f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.94f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.08f);
            if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;

            // glass_dirt.png is an sRGB picture, not an HDRP detail map: bound as one it tilted every glass normal.
            if (mat.HasProperty("_DetailMap")) mat.SetTexture("_DetailMap", null);
            // Almost no diffuse layer: at alpha 0.14 with a pale base, room light turned every window into a lit
            // milky film as bright as the ceiling. Glass shows through and reflects; it does not glow.
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.30f, 0.36f, 0.36f, 0.035f));
            if (mat.HasProperty("_EnableBlendModePreserveSpecularLighting")) mat.SetFloat("_EnableBlendModePreserveSpecularLighting", 1f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.96f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            // 0: with it on, the glass writes a transparent depth prepass that hides the rain behind every window
            if (mat.HasProperty("_ReceivesSSRTransparent")) mat.SetFloat("_ReceivesSSRTransparent", 0f);
            HDMaterial.ValidateMaterial(mat);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private Material BuildChainlinkMaterial()
        {
            const string matPath = MaterialsOutputFolder + "/MAT_ChainlinkFence.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader hdrpLit = Shader.Find("HDRP/Lit");
                mat = new Material(hdrpLit != null ? hdrpLit : Shader.Find("Standard")) { name = "MAT_ChainlinkFence" };
                AssetDatabase.CreateAsset(mat, matPath);
            }

            string texPath = FindSingleTextureByStem("chainlink_albedo") ?? FindSingleTextureByStem("chainlink");
            if (!string.IsNullOrEmpty(texPath) && mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
                // Cut-out wires with plain averaged mips. Review 1 saw every far fence as a wall (coverage-preserving
                // mips turn a distant cut-out mesh into a solid dotted panel); the blended version that replaced it
                // averaged into a milky veil that caught every lamp, and review 2 saw fences and the E-0 gate as
                // frosted glass. With plain mips the mean alpha (0.24) falls under the cut-off as the wires go
                // sub-pixel: near, the mesh is crisp dark wire; far away only posts and rails are left.
                mat.SetFloat("_SurfaceType", 0f);
                mat.SetFloat("_BlendMode", 0f);
                mat.SetFloat("_AlphaCutoffEnable", 1f);
                mat.SetFloat("_AlphaCutoff", 0.27f);     // just above the texture's mean alpha: far mips fall under it and vanish
                mat.SetFloat("_UseShadowThreshold", 1f);
                mat.SetFloat("_AlphaCutoffShadow", 0.4f);
                mat.SetFloat("_TransparentDepthPrepassEnable", 0f);
                mat.SetFloat("_TransparentWritingMotionVec", 0f);
                mat.SetFloat("_EnableFogOnTransparent", 1f);
                mat.SetFloat("_ReceivesSSRTransparent", 0f);
                // Review 2: pale, shiny wires averaged into a milky veil that caught every lamp: fences and the E-0
                // gate read as frosted glass. Weathered galvanised wire is a dark grey that takes little light.
                mat.SetColor("_BaseColor", new Color(0.34f, 0.35f, 0.36f, 1f));
                PlainMips(texPath);

                string normalPath = FindSingleTextureByStem("chainlink_normal");
                if (!string.IsNullOrEmpty(normalPath) && mat.HasProperty("_NormalMap"))
                {
                    mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                    mat.SetFloat("_NormalScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                // Real chain-link: the texture holds 32 diamonds across, so one repeat every 1.8 m gives 5.6 cm
                // diamonds on a fence of any size (UV mapping stretched one repeat over the whole panel: 0.5 m holes).
                // 2.6 m: 8 cm diamonds with wires thick enough to stay wires a few metres away (at 5.6 cm they were
                // under a pixel beyond 3 m and only the veil was left).
                ApplyWorldMapping(mat, 2.6f);
            }
            else
            {
                if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.32f, 0.35f, 0.38f, 0.42f));
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.5f);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
            if (mat.HasProperty("_DoubleSidedEnable")) mat.SetFloat("_DoubleSidedEnable", 1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Plain averaged mips with strong anisotropic filtering: a blended wire mesh fades to its mean alpha.</summary>
        private static void PlainMips(string texturePath)
        {
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer == null) return;
            if (!importer.mipMapsPreserveCoverage && importer.mipmapEnabled && importer.anisoLevel >= 8 && Mathf.Approximately(importer.mipMapBias, -1.0f)) return;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = false;
            importer.anisoLevel = 8;
            importer.mipMapBias = -1.0f;        // keep the wires a little longer before they average out
            importer.SaveAndReimport();
        }

        private Material LoadOrBuildCctvMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Settings/Rendering/MAT_CctvScreen_Live.mat");
            if (existing != null) return existing;

            const string matPath = MaterialsOutputFolder + "/MAT_CctvScreenFallback.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader sh = Shader.Find("Cretacea/HDRP/CctvScreen") ?? Shader.Find("HDRP/Lit");
                mat = new Material(sh) { name = "MAT_CctvScreenFallback" };
                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        public PhysicsMaterial GetSurfacePhysicsMaterial(SurfaceType surface)
        {
            if (_physicsCache.TryGetValue(surface, out PhysicsMaterial cached) && cached != null) return cached;
            string path = $"{PhysicsMaterialsFolder}/PM_{surface}.physicMaterial";
            PhysicsMaterial pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm != null) _physicsCache[surface] = pm;
            return pm;
        }

        /// <summary>
        /// Instantiates a real staged FBX/Prefab from Assets/Art/* matching assetStem, normalizing its scale
        /// and seating its bottom bounds cleanly at worldPos.y (grounded = true) with an Environment collider.
        /// If the 3D asset is not yet synced, creates a clean proportional architectural proxy instead of crashing.
        /// </summary>
        public GameObject InstantiatePropOrFallback(
            string assetStem,
            Transform parent,
            Vector3 worldPos,
            Vector3 eulerAngles,
            Vector3 targetSize,
            bool grounded = true,
            bool addCollider = true,
            bool useMeshCollider = false,
            Material fallbackMat = null,
            string customName = null)
        {
            GameObject prefab = FindModelAsset(assetStem);
            GameObject instance;

            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = string.IsNullOrEmpty(customName) ? assetStem : customName;
                instance.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(eulerAngles));

                NormalizeScaleAndGround(instance, worldPos, targetSize, grounded);

                if (addCollider && instance.GetComponentInChildren<Collider>() == null)
                {
                    AttachCleanCollider(instance, useMeshCollider);
                }
            }
            else
            {
                Vector3 sz = new Vector3(
                    Mathf.Max(0.12f, targetSize.x),
                    Mathf.Max(0.12f, targetSize.y),
                    Mathf.Max(0.12f, targetSize.z));
                instance = new GameObject(string.IsNullOrEmpty(customName) ? $"{assetStem}_Proxy" : customName);
                instance.transform.SetParent(parent, false);
                instance.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(eulerAngles));

                GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(instance.transform, false);
                body.transform.localScale = sz;
                body.transform.localPosition = grounded ? new Vector3(0f, sz.y * 0.5f, 0f) : Vector3.zero;
                MeshRenderer mr = body.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = fallbackMat != null ? fallbackMat : DarkPaintedMetal;

                if (!addCollider)
                {
                    Collider c = body.GetComponent<Collider>();
                    if (c != null) UnityEngine.Object.DestroyImmediate(c);
                }
            }

            GameLayers.SetLayerRecursively(instance, GameLayers.Environment);
            return instance;
        }

        private static void NormalizeScaleAndGround(GameObject go, Vector3 targetWorldPos, Vector3 targetSize, bool grounded)
        {
            if (!TryGetCombinedBounds(go, out Bounds b)) return;

            if (targetSize.sqrMagnitude > 0.0001f)
            {
                float maxTarget = Mathf.Max(targetSize.x, Mathf.Max(targetSize.y, targetSize.z));
                float maxCurrent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (maxCurrent > 0.0001f && maxTarget > 0.001f)
                {
                    float uniform = maxTarget / maxCurrent;
                    go.transform.localScale *= uniform;
                    TryGetCombinedBounds(go, out b);
                }
            }

            if (grounded)
            {
                float deltaY = targetWorldPos.y - b.min.y;
                go.transform.position += new Vector3(0f, deltaY, 0f);
            }
        }

        private static void AttachCleanCollider(GameObject go, bool useMeshCollider)
        {
            if (useMeshCollider)
            {
                MeshFilter[] filters = go.GetComponentsInChildren<MeshFilter>();
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i].sharedMesh == null) continue;
                    MeshCollider mc = filters[i].gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = filters[i].sharedMesh;
                }
                return;
            }

            // The box is measured in the prop's own space. Measured from the world-space bounds, a prop turned 90
            // degrees got a box turned the other way (a long bench blocked the aisle and could be walked through).
            if (TryGetLocalMeshBounds(go, out Bounds local))
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size;
                return;
            }

            if (!TryGetCombinedBounds(go, out Bounds b)) return;
            BoxCollider bc = go.AddComponent<BoxCollider>();
            bc.center = go.transform.InverseTransformPoint(b.center);
            Vector3 lossy = go.transform.lossyScale;
            bc.size = new Vector3(
                b.size.x / Mathf.Max(0.001f, Mathf.Abs(lossy.x)),
                b.size.y / Mathf.Max(0.001f, Mathf.Abs(lossy.y)),
                b.size.z / Mathf.Max(0.001f, Mathf.Abs(lossy.z)));
        }

        private static bool TryGetLocalMeshBounds(GameObject go, out Bounds local)
        {
            local = default;
            bool any = false;
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                    Vector3 point = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, corner));
                    if (!any) { local = new Bounds(point, Vector3.zero); any = true; }
                    else local.Encapsulate(point);
                }
            }
            return any;
        }

        private static bool TryGetCombinedBounds(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return true;
        }

        private GameObject FindModelAsset(string stem)
        {
            if (string.IsNullOrWhiteSpace(stem)) return null;
            if (_assetCache.TryGetValue(stem, out GameObject cached)) return cached;

            var validFolders = new List<string>();
            for (int i = 0; i < PropSearchRoots.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(PropSearchRoots[i])) validFolders.Add(PropSearchRoots[i]);
            }

            if (validFolders.Count > 0)
            {
                string compact = stem.Replace("_", string.Empty);
                string[] guids = AssetDatabase.FindAssets($"{stem} t:Prefab", validFolders.ToArray());
                GameObject best = PickExactStemMatch(guids, stem);
                if (best == null)
                {
                    guids = AssetDatabase.FindAssets($"{stem} t:Model", validFolders.ToArray());
                    best = PickExactStemMatch(guids, stem);
                }
                if (best == null && !string.Equals(compact, stem, StringComparison.OrdinalIgnoreCase))
                {
                    guids = AssetDatabase.FindAssets($"{compact} t:Model", validFolders.ToArray());
                    best = PickExactStemMatch(guids, compact);
                }
                _assetCache[stem] = best;
                return best;
            }

            _assetCache[stem] = null;
            return null;
        }

        private static GameObject PickExactStemMatch(string[] guids, string stem)
        {
            string normStem = stem.Replace("_", string.Empty);
            GameObject partial = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileStem = Path.GetFileNameWithoutExtension(path);
                string normFile = fileStem.Replace("_", string.Empty);
                GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj == null) continue;
                if (string.Equals(fileStem, stem, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normFile, normStem, StringComparison.OrdinalIgnoreCase))
                    return obj;
                if (partial == null && (fileStem.IndexOf(stem, StringComparison.OrdinalIgnoreCase) >= 0
                    || normFile.IndexOf(normStem, StringComparison.OrdinalIgnoreCase) >= 0))
                    partial = obj;
            }
            return partial;
        }

        private static void FindTextureSet(string[] folderHints, out string basePath, out string maskPath, out string normalPath)
        {
            basePath = null;
            maskPath = null;
            normalPath = null;
            if (!AssetDatabase.IsValidFolder(TexturesRootFolder) || folderHints == null) return;

            string[] allTexGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesRootFolder });
            for (int h = 0; h < folderHints.Length; h++)
            {
                string hint = folderHints[h].ToLowerInvariant();
                for (int i = 0; i < allTexGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(allTexGuids[i]);
                    string lower = path.Replace('\\', '/').ToLowerInvariant();
                    if (!lower.Contains(hint)) continue;

                    string name = Path.GetFileNameWithoutExtension(lower);
                    if (maskPath == null && (name.Contains("maskmap") || name.Contains("mask_map"))) maskPath = path;
                    else if (normalPath == null && (name.Contains("normal") || name.Contains("_nor"))) normalPath = path;
                    else if (basePath == null && (name.Contains("albedo") || name.Contains("basecolor") || name.Contains("diff") || name.Contains("color"))) basePath = path;
                }
                if (basePath != null) return;
            }
        }

        private static string FindSingleTextureByStem(string stem)
        {
            if (string.IsNullOrWhiteSpace(stem)) return null;
            string[] roots = { "Assets/Art", "Assets/Textures", "Assets/Settings" };
            var valid = new List<string>();
            for (int i = 0; i < roots.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(roots[i])) valid.Add(roots[i]);
            }
            if (valid.Count == 0) return null;

            string[] guids = AssetDatabase.FindAssets($"{stem} t:Texture2D", valid.ToArray());
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(Path.GetFileNameWithoutExtension(path), stem, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return guids.Length > 0 ? AssetDatabase.GUIDToAssetPath(guids[0]) : null;
        }

        public static void EnsureFolder(string assetFolderPath)
        {
            string normalized = assetFolderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized)) return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
