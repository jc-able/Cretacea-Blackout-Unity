using UnityEngine;

namespace Cretacea.Core
{
    public enum CheckpointId
    {
        Intro = 0,
        Night = 1,
        Records = 2,
        Dock = 3,
        Paddock = 4,
        Substation = 5,
        Yard = 6
    }

    public enum SurfaceType
    {
        Concrete = 0,
        Tile = 1,
        Gravel = 2,
        Metal = 3,
        Mud = 4,
        Grass = 5,
        Water = 6
    }

    public enum AtmospherePreset
    {
        GoldenDay = 0,
        DuskTransition = 1,
        NightCalm = 2,
        NightStormBlackout = 3,
        DawnCoast = 4
    }

    public enum LightGroupId
    {
        VCMain = 0,
        VCEmergency = 1,
        VCExit = 2,
        Plaza = 3,
        Paths = 4,
        DockMain = 5,
        Talon = 6,
        PaddockBattery = 7,
        SubBattery = 8,
        YardBattery = 9,
        AuxSub = 10,
        AuxRoute = 11,
        AuxYard = 12
    }

    public enum DinoState
    {
        Idle = 0,
        Patrol = 1,
        Investigate = 2,
        Alert = 3,
        Hunt = 4,
        Attack = 5,
        Search = 6,
        Scripted = 7
    }

    public enum SubstationStationType
    {
        Breaker1 = 0,
        Breaker2 = 1,
        Breaker3 = 2,
        FuelPump = 3,
        GeneratorStarter = 4,
        TransferLever = 5
    }

    public enum InputDeviceType
    {
        KeyboardMouse = 0,
        Gamepad = 1
    }

    /// <summary>
    /// Exact 14-layer physics contract matching unity_aaa_blueprint.md §3.1.
    /// Resolves named layers when present in TagManager, falling back safely to canonical indices.
    /// </summary>
    public static class GameLayers
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int EnvironmentIndex = 3;
        public const int Water = 4;
        public const int UI = 5;

        public const int PlayerIndex = 6;
        public const int DinosaurIndex = 7;
        public const int InteractableIndex = 8;
        public const int TriggerIndex = 9;
        public const int DinoBlockerIndex = 10;
        public const int DoorIndex = 11;
        public const int ViewmodelIndex = 12;
        public const int RainBlockerIndex = 13;

        public const string EnvironmentName = "Environment";
        public const string PlayerName = "Player";
        public const string DinosaurName = "Dinosaur";
        public const string InteractableName = "Interactable";
        public const string TriggerName = "Trigger";
        public const string DinoBlockerName = "DinoBlocker";
        public const string DoorName = "Door";
        public const string ViewmodelName = "Viewmodel";
        public const string RainBlockerName = "RainBlocker";

        public static int Environment => Resolve(EnvironmentName, EnvironmentIndex);
        public static int Player => Resolve(PlayerName, PlayerIndex);
        public static int Dinosaur => Resolve(DinosaurName, DinosaurIndex);
        public static int Interactable => Resolve(InteractableName, InteractableIndex);
        public static int Trigger => Resolve(TriggerName, TriggerIndex);
        public static int DinoBlocker => Resolve(DinoBlockerName, DinoBlockerIndex);
        public static int Door => Resolve(DoorName, DoorIndex);
        public static int Viewmodel => Resolve(ViewmodelName, ViewmodelIndex);
        public static int RainBlocker => Resolve(RainBlockerName, RainBlockerIndex);

        public static int Mask(int layer) => 1 << layer;

        /// <summary>Solid static geometry (Default + Environment) + doors (occludes interaction rays, footsteps, headroom, LOS).</summary>
        public static int EnvironmentAndDoorMask => Mask(Default) | Mask(Environment) | Mask(Door);

        /// <summary>All surfaces the player stands on for footstep/crouch raycasts.</summary>
        public static int GroundMask => Mask(Default) | Mask(Environment) | Mask(Door) | Mask(Water);

        /// <summary>Layers tested by PlayerInteractor (interact triggers + solid world/doors for occlusion).</summary>
        public static int InteractionQueryMask => Mask(Default) | Mask(Environment) | Mask(Door) | Mask(Interactable);

        /// <summary>Layers that block dinosaur line-of-sight.</summary>
        public static int VisionOcclusionMask => Mask(Default) | Mask(Environment) | Mask(Door);

        /// <summary>Layers blocking dinosaur movement (walls, closed doors, DinoBlocker barriers).</summary>
        public static int DinoCollisionMask => Mask(Default) | Mask(Environment) | Mask(Door) | Mask(DinoBlocker);

        /// <summary>Layers blocking player capsule movement (never DinoBlocker).</summary>
        public static int PlayerCollisionMask => Mask(Default) | Mask(Environment) | Mask(Door);

        public static int Resolve(string name, int fallbackIndex)
        {
            int idx = LayerMask.NameToLayer(name);
            return idx >= 0 ? idx : fallbackIndex;
        }

        public static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null) return;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.layer = layer;
            }
        }
    }
}
