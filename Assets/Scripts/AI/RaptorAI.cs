using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;
using Cretacea.Player;

namespace Cretacea.AI
{
    /// <summary>
    /// AAA Velociraptor predator state machine:
    /// Dormant -> Patrol -> Investigate (noise / door / flashlight sweep) -> Alert/Stalk (low posture
    /// clearing 2.2-2.4 m doorways) -> Hunt -> Attack (LungeBite) -> BashDoor (4 hits on closed unlocked IDoor)
    /// -> Search -> ScriptedSequence (Talon Ridge breakout, Lobby glass stalk, Service Path corner-2 crossing, E-0 slam).
    /// </summary>
    public sealed class RaptorAI : DinoControllerBase
    {
        [Header("Raptor Predator Tuning")]
        [SerializeField] private bool startsDormant;
        [SerializeField] private float dormantWakeRadius = 14f;
        [SerializeField] private float waypointWaitMin = 1.6f;
        [SerializeField] private float waypointWaitMax = 3.2f;
        [SerializeField] private float stalkDistanceThreshold = 9.5f;
        [SerializeField] private float lungeForwardSpeed = 5.4f;
        [SerializeField] private float searchRadiusMeters = 6.0f;
        [SerializeField] private int maxLockedDoorBashAttempts = 2;

        private bool _isDormant;
        private float _waypointPauseTimer;
        private bool _waitingAtWaypoint;
        private float _attackCooldownTimer;
        private float _doorBashTimer;
        private float _breathTimer;
        private float _searchStepTimer;
        private int _searchStepIndex;
        private bool _didInvestigateSniff;
        private bool _isBashingDoor;
        private IDoor _activeBashDoor;
        private int _lockedDoorBashCount;
        private Vector3 _lastProgressCheckPos;
        private float _stallTimer;

        protected override DinoSpeciesType DefaultSpeciesType => DinoSpeciesType.Velociraptor;

        public bool IsDormant => _isDormant;
        public bool IsBashingDoor => _isBashingDoor;

        protected override void Awake()
        {
            base.Awake();
            _isDormant = startsDormant;
        }

        private void Start()
        {
            if (AiEnabled && CurrentState == DinoState.Idle && !_isDormant && PatrolWaypoints.Count > 0)
            {
                TransitionState(DinoState.Patrol);
            }
        }

        public void SetDormant(bool dormant)
        {
            _isDormant = dormant;
            if (dormant)
            {
                StopNavigation();
                TransitionState(DinoState.Idle);
            }
            else if (AiEnabled && CurrentState == DinoState.Idle)
            {
                TransitionState(PatrolWaypoints.Count > 0 ? DinoState.Patrol : DinoState.Search);
            }
        }

        public void StartTalonBreakoutSequence(Vector3 gateExitPos, Vector3 fleeTargetPos, Action onComplete = null)
        {
            AiEnabled = false;
            _isDormant = false;
            EmitVocalization(config.roarSoundId, 0.9f, 1.3f, config.roarTraumaMax, config.roarTraumaRadius, ignoreCooldown: true);
            ScriptMoveTo(gateExitPos, config.runSpeed + 0.2f, () =>
            {
                ScriptMoveTo(fleeTargetPos, config.runSpeed + 0.2f, () =>
                {
                    if (onComplete != null) onComplete.Invoke();
                    else Despawn();
                });
            });
        }

        public void StartServicePathScareCrossing(Vector3 startPos, Vector3 crossEndPos, Action onComplete = null)
        {
            AiEnabled = false;
            _isDormant = false;
            Teleport(startPos, Quaternion.LookRotation((crossEndPos - startPos).normalized, Vector3.up).eulerAngles.y);
            EmitVocalization(config.roarSoundId, 0.92f, 1.1f, config.roarTraumaMax, config.roarTraumaRadius, ignoreCooldown: true);
            ScriptMoveTo(crossEndPos, config.runSpeed + 0.7f, () =>
            {
                if (onComplete != null) onComplete.Invoke();
                else Despawn();
            });
        }

