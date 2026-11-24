using System;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;
using Cretacea.Player;

namespace Cretacea.AI
{
    /// <summary>
    /// AAA Tyrannosaurus rex apex predator state machine:
    /// - Heavy booming footstep trauma impulses & mud footprint decals
    /// - GPU foliage trampling driver (_TRexTramplePos)
    /// - 9-point Tyrant Valley patrol with dramatic Look_Scan sweeps
    /// - Substation 3 perimeter leash respecting solid DinoBlocker barriers (ZERO fence clipping!)
    /// - Dramatic Fence_Bash impact sequence
    /// - Substation -> Old Yard tension rubber-band sprint chase (terrifying close pursuit without cheap instant kills
    ///   unless the player stops, gets snagged, or turns around!)
    /// </summary>
    public sealed class TrexAI : DinoControllerBase
    {
        private static readonly int TrexTramplePosId = Shader.PropertyToID("_TRexTramplePos");

        [Header("T. rex Apex Tuning")]
        [SerializeField] private float substationOrbitRadius = 30.5f;
        [SerializeField] private float fenceClearanceProbeDistance = 7.4f;
        [SerializeField] private float fenceBashStandoffMeters = 7.4f;
        [SerializeField] private Vector2 substationFenceHalfExtents = new Vector2(18.0f, 17.0f);
        [SerializeField] private float scanPauseMin = 2.6f;
        [SerializeField] private float scanPauseMax = 4.8f;
        [SerializeField] private bool enableAutoRubberBand = true;
        [SerializeField] private float rubberBandMinDist = 13.0f;
        [SerializeField] private float rubberBandMaxDist = 30.0f;
        [SerializeField] private float rubberBandSlowMult = 0.72f;
        [SerializeField] private float rubberBandFastMult = 1.30f;
        [SerializeField] private float rubberBandStationaryMult = 1.12f;

        private const float HardMinFenceBashStandoff = 7.2f;

        private float _attackCooldownTimer;
        private float _waypointScanTimer;
        private bool _scanningAtWaypoint;
        private float _leashOrbitAngleDeg;
        private float _searchStepTimer;
        private float _distantRumbleTimer = 6.0f;
        private float _fenceBashLockTimer;
        private Action _pendingFenceBashCallback;

        private bool IsAliveAndActive => this != null && gameObject != null && gameObject.activeInHierarchy;

        protected override DinoSpeciesType DefaultSpeciesType => DinoSpeciesType.Tyrannosaurus;

        protected override void OnDisable()
        {
            _pendingFenceBashCallback = null;
            _fenceBashLockTimer = 0f;
            base.OnDisable();
        }

        protected override void Update()
        {
            if (!IsAliveAndActive) return;

            if (_fenceBashLockTimer > 0f)
            {
                _fenceBashLockTimer = Mathf.Max(0f, _fenceBashLockTimer - Time.deltaTime);
                EnforceFenceStandoff(null);
            }

            Shader.SetGlobalVector(TrexTramplePosId, new Vector4(transform.position.x, transform.position.y, transform.position.z, 3.2f));
            base.Update();
        }

        private void Start()
        {
            if (GetComponent<TrexMoonRim>() == null) gameObject.AddComponent<TrexMoonRim>();
            if (!IsAliveAndActive) return;
            if (AiEnabled && CurrentState == DinoState.Idle && PatrolWaypoints.Count > 0)
            {
                TransitionState(DinoState.Patrol);
            }
        }

        public override void HuntPlayer()
        {
            if (!IsAliveAndActive) return;
            base.HuntPlayer();
        }

        public override void PlayActionClip(string clipTrigger, float durationSec = 1.4f)
        {
            if (!IsAliveAndActive) return;

            if (!string.IsNullOrWhiteSpace(clipTrigger))
            {
                string lower = clipTrigger.Trim().ToLowerInvariant();
                if (lower.Contains("fence") || lower.Contains("bash"))
                {
                    StopNavigation();
                    EnforceFenceStandoff(null);
                    _fenceBashLockTimer = Mathf.Max(_fenceBashLockTimer, Mathf.Max(1.4f, durationSec));
                }
            }

            base.PlayActionClip(clipTrigger, durationSec);
        }

