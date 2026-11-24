using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Project-wide render settings the look depends on (HDRP assets of every quality level):
    /// water system on (ocean + stream), a cached shadow atlas large enough for every lamp to cast shadows,
    /// a cookie atlas that fits the flashlight cookie at full size, 32-bit sun shadow depth.
    /// </summary>
    public static class LookRenderSettings
    {
        private static readonly string[] HdrpAssets =
        {
            "Assets/Settings/HDRP High Fidelity.asset",
            "Assets/Settings/HDRP Balanced.asset",
            "Assets/Settings/HDRP Performant.asset",
            "Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset",
        };

        public static string Apply()
        {
            int changed = 0;
            foreach (string path in HdrpAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
                if (asset == null) continue;
                bool high = path.Contains("High Fidelity");
                var so = new SerializedObject(asset);
                SerializedProperty rp = so.FindProperty("m_RenderPipelineSettings");
                bool dirty = false;
                dirty |= SetBool(rp, "supportWater", true);
                dirty |= SetInt(rp, "waterSimulationResolution", high ? 256 : 128);
                dirty |= SetBool(rp, "supportVolumetricClouds", true);
                dirty |= SetBool(rp, "supportVolumetrics", true);
                dirty |= SetInt(rp, "hdShadowInitParams.cachedPunctualLightShadowAtlas", high ? 8192 : 4096);
                dirty |= SetInt(rp, "hdShadowInitParams.directionalShadowsDepthBits", 32);
                dirty |= SetInt(rp, "hdShadowInitParams.maxShadowRequests", 256);
                dirty |= SetInt(rp, "lightLoopSettings.cookieAtlasSize", 2048);
                dirty |= SetInt(rp, "lightLoopSettings.maxLightsPerClusterCell", 64);   // at 24 lit rooms dropped lights in screen blocks
                // GPU Resident Drawer (Play mode and builds): instanced drawing of the static world plus GPU occlusion
                // culling from the depth buffer, so rooms hide the jungle behind their walls without any baking.
                // Measured Nov 20 at the substation: 5.75 M triangles per frame -> 1.65 M, no visual difference.
                dirty |= SetInt(rp, "gpuResidentDrawerSettings.mode", 1);     // GPUResidentDrawerMode.InstancedDrawing
                dirty |= SetBool(rp, "gpuResidentDrawerSettings.enableOcclusionCullingInCameras", true);
                if (dirty)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed++;
                }
            }
            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                // HDRP rebuilds its pipeline when an asset changes and fails to dispose the old one in a live editor
                // ("Blitter is already initialized" every frame). A script reload brings it back cleanly.
                EditorUtility.RequestScriptReload();
            }
            return $"hdrp assets updated={changed}" + (changed > 0 ? " (script reload requested)" : "");
        }

        private static bool SetBool(SerializedProperty root, string rel, bool v)
        {
            SerializedProperty p = root.FindPropertyRelative(rel);
            if (p == null) { Debug.LogWarning("[Look] HDRP setting not found: " + rel); return false; }
            if (p.boolValue == v) return false;
            p.boolValue = v;
            return true;
        }

        private static bool SetInt(SerializedProperty root, string rel, int v)
        {
            SerializedProperty p = root.FindPropertyRelative(rel);
            if (p == null) { Debug.LogWarning("[Look] HDRP setting not found: " + rel); return false; }
            if (p.intValue == v) return false;
            p.intValue = v;
            return true;
        }
    }
}
