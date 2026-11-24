using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Mean diffuse albedo of the surface behind every solid collider of the scene, baked by the Look Pass
    /// (LookAlbedoMap). The world's walls and floors are merged per material when the scene is built, so at run time
    /// a collider no longer knows what it looks like. The flashlight asks here to keep its hot spot off white:
    /// what the eye sees is lux times albedo.
    /// </summary>
    public sealed class SurfaceAlbedoMap : MonoBehaviour
    {
        [SerializeField] private Collider[] colliders = new Collider[0];
        [SerializeField] private float[] albedo = new float[0];

        private static readonly Dictionary<Collider, float> s_Map = new Dictionary<Collider, float>();

        public int Count => colliders != null ? colliders.Length : 0;

        public void Set(Collider[] c, float[] a)
        {
            colliders = c;
            albedo = a;
        }

        private void OnEnable()
        {
            s_Map.Clear();
            if (colliders == null || albedo == null) return;
            int n = Mathf.Min(colliders.Length, albedo.Length);
            for (int i = 0; i < n; i++)
            {
                if (colliders[i] != null) s_Map[colliders[i]] = albedo[i];
            }
        }

        private void OnDisable()
        {
            s_Map.Clear();
        }

        public static bool TryGet(Collider c, out float a)
        {
            return s_Map.TryGetValue(c, out a);
        }
    }
}
