using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Unity.Pipeline.Commands;
using Cretacea.Core;
using Cretacea.AI;

namespace Cretacea.EditorTools
{
    /// <summary>
    /// Automated Unity 6 Editor pipeline & [CliCommand] building AAA dinosaur AnimatorControllers,
    /// DinoConfig ScriptableObjects, HDRP Lit + SSS Materials, and ready-to-drop prefabs under
    /// Assets/Prefabs/Dinosaurs/ directly from dino-rigger FBXs + dino_manifest.json / metadata.json.
    ///
    /// CLI Invocation:
    ///   unity command cretacea build_dino_prefabs --project-path ~/CretaceaBlackoutUnity -- --overwrite true
    /// </summary>
    public static class DinoAnimatorBuilder
    {
        private const string PrefabOutputDir = "Assets/Prefabs/Dinosaurs";
        // Look pass: controllers live next to the prefabs that use them (Assets/Animations/Dinosaurs keeps the handover
        // versions untouched; nothing else references them).
        private const string AnimatorOutputDir = "Assets/Prefabs/Dinosaurs/Controllers";
        private const string ConfigOutputDir = "Assets/ScriptableObjects/AI";
        private const string MaterialOutputDir = "Assets/Art/Materials/Dinosaurs";

        private static readonly string[] DefaultSourceSearchDirs =
        {
            "Assets/Art/Dinosaurs",
            "Assets/Art/Models/Dinosaurs"
        };

        // =========================================================================
        // CLI COMMAND & MENU ENTRY POINTS
        // =========================================================================

        [CliCommand(
            "cretacea",
            "build_dino_prefabs",
            "Reads dino_manifest.json / per-species metadata.json and imported FBXs under Assets/Art/Dinosaurs (or Assets/Art/Models/Dinosaurs), configures Generic rigs & clip loops/events, builds velocity-matched AnimatorControllers & HDRP materials, and saves prefabs under Assets/Prefabs/Dinosaurs/.",
            MainThreadRequired = true)]
        public static string BuildDinoPrefabsCli(
            [CliArg("source_dir", "Optional Unity Assets path containing dinosaur FBX folders (auto-detects Assets/Art/Dinosaurs and Assets/Art/Models/Dinosaurs if empty)")]
            string sourceDir = "",
            [CliArg("overwrite", "Overwrite existing AnimatorControllers, DinoConfigs, and Prefabs")]
            bool overwrite = true)
        {
            return BuildAllDinosaurs(sourceDir, overwrite);
        }

        [MenuItem("Cretacea/Dinosaurs/Build Dinosaur Prefabs & Animators")]
        public static void BuildDinoPrefabsMenu()
        {
            string report = BuildAllDinosaurs(string.Empty, overwrite: true);
            Debug.Log($"[DinoAnimatorBuilder] Complete:\n{report}");
        }

        // =========================================================================
        // CORE PIPELINE
        // =========================================================================

        public static string BuildAllDinosaurs(string sourceDirOverride, bool overwrite)
        {
            EnsureFolderExists(PrefabOutputDir);
            EnsureFolderExists(AnimatorOutputDir);
            EnsureFolderExists(ConfigOutputDir);
            EnsureFolderExists(MaterialOutputDir);

            List<string> searchRoots = ResolveSearchRoots(sourceDirOverride);
            Dictionary<DinoSpeciesType, ManifestSpeciesEntry> entries = DiscoverSpeciesAssets(searchRoots);

            var builtPrefabs = new Dictionary<DinoSpeciesType, GameObject>();
            var builtConfigs = new Dictionary<DinoSpeciesType, DinoConfig>();
            var reportLines = new List<string>();
            int successCount = 0;

            foreach (DinoSpeciesType species in new[]
            {
                DinoSpeciesType.Velociraptor,
                DinoSpeciesType.Tyrannosaurus,
                DinoSpeciesType.Brachiosaurus,
                DinoSpeciesType.Triceratops
            })
            {
                if (!entries.TryGetValue(species, out ManifestSpeciesEntry entry) || string.IsNullOrEmpty(entry.FbxAssetPath))
                {
                    reportLines.Add($"  \"skip_{species}\": \"No FBX found under searched roots ({string.Join(", ", searchRoots)})\"");
                    continue;
                }

                try
                {
                    DinoConfig configAsset = BuildOrUpdateConfigAsset(species, entry, overwrite);
                    ConfigureModelImporter(entry.FbxAssetPath, configAsset, entry);
                    Dictionary<string, AnimationClip> clips = LoadImportedClips(entry.FbxAssetPath);
                    AnimatorController controller = BuildAnimatorController(species, configAsset, clips, overwrite);
                    Material mat = BuildOrUpdateHdrpMaterial(species, entry, overwrite);
                    GameObject prefab = BuildDinoPrefab(species, entry.FbxAssetPath, configAsset, controller, mat, overwrite);

                    builtPrefabs[species] = prefab;
                    builtConfigs[species] = configAsset;
                    successCount++;

                    string prefabPath = AssetDatabase.GetAssetPath(prefab);
                    string ctrlPath = AssetDatabase.GetAssetPath(controller);
                    reportLines.Add($"  \"{species}\": {{\"fbx\": \"{entry.FbxAssetPath}\", \"clips\": {clips.Count}, \"controller\": \"{ctrlPath}\", \"prefab\": \"{prefabPath}\"}}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[DinoAnimatorBuilder] Failed building {species}: {ex}");
                    reportLines.Add($"  \"error_{species}\": \"{EscapeJson(ex.Message)}\"");
                }
            }

            // Wire open scene's DinoSpawner automatically if present
            DinoSpawner sceneSpawner = UnityEngine.Object.FindAnyObjectByType<DinoSpawner>();
            bool wiredSceneSpawner = false;
            if (sceneSpawner != null && builtPrefabs.Count > 0)
            {
                WireSpawnerPrefabs(sceneSpawner, builtPrefabs, builtConfigs);
                wiredSceneSpawner = true;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"built_count\": {successCount},");
            sb.AppendLine($"  \"wired_scene_spawner\": {(wiredSceneSpawner ? "true" : "false")},");
            for (int i = 0; i < reportLines.Count; i++)
            {
                sb.Append(reportLines[i]);
                if (i < reportLines.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.Append("}");
            return sb.ToString();
        }

        // =========================================================================
        // 1. MANIFEST & FBX DISCOVERY
        // =========================================================================

        private sealed class ManifestClipEntry
        {
            public string Name;
            public float DurationSeconds;
            public bool Loop;
            public float StrideSpeedMps;
            public float HitOrImpactTimeSeconds = -1f;
        }

        private sealed class ManifestSpeciesEntry
        {
            public DinoSpeciesType SpeciesType;
            public string SpeciesFolderAssetPath;
            public string FbxAssetPath;
            public string AlbedoAssetPath;
            public string NormalAssetPath;
            public string MaskMapAssetPath;
            public readonly Dictionary<string, ManifestClipEntry> Clips = new Dictionary<string, ManifestClipEntry>(StringComparer.OrdinalIgnoreCase);
            public string RootBone;
            public string PelvisBone;
            public string HeadBone;
            public string JawBone;
            public string[] SpineBones;
            public string[] NeckBones;
            public string[] TailBones;
            public string LeftFootBone;
            public string RightFootBone;
            public string LeftFrontFootBone;
            public string RightFrontFootBone;
        }

        private static List<string> ResolveSearchRoots(string sourceDirOverride)
        {
            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(sourceDirOverride))
            {
                string clean = sourceDirOverride.Replace('\\', '/').TrimEnd('/');
                if (AssetDatabase.IsValidFolder(clean)) roots.Add(clean);
            }

            for (int i = 0; i < DefaultSourceSearchDirs.Length; i++)
            {
                string candidate = DefaultSourceSearchDirs[i];
                if (AssetDatabase.IsValidFolder(candidate) && !roots.Contains(candidate))
                    roots.Add(candidate);
            }

            if (roots.Count == 0) roots.Add("Assets");
            return roots;
        }

        private static Dictionary<DinoSpeciesType, ManifestSpeciesEntry> DiscoverSpeciesAssets(List<string> searchRoots)
        {
            var map = new Dictionary<DinoSpeciesType, ManifestSpeciesEntry>();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            // Pass A: Parse any dino_manifest.json or per-species metadata.json found inside search roots
            for (int r = 0; r < searchRoots.Count; r++)
            {
                string absRoot = Path.Combine(projectRoot, searchRoots[r]);
                if (!Directory.Exists(absRoot)) continue;

                string[] manifestFiles = Directory.GetFiles(absRoot, "*manifest*.json", SearchOption.AllDirectories);
                for (int i = 0; i < manifestFiles.Length; i++)
                {
                    TryParseManifestJson(manifestFiles[i], projectRoot, map);
                }

                string[] metaFiles = Directory.GetFiles(absRoot, "metadata.json", SearchOption.AllDirectories);
                for (int i = 0; i < metaFiles.Length; i++)
                {
                    TryParseSingleMetadataJson(metaFiles[i], projectRoot, map);
                }
            }

            // Pass B: Direct FBX discovery fallback for any species lacking JSON metadata
            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", searchRoots.ToArray());
            for (int i = 0; i < fbxGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) continue;
                string lower = assetPath.ToLowerInvariant();
                if (lower.Contains("skeleton") || lower.Contains("fossil")) continue;

                if (!TryIdentifySpeciesFromPath(lower, out DinoSpeciesType sp)) continue;

                if (!map.TryGetValue(sp, out ManifestSpeciesEntry entry))
                {
                    entry = new ManifestSpeciesEntry { SpeciesType = sp };
                    map[sp] = entry;
                }

                if (string.IsNullOrEmpty(entry.FbxAssetPath))
                {
                    entry.FbxAssetPath = assetPath;
                    entry.SpeciesFolderAssetPath = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                }
            }

            // Pass C: Resolve textures for every discovered entry
            foreach (KeyValuePair<DinoSpeciesType, ManifestSpeciesEntry> kv in map)
            {
                ResolveTexturesForEntry(kv.Value);
            }

            return map;
        }

