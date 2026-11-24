using System;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Interact
{
    /// <summary>
    /// Flexible interactable for Security Terminal clock-in, Control Room Paddock Feed Console,
    /// burnt Lobby shutter switch, dead Service Gate box, and E-0 Gate Control Box (9.0 s cumulative hold).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SimpleInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string interactId = "terminal";
        [SerializeField] private string promptText = "Use {interact}";
        [SerializeField] private string lockedHint = "No power.";
        [SerializeField] private float holdDuration = 0f;
        [SerializeField] private bool preserveHoldProgress = false;
        [SerializeField] private bool oneShot = true;
        [SerializeField] private bool startsEnabled = true;
        [SerializeField] private bool startsLocked = false;
        [SerializeField] private Transform focusAnchor;

        private bool _isEnabled;
        private bool _isLocked;
        private float _savedHoldProgress;

        public event Action<GameObject> OnActivated;
        public event Action<GameObject, float> OnHoldUpdated;
        public event Action<GameObject> OnLockedAttempted;

        public string InteractId => interactId;
        public string PromptText => promptText;
        public string LockedHint => lockedHint;
        public bool CanInteract => _isEnabled && !_isLocked;
        public bool IsEnabled => _isEnabled && gameObject.activeInHierarchy;
        public bool IsLocked => _isLocked;
        public float HoldDuration => holdDuration;
        public bool PreserveHoldProgress => preserveHoldProgress;
        public float SavedHoldProgress => _savedHoldProgress;
        public Transform FocusAnchor => focusAnchor != null ? focusAnchor : transform;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Interactable;
            _isEnabled = startsEnabled;
            _isLocked = startsLocked;
        }

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(interactId))
                WorldRegistry.EnsureInstance().RegisterInteractable(interactId, this);
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance != null && !string.IsNullOrWhiteSpace(interactId))
                WorldRegistry.Instance.UnregisterInteractable(interactId, this);
        }

        public void Configure(
            string id,
            string prompt,
            float holdSec = 0f,
            bool preserveProgress = false,
            bool singleUse = true,
            bool enabledState = true,
            bool lockedState = false,
            string hintIfLocked = "")
        {
            interactId = id;
            promptText = prompt;
            holdDuration = holdSec;
            preserveHoldProgress = preserveProgress;
            oneShot = singleUse;
            _isEnabled = enabledState;
            _isLocked = lockedState;
            lockedHint = hintIfLocked;
            if (!string.IsNullOrWhiteSpace(interactId))
                WorldRegistry.EnsureInstance().RegisterInteractable(interactId, this);
        }

        public void SetEnabledState(bool enabledState) => _isEnabled = enabledState;

        public void SetLockedState(bool locked, string newLockedHint = null)
        {
            _isLocked = locked;
            if (!string.IsNullOrEmpty(newLockedHint)) lockedHint = newLockedHint;
        }

        public void SetSavedHoldProgress(float progress01)
        {
            _savedHoldProgress = Mathf.Clamp01(progress01);
        }

        public void OnInteract(GameObject interactor)
        {
            if (!_isEnabled) return;
            if (_isLocked)
            {
                OnLockedInteract(interactor);
                return;
            }

            if (oneShot) _isEnabled = false;
            OnActivated?.Invoke(interactor);
        }

        public void OnHoldProgress(GameObject interactor, float normalized01)
        {
            if (preserveHoldProgress)
            {
                _savedHoldProgress = Mathf.Max(_savedHoldProgress, Mathf.Clamp01(normalized01));
                OnHoldUpdated?.Invoke(interactor, _savedHoldProgress);
            }
            else
            {
                OnHoldUpdated?.Invoke(interactor, Mathf.Clamp01(normalized01));
            }
        }

        public void OnLockedInteract(GameObject interactor)
        {
            GameEvents.RaisePlaySound("ui_denied", null, 0.75f);
            if (!string.IsNullOrEmpty(lockedHint))
            {
                GameEvents.RaiseToastMessage(lockedHint, 3.0f);
            }
            OnLockedAttempted?.Invoke(interactor);
        }
    }
}
