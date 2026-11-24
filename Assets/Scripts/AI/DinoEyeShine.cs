using UnityEngine;

namespace Cretacea.AI
{
    /// <summary>
    /// Retro-reflective eyeshine (tapetum lucidum): when the player's flashlight falls on a dinosaur that is looking
    /// roughly toward it, the eyes send a little of that light straight back. Luminance follows the real flashlight
    /// illuminance at the head (candela / d^2 x cone falloff), so it is a faint glint at the edge of the beam and a
    /// clear pair of amber points in the hot spot, never a self-lit cartoon eye. Writes through <see cref="DinoWetness"/>
    /// (which owns the renderer's MaterialPropertyBlock); the material's emissive map is the eye mask.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(DinoWetness))]
    public sealed class DinoEyeShine : MonoBehaviour
    {
        [SerializeField] private Color shineColor = new Color(1.0f, 0.78f, 0.36f);
        [Tooltip("Retro-reflection gain: eye luminance (nits) = lux at the eye x gain / pi.")]
        [SerializeField] private float retroGain = 0.6f;
        [Tooltip("Cap (nits). Storm exposure is EV 3-4.7 (display white ~10-30 nits): 8 keeps the glint a small amber point; 15 still clipped white at EV 3.")]
        [SerializeField] private float maxNits = 8f;
        [Tooltip("Eyes are on the side of the skull: shine when the head faces within this cone of the light (cos).")]
        [SerializeField] private float facingCosMin = 0.15f;
        [SerializeField] private float facingCosFull = 0.75f;

        /// <summary>Retro-reflection gain (the T. rex raises it so the torch finds its eyes across the valley).</summary>
        public float RetroGain { get => retroGain; set => retroGain = Mathf.Max(0f, value); }

        private DinoWetness _skin;
        private DinoProceduralRig _rig;
        private Light _flashlight;
        private bool _searchedFlashlight;
        private float _nextLightSearch;
        private float _nextOcclusionTest;
        private bool _occluded;
        private float _current;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            _skin = GetComponent<DinoWetness>();
            _rig = GetComponent<DinoProceduralRig>();
        }

        private void OnDisable()
        {
            if (_skin != null) _skin.EyeShine = Color.black;
            _current = 0f;
        }

        private void LateUpdate()
        {
            float target = ComputeTargetNits();
            _current = Mathf.Lerp(_current, target, 1f - Mathf.Exp(-14f * Time.deltaTime));
            _skin.EyeShine = _current > 0.05f ? new Color(shineColor.r * _current, shineColor.g * _current, shineColor.b * _current, 1f) : Color.black;
        }

        private float ComputeTargetNits()
        {
            // Cache the flashlight's spot once; re-query (cheaply, every 2 s) only while there is none.
            if (_flashlight == null && (!_searchedFlashlight || Time.time >= _nextLightSearch))
            {
                _searchedFlashlight = true;
                _nextLightSearch = Time.time + 2f;
                _flashlight = FindFlashlightSpot();
            }
            if (_flashlight == null || !_flashlight.isActiveAndEnabled || _flashlight.intensity <= 0f) return 0f;

            Vector3 eye = _rig != null ? _rig.GetEyeWorldPosition() : transform.position + Vector3.up;
            Transform lt = _flashlight.transform;
            Vector3 toEye = eye - lt.position;
            float d = toEye.magnitude;
            if (d < 0.3f || d > _flashlight.range) return 0f;
            Vector3 dir = toEye / d;

            // Spot cone: the FlashlightController's cookie keeps only ~12 % of the hot spot at the beam edge (the
            // inner angle is 0.96 x outer, so inner/outer alone would glow at full strength right up to the edge).
            float r = Vector3.Angle(lt.forward, dir) / Mathf.Max(1f, 0.5f * _flashlight.spotAngle);
            if (r >= 1f) return 0f;
            float cone = Mathf.Lerp(1f, 0.12f, Mathf.SmoothStep(0f, 1f, (r - 0.3f) / 0.7f));

            // the head has to look back toward the light
            Vector3 headFwd = _rig != null ? _rig.GetHeadForwardWorld() : transform.forward;
            float facing = Mathf.InverseLerp(facingCosMin, facingCosFull, Vector3.Dot(headFwd, -dir));
            if (facing <= 0f) return 0f;

            if (Time.time >= _nextOcclusionTest)
            {
                _nextOcclusionTest = Time.time + 0.12f;
                _occluded = IsOccluded(lt.position, eye, d);
            }
            if (_occluded) return 0f;

            float lux = _flashlight.intensity / (d * d) * cone;      // Unity 6 HDRP stores spot intensity in candela
            float nits = lux * retroGain / Mathf.PI * facing;
            return Mathf.Min(nits, maxNits);
        }

        private bool IsOccluded(Vector3 from, Vector3 to, float dist)
        {
            Vector3 dir = (to - from) / dist;
            int mask = ~((1 << gameObject.layer) | (1 << 2));
            var player = Cretacea.Player.PlayerController.Instance;
            Transform playerRoot = player != null ? player.transform : null;
            int n = Physics.RaycastNonAlloc(from, dir, _hits, Mathf.Max(0f, dist - 0.5f), mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Transform h = _hits[i].collider.transform;
                if (h.IsChildOf(transform)) continue;
                if (playerRoot != null && h.IsChildOf(playerRoot)) continue;   // the player's own capsule / held items
                return true;
            }
            return false;
        }

        private static Light FindFlashlightSpot()
        {
            var player = Cretacea.Player.PlayerController.Instance;
            if (player == null || player.Flashlight == null) return null;
            Light[] lights = player.Flashlight.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Spot) return lights[i];
            }
            return null;
        }
    }
}
