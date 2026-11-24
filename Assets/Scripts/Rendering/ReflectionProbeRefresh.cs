using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Keeps an OnDemand HDRP reflection probe truthful: it re-renders when the scene starts (chapter start), once
    /// more after auto exposure has settled, and every time the facility light groups finish switching
    /// (LightGroupController raises the global shader float <c>_CretaceaLightEpoch</c>). Outdoor probes also re-render
    /// when an atmosphere blend ends (<see cref="RequestAllOutdoor"/>, or the global float
    /// <c>_CretaceaAtmosphereEpoch</c> changes); there is no periodic re-render.
    ///
    /// Budget: HDRP renders a pending OnDemand probe the next time its influence volume meets a camera frustum, so a
    /// request for a probe behind the player would wait and then render together with every other waiting probe the
    /// moment the player turns (the burst that overflowed the reflection atlas). This component therefore keeps the
    /// render pending on its own side and only asks HDRP when the probe's influence box is in the main camera's view,
    /// at most one probe per frame in the whole scene, and never while a lightning flash is lighting the world.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HDAdditionalReflectionData))]
    public sealed class ReflectionProbeRefresh : MonoBehaviour
    {
        public static readonly int LightEpochId = Shader.PropertyToID("_CretaceaLightEpoch");
        /// <summary>Global shader float an atmosphere change bumps when a preset blend has finished (outdoor probes
        /// re-render). Lets Cretacea.Core signal without referencing this assembly.</summary>
        public static readonly int AtmosphereEpochId = Shader.PropertyToID("_CretaceaAtmosphereEpoch");
        /// <summary>Bumped once a preset blend has finished and the exposure has settled (indoor probes follow it).</summary>
        public static readonly int AtmosphereSettledId = Shader.PropertyToID("_CretaceaAtmosphereSettled");
        /// <summary>1 while an atmosphere preset blend is running (sunset, moonrise, sunrise).</summary>
        public static readonly int AtmosphereBlendingId = Shader.PropertyToID("_CretaceaAtmosphereBlending");

        [Tooltip("Outdoor probes are also refreshed by RequestAllOutdoor (end of an atmosphere blend).")]
        [SerializeField] private bool outdoor;
        [Tooltip("Seconds after a light change before rendering (lets fades finish their last frame).")]
        [SerializeField] private float settleDelay = 0.2f;

        private static readonly List<ReflectionProbeRefresh> s_all = new List<ReflectionProbeRefresh>();
        private static int s_instanceCount;
        private static int s_lastRequestFrame = -1;
        private static int s_planesFrame = -1;
        private static bool s_planesValid;
        private static readonly Plane[] s_planes = new Plane[6];
        private static Light s_lightning;
        private static bool s_lightningSearched;

        /// <summary>Probe render requests handed to HDRP since start (diagnostics).</summary>
        public static int RequestCount { get; private set; }

        private HDAdditionalReflectionData _probe;
        private float _lastEpoch = float.NaN;
        private float _lastAtmoEpoch = float.NaN;
        private float _lastSettledEpoch = float.NaN;
        private float _renderAt = -1f;
        private float _secondPassAt;
        private float _stagger;
        // An outdoor capture is only true for the light it was taken in. While the sky is changing (and until the
        // first capture after it) the probe hands over to the live sky: review 2 saw a twilight capture shown at the
        // night exposure as a white rectangle of mirror on the wet plaza.
        private bool _stale;
        private float _freshAt = -1f;
        private float _weight = 1f;

        public bool Outdoor { get => outdoor; set => outdoor = value; }

        /// <summary>True while a render is waiting (for its delay, a lightning flash, its turn or the camera view).</summary>
        public bool Pending => _renderAt >= 0f;

        /// <summary>Re-render every outdoor probe (call when an atmosphere preset blend has finished).</summary>
        public static void RequestAllOutdoor()
        {
            foreach (ReflectionProbeRefresh r in s_all)
                if (r != null && r.outdoor) r.Schedule(0f);
        }

        private void OnEnable()
        {
            _probe = GetComponent<HDAdditionalReflectionData>();
            _stagger = (s_instanceCount++ % 10) * 0.06f;
            _lastEpoch = Shader.GetGlobalFloat(LightEpochId);
            _lastAtmoEpoch = Shader.GetGlobalFloat(AtmosphereEpochId);
            _lastSettledEpoch = Shader.GetGlobalFloat(AtmosphereSettledId);
            Schedule(0.05f);
            _secondPassAt = Time.time + 2.5f + _stagger;   // second pass once exposure and the atmosphere settled
            if (!s_all.Contains(this)) s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
        }

        private void Update()
        {
            float epoch = Shader.GetGlobalFloat(LightEpochId);
            if (epoch != _lastEpoch)
            {
                _lastEpoch = epoch;
                Schedule(settleDelay);
            }

            if (outdoor)
            {
                bool blending = Shader.GetGlobalFloat(AtmosphereBlendingId) > 0.5f;
                float atmo = Shader.GetGlobalFloat(AtmosphereEpochId);
                if (atmo != _lastAtmoEpoch)
                {
                    _lastAtmoEpoch = atmo;
                    _stale = true;
                    Schedule(0f);
                }
                if (blending) { _stale = true; _freshAt = -1f; }
                else if (_stale && _freshAt >= 0f && Time.time >= _freshAt) { _stale = false; _freshAt = -1f; }
                float target = _stale ? 0f : 1f;
                if (!Mathf.Approximately(_weight, target))
                {
                    _weight = Mathf.MoveTowards(_weight, target, Time.deltaTime * (target < _weight ? 3f : 1.4f));
                    if (_probe != null) _probe.weight = _weight;
                }
            }
            else
            {
                // rooms see the sky through windows and skylights: once the new sky has settled, capture it too
                float settled = Shader.GetGlobalFloat(AtmosphereSettledId);
                if (settled != _lastSettledEpoch)
                {
                    _lastSettledEpoch = settled;
                    Schedule(0f);
                }
            }

            if (_secondPassAt >= 0f && Time.time >= _secondPassAt)
            {
                _secondPassAt = -1f;
                Schedule(0f);
            }

            if (_renderAt < 0f || Time.time < _renderAt) return;
            if (LightningActive())
            {
                _renderAt = Time.time + 0.35f;
                return;
            }
            // one probe render per frame in the whole scene
            if (s_lastRequestFrame == Time.frameCount) return;
            // only when HDRP will render it right away (influence volume in view); otherwise keep it pending here
            if (!InView()) return;
            s_lastRequestFrame = Time.frameCount;
            _renderAt = -1f;
            if (_probe != null)
            {
                _probe.RequestRenderNextUpdate();
                RequestCount++;
                if (_stale) _freshAt = Time.time + 0.3f;       // the new capture is in the atlas a few frames later
            }
        }

        /// <summary>Ask for a render after <paramref name="delay"/> seconds (plus this probe's stagger).</summary>
        public void Schedule(float delay)
        {
            float t = Time.time + delay + _stagger;
            _renderAt = _renderAt < 0f ? t : Mathf.Min(_renderAt, t);
        }

        private bool InView()
        {
            if (s_planesFrame != Time.frameCount)
            {
                s_planesFrame = Time.frameCount;
                Camera cam = Camera.main;
                s_planesValid = cam != null && cam.isActiveAndEnabled;
                if (s_planesValid) GeometryUtility.CalculateFrustumPlanes(cam, s_planes);
            }
            if (!s_planesValid || _probe == null) return true;      // no gameplay camera: nothing to wait for
            InfluenceVolume iv = _probe.influenceVolume;
            Vector3 size = iv.shape == InfluenceShape.Sphere ? Vector3.one * (iv.sphereRadius * 2f) : iv.boxSize;
            Vector3 centre = transform.position;
            return GeometryUtility.TestPlanesAABB(s_planes, new Bounds(centre, size));
        }

        private static bool LightningActive()
        {
            if (!s_lightningSearched)
            {
                s_lightningSearched = true;
                GameObject go = GameObject.Find("LightningFlashLight");
                if (go != null) s_lightning = go.GetComponent<Light>();
            }
            return s_lightning != null && s_lightning.isActiveAndEnabled && s_lightning.intensity > 0.5f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
            s_instanceCount = 0;
            s_lastRequestFrame = -1;
            s_planesFrame = -1;
            s_lightning = null;
            s_lightningSearched = false;
            RequestCount = 0;
        }
    }
}