        // =========================================================================
        // STATE MACHINE ENTRY & TICK
        // =========================================================================

        protected override void OnStateEntered(DinoState previous, DinoState next)
        {
            _isBashingDoor = false;
            _activeBashDoor = null;
            _lockedDoorBashCount = 0;
            _waitingAtWaypoint = false;
            _didInvestigateSniff = false;

            switch (next)
            {
                case DinoState.Idle:
                    StopNavigation();
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.08f, 0.35f);
                    break;

                case DinoState.Patrol:
                    _isDormant = false;
                    RestoreNavMeshControl();
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.10f, 0.45f);
                    if (PatrolWaypoints.Count > 0)
                    {
                        MoveToDestination(PatrolWaypoints[PatrolIndex], GetPostureAdjustedSpeed(config.walkSpeed));
                    }
                    break;

                case DinoState.Investigate:
                    _isDormant = false;
                    RestoreNavMeshControl();
                    proceduralRig?.SetLookTarget(LastNoisePosition + Vector3.up * 1.1f, 0.85f);
                    proceduralRig?.SetBreathingState(0.14f, 0.65f);
                    if (previous == DinoState.Idle || previous == DinoState.Patrol)
                    {
                        EmitVocalization(config.alertSoundId, 0.50f, 0.7f);
                    }
                    MoveToDestination(LastNoisePosition, config.stalkSpeed);
                    break;

                case DinoState.Alert:
                    _isDormant = false;
                    RestoreNavMeshControl();
                    proceduralRig?.SetBreathingState(0.18f, 0.85f);
                    EmitVocalization(config.alertSoundId, 0.62f, 0.9f);
                    break;

                case DinoState.Hunt:
                    _isDormant = false;
                    UnseenTimer = 0f;
                    RestoreNavMeshControl();
                    proceduralRig?.SetBreathingState(0.24f, 1.35f);
                    if (previous != DinoState.Attack)
                    {
                        // Play Roar_Call when confirming hunt — NEVER Bite_Lunge from far away!
                        PlayActionClip(config.roarClipName, 0.95f);
                        EmitVocalization(config.roarSoundId, 0.95f, 1.25f, config.roarTraumaMax, config.roarTraumaRadius,
                            "[A RAPTOR SCREECHES NEARBY!]");
                    }
                    break;

                case DinoState.Attack:
                    StopNavigation();
                    _attackCooldownTimer = config.attackCooldownSeconds;
                    PlayActionClip(config.biteClipName, 0.95f);
                    EmitVocalization(config.biteSoundId, 0.96f, 0.75f, 0.25f, 10f, ignoreCooldown: true);
                    break;

