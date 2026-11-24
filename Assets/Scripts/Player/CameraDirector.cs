using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Unity.Cinemachine;
using Cretacea.Core;
using Cretacea.Rendering;

namespace Cretacea.Player
{
    /// <summary>
    /// Cinemachine 3 camera director managing:
    /// - FPS player CinemachineCamera with smooth sprint/crouch FOV & squared trauma shake
    /// - Opening Cutscene 6-shot golden-hour -> dusk -> night -> Security Office real exterior window push-in
    /// - Dawn Coast Ending elevated crane shot rising OVER the Old Yard east wall across misty Tyrant Valley
    ///   before sweeping back to the open golden sunrise ocean horizon (fixes Godot blank-wall ending shot!).
    /// </summary>
    public sealed class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance { get; private set; }

        [Header("Cinemachine 3 Cameras")]
        [SerializeField] private CinemachineCamera fpsCamera;
        [SerializeField] private CinemachineCamera cutsceneCamera;
        [SerializeField] private Transform shakeHost;
        [SerializeField] private PlayerController playerController;

        [Header("FPS FOV & Trauma Shake")]
        [SerializeField] private float sprintFovBoost = 6.5f;
        [SerializeField] private float crouchFovOffset = -2.5f;
        [SerializeField] private float fovSmoothSpeed = 7.0f;
        [SerializeField] private float maxShakePosMeters = 0.09f;
        [SerializeField] private float maxShakeAngleDeg = 3.2f;
        [SerializeField] private float shakeNoiseFrequency = 22f;

        private const int ActivePriority = 20;
        private const int InactivePriority = 5;

        private float _trauma;
        private float _traumaDecay = 1.6f;
        private float _currentFov = 68f;
        private bool _cutsceneCamActive;
        private Coroutine _activeMoveRoutine;

        public bool IsCutsceneCameraActive => _cutsceneCamActive;

        private void Awake()
        {
            Instance = this;
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();

            EnsureCamerasExist();
            if (Camera.main != null)
            {
                ViewmodelCameraConfig.ExcludeViewmodelLayer(Camera.main);
            }
            _currentFov = SaveManager.Settings.fieldOfView;
            SwitchToFpsCamera();
        }

        private void OnEnable()
        {
            GameEvents.OnCameraTraumaRequested += AddTrauma;
            GameEvents.OnSettingsChanged += OnSettingsChanged;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }

        private void OnDisable()
        {
            GameEvents.OnCameraTraumaRequested -= AddTrauma;
            GameEvents.OnSettingsChanged -= OnSettingsChanged;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        }

        // ---- camera cuts ---------------------------------------------------------------------------------------------
        // A cut (cutscene shot change, respawn, checkpoint jump) must not carry the previous shot's exposure, TAA and
        // fog history: the first second of a bright room after a night exterior was blown out white otherwise.
        public static bool ResetHistoryOnCut = true;       // look lab switch
        public static int CutCount;
        private Camera _cutCam;
        private Vector3 _cutLastPos;
        private Quaternion _cutLastRot;
        private bool _cutHasLast;

        private void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (_cutCam == null) { _cutCam = Camera.main; _cutHasLast = false; }
            if (_cutCam == null) return;
            Transform t = _cutCam.transform;
            if (_cutHasLast)
            {
                // nothing a player or a dolly does moves 3 m or turns 60 degrees within one frame
                if ((t.position - _cutLastPos).sqrMagnitude > 9f || Quaternion.Angle(t.rotation, _cutLastRot) > 60f)
                {
                    CutCount++;
                    if (ResetHistoryOnCut) HDCamera.GetOrCreate(_cutCam).Reset();
                }
            }
            _cutLastPos = t.position;
            _cutLastRot = t.rotation;
            _cutHasLast = true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureCamerasExist()
        {
            if (fpsCamera == null && playerController != null && playerController.CameraPivot != null)
            {
                fpsCamera = playerController.CameraPivot.GetComponentInChildren<CinemachineCamera>(true);
            }

            if (shakeHost == null && fpsCamera != null)
            {
                shakeHost = fpsCamera.transform;
            }

            if (cutsceneCamera == null)
            {
                var cutGo = new GameObject("[CutsceneCinemachineCamera]");
                cutGo.transform.SetParent(transform, false);
                cutsceneCamera = cutGo.AddComponent<CinemachineCamera>();
                cutsceneCamera.Lens.FieldOfView = 50f;
                cutsceneCamera.Lens.NearClipPlane = 0.1f;
                cutsceneCamera.Lens.FarClipPlane = 12000f;
                cutsceneCamera.Priority = InactivePriority;
            }
        }

