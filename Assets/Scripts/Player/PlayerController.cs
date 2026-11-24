using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Player
{
    /// <summary>
    /// AAA First-Person CharacterController implementing Cretacea: Blackout movement physics,
    /// inertia, stamina, crouch-over-sprint priority, L3 sprint latch, headroom SphereCast protection
    /// (zero ceiling/wall clipping), headbob, landing dip, stealth visibility, terrain/tag surface footsteps,
    /// and GPU foliage trampling.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("Hierarchy References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private FlashlightController flashlightController;

        [Header("Movement Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 2.7f;
        [SerializeField] private float sprintSpeed = 5.7f;
        [SerializeField] private float crouchSpeed = 1.35f;
        [SerializeField] private float acceleration = 10.0f;
        [SerializeField] private float friction = 11.0f;
        [SerializeField] private float gravity = 18.0f;

        [Header("Capsule & Eye Heights")]
        [SerializeField] private float capsuleRadius = 0.34f;
        [SerializeField] private float standingHeight = 1.75f;
        [SerializeField] private float crouchedHeight = 1.05f;
        [SerializeField] private float standingEyeY = 1.62f;
        [SerializeField] private float crouchedEyeY = 0.98f;
        [SerializeField] private float crouchLerpSpeed = 10.0f;

        [Header("Stamina")]
        [SerializeField] private float sprintDrainSeconds = 7.5f;
        [SerializeField] private float staminaRegenDelay = 1.1f;
        [SerializeField] private float staminaRefillSeconds = 5.0f;
        [SerializeField] private float exhaustionRecoverThreshold = 0.35f;

        [Header("Look & Headbob")]
        [SerializeField] private float baseMouseSpeed = 0.14f;
        [SerializeField] private float baseGamepadLookSpeed = 145f;
        [SerializeField] private float minPitch = -82f;
        [SerializeField] private float maxPitch = 82f;
        [SerializeField] private float bobWalkAmp = 0.032f;
        [SerializeField] private float bobSprintAmp = 0.062f;
        [SerializeField] private float bobCrouchAmp = 0.018f;
        [SerializeField] private float landingDipMax = 0.11f;

        [Header("Terrain Splatmap Surface Order (0..7, = LookTerrainLayers)")]
        [SerializeField] private SurfaceType[] terrainLayerSurfaces =
        {
            SurfaceType.Mud,        // 0 Mud
            SurfaceType.Grass,      // 1 Grass
            SurfaceType.Gravel,     // 2 Gravel road
            SurfaceType.Concrete,   // 3 Basalt cliff
            SurfaceType.Grass,      // 4 Jungle floor (leaf litter)
            SurfaceType.Concrete,   // 5 Mossy rock
            SurfaceType.Grass,      // 6 Moss
            SurfaceType.Gravel      // 7 Sand
        };

        private static readonly int PlayerTramplePosId = Shader.PropertyToID("_PlayerTramplePos");

        private CharacterController _cc;
        private Vector3 _velocity;
        private float _yaw;
        private float _pitch;
        private float _currentHeight;
        private float _currentEyeY;

        private bool _controlEnabled = true;
        private bool _isCrouching;
        private bool _crouchOverSprint;
        private bool _sprintLatch;
        private float _stickNeutralTimer;
        private float _stamina = 1f;
        private float _staminaRegenTimer;
        private bool _exhausted;

        // Tall grass & water volume tracking
        private int _grassOverlapCount;
        private int _waterOverlapCount;
        private bool _wasGrounded = true;
        private float _airVerticalSpeed;
        private float _landingDipOffset;
        private float _bobPhase;
        private float _stepAccumulator;
        private float _stealthBroadcastTimer;

        // Scripted look override (e.g. Control Room gate breach, Lobby shutter)
        private bool _hasForcedLook;
        private Vector3 _forcedLookTarget;
        private float _forcedLookSpeed = 4.5f;

        public float SpeedMultiplier { get; set; } = 1.0f;
        public float StaminaDrainMultiplier { get; set; } = 1.0f;
        public bool IsDead { get; private set; }
        public bool IsCrouching => _isCrouching;
        public bool IsSprinting { get; private set; }
        public bool IsInGrass => _grassOverlapCount > 0;
        public bool IsExhausted => _exhausted;
        public float Stamina01 => _stamina;
        public float HorizontalSpeed => new Vector2(_velocity.x, _velocity.z).magnitude;
        public Vector3 Velocity => _velocity;
        public Transform CameraPivot => cameraPivot;
        public FlashlightController Flashlight => flashlightController;
        public Vector3 EyePosition => cameraPivot != null ? cameraPivot.position : transform.position + Vector3.up * _currentEyeY;

        public float Visibility => ComputeVisibility();
        public bool IsConcealed => IsInGrass && _isCrouching && (flashlightController == null || !flashlightController.IsBeamOn);

        private void Awake()
        {
            Instance = this;
            _cc = GetComponent<CharacterController>();
            ConfigureCharacterController();

            if (cameraPivot == null)
            {
                Transform found = transform.Find("CameraPivot");
                if (found != null) cameraPivot = found;
                else
                {
                    var pivotGo = new GameObject("CameraPivot");
                    pivotGo.transform.SetParent(transform, false);
                    cameraPivot = pivotGo.transform;
                }
            }

            if (flashlightController == null)
            {
                flashlightController = GetComponentInChildren<FlashlightController>(true);
            }

            gameObject.layer = GameLayers.Player;
            _currentHeight = standingHeight;
            _currentEyeY = standingEyeY;
            _yaw = transform.eulerAngles.y;
            cameraPivot.localPosition = new Vector3(0f, _currentEyeY, 0f);
        }

        private void Start()
        {
            CretaceaInput.EnsureInstance();
            GameState.EnsureInstance();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ConfigureCharacterController()
        {
            _cc.radius = capsuleRadius;
            _cc.height = standingHeight;
            _cc.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            _cc.stepOffset = 0.35f;
            _cc.slopeLimit = 48f;
            _cc.skinWidth = 0.04f;
        }

        private void Update()
        {
            Shader.SetGlobalVector(PlayerTramplePosId, new Vector4(transform.position.x, transform.position.y, transform.position.z, 1.15f));

            GameState gs = GameState.Instance;
            if (IsDead || (gs != null && gs.IsPaused)) return;

            bool allowInput = _controlEnabled && (gs == null || !gs.IsGameplayInputBlocked);

            if (allowInput)
            {
                HandleLookInput();
                HandlePostureAndSprintInput();
                HandleMovement();
            }
            else
            {
                IsSprinting = false;
                ApplyGravityOnly();
                if (_hasForcedLook)
                {
                    ApplyForcedLook(Time.deltaTime);
                }
            }

            UpdateCapsuleAndHeadbob();
            UpdateStealthBroadcast();
        }

        private void HandleLookInput()
        {
            if (_hasForcedLook)
            {
                ApplyForcedLook(Time.deltaTime);
                return;
            }

            CretaceaInput input = CretaceaInput.Instance;
            if (input == null) return;

            SettingsData settings = SaveManager.Settings;
            float invSign = settings.invertY ? 1f : -1f;

            Vector2 mouse = input.LookMouseDelta;
            Vector2 stick = input.LookStick;

            float yawDelta = mouse.x * baseMouseSpeed * settings.mouseSensitivity;
            float pitchDelta = mouse.y * baseMouseSpeed * settings.mouseSensitivity * invSign;

            if (stick.sqrMagnitude > 0.01f)
            {
                // Smooth quadratic stick response for precise dark-room aiming
                float mag = stick.magnitude;
                Vector2 shaped = stick.normalized * (mag * mag);
                yawDelta += shaped.x * baseGamepadLookSpeed * settings.gamepadSensitivity * Time.deltaTime;
                pitchDelta += shaped.y * baseGamepadLookSpeed * settings.gamepadSensitivity * invSign * Time.deltaTime;
            }

            _yaw = Mathf.Repeat(_yaw + yawDelta, 360f);
            _pitch = Mathf.Clamp(_pitch + pitchDelta, minPitch, maxPitch);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void ApplyForcedLook(float dt)
        {
            Vector3 toTarget = _forcedLookTarget - EyePosition;
            if (toTarget.sqrMagnitude < 0.001f) return;

            Quaternion targetWorldRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            Vector3 euler = targetWorldRot.eulerAngles;
            float targetPitch = euler.x > 180f ? euler.x - 360f : euler.x;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            _yaw = Mathf.LerpAngle(_yaw, euler.y, 1f - Mathf.Exp(-_forcedLookSpeed * dt));
            _pitch = Mathf.Lerp(_pitch, targetPitch, 1f - Mathf.Exp(-_forcedLookSpeed * dt));

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandlePostureAndSprintInput()
        {
            CretaceaInput input = CretaceaInput.Instance;
            if (input == null) return;

            Vector2 move = input.Move;
            bool pushingForward = move.y > 0.6f;

            // L3 sprint latch toggle (drops on neutral stick > 0.3s, crouch, or exhaustion)
            if (input.SprintLatchPressedThisFrame)
            {
                _sprintLatch = !_sprintLatch;
                if (_sprintLatch && _isCrouching && HasStandingHeadroom())
                {
                    SetCrouching(false);
                }
            }

            if (move.sqrMagnitude < 0.04f)
            {
                _stickNeutralTimer += Time.deltaTime;
                if (_stickNeutralTimer > 0.3f) _sprintLatch = false;
            }
            else
            {
                _stickNeutralTimer = 0f;
            }

            // Fresh sprint press stands up a crouched player if headroom is clear
            if (input.SprintPressedThisFrame && _isCrouching && HasStandingHeadroom())
            {
                SetCrouching(false);
                _crouchOverSprint = false;
            }

            if (!input.IsSprintHeld)
            {
                _crouchOverSprint = false;
            }

            // Crouch toggle; pressing Crouch while holding Shift/LB latches crouch-over-sprint!
            if (input.CrouchPressedThisFrame)
            {
                if (_isCrouching)
                {
                    if (HasStandingHeadroom())
                    {
                        SetCrouching(false);
                        _crouchOverSprint = false;
                    }
                }
                else
                {
                    SetCrouching(true);
                    _sprintLatch = false;
                    if (input.IsSprintHeld)
                    {
                        _crouchOverSprint = true;
                    }
                }
            }

            bool wantsSprint = (input.IsSprintHeld || _sprintLatch) && !_crouchOverSprint && !_isCrouching && pushingForward;
            bool canSprint = wantsSprint && !_exhausted && _stamina > 0.01f;
            IsSprinting = canSprint;

            if (IsSprinting && HorizontalSpeed > 0.5f)
            {
                float drain = (Time.deltaTime / Mathf.Max(0.5f, sprintDrainSeconds)) * StaminaDrainMultiplier;
                _stamina = Mathf.Max(0f, _stamina - drain);
                _staminaRegenTimer = staminaRegenDelay;

                if (_stamina <= 0f)
                {
                    _exhausted = true;
                    _sprintLatch = false;
                    IsSprinting = false;
                    GameEvents.RaisePlaySound("foley_breath_exhausted");
                }
                GameEvents.RaiseStaminaChanged(_stamina, _exhausted);
            }
            else
            {
                if (_staminaRegenTimer > 0f)
                {
                    _staminaRegenTimer -= Time.deltaTime;
                }
                else if (_stamina < 1f)
                {
                    _stamina = Mathf.Min(1f, _stamina + Time.deltaTime / Mathf.Max(0.5f, staminaRefillSeconds));
                    if (_exhausted && _stamina >= exhaustionRecoverThreshold)
                    {
                        _exhausted = false;
                    }
                    GameEvents.RaiseStaminaChanged(_stamina, _exhausted);
                }
            }
        }

        private void HandleMovement()
        {
            CretaceaInput input = CretaceaInput.Instance;
            Vector2 move = input != null ? input.Move : Vector2.zero;
            if (move.sqrMagnitude > 1f) move.Normalize();

            Vector3 wishDir = transform.right * move.x + transform.forward * move.y;
            float baseSpeed = _isCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            Vector3 targetHoriz = wishDir * (baseSpeed * SpeedMultiplier);

            Vector3 horiz = new Vector3(_velocity.x, 0f, _velocity.z);
            float rate = wishDir.sqrMagnitude > 0.001f ? acceleration : friction;
            horiz = Vector3.Lerp(horiz, targetHoriz, 1f - Mathf.Exp(-rate * Time.deltaTime));

            bool grounded = _cc.isGrounded;
            if (grounded && _velocity.y < 0f)
            {
                if (!_wasGrounded && _airVerticalSpeed < -3.0f)
                {
                    _landingDipOffset = -Mathf.Clamp(Mathf.Abs(_airVerticalSpeed) * 0.015f, 0.02f, landingDipMax);
                    EmitFootstep(forceLanding: true);
                }
                _velocity.y = -2.2f;
                _airVerticalSpeed = 0f;
            }
            else
            {
                _velocity.y -= gravity * Time.deltaTime;
                _airVerticalSpeed = _velocity.y;
            }

            _wasGrounded = grounded;
            _velocity.x = horiz.x;
            _velocity.z = horiz.z;

            _cc.Move(_velocity * Time.deltaTime);

            // Step cadence based on actual displaced horizontal speed
            float actualSpeed = new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude;
            if (_cc.isGrounded && actualSpeed > 0.35f)
            {
                float strideMeters = _isCrouching ? 1.15f : (IsSprinting ? 2.15f : 1.55f);
                _stepAccumulator += actualSpeed * Time.deltaTime;
                if (_stepAccumulator >= strideMeters)
                {
                    _stepAccumulator -= strideMeters;
                    EmitFootstep(forceLanding: false);
                }
            }
            else
            {
                _stepAccumulator = 0f;
            }
        }

        private void ApplyGravityOnly()
        {
            _velocity.x = Mathf.Lerp(_velocity.x, 0f, 1f - Mathf.Exp(-friction * Time.deltaTime));
            _velocity.z = Mathf.Lerp(_velocity.z, 0f, 1f - Mathf.Exp(-friction * Time.deltaTime));
            if (_cc.isGrounded && _velocity.y < 0f) _velocity.y = -2f;
            else _velocity.y -= gravity * Time.deltaTime;
            _cc.Move(_velocity * Time.deltaTime);
        }

        private void UpdateCapsuleAndHeadbob()
        {
            float targetH = _isCrouching ? crouchedHeight : standingHeight;
            float targetEye = _isCrouching ? crouchedEyeY : standingEyeY;

            _currentHeight = Mathf.Lerp(_currentHeight, targetH, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));
            _currentEyeY = Mathf.Lerp(_currentEyeY, targetEye, 1f - Mathf.Exp(-crouchLerpSpeed * Time.deltaTime));

            _cc.height = _currentHeight;
            _cc.center = new Vector3(0f, _currentHeight * 0.5f, 0f);

            // Landing dip recovery
            _landingDipOffset = Mathf.Lerp(_landingDipOffset, 0f, 1f - Mathf.Exp(-12f * Time.deltaTime));

            // Organic figure-8 headbob when moving on ground
            float horizSpeed = _cc.isGrounded ? new Vector2(_cc.velocity.x, _cc.velocity.z).magnitude : 0f;
            Vector3 bobOffset = Vector3.zero;
            if (horizSpeed > 0.25f && _controlEnabled)
            {
                float freq = _isCrouching ? 5.8f : (IsSprinting ? 11.2f : 7.8f);
                float amp = _isCrouching ? bobCrouchAmp : (IsSprinting ? bobSprintAmp : bobWalkAmp);
                _bobPhase += Time.deltaTime * freq;
                bobOffset.y = Mathf.Sin(_bobPhase) * amp;
                bobOffset.x = Mathf.Cos(_bobPhase * 0.5f) * amp * 0.55f;
            }
            else
            {
                _bobPhase = Mathf.Lerp(_bobPhase, 0f, Time.deltaTime * 4f);
            }

            cameraPivot.localPosition = new Vector3(bobOffset.x, _currentEyeY + bobOffset.y + _landingDipOffset, 0f);
        }

        /// <summary>
        /// Prevents clipping into ceilings, jammed roller doors, or bent Service Gate leaves when standing up.
        /// </summary>
        public bool HasStandingHeadroom()
        {
            float checkRadius = capsuleRadius * 0.88f;
            float castDistance = (standingHeight - crouchedHeight) + 0.06f;
            Vector3 origin = transform.position + Vector3.up * (crouchedHeight - checkRadius);
            return !Physics.SphereCast(origin, checkRadius, Vector3.up, out _, castDistance, GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore);
        }

        private void SetCrouching(bool crouching)
        {
            if (_isCrouching == crouching) return;
            _isCrouching = crouching;
            GameEvents.RaiseCrouchChanged(crouching);
            GameEvents.RaisePlaySound(crouching ? "foley_crouch" : "foley_stand_up", transform.position, 0.85f);
        }

        private void EmitFootstep(bool forceLanding)
        {
            SurfaceType surface = DetectSurfaceBelow(out float noiseMult);
            GameEvents.RaisePlayerFootstep(surface, transform.position, IsSprinting || forceLanding, _isCrouching);

            float baseRadius = _isCrouching ? 3.0f : (IsSprinting ? 24.0f : 9.0f);
            if (forceLanding) baseRadius = Mathf.Max(baseRadius, 12.0f);
            GameEvents.RaiseNoiseEmitted(transform.position, baseRadius * noiseMult, "footstep");
        }

        /// <summary>
        /// Resolves exact surface beneath feet: Water overlap -> SurfaceTag -> PhysicsMaterial name -> Terrain Splatmap.
        /// Eliminates Godot's AUD-3 bug where tile/gravel/metal steps never fired!
        /// </summary>
        public SurfaceType DetectSurfaceBelow(out float noiseMultiplier)
        {
            if (_waterOverlapCount > 0)
            {
                noiseMultiplier = SurfaceTag.GetDefaultNoiseMultiplier(SurfaceType.Water);
                return SurfaceType.Water;
            }

            Vector3 rayOrigin = transform.position + Vector3.up * 0.25f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 1.4f, GameLayers.GroundMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.TryGetComponent(out ISurfaceProvider provider))
                {
                    noiseMultiplier = provider.NoiseMultiplier;
                    return provider.Surface;
                }

                // Check parent surface tag for compound meshes
                ISurfaceProvider parentProvider = hit.collider.GetComponentInParent<ISurfaceProvider>();
                if (parentProvider != null)
                {
                    noiseMultiplier = parentProvider.NoiseMultiplier;
                    return parentProvider.Surface;
                }

                // Check PhysicsMaterial naming convention (PM_Concrete, PM_Tile, PM_Gravel, PM_Metal, PM_Mud, PM_Grass, PM_Water)
                PhysicsMaterial pm = hit.collider.sharedMaterial;
                if (pm != null && TryParseSurfaceName(pm.name, out SurfaceType fromPm))
                {
                    noiseMultiplier = SurfaceTag.GetDefaultNoiseMultiplier(fromPm);
                    return fromPm;
                }

                // Check ProjectSettings custom surface tag convention (Surface_Concrete, Surface_Tile, ...)
                string colTag = hit.collider.tag;
                if (!string.IsNullOrEmpty(colTag) && colTag.StartsWith("Surface_") && TryParseSurfaceName(colTag, out SurfaceType fromTag))
                {
                    noiseMultiplier = SurfaceTag.GetDefaultNoiseMultiplier(fromTag);
                    return fromTag;
                }

                // Check Unity Terrain dominant splatmap layer
                Terrain terrain = hit.collider.GetComponent<Terrain>();
                if (terrain != null && terrain.terrainData != null && terrain.terrainData.alphamapLayers > 0)
                {
                    SurfaceType terrainSurf = SampleTerrainDominantSurface(terrain, hit.point);
                    noiseMultiplier = SurfaceTag.GetDefaultNoiseMultiplier(terrainSurf);
                    return terrainSurf;
                }
            }

            noiseMultiplier = 1.0f;
            return SurfaceType.Concrete;
        }

        private SurfaceType SampleTerrainDominantSurface(Terrain terrain, Vector3 worldPos)
        {
            TerrainData td = terrain.terrainData;
            Vector3 rel = worldPos - terrain.transform.position;
            int mapX = Mathf.Clamp(Mathf.FloorToInt((rel.x / td.size.x) * td.alphamapWidth), 0, td.alphamapWidth - 1);
            int mapZ = Mathf.Clamp(Mathf.FloorToInt((rel.z / td.size.z) * td.alphamapHeight), 0, td.alphamapHeight - 1);

            float[,,] weights = td.GetAlphamaps(mapX, mapZ, 1, 1);
            int bestIndex = 0;
            float bestWeight = 0f;
            int layers = td.alphamapLayers;
            for (int i = 0; i < layers; i++)
            {
                if (weights[0, 0, i] > bestWeight)
                {
                    bestWeight = weights[0, 0, i];
                    bestIndex = i;
                }
            }

            if (terrainLayerSurfaces != null && bestIndex < terrainLayerSurfaces.Length)
                return terrainLayerSurfaces[bestIndex];
            return SurfaceType.Grass;
        }

        private static bool TryParseSurfaceName(string matName, out SurfaceType surface)
        {
            string lower = matName.ToLowerInvariant();
            if (lower.Contains("tile")) { surface = SurfaceType.Tile; return true; }
            if (lower.Contains("gravel")) { surface = SurfaceType.Gravel; return true; }
            if (lower.Contains("metal") || lower.Contains("grate") || lower.Contains("steel")) { surface = SurfaceType.Metal; return true; }
            if (lower.Contains("mud") || lower.Contains("dirt")) { surface = SurfaceType.Mud; return true; }
            if (lower.Contains("grass") || lower.Contains("foliage")) { surface = SurfaceType.Grass; return true; }
            if (lower.Contains("water") || lower.Contains("stream")) { surface = SurfaceType.Water; return true; }
            if (lower.Contains("concrete") || lower.Contains("asphalt") || lower.Contains("stone")) { surface = SurfaceType.Concrete; return true; }
            surface = SurfaceType.Concrete;
            return false;
        }

        public float ComputeVisibility()
        {
            bool lightOn = flashlightController != null && flashlightController.IsBeamOn;
            float crouchMult = _isCrouching ? 0.55f : 1.0f;
            float grassMult = IsInGrass ? (_isCrouching ? 0.18f : 0.50f) : 1.0f;
            float lightMult = lightOn ? 1.90f : 1.0f;
            float speedMult = 0.70f + 0.60f * Mathf.Clamp01(HorizontalSpeed / Mathf.Max(0.1f, sprintSpeed));
            return crouchMult * grassMult * lightMult * speedMult;
        }

        private void UpdateStealthBroadcast()
        {
            _stealthBroadcastTimer -= Time.deltaTime;
            if (_stealthBroadcastTimer <= 0f)
            {
                _stealthBroadcastTimer = 0.1f;
                GameEvents.RaiseStealthVisibilityChanged(ComputeVisibility(), IsConcealed);
            }
        }

        public void SetControlEnabled(bool enabled)
        {
            _controlEnabled = enabled;
            if (!enabled)
            {
                IsSprinting = false;
                _sprintLatch = false;
                _velocity.x = 0f;
                _velocity.z = 0f;
            }
        }

        public void Teleport(Vector3 worldPos, float yawDegrees, float pitchDegrees = 0f)
        {
            bool prevEnabled = _cc.enabled;
            _cc.enabled = false;
            transform.position = worldPos;
            _yaw = Mathf.Repeat(yawDegrees, 360f);
            _pitch = Mathf.Clamp(pitchDegrees, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            _velocity = Vector3.zero;
            _isCrouching = false;
            _crouchOverSprint = false;
            _sprintLatch = false;
            _currentHeight = standingHeight;
            _currentEyeY = standingEyeY;
            _cc.height = standingHeight;
            _cc.center = new Vector3(0f, standingHeight * 0.5f, 0f);
            _cc.enabled = prevEnabled;
        }

        public void ResetStaminaFull()
        {
            _stamina = 1f;
            _exhausted = false;
            _staminaRegenTimer = 0f;
            GameEvents.RaiseStaminaChanged(_stamina, false);
        }

        public void LookTowards(Vector3 worldTarget, float turnSpeed = 3.5f)
        {
            _hasForcedLook = true;
            _forcedLookTarget = worldTarget;
            _forcedLookSpeed = Mathf.Max(0.5f, turnSpeed);
        }

        public void ReleaseLook() => _hasForcedLook = false;

        public void KillPlayer(Vector3 attackerPosition)
        {
            if (IsDead) return;
            IsDead = true;
            SetControlEnabled(false);
            LookTowards(attackerPosition + Vector3.up * 1.4f, 9f);
            flashlightController?.SetCutsceneSuppressed(true);
            GameEvents.RaiseDamageFlash(1.0f);
            GameEvents.RaiseCameraTrauma(0.95f, 1.0f);
            GameState.EnsureInstance().RecordDeath();
        }

        public void ReviveForCheckpoint()
        {
            IsDead = false;
            ReleaseLook();
            SpeedMultiplier = 1.0f;
            StaminaDrainMultiplier = 1.0f;
            ResetStaminaFull();
            _grassOverlapCount = 0;
            _waterOverlapCount = 0;
            flashlightController?.SetCutsceneSuppressed(false);
            SetControlEnabled(true);
            GameState.EnsureInstance().ClearGameOver();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<TallGrassZoneVolume>() != null)
            {
                if (_grassOverlapCount == 0)
                {
                    GameEvents.RaisePlaySound("foley_grass_enter", transform.position, 0.85f);
                }
                _grassOverlapCount++;
            }
            else if (other.TryGetComponent(out ISurfaceProvider surf) && surf.Surface == SurfaceType.Water)
            {
                _waterOverlapCount++;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<TallGrassZoneVolume>() != null)
            {
                _grassOverlapCount = Mathf.Max(0, _grassOverlapCount - 1);
            }
            else if (other.TryGetComponent(out ISurfaceProvider surf) && surf.Surface == SurfaceType.Water)
            {
                _waterOverlapCount = Mathf.Max(0, _waterOverlapCount - 1);
            }
        }
    }
}
