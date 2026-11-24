using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Audio;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using Unity.Pipeline.Commands;
using Cretacea.Core;
using Cretacea.Player;
using Cretacea.Interact;
using Cretacea.Story;
using Cretacea.UI;
using Cretacea.Audio;
using Cretacea.AI;
using Cretacea.Rendering;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Master Unity 6 HDRP Scene Assembler, Chapter Jumper, and Automated Door/Flashlight/Document Verifier.
    /// Orchestrates Zones 1-4, Player Rig + Anti-Clip Viewmodel Custom Pass, Storm Rain Occlusion,
    /// Atmosphere Presets, AudioMixer/SoundBank, AudioReverbZones, Dual NavMeshSurfaces (Agent_Raptor & Agent_TRex),
    /// and Live 3D HDRP MainMenu.unity + MainWorld.unity build registration.
    ///
    /// CLI Commands (group "cretacea"):
    ///   unity command cretacea build_all_scenes
    ///   unity command cretacea jump_chapter -- --chapter <Intro|Night|Corridor|ServicePaths|Substation|OldYard|Escape|DawnCoast>
    ///   unity command cretacea verify_playmode_doors
    /// </summary>
    public static class CretaceaWorldAssembler
    {
        public const string MainWorldScenePath = "Assets/Scenes/MainWorld.unity";
        public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";

        private const string PrefabFlashlightPickup = "Assets/Prefabs/Interact/PF_FlashlightPickup.prefab";
        private const string PrefabFlashlightFpsRig = "Assets/Prefabs/Player/PF_FlashlightFpsRig.prefab";
        private const string PrefabViewmodelPassVol = "Assets/Settings/Rendering/PF_ViewmodelCustomPassVolume.prefab";
        private const string PrefabStormRainSystem = "Assets/VFX/PF_StormRainSystem.prefab";
        private const string PrefabTrexSkeleton = "Assets/Prefabs/Props/PF_TrexSkeleton.prefab";

        private const string MixerAssetPath = "Assets/Audio/CretaceaMixer.mixer";
        private const string SoundBankAssetPath = "Assets/Resources/CretaceaSoundBank.asset";

        private const string PresetOpeningDuskPath = "Assets/Settings/Atmosphere/Preset_OpeningDusk.asset";
        private const string PresetBlackoutStormPath = "Assets/Settings/Atmosphere/Preset_BlackoutStorm.asset";
        private const string PresetInteriorEmergencyPath = "Assets/Settings/Atmosphere/Preset_InteriorEmergency.asset";
        private const string PresetPowerRestoredPath = "Assets/Settings/Atmosphere/Preset_PowerRestored.asset";
        private const string PresetDawnCoastPath = "Assets/Settings/Atmosphere/Preset_DawnCoast.asset";

        private const string PrefabDinoRaptor = "Assets/Prefabs/Dinosaurs/PF_Velociraptor.prefab";
        private const string PrefabDinoTrex = "Assets/Prefabs/Dinosaurs/PF_Tyrannosaurus.prefab";
        private const string PrefabDinoBrachio = "Assets/Prefabs/Dinosaurs/PF_Brachiosaurus.prefab";
        private const string PrefabDinoTrike = "Assets/Prefabs/Dinosaurs/PF_Triceratops.prefab";

        private const string ConfigDinoRaptor = "Assets/ScriptableObjects/AI/DinoConfig_Velociraptor.asset";
        private const string ConfigDinoTrex = "Assets/ScriptableObjects/AI/DinoConfig_Tyrannosaurus.asset";
        private const string ConfigDinoBrachio = "Assets/ScriptableObjects/AI/DinoConfig_Brachiosaurus.asset";
        private const string ConfigDinoTrike = "Assets/ScriptableObjects/AI/DinoConfig_Triceratops.asset";

        private struct ChapterJumpSpec
        {
            public string CanonicalName;
            public CheckpointId Checkpoint;
            public Vector3 PlayerPos;
            public float YawDeg;
            public float PitchDeg;
            public string VolumeProfilePath;
            public AtmospherePreset AtmospherePreset;
            public bool Blackout;
            public bool AuxPower;
            public bool HasFlashlight;
        }

        private static readonly ChapterJumpSpec[] ChapterSpecs =
        {
            new ChapterJumpSpec
            {
                CanonicalName = "Intro",
                Checkpoint = CheckpointId.Intro,
                PlayerPos = new Vector3(-42.0f, 1.5f, -92.0f),
                YawDeg = 12f, PitchDeg = 2f,
                VolumeProfilePath = PresetOpeningDuskPath,
                AtmospherePreset = AtmospherePreset.GoldenDay,
                Blackout = false, AuxPower = false, HasFlashlight = false
            },
            new ChapterJumpSpec
            {
                CanonicalName = "Night",
                Checkpoint = CheckpointId.Night,
                PlayerPos = new Vector3(-64.5f, 0.05f, 28.0f),
                YawDeg = 0f, PitchDeg = -6f,
                VolumeProfilePath = PresetBlackoutStormPath,
                AtmospherePreset = AtmospherePreset.NightCalm,
                Blackout = false, AuxPower = false, HasFlashlight = false
            },
            new ChapterJumpSpec
            {
                CanonicalName = "Corridor",
                Checkpoint = CheckpointId.Records,
                PlayerPos = new Vector3(-56.0f, 0.05f, 20.5f),
                YawDeg = 90f, PitchDeg = -2f,
                VolumeProfilePath = PresetInteriorEmergencyPath,
                AtmospherePreset = AtmospherePreset.NightStormBlackout,
                Blackout = true, AuxPower = false, HasFlashlight = true
            },
            new ChapterJumpSpec
            {
                CanonicalName = "ServicePaths",
                Checkpoint = CheckpointId.Dock,
                PlayerPos = new Vector3(40.0f, 0.05f, 6.5f),
                YawDeg = 0f, PitchDeg = -2f,
                VolumeProfilePath = PresetBlackoutStormPath,
                AtmospherePreset = AtmospherePreset.NightStormBlackout,
                Blackout = true, AuxPower = false, HasFlashlight = true
            },
            new ChapterJumpSpec
            {
                CanonicalName = "Substation",
                Checkpoint = CheckpointId.Substation,
                PlayerPos = new Vector3(252.5f, 2.35f, 0.0f),
                YawDeg = 90f, PitchDeg = -3f,
                VolumeProfilePath = PresetBlackoutStormPath,
                AtmospherePreset = AtmospherePreset.NightStormBlackout,
                Blackout = true, AuxPower = false, HasFlashlight = true
            },
            new ChapterJumpSpec
            {
                CanonicalName = "OldYard",
                Checkpoint = CheckpointId.Yard,
                PlayerPos = new Vector3(404.5f, 1.05f, -22.8f),
                YawDeg = 90f, PitchDeg = -2f,
                VolumeProfilePath = PresetPowerRestoredPath,
                AtmospherePreset = AtmospherePreset.NightStormBlackout,
                Blackout = true, AuxPower = true, HasFlashlight = true
            },
            new ChapterJumpSpec
            {
                CanonicalName = "Escape",
                Checkpoint = CheckpointId.Yard,
                PlayerPos = new Vector3(495.5f, 1.05f, -47.5f),
                YawDeg = 82f, PitchDeg = -2f,
                VolumeProfilePath = PresetPowerRestoredPath,
                AtmospherePreset = AtmospherePreset.NightStormBlackout,
                Blackout = true, AuxPower = true, HasFlashlight = true
            },
            new ChapterJumpSpec
            {
                CanonicalName = "DawnCoast",
                Checkpoint = CheckpointId.Yard,
                PlayerPos = new Vector3(538.0f, 1.05f, -46.0f),
                YawDeg = 90f, PitchDeg = -2f,
                VolumeProfilePath = PresetDawnCoastPath,
                AtmospherePreset = AtmospherePreset.DawnCoast,
                Blackout = false, AuxPower = true, HasFlashlight = false
            }
        };

        // =========================================================================
        // 1. CLI COMMAND: build_all_scenes
        // =========================================================================

        [CliCommand(
            "cretacea",
            "build_all_scenes",
            "Builds Assets/Scenes/MainWorld.unity (Zones 1-4, Player Rig + Layer 12 Viewmodel Custom Pass, Rain Occlusion, Managers, Reverb Zones, Dual Raptor/TRex NavMeshes) and Assets/Scenes/MainMenu.unity (Live 3D HDRP Menu), then registers [MainMenu, MainWorld] in EditorBuildSettings.",
            MainThreadRequired = true)]
        public static string BuildAllScenesCli(
            [CliArg("bake_navmesh", "Bake both Agent_Raptor and Agent_TRex NavMeshSurfaces in MainWorld.unity")]
            bool bakeNavMesh = true)
        {
            return BuildAllScenes(bakeNavMesh);
        }

        [MenuItem("Cretacea/World/Build All Scenes (MainWorld + MainMenu)")]
        public static void BuildAllScenesMenu()
        {
            string report = BuildAllScenes(bakeNavMesh: true);
            Debug.Log($"[CretaceaWorldAssembler] Build complete:\n{report}");
        }

        public static string BuildAllScenes(bool bakeNavMesh = true)
        {
            WorldMaterialPalette.EnsureFolder("Assets/Scenes");
            var warnings = new List<string>();
            WorldMaterialPalette palette = new WorldMaterialPalette();

            // --- Build MainMenu.unity first so MainWorld.unity stays open when done ---
            MainMenuBuildStats menuStats = BuildMainMenuScene(palette, warnings);

            // --- Build MainWorld.unity ---
            MainWorldBuildStats worldStats = BuildMainWorldScene(palette, bakeNavMesh, warnings);

            // --- Register [MainMenu.unity, MainWorld.unity] in EditorBuildSettings ---
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainMenuScenePath, true),
                new EditorBuildSettingsScene(MainWorldScenePath, true)
            };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"status\": \"ok\",");
            sb.AppendLine($"  \"mainMenuScene\": \"{MainMenuScenePath}\",");
            sb.AppendLine($"  \"mainWorldScene\": \"{MainWorldScenePath}\",");
            sb.AppendLine($"  \"buildSettingsRegistered\": [\"{MainMenuScenePath}\", \"{MainWorldScenePath}\"],");
            sb.AppendLine($"  \"zonesBuilt\": {FormatStringArrayJson(worldStats.ZonesBuilt)},");
            sb.AppendLine($"  \"doorCount\": {worldStats.DoorCount},");
            sb.AppendLine($"  \"documentCount\": {worldStats.DocumentCount},");
            sb.AppendLine($"  \"reverbZoneCount\": {worldStats.ReverbZoneCount},");
            sb.AppendLine($"  \"replacedDeskFlashlightWithPrefab\": {(worldStats.ReplacedDeskFlashlight ? "true" : "false")},");
            sb.AppendLine($"  \"boundFpsFlashlightPrefab\": {(worldStats.BoundFpsFlashlightPrefab ? "true" : "false")},");
            sb.AppendLine($"  \"flashlightDisabledAtSpawn\": {(worldStats.FlashlightDisabledAtSpawn ? "true" : "false")},");
            sb.AppendLine($"  \"boundDinoPrefabs\": {worldStats.BoundDinoPrefabCount},");
            sb.AppendLine($"  \"boundAtmospherePresets\": {worldStats.BoundAtmospherePresetCount},");
            sb.AppendLine($"  \"boundAudioMixer\": {(worldStats.BoundAudioMixer ? "true" : "false")},");
            sb.AppendLine($"  \"boundSoundBank\": {(worldStats.BoundSoundBank ? "true" : "false")},");
            sb.AppendLine($"  \"boundPathLampSparksVfx\": {(worldStats.BoundPathLampSparksVfx ? "true" : "false")},");
            sb.AppendLine($"  \"lightningPeakLuxCap\": 55.0,");
            sb.AppendLine($"  \"navMeshRaptorBaked\": {(worldStats.NavMeshRaptorBaked ? "true" : "false")},");
            sb.AppendLine($"  \"navMeshTRexBaked\": {(worldStats.NavMeshTRexBaked ? "true" : "false")},");
            sb.AppendLine($"  \"mainMenuHasSkeleton\": {(menuStats.HasSkeleton ? "true" : "false")},");
            sb.AppendLine($"  \"mainMenuHasRain\": {(menuStats.HasRain ? "true" : "false")},");
            sb.AppendLine($"  \"warnings\": {FormatStringArrayJson(warnings)}");
            sb.Append("}");
            return sb.ToString();
        }

        private sealed class MainWorldBuildStats
        {
            public readonly List<string> ZonesBuilt = new List<string>();
            public int DoorCount;
            public int DocumentCount;
            public int ReverbZoneCount;
            public bool ReplacedDeskFlashlight;
            public bool BoundFpsFlashlightPrefab;
            public bool FlashlightDisabledAtSpawn;
            public int BoundDinoPrefabCount;
            public int BoundAtmospherePresetCount;
            public bool BoundAudioMixer;
            public bool BoundSoundBank;
            public bool BoundPathLampSparksVfx;
            public bool NavMeshRaptorBaked;
            public bool NavMeshTRexBaked;
        }

        private sealed class MainMenuBuildStats
        {
            public bool HasSkeleton;
            public bool HasRain;
        }

        // =========================================================================
        // MAIN WORLD ASSEMBLY (Assets/Scenes/MainWorld.unity)
        // =========================================================================

        private static MainWorldBuildStats BuildMainWorldScene(WorldMaterialPalette palette, bool bakeNavMesh, List<string> warnings)
        {
            var stats = new MainWorldBuildStats();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. World Root & Zones 1-4 (strictly typed direct invocation)
            var worldRoot = new GameObject("World");
            GameObject z1 = Zone1_VisitorCenterBuilder.Build(worldRoot.transform, palette);
            if (z1 != null) stats.ZonesBuilt.Add(z1.name);

            GameObject z2 = Zone2_ServicePathsBuilder.Build(worldRoot.transform, palette);
            if (z2 != null) stats.ZonesBuilt.Add(z2.name);
            ValleyNightDressing.Build(worldRoot.transform, palette);   // r4: Tyrant Valley battery beacons, flashers, delineators

            GameObject z3 = Zone3_SubstationAndYardBuilder.Build(worldRoot.transform, palette);
            if (z3 != null) stats.ZonesBuilt.Add(z3.name);

            GameObject z4 = Zone4_TerrainJungleAndDawnCoastBuilder.Build(worldRoot.transform, palette);
            if (z4 != null) stats.ZonesBuilt.Add(z4.name);

            // 2. Replace Security Office Desk Flashlight Placeholder with PF_FlashlightPickup.prefab if present
            stats.ReplacedDeskFlashlight = TryUpgradeDeskFlashlightPickup(warnings);

            // 3. Instantiate Player Rig at Ch.1 start anchor (-64.5, 0.05, 28.0, yaw 0 deg North facing CCTV desk!)
            Vector3 startPos = new Vector3(-64.5f, 0.05f, 28.0f);
            float startYaw = 0f;
            ResolveStartAnchor(ref startPos, ref startYaw);

            PlayerRigRefs playerRefs = BuildPlayerRigAndCameras(startPos, startYaw, out bool boundFpsRig, out bool vmDisabled, warnings);
            stats.BoundFpsFlashlightPrefab = boundFpsRig;
            stats.FlashlightDisabledAtSpawn = vmDisabled;

            // 4. HDRP Viewmodel Custom Pass Volume + Indoor-Occluded Storm Rain System tracking Player
            EnsureViewmodelCustomPassVolumeInScene();
            StormRainOcclusionController rainCtrl = InstantiateStormRainSystem(playerRefs.PlayerRoot.transform, warnings);

            // 5. Core Managers & Atmosphere / Audio / UI / DinoSpawner / StoryDirector
            var managersRoot = new GameObject("[Managers]");

            GameState gameState = CreateManager<GameState>(managersRoot.transform, "[GameState]");
            WorldRegistry worldRegistry = CreateManager<WorldRegistry>(managersRoot.transform, "[WorldRegistry]");
            LightGroupController lightGroupController = CreateManager<LightGroupController>(managersRoot.transform, "[LightGroupController]");

            AtmosphereController atmosphereController = BuildAtmosphereAndLightingRig(
                managersRoot.transform,
                rainCtrl,
                out int presetCount,
                warnings);
            stats.BoundAtmospherePresetCount = presetCount;

            AudioManager audioManager = BuildAudioManager(managersRoot.transform, out bool boundMixer, out bool boundBank, warnings);
            stats.BoundAudioMixer = boundMixer;
            stats.BoundSoundBank = boundBank;

            HudController hudController = BuildHudController(managersRoot.transform);
            JournalController journalController = BuildJournalController(managersRoot.transform);
            MenuController menuController = BuildMenuController(managersRoot.transform, isMainMenu: false);

            DinoSpawner dinoSpawner = BuildDinoSpawner(managersRoot.transform, out int dinoPrefabsBound, warnings);
            stats.BoundDinoPrefabCount = dinoPrefabsBound;

            StoryDirector storyDirector = BuildStoryDirector(
                managersRoot.transform,
                playerRefs.Player,
                playerRefs.Flashlight,
                playerRefs.CameraDirector,
                lightGroupController,
                atmosphereController,
                worldRegistry,
                out bool boundPathLampSparks);
            stats.BoundPathLampSparksVfx = boundPathLampSparks;

            // Suppress unused-local warnings cleanly while documenting ownership
            if (gameState == null || audioManager == null || hudController == null || journalController == null || menuController == null || dinoSpawner == null || storyDirector == null)
            {
                warnings.Add("One or more core manager components failed creation.");
            }

            // 6. AudioReverbZones (Visitor Center Concrete, Lobby Atrium, Feed Prep Tile, Service Paths, Workshop Metal, Exterior Storm)
            stats.ReverbZoneCount = BuildAudioReverbZones();

            // 7. Apply crisp Ch.1 Pre-Blackout Night lighting state in Edit Mode so Scene/Game view opens ready
            ApplyEditModeLightGroupState(blackout: false, auxPower: false);

            // 8. Dual NavMeshSurfaces (Agent_Raptor & Agent_TRex — strictly Default | Environment | Door layers!)
            Physics.SyncTransforms();
            BuildAndBakeNavMeshes(bakeNavMesh, out bool raptorBaked, out bool trexBaked, warnings);
            stats.NavMeshRaptorBaked = raptorBaked;
            stats.NavMeshTRexBaked = trexBaked;

            // Count doors & documents for build report
            stats.DoorCount = UnityEngine.Object.FindObjectsByType<DoorController>().Length;
            stats.DocumentCount = UnityEngine.Object.FindObjectsByType<DocumentPickup>().Length;

            // 9. Look Pass: lamps, materials on moving parts, static shadow casters, edit-mode atmosphere preview
            Debug.Log("[WorldAssembler] Look pass (MainWorld): " + Cretacea.Editor.Look.LookPass.RunOnBuild(isMainMenu: false));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainWorldScenePath);
            return stats;
        }

        private static bool TryUpgradeDeskFlashlightPickup(List<string> warnings)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFlashlightPickup);
            if (prefab == null)
            {
                warnings.Add($"'{PrefabFlashlightPickup}' not found; keeping Zone 1 desk flashlight pickup.");
                return false;
            }

            FlashlightPickup existing = UnityEngine.Object.FindAnyObjectByType<FlashlightPickup>();
            Transform targetParent = existing != null ? existing.transform.parent : null;
            Vector3 pos = existing != null ? existing.transform.position : new Vector3(-64.0f, 0.82f, 31.0f);
            Quaternion rot = existing != null ? existing.transform.rotation : Quaternion.Euler(0f, 28f, 0f);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, targetParent);
            instance.name = "Pickup_TacticalFlashlight";
            instance.transform.SetPositionAndRotation(pos, rot);
            GameLayers.SetLayerRecursively(instance, GameLayers.Interactable);

            FlashlightPickup fp = instance.GetComponent<FlashlightPickup>() ?? instance.AddComponent<FlashlightPickup>();
            var so = new SerializedObject(fp);
            SerializedProperty idProp = so.FindProperty("interactId");
            if (idProp != null) idProp.stringValue = "pickup_flashlight";
            so.ApplyModifiedPropertiesWithoutUndo();

            if (existing != null && existing.gameObject != instance)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
            SwapDeskTorchForHeldModel(instance);
            return true;
        }

        /// <summary>
        /// Round 5: the torch on the desk is the same scanned tactical torch the player then holds (the held model
        /// without the hand, same materials). The prefab's old model is hidden, not removed (it is part of the prefab).
        /// </summary>
        private static void SwapDeskTorchForHeldModel(GameObject pickup)
        {
            GameObject torch = AssetDatabase.LoadAssetAtPath<GameObject>(Cretacea.Editor.Look.LookMaterials.ViewmodelPickupModel);
            if (torch == null) return;
            foreach (MeshRenderer old in pickup.GetComponentsInChildren<MeshRenderer>(true)) old.enabled = false;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(torch, pickup.transform);
            inst.name = "VM_TorchPickup";
            // lying on the desk: the head (r 4.5 cm) and the tail cap rest on the top, so the tail dips about 2 deg
            inst.transform.localPosition = new Vector3(0f, 0.047f, 0f);
            inst.transform.localRotation = Quaternion.Euler(2f, 0f, 0f);
            Material[] mats = Cretacea.Editor.Look.LookMaterials.ViewmodelPickupMaterials();
            foreach (MeshRenderer mr in inst.GetComponentsInChildren<MeshRenderer>(true))
            {
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            GameLayers.SetLayerRecursively(inst, GameLayers.Interactable);
        }

        private static void ResolveStartAnchor(ref Vector3 pos, ref float yawDeg)
        {
            WorldAnchor[] anchors = UnityEngine.Object.FindObjectsByType<WorldAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                if (string.Equals(anchors[i].AnchorId, "start", StringComparison.OrdinalIgnoreCase))
                {
                    pos = anchors[i].transform.position;
                    yawDeg = anchors[i].transform.eulerAngles.y;
                    return;
                }
            }
        }

        private sealed class PlayerRigRefs
        {
            public GameObject PlayerRoot;
            public PlayerController Player;
            public FlashlightController Flashlight;
            public CameraDirector CameraDirector;
            public Camera MainCamera;
        }

        private static PlayerRigRefs BuildPlayerRigAndCameras(
            Vector3 startPos,
            float startYawDeg,
            out bool boundFpsRigPrefab,
            out bool viewmodelDisabledAtSpawn,
            List<string> warnings)
        {
            var refs = new PlayerRigRefs();

            var playerRoot = new GameObject("PlayerRoot");
            playerRoot.tag = "Player";
            playerRoot.layer = GameLayers.Player;
            playerRoot.transform.SetPositionAndRotation(startPos, Quaternion.Euler(0f, startYawDeg, 0f));

            CharacterController cc = playerRoot.AddComponent<CharacterController>();
            cc.radius = 0.34f;
            cc.height = 1.75f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 48f;
            cc.skinWidth = 0.04f;

            // Camera Pivot at eye height (1.62 m)
            var camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(playerRoot.transform, false);
            camPivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);
            camPivot.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);

            // ShakeHost -> FPS CinemachineCamera
            var shakeHost = new GameObject("ShakeHost");
            shakeHost.transform.SetParent(camPivot.transform, false);

            var fpsCamGo = new GameObject("CM_FPSCamera");
            fpsCamGo.transform.SetParent(shakeHost.transform, false);
            CinemachineCamera fpsCm = fpsCamGo.AddComponent<CinemachineCamera>();
            fpsCm.Priority = 20;
            var fpsLens = fpsCm.Lens;
            fpsLens.FieldOfView = 68f;
            fpsLens.NearClipPlane = 0.06f;
            fpsLens.FarClipPlane = 12000f;      // the sea must reach the horizon (at 4 km a strip of the sky model's ground showed)
            fpsCm.Lens = fpsLens;

            // FPS Flashlight Viewmodel Rig (local offset [0.18, -0.14, 0.34], Layer 12 Viewmodel, STRICTLY DISABLED AT SPAWN!)
            var swayPivotGo = new GameObject("FlashlightSwayPivot");
            swayPivotGo.transform.SetParent(camPivot.transform, false);
            swayPivotGo.transform.localPosition = new Vector3(0.23f, -0.225f, 0.49f);
            swayPivotGo.transform.localRotation = Quaternion.identity;

            GameObject vmRoot;
            Transform beamMuzzle = null;
            // grip position under the sway pivot and the torch's aim (pitch, yaw toward the crosshair, roll)
            // review 4: further right and lower, aimed in, so the barrel shows its side instead of its tail cap
            Vector3 ViewmodelPosePosition = new Vector3(0.11f, -0.03f, 0.03f);
            Vector3 ViewmodelPoseEuler = new Vector3(-2f, -9f, 34f);     // the roll brings the forearm in from the lower right and shows the knuckle row
            GameObject handModel = AssetDatabase.LoadAssetAtPath<GameObject>(Cretacea.Editor.Look.LookMaterials.ViewmodelHandModel);
            GameObject fpsRigPrefab = handModel != null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFlashlightFpsRig);
            if (handModel != null)
            {
                // Round 5: a scanned gloved right hand round a whole tactical torch (barrel, head, tail cap) with the
                // jacket sleeve running off the bottom of the screen. The model's lens points down its -Z; the pose
                // holds the torch at chest height right of centre, aimed a little in toward the crosshair.
                vmRoot = new GameObject("FlashlightViewmodel");
                vmRoot.transform.SetParent(swayPivotGo.transform, false);
                var handInst = (GameObject)PrefabUtility.InstantiatePrefab(handModel, vmRoot.transform);
                handInst.name = "VM_HandTorch";
                handInst.transform.localPosition = ViewmodelPosePosition;
                handInst.transform.localRotation = Quaternion.Euler(ViewmodelPoseEuler) * Quaternion.Euler(0f, 180f, 0f);
                Material[] handMats = Cretacea.Editor.Look.LookMaterials.EnsureViewmodelHandMaterials();
                foreach (MeshRenderer mr in handInst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.sharedMaterials = handMats;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                }
                beamMuzzle = handInst.transform.Find("Beam_Muzzle");
                boundFpsRigPrefab = true;
            }
            else if (fpsRigPrefab != null)
            {
                vmRoot = (GameObject)PrefabUtility.InstantiatePrefab(fpsRigPrefab, swayPivotGo.transform);
                vmRoot.name = "PF_FlashlightFpsRig";
                vmRoot.transform.localPosition = Vector3.zero;
                vmRoot.transform.localRotation = Quaternion.identity;
                boundFpsRigPrefab = true;
            }
            else
            {
                vmRoot = new GameObject("FlashlightFpsRig_Fallback");
                vmRoot.transform.SetParent(swayPivotGo.transform, false);
                boundFpsRigPrefab = false;
                warnings.Add($"'{PrefabFlashlightFpsRig}' not found; created fallback flashlight rig host.");
            }

            ViewmodelCameraConfig.SetHierarchyToViewmodelLayer(vmRoot);

            // Find or build SpotLight + BounceFillLight inside FPS Flashlight hierarchy
            Light spotLight = null;
            Light[] existingLights = vmRoot.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < existingLights.Length; i++)
            {
                if (existingLights[i].type == LightType.Spot)
                {
                    spotLight = existingLights[i];
                    break;
                }
            }

            Texture2D cookieTex = FindTextureAsset("flashlight_cookie");
            if (spotLight == null)
            {
                var spotGo = new GameObject("FlashlightSpotLight");
                spotGo.transform.SetParent(vmRoot.transform, false);
                spotGo.transform.localPosition = new Vector3(0f, 0f, 0.16f);
                spotLight = spotGo.AddComponent<Light>();
                spotLight.type = LightType.Spot;
                spotLight.lightUnit = LightUnit.Lumen;
                spotLight.range = 34f;
                spotLight.spotAngle = 25f;
                spotLight.innerSpotAngle = 14f;
                spotLight.intensity = 3200f;
                spotLight.useColorTemperature = true;
                spotLight.colorTemperature = 5200f;
                spotLight.shadows = LightShadows.Soft;
                if (cookieTex != null) spotLight.cookie = cookieTex;
                HDAdditionalLightData spotHd = spotGo.AddComponent<HDAdditionalLightData>();
                spotHd.affectsVolumetric = true;
                spotHd.SetLightDimmer(1f, 1.6f);
            }

            if (beamMuzzle != null)
            {
                // the beam leaves from the lens and follows the torch through every sway, bob and pull-back
                Transform model = beamMuzzle.parent;
                spotLight.transform.SetParent(model, false);
                spotLight.transform.position = beamMuzzle.position;
                spotLight.transform.rotation = Quaternion.LookRotation(model.TransformDirection(Vector3.back), model.up);
            }

            Light fillLight = null;
            for (int i = 0; i < existingLights.Length; i++)
            {
                if (existingLights[i].type == LightType.Point && existingLights[i].name.Contains("FlashlightBounce"))
                {
                    fillLight = existingLights[i];
                    break;
                }
            }

            if (fillLight == null)
            {
                var fillGo = new GameObject("FlashlightBounceFill");
                fillGo.transform.SetParent(vmRoot.transform, false);
                fillGo.transform.localPosition = new Vector3(0f, 0.04f, 0.25f);
                fillLight = fillGo.AddComponent<Light>();
                fillLight.type = LightType.Point;
                fillLight.lightUnit = LightUnit.Lumen;
                fillLight.range = 1.6f;
                fillLight.intensity = 4.5f;
                fillLight.useColorTemperature = true;
                fillLight.colorTemperature = 5200f;
                fillLight.shadows = LightShadows.None;
                HDAdditionalLightData fillHd = fillGo.AddComponent<HDAdditionalLightData>();
                fillHd.affectsVolumetric = false;
            }
            else
            {
                fillLight.intensity = 4.5f;
            }

            // IRONCLAD FL-1 ENFORCEMENT: Strictly disabled at Chapter 1 spawn until picked up from desk!
            spotLight.enabled = false;
            fillLight.enabled = false;
            vmRoot.SetActive(false);
            viewmodelDisabledAtSpawn = !vmRoot.activeSelf;

            // Core Player Components
            PlayerController player = playerRoot.AddComponent<PlayerController>();
            FlashlightController flashlight = playerRoot.AddComponent<FlashlightController>();
            PlayerInteractor interactor = playerRoot.AddComponent<PlayerInteractor>();
            playerRoot.AddComponent<SurfaceAudioDetector>();

            // Wire PlayerController serialized fields
            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("cameraPivot").objectReferenceValue = camPivot.transform;
            playerSo.FindProperty("flashlightController").objectReferenceValue = flashlight;
            playerSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire FlashlightController serialized fields
            var flSo = new SerializedObject(flashlight);
            flSo.FindProperty("viewmodelRoot").objectReferenceValue = vmRoot;
            flSo.FindProperty("swayPivot").objectReferenceValue = swayPivotGo.transform;
            flSo.FindProperty("cameraReference").objectReferenceValue = camPivot.transform;
            flSo.FindProperty("playerController").objectReferenceValue = player;
            flSo.FindProperty("spotLight").objectReferenceValue = spotLight;
            flSo.FindProperty("bounceFillLight").objectReferenceValue = fillLight;
            if (cookieTex != null) flSo.FindProperty("flashlightCookie").objectReferenceValue = cookieTex;
            flSo.ApplyModifiedPropertiesWithoutUndo();

            // Wire PlayerInteractor serialized fields
            var interSo = new SerializedObject(interactor);
            interSo.FindProperty("cameraOrigin").objectReferenceValue = camPivot.transform;
            interSo.FindProperty("playerController").objectReferenceValue = player;
            interSo.ApplyModifiedPropertiesWithoutUndo();

            // Main Camera (CinemachineBrain + HDRP Camera + Viewmodel Layer 12 Excluded!)
            var mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetPositionAndRotation(camPivot.transform.position, camPivot.transform.rotation);

            Camera mainCam = mainCamGo.AddComponent<Camera>();
            mainCam.fieldOfView = 68f;
            mainCam.nearClipPlane = 0.06f;
            mainCam.farClipPlane = 12000f;
            mainCam.cullingMask = ~(GameLayers.Mask(GameLayers.Viewmodel) | GameLayers.Mask(GameLayers.RainBlocker));

            HDAdditionalCameraData hdCam = mainCamGo.AddComponent<HDAdditionalCameraData>();
            hdCam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hdCam.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            hdCam.taaSharpenStrength = 0.55f;
            hdCam.taaAntiFlicker = 0.6f;
            hdCam.dithering = true;      // no banding in dark fog, night sky and the vignette
            hdCam.stopNaNs = true;       // a bad pixel must never bloom across the screen
            mainCamGo.AddComponent<AudioListener>();
            mainCamGo.AddComponent<CinemachineBrain>();
            ViewmodelCameraConfig vmCamCfg = mainCamGo.AddComponent<ViewmodelCameraConfig>();
            vmCamCfg.Apply();

            // CameraDirector + Cutscene CinemachineCamera
            var camDirectorGo = new GameObject("[CameraDirector]");
            var cutsceneCamGo = new GameObject("CM_CutsceneCamera");
            cutsceneCamGo.transform.SetParent(camDirectorGo.transform, false);
            CinemachineCamera cutCm = cutsceneCamGo.AddComponent<CinemachineCamera>();
            cutCm.Priority = 5;
            var cutLens = cutCm.Lens;
            cutLens.FieldOfView = 50f;
            cutLens.NearClipPlane = 0.1f;
            cutLens.FarClipPlane = 12000f;
            cutCm.Lens = cutLens;

            CameraDirector camDirector = camDirectorGo.AddComponent<CameraDirector>();
            var cdSo = new SerializedObject(camDirector);
            cdSo.FindProperty("fpsCamera").objectReferenceValue = fpsCm;
            cdSo.FindProperty("cutsceneCamera").objectReferenceValue = cutCm;
            cdSo.FindProperty("shakeHost").objectReferenceValue = shakeHost.transform;
            cdSo.FindProperty("playerController").objectReferenceValue = player;
            cdSo.ApplyModifiedPropertiesWithoutUndo();

            refs.PlayerRoot = playerRoot;
            refs.Player = player;
            refs.Flashlight = flashlight;
            refs.CameraDirector = camDirector;
            refs.MainCamera = mainCam;
            return refs;
        }

        private static void EnsureViewmodelCustomPassVolumeInScene()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabViewmodelPassVol);
            if (prefab != null)
            {
                GameObject vol = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                vol.name = "PF_ViewmodelCustomPassVolume";
                return;
            }

            ViewmodelCameraConfig.EnsureGlobalViewmodelCustomPassVolume();
        }

        private static StormRainOcclusionController InstantiateStormRainSystem(Transform followTarget, List<string> warnings)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabStormRainSystem);
            GameObject rainGo;
            if (prefab != null)
            {
                rainGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                rainGo.name = "PF_StormRainSystem";
            }
            else
            {
                rainGo = new GameObject("StormRainSystem");
                rainGo.AddComponent<StormRainOcclusionController>();
                warnings.Add($"'{PrefabStormRainSystem}' not found; created fallback StormRainOcclusionController.");
            }

            StormRainOcclusionController ctrl = rainGo.GetComponent<StormRainOcclusionController>();
            if (ctrl != null && followTarget != null)
            {
                var so = new SerializedObject(ctrl);
                SerializedProperty ft = so.FindProperty("followTarget");
                if (ft != null) ft.objectReferenceValue = followTarget;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return ctrl;
        }

        private const float CalibratedLightningPeakLux = 55f;

        private static AtmosphereController BuildAtmosphereAndLightingRig(
            Transform managersRoot,
            StormRainOcclusionController rainCtrl,
            out int boundPresetCount,
            List<string> warnings,
            bool isMainMenu = false)
        {
            // Verify all 5 calibrated HDRP Atmosphere Volume Profiles exist in project
            string[] allPresetPaths =
            {
                PresetOpeningDuskPath,
                PresetBlackoutStormPath,
                PresetInteriorEmergencyPath,
                PresetPowerRestoredPath,
                PresetDawnCoastPath
            };
            boundPresetCount = 0;
            VolumeProfile stormProfile = null;
            VolumeProfile dawnProfile = null;
            for (int i = 0; i < allPresetPaths.Length; i++)
            {
                VolumeProfile prof = AssetDatabase.LoadAssetAtPath<VolumeProfile>(allPresetPaths[i]);
                if (prof != null)
                {
                    boundPresetCount++;
                    if (allPresetPaths[i] == PresetBlackoutStormPath) stormProfile = prof;
                    if (allPresetPaths[i] == PresetDawnCoastPath) dawnProfile = prof;
                }
                else
                {
                    warnings.Add($"Missing HDRP Atmosphere profile '{allPresetPaths[i]}'.");
                }
            }

            // P1-4 FIX: Calibrate Preset_DawnCoast.asset PhysicallyBasedSky tints, Exposure, & coastal Fog
            if (dawnProfile != null)
            {
                CalibrateDawnCoastPresetAsset(dawnProfile);
            }

            // Global Atmosphere Volume:
            // - MainMenu uses a dedicated dark midnight GradientSky storm profile (ZERO grey twilight blowout!).
            // - MainWorld uses MainWorld_RuntimeAtmosphere.asset (storm GradientSky + calibrated DawnCoast PhysicallyBasedSky).
            var volGo = new GameObject("[GlobalAtmosphereVolume]");
            volGo.transform.SetParent(managersRoot, false);
            Volume globalVol = volGo.AddComponent<Volume>();
            globalVol.isGlobal = true;
            globalVol.priority = 10f;

            VolumeProfile activeProfile = isMainMenu
                ? BuildOrUpdateMainMenuStormAtmosphereProfile(stormProfile)
                : BuildOrUpdateUnifiedRuntimeAtmosphereProfile(stormProfile);

            if (activeProfile != null)
            {
                globalVol.sharedProfile = activeProfile;
            }
            else if (stormProfile != null)
            {
                globalVol.sharedProfile = stormProfile;
            }
            else
            {
                VolumeProfile anyProf = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PresetOpeningDuskPath);
                if (anyProf != null) globalVol.sharedProfile = anyProf;
            }

            // Key Directional Sun/Moon Light (Lux physical units)
            var keyGo = new GameObject("KeyDirectionalLight");
            keyGo.transform.SetParent(managersRoot, false);
            keyGo.transform.rotation = isMainMenu ? Quaternion.Euler(26f, 152f, 0f) : Quaternion.Euler(34f, 135f, 0f);
            Light keyLight = keyGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.lightUnit = LightUnit.Lux;
            keyLight.intensity = isMainMenu ? 0.65f : 1.2f;
            keyLight.useColorTemperature = true;
            keyLight.colorTemperature = isMainMenu ? 8400f : 7500f;
            keyLight.color = isMainMenu ? new Color(0.44f, 0.60f, 0.94f) : new Color(0.68f, 0.80f, 1.0f);
            keyLight.shadows = LightShadows.Soft;
            HDAdditionalLightData keyHd = keyGo.AddComponent<HDAdditionalLightData>();
            keyHd.affectsVolumetric = true;
            keyHd.SetLightDimmer(1f, isMainMenu ? 1.4f : 1.2f);
            keyHd.useContactShadow.useOverride = true;
            keyHd.useContactShadow.@override = true;

            // P1-11 FIX: Storm Lightning Flash Directional Light capped at 55 Lux (electric storm flash, ZERO whiteout at EV 0.6!)
            var lightnGo = new GameObject("LightningFlashLight");
            lightnGo.transform.SetParent(managersRoot, false);
            lightnGo.transform.rotation = Quaternion.Euler(46f, 25f, 0f);
            Light lightnLight = lightnGo.AddComponent<Light>();
            lightnLight.type = LightType.Directional;
            lightnLight.lightUnit = LightUnit.Lux;
            lightnLight.intensity = CalibratedLightningPeakLux;
            lightnLight.color = new Color(0.75f, 0.86f, 1.0f);
            lightnLight.shadows = LightShadows.None;
            lightnLight.enabled = false;
            HDAdditionalLightData lightnHd = lightnGo.AddComponent<HDAdditionalLightData>();
            lightnHd.affectsVolumetric = true;
            lightnHd.SetLightDimmer(1f, 0.65f);

            // Wire AtmosphereController with calibrated 5-preset array (including Eastern Ocean sunrise & capped lightningPeakLux)
            AtmosphereController atmo = CreateManager<AtmosphereController>(managersRoot, "[AtmosphereController]");
            var so = new SerializedObject(atmo);
            so.FindProperty("globalAtmosphereVolume").objectReferenceValue = globalVol;
            so.FindProperty("keyDirectionalLight").objectReferenceValue = keyLight;
            so.FindProperty("lightningFlashLight").objectReferenceValue = lightnLight;
            SerializedProperty peakLuxProp = so.FindProperty("lightningPeakLux");
            if (peakLuxProp != null) peakLuxProp.floatValue = CalibratedLightningPeakLux;

            SerializeAtmospherePresets(so, isMainMenu);

            if (rainCtrl != null)
            {
                ParticleSystem[] rainParticles = rainCtrl.GetComponentsInChildren<ParticleSystem>(true);
                SerializedProperty extRain = so.FindProperty("exteriorRainSystems");
                if (extRain != null && rainParticles.Length > 0)
                {
                    extRain.arraySize = rainParticles.Length;
                    for (int i = 0; i < rainParticles.Length; i++)
                        extRain.GetArrayElementAtIndex(i).objectReferenceValue = rainParticles[i];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return atmo;
        }

        private static void SerializeAtmospherePresets(SerializedObject atmoSo, bool isMainMenu)
        {
            SerializedProperty presetsProp = atmoSo.FindProperty("presets");
            if (presetsProp == null) return;

            AtmosphereProfile[] calibrated =
            {
                new AtmosphereProfile
                {
                    preset = AtmospherePreset.GoldenDay,
                    skyExposureEV = 11.5f,
                    fixedExposureEV = 11.2f,
                    keyLightLux = 62000f,
                    keyLightTemperatureK = 4200f,
                    keyLightFilter = new Color(1.0f, 0.92f, 0.80f),
                    keyLightEuler = new Vector3(16f, -55f, 0f),
                    enableVolumetricFog = true,
                    fogMeanFreePath = 220f,
                    fogBaseHeight = 2f,
                    fogMaximumHeight = 95f,
                    fogAlbedo = new Color(0.85f, 0.72f, 0.58f),
                    globalWetness = 0.0f,
                    rainIntensity = 0.0f
                },
                new AtmosphereProfile
                {
                    preset = AtmospherePreset.DuskTransition,
                    skyExposureEV = 7.5f,
                    fixedExposureEV = 7.5f,
                    keyLightLux = 8000f,
                    keyLightTemperatureK = 3200f,
                    keyLightFilter = new Color(0.95f, 0.78f, 0.68f),
                    keyLightEuler = new Vector3(8f, -68f, 0f),
                    enableVolumetricFog = true,
                    fogMeanFreePath = 140f,
                    fogBaseHeight = 4f,
                    fogMaximumHeight = 85f,
                    fogAlbedo = new Color(0.42f, 0.45f, 0.52f),
                    globalWetness = 0.35f,
                    rainIntensity = 0.25f
                },
                new AtmosphereProfile
                {
                    preset = AtmospherePreset.NightCalm,
                    skyExposureEV = isMainMenu ? 0.25f : 0.8f,
                    fixedExposureEV = isMainMenu ? 0.72f : 1.2f,
                    keyLightLux = isMainMenu ? 0.65f : 1.2f,
                    keyLightTemperatureK = isMainMenu ? 8400f : 7500f,
                    keyLightFilter = isMainMenu ? new Color(0.44f, 0.60f, 0.94f) : new Color(0.68f, 0.80f, 1.0f),
                    keyLightEuler = isMainMenu ? new Vector3(26f, 152f, 0f) : new Vector3(34f, 135f, 0f),
                    enableVolumetricFog = true,
                    fogMeanFreePath = isMainMenu ? 32f : 75f,
                    fogBaseHeight = isMainMenu ? 0.5f : 3f,
                    fogMaximumHeight = isMainMenu ? 18f : 65f,
                    fogAlbedo = isMainMenu ? new Color(0.032f, 0.056f, 0.090f) : new Color(0.13f, 0.19f, 0.27f),
                    globalWetness = isMainMenu ? 1.0f : 0.75f,
                    rainIntensity = isMainMenu ? 1.0f : 0.45f
                },
                new AtmosphereProfile
                {
                    preset = AtmospherePreset.NightStormBlackout,
                    skyExposureEV = 0.30f,
                    fixedExposureEV = isMainMenu ? 0.72f : 0.6f,
                    keyLightLux = 0.70f,
                    keyLightTemperatureK = 8300f,
                    keyLightFilter = new Color(0.50f, 0.66f, 0.95f),
                    keyLightEuler = new Vector3(28f, 145f, 0f),
                    enableVolumetricFog = true,
                    fogMeanFreePath = isMainMenu ? 32f : 42f,
                    fogBaseHeight = isMainMenu ? 0.5f : 2.5f,
                    fogMaximumHeight = isMainMenu ? 18f : 55f,
                    fogAlbedo = isMainMenu ? new Color(0.032f, 0.056f, 0.090f) : new Color(0.086f, 0.133f, 0.188f),
                    globalWetness = 1.0f,
                    rainIntensity = 1.0f
                },
                new AtmosphereProfile
                {
                    // P1-4 FIX: Light shines WESTWARD (-90 deg yaw) so PhysicallyBasedSky sun disc rises
                    // directly OVER THE OPEN EASTERN OCEAN (+X) in front of the Dawn Coast player!
                    preset = AtmospherePreset.DawnCoast,
                    skyExposureEV = 9.2f,
                    fixedExposureEV = 8.9f,
                    keyLightLux = 28000f,
                    keyLightTemperatureK = 3500f,
                    keyLightFilter = new Color(1.0f, 0.86f, 0.66f),
                    keyLightEuler = new Vector3(7.5f, -90f, 0f),
                    enableVolumetricFog = true,
                    fogMeanFreePath = 480f,
                    fogBaseHeight = -18f,
                    fogMaximumHeight = 52f,
                    fogAlbedo = new Color(1.0f, 0.80f, 0.56f),
                    globalWetness = 0.35f,
                    rainIntensity = 0.0f
                }
            };

            presetsProp.arraySize = calibrated.Length;
            for (int i = 0; i < calibrated.Length; i++)
            {
                SerializedProperty elem = presetsProp.GetArrayElementAtIndex(i);
                AtmosphereProfile p = calibrated[i];
                elem.FindPropertyRelative("preset").enumValueIndex = (int)p.preset;
                elem.FindPropertyRelative("hdriCubemap").objectReferenceValue = p.hdriCubemap;
                elem.FindPropertyRelative("skyExposureEV").floatValue = p.skyExposureEV;
                elem.FindPropertyRelative("fixedExposureEV").floatValue = p.fixedExposureEV;
                elem.FindPropertyRelative("keyLightLux").floatValue = p.keyLightLux;
                elem.FindPropertyRelative("keyLightTemperatureK").floatValue = p.keyLightTemperatureK;
                elem.FindPropertyRelative("keyLightFilter").colorValue = p.keyLightFilter;
                elem.FindPropertyRelative("keyLightEuler").vector3Value = p.keyLightEuler;
                elem.FindPropertyRelative("enableVolumetricFog").boolValue = p.enableVolumetricFog;
                elem.FindPropertyRelative("fogMeanFreePath").floatValue = p.fogMeanFreePath;
                elem.FindPropertyRelative("fogBaseHeight").floatValue = p.fogBaseHeight;
                elem.FindPropertyRelative("fogMaximumHeight").floatValue = p.fogMaximumHeight;
                elem.FindPropertyRelative("fogAlbedo").colorValue = p.fogAlbedo;
                elem.FindPropertyRelative("globalWetness").floatValue = p.globalWetness;
                elem.FindPropertyRelative("rainIntensity").floatValue = p.rainIntensity;
            }
        }

        private static AudioManager BuildAudioManager(
            Transform managersRoot,
            out bool boundMixer,
            out bool boundBank,
            List<string> warnings)
        {
            AudioManager mgr = CreateManager<AudioManager>(managersRoot, "[AudioManager]");

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerAssetPath);
            SoundBank bank = AssetDatabase.LoadAssetAtPath<SoundBank>(SoundBankAssetPath);
            boundMixer = mixer != null;
            boundBank = bank != null;

            if (!boundMixer) warnings.Add($"AudioMixer not found at '{MixerAssetPath}'.");
            if (!boundBank) warnings.Add($"SoundBank not found at '{SoundBankAssetPath}'.");

            var so = new SerializedObject(mgr);
            if (mixer != null) so.FindProperty("masterMixer").objectReferenceValue = mixer;
            if (bank != null) so.FindProperty("soundBank").objectReferenceValue = bank;
            so.ApplyModifiedPropertiesWithoutUndo();
            return mgr;
        }

        private static HudController BuildHudController(Transform managersRoot)
        {
            HudController hud = CreateManager<HudController>(managersRoot, "[HudController]");
            Sprite holdRing = FindSpriteAsset("interact_ring");
            Sprite damageVig = FindSpriteAsset("damage_red");

            if (holdRing != null || damageVig != null)
            {
                var so = new SerializedObject(hud);
                if (holdRing != null) so.FindProperty("customHoldRingSprite").objectReferenceValue = holdRing;
                if (damageVig != null) so.FindProperty("customDamageVignetteSprite").objectReferenceValue = damageVig;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return hud;
        }

        private static JournalController BuildJournalController(Transform managersRoot)
        {
            JournalController journal = CreateManager<JournalController>(managersRoot, "[JournalController]");
            Sprite routeMap = FindSpriteAsset("hud_minimap_route");
            if (routeMap != null)
            {
                var so = new SerializedObject(journal);
                so.FindProperty("routeMapSprite").objectReferenceValue = routeMap;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return journal;
        }

        private static MenuController BuildMenuController(Transform managersRoot, bool isMainMenu)
        {
            MenuController menu = CreateManager<MenuController>(managersRoot, "[MenuController]");
            var so = new SerializedObject(menu);
            so.FindProperty("isMainMenuScene").boolValue = isMainMenu;
            so.FindProperty("gameplaySceneName").stringValue = "MainWorld";
            so.FindProperty("mainMenuSceneName").stringValue = "MainMenu";
            so.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }

        private static DinoSpawner BuildDinoSpawner(Transform managersRoot, out int boundPrefabCount, List<string> warnings)
        {
            DinoSpawner spawner = CreateManager<DinoSpawner>(managersRoot, "[DinoSpawner]");
            var spawnContainer = new GameObject("ActiveDinosaurs");
            spawnContainer.transform.SetParent(spawner.transform, false);

            GameObject raptorPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDinoRaptor);
            GameObject trexPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDinoTrex);
            GameObject brachioPf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDinoBrachio);
            GameObject trikePf = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDinoTrike);

            DinoConfig raptorCfg = AssetDatabase.LoadAssetAtPath<DinoConfig>(ConfigDinoRaptor);
            DinoConfig trexCfg = AssetDatabase.LoadAssetAtPath<DinoConfig>(ConfigDinoTrex);
            DinoConfig brachioCfg = AssetDatabase.LoadAssetAtPath<DinoConfig>(ConfigDinoBrachio);
            DinoConfig trikeCfg = AssetDatabase.LoadAssetAtPath<DinoConfig>(ConfigDinoTrike);

            boundPrefabCount = 0;
            if (raptorPf != null) boundPrefabCount++; else warnings.Add($"Missing dinosaur prefab '{PrefabDinoRaptor}'.");
            if (trexPf != null) boundPrefabCount++; else warnings.Add($"Missing dinosaur prefab '{PrefabDinoTrex}'.");
            if (brachioPf != null) boundPrefabCount++; else warnings.Add($"Missing dinosaur prefab '{PrefabDinoBrachio}'.");
            if (trikePf != null) boundPrefabCount++; else warnings.Add($"Missing dinosaur prefab '{PrefabDinoTrike}'.");

            var so = new SerializedObject(spawner);
            so.FindProperty("raptorPrefab").objectReferenceValue = raptorPf;
            so.FindProperty("trexPrefab").objectReferenceValue = trexPf;
            so.FindProperty("brachiosaurusPrefab").objectReferenceValue = brachioPf;
            so.FindProperty("triceratopsPrefab").objectReferenceValue = trikePf;
            so.FindProperty("raptorConfig").objectReferenceValue = raptorCfg;
            so.FindProperty("trexConfig").objectReferenceValue = trexCfg;
            so.FindProperty("brachiosaurusConfig").objectReferenceValue = brachioCfg;
            so.FindProperty("triceratopsConfig").objectReferenceValue = trikeCfg;
            so.FindProperty("spawnContainer").objectReferenceValue = spawnContainer.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return spawner;
        }

        private static StoryDirector BuildStoryDirector(
            Transform managersRoot,
            PlayerController player,
            FlashlightController flashlight,
            CameraDirector cameraDirector,
            LightGroupController lightGroups,
            AtmosphereController atmosphere,
            WorldRegistry world,
            out bool boundPathLampSparks)
        {
            StoryDirector story = CreateManager<StoryDirector>(managersRoot, "[StoryDirector]");

            // P0-4 FIX: Auto-wire PathLampSparksVfx (and ShutterSparksVfx) onto StoryDirector!
            ParticleSystem pathLampSparks = FindOrCreateElectricalSparksVfx(
                preferredNameTokens: new[] { "PathLampSparks", "SparkLamp", "PathSparks" },
                anchorId: "spark_lamp",
                fallbackPos: new Vector3(62.2f, 4.1f, 15.65f),
                createdObjectName: "PathLampSparksVfx");

            ParticleSystem shutterSparks = FindOrCreateElectricalSparksVfx(
                preferredNameTokens: new[] { "ShutterSparks", "LobbyShutterSparks" },
                anchorId: "lobby_shutter_sparks",
                fallbackPos: new Vector3(-28.0f, 3.0f, -15.5f),
                createdObjectName: "ShutterSparksVfx");

            boundPathLampSparks = pathLampSparks != null;

            var so = new SerializedObject(story);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("flashlight").objectReferenceValue = flashlight;
            so.FindProperty("cameraDirector").objectReferenceValue = cameraDirector;
            so.FindProperty("lightGroups").objectReferenceValue = lightGroups;
            so.FindProperty("atmosphere").objectReferenceValue = atmosphere;
            so.FindProperty("world").objectReferenceValue = world;
            so.FindProperty("autoStartOnAwake").boolValue = true;
            so.FindProperty("editorStartCheckpoint").enumValueIndex = (int)CheckpointId.Night;

            SerializedProperty pathSparksProp = so.FindProperty("pathLampSparksVfx");
            if (pathSparksProp != null) pathSparksProp.objectReferenceValue = pathLampSparks;

            SerializedProperty shutterSparksProp = so.FindProperty("shutterSparksVfx");
            if (shutterSparksProp != null) shutterSparksProp.objectReferenceValue = shutterSparks;

            so.ApplyModifiedPropertiesWithoutUndo();
            return story;
        }

        private static ParticleSystem FindOrCreateElectricalSparksVfx(
            string[] preferredNameTokens,
            string anchorId,
            Vector3 fallbackPos,
            string createdObjectName)
        {
            // 1. Search existing scene ParticleSystems created by Zone builders (e.g. Zone2's PathLampSparksVfx)
            ParticleSystem[] existing = UnityEngine.Object.FindObjectsByType<ParticleSystem>();
            for (int i = 0; i < existing.Length; i++)
            {
                ParticleSystem ps = existing[i];
                if (ps == null) continue;
                for (int t = 0; t < preferredNameTokens.Length; t++)
                {
                    if (ps.gameObject.name.IndexOf(preferredNameTokens[t], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return ps;
                    }
                }
            }

            // 2. Locate exact anchor transform if present in scene
            Transform anchorTransform = null;
            Vector3 spawnPos = fallbackPos;
            WorldAnchor[] anchors = UnityEngine.Object.FindObjectsByType<WorldAnchor>();
            for (int i = 0; i < anchors.Length; i++)
            {
                if (string.Equals(anchors[i].AnchorId, anchorId, StringComparison.OrdinalIgnoreCase))
                {
                    anchorTransform = anchors[i].transform;
                    spawnPos = anchorTransform.position;
                    break;
                }
            }

            // Also check proximity to anchor in case Zone builder named the ParticleSystem differently
            if (anchorTransform != null)
            {
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null && Vector3.Distance(existing[i].transform.position, spawnPos) <= 1.25f)
                    {
                        return existing[i];
                    }
                }
            }

            // 3. Create calibrated HDRP amber-white electrical arc spark ParticleSystem cleanly at anchor
            var go = new GameObject(createdObjectName);
            if (anchorTransform != null)
            {
                go.transform.SetParent(anchorTransform, false);
                go.transform.localPosition = Vector3.zero;
            }
            else
            {
                go.transform.position = spawnPos;
            }

            ParticleSystem created = go.AddComponent<ParticleSystem>();
            var main = created.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1.0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.4f, 6.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.88f, 0.55f, 1.0f),
                new Color(1.0f, 0.52f, 0.14f, 1.0f));
            main.gravityModifier = 1.65f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;

            var emission = created.emission;
            emission.enabled = true;
            emission.rateOverTime = 2.0f;

            var shape = created.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var col = created.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.bounce = 0.45f;
            col.dampen = 0.25f;

            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.renderMode = ParticleSystemRenderMode.Stretch;
                psr.velocityScale = 0.08f;
                psr.lengthScale = 2.2f;
                Material sparkMat = GetOrCreateSparkParticleMaterial();
                if (sparkMat != null) psr.sharedMaterial = sparkMat;
            }

            return created;
        }

        private static Material GetOrCreateSparkParticleMaterial()
        {
            const string sparkMatPath = "Assets/Art/Materials/World/MAT_ElectricalSparksVfx.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(sparkMatPath);
            if (mat != null) return mat;

            WorldMaterialPalette.EnsureFolder("Assets/Art/Materials/World");
            Shader unlit = Shader.Find("HDRP/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (unlit == null) return null;

            mat = new Material(unlit) { name = "MAT_ElectricalSparksVfx" };
            if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", new Color(1.0f, 0.78f, 0.34f, 1.0f));
            if (mat.HasProperty("_EmissiveColor")) mat.SetColor("_EmissiveColor", new Color(1.0f, 0.72f, 0.25f, 1.0f) * 18f);
            if (mat.HasProperty("_SurfaceType")) mat.SetFloat("_SurfaceType", 1f);
            if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 1f);
            AssetDatabase.CreateAsset(mat, sparkMatPath);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static int BuildAudioReverbZones()
        {
            var root = new GameObject("[AudioReverbZones]");
            (string name, Vector3 pos, float minDist, float maxDist, AudioReverbPreset preset)[] specs =
            {
                ("Reverb_VC_ConcreteOffices",   new Vector3(-36f, 1.7f, 25f),   24f, 44f, AudioReverbPreset.Hallway),
                ("Reverb_VC_LobbyAtrium",       new Vector3(-31f, 5.0f, -6f),   18f, 34f, AudioReverbPreset.Auditorium),
                ("Reverb_VC_FeedPrepTile",      new Vector3(-2f,  1.7f, 2f),    12f, 22f, AudioReverbPreset.Bathroom),
                ("Reverb_ServicePaths_Canyon",  new Vector3(56f,  2.5f, 4f),    24f, 44f, AudioReverbPreset.Alley),
                ("Reverb_OldYard_MetalWorkshop",new Vector3(437f, 2.5f, -16f),  16f, 30f, AudioReverbPreset.Hangar),
                ("Reverb_ExteriorStorm_Valley", new Vector3(240f, 5.0f, 0f),    170f, 260f, AudioReverbPreset.Forest)
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var go = new GameObject(specs[i].name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = specs[i].pos;
                AudioReverbZone rz = go.AddComponent<AudioReverbZone>();
                rz.minDistance = specs[i].minDist;
                rz.maxDistance = specs[i].maxDist;
                rz.reverbPreset = specs[i].preset;
            }

            return specs.Length;
        }

        private static void BuildAndBakeNavMeshes(
            bool bakeNow,
            out bool raptorBaked,
            out bool trexBaked,
            List<string> warnings)
        {
            raptorBaked = false;
            trexBaked = false;

            var navRoot = new GameObject("[Navigation]");
            // Strictly include ONLY Default (0), Environment (3), and Door (11) — excluding Trigger, Viewmodel, RainBlocker, Interactable, Player, Dinosaur!
            int navLayerMask = GameLayers.Mask(GameLayers.Default)
                             | GameLayers.Mask(GameLayers.Environment)
                             | GameLayers.Mask(GameLayers.Door);

            NavMeshSurface raptorSurf = CreateNavMeshSurface(navRoot.transform, "NavMesh_Agent_Raptor", "Agent_Raptor", navLayerMask);
            NavMeshSurface trexSurf = CreateNavMeshSurface(navRoot.transform, "NavMesh_Agent_TRex", "Agent_TRex", navLayerMask);

            if (bakeNow)
            {
                try
                {
                    raptorSurf.BuildNavMesh();
                    raptorBaked = raptorSurf.navMeshData != null;
                    if (!raptorBaked) warnings.Add("Agent_Raptor NavMeshSurface baked with null navMeshData.");
                }
                catch (Exception ex)
                {
                    warnings.Add($"Agent_Raptor NavMesh bake error: {ex.Message}");
                }

                try
                {
                    trexSurf.BuildNavMesh();
                    trexBaked = trexSurf.navMeshData != null;
                    if (!trexBaked) warnings.Add("Agent_TRex NavMeshSurface baked with null navMeshData.");
                }
                catch (Exception ex)
                {
                    warnings.Add($"Agent_TRex NavMesh bake error: {ex.Message}");
                }
            }
        }

        private static NavMeshSurface CreateNavMeshSurface(Transform parent, string name, string agentTypeName, int layerMask)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            NavMeshSurface surf = go.AddComponent<NavMeshSurface>();
            surf.agentTypeID = DinoControllerBase.ResolveNavMeshAgentTypeId(agentTypeName);
            surf.collectObjects = CollectObjects.All;
            surf.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surf.layerMask = layerMask;
            return surf;
        }

        // =========================================================================
        // LIVE 3D HDRP MAIN MENU SCENE (Assets/Scenes/MainMenu.unity)
        // P1-1 AAA OVERHAUL: Fully enclosed stormy Visitor Center Atrium, overhead
        // steel-truss glass skylight with live rain shafts, dark rainforest storm
        // exterior backdrop (ZERO open grey horizon void!), and dramatic 3/4 hero
        // T. rex skeleton mounted flush inside a dark granite museum plinth!
        // =========================================================================

        private static MainMenuBuildStats BuildMainMenuScene(WorldMaterialPalette palette, List<string> warnings)
        {
            var stats = new MainMenuBuildStats();
            Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var stage = new GameObject("Menu_StormyAtriumVignette");

            // 1. Wet storm-slicked Visitor Center Atrium floor & reflective rain puddles
            GameObject floor = WorldArchitectureKit.CreateBox(stage.transform, "AtriumFloor_Wet", new Vector3(0.25f, -0.15f, 3.75f), new Vector3(34.5f, 0.30f, 29.5f), palette.ConcreteFloorWet, addCollider: true);
            WorldArchitectureKit.ApplySurface(floor, SurfaceType.Concrete, palette);
            WorldArchitectureKit.BuildDecal(stage.transform, "MenuPuddle_Center", new Vector3(2.2f, 0.05f, 5.2f), new Vector3(90f, 0f, 0f), new Vector3(11.5f, 8.5f, 0.6f), palette.DecalPuddleWet, 0.92f);
            WorldArchitectureKit.BuildDecal(stage.transform, "MenuPuddle_Front", new Vector3(-1.8f, 0.05f, 0.8f), new Vector3(90f, 0f, 0f), new Vector3(8.5f, 6.2f, 0.6f), palette.DecalPuddleWet, 0.88f);
            WorldArchitectureKit.BuildDecal(stage.transform, "MenuPuddle_EastRain", new Vector3(9.2f, 0.05f, 9.4f), new Vector3(90f, 0f, 0f), new Vector3(9.0f, 7.5f, 0.6f), palette.DecalPuddleWet, 0.94f);

            // 2. FULL 4-WALL ARCHITECTURAL ENCLOSURE (X = -16.5 .. +17.0, Z = -10.5 .. +18.0, H = 11.6 m)
            // North Facade: Grand mullioned curtain-wall looking into stormy jungle night
            WorldArchitectureKit.BuildWallWithOpenings(
                stage.transform,
                new Vector3(-16.5f, 0f, 18.0f),
                new Vector3(17.0f, 0f, 18.0f),
                0f, 11.6f, 0.40f, palette.ConcreteBoardform,
                new[]
                {
                    OpeningSpec.Window(9.2f, 11.4f, 0.85f, 9.1f, mullions: 3, frameMat: palette.DarkPaintedMetal, glassMat: palette.GlassArchitectural),
                    OpeningSpec.Window(23.8f, 12.2f, 0.85f, 9.1f, mullions: 3, frameMat: palette.DarkPaintedMetal, glassMat: palette.GlassArchitectural)
                },
                addSkirting: true, palette: palette, wallName: "MenuWall_NorthGlassFacade", includeInOcclusion: false);

            // East Wall: Tall mullioned rain-window bays + architectural upper mezzanine balcony (eliminates right-side horizon void!)
            WorldArchitectureKit.BuildWallWithOpenings(
                stage.transform,
                new Vector3(17.0f, 0f, -10.5f),
                new Vector3(17.0f, 0f, 18.0f),
                0f, 11.6f, 0.40f, palette.ConcreteBoardform,
                new[]
                {
                    OpeningSpec.Window(8.5f, 9.6f, 0.90f, 8.9f, mullions: 3, frameMat: palette.DarkPaintedMetal, glassMat: palette.GlassArchitectural),
                    OpeningSpec.Window(20.6f, 9.6f, 0.90f, 8.9f, mullions: 3, frameMat: palette.DarkPaintedMetal, glassMat: palette.GlassArchitectural)
                },
                addSkirting: true, palette: palette, wallName: "MenuWall_EastGlassFacade", includeInOcclusion: false);

            // East Upper Mezzanine Gallery Fascia & Steel Guardrail (breaks up right wall with rich museum architecture!)
            WorldArchitectureKit.CreateBox(stage.transform, "EastMezzanine_Slab", new Vector3(15.3f, 5.9f, 3.75f), new Vector3(3.1f, 0.34f, 28.2f), palette.ConcreteBoardform, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "EastMezzanine_Fascia", new Vector3(13.85f, 5.92f, 3.75f), new Vector3(0.18f, 0.42f, 28.2f), palette.DarkPaintedMetal, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "EastMezzanine_TopRail", new Vector3(13.85f, 7.05f, 3.75f), new Vector3(0.08f, 0.08f, 28.2f), palette.BrushedSteel, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "EastMezzanine_MidRail", new Vector3(13.85f, 6.55f, 3.75f), new Vector3(0.06f, 0.05f, 28.2f), palette.DarkPaintedMetal, addCollider: false);
            for (int mp = -2; mp <= 4; mp++)
            {
                float pz = mp * 4.2f;
                WorldArchitectureKit.CreateBox(stage.transform, $"EastMezzanine_Post_{mp}", new Vector3(13.85f, 6.55f, pz), new Vector3(0.07f, 1.10f, 0.07f), palette.DarkPaintedMetal, addCollider: false);
            }

            // West Architectural Wall (dark brooding concrete backdrop behind left-side menu UI)
            WorldArchitectureKit.BuildWallWithOpenings(
                stage.transform,
                new Vector3(-16.5f, 0f, -10.5f),
                new Vector3(-16.5f, 0f, 18.0f),
                0f, 11.6f, 0.42f, palette.ConcreteBoardform,
                Array.Empty<OpeningSpec>(),
                addSkirting: true, palette: palette, wallName: "MenuWall_WestSolid", includeInOcclusion: true);

            // South Rear Wall behind camera (encloses reflections & eliminates any sky leak behind camera)
            WorldArchitectureKit.BuildWallWithOpenings(
                stage.transform,
                new Vector3(-16.5f, 0f, -10.5f),
                new Vector3(17.0f, 0f, -10.5f),
                0f, 11.6f, 0.42f, palette.ConcreteBoardform,
                Array.Empty<OpeningSpec>(),
                addSkirting: true, palette: palette, wallName: "MenuWall_SouthRear", includeInOcclusion: true);

            // Structural Boardform Pilasters & Perimeter Roof Ring Beams
            for (int i = -2; i <= 2; i++)
            {
                float cx = i * 7.2f;
                WorldArchitectureKit.CreateBox(stage.transform, $"AtriumColumn_N_{i}", new Vector3(cx, 5.6f, 16.4f), new Vector3(0.70f, 11.2f, 0.70f), palette.ConcreteBoardform, addCollider: false);
            }
            for (int i = -1; i <= 2; i++)
            {
                float cz = i * 7.0f;
                WorldArchitectureKit.CreateBox(stage.transform, $"AtriumColumn_E_{i}", new Vector3(15.8f, 5.6f, cz), new Vector3(0.70f, 11.2f, 0.70f), palette.ConcreteBoardform, addCollider: false);
                WorldArchitectureKit.CreateBox(stage.transform, $"AtriumColumn_W_{i}", new Vector3(-15.5f, 5.6f, cz), new Vector3(0.70f, 11.2f, 0.70f), palette.ConcreteBoardform, addCollider: false);
            }

            // Solid perimeter ceiling border slabs around the skylight well so ZERO sky cracks leak at wall tops
            WorldArchitectureKit.CreateBox(stage.transform, "RoofRim_North", new Vector3(0.25f, 11.45f, 17.5f), new Vector3(34.5f, 0.65f, 2.0f), palette.ConcreteBoardform, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "RoofRim_South", new Vector3(0.25f, 11.45f, -9.9f), new Vector3(34.5f, 0.65f, 2.2f), palette.ConcreteBoardform, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "RoofRim_West", new Vector3(-15.9f, 11.45f, 3.75f), new Vector3(2.2f, 0.65f, 29.0f), palette.ConcreteBoardform, addCollider: false);
            WorldArchitectureKit.CreateBox(stage.transform, "RoofRim_East", new Vector3(16.4f, 11.45f, 3.75f), new Vector3(2.2f, 0.65f, 29.0f), palette.ConcreteBoardform, addCollider: false);

            // 3. FULL OVERHEAD STEEL-TRUSS GLASS SKYLIGHT ROOF (3 broken skylight panes in right-front bay let real storm rain pour through!)
            WorldArchitectureKit.BuildCeilingWithSkylight(
                stage.transform,
                "AtriumSkylightRoof",
                new Vector3(0.25f, 0f, 3.75f),
                widthX: 33.8f,
                depthZ: 28.8f,
                roofY: 11.2f,
                baysX: 5,
                baysZ: 4,
                palette: palette,
                brokenPaneIndices: new HashSet<int> { 11, 14, 15 });

            // Subtle glowing red emergency exit signage on dark perimeter for Jurassic Visitor Center storytelling
            WorldArchitectureKit.BuildIlluminatedSign(stage.transform, "MenuSign_ExitEast", new Vector3(13.6f, 5.15f, 12.8f), new Vector3(0f, -90f, 0f), new Vector2(1.4f, 0.30f), palette.EmissiveRed, LightGroupId.VCEmergency);

            // 4. DARK STORMY RAINFOREST EXTERIOR BACKDROP (outside North Z=+19..+36 & East X=+18..+34 — ZERO empty sky void!)
            var exterior = new GameObject("Menu_StormJungleExterior");
            exterior.transform.SetParent(stage.transform, false);

            // Wet dark basalt exterior terrain outside North & East windows
            WorldArchitectureKit.CreateBox(exterior.transform, "ExteriorGround_North", new Vector3(4f, -0.35f, 29.5f), new Vector3(64f, 0.45f, 24f), palette.RockyCliff, addCollider: false);
            WorldArchitectureKit.CreateBox(exterior.transform, "ExteriorGround_East", new Vector3(27.5f, -0.35f, 4.0f), new Vector3(22f, 0.45f, 44f), palette.RockyCliff, addCollider: false);

            // Dark towering volcanic ridge walls enclosing far exterior silhouette (eliminates bright horizon bands 100%)
            WorldArchitectureKit.CreateBox(exterior.transform, "StormRidge_North", new Vector3(6f, 11.5f, 37.0f), new Vector3(72f, 26.0f, 3.2f), palette.RockyCliff, addCollider: false);
            WorldArchitectureKit.CreateBox(exterior.transform, "StormRidge_East", new Vector3(34.5f, 11.5f, 8.0f), new Vector3(3.2f, 26.0f, 62.0f), palette.RockyCliff, addCollider: false);
            WorldArchitectureKit.CreateBox(exterior.transform, "StormRidge_CornerNE", new Vector3(28.0f, 11.5f, 29.5f), new Vector3(18.0f, 26.0f, 3.2f), palette.RockyCliff, addCollider: false, euler: new Vector3(0f, -45f, 0f));

            // Real tropical storm-lashed palm trees, ferns & volcanic boulders outside the North & East glass
            (string stem, Vector3 pos, float yaw, Vector3 size)[] jungleSilhouettes =
            {
                ("palm_tree", new Vector3(-6.5f, -0.1f, 22.8f), 25f, new Vector3(6.2f, 11.5f, 6.2f)),
                ("palm_tree", new Vector3(1.8f, -0.1f, 24.4f), 112f, new Vector3(6.8f, 12.8f, 6.8f)),
                ("palm_tree", new Vector3(10.8f, -0.1f, 22.5f), 210f, new Vector3(6.4f, 11.8f, 6.4f)),
                ("palm_tree", new Vector3(21.2f, -0.1f, 15.6f), 64f, new Vector3(6.6f, 12.2f, 6.6f)),
                ("palm_tree", new Vector3(21.8f, -0.1f, 6.2f), 148f, new Vector3(6.5f, 11.6f, 6.5f)),
                ("palm_tree", new Vector3(20.8f, -0.1f, -2.6f), 285f, new Vector3(6.0f, 11.0f, 6.0f)),
                ("pachira_aquatica_01", new Vector3(-1.5f, -0.1f, 20.6f), 40f, new Vector3(3.6f, 5.2f, 3.6f)),
                ("pachira_aquatica_01", new Vector3(13.8f, -0.1f, 20.4f), 175f, new Vector3(3.8f, 5.4f, 3.8f)),
                ("pachira_aquatica_01", new Vector3(19.6f, -0.1f, 10.4f), 92f, new Vector3(3.6f, 5.0f, 3.6f)),
                ("fern_02", new Vector3(5.6f, -0.1f, 20.2f), 18f, new Vector3(3.2f, 2.4f, 3.2f)),
                ("fern_02", new Vector3(19.2f, -0.1f, 2.2f), 130f, new Vector3(3.2f, 2.4f, 3.2f)),
                ("shrub_01", new Vector3(8.4f, -0.1f, 21.0f), 75f, new Vector3(3.8f, 2.8f, 3.8f)),
                ("coastal_cliff_01", new Vector3(18.5f, -0.2f, 26.5f), 32f, new Vector3(11.0f, 9.5f, 9.0f)),
                ("namaqualand_boulder_02", new Vector3(23.5f, -0.2f, 11.8f), 140f, new Vector3(5.5f, 4.2f, 5.5f))
            };
            for (int j = 0; j < jungleSilhouettes.Length; j++)
            {
                var item = jungleSilhouettes[j];
                palette.InstantiatePropOrFallback(
                    item.stem,
                    exterior.transform,
                    item.pos,
                    new Vector3(0f, item.yaw, 0f),
                    item.size,
                    grounded: true,
                    addCollider: false,
                    fallbackMat: item.stem.Contains("cliff") || item.stem.Contains("boulder") ? palette.RockyCliff : palette.FoliageDark);
            }

            // 5. CENTERPIECE MUSEUM GRANITE PLINTH & 3/4 HERO T. REX FOSSIL SKELETON
            // Aligned at yaw = -100 deg (~45 deg off camera sightline for dramatic roaring 3/4 skull + sweeping ribcage/tail profile!)
            // Skeleton is sunk -0.20 m flush INSIDE the dark granite plinth so its grey FBX baseplate is 100% hidden!
            Vector3 plinthCenter = new Vector3(3.1f, 0f, 8.6f);
            const float heroYawDeg = -100f;
            Quaternion heroRot = Quaternion.Euler(0f, heroYawDeg, 0f);

            var plinthRoot = new GameObject("FossilPlinth_Root");
            plinthRoot.transform.SetParent(stage.transform, false);
            plinthRoot.transform.SetPositionAndRotation(plinthCenter, heroRot);

            WorldArchitectureKit.CreateBox(plinthRoot.transform, "FossilPlinth_Step", new Vector3(0f, 0.14f, 0f), new Vector3(5.6f, 0.28f, 11.4f), palette.DarkGranitePlinth, addCollider: true);
            WorldArchitectureKit.CreateBox(plinthRoot.transform, "FossilPlinth_Base", new Vector3(0f, 0.48f, 0f), new Vector3(4.9f, 0.48f, 10.6f), palette.DarkGranitePlinth, addCollider: true);
            WorldArchitectureKit.CreateBox(plinthRoot.transform, "FossilPlinth_Trim", new Vector3(0f, 0.72f, 0f), new Vector3(4.65f, 0.05f, 10.3f), palette.BrushedSteel, addCollider: false);
            // Subtle warm museum floor-level accent light bar recessed beneath the plinth overhang
            WorldArchitectureKit.CreateBox(plinthRoot.transform, "FossilPlinth_LedTrim", new Vector3(0f, 0.27f, 0f), new Vector3(5.05f, 0.03f, 10.75f), palette.EmissiveAmber, addCollider: false);

            GameObject skeletonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTrexSkeleton);
            if (skeletonPrefab != null)
            {
                GameObject skel = (GameObject)PrefabUtility.InstantiatePrefab(skeletonPrefab, stage.transform);
                skel.name = "Hero_TrexSkeleton";
                // Sunk -0.20 m below plinth top (0.72 m -> 0.52 m) so built-in grey FBX baseplate sits completely inside the granite plinth!
                skel.transform.SetPositionAndRotation(plinthCenter + new Vector3(0f, 0.52f, 0f), heroRot);
                skel.transform.localScale = Vector3.one * 0.85f;
                stats.HasSkeleton = true;
            }
            else
            {
                GameObject skelFallback = palette.InstantiatePropOrFallback(
                    "TrexSkeleton",
                    stage.transform,
                    plinthCenter + new Vector3(0f, 0.52f, 0f),
                    new Vector3(0f, heroYawDeg, 0f),
                    new Vector3(3.6f, 6.2f, 9.8f),
                    grounded: true,
                    addCollider: false);
                stats.HasSkeleton = skelFallback != null;
            }

            // 6. DRAMATIC MUSEUM TUNGSTEN UPLIGHTS + VOLUMETRIC STORM BACKLIGHT + ROTATING CRIMSON BEACONS
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_Uplight_SkullKey", plinthCenter + new Vector3(-4.2f, 0.85f, -2.4f), new Vector3(-40f, 56f, 0f),
                LightType.Spot, new Color(1.0f, 0.80f, 0.52f), 5400f, 20f, LightGroupId.VCExit, volumetricDimmer: 2.0f, castShadows: true, spotOuterAngle: 52f, colorTempK: 3300f);
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_Uplight_RibsWarm", plinthCenter + new Vector3(1.8f, 0.85f, -3.2f), new Vector3(-44f, -15f, 0f),
                LightType.Spot, new Color(1.0f, 0.84f, 0.60f), 3400f, 18f, LightGroupId.VCExit, volumetricDimmer: 1.6f, castShadows: false, spotOuterAngle: 62f, colorTempK: 3500f);
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_SkylightStormRim", new Vector3(6.8f, 10.6f, 13.8f), new Vector3(44f, -152f, 0f),
                LightType.Spot, new Color(0.55f, 0.76f, 1.0f), 4200f, 28f, LightGroupId.VCExit, volumetricDimmer: 2.2f, castShadows: true, spotOuterAngle: 66f, colorTempK: 7600f);

            // Exterior storm-blue foliage silhouettes visible through the wet North & East curtain glass
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_StormExteriorRim_N", new Vector3(3.5f, 12.2f, 26.5f), new Vector3(38f, 180f, 0f),
                LightType.Spot, new Color(0.45f, 0.68f, 1.0f), 5200f, 34f, LightGroupId.VCExit, volumetricDimmer: 1.8f, castShadows: false, spotOuterAngle: 82f, colorTempK: 8000f);
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_StormExteriorRim_E", new Vector3(24.5f, 11.5f, 8.5f), new Vector3(36f, -90f, 0f),
                LightType.Spot, new Color(0.45f, 0.68f, 1.0f), 4600f, 32f, LightGroupId.VCExit, volumetricDimmer: 1.8f, castShadows: false, spotOuterAngle: 82f, colorTempK: 8000f);

            // Sweeping crimson emergency volumetric beacons (Jurassic blackout tension!)
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_EmergencyBeacon_L", new Vector3(-6.8f, 7.2f, 11.5f), new Vector3(26f, 48f, 0f),
                LightType.Spot, new Color(1.0f, 0.12f, 0.08f), 3400f, 26f, LightGroupId.VCEmergency, volumetricDimmer: 2.5f, castShadows: true, spotOuterAngle: 66f, rotatingBeacon: true, pulsingEmergency: true);
            WorldArchitectureKit.BuildHdrpLight(stage.transform, "Menu_EmergencyBeacon_R", new Vector3(12.2f, 7.8f, 14.8f), new Vector3(32f, -138f, 0f),
                LightType.Spot, new Color(1.0f, 0.14f, 0.10f), 2800f, 26f, LightGroupId.VCEmergency, volumetricDimmer: 2.2f, castShadows: false, spotOuterAngle: 72f, rotatingBeacon: true, pulsingEmergency: true);

            // Box-Projected Reflection Probe matching enclosed Atrium volume for crisp rain-slick floor reflections
            WorldArchitectureKit.BuildRoomReflectionProbe(stage.transform, "MenuAtrium", new Vector3(0.25f, 5.4f, 3.75f), new Vector3(33.5f, 11.2f, 28.5f), "menu", createInteriorVolume: false);

            // 7. Live Cinemachine 3 Hero Camera (frames 3/4 T. rex skeleton & rain-streaked glass cleanly on right side)
            Vector3 camPos = new Vector3(-5.8f, 2.55f, -4.2f);
            Vector3 camTarget = new Vector3(1.6f, 3.05f, 8.2f);
            Quaternion camRot = Quaternion.LookRotation((camTarget - camPos).normalized, Vector3.up);

            var mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.transform.SetPositionAndRotation(camPos, camRot);
            Camera mainCam = mainCamGo.AddComponent<Camera>();
            mainCam.fieldOfView = 46f;
            mainCam.nearClipPlane = 0.1f;
            mainCam.farClipPlane = 600f;
            mainCam.cullingMask = ~(GameLayers.Mask(GameLayers.Viewmodel) | GameLayers.Mask(GameLayers.RainBlocker));
            HDAdditionalCameraData hdCam = mainCamGo.AddComponent<HDAdditionalCameraData>();
            hdCam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hdCam.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            hdCam.taaSharpenStrength = 0.55f;
            hdCam.taaAntiFlicker = 0.6f;
            hdCam.dithering = true;      // no banding in dark fog, night sky and the vignette
            hdCam.stopNaNs = true;       // a bad pixel must never bloom across the screen
            mainCamGo.AddComponent<AudioListener>();
            mainCamGo.AddComponent<CinemachineBrain>();

            var menuCmGo = new GameObject("CM_MainMenuHeroCamera");
            menuCmGo.transform.SetPositionAndRotation(camPos, camRot);
            CinemachineCamera menuCm = menuCmGo.AddComponent<CinemachineCamera>();
            menuCm.Priority = 20;
            var menuLens = menuCm.Lens;
            menuLens.FieldOfView = 46f;
            menuLens.NearClipPlane = 0.1f;
            menuLens.FarClipPlane = 600f;
            menuCm.Lens = menuLens;

            // Gentle cinematic slow drift using built-in Cinemachine Perlin noise if a NoiseSettings profile exists
            CinemachineBasicMultiChannelPerlin perlin = menuCmGo.AddComponent<CinemachineBasicMultiChannelPerlin>();
            perlin.AmplitudeGain = 0.14f;
            perlin.FrequencyGain = 0.16f;
            NoiseSettings noisePreset = FindAnyCinemachineNoiseProfile();
            if (noisePreset != null) perlin.NoiseProfile = noisePreset;

            // Storm Rain System framed around Atrium (pours visibly through broken skylight & outside North/East curtain glass!)
            StormRainOcclusionController rain = InstantiateStormRainSystem(mainCamGo.transform, warnings);
            if (rain != null)
            {
                var rainSo = new SerializedObject(rain);
                SerializedProperty rainInt = rainSo.FindProperty("rainIntensity");
                if (rainInt != null) rainInt.floatValue = 1.0f;
                SerializedProperty wetProp = rainSo.FindProperty("globalWetness");
                if (wetProp != null) wetProp.floatValue = 1.0f;
                rainSo.ApplyModifiedPropertiesWithoutUndo();
            }
            stats.HasRain = rain != null;

            // Core Menu Managers (using dedicated MainMenu dark midnight storm atmosphere + capped 55 Lux lightning!)
            var managersRoot = new GameObject("[Managers]");
            CreateManager<GameState>(managersRoot.transform, "[GameState]");
            CreateManager<WorldRegistry>(managersRoot.transform, "[WorldRegistry]");
            CreateManager<LightGroupController>(managersRoot.transform, "[LightGroupController]");

            AtmosphereController atmo = BuildAtmosphereAndLightingRig(managersRoot.transform, rain, out _, warnings, isMainMenu: true);
            var atmoSo = new SerializedObject(atmo);
            atmoSo.FindProperty("lightningEnabled").boolValue = true;
            atmoSo.FindProperty("lightningRate").floatValue = 0.55f;
            SerializedProperty menuPeakLux = atmoSo.FindProperty("lightningPeakLux");
            if (menuPeakLux != null) menuPeakLux.floatValue = CalibratedLightningPeakLux;
            atmoSo.ApplyModifiedPropertiesWithoutUndo();

            BuildAudioManager(managersRoot.transform, out _, out _, warnings);
            BuildMenuController(managersRoot.transform, isMainMenu: true);

            // Ensure Edit Mode preview opens with moody emergency blackout lighting active
            ApplyEditModeLightGroupState(blackout: true, auxPower: false);

            Debug.Log("[WorldAssembler] Look pass (MainMenu): " + Cretacea.Editor.Look.LookPass.RunOnBuild(isMainMenu: true));

            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene, MainMenuScenePath);
            return stats;
        }

        private static NoiseSettings FindAnyCinemachineNoiseProfile()
        {
            string[] guids = AssetDatabase.FindAssets("t:NoiseSettings");
            NoiseSettings fallback = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                NoiseSettings ns = AssetDatabase.LoadAssetAtPath<NoiseSettings>(path);
                if (ns == null) continue;
                if (fallback == null) fallback = ns;
                if (path.IndexOf("Handheld", StringComparison.OrdinalIgnoreCase) >= 0)
                    return ns;
            }
            return fallback;
        }

        // =========================================================================
        // 2. CLI COMMAND: jump_chapter
        // =========================================================================

        [CliCommand(
            "cretacea",
            "jump_chapter",
            "Jumps cleanly to any chapter viewpoint/checkpoint (Intro|Night|Corridor|ServicePaths|Substation|OldYard|Escape|DawnCoast) in Play Mode via StoryDirector or in Edit Mode by moving Player/Camera & Atmosphere.",
            MainThreadRequired = true)]
        public static string JumpChapterCli(
            [CliArg("chapter", "Target chapter: Intro, Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast")]
            string chapter = "Night")
        {
            if (!TryResolveChapterSpec(chapter, out ChapterJumpSpec spec))
            {
                return $"{{\"status\": \"error\", \"message\": \"Unknown chapter '{EscapeJson(chapter)}'. Valid: Intro, Night, Corridor, ServicePaths, Substation, OldYard, Escape, DawnCoast.\"}}";
            }

            if (EditorApplication.isPlaying)
            {
                return JumpChapterInPlayMode(spec);
            }

            return JumpChapterInEditMode(spec);
        }

        private static bool TryResolveChapterSpec(string input, out ChapterJumpSpec spec)
        {
            string key = string.IsNullOrWhiteSpace(input) ? "night" : input.Trim().ToLowerInvariant();
            if (key == "records") key = "corridor";
            else if (key == "dock" || key == "paths") key = "servicepaths";
            else if (key == "paddock") key = "substation";
            else if (key == "yard") key = "oldyard";
            else if (key == "e0" || key == "ending") key = key == "e0" ? "escape" : "dawncoast";

            for (int i = 0; i < ChapterSpecs.Length; i++)
            {
                if (string.Equals(ChapterSpecs[i].CanonicalName, key, StringComparison.OrdinalIgnoreCase))
                {
                    spec = ChapterSpecs[i];
                    return true;
                }
            }
            spec = default;
            return false;
        }

        private static string JumpChapterInPlayMode(ChapterJumpSpec spec)
        {
            StoryDirector story = StoryDirector.Instance ?? UnityEngine.Object.FindAnyObjectByType<StoryDirector>();
            PlayerController player = PlayerController.Instance ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            AtmosphereController atmo = AtmosphereController.Instance ?? UnityEngine.Object.FindAnyObjectByType<AtmosphereController>();
            FlashlightController fl = player != null ? player.Flashlight : UnityEngine.Object.FindAnyObjectByType<FlashlightController>();
            StormRainOcclusionController rain = UnityEngine.Object.FindAnyObjectByType<StormRainOcclusionController>();

            // P0-5 FIX: Ensure pause/cutscene skip UI & letterbox bars never linger across chapter jumps
            GameState.Instance?.SetPaused(false);
            if (spec.CanonicalName != "Intro")
            {
                GameEvents.RaiseCutsceneSkipPrompt(false);
                GameEvents.RaiseLetterbox(false, 0f);
            }

            if (story != null)
            {
                story.StartFromCheckpoint(spec.Checkpoint, freshPreset: true);
            }

            // Re-clear any skip/letterbox prompt for direct gameplay or DawnCoast QA jump
            if (spec.CanonicalName != "Intro")
            {
                GameEvents.RaiseCutsceneSkipPrompt(false);
                GameEvents.RaiseLetterbox(false, 0f);
            }

            // Apply exact viewpoint & state refinements for sub-chapter QA inspection points
            if (spec.CanonicalName == "Corridor" || spec.CanonicalName == "ServicePaths" || spec.CanonicalName == "Escape" || spec.CanonicalName == "DawnCoast")
            {
                player?.Teleport(spec.PlayerPos, spec.YawDeg, spec.PitchDeg);
            }

            if (spec.CanonicalName == "DawnCoast")
            {
                if (atmo != null)
                {
                    atmo.LightningEnabled = false;
                    atmo.SetPreset(AtmospherePreset.DawnCoast, 0f);
                }
                // P2-7 FIX: Explicitly revoke flashlight ownership AND suppress viewmodel on DawnCoast!
                if (fl != null)
                {
                    fl.SetHasFlashlight(false, autoTurnOn: false, playEquipAnim: false);
                    fl.SetBeamEnabled(false, silent: true);
                    fl.SetCutsceneSuppressed(true);
                }
                GameState.Instance?.SetHasFlashlight(false);
                if (rain != null)
                {
                    rain.RainIntensity = 0f;
                    rain.GlobalWetness = 0.35f;
                }
            }
            else if (fl != null)
            {
                fl.SetCutsceneSuppressed(spec.CanonicalName == "Intro");
                fl.SetHasFlashlight(spec.HasFlashlight, autoTurnOn: spec.HasFlashlight, playEquipAnim: false);
            }

            return BuildJumpResultJson("playmode", spec);
        }

        private static string JumpChapterInEditMode(ChapterJumpSpec spec)
        {
            Scene active = SceneManager.GetActiveScene();
            if (!string.Equals(active.path, MainWorldScenePath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(MainWorldScenePath))
            {
                EditorSceneManager.OpenScene(MainWorldScenePath, OpenSceneMode.Single);
            }

            PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.SetPositionAndRotation(spec.PlayerPos, Quaternion.Euler(0f, spec.YawDeg, 0f));
                if (player.CameraPivot != null)
                {
                    player.CameraPivot.localRotation = Quaternion.Euler(spec.PitchDeg, 0f, 0f);
                }
            }

            Vector3 eyePos = spec.PlayerPos + new Vector3(0f, 1.62f, 0f);
            Quaternion eyeRot = Quaternion.Euler(spec.PitchDeg, spec.YawDeg, 0f);

            Camera mainCam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (mainCam != null)
            {
                mainCam.transform.SetPositionAndRotation(eyePos, eyeRot);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView sv = SceneView.lastActiveSceneView;
                sv.LookAtDirect(eyePos + eyeRot * Vector3.forward * 6f, eyeRot, 6f);
                sv.Repaint();
            }

            // Show the chapter's atmosphere through the same Look profile + key light that Play mode uses
            Cretacea.Editor.Look.LookPass.Preview(spec.AtmospherePreset);

            // Toggle Edit-Mode LightGroupMember lights to match chapter power state
            ApplyEditModeLightGroupState(spec.Blackout, spec.AuxPower);

            // Enforce ironclad Flashlight viewmodel state in Edit Mode too
            FlashlightController fl = UnityEngine.Object.FindAnyObjectByType<FlashlightController>();
            if (fl != null)
            {
                var flSo = new SerializedObject(fl);
                GameObject vm = flSo.FindProperty("viewmodelRoot")?.objectReferenceValue as GameObject;
                Light sp = flSo.FindProperty("spotLight")?.objectReferenceValue as Light;
                Light fill = flSo.FindProperty("bounceFillLight")?.objectReferenceValue as Light;
                if (vm != null) vm.SetActive(spec.HasFlashlight);
                if (sp != null) sp.enabled = spec.HasFlashlight;
                if (fill != null) fill.enabled = spec.HasFlashlight;
            }

            // Reposition StormRainOcclusionController around target chapter point
            StormRainOcclusionController rain = UnityEngine.Object.FindAnyObjectByType<StormRainOcclusionController>();
            if (rain != null)
            {
                rain.RainIntensity = (spec.CanonicalName == "Intro" || spec.CanonicalName == "DawnCoast") ? 0f : (spec.Blackout ? 1f : 0.45f);
                rain.ForceRefreshOcclusion();
            }

            return BuildJumpResultJson("editmode", spec);
        }

        private static void ApplyEditModeLightGroupState(bool blackout, bool auxPower)
        {
            LightGroupMember[] members = UnityEngine.Object.FindObjectsByType<LightGroupMember>();
            for (int i = 0; i < members.Length; i++)
            {
                LightGroupMember m = members[i];
                bool enabled = m.GroupId switch
                {
                    LightGroupId.VCMain => !blackout,
                    LightGroupId.Plaza => !blackout,
                    LightGroupId.Paths => !blackout,
                    LightGroupId.DockMain => !blackout,
                    LightGroupId.Talon => !blackout,
                    LightGroupId.VCEmergency => blackout,
                    LightGroupId.VCExit => true,
                    LightGroupId.PaddockBattery => true,
                    LightGroupId.SubBattery => true,
                    LightGroupId.YardBattery => true,
                    LightGroupId.AuxSub => auxPower,
                    LightGroupId.AuxRoute => auxPower,
                    LightGroupId.AuxYard => auxPower,
                    _ => true
                };

                Light[] lights = m.GetComponentsInChildren<Light>(true);
                for (int l = 0; l < lights.Length; l++)
                {
                    lights[l].enabled = enabled;
                }
            }
        }

        private static string BuildJumpResultJson(string mode, ChapterJumpSpec spec)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"status\": \"ok\", \"mode\": \"{0}\", \"chapter\": \"{1}\", \"checkpoint\": \"{2}\", \"position\": [{3:F2}, {4:F2}, {5:F2}], \"yaw\": {6:F1}, \"pitch\": {7:F1}, \"preset\": \"{8}\", \"hasFlashlight\": {9}}}",
                mode,
                spec.CanonicalName,
                spec.Checkpoint,
                spec.PlayerPos.x,
                spec.PlayerPos.y,
                spec.PlayerPos.z,
                spec.YawDeg,
                spec.PitchDeg,
                spec.VolumeProfilePath,
                spec.HasFlashlight ? "true" : "false");
        }

        // =========================================================================
        // 3. CLI COMMAND: verify_playmode_doors
        // =========================================================================

        [CliCommand(
            "cretacea",
            "verify_playmode_doors",
            "Verifies EVERY DoorController in MainWorld.unity for 1.2 m unobstructed clearance on both sides at chest & head height (catching wall-behind-door and dado-rail-slice bugs!), verifies FlashlightController is disabled when HasFlashlight == false, checks all 5/5 canonical documents exist, and validates both NavMeshSurfaces.",
            MainThreadRequired = true)]
        public static string VerifyPlaymodeDoorsCli(
            [CliArg("clearance_meters", "Minimum unobstructed distance required on both sides of each doorway")]
            float clearanceMeters = 1.20f)
        {
            return VerifySceneDoorsAndContracts(clearanceMeters);
        }

        [MenuItem("Cretacea/World/Verify Doors, Flashlight & Documents")]
        public static void VerifyPlaymodeDoorsMenu()
        {
            string report = VerifySceneDoorsAndContracts(1.20f);
            Debug.Log($"[CretaceaWorldAssembler] Verification Report:\n{report}");
        }

        public static string VerifySceneDoorsAndContracts(float clearanceMeters = 1.20f)
        {
            if (!EditorApplication.isPlaying)
            {
                Scene active = SceneManager.GetActiveScene();
                if (!string.Equals(active.path, MainWorldScenePath, StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(MainWorldScenePath))
                {
                    EditorSceneManager.OpenScene(MainWorldScenePath, OpenSceneMode.Single);
                }
            }

            Physics.SyncTransforms();

            int solidWallMask = GameLayers.Mask(GameLayers.Default) | GameLayers.Mask(GameLayers.Environment);
            DoorController[] doors = UnityEngine.Object.FindObjectsByType<DoorController>();
            var failedDoors = new List<string>();
            int passedDoors = 0;

            for (int i = 0; i < doors.Length; i++)
            {
                DoorController door = doors[i];
                if (door == null) continue;

                var doorSo = new SerializedObject(door);
                string doorId = doorSo.FindProperty("doorId")?.stringValue ?? door.name;
                DoorMechanism mech = (DoorMechanism)(doorSo.FindProperty("mechanism")?.enumValueIndex ?? 0);

                // Temporarily disable ONLY this door's own colliders so we verify true architectural clearance!
                Collider[] ownCols = door.GetComponentsInChildren<Collider>(true);
                bool[] prevStates = new bool[ownCols.Length];
                for (int c = 0; c < ownCols.Length; c++)
                {
                    prevStates[c] = ownCols[c].enabled;
                    ownCols[c].enabled = false;
                }

                string failureReason = null;
                try
                {
                    Physics.SyncTransforms();
                    Vector3 basePos = door.transform.position;
                    Vector3 fwd = door.transform.forward;
                    Vector3 right = door.transform.right;

                    // Test heights: standard doors test Chest (1.15 m) & Head (1.85 m);
                    // JammedRollerShutter (1.25 m crouch gap) tests crouch passage at 0.55 m & 0.95 m!
                    float[] testHeights = mech == DoorMechanism.JammedRollerShutter
                        ? new[] { 0.55f, 0.95f }
                        : new[] { 1.15f, 1.85f };

                    const float probeRadius = 0.18f;

                    for (int h = 0; h < testHeights.Length && failureReason == null; h++)
                    {
                        Vector3 origin = basePos + Vector3.up * testHeights[h];

                        // Check +forward (side A) and -forward (side B) out to clearanceMeters
                        for (int dirSign = -1; dirSign <= 1 && failureReason == null; dirSign += 2)
                        {
                            Vector3 castDir = fwd * dirSign;
                            // Start 0.05 m inside doorway centerline so any veneer pasted right on the door plane is immediately caught!
                            Vector3 castOrigin = origin - castDir * 0.05f;
                            if (Physics.SphereCast(castOrigin, probeRadius, castDir, out RaycastHit hit, clearanceMeters + 0.05f, solidWallMask, QueryTriggerInteraction.Ignore))
                            {
                                string hitName = hit.collider != null ? hit.collider.name : "unknown";
                                // Allow terrain/floor grazing only if normal is mostly upward (> 0.8)
                                if (hit.normal.y < 0.78f)
                                {
                                    failureReason = $"Blocked on side {(dirSign > 0 ? "+F" : "-F")} at Y+{testHeights[h]:F2}m by '{hitName}' at dist={hit.distance:F2}m";
                                }
                            }
                        }
                    }

                    // Horizontal slice test across opening centerline (catches unbroken dado rails / wall slabs slicing doorway!)
                    if (failureReason == null)
                    {
                        float sliceY = mech == DoorMechanism.JammedRollerShutter ? 0.75f : 1.00f;
                        Vector3 sliceStart = basePos + Vector3.up * sliceY - right * 0.32f;
                        if (Physics.Raycast(sliceStart, right, out RaycastHit sliceHit, 0.64f, solidWallMask, QueryTriggerInteraction.Ignore))
                        {
                            failureReason = $"Opening sliced across doorway at Y+{sliceY:F2}m by '{sliceHit.collider.name}'";
                        }
                    }
                }
                finally
                {
                    for (int c = 0; c < ownCols.Length; c++)
                    {
                        if (ownCols[c] != null) ownCols[c].enabled = prevStates[c];
                    }
                    Physics.SyncTransforms();
                }

                if (failureReason == null)
                {
                    passedDoors++;
                }
                else
                {
                    failedDoors.Add($"{doorId}: {failureReason}");
                }
            }

            // --- Verify FlashlightController Ironclad FL-1 Invariant ---
            bool flashlightOk = false;
            string flashlightNote;
            FlashlightController fl = UnityEngine.Object.FindAnyObjectByType<FlashlightController>();
            if (fl == null)
            {
                flashlightNote = "FlashlightController missing from scene";
            }
            else
            {
                var flSo = new SerializedObject(fl);
                GameObject vmRoot = flSo.FindProperty("viewmodelRoot")?.objectReferenceValue as GameObject;
                Light spot = flSo.FindProperty("spotLight")?.objectReferenceValue as Light;
                bool hasFl = EditorApplication.isPlaying
                    ? fl.HasFlashlight
                    : (GameState.Instance != null && GameState.Instance.HasFlashlight);

                if (vmRoot == null)
                {
                    flashlightNote = "viewmodelRoot is null";
                }
                else if (!hasFl && vmRoot.activeSelf)
                {
                    flashlightNote = "FL-1 VIOLATION: viewmodelRoot is active while HasFlashlight == false";
                }
                else if (!hasFl && spot != null && spot.enabled)
                {
                    flashlightNote = "FL-1 VIOLATION: spotLight is enabled while HasFlashlight == false";
                }
                else
                {
                    // Check every renderer under viewmodelRoot lives on Layer 12 (Viewmodel)
                    Renderer[] vmRenderers = vmRoot.GetComponentsInChildren<Renderer>(true);
                    int vmLayer = GameLayers.Viewmodel;
                    bool allOnVmLayer = true;
                    for (int r = 0; r < vmRenderers.Length; r++)
                    {
                        if (vmRenderers[r].gameObject.layer != vmLayer)
                        {
                            allOnVmLayer = false;
                            break;
                        }
                    }
                    flashlightOk = allOnVmLayer;
                    flashlightNote = allOnVmLayer
                        ? $"OK (HasFlashlight={hasFl}, viewmodelActive={vmRoot.activeSelf}, renderersOnLayer12={vmRenderers.Length})"
                        : "Layer 12 violation: some viewmodel renderers are not on Viewmodel layer";
                }
            }

            // --- Verify All 5/5 Canonical Documents ---
            DocumentPickup[] docPickups = UnityEngine.Object.FindObjectsByType<DocumentPickup>();
            var foundDocIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < docPickups.Length; i++)
            {
                var docSo = new SerializedObject(docPickups[i]);
                string docId = docSo.FindProperty("documentId")?.stringValue;
                if (!string.IsNullOrEmpty(docId)) foundDocIds.Add(docId);
            }

            var missingDocs = new List<string>();
            for (int i = 0; i < GameState.CanonicalDocumentIds.Length; i++)
            {
                string reqId = GameState.CanonicalDocumentIds[i];
                if (!foundDocIds.Contains(reqId)) missingDocs.Add(reqId);
            }

            // --- Verify Dual NavMeshSurfaces ---
            NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>();
            bool hasRaptorNav = false;
            bool hasTrexNav = false;
            int raptorId = DinoControllerBase.ResolveNavMeshAgentTypeId("Agent_Raptor");
            int trexId = DinoControllerBase.ResolveNavMeshAgentTypeId("Agent_TRex");
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i].navMeshData == null) continue;
                if (surfaces[i].agentTypeID == raptorId) hasRaptorNav = true;
                if (surfaces[i].agentTypeID == trexId) hasTrexNav = true;
            }

            bool allPassed = doors.Length > 0
                             && failedDoors.Count == 0
                             && flashlightOk
                             && missingDocs.Count == 0
                             && hasRaptorNav
                             && hasTrexNav;

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"passed\": {(allPassed ? "true" : "false")},");
            sb.AppendLine($"  \"doorCount\": {doors.Length},");
            sb.AppendLine($"  \"passedDoors\": {passedDoors},");
            sb.AppendLine($"  \"failedDoors\": {FormatStringArrayJson(failedDoors)},");
            sb.AppendLine($"  \"flashlightOk\": {(flashlightOk ? "true" : "false")},");
            sb.AppendLine($"  \"flashlightCheck\": \"{EscapeJson(flashlightNote)}\",");
            sb.AppendLine($"  \"documentsFoundCount\": {foundDocIds.Count},");
            sb.AppendLine($"  \"documentsRequiredCount\": {GameState.CanonicalDocumentIds.Length},");
            sb.AppendLine($"  \"missingDocuments\": {FormatStringArrayJson(missingDocs)},");
            sb.AppendLine($"  \"navMeshRaptorValid\": {(hasRaptorNav ? "true" : "false")},");
            sb.AppendLine($"  \"navMeshTRexValid\": {(hasTrexNav ? "true" : "false")}");
            sb.Append("}");
            return sb.ToString();
        }

        // =========================================================================
        // ASSET LOOKUP & JSON FORMATTING HELPERS
        // =========================================================================

        private static VolumeProfile BuildOrUpdateUnifiedRuntimeAtmosphereProfile(VolumeProfile stormSource)
        {
            if (stormSource == null) return null;
            const string unifiedPath = "Assets/Settings/Atmosphere/MainWorld_RuntimeAtmosphere.asset";
            WorldMaterialPalette.EnsureFolder("Assets/Settings/Atmosphere");

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(unifiedPath) != null)
            {
                AssetDatabase.DeleteAsset(unifiedPath);
            }

            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(stormSource), unifiedPath))
            {
                return stormSource;
            }

            VolumeProfile cloned = AssetDatabase.LoadAssetAtPath<VolumeProfile>(unifiedPath);
            if (cloned != null)
            {
                PhysicallyBasedSky pbs;
                if (!cloned.TryGet(out pbs))
                {
                    pbs = ScriptableObject.CreateInstance<PhysicallyBasedSky>();
                    pbs.name = nameof(PhysicallyBasedSky);
                    AssetDatabase.AddObjectToAsset(pbs, cloned);
                    cloned.components.Add(pbs);
                }
                ConfigureDawnPhysicallyBasedSky(pbs, exposureOffset: -0.6f);

                // Smooth coastal horizon fog mip blend (prevents hard bright band over Eastern ocean)
                if (cloned.TryGet(out Fog fog))
                {
                    fog.mipFogNear.overrideState = true;
                    fog.mipFogNear.value = 80f;
                    fog.mipFogFar.overrideState = true;
                    fog.mipFogFar.value = 1200f;
                    fog.mipFogMaxMip.overrideState = true;
                    fog.mipFogMaxMip.value = 0.65f;
                }

                EditorUtility.SetDirty(cloned);
                AssetDatabase.SaveAssets();
            }
            return cloned;
        }

        private static VolumeProfile BuildOrUpdateMainMenuStormAtmosphereProfile(VolumeProfile stormSource)
        {
            const string menuProfilePath = "Assets/Settings/Atmosphere/MainMenu_StormAtmosphere.asset";
            WorldMaterialPalette.EnsureFolder("Assets/Settings/Atmosphere");

            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(menuProfilePath) != null)
            {
                AssetDatabase.DeleteAsset(menuProfilePath);
            }

            VolumeProfile menuProf;
            if (stormSource != null && AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(stormSource), menuProfilePath))
            {
                menuProf = AssetDatabase.LoadAssetAtPath<VolumeProfile>(menuProfilePath);
            }
            else
            {
                menuProf = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(menuProf, menuProfilePath);
            }

            if (menuProf == null) return stormSource;

            // Strictly remove PhysicallyBasedSky so MainMenu NEVER washes out to grey daylight twilight!
            if (menuProf.TryGet(out PhysicallyBasedSky oldPbs))
            {
                menuProf.components.Remove(oldPbs);
                UnityEngine.Object.DestroyImmediate(oldPbs, allowDestroyingAssets: true);
            }

            VisualEnvironment visualEnv = GetOrAddVolumeComponent<VisualEnvironment>(menuProf);
            visualEnv.skyType.overrideState = true;
            visualEnv.skyType.value = (int)SkyType.Gradient;

            GradientSky grad = GetOrAddVolumeComponent<GradientSky>(menuProf);
            grad.top.overrideState = true;
            grad.top.value = new Color(0.004f, 0.009f, 0.022f, 1f);
            grad.middle.overrideState = true;
            grad.middle.value = new Color(0.009f, 0.019f, 0.042f, 1f);
            grad.bottom.overrideState = true;
            grad.bottom.value = new Color(0.003f, 0.006f, 0.013f, 1f);
            grad.exposure.overrideState = true;
            grad.exposure.value = -0.8f;

            Exposure exp = GetOrAddVolumeComponent<Exposure>(menuProf);
            exp.mode.overrideState = true;
            exp.mode.value = ExposureMode.Fixed;
            exp.fixedExposure.overrideState = true;
            exp.fixedExposure.value = 0.72f;

            Fog fog = GetOrAddVolumeComponent<Fog>(menuProf);
            fog.enabled.overrideState = true;
            fog.enabled.value = true;
            fog.enableVolumetricFog.overrideState = true;
            fog.enableVolumetricFog.value = true;
            fog.colorMode.overrideState = true;
            fog.colorMode.value = FogColorMode.ConstantColor;
            fog.color.overrideState = true;
            fog.color.value = new Color(0.026f, 0.046f, 0.076f, 1f);
            fog.albedo.overrideState = true;
            fog.albedo.value = new Color(0.045f, 0.075f, 0.115f, 1f);
            fog.meanFreePath.overrideState = true;
            fog.meanFreePath.value = 32f;
            fog.baseHeight.overrideState = true;
            fog.baseHeight.value = 0.5f;
            fog.maximumHeight.overrideState = true;
            fog.maximumHeight.value = 18f;

            EditorUtility.SetDirty(menuProf);
            AssetDatabase.SaveAssets();
            return menuProf;
        }

        private static void CalibrateDawnCoastPresetAsset(VolumeProfile dawnProfile)
        {
            if (dawnProfile == null) return;

            VisualEnvironment visualEnv = GetOrAddVolumeComponent<VisualEnvironment>(dawnProfile);
            visualEnv.skyType.overrideState = true;
            visualEnv.skyType.value = (int)SkyType.PhysicallyBased;

            PhysicallyBasedSky pbs = GetOrAddVolumeComponent<PhysicallyBasedSky>(dawnProfile);
            ConfigureDawnPhysicallyBasedSky(pbs, exposureOffset: 0.0f);

            Exposure exp = GetOrAddVolumeComponent<Exposure>(dawnProfile);
            exp.mode.overrideState = true;
            exp.mode.value = ExposureMode.Fixed;
            exp.fixedExposure.overrideState = true;
            exp.fixedExposure.value = 8.9f;

            Fog fog = GetOrAddVolumeComponent<Fog>(dawnProfile);
            fog.enabled.overrideState = true;
            fog.enabled.value = true;
            fog.enableVolumetricFog.overrideState = true;
            fog.enableVolumetricFog.value = true;
            fog.colorMode.overrideState = true;
            fog.colorMode.value = FogColorMode.SkyColor;
            fog.albedo.overrideState = true;
            fog.albedo.value = new Color(1.0f, 0.80f, 0.56f, 1f);
            fog.meanFreePath.overrideState = true;
            fog.meanFreePath.value = 480f;
            fog.baseHeight.overrideState = true;
            fog.baseHeight.value = -18f;
            fog.maximumHeight.overrideState = true;
            fog.maximumHeight.value = 52f;
            fog.mipFogNear.overrideState = true;
            fog.mipFogNear.value = 80f;
            fog.mipFogFar.overrideState = true;
            fog.mipFogFar.value = 1200f;
            fog.mipFogMaxMip.overrideState = true;
            fog.mipFogMaxMip.value = 0.65f;

            EditorUtility.SetDirty(dawnProfile);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureDawnPhysicallyBasedSky(PhysicallyBasedSky pbs, float exposureOffset)
        {
            if (pbs == null) return;
            pbs.active = true;
            pbs.type.overrideState = true;
            pbs.type.value = PhysicallyBasedSkyModel.EarthAdvanced;
            pbs.horizonTint.overrideState = true;
            pbs.horizonTint.value = new Color(1.0f, 0.74f, 0.44f);
            pbs.zenithTint.overrideState = true;
            pbs.zenithTint.value = new Color(0.46f, 0.64f, 0.94f);
            pbs.horizonZenithShift.overrideState = true;
            pbs.horizonZenithShift.value = 0.12f;
            pbs.aerosolDensity.overrideState = true;
            pbs.aerosolDensity.value = 0.024f;
            pbs.aerosolTint.overrideState = true;
            pbs.aerosolTint.value = new Color(1.0f, 0.78f, 0.48f);
            pbs.aerosolAnisotropy.overrideState = true;
            pbs.aerosolAnisotropy.value = 0.85f;
            pbs.exposure.overrideState = true;
            pbs.exposure.value = exposureOffset;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
            {
                existing.active = true;
                return existing;
            }
            T created = ScriptableObject.CreateInstance<T>();
            created.name = typeof(T).Name;
            created.active = true;
            AssetDatabase.AddObjectToAsset(created, profile);
            profile.components.Add(created);
            return created;
        }

        private static T CreateManager<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        private static Texture2D FindTextureAsset(string stem)
        {
            string[] roots = FilterValidFolders(new[] { "Assets/Art", "Assets/Settings" });
            if (roots.Length == 0) return null;
            string[] guids = AssetDatabase.FindAssets($"{stem} t:Texture2D", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), stem, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static Sprite FindSpriteAsset(string stem)
        {
            string[] roots = FilterValidFolders(new[] { "Assets/Art/UI", "Assets/Art" });
            if (roots.Length == 0) return null;
            string[] guids = AssetDatabase.FindAssets($"{stem} t:Sprite", roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), stem, StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
        }

        private static string[] FilterValidFolders(string[] candidates)
        {
            var valid = new List<string>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (AssetDatabase.IsValidFolder(candidates[i])) valid.Add(candidates[i]);
            }
            return valid.ToArray();
        }

        private static string FormatStringArrayJson(List<string> items)
        {
            if (items == null || items.Count == 0) return "[]";
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append('"').Append(EscapeJson(items[i])).Append('"');
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string EscapeJson(string s)
        {
            return string.IsNullOrEmpty(s)
                ? string.Empty
                : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", "");
        }
    }
}
