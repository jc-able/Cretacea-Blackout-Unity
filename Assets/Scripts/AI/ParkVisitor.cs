using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// A park visitor for the daytime prologue: either stands (idle / look around / phone / clap / cheer / wave)
    /// facing something, or strolls back and forth along a line at the walk clip's own stride speed so the feet do
    /// not skate. Feet are kept on the ground with a short downward raycast. No colliders, no AI, no NavMesh: the
    /// visitors exist only for the opening cutscene and are destroyed when it ends.
    /// Prefabs are built by <c>cretacea build_character_prefabs</c> (Assets/Editor/DinoAnimatorBuilder.cs).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParkVisitor : MonoBehaviour
    {
        public enum Activity { Idle, LookAround, Phone, Clap, Cheer, Wave, Walk }

        [SerializeField] private Animator animator;
        [Tooltip("Ground speed of the walk clip at this prefab's scale (m/s), measured by the builder.")]
        [SerializeField] private float walkClipSpeed = 0.8f;
        [SerializeField] private float turnSpeedDeg = 140f;
        [SerializeField] private LayerMask groundMask = ~0;

        private bool _strolling;
        private Vector3 _a, _b, _target;
        private float _speed;
        private float _groundTimer;
        private float _turnPause;
        private float _baseScaleY = 1f;

        /// <summary>Ground speed of the walk clip at the current scale (a taller copy strides further per cycle).</summary>
        private float ScaledClipSpeed => walkClipSpeed * Mathf.Max(0.1f, transform.localScale.y / _baseScaleY);

        public float WalkClipSpeed => walkClipSpeed;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _baseScaleY = Mathf.Max(0.01f, transform.localScale.y);
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            if (ignore >= 0) groundMask &= ~(1 << ignore);
            int dino = LayerMask.NameToLayer("Dinosaur");
            if (dino >= 0) groundMask &= ~(1 << dino);
            int player = LayerMask.NameToLayer("Player");
            if (player >= 0) groundMask &= ~(1 << player);
        }

        /// <summary>Stand in place doing <paramref name="activity"/>, facing <paramref name="lookAt"/>.</summary>
        public void Stand(Activity activity, Vector3 lookAt, float clipOffset01, float clipRate = 1f)
        {
            _strolling = false;
            SnapToGround(transform.position);
            Vector3 d = lookAt - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            Play(activity == Activity.Walk ? Activity.Idle : activity, clipOffset01, Mathf.Clamp(clipRate, 0.5f, 1.5f));
        }

        /// <summary>Walk between <paramref name="a"/> and <paramref name="b"/>, starting <paramref name="start01"/> of the way along.</summary>
        public void Stroll(Vector3 a, Vector3 b, float speedScale, float start01, float clipOffset01)
        {
            _strolling = true;
            _a = a; _b = b;
            _speed = ScaledClipSpeed * Mathf.Clamp(speedScale, 0.8f, 1.2f);
            Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(start01));
            _target = b;
            SnapToGround(p);
            Vector3 d = _target - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            // play the walk at the rate that matches the chosen ground speed
            Play(Activity.Walk, clipOffset01, _speed / Mathf.Max(0.05f, ScaledClipSpeed));
        }

        private void Play(Activity activity, float offset01, float speed)
        {
            if (animator == null) return;
            animator.speed = speed;
            string state = activity.ToString();
            int hash = Animator.StringToHash(state);
            if (!animator.HasState(0, hash)) hash = Animator.StringToHash(nameof(Activity.Idle));
            animator.Play(hash, 0, Mathf.Repeat(offset01, 1f));
        }

        private void Update()
        {
            if (!_strolling) return;
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            Vector3 to = _target - pos; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.3f)
            {
                _target = (_target - _b).sqrMagnitude < 0.01f ? _a : _b;
                to = _target - pos; to.y = 0f;
                _turnPause = 0.9f;
            }

            Quaternion want = to.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(to.normalized, Vector3.up) : transform.rotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeedDeg * dt);
            float facing = Mathf.Clamp01(Vector3.Dot(transform.forward, to.normalized));
            if (_turnPause > 0f) { _turnPause -= dt; facing *= 0.35f; }
            pos += transform.forward * (_speed * facing * dt);
            transform.position = pos;
            if (animator != null) animator.speed = Mathf.Max(0.3f, facing) * _speed / Mathf.Max(0.05f, ScaledClipSpeed);

            _groundTimer -= dt;
            if (_groundTimer <= 0f)
            {
                _groundTimer = 0.1f;
                SnapToGround(transform.position);
            }
        }

        private void SnapToGround(Vector3 p)
        {
            if (Physics.Raycast(new Vector3(p.x, p.y + 1.6f, p.z), Vector3.down, out RaycastHit hit, 4.5f, groundMask, QueryTriggerInteraction.Ignore))
            {
                p.y = hit.point.y;
            }
            transform.position = p;
        }
    }
}
