using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Makes the open-air world wet in the rain and lets it dry for the dawn.
    ///
    /// At start every outdoor renderer (not under a RainBlocker roof, not skinned, not on the viewmodel/blocker layers)
    /// gets runtime clones of its HDRP/Lit materials; indoor renderers keep the shared materials, so rooms stay dry
    /// and no material ASSET is ever modified. The clones follow a smoothed wetness (wetting in ~15 s, drying over
    /// ~90 s): albedo darkens on porous surfaces, smoothness rises towards a wet sheen. Puddle decals placed by the
    /// LookWeather step (WxPuddle_*) fade in only when the ground is soaked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WetnessDriver : MonoBehaviour
    {
        private static readonly int s_AtmoDrivenId = Shader.PropertyToID("_AtmoWeatherDriven");
        private static readonly int s_AtmoWetnessId = Shader.PropertyToID("_AtmoWetnessTarget");
        private static readonly int s_GlobalWetnessId = Shader.PropertyToID("_GlobalWetness");
        private static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int s_Smoothness = Shader.PropertyToID("_Smoothness");
        private static readonly int s_SmoothMin = Shader.PropertyToID("_SmoothnessRemapMin");
        private static readonly int s_SmoothMax = Shader.PropertyToID("_SmoothnessRemapMax");
        private static readonly int s_Metallic = Shader.PropertyToID("_Metallic");
        private static readonly int s_NormalScale = Shader.PropertyToID("_NormalScale");
        private static readonly int s_SurfaceType = Shader.PropertyToID("_SurfaceType");
        private static readonly int s_WetnessId = Shader.PropertyToID("_CretaceaWetness");
        private static readonly int s_FlashId = Shader.PropertyToID("_LightningFlashIntensity");
        private static readonly int s_EmissiveColor = Shader.PropertyToID("_EmissiveColor");
        private const string AtlasPuddleMaterial = "MAT_WetPuddleDecal_Atlas";

        [Tooltip("Luminance of the lightning-lit sky mirrored in a puddle at full flash, nits (about 30 nits x the water's grazing Fresnel).")]
        [SerializeField] private float puddleFlashNits = 6f;
        [SerializeField] private Color puddleFlashColor = new Color(0.6f, 0.7f, 1f);

        [Tooltip("Seconds to soak when the rain starts.")]
        [SerializeField] private float wettingTime = 15f;
        [Tooltip("Seconds to dry when the rain stops.")]
        [SerializeField] private float dryingTime = 90f;
        [Tooltip("Smoothness a soaked rough FLOOR reaches (top of the mask-map range).")]
        [SerializeField] private float wetSmoothnessMax = 0.7f;
        [Tooltip("Most the bottom of the smoothness range may rise (rough pores stay rough; a large rise kills the diffuse at grazing angles).")]
        [SerializeField] private float wetSmoothnessMinRise = 0.05f;
        // All darkening values below are LINEAR albedo multipliers (_BaseColor is sRGB: the clone gets k^(1/2.2)).
        [Tooltip("Linear albedo multiplier of a soaked porous floor (concrete, asphalt, wood, soil).")]
        [SerializeField] private float wetDarkening = 0.8f;
        [Tooltip("Linear albedo multiplier of a soaked porous wall: rain only streaks a wall, it does not soak it like a slab.")]
        [SerializeField] private float wallDarkening = 0.68f;   // review 3: at 0.84 the service-path walls looked dry and newly poured in a downpour
        [Tooltip("Fraction of the floor sheen a wall gets.")]
        [SerializeField] private float wallSheen = 0.5f;
        [Tooltip("Flat sealed slabs (paving, concrete, asphalt, tile): smoothness a soaked slab reaches, and the rise of the bottom of its range (a film of water over the whole slab, so every lamp throws a streak).")]
        [SerializeField] private float slabSmoothnessMax = 0.94f;
        [SerializeField] private float slabSmoothnessMinRise = 0.56f;
        [Tooltip("Most the bottom of a soaked slab's smoothness range may reach (at 0.76 the plaza still read dry in NightCalm: the far lamps and windows only mirror sharply above ~0.85).")]
        [SerializeField] private float slabSmoothnessMinCap = 0.85f;
        [Tooltip("Linear albedo multiplier of a soaked slab: wet stone and concrete lose 30-50 % of their albedo, which is what makes the reflections read.")]
        [SerializeField] private float slabDarkening = 0.6f;
        [Tooltip("Draining ground (gravel, soil, grass): gets darker, not glossy. Top of its wet smoothness range.")]
        [SerializeField] private float drainSmoothnessMax = 0.5f;
        [SerializeField] private float drainDarkening = 0.68f;
        [Tooltip("Foliage (alpha-clipped leaves and fronds): linear albedo multiplier and the most its smoothness may rise. Leaves shed water; a floor's gloss on a fern reads as hoar-frost.")]
        [SerializeField] private float foliageDarkening = 0.85f;
        [SerializeField] private float foliageSmoothnessMax = 0.45f;
        [Tooltip("Water film without rain (the dawn after the storm): the slabs keep this fraction of their wet gloss, so the drying yard is damp, not a flooded mirror.")]
        [SerializeField] private float filmWithoutRain = 0.35f;
        [Tooltip("Normal-map strength of a soaked floor (water fills the small relief; full-strength normals under a wet film sparkle like glitter).")]
        [SerializeField] private float slabWetNormal = 0.4f;
        [SerializeField] private float drainWetNormal = 0.85f;

        private sealed class WetMat
        {
            public Material clone;
            public Color baseColor;
            public float smoothness, smoothMin, smoothMax, porosity;
            public bool hasRemap, hasSmoothness, hasColor;
            public bool authoredWet;   // the palette's *Wet materials: authored soaked, they dry out instead
            public bool upFacing;      // a floor / slab / roof (flat renderer); otherwise a wall
            public int kind;           // SurfaceSlab, SurfaceDrain or SurfaceOther
            public bool foliage;       // alpha-clipped plant material
            public float normalScale;
            public bool hasNormal;
        }

        private const int SurfaceOther = 0, SurfaceSlab = 1, SurfaceDrain = 2;

        private static int KindOf(string lowerName)
        {
            if (lowerName.Contains("gravel") || lowerName.Contains("mud") || lowerName.Contains("soil") || lowerName.Contains("dirt")
                || lowerName.Contains("grass") || lowerName.Contains("sand") || lowerName.Contains("moss") || lowerName.Contains("leaf")
                || lowerName.Contains("jungle"))
                return SurfaceDrain;
            if (lowerName.Contains("paving") || lowerName.Contains("plaza") || lowerName.Contains("concrete") || lowerName.Contains("asphalt")
                || lowerName.Contains("tile") || lowerName.Contains("stone") || lowerName.Contains("granite") || lowerName.Contains("bitumen")
                || lowerName.Contains("road") || lowerName.Contains("kerb") || lowerName.Contains("curb"))
                return SurfaceSlab;
            return SurfaceOther;
        }

        // materials that only ever cover open ground (never a wall, plinth or ceiling)
        private static bool IsGroundOnly(string lowerName)
        {
            return lowerName.Contains("paving") || lowerName.Contains("plaza") || lowerName.Contains("asphalt") || lowerName.Contains("gravel")
                || lowerName.Contains("mud") || lowerName.Contains("road");
        }

        // one clone per (material, floor-or-wall): walls see the sky at grazing angles and must not get a floor's gloss
        private readonly Dictionary<(Material, bool), WetMat> _clones = new Dictionary<(Material, bool), WetMat>(128);
        private readonly List<DecalProjector> _puddles = new List<DecalProjector>(128);
        private readonly List<float> _puddleBaseFade = new List<float>(128);
        private Material _puddleMat;          // runtime copy of the atlas puddle material (emission per strike)
        // Outdoor grime / stain decals whose material blends smoothness over the WHOLE projector box (mask blend source
        // 1 with no mask map): on a wet glossy slab that stamps a dull, light rectangle (review 1, 23_ch6_yard_entry).
        // They get runtime copies whose smoothness and normal follow the decal's own alpha shape, and wet up with the slab.
        private readonly List<(Material clone, float smoothness)> _decalClones = new List<(Material, float)>(16);
        private float _puddleLevel, _lastFlash = -1f;
        // footprints of the placed puddles, for the ripples (4 m grid -> puddle indices)
        private readonly Dictionary<Vector2Int, List<int>> _puddleGrid = new Dictionary<Vector2Int, List<int>>(256);
        private readonly List<Matrix4x4> _puddleWorldToLocal = new List<Matrix4x4>(256);
        private readonly List<Vector2> _puddleHalf = new List<Vector2>(256);
        // Wet reflections: HDRP's screen-space reflections skip every pixel smoother than minSmoothness (default 0.9),
        // so wet paving (0.5-0.86) only mirrored the dark sky probe and read bone dry (review 1, plaza). A runtime global
        // volume, weighted by the film of water and only while the camera is outdoors, lowers only the SSR smoothness
        // thresholds while the ground is wet.
        [Tooltip("SSR minimum smoothness while soaked (HDRP default 0.9).")]
        [SerializeField] private float wetSsrMinSmoothness = 0.70f;
        [SerializeField] private float wetSsrFadeStart = 0.76f;
        private Volume _wetVolume;
        private VolumeProfile _wetProfile;
        private float _film;   // current water film 0..1 (drives the wet-reflection volume)
        private float _wetness = -1f;
        private float _prevTarget = -1f;
        private float _applied = -1f;
        private float _appliedRain = -1f;
        private bool _built;
        private static readonly int s_RainId = Shader.PropertyToID("_RainIntensity");

        /// <summary>Current smoothed wetness of the outdoor world, 0..1.</summary>
        public float Wetness => Mathf.Max(0f, _wetness);
        public int ClonedMaterialCount => _clones.Count;
        public int PuddleCount => _puddles.Count;

        /// <summary>The live driver (one per scene).</summary>
        public static WetnessDriver Instance { get; private set; }

        private void Awake() { Instance = this; }

        /// <summary>True when the point lies inside a puddle that is currently filled (ripples belong there).</summary>
        public bool IsInPuddle(Vector3 p)
        {
            if (_puddleLevel < 0.2f) return false;
            var key = new Vector2Int(Mathf.FloorToInt(p.x / 4f), Mathf.FloorToInt(p.z / 4f));
            if (!_puddleGrid.TryGetValue(key, out List<int> list)) return false;
            for (int k = 0; k < list.Count; k++)
            {
                int i = list[k];
                Vector3 l = _puddleWorldToLocal[i].MultiplyPoint3x4(p);
                Vector2 h = _puddleHalf[i];
                // the atlas shapes fill about the inner 70 % of the box
                if (Mathf.Abs(l.x) < h.x * 0.7f && Mathf.Abs(l.y) < h.y * 0.7f && Mathf.Abs(l.z) < 0.5f) return true;
            }
            return false;
        }

        private void Start()
        {
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_puddleMat != null) Destroy(_puddleMat);
            foreach (var dc in _decalClones) if (dc.clone != null) Destroy(dc.clone);
            _decalClones.Clear();
            if (_wetProfile != null) Destroy(_wetProfile);
            foreach (var kv in _clones)
                if (kv.Value.clone != null) Destroy(kv.Value.clone);
            _clones.Clear();
        }

        private static bool IsWettable(Material m)
        {
            if (m == null || m.shader == null) return false;
            string s = m.shader.name;
            if (s != "HDRP/Lit" && s != "HDRP/LitTessellation") return false;
            if (m.HasProperty(s_SurfaceType) && m.GetFloat(s_SurfaceType) > 0.5f) return false;   // transparent (glass, water)
            string n = m.name.ToLowerInvariant();
            if (n.Contains("emissive") || n.Contains("glow") || n.Contains("glass") || n.Contains("screen") || n.Contains("water")) return false;
            return true;
        }

        // Review 3: the substation's 36 x 34 m pad stayed bone dry in the downpour because its centre sits under the
        // transformer canopy. Anything wider than 8 m is judged by nine probes across its footprint.
        private static bool IsMostlyCovered(Vector3 probe, Vector3 extents)
        {
            if (Mathf.Max(extents.x, extents.z) < 4f) return RoofOcclusionMap.IsCovered(probe, 0.05f);
            int covered = 0;
            for (int ix = -1; ix <= 1; ix++)
            {
                for (int iz = -1; iz <= 1; iz++)
                {
                    if (RoofOcclusionMap.IsCovered(probe + new Vector3(ix * extents.x * 0.7f, 0f, iz * extents.z * 0.7f), 0.05f)) covered++;
                }
            }
            return covered >= 5;
        }

        private void Build()
        {
            _built = true;
            RoofOcclusionMap.EnsureCollected(1 << 13);
            var mats = new List<Material>(8);
            int wetRenderers = 0;
            var mergedLog = new System.Text.StringBuilder();
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
            {
                int layer = r.gameObject.layer;
                if (layer == 12 || layer == 13 || layer == 5) continue;   // viewmodel, rain blocker, UI
                Bounds b = r.bounds;
                // a point near the top of the object: walls and room shells under a roof stay dry
                Vector3 probe = new Vector3(b.center.x, Mathf.Min(b.max.y, b.center.y + 0.5f), b.center.z);
                if (IsMostlyCovered(probe, b.extents)) continue;

                r.GetSharedMaterials(mats);
                Vector3 sz = b.size;
                bool flat = sz.y < 0.5f * Mathf.Min(sz.x, sz.z);
                // a merged mesh (many objects combined) spans a whole zone, so it is flat in bounds whatever it holds, and
                // concrete / granite / tile / kerb merges hold walls and plinths too: only ground-only materials count as floor
                bool merged = r.name.IndexOf("Merged", System.StringComparison.OrdinalIgnoreCase) >= 0;
                bool changed = false;
                for (int i = 0; i < mats.Count; i++)
                {
                    Material src = mats[i];
                    if (!IsWettable(src)) continue;
                    bool upFacing = flat && (!merged || IsGroundOnly(src.name.ToLowerInvariant()));
                    if (!_clones.TryGetValue((src, upFacing), out WetMat wm))
                    {
                        wm = MakeClone(src, upFacing);
                        _clones[(src, upFacing)] = wm;
                    }
                    mats[i] = wm.clone;
                    changed = true;
                }
                if (changed)
                {
                    r.sharedMaterials = mats.ToArray(); wetRenderers++;
                    if (merged) mergedLog.Append(r.name).Append(flat ? " (floor-capable) " : " ").Append("; ");
                }
            }

            _puddles.Clear();
            _puddleBaseFade.Clear();
            foreach (DecalProjector d in FindObjectsByType<DecalProjector>(FindObjectsInactive.Include))
            {
                if (d.name.IndexOf("Puddle", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                // the world's atlas puddles share one runtime copy, so a strike can light them without touching the asset
                if (d.material != null && d.material.name == AtlasPuddleMaterial)
                {
                    if (_puddleMat == null) _puddleMat = new Material(d.material) { name = AtlasPuddleMaterial + " (runtime)" };
                    d.material = _puddleMat;
                }
                int index = _puddles.Count;
                _puddles.Add(d);
                _puddleBaseFade.Add(d.fadeFactor > 0.01f ? d.fadeFactor : 1f);
                Transform t = d.transform;
                // decal space: x = width, y = height, z = projection depth (pivot shifts the box along z)
                Matrix4x4 w2l = Matrix4x4.TRS(t.position + t.rotation * d.pivot, t.rotation, Vector3.one).inverse;
                _puddleWorldToLocal.Add(w2l);
                _puddleHalf.Add(new Vector2(d.size.x * 0.5f, d.size.y * 0.5f));
                Vector3 c = t.position;
                float rad = Mathf.Max(d.size.x, d.size.y) * 0.5f;
                for (int gx = Mathf.FloorToInt((c.x - rad) / 4f); gx <= Mathf.FloorToInt((c.x + rad) / 4f); gx++)
                for (int gz = Mathf.FloorToInt((c.z - rad) / 4f); gz <= Mathf.FloorToInt((c.z + rad) / 4f); gz++)
                {
                    var key = new Vector2Int(gx, gz);
                    if (!_puddleGrid.TryGetValue(key, out List<int> list)) _puddleGrid[key] = list = new List<int>(4);
                    list.Add(index);
                }
            }
            int fixedDecals = FixOutdoorDecals();
            EnsureWetReflectionVolume();
            Debug.Log($"[Weather] wetness driver: {_clones.Count} outdoor materials cloned on {wetRenderers} renderers, {_puddles.Count} puddles, {fixedDecals} outdoor stain decals follow their shape. Merged meshes wetted: {mergedLog}");
        }

        private void EnsureWetReflectionVolume()
        {
            if (_wetVolume != null) return;
            _wetProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _wetProfile.name = "Wet reflections (runtime)";
            var ssr = _wetProfile.Add<ScreenSpaceReflection>(false);
            // the thresholds are properties over private serialized parameters: override those parameters
            OverrideParam(ssr, "m_MinSmoothness", wetSsrMinSmoothness);
            OverrideParam(ssr, "m_SmoothnessFadeStart", Mathf.Max(wetSsrMinSmoothness, wetSsrFadeStart));
            var go = new GameObject("WetReflectionsVolume") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _wetVolume = go.AddComponent<Volume>();
            _wetVolume.isGlobal = true;
            _wetVolume.priority = 50f;
            _wetVolume.sharedProfile = _wetProfile;
            _wetVolume.weight = 0f;
        }

        private static void OverrideParam(VolumeComponent c, string field, float value)
        {
            var fi = c.GetType().GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (fi != null && fi.GetValue(c) is VolumeParameter<float> p) p.Override(value);
        }

        private int FixOutdoorDecals()
        {
            var map = new Dictionary<Material, Material>();
            int n = 0;
            foreach (DecalProjector d in FindObjectsByType<DecalProjector>(FindObjectsInactive.Include))
            {
                Material m = d.material;
                if (m == null || d.name.IndexOf("Puddle", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (m.shader == null || m.shader.name != "HDRP/Decal" || !m.HasProperty("_MaskBlendSrc")) continue;
                if (RoofOcclusionMap.IsCovered(d.transform.position + Vector3.up * 0.3f, 0.05f)) continue;
                if (!map.TryGetValue(m, out Material clone))
                {
                    clone = null;
                    if (m.GetFloat("_MaskBlendSrc") > 0.5f && m.GetTexture("_MaskMap") == null)
                    {
                        clone = new Material(m) { name = m.name + " (outdoor wet)" };
                        clone.SetFloat("_MaskBlendSrc", 0f);
                        clone.SetFloat("_NormalBlendSrc", 0f);
                        _decalClones.Add((clone, m.HasProperty(s_Smoothness) ? m.GetFloat(s_Smoothness) : 0.5f));
                    }
                    map[m] = clone;
                }
                if (clone == null) continue;
                d.material = clone;
                n++;
            }
            return n;
        }

        private static WetMat MakeClone(Material src, bool upFacing)
        {
            var wm = new WetMat { clone = new Material(src) { name = src.name + (upFacing ? " (wet floor)" : " (wet wall)") }, upFacing = upFacing };
            wm.hasColor = src.HasProperty(s_BaseColor);
            wm.baseColor = wm.hasColor ? src.GetColor(s_BaseColor) : Color.white;
            wm.hasSmoothness = src.HasProperty(s_Smoothness);
            wm.smoothness = wm.hasSmoothness ? src.GetFloat(s_Smoothness) : 0.5f;
            wm.hasRemap = src.HasProperty(s_SmoothMax) && src.GetTexture("_MaskMap") != null;
            wm.smoothMin = wm.hasRemap ? src.GetFloat(s_SmoothMin) : 0f;
            wm.smoothMax = wm.hasRemap ? src.GetFloat(s_SmoothMax) : 1f;
            float metallic = src.HasProperty(s_Metallic) ? src.GetFloat(s_Metallic) : 0f;
            string n = src.name.ToLowerInvariant();
            if (n.Contains("metal") || n.Contains("steel") || n.Contains("iron")) metallic = Mathf.Max(metallic, 0.7f);
            float gloss = wm.hasRemap ? wm.smoothMax : wm.smoothness;
            // rough, non-metal surfaces soak up water and darken; glossy and metal ones only bead
            wm.porosity = Mathf.Clamp01(1f - metallic) * Mathf.Clamp01(1.25f - gloss);
            wm.authoredWet = n.Contains("wet");
            if (wm.authoredWet) wm.porosity = Mathf.Clamp01(1f - metallic) * 0.6f;
            wm.kind = metallic > 0.5f ? SurfaceOther : KindOf(n);
            bool clipped = src.HasProperty("_AlphaCutoffEnable") && src.GetFloat("_AlphaCutoffEnable") > 0.5f;
            wm.foliage = clipped || n.Contains("fern") || n.Contains("palm") || n.Contains("plant") || n.Contains("vine")
                || n.Contains("monstera") || n.Contains("banana") || n.Contains("_tree") || n.Contains("bush") || n.Contains("frond")
                || n.Contains("leaf") || n.Contains("foliage") || n.Contains("impostor");
            wm.hasNormal = src.HasProperty(s_NormalScale) && src.GetTexture("_NormalMap") != null;
            wm.normalScale = wm.hasNormal ? src.GetFloat(s_NormalScale) : 1f;
            return wm;
        }

        private float TargetWetness()
        {
            if (Shader.GetGlobalFloat(s_AtmoDrivenId) > 0.5f) return Mathf.Clamp01(Shader.GetGlobalFloat(s_AtmoWetnessId));
            return Mathf.Clamp01(Shader.GetGlobalFloat(s_GlobalWetnessId));
        }

        private void Update()
        {
            if (!_built) return;
            float target = TargetWetness();
            if (_wetness < 0f) _wetness = target;   // start in the state the scene starts in
            // a jump in the weather inside one frame is a checkpoint load or chapter jump, not weather: snap to it
            if (_prevTarget >= 0f && Mathf.Abs(target - _prevTarget) > 0.3f) _wetness = target;
            float rate = target > _wetness ? 1f / Mathf.Max(0.1f, wettingTime) : 1f / Mathf.Max(0.1f, dryingTime);
            // a scripted fade (the 8.5 s dawn ending: 1.0 -> 0.5) moves the target faster than the natural drying: never
            // lag behind it, or the dawn yard is still a flooded mirror at the crane shot (fix round, 28_ending_2)
            if (_prevTarget >= 0f) rate = Mathf.Max(rate, Mathf.Abs(target - _prevTarget) / Mathf.Max(Time.deltaTime, 1e-4f));
            _prevTarget = target;
            _wetness = Mathf.MoveTowards(_wetness, target, rate * Time.deltaTime);
            Shader.SetGlobalFloat(s_WetnessId, _wetness);
            UpdatePuddleFlash();
            // wet reflections only while the camera is out in the rain: under a roof (rooms, the dock canopy) the
            // interiors keep their own reflection setup (probes), so this never changes another package's rooms
            // (the thresholds only admit slabs >= 0.70 and puddles, not foliage or props, so the plaza seen from the lobby
            // keeps its wet reflections and dry interiors are unaffected unless their floors are already that smooth)
            if (_wetVolume != null)
                _wetVolume.weight = Mathf.MoveTowards(_wetVolume.weight, _film, Time.deltaTime * 1.5f);
            float rain = Mathf.Clamp01(Shader.GetGlobalFloat(s_RainId));
            if (Mathf.Abs(_wetness - _applied) < 0.004f && Mathf.Abs(rain - _appliedRain) < 0.01f) return;
            _applied = _wetness;
            _appliedRain = rain;
            Apply(_wetness);
        }

        // A strike's bright sky mirrored in the standing water (HDRP decal emission, scaled by the decal's fade).
        private void UpdatePuddleFlash()
        {
            if (_puddleMat == null) return;
            float flash = Mathf.Clamp01(Shader.GetGlobalFloat(s_FlashId));
            if (Mathf.Abs(flash - _lastFlash) < 0.002f) return;
            _lastFlash = flash;
            Color c = puddleFlashColor * (puddleFlashNits * flash);
            _puddleMat.SetColor(s_EmissiveColor, new Color(c.r, c.g, c.b, 1f));
        }

        /// <summary>Applies a wetness immediately (0..1) — for previews and tests.</summary>
        public void SetWetnessImmediate(float w)
        {
            _wetness = Mathf.Clamp01(w);
            _applied = _wetness;
            Apply(_wetness);
        }

        // sRGB multiplier for a linear albedo multiplier lerped by t (the material colours are sRGB, LOOK_BIBLE section 4)
        private static float LinearDarken(float linearK, float t) => Mathf.Pow(Mathf.Lerp(1f, linearK, Mathf.Clamp01(t)), 1f / 2.2f);

        private void Apply(float w)
        {
            // water first darkens (soaks), then builds a film (gloss). The film stands only while it rains: without rain
            // (the dawn after the storm) a slab keeps a fraction of it, damp rather than flooded. Steady light rain
            // (NightCalm 0.45) already keeps the whole film.
            float rainW = Mathf.Lerp(filmWithoutRain, 1f, Mathf.Clamp01(Shader.GetGlobalFloat(s_RainId) / 0.4f));
            float soak = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(w * 1.6f));
            float film = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((w - 0.1f) / 0.6f)) * rainW;
            foreach (var kv in _clones)
            {
                WetMat m = kv.Value;
                if (m.clone == null) continue;
                if (m.authoredWet)
                {
                    // dry = lighter and duller than the soaked look it was authored with; the colour follows the soak,
                    // the gloss follows the film (so the damp dawn asphalt is dark but no longer a mirror)
                    float dryColour = 1f - soak;
                    float dry = 1f - film;
                    if (m.hasColor)
                    {
                        float kd = Mathf.Lerp(1f, 1.35f, dryColour * m.porosity);
                        Color c0 = m.baseColor;
                        m.clone.SetColor(s_BaseColor, new Color(c0.r * kd, c0.g * kd, c0.b * kd, c0.a));
                    }
                    if (m.hasRemap)
                    {
                        // soaked top capped at 0.85: above it the fine asphalt normals alias into glitter under the low
                        // storm moon (review 1, NightStormBlackout_coast_eye)
                        float wetTop = m.upFacing ? Mathf.Min(m.smoothMax, 0.85f) : m.smoothMax;
                        m.clone.SetFloat(s_SmoothMax, Mathf.Lerp(wetTop, m.smoothMax * 0.62f, dry));
                        // soaked, the whole slab stays above the wet-SSR threshold (0.70), so it reflects as one film
                        float wetBottom = m.upFacing ? Mathf.Max(Mathf.Min(m.smoothMin, wetTop - 0.1f), Mathf.Min(0.72f, wetTop - 0.05f)) : Mathf.Min(m.smoothMin, wetTop - 0.1f);
                        m.clone.SetFloat(s_SmoothMin, Mathf.Lerp(wetBottom, m.smoothMin * 0.35f, dry));
                    }
                    else if (m.hasSmoothness)
                        m.clone.SetFloat(s_Smoothness, Mathf.Lerp(m.smoothness, m.smoothness * 0.6f, dry));
                    // authored soaked: the film fills the relief, so the full normal map only returns as it dries
                    if (m.hasNormal && m.upFacing)
                        m.clone.SetFloat(s_NormalScale, m.normalScale * Mathf.Lerp(1f, slabWetNormal * 0.85f, 1f - dry));
                    continue;
                }
                if (m.foliage)
                {
                    // leaves shed the water: a little darker, a soft sheen at most, normals untouched
                    if (m.hasColor)
                    {
                        float kf = LinearDarken(foliageDarkening, soak);
                        Color cf = m.baseColor;
                        m.clone.SetColor(s_BaseColor, new Color(cf.r * kf, cf.g * kf, cf.b * kf, cf.a));
                    }
                    if (m.hasRemap)
                        m.clone.SetFloat(s_SmoothMax, Mathf.Lerp(m.smoothMax, Mathf.Min(m.smoothMax, foliageSmoothnessMax), film));
                    else if (m.hasSmoothness)
                        m.clone.SetFloat(s_Smoothness, Mathf.Lerp(m.smoothness, Mathf.Min(m.smoothness, foliageSmoothnessMax), film));
                    continue;
                }
                if (m.upFacing && m.kind == SurfaceDrain)
                {
                    // gravel, soil, grass: the water drains away. Darker, a soft low sheen, no film
                    if (m.hasColor)
                    {
                        float kd = LinearDarken(drainDarkening, soak);
                        Color cd = m.baseColor;
                        m.clone.SetColor(s_BaseColor, new Color(cd.r * kd, cd.g * kd, cd.b * kd, cd.a));
                    }
                    if (m.hasRemap)
                    {
                        float hi = Mathf.Min(m.smoothMax, drainSmoothnessMax);
                        hi = Mathf.Max(hi, Mathf.Min(m.smoothMin + 0.2f, drainSmoothnessMax));
                        m.clone.SetFloat(s_SmoothMax, Mathf.Lerp(m.smoothMax, hi, film));
                        m.clone.SetFloat(s_SmoothMin, Mathf.Lerp(m.smoothMin, Mathf.Min(m.smoothMin + wetSmoothnessMinRise, hi - 0.1f), film));
                    }
                    else if (m.hasSmoothness)
                        m.clone.SetFloat(s_Smoothness, Mathf.Lerp(m.smoothness, Mathf.Min(Mathf.Max(m.smoothness, 0.35f), drainSmoothnessMax), film));
                    if (m.hasNormal) m.clone.SetFloat(s_NormalScale, m.normalScale * Mathf.Lerp(1f, drainWetNormal, film));
                    continue;
                }
                bool slabFloor = m.upFacing && m.kind == SurfaceSlab;
                float darkening = slabFloor ? slabDarkening : m.upFacing ? wetDarkening : wallDarkening;
                if (m.hasColor)
                {
                    float k = LinearDarken(darkening, soak * (slabFloor ? 0.6f + 0.4f * m.porosity : m.porosity));
                    Color c = m.baseColor;
                    m.clone.SetColor(s_BaseColor, new Color(c.r * k, c.g * k, c.b * k, c.a));
                }
                bool slab = m.upFacing && m.kind == SurfaceSlab;
                float sheen = film * (slab ? 1f : (0.55f + 0.45f * m.porosity)) * (m.upFacing ? 1f : wallSheen);
                float wetMax = slab ? slabSmoothnessMax : m.upFacing ? wetSmoothnessMax : Mathf.Lerp(m.smoothMax, wetSmoothnessMax, 0.5f);
                if (m.hasNormal && m.upFacing)
                    m.clone.SetFloat(s_NormalScale, m.normalScale * Mathf.Lerp(1f, slab ? slabWetNormal : 0.7f, film));
                if (m.hasRemap)
                {
                    // the top of the range (the film on the smooth parts) rises; on a flat sealed slab the rough bottom
                    // rises too (a film of water over the whole slab), elsewhere it moves only a little
                    float rise = slab ? slabSmoothnessMinRise : wetSmoothnessMinRise;
                    float minWet = Mathf.Max(m.smoothMin, Mathf.Min(m.smoothMin + rise, slab ? slabSmoothnessMinCap : 0.5f));
                    m.clone.SetFloat(s_SmoothMax, Mathf.Lerp(m.smoothMax, Mathf.Max(m.smoothMax, wetMax), sheen));
                    m.clone.SetFloat(s_SmoothMin, Mathf.Lerp(m.smoothMin, minWet, sheen));
                }
                else if (m.hasSmoothness)
                {
                    m.clone.SetFloat(s_Smoothness, Mathf.Lerp(m.smoothness, Mathf.Max(m.smoothness, wetMax), sheen));
                }
            }

            _film = film;

            // a stain on a wet slab is as wet as the slab
            foreach (var dc in _decalClones)
                if (dc.clone != null) dc.clone.SetFloat(s_Smoothness, Mathf.Lerp(dc.smoothness, Mathf.Max(dc.smoothness, 0.74f), film));

            // puddles stand in real rain (night 0.75, storm 1.0) and are mostly gone by the dawn (0.5 -> about 0.3):
            // full mirrors on the yard slab read as part of a blockout in the dawn crane shot (review 1 check, 28_ending_2)
            float puddle = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((w - 0.35f) / 0.4f));
            _puddleLevel = puddle;
            for (int i = 0; i < _puddles.Count; i++)
            {
                DecalProjector d = _puddles[i];
                if (d == null) continue;
                d.fadeFactor = _puddleBaseFade[i] * puddle;
                d.enabled = puddle > 0.01f;
            }
        }
    }
}
