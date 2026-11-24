using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Keeps lamp shadows affordable. Measured on Nov 20 in the lit Visitor Center (121 shadow-casting lamps on):
    /// - every shadow-casting lamp costs CPU each frame even when its map is cached (17 ms with all of them, 9 ms with
    ///   none), so only lamps within <see cref="ShadowDistance"/> of the viewer (at most <see cref="MaxShadowed"/>)
    ///   cast shadows; a lamp that comes back into range re-renders its cached map once;
    /// - the live pass for moving things (dinosaurs, doors, the player) also redraws the terrain for every lamp
    ///   (17 ms more with 67 lamps), so only the <see cref="MaxLive"/> nearest lamps get it.
    /// Nobody can tell at these distances; a far lamp without shadows only leaks a little light through its own walls.
    /// Strong lamps (floods) are outside the budget: a 16,000 cd pen flood that lost its shadow lit the furniture of
    /// the security office through the wall, 13 m away (the opening's window shot, round 3). They keep their shadows
    /// out to <see cref="StrongShadowDistance"/>.
    /// </summary>
    public sealed class LampShadowBudget : MonoBehaviour
    {
        private const int MaxLive = 5;
        private const float LiveDistance = 24f;
        private const int MaxShadowed = 18;
        private const float ShadowDistance = 36f;
        private const float ShadowReleaseDistance = 42f;      // hysteresis: no flicker at the edge
        private const float Interval = 0.25f;
        private const float StrongCandela = 2500f;
        private const float StrongShadowDistance = 60f;
        private const float StrongReleaseDistance = 72f;
        // Review 3: a lamp's shadow edge showed as a ladder of steps on things right at the lens (the player's own
        // sleeve, the wall next to the camera). The two nearest shadowed lamps get twice their shadow map resolution
        // while the player stands within a few metres; point lights (six faces) stop at 1024.
        private const int NearBoostCount = 2;
        private const float NearBoostDistance = 7f;
        private const float NearBoostRelease = 9f;

        private readonly List<Light> _lights = new List<Light>();
        private readonly List<HDAdditionalLightData> _hd = new List<HDAdditionalLightData>();
        private readonly List<LightShadows> _authored = new List<LightShadows>();
        private readonly List<int> _order = new List<int>();
        private float[] _dist = new float[0];
        private float[] _peak = new float[0];                 // brightest the lamp has been (groups fade lamps in and out)
        private int[] _rank = new int[0];
        private bool[] _capOk = new bool[0];
        private bool[] _shadowed = new bool[0];
        private int[] _baseResolution = new int[0];
        private bool[] _boosted = new bool[0];
        private float _timer;
        private Transform _viewer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindAnyObjectByType<LampShadowBudget>() != null) return;
            var go = new GameObject("[LampShadowBudget]");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<LampShadowBudget>();
        }

        private void Start()
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional || l.shadows == LightShadows.None) continue;
                HDAdditionalLightData hd = l.GetComponent<HDAdditionalLightData>();
                if (hd == null || hd.shadowUpdateMode == ShadowUpdateMode.EveryFrame) continue;   // the flashlight
                hd.alwaysDrawDynamicShadows = false;
                _lights.Add(l);
                _hd.Add(hd);
                _authored.Add(l.shadows);
            }
            _dist = new float[_lights.Count];
            _peak = new float[_lights.Count];
            _rank = new int[_lights.Count];
            _capOk = new bool[_lights.Count];
            for (int i = 0; i < _peak.Length; i++) _peak[i] = _lights[i].intensity;
            _shadowed = new bool[_lights.Count];
            for (int i = 0; i < _shadowed.Length; i++) _shadowed[i] = true;
            _baseResolution = new int[_lights.Count];
            _boosted = new bool[_lights.Count];
            for (int i = 0; i < _baseResolution.Length; i++)
            {
                _baseResolution[i] = _hd[i].shadowResolution.useOverride ? _hd[i].shadowResolution.@override : 0;   // 0: set by quality level, left alone
            }
        }

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f || _lights.Count == 0) return;
            _timer = Interval;

            if (_viewer == null || !_viewer.gameObject.activeInHierarchy)
            {
                Camera cam = Camera.main;
                _viewer = cam != null ? cam.transform : null;
                if (_viewer == null) return;
            }
            Vector3 p = _viewer.position;

            // nearest first (only lamps that are on and inside their release distance)
            _order.Clear();
            for (int i = 0; i < _lights.Count; i++)
            {
                Light l = _lights[i];
                _rank[i] = int.MaxValue;
                _capOk[i] = false;
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) { _dist[i] = float.MaxValue; continue; }
                float d = (l.transform.position - p).magnitude;
                _dist[i] = d;
                if (l.intensity > _peak[i]) _peak[i] = l.intensity;
                if (d <= (_peak[i] >= StrongCandela ? StrongReleaseDistance : ShadowReleaseDistance)) _order.Add(i);
            }
            _order.Sort((a, b) => _dist[a].CompareTo(_dist[b]));

            // Rank by distance (the live pass goes to the nearest lamps, strong or not); the cap on shadow-casting lamps
            // counts the ordinary ones only: a strong lamp keeps its shadow whatever the count.
            int ordinary = 0;
            for (int k = 0; k < _order.Count; k++)
            {
                int i = _order[k];
                _rank[i] = k;
                _capOk[i] = _peak[i] >= StrongCandela || ordinary++ < MaxShadowed;
            }

            for (int i = 0; i < _lights.Count; i++)
            {
                if (_lights[i] == null) continue;
                bool strong = _peak[i] >= StrongCandela;
                float take = strong ? StrongShadowDistance : ShadowDistance;
                float release = strong ? StrongReleaseDistance : ShadowReleaseDistance;
                // keep a shadow until the lamp is clearly out of range, take a new one only when clearly inside
                bool want = _capOk[i] && (_shadowed[i] ? _dist[i] <= release : _dist[i] <= take);
                if (want != _shadowed[i])
                {
                    _shadowed[i] = want;
                    _lights[i].shadows = want ? _authored[i] : LightShadows.None;
                    if (want && _hd[i] != null) _hd[i].RequestShadowMapRendering();   // its cached map was released
                }

                bool live = want && _rank[i] < MaxLive && _dist[i] <= LiveDistance;
                if (_hd[i] != null && _hd[i].alwaysDrawDynamicShadows != live) _hd[i].alwaysDrawDynamicShadows = live;

                bool near = want && _baseResolution[i] > 0 && _rank[i] < NearBoostCount
                            && _dist[i] <= (_boosted[i] ? NearBoostRelease : NearBoostDistance);
                if (near != _boosted[i] && _hd[i] != null)
                {
                    _boosted[i] = near;
                    int cap = _lights[i].type == LightType.Point ? 1024 : 2048;
                    int res = near ? Mathf.Max(_baseResolution[i], Mathf.Min(cap, _baseResolution[i] * 2)) : _baseResolution[i];
                    _hd[i].SetShadowResolution(res);
                    _hd[i].RequestShadowMapRendering();
                }
            }
        }
    }
}
