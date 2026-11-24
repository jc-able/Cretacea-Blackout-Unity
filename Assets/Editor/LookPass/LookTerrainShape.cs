using UnityEngine;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// The island OUTSIDE the hand-built play area (X -125..560, Z -215..195): volcanic jungle hills that fall to a
    /// coast in every direction, one dominant volcano behind the Visitor Center, a lower one in the south-west, the
    /// north-east and south-east headlands that frame the dawn view, a beach under the overlook and a seabed that
    /// drops well below the sea (Y -40).
    ///
    /// Zone 4 blends this in with distance from the play area, so heights inside the play area stay exactly what
    /// zones 1-3 were built against. Deterministic (Mathf.PerlinNoise only).
    /// </summary>
    public static class LookTerrainShape
    {
        public const float SeaLevel = -40f;

        // dominant cones: (x, z, height, radius, crater radius)
        private static readonly Vector4[] Volcanoes =
        {
            new Vector4(-150f, 610f, 195f, 720f),     // Mount Tormenta, north behind the Visitor Center
            new Vector4(-560f, -380f, 150f, 520f),    // south-west cone behind Titan Meadows
            new Vector4(330f, 700f, 120f, 430f),      // north-east shoulder
        };

        private static float SS(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float P(float x, float z) => Mathf.PerlinNoise(x, z);

        private static float Fbm(float x, float z, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += P(x * f + i * 19.7f, z * f + i * 7.3f) * amp;
                norm += amp; amp *= 0.5f; f *= 2.03f;
            }
            return sum / norm;
        }

        /// <summary>Ridged multifractal 0..1: sharp crests and gullies, octave weight follows the previous one.</summary>
        private static float Ridged(float x, float z, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, f = 1f, weight = 1f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(P(x * f + i * 31.1f, z * f + i * 11.9f) * 2f - 1f);
                n *= n;
                n *= Mathf.Lerp(0.6f, 1f, weight);
                weight = n;
                sum += n * amp; norm += amp; amp *= 0.48f; f *= 2.11f;
            }
            return sum / norm;
        }

        /// <summary>Signed distance to the coast in metres (positive = land), roughly.</summary>
        public static float CoastDistance(float x, float z)
        {
            // gentle domain warp so bays and points are not ellipse-perfect
            float wx = x + (Fbm(x * 0.0021f + 3.7f, z * 0.0021f + 8.1f, 3) - 0.5f) * 180f;
            float wz = z + (Fbm(x * 0.0021f + 13.2f, z * 0.0021f + 1.4f, 3) - 0.5f) * 180f;

            // west / north / south: a warped ellipse
            const float cx = 120f, cz = 40f;
            float ax = wx < cx ? 840f : 1400f;
            float az = wz > cz ? 790f : 770f;
            float dx = (wx - cx) / ax, dz = (wz - cz) / az;
            float rho = Mathf.Sqrt(dx * dx + dz * dz);
            float dEllipse = (1f - rho) * 760f;

            // east: the coast runs north-south under the overlook (x ~625) and bulges out on the two headlands
            float headN = SS(20f, 230f, z), headS = SS(-130f, -330f, z);
            float head = Mathf.Max(headN, headS);
            float coastX = 628f + 125f * head + (Fbm(z * 0.010f + 4.4f, 2.2f, 3) - 0.5f) * 40f * (0.35f + 0.65f * head)
                           + (Fbm(z * 0.045f + 9.4f, 6.2f, 2) - 0.5f) * 10f;
            float dEast = coastX - x;

            // smooth minimum: no corner where the east coast meets the rest of the island
            const float k = 140f;
            float h = Mathf.Clamp01(0.5f + 0.5f * (dEast - dEllipse) / k);
            return Mathf.Lerp(dEast, dEllipse, h) - k * h * (1f - h);
        }

        private static float Volcano(float x, float z, Vector4 v)
        {
            float dx = x - v.x, dz = z - v.y;
            float r = Mathf.Sqrt(dx * dx + dz * dz);
            if (r >= v.w) return 0f;
            float t = 1f - SS(0f, v.w, r);
            float cone = v.z * Mathf.Pow(t, 1.25f);
            // radial gullies and spurs (strongest on the mid flanks)
            float ang = Mathf.Atan2(dz, dx);
            float gul = Ridged(Mathf.Cos(ang) * 2.6f + v.x * 0.01f, Mathf.Sin(ang) * 2.6f + r * 0.004f, 3);
            float flank = SS(0.05f, 0.35f, r / v.w) * (1f - SS(0.7f, 1f, r / v.w));
            cone *= 1f - 0.16f * flank * (1f - gul);
            // summit crater, broken on one side
            float crater = (1f - SS(0f, v.w * 0.09f, r)) * v.z * 0.10f * (0.6f + 0.4f * Mathf.Cos(ang - 2.2f));
            return cone - crater;
        }

        /// <summary>Inland height before the coast profile is applied (basin level ~0.6 m).</summary>
        private static float InlandHeight(float x, float z, float distOutsidePlay)
        {
            float wx = x + (Fbm(x * 0.0032f + 41.0f, z * 0.0032f + 2.0f, 3) - 0.5f) * 140f;
            float wz = z + (Fbm(x * 0.0032f + 7.0f, z * 0.0032f + 29.0f, 3) - 0.5f) * 140f;

            // hills grow with distance from the play area (the valley stays a valley)
            float rise = SS(0f, 300f, distOutsidePlay);
            float ridges = Ridged(wx * 0.0021f + 5.5f, wz * 0.0021f + 9.1f, 4);
            float rolls = Fbm(wx * 0.0045f + 1.9f, wz * 0.0045f + 6.6f, 4);
            float hills = ridges * 78f + rolls * 26f;

            float cones = 0f;
            for (int i = 0; i < Volcanoes.Length; i++) cones = Mathf.Max(cones, Volcano(wx, wz, Volcanoes[i]));

            // smooth max of hills and cones
            float k = 30f;
            float h = Mathf.Max(hills, cones);
            float mn = Mathf.Min(hills, cones);
            h += Mathf.Max(0f, k - (h - mn)) * Mathf.Max(0f, k - (h - mn)) / (4f * k) * 0.6f;
            h *= rise;

            // headlands that frame the dawn view (continue the ones zone 4 always had)
            float head = Mathf.Max(SS(45f, 165f, z), SS(-145f, -265f, z)) * SS(470f, 540f, x) * (1f - SS(660f, 760f, x));
            h = Mathf.Max(h, head * (34f + 10f * rolls));

            return 0.6f + h;
        }

        /// <summary>World height of the outer island at (x, z).</summary>
        public static float OuterElevation(float x, float z, float distOutsidePlay)
        {
            float dc = CoastDistance(x, z);
            float inland = InlandHeight(x, z, distOutsidePlay);

            // beach: tanh-like step around the waterline, then land rises to the inland height
            float beach = SeaLevel + 1.9f * (float)System.Math.Tanh(dc / 14f);
            if (dc >= 0f)
            {
                float k = Mathf.Pow(SS(4f, 170f, dc), 0.75f);
                return beach + (inland - beach) * k;
            }
            // seabed: a long shallow sandy shelf (teal shallows, sand visible through the water for ~100 m), then the
            // drop to deep water
            float ripple = (Fbm(x * 0.01f, z * 0.01f, 2) - 0.5f) * 3f;
            return beach - 3.5f * SS(0f, 140f, -dc) - 48f * SS(120f, 460f, -dc) + ripple * SS(100f, 300f, -dc);
        }
    }
}
