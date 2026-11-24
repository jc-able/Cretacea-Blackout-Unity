using System;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.AI
{
    /// <summary>
    /// Layer masks used across Cretacea.AI that include Layer 3 ("Environment" in TagManager.asset)
    /// alongside GameLayers.Default/Door/Water/DinoBlocker so Foot IK, LOS, and collision stay 100% solid
    /// whether level geometry sits on Default (0) or Environment (3).
    /// </summary>
    public static class DinoLayerMasks
    {
        public static int EnvironmentLayer => GameLayers.Resolve("Environment", 3);
        public static int EnvironmentBit => 1 << EnvironmentLayer;

        public static int GroundMask => GameLayers.GroundMask | EnvironmentBit;
        public static int VisionOcclusionMask => GameLayers.VisionOcclusionMask | EnvironmentBit;
        public static int DinoCollisionMask => GameLayers.DinoCollisionMask | EnvironmentBit;
        public static int EnvironmentAndDoorMask => GameLayers.EnvironmentAndDoorMask | EnvironmentBit;
    }

    public enum DinoSpeciesType
    {
        Velociraptor = 0,
        Tyrannosaurus = 1,
        Brachiosaurus = 2,
        Triceratops = 3
    }

    [Serializable]
    public struct DinoClipSpec
    {
        public string clipName;
        public float naturalStrideSpeed;
        public float durationSeconds;
        public bool loop;
        public float hitOrImpactTimeSeconds;
        public float[] footstepNormalizedTimes;

        public DinoClipSpec(
            string clipName,
            float naturalStrideSpeed,
            float durationSeconds,
            bool loop,
            float hitOrImpactTimeSeconds = -1f,
            float[] footstepNormalizedTimes = null)
        {
            this.clipName = clipName;
            this.naturalStrideSpeed = naturalStrideSpeed;
            this.durationSeconds = durationSeconds;
            this.loop = loop;
            this.hitOrImpactTimeSeconds = hitOrImpactTimeSeconds;
            this.footstepNormalizedTimes = footstepNormalizedTimes;
        }
    }

    [Serializable]
    public struct DinoRigOverrides
    {
        public bool isQuadruped;
        public string rootBone;
        public string pelvisBone;
        public string headBone;
        public string jawBone;
        public string[] spineBones;
        public string[] neckBones;
        public string[] tailBones;
        public string leftFootBone;
        public string rightFootBone;
        public string leftFrontFootBone;
        public string rightFrontFootBone;

        [Header("Anatomical Clamps")]
        public float maxNeckYawDeg;
        public float maxNeckPitchDeg;
        public float jawMaxOpenDeg;
        public float jawAxisSign;
        public float footGroundClearance;
        public float maxPelvisDropMeters;
        public float maxPelvisLiftMeters;
        public float maxFootSlopeAlignDeg;

        public static DinoRigOverrides CreateDefault(DinoSpeciesType species)
        {
            switch (species)
            {
                case DinoSpeciesType.Tyrannosaurus:
                    return new DinoRigOverrides
                    {
                        isQuadruped = false,
                        rootBone = "Root",
                        pelvisBone = "Pelvis",
                        headBone = "Head",
                        jawBone = "Jaw",
                        spineBones = new[] { "Spine_01", "Spine_02", "Spine_03" },
                        neckBones = new[] { "Neck_01", "Neck_02" },
                        tailBones = new[] { "Tail_01", "Tail_02", "Tail_03", "Tail_04", "Tail_05", "Tail_06" },
                        leftFootBone = "LeftFoot",
                        rightFootBone = "RightFoot",
                        leftFrontFootBone = string.Empty,
                        rightFrontFootBone = string.Empty,
                        maxNeckYawDeg = 55f,
                        maxNeckPitchDeg = 28f,
                        jawMaxOpenDeg = 42f,
                        jawAxisSign = 1f,
                        footGroundClearance = 0.02f,
                        maxPelvisDropMeters = 0.55f,
                        maxPelvisLiftMeters = 0.35f,
                        maxFootSlopeAlignDeg = 28f
                    };

                case DinoSpeciesType.Brachiosaurus:
                    return new DinoRigOverrides
                    {
                        isQuadruped = true,
                        rootBone = "Root",
                        pelvisBone = "Pelvis",
                        headBone = "Head",
                        jawBone = "Jaw",
                        spineBones = new[] { "Spine_01", "Spine_02", "Spine_03" },
                        neckBones = new[] { "Neck_01", "Neck_02", "Neck_03", "Neck_04", "Neck_05", "Neck_06" },
                        tailBones = new[] { "Tail_01", "Tail_02", "Tail_03", "Tail_04", "Tail_05", "Tail_06" },
                        leftFootBone = "LeftFoot",
                        rightFootBone = "RightFoot",
                        leftFrontFootBone = "LeftFrontFoot",
                        rightFrontFootBone = "RightFrontFoot",
                        maxNeckYawDeg = 50f,
                        maxNeckPitchDeg = 35f,
                        jawMaxOpenDeg = 22f,
                        jawAxisSign = 1f,
                        footGroundClearance = 0.02f,
                        maxPelvisDropMeters = 0.65f,
                        maxPelvisLiftMeters = 0.35f,
                        maxFootSlopeAlignDeg = 22f
                    };

                case DinoSpeciesType.Triceratops:
                    return new DinoRigOverrides
                    {
                        isQuadruped = true,
                        rootBone = "Root",
                        pelvisBone = "Pelvis",
                        headBone = "Head",
                        jawBone = "Jaw",
                        spineBones = new[] { "Spine_01", "Spine_02", "Spine_03" },
                        neckBones = new[] { "Neck_01" },
                        tailBones = new[] { "Tail_01", "Tail_02", "Tail_03", "Tail_04" },
                        leftFootBone = "LeftFoot",
                        rightFootBone = "RightFoot",
                        leftFrontFootBone = "LeftFrontFoot",
                        rightFrontFootBone = "RightFrontFoot",
                        maxNeckYawDeg = 38f,
                        maxNeckPitchDeg = 22f,
                        jawMaxOpenDeg = 24f,
                        jawAxisSign = 1f,
                        footGroundClearance = 0.02f,
                        maxPelvisDropMeters = 0.38f,
                        maxPelvisLiftMeters = 0.25f,
                        maxFootSlopeAlignDeg = 24f
                    };

                default: // Velociraptor
                    return new DinoRigOverrides
                    {
                        isQuadruped = false,
                        rootBone = "Root",
                        pelvisBone = "Pelvis",
                        headBone = "Head",
                        jawBone = "Jaw",
                        spineBones = new[] { "Spine_01", "Spine_02", "Spine_03" },
                        neckBones = new[] { "Neck_01", "Neck_02", "Neck_03" },
                        tailBones = new[] { "Tail_01", "Tail_02", "Tail_03", "Tail_04", "Tail_05", "Tail_06" },
                        leftFootBone = "LeftFoot",
                        rightFootBone = "RightFoot",
                        leftFrontFootBone = string.Empty,
                        rightFrontFootBone = string.Empty,
                        maxNeckYawDeg = 65f,
                        maxNeckPitchDeg = 30f,
                        jawMaxOpenDeg = 36f,
                        jawAxisSign = 1f,
                        footGroundClearance = 0.015f,
                        maxPelvisDropMeters = 0.26f,
                        maxPelvisLiftMeters = 0.18f,
                        maxFootSlopeAlignDeg = 30f
                    };
            }
        }
    }

    /// <summary>
    /// Authoritative species configuration for Velociraptor, Tyrannosaurus, Brachiosaurus, and Triceratops.
    /// Locks exact animation clip names, natural stride speeds (m/s), NavMesh agent types, sensory ranges,
    /// combat parameters, audio/trauma cues, and procedural rig overrides.
    /// </summary>
    [CreateAssetMenu(fileName = "DinoConfig", menuName = "Cretacea/AI/Dino Config")]
    public sealed class DinoConfig : ScriptableObject
    {
        [Header("Species Identity")]
        public DinoSpeciesType speciesType = DinoSpeciesType.Velociraptor;
        public string speciesKey = "raptor";
        public string displayName = "Velociraptor";

        [Header("NavMesh & Physical Bounds")]
        public string navMeshAgentTypeName = "Agent_Raptor";
        public float agentRadius = 0.45f;
        public float agentHeight = 1.70f;
        public float eyeHeight = 1.42f;
        public float turnSpeedDegPerSec = 320f;
        public float acceleration = 14f;
        public float stoppingDistance = 0.85f;

        [Header("Locomotion Speeds (m/s)")]
        public float stalkSpeed = 1.30f;
        public float walkSpeed = 1.90f;
        public float runSpeed = 6.20f;

        [Header("Animation Stride Calibration (Zero Foot Skating)")]
        public float stalkClipStrideSpeed = 1.90f;
        public float walkClipStrideSpeed = 1.90f;
        public float runClipStrideSpeed = 6.20f;
        public float minAnimSpeedScale = 0.72f;
        public float maxAnimSpeedScale = 1.42f;
        public float turnInPlaceAngleThresholdDeg = 42f;
        public float turnInPlaceMaxSpeed = 0.35f;

        [Header("Perception (Vision & Hearing)")]
        public float sightRangeMeters = 24f;
        public float sightFovDegrees = 130f;
        public float hearingSensitivity = 1.50f;
        public float awarenessGainRate = 0.95f;
        public float awarenessDecayRate = 0.22f;
        public float alertThreshold = 0.45f;
        public float huntThreshold = 1.0f;
        public float alertHoldSeconds = 1.4f;
        public float loseSightSeconds = 5.0f;
        public float searchDurationSeconds = 7.5f;

        [Header("Combat & Door Bashing")]
        public float attackRangeMeters = 2.7f;
        public float killRangeMeters = 2.3f;
        public float attackCooldownSeconds = 1.25f;
        public float attackHitDelaySeconds = 0.35f;
        public bool canBashDoors = true;
        public float doorBashRange = 2.15f;
        public float doorBashCooldown = 1.1f;

        [Header("Audio & Camera Trauma")]
        public string footstepSoundId = "raptor_step";
        public string alertSoundId = "raptor_chirp";
        public string roarSoundId = "raptor_screech";
        public string sniffSoundId = "raptor_sniff";
        public string biteSoundId = "raptor_bite";
        public string breathSoundId = "raptor_breath_close";
        public float footstepTraumaMax = 0.04f;
        public float footstepTraumaRadius = 8f;
        public float roarTraumaMax = 0.22f;
        public float roarTraumaRadius = 24f;
        public bool emitGroundFootprints = false;

        [Header("Canonical Animation Clip Names")]
        public string idleClipName = "Idle_Alert";
        public string stalkClipName = "Stalk_Walk";
        public string walkClipName = "Stalk_Walk";
        public string runClipName = "Sprint_Run";
        public string turnLeftClipName = "Turn_Left";
        public string turnRightClipName = "Turn_Right";
        public string inspectClipName = "Sniff_Inspect";
        public string roarClipName = "Roar_Call";
        public string biteClipName = "Bite_Lunge";
        public string bashClipName = "Bite_Lunge";
        public DinoClipSpec[] clipSpecs = Array.Empty<DinoClipSpec>();

        [Header("Procedural Rig Overrides")]
        public DinoRigOverrides rigOverrides;

        public bool HasTurnInPlaceClips =>
            !string.IsNullOrEmpty(turnLeftClipName) && !string.IsNullOrEmpty(turnRightClipName);

        /// <summary>
        /// Computes the expected natural stride speed (m/s) of the 1D locomotion blend tree at the given speed parameter,
        /// allowing Animator playback rate to match true agent velocity with zero foot skating.
        /// </summary>
        public float EvaluateExpectedClipStrideSpeed(float currentSpeed)
        {
            float absSpeed = Mathf.Abs(currentSpeed);
            if (absSpeed < 0.08f) return 1.0f;

            float lowThreshold = Mathf.Max(0.25f, walkClipStrideSpeed > 0.1f ? walkClipStrideSpeed : stalkClipStrideSpeed);
            float highThreshold = Mathf.Max(lowThreshold + 0.1f, runClipStrideSpeed);

            if (absSpeed <= lowThreshold)
            {
                return lowThreshold;
            }

            float t = Mathf.InverseLerp(lowThreshold, highThreshold, absSpeed);
            return Mathf.Lerp(lowThreshold, highThreshold, t);
        }

        public bool TryGetClipSpec(string clipName, out DinoClipSpec spec)
        {
            if (clipSpecs != null && !string.IsNullOrEmpty(clipName))
            {
                for (int i = 0; i < clipSpecs.Length; i++)
                {
                    if (string.Equals(clipSpecs[i].clipName, clipName, StringComparison.OrdinalIgnoreCase))
                    {
                        spec = clipSpecs[i];
                        return true;
                    }
                }
            }
            spec = default;
            return false;
        }

        public static string NormalizeSpeciesKey(string rawSpecies)
        {
            if (string.IsNullOrWhiteSpace(rawSpecies)) return "raptor";
            string s = rawSpecies.Trim().ToLowerInvariant();
            if (s.Contains("raptor") || s.Contains("velo")) return "raptor";
            if (s.Contains("trex") || s.Contains("t-rex") || s.Contains("t_rex") || s.Contains("tyran")) return "trex";
            if (s.Contains("brachio") || s.Contains("sauropod")) return "brachiosaurus";
            if (s.Contains("tricera") || s.Contains("trike")) return "triceratops";
            return s;
        }

        public static DinoSpeciesType ParseSpeciesType(string rawSpecies)
        {
            string key = NormalizeSpeciesKey(rawSpecies);
            switch (key)
            {
                case "trex": return DinoSpeciesType.Tyrannosaurus;
                case "brachiosaurus": return DinoSpeciesType.Brachiosaurus;
                case "triceratops": return DinoSpeciesType.Triceratops;
                default: return DinoSpeciesType.Velociraptor;
            }
        }

        public static DinoConfig CreateDefaultForSpeciesKey(string rawSpecies)
        {
            return CreateDefault(ParseSpeciesType(rawSpecies));
        }

        public static DinoConfig CreateDefault(DinoSpeciesType species)
        {
            DinoConfig cfg = CreateInstance<DinoConfig>();
            ApplyDefaults(cfg, species);
            return cfg;
        }

        public static void ApplyDefaults(DinoConfig cfg, DinoSpeciesType species)
        {
            if (cfg == null) return;
            cfg.speciesType = species;
            cfg.rigOverrides = DinoRigOverrides.CreateDefault(species);

            switch (species)
            {
                case DinoSpeciesType.Velociraptor:
                    cfg.name = "DinoConfig_Velociraptor";
                    cfg.speciesKey = "raptor";
                    cfg.displayName = "Velociraptor";
                    cfg.navMeshAgentTypeName = "Agent_Raptor";
                    cfg.agentRadius = 0.45f;
                    cfg.agentHeight = 1.67f; // Verified 1.67 m stalking height: clears 2.2-2.4 m doorways with >53 cm margin
                    cfg.eyeHeight = 1.42f;
                    cfg.turnSpeedDegPerSec = 320f;
                    cfg.acceleration = 14.5f;
                    cfg.stoppingDistance = 0.85f;
                    cfg.stalkSpeed = 1.30f;
                    cfg.walkSpeed = 1.90f;
                    cfg.runSpeed = 6.20f;
                    cfg.stalkClipStrideSpeed = 1.90f;
                    cfg.walkClipStrideSpeed = 1.90f;
                    cfg.runClipStrideSpeed = 6.20f;
                    cfg.minAnimSpeedScale = 0.72f;
                    cfg.maxAnimSpeedScale = 1.42f;
                    cfg.turnInPlaceAngleThresholdDeg = 42f;
                    cfg.turnInPlaceMaxSpeed = 0.35f;
                    cfg.sightRangeMeters = 24f;
                    cfg.sightFovDegrees = 130f;
                    cfg.hearingSensitivity = 1.50f;
                    cfg.awarenessGainRate = 0.95f;
                    cfg.awarenessDecayRate = 0.22f;
                    cfg.alertThreshold = 0.45f;
                    cfg.huntThreshold = 1.0f;
                    cfg.alertHoldSeconds = 1.4f;
                    cfg.loseSightSeconds = 5.0f;
                    cfg.searchDurationSeconds = 7.5f;
                    cfg.attackRangeMeters = 2.7f;
                    cfg.killRangeMeters = 2.3f;
                    cfg.attackCooldownSeconds = 1.25f;
                    cfg.attackHitDelaySeconds = 0.35f;
                    cfg.canBashDoors = true;
                    cfg.doorBashRange = 2.15f;
                    cfg.doorBashCooldown = 1.10f;
                    cfg.footstepSoundId = "raptor_step";
                    cfg.alertSoundId = "raptor_chirp";
                    cfg.roarSoundId = "raptor_screech";
                    cfg.sniffSoundId = "raptor_sniff";
                    cfg.biteSoundId = "raptor_bite";
                    cfg.breathSoundId = "raptor_breath_close";
                    cfg.footstepTraumaMax = 0.04f;
                    cfg.footstepTraumaRadius = 8f;
                    cfg.roarTraumaMax = 0.22f;
                    cfg.roarTraumaRadius = 24f;
                    cfg.emitGroundFootprints = false;
                    cfg.idleClipName = "Idle_Alert";
                    cfg.stalkClipName = "Stalk_Walk";
                    cfg.walkClipName = "Stalk_Walk";
                    cfg.runClipName = "Sprint_Run";
                    cfg.turnLeftClipName = "Turn_Left";
                    cfg.turnRightClipName = "Turn_Right";
                    cfg.inspectClipName = "Sniff_Inspect";
                    cfg.roarClipName = "Roar_Call";
                    cfg.biteClipName = "Bite_Lunge";
                    cfg.bashClipName = "Bite_Lunge";
                    cfg.clipSpecs = new[]
                    {
                        new DinoClipSpec("Idle_Alert",    0.0f,  3.6f,    true),
                        new DinoClipSpec("Stalk_Walk",    1.90f, 0.8f,    true,  -1f,   new[] { 0.22f, 0.72f }),
                        new DinoClipSpec("Sprint_Run",    6.20f, 0.4667f, true,  -1f,   new[] { 0.18f, 0.68f }),
                        new DinoClipSpec("Sniff_Inspect", 0.0f,  3.2f,    true),
                        new DinoClipSpec("Roar_Call",     0.0f,  2.2f,    false),
                        new DinoClipSpec("Bite_Lunge",    0.0f,  1.1f,    false, 0.35f),
                        new DinoClipSpec("Turn_Left",     0.0f,  0.8f,    true,  -1f,   new[] { 0.45f, 0.85f }),
                        new DinoClipSpec("Turn_Right",    0.0f,  0.8f,    true,  -1f,   new[] { 0.45f, 0.85f })
                    };
                    break;

                case DinoSpeciesType.Tyrannosaurus:
                    cfg.name = "DinoConfig_Tyrannosaurus";
                    cfg.speciesKey = "trex";
                    cfg.displayName = "Tyrannosaurus";
                    cfg.navMeshAgentTypeName = "Agent_TRex";
                    cfg.agentRadius = 1.45f;
                    cfg.agentHeight = 4.40f;
                    cfg.eyeHeight = 3.85f;
                    cfg.turnSpeedDegPerSec = 60f;     // review 4: 135 deg/s swivelled a 7 t animal 90 deg in under one step
                    cfg.acceleration = 8.5f;
                    cfg.stoppingDistance = 2.6f;
                    cfg.stalkSpeed = 1.80f;
                    cfg.walkSpeed = 2.60f;
                    cfg.runSpeed = 7.00f;
                    cfg.stalkClipStrideSpeed = 2.60f;
                    cfg.walkClipStrideSpeed = 2.60f;
                    cfg.runClipStrideSpeed = 7.00f;
                    cfg.minAnimSpeedScale = 0.72f;
                    cfg.maxAnimSpeedScale = 1.38f;
                    cfg.turnInPlaceAngleThresholdDeg = 45f;
                    cfg.turnInPlaceMaxSpeed = 0.40f;
                    cfg.sightRangeMeters = 62f;
                    cfg.sightFovDegrees = 150f;
                    cfg.hearingSensitivity = 1.25f;
                    cfg.awarenessGainRate = 0.78f;
                    cfg.awarenessDecayRate = 0.18f;
                    cfg.alertThreshold = 0.45f;
                    cfg.huntThreshold = 1.0f;
                    cfg.alertHoldSeconds = 2.2f;
                    cfg.loseSightSeconds = 7.0f;
                    cfg.searchDurationSeconds = 9.0f;
                    cfg.attackRangeMeters = 7.5f;
                    cfg.killRangeMeters = 5.0f;
                    cfg.attackCooldownSeconds = 2.1f;
                    cfg.attackHitDelaySeconds = 0.55f;
                    cfg.canBashDoors = false;
                    cfg.doorBashRange = 4.5f;
                    cfg.doorBashCooldown = 2.0f;
                    cfg.footstepSoundId = "trex_step";
                    cfg.alertSoundId = "trex_snort";
                    cfg.roarSoundId = "trex_roar_close";
                    cfg.sniffSoundId = "trex_snort";
                    cfg.biteSoundId = "trex_bite";
                    cfg.breathSoundId = "trex_breath";
                    cfg.footstepTraumaMax = 0.36f;
                    cfg.footstepTraumaRadius = 46f;
                    cfg.roarTraumaMax = 0.65f;
                    cfg.roarTraumaRadius = 85f;
                    cfg.emitGroundFootprints = true;
                    cfg.idleClipName = "Idle_Heavy";
                    cfg.stalkClipName = "Walk_Stomp";
                    cfg.walkClipName = "Walk_Stomp";
                    cfg.runClipName = "Charge_Run";
                    cfg.turnLeftClipName = string.Empty;
                    cfg.turnRightClipName = string.Empty;
                    cfg.inspectClipName = "Look_Scan";
                    cfg.roarClipName = "Roar_Massive";
                    cfg.biteClipName = "Bite_Snap";
                    cfg.bashClipName = "Fence_Bash";
                    cfg.clipSpecs = new[]
                    {
                        new DinoClipSpec("Idle_Heavy",   0.0f, 4.0f, true),
                        new DinoClipSpec("Walk_Stomp",   2.6f, 1.6f, true,  -1f,   new[] { 0.25f, 0.75f }),
                        new DinoClipSpec("Charge_Run",   7.0f, 0.8f, true,  -1f,   new[] { 0.22f, 0.72f }),
                        new DinoClipSpec("Roar_Massive", 0.0f, 3.2f, false),
                        new DinoClipSpec("Look_Scan",    0.0f, 4.0f, true),
                        new DinoClipSpec("Bite_Snap",    0.0f, 1.4f, false, 0.55f),
                        new DinoClipSpec("Fence_Bash",   0.0f, 2.0f, false, 0.85f)
                    };
                    break;

                case DinoSpeciesType.Brachiosaurus:
                    cfg.name = "DinoConfig_Brachiosaurus";
                    cfg.speciesKey = "brachiosaurus";
                    cfg.displayName = "Brachiosaurus";
                    cfg.navMeshAgentTypeName = "Agent_TRex";
                    cfg.agentRadius = 1.65f;
                    cfg.agentHeight = 12.0f;
                    cfg.eyeHeight = 11.5f;
                    cfg.turnSpeedDegPerSec = 52f;
                    cfg.acceleration = 2.8f;
                    cfg.stoppingDistance = 3.0f;
                    cfg.stalkSpeed = 0.95f;
                    cfg.walkSpeed = 1.35f;
                    cfg.runSpeed = 2.00f;
                    cfg.stalkClipStrideSpeed = 1.35f;
                    cfg.walkClipStrideSpeed = 1.35f;
                    cfg.runClipStrideSpeed = 2.00f;
                    cfg.minAnimSpeedScale = 0.70f;
                    cfg.maxAnimSpeedScale = 1.25f;
                    cfg.turnInPlaceAngleThresholdDeg = 55f;
                    cfg.turnInPlaceMaxSpeed = 0.25f;
                    cfg.sightRangeMeters = 35f;
                    cfg.sightFovDegrees = 160f;
                    cfg.hearingSensitivity = 0.60f;
                    cfg.awarenessGainRate = 0.35f;
                    cfg.awarenessDecayRate = 0.25f;
                    cfg.alertThreshold = 0.60f;
                    cfg.huntThreshold = 99f; // Peaceful herbivore
                    cfg.alertHoldSeconds = 3.0f;
                    cfg.loseSightSeconds = 4.0f;
                    cfg.searchDurationSeconds = 0f;
                    cfg.attackRangeMeters = 0f;
                    cfg.killRangeMeters = 0f;
                    cfg.attackCooldownSeconds = 4.0f;
                    cfg.attackHitDelaySeconds = 0f;
                    cfg.canBashDoors = false;
                    cfg.footstepSoundId = "trex_step";
                    cfg.alertSoundId = "sauropod_call_far";
                    cfg.roarSoundId = "sauropod_call_far";
                    cfg.sniffSoundId = string.Empty;
                    cfg.biteSoundId = string.Empty;
                    cfg.breathSoundId = string.Empty;
                    cfg.footstepTraumaMax = 0.16f;
                    cfg.footstepTraumaRadius = 38f;
                    cfg.roarTraumaMax = 0.12f;
                    cfg.roarTraumaRadius = 65f;
                    cfg.emitGroundFootprints = false;
                    cfg.idleClipName = "Idle_Graze";
                    cfg.stalkClipName = "Walk_Majestic";
                    cfg.walkClipName = "Walk_Majestic";
                    cfg.runClipName = "Walk_Majestic";
                    cfg.turnLeftClipName = string.Empty;
                    cfg.turnRightClipName = string.Empty;
                    cfg.inspectClipName = "Idle_Graze";
                    cfg.roarClipName = "Head_Lift_Call";
                    cfg.biteClipName = "Head_Lift_Call";
                    cfg.bashClipName = "Head_Lift_Call";
                    cfg.clipSpecs = new[]
                    {
                        new DinoClipSpec("Idle_Graze",     0.0f,  6.0f, true),
                        new DinoClipSpec("Walk_Majestic",  1.35f, 3.2f, true,  -1f, new[] { 0.12f, 0.37f, 0.62f, 0.87f }),
                        new DinoClipSpec("Head_Lift_Call", 0.0f,  4.0f, false)
                    };
                    break;

                case DinoSpeciesType.Triceratops:
                    cfg.name = "DinoConfig_Triceratops";
                    cfg.speciesKey = "triceratops";
                    cfg.displayName = "Triceratops";
                    cfg.navMeshAgentTypeName = "Agent_TRex";
                    cfg.agentRadius = 1.25f;
                    cfg.agentHeight = 3.10f;
                    cfg.eyeHeight = 2.05f;
                    cfg.turnSpeedDegPerSec = 60f;     // review 4
                    cfg.acceleration = 5.0f;
                    cfg.stoppingDistance = 2.0f;
                    cfg.stalkSpeed = 1.00f;
                    cfg.walkSpeed = 1.30f;
                    cfg.runSpeed = 3.20f;
                    cfg.stalkClipStrideSpeed = 1.30f;
                    cfg.walkClipStrideSpeed = 1.30f;
                    cfg.runClipStrideSpeed = 3.20f;
                    cfg.minAnimSpeedScale = 0.72f;
                    cfg.maxAnimSpeedScale = 1.35f;
                    cfg.turnInPlaceAngleThresholdDeg = 48f;
                    cfg.turnInPlaceMaxSpeed = 0.30f;
                    cfg.sightRangeMeters = 22f;
                    cfg.sightFovDegrees = 135f;
                    cfg.hearingSensitivity = 0.90f;
                    cfg.awarenessGainRate = 0.65f;
                    cfg.awarenessDecayRate = 0.25f;
                    cfg.alertThreshold = 0.45f;
                    cfg.huntThreshold = 99f; // Territorial display, not relentless predator
                    cfg.alertHoldSeconds = 2.8f;
                    cfg.loseSightSeconds = 4.5f;
                    cfg.searchDurationSeconds = 0f;
                    cfg.attackRangeMeters = 4.0f;
                    cfg.killRangeMeters = 0f;
                    cfg.attackCooldownSeconds = 3.5f;
                    cfg.attackHitDelaySeconds = 0.95f;
                    cfg.canBashDoors = false;
                    cfg.footstepSoundId = "trex_step";
                    cfg.alertSoundId = "trex_snort";
                    cfg.roarSoundId = "trex_snort";
                    cfg.sniffSoundId = "trex_snort";
                    cfg.biteSoundId = "trex_snort";
                    cfg.breathSoundId = string.Empty;
                    cfg.footstepTraumaMax = 0.10f;
                    cfg.footstepTraumaRadius = 20f;
                    cfg.roarTraumaMax = 0.16f;
                    cfg.roarTraumaRadius = 26f;
                    cfg.emitGroundFootprints = false;
                    cfg.idleClipName = "Idle_Alert";
                    cfg.stalkClipName = "Walk_Heavy";
                    cfg.walkClipName = "Walk_Heavy";
                    cfg.runClipName = "Walk_Heavy";
                    cfg.turnLeftClipName = string.Empty;
                    cfg.turnRightClipName = string.Empty;
                    cfg.inspectClipName = "Idle_Alert";
                    cfg.roarClipName = "Snort_Threat";
                    cfg.biteClipName = "Snort_Threat";
                    cfg.bashClipName = "Snort_Threat";
                    cfg.clipSpecs = new[]
                    {
                        new DinoClipSpec("Idle_Alert",   0.0f,  4.5f, true),
                        new DinoClipSpec("Walk_Heavy",   1.30f, 1.8f, true,  -1f,   new[] { 0.15f, 0.40f, 0.65f, 0.90f }),
                        new DinoClipSpec("Snort_Threat", 0.0f,  2.6f, false, 0.98f)
                    };
                    break;
            }
        }
    }
}
