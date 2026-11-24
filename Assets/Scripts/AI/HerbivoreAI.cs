using UnityEngine;
using Cretacea.Core;
using Cretacea.Player;

namespace Cretacea.AI
{
    /// <summary>
    /// Peaceful ambient & scripted behavior for Brachiosaurus (Titan Meadows / Tyrant Valley grazing,
    /// slow majestic walk, periodic Head_Lift_Call) and Triceratops (calm grazing/patrol + defensive
    /// Snort_Threat display if the player approaches too close).
    /// </summary>
    public sealed class HerbivoreAI : DinoControllerBase
    {
        [Header("Herbivore Species & Behavior")]
        [SerializeField] private DinoSpeciesType herbivoreSpecies = DinoSpeciesType.Brachiosaurus;
        [SerializeField] private float wanderRadiusMeters = 18f;
        [SerializeField] private float grazeDurationMin = 7.0f;
        [SerializeField] private float grazeDurationMax = 15.0f;
        [SerializeField] private float periodicCallIntervalMin = 14.0f;
        [SerializeField] private float periodicCallIntervalMax = 26.0f;

        [Header("Triceratops Territorial Threat")]
        [SerializeField] private float threatTriggerDistance = 11.0f;
        [SerializeField] private float threatCalmDistance = 15.5f;
        [SerializeField] private float threatShoveDistance = 3.6f;
        [SerializeField] private float snortCooldownSeconds = 5.0f;

        private Vector3 _homeAnchor;
        private float _grazeTimer;
        private float _callTimer;
        private float _snortCooldownTimer;
        private bool _hasHomeAnchor;

        protected override DinoSpeciesType DefaultSpeciesType =>
            herbivoreSpecies == DinoSpeciesType.Triceratops ? DinoSpeciesType.Triceratops : DinoSpeciesType.Brachiosaurus;

        private bool IsTriceratops => (config != null && config.speciesType == DinoSpeciesType.Triceratops)
                                      || herbivoreSpecies == DinoSpeciesType.Triceratops;

        public void SetHerbivoreSpecies(DinoSpeciesType species)
        {
            herbivoreSpecies = species == DinoSpeciesType.Triceratops
                ? DinoSpeciesType.Triceratops
                : DinoSpeciesType.Brachiosaurus;
        }

        private void Start()
        {
            _homeAnchor = transform.position;
            _hasHomeAnchor = true;
            _grazeTimer = UnityEngine.Random.Range(grazeDurationMin, grazeDurationMax);
            _callTimer = UnityEngine.Random.Range(periodicCallIntervalMin * 0.45f, periodicCallIntervalMax);
        }

        protected override void TickAmbientWhenAiDisabled(float dt)
        {
            // Keep Opening Cutscene Titan Meadows herbivores alive with gentle grazing, breathing & periodic calls!
            proceduralRig?.SetBreathingState(0.09f, 0.22f);

            _callTimer -= dt;
            if (_callTimer <= 0f && ActionLockTimer <= 0f)
            {
                _callTimer = UnityEngine.Random.Range(periodicCallIntervalMin, periodicCallIntervalMax);
                if (IsTriceratops)
                {
                    // Gentle curious head shift in opening meadow herd
                    float yaw = UnityEngine.Random.Range(-28f, 28f);
                    Vector3 look = EyePosition + (Quaternion.Euler(0f, yaw, 0f) * transform.forward) * 10f;
                    proceduralRig?.SetLookTarget(look, 0.65f);
                }
                else
                {
                    PlayActionClip(config.roarClipName, 4.0f);
                    EmitVocalization(config.roarSoundId, 0.55f, 3.2f, config.roarTraumaMax * 0.5f, config.roarTraumaRadius);
                }
            }
        }

        protected override void OnStateEntered(DinoState previous, DinoState next)
        {
            switch (next)
            {
                case DinoState.Idle:
                    StopNavigation();
                    _grazeTimer = UnityEngine.Random.Range(grazeDurationMin, grazeDurationMax);
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.08f, 0.22f);
                    break;

                case DinoState.Patrol:
                    RestoreNavMeshControl();
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.10f, 0.28f);
                    PickNextWalkDestination();
                    break;