        private static bool TryIdentifySpeciesFromPath(string lowerPath, out DinoSpeciesType species)
        {
            if (lowerPath.Contains("raptor") || lowerPath.Contains("velociraptor")) { species = DinoSpeciesType.Velociraptor; return true; }
            if (lowerPath.Contains("trex") || lowerPath.Contains("tyrannosaurus")) { species = DinoSpeciesType.Tyrannosaurus; return true; }
            if (lowerPath.Contains("brachiosaurus") || lowerPath.Contains("sauropod")) { species = DinoSpeciesType.Brachiosaurus; return true; }
            if (lowerPath.Contains("triceratops") || lowerPath.Contains("trike")) { species = DinoSpeciesType.Triceratops; return true; }
            species = DinoSpeciesType.Velociraptor;
            return false;
        }

        private static void TryParseManifestJson(string absJsonPath, string projectRoot, Dictionary<DinoSpeciesType, ManifestSpeciesEntry> map)
        {
            try
            {
                object parsed = MiniJson.Deserialize(File.ReadAllText(absJsonPath));
                if (!(parsed is Dictionary<string, object> rootObj)) return;

                string manifestDir = Path.GetDirectoryName(absJsonPath);

                // Supports {"assets": {"raptor": ...}} (canonical dino-rigger dino_manifest.json),
                // {"creatures": ...}, {"dinosaurs": ...}, {"species": ...}, or top-level keyed species map
                if (TryGetDict(rootObj, "assets", out Dictionary<string, object> dict)
                    || TryGetDict(rootObj, "creatures", out dict)
                    || TryGetDict(rootObj, "dinosaurs", out dict)
                    || TryGetDict(rootObj, "species", out dict))
                {
                    foreach (KeyValuePair<string, object> kv in dict)
                    {
                        if (kv.Value is Dictionary<string, object> item)
                        {
                            if (!item.ContainsKey("species")) item["species"] = kv.Key;
                            string speciesSubDir = Path.Combine(manifestDir, kv.Key);
                            string effectiveDir = Directory.Exists(speciesSubDir) ? speciesSubDir : manifestDir;
                            ConsumeSpeciesDict(item, effectiveDir, projectRoot, map);
                        }
                    }
                    return;
                }

                if (TryGetList(rootObj, "assets", out List<object> list)
                    || TryGetList(rootObj, "creatures", out list)
                    || TryGetList(rootObj, "dinosaurs", out list)
                    || TryGetList(rootObj, "species", out list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] is Dictionary<string, object> item)
                            ConsumeSpeciesDict(item, manifestDir, projectRoot, map);
                    }
                    return;
                }

                foreach (KeyValuePair<string, object> kv in rootObj)
                {
                    if (kv.Value is Dictionary<string, object> item && TryIdentifySpeciesFromPath(kv.Key.ToLowerInvariant(), out _))
                    {
                        if (!item.ContainsKey("species")) item["species"] = kv.Key;
                        string speciesSubDir = Path.Combine(manifestDir, kv.Key);
                        string effectiveDir = Directory.Exists(speciesSubDir) ? speciesSubDir : manifestDir;
                        ConsumeSpeciesDict(item, effectiveDir, projectRoot, map);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DinoAnimatorBuilder] Failed parsing manifest '{absJsonPath}': {ex.Message}");
            }
        }

        private static void TryParseSingleMetadataJson(string absJsonPath, string projectRoot, Dictionary<DinoSpeciesType, ManifestSpeciesEntry> map)
        {
            try
            {
                object parsed = MiniJson.Deserialize(File.ReadAllText(absJsonPath));
                if (parsed is Dictionary<string, object> dict)
                {
                    ConsumeSpeciesDict(dict, Path.GetDirectoryName(absJsonPath), projectRoot, map);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DinoAnimatorBuilder] Failed parsing metadata '{absJsonPath}': {ex.Message}");
            }
        }

        private static void ConsumeSpeciesDict(
            Dictionary<string, object> dict,
            string baseAbsDir,
            string projectRoot,
            Dictionary<DinoSpeciesType, ManifestSpeciesEntry> map)
        {
            string rawSpecies = GetString(dict, "species") ?? GetString(dict, "scientific_name") ?? GetString(dict, "name");
            if (string.IsNullOrWhiteSpace(rawSpecies)) return;
            string lower = rawSpecies.ToLowerInvariant();
            if (lower.Contains("skeleton") || lower.Contains("fossil")) return;
            if (!TryIdentifySpeciesFromPath(lower, out DinoSpeciesType sp)) return;

            if (!map.TryGetValue(sp, out ManifestSpeciesEntry entry))
            {
                entry = new ManifestSpeciesEntry { SpeciesType = sp };
                map[sp] = entry;
            }

            string fbxRel = GetString(dict, "fbx") ?? GetString(dict, "model") ?? GetString(dict, "fbx_path");
            if (!string.IsNullOrWhiteSpace(fbxRel))
            {
                string resolvedFbx = ResolveAssetPathFromRelative(baseAbsDir, fbxRel, projectRoot)
                                     ?? ResolveAssetPathFromRelative(Path.Combine(baseAbsDir, rawSpecies), fbxRel, projectRoot);
                if (!string.IsNullOrEmpty(resolvedFbx))
                {
                    entry.FbxAssetPath = resolvedFbx;
                    entry.SpeciesFolderAssetPath = Path.GetDirectoryName(resolvedFbx)?.Replace('\\', '/');
                }
            }

            if (string.IsNullOrEmpty(entry.SpeciesFolderAssetPath) && !string.IsNullOrEmpty(baseAbsDir))
            {
                entry.SpeciesFolderAssetPath = ToAssetRelativePath(baseAbsDir, projectRoot);
            }

            // Parse textures block
            if (TryGetDict(dict, "textures", out Dictionary<string, object> texDict))
            {
                string texSub = GetString(dict, "textures_dir") ?? "Textures";
                string texAbsBase = string.IsNullOrEmpty(baseAbsDir) ? projectRoot : Path.Combine(baseAbsDir, texSub);

                string alb = GetString(texDict, "albedo");
                string nrm = GetString(texDict, "normal");
                string msk = GetString(texDict, "hdrp_mask_map") ?? GetString(texDict, "mask_map") ?? GetString(texDict, "maskmap");

                if (!string.IsNullOrEmpty(alb)) entry.AlbedoAssetPath = ResolveAssetPathFromRelative(texAbsBase, alb, projectRoot) ?? ResolveAssetPathFromRelative(baseAbsDir, alb, projectRoot);
                if (!string.IsNullOrEmpty(nrm)) entry.NormalAssetPath = ResolveAssetPathFromRelative(texAbsBase, nrm, projectRoot) ?? ResolveAssetPathFromRelative(baseAbsDir, nrm, projectRoot);
                if (!string.IsNullOrEmpty(msk)) entry.MaskMapAssetPath = ResolveAssetPathFromRelative(texAbsBase, msk, projectRoot) ?? ResolveAssetPathFromRelative(baseAbsDir, msk, projectRoot);
            }

            // Parse key_bones / bones block
            if (TryGetDict(dict, "key_bones", out Dictionary<string, object> bonesDict)
                || TryGetDict(dict, "bones", out bonesDict))
            {
                entry.RootBone = GetString(bonesDict, "root") ?? entry.RootBone;
                entry.PelvisBone = GetString(bonesDict, "pelvis") ?? entry.PelvisBone;
                entry.HeadBone = GetString(bonesDict, "head") ?? entry.HeadBone;
                entry.JawBone = GetString(bonesDict, "jaw") ?? entry.JawBone;
                entry.LeftFootBone = GetString(bonesDict, "left_foot") ?? entry.LeftFootBone;
                entry.RightFootBone = GetString(bonesDict, "right_foot") ?? entry.RightFootBone;
                entry.LeftFrontFootBone = GetString(bonesDict, "left_front_foot") ?? entry.LeftFrontFootBone;
                entry.RightFrontFootBone = GetString(bonesDict, "right_front_foot") ?? entry.RightFrontFootBone;
                entry.SpineBones = GetStringArray(bonesDict, "spine") ?? entry.SpineBones;
                entry.NeckBones = GetStringArray(bonesDict, "neck") ?? entry.NeckBones;
                entry.TailBones = GetStringArray(bonesDict, "tail") ?? entry.TailBones;
            }

            // Parse clips array
            if (TryGetList(dict, "clips", out List<object> clipsList))
            {
                for (int i = 0; i < clipsList.Count; i++)
                {
                    if (!(clipsList[i] is Dictionary<string, object> cdict)) continue;
                    string cname = GetString(cdict, "name");
                    if (string.IsNullOrWhiteSpace(cname)) continue;

                    var ce = new ManifestClipEntry
                    {
                        Name = cname,
                        DurationSeconds = GetFloat(cdict, "duration_s", GetFloat(cdict, "duration", 1.5f)),
                        Loop = GetBool(cdict, "loop", true),
                        StrideSpeedMps = GetFloat(cdict, "stride_speed_mps", GetFloat(cdict, "natural_speed_mps", 0f)),
                        HitOrImpactTimeSeconds = GetFloat(cdict, "hit_time_s", GetFloat(cdict, "impact_time_s", -1f))
                    };
                    entry.Clips[cname] = ce;
                }
            }
        }