        /// <summary>
        /// Direct trigger for the Substation 3 dramatic Fence_Bash smash sequence.
        /// Enforces strict >= 7.2 m standoff so the 11.2 m T. rex's snout, jaws, and legs never clip the chainlink fence.
        /// </summary>
        public void TriggerSubstationFenceBash(Vector3 fenceTargetWorld, Action onImpact = null)
        {
            if (!IsAliveAndActive) return;

            _pendingFenceBashCallback = onImpact;
            StopNavigation();
            Vector3 flat = fenceTargetWorld - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }

            EnforceFenceStandoff(fenceTargetWorld);
            _fenceBashLockTimer = 1.65f;

            PlayActionClip(config.bashClipName, 1.65f);
            EmitVocalization(config.roarSoundId, 0.98f, 1.7f, config.roarTraumaMax, config.roarTraumaRadius,
                "[THE T. REX SMASHES THROUGH THE FENCE!]", ignoreCooldown: true);
        }

        protected override void OnBashImpactFrameInternal()
        {
            if (!IsAliveAndActive) return;
            EmitDistanceTrauma(0.68f, 55f, 1.15f);
            Action cb = _pendingFenceBashCallback;
            _pendingFenceBashCallback = null;
            cb?.Invoke();
        }

        // =========================================================================
        // STATE MACHINE ENTRY & TICK
        // =========================================================================

