using System.Collections.Generic;
using UnityEngine;
using Cretacea.Rendering;

namespace Cretacea.Editor.Look
{
    /// <summary>
    /// Bakes the SurfaceAlbedoMap of the open scene: for every solid collider, the mean albedo of what is drawn there.
    /// Props keep their renderer next to their collider. Walls, floors and dressing are merged per material by the
    /// zone builders ("..._Merged_..." meshes) and leave bare box colliders behind: those are matched to the merged
    /// mesh that has vertices on the box's corners.
    /// </summary>
    public static class LookAlbedoMap
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private const float Cell = 0.004f;

        public static string Apply()
        {
            var materialAlbedo = new Dictionary<Material, float>();

            // 1. corners of the merged static meshes
            var vertexAlbedo = new Dictionary<long, float>(1 << 17);
            var verts = new List<Vector3>();
            int mergedMeshes = 0;
            foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>())
            {
                if (mr.name.IndexOf("_Merged_", System.StringComparison.Ordinal) < 0) continue;
                MeshFilter mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                float a = Albedo(mr.sharedMaterial, materialAlbedo);
                Matrix4x4 toWorld = mr.localToWorldMatrix;
                mf.sharedMesh.GetVertices(verts);
                for (int i = 0; i < verts.Count; i++)
                {
                    long key = Key(toWorld.MultiplyPoint3x4(verts[i]));
                    if (!vertexAlbedo.ContainsKey(key)) vertexAlbedo[key] = a;
                }
                mergedMeshes++;
            }

            // 2. every solid collider
            var cols = new List<Collider>();
            var vals = new List<float>();
            var votes = new Dictionary<float, int>();
            int own = 0, byCorner = 0, byLod = 0, unknown = 0;
            Vector3 nudge = Vector3.one * (Cell * 0.3f);
            foreach (Collider c in Object.FindObjectsByType<Collider>())
            {
                if (c.isTrigger || c is TerrainCollider || c is CharacterController) continue;
                Renderer r = c.GetComponent<Renderer>();
                if (r == null) r = c.GetComponentInChildren<Renderer>();
                if (r != null && r.sharedMaterial != null)
                {
                    cols.Add(c);
                    vals.Add(Albedo(r.sharedMaterial, materialAlbedo));
                    own++;
                    continue;
                }
                if (c is BoxCollider box)
                {
                    votes.Clear();
                    Transform t = box.transform;
                    Vector3 h = box.size * 0.5f, centre = box.center;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 local = centre + new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
                        Vector3 world = t.TransformPoint(local);
                        // a corner on a cell border can round either way: look in the cell and on both sides of it
                        if (vertexAlbedo.TryGetValue(Key(world), out float a)
                            || vertexAlbedo.TryGetValue(Key(world + nudge), out a)
                            || vertexAlbedo.TryGetValue(Key(world - nudge), out a))
                        {
                            votes.TryGetValue(a, out int n);
                            votes[a] = n + 1;
                        }
                    }
                    float best = 0f;
                    int bestCount = 0;
                    foreach (KeyValuePair<float, int> kv in votes)
                    {
                        if (kv.Value > bestCount) { bestCount = kv.Value; best = kv.Key; }
                    }
                    if (bestCount >= 2)
                    {
                        cols.Add(c);
                        vals.Add(best);
                        byCorner++;
                        continue;
                    }
                }
                // trees and big plants: a trunk capsule next to the LOD meshes
                Transform parent = c.transform.parent;
                if (parent != null && parent.GetComponentInChildren<LODGroup>() != null)
                {
                    Renderer sibling = parent.GetComponentInChildren<Renderer>();
                    if (sibling != null && sibling.sharedMaterial != null)
                    {
                        cols.Add(c);
                        vals.Add(Albedo(sibling.sharedMaterial, materialAlbedo));
                        byLod++;
                        continue;
                    }
                }
                unknown++;
            }

            // 3. one object in the scene holds the table
            SurfaceAlbedoMap map = Object.FindAnyObjectByType<SurfaceAlbedoMap>(FindObjectsInactive.Include);
            if (map == null) map = new GameObject("SurfaceAlbedoMap").AddComponent<SurfaceAlbedoMap>();
            map.Set(cols.ToArray(), vals.ToArray());
            UnityEditor.EditorUtility.SetDirty(map);
            return "albedo map: " + cols.Count + " colliders (" + own + " with a renderer, " + byCorner + " matched to " + mergedMeshes
                + " merged meshes, " + byLod + " trunks, " + unknown + " unknown), " + materialAlbedo.Count + " materials";
        }

        private static long Key(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x / Cell) + (1L << 20);
            long y = Mathf.RoundToInt(p.y / Cell) + (1L << 20);
            long z = Mathf.RoundToInt(p.z / Cell) + (1L << 20);
            return (x & 0x1FFFFF) | ((y & 0x1FFFFF) << 21) | ((z & 0x1FFFFF) << 42);
        }

        private static float Luma(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

        /// <summary>Mean diffuse albedo (linear luminance) of a material: its base colour times the mean of its base map.</summary>
        public static float Albedo(Material m, Dictionary<Material, float> cache)
        {
            if (cache.TryGetValue(m, out float known)) return known;
            Color tint = m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId).linear : Color.gray;
            Texture tex = m.HasProperty(BaseColorMapId) ? m.GetTexture(BaseColorMapId)
                        : m.HasProperty(MainTexId) ? m.GetTexture(MainTexId) : null;
            Color mean = Color.white;
            if (tex is Texture2D)
            {
                // an 8x8 target makes the GPU sample the small mips: 64 texels are the mean of the whole map
                RenderTexture rt = RenderTexture.GetTemporary(8, 8, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
                RenderTexture before = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(8, 8, TextureFormat.RGBAHalf, false, true);
                read.ReadPixels(new Rect(0, 0, 8, 8), 0, 0);
                read.Apply();
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(rt);
                Color[] px = read.GetPixels();
                float r = 0f, g = 0f, b = 0f;
                for (int i = 0; i < px.Length; i++) { r += px[i].r; g += px[i].g; b += px[i].b; }
                mean = new Color(r / px.Length, g / px.Length, b / px.Length);
                Object.DestroyImmediate(read);
            }
            float a = Mathf.Clamp(Luma(new Color(tint.r * mean.r, tint.g * mean.g, tint.b * mean.b)), 0.03f, 0.95f);
            cache[m] = a;
            return a;
        }
    }
}
