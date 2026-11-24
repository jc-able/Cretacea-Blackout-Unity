using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Cretacea.Core;

namespace Cretacea.Interact
{
    public enum DoorMechanism
    {
        SingleHinged = 0,
        DoubleHingedOutward = 1,   // E-0 Service Exit Gate: BOTH leaves swing OUTWARD +X (POS-16 fix!)
        SlidingHorizontal = 2,     // Talon Ridge Raptor Pen Gate: slides +9.5 m East
        JammedRollerShutter = 3    // Loading Dock half-open shutter (1.25 m crouch clearance)
    }

    /// <summary>
    /// Unified door & gate controller implementing IInteractable and IDoor.
    /// Supports single hinged doors, outward-swinging E-0 double gates, sliding heavy containment gates,
    /// jammed crouch-under roller shutters, dynamic NavMeshObstacle carving (carves ONLY when locked/jammed
    /// so unlocked doors stay passable for Velociraptors), and 4-hit raptor door bashing!
    /// </summary>
    public sealed class DoorController : MonoBehaviour, IInteractable, IDoor
    {
        [Header("Identity & Mode")]
        [SerializeField] private string doorId = "door_interior";
        [SerializeField] private DoorMechanism mechanism = DoorMechanism.SingleHinged;
        [SerializeField] private bool startsOpen = false;
        [SerializeField] private bool startsLocked = false;
        [SerializeField] private bool allowPlayerToggle = true;
        [SerializeField] private string openPrompt = "Open door {interact}";
        [SerializeField] private string closePrompt = "Close door {interact}";
        [SerializeField] private string lockedHint = "Locked.";

        [Header("Leaf Transforms")]
        [SerializeField] private Transform primaryLeaf;
        [SerializeField] private Transform secondaryLeaf;
        [SerializeField] private Transform focusAnchor;

        [Header("Motion Parameters")]
        [Tooltip("Hinge open angle in degrees for primary leaf (use negative for opposite swing).")]
        [SerializeField] private float primaryOpenAngleDeg = 95f;
        [Tooltip("Hinge open angle in degrees for secondary leaf (DoubleHingedOutward: leaf B swings opposite +X).")]
        [SerializeField] private float secondaryOpenAngleDeg = -95f;
        [Tooltip("World/local slide displacement for SlidingHorizontal (e.g. Talon Ridge +9.5 m East).")]
        [SerializeField] private Vector3 slideOpenOffset = new Vector3(9.5f, 0f, 0f);
        [SerializeField] private float openDurationSeconds = 0.75f;
        [SerializeField] private float closeDurationSeconds = 0.75f;

        [Header("Raptor Door-Bashing")]
        [SerializeField] private bool bashableByRaptor = true;
        [SerializeField] private int hitsToBreak = 4;

        [Header("Audio Prefixes")]
        [SerializeField] private string soundPrefix = "door_interior"; // door_interior, door_old, door_metal, gate_heavy_slide

        [Header("NavMesh Carving")]
        [SerializeField] private NavMeshObstacle navObstacle;

        private Quaternion _primaryClosedRot;
        private Quaternion _secondaryClosedRot;
        private Vector3 _primaryClosedPos;

        private float _openProgress;
        private bool _isOpen;
        private bool _isLocked;
        private bool _isBroken;
        private int _bashCount;
        private Coroutine _animRoutine;
        private Coroutine _rattleRoutine;

        public string DoorId => doorId;
        public string InteractId => doorId;
        public bool IsOpen => _isOpen;
        public bool IsLocked => _isLocked;
        public bool IsBroken => _isBroken;
        public float OpenProgress => _openProgress;
        public Transform Transform => transform;
        public Transform FocusAnchor => focusAnchor != null ? focusAnchor : transform;

        public bool IsEnabled => allowPlayerToggle && mechanism != DoorMechanism.JammedRollerShutter && !_isBroken;
        public bool CanInteract => IsEnabled && !_isLocked;
        public float HoldDuration => 0f;
        public bool PreserveHoldProgress => false;
        public float SavedHoldProgress => 0f;
        public string PromptText => _isOpen ? closePrompt : openPrompt;
        public string LockedHint => lockedHint;

