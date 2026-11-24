using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Raises the sea with the weather: reads the global surface wetness the AtmosphereController publishes
    /// (_GlobalWetness: day 0.08, dusk 0.35, night 0.75, storm 1.0, dawn 0.5) and eases the ocean's swell, chaos and
    /// whitecap foam towards a calm tropical sea or a storm sea. Cheap: a handful of field writes per second.
    /// </summary>
    [RequireComponent(typeof(WaterSurface))]
    public sealed class WaterWeatherDriver : MonoBehaviour
    {
        [SerializeField] private float calmWindSpeed = 26f;     // km/h: a fresh trade-wind breeze, swell lines and first whitecaps
        [SerializeField] private float stormWindSpeed = 48f;
        [SerializeField] private float calmFoam = 0.55f;
        [SerializeField] private float stormFoam = 0.75f;
        [SerializeField] private float response = 0.15f;      // 1/s

        [Header("Night: less turquoise glow, greyer foam (wetness is ~0.75 at night, 1 in the storm)")]
        [SerializeField] private Color nightScattering = new Color(0.010f, 0.060f, 0.065f);
        [SerializeField] private Color nightFoam = new Color(0.50f, 0.55f, 0.58f);

        private static readonly int GlobalWetnessId = Shader.PropertyToID("_GlobalWetness");
        private WaterSurface _water;
        private Color _dayScattering, _dayFoam;
        private float _storm = -1f;
        private float _applied = -1f;

        private void OnEnable()
        {
            _water = GetComponent<WaterSurface>();
            _dayScattering = _water.scatteringColor;
            _dayFoam = _water.foamColor;
        }

        private void Update()
        {
            if (_water == null) return;
            float wet = Shader.GetGlobalFloat(GlobalWetnessId);
            float target = Mathf.Clamp01((wet - 0.4f) / 0.6f);
            target = target * target * (3f - 2f * target);
            float next = _storm < 0f ? target : Mathf.MoveTowards(_storm, target, response * Time.deltaTime);
            // write in small steps only: every change regenerates the wave spectrum
            bool settled = next == target && _applied == target;
            if (settled || (_applied >= 0f && Mathf.Abs(next - _applied) < 0.02f && next != target)) { _storm = next; return; }
            _storm = next;
            _applied = next;
            _water.largeWindSpeed = Mathf.Lerp(calmWindSpeed, stormWindSpeed, _storm);
            _water.largeChaos = Mathf.Lerp(0.82f, 0.95f, _storm);
            _water.simulationFoamAmount = Mathf.Lerp(calmFoam, stormFoam, _storm);
            _water.ripplesWindSpeed = Mathf.Lerp(10f, 18f, _storm);
            _water.scatteringColor = Color.Lerp(_dayScattering, nightScattering, _storm);
            _water.foamColor = Color.Lerp(_dayFoam, nightFoam, _storm);
        }
    }
}
