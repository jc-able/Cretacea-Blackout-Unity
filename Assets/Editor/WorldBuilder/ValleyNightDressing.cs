using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Cretacea.Core;

namespace Cretacea.Editor.WorldBuilder
{
    /// <summary>
    /// Tyrant Valley at night (review 3: "between strikes the valley is one dark band with nothing bright in it").
    /// Highlights that are plausible in a blackout (battery or solar backed) and lead the player down Service Road 3:
    ///   - an aviation obstruction mast (23 m, solar + battery box, steady red lamps at the top and half way) just
    ///     outside Substation 3's north-west fence corner, straight down the road from the valley entry;
    ///   - amber battery flashers on both kerbs at the west end of the stream bridge (the stream crossing reads);
    ///   - reflective delineator posts along both verges that light up in the torch (TorchReflectorGroup).
    /// No shadow-casting lamps. Ground heights from the terrain's own height function (the terrain is built later).
    /// Hooked once in CretaceaWorldAssembler right after the Zone 2 build.
    /// </summary>
    public static class ValleyNightDressing
    {
        private static readonly Vector2[] Road3 =
        {
            new Vector2(91f, -3.5f), new Vector2(120f, 0f), new Vector2(160f, 8f), new Vector2(200f, 4f)
        };

        private static readonly Vector3 MastXZ = new Vector3(247f, 0f, 21f);
        private const float MastHeight = 23f;

        public static GameObject Build(Transform worldRoot, WorldMaterialPalette p)
        {
            var root = new GameObject("Valley_NightDressing");
            if (worldRoot != null) root.transform.SetParent(worldRoot, false);
            BuildObstructionMast(root.transform, p);
            BuildBridgeFlashers(root.transform, p);
            BuildDelineators(root.transform, p);
            int lamps = Cretacea.Editor.Look.LookLights.ApplyValleyRules(root.transform);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = GameLayers.Environment;
            Debug.Log($"[ValleyNightDressing] built: mast, bridge flashers, delineators; {lamps} lamps calibrated");
            return root;
        }

        private static float Ground(float x, float z) => Zone4_TerrainJungleAndDawnCoastBuilder.EvaluateWorldElevation(x, z);

        private static Renderer Prim(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material mat, bool shadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return r;
        }

