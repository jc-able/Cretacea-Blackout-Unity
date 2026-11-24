using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Marks Tyrant Valley tall-grass stealth concealment patches.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class TallGrassZoneVolume : MonoBehaviour
    {
        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Trigger;
        }
    }
}
