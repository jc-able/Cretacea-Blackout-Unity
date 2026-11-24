using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Cretacea.Editor
{
    /// <summary>
    /// Automatic Unity 6 HDRP PBR asset postprocessor and MaskMap packer for Cretacea: Blackout.
    /// - Configures Normal maps (OpenGL +Y), linear data maps (Roughness/Metallic/AO/MaskMap/ORM),
    ///   and sRGB albedo/emissive maps cleanly for HDRP.
    /// - Sets clean FBX/model import defaults (no stray lights/cameras, readable meshes for
    ///   environment/prop MeshColliders, uncompressed meshes for rigged creatures).
    /// - Automatically packs Metallic (R), AO (G), Detail (B=0), Smoothness (A = 1 - Roughness)
    ///   into HDRP *_MaskMap.png textures (also supports glTF/Tripo/Blender *_ORM / *_ARM maps).
    /// </summary>
    public class CretaceaPbrImporter : AssetPostprocessor
    {
        private static bool s_IsPacking;

        private static readonly string[] k_IgnoredRoots =
        {
            "Assets/Settings/",
            "Assets/TutorialInfo/",
            "Assets/Editor/",
            "Assets/VARCOSound/",
            "Packages/"
        };

        private static readonly string[] k_UiPathTokens =
        {
            "/ui/",
            "/hud/",
            "/documents/",
            "/docs/",
            "/fonts/",
            "/icons/"
        };

        private static readonly string[] k_NormalTokens =
        {
            "_normal", "-normal", "_norm", "_nrm", "_nor_gl", "_nor", "normalmap"
        };

        private static readonly string[] k_MaskMapTokens =
        {
            "_maskmap", "_hdrpmask", "_mask_map"
        };

        private static readonly string[] k_LinearDataTokens =
        {
            "_roughness", "-roughness", "_rough",
            "_metallic", "-metallic", "_metalness", "_metal",
            "_smoothness", "_smooth", "_gloss",
            "_ao", "_ambientocclusion", "_occlusion",
            "_orm", "_arm", "_mra", "_rma",
            "_height", "_disp", "_displacement",
            "_opacity", "_alpha", "_puddle_mask", "_mask"
        };

        private static readonly string[] k_AlbedoTokens =
        {
            "_albedo", "_basecolor", "_base_color", "_color", "_col",
            "_diff", "_diffuse", "_emissive", "_emission"
        };

        private static readonly string[] k_HighResPathTokens =
        {
            "/4k/", "/hdri/", "/hdris/", "/sky/", "/dinosaurs/", "/creatures/", "/hero/", "/flashlight/"
        };

        private static readonly string[] k_ReadableModelTokens =
        {
            "/environment/", "/architecture/", "/props/", "/level/", "/terrain/", "/foliage/", "collider"
        };

        private static readonly string[] k_CreatureModelTokens =
        {
            "/dinosaurs/", "/creatures/", "/characters/", "/humans/", "raptor", "trex", "sauropod", "triceratops"
        };

        private static bool IsIgnoredPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var root in k_IgnoredRoots)
            {
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool IsUiPath(string lowerPath)
        {
            foreach (var tok in k_UiPathTokens)
            {
                if (lowerPath.Contains(tok))
                    return true;
            }
            return false;
        }

        private static bool MatchesAnyToken(string lowerBaseName, string[] tokens)
        {
            foreach (var tok in tokens)
            {
                if (lowerBaseName.EndsWith(tok, StringComparison.Ordinal) ||
                    lowerBaseName.Contains(tok + "_") ||
                    lowerBaseName.Contains(tok + "-"))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnPreprocessTexture()
        {
            if (IsIgnoredPath(assetPath))
                return;

            var importer = assetImporter as TextureImporter;
            if (importer == null)
                return;

            string lowerPath = assetPath.Replace('\\', '/').ToLowerInvariant();
            string lowerName = Path.GetFileNameWithoutExtension(lowerPath);

            // Preserve HDRI / EXR skies as HDRP Cubemaps (4096)
            if (lowerPath.EndsWith(".hdr") || lowerPath.EndsWith(".exr"))
            {
                importer.sRGBTexture = false;
                importer.maxTextureSize = 4096;
                if (lowerPath.Contains("/hdri/") || lowerPath.Contains("/hdris/") || lowerPath.Contains("/sky/"))
                {
                    importer.textureShape = TextureImporterShape.TextureCube;
                }
                return;
            }

            // UI / HUD / Document / Map textures import as crisp Sprites; 3D Signs stay Default sRGB textures
            if (IsUiPath(lowerPath))
            {
                importer.sRGBTexture = true;
                importer.maxTextureSize = 2048;
                bool isPage = lowerPath.Contains("/documents/") || lowerPath.Contains("/docs/") || lowerPath.Contains("/maps/");
                if (isPage)
                {
                    // Documents and maps are read up close (document reader page ~980 px tall, journal map) and
                    // minified 2-3x: full resolution, mip maps + trilinear, high-quality compression (BC7, never BC1).
                    importer.textureType = TextureImporterType.Sprite;
                    importer.maxTextureSize = 4096;
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 4;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    // Document pages are minified ~2.2x at 1080p: lean one half-step towards the sharper mip.
                    if (!lowerPath.Contains("/maps/")) importer.mipMapBias = -0.5f;
                }
                else if (lowerPath.Contains("/resources/ui/keys/"))
                {
                    // Keycap atlas for inline <sprite> keys in running text (drawn at ~30-60 % of its size).
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = true;
                    importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.alphaIsTransparency = true;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                }
                else if (lowerPath.Contains("/hud/") || lowerPath.Contains("/icons/"))
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true;
                }
                else if (lowerPath.Contains("/resources/ui/brand/"))
                {
                    // UI logo lockups: shown at ~15-30 % of their size, so mip maps keep the edges clean.
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = true;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.alphaIsTransparency = true;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                }
                else
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = true;
                    importer.anisoLevel = 8;
                }
                return;
            }

            int targetMaxSize = k_HighResPathTokens.Any(lowerPath.Contains) ? 4096 : 2048;
            importer.maxTextureSize = targetMaxSize;
            importer.mipmapEnabled = true;

            if (MatchesAnyToken(lowerName, k_NormalTokens))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.sRGBTexture = false;
                importer.convertToNormalmap = false; // OpenGL +Y normal maps used directly by Unity
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            if (MatchesAnyToken(lowerName, k_MaskMapTokens))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false;
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                return;
            }

            // VFX & Decal alpha maps (rain streaks, splashes, cookies, blood, footprints, grime, chainlink)
            if (lowerPath.Contains("/vfx/") || lowerPath.Contains("/decals/"))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.anisoLevel = 8;
                if (lowerName.Contains("flashlight_cookie"))
                {
                    // A light cookie is intensity data, not colour: read as sRGB its 11 % spill became 1 %.
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.mipmapEnabled = false;
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.alphaIsTransparency = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    // 16-bit source kept at 16 bits: at 8 bits the dim spill (1-11 of 255) showed as stepped rings
                    TextureImporterPlatformSettings ps = importer.GetDefaultPlatformTextureSettings();
                    // RGBAHalf, not R16 (a single red channel tints the beam red) and not RGBA64 (it works, but the
                    // importer logs 'RGBA 64 bit is not valid for texture type Default' on every import)
                    ps.format = TextureImporterFormat.RGBAHalf;
                    ps.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SetPlatformTextureSettings(ps);
                }
                return;
            }

            // Explicit Albedo/BaseColor tokens take precedence over linear tokens (e.g. fps_arm_BaseColor must NOT match _arm!)
            if (MatchesAnyToken(lowerName, k_AlbedoTokens))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.anisoLevel = 8;
                return;
            }

            if (MatchesAnyToken(lowerName, k_LinearDataTokens))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.anisoLevel = 4;
                return;
            }
        }

        private void OnPreprocessModel()
        {
            if (IsIgnoredPath(assetPath))
                return;

            var importer = assetImporter as ModelImporter;
            if (importer == null)
                return;

            string lowerPath = assetPath.Replace('\\', '/').ToLowerInvariant();

            // Strip stray cameras/lights exported from Blender or Tripo
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = true;
            importer.importVisibility = true;
            importer.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;

            bool isCreature = k_CreatureModelTokens.Any(lowerPath.Contains);
            bool isReadableEnv = k_ReadableModelTokens.Any(lowerPath.Contains);

            if (isCreature)
            {
                // Avoid mesh quantization/compression artifacts on skinned creatures
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.isReadable = false;
            }
            else if (isReadableEnv)
            {
                // Ensure static architecture/prop meshes can back MeshColliders cleanly
                importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Low;
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (s_IsPacking || importedAssets == null || importedAssets.Length == 0)
                return;

            var dirsToScan = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var asset in importedAssets)
            {
                if (IsIgnoredPath(asset))
                    continue;

                string lower = asset.Replace('\\', '/').ToLowerInvariant();
                if (IsUiPath(lower))
                    continue;

                if (!(lower.EndsWith(".png") || lower.EndsWith(".jpg") || lower.EndsWith(".jpeg") || lower.EndsWith(".tga")))
                    continue;

                string baseName = Path.GetFileNameWithoutExtension(lower);
                if (MatchesAnyToken(baseName, k_MaskMapTokens))
                    continue;

                if (MatchesAnyToken(baseName, k_LinearDataTokens))
                {
                    string dir = Path.GetDirectoryName(asset)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(dir))
                        dirsToScan.Add(dir);
                }
            }

            if (dirsToScan.Count == 0)
                return;

            foreach (var dir in dirsToScan)
            {
                PackFolderPbrSets(dir, recursive: false, overwriteExisting: false);
            }
        }

        [MenuItem("Cretacea/PBR/Pack All HDRP MaskMaps (Assets/Art & Assets/Textures)")]
        public static void PackAllProjectMaskMaps()
        {
            int packed = 0;
            foreach (var root in new[] { "Assets/Art", "Assets/Textures", "Assets/Models" })
            {
                if (AssetDatabase.IsValidFolder(root))
                    packed += PackFolderPbrSets(root, recursive: true, overwriteExisting: false);
            }
            Debug.Log($"[CretaceaPbrImporter] Packed {packed} HDRP MaskMap(s).");
        }

        /// <summary>
        /// Scans an Assets/ folder for PBR texture sets (Roughness/Metallic/AO or ORM/ARM)
        /// and writes HDRP MaskMaps (R=Metallic, G=AO, B=0, A=Smoothness=1-Roughness).
        /// Returns the number of MaskMaps generated.
        /// </summary>
        public static int PackFolderPbrSets(string assetFolder, bool recursive = true, bool overwriteExisting = false)
        {
            if (string.IsNullOrEmpty(assetFolder) || !assetFolder.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                return 0;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absFolder = Path.Combine(projectRoot, assetFolder);
            if (!Directory.Exists(absFolder))
                return 0;

            var searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var imageFiles = Directory.GetFiles(absFolder, "*.*", searchOpt)
                .Where(f =>
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga";
                })
                .ToList();

            // Group by (directory + material prefix)
            var groups = new Dictionary<string, PbrSetFiles>(StringComparer.OrdinalIgnoreCase);

            foreach (var absFile in imageFiles)
            {
                string dir = Path.GetDirectoryName(absFile);
                string fileName = Path.GetFileNameWithoutExtension(absFile);
                if (!TryExtractPbrRole(fileName, out string prefix, out string role))
                    continue;

                string key = Path.Combine(dir, prefix);
                if (!groups.TryGetValue(key, out var set))
                {
                    set = new PbrSetFiles { Directory = dir, Prefix = prefix };
                    groups[key] = set;
                }

                switch (role)
                {
                    case "metallic": set.MetallicPath = absFile; break;
                    case "ao": set.AoPath = absFile; break;
                    case "roughness": set.RoughnessPath = absFile; break;
                    case "smoothness": set.SmoothnessPath = absFile; break;
                    case "orm": set.OrmPath = absFile; break;
                    case "maskmap": set.MaskMapPath = absFile; break;
                }
            }

            int createdCount = 0;
            var createdAssetPaths = new List<string>();

            s_IsPacking = true;
            try
            {
                foreach (var kv in groups)
                {
                    var set = kv.Value;
                    bool hasSource = !string.IsNullOrEmpty(set.OrmPath) ||
                                     !string.IsNullOrEmpty(set.RoughnessPath) ||
                                     !string.IsNullOrEmpty(set.SmoothnessPath) ||
                                     !string.IsNullOrEmpty(set.MetallicPath) ||
                                     !string.IsNullOrEmpty(set.AoPath);
                    if (!hasSource)
                        continue;

                    string outAbsPath = Path.Combine(set.Directory, set.Prefix + "_MaskMap.png");
                    if (!overwriteExisting && File.Exists(outAbsPath))
                        continue;

                    if (PackMaskMapAbsolute(outAbsPath, set.MetallicPath, set.AoPath, set.RoughnessPath, set.SmoothnessPath, set.OrmPath))
                    {
                        createdCount++;
                        string relPath = "Assets" + outAbsPath.Substring(Application.dataPath.Length).Replace('\\', '/');
                        createdAssetPaths.Add(relPath);
                    }
                }
            }
            finally
            {
                s_IsPacking = false;
            }

            if (createdAssetPaths.Count > 0)
            {
                AssetDatabase.Refresh();
                foreach (var relPath in createdAssetPaths)
                {
                    if (AssetImporter.GetAtPath(relPath) is TextureImporter ti)
                    {
                        ti.textureType = TextureImporterType.Default;
                        ti.sRGBTexture = false;
                        ti.alphaSource = TextureImporterAlphaSource.FromInput;
                        ti.alphaIsTransparency = false;
                        ti.anisoLevel = 8;
                        ti.textureCompression = TextureImporterCompression.CompressedHQ;
                        ti.SaveAndReimport();
                    }
                }
            }

            return createdCount;
        }

        /// <summary>
        /// Packs PBR channel maps into a Unity HDRP MaskMap PNG (R=Metallic, G=AO, B=0, A=Smoothness).
        /// Paths may be absolute or project-relative (Assets/...).
        /// </summary>
        public static bool PackMaskMap(
            string outputAssetOrAbsPath,
            string metallicPath = null,
            string aoPath = null,
            string roughnessPath = null,
            string smoothnessPath = null,
            string ormPath = null,
            float defaultMetallic = 0.0f,
            float defaultSmoothness = 0.5f)
        {
            string absOut = ToAbsolutePath(outputAssetOrAbsPath);
            bool ok = PackMaskMapAbsolute(
                absOut,
                ToAbsolutePath(metallicPath),
                ToAbsolutePath(aoPath),
                ToAbsolutePath(roughnessPath),
                ToAbsolutePath(smoothnessPath),
                ToAbsolutePath(ormPath),
                defaultMetallic,
                defaultSmoothness);

            if (ok && absOut.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
            {
                string rel = "Assets" + absOut.Substring(Application.dataPath.Length).Replace('\\', '/');
                AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate);
            }
            return ok;
        }

        /// <summary>
        /// Creates or updates a standard HDRP/Lit material with BaseColor, MaskMap, NormalMap, and Emissive textures.
        /// </summary>
        public static Material CreateOrUpdateHdrpLitMaterial(
            string materialAssetPath,
            string baseColorAssetPath = null,
            string maskMapAssetPath = null,
            string normalAssetPath = null,
            string emissiveAssetPath = null,
            float metallic = 0f,
            float smoothness = 0.5f,
            float normalScale = 1.0f)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
                throw new InvalidOperationException("HDRP/Lit shader not found.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
            if (mat == null)
            {
                string dir = Path.GetDirectoryName(materialAssetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(ToAbsolutePath(dir));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, materialAssetPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (!string.IsNullOrEmpty(baseColorAssetPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColorAssetPath);
                if (tex != null) mat.SetTexture("_BaseColorMap", tex);
            }

            if (!string.IsNullOrEmpty(maskMapAssetPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskMapAssetPath);
                if (tex != null)
                {
                    mat.SetTexture("_MaskMap", tex);
                    mat.EnableKeyword("_MASKMAP");
                    mat.SetFloat("_MetallicRemapMin", 0f);
                    mat.SetFloat("_MetallicRemapMax", 1f);
                    mat.SetFloat("_SmoothnessRemapMin", 0f);
                    mat.SetFloat("_SmoothnessRemapMax", 1f);
                    mat.SetFloat("_AORemapMin", 0f);
                    mat.SetFloat("_AORemapMax", 1f);
                }
            }
            else
            {
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (!string.IsNullOrEmpty(normalAssetPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalAssetPath);
                if (tex != null)
                {
                    mat.SetTexture("_NormalMap", tex);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetFloat("_NormalScale", normalScale);
                }
            }

            if (!string.IsNullOrEmpty(emissiveAssetPath))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(emissiveAssetPath);
                if (tex != null)
                {
                    mat.SetTexture("_EmissiveColorMap", tex);
                    mat.SetColor("_EmissiveColor", Color.white);
                    mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
                }
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private class PbrSetFiles
        {
            public string Directory;
            public string Prefix;
            public string MetallicPath;
            public string AoPath;
            public string RoughnessPath;
            public string SmoothnessPath;
            public string OrmPath;
            public string MaskMapPath;
        }

        private static bool TryExtractPbrRole(string fileNameWithoutExt, out string prefix, out string role)
        {
            string lower = fileNameWithoutExt.ToLowerInvariant();
            (string suffix, string roleName)[] suffixTable =
            {
                ("_maskmap", "maskmap"),
                ("_mask_map", "maskmap"),
                ("_ambientocclusion", "ao"),
                ("_occlusion", "ao"),
                ("_ao", "ao"),
                ("_roughness", "roughness"),
                ("-roughness", "roughness"),
                ("_rough", "roughness"),
                ("_smoothness", "smoothness"),
                ("_smooth", "smoothness"),
                ("_gloss", "smoothness"),
                ("_metallic", "metallic"),
                ("-metallic", "metallic"),
                ("_metalness", "metallic"),
                ("_metal", "metallic"),
                ("_orm", "orm"),
                ("_arm", "orm")
            };

            foreach (var (suffix, roleName) in suffixTable)
            {
                if (lower.EndsWith(suffix, StringComparison.Ordinal) && fileNameWithoutExt.Length > suffix.Length)
                {
                    prefix = fileNameWithoutExt.Substring(0, fileNameWithoutExt.Length - suffix.Length);
                    role = roleName;
                    return true;
                }
            }

            prefix = null;
            role = null;
            return false;
        }

        private static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            if (Path.IsPathRooted(path))
                return path;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, path);
        }

        private static Texture2D LoadRawTexture(string absPath)
        {
            if (string.IsNullOrEmpty(absPath) || !File.Exists(absPath))
                return null;

            byte[] bytes = File.ReadAllBytes(absPath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
            if (!tex.LoadImage(bytes, markNonReadable: false))
            {
                UnityEngine.Object.DestroyImmediate(tex);
                return null;
            }
            return tex;
        }

        private static bool PackMaskMapAbsolute(
            string absOutPath,
            string metallicPath,
            string aoPath,
            string roughnessPath,
            string smoothnessPath,
            string ormPath,
            float defaultMetallic = 0.0f,
            float defaultSmoothness = 0.5f)
        {
            Texture2D ormTex = LoadRawTexture(ormPath);
            Texture2D metTex = LoadRawTexture(metallicPath);
            Texture2D aoTex = LoadRawTexture(aoPath);
            Texture2D roughTex = LoadRawTexture(roughnessPath);
            Texture2D smoothTex = LoadRawTexture(smoothnessPath);

            try
            {
                int width = 0;
                int height = 0;
                foreach (var t in new[] { ormTex, roughTex, smoothTex, metTex, aoTex })
                {
                    if (t != null)
                    {
                        width = Math.Max(width, t.width);
                        height = Math.Max(height, t.height);
                    }
                }

                if (width <= 0 || height <= 0)
                    return false;

                // Clamp massive 8K inputs to 4K max to keep memory bounded
                width = Math.Min(width, 4096);
                height = Math.Min(height, 4096);

                var outTex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: true);
                try
                {
                    var pixels = new Color32[width * height];
                    Color32[] ormPx = ormTex != null && ormTex.width == width && ormTex.height == height ? ormTex.GetPixels32() : null;
                    Color32[] metPx = metTex != null && metTex.width == width && metTex.height == height ? metTex.GetPixels32() : null;
                    Color32[] aoPx = aoTex != null && aoTex.width == width && aoTex.height == height ? aoTex.GetPixels32() : null;
                    Color32[] roughPx = roughTex != null && roughTex.width == width && roughTex.height == height ? roughTex.GetPixels32() : null;
                    Color32[] smoothPx = smoothTex != null && smoothTex.width == width && smoothTex.height == height ? smoothTex.GetPixels32() : null;

                    byte defMetByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(defaultMetallic) * 255f);
                    byte defSmoothByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(defaultSmoothness) * 255f);

                    for (int y = 0; y < height; y++)
                    {
                        float v = (y + 0.5f) / height;
                        int rowOffset = y * width;
                        for (int x = 0; x < width; x++)
                        {
                            int idx = rowOffset + x;
                            float u = (x + 0.5f) / width;

                            byte rMet = defMetByte;
                            byte gAo = 255;
                            byte aSmooth = defSmoothByte;

                            // glTF/Tripo ORM: R=AO, G=Roughness, B=Metallic
                            if (ormPx != null)
                            {
                                var c = ormPx[idx];
                                gAo = c.r;
                                aSmooth = (byte)(255 - c.g);
                                rMet = c.b;
                            }
                            else if (ormTex != null)
                            {
                                var c = ormTex.GetPixelBilinear(u, v);
                                gAo = (byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f);
                                aSmooth = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - c.g) * 255f);
                                rMet = (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f);
                            }

                            // Dedicated maps override ORM channels if present
                            if (metPx != null) rMet = metPx[idx].r;
                            else if (metTex != null) rMet = (byte)Mathf.RoundToInt(Mathf.Clamp01(metTex.GetPixelBilinear(u, v).r) * 255f);

                            if (aoPx != null) gAo = aoPx[idx].r;
                            else if (aoTex != null) gAo = (byte)Mathf.RoundToInt(Mathf.Clamp01(aoTex.GetPixelBilinear(u, v).r) * 255f);

                            if (smoothPx != null) aSmooth = smoothPx[idx].r;
                            else if (smoothTex != null) aSmooth = (byte)Mathf.RoundToInt(Mathf.Clamp01(smoothTex.GetPixelBilinear(u, v).r) * 255f);
                            else if (roughPx != null) aSmooth = (byte)(255 - roughPx[idx].r);
                            else if (roughTex != null) aSmooth = (byte)Mathf.RoundToInt(Mathf.Clamp01(1f - roughTex.GetPixelBilinear(u, v).r) * 255f);

                            pixels[idx] = new Color32(rMet, gAo, 0, aSmooth);
                        }
                    }

                    outTex.SetPixels32(pixels);
                    outTex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                    string dir = Path.GetDirectoryName(absOutPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    File.WriteAllBytes(absOutPath, outTex.EncodeToPNG());
                    return true;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(outTex);
                }
            }
            finally
            {
                if (ormTex != null) UnityEngine.Object.DestroyImmediate(ormTex);
                if (metTex != null) UnityEngine.Object.DestroyImmediate(metTex);
                if (aoTex != null) UnityEngine.Object.DestroyImmediate(aoTex);
                if (roughTex != null) UnityEngine.Object.DestroyImmediate(roughTex);
                if (smoothTex != null) UnityEngine.Object.DestroyImmediate(smoothTex);
            }
        }
    }
}
