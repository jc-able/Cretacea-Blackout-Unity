using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Rendering;

namespace Cretacea.Player
{
    /// <summary>
    /// IRONCLAD Flashlight & FPS Viewmodel Controller eliminating Godot bugs FL-1, FL-2, FL-3, and COL-4:
    /// 1. HasFlashlight defaults FALSE on Chapter 1 start -> viewmodelRoot.SetActive(false) strictly.
    /// 2. SetCutsceneSuppressed(true) hides viewmodel & beam unconditionally during Opening & Dawn Ending cutscenes.
    /// 3. Ending a cutscene NEVER shows the viewmodel unless HasFlashlight == true.
    /// 4. Assigns viewmodel hierarchy to Layer 12 (Viewmodel Custom Pass) AND runs a 0.65 m forward SphereCast
    ///    wall-pullback + spring sway so the hand/flashlight NEVER clips through walls or doors.
    /// 5. HDRP Volumetric SpotLight + soft bounce fill + horror proximity/scripted electrical flicker.
    /// </summary>
    public sealed class FlashlightController : MonoBehaviour
    {
        [Header("Hierarchy & Rig")]
        [SerializeField] private GameObject viewmodelRoot;
        [SerializeField] private Transform swayPivot;
        [SerializeField] private Transform cameraReference;
        [SerializeField] private PlayerController playerController;

        [Header("HDRP Lights")]
        [SerializeField] private Light spotLight;
        [SerializeField] private Light bounceFillLight;
        [SerializeField] private Texture flashlightCookie;
        // Legacy beam values, kept only so the prefab and generated scenes keep deserializing (see "Beam look" below).
#pragma warning disable CS0414
        [SerializeField] private float spotRange = 34f;
        [SerializeField] private float spotOuterAngle = 25f;
        [SerializeField] private float spotInnerAngle = 14f;
        [SerializeField] private float spotIntensityLumens = 3200f;
        [SerializeField] private float spotColorTempK = 5200f;
        [SerializeField] private float spotVolumetricDimmer = 1.6f;
        [SerializeField] private float bounceFillLumens = 4.5f;
#pragma warning restore CS0414

        [Header("Anti-Wall-Clip Pullback & Spring Sway")]
        [SerializeField] private float wallCheckDistance = 0.65f;
        [SerializeField] private float wallCheckRadius = 0.14f;
        [SerializeField] private float maxWallPullbackZ = 0.22f;
        [SerializeField] private float maxWallPitchDownDeg = 18f;
        [SerializeField] private float swayAmount = 0.012f;
        [SerializeField] private float swaySmooth = 10f;
        [SerializeField] private float equipRaiseDuration = 0.45f;

        [Header("Tension Flicker")]
        [SerializeField] private float terrorFlickerThresholdDist = 14f;

        private HDAdditionalLightData _spotHd;
        private HDAdditionalLightData _fillHd;

        private bool _hasFlashlight;
        private bool _isBeamOn;
        private bool _cutsceneSuppressed;
        private float _scriptedFlickerTimer;
        private float _scriptedFlickerStrength;
        private float _terrorProximity01;

        private Vector3 _restLocalPos;
        private Quaternion _restLocalRot;
        private float _currentWallBlend;
        private float _equipProgress = 1f;
        private float _sprintLowerBlend;
        private float _clickRecoil;
        private Vector3 _swayPos;
        private float _walkBobT;

        public bool HasFlashlight => _hasFlashlight;
        public bool IsBeamOn => _isBeamOn && ShouldShowViewmodel;
        public bool IsCutsceneSuppressed => _cutsceneSuppressed;

        /// <summary>Single source of truth for FPS viewmodel visibility across all chapters & cutscenes.</summary>
        public bool ShouldShowViewmodel
        {
            get
            {
                if (!_hasFlashlight || _cutsceneSuppressed) return false;
                if (playerController != null && playerController.IsDead) return false;
                GameState gs = GameState.Instance;
                if (gs != null && (gs.InCutscene || gs.IsMainMenu || gs.IsGameOver)) return false;
                return true;
            }
        }

        private void Awake()
        {
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
            if (cameraReference == null && playerController != null) cameraReference = playerController.CameraPivot;
            if (swayPivot == null && viewmodelRoot != null) swayPivot = viewmodelRoot.transform;

            if (swayPivot != null)
            {
                _restLocalPos = swayPivot.localPosition;
                _restLocalRot = swayPivot.localRotation;
            }

            if (viewmodelRoot != null)
            {
                ViewmodelCameraConfig.SetHierarchyToViewmodelLayer(viewmodelRoot);
            }

            EnsureViewmodelRenderingPipeline();
            ConfigureHdrpLights();

            // Ironclad safe initial state: strictly OFF until StoryDirector or Pickup grants it.
            _hasFlashlight = false;
            _isBeamOn = false;
            ApplyVisibilityAndBeamImmediate();
        }

        private void EnsureViewmodelRenderingPipeline()
        {
            Camera mainCam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (mainCam != null)
            {
                ViewmodelCameraConfig cfg = mainCam.GetComponent<ViewmodelCameraConfig>();
                if (cfg != null) cfg.Apply();
                else ViewmodelCameraConfig.ExcludeViewmodelLayer(mainCam);
            }

            if (Application.isPlaying)
            {
                ViewmodelCameraConfig.EnsureGlobalViewmodelCustomPassVolume();
            }
        }

