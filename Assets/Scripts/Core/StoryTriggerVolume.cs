using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Reusable trigger volume raising GameEvents.OnStoryTriggerEntered when the Player enters.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class StoryTriggerVolume : MonoBehaviour
    {
        [SerializeField] private string triggerId;
        [SerializeField] private bool oneShot = true;

        private bool _fired;

        public string TriggerId => triggerId;
        public bool HasFired => _fired;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Trigger;
        }

        public void ResetTrigger() => _fired = false;

        private void OnTriggerEnter(Collider other)
        {
            if (oneShot && _fired) return;
            if (other.gameObject.layer != GameLayers.Player) return;
            _fired = true;
            GameEvents.RaiseStoryTriggerEntered(triggerId);
        }
    }
}
