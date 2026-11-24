using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// The storm around the player: rain streaks, ground ripples and roof-edge drips, kept out of every covered room.
    ///
    /// Roof occlusion: <see cref="RoofOcclusionMap"/> rasterises the RainBlocker slabs (layer 13) into a top-down
    /// roof-height window (_CretaceaRainOcclusionTex, RFloat, row 0 = min Z) around the player whenever the player
    /// has moved a few metres. The rain shader discards any fragment below the roof, so inside a room there is no
    /// rain, while the rain outside the window keeps falling.
    ///
    /// Weather values come from the AtmosphereController globals (_AtmoRainTarget / _AtmoWetnessTarget) when it
    /// drives the weather (_AtmoWeatherDriven = 1); the inspector values are only a fallback.
    /// Lighting of the particles: <see cref="WeatherLightSampler"/> (added at runtime).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class StormRainOcclusionController : MonoBehaviour
    {
        private static readonly int s_RainOcclusionTexId = Shader.PropertyToID("_CretaceaRainOcclusionTex");
        private static readonly int s_RainOcclusionParamsId = Shader.PropertyToID("_CretaceaRainOcclusionParams");
        private static readonly int s_GlobalWetnessId = Shader.PropertyToID("_GlobalWetness");
        private static readonly int s_RainIntensityId = Shader.PropertyToID("_RainIntensity");
        private static readonly int s_WindId = Shader.PropertyToID("_CretaceaWind");
        private static readonly int s_AtmoDrivenId = Shader.PropertyToID("_AtmoWeatherDriven");
        private static readonly int s_AtmoRainId = Shader.PropertyToID("_AtmoRainTarget");
        private static readonly int s_AtmoWetnessId = Shader.PropertyToID("_AtmoWetnessTarget");

        [Header("Tracking & Rain Intensity")]
        [SerializeField] private Transform followTarget;
        [Range(0f, 1f)] [SerializeField] private float rainIntensity = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float globalWetness = 0.85f;
        [SerializeField] private bool followMainCameraWhenUnassigned = true;

        [Header("Roof Occlusion Heightmap")]
        [Tooltip("Layers that occlude rain from above (Layer 13 RainBlocker by default).")]
        [SerializeField] private LayerMask rainBlockerMask = 1 << 13;
        [SerializeField] private float occlusionHalfExtent = 32f;
        [SerializeField] private float occlusionTopHeight = 60f;      // unused since the CPU map; kept for the prefab
        [SerializeField] private float occlusionDepthSpan = 120f;     // unused since the CPU map; kept for the prefab
        [SerializeField] private int occlusionResolution = 512;
        [SerializeField] private float recenterDistanceThreshold = 6f;

        [Header("Particle Emitters (Optional — Wired in Prefab)")]
        [SerializeField] private ParticleSystem rainStreaksSystem;
        [SerializeField] private ParticleSystem rainSplashesSystem;
        [SerializeField] private float maxStreakRate = 1800f;
        [SerializeField] private float maxSplashRate = 320f;
        [SerializeField] private float splashSpawnRadius = 18f;
        [SerializeField] private LayerMask groundSplashMask = (1 << 0) | (1 << 3) | (1 << 4);

        [SerializeField] private Shader occlusionHeightShader;        // unused since the CPU map; kept for the prefab

        // ---- tuning (code-owned so a rebuilt prefab cannot drift) ----
        private const float StreakBoxSize = 34f;        // emitter square, m (pushed ahead of the camera)
        private const float StreakAhead = 6f;           // emitter centre ahead of the camera, m
        private const float StreakTopAbove = 6f;        // emitter box centre above the player, m
        private const float StreakBoxHeight = 17f;      // the box fills the whole fall column, so turning never shows a gap
        private const float FallSpeedMin = 6.0f;        // terminal velocity: 1 mm drizzle drops ~4 m/s, 4 mm drops ~9 m/s
        private const float FallSpeedMax = 10.0f;
        private const float StreakRate = 9800f;         // drops / s at rain 1 (the box is 34 x 34 m)
        private const float DripRatePerMetre = 3.5f;    // drips / s per metre of roof edge at rain 1
        private const string CurtainMaterialResource = "WxRainCurtain";
        private static readonly float[] CurtainRadii = { 55f, 110f, 180f };
        private const float CurtainHeight = 120f;       // ring from 30 m below the camera to 90 m above it

        private Texture2D _occlusionTex;
        private float[] _heights;
        private Vector3 _lastCaptureCenter = new Vector3(float.MaxValue, 0f, float.MaxValue);
        private float _splashAccumulator, _dripAccumulator;
        private Vector3 _wind;
        private ParticleSystem _drips;
        private GameObject _curtainGo;
        private Material _curtainMat;
        private Mesh _curtainMesh;
        private readonly List<Vector3> _roofEdges = new List<Vector3>(1024);
        private readonly List<ParticleSystemVertexStream> _streams = new List<ParticleSystemVertexStream>
        {
            ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Color,
            ParticleSystemVertexStream.UV, ParticleSystemVertexStream.AgePercent, ParticleSystemVertexStream.StableRandomX
        };

        /// <summary>The live controller (one per scene).</summary>
        public static StormRainOcclusionController Instance { get; private set; }

        /// <summary>Roof-edge drip points found around the player (for tests).</summary>
        public int RoofEdgeCount => _roofEdges.Count;
        public int DripCount => _drips != null ? _drips.particleCount : 0;

        /// <summary>Wind at the player, m/s (world space). Also published as _CretaceaWind.</summary>
        public Vector3 Wind => _wind;

        public float RainIntensity
        {
            get => rainIntensity;
            set { rainIntensity = Mathf.Clamp01(value); ApplyShaderGlobals(); }
        }

        public float GlobalWetness
        {
            get => globalWetness;
            set { globalWetness = Mathf.Clamp01(value); ApplyShaderGlobals(); }
        }

        private void OnEnable()
        {
            Instance = this;
            RoofOcclusionMap.Collect(BlockerMask);
            EnsureOcclusionResources();
            ApplyShaderGlobals();
            ForceRefreshOcclusion();
            if (Application.isPlaying)
            {
                ConfigureParticleSystems();
                EnsureCompanion<WeatherLightSampler>();
                EnsureCompanion<WetnessDriver>();
                EnsureCompanion<LightningBoltController>();
                EnsureCompanion<DustMotesController>();
                LetRainShowThroughGlass();
                EnsureRainCurtains();
            }
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalVector(s_RainOcclusionParamsId, Vector4.zero);
            ReleaseOcclusionResources();
        }

        private void OnDestroy()
        {
            ReleaseOcclusionResources();
            foreach (Material m in _glassClones) if (m != null) Destroy(m);
            _glassClones.Clear();
            if (_curtainGo != null) Destroy(_curtainGo);
            if (_curtainMat != null) Destroy(_curtainMat);
            if (_curtainMesh != null) Destroy(_curtainMesh);
        }

        // Distant rain curtains: three camera-centred cylinders (55 m, 110 m, 180 m) drawn with Cretacea/HDRP/RainCurtain. They
        // put a lit rain haze in front of everything farther than ~60 m (hills, tree lines), flash with the lightning,
        // and vanish with the rain. One mesh, 288 triangles.
        private void EnsureRainCurtains()
        {
            if (_curtainGo != null) return;
            Material src = Resources.Load<Material>(CurtainMaterialResource);
            if (src == null)
            {
                Shader sh = Shader.Find("Cretacea/HDRP/RainCurtain");
                if (sh == null) return;
                src = new Material(sh);
            }
            _curtainMat = new Material(src) { name = "Rain curtains (runtime)" };
            _curtainMat.SetFloat("_Height", CurtainHeight);
            const int seg = 48;
            var verts = new List<Vector3>(seg * 4 + 4);
            var uv = new List<Vector2>(verts.Capacity);
            var uv2 = new List<Vector2>(verts.Capacity);
            var tris = new List<int>(seg * 12);
            for (int ring = 0; ring < CurtainRadii.Length; ring++)
            {
                float r = CurtainRadii[ring];
                int b = verts.Count;
                // the ring must close without a seam: the arc coordinate is scaled so the streak noise lattice holds a whole
                // number of cells around the ring (period in uv2.y, the shader wraps its lattice with it)
                float streakPeriod = Mathf.Round(2f * Mathf.PI * r * 2.4f);
                float circumference = streakPeriod / 2.4f;
                for (int k = 0; k <= seg; k++)
                {
                    float a = k / (float)seg * Mathf.PI * 2f;
                    var d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * r;
                    float arc = k / (float)seg * circumference;
                    verts.Add(d + Vector3.down * 30f); uv.Add(new Vector2(arc, 0f)); uv2.Add(new Vector2(ring, streakPeriod));
                    verts.Add(d + Vector3.up * (CurtainHeight - 30f)); uv.Add(new Vector2(arc, CurtainHeight)); uv2.Add(new Vector2(ring, streakPeriod));
                    if (k < seg)
                    {
                        int i0 = b + k * 2;
                        tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);
                        tris.Add(i0 + 1); tris.Add(i0 + 3); tris.Add(i0 + 2);
                    }
                }
            }
            _curtainMesh = new Mesh { name = "RainCurtains" };
            _curtainMesh.SetVertices(verts);
            _curtainMesh.SetUVs(0, uv);
            _curtainMesh.SetUVs(1, uv2);
            _curtainMesh.SetTriangles(tris, 0);
            _curtainMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 400f);
            _curtainGo = new GameObject("RainCurtains") { hideFlags = HideFlags.DontSave };
            _curtainGo.AddComponent<MeshFilter>().sharedMesh = _curtainMesh;
            var mr = _curtainGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _curtainMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            _curtainGo.SetActive(false);
        }

        private void UpdateRainCurtains()
        {
            if (_curtainGo == null) return;
            Camera cam = Camera.main;
            Transform centre = CaptureAnchor != null ? CaptureAnchor : (cam != null ? cam.transform : null);
            bool on = centre != null && rainIntensity > 0.02f;
            if (_curtainGo.activeSelf != on) _curtainGo.SetActive(on);
            if (on) _curtainGo.transform.position = centre.position;
        }

        private readonly List<Material> _glassClones = new List<Material>(4);

        // Window glass with "receive SSR (transparent)" renders a transparent depth PREPASS: it writes its depth into
        // the depth buffer before the transparents, so every rain particle behind a window fails the depth test and
        // the rain outside a window is never seen from inside. At runtime the glass gets a copy without that prepass
        // (the material ASSET is unchanged; the permanent fix is in the palette, see the vfx hand-in).
        private void LetRainShowThroughGlass()
        {
            var map = new Dictionary<Material, Material>();
            var mats = new List<Material>(4);
            int renderers = 0;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                r.GetSharedMaterials(mats);
                bool changed = false;
                for (int i = 0; i < mats.Count; i++)
                {
                    Material m = mats[i];
                    if (m == null || m.name.IndexOf("Glass", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!map.TryGetValue(m, out Material clone))
                    {
                        clone = null;
                        if (m.HasProperty("_ReceivesSSRTransparent") && m.GetFloat("_ReceivesSSRTransparent") > 0.5f
                            && m.HasProperty("_SurfaceType") && m.GetFloat("_SurfaceType") > 0.5f)
                        {
                            clone = new Material(m) { name = m.name + " (rain-visible)" };
                            clone.SetFloat("_ReceivesSSRTransparent", 0f);
                            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(clone);
                            clone.SetShaderPassEnabled("TransparentDepthPrepass", false);
                            _glassClones.Add(clone);
                        }
                        map[m] = clone;
                    }
                    if (clone == null) continue;
                    mats[i] = clone;
                    changed = true;
                }
                if (changed) { r.sharedMaterials = mats.ToArray(); renderers++; }
            }
            if (renderers > 0) Debug.Log($"[Weather] {renderers} window glass renderers: transparent depth prepass off, so the rain shows through");
        }

        private T EnsureCompanion<T>() where T : Component
        {
            T c = GetComponent<T>();
            return c != null ? c : gameObject.AddComponent<T>();
        }

        private int MapResolution => Mathf.Clamp(occlusionResolution, 128, 384);   // 384 over 64 m = 0.17 m per texel
        private float MapHalfExtent => Mathf.Clamp(occlusionHalfExtent, 20f, 48f);

        private void OnValidate()
        {
            // legacy fields of the GPU height camera, kept so old prefabs and scenes deserialize cleanly
            occlusionTopHeight = Mathf.Max(1f, occlusionTopHeight);
            occlusionDepthSpan = Mathf.Max(1f, occlusionDepthSpan);
            if (occlusionHeightShader == null) occlusionHeightShader = null;
        }

        private int BlockerMask => rainBlockerMask.value != 0 ? rainBlockerMask.value : (1 << 13);

        private void Update()
        {
            Transform target = ResolveFollowTarget();

            // The atmosphere preset owns the weather: follow it instead of the inspector values (which rained at dawn).
            if (Shader.GetGlobalFloat(s_AtmoDrivenId) > 0.5f)
            {
                rainIntensity = Mathf.Clamp01(Shader.GetGlobalFloat(s_AtmoRainId));
                globalWetness = Mathf.Clamp01(Shader.GetGlobalFloat(s_AtmoWetnessId));
            }

            UpdateWind();

            if (target != null)
            {
                Vector3 pos = target.position;
                // keep the emitter upwind so the slanted rain still lands around the player
                float fall = StreakTopAbove / ((FallSpeedMin + FallSpeedMax) * 0.5f);
                Camera cam = Camera.main;
                Vector3 ahead = cam != null ? cam.transform.forward : Vector3.zero;
                ahead.y = 0f;
                ahead = ahead.sqrMagnitude > 1e-4f ? ahead.normalized * StreakAhead : Vector3.zero;
                Vector3 next = new Vector3(pos.x - _wind.x * fall + ahead.x, pos.y + StreakTopAbove, pos.z - _wind.z * fall + ahead.z);
                bool teleported = (next - transform.position).sqrMagnitude > 12f * 12f;
                transform.position = next;
                // A teleport (chapter jump, respawn) would smear one frame of drops along the whole jump: restart the
                // rain around the new spot, already falling.
                if (teleported && Application.isPlaying) RestartRainAround();

                Vector2 deltaXZ = new Vector2(pos.x - _lastCaptureCenter.x, pos.z - _lastCaptureCenter.z);
                if (deltaXZ.sqrMagnitude >= recenterDistanceThreshold * recenterDistanceThreshold)
                    CaptureOcclusionAt(pos);
            }

            ApplyShaderGlobals();
            if (Application.isPlaying)
            {
                UpdateStreaks();
                UpdateSplashes(target);
                UpdateDrips(target);
            }
        }

        private void LateUpdate()
        {
            if (Application.isPlaying) UpdateRainCurtains();
        }

        private void UpdateWind()
        {
            // Storm wind from the sea (east-north-east), gusting. Stronger with the rain.
            float t = Application.isPlaying ? Time.time : 0f;
            float gust = Mathf.PerlinNoise(t * 0.23f, 0.37f) * 0.8f + Mathf.PerlinNoise(t * 1.1f, 4.1f) * 0.35f;
            float speed = Mathf.Lerp(1.0f, 5.5f, rainIntensity) * (0.55f + gust);
            float yaw = (-112f + (Mathf.PerlinNoise(t * 0.05f, 9.3f) - 0.5f) * 40f) * Mathf.Deg2Rad;
            _wind = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * speed;
            Shader.SetGlobalVector(s_WindId, new Vector4(_wind.x, _wind.y, _wind.z, speed));
        }

        /// <summary>Re-renders the roof occlusion map immediately (call after roof breaches like Lobby skylight).</summary>
        public void ForceRefreshOcclusion()
        {
            Transform target = ResolveFollowTarget();
            Vector3 center = target != null ? target.position : transform.position;
            CaptureOcclusionAt(center);
        }

        /// <summary>Collects the RainBlocker slabs again and refreshes the map (roofs added or removed).</summary>
        public void RecollectRoofs()
        {
            RoofOcclusionMap.Collect(BlockerMask);
            ForceRefreshOcclusion();
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            ForceRefreshOcclusion();
        }

        /// <summary>Roof top height above (x,z) inside the current window, or RoofOcclusionMap.NoRoof.</summary>
        public float RoofHeightAt(Vector3 p)
        {
            if (_heights == null) return RoofOcclusionMap.RoofHeightAt(p.x, p.z);
            float texel = MapHalfExtent * 2f / MapResolution;
            int ix = Mathf.FloorToInt((p.x - (_lastCaptureCenter.x - MapHalfExtent)) / texel);
            int iz = Mathf.FloorToInt((p.z - (_lastCaptureCenter.z - MapHalfExtent)) / texel);
            if (ix < 0 || iz < 0 || ix >= MapResolution || iz >= MapResolution) return RoofOcclusionMap.RoofHeightAt(p.x, p.z);
            return _heights[iz * MapResolution + ix];
        }

        /// <summary>True when a roof is above the point.</summary>
        public bool IsCovered(Vector3 p) => RoofHeightAt(p) > p.y + 0.1f;

        /// <summary>
        /// Look tools only: while set, rain, curtains and the roof map are centred here instead of on the player.
        /// A free capture camera far from the player otherwise sees the weather from outside (the rain curtains as
        /// vertical seams across the sky, no rain in front of the lens).
        /// </summary>
        public static Transform CaptureAnchor;

        private Transform ResolveFollowTarget()
        {
            if (CaptureAnchor != null) return CaptureAnchor;
            Camera main = Camera.main;
            if (followTarget != null)
            {
                // A cutscene camera away from the player: the rain belongs where the picture is (review 2: no rain
                // fell in any night shot of the opening, under a thunder caption).
                if (Application.isPlaying && main != null && main.isActiveAndEnabled
                    && (main.transform.position - followTarget.position).sqrMagnitude > 12f * 12f) return main.transform;
                return followTarget;
            }
            if (!followMainCameraWhenUnassigned) return null;
            return main != null ? main.transform : null;
        }

        private void EnsureOcclusionResources()
        {
            if (_occlusionTex == null)
            {
                _occlusionTex = new Texture2D(MapResolution, MapResolution, TextureFormat.RFloat, false, true)
                {
                    name = "Cretacea_RainRoofHeight",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            if (_heights == null || _heights.Length != MapResolution * MapResolution)
                _heights = new float[MapResolution * MapResolution];
        }

        private void CaptureOcclusionAt(Vector3 worldCenter)
        {
            EnsureOcclusionResources();
            RoofOcclusionMap.EnsureCollected(BlockerMask);
            _lastCaptureCenter = new Vector3(worldCenter.x, worldCenter.y, worldCenter.z);
            RoofOcclusionMap.Rasterize(new Vector2(worldCenter.x, worldCenter.z), MapHalfExtent, MapResolution, _heights);
            _occlusionTex.SetPixelData(_heights, 0);
            _occlusionTex.Apply(false, false);
            Shader.SetGlobalTexture(s_RainOcclusionTexId, _occlusionTex);
            Shader.SetGlobalVector(s_RainOcclusionParamsId, new Vector4(worldCenter.x, worldCenter.z, MapHalfExtent, 1f));
            FindRoofEdges(worldCenter);
        }

        // Roof edges near the player where water runs off: texels with a roof whose neighbour has none.
        private void FindRoofEdges(Vector3 center)
        {
            _roofEdges.Clear();
            float texel = MapHalfExtent * 2f / MapResolution;
            float x0 = center.x - MapHalfExtent, z0 = center.z - MapHalfExtent;
            int step = 2;
            for (int iz = step; iz < MapResolution - step; iz += step)
            {
                for (int ix = step; ix < MapResolution - step; ix += step)
                {
                    float h = _heights[iz * MapResolution + ix];
                    if (h <= RoofOcclusionMap.NoRoof + 1f) continue;
                    if (h < center.y + 1.8f || h > center.y + 14f) continue;
                    // which side is open sky? the drip leaves just past the eave on that side
                    int res = MapResolution;
                    Vector2 open = Vector2.zero;
                    if (_heights[iz * res + ix + step] < h - 1f) open.x += 1f;
                    if (_heights[iz * res + ix - step] < h - 1f) open.x -= 1f;
                    if (_heights[(iz + step) * res + ix] < h - 1f) open.y += 1f;
                    if (_heights[(iz - step) * res + ix] < h - 1f) open.y -= 1f;
                    if (open == Vector2.zero) continue;
                    open.Normalize();
                    // walk out to the first open-sky (or lower-roof) texel, then half a texel more: the drip must start
                    // outside the eave, or the roof test in the drip shader discards it
                    float cx = x0 + (ix + 0.5f) * texel, cz = z0 + (iz + 0.5f) * texel;
                    bool found = false;
                    Vector3 p = Vector3.zero;
                    for (int k = 1; k <= step + 5 && !found; k++)
                    {
                        float qx = cx + open.x * texel * k, qz = cz + open.y * texel * k;
                        int jx = Mathf.FloorToInt((qx - x0) / texel), jz = Mathf.FloorToInt((qz - z0) / texel);
                        if (jx < 0 || jz < 0 || jx >= res || jz >= res) break;
                        if (_heights[jz * res + jx] < h - 1f)
                        {
                            p = new Vector3(qx + open.x * texel, h - 0.25f, qz + open.y * texel);
                            found = true;
                        }
                    }
                    if (found) _roofEdges.Add(p);
                }
            }
        }

        private void ApplyShaderGlobals()
        {
            Shader.SetGlobalFloat(s_RainIntensityId, rainIntensity);
            Shader.SetGlobalFloat(s_GlobalWetnessId, globalWetness);
            if (_occlusionTex != null)
                Shader.SetGlobalTexture(s_RainOcclusionTexId, _occlusionTex);
        }

        // ------------------------------------------------------------------------------------------------------------
        private void ConfigureParticleSystems()
        {
            if (rainStreaksSystem != null)
            {
                ParticleSystem ps = rainStreaksSystem;
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.2f);
                main.startSpeed = 0f;
                // the drop is 2-6 mm; the renderer's min size holds the quad at about 1.5 px and the shader scales the
                // opacity by the real coverage, so a far streak is a faint hair, never a fat white dash
                main.startSize = new ParticleSystem.MinMaxCurve(0.003f, 0.007f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 1f));
                main.maxParticles = 26000;
                main.gravityModifier = 0f;
                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(StreakBoxSize, StreakBoxHeight, StreakBoxSize);
                shape.position = Vector3.zero;
                shape.rotation = Vector3.zero;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                var col = ps.colorOverLifetime;
                col.enabled = false;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 1f;
                r.velocityScale = 0.075f;    // 0.45-0.8 m streaks at 6-10 m/s: a 1/13 s film exposure, the movie rain look
                r.cameraVelocityScale = 0f;
                r.maxParticleSize = 0.05f;
                r.minParticleSize = 0.0014f;  // ~1-1.8 px at 720p; thinner lines break up under TAA (opacity carries the rest)
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.SetActiveVertexStreams(_streams);
                if (r.sharedMaterial != null) r.sharedMaterial.SetFloat("_Mode", 0f);
            }

            if (rainSplashesSystem != null)
            {
                ParticleSystem ps = rainSplashesSystem;
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);   // real ripple rings: 7-18 cm
                main.maxParticles = 2000;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.SetActiveVertexStreams(_streams);
                if (r.sharedMaterial != null) r.sharedMaterial.SetFloat("_Mode", 1f);
            }

            if (_drips == null && rainStreaksSystem != null)
            {
                var go = new GameObject("RoofEdgeDrips");
                go.transform.SetParent(transform, false);
                _drips = go.AddComponent<ParticleSystem>();
                _drips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = _drips.main;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
                main.startSpeed = 0f;
                main.startSize = new ParticleSystem.MinMaxCurve(0.010f, 0.016f);
                main.gravityModifier = 1f;
                main.maxParticles = 3000;
                var em = _drips.emission;
                em.enabled = false;
                var shape = _drips.shape;
                shape.enabled = false;
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 1f;
                r.velocityScale = 0.05f;
                r.maxParticleSize = 0.05f;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                Material src = rainStreaksSystem.GetComponent<ParticleSystemRenderer>().sharedMaterial;
                if (src != null)
                {
                    var m = new Material(src) { name = "MAT_RainDrips (runtime)" };
                    // drips start just past the eave, over open sky or a LOWER roof: keep the roof test, so a drip
                    // from a tall roof never falls through the lower roof into the room under it
                    m.SetFloat("_UseRoofOcclusion", 1f);
                    m.SetFloat("_Mode", 0f);
                    r.sharedMaterial = m;
                }
                r.SetActiveVertexStreams(_streams);
                _drips.Play();
            }
        }

        private void RestartRainAround()
        {
            if (rainStreaksSystem != null && rainIntensity > 0.01f)
            {
                rainStreaksSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                UpdateStreaks();
                rainStreaksSystem.Simulate(1.6f, true, true, false);
                rainStreaksSystem.Play(true);
            }
            if (rainSplashesSystem != null) rainSplashesSystem.Clear(true);
            if (_drips != null) _drips.Clear(true);
        }

        private void UpdateStreaks()
        {
            if (rainStreaksSystem == null) return;
            var em = rainStreaksSystem.emission;
            em.rateOverTime = StreakRate * rainIntensity * Mathf.Clamp01(maxStreakRate / 1800f);
            var vel = rainStreaksSystem.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(_wind.x * 0.7f, _wind.x * 1.3f);
            vel.y = new ParticleSystem.MinMaxCurve(-FallSpeedMax, -FallSpeedMin);
            vel.z = new ParticleSystem.MinMaxCurve(_wind.z * 0.7f, _wind.z * 1.3f);
        }

        // Ripples where drops land: raycast a random column down, accept only open-sky ground near the player.
        private void UpdateSplashes(Transform target)
        {
            if (rainSplashesSystem == null || target == null || rainIntensity <= 0.01f) return;
            _splashAccumulator += maxSplashRate * 10f * rainIntensity * Time.deltaTime;   // ~3200 / s in a 10 m disc
            int toEmit = Mathf.Min(90, Mathf.FloorToInt(_splashAccumulator));
            if (toEmit <= 0) return;
            _splashAccumulator -= toEmit;
            Vector3 basePos = target.position;
            Camera cam = Camera.main;
            Vector3 fwd = cam != null ? cam.transform.forward : Vector3.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;

            for (int i = 0; i < toEmit; i++)
            {
                // two thirds in front of the camera, where they are seen; denser close by
                Vector2 rnd = Random.insideUnitCircle;
                rnd *= rnd.magnitude;
                Vector3 offset = new Vector3(rnd.x, 0f, rnd.y) * Mathf.Min(splashSpawnRadius, 10f);
                if (i % 3 != 0 && Vector3.Dot(offset, fwd) < 0f) offset = Vector3.Reflect(offset, fwd);
                Vector3 origin = new Vector3(basePos.x + offset.x, basePos.y + 6f, basePos.z + offset.z);
                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 14f, groundSplashMask.value, QueryTriggerInteraction.Ignore))
                    continue;
                if (hit.normal.y < 0.6f) continue;
                if (RoofHeightAt(hit.point) > hit.point.y + 0.05f) continue;   // under a roof

                // rings only on standing water (the authored-wet road and slab materials, or inside a puddle);
                // elsewhere the drop just throws its crown
                if (IsStandingWater(hit))
                {
                    var emitParams = new ParticleSystem.EmitParams
                    {
                        position = hit.point + hit.normal * 0.012f,
                        applyShapeToPosition = false
                    };
                    rainSplashesSystem.Emit(emitParams, 1);
                }

                // the crown: a couple of droplets thrown up, they sparkle when a light is on them
                if (_drips != null && (i & 3) == 0)
                {
                    for (int k = 0; k < 1; k++)
                    {
                        Vector2 h = Random.insideUnitCircle * 0.6f;
                        var drop = new ParticleSystem.EmitParams
                        {
                            position = hit.point + Vector3.up * 0.02f,
                            velocity = new Vector3(h.x, Random.Range(0.9f, 1.7f), h.y),
                            startLifetime = Random.Range(0.18f, 0.3f),
                            startSize = Random.Range(0.003f, 0.006f),
                            startColor = new Color(1f, 1f, 1f, 0.5f),
                            applyShapeToPosition = false
                        };
                        _drips.Emit(drop, 1);
                    }
                }
            }
        }

        private readonly Dictionary<Collider, bool> _wetCollider = new Dictionary<Collider, bool>(256);

        private bool IsStandingWater(RaycastHit hit)
        {
            Collider c = hit.collider;
            if (c != null)
            {
                if (!_wetCollider.TryGetValue(c, out bool wet))
                {
                    Renderer r = c.GetComponent<Renderer>();
                    Material m = r != null ? r.sharedMaterial : null;
                    // "Wet" with a capital W: the palette's AsphaltWet / ConcreteFloorWet / FloorTileWet (the runtime
                    // clones are named "... (wet floor)", lower case)
                    wet = m != null && m.name.Contains("Wet");
                    _wetCollider[c] = wet;
                }
                if (wet) return true;
            }
            WetnessDriver wd = WetnessDriver.Instance;
            return wd != null && wd.IsInPuddle(hit.point);
        }

        private void UpdateDrips(Transform target)
        {
            if (_drips == null || target == null || _roofEdges.Count == 0 || rainIntensity <= 0.01f) return;
            Vector3 p0 = target.position;
            // edges within 16 m
            float rate = DripRatePerMetre * 0.38f * rainIntensity;   // each edge sample stands for ~0.38 m of edge
            _dripAccumulator += rate * Mathf.Min(_roofEdges.Count, 400) * Time.deltaTime;
            int toEmit = Mathf.Min(30, Mathf.FloorToInt(_dripAccumulator));
            if (toEmit <= 0) return;
            _dripAccumulator -= toEmit;
            for (int i = 0; i < toEmit; i++)
            {
                Vector3 e = _roofEdges[Random.Range(0, _roofEdges.Count)];
                if ((e - p0).sqrMagnitude > 16f * 16f) continue;
                var emitParams = new ParticleSystem.EmitParams
                {
                    position = e + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f)),
                    velocity = new Vector3(_wind.x * 0.1f, -Random.Range(0.5f, 2.0f), _wind.z * 0.1f),
                    applyShapeToPosition = false
                };
                _drips.Emit(emitParams, 1);
            }
        }

        private void ReleaseOcclusionResources()
        {
            if (_occlusionTex != null)
            {
                if (Application.isPlaying) Destroy(_occlusionTex);
                else DestroyImmediate(_occlusionTex);
                _occlusionTex = null;
            }
        }
    }
}
