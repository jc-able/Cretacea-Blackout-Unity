using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cretacea.Core
{
    /// <summary>
    /// Self-contained Unity New Input System driver binding KB+M and Gamepad cleanly in code.
    /// Zero manual Inspector wiring required; handles dynamic device switching, prompt token formatting,
    /// and trauma-driven dual-motor gamepad rumble.
    /// </summary>
    public sealed class CretaceaInput : MonoBehaviour
    {
        public static CretaceaInput Instance { get; private set; }

        public InputActionAsset ActionAsset { get; private set; }
        public InputActionMap GameplayMap { get; private set; }

        public InputAction MoveAction { get; private set; }
        public InputAction LookMouseAction { get; private set; }
        public InputAction LookStickAction { get; private set; }
        public InputAction SprintAction { get; private set; }
        public InputAction SprintLatchAction { get; private set; }
        public InputAction CrouchAction { get; private set; }
        public InputAction FlashlightAction { get; private set; }
        public InputAction InteractAction { get; private set; }
        public InputAction JournalAction { get; private set; }
        public InputAction PauseAction { get; private set; }
        public InputAction SkipCutsceneAction { get; private set; }
        public InputAction CancelAction { get; private set; }

        public InputDeviceType ActiveDevice { get; private set; } = InputDeviceType.KeyboardMouse;

        private float _rumbleLow;
        private float _rumbleHigh;
        private float _rumbleTimer;

        // ---- Dev-only bot override ---------------------------------------------------------------------------------
        // While BotOverride is set (only by Cretacea.Dev.PlaythroughBot in Play mode), every value below is read from it
        // instead of the devices. Requests are latched once per frame at the top of the frame (this component runs
        // first), so "pressed this frame" means exactly one frame for every reader, as with a real key. Null = devices,
        // and the code paths are then exactly what they were before the override existed.
        public enum BotButton { Sprint, SprintLatch, Crouch, Flashlight, Interact, Journal, Pause, Skip, Cancel, Count }

        public sealed class BotInput
        {
            public Vector2 Move;              // held stick / WASD value
            public Vector2 LookDelta;         // mouse delta for the next frame (consumed once)
            public readonly bool[] Held = new bool[(int)BotButton.Count];
            internal readonly bool[] Tap = new bool[(int)BotButton.Count];
            /// <summary>Press and release a button: down for one frame.</summary>
            public void Press(BotButton b) => Tap[(int)b] = true;
        }

        public static BotInput BotOverride { get; set; }

        private readonly bool[] _botCur = new bool[(int)BotButton.Count];
        private readonly bool[] _botPrev = new bool[(int)BotButton.Count];
        private Vector2 _botLook;
        private int _botLatchedFrame = -1;

        private void LatchBot()
        {
            BotInput bot = BotOverride;
            if (bot == null || _botLatchedFrame == Time.frameCount) return;
            _botLatchedFrame = Time.frameCount;
            for (int i = 0; i < _botCur.Length; i++)
            {
                _botPrev[i] = _botCur[i];
                _botCur[i] = bot.Held[i] || bot.Tap[i];
                bot.Tap[i] = false;
            }
            _botLook = bot.LookDelta;
            bot.LookDelta = Vector2.zero;
        }

        private bool BotHeld(BotButton b) { LatchBot(); return _botCur[(int)b]; }
        private bool BotDown(BotButton b) { LatchBot(); return _botCur[(int)b] && !_botPrev[(int)b]; }
        private bool BotUp(BotButton b) { LatchBot(); return !_botCur[(int)b] && _botPrev[(int)b]; }

        public Vector2 Move => BotOverride != null ? BotOverride.Move : (MoveAction != null ? MoveAction.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 LookMouseDelta => BotOverride != null ? BotLook() : (LookMouseAction != null ? LookMouseAction.ReadValue<Vector2>() : Vector2.zero);
        public Vector2 LookStick => BotOverride != null ? Vector2.zero : (LookStickAction != null ? LookStickAction.ReadValue<Vector2>() : Vector2.zero);

        private Vector2 BotLook() { LatchBot(); return _botLook; }

        public bool IsSprintHeld => BotOverride != null ? BotHeld(BotButton.Sprint) : SprintAction != null && SprintAction.IsPressed();
        public bool SprintPressedThisFrame => BotOverride != null ? BotDown(BotButton.Sprint) : SprintAction != null && SprintAction.WasPressedThisFrame();
        public bool SprintLatchPressedThisFrame => BotOverride != null ? BotDown(BotButton.SprintLatch) : SprintLatchAction != null && SprintLatchAction.WasPressedThisFrame();
        public bool CrouchPressedThisFrame => BotOverride != null ? BotDown(BotButton.Crouch) : CrouchAction != null && CrouchAction.WasPressedThisFrame();
        public bool FlashlightPressedThisFrame => BotOverride != null ? BotDown(BotButton.Flashlight) : FlashlightAction != null && FlashlightAction.WasPressedThisFrame();
        public bool InteractHeld => BotOverride != null ? BotHeld(BotButton.Interact) : InteractAction != null && InteractAction.IsPressed();
        public bool InteractPressedThisFrame => BotOverride != null ? BotDown(BotButton.Interact) : InteractAction != null && InteractAction.WasPressedThisFrame();
        public bool InteractReleasedThisFrame => BotOverride != null ? BotUp(BotButton.Interact) : InteractAction != null && InteractAction.WasReleasedThisFrame();
        public bool JournalPressedThisFrame => BotOverride != null ? BotDown(BotButton.Journal) : JournalAction != null && JournalAction.WasPressedThisFrame();
        public bool PausePressedThisFrame => BotOverride != null ? BotDown(BotButton.Pause) : PauseAction != null && PauseAction.WasPressedThisFrame();
        public bool SkipCutsceneHeld => BotOverride != null ? BotHeld(BotButton.Skip) : SkipCutsceneAction != null && SkipCutsceneAction.IsPressed();
        public bool CancelPressedThisFrame => BotOverride != null ? BotDown(BotButton.Cancel) : CancelAction != null && CancelAction.WasPressedThisFrame();

        public static CretaceaInput EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<CretaceaInput>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("[CretaceaInput]");
            Instance = go.AddComponent<CretaceaInput>();
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            BuildInputActions();
        }

        private void OnEnable()
        {
            GameplayMap?.Enable();
            InputSystem.onEvent += OnRawInputEvent;
            GameEvents.OnCameraTraumaRequested += OnTraumaRumble;
            GameEvents.OnPauseStateChanged += OnPauseChanged;
        }

        private void OnDisable()
        {
            InputSystem.onEvent -= OnRawInputEvent;
            GameEvents.OnCameraTraumaRequested -= OnTraumaRumble;
            GameEvents.OnPauseStateChanged -= OnPauseChanged;
            StopRumbleImmediate();
            GameplayMap?.Disable();
        }

        private void OnDestroy()
        {
            StopRumbleImmediate();
            if (ActionAsset != null)
            {
                Destroy(ActionAsset);
                ActionAsset = null;
            }
        }

        private void Update()
        {
            if (_rumbleTimer > 0f)
            {
                _rumbleTimer -= Time.unscaledDeltaTime;
                if (_rumbleTimer <= 0f)
                {
                    StopRumbleImmediate();
                }
                else
                {
                    float fade = Mathf.Clamp01(_rumbleTimer * 2.5f);
                    ApplyGamepadMotorSpeeds(_rumbleLow * fade, _rumbleHigh * fade);
                }
            }
        }

        private void BuildInputActions()
        {
            ActionAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            ActionAsset.name = "CretaceaRuntimeControls";
            GameplayMap = new InputActionMap("Gameplay");
            ActionAsset.AddActionMap(GameplayMap);

            // Move (WASD + Arrows + Gamepad Left Stick)
            MoveAction = GameplayMap.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            MoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            MoveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            MoveAction.AddBinding("<Gamepad>/leftStick");

            // Look (Separate Mouse Delta & Gamepad Right Stick so sensitivity scales cleanly)
            LookMouseAction = GameplayMap.AddAction("LookMouse", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            LookStickAction = GameplayMap.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");

            // Sprint (Hold Shift / LB) & Sprint Latch (L3 click)
            SprintAction = GameplayMap.AddAction("Sprint", InputActionType.Button);
            SprintAction.AddBinding("<Keyboard>/leftShift");
            SprintAction.AddBinding("<Gamepad>/leftShoulder");

            SprintLatchAction = GameplayMap.AddAction("SprintLatch", InputActionType.Button);
            SprintLatchAction.AddBinding("<Gamepad>/leftStickPress");

            // Crouch (C / Left Ctrl / Gamepad East B)
            CrouchAction = GameplayMap.AddAction("Crouch", InputActionType.Button);
            CrouchAction.AddBinding("<Keyboard>/c");
            CrouchAction.AddBinding("<Keyboard>/leftCtrl");
            CrouchAction.AddBinding("<Gamepad>/buttonEast");

            // Flashlight (F / Gamepad North Y)
            FlashlightAction = GameplayMap.AddAction("Flashlight", InputActionType.Button);
            FlashlightAction.AddBinding("<Keyboard>/f");
            FlashlightAction.AddBinding("<Gamepad>/buttonNorth");

            // Interact (E / Left Mouse optional / Gamepad West X)
            InteractAction = GameplayMap.AddAction("Interact", InputActionType.Button);
            InteractAction.AddBinding("<Keyboard>/e");
            InteractAction.AddBinding("<Gamepad>/buttonWest");

            // Tactical Journal / Map (Tab / Gamepad Select-View)
            JournalAction = GameplayMap.AddAction("Journal", InputActionType.Button);
            JournalAction.AddBinding("<Keyboard>/tab");
            JournalAction.AddBinding("<Gamepad>/select");

            // Pause (Escape / Gamepad Start-Menu)
            PauseAction = GameplayMap.AddAction("Pause", InputActionType.Button);
            PauseAction.AddBinding("<Keyboard>/escape");
            PauseAction.AddBinding("<Gamepad>/start");

            // Skip Cutscene Hold (Space / Gamepad West X)
            SkipCutsceneAction = GameplayMap.AddAction("SkipCutscene", InputActionType.Button);
            SkipCutsceneAction.AddBinding("<Keyboard>/space");
            SkipCutsceneAction.AddBinding("<Gamepad>/buttonWest");

            // Cancel / Close Document (Escape / E / Right Mouse / Gamepad East B)
            CancelAction = GameplayMap.AddAction("Cancel", InputActionType.Button);
            CancelAction.AddBinding("<Keyboard>/escape");
            CancelAction.AddBinding("<Gamepad>/buttonEast");

            GameplayMap.Enable();
        }

        private void OnRawInputEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (device == null) return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>()) return;

            if (device is Gamepad gp)
            {
                // Ignore tiny stick drift noise when detecting gamepad activation
                bool active = gp.leftStick.ReadValue().sqrMagnitude > 0.08f
                              || gp.rightStick.ReadValue().sqrMagnitude > 0.08f
                              || gp.buttonSouth.isPressed || gp.buttonWest.isPressed
                              || gp.buttonEast.isPressed || gp.buttonNorth.isPressed
                              || gp.startButton.isPressed || gp.selectButton.isPressed
                              || gp.leftShoulder.isPressed || gp.rightShoulder.isPressed;
                if (active) SetActiveDevice(InputDeviceType.Gamepad);
            }
            else if (device is Keyboard || device is Mouse)
            {
                if (device is Mouse m && m.delta.ReadValue().sqrMagnitude < 4f && !m.leftButton.isPressed && !m.rightButton.isPressed)
                    return;
                SetActiveDevice(InputDeviceType.KeyboardMouse);
            }
        }

        private void SetActiveDevice(InputDeviceType deviceType)
        {
            if (ActiveDevice == deviceType) return;
            ActiveDevice = deviceType;
            GameEvents.RaiseInputDeviceChanged(deviceType);
        }

        private void OnTraumaRumble(float trauma01, float decayRate)
        {
            if (ActiveDevice != InputDeviceType.Gamepad) return;
            if (!SaveManager.Settings.gamepadRumble) return;
            float clamped = Mathf.Clamp01(trauma01);
            TriggerRumble(clamped * 0.85f, clamped * 0.55f, Mathf.Clamp(clamped / Mathf.Max(0.4f, decayRate), 0.12f, 1.2f));
        }

        private void OnPauseChanged(bool paused)
        {
            if (paused) StopRumbleImmediate();
        }

        public void TriggerRumble(float lowFrequency, float highFrequency, float durationSeconds)
        {
            if (ActiveDevice != InputDeviceType.Gamepad) return;
            if (!SaveManager.Settings.gamepadRumble) return;

            _rumbleLow = Mathf.Max(_rumbleLow, Mathf.Clamp01(lowFrequency));
            _rumbleHigh = Mathf.Max(_rumbleHigh, Mathf.Clamp01(highFrequency));
            _rumbleTimer = Mathf.Max(_rumbleTimer, durationSeconds);
            ApplyGamepadMotorSpeeds(_rumbleLow, _rumbleHigh);
        }

        public void StopRumbleImmediate()
        {
            _rumbleLow = 0f;
            _rumbleHigh = 0f;
            _rumbleTimer = 0f;
            ApplyGamepadMotorSpeeds(0f, 0f);
        }

        private static void ApplyGamepadMotorSpeeds(float low, float high)
        {
            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                gp.SetMotorSpeeds(low, high);
            }
        }

        /// <summary>
        /// Replaces semantic prompt tokens ({interact}, {flashlight}, {crouch}, {sprint}, {journal}, {skip})
        /// with the active KB+M or Xbox glyph text.
        /// </summary>
        public string FormatPrompt(string template)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;
            bool pad = ActiveDevice == InputDeviceType.Gamepad;

            return template
                .Replace("{interact}", pad ? "[X]" : "[E]")
                .Replace("{flashlight}", pad ? "[Y]" : "[F]")
                .Replace("{crouch}", pad ? "[B]" : "[C]")
                .Replace("{sprint}", pad ? "[LB / L3]" : "[Shift]")
                .Replace("{journal}", pad ? "[View]" : "[Tab]")
                .Replace("{skip}", pad ? "[Hold X]" : "[Hold Space]");
        }
    }
}