        // ------------------------------------------------------------------------------------------------ mast
        private static void BuildObstructionMast(Transform parent, WorldMaterialPalette p)
        {
            float gy = Ground(MastXZ.x, MastXZ.z);
            var mast = new GameObject("ObstructionMast_Sub3").transform;
            mast.SetParent(parent, false);
            Vector3 b = new Vector3(MastXZ.x, gy, MastXZ.z);

            Prim(mast, "Footing", PrimitiveType.Cylinder, b + Vector3.up * 0.12f, Vector3.zero, new Vector3(1.3f, 0.3f, 1.3f), p.ConcreteExterior);
            Prim(mast, "BasePlate", PrimitiveType.Cylinder, b + Vector3.up * 0.44f, Vector3.zero, new Vector3(0.62f, 0.03f, 0.62f), p.DarkPaintedMetal);
            // a galvanised pole in two tapered sections with a slip joint collar
            Renderer pole = Prim(mast, "PoleLower", PrimitiveType.Cylinder, b + Vector3.up * (0.45f + 6f), Vector3.zero, new Vector3(0.38f, 6f, 0.38f), p.BrushedSteel);
            pole.gameObject.AddComponent<CapsuleCollider>();   // the one solid part (the player can walk up to it)
            Prim(mast, "SlipCollar", PrimitiveType.Cylinder, b + Vector3.up * 12.45f, Vector3.zero, new Vector3(0.42f, 0.12f, 0.42f), p.BrushedSteel);
            float upper = MastHeight - 12.45f;
            Prim(mast, "PoleUpper", PrimitiveType.Cylinder, b + Vector3.up * (12.45f + upper * 0.5f), Vector3.zero, new Vector3(0.26f, upper * 0.5f, 0.26f), p.BrushedSteel);
            // conduit up the north face
            Prim(mast, "Conduit", PrimitiveType.Cylinder, b + new Vector3(0f, MastHeight * 0.5f, 0.24f), Vector3.zero, new Vector3(0.05f, MastHeight * 0.5f - 0.4f, 0.05f), p.DarkPaintedMetal, shadows: false);
            // climbing rungs on the west face, every 0.9 m from 2.5 m
            for (float y = 2.5f; y < MastHeight - 1f; y += 0.9f)
                Prim(mast, "Rung", PrimitiveType.Cylinder, b + new Vector3(-0.24f, y, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.03f, 0.16f, 0.03f), p.BrushedSteel, shadows: false);

            // battery cabinet on the footing and a south-facing solar panel at 3.2 m
            Prim(mast, "BatteryCabinet", PrimitiveType.Cube, b + new Vector3(0.62f, 0.95f, -0.1f), Vector3.zero, new Vector3(0.42f, 0.95f, 0.55f), p.PaintedMetalGreen);
            Prim(mast, "BatteryCabinetHood", PrimitiveType.Cube, b + new Vector3(0.62f, 1.46f, -0.1f), new Vector3(0f, 0f, -6f), new Vector3(0.5f, 0.05f, 0.62f), p.DarkPaintedMetal);
            Prim(mast, "SolarArm", PrimitiveType.Cube, b + new Vector3(0f, 3.2f, -0.45f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.7f), p.BrushedSteel);
            Prim(mast, "SolarFrame", PrimitiveType.Cube, b + new Vector3(0f, 3.35f, -0.95f), new Vector3(-35f, 0f, 0f), new Vector3(1.25f, 0.05f, 0.85f), p.BrushedSteel);
            Prim(mast, "SolarCells", PrimitiveType.Cube, b + new Vector3(0f, 3.38f, -0.93f), new Vector3(-35f, 0f, 0f), new Vector3(1.18f, 0.02f, 0.78f), p.GlassArchitectural, shadows: false);

            Material lens = p.GetOrBuildEmissive("MAT_Valley_ObstructionLens", new Color(1.0f, 0.10f, 0.06f), 18f);
            ObstructionHead(mast, p, lens, b + Vector3.up * (MastHeight + 0.02f), "Top");
            // the mid lamp sits on a short arm off the pole
            Prim(mast, "MidArm", PrimitiveType.Cube, b + new Vector3(0f, 12.2f, -0.45f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.75f), p.BrushedSteel);
            ObstructionHead(mast, p, lens, b + new Vector3(0f, 12.25f, -0.85f), "Mid");
        }

        private static void ObstructionHead(Transform parent, WorldMaterialPalette p, Material lens, Vector3 at, string tag)
        {
            Prim(parent, $"ObstructionBase_{tag}", PrimitiveType.Cylinder, at + Vector3.up * 0.05f, Vector3.zero, new Vector3(0.36f, 0.06f, 0.36f), p.DarkPaintedMetal);
            Renderer dome = Prim(parent, $"ObstructionLens_{tag}", PrimitiveType.Capsule, at + Vector3.up * 0.24f, Vector3.zero, new Vector3(0.32f, 0.2f, 0.32f), lens, shadows: false);
            Prim(parent, $"ObstructionCap_{tag}", PrimitiveType.Cylinder, at + Vector3.up * 0.45f, Vector3.zero, new Vector3(0.16f, 0.02f, 0.16f), p.DarkPaintedMetal, shadows: false);
            WorldArchitectureKit.BuildHdrpLight(parent, $"StatusLight_Valley_Obstruction{tag}", at + Vector3.up * 0.2f, Vector3.zero, LightType.Point,
                new Color(1.0f, 0.12f, 0.07f), 900f, 16f, LightGroupId.PaddockBattery, volumetricDimmer: 1.2f, pairedEmissives: new[] { dome });
        }

