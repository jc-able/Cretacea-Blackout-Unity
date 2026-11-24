using Cretacea.Core;
using UnityEngine;

namespace Cretacea.Player
{
    /// <summary>What the held hand needs to know about the player this frame (filled by FlashlightController).</summary>
    public struct ViewmodelMotionInput
    {
        public float Speed;            // horizontal m/s
        public bool Grounded;
        public bool Sprinting;
        public bool Crouching;
        public bool Exhausted;
        public bool Lowered;           // a document or the field tablet is open
        public bool Holding;           // holding an interaction (lever, pump, breaker, gate)
        public float WallBlend;        // 0..1, how close the torch is to a wall in front
        public float Equip01;          // 0 = just picked up (below the frame), 1 = raised
        public float Terror01;         // 0..1, a predator close by
        public Quaternion CameraRotation;
    }

    /// <summary>
    /// Procedural first-person motion of the held hand and torch, built from damped springs so nothing snaps or
    /// jitters: idle breathing, a walk and sprint gait (figure-eight bob with roll, driven by distance walked, not by
    /// time), a dip on every footstep, look sway that lags behind the camera and banks into turns, the crouch and
    /// sprint carry poses, lowering while a document or the tablet is open or while an interaction is held, a
    /// thumb-press kick when the switch is clicked, a fine tremor while a predator is close, and the wall pull-back.
    /// Offsets are in the sway pivot's local space; rotations turn about the grip.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ViewmodelMotion : MonoBehaviour
    {
        // ---- spring state --------------------------------------------------------------------------------------
        private Vector3 _pos, _posVel;          // positional offset (m)
        private Vector3 _rot, _rotVel;          // pitch, yaw, roll offset (deg)
        private Vector3 _sway, _swayVel;        // look sway rotation (deg), its own softer spring
        private float _gaitPhase;
        private float _gaitWeight;
        private float _breathPhase;
        private float _breathRate = 0.23f;      // Hz
        private Quaternion _lastCamRot;
        private bool _hasCam;
        private Vector2 _angVel;                // smoothed camera yaw/pitch speed, deg/s
        private float _sprintW, _crouchW, _lowerW, _holdW;
        private float _phaseCorrection;         // footstep phase lock still to apply (rad), eased in over ~0.1 s

        public Vector3 PositionOffset { get; private set; }
        public Quaternion RotationOffset { get; private set; } = Quaternion.identity;
        /// <summary>
        /// The aim the beam should follow: only the carry pose and the look sway (no tremor, gait or breathing), so
        /// the lit scene does not shake with every high-frequency hand motion.
        /// </summary>
        public Quaternion BeamRotation { get; private set; } = Quaternion.identity;

        private void OnEnable() { GameEvents.OnPlayerFootstep += OnFootstep; }
        private void OnDisable() { GameEvents.OnPlayerFootstep -= OnFootstep; _hasCam = false; }

        private void OnFootstep(SurfaceType surface, Vector3 pos, bool sprinting, bool crouching)
        {
            if (!isActiveAndEnabled) return;
            // the weight lands on the foot: the hand drops a few millimetres and nods, then the spring brings it back
            float k = sprinting ? 1f : (crouching ? 0.35f : 0.6f);
            _posVel += new Vector3(0f, -0.22f, -0.05f) * k;
            _rotVel += new Vector3(70f, 0f, 0f) * k;
            // phase-lock the gait to the real steps: the bob's low point (cos 2*phase = -1, phase = pi/2 + n*pi)
            // lands on each footstep, so the bob, the camera and the footstep sounds share one rhythm
            float target = Mathf.Round((_gaitPhase - Mathf.PI * 0.5f) / Mathf.PI) * Mathf.PI + Mathf.PI * 0.5f;
            _phaseCorrection = target - _gaitPhase;
        }

        /// <summary>The thumb presses the tail switch: a short squeeze of the grip with a click back.</summary>
        public void Click(bool on)
        {
            // big enough to read at 68 deg FOV: the grip squeezes down and back, the head nods, then springs home
            _posVel += new Vector3(0f, -0.18f, -0.6f);
            _rotVel += new Vector3(on ? 260f : 220f, 0f, -110f);
        }

        /// <summary>Snap the springs to rest (after a teleport or a cutscene).</summary>
        public void ResetMotion()
        {
            _pos = _posVel = _rot = _rotVel = _sway = _swayVel = Vector3.zero;
            _phaseCorrection = 0f;
            _hasCam = false;
        }

        public void Tick(in ViewmodelMotionInput i, float dt)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.05f);
            float Ease(float cur, float target, float rate) => Mathf.Lerp(cur, target, 1f - Mathf.Exp(-rate * dt));

