using System;
using System.Collections.Generic;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.AI
{
    /// <summary>
    /// Zero-dependency LateUpdate procedural rig controller running cleanly on top of Unity's Animator.
    /// Eliminates Godot's DinoOverlay bone-origin corruption by applying clamped, damped world-space
    /// additive adjustments strictly after Animator evaluation each frame:
    ///   1. Distributed Neck & Head LookAt with anatomical yaw/pitch clamps & automatic suppression during Roar/Bite.
    ///   2. Active Procedural Jaw Articulation (animation vocal pulse + AudioSource RMS envelope + subtle breathing).
    ///   3. Grounded Two-Bone Foot IK, pelvis terrain offset & slope normal alignment (Y >= groundY; zero sinking/floating).
    /// Auto-resolves bones from DinoConfig/manifest overrides or resilient fuzzy matching.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DinoProceduralRig : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private DinoConfig config;
        [SerializeField] private AudioSource vocalAudioSource;

        [Header("Resolved Skeleton References (Auto-Resolved if Empty)")]
        [SerializeField] private Transform rootBone;
        [SerializeField] private Transform pelvisBone;
        [SerializeField] private Transform headBone;
        [SerializeField] private Transform jawBone;
        [SerializeField] private Transform[] neckBones = Array.Empty<Transform>();
        [SerializeField] private Transform[] spineBones = Array.Empty<Transform>();
        [SerializeField] private Transform leftFootBone;
        [SerializeField] private Transform rightFootBone;
        [SerializeField] private Transform leftFrontFootBone;
        [SerializeField] private Transform rightFrontFootBone;
        [SerializeField] private Transform[] tailBones = Array.Empty<Transform>();

        [Header("Feature Toggles & Tuning")]
        [SerializeField] private bool enableLookAt = true;
        [SerializeField] private bool enableJawDriver = true;
        [SerializeField] private bool enableFootIK = true;
        [Tooltip("Pin planted feet in world space while the leg IK absorbs the clip's foot drift.")]
        [SerializeField] private bool footLockEnabled = true;
        [SerializeField] private float lookSmoothSpeed = 7.5f;
        [SerializeField] private float lookWeightBlendSpeed = 5.5f;
        [SerializeField] private float jawSmoothSpeed = 18.0f;
        [SerializeField] private float footIkSmoothSpeed = 14.0f;
        [SerializeField] private float pelvisSmoothSpeed = 10.0f;
        [SerializeField] private float audioRmsGain = 4.2f;
        [Tooltip("Tail trails behind body turns: seconds of yaw rate converted into lag (0 = off).")]
        [SerializeField] private float tailTurnLagSeconds = 0.16f;
        [SerializeField] private float tailMaxLagDeg = 34f;
        [Tooltip("Weight of the procedural gait layer (footfall dip, head counter-motion, tail swing, breathing). " +
                 "-1 = by species: T. rex 1, brachiosaur 0.8, triceratops 0.65, raptor 0.3.")]
        [SerializeField] private float gaitWeight = -1f;
        [Tooltip("Body-space pitch added over the tail chain (negative = down). NaN-free sentinel: 999 = by species " +
                 "(T. rex -26, raptor -10, others 0). The procedural clips carry the T. rex tail ~35 deg up.")]
        [SerializeField] private float tailPitchOffsetDeg = 999f;
        [Tooltip("Share of the clip's tail oscillation removed while moving (0 = clip as authored).")]
        [SerializeField, Range(0f, 1f)] private float tailClipDamping = 0.75f;
        [Tooltip("Share of the clip's pelvis bob (deviation from its 0.8 s average) removed while walking.")]
        [SerializeField, Range(0f, 1f)] private float pelvisBobDamping = 0.85f;

        private struct LimbIkChain
        {
            public Transform Upper;
            public Transform Mid;
            public Transform Foot;
            public bool IsLeft;
            public bool IsFront;
            public float UpperLen;
            public float LowerLen;
            public float RestFootHeightAboveRoot;
            public float SmoothedTargetY;
            public Vector3 SmoothedNormal;
            public bool HasGroundHit;
            public Vector3 LastHitPoint;
            public Vector3 LastHitNormal;
            public bool PlantedInit;
            public float PlantedHeight;
            public float AnimLift;
            public float Stance;
            public float TerrainDelta;
            public float SmoothedOffset;
            public bool Locked;
            public Vector3 LockPos;
            public float LockWeight;
            public float LockYaw;
            public bool NeedsLift;
            public float MaxLift;      // how high this foot actually lifts in the current clip (slow decay)
            public float LiftLo;       // planted below this
            public float LiftHi;       // lifted above this     // released by drift/turn while still planted: slide out, re-lock only after a lift
            public bool InStanceSet;   // hysteresis for the pelvis' lowest-stance pick (in > 0.7, out < 0.3)
            public bool Stepping;      // procedural pivot step in progress (standing turn without turn clips)
            public float StepT;
            public Vector3 StepFrom;
            public bool IsValid => Foot != null;
            public bool CanSolveTwoBone => Upper != null && Mid != null && Foot != null && UpperLen > 0.05f && LowerLen > 0.05f;
        }

        private readonly List<LimbIkChain> _limbs = new List<LimbIkChain>(4);
        private readonly RaycastHit[] _rayBuffer = new RaycastHit[8];
        private readonly float[] _audioSampleBuf = new float[256];

        // Bind-pose anatomical axes cached cleanly in bone local space so Blender Y-along-bone exports work flawlessly
        private Vector3 _headLocalRight = Vector3.right;
        private Vector3 _headLocalForward = Vector3.forward;
        private bool _bindAxesCached;
        private Quaternion _jawRestLocal = Quaternion.identity;
        private bool _jawRestCached;
        private bool _bonesResolved;

        // LookAt runtime state
        private bool _hasLookTarget;
        private Vector3 _lookTargetWorld;
        private float _requestedLookWeight;
        private bool _lookSuppressed;
        private float _currentLookWeight;
        private float _currentLookYaw;
        private float _currentLookPitch;

        // Jaw runtime state
        private float _jawPulsePeak01;
        private float _jawPulseAttack;
        private float _jawPulseHold;
        private float _jawPulseRelease;
        private float _jawPulseElapsed;
        private bool _jawPulseActive;
        private float _jawBreathIntensity = 0.08f;
        private float _jawBreathFreq = 0.35f;
        private float _jawBreathPhase;
        private float _smoothedAudioRms;
        private float _currentJawDeg;

        // Foot IK runtime state
        private float _smoothedPelvisOffsetY;
        private float _footIkWeight = 1f;
        private float spinNoLockDegPerSec = 90f;    // above this body yaw rate no new foot locks (fast pivots slide instead of pinning)
        private bool _ikYawInit;
        private float _ikLastYaw;
        private float _ikYawRate;
        private Vector3 _ikLastPos;
        private float _ikSpeed;

        // Tail inertia runtime state
        private bool _yawInit;
        private float _lastYaw;
        private float _smoothedYawRate;
        private float _tailLagDeg;
        private float _tailLagVel;

        // Gait weight layer (round 5): a damped spring kicked on every hind footfall
        private float _gaitDip;          // m, negative = pelvis down
        private float _gaitDipVel;
        private float _gaitSway;         // -1 (left foot carries) .. +1 (right)
        private float _tailSwingDeg;
        private float _tailSwingVel;
        private float _breathPhase;
        private float _prevStanceL = 1f, _prevStanceR = 1f;

        // Fix round: gait suppression during authored actions (roar, bite, look), pelvis de-bob, tail carriage
        private bool _gaitSuppressed;
        private float _gaitSuppression;   // 0..1, faded over 0.2 s
        private float _pelvisYAvg;
        private bool _pelvisAvgInit;
        private float _pelvisOffsetVel;
        private Quaternion[] _tailAvgLocal = Array.Empty<Quaternion>();
        private bool _tailAvgInit;

        public Transform HeadBone => headBone;
        public Transform JawBone => jawBone;
        public Transform PelvisBone => pelvisBone;
        public float CurrentJawOpenDegrees => _currentJawDeg;
        public float CurrentLookWeight => _currentLookWeight;
        public bool IsQuadruped => config != null ? config.rigOverrides.isQuadruped : (leftFrontFootBone != null || rightFrontFootBone != null);

        private void Awake()
        {
            ResolveBonesIfNeeded();
            CacheBindAxes();
        }

        public void Initialize(DinoConfig dinoConfig, AudioSource audioSource = null, bool forceReResolve = false)
        {
            if (dinoConfig != null) config = dinoConfig;
            if (audioSource != null) vocalAudioSource = audioSource;
            if (forceReResolve || !_bonesResolved)
            {
                ResolveBones();
                CacheBindAxes();
            }
        }

        /// <summary>
        /// Resolves skeleton transforms from DinoConfig manifest overrides first, then falls back to fuzzy matching.
        /// Safe to call both in Edit Mode (from DinoAnimatorBuilder) and at runtime.
        /// </summary>
        public void ResolveBones()
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            DinoRigOverrides ov = config != null ? config.rigOverrides : DinoRigOverrides.CreateDefault(DinoSpeciesType.Velociraptor);

            rootBone = FindExactOrFuzzy(all, ov.rootBone, new[] { "root", "armature" }, excludeTokens: new[] { "mesh" });
            pelvisBone = FindExactOrFuzzy(all, ov.pelvisBone, new[] { "pelvis", "hips", "hip", "cog" });
            headBone = FindExactOrFuzzy(all, ov.headBone, new[] { "head", "skull", "cranium" }, excludeTokens: new[] { "lift", "call" });
            jawBone = FindExactOrFuzzy(all, ov.jawBone, new[] { "jaw", "mandible", "lowerjaw", "chin" });

            neckBones = ResolveChain(all, ov.neckBones, "neck");
            spineBones = ResolveChain(all, ov.spineBones, "spine");
            tailBones = ResolveChain(all, ov.tailBones, "tail");

            leftFootBone = FindExactOrFuzzy(all, ov.leftFootBone,
                new[] { "leftfoot", "foot_l", "foot.l", "l_foot", "hindfoot_l", "ankle_l", "toe_l" },
                excludeTokens: new[] { "front", "hand" });
            rightFootBone = FindExactOrFuzzy(all, ov.rightFootBone,
                new[] { "rightfoot", "foot_r", "foot.r", "r_foot", "hindfoot_r", "ankle_r", "toe_r" },
                excludeTokens: new[] { "front", "hand" });

            if (ov.isQuadruped)
            {
                leftFrontFootBone = FindExactOrFuzzy(all, ov.leftFrontFootBone,
                    new[] { "leftfrontfoot", "frontfoot_l", "front_foot_l", "hand_l", "forefoot_l" });
                rightFrontFootBone = FindExactOrFuzzy(all, ov.rightFrontFootBone,
                    new[] { "rightfrontfoot", "frontfoot_r", "front_foot_r", "hand_r", "forefoot_r" });
            }
            else
            {
                leftFrontFootBone = null;
                rightFrontFootBone = null;
            }

            if (vocalAudioSource == null)
            {
                vocalAudioSource = GetComponent<AudioSource>();
            }

            BuildLimbChains();
            _bonesResolved = true;
        }

        private void ResolveBonesIfNeeded()
        {
            if (!_bonesResolved || (headBone == null && pelvisBone == null))
            {
                ResolveBones();
            }
            else if (_limbs.Count == 0)
            {
                BuildLimbChains();
            }
        }

        private void CacheBindAxes()
        {
            if (_bindAxesCached) return;
            if (headBone != null)
            {
                _headLocalRight = headBone.InverseTransformDirection(transform.right).normalized;
                _headLocalForward = headBone.InverseTransformDirection(transform.forward).normalized;
                if (_headLocalRight.sqrMagnitude < 0.01f) _headLocalRight = Vector3.right;
                if (_headLocalForward.sqrMagnitude < 0.01f) _headLocalForward = Vector3.forward;
            }
            if (jawBone != null)
            {
                _jawRestLocal = jawBone.localRotation;
                _jawRestCached = true;
            }
            _bindAxesCached = true;
        }

        private void BuildLimbChains()
        {
            _limbs.Clear();
            TryAddLimb(leftFootBone, isLeft: true, isFront: false);
            TryAddLimb(rightFootBone, isLeft: false, isFront: false);
            if (leftFrontFootBone != null) TryAddLimb(leftFrontFootBone, isLeft: true, isFront: true);
            if (rightFrontFootBone != null) TryAddLimb(rightFrontFootBone, isLeft: false, isFront: true);
        }

        private void TryAddLimb(Transform foot, bool isLeft, bool isFront)
        {
            if (foot == null) return;

            Transform mid = foot.parent;
            Transform upper = mid != null ? mid.parent : null;

            // For 3-segment digitigrade theropods (Thigh -> Shin -> Metatarsus -> LeftFoot),
            // climb one joint higher if Mid is a short ankle/metatarsus so IK bends cleanly at the knee/ankle!
            if (!isFront && upper != null && upper.parent != null && upper.parent != pelvisBone && upper.parent != rootBone)
            {
                float shortDist = Vector3.Distance(mid.position, foot.position);
                float parentDist = Vector3.Distance(upper.position, mid.position);
                if (shortDist < parentDist * 0.45f)
                {
                    mid = upper;
                    upper = upper.parent;
                }
            }

            // Ensure upper is not pelvis/root itself
            if (upper == pelvisBone || upper == rootBone || upper == transform)
            {
                upper = null;
                mid = null;
            }

            float upperLen = (upper != null && mid != null) ? Vector3.Distance(upper.position, mid.position) : 0f;
            float lowerLen = (mid != null) ? Vector3.Distance(mid.position, foot.position) : 0f;
            float restFootHeight = Mathf.Max(0f, foot.position.y - transform.position.y);

            _limbs.Add(new LimbIkChain
            {
                Upper = upper,
                Mid = mid,
                Foot = foot,
                IsLeft = isLeft,
                IsFront = isFront,
                UpperLen = upperLen,
                LowerLen = lowerLen,
                RestFootHeightAboveRoot = restFootHeight,
                SmoothedTargetY = foot.position.y,
                SmoothedNormal = Vector3.up,
                HasGroundHit = false,
                LastHitPoint = foot.position,
                LastHitNormal = Vector3.up
            });
        }

        // =========================================================================
        // PUBLIC RUNTIME CONTROLS (CALLED BY DinoControllerBase & SPECIES AI)
        // =========================================================================

        public void SetLookTarget(Vector3 worldPos, float weight = 1.0f)
        {
            _hasLookTarget = true;
            _lookTargetWorld = worldPos;
            _requestedLookWeight = Mathf.Clamp01(weight);
        }

        public void ClearLookTarget()
        {
            _hasLookTarget = false;
            _requestedLookWeight = 0f;
        }

        public void SetLookSuppression(bool suppressed)
        {
            _lookSuppressed = suppressed;
        }

        /// <summary>
        /// Fades the gait layer's head stabiliser and pelvis de-bob out (over 0.2 s) while an authored action plays,
        /// so a roar, bite or look-scan reads exactly as the clip was made.
        /// </summary>
        public void SetGaitSuppression(bool suppressed)
        {
            _gaitSuppressed = suppressed;
        }

        public void SetFootIKWeight(float weight01)
        {
            _footIkWeight = Mathf.Clamp01(weight01);
        }

        public void SetBreathingState(float intensity01, float frequencyHz)
        {
            _jawBreathIntensity = Mathf.Clamp(intensity01, 0f, 0.35f);
            _jawBreathFreq = Mathf.Clamp(frequencyHz, 0.1f, 3.0f);
        }

        /// <summary>
        /// Fires a smooth envelope pulse driving the lower Jaw bone open for roars, screeches, bites, or snorts.
        /// </summary>
        public void TriggerJawPulse(float peakOpen01, float attackSeconds = 0.12f, float holdSeconds = 0.45f, float releaseSeconds = 0.35f)
        {
            _jawPulsePeak01 = Mathf.Clamp01(peakOpen01);
            _jawPulseAttack = Mathf.Max(0.02f, attackSeconds);
            _jawPulseHold = Mathf.Max(0f, holdSeconds);
            _jawPulseRelease = Mathf.Max(0.05f, releaseSeconds);
            _jawPulseElapsed = 0f;
            _jawPulseActive = true;
        }

        public Vector3 GetEyeWorldPosition(float fallbackEyeHeight = 1.5f)
        {
            if (headBone != null) return headBone.position;
            return transform.position + Vector3.up * fallbackEyeHeight;
        }

        public Vector3 GetHeadForwardWorld()
        {
            if (headBone != null && _bindAxesCached)
            {
                Vector3 fwd = headBone.TransformDirection(_headLocalForward);
                if (fwd.sqrMagnitude > 0.001f) return fwd.normalized;
            }
            return transform.forward;
        }

        public Vector3 GetFootWorldPosition(bool left, bool front = false)
        {
            for (int i = 0; i < _limbs.Count; i++)
            {
                if (_limbs[i].IsLeft == left && _limbs[i].IsFront == front && _limbs[i].Foot != null)
                {
                    return _limbs[i].HasGroundHit ? _limbs[i].LastHitPoint : _limbs[i].Foot.position;
                }
            }
            float sideOffset = (left ? -0.35f : 0.35f) * (config != null ? config.agentRadius : 0.5f);
            return transform.position + transform.right * sideOffset;
        }

        public bool TryGetFootGround(bool left, bool front, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            for (int i = 0; i < _limbs.Count; i++)
            {
                if (_limbs[i].IsLeft == left && _limbs[i].IsFront == front && _limbs[i].HasGroundHit)
                {
                    hitPoint = _limbs[i].LastHitPoint;
                    hitNormal = _limbs[i].LastHitNormal;
                    return true;
                }
            }
            hitPoint = transform.position;
            hitNormal = Vector3.up;
            return false;
        }

        // =========================================================================
        // LATEUPDATE PROCEDURAL PASS (RUNS CLEANLY ON TOP OF UNITY ANIMATOR)
        // =========================================================================

        private void LateUpdate()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>One procedural pass after the Animator (separate from LateUpdate so edit-mode tools can step it).</summary>
        private void Tick(float dt)
        {
            if (dt <= 0.00001f) return;

            ResolveBonesIfNeeded();
            CacheBindAxes();

            // Body yaw rate (deg/s) for the foot lock: no new locks while spinning fast on the spot.
            {
                float y = transform.eulerAngles.y;
                if (!_ikYawInit) { _ikLastYaw = y; _ikYawInit = true; }
                float r = Mathf.DeltaAngle(_ikLastYaw, y) / dt;
                _ikLastYaw = y;
                _ikYawRate = Mathf.Lerp(_ikYawRate, r, 1f - Mathf.Exp(-12f * dt));
                Vector3 p = transform.position;
                if (_ikLastPos == Vector3.zero) _ikLastPos = p;
                float sp = new Vector2(p.x - _ikLastPos.x, p.z - _ikLastPos.z).magnitude / dt;
                _ikLastPos = p;
                _ikSpeed = Mathf.Lerp(_ikSpeed, Mathf.Min(sp, 30f), 1f - Mathf.Exp(-8f * dt));
            }

            UpdateGaitWeight(dt);
            _gaitSuppression = Mathf.MoveTowards(_gaitSuppression, _gaitSuppressed ? 1f : 0f, dt / 0.2f);

            // 1. Grounded Foot IK + Pelvis Slope Alignment FIRST so upper body sits at true grounded height
            if (enableFootIK && _footIkWeight > 0.001f && _limbs.Count > 0)
            {
                ApplyGroundedFootIK(dt);
            }

            // 2. Smooth Distributed Neck & Head LookAt
            if (enableLookAt && headBone != null)
            {
                ApplyNeckAndHeadLookAt(dt);
            }

            ApplyGaitUpperBody();

            // 3. Tail inertia: the NavMesh turns the body rigidly; let the tail trail behind the turn.
            if (tailBones != null && tailBones.Length > 0 && tailTurnLagSeconds > 0f)
            {
                ApplyTailInertia(dt);
            }

            // 4. Active Procedural Jaw Articulation (runs AFTER Head LookAt so skull right axis is exact)
            if (enableJawDriver && jawBone != null)
            {
                ApplyJawArticulation(dt);
            }
        }

        private void ApplyNeckAndHeadLookAt(float dt)
        {
            float targetWeight = (_hasLookTarget && !_lookSuppressed) ? _requestedLookWeight : 0f;
            _currentLookWeight = Mathf.Lerp(_currentLookWeight, targetWeight, 1f - Mathf.Exp(-lookWeightBlendSpeed * dt));

            float maxYaw = config != null ? config.rigOverrides.maxNeckYawDeg : 60f;
            float maxPitch = config != null ? config.rigOverrides.maxNeckPitchDeg : 28f;

            float desiredYaw = 0f;
            float desiredPitch = 0f;

            if (_hasLookTarget && _currentLookWeight > 0.001f)
            {
                Vector3 toTargetWorld = _lookTargetWorld - headBone.position;
                if (toTargetWorld.sqrMagnitude > 0.04f)
                {
                    Vector3 localDir = transform.InverseTransformDirection(toTargetWorld.normalized);
                    float rawYaw = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                    float horizDist = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
                    float rawPitch = -Mathf.Atan2(localDir.y, Mathf.Max(0.001f, horizDist)) * Mathf.Rad2Deg;

                    // If target is far behind (> 145 deg), smoothly fade yaw back instead of snapping across ±180
                    if (Mathf.Abs(rawYaw) > 145f)
                    {
                        float backFade = Mathf.InverseLerp(180f, 145f, Mathf.Abs(rawYaw));
                        rawYaw = Mathf.Clamp(rawYaw, -maxYaw, maxYaw) * backFade;
                    }

                    desiredYaw = Mathf.Clamp(rawYaw, -maxYaw, maxYaw);
                    desiredPitch = Mathf.Clamp(rawPitch, -maxPitch, maxPitch);
                }
            }

            float smoothT = 1f - Mathf.Exp(-lookSmoothSpeed * dt);
            _currentLookYaw = Mathf.Lerp(_currentLookYaw, desiredYaw, smoothT);
            _currentLookPitch = Mathf.Lerp(_currentLookPitch, desiredPitch, smoothT);

            if (_currentLookWeight <= 0.001f) return;

            float appliedYaw = _currentLookYaw * _currentLookWeight;
            float appliedPitch = _currentLookPitch * _currentLookWeight;

            // Distribute rotation smoothly across Neck chain + Head (progressively heavier toward Head)
            int neckCount = neckBones != null ? neckBones.Length : 0;
            float headShare = neckCount > 0 ? 0.48f : 1.0f;
            float neckTotalShare = 1.0f - headShare;

            Vector3 upAxis = transform.up;
            Vector3 rightAxis = transform.right;

            if (neckCount > 0)
            {
                float weightSum = 0f;
                for (int i = 0; i < neckCount; i++) weightSum += (i + 1);

                for (int i = 0; i < neckCount; i++)
                {
                    Transform neck = neckBones[i];
                    if (neck == null) continue;
                    float frac = ((i + 1) / weightSum) * neckTotalShare;
                    Quaternion delta = Quaternion.AngleAxis(appliedYaw * frac, upAxis)
                                     * Quaternion.AngleAxis(appliedPitch * frac, rightAxis);
                    neck.rotation = delta * neck.rotation;
                }
            }

            Quaternion headDelta = Quaternion.AngleAxis(appliedYaw * headShare, upAxis)
                                 * Quaternion.AngleAxis(appliedPitch * headShare, rightAxis);
            headBone.rotation = headDelta * headBone.rotation;
        }

        private float ResolveGaitWeight()
        {
            if (gaitWeight >= 0f) return gaitWeight;
            if (config == null) return 0.3f;
            switch (config.speciesType)
            {
                case DinoSpeciesType.Tyrannosaurus: return 1.0f;
                case DinoSpeciesType.Brachiosaurus: return 0.8f;
                case DinoSpeciesType.Triceratops: return 0.65f;
                default: return 0.3f;
            }
        }

        private float GaitScale => Mathf.Clamp(config != null ? config.agentHeight / 1.67f : 1f, 0.6f, 3.5f);
        private float _gaitSideShift;
        private float _headLag;
        private float _headLagVel;
        private float _headYAvg;
        private bool _headAvgInit;

        /// <summary>
        /// Round 5 gait weight layer. The procedural clips carry the legs but not the mass: the body glided at one
        /// height. Every hind footfall now kicks a damped spring that drops the pelvis (the planted leg's knee takes
        /// it through the IK), the head lags the drop, the tail swings over the carrying leg and bounces, and an idle
        /// animal breathes through its chest.
        /// </summary>
        private void UpdateGaitWeight(float dt)
        {
            float w = ResolveGaitWeight();
            float scale = GaitScale;
            float moving = Mathf.Clamp01((_ikSpeed - 0.15f) / 0.6f);
            float runRef = config != null ? Mathf.Max(1f, config.runSpeed) : 6f;
            float speed01 = Mathf.Clamp01(_ikSpeed / runRef);

            float sl = 1f, sr = 1f;
            for (int i = 0; i < _limbs.Count; i++)
            {
                if (_limbs[i].IsFront || !_limbs[i].IsValid) continue;
                if (_limbs[i].IsLeft) sl = _limbs[i].Stance; else sr = _limbs[i].Stance;
            }
            bool landL = _prevStanceL < 0.5f && sl >= 0.5f;
            bool landR = _prevStanceR < 0.5f && sr >= 0.5f;
            _prevStanceL = sl; _prevStanceR = sr;

            float omega = 13f / Mathf.Sqrt(scale);
            if (w > 0.001f && moving > 0.05f && (landL || landR))
            {
                // The clips already bob the pelvis (T. rex walk: 34 cm): this only adds the impact on contact.
                float dipTarget = w * scale * (0.010f + 0.012f * speed01) * moving;
                _gaitDipVel -= dipTarget * omega / 0.6f;
            }
            const float zeta = 0.55f;
            float acc = -omega * omega * _gaitDip - 2f * zeta * omega * _gaitDipVel;
            _gaitDipVel += acc * dt;
            _gaitDip = Mathf.Clamp(_gaitDip + _gaitDipVel * dt, -0.16f * scale, 0.05f * scale);

            // The head follows the body's drop late and softer.
            float hOmega = omega * 0.55f;
            float hAcc = -hOmega * hOmega * (_headLag - _gaitDip) - 2f * 0.8f * hOmega * _headLagVel;
            _headLagVel += hAcc * dt;
            _headLag += _headLagVel * dt;

            float swayTarget = (sl - sr) * moving;            // + = left foot carries the weight
            _gaitSway = Mathf.Lerp(_gaitSway, swayTarget, 1f - Mathf.Exp(-7f * dt));
            _gaitSideShift = -_gaitSway * w * 0.016f * scale;

            float tailTarget = _gaitSway * w * 4f;           // + yaw swings a backward tail to the left
            float tAcc = (tailTarget - _tailSwingDeg) * 30f - _tailSwingVel * 6.5f;
            _tailSwingVel += tAcc * dt;
            _tailSwingDeg += _tailSwingVel * dt;

            _breathPhase += dt * Mathf.PI * 2f * (0.55f / Mathf.Sqrt(scale));
        }

        private void ApplyGaitUpperBody()
        {
            float w = ResolveGaitWeight();
            if (w <= 0.001f) return;
            float scale = GaitScale;
            Vector3 right = transform.right;
            Vector3 up = transform.up;

            float rel = (_headLag - _gaitDip) / scale;       // > 0 while the body is below where the head expects

            // Head stabilisation (measured: the T. rex walk bobbed the head 35 cm, exactly with the pelvis, which read
            // as a stiff rig). Like a large bird, the neck now absorbs most of the bob: the neck base pitches against
            // the head's deviation from its running average and the skull counter-rotates so the gaze stays level.
            // Fix round: only while walking (0 at rest) and never during an authored action (the stabiliser damped
            // and lagged Roar_Massive, Bite_Snap, Look_Scan and the brachiosaur's head lift). The 0.4 s average was
            // kept: a 0.25 s average measured a larger head bob (0.37 m) and the gates already protect the actions.
            float movingGate = Mathf.Clamp01((_ikSpeed - 0.15f) / 0.6f) * (1f - _gaitSuppression);
            if (headBone != null && neckBones != null && neckBones.Length > 0 && neckBones[0] != null)
            {
                float hy = transform.InverseTransformPoint(headBone.position).y;
                float k = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
                if (!_headAvgInit) { _headYAvg = hy; _headAvgInit = true; }
                _headYAvg = Mathf.Lerp(_headYAvg, hy, k);
                float err = hy - _headYAvg;
                float neckLen = Mathf.Max(0.2f, Vector3.Distance(neckBones[0].position, headBone.position));
                float stabilise = Mathf.Lerp(0.35f, 0.65f, w) * movingGate;
                float ang = Mathf.Clamp(Mathf.Rad2Deg * stabilise * err / neckLen, -9f, 9f);
                if (Mathf.Abs(ang) > 0.02f)
                {
                    neckBones[0].rotation = Quaternion.AngleAxis(ang, right) * neckBones[0].rotation;
                    headBone.rotation = Quaternion.AngleAxis(-ang * 0.85f, right) * headBone.rotation;
                }
            }

            // Breathing through the chest: strongest at rest.
            float moving = Mathf.Clamp01((_ikSpeed - 0.15f) / 0.6f);
            float breath = Mathf.Sin(_breathPhase) * 0.9f * w * (1f - 0.6f * moving);
            if (spineBones != null && spineBones.Length > 0 && Mathf.Abs(breath) > 0.01f)
            {
                float per = -breath / spineBones.Length;     // chest rises on the in-breath
                for (int i = 0; i < spineBones.Length; i++)
                    if (spineBones[i] != null) spineBones[i].rotation = Quaternion.AngleAxis(per, right) * spineBones[i].rotation;
            }

            // Tail: swings over the carrying leg and bounces against the footfall.
            if (tailBones != null && tailBones.Length > 0)
            {
                ApplyTailCarriage();
                float tailPitch = Mathf.Clamp(rel * 40f * w, -2f, 2f);   // fix round: +-7 deg whipped the tip ~0.7 m
                int n = tailBones.Length;
                float wsum = n * (n + 1) * 0.5f;
                for (int i = 0; i < n; i++)
                {
                    Transform t = tailBones[i];
                    if (t == null) continue;
                    float share = (i + 1) / wsum;              // more bend toward the tip
                    t.rotation = Quaternion.AngleAxis(_tailSwingDeg * share, up)
                               * Quaternion.AngleAxis(tailPitch * share, right) * t.rotation;
                }
            }
        }

        private float ResolveTailPitchOffset()
        {
            if (tailPitchOffsetDeg < 900f) return tailPitchOffsetDeg;
            if (config == null) return 0f;
            switch (config.speciesType)
            {
                case DinoSpeciesType.Tyrannosaurus: return -26f;
                case DinoSpeciesType.Velociraptor: return -10f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Fix round: the procedural clips carry the T. rex tail cocked ~35 deg up with the tip whipping 1 m per step,
        /// and hook the raptor tail into an S. Damp the clip's oscillation toward its running average (the counterweight
        /// moves less than the legs) and lower the whole chain in body space so it reads as a level counterweight.
        /// </summary>
        private void ApplyTailCarriage()
        {
            int n = tailBones.Length;
            float dt = Time.deltaTime;
            if (_tailAvgLocal.Length != n) { _tailAvgLocal = new Quaternion[n]; _tailAvgInit = false; }
            float moving = Mathf.Clamp01((_ikSpeed - 0.15f) / 0.6f);
            float damp = tailClipDamping * moving * (1f - _gaitSuppression);
            float k = 1f - Mathf.Exp(-dt / 1.2f);   // longer than one step cycle (0.8 s for the T. rex)
            for (int i = 0; i < n; i++)
            {
                Transform t = tailBones[i];
                if (t == null) continue;
                Quaternion anim = t.localRotation;
                if (!_tailAvgInit) _tailAvgLocal[i] = anim;
                else _tailAvgLocal[i] = Quaternion.Slerp(_tailAvgLocal[i], anim, k);
                if (damp > 0.001f) t.localRotation = Quaternion.Slerp(anim, _tailAvgLocal[i], damp);
            }
            _tailAvgInit = true;

            float pitch = ResolveTailPitchOffset();
            if (Mathf.Abs(pitch) < 0.01f) return;
            // Mostly at the root (that is where the clip cocks it), tapering down the chain. The axis is horizontal and
            // square to each bone's current direction, so a tail swung to the side is lowered as much as a straight one
            // (about the body's right axis the swung tip still rose at each side: a 0.7 m vertical arc per step).
            Vector3 up = transform.up;
            float wsum = n * (n + 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                Transform t = tailBones[i];
                if (t == null) continue;
                Vector3 dir = i + 1 < n && tailBones[i + 1] != null ? tailBones[i + 1].position - t.position
                            : (i > 0 && tailBones[i - 1] != null ? t.position - tailBones[i - 1].position : -transform.forward);
                Vector3 axis = Vector3.Cross(dir, up);          // = transform.right for a tail pointing straight back
                if (axis.sqrMagnitude < 1e-6f) continue;
                float share = (n - i) / wsum;
                t.rotation = Quaternion.AngleAxis(pitch * share, axis.normalized) * t.rotation;   // pitch < 0 lowers it
            }

            // Carriage hold: the clip swings the tail on a cone, so its tip still rose ~0.7 m at each side of the sweep.
            // Pitch the chain against 80 % of the tip's height error from a slightly drooping line (no lag: measured
            // and corrected in the same frame), faded out during authored actions.
            Transform root = tailBones[0], tip = tailBones[n - 1];
            if (root == null || tip == null || n < 2) return;
            float hold = 0.8f * (1f - _gaitSuppression);
            if (hold < 0.01f) return;
            Vector3 rootL = transform.InverseTransformPoint(root.position);
            Vector3 tipL = transform.InverseTransformPoint(tip.position);
            float len = Mathf.Max(0.3f, Vector3.Distance(rootL, tipL));
            float err = tipL.y - (rootL.y - 0.06f * len);
            float corr = -Mathf.Asin(Mathf.Clamp(err / len, -0.6f, 0.6f)) * Mathf.Rad2Deg * hold;   // deg, < 0 lowers
            for (int i = 0; i < n - 1; i++)
            {
                Transform t = tailBones[i];
                if (t == null || tailBones[i + 1] == null) continue;
                Vector3 axis = Vector3.Cross(tailBones[i + 1].position - t.position, up);
                if (axis.sqrMagnitude < 1e-6f) continue;
                float share = (n - 1 - i) / (n * (n - 1) * 0.5f);
                t.rotation = Quaternion.AngleAxis(corr * share, axis.normalized) * t.rotation;
            }
        }

        private void ApplyTailInertia(float dt)
        {
            float yaw = transform.eulerAngles.y;
            if (!_yawInit) { _lastYaw = yaw; _yawInit = true; }
            float rate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;
            _smoothedYawRate = Mathf.Lerp(_smoothedYawRate, rate, 1f - Mathf.Exp(-6f * dt));
            float target = Mathf.Clamp(-_smoothedYawRate * tailTurnLagSeconds, -tailMaxLagDeg, tailMaxLagDeg);
            // under-damped follow so the tail overshoots a little when the turn stops
            float accel = (target - _tailLagDeg) * 38f - _tailLagVel * 7.5f;
            _tailLagVel += accel * dt;
            _tailLagDeg += _tailLagVel * dt;
            if (Mathf.Abs(_tailLagDeg) < 0.05f) return;
            Vector3 up = transform.up;
            int n = tailBones.Length;
            float per = _tailLagDeg / n;
            for (int i = 0; i < n; i++)
            {
                Transform t = tailBones[i];
                if (t == null) continue;
                t.rotation = Quaternion.AngleAxis(per, up) * t.rotation;
            }
        }

        private void ApplyJawArticulation(float dt)
        {
            // 1. Breathing micro-pulse
            _jawBreathPhase += dt * _jawBreathFreq * Mathf.PI * 2f;
            float breath01 = (0.5f + 0.5f * Mathf.Sin(_jawBreathPhase)) * _jawBreathIntensity;

            // 2. Active vocal/bite envelope
            float pulse01 = 0f;
            if (_jawPulseActive)
            {
                _jawPulseElapsed += dt;
                float totalDur = _jawPulseAttack + _jawPulseHold + _jawPulseRelease;
                if (_jawPulseElapsed >= totalDur)
                {
                    _jawPulseActive = false;
                }
                else if (_jawPulseElapsed < _jawPulseAttack)
                {
                    float t = _jawPulseElapsed / _jawPulseAttack;
                    pulse01 = _jawPulsePeak01 * (t * t * (3f - 2f * t));
                }
                else if (_jawPulseElapsed < _jawPulseAttack + _jawPulseHold)
                {
                    // Organic vocal flutter during sustained roar
                    float flutter = 0.08f * Mathf.Sin(_jawPulseElapsed * 38f);
                    pulse01 = Mathf.Clamp01(_jawPulsePeak01 + flutter);
                }
                else
                {
                    float t = (_jawPulseElapsed - _jawPulseAttack - _jawPulseHold) / _jawPulseRelease;
                    float easeOut = 1f - (t * t * (3f - 2f * t));
                    pulse01 = _jawPulsePeak01 * easeOut;
                }
            }

            // 3. Live AudioSource RMS envelope
            float targetAudioRms = 0f;
            if (vocalAudioSource != null && vocalAudioSource.isPlaying)
            {
                vocalAudioSource.GetOutputData(_audioSampleBuf, 0);
                float sumSq = 0f;
                for (int i = 0; i < _audioSampleBuf.Length; i++)
                {
                    float s = _audioSampleBuf[i];
                    sumSq += s * s;
                }
                float rms = Mathf.Sqrt(sumSq / _audioSampleBuf.Length);
                targetAudioRms = Mathf.Clamp01(rms * audioRmsGain);
            }
            float rmsRate = targetAudioRms > _smoothedAudioRms ? 24f : 10f;
            _smoothedAudioRms = Mathf.Lerp(_smoothedAudioRms, targetAudioRms, 1f - Mathf.Exp(-rmsRate * dt));

            float maxOpenDeg = config != null ? config.rigOverrides.jawMaxOpenDeg : 36f;
            float sign = (config != null && Mathf.Abs(config.rigOverrides.jawAxisSign) > 0.01f)
                ? Mathf.Sign(config.rigOverrides.jawAxisSign)
                : 1f;

            float combined01 = Mathf.Clamp01(Mathf.Max(breath01, Mathf.Max(pulse01, _smoothedAudioRms)));
            // The roar/bite clips already open the jaw 35-44 deg. The procedural pulse used to be added on top of
            // that (~75 deg: a dislocated jaw). Only add what the animation is not already doing.
            float animOpenDeg = _jawRestCached ? Quaternion.Angle(_jawRestLocal, jawBone.localRotation) : 0f;
            float targetDeg = Mathf.Max(0f, combined01 * maxOpenDeg - animOpenDeg);

            _currentJawDeg = Mathf.Lerp(_currentJawDeg, targetDeg, 1f - Mathf.Exp(-jawSmoothSpeed * dt));
            if (_currentJawDeg <= 0.05f) return;

            // Rotate jaw cleanly around the skull's exact anatomical right axis (+X opens downward in left-handed Unity!)
            Vector3 skullRight = headBone != null
                ? headBone.TransformDirection(_headLocalRight).normalized
                : transform.right;

            Quaternion jawDelta = Quaternion.AngleAxis(_currentJawDeg * sign, skullRight);
            jawBone.rotation = jawDelta * jawBone.rotation;
        }

        private void ApplyGroundedFootIK(float dt)
        {
            // The clips are authored with the soles on the root's ground plane (measured: lowest skinned vertex = 0.000 m
            // in every clip). So the IK only has to correct for the difference between the real ground under each foot
            // and that plane: never lift a foot to a bind-pose "rest height" (that made the trike and brachio hover
            // 4-7 cm and added a 2 cm gap to everyone).
            float maxDrop = config != null ? config.rigOverrides.maxPelvisDropMeters : 0.30f;
            float maxLift = config != null ? config.rigOverrides.maxPelvisLiftMeters : 0.20f;
            float maxSlopeDeg = config != null ? config.rigOverrides.maxFootSlopeAlignDeg : 28f;
            float scale = Mathf.Max(0.2f, config != null ? config.agentHeight / 1.67f : 1f);   // raptor = 1
            float rayUp = config != null ? Mathf.Clamp(config.agentHeight * 0.45f, 0.6f, 2.2f) : 1.0f;
            float rayDist = rayUp + Mathf.Max(1.2f, maxDrop + 1.0f);
            float rootY = transform.position.y;
            float swingLo = 0.05f * scale, swingHi = 0.30f * scale;

            float lowestStanceDelta = float.MaxValue;
            for (int i = 0; i < _limbs.Count; i++)
            {
                LimbIkChain limb = _limbs[i];
                if (!limb.IsValid) continue;
                Vector3 footPos = limb.Foot.position;
                float h = footPos.y - rootY;

                // Planted height of this foot in the current clip: follows the lowest point quickly, relaxes slowly.
                if (!limb.PlantedInit) { limb.PlantedHeight = h; limb.PlantedInit = true; }
                else if (h < limb.PlantedHeight) limb.PlantedHeight = h;
                else limb.PlantedHeight = Mathf.MoveTowards(limb.PlantedHeight, h, 0.12f * scale * dt);
                limb.AnimLift = Mathf.Max(0f, h - limb.PlantedHeight);
                // Thresholds follow how high the clip really lifts this foot: the procedural quadruped walks lift the
                // trike's feet 14-17 cm and the brachiosaur's 8-17 cm, below the scale-based 0.05/0.30 x scale band, so
                // with fixed thresholds their feet never counted as lifted (locked 99 % of the time, then dragged).
                limb.MaxLift = Mathf.Max(limb.AnimLift, limb.MaxLift * (1f - 0.15f * dt));
                float liftRef = Mathf.Max(0.04f, limb.MaxLift);
                limb.LiftLo = Mathf.Min(swingLo, 0.25f * liftRef);
                limb.LiftHi = Mathf.Min(swingLo * 1.8f, 0.5f * liftRef);
                limb.Stance = 1f - Mathf.InverseLerp(limb.LiftLo, Mathf.Min(swingHi, 0.9f * liftRef), limb.AnimLift);

                Vector3 rayOrigin = new Vector3(footPos.x, rootY + rayUp + Mathf.Max(0f, h), footPos.z);
                if (RaycastGroundIgnoringSelf(rayOrigin, rayDist + Mathf.Max(0f, h), out Vector3 hitPt, out Vector3 hitNormal))
                {
                    limb.HasGroundHit = true;
                    limb.LastHitPoint = hitPt;
                    limb.LastHitNormal = hitNormal;
                    float delta = Mathf.Clamp(hitPt.y - rootY, -maxDrop, maxLift);
                    limb.TerrainDelta = delta;
                    // Hysteresis (fix round): a foot joins the pelvis' support set above 0.7 stance and leaves below
                    // 0.3, so the pick does not flip-flop at each footfall (that shuddered the pelvis 2-6 cm per frame).
                    if (!limb.InStanceSet && limb.Stance > 0.7f && !limb.Stepping) limb.InStanceSet = true;
                    else if (limb.InStanceSet && (limb.Stance < 0.3f || limb.Stepping)) limb.InStanceSet = false;
                    if (limb.InStanceSet) lowestStanceDelta = Mathf.Min(lowestStanceDelta, delta);
                }
                else
                {
                    limb.HasGroundHit = false;
                    limb.TerrainDelta = 0f;
                }
                _limbs[i] = limb;
            }

            // Pelvis follows the lowest planted foot (so it can reach down a slope); the higher foot bends.
            float desiredPelvisOffset = lowestStanceDelta < float.MaxValue ? Mathf.Min(0f, lowestStanceDelta) : 0f;
            // SmoothDamp (fix round): the exponential lerp passed every footfall's step change straight through.
            _smoothedPelvisOffsetY = Mathf.SmoothDamp(_smoothedPelvisOffsetY, desiredPelvisOffset * _footIkWeight,
                ref _pelvisOffsetVel, 0.8f / Mathf.Max(1f, pelvisSmoothSpeed), Mathf.Infinity, dt);   // 10 -> 0.08 s

            // De-bob (fix round): the clips bounce the pelvis 34 cm on a 4.4 m T. rex (springy, not heavy). Remove a
            // share of its deviation from the 0.8 s average while walking; planted feet stay put through the leg IK.
            float deBob = 0f;
            if (pelvisBone != null)
            {
                float py = transform.InverseTransformPoint(pelvisBone.position).y;
                if (!_pelvisAvgInit) { _pelvisYAvg = py; _pelvisAvgInit = true; }
                _pelvisYAvg = Mathf.Lerp(_pelvisYAvg, py, 1f - Mathf.Exp(-dt / 0.8f));
                float movingP = Mathf.Clamp01((_ikSpeed - 0.15f) / 0.6f);
                float share = pelvisBobDamping * Mathf.Clamp01(ResolveGaitWeight()) * movingP * (1f - _gaitSuppression);
                deBob = -share * (py - _pelvisYAvg);
            }
            // The footfall dip rides on the pelvis; planted feet are put back on the ground below (knees take it).
            float pelvisShift = _smoothedPelvisOffsetY + (_gaitDip + deBob) * _footIkWeight;
            if (pelvisBone != null && (Mathf.Abs(pelvisShift) > 0.0005f || Mathf.Abs(_gaitSideShift) > 0.0005f))
            {
                pelvisBone.position += Vector3.up * pelvisShift + transform.right * (_gaitSideShift * _footIkWeight);
            }

            float ikSmooth = 1f - Mathf.Exp(-footIkSmoothSpeed * dt);
            // Procedural pivot steps (fix round): a standing turn without turn clips used to play the walk cycle under a
            // motionless body (treadmill). Now the idle pose stays and each foot is re-planted with a short lifted step
            // once its pin has drifted 20 deg of body yaw (or 18 % of the leg) from where it landed; bipeds step one
            // foot at a time, quadrupeds at most one per side.
            int stepCount = 0; bool stepLeft = false, stepRight = false;
            for (int i = 0; i < _limbs.Count; i++)
                if (_limbs[i].Stepping) { stepCount++; if (_limbs[i].IsLeft) stepLeft = true; else stepRight = true; }
            bool quad = IsQuadruped;
            bool standing = _ikSpeed < 0.35f;
            float stepDur = 0.26f * Mathf.Sqrt(Mathf.Max(0.6f, scale));
            float stepH = 0.06f * scale;
            for (int i = 0; i < _limbs.Count; i++)
            {
                LimbIkChain limb = _limbs[i];
                if (!limb.IsValid || !limb.HasGroundHit) continue;
                // Offset this foot needs relative to where the (pelvis-shifted) animation put it.
                float wanted = Mathf.Lerp(pelvisShift, limb.TerrainDelta, limb.Stance) - pelvisShift;
                // A swinging foot must never dip under the local ground either.
                float animY = limb.Foot.position.y;
                float floorY = limb.LastHitPoint.y + limb.PlantedHeight;
                if (animY + wanted < floorY) wanted = floorY - animY;
                wanted *= _footIkWeight;
                limb.SmoothedOffset = Mathf.Lerp(limb.SmoothedOffset, wanted, ikSmooth);

                // Foot locking: the procedural clips let planted feet drift (trike/brachio front feet move back at
                // 0.95-1.6 m/s against a 1.3 m/s body, hind feet are flat only ~15 % of the cycle). While a foot is
                // planted, pin it where it landed and let the leg IK absorb the drift; release on lift-off.
                Vector3 cur = limb.Foot.position;
                float legLen = limb.UpperLen + limb.LowerLen;
                float yaw = transform.eulerAngles.y;
                Vector3 curFlat = new Vector3(cur.x, 0f, cur.z);
                if (limb.NeedsLift && limb.AnimLift > limb.LiftHi) limb.NeedsLift = false;
                bool spinning = Mathf.Abs(_ikYawRate) > spinNoLockDegPerSec;
                bool planted = limb.AnimLift < limb.LiftLo && footLockEnabled && _footIkWeight > 0.5f && !limb.NeedsLift;
                float stepLift = 0f;
                if (!limb.Stepping && limb.Locked && standing && limb.LockWeight > 0.9f
                    && (quad ? (stepCount < 2 && !(limb.IsLeft ? stepLeft : stepRight)) : stepCount == 0))
                {
                    float yawDrift = Mathf.Abs(Mathf.DeltaAngle(limb.LockYaw, yaw));
                    float posDrift = (limb.LockPos - curFlat).magnitude;
                    if (yawDrift > 10f || posDrift > legLen * 0.18f)
                    {
                        limb.Stepping = true; limb.StepT = 0f; limb.StepFrom = limb.LockPos; limb.Locked = false;
                        stepCount++; if (limb.IsLeft) stepLeft = true; else stepRight = true;
                    }
                }
                if (limb.Stepping)
                {
                    limb.StepT += dt / stepDur;
                    float u = Mathf.Clamp01(limb.StepT);
                    limb.LockPos = Vector3.Lerp(limb.StepFrom, curFlat, u * u * (3f - 2f * u));
                    limb.LockWeight = 1f;
                    stepLift = Mathf.Sin(Mathf.PI * u) * stepH;
                    if (limb.StepT >= 1f)
                    {
                        limb.Stepping = false; limb.Locked = true; limb.LockPos = curFlat; limb.LockYaw = yaw;
                    }
                }
                else if (planted && !limb.Locked && !spinning && limb.LockWeight < 0.05f)
                {
                    // Pin the foot where it is DISPLAYED (blend of the fading old pin and the animation), weight 1:
                    // continuous, no pop when a foot lands while the previous pin is still fading out.
                    limb.Locked = true;
                    limb.LockPos = limb.LockWeight > 0.001f ? Vector3.Lerp(curFlat, limb.LockPos, limb.LockWeight) : curFlat;
                    limb.LockWeight = limb.LockWeight > 0.001f ? 1f : 0f;
                    limb.LockYaw = yaw;
                }
                else if (limb.Locked)
                {
                    Vector3 toPin = limb.LockPos - curFlat;
                    float drift = toPin.magnitude;
                    if (limb.AnimLift > limb.LiftHi)
                        limb.Locked = false;                       // normal lift-off
                    else if (drift > legLen * 0.55f)
                    {
                        // Warp / extreme drift while still on the ground: do NOT re-lock at the animated spot next frame
                        // (that popped the foot ~0.9 x the offset in one frame). Slide the pin out while the weight fades
                        // and wait for the next lift before locking again.
                        limb.Locked = false;
                        limb.NeedsLift = true;
                    }
                    else
                    {
                        // Soft leash instead of a release on turns: past 28 % of the leg the pin is eased toward the
                        // animated foot (so the foot pivots instead of jumping), hard limit 40 %; a warp past 55 % releases it.
                        float soft = legLen * (standing ? 0.36f : 0.28f), hard = legLen * (standing ? 0.46f : 0.40f);
                        if (drift > soft)
                        {
                            float keep = Mathf.Lerp(drift, soft, 1f - Mathf.Exp(-10f * dt));   // the pin eases toward the foot
                            keep = Mathf.Min(keep, hard);
                            limb.LockPos = curFlat + toPin * (keep / drift);
                        }
                    }
                }
                if (limb.Stepping) { }
                else if (limb.Locked)
                {
                    limb.LockWeight = Mathf.MoveTowards(limb.LockWeight, 1f, dt / 0.06f);
                }
                else if (limb.NeedsLift)
                {
                    limb.LockPos = Vector3.MoveTowards(limb.LockPos, curFlat, 2f * scale * dt);
                    limb.LockWeight = Mathf.MoveTowards(limb.LockWeight, 0f, dt / 0.20f);
                }
                else if (limb.LockWeight > 0.001f)
                {
                    // Lift-off: fade the pin out in 0.10 s, longer when the pin is far from the animated foot (a fixed
                    // 0.10 s fade of a 1.6 m pin offset moved the brachiosaur's foot ~30 cm in one frame).
                    float off = Vector2.Distance(new Vector2(limb.LockPos.x, limb.LockPos.z), new Vector2(curFlat.x, curFlat.z));
                    float catchSpeed = Mathf.Max(3.5f, 1.5f * _ikSpeed);    // visible catch-up speed (m/s)
                    float fade = Mathf.Max(0.10f, off / catchSpeed);
                    limb.LockWeight = Mathf.MoveTowards(limb.LockWeight, 0f, dt / fade);
                }
                Vector3 horiz = limb.LockWeight > 0.001f
                    ? new Vector3(limb.LockPos.x - cur.x, 0f, limb.LockPos.z - cur.z) * limb.LockWeight
                    : Vector3.zero;

                Vector3 target = cur + Vector3.up * (limb.SmoothedOffset + stepLift) + horiz;
                if ((target - cur).sqrMagnitude > 0.000004f && limb.CanSolveTwoBone)
                {
                    Quaternion footRot = limb.Foot.rotation;      // keep the sole's orientation (toes stay flat)
                    SolveTwoBoneIK(limb.Upper, limb.Mid, limb.Foot, target, limb.UpperLen, limb.LowerLen);
                    limb.Foot.rotation = footRot;
                }

                // Slope alignment while planted.
                limb.SmoothedNormal = Vector3.Slerp(limb.SmoothedNormal, limb.LastHitNormal, ikSmooth).normalized;
                float stanceW = limb.Stance * _footIkWeight;
                if (stanceW > 0.05f)
                {
                    Quaternion slopeAlign = Quaternion.FromToRotation(Vector3.up, limb.SmoothedNormal);
                    slopeAlign.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    float clampedAngle = Mathf.Clamp(angle, -maxSlopeDeg, maxSlopeDeg) * stanceW;
                    if (Mathf.Abs(clampedAngle) > 0.25f && axis.sqrMagnitude > 0.001f)
                    {
                        limb.Foot.rotation = Quaternion.AngleAxis(clampedAngle, axis) * limb.Foot.rotation;
                    }
                }
                _limbs[i] = limb;
            }
        }

        private bool RaycastGroundIgnoringSelf(Vector3 origin, float maxDistance, out Vector3 hitPoint, out Vector3 hitNormal)
        {
            int count = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _rayBuffer,
                maxDistance,
                DinoLayerMasks.GroundMask,
                QueryTriggerInteraction.Ignore);

            float bestDist = float.MaxValue;
            int bestIdx = -1;
            for (int i = 0; i < count; i++)
            {
                Collider col = _rayBuffer[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;
                if (_rayBuffer[i].distance < bestDist)
                {
                    bestDist = _rayBuffer[i].distance;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                hitPoint = _rayBuffer[bestIdx].point;
                hitNormal = _rayBuffer[bestIdx].normal;
                return true;
            }

            hitPoint = Vector3.zero;
            hitNormal = Vector3.up;
            return false;
        }

        /// <summary>
        /// Exact analytical 2-bone limb IK preserving anatomical knee/ankle bend plane without pole flipping.
        /// </summary>
        private static void SolveTwoBoneIK(
            Transform upper,
            Transform mid,
            Transform foot,
            Vector3 targetPos,
            float lenUpper,
            float lenLower)
        {
            // Analytic two-bone IK (hip/knee angles from the law of cosines, then swing the chain onto the target).
            // The previous solver mixed cached lengths with a mis-signed swing and missed a 13 cm target by 21-26 cm,
            // so every foot correction moved the foot the wrong way. Verified: 0.0000 m error on all dinosaur legs.
            Vector3 a = upper.position;
            Vector3 b = mid.position;
            Vector3 c = foot.position;
            float lab = (b - a).magnitude;
            float lcb = (c - b).magnitude;
            if (lab < 0.001f || lcb < 0.001f) return;
            float lat = Mathf.Clamp((targetPos - a).magnitude, Mathf.Abs(lab - lcb) + 0.001f, lab + lcb - 0.001f);

            Vector3 ac = (c - a).normalized;
            float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, (b - a).normalized), -1f, 1f));
            float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
            float acAt0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac, (targetPos - a).normalized), -1f, 1f));
            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 axis0 = Vector3.Cross(c - a, b - a);
            if (axis0.sqrMagnitude < 1e-10f) return;       // fully straight leg: no bend plane to work in
            axis0.Normalize();
            Vector3 axis1 = Vector3.Cross(c - a, targetPos - a);
            axis1 = axis1.sqrMagnitude > 1e-10f ? axis1.normalized : axis0;

            upper.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis0) * upper.rotation;
            mid.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis0) * mid.rotation;
            upper.rotation = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, axis1) * upper.rotation;
        }

        // =========================================================================
        // RESILIENT BONE LOOKUP HELPERS
        // =========================================================================

        private static Transform FindExactOrFuzzy(
            Transform[] all,
            string preferredName,
            string[] fuzzyTokens,
            string[] excludeTokens = null)
        {
            if (!string.IsNullOrWhiteSpace(preferredName))
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (string.Equals(all[i].name, preferredName, StringComparison.OrdinalIgnoreCase))
                        return all[i];
                }
            }

            if (fuzzyTokens == null) return null;
            for (int t = 0; t < fuzzyTokens.Length; t++)
            {
                string tok = fuzzyTokens[t];
                for (int i = 0; i < all.Length; i++)
                {
                    string lower = all[i].name.ToLowerInvariant();
                    if (!lower.Contains(tok)) continue;
                    if (ContainsAny(lower, excludeTokens)) continue;
                    return all[i];
                }
            }
            return null;
        }

        private static Transform[] ResolveChain(Transform[] all, string[] explicitNames, string fuzzyPrefix)
        {
            var found = new List<Transform>();
            if (explicitNames != null && explicitNames.Length > 0)
            {
                for (int n = 0; n < explicitNames.Length; n++)
                {
                    if (string.IsNullOrWhiteSpace(explicitNames[n])) continue;
                    for (int i = 0; i < all.Length; i++)
                    {
                        if (string.Equals(all[i].name, explicitNames[n], StringComparison.OrdinalIgnoreCase))
                        {
                            found.Add(all[i]);
                            break;
                        }
                    }
                }
            }

            if (found.Count == 0 && !string.IsNullOrWhiteSpace(fuzzyPrefix))
            {
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].name.ToLowerInvariant().Contains(fuzzyPrefix))
                    {
                        found.Add(all[i]);
                    }
                }
                found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            }

            return found.ToArray();
        }

        private static bool ContainsAny(string text, string[] tokens)
        {
            if (tokens == null) return false;
            for (int i = 0; i < tokens.Length; i++)
            {
                if (text.Contains(tokens[i])) return true;
            }
            return false;
        }
    }
}
