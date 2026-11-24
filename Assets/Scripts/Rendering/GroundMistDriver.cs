using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Ground mist belongs to wet weather. The valley and stream mist volumes used to keep one density in every
    /// preset: in the dry golden afternoon they showed as a flat grey-brown slab at the foot of the trees (review 2),
    /// and under the low morning sun as a bright band with a hard top. This follows the atmosphere's wetness
    /// (_AtmoWetnessTarget, set by AtmosphereController): no mist in dry daylight, faint at dusk, full in the night
    /// rain, thinner again under the morning sun (_AtmoDaylight).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundMistDriver : MonoBehaviour
    {
        private static readonly int WetnessId = Shader.PropertyToID("_AtmoWetnessTarget");
        private static readonly int DaylightId = Shader.PropertyToID("_AtmoDaylight");

        [SerializeField] private string[] namePrefixes = { "ValleyGroundMist_", "StreamMist_" };
        [Tooltip("Wetness at which mist starts and at which it is full.")]
        [SerializeField] private Vector2 wetnessRange = new Vector2(0.15f, 0.70f);
        [Tooltip("Share of the night density left under a risen sun.")]
        [Range(0f, 1f)] [SerializeField] private float daylightShare = 0.75f;   // 0.45 until review 3: the dawn valley had no mist left for "the misty island"

        private LocalVolumetricFog[] _volumes;
        private float[] _baseMeanFreePath;
        private float _applied = -1f;

        private void OnEnable()
        {
            var found = new System.Collections.Generic.List<LocalVolumetricFog>();
            foreach (LocalVolumetricFog f in GetComponentsInChildren<LocalVolumetricFog>(true))
            {
                for (int i = 0; i < namePrefixes.Length; i++)
                {
                    if (f.name.StartsWith(namePrefixes[i])) { found.Add(f); break; }
                }
            }
            _volumes = found.ToArray();
            _baseMeanFreePath = new float[_volumes.Length];
            for (int i = 0; i < _volumes.Length; i++) _baseMeanFreePath[i] = Mathf.Max(0.05f, _volumes[i].parameters.meanFreePath);
            _applied = -1f;
        }

        private void OnDisable()
        {
            if (_volumes == null) return;
            for (int i = 0; i < _volumes.Length; i++)
            {
                if (_volumes[i] == null) continue;
                var p = _volumes[i].parameters;
                p.meanFreePath = _baseMeanFreePath[i];
                _volumes[i].parameters = p;
                _volumes[i].enabled = true;
            }
        }

        private void Update()
        {
            if (_volumes == null || _volumes.Length == 0) return;
            float wet = Shader.GetGlobalFloat(WetnessId);
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(wetnessRange.x, wetnessRange.y, wet));
            strength *= Mathf.Lerp(1f, daylightShare, Mathf.Clamp01(Shader.GetGlobalFloat(DaylightId)));
            if (Mathf.Abs(strength - _applied) < 0.01f) return;
            _applied = strength;
            bool on = strength > 0.04f;
            for (int i = 0; i < _volumes.Length; i++)
            {
                LocalVolumetricFog f = _volumes[i];
                if (f == null) continue;
                if (f.enabled != on) f.enabled = on;
                if (!on) continue;
                var p = f.parameters;
                p.meanFreePath = _baseMeanFreePath[i] / strength;
                f.parameters = p;
            }
        }
    }
}
