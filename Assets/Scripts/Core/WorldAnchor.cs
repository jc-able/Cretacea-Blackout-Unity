using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Drops onto any scene GameObject to auto-register as a named anchor and/or named object in WorldRegistry.
    /// </summary>
    public sealed class WorldAnchor : MonoBehaviour
    {
        [SerializeField] private string anchorId;
        [SerializeField] private bool registerAsNamedObject = true;

        public string AnchorId => anchorId;

        private void OnEnable()
        {
            WorldRegistry reg = WorldRegistry.EnsureInstance();
            if (!string.IsNullOrWhiteSpace(anchorId))
            {
                reg.RegisterAnchor(anchorId, transform);
                if (registerAsNamedObject) reg.RegisterObject(anchorId, gameObject);
            }
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance == null || string.IsNullOrWhiteSpace(anchorId)) return;
            WorldRegistry.Instance.UnregisterAnchor(anchorId, transform);
            if (registerAsNamedObject) WorldRegistry.Instance.UnregisterObject(anchorId, gameObject);
        }
    }
}
