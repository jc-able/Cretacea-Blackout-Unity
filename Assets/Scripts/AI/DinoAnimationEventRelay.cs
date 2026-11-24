using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// Automatically attached to child Animator GameObjects when the Animator is not on the DinoControllerBase root,
    /// forwarding frame-exact FBX Animation Events cleanly to the owning DinoControllerBase.
    /// </summary>
    public sealed class DinoAnimationEventRelay : MonoBehaviour
    {
        private DinoControllerBase _owner;

        public void Bind(DinoControllerBase owner) => _owner = owner;

        private void Awake()
        {
            if (_owner == null) _owner = GetComponentInParent<DinoControllerBase>();
        }

        public void OnFootstepLeft() => _owner?.OnFootstepLeft();
        public void OnFootstepRight() => _owner?.OnFootstepRight();
        public void OnFootstepFrontLeft() => _owner?.OnFootstepFrontLeft();
        public void OnFootstepFrontRight() => _owner?.OnFootstepFrontRight();
        public void OnBiteHitFrame() => _owner?.OnBiteHitFrame();
        public void OnBashImpactFrame() => _owner?.OnBashImpactFrame();
    }
}
