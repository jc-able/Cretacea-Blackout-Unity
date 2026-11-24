using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Core
{
    /// <summary>
    /// Attach to any HDRP Light, emissive fixture, Control Room status wall, or map_park_modern quad
    /// to bind it cleanly to one of the 13 facility LightGroupIds.
    /// Guarantees map_park_modern cuts out cleanly when VCMain fails during blackout (POS-10 fix).
    /// </summary>
    public sealed class LightGroupMember : MonoBehaviour
    {
        [SerializeField] private LightGroupId groupId = LightGroupId.VCMain;
        [Tooltip("Optional ordering index used for sequential east-bound AuxRoute ripple (X >= 286 -> 401).")]
        [SerializeField] private int sequenceOrder;
        [Tooltip("If enabled, pulses/rotates while group is active (e.g. VCEmergency red beacons).")]
        [SerializeField] private bool rotatingBeacon;
        [SerializeField] private float rotationSpeedDegPerSec = 140f;
        [SerializeField] private bool pulsingEmergency;
        [SerializeField] private float pulseFrequencyHz = 1.15f;
        [Tooltip("Optional backup dim multiplier kept on even when group turns OFF (e.g. 0.12 for Lobby T. rex fossil).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float blackoutBackupFactor = 0f;
        [SerializeField] private Light[] managedLights;
        [SerializeField] private Renderer[] emissiveRenderers;
        [SerializeField] private GameObject[] toggleObjects;

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private HDAdditionalLightData[] _hdLights;
        private float[] _baseLightDimmers;
        private float[] _baseVolumetricDimmers;
        private float[] _baseIntensities;
        private MaterialPropertyBlock _mpb;
        private Color[] _baseEmissiveColors;
        private float _currentFactor = 1f;
        private bool _cached;

        public LightGroupId GroupId => groupId;
        public int SequenceOrder => sequenceOrder;

        private void Awake()
        {
            CacheDefaults();
        }

        private void OnEnable()
        {
            CacheDefaults();
            LightGroupController.EnsureInstance().RegisterMember(this);
        }

        private void OnDisable()
        {
            if (LightGroupController.Instance != null)
                LightGroupController.Instance.UnregisterMember(this);
        }

        private void Update()
        {
            if (_currentFactor <= 0.001f) return;

            if (rotatingBeacon)
            {
                transform.Rotate(Vector3.up, rotationSpeedDegPerSec * Time.deltaTime, Space.Self);
            }

            if (pulsingEmergency)
            {
                float wave = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseFrequencyHz * Mathf.PI * 2f));
                ApplyFactorImmediate(_currentFactor * wave, updateStoredFactor: false);
            }
        }

        private void CacheDefaults()
        {
            if (_cached)
            {
                // a script reload in Play mode keeps the serialized flag but drops the block (it threw every frame)
                if (_mpb == null) _mpb = new MaterialPropertyBlock();
                return;
            }
            _cached = true;
            _mpb = new MaterialPropertyBlock();

            if (managedLights == null || managedLights.Length == 0)
            {
                managedLights = GetComponentsInChildren<Light>(true);
            }

            _hdLights = new HDAdditionalLightData[managedLights.Length];
            _baseLightDimmers = new float[managedLights.Length];
            _baseVolumetricDimmers = new float[managedLights.Length];
            _baseIntensities = new float[managedLights.Length];

            for (int i = 0; i < managedLights.Length; i++)
            {
                Light l = managedLights[i];
                if (l == null) continue;
                _baseIntensities[i] = l.intensity;
                HDAdditionalLightData hd = l.GetComponent<HDAdditionalLightData>();
                _hdLights[i] = hd;
                _baseLightDimmers[i] = hd != null ? hd.lightDimmer : 1f;
                _baseVolumetricDimmers[i] = hd != null ? hd.volumetricDimmer : 1f;
            }

            if (emissiveRenderers != null && emissiveRenderers.Length > 0)
            {
                _baseEmissiveColors = new Color[emissiveRenderers.Length];
                for (int i = 0; i < emissiveRenderers.Length; i++)
                {
                    Renderer r = emissiveRenderers[i];
                    if (r == null || r.sharedMaterial == null) continue;
                    if (r.sharedMaterial.HasProperty(EmissiveColorId))
                        _baseEmissiveColors[i] = r.sharedMaterial.GetColor(EmissiveColorId);
                    else if (r.sharedMaterial.HasProperty(EmissionColorId))
                        _baseEmissiveColors[i] = r.sharedMaterial.GetColor(EmissionColorId);
                    else
                        _baseEmissiveColors[i] = Color.white;
                }
            }
        }

        public void SetFactor(float factor01)
        {
            CacheDefaults();
            float effective = Mathf.Max(Mathf.Clamp01(factor01), blackoutBackupFactor);
            ApplyFactorImmediate(effective, updateStoredFactor: true);
        }

        private void ApplyFactorImmediate(float factor, bool updateStoredFactor)
        {
            if (updateStoredFactor) _currentFactor = factor;
            bool visible = factor > 0.005f;

            for (int i = 0; i < managedLights.Length; i++)
            {
                Light l = managedLights[i];
                if (l == null) continue;
                l.enabled = visible;
                if (!visible) continue;

                HDAdditionalLightData hd = _hdLights[i];
                if (hd != null)
                {
                    hd.SetLightDimmer(_baseLightDimmers[i] * factor, _baseVolumetricDimmers[i] * factor);
                }
                else
                {
                    l.intensity = _baseIntensities[i] * factor;
                }
            }

            if (emissiveRenderers != null && _baseEmissiveColors != null)
            {
                for (int i = 0; i < emissiveRenderers.Length; i++)
                {
                    Renderer r = emissiveRenderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    Color scaled = _baseEmissiveColors[i] * factor;
                    _mpb.SetColor(EmissiveColorId, scaled);
                    _mpb.SetColor(EmissionColorId, scaled);
                    r.SetPropertyBlock(_mpb);
                }
            }

            if (toggleObjects != null)
            {
                for (int i = 0; i < toggleObjects.Length; i++)
                {
                    if (toggleObjects[i] != null)
                        toggleObjects[i].SetActive(visible);
                }
            }
        }
    }
}
