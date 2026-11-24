using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Floor/grating/water surface descriptor for accurate footstep SFX and stealth noise scaling.
    /// </summary>
    public sealed class SurfaceTag : MonoBehaviour, ISurfaceProvider
    {
        [SerializeField] private SurfaceType surfaceType = SurfaceType.Concrete;
        [SerializeField] private float customNoiseMultiplier = -1f;

        public SurfaceType Surface => surfaceType;

        public float NoiseMultiplier
        {
            get
            {
                if (customNoiseMultiplier > 0f) return customNoiseMultiplier;
                return GetDefaultNoiseMultiplier(surfaceType);
            }
        }

        public static float GetDefaultNoiseMultiplier(SurfaceType surface)
        {
            return surface switch
            {
                SurfaceType.Metal => 1.5f,
                SurfaceType.Water => 1.3f,
                SurfaceType.Grass => 0.7f,
                SurfaceType.Mud => 0.85f,
                _ => 1.0f
            };
        }
    }
}
