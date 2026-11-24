using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Cretacea.Editor.UI
{
    /// <summary>
    /// Generates the TextMeshPro SDF font assets the runtime UI loads from Resources/UI/Fonts, from the OFL TTFs in
    /// Assets/Art/UI/Fonts. Static atlases (no runtime growth, so no asset churn in git) with a character set that
    /// covers every string in the game, real bold weights wired into the weight tables, LiberationSans as fallback,
    /// and one "Shadow" material preset per font (soft black underlay) so text holds over bright lamps.
    ///
    ///   unity command cretacea build_ui_fonts
    /// </summary>
    public static class CretaceaFontAssets
    {
        public const string OutputFolder = "Assets/Resources/UI/Fonts";
        private const string SourceFolder = "Assets/Art/UI/Fonts";
        private const string FallbackPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        private struct Spec
        {
            public string Ttf;       // path under SourceFolder
            public string Name;      // asset name (loaders use "UI/Fonts/<Name>")
            public int Sampling;     // sampling point size
            public int Padding;      // SDF spread in atlas pixels
            public int Width, Height;
            public Spec(string ttf, string name, int sampling, int padding, int w, int h)
            { Ttf = ttf; Name = name; Sampling = sampling; Padding = padding; Width = w; Height = h; }
        }

        // Headings / labels (Barlow Condensed), body (Barlow), terminals & documents (Courier Prime),
        // handwritten notes (Caveat, Kalam), stencilled titles (Stardos Stencil).
        private static readonly Spec[] Specs =
        {
            new Spec("BarlowCondensed/BarlowCondensed-Bold.ttf",     "BarlowCondensed-Bold SDF",     84, 9, 2048, 1024),
            new Spec("BarlowCondensed/BarlowCondensed-SemiBold.ttf", "BarlowCondensed-SemiBold SDF", 64, 7, 1024, 1024),
            new Spec("Barlow/Barlow-Regular.ttf",                    "Barlow-Regular SDF",           60, 7, 1024, 1024),
            new Spec("Barlow/Barlow-Medium.ttf",                     "Barlow-Medium SDF",            60, 7, 1024, 1024),
            new Spec("Barlow/Barlow-SemiBold.ttf",                   "Barlow-SemiBold SDF",          60, 7, 1024, 1024),
            new Spec("Barlow/Barlow-Bold.ttf",                       "Barlow-Bold SDF",              60, 7, 1024, 1024),
            new Spec("Barlow/Barlow-Italic.ttf",                     "Barlow-Italic SDF",            60, 7, 1024, 1024),
            new Spec("CourierPrime/CourierPrime-Regular.ttf",        "CourierPrime-Regular SDF",     56, 7, 1024, 1024),
            new Spec("CourierPrime/CourierPrime-Bold.ttf",           "CourierPrime-Bold SDF",        56, 7, 1024, 1024),
            new Spec("Caveat/Caveat-VariableFont_wght.ttf",          "Caveat-Regular SDF",           64, 7, 1024, 1024),
            new Spec("Kalam/Kalam-Regular.ttf",                      "Kalam-Regular SDF",            56, 7, 1024, 1024),
            new Spec("StardosStencil/StardosStencil-Bold.ttf",       "StardosStencil-Bold SDF",      84, 9, 2048, 1024),
        };

        // (regular, bold, italic) — the bold/italic assets are wired into the regular asset's weight table so
        // <b>/FontStyles.Bold use the real bold cut instead of TMP's faux bold.
        private static readonly string[][] WeightLinks =
        {
            new[] { "Barlow-Regular SDF", "Barlow-Bold SDF", "Barlow-Italic SDF" },
            new[] { "Barlow-Medium SDF", "Barlow-Bold SDF", null },
            new[] { "BarlowCondensed-SemiBold SDF", "BarlowCondensed-Bold SDF", null },
            new[] { "CourierPrime-Regular SDF", "CourierPrime-Bold SDF", null },
        };

        /// <summary>Every character the game's UI, documents and subtitles can show.</summary>
        public static string CharacterSet()
        {
            var sb = new StringBuilder();
            for (int c = 32; c < 127; c++) sb.Append((char)c);
            for (int c = 160; c < 256; c++) sb.Append((char)c);
            sb.Append("–—‘’‚“”„†•…‰′″‹›");
            sb.Append("€™←↑→↓−×№ŒœŠšŸŽžƒ");
            return sb.ToString();
        }

        [MenuItem("Cretacea/UI/Build Font Assets")]
        public static void BuildMenu() => Debug.Log("[UI Fonts] " + BuildAll());

        [CliCommand("cretacea", "build_ui_fonts",
            "Generate the TextMeshPro SDF font assets (Barlow, Barlow Condensed, Courier Prime, Caveat, Kalam, Stardos Stencil) in Assets/Resources/UI/Fonts.",
            MainThreadRequired = true)]
        public static string BuildCli() => BuildAll();

        public static string BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources/UI")) AssetDatabase.CreateFolder("Assets/Resources", "UI");
            if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder("Assets/Resources/UI", "Fonts");

            TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackPath);
            string chars = CharacterSet();
            var log = new List<string>();
            var built = new Dictionary<string, TMP_FontAsset>();

            foreach (Spec spec in Specs)
            {
                string src = $"{SourceFolder}/{spec.Ttf}";
                Font font = AssetDatabase.LoadAssetAtPath<Font>(src);
                if (font == null) { log.Add($"MISSING {src}"); continue; }
                TMP_FontAsset fa = BuildOne(font, spec, chars, fallback, out string report);
                if (fa != null) built[spec.Name] = fa;
                log.Add(report);
            }

            foreach (string[] link in WeightLinks)
            {
                if (!built.TryGetValue(link[0], out TMP_FontAsset regular)) continue;
                TMP_FontWeightPair[] table = regular.fontWeightTable;
                if (table == null || table.Length < 10) continue;
                if (link[1] != null && built.TryGetValue(link[1], out TMP_FontAsset bold))
                {
                    table[7].regularTypeface = bold;
                    if (link[2] != null && built.TryGetValue(link[2], out TMP_FontAsset it)) table[4].italicTypeface = it;
                }
                EditorUtility.SetDirty(regular); // the getter returns the serialized array itself
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return string.Join("\n", log);
        }

        private static TMP_FontAsset BuildOne(Font font, Spec spec, string chars, TMP_FontAsset fallback, out string report)
        {
            string path = $"{OutputFolder}/{spec.Name}.asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, spec.Sampling, spec.Padding, GlyphRenderMode.SDFAA,
                spec.Width, spec.Height, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            if (fa == null) { report = $"FAILED {spec.Name}"; return null; }
            fa.name = spec.Name;

            fa.TryAddCharacters(chars, out string missing);
            fa.atlasPopulationMode = AtlasPopulationMode.Static;
            fa.fallbackFontAssetTable = fallback != null ? new List<TMP_FontAsset> { fallback } : new List<TMP_FontAsset>();

            AssetDatabase.CreateAsset(fa, path);
            Texture2D[] atlases = fa.atlasTextures;
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] == null) continue;
                atlases[i].name = $"{spec.Name} Atlas{(i > 0 ? " " + i : "")}";
                AssetDatabase.AddObjectToAsset(atlases[i], fa);
            }
            fa.material.name = $"{spec.Name} Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();

            WriteShadowPreset(fa, spec.Name);

            string missingVisible = new string((missing ?? "").Where(c => c > 32 && c != 160 && c != 173).ToArray());
            report = $"{spec.Name}: {fa.characterTable.Count} chars, {atlases.Length} atlas(es) {spec.Width}x{spec.Height}" +
                     (missingVisible.Length > 0 ? $", not in font (fallback): {missingVisible}" : "");
            return fa;
        }

        /// <summary>"Name Shadow.mat": the font's own material plus a soft black underlay (and a thin dark outline).</summary>
        private static void WriteShadowPreset(TMP_FontAsset fa, string name)
        {
            string matPath = $"{OutputFolder}/{name} Shadow.mat";
            var mat = new Material(fa.material) { name = $"{name} Shadow" };
            mat.SetTexture(ShaderUtilities.ID_MainTex, fa.atlasTexture);
            mat.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            mat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.72f));
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.35f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.55f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.25f);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.45f);
            if (File.Exists(matPath)) AssetDatabase.DeleteAsset(matPath);
            AssetDatabase.CreateAsset(mat, matPath);
        }
    }
}
