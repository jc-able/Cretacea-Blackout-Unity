using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// The flash a burst of electrical sparks throws on nearby surfaces: a small warm point light (created at runtime,
    /// so the lamp table never sees it) that follows the number of live sparks with an arc-like flicker.
    /// Added to the spark ParticleSystems by the LookWeather step.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    [DisallowMultipleComponent]
    public sealed class SparkGlow : MonoBehaviour
    {
        [Tooltip("Peak intensity of the arc glow, candela (220 cd lights a shutter 1.5 m away at ~100 lux, a lamp pool).")]
        [SerializeField] private float peakCandela = 220f;
        [Tooltip("Range, m. Short, and the light casts hard shadows while it burns, so it never lights the far side of the shutter or wall.")]
        [SerializeField] private float range = 4.5f;
        [SerializeField] private float kelvin = 2600f;
        [Tooltip("Live sparks for full glow.")]
        [SerializeField] private int fullAt = 14;

        private ParticleSystem _ps;
        private Light _light;
        private float _level;

        public float CurrentCandela => _light != null && _light.enabled ? _light.intensity : 0f;

        private void Start()
        {
            _ps = GetComponent<ParticleSystem>();
            var go = new GameObject("SparkGlowLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.down * 0.15f;
            HDAdditionalLightData hd = go.AddHDLight(LightType.Point);
            _light = go.GetComponent<Light>();
            _light.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
            _light.useColorTemperature = true;
            _light.colorTemperature = kelvin;
            _light.color = new Color(1f, 0.82f, 0.6f);
            _light.range = range;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            if (hd != null) { hd.affectsVolumetric = true; hd.volumetricDimmer = 0.6f; }
            _light.enabled = false;
        }

        private void LateUpdate()
        {
            if (_ps == null || _light == null) return;
            float target = Mathf.Clamp01(_ps.particleCount / (float)Mathf.Max(1, fullAt));
            // fast attack, quick decay: an arc, not a lamp
            _level = target > _level ? target : Mathf.MoveTowards(_level, target, Time.deltaTime * 6f);
            float flicker = 0.55f + 0.45f * Mathf.PerlinNoise(Time.time * 37f, 1.7f);
            float cd = peakCandela * _level * flicker;
            bool on = cd > 0.3f;
            if (on != _light.enabled)
            {
                // shadows only while the arc burns (a point-light shadow costs six faces)
                _light.shadows = on ? LightShadows.Hard : LightShadows.None;
                _light.enabled = on;
            }
            _light.intensity = cd;
        }
    }
}