        private void OnEnable()
        {
            GameEvents.OnCutsceneStateChanged += OnCutsceneStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnCutsceneStateChanged -= OnCutsceneStateChanged;
        }

        // ---- Beam look (the Look Pass owns these; the old serialized lumen fields are ignored) -----------------------
        // A real torch: a ~10 degree hot spot inside a wide soft spill (the cookie draws both), plus one bounce of
        // the light off whatever the beam hits, so a dark room reads as a room and not as a disc on a wall.
        // 12,000 cd since round 3 (7,200 before): with the bounce cut to its real size the beam itself has to light
        // the road ahead. The hot-spot budget below still holds it down on anything close.
        public static float BeamCandela = 12000f;        // on-axis intensity; the cookie drops the spill to ~6 %
        private const float BeamOuterAngle = 76f;
        private const float BeamRange = 46f;
        private const float BeamKelvin = 5600f;
        private const float BeamVolumetric = 0.42f;      // visible shaft in rain and haze without whiting out
        // Review 3: in a dry hall the 12,000 cd shaft was the brightest thing on screen (it read as smoke at the hand).
        public static float BeamVolumetricIndoor = 0.22f;
        // Review 2 (Nov 20): at 0.034 the bounce lit a dark office to 79 % of the lit one and put a brighter patch on
        // the ceiling than the beam put on the wall. What a wall really sends back is about 1 % of the beam's candela.
        public static float BounceShare = 0.004f;        // candela of the bounce light per candela of beam (the screen-space bounce carries the rest)
        public static float BounceRange = 9f;
        public static float BounceMinStandoff = 1.5f;    // a light closer than this draws its own pool on the floor by the wall
        public static float BounceCeilingGap = 1.0f;     // never parked right under a ceiling
        // Review 3: as a point light the bounce lit the ceiling above it as brightly as the beam lit the wall (measured
        // in the Records room: ceiling 119/255 with it, 89 without, wall pool 138). A wall sends light back into the
        // room, not along itself: the bounce is a wide cone along the surface normal, so the wall it stands off and the
        // ceiling over it get nothing from it.
        public static float BounceCone = 165f;
        // Eye adaptation for the hot spot: a real 7200 cd torch puts 7000 lux on a wall one metre away, which no
        // camera exposure can hold together with the rest of the room. The beam is dimmed so that the hot spot (beam
        // plus its own bounce) never lands brighter than this on the surface it points at; the shaft in the haze keeps
        // its full strength.
        // Review 3: one lux budget for every surface clipped on white tile (1,300 px at 250+) while dark brick stayed
        // dull. What the eye sees is lux x albedo, so the budget follows the albedo of what the beam lands on
        // (SurfaceAlbedoMap, baked by the Look Pass). Bright surfaces are also the glossy ones (glazed tile, enamel),
        // so the budget falls a little faster than 1/albedo: 120 lux on brick (0.25), 40 on white tile (0.52).
        public static float HotSpotLuxAtRef = 120f;      // lux the hot spot may put on a surface of the reference albedo
        public static float HotSpotRefAlbedo = 0.25f;
        public static float HotSpotAlbedoPower = 1.5f;
        public static float HotSpotMinLux = 30f;
        public static float HotSpotMaxLux = 140f;
        public static float HotSpotDefaultAlbedo = 0.35f;   // unknown surface (creatures, anything without a renderer on its collider)
        public static float HotSpotTerrainAlbedo = 0.25f;   // gravel, mud, grass
        public static float AdaptMin = 0.004f;           // nose to a wall the disc still holds its texture
        // The arm and the torch body in the dark: a real torch throws a little light back on the hand that holds it.
        // Without it the glove was a black cut-out (6/255) in every blackout frame.
        public static float ViewmodelFillCandela = 7f;      // review 4: from above it lit the glove like a rag; now below and in front (lens spill and lit ground)
        public const uint ViewmodelFillLayer = 1u << 5;     // rendering layer shared by the fill light and the viewmodel only
        private static readonly Vector2[] AdaptRays = { new Vector2(0.06f, 0f), new Vector2(-0.06f, 0f), new Vector2(0f, 0.06f), new Vector2(0f, -0.06f) };
        private readonly float[] _adaptDistances = new float[5];

        private float _beamFactor = 1f;
        private float _adapt = 1f;
        private float _albedo = 0.35f;
        private Light _viewmodelFill;
        private Vector3 _bounceDir = Vector3.up;
        private Vector3 _bouncePos;
        private float _bounceLevel;
        private bool _bounceDetached;

