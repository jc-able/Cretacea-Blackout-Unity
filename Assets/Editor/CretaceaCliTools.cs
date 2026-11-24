using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Cretacea.Editor
{
    /// <summary>
    /// Custom Unity CLI [CliCommand] verification and build tools for the Cretacea: Blackout swarm.
    /// Registered under the "cretacea" command group in com.unity.pipeline:
    ///   - unity command cretacea capture -- ...
    ///   - unity command cretacea scene_audit -- ...
    ///   - unity command cretacea navmesh_bake -- ...
    ///   - unity command cretacea pack_pbr -- ...
    /// </summary>
    public static class CretaceaCliTools
    {
        [CliCommand("cretacea", "capture",
            "Capture HDRP GameView or a positioned camera (world pos/euler/look_at/fov/res) to PNG in Edit Mode or Play Mode.",
            MainThreadRequired = true,
            Effect = CommandEffect.ReadOnly)]
        public static string Capture(
            [CliArg("output_path", "Output PNG path (absolute or project-relative, default Screenshots/capture.png)")]
            string outputPath = "Screenshots/capture.png",
            [CliArg("mode", "Capture mode: 'game_view' (active MainCamera) or 'camera' (temporary positioned camera)")]
            string mode = "game_view",
            [CliArg("pos", "World position 'x,y,z' for temporary camera (automatically enables positioned camera capture)")]
            string pos = "",
            [CliArg("euler", "World Euler angles 'pitch,yaw,roll' for temporary camera")]
            string euler = "",
            [CliArg("look_at", "World target 'x,y,z' for temporary camera to look at (overrides euler if set)")]
            string lookAt = "",
            [CliArg("fov", "Vertical field of view in degrees (default 60)")]
            float fov = 60f,
            [CliArg("width", "Image width in pixels (default 1280)")]
            int width = 1280,
            [CliArg("height", "Image height in pixels (default 720)")]
            int height = 720,
            [CliArg("near_clip", "Near clip plane in meters (default 0.05)")]
            float nearClip = 0.05f,
            [CliArg("far_clip", "Far clip plane in meters (default 12000, the game cameras' value)")]
            float farClip = 12000f,
            [CliArg("warmup_frames", "HDRP warmup render passes before PNG readback (default 3)")]
            int warmupFrames = 3)
        {
            width = Mathf.Clamp(width, 64, 3840);
            height = Mathf.Clamp(height, 64, 2160);
            fov = Mathf.Clamp(fov, 10f, 140f);
            warmupFrames = Mathf.Clamp(warmupFrames, 1, 10);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string absOut = Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.Combine(projectRoot, outputPath);

            string outDir = Path.GetDirectoryName(absOut);
            if (!string.IsNullOrEmpty(outDir))
                Directory.CreateDirectory(outDir);

            bool hasExplicitPos = TryParseVector3(pos, out Vector3 parsedPos);
            bool hasExplicitEuler = TryParseVector3(euler, out Vector3 parsedEuler);
            bool hasExplicitLookAt = TryParseVector3(lookAt, out Vector3 parsedLookAt);
            bool useTempCamera = string.Equals(mode, "camera", StringComparison.OrdinalIgnoreCase) ||
                                 hasExplicitPos || hasExplicitEuler || hasExplicitLookAt;

            Camera sourceCam = null;
            GameObject tempCamGo = null;
            bool createdTemp = false;

            try
            {
                if (useTempCamera)
                {
                    tempCamGo = new GameObject("__CretaceaCliCaptureCamera");
                    tempCamGo.hideFlags = HideFlags.HideAndDontSave;
                    sourceCam = tempCamGo.AddComponent<Camera>();
                    // like the gameplay camera: no first-person arm, no rain-occlusion proxies
                    sourceCam.cullingMask &= ~((1 << Cretacea.Core.GameLayers.ViewmodelIndex) | (1 << Cretacea.Core.GameLayers.RainBlockerIndex));
                    var hdData = tempCamGo.AddComponent<HDAdditionalCameraData>();
                    hdData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
                    hdData.volumeLayerMask = ~0;
                    hdData.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    hdData.dithering = true;

                    Camera mainRef = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
                    Vector3 finalPos = hasExplicitPos
                        ? parsedPos
                        : (mainRef != null ? mainRef.transform.position : new Vector3(0f, 1.7f, -4f));

                    tempCamGo.transform.position = finalPos;
                    if (hasExplicitLookAt)
                        tempCamGo.transform.LookAt(parsedLookAt, Vector3.up);
                    else if (hasExplicitEuler)
                        tempCamGo.transform.rotation = Quaternion.Euler(parsedEuler);
                    else if (mainRef != null)
                        tempCamGo.transform.rotation = mainRef.transform.rotation;

                    sourceCam.fieldOfView = fov;
                    sourceCam.nearClipPlane = Mathf.Max(0.01f, nearClip);
                    sourceCam.farClipPlane = Mathf.Max(1f, farClip);
                    createdTemp = true;
                }
                else
                {
                    sourceCam = Camera.main;
                    if (sourceCam == null)
                    {
                        var cams = UnityEngine.Object.FindObjectsByType<Camera>();
                        sourceCam = cams.FirstOrDefault(c => c.isActiveAndEnabled) ?? cams.FirstOrDefault();
                    }
                    if (sourceCam == null)
                        throw new InvalidOperationException("No active Camera found in scene. Pass --mode camera --pos x,y,z to spawn a temporary capture camera.");
                }

                var rtDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, 24)
                {
                    sRGB = true,
                    msaaSamples = 1,
                    useMipMap = false,
                    autoGenerateMips = false
                };

                var rt = new RenderTexture(rtDesc);
                var prevTarget = sourceCam.targetTexture;
                var prevActive = RenderTexture.active;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, mipChain: false, linear: false);

                try
                {
                    rt.Create();
                    sourceCam.targetTexture = rt;

                    // Render warmup passes so HDRP auto-exposure & temporal fog settle
                    for (int i = 0; i < warmupFrames; i++)
                    {
                        sourceCam.Render();
                    }

                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                    byte[] png = tex.EncodeToPNG();
                    File.WriteAllBytes(absOut, png);

                    // Compute basic luma statistics to detect accidental black frames
                    Color32[] pixels = tex.GetPixels32();
                    double lumaSum = 0.0;
                    float maxLuma = 0f;
                    int nonBlack = 0;
                    int stride = Math.Max(1, pixels.Length / 16384);
                    int sampled = 0;
                    for (int i = 0; i < pixels.Length; i += stride)
                    {
                        var c = pixels[i];
                        float l = (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;
                        lumaSum += l;
                        if (l > maxLuma) maxLuma = l;
                        if (l > 0.01f) nonBlack++;
                        sampled++;
                    }

                    var result = new CaptureResult
                    {
                        ok = true,
                        outputPath = absOut,
                        mode = useTempCamera ? "camera" : "game_view",
                        isPlaying = EditorApplication.isPlaying,
                        cameraName = sourceCam.name,
                        cameraPos = FormatVec3(sourceCam.transform.position),
                        cameraEuler = FormatVec3(sourceCam.transform.eulerAngles),
                        fov = sourceCam.fieldOfView,
                        width = width,
                        height = height,
                        bytes = png.Length,
                        meanLuma = sampled > 0 ? (float)(lumaSum / sampled) : 0f,
                        maxLuma = maxLuma,
                        nonBlackPercent = sampled > 0 ? (100f * nonBlack / sampled) : 0f
                    };

                    return JsonUtility.ToJson(result, prettyPrint: false);
                }
                finally
                {
                    sourceCam.targetTexture = prevTarget;
                    RenderTexture.active = prevActive;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt);
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                if (createdTemp && tempCamGo != null)
                    UnityEngine.Object.DestroyImmediate(tempCamGo);
            }
        }

        [CliCommand("cretacea", "scene_audit",
            "Audit active scene(s) for missing scripts, pink/null/non-HDRP materials, door/collider obstructions, lighting/volume & HDRP config, and NavMesh status.",
            MainThreadRequired = true,
            Effect = CommandEffect.ReadOnly)]
        public static string SceneAudit(
            [CliArg("door_clearance_radius", "Half-extent probe depth (meters) on both sides of door objects to detect walls behind doors (default 0.35)")]
            float doorClearanceRadius = 0.35f,
            [CliArg("verbose", "Include detailed per-object diagnostic lists")]
            bool verbose = false)
        {
            var report = new SceneAuditReport
            {
                activeScene = SceneManager.GetActiveScene().path,
                loadedSceneCount = SceneManager.sceneCount,
                isPlaying = EditorApplication.isPlaying
            };

            // 1. HDRP Asset Status
            var rp = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (rp != null)
            {
                var s = rp.currentPlatformRenderPipelineSettings;
                report.hdrpAsset = AssetDatabase.GetAssetPath(rp);
                report.rayTracingEnabled = s.supportRayTracing;
                report.volumetricsEnabled = s.supportVolumetrics;
                report.contactShadowsEnabled = s.hdShadowInitParams.supportContactShadows;
                report.ssgiEnabled = s.supportSSGI;
                report.ssrEnabled = s.supportSSR;
                report.decalsEnabled = s.supportDecals && s.supportDecalLayers;
                report.stpUpscalingEnabled = s.dynamicResolutionSettings.enabled &&
                    s.dynamicResolutionSettings.advancedUpscalerNames != null &&
                    s.dynamicResolutionSettings.advancedUpscalerNames.Contains(AdvancedUpscalers.STP.ToString());
            }
            else
            {
                report.warnings.Add("Active render pipeline is NOT an HDRenderPipelineAsset!");
            }

            report.runInBackground = PlayerSettings.runInBackground;

            // 2. Scan all GameObjects across loaded scenes
            var allObjects = new List<GameObject>();
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var scene = SceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    CollectHierarchy(root, allObjects);
                }
            }
            report.totalGameObjects = allObjects.Count;

            // Missing scripts, pink/null materials, doors, lights, volumes, cameras, colliders
            var doorObjects = new List<GameObject>();
            var activeVolumeTypes = new HashSet<string>();

            foreach (var go in allObjects)
            {
                string path = GetHierarchyPath(go);

                // Missing scripts
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                {
                    report.missingScriptCount += missing;
                    report.missingScriptObjects.Add($"{path} ({missing})");
                }

                // Materials check
                if (go.TryGetComponent<Renderer>(out var rend))
                {
                    report.rendererCount++;
                    var mats = rend.sharedMaterials;
                    if (!rend.enabled)
                    {
                        // never drawn (for example the mesh an HDRP water surface takes its shape from)
                    }
                    else if (mats == null || mats.Length == 0)
                    {
                        report.badMaterialCount++;
                        report.badMaterials.Add($"{path}: no materials assigned");
                    }
                    else
                    {
                        for (int mi = 0; mi < mats.Length; mi++)
                        {
                            var mat = mats[mi];
                            if (mat == null)
                            {
                                report.badMaterialCount++;
                                report.badMaterials.Add($"{path}[{mi}]: null material");
                            }
                            else if (mat.shader == null ||
                                     mat.shader.name == "Hidden/InternalErrorShader" ||
                                     !mat.shader.isSupported)
                            {
                                report.badMaterialCount++;
                                report.badMaterials.Add($"{path}[{mi}]: error/unsupported shader '{mat.shader?.name}'");
                            }
                            else if (mat.shader.name == "Standard" ||
                                     mat.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                            {
                                report.badMaterialCount++;
                                report.badMaterials.Add($"{path}[{mi}]: non-HDRP shader '{mat.shader.name}' (will render pink in HDRP)");
                            }
                        }
                    }
                }

                // Lights check
                if (go.TryGetComponent<Light>(out var light))
                {
                    report.lightCount++;
                    if (light.type == LightType.Directional) report.directionalLightCount++;
                    if (light.enabled && light.intensity < 2.0f && light.type != LightType.Directional &&
                        go.name.IndexOf("Led", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        report.lowIntensityHdrpLights.Add($"{path} ({light.type}, intensity={light.intensity:F2}, unit={light.lightUnit})");
                    }
                }

                // Volumes check
                if (go.TryGetComponent<Volume>(out var vol))
                {
                    report.volumeCount++;
                    var prof = vol.sharedProfile;
                    if (prof != null)
                    {
                        foreach (var comp in prof.components)
                        {
                            if (comp != null && comp.active)
                                activeVolumeTypes.Add(comp.GetType().Name);
                        }
                    }
                }

                // Door/Gate identification: prefer real DoorController components (avoid false positives on roads/posts/trigger boxes)
                if (go.GetComponent("DoorController") != null)
                {
                    doorObjects.Add(go);
                }

                // Negative scale collider check
                if (go.TryGetComponent<Collider>(out var col))
                {
                    report.colliderCount++;
                    Vector3 ls = go.transform.lossyScale;
                    if (ls.x <= 0f || ls.y <= 0f || ls.z <= 0f)
                        report.colliderIssues.Add($"{path}: non-positive scale {FormatVec3(ls)}");
                }
            }

            // Include Global Default Volume Profile components in activeVolumeTypes
            var defVolSettings = GraphicsSettings.GetRenderPipelineSettings<HDRPDefaultVolumeProfileSettings>();
            if (defVolSettings != null && defVolSettings.volumeProfile != null)
            {
                report.defaultVolumeProfile = AssetDatabase.GetAssetPath(defVolSettings.volumeProfile);
                foreach (var comp in defVolSettings.volumeProfile.components)
                {
                    if (comp != null && comp.active)
                        activeVolumeTypes.Add("Default:" + comp.GetType().Name);
                }
            }
            report.activeVolumeOverrides = activeVolumeTypes.OrderBy(x => x).ToList();

            // 3. Door / Wall-Behind-Door Obstruction Probe
            report.doorCount = doorObjects.Count;
            Physics.SyncTransforms();
            foreach (var door in doorObjects)
            {
                Bounds bounds = ComputeHierarchyBounds(door);
                if (bounds.size.sqrMagnitude < 0.0001f)
                    continue;

                Vector3 center = bounds.center;
                Vector3 fwd = door.transform.forward;
                float probeDist = Mathf.Max(0.25f, doorClearanceRadius + 0.15f);
                Vector3 boxHalf = new Vector3(
                    Mathf.Clamp(bounds.extents.x * 0.4f, 0.15f, 0.6f),
                    Mathf.Clamp(bounds.extents.y * 0.4f, 0.25f, 0.8f),
                    0.12f);

                foreach (int dirSign in new[] { -1, 1 })
                {
                    Vector3 probeCenter = center + fwd * (dirSign * probeDist);
                    Collider[] hits = Physics.OverlapBox(probeCenter, boxHalf, door.transform.rotation);
                    foreach (var hit in hits)
                    {
                        if (hit == null || hit.isTrigger) continue;
                        if (hit.transform.IsChildOf(door.transform) || door.transform.IsChildOf(hit.transform)) continue;
                        string hitName = hit.gameObject.name.ToLowerInvariant();
                        if (hitName.Contains("floor") || hitName.Contains("ground") || hitName.Contains("terrain") || hitName.Contains("frame"))
                            continue;

                        report.doorObstructions.Add(
                            $"{GetHierarchyPath(door)} blocked on {(dirSign > 0 ? "+fwd" : "-fwd")} ({probeDist:F2}m) by {GetHierarchyPath(hit.gameObject)}");
                    }
                }
            }

            // 4. Camera & NavMesh Status
            report.hasMainCamera = Camera.main != null;
            report.cameraCount = UnityEngine.Object.FindObjectsByType<Camera>().Length;
            var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>();
            report.navMeshSurfaceCount = surfaces.Length;
            var tri = NavMesh.CalculateTriangulation();
            report.navMeshVertices = tri.vertices != null ? tri.vertices.Length : 0;
            report.navMeshTriangles = tri.indices != null ? tri.indices.Length / 3 : 0;

            if (!verbose)
            {
                if (report.activeVolumeOverrides.Count > 15)
                {
                    report.activeVolumeOverrides = report.activeVolumeOverrides
                        .Where(n => n.Contains("Fog") || n.Contains("Tonemapping") || n.Contains("Exposure") ||
                                    n.Contains("GlobalIllumination") || n.Contains("ScreenSpaceReflection") ||
                                    n.Contains("ContactShadows") || n.Contains("AmbientOcclusion"))
                        .ToList();
                }
            }

            report.healthy = report.missingScriptCount == 0 &&
                             report.badMaterialCount == 0 &&
                             report.doorObstructions.Count == 0 &&
                             report.colliderIssues.Count == 0 &&
                             !report.rayTracingEnabled &&
                             !string.IsNullOrEmpty(report.hdrpAsset);

            return JsonUtility.ToJson(report, prettyPrint: false);
        }

        [CliCommand("cretacea", "navmesh_bake",
            "Bake all NavMeshSurface components in the active scene(s) and report resulting NavMesh triangulation counts.",
            MainThreadRequired = true,
            Effect = CommandEffect.Idempotent)]
        public static string NavMeshBake(
            [CliArg("surface_name", "Optional GameObject name filter to bake only matching NavMeshSurface(s)")]
            string surfaceName = "",
            [CliArg("create_if_missing", "If true and no NavMeshSurface exists, creates a root NavMeshSurface and bakes it")]
            bool createIfMissing = false)
        {
            var surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>().ToList();

            if (!string.IsNullOrEmpty(surfaceName))
            {
                surfaces = surfaces
                    .Where(s => s.gameObject.name.IndexOf(surfaceName, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            if (surfaces.Count == 0 && createIfMissing)
            {
                var go = new GameObject("NavMesh_Surface");
                var surface = go.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.All;
                surfaces.Add(surface);
                EditorSceneManager.MarkSceneDirty(go.scene);
            }

            var bakedNames = new List<string>();
            foreach (var surface in surfaces)
            {
                if (surface == null) continue;
                surface.BuildNavMesh();
                EditorUtility.SetDirty(surface);
                EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
                bakedNames.Add(GetHierarchyPath(surface.gameObject));
            }

            var tri = NavMesh.CalculateTriangulation();
            var result = new NavMeshBakeResult
            {
                ok = true,
                bakedSurfaceCount = bakedNames.Count,
                bakedSurfaces = bakedNames,
                navMeshVertices = tri.vertices != null ? tri.vertices.Length : 0,
                navMeshTriangles = tri.indices != null ? tri.indices.Length / 3 : 0
            };

            return JsonUtility.ToJson(result, prettyPrint: false);
        }

        [CliCommand("cretacea", "pack_pbr",
            "Pack PBR Metallic/AO/Roughness/ORM textures in an Assets/ folder into HDRP MaskMaps (R=Metallic, G=AO, B=0, A=Smoothness).",
            MainThreadRequired = true,
            Effect = CommandEffect.Additive)]
        public static string PackPbr(
            [CliArg("folder", "Project-relative folder under Assets/ (default Assets/Art)")]
            string folder = "Assets/Art",
            [CliArg("recursive", "Scan subdirectories recursively (default true)")]
            bool recursive = true,
            [CliArg("overwrite", "Overwrite existing *_MaskMap.png files (default false)")]
            bool overwrite = false)
        {
            int count = CretaceaPbrImporter.PackFolderPbrSets(folder, recursive, overwrite);
            return $"{{\"ok\":true,\"folder\":\"{folder}\",\"packedMaskMaps\":{count}}}";
        }

        private static bool TryParseVector3(string csv, out Vector3 v)
        {
            v = Vector3.zero;
            if (string.IsNullOrWhiteSpace(csv)) return false;
            var parts = csv.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return false;
            if (float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
            {
                v = new Vector3(x, y, z);
                return true;
            }
            return false;
        }

        private static string FormatVec3(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", v.x, v.y, v.z);
        }

        private static void CollectHierarchy(GameObject go, List<GameObject> list)
        {
            list.Add(go);
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
                CollectHierarchy(t.GetChild(i).gameObject, list);
        }

        private static string GetHierarchyPath(GameObject go)
        {
            if (go == null) return "<null>";
            var stack = new Stack<string>();
            Transform cur = go.transform;
            while (cur != null)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", stack);
        }

        private static Bounds ComputeHierarchyBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                return b;
            }
            var colliders = go.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds b = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
                return b;
            }
            return new Bounds(go.transform.position, Vector3.zero);
        }

        [Serializable]
        private class CaptureResult
        {
            public bool ok;
            public string outputPath;
            public string mode;
            public bool isPlaying;
            public string cameraName;
            public string cameraPos;
            public string cameraEuler;
            public float fov;
            public int width;
            public int height;
            public int bytes;
            public float meanLuma;
            public float maxLuma;
            public float nonBlackPercent;
        }

        [Serializable]
        private class SceneAuditReport
        {
            public bool healthy;
            public string activeScene;
            public int loadedSceneCount;
            public bool isPlaying;
            public string hdrpAsset;
            public string defaultVolumeProfile;
            public bool rayTracingEnabled;
            public bool volumetricsEnabled;
            public bool contactShadowsEnabled;
            public bool ssgiEnabled;
            public bool ssrEnabled;
            public bool decalsEnabled;
            public bool stpUpscalingEnabled;
            public bool runInBackground;
            public int totalGameObjects;
            public int rendererCount;
            public int colliderCount;
            public int lightCount;
            public int directionalLightCount;
            public int volumeCount;
            public int doorCount;
            public bool hasMainCamera;
            public int cameraCount;
            public int navMeshSurfaceCount;
            public int navMeshVertices;
            public int navMeshTriangles;
            public int missingScriptCount;
            public int badMaterialCount;
            public List<string> activeVolumeOverrides = new List<string>();
            public List<string> missingScriptObjects = new List<string>();
            public List<string> badMaterials = new List<string>();
            public List<string> doorObstructions = new List<string>();
            public List<string> colliderIssues = new List<string>();
            public List<string> lowIntensityHdrpLights = new List<string>();
            public List<string> warnings = new List<string>();
        }

        [Serializable]
        private class NavMeshBakeResult
        {
            public bool ok;
            public int bakedSurfaceCount;
            public List<string> bakedSurfaces = new List<string>();
            public int navMeshVertices;
            public int navMeshTriangles;
        }
    }
}
