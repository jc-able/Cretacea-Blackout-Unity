using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;

namespace Cretacea.AI
{
    /// <summary>
    /// Centralized dinosaur spawner implementing IDinoSpawner and registering with WorldRegistry.
    /// Handles spawning, activation, teleportation, despawning, and clean checkpoint resets for
    /// Velociraptor, Tyrannosaurus, Brachiosaurus, and Triceratops across all 7 chapters & set-pieces.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DinoSpawner : MonoBehaviour, IDinoSpawner
    {
        public static DinoSpawner Instance { get; private set; }

        [Header("Species Prefabs (Auto-Wired by DinoAnimatorBuilder)")]
        [SerializeField] private GameObject raptorPrefab;
        [SerializeField] private GameObject trexPrefab;
        [SerializeField] private GameObject brachiosaurusPrefab;
        [SerializeField] private GameObject triceratopsPrefab;

        [Header("Optional Config Overrides (Defaults Used if Null)")]
        [SerializeField] private DinoConfig raptorConfig;
        [SerializeField] private DinoConfig trexConfig;
        [SerializeField] private DinoConfig brachiosaurusConfig;
        [SerializeField] private DinoConfig triceratopsConfig;

        [Header("Hierarchy Organization")]
        [SerializeField] private Transform spawnContainer;

        private readonly List<DinoControllerBase> _activeDinos = new List<DinoControllerBase>();
        private int _spawnSequence;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (spawnContainer == null) spawnContainer = transform;
            WorldRegistry.EnsureInstance().RegisterDinoSpawner(this);
        }

        private void OnEnable()
        {
            WorldRegistry.EnsureInstance().RegisterDinoSpawner(this);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (WorldRegistry.Instance != null && ReferenceEquals(WorldRegistry.Instance.DinoSpawner, this))
            {
                WorldRegistry.Instance.RegisterDinoSpawner(null);
            }
        }

        // =========================================================================
        // IDinoSpawner IMPLEMENTATION
        // =========================================================================

        public IDinoController Spawn(string species, Vector3 position, float yawDegrees, string customId = null)
        {
            PruneNulls();

            DinoSpeciesType speciesType = DinoConfig.ParseSpeciesType(species);
            string canonicalKey = DinoConfig.NormalizeSpeciesKey(species);
            string instanceId = !string.IsNullOrWhiteSpace(customId)
                ? customId
                : $"{canonicalKey}_{++_spawnSequence:00}";

            // Replace any existing dino sharing the same explicit customId to prevent duplicates on reload
            if (!string.IsNullOrWhiteSpace(customId))
            {
                DespawnById(customId);
            }

            Vector3 groundedPos = ResolveSpawnPosition(position, speciesType);
            Quaternion rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            GameObject prefab = GetPrefab(speciesType);
            DinoConfig cfgOverride = GetConfigOverride(speciesType);

            GameObject instanceGo;
            if (prefab != null)
            {
                instanceGo = Instantiate(prefab, groundedPos, rotation, spawnContainer);
            }
            else
            {
                Debug.LogWarning($"[DinoSpawner] Prefab for '{speciesType}' is not assigned yet; spawning runtime fallback actor for '{instanceId}'. Run 'cretacea build_dino_prefabs' once FBXs are imported.");
                instanceGo = CreateFallbackRuntimeDino(speciesType, groundedPos, rotation, cfgOverride);
            }

            instanceGo.name = instanceId;
            DinoControllerBase controller = instanceGo.GetComponent<DinoControllerBase>();
            if (controller == null)
            {
                controller = AttachSpeciesController(instanceGo, speciesType);
            }

            controller.InitializeInstance(instanceId, cfgOverride);
            controller.Teleport(groundedPos, yawDegrees);
            controller.OnDespawned += HandleDinoDespawned;

            _activeDinos.Add(controller);
            return controller;
        }