        private void Update()
        {
            if (GameState.Instance != null && GameState.Instance.IsPaused) return;

            UpdateFpsFov();
            UpdateTraumaShake();
        }

        private void OnSettingsChanged()
        {
            _currentFov = Mathf.Clamp(SaveManager.Settings.fieldOfView, 55f, 90f);
        }

        private void UpdateFpsFov()
        {
            if (fpsCamera == null || _cutsceneCamActive) return;

            float baseFov = Mathf.Clamp(SaveManager.Settings.fieldOfView, 55f, 90f);
            float targetFov = baseFov;
            if (playerController != null)
            {
                if (playerController.IsSprinting && playerController.HorizontalSpeed > 1.5f)
                    targetFov += sprintFovBoost;
                else if (playerController.IsCrouching)
                    targetFov += crouchFovOffset;
            }

            _currentFov = Mathf.Lerp(_currentFov, targetFov, 1f - Mathf.Exp(-fovSmoothSpeed * Time.deltaTime));
            var lens = fpsCamera.Lens;
            lens.FieldOfView = _currentFov;
            fpsCamera.Lens = lens;
        }

        private void UpdateTraumaShake()
        {
            if (shakeHost == null) return;

            if (_trauma <= 0.0005f)
            {
                shakeHost.localPosition = Vector3.zero;
                shakeHost.localRotation = Quaternion.identity;
                return;
            }

            float shake = _trauma * _trauma; // Squared trauma feels weighty and organic
            float t = Time.time * shakeNoiseFrequency;

            float rx = (Mathf.PerlinNoise(t, 1.3f) * 2f - 1f) * maxShakeAngleDeg * shake;
            float ry = (Mathf.PerlinNoise(2.7f, t) * 2f - 1f) * maxShakeAngleDeg * shake;
            float rz = (Mathf.PerlinNoise(t, 5.1f) * 2f - 1f) * (maxShakeAngleDeg * 1.25f) * shake;

            float px = (Mathf.PerlinNoise(t + 11f, 3.9f) * 2f - 1f) * maxShakePosMeters * shake;
            float py = (Mathf.PerlinNoise(7.4f, t + 19f) * 2f - 1f) * maxShakePosMeters * shake;

            shakeHost.localPosition = new Vector3(px, py, 0f);
            shakeHost.localRotation = Quaternion.Euler(rx, ry, rz);

            _trauma = Mathf.Max(0f, _trauma - _traumaDecay * Time.deltaTime);
        }

        public void AddTrauma(float amount01, float decayRate = 1.6f)
        {
            _trauma = Mathf.Clamp01(Mathf.Max(_trauma, amount01));
            _traumaDecay = Mathf.Max(0.4f, decayRate);
        }

        public void SwitchToCutsceneCamera(Vector3 initialPos, Vector3 initialLookAt, float fov = 50f)
        {
            EnsureCamerasExist();
            _cutsceneCamActive = true;
            cutsceneCamera.transform.position = initialPos;
            cutsceneCamera.transform.rotation = Quaternion.LookRotation((initialLookAt - initialPos).normalized, Vector3.up);
            var lens = cutsceneCamera.Lens;
            lens.FieldOfView = fov;
            cutsceneCamera.Lens = lens;

            cutsceneCamera.Priority = ActivePriority;
            if (fpsCamera != null) fpsCamera.Priority = InactivePriority;
        }

        public void SwitchToFpsCamera()
        {
            if (_activeMoveRoutine != null)
            {
                StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = null;
            }

            _cutsceneCamActive = false;
            if (cutsceneCamera != null) cutsceneCamera.Priority = InactivePriority;
            if (fpsCamera != null)
            {
                fpsCamera.Priority = ActivePriority;
                var lens = fpsCamera.Lens;
                lens.FieldOfView = Mathf.Clamp(SaveManager.Settings.fieldOfView, 55f, 90f);
                fpsCamera.Lens = lens;
            }
        }