        protected override void OnStateEntered(DinoState previous, DinoState next)
        {
            if (!IsAliveAndActive) return;
            _scanningAtWaypoint = false;

            switch (next)
            {
                case DinoState.Idle:
                    StopNavigation();
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.10f, 0.25f);
                    break;

                case DinoState.Patrol:
                    RestoreNavMeshControl();
                    proceduralRig?.ClearLookTarget();
                    proceduralRig?.SetBreathingState(0.12f, 0.30f);
                    AssignNextPatrolOrLeashTarget();
                    break;

                case DinoState.Investigate:
                    RestoreNavMeshControl();
                    proceduralRig?.SetLookTarget(LastNoisePosition + Vector3.up * 1.4f, 0.9f);
                    proceduralRig?.SetBreathingState(0.16f, 0.42f);
                    if (previous == DinoState.Idle || previous == DinoState.Patrol)
                    {
                        EmitVocalization(config.alertSoundId, 0.55f, 1.2f, 0.18f, 38f);
                    }
                    MoveRespectingDinoBlocker(LastNoisePosition, config.walkSpeed);
                    break;

                case DinoState.Alert:
                    StopNavigation();
                    proceduralRig?.SetBreathingState(0.20f, 0.55f);
                    EmitVocalization(config.alertSoundId, 0.65f, 1.4f, 0.22f, 45f);
                    PlayActionClip(config.inspectClipName, 1.8f);
                    break;

                case DinoState.Hunt:
                    UnseenTimer = 0f;
                    RestoreNavMeshControl();
                    proceduralRig?.SetBreathingState(0.28f, 0.95f);
                    if (previous != DinoState.Attack)
                    {
                        PlayActionClip(config.roarClipName, 1.55f);
                        EmitVocalization(config.roarSoundId, 0.98f, 2.2f, config.roarTraumaMax, config.roarTraumaRadius,
                            "[A MASSIVE T. REX ROAR SHAKES THE GROUND!]");
                    }
                    break;

                case DinoState.Attack:
                    StopNavigation();
                    _attackCooldownTimer = config.attackCooldownSeconds;
                    PlayActionClip(config.biteClipName, 1.35f);
                    EmitVocalization(config.biteSoundId, 1.0f, 1.0f, 0.48f, 25f, ignoreCooldown: true);
                    break;

                case DinoState.Search:
                    RestoreNavMeshControl();
                    _searchStepTimer = 0f;
                    proceduralRig?.SetBreathingState(0.18f, 0.55f);
                    MoveRespectingDinoBlocker(LastSeenPosition, config.walkSpeed);
                    break;
            }
        }

        protected override void TickAI(float dt)
        {
            if (!IsAliveAndActive) return;
            if (_attackCooldownTimer > 0f) _attackCooldownTimer -= dt;

            if (IsSubstationBlockerActive())
            {
                EnforceFenceStandoff(null);
            }

            UpdateAmbientValleyRumble(dt);

            switch (CurrentState)
            {
                case DinoState.Idle:
                    if (HasLineOfSightToPlayer && Awareness >= config.alertThreshold)
                    {
                        TransitionState(Awareness >= config.huntThreshold ? DinoState.Hunt : DinoState.Alert);
                    }
                    else if ((PatrolWaypoints.Count > 0 || LeashCenter.HasValue) && StateTimer > 1.2f)
                    {
                        TransitionState(DinoState.Patrol);
                    }
                    break;

                case DinoState.Patrol:
                    TickPatrol(dt);
                    break;

                case DinoState.Investigate:
                    TickInvestigate(dt);
                    break;

                case DinoState.Alert:
                    TickAlert(dt);
                    break;

                case DinoState.Hunt:
                    TickHunt(dt);
                    break;

                case DinoState.Attack:
                    TickAttack(dt);
                    break;

                case DinoState.Search:
                    TickSearch(dt);
                    break;
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

            if (_scanningAtWaypoint)
            {
                _waypointScanTimer -= dt;
                float sweep = Mathf.Sin(StateTimer * 0.95f) * 46f;
                Vector3 lookPt = EyePosition + (Quaternion.Euler(0f, sweep, 0f) * transform.forward) * 16f;
                proceduralRig?.SetLookTarget(lookPt, 0.85f);

                if (_waypointScanTimer <= 0f)
                {
                    _scanningAtWaypoint = false;
                    AssignNextPatrolOrLeashTarget();
                }
                return;
            }

            SteerWithFenceProtection(dt, 1.0f);

            if (HasReachedDestination(1.4f))
            {
                _scanningAtWaypoint = true;
                _waypointScanTimer = UnityEngine.Random.Range(scanPauseMin, scanPauseMax);
                StopNavigation();
                PlayActionClip(config.inspectClipName, 2.4f);
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

            proceduralRig?.SetLookTarget(LastNoisePosition + Vector3.up * 1.4f, 0.95f);

            if (!IsBlockedAheadByDinoBlocker(LastNoisePosition))
            {
                MoveRespectingDinoBlocker(LastNoisePosition, config.walkSpeed * 1.1f);
                SteerWithFenceProtection(dt, 1.1f);
            }
            else
            {
                // Reached solid Substation perimeter fence: stop cleanly outside with ZERO fence clipping and scan!
                StopNavigation();
                RotateTowardsPoint(LastNoisePosition, dt, 1.0f);
            }

            if (HasReachedDestination(2.4f) || StateTimer > 10.0f)
            {
                StopNavigation();
                PlayActionClip(config.inspectClipName, 2.4f);
                if (StateTimer > 4.5f)
                {
                    TransitionState(DinoState.Patrol);
                }
            }
        }

        private void TickAlert(float dt)
        {
            PlayerController player = PlayerController.Instance;
            Vector3 lookPos = HasLineOfSightToPlayer && player != null ? player.EyePosition : LastSeenPosition + Vector3.up * 1.3f;
            proceduralRig?.SetLookTarget(lookPos, 1.0f);
            RotateTowardsPoint(lookPos, dt, 1.15f);

            if (HasLineOfSightToPlayer)
            {
                // Gives crouched-in-grass player a heart-pounding 2.2 s freeze window before charge!
                if (Awareness >= config.huntThreshold || StateTimer >= config.alertHoldSeconds)
                {
                    TransitionState(DinoState.Hunt);
                    return;
                }
            }
            else if (StateTimer >= config.alertHoldSeconds + 0.8f)
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
            proceduralRig?.SetLookTarget(HasLineOfSightToPlayer ? player.EyePosition : targetPos + Vector3.up * 1.3f, 1.0f);

            // Lethal Bite_Snap ONLY if within attackRange AND no DinoBlocker/wall stands in between!
            float distToPlayer = Vector3.Distance(transform.position, player.transform.position);
            if (_attackCooldownTimer <= 0f
                && ActionLockTimer <= 0f
                && distToPlayer <= config.attackRangeMeters
                && !Physics.Linecast(EyePosition, player.EyePosition, DinoLayerMasks.DinoCollisionMask, QueryTriggerInteraction.Ignore))
            {
                TransitionState(DinoState.Attack);
                return;
            }

            // Compute rubber-banded chase speed (respects StoryDirector's ChaseRubberBand OR self-computes cleanly)
            float rubberMult = ComputeEffectiveChaseRubberBand(player, distToPlayer);
            float desiredSpeed = config.runSpeed * rubberMult;

            if (ActionLockTimer <= 0.25f)
            {
                if (IsBlockedAheadByDinoBlocker(targetPos))
                {
                    // Never clip snout or legs through the Substation perimeter fence or Old Yard squeeze wall!
                    StopNavigation();
                    RotateTowardsPoint(targetPos, dt, 1.25f);
                }
                else
                {
                    MoveRespectingDinoBlocker(targetPos, desiredSpeed);
                    SteerWithFenceProtection(dt, 1.35f);
                }
            }
            else
            {
                RotateTowardsPoint(targetPos, dt, 1.25f);
            }

            if (!HasLineOfSightToPlayer && UnseenTimer >= config.loseSightSeconds)
            {
                TransitionState(DinoState.Search);
            }
        }

        private void TickAttack(float dt)
        {
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                RotateTowardsPoint(player.transform.position, dt, 1.75f);
                proceduralRig?.SetLookTarget(player.EyePosition, 0.55f);
            }

            if (StateTimer >= 1.35f && ActionLockTimer <= 0f)
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
            if (!IsAliveAndActive) return;
            if (CurrentState != DinoState.Attack) return;
            PlayerController player = PlayerController.Instance;
            if (CanKillPlayer(player))
            {
                player.KillPlayer(transform.position);
                EmitVocalization(config.roarSoundId, 0.98f, 1.8f, 0.85f, 40f, ignoreCooldown: true);
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
            SteerWithFenceProtection(dt, 1.0f);

            if (HasReachedDestination(2.2f) || _searchStepTimer > 4.2f)
            {
                _searchStepTimer = 0f;
                PlayActionClip(config.inspectClipName, 2.4f);
                EmitVocalization(config.alertSoundId, 0.45f, 1.2f, 0.16f, 35f);

                Vector2 rnd = UnityEngine.Random.insideUnitCircle * 14f;
                Vector3 probe = LastSeenPosition + new Vector3(rnd.x, 0f, rnd.y);
                probe.y = ResolveGroundY(probe.x, probe.z, probe.y);
                MoveRespectingDinoBlocker(probe, config.walkSpeed);
            }

            if (StateTimer >= config.searchDurationSeconds)
            {
                Awareness = 0f;
                TransitionState(DinoState.Patrol);
            }
        }

        // =========================================================================
        // SUBSTATION PERIMETER LEASH, FENCE PROTECTION & RUBBER-BANDING
        // =========================================================================

        private float ComputeEffectiveChaseRubberBand(PlayerController player, float distToPlayer)
        {
            // StoryDirector explicitly updates ChaseRubberBand every frame during the scripted Substation->Yard chase.
            // If it's left at default 1.0f in a standalone chase, apply our balanced rubber-band formula directly!
            if (Mathf.Abs(ChaseRubberBand - 1.0f) > 0.005f)
            {
                return Mathf.Clamp(ChaseRubberBand, 0.65f, 1.38f);
            }

            if (!enableAutoRubberBand || player == null) return 1.0f;

            if (player.HorizontalSpeed > 3.0f)
            {
                float t = Mathf.InverseLerp(rubberBandMinDist, rubberBandMaxDist, distToPlayer);
                return Mathf.Lerp(rubberBandSlowMult, rubberBandFastMult, t);
            }

            return rubberBandStationaryMult;
        }

        private void AssignNextPatrolOrLeashTarget()
        {
            if (LeashCenter.HasValue)
            {
                // Prowl menacingly around the exterior perimeter of Substation 3 without ever touching the fence
                _leashOrbitAngleDeg = Mathf.Repeat(_leashOrbitAngleDeg + UnityEngine.Random.Range(45f, 85f), 360f);
                float rad = _leashOrbitAngleDeg * Mathf.Deg2Rad;
                Vector3 center = LeashCenter.Value;
                Vector3 orbitPos = center + new Vector3(Mathf.Cos(rad) * substationOrbitRadius, 0f, Mathf.Sin(rad) * substationOrbitRadius);
                orbitPos.y = ResolveGroundY(orbitPos.x, orbitPos.z, center.y);
                MoveRespectingDinoBlocker(orbitPos, config.walkSpeed);
                return;
            }

            if (PatrolWaypoints.Count > 0)
            {
                PatrolIndex = (PatrolIndex + 1) % PatrolWaypoints.Count;
                MoveRespectingDinoBlocker(PatrolWaypoints[PatrolIndex], config.walkSpeed);
            }
        }

        private bool IsBlockedAheadByDinoBlocker(Vector3 destinationWorld)
        {
            if (!IsAliveAndActive) return true;

            Vector3 toDest = destinationWorld - transform.position;
            toDest.y = 0f;
            float dist = toDest.magnitude;
            if (dist < 0.25f) return false;

            Vector3 dir = toDest / dist;
            float minStandoff = Mathf.Max(HardMinFenceBashStandoff, fenceBashStandoffMeters);
            float probeDist = Mathf.Min(dist, Mathf.Max(minStandoff, fenceClearanceProbeDistance));
            Vector3 origin = transform.position + Vector3.up * 1.8f;

            if (Physics.SphereCast(origin, config.agentRadius * 0.85f, dir, out _, probeDist,
                GameLayers.Mask(GameLayers.DinoBlocker), QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // Also verify destination does not drive T. rex inside the Substation perimeter standoff while sub_blocker is active
            if (IsSubstationBlockerActive() && TryGetSubstationFencePushback(transform.position + dir * Mathf.Min(dist, 2.0f), minStandoff, out _))
            {
                return true;
            }

            return false;
        }

        private void MoveRespectingDinoBlocker(Vector3 destinationWorld, float speed)
        {
            if (!IsAliveAndActive) return;

            float minStandoff = Mathf.Max(HardMinFenceBashStandoff, fenceBashStandoffMeters);

            // Clamp destination outside Substation fence envelope while perimeter blocker is active
            if (IsSubstationBlockerActive() && TryGetSubstationFencePushback(destinationWorld, minStandoff, out Vector3 pushedDest))
            {
                destinationWorld = pushedDest;
            }

            // Clamp target point cleanly outside any DinoBlocker between T. rex and target
            Vector3 origin = transform.position + Vector3.up * 1.8f;
            Vector3 toDest = destinationWorld - transform.position;
            toDest.y = 0f;
            float dist = toDest.magnitude;

            if (dist > 0.5f)
            {
                Vector3 dir = toDest / dist;
                if (Physics.SphereCast(origin, config.agentRadius * 0.8f, dir, out RaycastHit hit, dist,
                    GameLayers.Mask(GameLayers.DinoBlocker), QueryTriggerInteraction.Ignore))
                {
                    // DinoBlocker sits ~3.5 m outside the chainlink fence; keep >= 4.2 m outside the blocker
                    // (and >= minStandoff from the fence itself) so T. rex's snout and legs NEVER penetrate!
                    float blockerBuffer = Mathf.Max(4.2f, minStandoff - 3.2f);
                    Vector3 safeStop = hit.point - dir * blockerBuffer;
                    if (TryGetSubstationFencePushback(safeStop, minStandoff, out Vector3 fenceSafe))
                    {
                        safeStop = fenceSafe;
                    }
                    safeStop.y = ResolveGroundY(safeStop.x, safeStop.z, transform.position.y);
                    MoveToDestination(safeStop, speed);
                    return;
                }
            }

            MoveToDestination(destinationWorld, speed);
        }

        /// <summary>
        /// Enforces strict >= 7.2 m standoff from Substation 3's chainlink perimeter fence and forward DinoBlockers
        /// during Fence_Bash and while the perimeter barrier is intact, eliminating snout/jaw/leg penetration (P1-12).
        /// </summary>
        private void EnforceFenceStandoff(Vector3? explicitFencePoint)
        {
            if (!IsAliveAndActive) return;

            float minStandoff = Mathf.Max(HardMinFenceBashStandoff, fenceBashStandoffMeters);
            Vector3 pos = transform.position;
            bool adjusted = false;

            // 1. Explicit fence bash target standoff check
            if (explicitFencePoint.HasValue)
            {
                Vector3 away = pos - explicitFencePoint.Value;
                away.y = 0f;
                float flatDist = away.magnitude;
                if (flatDist < minStandoff)
                {
                    Vector3 awayDir = flatDist > 0.05f ? away / flatDist : -transform.forward;
                    awayDir.y = 0f;
                    if (awayDir.sqrMagnitude < 0.001f) awayDir = Vector3.left;
                    awayDir.Normalize();
                    pos = new Vector3(
                        explicitFencePoint.Value.x + awayDir.x * minStandoff,
                        pos.y,
                        explicitFencePoint.Value.z + awayDir.z * minStandoff);
                    adjusted = true;
                }
            }

            // 2. Substation 3 perimeter fence envelope standoff check (West X=250, East X=286, North Z=+17, South Z=-17)
            if (TryGetSubstationFencePushback(pos, minStandoff, out Vector3 clampedSubPos))
            {
                pos = clampedSubPos;
                adjusted = true;
            }

            // 3. Forward fence / DinoBlocker SphereCast clamp (catches any angle facing the fence during Fence_Bash)
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
            {
                fwd.Normalize();
                Vector3 rayOrigin = pos + Vector3.up * 2.1f;
                int fenceMask = GameLayers.Mask(GameLayers.DinoBlocker)
                              | GameLayers.Mask(GameLayers.Environment)
                              | GameLayers.Mask(GameLayers.Door);

                if (Physics.SphereCast(rayOrigin, 0.75f, fwd, out RaycastHit hit, minStandoff, fenceMask, QueryTriggerInteraction.Ignore))
                {
                    bool isFenceOrBlocker = hit.collider != null && (
                        hit.collider.gameObject.layer == GameLayers.DinoBlocker
                        || hit.collider.name.IndexOf("SubFence", StringComparison.OrdinalIgnoreCase) >= 0
                        || hit.collider.name.IndexOf("Fence", StringComparison.OrdinalIgnoreCase) >= 0
                        || hit.collider.name.IndexOf("DinoBlocker", StringComparison.OrdinalIgnoreCase) >= 0);

                    if (isFenceOrBlocker)
                    {
                        float requiredDist = hit.collider.gameObject.layer == GameLayers.DinoBlocker
                            ? Mathf.Max(4.2f, minStandoff - 3.2f)
                            : minStandoff;
                        float pushBack = requiredDist - hit.distance;
                        if (pushBack > 0.05f)
                        {
                            pos -= fwd * pushBack;
                            adjusted = true;
                        }
                    }
                }
            }

            if (adjusted)
            {
                ApplySafePosition(pos);
            }
        }

        private bool IsSubstationBlockerActive()
        {
            WorldRegistry reg = WorldRegistry.Instance;
            if (reg == null) return false;
            GameObject blocker = reg.GetObject("sub_blocker");
            return blocker != null && blocker.activeInHierarchy;
        }

        private bool TryGetSubstationFencePushback(Vector3 candidatePos, float minStandoff, out Vector3 pushedPos)
        {
            pushedPos = candidatePos;
            Vector3 subCenter = new Vector3(268.0f, 2.3f, 0.0f);
            WorldRegistry reg = WorldRegistry.Instance;
            if (reg != null)
            {
                subCenter = reg.GetPoint("substation_center", subCenter);
            }

            float hx = Mathf.Max(8f, substationFenceHalfExtents.x);
            float hz = Mathf.Max(8f, substationFenceHalfExtents.y);

            float dx = candidatePos.x - subCenter.x;
            float dz = candidatePos.z - subCenter.z;

            // Ignore when T. rex is far away from Substation 3 (> 55 m from compound)
            if (Mathf.Abs(dx) > hx + 35f || Mathf.Abs(dz) > hz + 35f)
            {
                return false;
            }

            float clampedX = Mathf.Clamp(dx, -hx, hx);
            float clampedZ = Mathf.Clamp(dz, -hz, hz);
            float outsideDx = dx - clampedX;
            float outsideDz = dz - clampedZ;
            float distOutside = Mathf.Sqrt(outsideDx * outsideDx + outsideDz * outsideDz);

            // Already safely outside the fence by >= minStandoff
            if (distOutside >= minStandoff)
            {
                return false;
            }

            // Inside or too close to Substation fence boundary: push outward along nearest fence face normal
            Vector3 outDir;
            Vector3 boundaryPt;
            if (distOutside > 0.001f)
            {
                outDir = new Vector3(outsideDx / distOutside, 0f, outsideDz / distOutside);
                boundaryPt = new Vector3(subCenter.x + clampedX, candidatePos.y, subCenter.z + clampedZ);
            }
            else
            {
                // Candidate is inside the fence box (e.g., QA teleported near perimeter): push out nearest side
                float penX = hx - Mathf.Abs(dx);
                float penZ = hz - Mathf.Abs(dz);
                if (penX <= penZ)
                {
                    float signX = dx < 0f ? -1f : 1f;
                    outDir = new Vector3(signX, 0f, 0f);
                    boundaryPt = new Vector3(subCenter.x + signX * hx, candidatePos.y, subCenter.z + clampedZ);
                }
                else
                {
                    float signZ = dz < 0f ? -1f : 1f;
                    outDir = new Vector3(0f, 0f, signZ);
                    boundaryPt = new Vector3(subCenter.x + clampedX, candidatePos.y, subCenter.z + signZ * hz);
                }
            }

            pushedPos = boundaryPt + outDir * minStandoff;
            pushedPos.y = ResolveGroundY(pushedPos.x, pushedPos.z, candidatePos.y);
            return true;
        }

        private void ApplySafePosition(Vector3 targetPos)
        {
            if (!IsAliveAndActive) return;
            targetPos.y = ResolveGroundY(targetPos.x, targetPos.z, targetPos.y);
            float minStandoff = Mathf.Max(HardMinFenceBashStandoff, fenceBashStandoffMeters);

            if (agent != null && agent.enabled)
            {
                if (NavMesh.SamplePosition(targetPos, out NavMeshHit navHit, 2.2f, NavMesh.AllAreas)
                    && !TryGetSubstationFencePushback(navHit.position, minStandoff - 0.1f, out _))
                {
                    agent.Warp(navHit.position);
                    return;
                }
            }

            transform.position = targetPos;
        }

        private void SteerWithFenceProtection(float dt, float turnMult)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (agent.velocity.sqrMagnitude > 0.08f)
                {
                    RotateTowardsPoint(transform.position + agent.velocity, dt, turnMult);
                }
                else if (agent.hasPath)
                {
                    RotateTowardsPoint(agent.steeringTarget, dt, turnMult);
                }
            }
        }

        private void UpdateAmbientValleyRumble(float dt)
        {
            if (CurrentState == DinoState.Hunt || CurrentState == DinoState.Attack) return;
            _distantRumbleTimer -= dt;
            if (_distantRumbleTimer <= 0f)
            {
                _distantRumbleTimer = UnityEngine.Random.Range(9.0f, 16.0f);
                if (LastDistanceToPlayer < 85f)
                {
                    GameEvents.RaisePlaySound("trex_breath", EyePosition, 0.75f);
                    proceduralRig?.TriggerJawPulse(0.26f, 0.35f, 0.65f, 0.65f);
                }
            }
        }
    }
}
