using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// One diffuse bounce for a ceiling fixture. The game has no baked lighting, so a downward lamp would leave the
    /// ceiling and the upper walls black. The Look Pass adds a wide, shadowless spot under the floor below each
    /// fixture, pointing up (it lights the ceiling, the walls and the undersides, never the floor itself). This
    /// component keeps that light in step with its lamp: off when the lamp is off, dimmed and flickering with it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LampBounce : MonoBehaviour
    {
        [SerializeField] private Light source;
        [SerializeField] private Light bounce;
        [Tooltip("Candela of the bounce per candela of the lamp.")]
        [SerializeField] private float share = 0.45f;

        private HDAdditionalLightData _sourceHd;
        private HDAdditionalLightData _bounceHd;
        private bool _cached;

        public void Bind(Light sourceLight, Light bounceLight, float bounceShare)
        {
            source = sourceLight;
            bounce = bounceLight;
            share = bounceShare;
            _cached = false;
        }

        private void Cache()
        {
            _cached = true;
            _sourceHd = source != null ? source.GetComponent<HDAdditionalLightData>() : null;
            _bounceHd = bounce != null ? bounce.GetComponent<HDAdditionalLightData>() : null;
        }

        private void LateUpdate()
        {
            if (!_cached) Cache();
            if (bounce == null) return;

            bool on = source != null && source.enabled && source.gameObject.activeInHierarchy;
            float want = 0f;
            if (on)
            {
                float dimmer = _sourceHd != null ? _sourceHd.lightDimmer : 1f;
                want = source.intensity * dimmer * share;
            }

            if (want < 0.05f)   // 0.5 cd until round 4: it switched off every faint spill light (a desk lamp's pool)
            {
                if (bounce.enabled) bounce.enabled = false;
                return;
            }

            if (!bounce.enabled) bounce.enabled = true;
            if (Mathf.Abs(bounce.intensity - want) > want * 0.01f) bounce.intensity = want;
            // a light group may also manage this light: its dimmer is already part of the lamp's own value
            if (_bounceHd != null && Mathf.Abs(_bounceHd.lightDimmer - 1f) > 0.001f) _bounceHd.SetLightDimmer(1f, 0f);
        }
    }
}