                case DinoState.Alert:
                    StopNavigation();
                    proceduralRig?.SetBreathingState(0.18f, 0.55f);
                    if (IsTriceratops && _snortCooldownTimer <= 0f)
                    {
                        TriggerTriceratopsSnort();
                    }
                    break;
            }
        }

        protected override void TickAI(float dt)
        {
            if (!_hasHomeAnchor)
            {
                _homeAnchor = transform.position;
                _hasHomeAnchor = true;
            }

            if (_snortCooldownTimer > 0f) _snortCooldownTimer -= dt;
            _callTimer -= dt;

            // Triceratops territorial awareness if player approaches too close
            PlayerController player = PlayerController.Instance;
            if (IsTriceratops && player != null && !player.IsDead)
            {
                float dist = Vector3.Distance(transform.position, player.transform.position);
                if (dist <= threatTriggerDistance && CurrentState != DinoState.Alert)
                {
                    TransitionState(DinoState.Alert);
                    return;
                }
            }

            switch (CurrentState)
            {
                case DinoState.Idle:
                    TickGrazeIdle(dt);
                    break;

                case DinoState.Patrol:
                    TickMajesticWalk(dt);
                    break;

                case DinoState.Alert:
                    TickTerritorialThreat(dt, player);
                    break;

                default:
                    TransitionState(DinoState.Idle);
                    break;
            }
        }

        private void TickGrazeIdle(float dt)
        {
            // Brachiosaurus periodically lifts its long neck for a majestic valley call
            if (!IsTriceratops && _callTimer <= 0f && ActionLockTimer <= 0f)
            {
                _callTimer = UnityEngine.Random.Range(periodicCallIntervalMin, periodicCallIntervalMax);
                PlayActionClip(config.roarClipName, 4.0f);
                EmitVocalization(config.roarSoundId, 0.58f, 3.2f, config.roarTraumaMax, config.roarTraumaRadius);
            }

            _grazeTimer -= dt;
            if (_grazeTimer <= 0f && ActionLockTimer <= 0f)
            {
                TransitionState(DinoState.Patrol);
            }
        }

        private void TickMajesticWalk(float dt)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.velocity.sqrMagnitude > 0.04f)
                {
                    RotateTowardsPoint(transform.position + agent.velocity, dt, 1.0f);
                }
                else if (agent.hasPath)
                {
                    RotateTowardsPoint(agent.steeringTarget, dt, 1.0f);
                }
            }

            if (HasReachedDestination(1.5f) || StateTimer > 18.0f)
            {
                TransitionState(DinoState.Idle);
            }
        }

        private void TickTerritorialThreat(float dt, PlayerController player)
        {
            if (player == null || player.IsDead)
            {
                TransitionState(DinoState.Idle);
                return;
            }

            float dist = Vector3.Distance(transform.position, player.transform.position);
            RotateTowardsPoint(player.transform.position, dt, 1.35f);
            proceduralRig?.SetLookTarget(player.EyePosition, 1.0f);

            if (dist >= threatCalmDistance && ActionLockTimer <= 0f)
            {
                TransitionState(DinoState.Idle);
                return;
            }

            if (_snortCooldownTimer <= 0f && ActionLockTimer <= 0f)
            {
                TriggerTriceratopsSnort();
                if (dist <= threatShoveDistance)
                {
                    // Defensive close-range warning jolt without cheap instant death
                    GameEvents.RaiseCameraTrauma(0.42f, 1.5f);
                    GameEvents.RaiseDamageFlash(0.30f);
                }
            }
        }

        private void TriggerTriceratopsSnort()
        {
            _snortCooldownTimer = snortCooldownSeconds;
            PlayActionClip(config.roarClipName, 2.6f);
            EmitVocalization(config.roarSoundId, 0.72f, 1.8f, config.roarTraumaMax, config.roarTraumaRadius,
                "[TRICERATOPS SNORTS A WARNING]");
        }

        protected override void OnHeardNoise(Vector3 noisePos, float effectiveRadius, string category)
        {
            // Herbivores glance curiously toward nearby noises without hunting the player
            proceduralRig?.SetLookTarget(noisePos + Vector3.up * 1.2f, 0.75f);
            if (IsTriceratops && Vector3.Distance(transform.position, noisePos) <= threatTriggerDistance)
            {
                TransitionState(DinoState.Alert);
            }
        }

        private void PickNextWalkDestination()
        {
            if (PatrolWaypoints.Count > 0)
            {
                PatrolIndex = (PatrolIndex + 1) % PatrolWaypoints.Count;
                MoveToDestination(PatrolWaypoints[PatrolIndex], config.walkSpeed);
                return;
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadiusMeters;
            Vector3 target = _homeAnchor + new Vector3(offset.x, 0f, offset.y);
            target.y = ResolveGroundY(target.x, target.z, _homeAnchor.y);
            if (!MoveToDestination(target, config.walkSpeed))
            {
                // If meadow has no baked NavMesh (e.g. Opening Cutscene distant valley), stay gracefully grazing
                TransitionState(DinoState.Idle);
            }
        }
    }
}