            _sprintW = Ease(_sprintW, i.Sprinting && i.Speed > 1.5f ? 1f : 0f, 7f);
            _crouchW = Ease(_crouchW, i.Crouching ? 1f : 0f, 6f);
            _lowerW = Ease(_lowerW, i.Lowered ? 1f : 0f, 5f);
            _holdW = Ease(_holdW, i.Holding ? 1f : 0f, 6f);

            // ---- camera angular speed -> look sway (lags, banks into the turn) -------------------------------------
            Vector2 angVel = Vector2.zero;
            if (_hasCam)
            {
                // the delta in the camera's own frame: pitch is euler x and yaw euler y at any compass heading
                Quaternion d = Quaternion.Inverse(_lastCamRot) * i.CameraRotation;
                Vector3 e = d.eulerAngles;
                float yaw = Mathf.DeltaAngle(0f, e.y), pitch = Mathf.DeltaAngle(0f, e.x);
                angVel = new Vector2(yaw, pitch) / dt;
                if (angVel.sqrMagnitude > 2000f * 2000f) angVel = Vector2.zero;      // a teleport or a camera cut
            }
            _lastCamRot = i.CameraRotation; _hasCam = true;
            _angVel = Vector2.Lerp(_angVel, angVel, 1f - Mathf.Exp(-18f * dt));
            Vector3 swayTarget = new Vector3(
                Mathf.Clamp(-_angVel.y * 0.022f, -3.5f, 3.5f),         // looking down (pitch rate > 0): the torch trails up
                Mathf.Clamp(-_angVel.x * 0.026f, -5f, 5f),             // turning right: the torch trails left
                Mathf.Clamp(-_angVel.x * 0.018f, -4f, 4f));            // and banks into the turn
            Spring(ref _sway, ref _swayVel, swayTarget, 7.5f, 0.62f, dt);

            // ---- gait: phase follows distance walked, so the bob matches the steps at any speed ---------------------
            float moving = i.Grounded ? Mathf.Clamp01((i.Speed - 0.2f) / 1.2f) : 0f;
            _gaitWeight = Ease(_gaitWeight, moving, 6f);
            // metres per two steps: twice PlayerController's footstep stride (1.55 walk, 2.15 sprint, 1.15 crouch),
            // so cos(2*phase) dips once per footstep
            float stride = Mathf.Lerp(Mathf.Lerp(3.1f, 4.3f, _sprintW), 2.3f, _crouchW);
            _gaitPhase += i.Speed * dt / stride * Mathf.PI * 2f;
            if (Mathf.Abs(_phaseCorrection) > 1e-4f)
            {
                float step = _phaseCorrection * (1f - Mathf.Exp(-dt / 0.1f));
                _gaitPhase += step; _phaseCorrection -= step;
            }
            if (_gaitPhase > Mathf.PI * 16f) _gaitPhase -= Mathf.PI * 16f;
            float ampX = Mathf.Lerp(0.0055f, 0.016f, _sprintW) * Mathf.Lerp(1f, 0.55f, _crouchW);
            float ampY = Mathf.Lerp(0.0040f, 0.011f, _sprintW) * Mathf.Lerp(1f, 0.6f, _crouchW);
            float s = Mathf.Sin(_gaitPhase), c2 = Mathf.Cos(_gaitPhase * 2f);
            Vector3 gaitPos = new Vector3(s * ampX, (c2 - 1f) * 0.5f * ampY, 0f) * _gaitWeight;
            Vector3 gaitRot = new Vector3(
                (1f - c2) * 0.5f * Mathf.Lerp(0.5f, 1.6f, _sprintW),
                s * Mathf.Lerp(0.6f, 1.8f, _sprintW),
                -s * Mathf.Lerp(0.9f, 3.0f, _sprintW)) * _gaitWeight;

