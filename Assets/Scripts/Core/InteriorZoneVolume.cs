using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Marks indoor building footprints for ambience low-pass filtering & reverb transitions.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class InteriorZoneVolume : MonoBehaviour
    {
        [SerializeField] private string zoneId = "vc_offices";
        private static int _activeInteriorDepth;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Trigger;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer != GameLayers.Player) return;
            _activeInteriorDepth++;
            GameEvents.RaiseInteriorChanged(true, zoneId);
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.layer != GameLayers.Player) return;
            _activeInteriorDepth = Mathf.Max(0, _activeInteriorDepth - 1);
            if (_activeInteriorDepth == 0)
            {
                GameEvents.RaiseInteriorChanged(false, string.Empty);
            }
        }

        public static void ResetCounter() => _activeInteriorDepth = 0;
    }
}
