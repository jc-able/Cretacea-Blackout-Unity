using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Editor.WorldBuilder;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Reflection probes that tell the truth. Every probe in the scene becomes realtime OnDemand with multiplier 1,
    /// sky behind windows and a culling mask without the viewmodel and rain-blocker layers, re-rendered by
    /// ReflectionProbeRefresh when light groups finish switching and at chapter start. Outdoor probes are added at
    /// the places where wet ground should reflect its real surroundings instead of a lit room or nothing.
    /// </summary>
    public static class LookProbes
    {
        public const string OutdoorPrefix = "LookProbe_Outdoor_";

        // name, XZ centre, influence box size (x, y, z)
        private static readonly (string name, Vector2 xz, Vector3 size)[] Outdoor =
        {
            ("Plaza",          new Vector2(-32f, -46f),  new Vector3(74f, 8f, 22f)),
            ("PathsWest",      new Vector2(35f, -2f),    new Vector3(22f, 8f, 26f)),
            ("PathsNorth",     new Vector2(56f, 13f),    new Vector3(26f, 8f, 16f)),
            ("PathsEast",      new Vector2(80f, 2f),     new Vector3(24f, 8f, 24f)),
            ("SubstationPad",  new Vector2(268f, 0f),    new Vector3(44f, 8f, 40f)),
            ("AuxRoute",       new Vector2(340f, -14f),  new Vector3(64f, 8f, 26f)),
            ("OldYard",        new Vector2(440f, -32f),  new Vector3(56f, 8f, 36f)),
            ("ExitCourt",      new Vector2(490f, -42f),  new Vector3(34f, 8f, 30f)),
            ("CoastRoad",      new Vector2(526f, -46f),  new Vector3(40f, 8f, 24f)),
        };

        public static string Apply()
        {
            GameObject world = GameObject.Find("World");
            if (world == null) return "probes: no World root";

            // previous outdoor probes
            var old = new List<GameObject>();
            foreach (Transform t in world.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(OutdoorPrefix) || t.name == "LookProbes") old.Add(t.gameObject);
            foreach (GameObject g in old) if (g != null) Object.DestroyImmediate(g);

            Physics.SyncTransforms();
            var holder = new GameObject("LookProbes");
            holder.transform.SetParent(world.transform, false);
            int mask = ~((1 << GameLayers.RainBlocker) | (1 << GameLayers.Viewmodel) | (1 << GameLayers.Trigger) | (1 << 2));
            int added = 0;
            foreach (var (name, xz, size) in Outdoor)
            {
                float groundY = 0f;
                if (Physics.Raycast(new Vector3(xz.x, 120f, xz.y), Vector3.down, out RaycastHit hit, 240f, mask, QueryTriggerInteraction.Ignore))
                    groundY = hit.point.y;
                var go = new GameObject(OutdoorPrefix + name);
                go.transform.SetParent(holder.transform, false);
                // capture point at head height; the influence box spans 2.2 m below to ~6 m above it
                go.transform.position = new Vector3(xz.x, groundY + 1.8f, xz.y);
                ReflectionProbe probe = go.AddComponent<ReflectionProbe>();
                probe.boxProjection = true;
                probe.size = size;
                HDAdditionalReflectionData hd = go.AddComponent<HDAdditionalReflectionData>();
                hd.influenceVolume.shape = InfluenceShape.Box;
                hd.influenceVolume.boxSize = size;
                hd.influenceVolume.boxBlendDistancePositive = Vector3.one * 4f;
                // wide horizontal blends: with 4 m the edge of a probe showed as a straight line across wet paving
                float bx = Mathf.Min(9f, size.x * 0.3f), bz = Mathf.Min(9f, size.z * 0.3f);
                hd.influenceVolume.boxBlendDistanceNegative = new Vector3(bx, 0.5f, bz); hd.influenceVolume.boxBlendDistancePositive = new Vector3(bx, 1.5f, bz);
                WorldArchitectureKit.ConfigureTruthfulProbe(hd, outdoor: true);
                added++;
            }

            int configured = 0;
            foreach (HDAdditionalReflectionData hd in Object.FindObjectsByType<HDAdditionalReflectionData>(FindObjectsInactive.Include))
            {
                bool outdoor = hd.name.StartsWith(OutdoorPrefix);
                if (!outdoor) WorldArchitectureKit.ConfigureTruthfulProbe(hd, outdoor: false);
                configured++;
            }
            return $"probes: outdoor added={added}, total OnDemand+masked={configured}";
        }
    }
}
