using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;
using Cretacea.Player;

namespace Cretacea.AI
{
    /// <summary>
    /// Shared AAA dinosaur foundation implementing IDinoController:
    /// - NavMeshAgent locomotion with velocity-to-stride-speed Animator scaling (zero foot skating)
    /// - Smooth Turn-In-Place detection & blending (>42 deg at low speed)
    /// - Dual-mode (Animation Event + stride-phase fallback) footstep, footprint & camera-trauma emitter
    /// - Multi-point line-of-sight vision + PlayerController stealth/flashlight-sweep awareness
    /// - Wall-occluded sound hearing listener with AUD-5 state-transition bugfix
    /// - Hybrid NavMesh / grounded-direct scripted cutscene steering
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class DinoControllerBase : MonoBehaviour, IDinoController
    {
        // Standardized Animator parameter hashes
        protected static readonly int HashSpeed = Animator.StringToHash("Speed");
        protected static readonly int HashLocomotionSpeedMult = Animator.StringToHash("LocomotionSpeedMult");
        protected static readonly int HashTurnAngle = Animator.StringToHash("TurnAngle");
        protected static readonly int HashIsTurning = Animator.StringToHash("IsTurning");
        protected static readonly int HashIsAlert = Animator.StringToHash("IsAlert");
        protected static readonly int HashIsHunting = Animator.StringToHash("IsHunting");
        protected static readonly int HashIsStalking = Animator.StringToHash("IsStalking");
        protected static readonly int HashTriggerRoar = Animator.StringToHash("TriggerRoar");
        protected static readonly int HashTriggerBite = Animator.StringToHash("TriggerBite");
        protected static readonly int HashTriggerInspect = Animator.StringToHash("TriggerInspect");
        protected static readonly int HashTriggerBash = Animator.StringToHash("TriggerBash");
        protected static readonly int HashTurnLeft = Animator.StringToHash("TurnLeft");
        protected static readonly int HashTurnRight = Animator.StringToHash("TurnRight");

        [Header("Species Config & Core Components")]
        [SerializeField] protected DinoConfig config;
        [SerializeField] protected NavMeshAgent agent;
        [SerializeField] protected Animator animator;
        [SerializeField] protected DinoProceduralRig proceduralRig;
        [SerializeField] protected AudioSource audioSource;

        [Header("Runtime Identity")]
        [SerializeField] private string dinoId;
        [SerializeField] private bool aiEnabled = true;

        // Cached Animator parameter table so missing optional parameters never log Console warnings
        private readonly HashSet<int> _validAnimParams = new HashSet<int>();
        protected readonly List<Vector3> PatrolWaypoints = new List<Vector3>();
        protected int PatrolIndex;

        // State & perception
        private DinoState _currentState = DinoState.Idle;
        protected float StateTimer;
        protected float ActionLockTimer;
        protected float LastVocalTime = -10f;
        protected float LastTurnTriggerTime = -10f;
        protected bool HasLineOfSightToPlayer;
        protected float LastDistanceToPlayer = float.MaxValue;
        protected float CachedEventVisibility = 1.0f;
        protected bool CachedEventConcealed;

        // Velocity & stride tracking
        private Vector3 _prevPosition;
        private float _smoothedHorizSpeed;
        private float _fallbackStepDistAccum;
        private float _lastAnimEventStepTime = -10f;
        private int _fallbackFootCycle;
        private bool _usingDirectScriptMove;
        private float _directMoveSpeed;
        private float _agentBaseSpeed = -1f;
        private float _prevYaw;
        private float _yawRateSmoothed;

        // Scripted sequence state
        private bool _hasScriptMoveTarget;
        private Vector3 _scriptMoveTarget;
        private float _scriptMoveSpeed = 2.0f;
        private Action _scriptOnArrived;
        private bool _hasScriptLookTarget;
        private Vector3 _scriptLookTarget;

        // Pending hit/impact timers for fallback when FBX Animation Event is not yet embedded
        protected bool PendingBiteHit;
        protected float PendingBiteHitTimer;
        protected bool PendingBashImpact;
        protected float PendingBashImpactTimer;

        public event Action<DinoControllerBase> OnDespawned;

        // ---- IDinoController Contract ----
        public string DinoId => string.IsNullOrEmpty(dinoId) ? gameObject.name : dinoId;
        public string Species => config != null ? config.speciesKey : DefaultSpeciesType.ToString().ToLowerInvariant();
        public DinoState CurrentState => _currentState;

        public bool AiEnabled
        {
            get => aiEnabled;
            set
            {
                aiEnabled = value;
                if (!aiEnabled && _currentState != DinoState.Scripted && _currentState != DinoState.Idle)
                {
                    TransitionState(DinoState.Idle);
                }
            }
        }

        public float Aggression { get; set; } = 1.0f;
        public float Awareness { get; set; }
        public float ChaseRubberBand { get; set; } = 1.0f;
        public Vector3 LastSeenPosition { get; set; }
        public Vector3 LastNoisePosition { get; set; }
        public float UnseenTimer { get; set; }
        public Vector3? LeashCenter { get; set; }
        public Transform Transform => transform;

        public DinoConfig Config => config;
        public float CurrentHorizontalSpeed => _smoothedHorizSpeed;
        public Vector3 EyePosition => proceduralRig != null
            ? proceduralRig.GetEyeWorldPosition(config != null ? config.eyeHeight : 1.5f)
            : transform.position + Vector3.up * (config != null ? config.eyeHeight : 1.5f);

        protected abstract DinoSpeciesType DefaultSpeciesType { get; }

        // =========================================================================
        // LIFECYCLE & INITIALIZATION
        // =========================================================================

        protected virtual void Awake()
        {
            if (config == null)
            {
                config = DinoConfig.CreateDefault(DefaultSpeciesType);
            }

            if (agent == null) agent = GetComponent<NavMeshAgent>();
            if (animator == null) animator = GetComponentInChildren<Animator>(true);
            if (proceduralRig == null) proceduralRig = GetComponent<DinoProceduralRig>();
            if (proceduralRig == null) proceduralRig = gameObject.AddComponent<DinoProceduralRig>();
            if (audioSource == null) audioSource = GetComponent<AudioSource>();

            // Bind Animation Event relay if Animator lives on a child object
            if (animator != null && animator.gameObject != gameObject)
            {
                DinoAnimationEventRelay relay = animator.gameObject.GetComponent<DinoAnimationEventRelay>();
                if (relay == null) relay = animator.gameObject.AddComponent<DinoAnimationEventRelay>();
                relay.Bind(this);
            }

            // Ensure SkinnedMeshRenderers never falsely frustum-cull during dramatic lunges/roars
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].updateWhenOffscreen = true;
            }

