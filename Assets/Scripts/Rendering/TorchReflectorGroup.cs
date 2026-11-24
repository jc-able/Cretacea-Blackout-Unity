using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Retro-reflectors (road delineators, cat's-eyes) that light up in the player's torch: each reflector's emission
    /// follows the flashlight's illuminance at it (candela / d^2, cone falloff) times how squarely it faces the light,
    /// capped at signal-lens luminance (Look Bible: 14-22 nits), so a row of posts down a dark road answers the beam
    /// with a line of points instead of staying invisible. One component drives the whole set (no per-post Update);
    /// it writes _EmissiveColor through a MaterialPropertyBlock, the materials stay shared.
    /// </summary>
    public sealed class TorchReflectorGroup : MonoBehaviour
    {
        [SerializeField] private Renderer[] reflectors = System.Array.Empty<Renderer>();
        [SerializeField] private Color[] colors = System.Array.Empty<Color>();
        [Tooltip("Luminance per lux at the reflector (nits / lux): a retro-reflector sends the torch light back at the eye.")]
        [SerializeField] private float gain = 4.5f;
        [SerializeField] private float maxNits = 18f;
        [Tooltip("Faint glow without the torch (moonlight caught by the prisms), nits.")]
        [SerializeField] private float idleNits = 0.25f;

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private MaterialPropertyBlock _mpb;
        private Vector3[] _pos;
        private Vector3[] _normal;
        private float[] _current;
        private Light _torch;
        private float _nextSearch;

        public void Configure(Renderer[] renderers, Color[] tints)
        {
            reflectors = renderers ?? System.Array.Empty<Renderer>();
            colors = tints ?? System.Array.Empty<Color>();
        }

        private void Start()
        {
            _mpb = new MaterialPropertyBlock();
            int n = reflectors.Length;
            _pos = new Vector3[n];
            _normal = new Vector3[n];
            _current = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (reflectors[i] == null) continue;
                _pos[i] = reflectors[i].bounds.center;
                _normal[i] = reflectors[i].transform.forward;
                _current[i] = -1f;
            }
        }

        private Light FindTorch()
        {
            if (WeatherLightSampler.PriorityLight != null) return WeatherLightSampler.PriorityLight;
            if (Time.time < _nextSearch) return null;
            _nextSearch = Time.time + 2f;
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
            for (int i = 0; i < lights.Length; i++)
                if (lights[i].type == LightType.Spot && lights[i].name.StartsWith("Flashlight")) return lights[i];
            return null;
        }

        private void LateUpdate()
        {
            if (_pos == null) return;
            if (_torch == null) _torch = FindTorch();
            bool on = _torch != null && _torch.isActiveAndEnabled && _torch.intensity > 0f;
            Vector3 lp = on ? _torch.transform.position : Vector3.zero;
            Vector3 lf = on ? _torch.transform.forward : Vector3.forward;
            float range = on ? _torch.range : 0f;
            float halfAngle = on ? Mathf.Max(1f, 0.5f * _torch.spotAngle) : 1f;
            float k = 1f - Mathf.Exp(-16f * Time.deltaTime);

            for (int i = 0; i < reflectors.Length; i++)
            {
                Renderer r = reflectors[i];
                if (r == null) continue;
                float target = idleNits;
                if (on)
                {
                    Vector3 to = _pos[i] - lp;
                    float d = to.magnitude;
                    if (d > 0.3f && d < range)
                    {
                        Vector3 dir = to / d;
                        float a = Vector3.Angle(lf, dir) / halfAngle;
                        if (a < 1f)
                        {
                            float cone = Mathf.Lerp(1f, 0.12f, Mathf.SmoothStep(0f, 1f, (a - 0.3f) / 0.7f));
                            float facing = Mathf.InverseLerp(0.1f, 0.7f, Vector3.Dot(_normal[i], -dir));
                            float lux = _torch.intensity / (d * d) * cone;
                            target = Mathf.Max(idleNits, Mathf.Min(maxNits, lux * gain * facing));
                        }
                    }
                }
                float cur = _current[i] < 0f ? target : Mathf.Lerp(_current[i], target, k);
                if (Mathf.Abs(cur - _current[i]) < 0.002f) continue;
                _current[i] = cur;
                Color c = i < colors.Length ? colors[i] : Color.white;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissiveColorId, new Color(c.r * cur, c.g * cur, c.b * cur, 1f));
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