        // ------------------------------------------------------------------------------------------------ bridge
        private static void BuildBridgeFlashers(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("BridgeFlashers").transform;
            g.SetParent(parent, false);
            Material lens = p.GetOrBuildEmissive("MAT_Valley_FlasherLens", new Color(1.0f, 0.55f, 0.12f), 18f);
            // the road meets the bridge's west embankment at about x 190.5 (bridge root 201, 0, 4; deck 1.5 m up)
            Vector2 a = Road3[2], c = Road3[3];
            Vector2 dir = (c - a).normalized;
            Vector2 side = new Vector2(dir.y, -dir.x);
            float t = (189.5f - a.x) / (c.x - a.x);
            Vector2 centre = Vector2.Lerp(a, c, t);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 q = centre + side * (3.6f * s);
                float gy = Ground(q.x, q.y);
                Vector3 b = new Vector3(q.x, gy, q.y);
                string tag = s < 0 ? "N" : "S";
                Prim(g, $"FlasherPost_{tag}", PrimitiveType.Cube, b + Vector3.up * 0.62f, Vector3.zero, new Vector3(0.08f, 1.3f, 0.08f), p.DarkPaintedMetal);
                Prim(g, $"FlasherFoot_{tag}", PrimitiveType.Cube, b + Vector3.up * 0.06f, Vector3.zero, new Vector3(0.5f, 0.12f, 0.5f), p.ConcreteExterior);
                // the hazard board (chevrons are the yellow paint with dark bands)
                Prim(g, $"FlasherBoard_{tag}", PrimitiveType.Cube, b + Vector3.up * 1.0f, new Vector3(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f), new Vector3(0.62f, 0.32f, 0.03f), p.PaintedMetalYellow);
                Prim(g, $"FlasherBattery_{tag}", PrimitiveType.Cube, b + Vector3.up * 1.36f, Vector3.zero, new Vector3(0.16f, 0.14f, 0.12f), p.DarkPaintedMetal);
                Renderer dome = Prim(g, $"FlasherLens_{tag}", PrimitiveType.Cylinder, b + new Vector3(0f, 1.36f, 0f) - new Vector3(dir.x, 0f, dir.y) * 0.08f,
                    new Vector3(90f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f), new Vector3(0.17f, 0.02f, 0.17f), lens, shadows: false);
                WorldArchitectureKit.BuildHdrpLight(g, $"StatusLight_Valley_BridgeFlasher_{tag}", b + Vector3.up * 1.36f - new Vector3(dir.x, 0f, dir.y) * 0.25f, Vector3.zero, LightType.Point,
                    new Color(1.0f, 0.55f, 0.12f), 120f, 6.5f, LightGroupId.PaddockBattery, volumetricDimmer: 0.35f, pulsingEmergency: true, pairedEmissives: new[] { dome });
            }
        }