            gameObject.layer = GameLayers.Dinosaur;
            ConfigureNavMeshAgent();
            CacheAnimatorParameters();
            proceduralRig.Initialize(config, audioSource);

            _prevPosition = transform.position;
            _prevYaw = transform.eulerAngles.y; _yawRateSmoothed = 0f;
        }

        protected virtual void OnEnable()
        {
            GameEvents.OnNoiseEmitted += HandleNoiseEmitted;
            GameEvents.OnStealthVisibilityChanged += HandleStealthVisibilityChanged;
            _prevPosition = transform.position;
        }

        protected virtual void OnDisable()
        {
            GameEvents.OnNoiseEmitted -= HandleNoiseEmitted;
            GameEvents.OnStealthVisibilityChanged -= HandleStealthVisibilityChanged;
        }

        public void InitializeInstance(string customId, DinoConfig overrideConfig = null)
        {
            if (!string.IsNullOrWhiteSpace(customId))
            {
                dinoId = customId;
                gameObject.name = customId;
            }
            if (overrideConfig != null)
            {
                config = overrideConfig;
                ConfigureNavMeshAgent();
                proceduralRig?.Initialize(config, audioSource, forceReResolve: true);
            }
        }

        private void ConfigureNavMeshAgent()
        {
            if (agent == null || config == null) return;

            int resolvedTypeId = ResolveNavMeshAgentTypeId(config.navMeshAgentTypeName);
            if (resolvedTypeId >= 0)
            {
                agent.agentTypeID = resolvedTypeId;
            }

            agent.radius = config.agentRadius;
            agent.height = config.agentHeight;
            agent.speed = config.walkSpeed;
            agent.angularSpeed = config.turnSpeedDegPerSec;
            agent.acceleration = config.acceleration;
            agent.stoppingDistance = config.stoppingDistance;
            agent.autoBraking = true;
            // Manual smooth rotation allows authentic Turn-In-Place and head-first aiming
            agent.updateRotation = false;
            agent.updatePosition = true;
        }

        public static int ResolveNavMeshAgentTypeId(string agentTypeName)
        {
            if (string.IsNullOrWhiteSpace(agentTypeName)) return 0;
            int count = NavMesh.GetSettingsCount();
            for (int i = 0; i < count; i++)
            {
                NavMeshBuildSettings settings = NavMesh.GetSettingsByIndex(i);
                string name = NavMesh.GetSettingsNameFromID(settings.agentTypeID);
                if (string.Equals(name, agentTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return settings.agentTypeID;
                }
            }
            return count > 0 ? NavMesh.GetSettingsByIndex(0).agentTypeID : 0;
        }

        private void CacheAnimatorParameters()
        {
            _validAnimParams.Clear();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                _validAnimParams.Add(parameters[i].nameHash);
            }
        }

        // =========================================================================
        // MAIN UPDATE LOOP
        // =========================================================================

        protected virtual void Update()
        {
            GameState gs = GameState.Instance;
            if (gs != null && gs.IsPaused) return;

            float dt = Time.deltaTime;
            if (dt <= 0.00001f) return;

            StateTimer += dt;
            if (ActionLockTimer > 0f)
            {
                ActionLockTimer -= dt;
                if (ActionLockTimer <= 0f)
                {
                    proceduralRig?.SetLookSuppression(false);
                    proceduralRig?.SetGaitSuppression(false);
                }
            }
            UpdateDeferredAction(dt);

            UpdatePendingActionTimers(dt);

            if (_currentState == DinoState.Scripted)
            {
                UpdateScriptedBehavior(dt);
            }
            else if (aiEnabled)
            {
                UpdatePerception(dt);
                TickAI(dt);
            }
            else
            {
                StopNavigation();
                TickAmbientWhenAiDisabled(dt);
            }

            ApplyAgentHeadingLimit();
            UpdateLocomotionAndAnimator(dt);
            UpdateFallbackFootsteps(dt);

            _prevPosition = transform.position;
        }

        private void UpdatePendingActionTimers(float dt)
        {
            if (PendingBiteHit)
            {
                PendingBiteHitTimer -= dt;
                if (PendingBiteHitTimer <= 0f)
                {
                    PendingBiteHit = false;
                    OnBiteHitFrameInternal();
                }
            }

            if (PendingBashImpact)
            {
                PendingBashImpactTimer -= dt;
                if (PendingBashImpactTimer <= 0f)
                {
                    PendingBashImpact = false;
                    OnBashImpactFrameInternal();
                }
            }
        }

        protected virtual void TickAmbientWhenAiDisabled(float dt)
        {
            proceduralRig?.SetBreathingState(0.08f, 0.32f);
        }

        protected abstract void TickAI(float dt);
        protected virtual void OnStateEntered(DinoState previous, DinoState next) { }
        protected virtual void OnBiteHitFrameInternal() { }
        protected virtual void OnBashImpactFrameInternal() { }

        // =========================================================================
        // IDinoController IMPLEMENTATION
        // =========================================================================

        public void SetPatrolWaypoints(IReadOnlyList<Vector3> waypoints, int startIndex = 0)
        {
            PatrolWaypoints.Clear();
            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Count; i++)
                {
                    PatrolWaypoints.Add(waypoints[i]);
                }
            }

            PatrolIndex = PatrolWaypoints.Count > 0
                ? Mathf.Clamp(startIndex, 0, PatrolWaypoints.Count - 1)
                : 0;

            if (aiEnabled && (_currentState == DinoState.Idle || _currentState == DinoState.Patrol) && PatrolWaypoints.Count > 0)
            {
                TransitionState(DinoState.Patrol);
            }
        }

        public void ForceState(DinoState state)
        {
            if (state == DinoState.Hunt)
            {
                // AUD-5 protection: never enter Hunt with stale UnseenTimer
                UnseenTimer = 0f;
            }
            TransitionState(state);
        }

        public virtual void HuntPlayer()
        {
            PlayerController player = PlayerController.Instance;
            if (player != null)
            {
                LastSeenPosition = player.transform.position;
            }
            UnseenTimer = 0f;
            Awareness = Mathf.Max(Awareness, config.huntThreshold);
            aiEnabled = true;
            _hasScriptMoveTarget = false;
            _hasScriptLookTarget = false;
            RestoreNavMeshControl();
            TransitionState(DinoState.Hunt);
        }

        private bool _forceDirectScriptMove;

        /// <summary>
        /// Scripted move in a straight line over the ground, ignoring the NavMesh. For beats that must cross a
        /// particular spot in view: with a baked NavMesh the service-path raptor took the agent's path round the
        /// junction and never crossed the lane in front of the player (round 4).
        /// </summary>
        public void ScriptMoveDirect(Vector3 targetWorldPos, float moveSpeed, Action onArrived = null)
        {
            _forceDirectScriptMove = true;
            ScriptMoveTo(targetWorldPos, moveSpeed, onArrived);
            _forceDirectScriptMove = false;
        }

        public void ScriptMoveTo(Vector3 targetWorldPos, float moveSpeed, Action onArrived = null)
        {
            _scriptMoveTarget = targetWorldPos;
            _scriptMoveSpeed = Mathf.Max(0.4f, moveSpeed);
            _scriptOnArrived = onArrived;
            _hasScriptMoveTarget = true;
            _hasScriptLookTarget = false;
            _directMoveSpeed = _smoothedHorizSpeed;   // chained moves keep their pace; a standing start accelerates

            TransitionState(DinoState.Scripted);

            // Use NavMeshAgent if both current position and target sit cleanly on a connected NavMesh;
            // otherwise use grounded direct steering so exterior/cutscene beats never stall!
            _usingDirectScriptMove = true;
            if (!_forceDirectScriptMove && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                var testPath = new NavMeshPath();
                if (NavMesh.SamplePosition(targetWorldPos, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)
                    && agent.CalculatePath(hit.position, testPath)
                    && testPath.status == NavMeshPathStatus.PathComplete)
                {
                    _usingDirectScriptMove = false;
                    agent.isStopped = false;
                    agent.speed = _scriptMoveSpeed;
                    _agentBaseSpeed = _scriptMoveSpeed;
                    agent.SetPath(testPath);
                }
            }

            if (_usingDirectScriptMove && agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                agent.updatePosition = false;
            }
        }

        public void ScriptHoldAndLookAt(Vector3 worldLookTarget)
        {
            _hasScriptMoveTarget = false;
            _scriptOnArrived = null;
            _hasScriptLookTarget = true;
            _scriptLookTarget = worldLookTarget;

            StopNavigation();
            TransitionState(DinoState.Scripted);
            proceduralRig?.SetLookTarget(worldLookTarget, 1.0f);
        }

        // Fix round: a scripted hold-and-look plus an immediate roar spun a species without turn clips on its roar pose
        // (90 deg at 61 deg/s = 1.5 s of skating roar). The roar now waits until the animal faces the look target
        // within 20 deg, at most 2 s.
        private string _deferredActionTrigger;
        private float _deferredActionDuration;
        private float _deferredActionTimer;

        private bool ShouldDeferForFacing()
        {
            if (config == null || config.HasTurnInPlaceClips) return false;
            if (_currentState != DinoState.Scripted || !_hasScriptLookTarget || _hasScriptMoveTarget) return false;
            Vector3 to = _scriptLookTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude < 0.01f) return false;
            return Vector3.Angle(transform.forward, to) > 20f;
        }

        private void UpdateDeferredAction(float dt)
        {
            if (_deferredActionTrigger == null) return;
            _deferredActionTimer -= dt;
            if (_deferredActionTimer <= 0f || !ShouldDeferForFacing())
            {
                string trig = _deferredActionTrigger;
                _deferredActionTrigger = null;
                StartActionClip(trig, _deferredActionDuration);
            }
        }

        public virtual void PlayActionClip(string clipTrigger, float durationSec = 1.4f)
        {
            if (string.IsNullOrWhiteSpace(clipTrigger)) return;
            if (ShouldDeferForFacing())
            {
                _deferredActionTrigger = clipTrigger;
                _deferredActionDuration = durationSec;
                _deferredActionTimer = 2f;
                return;
            }
            _deferredActionTrigger = null;
            StartActionClip(clipTrigger, durationSec);
        }

        private void StartActionClip(string clipTrigger, float durationSec)
        {
            ActionLockTimer = Mathf.Max(ActionLockTimer, Mathf.Max(0.25f, durationSec));
            proceduralRig?.SetLookSuppression(true);
            proceduralRig?.SetGaitSuppression(true);

            string lower = clipTrigger.Trim().ToLowerInvariant();
            if (lower.Contains("roar") || lower.Contains("call") || lower.Contains("snort"))
            {
                TriggerAnimatorParam(HashTriggerRoar, config.roarClipName);
                proceduralRig?.TriggerJawPulse(0.95f, 0.14f, Mathf.Max(0.35f, durationSec * 0.55f), Mathf.Max(0.25f, durationSec * 0.30f));
                EmitDistanceTrauma(config.roarTraumaMax, config.roarTraumaRadius, 1.25f);
            }
            else if (lower.Contains("bash") || lower.Contains("fence"))
            {
                TriggerAnimatorParam(HashTriggerBash, config.bashClipName);
                proceduralRig?.TriggerJawPulse(0.82f, 0.10f, 0.45f, 0.30f);
                PendingBashImpact = true;
                PendingBashImpactTimer = config.TryGetClipSpec(config.bashClipName, out DinoClipSpec bashSpec) && bashSpec.hitOrImpactTimeSeconds > 0f
                    ? bashSpec.hitOrImpactTimeSeconds
                    : 0.75f;
            }
            else if (lower.Contains("bite") || lower.Contains("lunge") || lower.Contains("snap"))
            {
                TriggerAnimatorParam(HashTriggerBite, config.biteClipName);
                proceduralRig?.TriggerJawPulse(0.92f, 0.08f, 0.28f, 0.25f);
                PendingBiteHit = true;
                PendingBiteHitTimer = config.attackHitDelaySeconds;
            }
            else if (lower.Contains("sniff") || lower.Contains("inspect") || lower.Contains("scan") || lower.Contains("look"))
            {
                TriggerAnimatorParam(HashTriggerInspect, config.inspectClipName);
                proceduralRig?.TriggerJawPulse(0.22f, 0.15f, 0.30f, 0.30f);
            }
            else
            {
                // Direct fallback: try triggering by exact name or CrossFade directly onto state name
                if (animator != null)
                {
                    int hash = Animator.StringToHash(clipTrigger);
                    if (_validAnimParams.Contains(hash))
                    {
                        animator.SetTrigger(hash);
                    }
                    else
                    {
                        animator.CrossFadeInFixedTime(clipTrigger, 0.15f);
                    }
                }
            }
        }

        public void Teleport(Vector3 worldPos, float yawDegrees)
        {
            _hasScriptMoveTarget = false;
            _scriptOnArrived = null;
            _usingDirectScriptMove = false;

            Vector3 groundedPos = ResolveGroundedPosition(worldPos);
            Quaternion rot = Quaternion.Euler(0f, yawDegrees, 0f);

            if (agent != null && agent.enabled)
            {
                agent.updatePosition = true;
                if (NavMesh.SamplePosition(groundedPos, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                    transform.rotation = rot;
                }
                else
                {
                    bool wasEnabled = agent.enabled;
                    agent.enabled = false;
                    transform.SetPositionAndRotation(groundedPos, rot);
                    agent.enabled = wasEnabled;
                    if (agent.isOnNavMesh) agent.Warp(transform.position);
                }
            }
            else
            {
                transform.SetPositionAndRotation(groundedPos, rot);
            }

            _prevPosition = transform.position;
            _prevYaw = transform.eulerAngles.y; _yawRateSmoothed = 0f;
            _smoothedHorizSpeed = 0f;
            _directMoveSpeed = 0f;
            _fallbackStepDistAccum = 0f;
        }

        public void ReleaseToAI(DinoState resumeState = DinoState.Patrol)
        {
            _hasScriptMoveTarget = false;
            _hasScriptLookTarget = false;
            _scriptOnArrived = null;
            aiEnabled = true;
            RestoreNavMeshControl();
            proceduralRig?.ClearLookTarget();
            proceduralRig?.SetLookSuppression(false);
            TransitionState(resumeState);
        }

        public void Despawn()
        {
            _hasScriptMoveTarget = false;
            _scriptOnArrived = null;
            OnDespawned?.Invoke(this);
            Destroy(gameObject);
        }

        // =========================================================================
        // STATE TRANSITIONS & NAVIGATION HELPERS
        // =========================================================================

        protected void TransitionState(DinoState next)
        {
            if (_currentState == next) return;
            DinoState prev = _currentState;
            _currentState = next;
            StateTimer = 0f;

            if (next == DinoState.Hunt)
            {
                UnseenTimer = 0f;
            }

            OnStateEntered(prev, next);
            GameEvents.RaiseDinoStateChanged(DinoId, Species, next);
        }

        protected void RestoreNavMeshControl()
        {
            _usingDirectScriptMove = false;
            if (agent == null) return;

            if (!agent.enabled) agent.enabled = true;
            agent.updatePosition = true;
            if (!agent.isOnNavMesh && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
            }
        }

        protected bool MoveToDestination(Vector3 worldPos, float speed)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }

            agent.updatePosition = true;
            agent.isStopped = false;
            agent.speed = Mathf.Max(0.2f, speed);
            _agentBaseSpeed = agent.speed;

            if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, 4.5f, NavMesh.AllAreas))
            {
                return agent.SetDestination(hit.position);
            }
            return agent.SetDestination(worldPos);
        }

        protected void StopNavigation()
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
        }

        protected bool HasReachedDestination(float extraTolerance = 0.45f)
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return true;
            if (agent.pathPending) return false;
            return agent.remainingDistance <= agent.stoppingDistance + extraTolerance;
        }

        protected void RotateTowardsPoint(Vector3 worldPoint, float dt, float speedMultiplier = 1f)
        {
            Vector3 toTarget = worldPoint - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0025f) return;

            float signedAngle = Vector3.SignedAngle(transform.forward, toTarget.normalized, Vector3.up);
            UpdateTurnInPlace(signedAngle);

            float maxTurn = (config != null ? config.turnSpeedDegPerSec : 220f) * speedMultiplier * dt;
            // Species without turn-in-place clips (T. rex, herbivores) pivot on stepping feet: at a standstill they
            // turn at half rate so the planted feet can lock and step round instead of skating (round 5).
            if (config != null && !config.HasTurnInPlaceClips)
            {
                float walk = Mathf.Max(0.3f, config.walkSpeed);
                maxTurn *= Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(_smoothedHorizSpeed / walk));
            }
            Quaternion desired = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, maxTurn);
        }

        private void UpdateTurnInPlace(float signedAngleDeg)
        {
            SetAnimFloat(HashTurnAngle, signedAngleDeg);
            bool shouldTurnInPlace = _smoothedHorizSpeed <= config.turnInPlaceMaxSpeed
                                     && Mathf.Abs(signedAngleDeg) >= config.turnInPlaceAngleThresholdDeg
                                     && ActionLockTimer <= 0f;
            SetAnimBool(HashIsTurning, shouldTurnInPlace);

            if (shouldTurnInPlace && config.HasTurnInPlaceClips && Time.time - LastTurnTriggerTime > 0.75f)
            {
                LastTurnTriggerTime = Time.time;
                if (signedAngleDeg < 0f) TriggerAnimatorParam(HashTurnLeft, config.turnLeftClipName);
                else TriggerAnimatorParam(HashTurnRight, config.turnRightClipName);
            }
        }

        private void UpdateScriptedBehavior(float dt)
        {
            if (_hasScriptMoveTarget)
            {
                if (_usingDirectScriptMove)
                {
                    Vector3 flatToTarget = _scriptMoveTarget - transform.position;
                    flatToTarget.y = 0f;
                    float dist = flatToTarget.magnitude;

                    Vector3 dir = dist > 0.001f ? flatToTarget / dist : transform.forward;
                    float offAxis = Vector3.Angle(transform.forward, dir);
                    // Close to the mark but facing well off it: done (never orbit the point at the turn radius).
                    if (dist <= 0.45f || (dist <= 1.2f && offAxis > 60f))
                    {
                        CompleteScriptMove();
                    }
                    else
                    {
                        RotateTowardsPoint(_scriptMoveTarget, dt, 1.45f);
                        // Round 5: the body used to slide straight at the mark at full speed from the first frame,
                        // whatever way it faced (a T. rex turning round walked backwards for a second). Now it
                        // accelerates like the animal, slows while the mark is off its nose, and travels mostly
                        // along its own heading so turns are arcs, not crab walks.
                        float desired = _scriptMoveSpeed * HeadingSpeedFactor(offAxis);
                        float accel = config != null ? Mathf.Max(1.5f, config.acceleration) : 8f;
                        _directMoveSpeed = Mathf.MoveTowards(_directMoveSpeed, desired, accel * dt);
                        Vector3 fwd = transform.forward; fwd.y = 0f;
                        Vector3 travel = Vector3.Lerp(dir, fwd.sqrMagnitude > 0.001f ? fwd.normalized : dir, 0.8f);
                        travel = travel.sqrMagnitude > 0.0001f ? travel.normalized : dir;
                        float step = Mathf.Min(dist, _directMoveSpeed * dt);
                        Vector3 nextPos = transform.position + travel * step;
                        nextPos.y = ResolveGroundY(nextPos.x, nextPos.z, nextPos.y);
                        transform.position = nextPos;
                        proceduralRig?.SetLookTarget(_scriptMoveTarget + Vector3.up * config.eyeHeight, 0.75f);
                    }
                }
                else
                {
                    if (agent != null && agent.isOnNavMesh)
                    {
                        if (agent.velocity.sqrMagnitude > 0.04f)
                        {
                            RotateTowardsPoint(transform.position + agent.velocity, dt, 1.35f);
                        }
                        else
                        {
                            RotateTowardsPoint(_scriptMoveTarget, dt, 1.25f);
                        }

                        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(0.55f, agent.stoppingDistance))
                        {
                            CompleteScriptMove();
                        }
                    }
                    else
                    {
                        _usingDirectScriptMove = true;
                    }
                }
            }
            else if (_hasScriptLookTarget)
            {
                RotateTowardsPoint(_scriptLookTarget, dt, 0.9f);
                proceduralRig?.SetLookTarget(_scriptLookTarget, 1.0f);
            }
        }

        /// <summary>Share of the commanded speed allowed while the goal is this many degrees off the nose.</summary>
        protected static float HeadingSpeedFactor(float offAxisDeg)
        {
            if (offAxisDeg <= 25f) return 1f;
            if (offAxisDeg >= 120f) return 0.12f;
            float t = (offAxisDeg - 25f) / 95f;
            return Mathf.Lerp(1f, 0.12f, t * t * (3f - 2f * t));
        }

        /// <summary>
        /// The NavMeshAgent turns its velocity almost instantly while the body turns at the species' rate (a T. rex at
        /// 135 deg/s, a brachiosaur at 52): on every corner the animal crab-walked sideways. Cap the agent's speed by
        /// how far the path direction is off the body's nose, so heavy animals slow into corners and pivot.
        /// </summary>
        private void ApplyAgentHeadingLimit()
        {
            if (_usingDirectScriptMove || agent == null || !agent.enabled || !agent.isOnNavMesh || _agentBaseSpeed <= 0f) return;
            Vector3 want = agent.desiredVelocity; want.y = 0f;
            if (want.sqrMagnitude < 0.01f) { agent.speed = _agentBaseSpeed; return; }
            Vector3 fwd = transform.forward; fwd.y = 0f;
            float off = Vector3.Angle(fwd, want);
            float factor = HeadingSpeedFactor(off);
            // A chase must stay a threat: a sidestepping player would otherwise drop the T. rex to 12-33 % speed.
            if (_currentState == DinoState.Hunt || _currentState == DinoState.Attack)
                factor = Mathf.Max(0.5f, factor);
            agent.speed = Mathf.Max(0.2f, _agentBaseSpeed * factor);
        }

        private void CompleteScriptMove()
        {
            _hasScriptMoveTarget = false;
            Action callback = _scriptOnArrived;
            _scriptOnArrived = null;
            StopNavigation();
            callback?.Invoke();
        }

        // =========================================================================
        // PERCEPTION: MULTI-POINT LINE OF SIGHT, STEALTH VISIBILITY & HEARING (AUD-5)
        // =========================================================================

        protected virtual void UpdatePerception(float dt)
        {
            HasLineOfSightToPlayer = false;
            PlayerController player = PlayerController.Instance;
            GameState gs = GameState.Instance;

            if (player == null || player.IsDead || (gs != null && (gs.InCutscene || gs.IsMainMenu)))
            {
                Awareness = Mathf.Max(0f, Awareness - config.awarenessDecayRate * dt);
                return;
            }

            Vector3 eyePos = EyePosition;
            Vector3 playerEye = player.EyePosition;
            Vector3 toPlayer = playerEye - eyePos;
            float dist = toPlayer.magnitude;
            LastDistanceToPlayer = dist;

            float playerVis = player.Visibility;
            bool flashlightOn = player.Flashlight != null && player.Flashlight.IsBeamOn;

            // Vision check (distance + FOV cone + multi-point LOS respecting solid walls & closed doors)
            if (dist <= config.sightRangeMeters)
            {
                Vector3 dir = toPlayer / Mathf.Max(0.001f, dist);
                Vector3 bodyFwd = transform.forward;
                Vector3 headFwd = proceduralRig != null ? proceduralRig.GetHeadForwardWorld() : bodyFwd;
                Vector3 senseFwd = Vector3.Slerp(bodyFwd, headFwd, 0.55f).normalized;

                float halfFov = config.sightFovDegrees * 0.5f;
                float angle = Vector3.Angle(senseFwd, dir);

                // Close proximity peripheral sense (within 3.5 m, predator senses movement around itself)
                bool inCone = angle <= halfFov || (dist < 3.5f && playerVis > 0.35f);
                if (inCone && HasClearVisualLOS(eyePos, player))
                {
                    HasLineOfSightToPlayer = true;
                    LastSeenPosition = player.transform.position;
                    UnseenTimer = 0f;

                    float distFalloff = Mathf.Lerp(1.65f, 0.28f, Mathf.Clamp01(dist / config.sightRangeMeters));
                    float gain = config.awarenessGainRate * Mathf.Max(0.05f, playerVis) * distFalloff * Aggression;

                    // Direct flashlight sweep right into creature's face spikes awareness fast!
                    if (flashlightOn && dist <= 34f)
                    {
                        Transform camPivot = player.CameraPivot != null ? player.CameraPivot : player.transform;
                        float beamAngle = Vector3.Angle(camPivot.forward, (eyePos - playerEye).normalized);
                        if (beamAngle < 18f)
                        {
                            gain += 0.95f * (1f - beamAngle / 18f) * distFalloff;
                        }
                    }

                    Awareness = Mathf.Clamp(Awareness + gain * dt, 0f, 1.5f);
                    return;
                }
            }

            // Flashlight beam sweep hitting dinosaur body from outside FOV still triggers investigation!
            if (flashlightOn && dist <= 30f && HasClearVisualLOS(eyePos, player))
            {
                Transform camPivot = player.CameraPivot != null ? player.CameraPivot : player.transform;
                float beamAngle = Vector3.Angle(camPivot.forward, (eyePos - playerEye).normalized);
                if (beamAngle < 14f)
                {
                    LastNoisePosition = player.transform.position;
                    Awareness = Mathf.Clamp(Awareness + 0.35f * dt, 0f, 0.95f);
                    if (_currentState == DinoState.Patrol || _currentState == DinoState.Idle)
                    {
                        OnHeardNoise(player.transform.position, 22f, "flashlight_sweep");
                    }
                }
            }

            UnseenTimer += dt;
            float decay = _currentState == DinoState.Hunt ? config.awarenessDecayRate * 0.35f : config.awarenessDecayRate;
            Awareness = Mathf.Max(0f, Awareness - decay * dt);
        }

        protected bool HasClearVisualLOS(Vector3 eyePos, PlayerController player)
        {
            if (player == null) return false;
            Vector3 playerEye = player.EyePosition;
            if (!Physics.Linecast(eyePos, playerEye, DinoLayerMasks.VisionOcclusionMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }
            Vector3 playerChest = player.transform.position + Vector3.up * (player.IsCrouching ? 0.55f : 1.05f);
            return !Physics.Linecast(eyePos, playerChest, DinoLayerMasks.VisionOcclusionMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// Strictly validates a lethal bite — NEVER kills through solid walls, closed doors, or DinoBlocker fences!
        /// </summary>
        protected bool CanKillPlayer(PlayerController player)
        {
            if (player == null || player.IsDead || config.killRangeMeters <= 0f) return false;
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist > config.killRangeMeters) return false;

            Vector3 origin = EyePosition;
            Vector3 target = player.EyePosition;
            return !Physics.Linecast(origin, target, DinoLayerMasks.DinoCollisionMask, QueryTriggerInteraction.Ignore);
        }

        private void HandleStealthVisibilityChanged(float visibility, bool concealed)
        {
            CachedEventVisibility = visibility;
            CachedEventConcealed = concealed;
        }

        private void HandleNoiseEmitted(Vector3 worldPos, float radiusMeters, string category)
        {
            if (!aiEnabled || _currentState == DinoState.Scripted) return;
            if (string.Equals(category, "dino_step", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(category, "door_bash", StringComparison.OrdinalIgnoreCase) && Vector3.Distance(transform.position, worldPos) < 3.5f) return;

            float effectiveRadius = radiusMeters * config.hearingSensitivity;
            float dist = Vector3.Distance(transform.position, worldPos);
            if (dist > effectiveRadius) return;

            // Muffle noise radius cleanly through walls/closed doors
            if (Physics.Linecast(EyePosition, worldPos + Vector3.up * 0.6f, DinoLayerMasks.VisionOcclusionMask, QueryTriggerInteraction.Ignore))
            {
                if (dist > effectiveRadius * 0.68f) return;
            }

            OnHeardNoise(worldPos, effectiveRadius, category);
        }

        /// <summary>
        /// AUD-5 bugfix: non-visual loud sounds route cleanly to Investigate, and any escalation to Hunt
        /// IMMEDIATELY resets UnseenTimer = 0f and updates LastSeenPosition so the AI never snaps Hunt->Search
        /// on the next frame or double-plays screech + sniff!
        /// </summary>
        protected virtual void OnHeardNoise(Vector3 noisePos, float effectiveRadius, string category)
        {
            float dist = Vector3.Distance(transform.position, noisePos);
            float proximity01 = 1f - Mathf.Clamp01(dist / Mathf.Max(1f, effectiveRadius));
            LastNoisePosition = noisePos;

            bool isLoudEvent = string.Equals(category, "machine", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(category, "generator", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(category, "breaker", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(category, "e0_gate", StringComparison.OrdinalIgnoreCase);

            float awarenessBump = isLoudEvent ? Mathf.Lerp(0.30f, 0.72f, proximity01) : Mathf.Lerp(0.16f, 0.50f, proximity01);
            Awareness = Mathf.Clamp(Awareness + awarenessBump, 0f, 1.25f);

            if (_currentState == DinoState.Hunt || _currentState == DinoState.Attack)
            {
                LastSeenPosition = noisePos;
                UnseenTimer = 0f;
                return;
            }

            // Escalate directly to Hunt ONLY if already Alert with high awareness or noise occurred almost on top of predator
            if ((_currentState == DinoState.Alert && Awareness >= 0.90f) || (dist < 5.5f && Awareness >= 0.85f))
            {
                LastSeenPosition = noisePos;
                UnseenTimer = 0f;
                TransitionState(DinoState.Hunt);
                return;
            }

            TransitionState(DinoState.Investigate);
        }

        // =========================================================================
        // VELOCITY-MATCHED ANIMATOR DRIVER (ZERO FOOT SKATING!)
        // =========================================================================

        private void UpdateLocomotionAndAnimator(float dt)
        {
            Vector3 disp = transform.position - _prevPosition;
            disp.y = 0f;
            float measuredSpeed = disp.magnitude / dt;

            // Blend measured frame displacement with NavMeshAgent velocity for rock-solid smoothness
            float rawSpeed = (! _usingDirectScriptMove && agent != null && agent.enabled && agent.isOnNavMesh)
                ? Mathf.Lerp(measuredSpeed, new Vector2(agent.velocity.x, agent.velocity.z).magnitude, 0.5f)
                : measuredSpeed;

            _smoothedHorizSpeed = Mathf.Lerp(_smoothedHorizSpeed, rawSpeed, 1f - Mathf.Exp(-12f * dt));

            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_prevYaw, yaw) / dt;
            _prevYaw = yaw;
            _yawRateSmoothed = Mathf.Lerp(_yawRateSmoothed, yawRate, 1f - Mathf.Exp(-10f * dt));

            if (animator == null) return;

            // Pivoting on the spot without turn clips: the animator keeps the real speed (idle pose) and
            // DinoProceduralRig re-plants each foot with a lifted step as the body turns (fix round: feeding a slow
            // walk here played the stride under a motionless body, a treadmill moonwalk).
            float animSpeed = _smoothedHorizSpeed;

            SetAnimFloat(HashSpeed, animSpeed);

            // Exact velocity-to-stride-speed scaling on the BlendTree's Speed Multiplier parameter
            float strideMult = 1.0f;
            if (animSpeed > 0.14f)
            {
                float expectedClipStride = config.EvaluateExpectedClipStrideSpeed(animSpeed);
                strideMult = Mathf.Clamp(animSpeed / Mathf.Max(0.2f, expectedClipStride),
                    config.minAnimSpeedScale, config.maxAnimSpeedScale);
            }
            SetAnimFloat(HashLocomotionSpeedMult, strideMult);

            SetAnimBool(HashIsAlert, _currentState == DinoState.Alert || _currentState == DinoState.Investigate);
            SetAnimBool(HashIsHunting, _currentState == DinoState.Hunt || _currentState == DinoState.Attack);
            SetAnimBool(HashIsStalking, _currentState == DinoState.Alert || _currentState == DinoState.Search || _currentState == DinoState.Investigate);
        }

        // =========================================================================
        // FOOTSTEP, FOOTPRINT, VOCAL & CAMERA TRAUMA EMITTER
        // =========================================================================

        public void OnFootstepLeft() => EmitFootstep(left: true, front: false, fromAnimEvent: true);
        public void OnFootstepRight() => EmitFootstep(left: false, front: false, fromAnimEvent: true);
        public void OnFootstepFrontLeft() => EmitFootstep(left: true, front: true, fromAnimEvent: true);
        public void OnFootstepFrontRight() => EmitFootstep(left: false, front: true, fromAnimEvent: true);

        public void OnBiteHitFrame()
        {
            PendingBiteHit = false;
            OnBiteHitFrameInternal();
        }

        public void OnBashImpactFrame()
        {
            PendingBashImpact = false;
            OnBashImpactFrameInternal();
        }

        private void EmitFootstep(bool left, bool front, bool fromAnimEvent)
        {
            if (fromAnimEvent)
            {
                // Suppress idle micro-shuffle events when completely stationary
                if (_smoothedHorizSpeed < 0.12f && ActionLockTimer <= 0f) return;
                _lastAnimEventStepTime = Time.time;
                _fallbackStepDistAccum = 0f;
            }

            Vector3 footPos = proceduralRig != null
                ? proceduralRig.GetFootWorldPosition(left, front)
                : transform.position;

            float speedFactor = Mathf.Clamp01(_smoothedHorizSpeed / Mathf.Max(0.5f, config.runSpeed));
            float volume = Mathf.Lerp(0.65f, 1.15f, speedFactor);

            if (!string.IsNullOrEmpty(config.footstepSoundId))
            {
                GameEvents.RaisePlaySound(config.footstepSoundId, footPos, volume);
            }

            if (config.footstepTraumaMax > 0.001f)
            {
                float traumaScale = Mathf.Lerp(0.65f, 1.15f, speedFactor);
                EmitDistanceTrauma(config.footstepTraumaMax * traumaScale, config.footstepTraumaRadius, 2.1f);
            }

            if (config.emitGroundFootprints)
            {
                Quaternion footRot = Quaternion.LookRotation(transform.forward, Vector3.up);
                if (proceduralRig != null && proceduralRig.TryGetFootGround(left, front, out Vector3 hitPt, out Vector3 hitNorm))
                {
                    footPos = hitPt;
                    Vector3 projFwd = Vector3.ProjectOnPlane(transform.forward, hitNorm);
                    if (projFwd.sqrMagnitude > 0.001f)
                        footRot = Quaternion.LookRotation(projFwd.normalized, hitNorm);
                }
                GameEvents.RaiseTRexFootprint(footPos, Mathf.Lerp(0.75f, 1.25f, speedFactor), footRot);
            }
        }

        private void UpdateFallbackFootsteps(float dt)
        {
            if (_smoothedHorizSpeed < 0.25f)
            {
                _fallbackStepDistAccum = 0f;
                return;
            }

            // If Animation Events fired recently, let them remain authoritative (zero double footsteps!)
            if (Time.time - _lastAnimEventStepTime < 1.15f) return;

            float expectedStride = Mathf.Max(0.8f, config.EvaluateExpectedClipStrideSpeed(_smoothedHorizSpeed));
            float stepMeters = (proceduralRig != null && proceduralRig.IsQuadruped)
                ? Mathf.Clamp(expectedStride * 0.42f, 0.55f, 1.6f)
                : Mathf.Clamp(expectedStride * 0.52f, 0.65f, 2.4f);

            _fallbackStepDistAccum += _smoothedHorizSpeed * dt;
            if (_fallbackStepDistAccum >= stepMeters)
            {
                _fallbackStepDistAccum -= stepMeters;
                bool isQuad = proceduralRig != null && proceduralRig.IsQuadruped;
                if (isQuad)
                {
                    int idx = _fallbackFootCycle++ % 4;
                    EmitFootstep(left: idx == 0 || idx == 2, front: idx == 0 || idx == 3, fromAnimEvent: false);
                }
                else
                {
                    bool left = (_fallbackFootCycle++ % 2) == 0;
                    EmitFootstep(left, front: false, fromAnimEvent: false);
                }
            }
        }

        protected void EmitVocalization(
            string soundId,
            float jawPeak01,
            float durationSec,
            float traumaMax = 0f,
            float traumaRadius = 25f,
            string soundCaption = null,
            bool ignoreCooldown = false)
        {
            if (!ignoreCooldown && Time.time - LastVocalTime < 0.65f) return;
            LastVocalTime = Time.time;

            if (!string.IsNullOrEmpty(soundId))
            {
                GameEvents.RaisePlaySound(soundId, EyePosition, 1.0f);
            }

            proceduralRig?.TriggerJawPulse(jawPeak01, 0.10f, Mathf.Max(0.2f, durationSec * 0.5f), Mathf.Max(0.2f, durationSec * 0.35f));

            if (traumaMax > 0.001f)
            {
                EmitDistanceTrauma(traumaMax, traumaRadius, 1.3f);
            }

            if (!string.IsNullOrEmpty(soundCaption))
            {
                GameEvents.RaiseSoundCaption(soundCaption, 2.5f);
            }
        }

        protected void EmitDistanceTrauma(float maxTrauma, float maxRadius, float decayRate = 1.6f)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || maxRadius <= 0.1f) return;
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist >= maxRadius) return;
            float t = 1f - Mathf.Clamp01(dist / maxRadius);
            float trauma = maxTrauma * Mathf.Pow(t, 1.55f);
            if (trauma > 0.01f)
            {
                GameEvents.RaiseCameraTrauma(trauma, decayRate);
            }
        }

        // =========================================================================
        // GROUNDING & SAFE ANIMATOR HELPERS
        // =========================================================================

        protected Vector3 ResolveGroundedPosition(Vector3 pos)
        {
            pos.y = ResolveGroundY(pos.x, pos.z, pos.y);
            return pos;
        }

        protected float ResolveGroundY(float x, float z, float fallbackY)
        {
            Vector3 probe = new Vector3(x, fallbackY + 3.0f, z);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 8.0f, DinoLayerMasks.GroundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            if (WorldRegistry.Instance != null)
            {
                return WorldRegistry.Instance.SampleGroundHeight(x, z, fallbackY);
            }
            return fallbackY;
        }

        protected void SetAnimFloat(int hash, float value)
        {
            if (animator != null && _validAnimParams.Contains(hash))
                animator.SetFloat(hash, value);
        }

        protected void SetAnimBool(int hash, bool value)
        {
            if (animator != null && _validAnimParams.Contains(hash))
                animator.SetBool(hash, value);
        }

        protected void TriggerAnimatorParam(int triggerHash, string fallbackStateName = null)
        {
            if (animator == null) return;
            if (_validAnimParams.Contains(triggerHash))
            {
                animator.SetTrigger(triggerHash);
            }
            else if (!string.IsNullOrEmpty(fallbackStateName))
            {
                animator.CrossFadeInFixedTime(fallbackStateName, 0.12f);
            }
        }
    }
}
