using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// Per-instance hide response for a dinosaur, written through one MaterialPropertyBlock per renderer:
    ///  - Wetness: follows the weather (<c>_GlobalWetness</c>, set by AtmosphereController: 0.08 by day, 1.0 in the
    ///    storm). Wet hide gets smoother (the smoothness remap floor rises, so folds and scales both gain a sheen) and
    ///    a little darker. Under a roof the hide dries slowly; out in the rain it soaks within a few seconds.
    ///  - Eyeshine: an emissive colour (nits) on the eye texels only (the material's emissive map is an eye mask),
    ///    set by <see cref="DinoEyeShine"/>. Black unless something feeds it.
    /// SSS materials cannot use HDRP's coat, so the wet look is smoothness + albedo only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DinoWetness : MonoBehaviour
    {
        [SerializeField] private Renderer[] targetRenderers = System.Array.Empty<Renderer>();
        [Tooltip("Smoothness remap floor when fully wet (dry = 0). The mask map's smoothness is lerped from this floor to 1.")]
        [SerializeField] private float wetSmoothnessFloor = 0.38f;
        [Tooltip("BaseColor multiplier when fully wet (sRGB; 0.82 ~ 35 % darker in linear), applied on top of the material's own tint.")]
        [SerializeField] private float wetAlbedoMultiplier = 0.82f;
        [SerializeField] private float soakSeconds = 4f;
        [SerializeField] private float drySeconds = 45f;
        [SerializeField] private float shelterProbeHeight = 25f;
        [SerializeField] private LayerMask shelterMask = ~0;

        private static readonly int GlobalWetnessId = Shader.PropertyToID("_GlobalWetness");
        private static readonly int SmoothMinId = Shader.PropertyToID("_SmoothnessRemapMin");
        private static readonly int SmoothMaxId = Shader.PropertyToID("_SmoothnessRemapMax");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        private MaterialPropertyBlock _mpb;
        private float _wet = -1f;
        private bool _sheltered;
        private float _nextShelterProbe;
        private Color _eyeShine = Color.black;
        private float _lastWrittenWet = -1f;
        private Color _lastWrittenShine = new Color(-1f, 0f, 0f, 0f);
        private float _baseSmoothMin;
        private float _baseSmoothMax = 1f;
        private Color _baseColor = Color.white;

        /// <summary>Current wetness 0..1 actually applied to the hide.</summary>
        public float Wetness => Mathf.Max(0f, _wet);

        /// <summary>Linear HDR emissive colour (nits) for the eye texels. Black = off.</summary>
        public Color EyeShine
        {
            get => _eyeShine;
            set => _eyeShine = value;
        }

        private void Awake()
        {
            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
            Material m = targetRenderers.Length > 0 && targetRenderers[0] != null ? targetRenderers[0].sharedMaterial : null;
            if (m != null)
            {
                if (m.HasProperty(SmoothMinId)) _baseSmoothMin = m.GetFloat(SmoothMinId);
                if (m.HasProperty(SmoothMaxId)) _baseSmoothMax = m.GetFloat(SmoothMaxId);
                if (m.HasProperty(BaseColorId)) _baseColor = m.GetColor(BaseColorId);   // keep the material's tint
            }
            _mpb = new MaterialPropertyBlock();
            int dinoLayer = gameObject.layer;
            shelterMask &= ~(1 << dinoLayer);
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycast >= 0) shelterMask &= ~(1 << ignoreRaycast);
        }

        private void OnDisable()
        {
            _lastWrittenWet = -1f;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (Time.time >= _nextShelterProbe)
            {
                _nextShelterProbe = Time.time + 0.5f + Random.value * 0.2f;
                _sheltered = ProbeShelter();
            }

            float weather = Mathf.Clamp01(Shader.GetGlobalFloat(GlobalWetnessId));
            float target = _sheltered ? 0f : weather;
            if (_wet < 0f) _wet = target;
            float rate = target > _wet ? 1f / Mathf.Max(0.1f, soakSeconds) : 1f / Mathf.Max(0.1f, drySeconds);
            _wet = Mathf.MoveTowards(_wet, target, rate * dt);

            if (Mathf.Abs(_wet - _lastWrittenWet) < 0.004f && _eyeShine == _lastWrittenShine) return;
            Write();
        }

        private bool ProbeShelter()
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            Renderer r = targetRenderers.Length > 0 ? targetRenderers[0] : null;
            if (r != null) origin = r.bounds.center + Vector3.up * (r.bounds.extents.y + 0.2f);
            return Physics.Raycast(origin, Vector3.up, shelterProbeHeight, shelterMask, QueryTriggerInteraction.Ignore);
        }

        private void Write()
        {
            float w = Mathf.SmoothStep(0f, 1f, _wet);
            float smin = Mathf.Lerp(_baseSmoothMin, wetSmoothnessFloor, w);
            float smax = Mathf.Lerp(_baseSmoothMax, 1f, w);
            float a = Mathf.Lerp(1f, wetAlbedoMultiplier, w);
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer r = targetRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(SmoothMinId, smin);
                _mpb.SetFloat(SmoothMaxId, smax);
                _mpb.SetColor(BaseColorId, new Color(_baseColor.r * a, _baseColor.g * a, _baseColor.b * a, _baseColor.a));
                _mpb.SetColor(EmissiveColorId, _eyeShine);
                r.SetPropertyBlock(_mpb);
            }
            _lastWrittenWet = _wet;
            _lastWrittenShine = _eyeShine;
        }
    }
}
