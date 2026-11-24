using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;
using Cretacea.Interact;

namespace Cretacea.Dev
{
    /// <summary>
    /// A NavMesh for a person, built at runtime for the playthrough bot only. The scene's baked NavMeshes are for the
    /// raptor (radius 0.45) and the T. rex: neither fits through a 1 m office door, so a raptor path from the security
    /// office ended 22 m short. This one uses the player's capsule (radius 0.34) at crouch height (the jammed roller door
    /// and the bent gate leaf are on the mesh; the bot crouches when the ceiling ahead is low) and the colliders the
    /// player collides with (Default and Environment, no triggers) over the story route's bounds. Closed doors are
    /// left out, so a doorway is a way through (the bot opens the door when it gets there); OPEN door leaves are put
    /// in, because an open leaf stands in the corridor. The mesh is rebuilt (async, well under a second) whenever the
    /// set of open doors changes.
    /// </summary>
    public static class PlayerNavMesh
    {
        public static int AgentTypeId { get; private set; } = -1;
        public static bool Ready { get; private set; }
        public static float BuildSeconds { get; private set; }
        public static string Info { get; private set; } = "";
        public static int Rebuilds { get; private set; }

        private static NavMeshData _data;
        private static NavMeshDataInstance _instance;
        private static NavMeshBuildSettings _settings;
        private static readonly List<NavMeshBuildSource> BaseSources = new List<NavMeshBuildSource>();
        private static AsyncOperation _op;
        private static float _startedAt;
        private static string _doorSignature = "";

        // x -95 .. 565, z -125 .. 95: the visitor center to the dawn coast road
        private static readonly Bounds RouteBounds = new Bounds(new Vector3(235f, 10f, -15f), new Vector3(660f, 70f, 220f));

        // Domain reload on Play is off in this project: forget the last session's mesh (Play mode removed it).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Ready = false; _op = null; _data = null; AgentTypeId = -1; BuildSeconds = 0f; Info = ""; Rebuilds = 0;
            _doorSignature = "";
            BaseSources.Clear();
        }

        public static bool Building => _op != null && !_op.isDone;

        /// <summary>
        /// Forget the mesh so the next EnsureBuilt collects the scene that is loaded now. A run started from the main
        /// menu built its first mesh from the menu scene (47 sources) and the bot then walked chapter 1 on a mesh that
        /// did not know the Visitor Center's furniture.
        /// </summary>
        public static void ResetForScene()
        {
            if (_data != null) NavMesh.RemoveNavMeshData(_instance);
            _data = null; _op = null; Ready = false; AgentTypeId = -1; _doorSignature = "";
            BaseSources.Clear();
        }

        public static void EnsureBuilt()
        {
            if (_data != null) return;
            _settings = NavMesh.CreateSettings();
            _settings.agentRadius = 0.34f;
            _settings.agentHeight = 1.1f;      // crouched capsule 1.05: crawl gaps are walkable, the bot crouches under them
            _settings.agentClimb = 0.35f;       // CharacterController step offset
            _settings.agentSlope = 46f;
            _settings.overrideVoxelSize = true;
            _settings.voxelSize = 0.12f;
            _settings.overrideTileSize = true;
            _settings.tileSize = 256;
            _settings.minRegionArea = 2f;
            AgentTypeId = _settings.agentTypeID;

            BaseSources.Clear();
            int mask = GameLayers.Mask(GameLayers.Default) | GameLayers.Mask(GameLayers.Environment);
            NavMeshBuilder.CollectSources(RouteBounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), BaseSources);
            int dropped = BaseSources.RemoveAll(src => src.component is Collider c && (c.isTrigger || !c.enabled));
            Info = $"{BaseSources.Count} sources ({dropped} triggers dropped)";

            _data = new NavMeshData(_settings.agentTypeID);
            _instance = NavMesh.AddNavMeshData(_data);
            StartBuild();
        }

        /// <summary>
        /// Recollect the world and rebuild: the story switches whole objects on and off (the Lobby shutter, the
        /// Staff Exit collapse, the substation fence), and a mesh collected before the blackout walks into them.
        /// </summary>
        public static void RebuildWorld()
        {
            if (_data == null) return;
            BaseSources.Clear();
            int mask = GameLayers.Mask(GameLayers.Default) | GameLayers.Mask(GameLayers.Environment);
            NavMeshBuilder.CollectSources(RouteBounds, mask, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), BaseSources);
            BaseSources.RemoveAll(src => src.component is Collider c && (c.isTrigger || !c.enabled));
            Rebuilds++;
            StartBuild();
        }

        /// <summary>Rebuild when a door opened or closed since the last build. Cheap enough to call every second.</summary>
        public static bool RefreshDoors()
        {
            if (_data == null || Building) return false;
            if (DoorSignature() == _doorSignature) return false;
            Rebuilds++;
            StartBuild();
            return true;
        }

        private static void StartBuild()
        {
            _doorSignature = DoorSignature();
            var doors = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(RouteBounds, GameLayers.Mask(GameLayers.Door), NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), doors);
            var sources = new List<NavMeshBuildSource>(BaseSources);
            foreach (NavMeshBuildSource src in doors)
            {
                if (!(src.component is Collider c) || c.isTrigger || !c.enabled) continue;
                // Door-layer blockers that are not doors (the Lobby shutter, the collapse), locked doors (walls until
                // the story unlocks them) and open leaves are solid; closed unlocked doors are a way through.
                DoorController d = c.GetComponentInParent<DoorController>();
                if (d == null || d.IsLocked || (d.IsOpen && d.OpenProgress > 0.5f)) sources.Add(src);
            }
            _startedAt = Time.realtimeSinceStartup;
            _op = NavMeshBuilder.UpdateNavMeshDataAsync(_data, _settings, sources, RouteBounds);
        }

        private static string DoorSignature()
        {
            var sb = new StringBuilder();
            foreach (DoorController d in Object.FindObjectsByType<DoorController>())
                if (d.IsLocked || (d.IsOpen && d.OpenProgress > 0.5f)) sb.Append(d.DoorId).Append(d.IsLocked ? "L," : ",");
            return sb.ToString();
        }

        /// <summary>Call every frame while waiting; true once the first mesh is usable (it stays usable during rebuilds).</summary>
        public static bool Poll()
        {
            if (!Ready && _op != null && _op.isDone)
            {
                Ready = true;
                BuildSeconds = Time.realtimeSinceStartup - _startedAt;
            }
            return Ready;
        }
    }
}
