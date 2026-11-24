using Cretacea.Core;
using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// A faint moon-side rim on the T. rex at night (review 3: "the reveal shows no T. rex": 30 px of dark skin in dark
    /// haze). One shadowless spot sits up toward the moon (azimuth 115, elevation 29, the storm key) and aims at the
    /// body, so the back, the skull and the moon side of the tail catch a cool edge that separates the animal from the
    /// treeline between strikes. The light is on its own light layer (bit 9) and only the animal's renderers listen to
    /// it: the ground under the animal never shows a moving pool. Off by day.
    /// Also raises the eyes' retro-reflection so the torch finds them across the valley (40-60 m).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrexMoonRim : MonoBehaviour
    {
        public const uint RimLayer = 1u << 9;

        [SerializeField] private float azimuthDeg = 115f;
        [SerializeField] private float elevationDeg = 29f;
        [SerializeField] private float distance = 11f;
        [Tooltip("Candela. 11 m away: ~14 lux on the moon side of the hide.")]
        [SerializeField] private float candela = 1700f;
        [SerializeField] private Color color = new Color(0.74f, 0.83f, 1.0f);
        [SerializeField] private float eyeRetroGain = 4f;

        private Light _light;
        private Renderer[] _renderers;
        private float _nextPresetCheck;
        private bool _night;

        private void Start()
        {
            var go = new GameObject("TrexMoonRim");
            go.transform.SetParent(transform, false);
            _light = go.AddComponent<Light>();
            _light.type = LightType.Spot;
            _light.color = color;
            _light.range = distance + 14f;
            _light.spotAngle = 80f;
            _light.innerSpotAngle = 50f;
            _light.shadows = LightShadows.None;
            _light.renderingLayerMask = (int)RimLayer;
            _light.intensity = 0f;

            _renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].renderingLayerMask |= RimLayer;

            var eyes = GetComponent<DinoEyeShine>();
            if (eyes != null) eyes.RetroGain = eyeRetroGain;
        }

        private void LateUpdate()
        {
            if (_light == null) return;
            if (Time.time >= _nextPresetCheck)
            {
                _nextPresetCheck = Time.time + 1f;
                AtmosphereController atmo = AtmosphereController.Instance;
                _night = atmo != null && (atmo.CurrentPreset == AtmospherePreset.NightStormBlackout || atmo.CurrentPreset == AtmospherePreset.NightCalm);
            }
            if (!_night)
            {
                if (_light.enabled) _light.enabled = false;
                return;
            }
            if (!_light.enabled) _light.enabled = true;

            float el = elevationDeg * Mathf.Deg2Rad, az = azimuthDeg * Mathf.Deg2Rad;
            Vector3 toMoon = new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
            Vector3 body = transform.position + Vector3.up * 3.0f;
            Transform lt = _light.transform;
            lt.position = body + toMoon * distance;
            lt.rotation = Quaternion.LookRotation(-toMoon, Vector3.up);
            _light.intensity = candela;
        }

        private void OnDestroy()
        {
            if (_renderers == null) return;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].renderingLayerMask &= ~RimLayer;
        }
    }
}