                case DinoState.Search:
                    RestoreNavMeshControl();
                    _searchStepIndex = 0;
                    _searchStepTimer = 0f;
                    proceduralRig?.SetBreathingState(0.16f, 0.85f);
                    MoveToDestination(LastSeenPosition, config.walkSpeed);
                    break;
            }
        }

        protected override void TickAI(float dt)
        {
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;
            if (_doorBashTimer > 0f) _doorBashTimer -= dt;

            UpdateCloseBreathingCue(dt);
            UpdateLowDoorwayPosture();

            if (_isBashingDoor)
            {
                TickDoorBashing(dt);
                return;
            }

            switch (CurrentState)
            {
                case DinoState.Idle:
                    TickIdleOrDormant(dt);
                    break;
                case DinoState.Patrol:
                    TickPatrol(dt);
                    break;
                case DinoState.Investigate:
                    TickInvestigate(dt);
                    break;
                case DinoState.Alert:
                    TickAlertStalk(dt);
                    break;
                case DinoState.Hunt:
                    TickHunt(dt);
                    break;
                case DinoState.Attack:
                    TickAttackLunge(dt);
                    break;
                case DinoState.Search:
                    TickSearch(dt);
                    break;
            }
        }

        private void TickIdleOrDormant(float dt)
        {
            if (_isDormant)
            {
                PlayerController player = PlayerController.Instance;
                if (HasLineOfSightToPlayer || Awareness >= config.alertThreshold
                    || (player != null && !player.IsDead && Vector3.Distance(transform.position, player.transform.position) <= dormantWakeRadius && !player.IsConcealed))
                {
                    _isDormant = false;
                    TransitionState(Awareness >= config.huntThreshold ? DinoState.Hunt : DinoState.Alert);
                }
                return;
            }

            if (HasLineOfSightToPlayer && Awareness >= config.alertThreshold)
            {
                TransitionState(Awareness >= config.huntThreshold ? DinoState.Hunt : DinoState.Alert);
            }
            else if (PatrolWaypoints.Count > 0 && StateTimer > 1.0f)
            {
                TransitionState(DinoState.Patrol);
            }
        }

        private void TickPatrol(float dt)
        {
            if (HasLineOfSightToPlayer && Awareness >= config.huntThreshold)
            {
                TransitionState(DinoState.Hunt);
                return;
            }
            if (HasLineOfSightToPlayer && Awareness >= config.alertThreshold)
            {
                TransitionState(DinoState.Alert);
                return;
            }

            if (PatrolWaypoints.Count == 0)
            {
                TransitionState(DinoState.Idle);
                return;
            }

            if (_waitingAtWaypoint)
            {
                _waypointPauseTimer -= dt;
                // Subtle curious head sweep while paused
                float sweepAngle = Mathf.Sin(StateTimer * 1.4f) * 40f;
                Vector3 lookPt = EyePosition + (Quaternion.Euler(0f, sweepAngle, 0f) * transform.forward) * 6f;
                proceduralRig?.SetLookTarget(lookPt, 0.7f);

                if (_waypointPauseTimer <= 0f)
                {
                    _waitingAtWaypoint = false;
                    PatrolIndex = (PatrolIndex + 1) % PatrolWaypoints.Count;
                    MoveToDestination(PatrolWaypoints[PatrolIndex], GetPostureAdjustedSpeed(config.walkSpeed));
                }
                return;
            }

            SteerAlongAgentVelocity(dt);
            TryDetectAndStartDoorBash(dt, onlyDuringChase: false);

            if (HasReachedDestination(0.55f))
            {
                _waitingAtWaypoint = true;
                _waypointPauseTimer = UnityEngine.Random.Range(waypointWaitMin, waypointWaitMax);
                StopNavigation();
                if (UnityEngine.Random.value < 0.38f)
                {
                    PlayActionClip(config.inspectClipName, 1.8f);
                    EmitVocalization(config.sniffSoundId, 0.25f, 0.9f);
                }
            }
        }

        private void TickInvestigate(float dt)
        {
            if (HasLineOfSightToPlayer && Awareness >= config.huntThreshold)
            {
                TransitionState(DinoState.Hunt);
                return;
            }
            if (HasLineOfSightToPlayer && Awareness >= config.alertThreshold)
            {
                TransitionState(DinoState.Alert);
                return;
            }

            proceduralRig?.SetLookTarget(LastNoisePosition + Vector3.up * 1.0f, 0.9f);

            if (!_didInvestigateSniff)
            {
                MoveToDestination(LastNoisePosition, config.stalkSpeed);
                SteerAlongAgentVelocity(dt);
                TryDetectAndStartDoorBash(dt, onlyDuringChase: false);

                if (HasReachedDestination(0.85f) || StateTimer > 9.0f)
                {
                    _didInvestigateSniff = true;
                    StateTimer = 0f;
                    StopNavigation();
                    PlayActionClip(config.inspectClipName, 2.2f);
                    EmitVocalization(config.sniffSoundId, 0.28f, 1.1f);
                }
            }
            else
            {
                float scanYaw = Mathf.Sin(StateTimer * 2.1f) * 52f;
                Vector3 scanTarget = EyePosition + (Quaternion.Euler(0f, scanYaw, 0f) * transform.forward) * 7f;
                proceduralRig?.SetLookTarget(scanTarget, 0.85f);

                if (StateTimer >= 3.4f)
                {
                    TransitionState(PatrolWaypoints.Count > 0 ? DinoState.Patrol : DinoState.Idle);
                }
            }
        }

        private void TickAlertStalk(float dt)
        {
            PlayerController player = PlayerController.Instance;
            Vector3 focusPos = HasLineOfSightToPlayer && player != null ? player.EyePosition : LastSeenPosition + Vector3.up * 1.1f;

            proceduralRig?.SetLookTarget(focusPos, 1.0f);
            RotateTowardsPoint(focusPos, dt, 1.25f);

            if (HasLineOfSightToPlayer)
            {
                // Menacing low stalk closing distance toward player before full sprint escalation
                if (LastDistanceToPlayer > stalkDistanceThreshold * 0.45f)
                {
                    MoveToDestination(player.transform.position, config.stalkSpeed);
                }
                else
                {
                    StopNavigation();
                }

                if (Awareness >= config.huntThreshold || StateTimer >= config.alertHoldSeconds || LastDistanceToPlayer < 4.5f)
                {
                    TransitionState(DinoState.Hunt);
                    return;
                }
            }
            else if (StateTimer >= config.alertHoldSeconds + 0.6f)
            {
                LastNoisePosition = LastSeenPosition;
                TransitionState(DinoState.Investigate);
            }
        }

        private void TickHunt(float dt)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || player.IsDead)
            {
                TransitionState(DinoState.Patrol);
                return;
            }

            Vector3 targetPos = HasLineOfSightToPlayer ? player.transform.position : LastSeenPosition;
            proceduralRig?.SetLookTarget(HasLineOfSightToPlayer ? player.EyePosition : targetPos + Vector3.up * 1.1f, 1.0f);

            // Check attack range ONLY when close with clear physical path (never lunge from 20 m or through closed doors!)
            float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
            if (_attackCooldownTimer <= 0f
                && ActionLockTimer <= 0f
                && distToPlayer <= config.attackRangeMeters
                && !Physics.Linecast(EyePosition, player.EyePosition, DinoLayerMasks.DinoCollisionMask, QueryTriggerInteraction.Ignore))
            {
                TransitionState(DinoState.Attack);
                return;
            }

            if (ActionLockTimer <= 0.15f)
            {
                float chaseSpeed = config.runSpeed * Mathf.Clamp(ChaseRubberBand, 0.75f, 1.25f);
                MoveToDestination(targetPos, chaseSpeed);
                SteerAlongAgentVelocity(dt, 1.45f);
            }
            else
            {
                RotateTowardsPoint(targetPos, dt, 1.4f);
            }

            // Detect closed door blocking chase path -> bash through!
            if (TryDetectAndStartDoorBash(dt, onlyDuringChase: true))
            {
                return;
            }

            if (!HasLineOfSightToPlayer && UnseenTimer >= config.loseSightSeconds)
            {
                TransitionState(DinoState.Search);
            }
        }

        private void TickAttackLunge(float dt)
        {
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                RotateTowardsPoint(player.transform.position, dt, 2.0f);
                proceduralRig?.SetLookTarget(player.EyePosition, 0.6f);

                // Controlled forward lunge burst during strike window before hit frame
                if (StateTimer < config.attackHitDelaySeconds)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    if (dist > 1.2f)
                    {
                        Vector3 dir = (player.transform.position - transform.position);
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.01f)
                        {
                            Vector3 next = transform.position + dir.normalized * (lungeForwardSpeed * dt);
                            if (!Physics.Linecast(EyePosition, next + Vector3.up * 0.9f, DinoLayerMasks.DinoCollisionMask, QueryTriggerInteraction.Ignore))
                            {
                                next.y = ResolveGroundY(next.x, next.z, next.y);
                                transform.position = next;
                            }
                        }
                    }
                }
            }

            if (StateTimer >= 0.95f && ActionLockTimer <= 0f)
            {
                RestoreNavMeshControl();
                if (player != null && !player.IsDead)
                {
                    TransitionState(DinoState.Hunt);
                }
                else
                {
                    TransitionState(DinoState.Patrol);
                }
            }
        }

        protected override void OnBiteHitFrameInternal()
        {
            if (CurrentState != DinoState.Attack) return;
            PlayerController player = PlayerController.Instance;
            if (CanKillPlayer(player))
            {
                player.KillPlayer(transform.position);
                EmitVocalization(config.roarSoundId, 0.95f, 1.1f, 0.55f, 15f, ignoreCooldown: true);
            }
        }

        private void TickSearch(float dt)
        {
            if (HasLineOfSightToPlayer && Awareness >= config.huntThreshold)
            {
                TransitionState(DinoState.Hunt);
                return;
            }
            if (HasLineOfSightToPlayer && Awareness >= config.alertThreshold)
            {
                TransitionState(DinoState.Alert);
                return;
            }

            _searchStepTimer += dt;
            SteerAlongAgentVelocity(dt);

            if (HasReachedDestination(0.75f) || _searchStepTimer > 3.2f)
            {
                _searchStepTimer = 0f;
                _searchStepIndex++;
                if (_searchStepIndex == 1 || _searchStepIndex == 2)
                {
                    PlayActionClip(config.inspectClipName, 1.8f);
                    EmitVocalization(config.sniffSoundId, 0.28f, 1.0f);

                    Vector2 rnd = UnityEngine.Random.insideUnitCircle * searchRadiusMeters;
                    Vector3 probe = LastSeenPosition + new Vector3(rnd.x, 0f, rnd.y);
                    MoveToDestination(probe, config.stalkSpeed);
                }
            }

            if (StateTimer >= config.searchDurationSeconds)
            {
                Awareness = 0f;
                TransitionState(PatrolWaypoints.Count > 0 ? DinoState.Patrol : DinoState.Idle);
            }
        }

        // =========================================================================
        // 4-HIT RAPTOR DOOR BASHING
        // =========================================================================

        private bool TryDetectAndStartDoorBash(float dt, bool onlyDuringChase)
        {
            if (!config.canBashDoors || _doorBashTimer > 0f) return false;

            // Track navigation stall so we immediately detect closed doors blocking the NavMesh path
            if (Vector3.Distance(transform.position, _lastProgressCheckPos) < 0.25f)
            {
                _stallTimer += dt;
            }
            else
            {
                _lastProgressCheckPos = transform.position;
                _stallTimer = 0f;
            }

            bool stalled = _stallTimer > 0.35f;
            if (!onlyDuringChase && !stalled) return false;

            IDoor blockingDoor = FindBlockingClosedDoor();
            if (blockingDoor == null) return false;

            _activeBashDoor = blockingDoor;
            _isBashingDoor = true;
            _lockedDoorBashCount = 0;
            _stallTimer = 0f;
            StopNavigation();
            return true;
        }

        private IDoor FindBlockingClosedDoor()
        {
            // 1. Fast forward spherecast on Door layer
            Vector3 probeOrigin = transform.position + Vector3.up * 1.0f;
            if (Physics.SphereCast(probeOrigin, 0.45f, transform.forward, out RaycastHit hit, config.doorBashRange,
                GameLayers.Mask(GameLayers.Door), QueryTriggerInteraction.Ignore))
            {
                IDoor door = hit.collider.GetComponentInParent<IDoor>();
                if (door != null && !door.IsOpen && !door.IsBroken)
                {
                    return door;
                }
            }

            // 2. Fallback proximity check against WorldRegistry doors along heading
            if (WorldRegistry.Instance == null) return null;
            IDoor closest = null;
            float bestDist = config.doorBashRange + 0.35f;
            foreach (IDoor door in WorldRegistry.Instance.GetAllDoors())
            {
                if (door == null || door.IsOpen || door.IsBroken || door.Transform == null) continue;
                Vector3 toDoor = door.Transform.position - transform.position;
                toDoor.y = 0f;
                float d = toDoor.magnitude;
                if (d < bestDist && Vector3.Dot(transform.forward, toDoor.normalized) > 0.25f)
                {
                    bestDist = d;
                    closest = door;
                }
            }
            return closest;
        }

        private void TickDoorBashing(float dt)
        {
            if (_activeBashDoor == null || _activeBashDoor.IsOpen || _activeBashDoor.IsBroken)
            {
                FinishDoorBashing();
                return;
            }

            Vector3 doorPos = _activeBashDoor.Transform.position;
            RotateTowardsPoint(doorPos, dt, 1.8f);
            proceduralRig?.SetLookTarget(doorPos + Vector3.up * 1.1f, 0.85f);

            if (_doorBashTimer > 0f || ActionLockTimer > 0f) return;

            // Slam door with Bite_Lunge ram!
            _doorBashTimer = config.doorBashCooldown;
            PlayActionClip(config.bashClipName, 0.85f);
            EmitVocalization(config.roarSoundId, 0.85f, 0.65f, 0.16f, 14f, ignoreCooldown: true);

            bool brokenOpen = _activeBashDoor.BashDoor(doorPos + Vector3.up * 1.0f);
            if (brokenOpen)
            {
                FinishDoorBashing();
                return;
            }

            // If door is permanently locked (e.g. maglock), rattle dramatically twice then peel off cleanly
            if (_activeBashDoor.IsLocked)
            {
                _lockedDoorBashCount++;
                if (_lockedDoorBashCount >= maxLockedDoorBashAttempts)
                {
                    FinishDoorBashing();
                    if (CurrentState == DinoState.Hunt)
                    {
                        TransitionState(DinoState.Search);
                    }
                    else
                    {
                        TransitionState(PatrolWaypoints.Count > 0 ? DinoState.Patrol : DinoState.Idle);
                    }
                }
            }
        }

        private void FinishDoorBashing()
        {
            _isBashingDoor = false;
            _activeBashDoor = null;
            _lockedDoorBashCount = 0;
            RestoreNavMeshControl();
        }

        // =========================================================================
        // HELPERS (DOORWAY CLEARANCE POSTURE, STEERING, BREATHING)
        // =========================================================================

        private void SteerAlongAgentVelocity(float dt, float turnMult = 1.0f)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.velocity.sqrMagnitude > 0.05f)
                {
                    RotateTowardsPoint(transform.position + agent.velocity, dt, turnMult);
                }
                else if (agent.hasPath)
                {
                    RotateTowardsPoint(agent.steeringTarget, dt, turnMult);
                }
            }
        }

        private float GetPostureAdjustedSpeed(float baseSpeed)
        {
            // Slightly lower indoor speed through tight doorways for natural stealth pacing
            bool underLowCeiling = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.up, 2.65f,
                DinoLayerMasks.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore);
            return underLowCeiling ? Mathf.Min(baseSpeed, config.walkSpeed) : baseSpeed;
        }

        private void UpdateLowDoorwayPosture()
        {
            bool underLowCeiling = Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.up, 2.65f,
                DinoLayerMasks.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore);
            if (underLowCeiling && CurrentState != DinoState.Hunt && CurrentState != DinoState.Attack)
            {
                SetAnimBool(HashIsStalking, true);
            }
        }

        private void UpdateCloseBreathingCue(float dt)
        {
            if (string.IsNullOrEmpty(config.breathSoundId)) return;
            if (LastDistanceToPlayer > 11f || CurrentState == DinoState.Hunt || CurrentState == DinoState.Attack) return;

            _breathTimer -= dt;
            if (_breathTimer <= 0f)
            {
                _breathTimer = UnityEngine.Random.Range(3.2f, 5.5f);
                GameEvents.RaisePlaySound(config.breathSoundId, EyePosition, 0.72f);
                proceduralRig?.TriggerJawPulse(0.20f, 0.25f, 0.35f, 0.45f);
            }
        }
    }
}