        // ------------------------------------------------------------------------------------------------ delineators
        private static void BuildDelineators(Transform parent, WorldMaterialPalette p)
        {
            var g = new GameObject("RoadDelineators").transform;
            g.SetParent(parent, false);
            Material white = p.GetOrBuildEmissive("MAT_Valley_ReflectorWhite", new Color(0.95f, 0.95f, 0.9f), 0.25f);
            Material amber = p.GetOrBuildEmissive("MAT_Valley_ReflectorAmber", new Color(1.0f, 0.6f, 0.15f), 0.25f);
            var refl = new List<Renderer>();
            var tints = new List<Color>();

            // along both verges, 3.9 m off the centre line, every 13 m, the two sides staggered by half a step
            float total = 0f;
            for (int i = 0; i < Road3.Length - 1; i++) total += Vector2.Distance(Road3[i], Road3[i + 1]);
            int n = 0;
            for (int s = -1; s <= 1; s += 2)
            for (float d = 9f + (s > 0 ? 6.5f : 0f); d < total; d += 13f)
            {
                if (!PointAt(d, out Vector2 c, out Vector2 dir) || c.x > 186f) break;
                Vector2 side = new Vector2(dir.y, -dir.x);       // +side = south of the eastbound road
                Vector2 q = c + side * (3.9f * s);
                float gy = Ground(q.x, q.y);
                Vector3 b = new Vector3(q.x, gy, q.y);
                float lean = Mathf.Sin(d * 1.7f + s) * 3f;
                float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
                Quaternion rot = Quaternion.Euler(lean, yaw, Mathf.Cos(d * 0.9f) * 2f);
                var post = new GameObject($"Delineator_{(s < 0 ? "N" : "S")}{n:00}").transform;
                post.SetParent(g, false);
                post.SetPositionAndRotation(b, rot);
                Prim(post, "Post", PrimitiveType.Cube, b + rot * new Vector3(0f, 0.55f, 0f), rot.eulerAngles, new Vector3(0.1f, 1.3f, 0.07f), p.PaintedMetalYellow);
                Prim(post, "Band", PrimitiveType.Cube, b + rot * new Vector3(0f, 1.12f, 0f), rot.eulerAngles, new Vector3(0.105f, 0.12f, 0.075f), p.DarkPaintedMetal);
                // the reflector faces the oncoming (westbound-looking) player: forward = -road direction
                Quaternion face = rot * Quaternion.Euler(0f, 180f, 0f);
                Renderer r = Prim(post, "Reflector", PrimitiveType.Cube, b + rot * new Vector3(0f, 0.93f, -0.04f), face.eulerAngles, new Vector3(0.085f, 0.22f, 0.012f), s < 0 ? amber : white, shadows: false);
                refl.Add(r);
                tints.Add(s < 0 ? new Color(1.0f, 0.6f, 0.15f) : new Color(0.95f, 0.95f, 0.9f));
                n++;
            }
            // curve chevrons on the outside (north) of the right-hand bend at (160, 8): big retro-reflective boards
            // that answer the torch from 30-40 m and show where the road goes
            Material yellow = p.GetOrBuildEmissive("MAT_Valley_ReflectorYellow", new Color(1.0f, 0.82f, 0.25f), 0.25f);
            float[] chevronS = { 58f, 65.5f, 73f, 80.5f };
            for (int i = 0; i < chevronS.Length; i++)
            {
                if (!PointAt(chevronS[i], out Vector2 c, out Vector2 dir)) continue;
                Vector2 side = new Vector2(dir.y, -dir.x);
                Vector2 q = c - side * 4.7f;
                // all three face the driver coming up the straight (heading of the first segment into the bend)
                Vector2 approach = (Road3[2] - Road3[1]).normalized;
                float yaw = Mathf.Atan2(approach.x, approach.y) * Mathf.Rad2Deg + 180f;   // board faces back down the road
                Vector3 b = new Vector3(q.x, Ground(q.x, q.y), q.y);
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                var sign = new GameObject($"CurveChevron_{i}").transform;
                sign.SetParent(g, false);
                sign.SetPositionAndRotation(b, rot);
                Prim(sign, "Post", PrimitiveType.Cube, b + Vector3.up * 0.8f + rot * new Vector3(0f, 0f, -0.05f), rot.eulerAngles, new Vector3(0.07f, 1.6f, 0.07f), p.BrushedSteel);
                Prim(sign, "Back", PrimitiveType.Cube, b + Vector3.up * 1.65f, rot.eulerAngles, new Vector3(0.79f, 0.94f, 0.02f), p.DarkPaintedMetal);
                Renderer face = Prim(sign, "Face", PrimitiveType.Cube, b + Vector3.up * 1.65f + rot * new Vector3(0f, 0f, 0.012f), rot.eulerAngles, new Vector3(0.75f, 0.9f, 0.006f), yellow, shadows: false);
                // the black chevron (pointing right, the way the road turns) painted over the sheeting
                for (int k = -1; k <= 1; k += 2)
                    Prim(sign, $"Chevron_{(k < 0 ? "U" : "D")}", PrimitiveType.Cube, b + Vector3.up * (1.65f + 0.16f * -k) + rot * new Vector3(0f, 0f, 0.017f),
                        (rot * Quaternion.Euler(0f, 0f, -38f * k)).eulerAngles, new Vector3(0.46f, 0.11f, 0.004f), p.DarkPaintedMetal, shadows: false);
                refl.Add(face);
                tints.Add(new Color(1.0f, 0.82f, 0.25f));
            }

            var group = g.gameObject.AddComponent<Cretacea.Rendering.TorchReflectorGroup>();
            group.Configure(refl.ToArray(), tints.ToArray());
        }

        private static bool PointAt(float s, out Vector2 p, out Vector2 dir)
        {
            for (int i = 0; i < Road3.Length - 1; i++)
            {
                float len = Vector2.Distance(Road3[i], Road3[i + 1]);
                if (s <= len)
                {
                    dir = (Road3[i + 1] - Road3[i]) / len;
                    p = Road3[i] + dir * s;
                    return true;
                }
                s -= len;
            }
            p = dir = Vector2.zero;
            return false;
        }
    }
}