        /// <summary>
        /// Smooth Cinemachine dolly glide used across Opening Cutscene (incl. Shot 6 real Security Office exterior window)
        /// and the Dawn Coast Ending elevated crane shot over the eastern cliffs.
        /// Respects Pause seamlessly without advancing while paused.
        /// </summary>
        public IEnumerator GlideCutsceneShot(
            Vector3 fromPos,
            Vector3 toPos,
            Vector3 lookFrom,
            Vector3 lookTo,
            float durationSeconds,
            float fovStart = 50f,
            float fovEnd = 50f,
            System.Func<bool> shouldAbort = null)
        {
            EnsureCamerasExist();
            if (!_cutsceneCamActive)
            {
                SwitchToCutsceneCamera(fromPos, lookFrom, fovStart);
            }

            float elapsed = 0f;
            float dur = Mathf.Max(0.05f, durationSeconds);

            while (elapsed < dur)
            {
                if (shouldAbort != null && shouldAbort()) yield break;

                if (GameState.Instance == null || !GameState.Instance.IsPaused)
                {
                    elapsed += Time.deltaTime;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / dur));
                    Vector3 pos = Vector3.Lerp(fromPos, toPos, k);
                    Vector3 look = Vector3.Lerp(lookFrom, lookTo, k);
                    Vector3 dir = look - pos;
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        cutsceneCamera.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir.normalized, Vector3.up));
                    }
                    else
                    {
                        cutsceneCamera.transform.position = pos;
                    }

                    var lens = cutsceneCamera.Lens;
                    lens.FieldOfView = Mathf.Lerp(fovStart, fovEnd, k);
                    cutsceneCamera.Lens = lens;
                }

                yield return null;
            }
        }

        /// <summary>
        /// Executes the elevated Dawn Coast Ending crane shot:
        /// 1. Starts behind player looking out over the open shimmering golden Eastern Ocean (+X).
        /// 2. On distant inland T. rex roar, cranes UP to Y = 15 m clearing the Old Yard walls to reveal misty Tyrant Valley
        ///    & distant Visitor Center beacons, then sweeps majestically back toward the sunlit ocean evac horizon!
        /// </summary>
        public IEnumerator PlayDawnCoastCraneShot(Vector3 overlookPos, System.Action onDistantRoarBeat)
        {
            Vector3 oceanHorizon = new Vector3(900f, -12f, overlookPos.z + 6f);
            Vector3 startCam = overlookPos + new Vector3(-2.5f, 1.65f, 0f);
            Vector3 railCam = overlookPos + new Vector3(0.4f, 1.65f, 0f);

            SwitchToCutsceneCamera(startCam, oceanHorizon, 48f);

            // Walk out to the coastal cliff rail overlooking the open dawn ocean
            yield return GlideCutsceneShot(startCam, railCam, oceanHorizon, oceanHorizon + Vector3.up * 10f, 5.5f, 48f, 44f);

            onDistantRoarBeat?.Invoke();
            yield return new WaitForSeconds(1.1f);

            // Elevate UP to Y = 15 m above the yard concrete walls so the glimpse inland frames misty Tyrant Valley!
            // 27 m up: at 15 m half the frame was the yard wall. The look target sits over the valley canopy.
            Vector3 cranePeak = overlookPos + new Vector3(-7.0f, 27.0f, 4.0f);
            Vector3 valleyCanopyLook = new Vector3(310f, 10f, -15f);
            yield return GlideCutsceneShot(railCam, cranePeak, oceanHorizon + Vector3.up * 10f, valleyCanopyLook, 3.8f, 44f, 50f);

            yield return new WaitForSeconds(1.5f);

            // Final sweeping turn back out over the golden ocean & distant Coast Guard rescue beacon
            Vector3 finalSeaCam = overlookPos + new Vector3(3.0f, 12.0f, -3.0f);
            yield return GlideCutsceneShot(cranePeak, finalSeaCam, valleyCanopyLook, oceanHorizon + Vector3.up * 18f, 4.2f, 50f, 46f);
        }
    }
}
