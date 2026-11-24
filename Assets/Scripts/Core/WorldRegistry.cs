using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Centralized named lookup table for zones, doors, light groups, CCTV feeds, spawn/story anchors,
    /// DinoBlockers, and named scene objects. Eliminates fragile GameObject.Find() calls across the world.
    /// </summary>
    public sealed class WorldRegistry : MonoBehaviour
    {
        public static WorldRegistry Instance { get; private set; }

        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _namedObjects = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IDoor> _doors = new Dictionary<string, IDoor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICctvController> _cctvs = new Dictionary<string, ICctvController>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _zones = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, IInteractable> _interactables = new Dictionary<string, IInteractable>(StringComparer.OrdinalIgnoreCase);

        public IDinoSpawner DinoSpawner { get; private set; }
        public LightGroupController LightGroups { get; private set; }
        public AtmosphereController Atmosphere { get; private set; }

        /// <summary>
        /// Canonical fallback Unity coordinates converted from Godot via Unity(x,y,z) = Godot(x,y,-z).
        /// Used gracefully if an anchor Transform has not yet been placed in a partial dev scene.
        /// </summary>
        private static readonly Dictionary<string, Vector3> DefaultAnchorPositions = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
        {
            // Checkpoints & Player Spawns (Unity +Z = North, +X = East)
            { "start", new Vector3(-64.5f, 0.05f, 28.0f) },
            { "spawn_records", new Vector3(-6.0f, 0.05f, 24.5f) },
            { "dock", new Vector3(18.0f, 0.05f, -10.0f) },
            { "paddock_entry", new Vector3(93.5f, 0.1f, -2.5f) },
            { "sub_checkpoint", new Vector3(252.0f, 2.35f, 0.0f) },
            { "substation_center", new Vector3(268.0f, 2.3f, 0.0f) },
            { "yard_gap", new Vector3(401.0f, 1.0f, -22.8f) },
            { "yard_gate_outside", new Vector3(397.0f, 1.0f, -22.8f) },
            { "ending_view", new Vector3(543.0f, 1.05f, -46.0f) },

            // Visitor Center Landmarks
            { "flashlight_desk", new Vector3(-64.0f, 0.82f, 31.0f) },
            { "memo_switchover", new Vector3(-65.8f, 0.81f, 30.9f) },
            { "terminal", new Vector3(-61.0f, 0.9f, 26.0f) },
            { "control_console", new Vector3(-37.0f, 0.95f, 31.2f) },
            { "talon_gate_view", new Vector3(-36.0f, 2.5f, 42.0f) },
            { "lobby_shutter_look", new Vector3(-32.0f, 2.0f, -31.6f) },
            { "shutter_motor", new Vector3(-28.6f, 3.6f, -31.4f) },
            { "staff_exit_trigger", new Vector3(-55.0f, 1.0f, -10.0f) },
            { "staff_exit_look", new Vector3(-64.5f, 1.2f, -10.0f) },
            { "manual", new Vector3(-3.6f, 0.82f, 27.3f) },
            { "keeper_note", new Vector3(4.2f, 0.92f, 14.0f) },
            { "wing_hall", new Vector3(-2.0f, 0.0f, 11.0f) },
            { "wing_cold", new Vector3(-7.0f, 0.0f, -4.0f) },
            { "wing_vet", new Vector3(3.0f, 0.0f, -3.0f) },
            { "wing_south", new Vector3(0.0f, 0.0f, -11.0f) },
            { "dock_door", new Vector3(8.0f, 0.0f, -11.0f) },

            // Service Paths Landmarks
            { "paths_corner2", new Vector3(56.0f, 0.0f, 14.0f) },
            { "paths_mid", new Vector3(71.0f, 0.0f, 6.0f) },
            { "paths_end", new Vector3(85.0f, 0.0f, -4.0f) },
            { "raptor_cross_a", new Vector3(64.0f, 0.0f, 30.0f) },
            { "raptor_cross_b", new Vector3(64.0f, 0.0f, -6.0f) },
            { "fence_gap", new Vector3(91.0f, 0.0f, -2.4f) },
            { "service_gate_box", new Vector3(89.6f, 1.1f, -6.0f) },
            { "spark_lamp", new Vector3(62.0f, 4.2f, 14.0f) },

            // Tyrant Valley & Substation 3
            { "trex_drink", new Vector3(192.0f, 0.0f, 40.0f) },
            { "jeep_radio", new Vector3(183.5f, 1.0f, -21.0f) },
            { "breaker_1", new Vector3(256.0f, 2.3f, 13.0f) },
            { "breaker_2", new Vector3(258.0f, 2.3f, -13.0f) },
            { "breaker_3", new Vector3(282.0f, 2.3f, 13.0f) },
            { "fuel_pump", new Vector3(263.8f, 2.3f, 11.0f) },
            { "generator", new Vector3(272.0f, 2.3f, 9.0f) },
            { "generator_starter", new Vector3(273.5f, 2.3f, 7.1f) },
            { "transfer_lever", new Vector3(277.6f, 2.3f, -7.0f) },

            // Old Yard & Coast
            { "yard_raptor_a", new Vector3(445.0f, 1.0f, 20.0f) },
            { "yard_raptor_b", new Vector3(470.0f, 1.0f, -30.0f) },
            { "halvorsen_log", new Vector3(420.4f, 1.85f, -11.3f) },
            { "site_map", new Vector3(442.0f, 2.2f, -17.85f) },
            { "e0_gate", new Vector3(505.0f, 1.0f, -46.0f) },
            { "gate_box", new Vector3(503.8f, 1.9f, -52.6f) },
            { "coast", new Vector3(543.0f, 1.0f, -46.0f) },

            // Opening Cutscene Meadows
            { "meadow_sauropod_a", new Vector3(-40.0f, -6.2f, -150.0f) },
            { "meadow_sauropod_b", new Vector3(-95.0f, -6.2f, -185.0f) },
            { "meadow_trike", new Vector3(-18.0f, -6.2f, -142.0f) }
        };

        public static WorldRegistry EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<WorldRegistry>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("[WorldRegistry]");
            Instance = go.AddComponent<WorldRegistry>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RegisterAnchor(string id, Transform anchor)
        {
            if (!string.IsNullOrWhiteSpace(id) && anchor != null)
                _anchors[id] = anchor;
        }

        public void UnregisterAnchor(string id, Transform anchor)
        {
            if (!string.IsNullOrWhiteSpace(id) && _anchors.TryGetValue(id, out Transform existing) && existing == anchor)
                _anchors.Remove(id);
        }

        public bool TryGetAnchor(string id, out Transform anchor) => _anchors.TryGetValue(id, out anchor);

        public Vector3 GetPoint(string id, Vector3 fallback = default)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                if (_anchors.TryGetValue(id, out Transform t) && t != null)
                    return t.position;
                if (DefaultAnchorPositions.TryGetValue(id, out Vector3 canonical))
                    return SampleTerrainIfNeeded(id, canonical);
            }
            return fallback;
        }

        public Quaternion GetRotation(string id, Quaternion fallback)
        {
            if (!string.IsNullOrWhiteSpace(id) && _anchors.TryGetValue(id, out Transform t) && t != null)
                return t.rotation;
            return fallback;
        }

        /// <summary>Samples active Unity Terrain height when computing outdoor valley coordinates.</summary>
        public float SampleGroundHeight(float x, float z, float fallbackY = 0f)
        {
            // The island is several terrain tiles (the playable one plus the outer ring): Terrain.activeTerrain is
            // whichever tile Unity lists first, so look for the tile that holds the point.
            Terrain[] terrains = Terrain.activeTerrains;
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 terrainPos = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x >= terrainPos.x && x <= terrainPos.x + size.x && z >= terrainPos.z && z <= terrainPos.z + size.z)
                {
                    return terrain.SampleHeight(new Vector3(x, 0f, z)) + terrainPos.y;
                }
            }
            return fallbackY;
        }

        private Vector3 SampleTerrainIfNeeded(string id, Vector3 pos)
        {
            if (id.Equals("trex_drink", StringComparison.OrdinalIgnoreCase)
                || id.Equals("jeep_radio", StringComparison.OrdinalIgnoreCase)
                || id.Equals("paddock_entry", StringComparison.OrdinalIgnoreCase))
            {
                float h = SampleGroundHeight(pos.x, pos.z, pos.y);
                return new Vector3(pos.x, id.Equals("jeep_radio", StringComparison.OrdinalIgnoreCase) ? h + 0.9f : h, pos.z);
            }
            return pos;
        }

        public void RegisterObject(string id, GameObject obj)
        {
            if (!string.IsNullOrWhiteSpace(id) && obj != null)
                _namedObjects[id] = obj;
        }

        public void UnregisterObject(string id, GameObject obj)
        {
            if (!string.IsNullOrWhiteSpace(id) && _namedObjects.TryGetValue(id, out GameObject existing) && existing == obj)
                _namedObjects.Remove(id);
        }

        public GameObject GetObject(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _namedObjects.TryGetValue(id, out GameObject obj) ? obj : null;
        }

        public void RegisterZone(string zoneId, GameObject zoneRoot)
        {
            if (!string.IsNullOrWhiteSpace(zoneId) && zoneRoot != null)
                _zones[zoneId] = zoneRoot;
        }

        public GameObject GetZone(string zoneId)
        {
            return !string.IsNullOrWhiteSpace(zoneId) && _zones.TryGetValue(zoneId, out GameObject z) ? z : null;
        }

        public void RegisterDoor(string doorId, IDoor door)
        {
            if (!string.IsNullOrWhiteSpace(doorId) && door != null)
                _doors[doorId] = door;
        }

        public void UnregisterDoor(string doorId, IDoor door)
        {
            if (!string.IsNullOrWhiteSpace(doorId) && _doors.TryGetValue(doorId, out IDoor existing) && ReferenceEquals(existing, door))
                _doors.Remove(doorId);
        }

        public IDoor GetDoor(string doorId)
        {
            return !string.IsNullOrWhiteSpace(doorId) && _doors.TryGetValue(doorId, out IDoor door) ? door : null;
        }

        public IEnumerable<IDoor> GetAllDoors() => _doors.Values;

        public void RegisterInteractable(string id, IInteractable interactable)
        {
            if (!string.IsNullOrWhiteSpace(id) && interactable != null)
                _interactables[id] = interactable;
        }

        public void UnregisterInteractable(string id, IInteractable interactable)
        {
            if (!string.IsNullOrWhiteSpace(id) && _interactables.TryGetValue(id, out IInteractable existing) && ReferenceEquals(existing, interactable))
                _interactables.Remove(id);
        }

        public IInteractable GetInteractable(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && _interactables.TryGetValue(id, out IInteractable it) ? it : null;
        }

        public void RegisterCctv(ICctvController cctv)
        {
            if (cctv != null && !string.IsNullOrWhiteSpace(cctv.ControllerId))
                _cctvs[cctv.ControllerId] = cctv;
        }

        public void UnregisterCctv(ICctvController cctv)
        {
            if (cctv != null && !string.IsNullOrWhiteSpace(cctv.ControllerId))
                _cctvs.Remove(cctv.ControllerId);
        }

        public void SetAllCctvStatic(bool staticEnabled)
        {
            foreach (ICctvController cctv in _cctvs.Values)
            {
                cctv.SetStatic(staticEnabled);
            }
        }

        public void RegisterDinoSpawner(IDinoSpawner spawner) => DinoSpawner = spawner;
        public void RegisterLightGroupController(LightGroupController controller) => LightGroups = controller;
        public void RegisterAtmosphereController(AtmosphereController controller) => Atmosphere = controller;
    }
}
