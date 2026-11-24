using UnityEngine;
using Cretacea.Core;
using Z4 = Cretacea.Editor.WorldBuilder.Zone4_TerrainJungleAndDawnCoastBuilder;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// The edge of the playable island. The old valley ring rose 60-136 m with 40-55 deg slopes; the new outer hills are
    /// walkable, so an invisible collider ring now runs through the jungle a fixed distance outside the hand-built play
    /// area (rounded rectangle). It blocks the player and, because the NavMesh is baked from colliders, cuts the dinosaur
    /// NavMesh off from the outer island. No renderer: views (the dawn overlook, cutscenes) are not affected.
    /// </summary>
    public static class LookTerrainBounds
    {
        /// <summary>Distance of the ring outside the play rectangle (m).</summary>
        public const float RingDistance = 46f;
        private const float Step = 8f;
        private const float Thickness = 1.6f;
        private const float HeightAbove = 30f;

        /// <summary>Point on the ring for parameter t in [0, 1).</summary>
        private static Vector2 RingPoint(float t)
        {
            float w = Z4.PlayMaxX - Z4.PlayMinX, h = Z4.PlayMaxZ - Z4.PlayMinZ, r = RingDistance;
            float arc = Mathf.PI * 0.5f * r;
            float perim = 2f * (w + h) + 4f * arc;
            float s = Mathf.Repeat(t, 1f) * perim;
            // walk: bottom edge (west->east), SE arc, east edge (south->north), NE arc, top edge (east->west), NW arc,
            // west edge (north->south), SW arc
            float[] lens = { w, arc, h, arc, w, arc, h, arc };
            int seg = 0;
            while (seg < 7 && s > lens[seg]) { s -= lens[seg]; seg++; }
            float u = lens[seg] > 0f ? s / lens[seg] : 0f;
            switch (seg)
            {
                case 0: return new Vector2(Z4.PlayMinX + u * w, Z4.PlayMinZ - r);
                case 1: { float a = -Mathf.PI * 0.5f + u * Mathf.PI * 0.5f; return new Vector2(Z4.PlayMaxX + Mathf.Cos(a) * r, Z4.PlayMinZ + Mathf.Sin(a) * r); }
                case 2: return new Vector2(Z4.PlayMaxX + r, Z4.PlayMinZ + u * h);
                case 3: { float a = u * Mathf.PI * 0.5f; return new Vector2(Z4.PlayMaxX + Mathf.Cos(a) * r, Z4.PlayMaxZ + Mathf.Sin(a) * r); }
                case 4: return new Vector2(Z4.PlayMaxX - u * w, Z4.PlayMaxZ + r);
                case 5: { float a = Mathf.PI * 0.5f + u * Mathf.PI * 0.5f; return new Vector2(Z4.PlayMinX + Mathf.Cos(a) * r, Z4.PlayMaxZ + Mathf.Sin(a) * r); }
                case 6: return new Vector2(Z4.PlayMinX - r, Z4.PlayMaxZ - u * h);
                default: { float a = Mathf.PI + u * Mathf.PI * 0.5f; return new Vector2(Z4.PlayMinX + Mathf.Cos(a) * r, Z4.PlayMinZ + Mathf.Sin(a) * r); }
            }
        }

        public static string Build(Transform parent)
        {
            var root = new GameObject("IslaTormenta_PlayBounds");
            root.transform.SetParent(parent, false);
            float w = Z4.PlayMaxX - Z4.PlayMinX, h = Z4.PlayMaxZ - Z4.PlayMinZ;
            float perim = 2f * (w + h) + 2f * Mathf.PI * RingDistance;
            int n = Mathf.CeilToInt(perim / Step);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = RingPoint(i / (float)n), b = RingPoint((i + 1) / (float)n);
                Vector2 mid = (a + b) * 0.5f;
                float yMin = float.MaxValue, yMax = float.MinValue;
                for (int k = 0; k <= 4; k++)
                {
                    Vector2 q = Vector2.Lerp(a, b, k / 4f);
                    float y = Z4.EvaluateWorldElevation(q.x, q.y);
                    yMin = Mathf.Min(yMin, y); yMax = Mathf.Max(yMax, y);
                }
                float len = Vector2.Distance(a, b) + Thickness;    // overlap the neighbours: no gaps at the joints
                float bottom = yMin - 4f, top = yMax + HeightAbove;
                var go = new GameObject($"PlayBounds_{i:000}");
                go.transform.SetParent(root.transform, false);
                go.layer = GameLayers.Default;
                go.isStatic = true;
                go.transform.SetPositionAndRotation(new Vector3(mid.x, (bottom + top) * 0.5f, mid.y),
                    Quaternion.Euler(0f, Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg, 0f));
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(Thickness, top - bottom, len);
            }
            return $"play bounds: {n} invisible wall segments, {RingDistance} m outside the play area";
        }
    }
}
