using System;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Interact
{
    [Serializable]
    public struct CctvFeedBinding
    {
        public string feedLabel;
        public Camera feedCamera;
        public Renderer monitorScreenRenderer;
        public int screenMaterialIndex;
    }

    /// <summary>
    /// Live 10 Hz round-robin RenderTexture CCTV switcher for Security Office (6 monitors) & Control Room feeds.
    /// Renders ONLY 1 CCTV camera per step (zero M5 Max bottleneck!) and switches seamlessly to animated
    /// CRT static snow (_BlackoutStatic / _StaticAmount) the instant the midnight blackout hits.
    /// </summary>
    public sealed class CctvMonitorController : MonoBehaviour, ICctvController
    {
        [Header("Identity")]
        [SerializeField] private string controllerId = "vc_security_cctv";
        [SerializeField] private CctvFeedBinding[] feeds;

        [Header("RenderTexture & Update Cadence")]
        [SerializeField] private int renderWidth = 256;
        [SerializeField] private int renderHeight = 144;
        [Tooltip("Target refresh rate per individual monitor (10 Hz feels authentic & stays ultra-cheap).")]
        [SerializeField] private float perFeedHz = 10f;
        [Tooltip("Only render live feeds when player is within this distance of the monitors.")]
        [SerializeField] private float maxRenderDistance = 36f;

        [Header("Fallback Static Texture (Optional)")]
        [SerializeField] private Texture2D fallbackStaticTexture;

        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int EmissiveMapId = Shader.PropertyToID("_EmissiveColorMap");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int BlackoutStaticId = Shader.PropertyToID("_BlackoutStatic");
        private static readonly int StaticAmountId = Shader.PropertyToID("_StaticAmount");
        private static readonly int PowerId = Shader.PropertyToID("_Power");

        private RenderTexture[] _renderTextures;
        private MaterialPropertyBlock _mpb;
        private int _roundRobinIndex;
        private float _stepTimer;
        private bool _isStatic;
        private bool _isActive = true;

        public string ControllerId => controllerId;
        public bool IsStatic => _isStatic;

        private void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            AllocateRenderTextures();
        }

        private void OnEnable()
        {
            WorldRegistry.EnsureInstance().RegisterCctv(this);
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance != null)
                WorldRegistry.Instance.UnregisterCctv(this);
        }

        private void OnDestroy()
        {
            ReleaseRenderTextures();
        }

        private void Update()
        {
            if (!_isActive || _isStatic || feeds == null || feeds.Length == 0) return;
            if (GameState.Instance != null && GameState.Instance.IsPaused) return;

            // Skip GPU render work once the player has left the Visitor Center
            Camera mainCam = Camera.main;
            if (mainCam != null && (mainCam.transform.position - transform.position).sqrMagnitude > maxRenderDistance * maxRenderDistance)
            {
                return;
            }

            float stepInterval = 1f / Mathf.Max(1f, perFeedHz * feeds.Length);
            _stepTimer += Time.deltaTime;
            if (_stepTimer < stepInterval) return;
            _stepTimer = 0f;

            RenderFeedIndex(_roundRobinIndex);
            _roundRobinIndex = (_roundRobinIndex + 1) % feeds.Length;
        }

        private void AllocateRenderTextures()
        {
            if (feeds == null || feeds.Length == 0) return;
            _renderTextures = new RenderTexture[feeds.Length];

            for (int i = 0; i < feeds.Length; i++)
            {
                var rt = new RenderTexture(renderWidth, renderHeight, 16, RenderTextureFormat.DefaultHDR)
                {
                    name = $"RT_CCTV_{controllerId}_{i}",
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false
                };
                rt.Create();
                _renderTextures[i] = rt;

                if (feeds[i].feedCamera != null)
                {
                    feeds[i].feedCamera.targetTexture = rt;
                    feeds[i].feedCamera.enabled = false; // Driven manually via round-robin Render()
                }

                ApplyScreenBinding(i, staticMode: _isStatic);
            }

            // Prime only the first screen on Awake; round-robin Update fills the rest (1 cam/step)
            // so we never overload HDRP's single-frame graphics ring buffer with 6 simultaneous renders.
            if (!_isStatic && feeds.Length > 0)
            {
                RenderFeedIndex(0);
                _roundRobinIndex = feeds.Length > 1 ? 1 : 0;
            }
        }

        private void ReleaseRenderTextures()
        {
            if (_renderTextures == null) return;
            for (int i = 0; i < _renderTextures.Length; i++)
            {
                if (feeds != null && i < feeds.Length && feeds[i].feedCamera != null)
                {
                    feeds[i].feedCamera.targetTexture = null;
                }
                if (_renderTextures[i] != null)
                {
                    _renderTextures[i].Release();
                    Destroy(_renderTextures[i]);
                }
            }
            _renderTextures = null;
        }

        private void RenderFeedIndex(int idx)
        {
            if (feeds == null || idx < 0 || idx >= feeds.Length) return;
            Camera cam = feeds[idx].feedCamera;
            if (cam == null || !cam.gameObject.activeInHierarchy) return;
            cam.Render();
        }

        public void SetStatic(bool staticEnabled)
        {
            _isStatic = staticEnabled;
            if (feeds == null) return;

            for (int i = 0; i < feeds.Length; i++)
            {
                ApplyScreenBinding(i, staticEnabled);
            }
            if (!staticEnabled)
            {
                _stepTimer = 999f; // Trigger immediate staggered round-robin refresh starting next frame
            }
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            if (feeds == null) return;
            for (int i = 0; i < feeds.Length; i++)
            {
                ApplyScreenBinding(i, _isStatic);
            }
        }

        private void ApplyScreenBinding(int idx, bool staticMode)
        {
            if (feeds == null || idx >= feeds.Length) return;
            Renderer r = feeds[idx].monitorScreenRenderer;
            if (r == null) return;

            int matIdx = Mathf.Clamp(feeds[idx].screenMaterialIndex, 0, Mathf.Max(0, r.sharedMaterials.Length - 1));
            r.GetPropertyBlock(_mpb, matIdx);

            Texture tex = staticMode && fallbackStaticTexture != null
                ? fallbackStaticTexture
                : (_renderTextures != null && idx < _renderTextures.Length ? _renderTextures[idx] : null);

            if (tex != null)
            {
                _mpb.SetTexture(BaseColorMapId, tex);
                _mpb.SetTexture(MainTexId, tex);
                _mpb.SetTexture(EmissiveMapId, tex);
            }

            _mpb.SetFloat(BlackoutStaticId, staticMode ? 1f : 0f);
            _mpb.SetFloat(StaticAmountId, staticMode ? 1f : 0f);
            _mpb.SetFloat(PowerId, _isActive ? 1f : 0f);
            _mpb.SetColor(EmissiveColorId, !_isActive ? Color.black : (staticMode ? new Color(0.28f, 0.30f, 0.34f) : new Color(1.35f, 1.45f, 1.40f)));

            r.SetPropertyBlock(_mpb, matIdx);
        }
    }
}