        private static void ResolveTexturesForEntry(ManifestSpeciesEntry entry)
        {
            if (string.IsNullOrEmpty(entry.SpeciesFolderAssetPath) || !AssetDatabase.IsValidFolder(entry.SpeciesFolderAssetPath))
                return;

            string[] texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { entry.SpeciesFolderAssetPath });
            for (int i = 0; i < texGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(texGuids[i]);
                string fn = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                if (string.IsNullOrEmpty(entry.AlbedoAssetPath) && (fn.Contains("albedo") || fn.Contains("basecolor") || fn.Contains("diffuse")))
                    entry.AlbedoAssetPath = path;
                else if (string.IsNullOrEmpty(entry.NormalAssetPath) && fn.Contains("normal"))
                    entry.NormalAssetPath = path;
                else if (string.IsNullOrEmpty(entry.MaskMapAssetPath) && fn.Contains("maskmap"))
                    entry.MaskMapAssetPath = path;
            }
        }

        // =========================================================================
        // 2. DINOCONFIG SCRIPTABLEOBJECT BUILDER
        // =========================================================================

        private static DinoConfig BuildOrUpdateConfigAsset(DinoSpeciesType species, ManifestSpeciesEntry entry, bool overwrite)
        {
            string assetPath = $"{ConfigOutputDir}/DinoConfig_{species}.asset";
            DinoConfig cfg = AssetDatabase.LoadAssetAtPath<DinoConfig>(assetPath);
            if (cfg == null)
            {
                cfg = DinoConfig.CreateDefault(species);
                AssetDatabase.CreateAsset(cfg, assetPath);
            }
            else if (overwrite)
            {
                DinoConfig.ApplyDefaults(cfg, species);
            }

            // Overlay exact manifest/metadata bone names
            DinoRigOverrides ov = cfg.rigOverrides;
            if (!string.IsNullOrEmpty(entry.RootBone)) ov.rootBone = entry.RootBone;
            if (!string.IsNullOrEmpty(entry.PelvisBone)) ov.pelvisBone = entry.PelvisBone;
            if (!string.IsNullOrEmpty(entry.HeadBone)) ov.headBone = entry.HeadBone;
            if (!string.IsNullOrEmpty(entry.JawBone)) ov.jawBone = entry.JawBone;
            if (!string.IsNullOrEmpty(entry.LeftFootBone)) ov.leftFootBone = entry.LeftFootBone;
            if (!string.IsNullOrEmpty(entry.RightFootBone)) ov.rightFootBone = entry.RightFootBone;
            if (!string.IsNullOrEmpty(entry.LeftFrontFootBone)) ov.leftFrontFootBone = entry.LeftFrontFootBone;
            if (!string.IsNullOrEmpty(entry.RightFrontFootBone)) ov.rightFrontFootBone = entry.RightFrontFootBone;
            if (entry.SpineBones != null && entry.SpineBones.Length > 0) ov.spineBones = entry.SpineBones;
            if (entry.NeckBones != null && entry.NeckBones.Length > 0) ov.neckBones = entry.NeckBones;
            if (entry.TailBones != null && entry.TailBones.Length > 0) ov.tailBones = entry.TailBones;
            cfg.rigOverrides = ov;

            // Overlay exact manifest/metadata stride speeds, durations, loops & hit frames
            if (entry.Clips.Count > 0 && cfg.clipSpecs != null)
            {
                for (int i = 0; i < cfg.clipSpecs.Length; i++)
                {
                    DinoClipSpec spec = cfg.clipSpecs[i];
                    if (entry.Clips.TryGetValue(spec.clipName, out ManifestClipEntry mc))
                    {
                        spec.durationSeconds = mc.DurationSeconds;
                        spec.loop = mc.Loop;
                        if (mc.StrideSpeedMps > 0.01f) spec.naturalStrideSpeed = mc.StrideSpeedMps;
                        if (mc.HitOrImpactTimeSeconds > 0f) spec.hitOrImpactTimeSeconds = mc.HitOrImpactTimeSeconds;
                        cfg.clipSpecs[i] = spec;

                        if (string.Equals(spec.clipName, cfg.stalkClipName, StringComparison.OrdinalIgnoreCase) && mc.StrideSpeedMps > 0.01f)
                            cfg.stalkClipStrideSpeed = mc.StrideSpeedMps;
                        if (string.Equals(spec.clipName, cfg.walkClipName, StringComparison.OrdinalIgnoreCase) && mc.StrideSpeedMps > 0.01f)
                            cfg.walkClipStrideSpeed = mc.StrideSpeedMps;
                        if (string.Equals(spec.clipName, cfg.runClipName, StringComparison.OrdinalIgnoreCase) && mc.StrideSpeedMps > 0.01f)
                            cfg.runClipStrideSpeed = mc.StrideSpeedMps;
                        if (string.Equals(spec.clipName, cfg.biteClipName, StringComparison.OrdinalIgnoreCase) && mc.HitOrImpactTimeSeconds > 0f)
                            cfg.attackHitDelaySeconds = mc.HitOrImpactTimeSeconds;
                    }
                }
            }

            ApplyAnimationPlaybackLimits(species, cfg);

            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        /// <summary>
        /// Playback-rate limits so the feet keep up with the NavMesh speed (measured stance-foot speeds match the
        /// manifest stride speeds within 1 %). Stalk speeds sit at 0.68-0.77 x the walk stride and the trike/brachio
        /// run on their walk clip at up to 2.5 x / 1.5 x, so the old 0.72 floor and 1.25-1.38 ceilings made them skate.
        /// </summary>
        private static void ApplyAnimationPlaybackLimits(DinoSpeciesType species, DinoConfig cfg)
        {
            cfg.minAnimSpeedScale = 0.55f;
            float walkStride = Mathf.Max(0.2f, cfg.walkClipStrideSpeed);
            float topClipStride = Mathf.Max(walkStride, cfg.runClipStrideSpeed);
            bool sharedClip = string.Equals(cfg.walkClipName, cfg.runClipName, StringComparison.OrdinalIgnoreCase);
            float needed = sharedClip ? cfg.runSpeed / walkStride : cfg.runSpeed / topClipStride;
            cfg.maxAnimSpeedScale = Mathf.Clamp(Mathf.Max(cfg.maxAnimSpeedScale, needed * 1.03f), 1.0f, 2.6f);
        }

        // =========================================================================
        // 3. FBX MODEL IMPORTER (GENERIC RIG, CLEAN CLIP NAMES, LOOPS & EVENTS)
        // =========================================================================

        private static void ConfigureModelImporter(string fbxAssetPath, DinoConfig cfg, ManifestSpeciesEntry entry)
        {
            ModelImporter importer = AssetImporter.GetAtPath(fbxAssetPath) as ModelImporter;
            if (importer == null) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.animationCompression = ModelImporterAnimationCompression.Off;
            importer.resampleCurves = true;
            importer.importBlendShapes = true;
            importer.isReadable = true;
            // Mesh LOD (importer.generateMeshLods / maximumMeshLod) would be the cheap LOD path, but the FBX import
            // settings belong to the art owner: requested from the lead in docs/handin/creatures.md, not set here.

            ModelImporterClipAnimation[] sourceClips = importer.defaultClipAnimations;
            if (sourceClips != null && sourceClips.Length > 0)
            {
                var configuredClips = new ModelImporterClipAnimation[sourceClips.Length];
                for (int i = 0; i < sourceClips.Length; i++)
                {
                    ModelImporterClipAnimation clip = sourceClips[i];
                    string cleanName = StripArmaturePrefix(clip.takeName);
                    if (string.IsNullOrEmpty(cleanName)) cleanName = StripArmaturePrefix(clip.name);
                    clip.name = cleanName;

                    bool hasSpec = cfg.TryGetClipSpec(cleanName, out DinoClipSpec spec);
                    bool hasManifest = entry.Clips.TryGetValue(cleanName, out ManifestClipEntry mc);

                    bool shouldLoop = hasManifest ? mc.Loop : (hasSpec ? spec.loop : InferLoopFromClipName(cleanName));
                    clip.loopTime = shouldLoop;
                    clip.loopPose = shouldLoop;

                    // Lock root transform in-place so NavMeshAgent has sole authority over ground translation
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;

                    // Embed frame-exact AnimationEvents for footsteps, bite hits, and fence bash impacts
                    clip.events = BuildClipAnimationEvents(cleanName, cfg, hasSpec ? spec : default, hasManifest ? mc : null);
                    configuredClips[i] = clip;
                }

                importer.clipAnimations = configuredClips;
            }

            importer.SaveAndReimport();
        }

        private static AnimationEvent[] BuildClipAnimationEvents(
            string clipName,
            DinoConfig cfg,
            DinoClipSpec spec,
            ManifestClipEntry mc)
        {
            var events = new List<AnimationEvent>();
            float duration = mc != null && mc.DurationSeconds > 0.05f
                ? mc.DurationSeconds
                : (spec.durationSeconds > 0.05f ? spec.durationSeconds : 1.0f);

            // Footstep events on stride clips
            if (spec.footstepNormalizedTimes != null && spec.footstepNormalizedTimes.Length > 0)
            {
                bool isQuad = cfg.rigOverrides.isQuadruped;
                for (int i = 0; i < spec.footstepNormalizedTimes.Length; i++)
                {
                    string fnName;
                    if (isQuad)
                    {
                        fnName = (i % 4) switch
                        {
                            0 => nameof(DinoControllerBase.OnFootstepFrontLeft),
                            1 => nameof(DinoControllerBase.OnFootstepRight),
                            2 => nameof(DinoControllerBase.OnFootstepFrontRight),
                            _ => nameof(DinoControllerBase.OnFootstepLeft)
                        };
                    }
                    else
                    {
                        fnName = (i % 2 == 0)
                            ? nameof(DinoControllerBase.OnFootstepLeft)
                            : nameof(DinoControllerBase.OnFootstepRight);
                    }

                    events.Add(new AnimationEvent
                    {
                        functionName = fnName,
                        time = Mathf.Clamp01(spec.footstepNormalizedTimes[i])
                    });
                }
            }

            // Bite hit event
            string lower = clipName.ToLowerInvariant();
            float hitTime = mc != null && mc.HitOrImpactTimeSeconds > 0f
                ? mc.HitOrImpactTimeSeconds
                : spec.hitOrImpactTimeSeconds;

            if ((lower.Contains("bite") || lower.Contains("lunge") || lower.Contains("snap")) && hitTime > 0f)
            {
                events.Add(new AnimationEvent
                {
                    functionName = nameof(DinoControllerBase.OnBiteHitFrame),
                    time = Mathf.Clamp01(hitTime / duration)
                });
            }
            else if ((lower.Contains("bash") || lower.Contains("fence")) && hitTime > 0f)
            {
                events.Add(new AnimationEvent
                {
                    functionName = nameof(DinoControllerBase.OnBashImpactFrame),
                    time = Mathf.Clamp01(hitTime / duration)
                });
            }

            return events.ToArray();
        }

        private static Dictionary<string, AnimationClip> LoadImportedClips(string fbxAssetPath)
        {
            var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
            for (int i = 0; i < subAssets.Length; i++)
            {
                if (subAssets[i] is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    clips[clip.name] = clip;
                }
            }
            return clips;
        }

        // =========================================================================
        // 4. VELOCITY-MATCHED ANIMATOR CONTROLLER BUILDER
        // =========================================================================

        private static AnimatorController BuildAnimatorController(
            DinoSpeciesType species,
            DinoConfig cfg,
            Dictionary<string, AnimationClip> clips,
            bool overwrite)
        {
            string path = $"{AnimatorOutputDir}/AC_{species}.controller";
            if (!overwrite)
            {
                AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (existing != null) return existing;
            }
            else if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Standardized parameters matching DinoControllerBase hashes
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "LocomotionSpeedMult",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1.0f
            });
            controller.AddParameter("TurnAngle", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsTurning", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAlert", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsHunting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsStalking", AnimatorControllerParameterType.Bool);
            controller.AddParameter("TriggerRoar", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TriggerBite", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TriggerInspect", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TriggerBash", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TurnLeft", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("TurnRight", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // 1D Velocity-Matched Locomotion Blend Tree (Idle -> Walk/Stalk -> Run/Charge)
            AnimatorState locoState = sm.AddState("Locomotion", new Vector3(260f, 120f, 0f));
            BlendTree blendTree = new BlendTree
            {
                name = "LocomotionBlendTree",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(blendTree, controller);

            AnimationClip idleClip = FindClip(clips, cfg.idleClipName, "idle");
            AnimationClip walkClip = FindClip(clips, cfg.walkClipName, "walk", "stalk");
            AnimationClip runClip = FindClip(clips, cfg.runClipName, "run", "sprint", "charge");

            if (idleClip != null)
                blendTree.AddChild(idleClip, 0.0f);
            if (walkClip != null && walkClip != idleClip)
            {
                // Pure walk from minAnimSpeedScale x stride up (played slower by LocomotionSpeedMult), so stalking at
                // 0.7 x stride is a slow walk with planted feet instead of a 30 % idle blend that skates.
                float walkStride = Mathf.Max(0.5f, cfg.walkClipStrideSpeed);
                float slowWalk = Mathf.Max(0.25f, walkStride * Mathf.Clamp(cfg.minAnimSpeedScale, 0.3f, 0.95f));
                blendTree.AddChild(walkClip, slowWalk);
                blendTree.AddChild(walkClip, walkStride);
            }
            if (runClip != null && runClip != walkClip && runClip != idleClip)
                blendTree.AddChild(runClip, Mathf.Max(cfg.walkClipStrideSpeed + 0.5f, cfg.runClipStrideSpeed));

            locoState.motion = blendTree;
            locoState.speed = 1.0f;
            locoState.speedParameterActive = true;
            locoState.speedParameter = "LocomotionSpeedMult";
            sm.defaultState = locoState;

            // Turn-In-Place states (Velociraptor Turn_Left / Turn_Right)
            AnimationClip turnLeftClip = FindClip(clips, cfg.turnLeftClipName, "turn_left");
            AnimationClip turnRightClip = FindClip(clips, cfg.turnRightClipName, "turn_right");
            AddTurnInPlaceState(sm, locoState, "Turn_Left", turnLeftClip, "TurnLeft", new Vector3(260f, 250f, 0f));
            AddTurnInPlaceState(sm, locoState, "Turn_Right", turnRightClip, "TurnRight", new Vector3(260f, 320f, 0f));

            // One-shot / Action States triggered via AnyState (never self-interrupting)
            AnimationClip roarClip = FindClip(clips, cfg.roarClipName, "roar", "call", "snort");
            AnimationClip biteClip = FindClip(clips, cfg.biteClipName, "bite", "lunge", "snap");
            AnimationClip inspectClip = FindClip(clips, cfg.inspectClipName, "sniff", "inspect", "scan");
            AnimationClip bashClip = FindClip(clips, cfg.bashClipName, "fence_bash", "bash", "bite");

            AddActionState(sm, locoState, "Roar", roarClip, "TriggerRoar", 0.14f, new Vector3(540f, 20f, 0f));
            AddActionState(sm, locoState, "Bite", biteClip, "TriggerBite", 0.08f, new Vector3(540f, 95f, 0f));
            AddActionState(sm, locoState, "Inspect", inspectClip, "TriggerInspect", 0.18f, new Vector3(540f, 170f, 0f));
            AddActionState(sm, locoState, "Bash", bashClip, "TriggerBash", 0.10f, new Vector3(540f, 245f, 0f));

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddTurnInPlaceState(
            AnimatorStateMachine sm,
            AnimatorState locoState,
            string stateName,
            AnimationClip clip,
            string triggerParam,
            Vector3 pos)
        {
            if (clip == null) return;
            AnimatorState st = sm.AddState(stateName, pos);
            st.motion = clip;

            AnimatorStateTransition enter = locoState.AddTransition(st);
            enter.hasExitTime = false;
            enter.duration = 0.12f;
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);

            AnimatorStateTransition exitDone = st.AddTransition(locoState);
            exitDone.hasExitTime = true;
            exitDone.exitTime = 0.88f;
            exitDone.duration = 0.15f;

            AnimatorStateTransition exitMoving = st.AddTransition(locoState);
            exitMoving.hasExitTime = false;
            exitMoving.duration = 0.12f;
            exitMoving.AddCondition(AnimatorConditionMode.Greater, 0.55f, "Speed");

            AnimatorStateTransition exitStoppedTurning = st.AddTransition(locoState);
            exitStoppedTurning.hasExitTime = false;
            exitStoppedTurning.duration = 0.15f;
            exitStoppedTurning.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTurning");
        }

        private static void AddActionState(
            AnimatorStateMachine sm,
            AnimatorState locoState,
            string stateName,
            AnimationClip clip,
            string triggerParam,
            float transitionDuration,
            Vector3 pos)
        {
            if (clip == null) return;
            AnimatorState st = sm.AddState(stateName, pos);
            st.motion = clip;

            AnimatorStateTransition anyEnter = sm.AddAnyStateTransition(st);
            anyEnter.hasExitTime = false;
            anyEnter.canTransitionToSelf = false;
            anyEnter.duration = transitionDuration;
            anyEnter.AddCondition(AnimatorConditionMode.If, 0f, triggerParam);

            AnimatorStateTransition exit = st.AddTransition(locoState);
            exit.hasExitTime = true;
            exit.exitTime = 0.90f;
            exit.duration = 0.18f;
        }

        private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, string exactName, params string[] fuzzyTokens)
        {
            if (!string.IsNullOrEmpty(exactName))
            {
                if (clips.TryGetValue(exactName, out AnimationClip exact))
                    return exact;

                // Handle Blender NLA suffix format "<Species>_Armature|<ClipName>" cleanly
                string pipeSuffix = "|" + exactName;
                foreach (KeyValuePair<string, AnimationClip> kv in clips)
                {
                    if (kv.Key.EndsWith(pipeSuffix, StringComparison.OrdinalIgnoreCase))
                        return kv.Value;
                }
            }

            if (fuzzyTokens != null)
            {
                for (int t = 0; t < fuzzyTokens.Length; t++)
                {
                    foreach (KeyValuePair<string, AnimationClip> kv in clips)
                    {
                        if (kv.Key.ToLowerInvariant().Contains(fuzzyTokens[t]))
                            return kv.Value;
                    }
                }
            }
            return null;
        }

        // =========================================================================
        // 5. HDRP LIT + SSS MATERIAL BUILDER
        // =========================================================================

        private static Material BuildOrUpdateHdrpMaterial(DinoSpeciesType species, ManifestSpeciesEntry entry, bool overwrite)
        {
            string matPath = $"{MaterialOutputDir}/MAT_{species}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null && !overwrite) return mat;

            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            else
            {
                mat.shader = shader;
            }

            // Ensure proper texture import types (NormalMap / linear MaskMap) before binding
            ConfigureTextureImporter(entry.NormalAssetPath, isNormal: true, isLinear: true);
            ConfigureTextureImporter(entry.MaskMapAssetPath, isNormal: false, isLinear: true);

            Texture2D albedo = LoadTexture(entry.AlbedoAssetPath);
            // Look pass: night-safe derived albedo (hide.py: linear luminance soft-capped near 0.25; the raptor's cream
            // chest was 0.43 and blew out in the flashlight). sRGB colour texture.
            string hideAlbedoPath = DerivedTexturePath(entry.MaskMapAssetPath, "_Hide_Albedo");
            if (!string.IsNullOrEmpty(hideAlbedoPath))
            {
                Texture2D hideAlbedo = LoadTexture(hideAlbedoPath);
                if (hideAlbedo != null) albedo = hideAlbedo;
            }
            Texture2D normal = LoadTexture(entry.NormalAssetPath);
            Texture2D maskMap = LoadTexture(entry.MaskMapAssetPath);

            if (albedo != null)
            {
                if (mat.HasProperty("_BaseColorMap")) mat.SetTexture("_BaseColorMap", albedo);
                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }

            if (normal != null)
            {
                if (mat.HasProperty("_NormalMap"))
                {
                    mat.SetTexture("_NormalMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                }
                else if (mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
            }

            // Look pass: a derived hide mask (generator: Assets/Art/Materials/Dinosaurs/Tools~/hide.py) replaces the source
            // mask map when present. R metallic = 0, G cavity/fold AO, B detail mask (0 on eyes/teeth/horn/claws),
            // A smoothness with real variation (matte scales 0.25-0.35, folds 0.5-0.6, keratin 0.5, eyes 0.93).
            string hideMaskPath = DerivedTexturePath(entry.MaskMapAssetPath, "_Hide_MaskMap");
            string sssMaskPath = DerivedTexturePath(entry.MaskMapAssetPath, "_SSS_Mask");
            if (!string.IsNullOrEmpty(hideMaskPath))
            {
                ConfigureTextureImporter(hideMaskPath, isNormal: false, isLinear: true);
                Texture2D hideMask = LoadTexture(hideMaskPath);
                if (hideMask != null) maskMap = hideMask;
            }

            if (maskMap != null && mat.HasProperty("_MaskMap"))
            {
                mat.SetTexture("_MaskMap", maskMap);
                mat.EnableKeyword("_MASKMAP");
                bool derived = maskMap.name.EndsWith("_Hide_MaskMap", StringComparison.Ordinal);
                // Smoothness lives in the derived mask; the wet look is added per instance by DinoWetness.
                SetFloatIfPresent(mat, "_SmoothnessRemapMin", derived ? 0.0f : 0.12f);
                SetFloatIfPresent(mat, "_SmoothnessRemapMax", derived ? 1.0f : 0.62f);
                SetFloatIfPresent(mat, "_AORemapMin", derived ? 0.0f : 0.35f);
                SetFloatIfPresent(mat, "_AORemapMax", 1.0f);
            }

            // Hide is never metal (the source masks leak up to 1.0 on the brachiosaur).
            SetFloatIfPresent(mat, "_Metallic", 0f);
            SetFloatIfPresent(mat, "_MetallicRemapMin", 0f);
            SetFloatIfPresent(mat, "_MetallicRemapMax", 0f);
            SetFloatIfPresent(mat, "_SpecularOcclusionMode", 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);

            // Tiling scale-pattern detail map for close-ups (masked off eyes/teeth/claws by the mask map's B channel).
            Texture2D detail = LoadTexture(DetailMapPath);
            if (detail != null && mat.HasProperty("_DetailMap"))
            {
                ConfigureTextureImporter(DetailMapPath, isNormal: false, isLinear: true);
                mat.SetTexture("_DetailMap", detail);
                float tiling = DetailTilingFor(species);
                mat.SetTextureScale("_DetailMap", new Vector2(tiling, tiling));
                SetFloatIfPresent(mat, "_DetailAlbedoScale", 0.35f);
                SetFloatIfPresent(mat, "_DetailNormalScale", 0.45f);
                SetFloatIfPresent(mat, "_DetailSmoothnessScale", 0.5f);
                mat.EnableKeyword("_DETAIL_MAP");
            }

            // Bind optional SSS_DinoSkin Diffusion Profile cleanly via HDRP GUID + Hash properties when present
            string[] sssGuids = AssetDatabase.FindAssets("SSS_DinoSkin");
            if (sssGuids != null && sssGuids.Length > 0 && mat.HasProperty("_MaterialID")
                && mat.HasProperty("_DiffusionProfileAsset") && mat.HasProperty("_DiffusionProfileHash"))
            {
                string sssGuid = sssGuids[0];
                string sssPath = AssetDatabase.GUIDToAssetPath(sssGuid);
                UnityEngine.Object sssAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(sssPath);
                if (sssAsset != null && TryBindHdrpDiffusionProfile(mat, sssAsset, sssGuid))
                {
                    mat.SetFloat("_MaterialID", 0f);
                    mat.EnableKeyword("_MATERIAL_FEATURE_SUBSURFACE_SCATTERING");
                    // Skin scatters, teeth/claws/horn/eyes do not: the mask map carries the split, the scalar is a cap.
                    Texture2D sssMask = string.IsNullOrEmpty(sssMaskPath) ? null : LoadTexture(sssMaskPath);
                    if (sssMask != null && mat.HasProperty("_SubsurfaceMaskMap"))
                    {
                        ConfigureTextureImporter(sssMaskPath, isNormal: false, isLinear: true);
                        mat.SetTexture("_SubsurfaceMaskMap", sssMask);
                        mat.EnableKeyword("_SUBSURFACE_MASK_MAP");
                        SetFloatIfPresent(mat, "_SubsurfaceMask", 0.55f);
                    }
                    else
                    {
                        SetFloatIfPresent(mat, "_SubsurfaceMask", 0.35f);
                    }
                    // Thick creatures: transmission only through thin parts (thickness 0.85 of the profile's range).
                    SetFloatIfPresent(mat, "_Thickness", 0.85f);
                }
            }

            // Eyeshine: the emissive map is an eye-only mask; the colour stays black and DinoEyeShine drives it per
            // instance (MaterialPropertyBlock) from the flashlight.
            string eyeShinePath = DerivedTexturePath(entry.MaskMapAssetPath, "_EyeShine_Emissive");
            Texture2D eyeShine = string.IsNullOrEmpty(eyeShinePath) ? null : LoadTexture(eyeShinePath);
            if (eyeShine != null && mat.HasProperty("_EmissiveColorMap"))
            {
                mat.SetTexture("_EmissiveColorMap", eyeShine);
                if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", Color.black);
                if (mat.HasProperty("_EmissiveColorLDR")) mat.SetColor("_EmissiveColorLDR", Color.black);
                SetFloatIfPresent(mat, "_EmissiveExposureWeight", 1f);
                SetFloatIfPresent(mat, "_AlbedoAffectEmissive", 0f);
            }

            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private const string DetailMapPath = MaterialOutputDir + "/T_DinoScaleDetail_mask.png";

        /// <summary>Detail tiles per UV unit so one scale is ~1.1 cm on the raptor, ~3 cm on the big species
        /// (measured texel density: raptor 3.3 m, T. rex 11.7 m, trike 8.6 m, brachio 17.3 m per UV unit; 22 scales per tile).</summary>
        private static float DetailTilingFor(DinoSpeciesType species)
        {
            switch (species)
            {
                case DinoSpeciesType.Tyrannosaurus: return 18f;
                case DinoSpeciesType.Triceratops: return 13f;
                case DinoSpeciesType.Brachiosaurus: return 15f;
                default: return 14f;
            }
        }

        private static string DerivedTexturePath(string maskMapAssetPath, string suffix)
        {
            if (string.IsNullOrEmpty(maskMapAssetPath)) return null;
            string dir = Path.GetDirectoryName(maskMapAssetPath)?.Replace('\\', '/');
            string stem = Path.GetFileNameWithoutExtension(maskMapAssetPath);
            if (stem.EndsWith("_Hide_MaskMap", StringComparison.Ordinal)) stem = stem.Substring(0, stem.Length - "_Hide_MaskMap".Length);
            else if (stem.EndsWith("_MaskMap", StringComparison.Ordinal)) stem = stem.Substring(0, stem.Length - "_MaskMap".Length);
            string p = $"{dir}/{stem}{suffix}.png";
            if (!File.Exists(p)) return null;
            if (AssetImporter.GetAtPath(p) == null) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceSynchronousImport);
            return p;
        }

        private static void SetFloatIfPresent(Material mat, string prop, float value)
        {
            if (mat.HasProperty(prop)) mat.SetFloat(prop, value);
        }

        private static bool TryBindHdrpDiffusionProfile(Material mat, UnityEngine.Object profileAsset, string guid)
        {
            try
            {
                Type profileType = profileAsset.GetType();
                System.Reflection.FieldInfo profileField = profileType.GetField("profile",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                object innerProfile = profileField?.GetValue(profileAsset);
                if (innerProfile == null) return false;

                System.Reflection.FieldInfo hashField = innerProfile.GetType().GetField("hash",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (hashField == null) return false;

                uint hashVal = Convert.ToUInt32(hashField.GetValue(innerProfile));
                float hashAsFloat = BitConverter.ToSingle(BitConverter.GetBytes(hashVal), 0);

                // Encode 32-hex-char GUID into Vector4 matching HDUtils.ConvertGUIDToVector4
                if (string.IsNullOrEmpty(guid) || guid.Length != 32) return false;
                byte[] bytes = new byte[16];
                for (int i = 0; i < 16; i++)
                {
                    bytes[i] = Convert.ToByte(guid.Substring(i * 2, 2), 16);
                }
                Vector4 guidVec = new Vector4(
                    BitConverter.ToSingle(bytes, 0),
                    BitConverter.ToSingle(bytes, 4),
                    BitConverter.ToSingle(bytes, 8),
                    BitConverter.ToSingle(bytes, 12));

                mat.SetVector("_DiffusionProfileAsset", guidVec);
                mat.SetFloat("_DiffusionProfileHash", hashAsFloat);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ConfigureTextureImporter(string assetPath, bool isNormal, bool isLinear)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti == null) return;

            bool dirty = false;
            if (isNormal && ti.textureType != TextureImporterType.NormalMap)
            {
                ti.textureType = TextureImporterType.NormalMap;
                dirty = true;
            }
            if (isLinear && ti.sRGBTexture)
            {
                ti.sRGBTexture = false;
                dirty = true;
            }
            if (dirty) ti.SaveAndReimport();
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // =========================================================================
        // 6. READY-TO-DROP PREFAB ASSEMBLY
        // =========================================================================

        private static GameObject BuildDinoPrefab(
            DinoSpeciesType species,
            string fbxAssetPath,
            DinoConfig cfg,
            AnimatorController controller,
            Material material,
            bool overwrite)
        {
            string prefabPath = $"{PrefabOutputDir}/PF_{species}.prefab";
            if (!overwrite)
            {
                GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null) return existing;
            }

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxAssetPath);
            if (fbxAsset == null)
            {
                throw new InvalidOperationException($"Could not load FBX GameObject at '{fbxAssetPath}'.");
            }
            // AI behaviour is not this builder's to change: the rebuild re-adds the species AI with code defaults, so copy
            // the previous prefab's tuned values back (e.g. TrexAI substationOrbitRadius 26.5 vs the code's 30.5).
            GameObject previousPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            DinoControllerBase previousAi = previousPrefab != null ? previousPrefab.GetComponent<DinoControllerBase>() : null;

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = $"PF_{species}";
                GameLayers.SetLayerRecursively(instance, GameLayers.Dinosaur);

                // Apply HDRP material and prevent offscreen frustum-culling pops
                SkinnedMeshRenderer[] smrs = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int i = 0; i < smrs.Length; i++)
                {
                    smrs[i].updateWhenOffscreen = true;
                    if (material != null)
                    {
                        Material[] mats = new Material[Mathf.Max(1, smrs[i].sharedMaterials.Length)];
                        for (int m = 0; m < mats.Length; m++) mats[m] = material;
                        smrs[i].sharedMaterials = mats;
                    }
                }

                // Animator directly on root so FBX Animation Events hit DinoControllerBase cleanly
                Animator anim = instance.GetComponent<Animator>();
                if (anim == null) anim = instance.AddComponent<Animator>();
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                // NavMeshAgent configured with species agentTypeID & bounds
                NavMeshAgent nav = instance.GetComponent<NavMeshAgent>();
                if (nav == null) nav = instance.AddComponent<NavMeshAgent>();
                nav.agentTypeID = DinoControllerBase.ResolveNavMeshAgentTypeId(cfg.navMeshAgentTypeName);
                nav.radius = cfg.agentRadius;
                nav.height = cfg.agentHeight;
                nav.speed = cfg.walkSpeed;
                nav.angularSpeed = cfg.turnSpeedDegPerSec;
                nav.acceleration = cfg.acceleration;
                nav.stoppingDistance = cfg.stoppingDistance;
                nav.updateRotation = false;

                // Physics CapsuleCollider on Dinosaur layer
                CapsuleCollider col = instance.GetComponent<CapsuleCollider>();
                if (col == null) col = instance.AddComponent<CapsuleCollider>();
                col.radius = cfg.agentRadius;
                col.height = cfg.agentHeight;
                col.center = new Vector3(0f, cfg.agentHeight * 0.5f, 0f);

                // 3D Spatial AudioSource (also feeds DinoProceduralRig Jaw RMS driver!)
                AudioSource audio = instance.GetComponent<AudioSource>();
                if (audio == null) audio = instance.AddComponent<AudioSource>();
                audio.playOnAwake = false;
                audio.spatialBlend = 1.0f;
                audio.rolloffMode = AudioRolloffMode.Logarithmic;
                audio.dopplerLevel = 0.2f;
                audio.minDistance = species == DinoSpeciesType.Tyrannosaurus || species == DinoSpeciesType.Brachiosaurus ? 8.0f : 3.0f;
                audio.maxDistance = species == DinoSpeciesType.Tyrannosaurus || species == DinoSpeciesType.Brachiosaurus ? 110.0f : 48.0f;

                // Procedural Rig (resolve & serialize exact skeleton bone links right into prefab!)
                DinoProceduralRig rig = instance.GetComponent<DinoProceduralRig>();
                if (rig == null) rig = instance.AddComponent<DinoProceduralRig>();
                rig.Initialize(cfg, audio, forceReResolve: true);

                // Hide response: weather wetness + flashlight eyeshine (MaterialPropertyBlock, per instance)
                if (instance.GetComponent<DinoWetness>() == null) instance.AddComponent<DinoWetness>();
                if (instance.GetComponent<DinoEyeShine>() == null) instance.AddComponent<DinoEyeShine>();

                // Species AI component
                DinoControllerBase ai = AttachAndWireAiComponent(instance, species, cfg, nav, anim, rig, audio);
                CopyTunedAiValues(previousAi, ai);
                EditorUtility.SetDirty(ai);
                EditorUtility.SetDirty(rig);

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                // The controller was deleted and re-created at the same path (same GUID). When the prefab text comes
                // out byte-identical Unity skips the re-import and the in-memory prefab keeps a dangling reference
                // to the destroyed controller (Animator with no controller = frozen bind pose until an editor restart).
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Copies every non-reference serialized value of the old AI component onto the new one (same type only).</summary>
        private static void CopyTunedAiValues(DinoControllerBase previous, DinoControllerBase fresh)
        {
            if (previous == null || fresh == null || previous.GetType() != fresh.GetType()) return;
            var src = new SerializedObject(previous);
            var dst = new SerializedObject(fresh);
            SerializedProperty it = src.GetIterator();
            bool enter = true;
            while (it.NextVisible(enter))
            {
                enter = false;
                if (it.propertyPath == "m_Script" || it.propertyType == SerializedPropertyType.ObjectReference) continue;
                if (HasObjectReference(it)) continue;      // wired references (config, agent, animator...) stay fresh
                if (dst.FindProperty(it.propertyPath) != null) dst.CopyFromSerializedProperty(it);
            }
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool HasObjectReference(SerializedProperty prop)
        {
            SerializedProperty p = prop.Copy();
            SerializedProperty end = p.GetEndProperty();
            bool enter = true;
            while (p.Next(enter) && !SerializedProperty.EqualContents(p, end))
            {
                enter = true;
                if (p.propertyType == SerializedPropertyType.ObjectReference) return true;
            }
            return false;
        }

        private static DinoControllerBase AttachAndWireAiComponent(
            GameObject root,
            DinoSpeciesType species,
            DinoConfig cfg,
            NavMeshAgent nav,
            Animator anim,
            DinoProceduralRig rig,
            AudioSource audio)
        {
            DinoControllerBase ai;
            switch (species)
            {
                case DinoSpeciesType.Tyrannosaurus:
                    ai = root.AddComponent<TrexAI>();
                    break;
                case DinoSpeciesType.Brachiosaurus:
                case DinoSpeciesType.Triceratops:
                    HerbivoreAI herb = root.AddComponent<HerbivoreAI>();
                    herb.SetHerbivoreSpecies(species);
                    ai = herb;
                    break;
                default:
                    ai = root.AddComponent<RaptorAI>();
                    break;
            }

            var so = new SerializedObject(ai);
            AssignProp(so, "config", cfg);
            AssignProp(so, "agent", nav);
            AssignProp(so, "animator", anim);
            AssignProp(so, "proceduralRig", rig);
            AssignProp(so, "audioSource", audio);
            so.ApplyModifiedPropertiesWithoutUndo();
            return ai;
        }

        private static void WireSpawnerPrefabs(
            DinoSpawner spawner,
            Dictionary<DinoSpeciesType, GameObject> prefabs,
            Dictionary<DinoSpeciesType, DinoConfig> configs)
        {
            var so = new SerializedObject(spawner);
            if (prefabs.TryGetValue(DinoSpeciesType.Velociraptor, out GameObject rP)) AssignProp(so, "raptorPrefab", rP);
            if (prefabs.TryGetValue(DinoSpeciesType.Tyrannosaurus, out GameObject tP)) AssignProp(so, "trexPrefab", tP);
            if (prefabs.TryGetValue(DinoSpeciesType.Brachiosaurus, out GameObject bP)) AssignProp(so, "brachiosaurusPrefab", bP);
            if (prefabs.TryGetValue(DinoSpeciesType.Triceratops, out GameObject cP)) AssignProp(so, "triceratopsPrefab", cP);

            if (configs.TryGetValue(DinoSpeciesType.Velociraptor, out DinoConfig rC)) AssignProp(so, "raptorConfig", rC);
            if (configs.TryGetValue(DinoSpeciesType.Tyrannosaurus, out DinoConfig tC)) AssignProp(so, "trexConfig", tC);
            if (configs.TryGetValue(DinoSpeciesType.Brachiosaurus, out DinoConfig bC)) AssignProp(so, "brachiosaurusConfig", bC);
            if (configs.TryGetValue(DinoSpeciesType.Triceratops, out DinoConfig cC)) AssignProp(so, "triceratopsConfig", cC);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        // =========================================================================
        // 7. PROLOGUE PEOPLE: park guide + visitors (prefabs, looping Animator controllers)
        // =========================================================================

        private const string CharacterSourceDir = "Assets/Art/Characters";
        private const string CharacterOutputDir = "Assets/Prefabs/Characters";
        private const string CharacterPrefabDir = "Assets/Prefabs/Characters";
        private const string CrowdPrefabPath = "Assets/Prefabs/Characters/Resources/PF_OpeningCrowd.prefab";

        /// <summary>Target standing heights (m) from the Godot original's opening.gd.</summary>
        private static readonly Dictionary<string, float> CharacterHeights = new Dictionary<string, float>
        {
            { "park_guide", 1.80f }, { "visitor_man", 1.80f }, { "visitor_woman", 1.68f },
            { "visitor_teen", 1.74f }, { "visitor_child", 1.25f }
        };

        /// <summary>Animator state (ParkVisitor.Activity name) -> embedded clip names to try, in order.</summary>
        private static readonly (string state, string[] clips)[] CharacterStates =
        {
            ("Idle", new[] { "idle" }),
            ("LookAround", new[] { "look_around", "idle" }),
            ("Phone", new[] { "phone_standing", "make_a_call_02", "idle" }),
            ("Clap", new[] { "clap", "cheer", "idle" }),
            ("Cheer", new[] { "cheer", "clap", "idle" }),
            ("Wave", new[] { "wave_goodbye_01", "clap", "idle" }),
            ("Walk", new[] { "walk" }),
        };

        [CliCommand(
            "cretacea",
            "build_character_prefabs",
            "Builds the prologue people (park guide + 4 visitors) from Assets/Art/Characters: looping Animator controllers from the embedded clips, height-correct prefabs under Assets/Prefabs/Characters/Resources/Visitors with ParkVisitor (walk speed measured from the clip).",
            MainThreadRequired = true)]
        public static string BuildCharacterPrefabsCli()
        {
            EnsureFolderExists(CharacterOutputDir);
            EnsureFolderExists(CharacterPrefabDir);
            EnsureFolderExists("Assets/Prefabs/Characters/Resources");
            var lines = new List<string>();
            foreach (KeyValuePair<string, float> kv in CharacterHeights)
            {
                try { lines.Add(BuildCharacterPrefab(kv.Key, kv.Value)); }
                catch (Exception ex)
                {
                    Debug.LogError($"[DinoAnimatorBuilder] Character {kv.Key} failed: {ex}");
                    lines.Add($"\"{kv.Key}\": \"error {EscapeJson(ex.Message)}\"");
                }
            }

            // Opening crowd root (StoryDirector loads Resources/PF_OpeningCrowd and destroys it after the opening).
            var crowdGo = new GameObject("PF_OpeningCrowd");
            try
            {
                ParkVisitorCrowd crowd = crowdGo.AddComponent<ParkVisitorCrowd>();
                var cso = new SerializedObject(crowd);
                AssignProp(cso, "parkGuide", AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterPrefabDir}/PF_park_guide.prefab"));
                AssignProp(cso, "visitorMan", AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterPrefabDir}/PF_visitor_man.prefab"));
                AssignProp(cso, "visitorWoman", AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterPrefabDir}/PF_visitor_woman.prefab"));
                AssignProp(cso, "visitorTeen", AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterPrefabDir}/PF_visitor_teen.prefab"));
                AssignProp(cso, "visitorChild", AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterPrefabDir}/PF_visitor_child.prefab"));
                cso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(crowdGo, CrowdPrefabPath);
                AssetDatabase.ImportAsset(CrowdPrefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                lines.Add($"\"crowd\": \"{CrowdPrefabPath}\"");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(crowdGo);
            }
            AssetDatabase.SaveAssets();
            return "{\n  " + string.Join(",\n  ", lines) + "\n}";
        }

        private static string BuildCharacterPrefab(string name, float targetHeight)
        {
            string fbxPath = $"{CharacterSourceDir}/{name}/{name}.fbx";
            GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbx == null) throw new InvalidOperationException("missing " + fbxPath);
            var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            foreach (UnityEngine.Object o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview", StringComparison.Ordinal))
                {
                    string shortName = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                    clips[shortName] = c;
                }
            }

            // Controller: one looping state per activity. The FBX clips import without loop time and the FBX import
            // settings are not ours to change, so each chosen clip is copied into Assets/Prefabs/Characters/Clips with
            // loop time (and loop pose for the walk) on. (A self-transition re-blend jumped the gait every 2.29 s.)
            EnsureFolderExists(CharacterClipDir);
            string ctrlPath = $"{CharacterOutputDir}/AC_{name}.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath) != null) AssetDatabase.DeleteAsset(ctrlPath);
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            int row = 0;
            foreach ((string state, string[] names) in CharacterStates)
            {
                AnimationClip clip = null;
                foreach (string n in names) if (clips.TryGetValue(n, out clip)) break;
                if (clip == null) continue;
                AnimationClip looped = LoopingClipCopy(clip, $"{name}_{state}", loopPose: state == "Walk");
                AnimatorState st = sm.AddState(state, new Vector3(300f, 60f * row++, 0f));
                st.motion = looped;
                st.writeDefaultValues = true;
                if (state == "Idle") sm.defaultState = st;
            }
            EditorUtility.SetDirty(ctrl);

            GameObject root = new GameObject("PF_" + name);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            try
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.name = "Model";
                model.transform.SetParent(root.transform, false);
                // The Tripo rigs walk toward model -X (measured: stance foot moves +X); turn them to face +Z.
                model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                Animator anim = model.GetComponent<Animator>();
                if (anim == null) anim = model.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                // Height from the idle pose (top of the skinned mesh), walk speed from the walk clip's planted foot.
                SkinnedMeshRenderer smr = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
                float idleTop = MeasureTop(model, smr, clips.TryGetValue("idle", out AnimationClip idle) ? idle : null);
                float scale = idleTop > 0.3f ? targetHeight / idleTop : 1f;
                float walkSpeed = clips.TryGetValue("walk", out AnimationClip walk) ? MeasureWalkSpeed(model, walk) : 0.8f;
                if (idle != null) idle.SampleAnimation(model, 0f);
                model.transform.localScale = Vector3.one * scale;

                foreach (SkinnedMeshRenderer r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    BuildOrUpdateCharacterMaterial(name, r.sharedMaterial);
                    r.updateWhenOffscreen = false;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.skinnedMotionVectors = true;
                }

                ParkVisitor visitor = root.AddComponent<ParkVisitor>();
                var so = new SerializedObject(visitor);
                AssignProp(so, "animator", anim);
                SerializedProperty sp = so.FindProperty("walkClipSpeed");
                if (sp != null) sp.floatValue = walkSpeed * scale;
                so.ApplyModifiedPropertiesWithoutUndo();

                string prefabPath = $"{CharacterPrefabDir}/PF_{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                return $"\"{name}\": {{\"prefab\": \"{prefabPath}\", \"scale\": {scale:F3}, \"idle_top_m\": {idleTop:F2}, \"walk_mps\": {walkSpeed * scale:F2}, \"states\": {sm.states.Length}}}";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private const string CharacterClipDir = "Assets/Prefabs/Characters/Clips";
        private const string CharacterMaterialDir = "Assets/Art/Materials/Characters";

        /// <summary>A copy of an FBX-embedded clip with loop time on (the FBX import settings stay untouched).</summary>
        private static AnimationClip LoopingClipCopy(AnimationClip source, string copyName, bool loopPose)
        {
            string path = $"{CharacterClipDir}/{copyName}.anim";
            AnimationClip copy = UnityEngine.Object.Instantiate(source);
            copy.name = copyName;
            AnimationClipSettings st = AnimationUtility.GetAnimationClipSettings(copy);
            st.loopTime = true;
            st.loopBlend = loopPose;
            AnimationUtility.SetAnimationClipSettings(copy, st);
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        /// <summary>
        /// Visitor materials (Assets/Art/Materials/Characters/MAT_*.mat): the source masks have blank AO, metallic leaks
        /// up to 0.71 and one ~0.47 smoothness for skin and cloth, so the people read as glossy toys next to the matte
        /// hides. Bind the derived look mask (Tools~/charmask.py: curvature AO, skin ~0.40 / cloth 0.12-0.30) when present,
        /// metallic 0 either way, specular occlusion from AO.
        /// </summary>
        private static void BuildOrUpdateCharacterMaterial(string name, Material mat)
        {
            if (mat == null) return;
            string matPath = AssetDatabase.GetAssetPath(mat);
            if (string.IsNullOrEmpty(matPath) || !matPath.StartsWith(CharacterMaterialDir + "/", StringComparison.Ordinal)) return;
            string lookMaskPath = $"{CharacterMaterialDir}/T_{name}_Mask_Look.png";
            Texture2D lookMask = null;
            if (File.Exists(lookMaskPath))
            {
                if (AssetImporter.GetAtPath(lookMaskPath) == null) AssetDatabase.ImportAsset(lookMaskPath, ImportAssetOptions.ForceSynchronousImport);
                ConfigureTextureImporter(lookMaskPath, isNormal: false, isLinear: true);
                lookMask = LoadTexture(lookMaskPath);
            }
            if (lookMask != null && mat.HasProperty("_MaskMap"))
            {
                mat.SetTexture("_MaskMap", lookMask);
                mat.EnableKeyword("_MASKMAP");
                SetFloatIfPresent(mat, "_SmoothnessRemapMin", 0f);
                SetFloatIfPresent(mat, "_SmoothnessRemapMax", 1f);
                SetFloatIfPresent(mat, "_AORemapMin", 0f);
            }
            else
            {
                SetFloatIfPresent(mat, "_SmoothnessRemapMin", 0.05f);
                SetFloatIfPresent(mat, "_SmoothnessRemapMax", 0.5f);
                SetFloatIfPresent(mat, "_AORemapMin", 0.35f);
            }
            SetFloatIfPresent(mat, "_AORemapMax", 1f);
            SetFloatIfPresent(mat, "_Metallic", 0f);
            SetFloatIfPresent(mat, "_MetallicRemapMin", 0f);
            SetFloatIfPresent(mat, "_MetallicRemapMax", 0f);
            SetFloatIfPresent(mat, "_SpecularOcclusionMode", 1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
        }

        private static float MeasureTop(GameObject model, SkinnedMeshRenderer smr, AnimationClip idle)
        {
            if (smr == null) return 0f;
            if (idle != null) idle.SampleAnimation(model, idle.length * 0.25f);
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            float top = float.MinValue, bottom = float.MaxValue;
            foreach (Vector3 v in mesh.vertices)
            {
                float y = smr.transform.TransformPoint(v).y - model.transform.position.y;
                if (y > top) top = y;
                if (y < bottom) bottom = y;
            }
            UnityEngine.Object.DestroyImmediate(mesh);
            return top - Mathf.Min(0f, bottom);
        }

        /// <summary>Median backward speed of the lowest foot along the model's -X walking axis (m/s, unscaled).</summary>
        private static float MeasureWalkSpeed(GameObject model, AnimationClip walk)
        {
            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            Transform foot = Array.Find(all, t => t.name == "L_Foot");
            if (foot == null) return 0.8f;
            const int n = 96;
            var ys = new float[n + 1];
            var xs = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                walk.SampleAnimation(model, walk.length * i / n);
                Vector3 p = model.transform.InverseTransformPoint(foot.position);
                ys[i] = p.y; xs[i] = p.x;
            }
            float minY = float.MaxValue;
            for (int i = 0; i <= n; i++) minY = Mathf.Min(minY, ys[i]);
            var speeds = new List<float>();
            float dt = walk.length / n;
            for (int i = 0; i < n; i++)
                if (ys[i] - minY < 0.03f && ys[i + 1] - minY < 0.03f) speeds.Add((xs[i + 1] - xs[i]) / dt);
            if (speeds.Count == 0) return 0.8f;
            speeds.Sort();
            return Mathf.Clamp(speeds[speeds.Count / 2], 0.3f, 2.0f);
        }

        private static void AssignProp(SerializedObject so, string propName, UnityEngine.Object value)
        {
            SerializedProperty prop = so.FindProperty(propName);
            if (prop != null) prop.objectReferenceValue = value;
        }

        // =========================================================================
        // PATH, STRING & DEPENDENCY-FREE JSON HELPERS
        // =========================================================================

        private static string StripArmaturePrefix(string rawName)
        {
            if (string.IsNullOrEmpty(rawName)) return string.Empty;
            int bar = rawName.LastIndexOf('|');
            return bar >= 0 && bar + 1 < rawName.Length ? rawName.Substring(bar + 1).Trim() : rawName.Trim();
        }

        private static bool InferLoopFromClipName(string name)
        {
            string l = name.ToLowerInvariant();
            if (l.Contains("roar") || l.Contains("bite") || l.Contains("lunge") || l.Contains("snap")
                || l.Contains("bash") || l.Contains("snort") || l.Contains("call") || l.Contains("turn"))
                return false;
            return true;
        }

        private static void EnsureFolderExists(string assetFolderPath)
        {
            string[] parts = assetFolderPath.Split('/');
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

        private static string ResolveAssetPathFromRelative(string absBaseDir, string relPath, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(relPath)) return null;
            string cleanRel = relPath.Replace('\\', '/');
            if (cleanRel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return File.Exists(Path.Combine(projectRoot, cleanRel)) ? cleanRel : null;

            string candidateAbs = Path.GetFullPath(Path.Combine(absBaseDir, relPath));
            if (!File.Exists(candidateAbs)) return null;
            return ToAssetRelativePath(candidateAbs, projectRoot);
        }

        private static string ToAssetRelativePath(string absPath, string projectRoot)
        {
            string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/') + "/";
            string fullTarget = Path.GetFullPath(absPath).Replace('\\', '/');
            if (fullTarget.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return fullTarget.Substring(fullRoot.Length);
            }
            return null;
        }

        private static string EscapeJson(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ");
        }

        private static string GetString(Dictionary<string, object> d, string key)
        {
            return d != null && d.TryGetValue(key, out object v) && v != null ? Convert.ToString(v) : null;
        }

        private static float GetFloat(Dictionary<string, object> d, string key, float fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v != null)
            {
                if (v is double dbl) return (float)dbl;
                if (v is long lng) return lng;
                if (float.TryParse(Convert.ToString(v), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                    return parsed;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> d, string key, bool fallback)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is bool b) return b;
            return fallback;
        }

        private static bool TryGetDict(Dictionary<string, object> d, string key, out Dictionary<string, object> result)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is Dictionary<string, object> dict)
            {
                result = dict;
                return true;
            }
            result = null;
            return false;
        }

        private static bool TryGetList(Dictionary<string, object> d, string key, out List<object> result)
        {
            if (d != null && d.TryGetValue(key, out object v) && v is List<object> list)
            {
                result = list;
                return true;
            }
            result = null;
            return false;
        }

        private static string[] GetStringArray(Dictionary<string, object> d, string key)
        {
            if (!TryGetList(d, key, out List<object> list) || list.Count == 0) return null;
            var arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = Convert.ToString(list[i]);
            return arr;
        }

        /// <summary>
        /// Compact, zero-dependency JSON parser for arbitrary manifest / metadata JSON files.
        /// </summary>
        private static class MiniJson
        {
            public static object Deserialize(string json)
            {
                if (string.IsNullOrEmpty(json)) return null;
                int index = 0;
                return ParseValue(json, ref index);
            }

            private static void SkipWhitespace(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            private static object ParseValue(string s, ref int i)
            {
                SkipWhitespace(s, ref i);
                if (i >= s.Length) return null;
                char c = s[i];
                if (c == '{') return ParseObject(s, ref i);
                if (c == '[') return ParseArray(s, ref i);
                if (c == '"') return ParseString(s, ref i);
                if (c == 't' || c == 'f') return ParseBool(s, ref i);
                if (c == 'n') { i += 4; return null; }
                return ParseNumber(s, ref i);
            }

            private static Dictionary<string, object> ParseObject(string s, ref int i)
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++; // '{'
                while (i < s.Length)
                {
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == '}') { i++; break; }
                    string key = ParseString(s, ref i);
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == ':') i++;
                    object val = ParseValue(s, ref i);
                    dict[key] = val;
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; break; }
                }
                return dict;
            }

            private static List<object> ParseArray(string s, ref int i)
            {
                var list = new List<object>();
                i++; // '['
                while (i < s.Length)
                {
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == ']') { i++; break; }
                    list.Add(ParseValue(s, ref i));
                    SkipWhitespace(s, ref i);
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; break; }
                }
                return list;
            }

            private static string ParseString(string s, ref int i)
            {
                var sb = new StringBuilder();
                if (i < s.Length && s[i] == '"') i++;
                while (i < s.Length)
                {
                    char c = s[i++];
                    if (c == '"') break;
                    if (c == '\\' && i < s.Length)
                    {
                        char esc = s[i++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 <= s.Length && int.TryParse(s.Substring(i, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                                break;
                            default: sb.Append(esc); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            private static bool ParseBool(string s, ref int i)
            {
                if (s.Substring(i).StartsWith("true", StringComparison.OrdinalIgnoreCase)) { i += 4; return true; }
                i += 5;
                return false;
            }

            private static object ParseNumber(string s, ref int i)
            {
                int start = i;
                while (i < s.Length && "0123456789+-.eE".IndexOf(s[i]) >= 0) i++;
                string token = s.Substring(start, i - start);
                if (double.TryParse(token, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double d))
                    return d;
                return 0.0;
            }
        }
    }
}