            // ---- breathing: slower and shallow at rest, faster and deeper after a sprint or with a predator near ----
            float exertion = Mathf.Max(_sprintW, i.Exhausted ? 1f : 0f);
            float rateTarget = Mathf.Lerp(0.23f, 0.55f, Mathf.Max(exertion, i.Terror01 * 0.8f));
            _breathRate = Mathf.Lerp(_breathRate, rateTarget, 1f - Mathf.Exp(-0.8f * dt));
            _breathPhase += dt * _breathRate * Mathf.PI * 2f;
            if (_breathPhase > Mathf.PI * 2f) _breathPhase -= Mathf.PI * 2f;
            float breathAmp = Mathf.Lerp(1f, 2.4f, Mathf.Max(exertion, i.Terror01)) * (1f - _gaitWeight * 0.5f);
            float b = Mathf.Sin(_breathPhase);
            Vector3 breathPos = new Vector3(0.0008f * Mathf.Sin(_breathPhase * 0.5f), 0.0026f * b, 0.0010f * b) * breathAmp;
            Vector3 breathRot = new Vector3(-0.45f * b, 0.2f * Mathf.Sin(_breathPhase * 0.5f + 1f), 0.25f * b) * breathAmp;

            // ---- tremor: a fine shake with a slow wander, only when a predator is close -----------------------------
            Vector3 tremPos = Vector3.zero, tremRot = Vector3.zero;
            if (i.Terror01 > 0.01f)
            {
                float t = Time.time;
                float a = i.Terror01 * i.Terror01;
                tremPos = new Vector3(Noise(t * 11f, 1.3f), Noise(t * 12.5f, 7.1f), Noise(t * 3f, 4.4f) * 0.5f) * 0.0018f * a;
                tremRot = new Vector3(Noise(t * 10f, 2.9f), Noise(t * 9f, 5.5f), Noise(t * 13f, 8.2f)) * 0.75f * a;
            }

            // ---- carry poses ----------------------------------------------------------------------------------------
            Vector3 poseP = Vector3.zero, poseR = Vector3.zero;
            // sprint: the torch swings low and rolls in across the body
            poseP += new Vector3(-0.025f, -0.055f, -0.03f) * _sprintW;
            poseR += new Vector3(15f, -13f, 9f) * _sprintW;
            // crouch: drawn in closer to the chest, a little lower, head-down aim
            poseP += new Vector3(-0.012f, -0.012f, -0.035f) * _crouchW;
            poseR += new Vector3(3f, -2f, -4f) * _crouchW;
            // document / tablet open: lowered almost out of the frame, beam to the floor
            poseP += new Vector3(0.02f, -0.20f, -0.06f) * _lowerW;
            poseR += new Vector3(38f, 6f, 10f) * _lowerW;
            // holding an interaction: the other hand works, the torch drops and points at the work
            poseP += new Vector3(-0.03f, -0.08f, -0.02f) * _holdW;
            poseR += new Vector3(16f, -9f, 12f) * _holdW;
            // wall in front: pull back, tuck down
            poseP += new Vector3(0f, -0.05f, -0.22f) * i.WallBlend;
            poseR += new Vector3(18f, 0f, 0f) * i.WallBlend;
            // equip raise: from below the frame with the beam tipped down
            float eq = 1f - Mathf.Pow(1f - Mathf.Clamp01(i.Equip01), 3f);
            poseP += new Vector3(0.02f, -0.32f, -0.04f) * (1f - eq);
            poseR += new Vector3(34f, 8f, 14f) * (1f - eq);

            // the carry poses ride one springy layer (a little overshoot when the pose changes)
            Spring(ref _pos, ref _posVel, poseP, 9f, 0.72f, dt);
            Spring(ref _rot, ref _rotVel, poseR, 9.5f, 0.68f, dt);

            BeamRotation = Quaternion.Euler(_rot.x + _sway.x, _rot.y + _sway.y, _rot.z + _sway.z);

            Vector3 p = _pos + gaitPos + breathPos + tremPos;
            Vector3 r = _rot + gaitRot + breathRot + tremRot + _sway;
            // positional sway follows the rotational sway a little (the hand is a lever from the shoulder)
            p += new Vector3(_sway.y * 0.0014f, -_sway.x * 0.0012f, 0f);
            PositionOffset = p;
            RotationOffset = Quaternion.Euler(r.x, r.y, r.z);
        }

        private static float Noise(float x, float seed) => Mathf.PerlinNoise(x, seed) * 2f - 1f;

        /// <summary>Semi-implicit damped spring toward a target (frequency in Hz, zeta = damping ratio).</summary>
        private static void Spring(ref Vector3 x, ref Vector3 v, Vector3 target, float hz, float zeta, float dt)
        {
            float w = 2f * Mathf.PI * hz;
            int n = Mathf.Clamp(Mathf.CeilToInt(dt * 240f), 1, 12);      // sub-steps keep it stable at any frame rate
            float h = dt / n;
            for (int k = 0; k < n; k++)
            {
                Vector3 a = w * w * (target - x) - 2f * zeta * w * v;
                v += a * h;
                x += v * h;
            }
        }
    }
}
