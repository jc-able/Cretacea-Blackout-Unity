using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Publishes the light that reaches the weather particles: the 8 strongest point / spot lights around the camera
    /// (flashlight first), the key directional light (moon / sun), the lightning directional light and a sky ambient.
    /// Read by Cretacea/HDRP/RainParticle. Values are physical: candela for punctual lights, lux for directional
    /// lights, so the particle radiance follows the Look Bible units and HDRP's pre-exposure.
    ///
    /// Lamps under a roof are flagged: the rain ignores them (an indoor lamp would otherwise light the rain outside
    /// through the wall), the indoor dust motes use them. They are ranked by where the camera is.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherLightSampler : MonoBehaviour
    {
        public const int MaxLights = 8;

        [Tooltip("Lamps farther than this from the camera are ignored.")]
        [SerializeField] private float searchRadius = 45f;
        [Tooltip("Seconds between rescans of the scene's lights.")]
        [SerializeField] private float rescanInterval = 2f;
        [Tooltip("Sky dome illuminance as a fraction of the key directional light (moonlight scattered by the clouds).")]
        [SerializeField] private float ambientFromKey = 0.6f;
        [Tooltip("Luminance cap per lamp, nits (lamp diffusers are ~170 nits, signal lamps 45-70).")]
        [SerializeField] private float lampCapNits = 60f;
        [SerializeField] private float signalCapNits = 25f;
        [SerializeField] private float flashlightCapNits = 45f;
        [Tooltip("Slots kept for outdoor lamps (the rain's), whatever the camera's position.")]
        [SerializeField] private int reservedOutdoorSlots = 5;
        [Tooltip("Colour factor of a lamp with no line of sight to the camera (its rain is behind a wall).")]
        [SerializeField] private float blockedLampFactor = 0.25f;

        private static readonly int s_Pos = Shader.PropertyToID("_CretaceaWxLightPos");
        private static readonly int s_Col = Shader.PropertyToID("_CretaceaWxLightColor");
        private static readonly int s_Spot = Shader.PropertyToID("_CretaceaWxLightSpot");
        private static readonly int s_Spot2 = Shader.PropertyToID("_CretaceaWxLightSpot2");
        private static readonly int s_Count = Shader.PropertyToID("_CretaceaWxLightCount");
        private static readonly int s_DirCol0 = Shader.PropertyToID("_CretaceaWxDirColor0");
        private static readonly int s_DirFwd0 = Shader.PropertyToID("_CretaceaWxDirFwd0");
        private static readonly int s_DirCol1 = Shader.PropertyToID("_CretaceaWxDirColor1");
        private static readonly int s_DirFwd1 = Shader.PropertyToID("_CretaceaWxDirFwd1");
        private static readonly int s_Ambient = Shader.PropertyToID("_CretaceaWxAmbient");

        private struct Candidate
        {
            public Light light;
            public HDAdditionalLightData hd;
            public bool indoor;
            public bool signal;
        }

        private readonly List<Candidate> _candidates = new List<Candidate>(512);
        private readonly List<(float score, int index)> _scored = new List<(float, int)>(64);
        private readonly List<(float score, int index)> _outdoor = new List<(float, int)>(64);
        private readonly List<(float score, int index)> _picked = new List<(float, int)>(MaxLights);
        private readonly Vector4[] _pos = new Vector4[MaxLights];
        private readonly Vector4[] _col = new Vector4[MaxLights];
        private readonly Vector4[] _spot = new Vector4[MaxLights];
        private readonly Vector4[] _spot2 = new Vector4[MaxLights];
        private Light _keyLight, _lightningLight;
        private HDAdditionalLightData _keyHd, _lightningHd;
        private float _nextScan;

        /// <summary>The flashlight (or any light) that always takes slot 0 while it is on.</summary>
        public static Light PriorityLight { get; set; }

        /// <summary>Candela of the flashlight while it is on (0 when off).</summary>
        public static float CameraFlashlightLux { get; private set; }

        /// <summary>True while a roof is above the camera.</summary>
        public static bool CameraIndoors { get; private set; }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(s_Count, 0f);
            Shader.SetGlobalVector(s_DirCol0, Vector4.zero);
            Shader.SetGlobalVector(s_DirCol1, Vector4.zero);
            Shader.SetGlobalVector(s_Ambient, Vector4.zero);
        }

        private void Rescan()
        {
            _candidates.Clear();
            _keyLight = _lightningLight = null;
            RoofOcclusionMap.EnsureCollected(1 << 13);
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.type == LightType.Directional)
                {
                    string n = l.name.ToLowerInvariant();
                    if (n.Contains("lightning")) _lightningLight = l;
                    else if (_keyLight == null || l.intensity > _keyLight.intensity) _keyLight = l;
                    continue;
                }
                if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                bool isFlash = l.GetComponentInParent<Camera>() != null || l.name.ToLowerInvariant().Contains("flashlight");
                if (isFlash && PriorityLight == null && l.type == LightType.Spot) PriorityLight = l;
                if (l.name.ToLowerInvariant().Contains("bouncefill")) continue;
                string ln = l.name.ToLowerInvariant();
                _candidates.Add(new Candidate
                {
                    light = l,
                    hd = l.GetComponent<HDAdditionalLightData>(),
                    indoor = !isFlash && IsIndoorLamp(l.transform.position),
                    signal = ln.Contains("beacon") || ln.Contains("indicator") || ln.Contains("bollard") || ln.Contains("signal")
                });
            }
            _keyHd = _keyLight != null ? _keyLight.GetComponent<HDAdditionalLightData>() : null;
            _lightningHd = _lightningLight != null ? _lightningLight.GetComponent<HDAdditionalLightData>() : null;
        }

        // A lamp is indoor only when it is covered AND a 1.5 m ring around it is covered too: an eave, porch or canopy
        // lamp has open sky beside it and lights the rain falling past the roof edge.
        private static bool IsIndoorLamp(Vector3 p)
        {
            if (!RoofOcclusionMap.IsCovered(p, 0.05f)) return false;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                if (!RoofOcclusionMap.IsCovered(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 1.5f, 0.05f)) return false;
            }
            return true;
        }

        private static Color LightColor(Light l)
        {
            Color c = l.color.linear;
            if (l.useColorTemperature) c *= Mathf.CorrelatedColorTemperatureToRGB(l.colorTemperature);
            return c;
        }

        private static Vector4 DirColor(Light l, HDAdditionalLightData hd)
        {
            if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) return Vector4.zero;
            float dim = hd != null ? hd.lightDimmer : 1f;
            Color c = LightColor(l) * (l.intensity * dim);
            return new Vector4(c.r, c.g, c.b, 0f);
        }

        private static bool Blocked(Vector3 lamp, Vector3 camPos)
        {
            Vector3 d = camPos - lamp;
            float len = d.magnitude;
            if (len < 0.8f) return false;
            Vector3 dir = d / len;
            // start clear of the lamp's own fixture; window glass does not block (the rain is seen through it)
            int hits = Physics.RaycastNonAlloc(lamp + dir * 0.4f, dir, s_Hits, Mathf.Max(0f, len - 0.6f), (1 << 0) | (1 << 3), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider col = s_Hits[i].collider;
                if (col == null) continue;
                if (!s_GlassColliders.TryGetValue(col, out bool glass))
                {
                    Renderer r = col.GetComponent<Renderer>();
                    Material m = r != null ? r.sharedMaterial : null;
                    glass = m != null && m.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    s_GlassColliders[col] = glass;
                }
                if (!glass) return true;
            }
            return false;
        }

        private static readonly RaycastHit[] s_Hits = new RaycastHit[8];
        private static readonly Dictionary<Collider, bool> s_GlassColliders = new Dictionary<Collider, bool>(256);

        private void LateUpdate()
        {
            if (Time.unscaledTime >= _nextScan)
            {
                _nextScan = Time.unscaledTime + rescanInterval;
                Rescan();
            }

            Camera cam = Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : transform.position;

            _scored.Clear();
            bool camIndoor = RoofOcclusionMap.IsCovered(camPos, 0.1f);
            CameraIndoors = camIndoor;
            float r2 = searchRadius * searchRadius;
            for (int i = 0; i < _candidates.Count; i++)
            {
                Candidate c = _candidates[i];
                Light l = c.light;
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                float dim = c.hd != null ? c.hd.lightDimmer : 1f;
                float cd = l.intensity * dim;
                if (cd < 0.5f) continue;
                if (l == PriorityLight) { _scored.Add((float.MaxValue, i)); continue; }
                float d2 = (l.transform.position - camPos).sqrMagnitude;
                if (d2 > r2) continue;
                float place = c.indoor == camIndoor ? 1f : 0.15f;
                _scored.Add((place * cd / Mathf.Max(4f, d2), i));
            }
            _scored.Sort((a, b) => b.score.CompareTo(a.score));

            // Slot picking: the flashlight, then at least `reservedOutdoorSlots` outdoor lamps (the rain outside the
            // window keeps its lamp light while the camera is indoors), then the rest by score (indoor lamps for the
            // dust motes).
            _picked.Clear();
            _outdoor.Clear();
            for (int k = 0; k < _scored.Count; k++)
            {
                Candidate c = _candidates[_scored[k].index];
                if (c.light == PriorityLight) { _picked.Add(_scored[k]); continue; }
                if (!c.indoor) _outdoor.Add(_scored[k]);
            }
            int outdoorWanted = Mathf.Min(reservedOutdoorSlots, _outdoor.Count, MaxLights - _picked.Count);
            for (int k = 0; k < outdoorWanted; k++) _picked.Add(_outdoor[k]);
            for (int k = 0; k < _scored.Count && _picked.Count < MaxLights; k++)
                if (!_picked.Contains(_scored[k])) _picked.Add(_scored[k]);

            int n = _picked.Count;
            CameraFlashlightLux = 0f;
            for (int k = 0; k < n; k++)
            {
                Candidate c = _candidates[_picked[k].index];
                Light l = c.light;
                float dim = c.hd != null ? c.hd.lightDimmer : 1f;
                float range = Mathf.Max(0.5f, l.range);
                Vector3 p = l.transform.position;
                _pos[k] = new Vector4(p.x, p.y, p.z, 1f / (range * range));
                Color col = LightColor(l) * (l.intensity * dim);
                // no line of sight from the lamp to the camera: the rain it lights is mostly behind a wall
                if (l != PriorityLight && Blocked(p, camPos)) col *= blockedLampFactor;
                float cap = l == PriorityLight ? flashlightCapNits : (c.signal ? signalCapNits : lampCapNits);
                _col[k] = new Vector4(col.r, col.g, col.b, cap);
                Vector3 f = l.transform.forward;
                if (l.type == LightType.Spot)
                {
                    float cosOuter = Mathf.Cos(0.5f * l.spotAngle * Mathf.Deg2Rad);
                    float inner = Mathf.Clamp(l.innerSpotAngle, 0f, l.spotAngle - 0.5f);
                    float cosInner = Mathf.Cos(0.5f * inner * Mathf.Deg2Rad);
                    float scale = 1f / Mathf.Max(1e-3f, cosInner - cosOuter);
                    _spot[k] = new Vector4(f.x, f.y, f.z, scale);
                    _spot2[k] = new Vector4(-cosOuter * scale, c.indoor ? 1f : 0f, 0f, 0f);
                    if (l == PriorityLight) CameraFlashlightLux = l.intensity * dim;
                }
                else
                {
                    _spot[k] = new Vector4(0f, 0f, 1f, 0f);
                    _spot2[k] = new Vector4(1f, c.indoor ? 1f : 0f, 0f, 0f);
                }
            }
            for (int k = n; k < MaxLights; k++) { _pos[k] = Vector4.zero; _col[k] = Vector4.zero; _spot[k] = Vector4.zero; _spot2[k] = Vector4.zero; }

            Shader.SetGlobalVectorArray(s_Pos, _pos);
            Shader.SetGlobalVectorArray(s_Col, _col);
            Shader.SetGlobalVectorArray(s_Spot, _spot);
            Shader.SetGlobalVectorArray(s_Spot2, _spot2);
            Shader.SetGlobalFloat(s_Count, n);

            Vector4 key = DirColor(_keyLight, _keyHd);
            Vector4 flash = DirColor(_lightningLight, _lightningHd);
            Shader.SetGlobalVector(s_DirCol0, key);
            Shader.SetGlobalVector(s_DirFwd0, _keyLight != null ? (Vector4)_keyLight.transform.forward : Vector4.zero);
            Shader.SetGlobalVector(s_DirCol1, flash);
            Shader.SetGlobalVector(s_DirFwd1, _lightningLight != null ? (Vector4)_lightningLight.transform.forward : Vector4.zero);
            // clouds scatter the moon (and a strike) over the whole dome
            Shader.SetGlobalVector(s_Ambient, key * ambientFromKey + flash * 0.5f);
        }
    }
}
