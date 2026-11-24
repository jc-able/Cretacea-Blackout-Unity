using UnityEngine;

namespace Cretacea.Player
{
    /// <summary>
    /// Applies a constant rig-space positional offset to an animated bone AFTER the Animator has written
    /// its pose each frame (LateUpdate). Used to seat the held tactical flashlight deeper into the gloved palm
    /// (QA P1-7) without re-authoring the FBX clips, which key Flashlight_Bone position on every frame.
    /// Safe if the Animator is disabled/culled: the previous frame's offset is reverted before re-applying,
    /// so the offset never accumulates.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelGripOffset : MonoBehaviour
    {
        [Tooltip("Bone to offset (e.g. Flashlight_Bone). Only vertices skinned to this bone move.")]
        [SerializeField] private Transform targetBone;

        [Tooltip("Offset in this rig root's local space (meters). +Y up, +Z forward along the beam.")]
        [SerializeField] private Vector3 rigSpaceOffset = new Vector3(0f, -0.025f, -0.02f);

        private bool _hasApplied;
        private Vector3 _lastAppliedLocalPos;
        private Vector3 _lastBaseLocalPos;

        public Vector3 RigSpaceOffset
        {
            get => rigSpaceOffset;
            set => rigSpaceOffset = value;
        }

        private void Awake()
        {
            if (targetBone == null)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == "Flashlight_Bone")
                    {
                        targetBone = t;
                        break;
                    }
                }
            }
        }

        private void OnDisable()
        {
            RevertIfUnchanged();
            _hasApplied = false;
        }

        private void LateUpdate()
        {
            if (targetBone == null || targetBone.parent == null) return;

            // If the Animator did not rewrite the bone this frame, strip last frame's offset first.
            RevertIfUnchanged();

            Vector3 baseLocal = targetBone.localPosition;
            Vector3 worldDelta = transform.TransformVector(rigSpaceOffset);
            Vector3 localDelta = targetBone.parent.InverseTransformVector(worldDelta);

            targetBone.localPosition = baseLocal + localDelta;
            _lastBaseLocalPos = baseLocal;
            _lastAppliedLocalPos = targetBone.localPosition;
            _hasApplied = true;
        }

        private void RevertIfUnchanged()
        {
            if (!_hasApplied || targetBone == null) return;
            if ((targetBone.localPosition - _lastAppliedLocalPos).sqrMagnitude < 1e-12f)
            {
                targetBone.localPosition = _lastBaseLocalPos;
            }
            _hasApplied = false;
        }
    }
}
