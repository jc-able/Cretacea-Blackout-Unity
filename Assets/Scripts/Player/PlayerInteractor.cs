using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Player
{
    /// <summary>
    /// First-person raycast + forgiving thin-sphere interaction scanner.
    /// Occluded by solid Environment/Door colliders so players cannot interact through walls.
    /// Always surfaces active PromptText OR prerequisite LockedHint (UX-4), manages instant and hold-to-use
    /// interactions (preserving E-0 gate high-water progress), and prevents double-triggers after document close.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform cameraOrigin;
        [SerializeField] private PlayerController playerController;

        [Header("Reach & Casting")]
        [SerializeField] private float reachDistance = 2.45f;
        [SerializeField] private float sphereFallbackRadius = 0.13f;
        [SerializeField] private float holdReleaseDecayPerSec = 1.5f;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

        private IInteractable _focused;
        private float _holdProgress;
        private bool _holdingActive;
        private bool _requireButtonRelease;
        private bool _wasDocumentOpen;
        private InteractionFocusInfo _lastBroadcast;

        public IInteractable FocusedInteractable => _focused;
        public float CurrentHoldProgress01 => _holdProgress;

        private void Awake()
        {
            if (playerController == null) playerController = GetComponentInParent<PlayerController>();
            if (cameraOrigin == null && playerController != null) cameraOrigin = playerController.CameraPivot;
        }

        private void Update()
        {
            GameState gs = GameState.Instance;
            if (gs != null && gs.IsPaused) return;

            bool docOpen = gs != null && gs.IsDocumentOpen;
            if (_wasDocumentOpen && !docOpen)
            {
                // Require key release after closing a document before accepting a new interact press
                _requireButtonRelease = true;
            }
            _wasDocumentOpen = docOpen;

            // (and none while the field tablet is open: E / gamepad X press the tablet's focused button there, and must not
            // also open a door or take an item behind it)
            if (gs != null && (gs.IsGameplayInputBlocked || gs.IsJournalOpen))
            {
                ClearFocus();
                return;
            }

            CretaceaInput input = CretaceaInput.Instance;
            if (input != null && _requireButtonRelease && !input.InteractHeld)
            {
                _requireButtonRelease = false;
            }

            IInteractable nextTarget = FindFocusedInteractable();
            if (!ReferenceEquals(nextTarget, _focused))
            {
                _focused = nextTarget;
                _holdingActive = false;
                _holdProgress = _focused != null && _focused.PreserveHoldProgress ? _focused.SavedHoldProgress : 0f;
            }

            if (_focused != null && input != null && !_requireButtonRelease)
            {
                ProcessInteractionInput(input);
            }
            else if (_focused == null)
            {
                _holdingActive = false;
                _holdProgress = 0f;
            }

            BroadcastFocusIfChanged();
        }

        private IInteractable FindFocusedInteractable()
        {
            if (cameraOrigin == null) return null;

            Vector3 origin = cameraOrigin.position;
            Vector3 dir = cameraOrigin.forward;

            // Measure distance to nearest solid wall/door so interactables behind walls are strictly occluded
            float maxDist = reachDistance;
            if (Physics.Raycast(origin, dir, out RaycastHit wallHit, reachDistance, GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore))
            {
                // Allow interacting with a Door directly hit by the ray!
                IInteractable directDoor = wallHit.collider.GetComponentInParent<IInteractable>();
                if (directDoor != null && directDoor.IsEnabled)
                {
                    return directDoor;
                }
                maxDist = wallHit.distance + 0.08f;
            }

            // 1. Exact center ray against Interactable layer (supports trigger colliders)
            IInteractable best = QueryClosestInteractable(origin, dir, maxDist, 0f);
            if (best != null) return best;

            // 2. Forgiving thin SphereCast fallback for thin desks, valve wheels, and levers
            return QueryClosestInteractable(origin, dir, maxDist, sphereFallbackRadius);
        }

        private IInteractable QueryClosestInteractable(Vector3 origin, Vector3 dir, float maxDist, float radius)
        {
            int mask = GameLayers.Mask(GameLayers.Interactable) | GameLayers.Mask(GameLayers.Door);
            int count = radius <= 0.001f
                ? Physics.RaycastNonAlloc(origin, dir, _hitBuffer, maxDist, mask, QueryTriggerInteraction.Collide)
                : Physics.SphereCastNonAlloc(origin, radius, dir, _hitBuffer, maxDist, mask, QueryTriggerInteraction.Collide);

            IInteractable closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.collider == null) continue;
                IInteractable candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.IsEnabled) continue;

                // Secondary LOS check to FocusAnchor if spherecast reached around a wall edge
                if (radius > 0f && candidate.FocusAnchor != null)
                {
                    Vector3 toAnchor = candidate.FocusAnchor.position - origin;
                    if (Physics.Raycast(origin, toAnchor.normalized, out RaycastHit blockHit, Mathf.Max(0.05f, toAnchor.magnitude - 0.12f), GameLayers.EnvironmentAndDoorMask, QueryTriggerInteraction.Ignore))
                    {
                        if (!blockHit.collider.transform.IsChildOf(candidate.FocusAnchor) && blockHit.collider.GetComponentInParent<IInteractable>() != candidate)
                            continue;
                    }
                }

                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    closest = candidate;
                }
            }

            return closest;
        }

        private void ProcessInteractionInput(CretaceaInput input)
        {
            // Locked or prerequisite-unmet station: still respond cleanly on tap with LockedHint feedback!
            if (_focused.IsLocked || !_focused.CanInteract)
            {
                if (input.InteractPressedThisFrame)
                {
                    _focused.OnLockedInteract(gameObject);
                    _requireButtonRelease = true;
                }
                _holdingActive = false;
                return;
            }

            // Instant tap interaction
            if (_focused.HoldDuration <= 0.001f)
            {
                if (input.InteractPressedThisFrame)
                {
                    _focused.OnInteract(gameObject);
                    _requireButtonRelease = true;
                }
                return;
            }

            // Hold interaction (Fuel Pump 4 s, Breaker 1.2 s, Transfer Lever 2.2 s, E-0 Gate 9.0 s saved)
            if (input.InteractHeld)
            {
                if (!_holdingActive)
                {
                    _holdingActive = true;
                    if (_focused.PreserveHoldProgress)
                    {
                        _holdProgress = Mathf.Max(_holdProgress, _focused.SavedHoldProgress);
                    }
                }

                _holdProgress = Mathf.Clamp01(_holdProgress + Time.deltaTime / _focused.HoldDuration);
                _focused.OnHoldProgress(gameObject, _holdProgress);

                if (_holdProgress >= 1f)
                {
                    _holdingActive = false;
                    _requireButtonRelease = true;
                    _focused.OnInteract(gameObject);
                    _holdProgress = 0f;
                }
            }
            else
            {
                _holdingActive = false;
                if (_focused.PreserveHoldProgress)
                {
                    _holdProgress = _focused.SavedHoldProgress;
                }
                else if (_holdProgress > 0f)
                {
                    _holdProgress = Mathf.Max(0f, _holdProgress - holdReleaseDecayPerSec * Time.deltaTime);
                    _focused.OnHoldProgress(gameObject, _holdProgress);
                }
            }
        }

        private void ClearFocus()
        {
            _focused = null;
            _holdingActive = false;
            _holdProgress = 0f;
            if (_lastBroadcast.HasTarget)
            {
                _lastBroadcast = InteractionFocusInfo.Empty;
                GameEvents.RaiseInteractionFocusChanged(_lastBroadcast);
            }
        }

        private void BroadcastFocusIfChanged()
        {
            CretaceaInput input = CretaceaInput.Instance;
            InteractionFocusInfo current;

            if (_focused == null || !_focused.IsEnabled)
            {
                current = InteractionFocusInfo.Empty;
            }
            else
            {
                string prompt = input != null ? input.FormatPrompt(_focused.PromptText) : _focused.PromptText;
                string hint = input != null ? input.FormatPrompt(_focused.LockedHint) : _focused.LockedHint;
                current = new InteractionFocusInfo(
                    hasTarget: true,
                    interactId: _focused.InteractId,
                    promptText: prompt,
                    lockedHint: hint,
                    isLocked: _focused.IsLocked || !_focused.CanInteract,
                    canInteract: _focused.CanInteract && !_focused.IsLocked,
                    holdDuration: _focused.HoldDuration,
                    holdProgress01: _holdProgress,
                    preserveHoldProgress: _focused.PreserveHoldProgress);
            }

            bool changed = current.HasTarget != _lastBroadcast.HasTarget
                           || current.InteractId != _lastBroadcast.InteractId
                           || current.PromptText != _lastBroadcast.PromptText
                           || current.LockedHint != _lastBroadcast.LockedHint
                           || current.IsLocked != _lastBroadcast.IsLocked
                           || Mathf.Abs(current.HoldProgress01 - _lastBroadcast.HoldProgress01) > 0.005f;

            if (changed)
            {
                _lastBroadcast = current;
                GameEvents.RaiseInteractionFocusChanged(current);
            }
        }
    }
}
