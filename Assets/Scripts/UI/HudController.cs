using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cretacea.Core;

namespace Cretacea.UI
{
    /// <summary>
    /// AAA Survival-Horror HUD Controller built deterministically via CretaceaUiFactory.
    /// Fixes ALL Godot UX bugs:
    /// - Persistent sleek top-left Objective Banner (never fades to invisible permanently; shows [TAB] Journal & Map)
    /// - Dynamic Stealth / Visibility Eye indicator reacting to crouch, tall grass, darkness, flashlight, and predator alert
    /// - Center Interaction Prompt + amber LockedHint prerequisite card + Radial Hold Ring + E-0 saved progress bar
    /// - Flashlight badge (strictly hidden until acquired!) + Master Key chip + auto-hiding Stamina Bar (<95%)
    /// - Bottom-safe Subtitles, Radio VO Banner & Accessibility Sound Captions cleanly hidden during Pause/Settings/Ending
    /// - Chapter Title / Timestamp Card, Letterbox Bars, Cutscene Skip Prompt, Danger Vignette, and Screen Fade!
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        public static HudController Instance { get; private set; }

        [Header("Optional Sprite Overrides (Auto-Generated / Resources Loaded if Empty)")]
        [SerializeField] private Sprite customEyeClosedSprite;
        [SerializeField] private Sprite customEyeHalfSprite;
        [SerializeField] private Sprite customEyeOpenSprite;
        [SerializeField] private Sprite customHoldRingSprite;
        [SerializeField] private Sprite customDamageVignetteSprite;

        private Canvas _canvas;
        private CanvasGroup _gameplayHudGroup;
        // Subtitles, sound captions and the stealth eye live on their own canvas above the field tablet (sort 30 >
        // journal 25): the world keeps running while the tablet is open, so they must stay readable.
        private Canvas _overlayCanvas;
        private CanvasGroup _overlayGroup;
        private bool _beamOn;
        private RectTransform _objectivePlate;

        // Top-left Persistent Objective Banner
        private CanvasGroup _objectiveGroup;
        private TextMeshProUGUI _objectiveText;
        private TextMeshProUGUI _journalHintText;
        private float _objectiveHighlightTimer;
        private bool _journalWasOpen;
        private const float ObjectiveShowSeconds = 9f;      // full, then muted, then gone
        private const float ObjectiveSettleSeconds = 4.5f;  // the last 4.5 s are muted (0.86)
        private const float ObjectiveReshowSeconds = 6f;
        private CanvasGroup _flashlightChipGroup;
        private CretaceaPanelGraphic _flashlightChipBox;
        private float _flashlightChipTimer;
        private float _flashlightPulse;
        private const float FlashlightPulseSeconds = 1.6f;
        private string _stealthState = string.Empty;
        private float _stealthStateTimer;
        private int _toastFrame = -1;
        private string _objectiveShownCopy = string.Empty;
        private float _objectiveLegTimer;

        private static Vector2 PlayerXZ()
        {
            Camera cam = Camera.main;
            if (cam == null) return new Vector2(-1000f, 0f);
            Vector3 p = cam.transform.position;
            return new Vector2(p.x, p.z);
        }

        // Dynamic Stealth Eye (top-center subtle)
        private CanvasGroup _stealthGroup;
        private Image _stealthEyeImage;
        private TextMeshProUGUI _stealthLabel;
        private float _latestVisibility = 0.5f;
        private bool _latestConcealed;
        private bool _predatorAlertActive;

        // Center Reticle, Interaction Prompt, LockedHint & Radial Hold Ring
        private Image _reticleDot;
        private Image _holdRingImage;
        private CanvasGroup _promptGroup;
        private Image _promptAccentBar;
        private TextMeshProUGUI _promptTitleText;
        private TextMeshProUGUI _promptSubHintText;
        private GameObject _e0ProgressRoot;
        private Image _e0ProgressFill;
        private TextMeshProUGUI _e0ProgressLabel;

        // Bottom-left Equipment & Stamina
        private GameObject _flashlightChip;
        private TextMeshProUGUI _flashlightChipText;
        private Image _flashlightChipDot;
        private Image _flashlightChipGlow;
        private GameObject _masterKeyChip;
        private CanvasGroup _masterKeyGroup;
        private float _masterKeyTimer;
        private bool _hintManualShown;
        private CanvasGroup _staminaGroup;
        private Image _staminaFill;

        // Subtitles, Toast & Sound Captions
        private CanvasGroup _subtitleGroup;
        private TextMeshProUGUI _subtitleSpeakerText;
        private TextMeshProUGUI _subtitleBodyText;
        private float _subtitleTimer;

        private CanvasGroup _toastGroup;
        private TextMeshProUGUI _toastText;
        private float _toastTimer;

        private CanvasGroup _captionGroup;
        private TextMeshProUGUI _captionText;
        private float _captionTimer;

        // Title Card, Letterbox, Skip, Damage Vignette & Fade
        private CanvasGroup _titleCardGroup;
        private TextMeshProUGUI _titleMainText;
        private TextMeshProUGUI _titleSubText;
        private Coroutine _titleCardRoutine;

        private RectTransform _letterboxTop;
        private RectTransform _letterboxBottom;
        private Coroutine _letterboxRoutine;

        private CanvasGroup _skipGroup;
        private TextMeshProUGUI _skipText;
        private Image _damageVignette;
        private float _damageFlashAlpha;
        private Image _screenFadeImage;
        private Coroutine _fadeRoutine;

        // Look-pass additions: sized boxes, keycaps, hold-ring track
        private RectTransform _promptBox;
        private CretaceaUiFactory.Keycap _promptKey;
        private CretaceaUiFactory.Keycap _flashlightKey;
        private Image _holdRingTrack;
        private RectTransform _toastBox;
        private RectTransform _captionBox;
        private RectTransform _subtitleBox;

