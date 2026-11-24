using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Rain-wet ground for the terrain. HDRP TerrainLit has no wetness input, so this feeds each terrain a splat
    /// MaterialPropertyBlock that raises the layers' smoothness and darkens their albedo with the global surface
    /// wetness the AtmosphereController publishes (_GlobalWetness: day 0.08 ... storm 1.0). The TerrainLayer assets
    /// are never touched (no asset changes in Play mode). Updates a few times per second, only when wetness moves.
    /// </summary>
    public sealed class TerrainWetnessDriver : MonoBehaviour
    {

        private static readonly int GlobalWetnessId = Shader.PropertyToID("_GlobalWetness");
        private static readonly int RainId = Shader.PropertyToID("_RainIntensity");
        private static int[] s_offsetIds, s_scaleIds, s_diffuseIds, s_normalIds;

        // Per-layer wet look (review 1: wet gravel and grass sparkled like hoar-frost under the low moon). Soil and
        // gravel drain: they get darker with a soft, low sheen; only mud holds a film. The normal map is flattened as
        // water fills the relief, and the top of the smoothness range is capped, so no pebble top turns into a mirror.
        private struct WetProfile { public float add, max, normal, albedo; }
        private static WetProfile ProfileFor(string n)
        {
            if (n.Contains("Mud")) return new WetProfile { add = 0.34f, max = 0.70f, normal = 0.55f, albedo = 0.70f };
            // gravel keeps most of its relief and colour (fix round: at 0.60 / 0.70 the torch-lit path read as flat asphalt)
            if (n.Contains("Gravel")) return new WetProfile { add = 0.10f, max = 0.42f, normal = 0.85f, albedo = 0.80f };
            if (n.Contains("Rock")) return new WetProfile { add = 0.22f, max = 0.58f, normal = 0.75f, albedo = 0.76f };
            if (n.Contains("Sand")) return new WetProfile { add = 0.18f, max = 0.50f, normal = 0.80f, albedo = 0.78f };
            if (n.Contains("Grass") || n.Contains("Moss")) return new WetProfile { add = 0.10f, max = 0.40f, normal = 0.80f, albedo = 0.82f };
            return new WetProfile { add = 0.16f, max = 0.46f, normal = 0.75f, albedo = 0.78f };   // jungle floor, other soil
        }
        private MaterialPropertyBlock _mpb;
        private float _applied = -1f, _appliedRain = -1f;
        private float _next;

        private static void EnsureIds()
        {
            if (s_offsetIds != null) return;
            s_offsetIds = new int[8]; s_scaleIds = new int[8]; s_diffuseIds = new int[8]; s_normalIds = new int[8];
            for (int i = 0; i < 8; i++)
            {
                s_offsetIds[i] = Shader.PropertyToID("_MaskMapRemapOffset" + i);
                s_scaleIds[i] = Shader.PropertyToID("_MaskMapRemapScale" + i);
                s_diffuseIds[i] = Shader.PropertyToID("_DiffuseRemapScale" + i);
                s_normalIds[i] = Shader.PropertyToID("_NormalScale" + i);
            }
        }

        private void OnEnable()
        {
            EnsureIds();
            _mpb ??= new MaterialPropertyBlock();
            _applied = -1f;
        }

        private void OnDisable()
        {
            foreach (Terrain t in Terrain.activeTerrains)
                if (t != null) t.SetSplatMaterialPropertyBlock(null);
        }

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.25f;
            float wet = Mathf.Clamp01(Shader.GetGlobalFloat(GlobalWetnessId));
            float rain = Mathf.Clamp01(Shader.GetGlobalFloat(RainId));
            if (Mathf.Abs(wet - _applied) < 0.02f && Mathf.Abs(rain - _appliedRain) < 0.02f) return;
            _applied = wet;
            _appliedRain = rain;
            foreach (Terrain t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                Apply(t, wet, rain);
            }
        }

        private void Apply(Terrain t, float wet, float rain)
        {
            TerrainLayer[] layers = t.terrainData.terrainLayers;
            _mpb.Clear();
            t.GetSplatMaterialPropertyBlock(_mpb);
            for (int i = 0; i < layers.Length && i < 8; i++)
            {
                TerrainLayer l = layers[i];
                if (l == null) continue;
                Vector4 min = l.maskMapRemapMin, max = l.maskMapRemapMax;
                Vector4 scale = max - min, offset = min;
                WetProfile p = ProfileFor(l.name);
                // the ground soaks quickly but only builds a sheen in real rain: the gloss follows wetness squared
                // (storm 1.0 -> 1, night 0.75 -> 0.56, dawn 0.5 -> 0.25, dusk 0.35 -> 0.12), the darkening follows it directly
                // and only while it rains: the drying dawn verge (wetness 0.5, no rain) stays matte instead of glittering
                float gloss = wet * wet * Mathf.Lerp(0.3f, 1f, rain);
                // smoothness = offset.w + mask.a * scale.w : wet range [lo, hi], blended from the dry one
                float hi = Mathf.Min(offset.w + scale.w, p.max);
                float lo = Mathf.Min(offset.w + p.add, hi - 0.08f);
                float dryLo = offset.w, dryHi = offset.w + scale.w;
                float newLo = Mathf.Lerp(dryLo, Mathf.Max(dryLo, lo), gloss);
                float newHi = Mathf.Lerp(dryHi, Mathf.Max(newLo + 0.05f, hi), gloss);
                offset.w = newLo;
                scale.w = Mathf.Max(0.05f, newHi - newLo);
                _mpb.SetVector(s_offsetIds[i], offset);
                _mpb.SetVector(s_scaleIds[i], scale);
                _mpb.SetFloat(s_normalIds[i], l.normalScale * Mathf.Lerp(1f, p.normal, gloss));
                Vector4 d = l.diffuseRemapMax - l.diffuseRemapMin;
                float k = Mathf.Lerp(1f, p.albedo, wet);
                _mpb.SetVector(s_diffuseIds[i], new Vector4(d.x * k, d.y * k, d.z * k, d.w));
            }
            t.SetSplatMaterialPropertyBlock(_mpb);
        }
    }
}