        private void ConfigureHdrpLights()
        {
            if (spotLight != null)
            {
                spotLight.type = LightType.Spot;
                spotLight.lightUnit = LightUnit.Candela;
                spotLight.range = BeamRange;
                spotLight.spotAngle = BeamOuterAngle;
                spotLight.innerSpotAngle = BeamOuterAngle * 0.96f;   // the cookie shapes the falloff
                spotLight.intensity = BeamCandela;
                spotLight.useColorTemperature = true;
                spotLight.colorTemperature = BeamKelvin;
                spotLight.color = new Color(1.0f, 0.985f, 0.96f);
                spotLight.shadows = LightShadows.Soft;
                if (flashlightCookie != null) spotLight.cookie = flashlightCookie;

                _spotHd = spotLight.GetComponent<HDAdditionalLightData>();
                if (_spotHd != null)
                {
                    _spotHd.affectsVolumetric = true;
                    _spotHd.SetLightDimmer(1f, BeamVolumetric);
                    _spotHd.shadowResolution.useOverride = true;
                    _spotHd.shadowResolution.@override = 1024;
                    _spotHd.shadowUpdateMode = ShadowUpdateMode.EveryFrame;
                    _spotHd.shadowNearPlane = 0.25f;                 // skip the torch body and the hand
                    _spotHd.useContactShadow.useOverride = true;
                    _spotHd.useContactShadow.@override = true;
                    _spotHd.volumetricShadowDimmer = 1f;
                }
            }

            if (bounceFillLight != null)
            {
                bounceFillLight.type = LightType.Spot;
                bounceFillLight.spotAngle = BounceCone;
                bounceFillLight.innerSpotAngle = 0f;                  // fades smoothly to the rim: no edge on the floor
                bounceFillLight.lightUnit = LightUnit.Candela;
                bounceFillLight.range = BounceRange;
                bounceFillLight.intensity = 0f;
                bounceFillLight.useColorTemperature = true;
                bounceFillLight.colorTemperature = BeamKelvin - 600f;   // a bounce picks up a little warmth
                bounceFillLight.color = Color.white;
                bounceFillLight.shadows = LightShadows.None;
                _fillHd = bounceFillLight.GetComponent<HDAdditionalLightData>();
                if (_fillHd != null)
                {
                    _fillHd.affectsVolumetric = false;
                    _fillHd.affectSpecular = false;                    // no second highlight on wet floors
                }
            }

            // the arm and the torch never shadow their own beam, never take floor decals (the E-0 hazard stripes were
            // printed across the glove) and share one rendering layer with their own small fill light
            if (viewmodelRoot != null)
            {
                foreach (Renderer r in viewmodelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.renderingLayerMask |= ViewmodelFillLayer;
                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null || !m.HasProperty(SupportDecalsId) || m.GetFloat(SupportDecalsId) < 0.5f) continue;
                        m.SetFloat(SupportDecalsId, 0f);
                        m.EnableKeyword("_DISABLE_DECALS");
                    }
                }
                EnsureViewmodelFill();
            }
        }

        private static readonly int SupportDecalsId = Shader.PropertyToID("_SupportDecals");

        private void EnsureViewmodelFill()
        {
            if (_viewmodelFill != null || spotLight == null) return;
            var go = new GameObject("ViewmodelFill");
            go.transform.SetParent(spotLight.transform, false);
            go.transform.localPosition = new Vector3(0.02f, -0.32f, -0.06f);   // under the lens, a little in front of the hand
            go.transform.localRotation = Quaternion.identity;
            _viewmodelFill = go.AddComponent<Light>();
            HDAdditionalLightData hd = go.AddComponent<HDAdditionalLightData>();
            _viewmodelFill.type = LightType.Point;
            _viewmodelFill.lightUnit = LightUnit.Candela;
            _viewmodelFill.range = 1.6f;
            _viewmodelFill.intensity = 0f;
            _viewmodelFill.useColorTemperature = true;
            _viewmodelFill.colorTemperature = 5600f;
            _viewmodelFill.color = Color.white;
            _viewmodelFill.shadows = LightShadows.None;
            _viewmodelFill.renderingLayerMask = (int)ViewmodelFillLayer;      // the arm and the torch only
            hd.affectsVolumetric = false;
            hd.affectSpecular = false;
            _viewmodelFill.enabled = false;
        }

        // ---- What the beam landed on: mean albedo of the surface's material, measured once per material ---------------
        private static readonly System.Collections.Generic.Dictionary<Material, float> s_Albedo = new System.Collections.Generic.Dictionary<Material, float>();
        private static readonly System.Collections.Generic.Dictionary<Collider, Material> s_ColliderMaterial = new System.Collections.Generic.Dictionary<Collider, Material>();
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetAlbedoCache()
        {
            s_Albedo.Clear();
            s_ColliderMaterial.Clear();
        }

        private static float Luma(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

        /// <summary>Mean diffuse albedo (linear luminance) of the surface a ray hit. Unknown surfaces get a mid value.</summary>
        public static float SurfaceAlbedo(Collider col)
        {
            if (col == null) return HotSpotDefaultAlbedo;
            if (col is TerrainCollider) return HotSpotTerrainAlbedo;
            if (SurfaceAlbedoMap.TryGet(col, out float baked)) return baked;   // everything the world builder placed
            // what is left: things spawned at run time
            if (!s_ColliderMaterial.TryGetValue(col, out Material mat))
            {
                Renderer r = col.GetComponent<Renderer>();
                if (r == null) r = col.GetComponentInChildren<Renderer>();
                mat = r != null ? r.sharedMaterial : null;
                s_ColliderMaterial[col] = mat;
            }
            if (mat == null) return HotSpotDefaultAlbedo;
            if (s_Albedo.TryGetValue(mat, out float known)) return known;

            Color tint = mat.HasProperty(BaseColorId) ? mat.GetColor(BaseColorId).linear : Color.gray;
            Texture tex = mat.HasProperty(BaseColorMapId) ? mat.GetTexture(BaseColorMapId)
                        : mat.HasProperty(MainTexId) ? mat.GetTexture(MainTexId) : null;
            if (!(tex is Texture2D))
            {
                float flat = Mathf.Clamp(Luma(tint), 0.03f, 0.95f);
                s_Albedo[mat] = flat;
                return flat;
            }

            // Until the measurement is back: the tint over a mid-grey texture.
            float guess = Mathf.Clamp(Luma(tint) * 0.5f, 0.03f, 0.95f);
            s_Albedo[mat] = guess;
            // An 8x8 target makes the GPU sample the small mips, so 64 texels are the mean of the whole map.
            RenderTexture rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            Material key = mat;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBAHalf, req =>
            {
                RenderTexture.ReleaseTemporary(rt);
                if (req.hasError || key == null) return;
                Unity.Collections.NativeArray<ushort> px = req.GetData<ushort>();
                float r = 0f, g = 0f, b = 0f;
                int n = px.Length / 4;
                for (int i = 0; i < n; i++)
                {
                    r += Mathf.HalfToFloat(px[i * 4]);
                    g += Mathf.HalfToFloat(px[i * 4 + 1]);
                    b += Mathf.HalfToFloat(px[i * 4 + 2]);
                }
                if (n == 0) return;
                var mean = new Color(r / n * tint.r, g / n * tint.g, b / n * tint.b);
                s_Albedo[key] = Mathf.Clamp(Luma(mean), 0.03f, 0.95f);
            });
            return guess;
        }

        /// <summary>For the look tools: the albedo the hot-spot budget is using right now, and the beam's dimming.</summary>
        public string DebugBudget() { return "albedo " + _albedo.ToString("F3") + " adapt " + _adapt.ToString("F4") + " materials " + s_Albedo.Count; }

        /// <summary>One diffuse bounce: a soft point light where the hot spot lands.</summary>
        private void UpdateBounceLight()
        {
            if (bounceFillLight == null || spotLight == null) return;
            if (!bounceFillLight.enabled) { _bounceLevel = 0f; return; }

            if (!_bounceDetached)
            {
                // world-driven from now on (it lives where the beam lands, not in the hand)
                bounceFillLight.transform.SetParent(null, true);
                bounceFillLight.gameObject.layer = GameLayers.Default;
                _bouncePos = spotLight.transform.position + spotLight.transform.forward * 2f;
                _bounceDetached = true;
            }

            Vector3 origin = spotLight.transform.position;
            Vector3 dir = spotLight.transform.forward;
            float target = 0f;
            float adaptTarget = 1f;
            float albedoTarget = _albedo;
            Vector3 targetPos = origin + dir * 6f;
            Vector3 targetDir = -dir;
            int envMask = GameLayers.EnvironmentAndDoorMask;
            if (Physics.SphereCast(origin, 0.12f, dir, out RaycastHit hit, 26f, envMask | GameLayers.Mask(GameLayers.Dinosaur), QueryTriggerInteraction.Ignore))
            {
                float d = Mathf.Max(0.5f, hit.distance);
                // Stand well off the surface so the fill spreads over the room instead of drawing a second disc, but
                // never behind the torch.
                float standoff = Mathf.Min(Mathf.Clamp(hit.distance * 0.32f, BounceMinStandoff, 3.2f), hit.distance * 0.85f);
                targetPos = hit.point + hit.normal * standoff;
                targetDir = hit.normal;
                albedoTarget = SurfaceAlbedo(hit.collider);
                // ... and never right under a ceiling
                if (Physics.Raycast(targetPos, Vector3.up, out RaycastHit roof, BounceCeilingGap + 2.5f, envMask, QueryTriggerInteraction.Ignore)
                    && roof.distance < BounceCeilingGap)
                {
                    float lower = BounceCeilingGap - roof.distance;
                    if (Physics.Raycast(targetPos, Vector3.down, out RaycastHit floor, lower + 0.5f, envMask, QueryTriggerInteraction.Ignore))
                        lower = Mathf.Min(lower, Mathf.Max(0f, floor.distance - 0.5f));
                    targetPos += Vector3.down * lower;
                }
                // Full strength for anything within ~9 m, fading out with distance (the patch gets too dim to matter)
                target = Mathf.InverseLerp(26f, 9f, hit.distance);
                if (hit.distance < 0.9f) target *= hit.distance / 0.9f;

                // How far away the hot spot lands: the median of five rays inside it. One ray alone dimmed the whole
                // beam whenever its centre crossed a chair back or a door frame a metre away, and the room went black.
                _adaptDistances[0] = d;
                Vector3 right = spotLight.transform.right, up = spotLight.transform.up;
                for (int i = 0; i < AdaptRays.Length; i++)
                {
                    Vector3 rd = (dir + right * AdaptRays[i].x + up * AdaptRays[i].y).normalized;
                    _adaptDistances[i + 1] = Physics.Raycast(origin, rd, out RaycastHit side, 26f, envMask | GameLayers.Mask(GameLayers.Dinosaur), QueryTriggerInteraction.Ignore)
                        ? Mathf.Max(0.5f, side.distance) : 26f;
                }
                System.Array.Sort(_adaptDistances);
                d = _adaptDistances[2];

                // The hot spot stays inside its budget (the bounce cone points away from the surface and adds nothing
                // to it). A beam that grazes the ground ahead lands far fewer lux than one square on a wall: count the
                // angle, or the budget dims the beam exactly where it has to reach (the road in front of the player).
                float facing = Mathf.Clamp(Vector3.Dot(-dir, hit.normal), 0.3f, 1f);
                float a = BeamCandela * facing / (d * d);
                float budget = Mathf.Clamp(HotSpotLuxAtRef * Mathf.Pow(HotSpotRefAlbedo / Mathf.Max(0.05f, _albedo), HotSpotAlbedoPower), HotSpotMinLux, HotSpotMaxLux);
                adaptTarget = Mathf.Clamp(budget / a, AdaptMin, 1f);
            }

            float dt = Time.deltaTime;
            _bouncePos = Vector3.Lerp(_bouncePos, targetPos, 1f - Mathf.Exp(-14f * dt));
            _bounceLevel = Mathf.Lerp(_bounceLevel, target, 1f - Mathf.Exp(-10f * dt));
            _adapt = Mathf.Lerp(_adapt, adaptTarget, 1f - Mathf.Exp(-7f * dt));
            _albedo = Mathf.Lerp(_albedo, albedoTarget, 1f - Mathf.Exp(-9f * dt));
            _bounceDir = Vector3.Slerp(_bounceDir, targetDir, 1f - Mathf.Exp(-10f * dt));
            bounceFillLight.transform.position = _bouncePos;
            if (_bounceDir.sqrMagnitude > 1e-4f) bounceFillLight.transform.rotation = Quaternion.LookRotation(_bounceDir, Mathf.Abs(_bounceDir.y) > 0.95f ? Vector3.forward : Vector3.up);
            // the bounce follows the dimmed beam only half way, so pointing at a near wall still shows the room
            bounceFillLight.intensity = BeamCandela * BounceShare * _bounceLevel * _beamFactor * Mathf.Sqrt(_adapt);
        }

        private void Update()
        {
            GameState gs = GameState.Instance;
            if (gs != null && gs.IsPaused) return;

            bool shouldShow = ShouldShowViewmodel;
            if (viewmodelRoot != null && viewmodelRoot.activeSelf != shouldShow)
            {
                ApplyVisibilityAndBeamImmediate();
                GameEvents.RaiseFlashlightToggled(IsBeamOn);
            }

            if (shouldShow && (gs == null || !gs.IsGameplayInputBlocked))
            {
                CretaceaInput input = CretaceaInput.Instance;
                if (input != null && input.FlashlightPressedThisFrame)
                {
                    SetBeamEnabled(!_isBeamOn, silent: false);
                }
            }

            if (shouldShow)
            {
                UpdateViewmodelMotionAndPullback();
                UpdateFlickerEnvelope();
            }
            UpdateBounceLight();
            if (_viewmodelFill != null && _viewmodelFill.enabled)
            {
                AtmosphereController atmoFill = AtmosphereController.Instance;
                float indoorFill = atmoFill != null ? atmoFill.IndoorWeight : 1f;
                // beam off: a trace indoors (eyes adapted), none outdoors (the moon lights the hand)
                _viewmodelFill.intensity = ViewmodelFillCandela * (_isBeamOn ? _beamFactor : 0.25f * indoorFill);
            }
        }

        private void OnCutsceneStateChanged(bool inCutscene)
        {
            SetCutsceneSuppressed(inCutscene);
        }

        public void SetCutsceneSuppressed(bool suppressed)
        {
            _cutsceneSuppressed = suppressed;
            ApplyVisibilityAndBeamImmediate();
            GameEvents.RaiseFlashlightToggled(IsBeamOn);
        }

        /// <summary>
        /// Authoritatively grants or removes the tactical flashlight.
        /// Setting false immediately disables the FPS viewmodel (eliminates FL-1/FL-2).
        /// </summary>
        public void SetHasFlashlight(bool hasFlashlight, bool autoTurnOn = false, bool playEquipAnim = false)
        {
            _hasFlashlight = hasFlashlight;
            if (!hasFlashlight)
            {
                _isBeamOn = false;
            }
            else if (autoTurnOn)
            {
                _isBeamOn = true;
            }

            if (hasFlashlight && playEquipAnim)
            {
                _equipProgress = 0f;
            }
            else
            {
                _equipProgress = 1f;
            }

            GameState.EnsureInstance().SetHasFlashlight(hasFlashlight);
            ApplyVisibilityAndBeamImmediate();
            GameEvents.RaiseFlashlightToggled(IsBeamOn);
        }

        public void SetBeamEnabled(bool enabled, bool silent = false)
        {
            if (!_hasFlashlight && enabled) return;
            if (_isBeamOn == enabled) return;

            _isBeamOn = enabled;
            if (!silent) _clickRecoil = 1f;      // the thumb kick only for a real click, not a scripted toggle
            ApplyVisibilityAndBeamImmediate();

            if (!silent)
            {
                GameEvents.RaisePlaySound(enabled ? "flashlight_click_on" : "flashlight_click_off");
                GameEvents.RaiseNoiseEmitted(transform.position, 4.5f, "flashlight");
            }
            GameEvents.RaiseFlashlightToggled(IsBeamOn);
        }

        /// <summary>Triggers scripted electrical/terror flicker for horror beats.</summary>
        public void TriggerFlicker(float durationSeconds, float strength01 = 0.85f)
        {
            _scriptedFlickerTimer = Mathf.Max(_scriptedFlickerTimer, durationSeconds);
            _scriptedFlickerStrength = Mathf.Clamp01(strength01);
        }

        /// <summary>Called by AI/Story when a hunting dinosaur closes within terror proximity.</summary>
        public void SetNearestPredatorDistance(float distanceMeters)
        {
            _terrorProximity01 = distanceMeters < terrorFlickerThresholdDist
                ? 1f - Mathf.Clamp01(distanceMeters / terrorFlickerThresholdDist)
                : 0f;
        }

        private void ApplyVisibilityAndBeamImmediate()
        {
            bool showVm = ShouldShowViewmodel;
            if (viewmodelRoot != null && viewmodelRoot.activeSelf != showVm)
            {
                viewmodelRoot.SetActive(showVm);
            }

            bool beamActive = showVm && _isBeamOn;
            if (spotLight != null) spotLight.enabled = beamActive;
            if (bounceFillLight != null) bounceFillLight.enabled = beamActive;
            if (_viewmodelFill != null) _viewmodelFill.enabled = showVm;
        }

        private void UpdateViewmodelMotionAndPullback()
        {
            if (swayPivot == null) return;

            float dt = Time.deltaTime;
            _equipProgress = Mathf.MoveTowards(_equipProgress, 1f, dt / Mathf.Max(0.1f, equipRaiseDuration));
            bool clicked = _clickRecoil >= 0.999f;
            _clickRecoil = Mathf.MoveTowards(_clickRecoil, 0f, dt * 6f);

            // Anti-wall-clip forward SphereCast from camera eye
            float targetWallBlend = 0f;
            if (cameraReference != null)
            {
                Ray ray = new Ray(cameraReference.position, cameraReference.forward);
                if (Physics.SphereCast(ray, wallCheckRadius, out RaycastHit hit, wallCheckDistance, GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore))
                {
                    targetWallBlend = 1f - Mathf.Clamp01((hit.distance - 0.12f) / Mathf.Max(0.05f, wallCheckDistance - 0.12f));
                }
            }
            _currentWallBlend = Mathf.Lerp(_currentWallBlend, targetWallBlend, 1f - Mathf.Exp(-16f * dt));

            ViewmodelMotion motion = EnsureViewmodelMotion();
            if (motion != null)
            {
                ApplySpringViewmodelMotion(motion, dt, clicked);
                return;
            }

            // Sprint lower blend (legacy path, only if the motion component cannot be added)
            bool sprinting = playerController != null && playerController.IsSprinting;
            _sprintLowerBlend = Mathf.Lerp(_sprintLowerBlend, sprinting ? 1f : 0f, 1f - Mathf.Exp(-8f * dt));

            // Look sway
            CretaceaInput input = CretaceaInput.Instance;
            Vector2 look = input != null ? (input.LookMouseDelta * 0.08f + input.LookStick * 0.6f) : Vector2.zero;
            Vector3 targetSway = new Vector3(
                Mathf.Clamp(-look.x * swayAmount, -0.04f, 0.04f),
                Mathf.Clamp(-look.y * swayAmount, -0.03f, 0.03f),
                0f);
            _swayPos = Vector3.Lerp(_swayPos, targetSway, 1f - Mathf.Exp(-swaySmooth * dt));

            // Walk bob
            float speed = playerController != null ? playerController.HorizontalSpeed : 0f;
            Vector3 bob = Vector3.zero;
            if (speed > 0.25f)
            {
                _walkBobT += dt * (sprinting ? 10.5f : 7.0f);
                float amp = sprinting ? 0.018f : 0.009f;
                bob.x = Mathf.Cos(_walkBobT * 0.5f) * amp;
                bob.y = Mathf.Sin(_walkBobT) * amp;
            }

            // Equip raise arc (rises cleanly from below frame on pickup)
            float equipEase = 1f - Mathf.Pow(1f - _equipProgress, 3f);
            float equipDropY = (1f - equipEase) * -0.32f;
            float equipPitch = (1f - equipEase) * 32f;

            Vector3 localOffset = _swayPos + bob;
            localOffset.y += equipDropY - _sprintLowerBlend * 0.08f - _currentWallBlend * 0.05f;
            localOffset.z -= _currentWallBlend * maxWallPullbackZ + _clickRecoil * 0.015f;

            Quaternion extraRot = Quaternion.Euler(
                equipPitch + _sprintLowerBlend * 16f + _currentWallBlend * maxWallPitchDownDeg + _clickRecoil * 2.5f,
                -_sprintLowerBlend * 14f,
                _sprintLowerBlend * 8f);

            swayPivot.localPosition = _restLocalPos + localOffset;
            swayPivot.localRotation = _restLocalRot * extraRot;
        }

        // ---- Spring-driven viewmodel motion (round 5): see ViewmodelMotion -------------------------------------------
        private ViewmodelMotion _motion;
        private PlayerInteractor _interactor;
        private CharacterController _playerCc;
        private float _lastHoldProgress;
        private float _holdSeenTimer;
        private Renderer _ledRenderer;
        private int _ledIndex = -1;
        private float _ledLevel = -1f;
        private MaterialPropertyBlock _ledBlock;
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private const float LedNits = 60f;

        // the LED behind the lens glows with the beam (and dips with its flicker); dark when the torch is off
        private void UpdateLedGlow()
        {
            if (_ledIndex < 0)
            {
                if (_ledIndex == -2 || viewmodelRoot == null) return;
                _ledIndex = -2;
                foreach (Renderer r in viewmodelRoot.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    for (int k = 0; k < mats.Length; k++)
                    {
                        if (mats[k] != null && mats[k].name == "M_VM_LED") { _ledRenderer = r; _ledIndex = k; break; }
                    }
                    if (_ledIndex >= 0) break;
                }
                if (_ledIndex < 0) return;
                _ledBlock = new MaterialPropertyBlock();
            }
            float level = _isBeamOn ? Mathf.Clamp01(_beamFactor) : 0f;
            if (Mathf.Abs(level - _ledLevel) < 0.01f) return;
            _ledLevel = level;
            _ledRenderer.GetPropertyBlock(_ledBlock, _ledIndex);
            _ledBlock.SetColor(EmissiveColorId, new Color(1f, 0.97f, 0.92f) * (LedNits * level));
            _ledRenderer.SetPropertyBlock(_ledBlock, _ledIndex);
        }

        private ViewmodelMotion EnsureViewmodelMotion()
        {
            if (_motion != null) return _motion;
            if (swayPivot == null) return null;
            _motion = swayPivot.GetComponent<ViewmodelMotion>();
            if (_motion == null) _motion = swayPivot.gameObject.AddComponent<ViewmodelMotion>();
            if (playerController != null)
            {
                _interactor = playerController.GetComponent<PlayerInteractor>();
                _playerCc = playerController.GetComponent<CharacterController>();
                // the nearest predator drives the tremor, the breathing and the terror flicker
                PredatorProximityProbe probe = playerController.GetComponent<PredatorProximityProbe>();
                if (probe == null) probe = playerController.gameObject.AddComponent<PredatorProximityProbe>();
                probe.Bind(this, cameraReference);
            }
            BindStableBeam();
            return _motion;
        }

        private void ApplySpringViewmodelMotion(ViewmodelMotion motion, float dt, bool clicked)
        {
            // the thumb on the tail switch: SetBeamEnabled arms _clickRecoil at 1
            if (clicked) motion.Click(_isBeamOn);
            UpdateLedGlow();

            // holding an interaction: the hold progress climbs while the key is held (levers, pump, breaker, gate)
            // (the key must really be held: focusing a half-done gate restores its saved progress, which is no hold)
            float hold = _interactor != null ? _interactor.CurrentHoldProgress01 : 0f;
            CretaceaInput cin = CretaceaInput.Instance;
            bool keyHeld = cin != null && cin.InteractHeld;
            if (keyHeld && hold > _lastHoldProgress + 1e-5f && hold < 1f) _holdSeenTimer = 0.25f;
            else _holdSeenTimer = Mathf.Max(0f, _holdSeenTimer - dt);
            _lastHoldProgress = hold;

            GameState gs = GameState.Instance;
            var input = new ViewmodelMotionInput
            {
                Speed = playerController != null ? playerController.HorizontalSpeed : 0f,
                Grounded = _playerCc == null || _playerCc.isGrounded,
                Sprinting = playerController != null && playerController.IsSprinting,
                Crouching = playerController != null && playerController.IsCrouching,
                Exhausted = playerController != null && playerController.IsExhausted,
                Lowered = gs != null && (gs.IsDocumentOpen || gs.IsJournalOpen),
                Holding = _holdSeenTimer > 0f,
                WallBlend = Mathf.Max(_currentWallBlend * (maxWallPullbackZ / 0.22f), _muzzleTuck),
                Equip01 = _equipProgress,
                Terror01 = _terrorProximity01,
                CameraRotation = cameraReference != null ? cameraReference.rotation : swayPivot.rotation,
            };
            motion.Tick(input, dt);
            UpdateMuzzleTuck(dt);
            swayPivot.localPosition = _restLocalPos + motion.PositionOffset;
            swayPivot.localRotation = _restLocalRot * motion.RotationOffset;
            UpdateStableBeam(motion, dt);
        }

        // ---- The beam follows the lens but not the hand's jitter ----------------------------------------------------
        // The spot leaves from the lens (its position tracks the muzzle every frame) but its aim follows only the
        // carry pose and the look sway (ViewmodelMotion.BeamRotation), eased, so tremor, gait, breathing and footstep
        // nods do not shake the pool, the cookie and its shadows across the room. If the lens is inside or behind a
        // wall (standing flush), the light origin is pulled back to this side of it.
        private Transform _beamMuzzle;
        private Quaternion _spotRotInPivot = Quaternion.identity;
        private bool _stableBeamBound;
        private Quaternion _beamLocalRot = Quaternion.identity;
        private bool _beamLocalInit;
        private Vector3 _muzzleInPivot;          // the lens in sway-pivot space (rest pose)
        private float _muzzleTuck;               // extra wall pull-back (ViewmodelMotion WallBlend units, up to 1.6)

        // Standing flush to a wall the lens (about 0.7 m ahead of the eye) can end up past the wall. Measure that
        // from the REST pose (no feedback from the pull-back itself) and tuck the torch back far enough, so the
        // light stays on this side of the wall without moving it behind the lens.
        private void UpdateMuzzleTuck(float dt)
        {
            float target = 0f;
            if (_beamMuzzle != null && cameraReference != null)
            {
                Transform rigParent = swayPivot.parent;
                Vector3 restLocal = _restLocalPos + _restLocalRot * _muzzleInPivot;
                Vector3 restMuzzle = rigParent != null ? rigParent.TransformPoint(restLocal) : restLocal;
                Vector3 eye = cameraReference.position;
                if (Physics.Linecast(eye, restMuzzle, out RaycastHit hit, GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore))
                {
                    float deficit = (restMuzzle - eye).magnitude - hit.distance + 0.06f;   // how far past the wall, plus a margin
                    target = Mathf.Clamp(deficit / 0.22f, 0f, 1.6f);                        // 0.22 m of pull-back per unit
                }
            }
            _muzzleTuck = Mathf.Lerp(_muzzleTuck, target, 1f - Mathf.Exp(-14f * dt));
        }

        private void BindStableBeam()
        {
            if (_stableBeamBound || spotLight == null || swayPivot == null) return;
            _stableBeamBound = true;
            Transform spot = spotLight.transform;
            Transform model = spot.parent;
            _beamMuzzle = model != null ? model.Find("Beam_Muzzle") : null;
            if (_beamMuzzle == null) return;                  // legacy rig: the spot stays where the scene put it
            // the spot's aim relative to the sway pivot at rest (swayPivot is still at its rest pose here)
            _spotRotInPivot = Quaternion.Inverse(swayPivot.rotation) * spot.rotation;
            _muzzleInPivot = swayPivot.InverseTransformPoint(_beamMuzzle.position);
            spot.SetParent(swayPivot.parent, true);
        }

        private void UpdateStableBeam(ViewmodelMotion motion, float dt)
        {
            if (_beamMuzzle == null || spotLight == null) return;
            Transform spot = spotLight.transform;
            Transform rigParent = swayPivot.parent;
            Quaternion parentRot = rigParent != null ? rigParent.rotation : Quaternion.identity;
            // eased in the rig's own frame, so a camera turn is not delayed (the look sway already lags it)
            Quaternion targetLocal = _restLocalRot * motion.BeamRotation * _spotRotInPivot;
            _beamLocalRot = _beamLocalInit ? Quaternion.Slerp(_beamLocalRot, targetLocal, 1f - Mathf.Exp(-12f * dt)) : targetLocal;
            _beamLocalInit = true;
            spot.rotation = parentRot * _beamLocalRot;

            Vector3 origin = _beamMuzzle.position;
            if (cameraReference != null)
            {
                Vector3 eye = cameraReference.position;
                Vector3 seg = origin - eye;
                float len = seg.magnitude;
                if (len > 1e-3f && Physics.Linecast(eye, origin, out RaycastHit hit, GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore))
                {
                    Vector3 dir = seg / len;
                    origin = hit.point - dir * 0.03f;     // last resort: the tuck keeps this to a few cm
                }
            }
            spot.position = origin;
        }

        private void UpdateFlickerEnvelope()
        {
            if (!_isBeamOn || spotLight == null) return;

            float factor = 1f;
            if (_scriptedFlickerTimer > 0f)
            {
                _scriptedFlickerTimer = Mathf.Max(0f, _scriptedFlickerTimer - Time.deltaTime);
                float n = Mathf.PerlinNoise(Time.time * 34f, 2.13f);
                if (n < 0.45f * _scriptedFlickerStrength)
                {
                    factor = Mathf.Lerp(1f, 0.08f, _scriptedFlickerStrength);
                }
            }
            else if (_terrorProximity01 > 0.25f)
            {
                float n = Mathf.PerlinNoise(Time.time * 22f, 9.4f);
                if (n < _terrorProximity01 * 0.28f)
                {
                    factor = 0.35f;
                }
            }

            _beamFactor = factor;
            AtmosphereController atmo = AtmosphereController.Instance;
            float indoor = atmo != null ? atmo.IndoorWeight : 0f;
            if (_spotHd != null)
            {
                // surfaces see the adapted beam; the volumetric shaft is not scaled by the light dimmer in HDRP
                _spotHd.SetLightDimmer(factor * _adapt, Mathf.Lerp(BeamVolumetric, BeamVolumetricIndoor, indoor) * factor);
            }
            else
            {
                spotLight.intensity = BeamCandela * factor * _adapt;
            }
        }
    }
}