        private bool _suppressedByModal;
        private bool _suppressedByCutscene;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildHudHierarchy();
        }

        private void OnEnable()
        {
            GameEvents.OnObjectiveChanged += OnObjectiveChanged;
            GameEvents.OnTitleCardRequested += OnTitleCardRequested;
            GameEvents.OnScreenFadeRequested += OnScreenFadeRequested;
            GameEvents.OnLetterboxRequested += OnLetterboxRequested;
            GameEvents.OnCutsceneStateChanged += OnCutsceneStateChanged;
            GameEvents.OnCutsceneSkipPromptChanged += OnCutsceneSkipPromptChanged;
            GameEvents.OnPauseStateChanged += OnPauseStateChanged;
            GameEvents.OnFlashlightAcquiredChanged += OnFlashlightAcquiredChanged;
            GameEvents.OnFlashlightToggled += OnFlashlightToggled;
            GameEvents.OnFlashlightPickedUp += OnFlashlightPickedUp;
            GameEvents.OnMasterKeyChanged += OnMasterKeyChanged;
            GameEvents.OnStealthVisibilityChanged += OnStealthVisibilityChanged;
            GameEvents.OnStaminaChanged += OnStaminaChanged;
            GameEvents.OnInteractionFocusChanged += OnInteractionFocusChanged;
            GameEvents.OnE0GateProgressChanged += OnE0GateProgressChanged;
            GameEvents.OnSubtitleRequested += OnSubtitleRequested;
            GameEvents.OnToastMessageRequested += OnToastMessageRequested;
            GameEvents.OnSoundCaptionRequested += OnSoundCaptionRequested;
            GameEvents.OnDamageFlashRequested += OnDamageFlashRequested;
            GameEvents.OnDinoStateChanged += OnDinoStateChanged;
            GameEvents.OnInputDeviceChanged += OnInputDeviceChanged;
            GameEvents.OnGameFinished += OnGameFinished;
        }

        private void OnDisable()
        {
            GameEvents.OnObjectiveChanged -= OnObjectiveChanged;
            GameEvents.OnTitleCardRequested -= OnTitleCardRequested;
            GameEvents.OnScreenFadeRequested -= OnScreenFadeRequested;
            GameEvents.OnLetterboxRequested -= OnLetterboxRequested;
            GameEvents.OnCutsceneStateChanged -= OnCutsceneStateChanged;
            GameEvents.OnCutsceneSkipPromptChanged -= OnCutsceneSkipPromptChanged;
            GameEvents.OnPauseStateChanged -= OnPauseStateChanged;
            GameEvents.OnFlashlightAcquiredChanged -= OnFlashlightAcquiredChanged;
            GameEvents.OnFlashlightToggled -= OnFlashlightToggled;
            GameEvents.OnFlashlightPickedUp -= OnFlashlightPickedUp;
            GameEvents.OnMasterKeyChanged -= OnMasterKeyChanged;
            GameEvents.OnStealthVisibilityChanged -= OnStealthVisibilityChanged;
            GameEvents.OnStaminaChanged -= OnStaminaChanged;
            GameEvents.OnInteractionFocusChanged -= OnInteractionFocusChanged;
            GameEvents.OnE0GateProgressChanged -= OnE0GateProgressChanged;
            GameEvents.OnSubtitleRequested -= OnSubtitleRequested;
            GameEvents.OnToastMessageRequested -= OnToastMessageRequested;
            GameEvents.OnSoundCaptionRequested -= OnSoundCaptionRequested;
            GameEvents.OnDamageFlashRequested -= OnDamageFlashRequested;
            GameEvents.OnDinoStateChanged -= OnDinoStateChanged;
            GameEvents.OnInputDeviceChanged -= OnInputDeviceChanged;
            GameEvents.OnGameFinished -= OnGameFinished;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            SyncInitialState();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // Objective banner: full for a few seconds when the objective changes, then it steps back and fades out
            // so dread can build on a clean screen. It comes back on a new objective and when the tablet closes.
            bool journalOpenNow = GameState.Instance != null && GameState.Instance.IsJournalOpen;
            if (_journalWasOpen && !journalOpenNow)
            {
                _objectiveHighlightTimer = Mathf.Max(_objectiveHighlightTimer, ObjectiveReshowSeconds);
                _masterKeyTimer = Mathf.Max(_masterKeyTimer, 4f);
            }
            _journalWasOpen = journalOpenNow;
            if (_objectiveGroup != null && !string.IsNullOrEmpty(_objectiveText.text))
            {
                if (_objectiveHighlightTimer > 0f) _objectiveHighlightTimer -= dt;
                float targetAlpha = _objectiveHighlightTimer > ObjectiveSettleSeconds ? 1.0f : (_objectiveHighlightTimer > 0f ? 0.86f : 0f);
                _objectiveGroup.alpha = Mathf.MoveTowards(_objectiveGroup.alpha, targetAlpha, dt * (targetAlpha > 0f ? 1.4f : 0.7f));
            }

            // Long route objectives advance leg by leg with the player: re-check twice a second, re-show on a change.
            _objectiveLegTimer -= dt;
            if (_objectiveLegTimer <= 0f && GameState.Instance != null)
            {
                _objectiveLegTimer = 0.5f;
                string cur = GameState.Instance.CurrentObjective;
                if (!string.IsNullOrEmpty(cur) && ObjectiveGuide.Display(cur, PlayerXZ()) != _objectiveShownCopy) OnObjectiveChanged(cur);
                if (GameState.Instance.GetFlag("manual_read") != _hintManualShown) RefreshJournalHintText();
            }

            // Flashlight chip: shown on pickup and for a few seconds after each toggle, then it fades away.
            if (_flashlightChipGroup != null)
            {
                if (_flashlightChipTimer > 0f) _flashlightChipTimer -= dt;
                float chipTarget = _flashlightChipTimer > 0f ? 1f : 0f;
                _flashlightChipGroup.alpha = Mathf.MoveTowards(_flashlightChipGroup.alpha, chipTarget, dt * (chipTarget > 0f ? 4f : 0.8f));
                if (_flashlightPulse > 0f && _flashlightChipBox != null)
                {
                    _flashlightPulse = Mathf.Max(0f, _flashlightPulse - dt);
                    float k = Mathf.Sin(Mathf.Clamp01(1f - _flashlightPulse / FlashlightPulseSeconds) * Mathf.PI * 3f);
                    _flashlightChipBox.SetBorder(Color.Lerp(CretaceaUiFactory.BorderSubtle, CretaceaUiFactory.AccentAmber, Mathf.Abs(k)), 1f + Mathf.Abs(k) * 1.5f);
                    if (_flashlightPulse <= 0f) _flashlightChipBox.SetBorder(CretaceaUiFactory.BorderSubtle, 1f);
                }
            }

            // Master key chip: shown when the key is taken, at a locked door and when the tablet closes, then it fades.
            if (_masterKeyGroup != null)
            {
                if (_masterKeyTimer > 0f) _masterKeyTimer -= dt;
                float keyTarget = _masterKeyTimer > 0f ? 1f : 0f;
                _masterKeyGroup.alpha = Mathf.MoveTowards(_masterKeyGroup.alpha, keyTarget, dt * (keyTarget > 0f ? 4f : 0.8f));
            }

            // Subtitle timer
            if (_subtitleTimer > 0f)
            {
                _subtitleTimer -= dt;
                if (_subtitleTimer <= 0f) FadeTo(_subtitleGroup, 0f);
            }

            // Toast timer
            if (_toastTimer > 0f)
            {
                _toastTimer -= dt;
                if (_toastTimer <= 0f) FadeTo(_toastGroup, 0f);
            }

            // Sound caption timer
            if (_captionTimer > 0f)
            {
                _captionTimer -= dt;
                if (_captionTimer <= 0f) FadeTo(_captionGroup, 0f);
            }

            // Damage flash decay
            if (_damageVignette != null && _damageFlashAlpha > 0.001f)
            {
                _damageFlashAlpha = Mathf.MoveTowards(_damageFlashAlpha, 0f, dt * 0.9f);
                Color c = _damageVignette.color;
                c.a = _damageFlashAlpha;
                _damageVignette.color = c;
            }

            // P1-5 FIX: Guarantee Title Card never bleeds behind Pause/Settings/Ending modals!
            if (_titleCardGroup != null && (_suppressedByModal || (GameState.Instance != null && GameState.Instance.IsMainMenu)))
            {
                _titleCardGroup.alpha = 0f;
            }

            // The field tablet covers the screen: fade the gameplay HUD under it instead of showing through.
            if (_gameplayHudGroup != null && !_suppressedByModal && !_suppressedByCutscene && GameState.Instance != null && !GameState.Instance.IsMainMenu)
            {
                float hudTarget = GameState.Instance.IsJournalOpen ? 0f : 1f;
                _gameplayHudGroup.alpha = Mathf.MoveTowards(_gameplayHudGroup.alpha, hudTarget, dt / FadeSeconds);
            }

            ApplyJournalLayout(GameState.Instance != null && GameState.Instance.IsJournalOpen);
            TickFades(dt);
            UpdateStealthEyeVisual();
        }

        /// <summary>Hides gameplay HUD, subtitles, and timestamp title cards cleanly whenever Pause/Settings/Ending is open.</summary>
        public void SetModalSuppressed(bool suppressed)
        {
            _suppressedByModal = suppressed;
            RefreshHudLayerVisibility();
        }

        private void RefreshHudLayerVisibility()
        {
            GameState gs = GameState.Instance;
            bool inMenu = gs != null && gs.IsMainMenu;
            bool hideGameplay = inMenu || _suppressedByModal || _suppressedByCutscene;

            if (_gameplayHudGroup != null)
            {
                _gameplayHudGroup.alpha = hideGameplay ? 0f : 1f;
                _gameplayHudGroup.blocksRaycasts = false;
            }
            // Subtitles and captions stay up in cutscenes (dialogue) and under the field tablet; only menus,
            // pause, settings, documents and the ending hide them. The stealth eye hides itself in cutscenes.
            if (_overlayGroup != null)
            {
                _overlayGroup.alpha = (inMenu || _suppressedByModal) ? 0f : 1f;
                _overlayGroup.blocksRaycasts = false;
            }

            // P1-5 FIX: Stop any active TitleCard fade coroutine immediately so "23:05" NEVER bleeds behind Pause/Settings/Ending!
            if (_suppressedByModal || inMenu)
            {
                if (_titleCardRoutine != null)
                {
                    StopCoroutine(_titleCardRoutine);
                    _titleCardRoutine = null;
                }
                if (_titleCardGroup != null)
                {
                    _titleCardGroup.alpha = 0f;
                }
            }
        }

        private void SyncInitialState()
        {
            GameState gs = GameState.Instance;
            if (gs == null) return;
            OnFlashlightAcquiredChanged(gs.HasFlashlight);
            OnMasterKeyChanged(gs.HasMasterKey);
            if (!string.IsNullOrEmpty(gs.CurrentObjective)) OnObjectiveChanged(gs.CurrentObjective);
            RefreshJournalHintText();
            RefreshHudLayerVisibility();
        }

        // =========================================================================
        // DETERMINISTIC HUD CONSTRUCTION
        // =========================================================================

        private void BuildHudHierarchy()
        {
            _canvas = CretaceaUiFactory.CreateRootCanvas("[HUDCanvas]", sortOrder: 10);
            _canvas.transform.SetParent(transform, false);
            RectTransform root = _canvas.GetComponent<RectTransform>();

            // Full-screen Blood/Danger Vignette (behind HUD widgets)
            // The damage_red art carries its own blood red; tint white so it reads at night instead of near-black.
            RectTransform vigRt = CretaceaUiFactory.CreatePanel(root, "DamageVignette", new Color(1f, 0.92f, 0.9f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _damageVignette = vigRt.GetComponent<Image>();
            _damageVignette.sprite = customDamageVignetteSprite != null ? customDamageVignetteSprite : CretaceaUiFactory.GetVignetteSprite();

            // Gameplay HUD Container
            var hudGo = new GameObject("GameplayHUD", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform hudRt = hudGo.GetComponent<RectTransform>();
            hudRt.SetParent(root, false);
            hudRt.anchorMin = Vector2.zero;
            hudRt.anchorMax = Vector2.one;
            hudRt.offsetMin = Vector2.zero;
            hudRt.offsetMax = Vector2.zero;
            _gameplayHudGroup = hudGo.GetComponent<CanvasGroup>();
            _gameplayHudGroup.interactable = false;
            _gameplayHudGroup.blocksRaycasts = false;

            BuildObjectiveBanner(hudRt);
            BuildCenterReticleAndPrompt(hudRt);
            BuildBottomLeftStatusStrip(hudRt);
            BuildToast(hudRt);

            // Overlay canvas: subtitles, captions, stealth eye (not hidden by the field tablet)
            _overlayCanvas = CretaceaUiFactory.CreateRootCanvas("[HUDOverlay]", sortOrder: 30);
            _overlayCanvas.transform.SetParent(transform, false);
            var ovGo = new GameObject("OverlayHUD", typeof(RectTransform), typeof(CanvasGroup));
            RectTransform ovRt = ovGo.GetComponent<RectTransform>();
            ovRt.SetParent(_overlayCanvas.transform, false);
            ovRt.anchorMin = Vector2.zero;
            ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = Vector2.zero;
            ovRt.offsetMax = Vector2.zero;
            _overlayGroup = ovGo.GetComponent<CanvasGroup>();
            _overlayGroup.interactable = false;
            _overlayGroup.blocksRaycasts = false;
            BuildStealthEyeIndicator(ovRt);
            BuildSubtitlesAndCaptions(ovRt);

            // Cutscene & Transition Overlays (above Gameplay HUD)
            BuildLetterboxBars(root);
            BuildTitleCard(root);
            BuildSkipPrompt(root);

            // Full-screen Fade Curtain (top of HUD canvas)
            RectTransform fadeRt = CretaceaUiFactory.CreatePanel(root, "ScreenFade", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _screenFadeImage = fadeRt.GetComponent<Image>();
        }

        private void BuildObjectiveBanner(RectTransform parent)
        {
            // The project's objective bar art (dark gradient with an amber edge), not a slab.
            RectTransform box = CretaceaUiFactory.CreateAnchoredPanel(parent, "ObjectiveBanner", Color.clear,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -32f), new Vector2(660f, 132f));
            // A soft card sized to the text sits under the bar art, so a long objective line never ends up on the
            // bare world where the art fades out (bright GoldenDay sky, lamps).
            _objectivePlate = CretaceaUiFactory.CreatePanel(box, "Plate", new Color(CretaceaUiFactory.PanelBg.r, CretaceaUiFactory.PanelBg.g, CretaceaUiFactory.PanelBg.b, 0.6f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(660f, 0f));
            CretaceaUiFactory.StyleCard(_objectivePlate, 5f, 0.45f, new Color(CretaceaUiFactory.BorderSubtle.r, CretaceaUiFactory.BorderSubtle.g, CretaceaUiFactory.BorderSubtle.b, 0.18f));
            RectTransform art = CretaceaUiFactory.CreatePanel(box, "BarArt", new Color(1f, 1f, 1f, 0.97f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sprite bar = CretaceaUiFactory.LoadSprite("UI/HUD/objective_bar");
            if (bar != null) art.GetComponent<Image>().sprite = bar;
            else art.GetComponent<Image>().color = Color.clear;
            _objectiveGroup = box.gameObject.AddComponent<CanvasGroup>();
            _objectiveGroup.alpha = 0f;

            TextMeshProUGUI header = CretaceaUiFactory.CreateText(box, "Header", "CURRENT OBJECTIVE", 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            header.characterSpacing = 4f;
            header.rectTransform.offsetMin = new Vector2(26f, 0f);
            header.rectTransform.offsetMax = new Vector2(-20f, -14f);

            _objectiveText = CretaceaUiFactory.CreateText(box, "Body", string.Empty, 24f, CretaceaUiFactory.TextPrimary, UiFont.BodyMedium, TextAlignmentOptions.TopLeft);
            _objectiveText.rectTransform.offsetMin = new Vector2(26f, 30f);
            _objectiveText.rectTransform.offsetMax = new Vector2(-40f, -40f);
            _objectiveText.lineSpacing = 0f;

            _journalHintText = CretaceaUiFactory.CreateText(box, "JournalChip", "[Tab] Journal & Route Map", 21f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.BottomLeft);
            _journalHintText.characterSpacing = 1f;
            _journalHintText.rectTransform.offsetMin = new Vector2(26f, HintBottomPad);   // the inline keycap hangs below the line: even air under it
            _journalHintText.rectTransform.offsetMax = new Vector2(-20f, -96f);
        }

        private void BuildStealthEyeIndicator(RectTransform parent)
        {
            RectTransform host = CretaceaUiFactory.CreateAnchoredPanel(parent, "StealthEye", CretaceaUiFactory.PanelBg,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(176f, 82f));
            CretaceaUiFactory.StyleCard(host, 6f, 0.5f);
            _stealthGroup = host.gameObject.AddComponent<CanvasGroup>();
            _stealthGroup.alpha = 0f;

            RectTransform eyeRt = CretaceaUiFactory.CreateAnchoredPanel(host, "EyeIcon", Color.white,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(76f, 38f));
            _stealthEyeImage = eyeRt.GetComponent<Image>();
            _stealthEyeImage.sprite = customEyeHalfSprite != null ? customEyeHalfSprite : CretaceaUiFactory.GetStealthEyeSprite(1);
            _stealthEyeImage.preserveAspect = true;

            _stealthLabel = CretaceaUiFactory.CreateText(host, "StealthLabel", "SHADOWED", 18f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.Bottom);
            _stealthLabel.characterSpacing = 3f;
            _stealthLabel.rectTransform.offsetMin = new Vector2(4f, 8f);
            _stealthLabel.rectTransform.offsetMax = new Vector2(-4f, -46f);
        }

        private void BuildCenterReticleAndPrompt(RectTransform parent)
        {
            // Reticle: a warm off-white dot inside a 2 px dark ring, so it reads on lit glass and on black alike.
            RectTransform ringBackRt = CretaceaUiFactory.CreateAnchoredPanel(parent, "ReticleRing", new Color(0f, 0f, 0f, 0.55f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(11f, 11f));
            ringBackRt.GetComponent<Image>().sprite = CretaceaUiFactory.GetCircleSprite();
            RectTransform dotRt = CretaceaUiFactory.CreateAnchoredPanel(parent, "ReticleDot", new Color(1f, 0.97f, 0.9f, 0.92f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7f, 7f));
            _reticleDot = dotRt.GetComponent<Image>();
            _reticleDot.sprite = CretaceaUiFactory.GetCircleSprite();

            // Hold ring: faint track + amber radial fill
            RectTransform trackRt = CretaceaUiFactory.CreateAnchoredPanel(parent, "HoldRingTrack", new Color(1f, 0.95f, 0.85f, 0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            _holdRingTrack = trackRt.GetComponent<Image>();
            _holdRingTrack.sprite = CretaceaUiFactory.GetRadialRingSprite();

            RectTransform ringRt = CretaceaUiFactory.CreateAnchoredPanel(parent, "HoldRing", CretaceaUiFactory.AccentAmber,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(62f, 62f));
            _holdRingImage = ringRt.GetComponent<Image>();
            _holdRingImage.sprite = CretaceaUiFactory.GetRadialRingSprite();
            _holdRingImage.type = Image.Type.Filled;
            _holdRingImage.fillMethod = Image.FillMethod.Radial360;
            _holdRingImage.fillOrigin = (int)Image.Origin360.Top;
            _holdRingImage.fillClockwise = true;
            _holdRingImage.fillAmount = 0f;

            // Interaction prompt: [keycap] action text, optional second line (hold time / why it is locked)
            RectTransform promptBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "InteractPrompt", CretaceaUiFactory.PanelBg,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PromptY), new Vector2(520f, 68f));
            CretaceaUiFactory.StyleCard(promptBox, 5f, 0.6f);
            _promptBox = promptBox;
            _promptGroup = promptBox.gameObject.AddComponent<CanvasGroup>();
            _promptGroup.alpha = 0f;

            RectTransform accentRt = CretaceaUiFactory.CreatePanel(promptBox, "Accent", CretaceaUiFactory.AccentAmber,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(14f, 0f), new Vector2(-14f, 2f));
            _promptAccentBar = accentRt.GetComponent<Image>();

            _promptKey = CretaceaUiFactory.CreateKeycap(promptBox, "Key", 42f);
            _promptKey.Root.anchorMin = _promptKey.Root.anchorMax = new Vector2(0f, 0.5f);
            _promptKey.Root.pivot = new Vector2(0f, 0.5f);
            _promptKey.Root.anchoredPosition = new Vector2(14f, 0f);

            _promptTitleText = CretaceaUiFactory.CreateText(promptBox, "PromptTitle", string.Empty, 26f, CretaceaUiFactory.TextPrimary, UiFont.BodySemiBold, TextAlignmentOptions.MidlineLeft);
            _promptTitleText.textWrappingMode = TextWrappingModes.NoWrap;

            _promptSubHintText = CretaceaUiFactory.CreateText(promptBox, "PromptHint", string.Empty, 20f, CretaceaUiFactory.AccentAmber, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            _promptSubHintText.characterSpacing = 1.5f;
            _promptSubHintText.textWrappingMode = TextWrappingModes.NoWrap;

            // E-0 gate progress (green = the escape route)
            RectTransform e0Box = CretaceaUiFactory.CreateAnchoredPanel(parent, "E0Progress", CretaceaUiFactory.PanelBg,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, E0BoxY), new Vector2(500f, 56f));
            CretaceaUiFactory.StyleCard(e0Box, 5f, 0.5f);
            _e0ProgressRoot = e0Box.gameObject;
            RectTransform e0Track = CretaceaUiFactory.CreatePanel(e0Box, "Track", CretaceaUiFactory.TrackColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(18f, 12f), new Vector2(-18f, 20f));
            CretaceaUiFactory.StyleRounded(e0Track, 4f);
            RectTransform e0FillRt = CretaceaUiFactory.CreatePanel(e0Track, "Fill", CretaceaUiFactory.AccentGreen,
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleRounded(e0FillRt, 4f);
            _e0ProgressFill = e0FillRt.GetComponent<Image>();
            _e0ProgressLabel = CretaceaUiFactory.CreateText(e0Box, "Label", "E-0 GATE  0%  -  PROGRESS SAVED", 19f, CretaceaUiFactory.TextPrimary, UiFont.Label, TextAlignmentOptions.Top);
            _e0ProgressLabel.characterSpacing = 1.5f;
            _e0ProgressLabel.rectTransform.offsetMin = new Vector2(18f, 22f);
            _e0ProgressLabel.rectTransform.offsetMax = new Vector2(-18f, -6f);
            _e0ProgressRoot.SetActive(false);
        }

        private void BuildBottomLeftStatusStrip(RectTransform parent)
        {
            // Flashlight chip (hidden until the flashlight is picked up)
            RectTransform flBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "FlashlightChip", CretaceaUiFactory.PanelBg,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 74f), new Vector2(240f, 50f));
            CretaceaUiFactory.StyleCard(flBox, 5f, 0.5f);
            _flashlightChip = flBox.gameObject;
            _flashlightChipBox = flBox.GetComponent<CretaceaPanelGraphic>();
            _flashlightChipGroup = flBox.gameObject.AddComponent<CanvasGroup>();
            _flashlightChipGroup.alpha = 0f;

            _flashlightKey = CretaceaUiFactory.CreateKeycap(flBox, "Key", 36f);
            _flashlightKey.Root.anchorMin = _flashlightKey.Root.anchorMax = new Vector2(0f, 0.5f);
            _flashlightKey.Root.pivot = new Vector2(0f, 0.5f);
            _flashlightKey.Root.anchoredPosition = new Vector2(10f, 0f);

            // State lamp: a lit amber dot with a soft halo when the beam is on, a hollow grey ring when it is off.
            // Glow and dot share one centre (review 3: both were right-aligned, an off-centre "eclipse"); the glow is a
            // radial falloff, so it reads as light around the lamp, not as a second disc.
            RectTransform flGlow = CretaceaUiFactory.CreateAnchoredPanel(flBox, "Glow", new Color(0.96f, 0.69f, 0.28f, 0.55f),
                new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(34f, 34f));
            _flashlightChipGlow = flGlow.GetComponent<Image>();
            _flashlightChipGlow.sprite = CretaceaUiFactory.GetGlowSprite();
            _flashlightChipGlow.raycastTarget = false;
            RectTransform flDot = CretaceaUiFactory.CreateAnchoredPanel(flBox, "Dot", CretaceaUiFactory.AccentAmber,
                new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, 0f), new Vector2(11f, 11f));
            _flashlightChipDot = flDot.GetComponent<Image>();
            _flashlightChipDot.sprite = CretaceaUiFactory.GetCircleSprite();

            _flashlightChipText = CretaceaUiFactory.CreateText(flBox, "Text", "FLASHLIGHT", 21f, CretaceaUiFactory.TextPrimary, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            _flashlightChipText.characterSpacing = 2f;
            _flashlightChipText.rectTransform.offsetMin = new Vector2(56f, 2f);
            _flashlightChipText.rectTransform.offsetMax = new Vector2(-44f, -2f);
            _flashlightChip.SetActive(false);

            // Master key chip
            RectTransform keyBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "MasterKeyChip", CretaceaUiFactory.PanelBg,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 132f), new Vector2(188f, 50f));
            CretaceaUiFactory.StyleCard(keyBox, 5f, 0.5f);
            _masterKeyChip = keyBox.gameObject;
            _masterKeyGroup = keyBox.gameObject.AddComponent<CanvasGroup>();
            _masterKeyGroup.alpha = 0f;
            RectTransform keyDot = CretaceaUiFactory.CreateAnchoredPanel(keyBox, "Dot", CretaceaUiFactory.AccentAmber,
                new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(22f, 0f), new Vector2(10f, 10f));
            keyDot.localRotation = Quaternion.Euler(0f, 0f, 45f);
            TextMeshProUGUI keyTxt = CretaceaUiFactory.CreateText(keyBox, "Text", "MASTER KEY", 21f, CretaceaUiFactory.TextPrimary, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            keyTxt.characterSpacing = 2f;
            keyTxt.rectTransform.offsetMin = new Vector2(40f, 2f);
            keyTxt.rectTransform.offsetMax = new Vector2(-10f, -2f);
            keyBox.sizeDelta = new Vector2(Mathf.Max(212f, Mathf.Ceil(40f + keyTxt.GetPreferredValues("MASTER KEY").x + 28f)), 50f);
            _masterKeyChip.SetActive(false);

            // Stamina (auto-hides at >= 95 %)
            RectTransform stamBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "StaminaBar", new Color(0.02f, 0.018f, 0.016f, 0.7f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 46f), new Vector2(300f, 12f));
            CretaceaUiFactory.StyleCard(stamBox, 6f, 0.45f, new Color(0f, 0f, 0f, 0.5f));
            _staminaGroup = stamBox.gameObject.AddComponent<CanvasGroup>();
            _staminaGroup.alpha = 0f;

            RectTransform stamFillRt = CretaceaUiFactory.CreatePanel(stamBox, "Fill", StaminaColor,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(2f, 2f), new Vector2(-2f, -2f));
            CretaceaUiFactory.StyleRounded(stamFillRt, 4f);
            _staminaFill = stamFillRt.GetComponent<Image>();
        }

        private void BuildToast(RectTransform parent)
        {
            // Toast (top right)
            RectTransform toastBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "ToastBanner", CretaceaUiFactory.PanelBg,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -32f), new Vector2(560f, 66f));
            CretaceaUiFactory.StyleCard(toastBox, 5f, 0.55f);
            _toastBox = toastBox;
            _toastGroup = toastBox.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            RectTransform toastBar = CretaceaUiFactory.CreatePanel(toastBox, "RightAccent", CretaceaUiFactory.AccentAmber,
                new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-3f, 12f), new Vector2(0f, -12f));
            CretaceaUiFactory.StyleRounded(toastBar, 1.5f);
            // Toasts share the prompt style: Barlow Condensed caps with the keycap first ("[F]  TOGGLE FLASHLIGHT").
            _toastText = CretaceaUiFactory.CreateText(toastBox, "ToastText", string.Empty, 24f, CretaceaUiFactory.TextPrimary, UiFont.Label, TextAlignmentOptions.MidlineRight);
            _toastText.fontStyle = FontStyles.UpperCase;
            _toastText.characterSpacing = 1.5f;
            _toastText.rectTransform.offsetMin = new Vector2(20f, 6f);
            _toastText.rectTransform.offsetMax = new Vector2(-22f, -6f);
        }

        private void BuildSubtitlesAndCaptions(RectTransform parent)
        {
            // Sound caption (above subtitles)
            RectTransform capBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "SoundCaption", new Color(0f, 0f, 0f, 0.62f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, CaptionY), new Vector2(560f, 42f));
            CretaceaUiFactory.StyleCard(capBox, 5f, 0.35f, new Color(0f, 0f, 0f, 0f));
            _captionBox = capBox;
            _captionGroup = capBox.gameObject.AddComponent<CanvasGroup>();
            _captionGroup.alpha = 0f;
            _captionText = CretaceaUiFactory.CreateText(capBox, "CaptionText", string.Empty, 22f, new Color(0.90f, 0.80f, 0.62f, 1f), UiFont.Body, TextAlignmentOptions.Center);
            _captionText.fontStyle = FontStyles.Italic;
            _captionText.textWrappingMode = TextWrappingModes.NoWrap;

            // Subtitles / radio (bottom, never under the letterbox)
            RectTransform subBox = CretaceaUiFactory.CreateAnchoredPanel(parent, "SubtitleBanner", new Color(0f, 0f, 0f, 0.62f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, SubtitleY), new Vector2(SubtitleMaxW, 124f));
            subBox.GetComponent<Image>().color = SubtitleBg;
            CretaceaUiFactory.StyleCard(subBox, 6f, 0.4f, new Color(0f, 0f, 0f, 0f));
            _subtitleBox = subBox;
            _subtitleGroup = subBox.gameObject.AddComponent<CanvasGroup>();
            _subtitleGroup.alpha = 0f;

            _subtitleSpeakerText = CretaceaUiFactory.CreateText(subBox, "Speaker", string.Empty, 20f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.Top);
            _subtitleSpeakerText.characterSpacing = 3.5f;
            _subtitleSpeakerText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _subtitleSpeakerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _subtitleSpeakerText.rectTransform.offsetMin = new Vector2(28f, -38f);
            _subtitleSpeakerText.rectTransform.offsetMax = new Vector2(-28f, -12f);

            _subtitleBodyText = CretaceaUiFactory.CreateText(subBox, "Body", string.Empty, 28f, CretaceaUiFactory.TextPrimary, UiFont.BodyMedium, TextAlignmentOptions.Top);
            _subtitleBodyText.lineSpacing = 0f;
            _subtitleBodyText.rectTransform.offsetMin = new Vector2(32f, 14f);
            _subtitleBodyText.rectTransform.offsetMax = new Vector2(-32f, -40f);
        }

        private void BuildLetterboxBars(RectTransform root)
        {
            _letterboxTop = CretaceaUiFactory.CreatePanel(root, "LetterboxTop", Color.black,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _letterboxBottom = CretaceaUiFactory.CreatePanel(root, "LetterboxBottom", Color.black,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f));
        }

        // Title card: one lower-left block for every card (park title, time stamps, chapter names), above the letterbox
        // and the HUD chips, never across the middle of the frame where every shot keeps its subject (review 2, intro_19).
        // A dark rounded plate with an amber edge bar sized to the text, a condensed white heading, an amber rule and an
        // amber mono line. Reference pixels (1920x1080 canvas).
        private const float TitleCardLeft = 104f;
        private const float TitleCardBottom = 212f;
        private const float TitleCardPadX = 34f;
        private RectTransform _titleCardRect;
        private RectTransform _titlePlate;
        private RectTransform _titleRule;

        private void BuildTitleCard(RectTransform root)
        {
            RectTransform card = CretaceaUiFactory.CreateAnchoredPanel(root, "ChapterTitleCard", Color.clear,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(TitleCardLeft, TitleCardBottom), new Vector2(980f, 150f));
            _titleCardRect = card;
            _titleCardGroup = card.gameObject.AddComponent<CanvasGroup>();
            _titleCardGroup.alpha = 0f;
            _titleCardGroup.blocksRaycasts = false;

            // Legible plate: the HUD's dark card (rounded, warm 1 px border, soft shadow) with an amber edge bar
            _titlePlate = CretaceaUiFactory.CreateAnchoredPanel(card, "Plate", new Color(0.030f, 0.027f, 0.024f, 0.78f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(600f, 150f));
            CretaceaUiFactory.StyleCard(_titlePlate, 6f, 0.5f);
            RectTransform bar = CretaceaUiFactory.CreateAnchoredPanel(_titlePlate, "EdgeBar", CretaceaUiFactory.AccentAmber,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(4f, 110f));
            bar.anchorMin = new Vector2(0f, 0f); bar.anchorMax = new Vector2(0f, 1f);
            bar.offsetMin = new Vector2(0f, 14f); bar.offsetMax = new Vector2(4f, -14f);

            _titleMainText = CretaceaUiFactory.CreateText(card, "MainTitle", string.Empty, 56f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.BottomLeft);
            _titleMainText.characterSpacing = 6f;
            // One line always: long chapter names shrink a little instead of wrapping out of the plate.
            _titleMainText.textWrappingMode = TextWrappingModes.NoWrap;
            _titleMainText.enableAutoSizing = true;
            _titleMainText.fontSizeMin = 34f;
            _titleMainText.fontSizeMax = 56f;
            SetTitleRect(_titleMainText.rectTransform, 0f, 0f);

            _titleRule = CretaceaUiFactory.CreateAnchoredPanel(card, "Rule", new Color(CretaceaUiFactory.AccentAmber.r, CretaceaUiFactory.AccentAmber.g, CretaceaUiFactory.AccentAmber.b, 0.8f),
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(TitleCardPadX, 0f), new Vector2(96f, 2f));
            _titleRule.name = "Rule";

            _titleSubText = CretaceaUiFactory.CreateText(card, "SubTitle", string.Empty, 26f, CretaceaUiFactory.AccentAmber, UiFont.Mono, TextAlignmentOptions.BottomLeft);
            _titleSubText.characterSpacing = 2f;
            _titleSubText.textWrappingMode = TextWrappingModes.NoWrap;
            SetTitleRect(_titleSubText.rectTransform, 0f, 0f);
        }

        private static void SetTitleRect(RectTransform rt, float bottom, float height)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.offsetMin = new Vector2(TitleCardPadX, bottom);
            rt.offsetMax = new Vector2(-TitleCardPadX, bottom + height);
        }

        /// <summary>Stacks heading / rule / mono line from the bottom up and sizes the plate to the text. The text column
        /// is at most <see cref="TitleTextMaxW"/> wide (the heading shrinks, the mono line wraps to a second line), so the
        /// card's right edge stays left of the centred sound-caption box.</summary>
        private const float TitleTextMaxW = 560f;

        private void LayoutTitleCard(bool hasTitle, bool hasSub)
        {
            const float pad = 18f, headH = 66f, gap = 14f, subLineH = 32f;
            _titleMainText.gameObject.SetActive(hasTitle);
            _titleSubText.gameObject.SetActive(hasSub);
            _titleRule.gameObject.SetActive(hasTitle && hasSub);

            float w = 0f;
            if (hasTitle)
            {
                // measured at fontSizeMax (auto-size); wider headings shrink into the column, down to fontSizeMin
                Vector2 pref = _titleMainText.GetPreferredValues(_titleMainText.text, 4000f, headH);
                w = Mathf.Max(w, Mathf.Min(pref.x, TitleTextMaxW));
            }
            float subH = 0f;
            if (hasSub)
            {
                _titleSubText.textWrappingMode = TextWrappingModes.NoWrap;
                float one = _titleSubText.GetPreferredValues(_titleSubText.text, 4000f, subLineH).x;
                if (one > TitleTextMaxW && _titleSubText.text.Contains(" \u2014 "))
                {
                    // break at the dash ("Night Shift" / "Cretacea Park Visitor Center"), never mid-phrase
                    _titleSubText.text = _titleSubText.text.Replace(" \u2014 ", "\n");
                    one = _titleSubText.GetPreferredValues(_titleSubText.text, 4000f, 400f).x;
                }
                bool wraps = one > TitleTextMaxW;
                w = Mathf.Max(w, Mathf.Min(one, TitleTextMaxW));
                _titleSubText.textWrappingMode = wraps ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                subH = Mathf.Ceil(_titleSubText.GetPreferredValues(_titleSubText.text, Mathf.Min(one, TitleTextMaxW) + 1f, 400f).y) + 4f;
                subH = Mathf.Max(subH, subLineH + 2f);
            }
            w = Mathf.Ceil(w) + 4f;

            float y = pad;
            if (hasSub) { SetTitleRect(_titleSubText.rectTransform, y, subH); y += subH; }
            if (hasSub && hasTitle) { y += gap * 0.5f; _titleRule.anchoredPosition = new Vector2(TitleCardPadX, y); y += 2f + gap * 0.5f + 4f; }
            if (hasTitle) { SetTitleRect(_titleMainText.rectTransform, y, headH); y += headH; }
            y += pad - 6f;

            float plateW = Mathf.Max(w + TitleCardPadX * 2f, 240f);
            _titleCardRect.sizeDelta = new Vector2(plateW, y);
            _titlePlate.sizeDelta = new Vector2(plateW, y);
        }

        private void BuildSkipPrompt(RectTransform root)
        {
            RectTransform skipBox = CretaceaUiFactory.CreateAnchoredPanel(root, "CutsceneSkip", CretaceaUiFactory.PanelBg,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(300f, 50f));
            CretaceaUiFactory.StyleCard(skipBox, 5f, 0.45f);
            _skipGroup = skipBox.gameObject.AddComponent<CanvasGroup>();
            _skipGroup.alpha = 0f;
            _skipText = CretaceaUiFactory.CreateText(skipBox, "SkipText", CretaceaUiFactory.FormatKeyTokens("[Hold Space] Skip Intro"), 21f, CretaceaUiFactory.TextPrimary, UiFont.Label, TextAlignmentOptions.Center);
            _skipText.characterSpacing = 1.5f;
        }

        // =========================================================================
        // FADES & LAYOUT HELPERS
        // =========================================================================

        private const float FadeSeconds = 0.15f;

        // Lower-third stack (1080p reference px from the bottom edge). Worst case, all at once: a 2-line subtitle
        // with speaker (62..204), the caption pill (214..256), a 2-line prompt (276..368) and the E-0 bar (380..436),
        // each at least 10 px apart; the equipment chips sit at the far left (x 34..246), clear of the centred
        // subtitle box (max 1060 wide: left edge >= 430).
        private const float SubtitleY = 62f;
        private const float SubtitleMaxW = 860f;    // about 45-55 characters a line at 28 px
        private const float SubtitleWrapW = 780f;
        private const float CaptionY = 214f;
        private const float HintBottomPad = 20f;
        private const float PromptY = 276f;
        private const float E0BoxY = 380f;
        private static readonly Color StaminaColor = new Color(0.90f, 0.86f, 0.76f, 1f);
        private readonly System.Collections.Generic.Dictionary<CanvasGroup, float> _fadeTargets = new System.Collections.Generic.Dictionary<CanvasGroup, float>();

        /// <summary>Fades a HUD element to the target alpha over 0.15 s (unscaled time).</summary>
        private void FadeTo(CanvasGroup grp, float target)
        {
            if (grp == null) return;
            _fadeTargets[grp] = target;
        }

        private void TickFades(float dt)
        {
            if (_fadeTargets.Count == 0) return;
            _fadeDone.Clear();
            foreach (var kv in _fadeTargets)
            {
                if (kv.Key == null) { _fadeDone.Add(kv.Key); continue; }
                kv.Key.alpha = Mathf.MoveTowards(kv.Key.alpha, kv.Value, dt / FadeSeconds);
                if (Mathf.Approximately(kv.Key.alpha, kv.Value)) _fadeDone.Add(kv.Key);
            }
            foreach (CanvasGroup g in _fadeDone) _fadeTargets.Remove(g);
        }
        private readonly System.Collections.Generic.List<CanvasGroup> _fadeDone = new System.Collections.Generic.List<CanvasGroup>();

        /// <summary>Lays the prompt out as [key] title / hint and sizes the card to its contents.</summary>
        private void LayoutPrompt(string rawTitle, string hint, bool locked)
        {
            string key = CretaceaUiFactory.ExtractKey(rawTitle, out string title);
            bool hold = false;
            if (key != null && key.StartsWith("HOLD ")) { hold = true; key = key.Substring(5); }
            if (!string.IsNullOrEmpty(title) && title.StartsWith("Hold ", StringComparison.OrdinalIgnoreCase)) { hold = true; title = title.Substring(5); }
            bool showKey = key != null && !locked;
            _promptKey.Root.gameObject.SetActive(showKey);
            float x = 22f;
            if (showKey)
            {
                _promptKey.SetKey(key);
                x = 14f + _promptKey.Root.sizeDelta.x + 16f;
            }
            if (hold && string.IsNullOrEmpty(hint)) hint = "HOLD";

            _promptTitleText.text = title;
            _promptSubHintText.text = hint ?? string.Empty;
            float titleW = _promptTitleText.GetPreferredValues(title).x;
            float hintW = string.IsNullOrEmpty(hint) ? 0f : _promptSubHintText.GetPreferredValues(hint).x;
            bool twoLines = !string.IsNullOrEmpty(hint) && (locked || hintW > 160f);

            float h = twoLines ? 92f : 68f;
            float textW = twoLines ? Mathf.Max(titleW, hintW) : titleW + (hintW > 0f ? 18f + hintW : 0f);
            float w = Mathf.Clamp(x + textW + 26f, 260f, 1100f);
            _promptBox.sizeDelta = new Vector2(w, h);

            RectTransform tr = _promptTitleText.rectTransform;
            RectTransform hr = _promptSubHintText.rectTransform;
            if (twoLines)
            {
                tr.anchorMin = new Vector2(0f, 0.5f); tr.anchorMax = new Vector2(1f, 1f);
                tr.offsetMin = new Vector2(x, -2f); tr.offsetMax = new Vector2(-20f, -8f);
                hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0.5f);
                hr.offsetMin = new Vector2(x, 10f); hr.offsetMax = new Vector2(-20f, 2f);
            }
            else
            {
                tr.anchorMin = new Vector2(0f, 0f); tr.anchorMax = new Vector2(1f, 1f);
                tr.offsetMin = new Vector2(x, 2f); tr.offsetMax = new Vector2(-20f, 0f);
                hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 1f);
                hr.offsetMin = new Vector2(x + titleW + 18f, 2f); hr.offsetMax = new Vector2(-20f, 0f);
            }
            if (_promptKey.Root.gameObject.activeSelf)
                _promptKey.Root.anchoredPosition = new Vector2(14f, 0f);
        }

        /// <summary>Objective plate: as wide as the longest line (body text, journal hint) plus padding.</summary>
        private void SizeObjectivePlate()
        {
            if (_objectivePlate == null || _objectiveText == null) return;
            float bodyW = _objectiveText.GetPreferredValues(_objectiveText.text, 660f - 66f, 0f).x;
            float hintW = _journalHintText != null ? _journalHintText.GetPreferredValues(_journalHintText.text).x : 0f;
            float w = Mathf.Clamp(26f + Mathf.Max(bodyW, hintW, 260f) + 60f, 340f, 660f);
            // The banner grows downward with the text: a three-line objective used to run into the journal hint.
            if (_objectiveGroup != null && _objectiveGroup.transform is RectTransform box)
            {
                float bodyH = _objectiveText.GetPreferredValues(_objectiveText.text, 660f - 66f, 0f).y;
                // Top-down with fixed spacing (review 3: the hint hugged the bottom edge): header band 40, the copy,
                // 14 between the copy and the hint, the hint line 30, then the same padding as above the header.
                box.sizeDelta = new Vector2(box.sizeDelta.x, Mathf.Max(126f, 40f + bodyH + 14f + 30f + HintBottomPad));
            }
            _objectivePlate.offsetMin = Vector2.zero;
            _objectivePlate.offsetMax = new Vector2(w, 0f);
        }

        private void SizeBoxToText(RectTransform box, TextMeshProUGUI txt, float padX, float minW, float maxW)
        {
            if (box == null || txt == null) return;
            float w = txt.GetPreferredValues(txt.text, maxW - padX, 0f).x + padX;
            box.sizeDelta = new Vector2(Mathf.Clamp(w, minW, maxW), box.sizeDelta.y);
        }

        // =========================================================================
        // EVENT HANDLERS
        // =========================================================================

        private void OnObjectiveChanged(string objective)
        {
            if (_objectiveText == null || _objectiveGroup == null) return;
            string copy = ObjectiveGuide.Display(objective, PlayerXZ());
            _objectiveShownCopy = copy;
            string formatted = !string.IsNullOrEmpty(copy) && CretaceaInput.Instance != null
                ? CretaceaInput.Instance.FormatPrompt(copy)
                : (copy ?? string.Empty);
            _objectiveText.text = CretaceaUiFactory.FormatKeyTokens(SanitizeUiGlyphs(formatted));
            SizeObjectivePlate();
            if (string.IsNullOrEmpty(objective))
            {
                _objectiveGroup.alpha = 0f;
                _objectiveHighlightTimer = 0f;
            }
            else
            {
                _objectiveHighlightTimer = ObjectiveShowSeconds;
                _objectiveGroup.alpha = 1.0f;
            }
        }

        private void OnFlashlightAcquiredChanged(bool hasFlashlight)
        {
            if (_flashlightChip != null) _flashlightChip.SetActive(hasFlashlight);
            if (hasFlashlight) _flashlightChipTimer = Mathf.Max(_flashlightChipTimer, 5f);
            if (!hasFlashlight) _beamOn = false;
            RefreshFlashlightChipText(hasFlashlight && _beamOn);
        }

        private void OnFlashlightToggled(bool beamOn)
        {
            _beamOn = beamOn;
            RefreshFlashlightChipText(beamOn);
            _flashlightChipTimer = 3.5f;
        }

        /// <summary>Pickup: the light chip itself teaches the key (one amber pulse), instead of a second prompt style.</summary>
        private void OnFlashlightPickedUp()
        {
            _flashlightChipTimer = 8f;
            _flashlightPulse = FlashlightPulseSeconds;
            // The pickup raises a "[F] Toggle flashlight" toast in the same frame: the pulsing chip already teaches
            // that key, so one prompt style is enough.
            if (_toastFrame == Time.frameCount && _toastGroup != null)
            {
                _toastTimer = 0f;
                _toastGroup.alpha = 0f;
                FadeTo(_toastGroup, 0f);
            }
        }

        private void RefreshFlashlightChipText(bool beamOn)
        {
            if (_flashlightChipText == null) return;
            string token = CretaceaInput.Instance != null ? CretaceaInput.Instance.FormatPrompt("{flashlight}") : "[F]";
            string key = CretaceaUiFactory.ExtractKey(token, out _) ?? "F";
            if (_flashlightKey != null) _flashlightKey.SetKey(key);
            float textX = 10f + (_flashlightKey != null ? _flashlightKey.Root.sizeDelta.x : 32f) + 12f;
            _flashlightChipText.rectTransform.offsetMin = new Vector2(textX, 2f);
            // Name the action; the lamp carries the state ("[F] LIGHT ON" read as an instruction).
            _flashlightChipText.text = "FLASHLIGHT";
            _flashlightChipText.color = beamOn ? CretaceaUiFactory.TextPrimary : CretaceaUiFactory.TextMuted;
            if (_flashlightChipDot != null)
            {
                _flashlightChipDot.sprite = beamOn ? CretaceaUiFactory.GetCircleSprite() : CretaceaUiFactory.GetRadialRingSprite();
                _flashlightChipDot.color = beamOn ? CretaceaUiFactory.AccentAmber : new Color(0.52f, 0.49f, 0.44f, 1f);
            }
            if (_flashlightChipGlow != null) _flashlightChipGlow.enabled = beamOn;
        }

        private void OnMasterKeyChanged(bool hasKey)
        {
            if (_masterKeyChip != null) _masterKeyChip.SetActive(hasKey);
            if (hasKey) _masterKeyTimer = 6f;
        }

        private void OnStealthVisibilityChanged(float visibility, bool concealed)
        {
            _latestVisibility = visibility;
            _latestConcealed = concealed;
        }

        private void OnDinoStateChanged(string dinoId, string species, DinoState state)
        {
            _predatorAlertActive = state == DinoState.Alert || state == DinoState.Hunt || state == DinoState.Attack;
        }

        private void UpdateStealthEyeVisual()
        {
            if (_stealthGroup == null || _stealthEyeImage == null) return;

            GameState gs = GameState.Instance;
            bool blackoutActive = gs != null && gs.GetFlag("blackout");
            string state = _predatorAlertActive ? "spotted" : (_latestVisibility > 0.95f ? "exposed" : ((_latestConcealed || _latestVisibility <= 0.18f) ? "concealed" : "shadowed"));
            if (state != _stealthState) { _stealthState = state; _stealthStateTimer = 3f; }
            else if (_stealthStateTimer > 0f) _stealthStateTimer -= Time.unscaledDeltaTime;
            bool journalOpen = gs != null && gs.IsJournalOpen;
            bool showEye = blackoutActive && !_suppressedByCutscene && !_suppressedByModal && !journalOpen
                && (_predatorAlertActive || _stealthStateTimer > 0f);
            float targetAlpha = showEye ? (_predatorAlertActive ? 0.98f : 0.78f) : 0f;
            _stealthGroup.alpha = Mathf.MoveTowards(_stealthGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 3.5f);
            if (!showEye) return;

            if (_predatorAlertActive || _latestVisibility > 0.95f)
            {
                _stealthEyeImage.sprite = customEyeOpenSprite != null ? customEyeOpenSprite : CretaceaUiFactory.GetStealthEyeSprite(2);
                float pulse = _predatorAlertActive ? (0.65f + 0.35f * Mathf.Sin(Time.time * 9f)) : 1f;
                Color col = _predatorAlertActive ? CretaceaUiFactory.AccentDanger : CretaceaUiFactory.AccentAmber;
                _stealthEyeImage.color = col * pulse;
                _stealthLabel.text = _predatorAlertActive ? "SPOTTED!" : "EXPOSED";
                _stealthLabel.color = col;
            }
            else if (_latestConcealed || _latestVisibility <= 0.18f)
            {
                _stealthEyeImage.sprite = customEyeClosedSprite != null ? customEyeClosedSprite : CretaceaUiFactory.GetStealthEyeSprite(0);
                _stealthEyeImage.color = CretaceaUiFactory.AccentTeal;
                _stealthLabel.text = "CONCEALED";
                _stealthLabel.color = CretaceaUiFactory.AccentTeal;
            }
            else
            {
                _stealthEyeImage.sprite = customEyeHalfSprite != null ? customEyeHalfSprite : CretaceaUiFactory.GetStealthEyeSprite(1);
                _stealthEyeImage.color = CretaceaUiFactory.TextMuted;
                _stealthLabel.text = "SHADOWED";
                _stealthLabel.color = CretaceaUiFactory.TextMuted;
            }
        }

        private void OnStaminaChanged(float stamina01, bool exhausted)
        {
            if (_staminaGroup == null || _staminaFill == null) return;
            bool show = exhausted || stamina01 < 0.95f;
            FadeTo(_staminaGroup, show ? 0.92f : 0f);
            _staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(stamina01), 1f);
            _staminaFill.color = exhausted ? CretaceaUiFactory.AccentDanger : StaminaColor;
        }

        private void OnInteractionFocusChanged(InteractionFocusInfo info)
        {
            if (_promptGroup == null) return;

            if (!info.HasTarget)
            {
                FadeTo(_promptGroup, 0f);
                if (_holdRingImage != null) _holdRingImage.fillAmount = 0f;
                if (_holdRingTrack != null) _holdRingTrack.color = new Color(1f, 0.95f, 0.85f, 0f);
                if (_reticleDot != null) _reticleDot.rectTransform.sizeDelta = new Vector2(7f, 7f);
                if (_e0ProgressRoot != null) _e0ProgressRoot.SetActive(false);
                return;
            }

            FadeTo(_promptGroup, 1f);
            if (_reticleDot != null) _reticleDot.rectTransform.sizeDelta = new Vector2(11f, 11f);

            bool isE0 = info.PreserveHoldProgress && string.Equals(info.InteractId, "gate_box", StringComparison.OrdinalIgnoreCase);
            bool locked = info.IsLocked || !info.CanInteract;
            if (locked && GameState.Instance != null && GameState.Instance.HasMasterKey) _masterKeyTimer = Mathf.Max(_masterKeyTimer, 4f);
            if (locked)
            {
                LayoutPrompt(SanitizeUiGlyphs(string.IsNullOrEmpty(info.PromptText) ? "LOCKED" : info.PromptText), SanitizeUiGlyphs(info.LockedHint ?? string.Empty), true);
                _promptTitleText.color = CretaceaUiFactory.TextMuted;
                _promptSubHintText.color = CretaceaUiFactory.AccentDanger;
                _promptAccentBar.color = CretaceaUiFactory.AccentDanger;
                if (_holdRingImage != null) _holdRingImage.fillAmount = 0f;
                if (_holdRingTrack != null) _holdRingTrack.color = new Color(1f, 0.95f, 0.85f, 0f);
            }
            else
            {
                bool hold = info.HoldDuration > 0.01f;
                string hint = hold
                    ? (info.PreserveHoldProgress
                        ? (isE0 ? "HOLD" : "HOLD  \u00B7  PROGRESS KEPT IF YOU LET GO")
                        : $"HOLD {info.HoldDuration:0.0}S")
                    : string.Empty;
                LayoutPrompt(SanitizeUiGlyphs(info.PromptText ?? string.Empty), hint, false);
                _promptTitleText.color = CretaceaUiFactory.TextPrimary;
                _promptSubHintText.color = CretaceaUiFactory.AccentAmber;
                _promptAccentBar.color = CretaceaUiFactory.AccentAmber;
                if (_holdRingImage != null) _holdRingImage.fillAmount = hold ? info.HoldProgress01 : 0f;
                if (_holdRingTrack != null) _holdRingTrack.color = new Color(1f, 0.95f, 0.85f, hold ? 0.22f : 0f);
            }

            if (_e0ProgressRoot != null)
            {
                _e0ProgressRoot.SetActive(isE0 && info.CanInteract);
                if (isE0) UpdateE0Bar(info.HoldProgress01);
            }
        }

        private void OnE0GateProgressChanged(float progress01)
        {
            UpdateE0Bar(progress01);
        }

        private void UpdateE0Bar(float progress01)
        {
            if (_e0ProgressFill != null) _e0ProgressFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);
            if (_e0ProgressLabel != null) _e0ProgressLabel.text = $"E-0 GATE  {Mathf.RoundToInt(progress01 * 100f)}%  -  PROGRESS SAVED";
        }

        private void OnSubtitleRequested(string speaker, string text, float duration)
        {
            if (!SaveManager.Settings.subtitlesEnabled || _subtitleGroup == null) return;
            string spk = SanitizeUiGlyphs(speaker ?? string.Empty).ToUpperInvariant();
            _subtitleSpeakerText.text = spk;
            _subtitleBodyText.text = SanitizeUiGlyphs(text ?? string.Empty);
            // Size the box to the text: 1-2 lines, speaker line only when there is a speaker.
            bool hasSpeaker = !string.IsNullOrEmpty(spk);
            float wrapW = BalancedWrapWidth(_subtitleBodyText, _subtitleBodyText.text, SubtitleWrapW);
            Vector2 bodySize = _subtitleBodyText.GetPreferredValues(_subtitleBodyText.text, wrapW, 0f);
            float bodyH = bodySize.y;
            float bodyW = Mathf.Min(bodySize.x, wrapW);
            float boxH = Mathf.Clamp(bodyH, 34f, 110f) + (hasSpeaker ? 40f : 0f) + 26f;
            float boxW = Mathf.Clamp(Mathf.Max(bodyW, hasSpeaker ? _subtitleSpeakerText.GetPreferredValues(spk).x : 0f) + 72f, 420f, SubtitleMaxW);
            if (_subtitleBox != null) _subtitleBox.sizeDelta = new Vector2(boxW, boxH);
            _subtitleBodyText.rectTransform.offsetMax = new Vector2(-32f, hasSpeaker ? -42f : -13f);
            FadeTo(_subtitleGroup, string.IsNullOrEmpty(text) ? 0f : 1f);
            _subtitleTimer = Mathf.Max(0.5f, duration);
        }

        /// <summary>
        /// Narrowest wrap width (at most maxW) that keeps the caption on as many lines as at maxW, so a two-line
        /// caption breaks near its middle instead of leaving one word alone on the second line.
        /// </summary>
        private static float BalancedWrapWidth(TextMeshProUGUI tmp, string text, float maxW)
        {
            if (tmp == null || string.IsNullOrEmpty(text)) return maxW;
            float h = tmp.GetPreferredValues(text, maxW, 0f).y;
            float lineH = tmp.GetPreferredValues("Ag", maxW, 0f).y;
            if (h < lineH * 1.5f) return maxW;                 // one line: nothing to balance
            float lo = maxW * 0.35f, hi = maxW;
            for (int i = 0; i < 10; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (tmp.GetPreferredValues(text, mid, 0f).y <= h + 0.5f) hi = mid; else lo = mid;
            }
            return Mathf.Min(maxW, hi + 2f);
        }

        private void OnToastMessageRequested(string text, float duration)
        {
            if (_toastGroup == null || string.IsNullOrEmpty(text)) return;
            string formatted = CretaceaInput.Instance != null ? CretaceaInput.Instance.FormatPrompt(text) : text;
            _toastText.text = CretaceaUiFactory.FormatKeyTokens(SanitizeUiGlyphs(formatted));
            SizeBoxToText(_toastBox, _toastText, 46f, 300f, 760f);
            FadeTo(_toastGroup, 1f);
            _toastTimer = Mathf.Max(1.0f, duration);
            _toastFrame = Time.frameCount;
        }

        private void OnSoundCaptionRequested(string caption, float duration)
        {
            if (!SaveManager.Settings.soundCaptionsEnabled || _captionGroup == null || string.IsNullOrEmpty(caption)) return;
            _captionText.text = SanitizeUiGlyphs(caption);
            SizeBoxToText(_captionBox, _captionText, 48f, 260f, 900f);
            FadeTo(_captionGroup, 1f);
            _captionTimer = Mathf.Max(0.8f, duration);
        }

        private void OnDamageFlashRequested(float intensity01)
        {
            _damageFlashAlpha = Mathf.Clamp01(Mathf.Max(_damageFlashAlpha, intensity01 * 0.6f));
        }

        private void OnTitleCardRequested(string title, string subtitle, float duration)
        {
            if (_titleCardGroup == null || _suppressedByModal) return;
            if (_titleCardRoutine != null) StopCoroutine(_titleCardRoutine);
            _titleCardRoutine = StartCoroutine(TitleCardRoutine(title, subtitle, duration));
        }

        private IEnumerator TitleCardRoutine(string title, string subtitle, float duration)
        {
            string head = SanitizeUiGlyphs(title ?? string.Empty);
            string sub = SanitizeUiGlyphs(subtitle ?? string.Empty);
            // "CHAPTER 6 — OLD MAINTENANCE YARD": the chapter number goes on the mono line, the name is the heading, so
            // every chapter card has the same shape and the longest names still fit the column at the heading size.
            int dash = head.IndexOf(" \u2014 ", StringComparison.Ordinal);
            if (sub.Length == 0 && dash > 0 && head.StartsWith("CHAPTER", StringComparison.OrdinalIgnoreCase))
            {
                sub = head.Substring(0, dash);
                head = head.Substring(dash + 3);
            }
            _titleMainText.text = head;
            _titleSubText.text = sub;
            LayoutTitleCard(_titleMainText.text.Length > 0, _titleSubText.text.Length > 0);

            yield return FadeCanvasGroup(_titleCardGroup, 0f, 1f, 0.55f);
            yield return new WaitForSeconds(Mathf.Max(0.8f, duration));
            yield return FadeCanvasGroup(_titleCardGroup, _titleCardGroup.alpha, 0f, 0.65f);
            _titleCardRoutine = null;
        }

        private static string SanitizeUiGlyphs(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // The Barlow / Courier Prime SDF assets carry dashes, quotes and the middle dot; arrows are not in the fonts.
            // A spaced hyphen in story text is a dash: set it as an em dash (ranges like 7.4-7.5 have no spaces).
            return s.Replace("\u2192", "->").Replace("\u2190", "<-").Replace(" - ", " \u2014 ");
        }

        private void OnScreenFadeRequested(float targetAlpha, float duration)
        {
            if (_screenFadeImage == null) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeImageRoutine(_screenFadeImage, targetAlpha, duration));
        }

        private void OnLetterboxRequested(bool enabled, float duration)
        {
            if (_letterboxTop == null || _letterboxBottom == null) return;
            if (_letterboxRoutine != null) StopCoroutine(_letterboxRoutine);
            _letterboxRoutine = StartCoroutine(LetterboxRoutine(enabled ? 92f : 0f, Mathf.Max(0.05f, duration)));
        }

        private IEnumerator LetterboxRoutine(float targetHeightPx, float duration)
        {
            float startH = _letterboxBottom.offsetMax.y;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float h = Mathf.Lerp(startH, targetHeightPx, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                _letterboxTop.offsetMin = new Vector2(0f, -h);
                _letterboxBottom.offsetMax = new Vector2(0f, h);
                ApplyLetterboxLift(h);
                yield return null;
            }
            _letterboxTop.offsetMin = new Vector2(0f, -targetHeightPx);
            _letterboxBottom.offsetMax = new Vector2(0f, targetHeightPx);
            ApplyLetterboxLift(targetHeightPx);
            _letterboxRoutine = null;
        }

        private bool _journalLayout;
        private float _letterboxLift;
        private static readonly Color SubtitleBg = new Color(0f, 0f, 0f, 0.62f);   // pure black: cannot tint on the 8-bit linear UI steps

        /// <summary>
        /// While the field tablet is open: the caption pill moves to the free band above the tablet (top left) so it
        /// never lands on the tablet's text, and the subtitle box turns nearly opaque over the tablet.
        /// </summary>
        private void ApplyJournalLayout(bool journal)
        {
            if (journal == _journalLayout) return;
            _journalLayout = journal;
            if (_captionBox != null)
            {
                Vector2 a = journal ? new Vector2(0f, 1f) : new Vector2(0.5f, 0f);
                _captionBox.anchorMin = _captionBox.anchorMax = a;
                _captionBox.pivot = journal ? new Vector2(0f, 1f) : new Vector2(0.5f, 0f);
                _captionBox.anchoredPosition = journal ? new Vector2(212f, -44f) : new Vector2(0f, CaptionY + _letterboxLift);
            }
            if (_subtitleBox != null)
            {
                Image bg = _subtitleBox.GetComponent<Image>();
                if (bg != null) bg.color = journal ? new Color(SubtitleBg.r, SubtitleBg.g, SubtitleBg.b, 0.94f) : SubtitleBg;
            }
        }

        /// <summary>Keeps subtitles and captions above the cutscene letterbox bar.</summary>
        private void ApplyLetterboxLift(float barHeight)
        {
            float lift = Mathf.Max(0f, barHeight + 16f - SubtitleY);
            _letterboxLift = lift;
            if (_subtitleBox != null) _subtitleBox.anchoredPosition = new Vector2(0f, SubtitleY + lift);
            if (_captionBox != null && !_journalLayout)
            {
                // In a letterboxed cutscene the sound caption sits inside the lower black bar, clear of the picture
                // (review 3: the ending's caption boxed the hero boat in on the rail); in gameplay it stays above
                // the subtitles.
                bool inBar = barHeight >= _captionBox.sizeDelta.y + 12f;
                _captionBox.anchoredPosition = inBar
                    ? new Vector2(0f, Mathf.Round((barHeight - _captionBox.sizeDelta.y) * 0.5f))
                    : new Vector2(0f, CaptionY + lift);
                Image capBg = _captionBox.GetComponent<Image>();
                if (capBg != null) capBg.color = new Color(0f, 0f, 0f, inBar ? 0f : 0.62f);
            }
        }

        private void OnCutsceneStateChanged(bool inCutscene)
        {
            _suppressedByCutscene = inCutscene;
            RefreshHudLayerVisibility();
        }

        private void OnCutsceneSkipPromptChanged(bool visible, Action onSkip)
        {
            if (_skipGroup == null) return;
            FadeTo(_skipGroup, visible ? 0.95f : 0f);
            if (visible && _skipText != null && CretaceaInput.Instance != null)
            {
                _skipText.text = CretaceaUiFactory.FormatKeyTokens(CretaceaInput.Instance.FormatPrompt("{skip} Skip Intro"));
            }
        }

        private void OnPauseStateChanged(bool paused)
        {
            SetModalSuppressed(paused);
        }

        private void OnGameFinished(string summary)
        {
            SetModalSuppressed(true);
            // The ending fades to black for its title card; the end card then lifts the curtain so the dawn coast
            // shows (dimmed) behind it instead of a flat black screen.
            OnScreenFadeRequested(0f, 2.5f);
        }

        private void OnInputDeviceChanged(InputDeviceType device)
        {
            RefreshJournalHintText();
            if (GameState.Instance != null)
            {
                if (GameState.Instance.HasFlashlight)
                    RefreshFlashlightChipText(_beamOn);
                if (!string.IsNullOrEmpty(GameState.Instance.CurrentObjective) && _objectiveText != null)
                {
                    string copy = ObjectiveGuide.Display(GameState.Instance.CurrentObjective, PlayerXZ());
                    string raw = CretaceaInput.Instance != null ? CretaceaInput.Instance.FormatPrompt(copy) : copy;
                    _objectiveText.text = CretaceaUiFactory.FormatKeyTokens(SanitizeUiGlyphs(raw));
                    SizeObjectivePlate();
                }
            }
        }

        private void RefreshJournalHintText()
        {
            if (_journalHintText == null) return;
            string token = CretaceaInput.Instance != null ? CretaceaInput.Instance.FormatPrompt("{journal}") : "[Tab]";
            // the route map exists only once the manual is read
            _hintManualShown = GameState.Instance != null && GameState.Instance.GetFlag("manual_read");
            _journalHintText.text = CretaceaUiFactory.FormatKeyTokens($"{token}  " + (_hintManualShown ? "JOURNAL & ROUTE MAP" : "FIELD TABLET"));
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup grp, float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                grp.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
            grp.alpha = to;
        }

        private static IEnumerator FadeImageRoutine(Image img, float targetAlpha, float dur)
        {
            float start = img.color.a;
            if (dur <= 0.01f)
            {
                Color c0 = img.color;
                c0.a = targetAlpha;
                img.color = c0;
                yield break;
            }

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                Color c = img.color;
                c.a = Mathf.Lerp(start, targetAlpha, Mathf.Clamp01(t / dur));
                img.color = c;
                yield return null;
            }
        }
    }
}
