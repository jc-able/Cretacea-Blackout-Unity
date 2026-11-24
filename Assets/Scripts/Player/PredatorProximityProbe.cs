using Cretacea.Core;
using UnityEngine;

namespace Cretacea.Player
{
    /// <summary>
    /// Tells the flashlight how close the nearest predator is, so the held hand trembles, breathing quickens and the
    /// beam's terror flicker can fire in the real game (not only from a test harness). Four times a second it looks
    /// for dinosaur colliders around the eye, skips herbivores, and reports the nearest one; a predator that has not
    /// noticed anything (idle, patrolling) counts as half again as far away. Reports 99 m when none is near.
    /// Added to the player at runtime by <see cref="FlashlightController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PredatorProximityProbe : MonoBehaviour
    {
        [SerializeField] private float radius = 14f;           // matches FlashlightController.terrorFlickerThresholdDist
        [SerializeField] private float interval = 0.25f;
        [SerializeField] private float unawareDistanceScale = 1.5f;

        private FlashlightController _flashlight;
        private Transform _eye;
        private float _timer;
        private float _lastReported = -1f;
        private readonly Collider[] _hits = new Collider[48];

        public float NearestDistance { get; private set; } = 99f;

        public void Bind(FlashlightController flashlight, Transform eye)
        {
            _flashlight = flashlight;
            _eye = eye;
        }

        private void Update()
        {
            if (_flashlight == null) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = interval;

            Vector3 eye = _eye != null ? _eye.position : transform.position + Vector3.up * 1.6f;
            int n = Physics.OverlapSphereNonAlloc(eye, radius, _hits, GameLayers.Mask(GameLayers.Dinosaur), QueryTriggerInteraction.Collide);
            float best = 99f;
            for (int k = 0; k < n; k++)
            {
                Collider c = _hits[k];
                if (c == null) continue;
                IDinoController dino = c.GetComponentInParent<IDinoController>();
                if (dino == null || !(dino is Behaviour b) || !b.isActiveAndEnabled) continue;
                if (b.GetType().Name.Contains("Herbivore")) continue;
                bool concave = c is MeshCollider mc && !mc.convex;      // ClosestPoint needs a convex shape
                float d = Vector3.Distance(eye, concave ? c.bounds.ClosestPoint(eye) : c.ClosestPoint(eye));
                DinoState s = dino.CurrentState;
                if (s == DinoState.Idle || s == DinoState.Patrol) d *= unawareDistanceScale;
                if (d < best) best = d;
            }
            NearestDistance = best;
            if (Mathf.Abs(best - _lastReported) < 0.05f) return;
            _lastReported = best;
            _flashlight.SetNearestPredatorDistance(best);
        }

        private void OnDisable()
        {
            NearestDistance = 99f;
            _lastReported = -1f;
            if (_flashlight != null) _flashlight.SetNearestPredatorDistance(99f);
        }
    }
}
