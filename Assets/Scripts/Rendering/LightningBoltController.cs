using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Makes lightning visible. On every strike (a rising edge of the global _LightningFlashIntensity, set by
    /// AtmosphereController) the clouds glow where the strike is, and some of the time a forked bolt is seen in the
    /// distance (350-900 m), built as a camera-facing ribbon mesh with branches. Its brightness follows the flash
    /// level frame by frame, so it flickers with the double stroke and dies with the fade.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LightningBoltController : MonoBehaviour
    {
        private static readonly int s_FlashId = Shader.PropertyToID("_LightningFlashIntensity");
        private static readonly int s_StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int s_ModeId = Shader.PropertyToID("_Mode");
        private const string MaterialResource = "WxLightningBolt";
        private static readonly int s_RainId = Shader.PropertyToID("_RainIntensity");
        private static readonly int s_SkyFlickerId = Shader.PropertyToID("_CretaceaSkyFlicker");
        private static readonly int s_LocalFlashId = Shader.PropertyToID("_LocalFlash");

        // Distant cloud flicker ("sheet lightning"): between the main strikes (AtmosphereController, every 13-32 s in
        // the storm) the cloud deck flickers somewhere in front of the player every few seconds, so even a 10-second
        // look reads as a storm. No light, no thunder: a glow on the clouds and on the distant rain curtains only.
        [SerializeField] private Vector2 flickerInterval = new Vector2(2.5f, 6.5f);
        [SerializeField] private float flickerGlowNits = 7f;
        [Tooltip("Illuminance the flicker adds to the distant rain curtains at its peak, lux.")]
        [SerializeField] private float flickerCurtainLux = 30f;

        // Review 2: a strike lit the ground while the sky the player was looking at stayed black whenever the strike
        // glow was out of view. Every strike now also lifts the whole cloud deck over the player, brightest toward the
        // strike, so the world shows as silhouettes against a lit sky.
        [SerializeField] private float skyGlowNits = 8f;
        [SerializeField] private float skyGlowSize = 2800f;

        [Range(0f, 1f)] [SerializeField] private float boltChance = 0.55f;
        [SerializeField] private Vector2 distanceRange = new Vector2(380f, 900f);
        [SerializeField] private Vector2 cloudBaseRange = new Vector2(260f, 380f);

        private Material _boltMat, _glowMat, _sheetMat, _skyMat;
        private Mesh _boltMesh, _glowMesh, _sheetMesh, _skyMesh;
        private GameObject _boltGo, _glowGo, _sheetGo, _skyGo;
        private float _nextFlicker = 3f, _flickerStart = -10f, _flickerAmp;
        private readonly float[] _pulseAt = new float[4];
        private readonly float[] _pulseAmp = new float[4];
        private int _pulses;
        private float _holdUntil = -1f;
        public int FlickersSeen { get; private set; }

        /// <summary>Starts a distant cloud flicker now and holds it at its peak for holdSeconds (tests, captures).</summary>
        public void ForceFlicker(float holdSeconds = 0f)
        {
            StartFlicker();
            _flickerAmp = 1f;
            _holdUntil = Time.time + Mathf.Max(0f, holdSeconds);
        }
        private float _prevFlash;
        private float _forcedUntil;
        private float _lastStrike = -10f;   // a double stroke (dip and re-rise inside one flash) is the same strike
        private readonly List<Vector3> _verts = new List<Vector3>(4096);
        private readonly List<Vector2> _uvs = new List<Vector2>(4096);
        private readonly List<Color> _cols = new List<Color>(4096);
        private readonly List<int> _tris = new List<int>(8192);
        private readonly List<Vector3> _path = new List<Vector3>(256);

        public int StrikesSeen { get; private set; }
        public int BoltsShown { get; private set; }

        private void Start()
        {
            Material src = Resources.Load<Material>(MaterialResource);
            if (src == null)
            {
                Shader sh = Shader.Find("Cretacea/HDRP/LightningBolt");
                if (sh == null) { enabled = false; return; }
                src = new Material(sh);
            }
            _boltMat = new Material(src) { name = "Lightning bolt (runtime)" };
            _boltMat.SetFloat(s_ModeId, 0f);
            _glowMat = new Material(src) { name = "Lightning cloud glow (runtime)" };
            _glowMat.SetFloat(s_ModeId, 1f);
            _glowMat.SetFloat("_GlowNits", 14f);

            _boltMesh = new Mesh { name = "LightningBolt" };
            _boltMesh.MarkDynamic();
            _glowMesh = new Mesh { name = "LightningGlow" };
            _boltGo = MakeRenderer("LightningBolt", _boltMesh, _boltMat);
            _glowGo = MakeRenderer("LightningCloudGlow", _glowMesh, _glowMat);
            _sheetMat = new Material(src) { name = "Lightning cloud flicker (runtime)" };
            _sheetMat.SetFloat(s_ModeId, 1f);
            _sheetMat.SetFloat("_GlowNits", flickerGlowNits);
            _sheetMat.SetFloat("_UseLocalFlash", 1f);
            _sheetMesh = new Mesh { name = "LightningFlicker" };
            _sheetGo = MakeRenderer("LightningCloudFlicker", _sheetMesh, _sheetMat);
            _skyMat = new Material(src) { name = "Lightning sky glow (runtime)" };
            _skyMat.SetFloat(s_ModeId, 1f);
            _skyMat.SetFloat("_GlowNits", skyGlowNits);
            _skyMat.SetFloat("_FogPenetration", 0.8f);
            _skyMesh = new Mesh { name = "LightningSkyGlow" };
            _skyGo = MakeRenderer("LightningSkyGlow", _skyMesh, _skyMat);
        }

        private GameObject MakeRenderer(string n, Mesh mesh, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(null, false);
            go.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            go.SetActive(false);
            return go;
        }

        private void OnDestroy()
        {
            if (_boltGo != null) Destroy(_boltGo);
            if (_glowGo != null) Destroy(_glowGo);
            if (_boltMat != null) Destroy(_boltMat);
            if (_glowMat != null) Destroy(_glowMat);
            if (_boltMesh != null) Destroy(_boltMesh);
            if (_glowMesh != null) Destroy(_glowMesh);
            if (_sheetGo != null) Destroy(_sheetGo);
            if (_sheetMat != null) Destroy(_sheetMat);
            if (_sheetMesh != null) Destroy(_sheetMesh);
            if (_skyGo != null) Destroy(_skyGo);
            if (_skyMat != null) Destroy(_skyMat);
            if (_skyMesh != null) Destroy(_skyMesh);
            Shader.SetGlobalFloat(s_SkyFlickerId, 0f);
        }

        private void UpdateFlicker(float mainFlash)
        {
            float rain = Shader.GetGlobalFloat(s_RainId);
            float level = 0f;
            float age = Time.time - _flickerStart;
            if (Time.time < _holdUntil) level = 1f;
            else if (age < 1.2f + Mathf.Max(0f, _holdUntil - _flickerStart))
            {
                for (int i = 0; i < _pulses; i++)
                {
                    float dt = age - Mathf.Max(0f, _holdUntil - _flickerStart) - _pulseAt[i];
                    if (dt >= 0f) level = Mathf.Max(level, _pulseAmp[i] * Mathf.Exp(-dt * 14f));
                }
            }
            else if (rain > 0.6f && mainFlash < 0.05f)
            {
                _nextFlicker -= Time.deltaTime;
                if (_nextFlicker <= 0f)
                {
                    _nextFlicker = Random.Range(flickerInterval.x, flickerInterval.y);
                    StartFlicker();
                }
            }
            level *= _flickerAmp;
            _sheetMat.SetFloat(s_LocalFlashId, level);
            Shader.SetGlobalFloat(s_SkyFlickerId, level * flickerCurtainLux);
            bool on = level > 0.004f;
            if (_sheetGo.activeSelf != on) _sheetGo.SetActive(on);
        }

        private void StartFlicker()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform ct = cam.transform;
            Vector3 fwd = ct.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * fwd;
            float dist = Random.Range(500f, 1100f);
            Vector3 c = ct.position + dir * dist;
            c.y = ct.position.y + Random.Range(cloudBaseRange.x, cloudBaseRange.y) + 40f;
            BuildQuad(_sheetMesh, c, ct.position, dist * Random.Range(0.6f, 1.0f));
            _sheetMat.SetFloat(s_StrengthId, 1f);
            _flickerStart = Time.time;
            _flickerAmp = Random.Range(0.55f, 1f);
            _pulses = Random.Range(2, 5);
            float t = 0f;
            for (int i = 0; i < _pulses; i++)
            {
                _pulseAt[i] = t;
                _pulseAmp[i] = i == 0 ? 1f : Random.Range(0.35f, 0.95f);
                t += Random.Range(0.06f, 0.22f);
            }
            FlickersSeen++;
        }

        private void LateUpdate()
        {
            if (_boltGo == null) return;
            float flash = Shader.GetGlobalFloat(s_FlashId);
            if (flash > 0.35f && _prevFlash < 0.15f && Time.time - _lastStrike > 0.6f && Time.time >= _forcedUntil) OnStrike(flash, null);
            _prevFlash = flash;
            UpdateFlicker(flash);
            if (flash < 0.01f)
            {
                if (_boltGo.activeSelf) _boltGo.SetActive(false);
                if (_glowGo.activeSelf) _glowGo.SetActive(false);
                if (_skyGo != null && _skyGo.activeSelf) _skyGo.SetActive(false);
            }
            else if (_skyGo != null && !_skyGo.activeSelf && _skyMesh.vertexCount == 0)
            {
                // a strike that began before this component saw a rising edge (held for a capture)
                OnStrike(flash, null);
            }
        }

        /// <summary>Shows a bolt now in front of the camera (for tests and scripted moments).</summary>
        public void ForceBolt(float strength = 1f)
        {
            _forcedUntil = Time.time + 0.75f;
            OnStrike(strength, true);
        }

        private void OnStrike(float strength, bool? forceBolt)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _lastStrike = Time.time;
            StrikesSeen++;
            Transform ct = cam.transform;
            Vector3 camFwd = ct.forward; camFwd.y = 0f;
            camFwd = camFwd.sqrMagnitude > 1e-4f ? camFwd.normalized : Vector3.forward;

            // where the strike is: from the lightning light's direction, pulled into view half of the time
            Vector3 dir = -camFwd;
            Light l = FindLightningLight();
            if (l != null) { dir = -l.transform.forward; dir.y = 0f; dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : camFwd; }
            if (Vector3.Angle(dir, camFwd) > 65f && Random.value < 0.6f)
                dir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * camFwd;

            bool bolt = forceBolt ?? (Random.value < boltChance);
            if (forceBolt == true) dir = Quaternion.Euler(0f, Random.Range(-18f, 18f), 0f) * camFwd;

            float dist = Random.Range(distanceRange.x, distanceRange.y);
            Vector3 groundPoint = ct.position + dir * dist;
            groundPoint.y = Mathf.Min(ct.position.y, 0f) - 5f;
            float top = ct.position.y + Random.Range(cloudBaseRange.x, cloudBaseRange.y);

            BuildGlow(new Vector3(groundPoint.x, top + 30f, groundPoint.z), ct.position, dist * 0.9f);
            _glowMat.SetFloat(s_StrengthId, Mathf.Lerp(0.6f, 1f, strength));
            _glowGo.SetActive(true);

            // the cloud deck over the player, shifted toward the strike
            BuildFlat(_skyMesh, new Vector3(ct.position.x, top + 70f, ct.position.z) + dir * (skyGlowSize * 0.22f), skyGlowSize);
            _skyMat.SetFloat(s_StrengthId, Mathf.Lerp(0.6f, 1f, strength));
            _skyGo.SetActive(true);

            if (!bolt) { _boltGo.SetActive(false); return; }
            BoltsShown++;
            BuildBolt(new Vector3(groundPoint.x + Random.Range(-60f, 60f), top, groundPoint.z + Random.Range(-60f, 60f)), groundPoint, ct.position, dist);
            _boltMat.SetFloat(s_StrengthId, Mathf.Lerp(0.7f, 1f, strength));
            _boltGo.SetActive(true);
        }

        private static Light FindLightningLight()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.name.ToLowerInvariant().Contains("lightning")) return l;
            return null;
        }

        private void BuildGlow(Vector3 center, Vector3 camPos, float size) => BuildQuad(_glowMesh, center, camPos, size);

        /// <summary>A horizontal sheet (the underside of the cloud deck), half-size metres each way.</summary>
        private static void BuildFlat(Mesh mesh, Vector3 center, float halfSize)
        {
            Vector3 x = Vector3.right * halfSize, z = Vector3.forward * halfSize;
            mesh.Clear();
            mesh.SetVertices(new List<Vector3> { center - x - z, center + x - z, center + x + z, center - x + z });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
        }

        private static void BuildQuad(Mesh mesh, Vector3 center, Vector3 camPos, float size)
        {
            Vector3 toCam = (camPos - center).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toCam).normalized;
            Vector3 up = Vector3.Cross(toCam, right).normalized;
            float w = size * 0.8f, h = size * 0.35f;
            mesh.Clear();
            mesh.SetVertices(new List<Vector3> { center - right * w - up * h, center + right * w - up * h, center + right * w + up * h, center - right * w + up * h });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new List<int> { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
        }

        // Midpoint displacement: a jagged channel between two points.
        private void Jagged(Vector3 a, Vector3 b, int depth, float rough, List<Vector3> outPts)
        {
            outPts.Clear();
            outPts.Add(a);
            outPts.Add(b);
            for (int d = 0; d < depth; d++)
            {
                for (int i = outPts.Count - 1; i > 0; i--)
                {
                    Vector3 p0 = outPts[i - 1], p1 = outPts[i];
                    float len = (p1 - p0).magnitude;
                    Vector3 mid = (p0 + p1) * 0.5f + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.35f, 0.35f), Random.Range(-1f, 1f)) * len * rough;
                    outPts.Insert(i, mid);
                }
            }
        }

        // A ribbon that narrows and dims toward its end: a channel is thickest where the current is.
        private void AddRibbon(List<Vector3> pts, Vector3 camPos, float halfWidthStart, float halfWidthEnd, float strengthStart, float strengthEnd)
        {
            int baseIndex = _verts.Count;
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = pts[i];
                Vector3 t = (i < n - 1 ? pts[i + 1] - p : p - pts[i - 1]).normalized;
                float v = i / (float)(n - 1);
                float halfWidth = Mathf.Lerp(halfWidthStart, halfWidthEnd, v * v * (3f - 2f * v));
                float strength = Mathf.Lerp(strengthStart, strengthEnd, v);
                Vector3 side = Vector3.Cross(t, (camPos - p).normalized).normalized * halfWidth;
                _verts.Add(p - side); _uvs.Add(new Vector2(-1f, v)); _cols.Add(new Color(1, 1, 1, strength));
                _verts.Add(p + side); _uvs.Add(new Vector2(1f, v)); _cols.Add(new Color(1, 1, 1, strength));
                if (i < n - 1)
                {
                    int k = baseIndex + i * 2;
                    _tris.Add(k); _tris.Add(k + 2); _tris.Add(k + 1);
                    _tris.Add(k + 1); _tris.Add(k + 2); _tris.Add(k + 3);
                }
            }
        }

        private readonly List<Vector3> _twig = new List<Vector3>(64);

        // Review 3: the bolt was a tube of one width with a few blunt forks and read as a decal. The main channel is
        // thinner and tapers a little; the forks leave it at a shallow angle, thin out to a point, fade, and the long
        // ones fork again.
        private void BuildBolt(Vector3 top, Vector3 bottom, Vector3 camPos, float dist)
        {
            _verts.Clear(); _uvs.Clear(); _cols.Clear(); _tris.Clear();
            float halfWidth = dist * 0.0095f;
            var main = new List<Vector3>(256);
            Jagged(top, bottom, 6, 0.16f, main);
            AddRibbon(main, camPos, halfWidth, halfWidth * 0.62f, 1f, 0.9f);

            int branches = Random.Range(5, 10);
            for (int b = 0; b < branches; b++)
            {
                int start = Random.Range(main.Count / 12, main.Count * 8 / 10);
                Vector3 s = main[start];
                float remaining = s.y - bottom.y;
                Vector3 outward = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
                float reach = Random.Range(0.18f, 0.55f);
                Vector3 e = s + outward * remaining * reach + Vector3.down * remaining * Random.Range(0.30f, 0.60f);
                Jagged(s, e, 4, 0.22f, _path);
                float w = halfWidth * Random.Range(0.30f, 0.55f);
                float a = Random.Range(0.30f, 0.60f);
                AddRibbon(_path, camPos, w, w * 0.06f, a, a * 0.25f);

                // the longer forks fork again
                if (reach > 0.34f && _path.Count > 6)
                {
                    int ts = Random.Range(_path.Count / 4, _path.Count * 3 / 4);
                    Vector3 p0 = _path[ts];
                    Vector3 away = (outward + new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f))).normalized;
                    float left = p0.y - bottom.y;
                    Vector3 p1 = p0 + away * left * Random.Range(0.12f, 0.28f) + Vector3.down * left * Random.Range(0.18f, 0.35f);
                    Jagged(p0, p1, 3, 0.24f, _twig);
                    AddRibbon(_twig, camPos, w * 0.5f, w * 0.03f, a * 0.6f, a * 0.12f);
                }
            }

            _boltMesh.Clear();
            _boltMesh.indexFormat = _verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            _boltMesh.SetVertices(_verts);
            _boltMesh.SetUVs(0, _uvs);
            _boltMesh.SetColors(_cols);
            _boltMesh.SetTriangles(_tris, 0);
            _boltMesh.RecalculateBounds();
        }
    }
}
