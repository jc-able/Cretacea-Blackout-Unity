using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Audio
{
    /// <summary>
    /// Lightweight staggered raycast wall/door audio occlusion component (AUD-4 fix).
    /// Drives AudioLowPassFilter cutoff (22 kHz open -> 920 Hz muffled behind concrete/closed doors)
    /// and smooth gain attenuation so raptors, thunder, and generators open up dramatically when doors swing open!
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioOcclusion3D : MonoBehaviour
    {
        [SerializeField] private float clearCutoffHz = 22000f;
        [SerializeField] private float occludedCutoffHz = 920f;
        [SerializeField] private float occludedVolumeScale = 0.36f;
        [SerializeField] private float checkIntervalSeconds = 0.14f;
        [SerializeField] private float smoothSpeed = 9.0f;

        private AudioSource _source;
        private AudioLowPassFilter _lowPass;
        private float _baseVolume = 1f;
        private float _targetOcclusion01;
        private float _currentOcclusion01;
        private float _nextCheckTime;
        private bool _occlusionActive = true;

        public float BaseVolume
        {
            get => _baseVolume;
            set => _baseVolume = Mathf.Clamp01(value);
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _lowPass = GetComponent<AudioLowPassFilter>();
            if (_lowPass == null) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowPass.cutoffFrequency = clearCutoffHz;
            _baseVolume = _source.volume;
            // Stagger checks across pooled emitters so rays spread evenly across frames
            _nextCheckTime = Time.time + UnityEngine.Random.Range(0f, checkIntervalSeconds);
        }

        public void Configure(float baseVol, bool enableOcclusion)
        {
            _baseVolume = Mathf.Clamp01(baseVol);
            _occlusionActive = enableOcclusion;
            if (!enableOcclusion)
            {
                _currentOcclusion01 = 0f;
                _targetOcclusion01 = 0f;
                if (_lowPass != null) _lowPass.cutoffFrequency = clearCutoffHz;
                if (_source != null) _source.volume = _baseVolume;
            }
            else
            {
                EvaluateOcclusionImmediate();
            }
        }

        private void Update()
        {
            if (!_occlusionActive || _source == null || !_source.isPlaying) return;

            if (Time.time >= _nextCheckTime)
            {
                _nextCheckTime = Time.time + checkIntervalSeconds;
                _targetOcclusion01 = SampleWallOcclusion();
            }

            _currentOcclusion01 = Mathf.Lerp(_currentOcclusion01, _targetOcclusion01, 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime));
            ApplyFilter();
        }

        public void EvaluateOcclusionImmediate()
        {
            _targetOcclusion01 = SampleWallOcclusion();
            _currentOcclusion01 = _targetOcclusion01;
            ApplyFilter();
        }

        private float SampleWallOcclusion()
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null) return 0f;

            Vector3 from = transform.position;
            Vector3 to = listener.position;
            Vector3 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 0.35f) return 0f;

            Vector3 dir = delta / dist;
            int mask = GameLayers.EnvironmentAndDoorMask;

            // Center ray + 2 subtle side-offset rays for natural doorway diffraction
            Vector3 right = Vector3.Cross(Vector3.up, dir).normalized * 0.35f;
            int blockedCount = 0;

            if (Physics.Raycast(from, dir, dist - 0.2f, mask, QueryTriggerInteraction.Ignore)) blockedCount++;
            if (Physics.Raycast(from + right, (to - (from + right)).normalized, dist - 0.2f, mask, QueryTriggerInteraction.Ignore)) blockedCount++;
            if (Physics.Raycast(from - right, (to - (from - right)).normalized, dist - 0.2f, mask, QueryTriggerInteraction.Ignore)) blockedCount++;

            return blockedCount / 3f;
        }

        private void ApplyFilter()
        {
            if (_lowPass != null)
            {
                // Logarithmic frequency blend sounds perceptually linear to the human ear
                float logClear = Mathf.Log(clearCutoffHz);
                float logOcc = Mathf.Log(occludedCutoffHz);
                _lowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(logClear, logOcc, _currentOcclusion01));
            }

            if (_source != null)
            {
                _source.volume = _baseVolume * Mathf.Lerp(1f, occludedVolumeScale, _currentOcclusion01);
            }
        }

        private static Transform ResolveListenerTransform()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam.transform;
            AudioListener listener = FindAnyObjectByType<AudioListener>();
            return listener != null ? listener.transform : null;
        }
    }
}