        public IReadOnlyList<IDinoController> GetActiveDinos(string speciesFilter = null)
        {
            PruneNulls();
            var result = new List<IDinoController>(_activeDinos.Count);
            bool hasFilter = !string.IsNullOrWhiteSpace(speciesFilter);
            string normalizedFilter = hasFilter ? DinoConfig.NormalizeSpeciesKey(speciesFilter) : null;

            for (int i = 0; i < _activeDinos.Count; i++)
            {
                DinoControllerBase dino = _activeDinos[i];
                if (dino == null) continue;
                if (hasFilter && !string.Equals(dino.Species, normalizedFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add(dino);
            }
            return result;
        }

        public void DespawnAll(string speciesFilter = null)
        {
            PruneNulls();
            bool hasFilter = !string.IsNullOrWhiteSpace(speciesFilter);
            string normalizedFilter = hasFilter ? DinoConfig.NormalizeSpeciesKey(speciesFilter) : null;

            for (int i = _activeDinos.Count - 1; i >= 0; i--)
            {
                DinoControllerBase dino = _activeDinos[i];
                if (dino == null)
                {
                    _activeDinos.RemoveAt(i);
                    continue;
                }
                if (hasFilter && !string.Equals(dino.Species, normalizedFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                dino.OnDespawned -= HandleDinoDespawned;
                _activeDinos.RemoveAt(i);
                Destroy(dino.gameObject);
            }
        }

        // =========================================================================
        // CONVENIENCE MANAGEMENT & CHECKPOINT RESET API
        // =========================================================================

        public IDinoController GetDinoById(string dinoId)
        {
            if (string.IsNullOrWhiteSpace(dinoId)) return null;
            PruneNulls();
            for (int i = 0; i < _activeDinos.Count; i++)
            {
                if (string.Equals(_activeDinos[i].DinoId, dinoId, StringComparison.OrdinalIgnoreCase))
                    return _activeDinos[i];
            }
            return null;
        }

        public bool DespawnById(string dinoId)
        {
            if (string.IsNullOrWhiteSpace(dinoId)) return false;
            PruneNulls();
            for (int i = _activeDinos.Count - 1; i >= 0; i--)
            {
                DinoControllerBase dino = _activeDinos[i];
                if (string.Equals(dino.DinoId, dinoId, StringComparison.OrdinalIgnoreCase))
                {
                    dino.OnDespawned -= HandleDinoDespawned;
                    _activeDinos.RemoveAt(i);
                    Destroy(dino.gameObject);
                    return true;
                }
            }
            return false;
        }

        public bool TeleportDino(string dinoId, Vector3 worldPos, float yawDegrees)
        {
            IDinoController dino = GetDinoById(dinoId);
            if (dino == null) return false;
            dino.Teleport(worldPos, yawDegrees);
            return true;
        }

        public void SetSpeciesActive(string speciesFilter, bool active)
        {
            PruneNulls();
            string normalizedFilter = string.IsNullOrWhiteSpace(speciesFilter)
                ? null
                : DinoConfig.NormalizeSpeciesKey(speciesFilter);

            for (int i = 0; i < _activeDinos.Count; i++)
            {
                DinoControllerBase dino = _activeDinos[i];
                if (normalizedFilter != null && !string.Equals(dino.Species, normalizedFilter, StringComparison.OrdinalIgnoreCase))
                    continue;
                dino.gameObject.SetActive(active);
            }
        }

        public void ResetForCheckpoint()
        {
            DespawnAll();
            _spawnSequence = 0;
        }

        public void AssignPrefab(DinoSpeciesType species, GameObject prefab)
        {
            switch (species)
            {
                case DinoSpeciesType.Velociraptor: raptorPrefab = prefab; break;
                case DinoSpeciesType.Tyrannosaurus: trexPrefab = prefab; break;
                case DinoSpeciesType.Brachiosaurus: brachiosaurusPrefab = prefab; break;
                case DinoSpeciesType.Triceratops: triceratopsPrefab = prefab; break;
            }
        }

        public void AssignConfig(DinoSpeciesType species, DinoConfig cfg)
        {
            switch (species)
            {
                case DinoSpeciesType.Velociraptor: raptorConfig = cfg; break;
                case DinoSpeciesType.Tyrannosaurus: trexConfig = cfg; break;
                case DinoSpeciesType.Brachiosaurus: brachiosaurusConfig = cfg; break;
                case DinoSpeciesType.Triceratops: triceratopsConfig = cfg; break;
            }
        }

        // =========================================================================
        // INTERNAL HELPERS
        // =========================================================================

        private GameObject GetPrefab(DinoSpeciesType species)
        {
            switch (species)
            {
                case DinoSpeciesType.Velociraptor: return raptorPrefab;
                case DinoSpeciesType.Tyrannosaurus: return trexPrefab;
                case DinoSpeciesType.Brachiosaurus: return brachiosaurusPrefab;
                case DinoSpeciesType.Triceratops: return triceratopsPrefab;
                default: return raptorPrefab;
            }
        }

        private DinoConfig GetConfigOverride(DinoSpeciesType species)
        {
            switch (species)
            {
                case DinoSpeciesType.Velociraptor: return raptorConfig;
                case DinoSpeciesType.Tyrannosaurus: return trexConfig;
                case DinoSpeciesType.Brachiosaurus: return brachiosaurusConfig;
                case DinoSpeciesType.Triceratops: return triceratopsConfig;
                default: return null;
            }
        }

        private Vector3 ResolveSpawnPosition(Vector3 requestedPos, DinoSpeciesType species)
        {
            // 1. Prefer snapping cleanly onto the matching NavMesh surface if close
            DinoConfig cfg = GetConfigOverride(species);
            string agentTypeName = cfg != null ? cfg.navMeshAgentTypeName : (species == DinoSpeciesType.Velociraptor ? "Agent_Raptor" : "Agent_TRex");
            int agentTypeId = DinoControllerBase.ResolveNavMeshAgentTypeId(agentTypeName);

            NavMeshQueryFilter filter = new NavMeshQueryFilter
            {
                agentTypeID = agentTypeId,
                areaMask = NavMesh.AllAreas
            };
            if (NavMesh.SamplePosition(requestedPos, out NavMeshHit navHit, 3.0f, filter))
            {
                return navHit.position;
            }

            // 2. Fall back to downward physics raycast or Terrain height so exterior cutscene spawns never float
            Vector3 probe = new Vector3(requestedPos.x, requestedPos.y + 4.0f, requestedPos.z);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit rayHit, 12.0f, DinoLayerMasks.GroundMask, QueryTriggerInteraction.Ignore))
            {
                return rayHit.point;
            }

            if (WorldRegistry.Instance != null)
            {
                requestedPos.y = WorldRegistry.Instance.SampleGroundHeight(requestedPos.x, requestedPos.z, requestedPos.y);
            }
            return requestedPos;
        }

        private GameObject CreateFallbackRuntimeDino(DinoSpeciesType species, Vector3 pos, Quaternion rot, DinoConfig cfgOverride)
        {
            DinoConfig cfg = cfgOverride != null ? cfgOverride : DinoConfig.CreateDefault(species);
            var root = new GameObject($"Fallback_{species}");
            root.transform.SetPositionAndRotation(pos, rot);
            root.transform.SetParent(spawnContainer, true);
            root.layer = GameLayers.Dinosaur;

            NavMeshAgent nav = root.AddComponent<NavMeshAgent>();
            nav.radius = cfg.agentRadius;
            nav.height = cfg.agentHeight;
            nav.speed = cfg.walkSpeed;

            CapsuleCollider col = root.AddComponent<CapsuleCollider>();
            col.radius = cfg.agentRadius;
            col.height = cfg.agentHeight;
            col.center = new Vector3(0f, cfg.agentHeight * 0.5f, 0f);

            AudioSource audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 1.0f;
            audio.playOnAwake = false;
            audio.minDistance = species == DinoSpeciesType.Tyrannosaurus ? 8f : 3f;
            audio.maxDistance = species == DinoSpeciesType.Tyrannosaurus ? 95f : 42f;

            root.AddComponent<DinoProceduralRig>();
            AttachSpeciesController(root, species);
            return root;
        }

        private static DinoControllerBase AttachSpeciesController(GameObject go, DinoSpeciesType species)
        {
            switch (species)
            {
                case DinoSpeciesType.Tyrannosaurus:
                    return go.AddComponent<TrexAI>();

                case DinoSpeciesType.Brachiosaurus:
                case DinoSpeciesType.Triceratops:
                    HerbivoreAI herb = go.AddComponent<HerbivoreAI>();
                    herb.SetHerbivoreSpecies(species);
                    return herb;

                default:
                    return go.AddComponent<RaptorAI>();
            }
        }

        private void HandleDinoDespawned(DinoControllerBase dino)
        {
            if (dino != null)
            {
                dino.OnDespawned -= HandleDinoDespawned;
                _activeDinos.Remove(dino);
            }
        }

        private void PruneNulls()
        {
            for (int i = _activeDinos.Count - 1; i >= 0; i--)
            {
                if (_activeDinos[i] == null)
                {
                    _activeDinos.RemoveAt(i);
                }
            }
        }
    }
}