        private void Awake()
        {
            if (primaryLeaf == null) primaryLeaf = transform;
            _primaryClosedRot = primaryLeaf.localRotation;
            _primaryClosedPos = primaryLeaf.localPosition;
            if (secondaryLeaf != null) _secondaryClosedRot = secondaryLeaf.localRotation;
            if (navObstacle == null) navObstacle = GetComponentInChildren<NavMeshObstacle>(true);

            // Doors live on Layer 11 (Door) so they are excluded from NavMesh bake & collide with Player/Dino
            GameLayers.SetLayerRecursively(gameObject, GameLayers.Door);

            _isLocked = startsLocked;
            _isOpen = startsOpen;
            _openProgress = startsOpen ? 1f : 0f;
            ApplyLeafTransform(_openProgress);
            UpdateNavObstacle();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrWhiteSpace(doorId))
            {
                WorldRegistry.EnsureInstance().RegisterDoor(doorId, this);
                WorldRegistry.EnsureInstance().RegisterInteractable(doorId, this);
            }
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance == null || string.IsNullOrWhiteSpace(doorId)) return;
            WorldRegistry.Instance.UnregisterDoor(doorId, this);
            WorldRegistry.Instance.UnregisterInteractable(doorId, this);
        }

        public void SetLocked(bool locked, string newLockedHint = null)
        {
            _isLocked = locked;
            if (!string.IsNullOrEmpty(newLockedHint))
            {
                lockedHint = newLockedHint;
            }
            UpdateNavObstacle();
        }

        public void OnInteract(GameObject interactor)
        {
            if (_isBroken) return;
            if (_isLocked)
            {
                OnLockedInteract(interactor);
                return;
            }

            if (_isOpen) Close(instant: false);
            else Open(interactor, instant: false);
        }

        public void OnHoldProgress(GameObject interactor, float normalized01) { }

        public void OnLockedInteract(GameObject interactor)
        {
            GameEvents.RaisePlaySound("door_locked_rattle", FocusAnchor.position);
            GameEvents.RaisePlaySound("ui_denied", null, 0.7f);
            if (!string.IsNullOrEmpty(lockedHint))
            {
                GameEvents.RaiseToastMessage(lockedHint, 3.0f);
            }
            TriggerRattle(1.0f);
        }

        public void Open(GameObject opener = null, bool instant = false)
        {
            if (_isOpen && _openProgress >= 0.99f) return;
            _isOpen = true;
            UpdateNavObstacle();

            if (instant)
            {
                StopAnim();
                _openProgress = 1f;
                ApplyLeafTransform(1f);
            }
            else
            {
                PlayDoorAudio(opening: true);
                AnimateTo(1f, openDurationSeconds);
            }

            GameEvents.RaiseDoorStateChanged(doorId, true);
        }

        public void Close(bool instant = false)
        {
            if (!_isOpen && _openProgress <= 0.01f) return;
            _isOpen = false;
            UpdateNavObstacle();

            if (instant)
            {
                StopAnim();
                _openProgress = 0f;
                ApplyLeafTransform(0f);
            }
            else
            {
                PlayDoorAudio(opening: false);
                AnimateTo(0f, closeDurationSeconds);
            }

            GameEvents.RaiseDoorStateChanged(doorId, false);
        }

        /// <summary>
        /// Changes how far a hinged leaf swings (degrees, sign = direction) and re-poses it at its current progress.
        /// Used for doors whose full swing into a corridor meets a prop: the leaf stops short, as a real door would.
        /// </summary>
        public void SetOpenAngle(float primaryDeg)
        {
            primaryOpenAngleDeg = primaryDeg;
            ApplyLeafTransform(_openProgress);
        }

        /// <summary>Directly drives leaf animation progress (0..1) for E-0 gate grinding strain & slam.</summary>
        public void SetNormalizedOpen(float normalized01)
        {
            StopAnim();
            _openProgress = Mathf.Clamp01(normalized01);
            _isOpen = _openProgress > 0.5f;
            ApplyLeafTransform(_openProgress);
            UpdateNavObstacle();
        }

        public Coroutine AnimateOpenTo(float targetProgress01, float durationSeconds, System.Action onComplete = null)
        {
            StopAnim();
            _animRoutine = StartCoroutine(AnimateRoutine(_openProgress, Mathf.Clamp01(targetProgress01), Mathf.Max(0.05f, durationSeconds), onComplete));
            return _animRoutine;
        }

        /// <summary>
        /// Called by Velociraptor AI when blocked by a closed unlocked door.
        /// Rattles fiercely on hits 1..3 and breaks violently open on hit 4!
        /// </summary>
        public bool BashDoor(Vector3 impactWorldPos)
        {
            if (!bashableByRaptor || _isOpen || _isBroken || mechanism != DoorMechanism.SingleHinged)
                return false;
            if (_isLocked)
            {
                // Locked steel/maglock doors rattle hard but hold
                GameEvents.RaisePlaySound("door_bash_metal", impactWorldPos);
                GameEvents.RaiseCameraTrauma(0.18f, 2.2f);
                TriggerRattle(1.6f);
                return false;
            }

            _bashCount++;
            GameEvents.RaiseCameraTrauma(0.22f, 2.0f);
            GameEvents.RaiseNoiseEmitted(impactWorldPos, 18f, "door_bash");

            if (_bashCount >= hitsToBreak)
            {
                _isBroken = true;
                _isOpen = true;
                UpdateNavObstacle();
                GameEvents.RaisePlaySound("door_break_open", impactWorldPos);
                GameEvents.RaiseCameraTrauma(0.45f, 1.5f);
                GameEvents.RaiseSoundCaption("[A DOOR IS BASHED OPEN!]", 2.4f);
                AnimateTo(1.08f, 0.22f);
                GameEvents.RaiseDoorBroken(doorId);
                GameEvents.RaiseDoorStateChanged(doorId, true);
                return true;
            }

            GameEvents.RaisePlaySound("door_bash_hit", impactWorldPos);
            TriggerRattle(2.2f);
            return false;
        }

        private void AnimateTo(float target, float duration)
        {
            StopAnim();
            _animRoutine = StartCoroutine(AnimateRoutine(_openProgress, target, Mathf.Max(0.05f, duration), null));
        }

        private void StopAnim()
        {
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
        }

        private IEnumerator AnimateRoutine(float from, float to, float duration, System.Action onComplete)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                _openProgress = Mathf.Lerp(from, to, k);
                ApplyLeafTransform(_openProgress);
                yield return null;
            }

            _openProgress = to;
            _isOpen = to > 0.5f;
            ApplyLeafTransform(_openProgress);
            UpdateNavObstacle();
            _animRoutine = null;
            onComplete?.Invoke();
        }

        private void ApplyLeafTransform(float progress)
        {
            if (primaryLeaf == null) return;

            switch (mechanism)
            {
                case DoorMechanism.SingleHinged:
                    primaryLeaf.localRotation = _primaryClosedRot * Quaternion.Euler(0f, primaryOpenAngleDeg * progress, 0f);
                    break;

                case DoorMechanism.DoubleHingedOutward:
                    // Leaf A (North hinge Z=-42) & Leaf B (South hinge Z=-50) BOTH swing OUTWARD +X onto Coast Road
                    primaryLeaf.localRotation = _primaryClosedRot * Quaternion.Euler(0f, primaryOpenAngleDeg * progress, 0f);
                    if (secondaryLeaf != null)
                        secondaryLeaf.localRotation = _secondaryClosedRot * Quaternion.Euler(0f, secondaryOpenAngleDeg * progress, 0f);
                    break;

                case DoorMechanism.SlidingHorizontal:
                    primaryLeaf.localPosition = _primaryClosedPos + slideOpenOffset * progress;
                    break;

                case DoorMechanism.JammedRollerShutter:
                    // Static jammed half-open height (1.25 m clear crouch gap)
                    break;
            }
        }

        private void UpdateNavObstacle()
        {
            if (navObstacle == null) return;
            // Carve NavMesh ONLY when locked/jammed or closed un-bashable gate; unlocked doors stay walkable for Raptors
            bool shouldCarve = _isLocked || (!bashableByRaptor && !_isOpen);
            navObstacle.carving = shouldCarve;
            navObstacle.enabled = shouldCarve;
        }

        private void PlayDoorAudio(bool opening)
        {
            string clip = mechanism == DoorMechanism.SlidingHorizontal
                ? "gate_heavy_slide"
                : $"{soundPrefix}_{(opening ? "open" : "close")}";
            GameEvents.RaisePlaySound(clip, FocusAnchor.position);
            GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 11f, "door");
        }

        private void TriggerRattle(float amplitudeDeg)
        {
            if (primaryLeaf == null || mechanism != DoorMechanism.SingleHinged) return;
            if (_rattleRoutine != null) StopCoroutine(_rattleRoutine);
            _rattleRoutine = StartCoroutine(RattleRoutine(amplitudeDeg));
        }

        private IEnumerator RattleRoutine(float amp)
        {
            float elapsed = 0f;
            const float dur = 0.24f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - elapsed / dur;
                float angle = Mathf.Sin(elapsed * 58f) * amp * decay;
                primaryLeaf.localRotation = _primaryClosedRot * Quaternion.Euler(0f, primaryOpenAngleDeg * _openProgress + angle, 0f);
                yield return null;
            }
            ApplyLeafTransform(_openProgress);
            _rattleRoutine = null;
        }
    }
}
