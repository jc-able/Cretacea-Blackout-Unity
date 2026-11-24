using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Where the sky is covered. Collects every RainBlocker slab (layer 13 renderers and colliders, built by
    /// WorldArchitectureKit.BuildRainBlockerSlab, invisible to the main camera) as an oriented box, answers
    /// "how high is the roof above this point" on the CPU, and rasterises a top-down roof-height window around the
    /// player for the GPU (rain, ripples and drips are discarded below the roof).
    ///
    /// Replaces the old Camera.RenderWithShader height map, which HDRP never rendered. Pipeline independent, exact
    /// to the slab footprint, and costs ~1 ms when the player has moved a few metres.
    /// </summary>
    public static class RoofOcclusionMap
    {
        public const float NoRoof = -9999f;

        private struct Slab
        {
            public Matrix4x4 worldToLocal;
            public Vector3 localCenter;
            public Vector3 localExtents;
            public Vector2 minXZ, maxXZ;
            public float topY;
        }

        private static readonly List<Slab> s_Slabs = new List<Slab>(256);
        private static int s_CollectedFrame = -1;
        private static int s_LayerMask;

        public static int SlabCount => s_Slabs.Count;

        /// <summary>Gathers the blocker slabs again (call after roofs change, e.g. a breach).</summary>
        public static void Collect(int layerMask)
        {
            s_Slabs.Clear();
            s_LayerMask = layerMask;
            s_CollectedFrame = Time.frameCount;

            foreach (MeshFilter mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude))
            {
                GameObject go = mf.gameObject;
                if ((layerMask & (1 << go.layer)) == 0 || mf.sharedMesh == null) continue;
                AddBox(mf.transform, mf.sharedMesh.bounds);
            }
            foreach (BoxCollider bc in Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude))
            {
                if ((layerMask & (1 << bc.gameObject.layer)) == 0 || bc.isTrigger) continue;
                if (bc.GetComponent<MeshFilter>() != null) continue;
                AddBox(bc.transform, new Bounds(bc.center, bc.size));
            }
        }

        private static void AddBox(Transform t, Bounds local)
        {
            Matrix4x4 l2w = t.localToWorldMatrix;
            Vector3 e = local.extents;
            Vector2 mn = new Vector2(float.MaxValue, float.MaxValue), mx = new Vector2(float.MinValue, float.MinValue);
            float top = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 w = l2w.MultiplyPoint3x4(corner);
                mn = Vector2.Min(mn, new Vector2(w.x, w.z));
                mx = Vector2.Max(mx, new Vector2(w.x, w.z));
                top = Mathf.Max(top, w.y);
            }
            s_Slabs.Add(new Slab
            {
                worldToLocal = l2w.inverse,
                localCenter = local.center,
                localExtents = e,
                minXZ = mn,
                maxXZ = mx,
                topY = top
            });
        }

        public static void EnsureCollected(int layerMask)
        {
            if (s_CollectedFrame < 0 || layerMask != s_LayerMask) Collect(layerMask);
        }

        private static bool Inside(in Slab s, float x, float z)
        {
            if (x < s.minXZ.x || x > s.maxXZ.x || z < s.minXZ.y || z > s.maxXZ.y) return false;
            // test in the slab's local XZ at its own mid height
            Vector3 lp = s.worldToLocal.MultiplyPoint3x4(new Vector3(x, s.topY, z));
            Vector3 d = lp - s.localCenter;
            return Mathf.Abs(d.x) <= s.localExtents.x && Mathf.Abs(d.z) <= s.localExtents.z;
        }

        /// <summary>Highest roof top above (x,z), or NoRoof.</summary>
        public static float RoofHeightAt(float x, float z)
        {
            float best = NoRoof;
            for (int i = 0; i < s_Slabs.Count; i++)
            {
                Slab s = s_Slabs[i];
                if (s.topY > best && Inside(s, x, z)) best = s.topY;
            }
            return best;
        }

        /// <summary>True when a roof covers this point (the roof top is above it).</summary>
        public static bool IsCovered(Vector3 p, float margin = 0.1f)
        {
            float y = RoofHeightAt(p.x, p.z);
            return y > NoRoof + 1f && y > p.y + margin;
        }

        /// <summary>Rasterises the roof-top heights of a square window (row 0 = min Z) into <paramref name="heights"/>.</summary>
        public static void Rasterize(Vector2 centerXZ, float halfExtent, int res, float[] heights)
        {
            for (int i = 0; i < heights.Length; i++) heights[i] = NoRoof;
            float texel = halfExtent * 2f / res;
            float x0 = centerXZ.x - halfExtent, z0 = centerXZ.y - halfExtent;
            for (int si = 0; si < s_Slabs.Count; si++)
            {
                Slab s = s_Slabs[si];
                int ix0 = Mathf.Max(0, Mathf.FloorToInt((s.minXZ.x - x0) / texel));
                int ix1 = Mathf.Min(res - 1, Mathf.CeilToInt((s.maxXZ.x - x0) / texel));
                int iz0 = Mathf.Max(0, Mathf.FloorToInt((s.minXZ.y - z0) / texel));
                int iz1 = Mathf.Min(res - 1, Mathf.CeilToInt((s.maxXZ.y - z0) / texel));
                if (ix0 > ix1 || iz0 > iz1) continue;
                for (int iz = iz0; iz <= iz1; iz++)
                {
                    float z = z0 + (iz + 0.5f) * texel;
                    int row = iz * res;
                    for (int ix = ix0; ix <= ix1; ix++)
                    {
                        if (heights[row + ix] >= s.topY) continue;
                        float x = x0 + (ix + 0.5f) * texel;
                        if (Inside(s, x, z)) heights[row + ix] = s.topY;
                    }
                }
            }
        }
    }
}
