using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Cretacea.Core;

namespace Cretacea.UI
{
    /// <summary>
    /// Complete modal & menu controller with strict single-active-overlay discipline (ZERO overlapping logos,
    /// timestamps, subtitles, or clipped titles!):
    /// - Live 3D HDRP Main Menu (dark-glass left sidebar over live Isla Tormenta storm scene — never baked menu_bg.jpg!)
    /// - Pause Menu (hides HUD timestamp cards & subtitles cleanly)
    /// - Settings Modal (generous 64 px header clearance so "CRETACEA" never clips; live audio/sensitivity/FOV/quality)
    /// - Chapter Select (all 7 chapters)
    /// - Document Inspection Modal (zoomable image + readable summary; clean close)
    /// - Death / Retry Overlay & Dawn Coast Ending Summary + Credits (accurate 5/5 documents & completion stats!)
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        public static MenuController Instance { get; private set; }

        [Header("Scene Configuration")]
        [SerializeField] private bool isMainMenuScene = false;
        [SerializeField] private string gameplaySceneName = "IslaTormenta";
        [SerializeField] private string mainMenuSceneName = "MainMenu";

        private Canvas _canvas;

        // Distinct non-overlapping screen roots
        private GameObject _mainMenuRoot;
        private GameObject _pauseMenuRoot;
        private GameObject _settingsRoot;
        private GameObject _controlsRoot;
        private GameObject _returnAfterControls;
        private Button _controlsBackButton;
        private GameObject _chapterSelectRoot;
        private GameObject _documentModalRoot;
        private GameObject _deathRoot;
        private GameObject _endingRoot;

        private Button _continueButton;
        private Button[] _chapterButtons;

        // Document Modal widgets
        private Image _docModalImage;
        private TextMeshProUGUI _docModalSummaryText;
        private Action _docModalOnClosed;
        // Owner's playtest (Nov 21): reading a document with E / gamepad X showed nothing. The press that opens the page
        // is still 'pressed this frame' when this Update runs after PlayerInteractor's in the same frame (the script order
        // is not fixed, and in the player build it falls that way), so the page closed in the frame it opened. A close
        // press counts from the next frame and 0.25 s after opening.
        private int _docOpenedFrame = -1;
        private float _docOpenedAt = -10f;
        private const float DocCloseArmSeconds = 0.25f;
        private bool DocCloseArmed => Time.frameCount > _docOpenedFrame && Time.unscaledTime - _docOpenedAt >= DocCloseArmSeconds;

        // Ending Summary text
        private TextMeshProUGUI _endingStatsText;
        private TextMeshProUGUI _endingHeadline;
        private TextMeshProUGUI[] _endingStatValues;

        private GameObject _returnMenuAfterSettings;

        // Look-pass additions
        private Button _newGameButton;
        private Button _resumeButton;
        private Selectable _firstSettingsControl;
        private TextMeshProUGUI _pauseChapterText;
        private TextMeshProUGUI _docTitleText;
        private TextMeshProUGUI _docKickerText;
        private TextMeshProUGUI _docCloseLabel;
        private CretaceaUiFactory.Keycap _docCloseKey;
        private RectTransform _docPageShadow;
        private RectTransform _docColumn;
        private RectTransform _docRule;
        private TextMeshProUGUI _docTranscriptKicker;
        private TextMeshProUGUI _docTranscriptText;
        private RectTransform _docTranscriptRule;
        private TextMeshProUGUI _deathSubText;
        private Button _deathRetryButton;
        private Button _endingMenuButton;
        private Button _endingCreditsButton;
        private CanvasGroup _endingGroup;
        private RectTransform _endingStatsCard, _endingCreditsCard;
        private Coroutine _endingRevealRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildAllMenus();
        }

        private void OnEnable()
        {
            GameEvents.OnShowDocumentRequested += OnShowDocumentRequested;
            GameEvents.OnPlayerDied += OnPlayerDied;
            GameEvents.OnGameFinished += OnGameFinished;
            GameEvents.OnCheckpointReached += OnCheckpointReached;
            GameEvents.OnCutsceneStateChanged += OnCutsceneStateChangedForModals;
            GameEvents.OnCutsceneSkipPromptChanged += OnSkipPromptChangedForModals;
            GameEvents.OnTitleCardRequested += OnTitleCardForModals;
            GameEvents.OnPauseStateChanged += OnPauseStateChangedForModals;
        }

        private void OnDisable()
        {
            GameEvents.OnShowDocumentRequested -= OnShowDocumentRequested;
            GameEvents.OnPlayerDied -= OnPlayerDied;
            GameEvents.OnGameFinished -= OnGameFinished;
            GameEvents.OnCheckpointReached -= OnCheckpointReached;
            GameEvents.OnCutsceneStateChanged -= OnCutsceneStateChangedForModals;
            GameEvents.OnCutsceneSkipPromptChanged -= OnSkipPromptChangedForModals;
            GameEvents.OnTitleCardRequested -= OnTitleCardForModals;
            GameEvents.OnPauseStateChanged -= OnPauseStateChangedForModals;
        }

        // ---- Story beats close the reading / pause overlays ------------------------------------------------
        // A chapter start, a checkpoint jump (StoryDirector.StartFromCheckpoint always hides the skip prompt first),
        // a chapter title card or a cutscene must never play under an open document, tablet or pause panel.

        private void OnCutsceneStateChangedForModals(bool inCutscene) { if (inCutscene) DismissStoryOverlays(); }
        private void OnSkipPromptChangedForModals(bool visible, Action _) => DismissStoryOverlays();
        private void OnTitleCardForModals(string title, string subtitle, float duration) => DismissStoryOverlays();

        private void OnPauseStateChangedForModals(bool paused)
        {
            // Something else unpaused the game (a chapter jump, a revive): the pause and settings panels go with it.
            if (paused || isMainMenuScene) return;
            bool pauseUp = (_pauseMenuRoot != null && _pauseMenuRoot.activeSelf) || (_settingsRoot != null && _settingsRoot.activeSelf)
                           || (_chapterSelectRoot != null && _chapterSelectRoot.activeSelf) || (_controlsRoot != null && _controlsRoot.activeSelf);
            if (!pauseUp) return;
            if (_pauseMenuRoot != null) _pauseMenuRoot.SetActive(false);
            if (_settingsRoot != null) _settingsRoot.SetActive(false);
            if (_controlsRoot != null) _controlsRoot.SetActive(false);
            if (_chapterSelectRoot != null) _chapterSelectRoot.SetActive(false);
            HudController.Instance?.SetModalSuppressed(false);
            GameState.Instance?.RefreshCursor();
        }

        /// <summary>
        /// Closes the document reader (without opening its next page), the field tablet and the pause panels.
        /// Death and ending overlays are left to their own flow.
        /// </summary>
        public void DismissStoryOverlays()
        {
            if (isMainMenuScene) return;
            JournalController.Instance?.CloseJournal();
            if (_documentModalRoot != null && _documentModalRoot.activeSelf) AbortDocumentModal();
            GameState gs = GameState.Instance;
            if (gs != null && gs.IsPaused) ResumeGame();
            else OnPauseStateChangedForModals(false);
        }

        /// <summary>Hides the reader without running its page chain, and frees the pickup so it can be read again.</summary>
        private void AbortDocumentModal()
        {
            _docModalOnClosed = null;
            _documentModalRoot.SetActive(false);
            HudController.Instance?.SetModalSuppressed(false);
            GameState gs = GameState.Instance;
            if (gs != null) { gs.SetDocumentOpen(false); gs.RefreshCursor(); }
            ResetShowingDocumentPickups();
        }

        // DocumentPickup (Cretacea.Interact, not referenced by the UI assembly) keeps a private "showing" latch until its
        // last page closes. An aborted reader would leave that latch set and the document unreadable, so it is reset
        // here by name. (Requested from the lead: a public DocumentPickup.CancelReading().)
        private static Type s_DocPickupType;
        private static System.Reflection.FieldInfo s_DocShowingField;
        private static void ResetShowingDocumentPickups()
        {
            if (s_DocPickupType == null) s_DocPickupType = Type.GetType("Cretacea.Interact.DocumentPickup, Cretacea.Interact");
            if (s_DocPickupType == null) return;
            if (s_DocShowingField == null)
                s_DocShowingField = s_DocPickupType.GetField("_isShowing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (s_DocShowingField == null) return;
            foreach (UnityEngine.Object o in FindObjectsByType(s_DocPickupType, FindObjectsInactive.Include))
                if (o != null && s_DocShowingField.GetValue(o) is bool b && b) s_DocShowingField.SetValue(o, false);
        }

        private void OnCheckpointReached(CheckpointId checkpoint)
        {
            if (isMainMenuScene) return;
            if ((_endingRoot != null && _endingRoot.activeSelf) || (_deathRoot != null && _deathRoot.activeSelf))
            {
                HideAllModals();
                if (HudController.Instance != null) HudController.Instance.SetModalSuppressed(false);
                return;
            }
            // A new checkpoint is a story beat: no reader, tablet or pause panel stays over it. (The manual's own
            // checkpoint is saved from its close callback, after the reader is already hidden.)
            DismissStoryOverlays();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            GameState gs = GameState.EnsureInstance();
            CretaceaInput.EnsureInstance();

            if (isMainMenuScene)
            {
                gs.SetMainMenuState(true);
                gs.RefreshCursor();
                ShowMainMenu();
                GameEvents.RaiseMusicState("menu", 2.0f);
                GameEvents.RaiseAmbienceZone("menu", indoors: false, fadeSeconds: 2.0f);
            }
            else
            {
                HideAllModals();
            }
        }

        private void Update()
        {
            CretaceaInput input = CretaceaInput.Instance;
            GameState gs = GameState.Instance;
            if (input == null || gs == null) return;

            // The death panel never outlives the death: any revive (retry, checkpoint jump) clears IsGameOver.
            if (_deathRoot != null && _deathRoot.activeSelf && !gs.IsGameOver)
            {
                _deathRoot.SetActive(false);
                if (HudController.Instance != null) HudController.Instance.SetModalSuppressed(false);
                gs.RefreshCursor();
            }

            // Document viewer close on Interact/Cancel
            if (_documentModalRoot != null && _documentModalRoot.activeSelf && (gs.InCutscene || gs.IsGameOver))
            {
                AbortDocumentModal();
            }
            if (_documentModalRoot != null && _documentModalRoot.activeSelf)
            {
                if (DocCloseArmed && (input.CancelPressedThisFrame || input.InteractPressedThisFrame))
                {
                    CloseDocumentModal();
                }
                return;
            }

            if (isMainMenuScene)
            {
                if (input.CancelPressedThisFrame && _controlsRoot != null && _controlsRoot.activeSelf)
                {
                    CloseControls();
                }
                else if (input.CancelPressedThisFrame && (_settingsRoot.activeSelf || _chapterSelectRoot.activeSelf))
                {
                    ShowMainMenu();
                }
                return;
            }

            if (gs.IsGameOver || (_endingRoot != null && _endingRoot.activeSelf)) return;

            if (input.PausePressedThisFrame)
            {
                if (JournalController.Instance != null && JournalController.Instance.IsOpen)
                {
                    JournalController.Instance.CloseJournal();
                    return;
                }

                if (_controlsRoot != null && _controlsRoot.activeSelf)
                {
                    CloseControls();
                }
                else if (_settingsRoot.activeSelf || _chapterSelectRoot.activeSelf)
                {
                    ShowPauseMenu();
                }
                else if (gs.IsPaused)
                {
                    ResumeGame();
                }
                else
                {
                    ShowPauseMenu();
                }
            }
        }

        // =========================================================================
        // STRICT SINGLE-OVERLAY SWITCHING (ZERO OVERLAPPING TEXT/LOGOS)
        // =========================================================================

        private void HideAllModals()
        {
            if (_mainMenuRoot != null) _mainMenuRoot.SetActive(false);
            if (_pauseMenuRoot != null) _pauseMenuRoot.SetActive(false);
            if (_settingsRoot != null) _settingsRoot.SetActive(false);
            if (_controlsRoot != null) _controlsRoot.SetActive(false);
            if (_chapterSelectRoot != null) _chapterSelectRoot.SetActive(false);
            if (_documentModalRoot != null) _documentModalRoot.SetActive(false);
            if (_deathRoot != null) _deathRoot.SetActive(false);
            if (_endingRoot != null) _endingRoot.SetActive(false);
            HudController.Instance?.SetModalSuppressed(false);
        }

        public void ShowMainMenu()
        {
            HideAllModals();
            if (_continueButton != null) _continueButton.gameObject.SetActive(SaveManager.HasSaveFile());
            _mainMenuRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            CretaceaUiFactory.Focus(_continueButton != null && _continueButton.gameObject.activeSelf ? _continueButton : _newGameButton);
        }

        public void ShowPauseMenu()
        {
            HideAllModals();
            JournalController.Instance?.CloseJournal();
            GameState.EnsureInstance().SetPaused(true);
            if (_pauseChapterText != null) _pauseChapterText.text = GameState.GetChapterTitle(GameState.EnsureInstance().CurrentCheckpoint);
            _pauseMenuRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            CretaceaUiFactory.Focus(_resumeButton);
        }

        public void ResumeGame()
        {
            HideAllModals();
            GameState.EnsureInstance().SetPaused(false);
        }

        public void OpenSettings(GameObject returnTarget)
        {
            _returnMenuAfterSettings = returnTarget;
            HideAllModals();
            _settingsRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            CretaceaUiFactory.Focus(_firstSettingsControl);
        }

        /// <summary>Keyboard and controller reference. Back returns to the pause menu or the settings it came from.</summary>
        public void OpenControls(GameObject returnTarget)
        {
            _returnAfterControls = returnTarget;
            HideAllModals();
            _controlsRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            CretaceaUiFactory.Focus(_controlsBackButton);
        }

        private void CloseControls()
        {
            if (_returnAfterControls == _settingsRoot) OpenSettings(_returnMenuAfterSettings);
            else if (!isMainMenuScene) ShowPauseMenu();
            else ShowMainMenu();
        }

        public void OpenChapterSelect()
        {
            HideAllModals();
            RefreshChapterButtons();
            _chapterSelectRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            if (_chapterButtons != null && _chapterButtons.Length > 0) CretaceaUiFactory.Focus(_chapterButtons[0]);
        }

        private void OnShowDocumentRequested(string docId, string assetKey, string summary, Action onClosed)
        {
            HideAllModals();
            _docModalOnClosed = onClosed;
            _documentModalRoot.SetActive(true);
            _docOpenedFrame = Time.frameCount;
            _docOpenedAt = Time.unscaledTime;
            HudController.Instance?.SetModalSuppressed(true);

            Sprite pageSpr = CretaceaUiFactory.LoadSprite($"UI/Docs/{assetKey}");
            float pageW = DocPageMaxW, pageH = DocPageMaxH;
            if (_docModalImage != null)
            {
                _docModalImage.sprite = pageSpr;
                _docModalImage.color = pageSpr != null ? Color.white : new Color(0.10f, 0.095f, 0.085f, 1f);
                // Fit the page into 714 x 980 (portrait pages fill the height: ~980 px at 1080p).
                float aspect = pageSpr != null ? pageSpr.rect.width / Mathf.Max(1f, pageSpr.rect.height) : 0.727f;
                pageH = DocPageMaxH; pageW = pageH * aspect;
                if (pageW > DocPageMaxW) { pageW = DocPageMaxW; pageH = pageW / aspect; }
            }
            // Page and reading column side by side, the pair centred on screen.
            float total = pageW + DocGap + DocColumnW;
            RectTransform holder = _docModalImage != null ? _docModalImage.rectTransform.parent as RectTransform : null;
            if (holder != null)
            {
                holder.sizeDelta = new Vector2(pageW, pageH);
                holder.anchoredPosition = new Vector2(-total * 0.5f + pageW * 0.5f, 0f);
            }
            if (_docColumn != null) _docColumn.anchoredPosition = new Vector2(total * 0.5f - DocColumnW * 0.5f, 0f);

            if (_docTitleText != null) _docTitleText.text = DocumentTitle(docId).ToUpperInvariant();
            if (_docKickerText != null)
            {
                string page = string.Empty;
                int pi = assetKey != null ? assetKey.LastIndexOf("_p", StringComparison.Ordinal) : -1;
                if (pi > 0 && int.TryParse(assetKey.Substring(pi + 2), out int pn)) page = $"   \u00B7   PAGE {pn}";
                _docKickerText.text = "DOCUMENT" + page;
            }
            // The reading column says each thing once: the page's transcript when there is one, else the summary.
            string transcript = JournalController.GetPageTranscript(docId, assetKey);
            if (_docModalSummaryText != null)
            {
                _docModalSummaryText.text = string.IsNullOrEmpty(transcript) ? (summary ?? string.Empty) : string.Empty;
            }
            if (_docTranscriptText != null)
            {
                bool hand = JournalController.IsHandwritten(docId);
                CretaceaUiFactory.SetFont(_docTranscriptText, hand ? UiFont.Hand : UiFont.Mono);
                _docTranscriptText.fontSizeMax = hand ? 32f : 23f;
                _docTranscriptText.fontSize = _docTranscriptText.fontSizeMax;
                _docTranscriptText.lineSpacing = hand ? -2f : 8f;
                _docTranscriptText.paragraphSpacing = hand ? 0f : 55f;   // air between numbered items (TMP: 1/100 em, so 55 = 0.55 em)
                _docTranscriptText.text = transcript;
            }
            LayoutDocumentColumn(!string.IsNullOrEmpty(transcript));
            if (_docCloseKey != null && CretaceaInput.Instance != null)
            {
                string key = CretaceaUiFactory.ExtractKey(CretaceaInput.Instance.FormatPrompt("{interact}"), out _) ?? "E";
                _docCloseKey.SetKey(key);
            }
            GameState.EnsureInstance().SetDocumentOpen(true);
        }

        public void CloseDocumentModal()
        {
            Action cb = _docModalOnClosed;
            _docModalOnClosed = null;
            _documentModalRoot.SetActive(false);
            HudController.Instance?.SetModalSuppressed(false);
            GameEvents.RaisePlaySound("foley_paper_rustle", null, 0.7f);
            cb?.Invoke();
        }

        private void OnPlayerDied()
        {
            if (_documentModalRoot != null && _documentModalRoot.activeSelf) AbortDocumentModal();
            HideAllModals();
            JournalController.Instance?.CloseJournal();
            if (_deathSubText != null) _deathSubText.text = "LAST CHECKPOINT   \u00B7   " + GameState.GetChapterTitle(GameState.EnsureInstance().CurrentCheckpoint);
            _deathRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            CretaceaUiFactory.Focus(_deathRetryButton);
        }

        private void OnGameFinished(string statsSummary)
        {
            HideAllModals();
            JournalController.Instance?.CloseJournal();
            FillEndingCard(statsSummary);
            if (_endingCreditsCard != null && _endingCreditsCard.gameObject.activeSelf) ToggleEndingCredits();
            _endingRoot.SetActive(true);
            HudController.Instance?.SetModalSuppressed(true);
            GameState.EnsureInstance().RefreshCursor();
            if (_endingRevealRoutine != null) StopCoroutine(_endingRevealRoutine);
            _endingRevealRoutine = StartCoroutine(EndingRevealRoutine());
        }

        // =========================================================================
        // ACTIONS
        // =========================================================================

        private void OnContinueClicked()
        {
            GameState gs = GameState.EnsureInstance();
            SaveData data = SaveManager.LoadSaveData();
            if (data != null) gs.LoadFromSaveData(data);
            LaunchGameplay(gs.CurrentCheckpoint, freshPreset: false);
        }

        private void OnNewGameClicked()
        {
            GameState gs = GameState.EnsureInstance();
            gs.ResetForNewGame(CheckpointId.Intro);
            LaunchGameplay(CheckpointId.Intro, freshPreset: true);
        }

        private void OnChapterClicked(CheckpointId cp)
        {
            GameState gs = GameState.EnsureInstance();
            gs.ApplyCheckpointPreset(cp);
            LaunchGameplay(cp, freshPreset: true);
        }

        private void OnRestartCheckpointClicked()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetPaused(false);
            LaunchGameplay(gs.CurrentCheckpoint, freshPreset: false);
        }

        private void OnReturnToMainMenuClicked()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetPaused(false);
            gs.SetMainMenuState(true);
            SceneManager.LoadScene(mainMenuSceneName);
        }

        private void LaunchGameplay(CheckpointId checkpoint, bool freshPreset)
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetPaused(false);
            gs.SetMainMenuState(false);
            if (freshPreset) gs.ApplyCheckpointPreset(checkpoint);
            gs.SetFlag("launch_from_menu");
            SceneManager.LoadScene(gameplaySceneName);
        }

        private static void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =========================================================================
        // DETERMINISTIC MENU CONSTRUCTION (GENEROUS PADDING, ZERO CLIPPING)
        // =========================================================================

        private void BuildAllMenus()
        {
            _canvas = CretaceaUiFactory.CreateRootCanvas("[MenuCanvas]", sortOrder: 40);
            _canvas.transform.SetParent(transform, false);
            RectTransform root = _canvas.GetComponent<RectTransform>();

            BuildMainMenu(root);
            BuildPauseMenu(root);
            BuildSettingsModal(root);
            BuildControlsModal(root);
            BuildChapterSelectModal(root);
            BuildDocumentModal(root);
            BuildDeathOverlay(root);
            BuildEndingOverlay(root);
        }

        private void BuildMainMenu(RectTransform root)
        {
            // Left-side dark gradient over the live 3D storm scene (no hard slab), logo lockup, list menu, footer.
            RectTransform host = CretaceaUiFactory.CreatePanel(root, "MainMenuRoot", Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _mainMenuRoot = host.gameObject;

            RectTransform shade = CretaceaUiFactory.CreatePanel(host, "LeftShade", new Color(0.016f, 0.014f, 0.012f, 0.90f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(1180f, 0f), raycastTarget: true);
            shade.GetComponent<Image>().sprite = CretaceaUiFactory.GetSideGradientSprite();

            RectTransform lockup = CretaceaUiFactory.CreateAnchoredPanel(host, "LogoLockup", Color.clear,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(96f, -96f), new Vector2(720f, 190f));
            Texture2D logoTex = Resources.Load<Texture2D>("UI/Brand/park_logo");
            if (logoTex != null)
            {
                var logoGo = new GameObject("ParkLogo", typeof(RectTransform), typeof(RawImage));
                RectTransform lrt = logoGo.GetComponent<RectTransform>();
                lrt.SetParent(lockup, false);
                lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
                lrt.pivot = new Vector2(0f, 0.5f);
                lrt.anchoredPosition = Vector2.zero;
                lrt.sizeDelta = new Vector2(156f, 156f);
                RawImage logo = logoGo.GetComponent<RawImage>();
                logo.texture = logoTex;
                logo.raycastTarget = false;
            }
            // Compact lockup: it ends left of x ~600 so the atrium's T. rex skull (the scene's subject) stays clear.
            float tx = logoTex != null ? 180f : 0f;
            TextMeshProUGUI title = CretaceaUiFactory.CreateText(lockup, "GameTitle", "CRETACEA", 80f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.BottomLeft);
            title.characterSpacing = 6f;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.rectTransform.offsetMin = new Vector2(tx, 88f);
            title.rectTransform.offsetMax = new Vector2(0f, -8f);
            TextMeshProUGUI sub = CretaceaUiFactory.CreateText(lockup, "GameSubtitle", "BLACKOUT", 50f, CretaceaUiFactory.AccentAmber, UiFont.Stencil, TextAlignmentOptions.TopLeft);
            sub.characterSpacing = 15f;
            sub.textWrappingMode = TextWrappingModes.NoWrap;
            sub.rectTransform.offsetMin = new Vector2(tx + 3f, 0f);
            sub.rectTransform.offsetMax = new Vector2(0f, -110f);

            TextMeshProUGUI tag = CretaceaUiFactory.CreateText(host, "Tagline", "ISLA TORMENTA  ·  SURVIVAL HORROR", 21f, CretaceaUiFactory.TextMuted, UiFont.Mono, TextAlignmentOptions.TopLeft);
            tag.characterSpacing = 3f;
            tag.rectTransform.anchorMin = tag.rectTransform.anchorMax = new Vector2(0f, 1f);
            tag.rectTransform.pivot = new Vector2(0f, 1f);
            tag.rectTransform.anchoredPosition = new Vector2(100f, -306f);
            tag.rectTransform.sizeDelta = new Vector2(720f, 30f);

            RectTransform rule = CretaceaUiFactory.CreateAnchoredPanel(host, "Rule", new Color(CretaceaUiFactory.AccentAmber.r, CretaceaUiFactory.AccentAmber.g, CretaceaUiFactory.AccentAmber.b, 0.65f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(100f, -352f), new Vector2(64f, 2f));
            rule.name = "Rule";

            var btnStack = new GameObject("ButtonStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform stackRt = btnStack.GetComponent<RectTransform>();
            stackRt.SetParent(host, false);
            stackRt.anchorMin = stackRt.anchorMax = new Vector2(0f, 1f);
            stackRt.pivot = new Vector2(0f, 1f);
            stackRt.anchoredPosition = new Vector2(76f, -396f);
            stackRt.sizeDelta = new Vector2(560f, 420f);

            VerticalLayoutGroup vlg = btnStack.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            Vector2 bs = new Vector2(540f, 66f);
            _continueButton = CretaceaUiFactory.CreateListButton(stackRt, "BtnContinue", "CONTINUE", bs, OnContinueClicked, 34f);
            _newGameButton = CretaceaUiFactory.CreateListButton(stackRt, "BtnNewGame", "NEW GAME", bs, OnNewGameClicked, 34f);
            CretaceaUiFactory.CreateListButton(stackRt, "BtnChapters", "CHAPTER SELECT", bs, OpenChapterSelect, 34f);
            CretaceaUiFactory.CreateListButton(stackRt, "BtnSettings", "SETTINGS", bs, () => OpenSettings(_mainMenuRoot), 34f);
            CretaceaUiFactory.CreateListButton(stackRt, "BtnQuit", "QUIT", bs, OnQuitClicked, 34f);

            // Footer: controls and build
            TextMeshProUGUI footer = CretaceaUiFactory.CreateText(host, "Footer", CretaceaUiFactory.FormatKeyTokens("[Enter]  SELECT        [Esc]  BACK"), 19f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.BottomLeft);
            footer.characterSpacing = 2f;
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(0f, 0f);
            footer.rectTransform.pivot = new Vector2(0f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(100f, 56f);
            footer.rectTransform.sizeDelta = new Vector2(700f, 30f);

            TextMeshProUGUI ver = CretaceaUiFactory.CreateText(host, "Version", "v" + Application.version, 18f, new Color(0.55f, 0.52f, 0.46f, 1f), UiFont.Mono, TextAlignmentOptions.BottomRight);
            ver.rectTransform.anchorMin = ver.rectTransform.anchorMax = new Vector2(1f, 0f);
            ver.rectTransform.pivot = new Vector2(1f, 0f);
            ver.rectTransform.anchoredPosition = new Vector2(-48f, 40f);
            ver.rectTransform.sizeDelta = new Vector2(400f, 26f);

            _mainMenuRoot.SetActive(false);
        }

        /// <summary>Full-screen dim with a soft dark vignette (behind every modal card).</summary>
        private static RectTransform CreateBackdrop(RectTransform root, string name, float alpha)
        {
            RectTransform backdrop = CretaceaUiFactory.CreatePanel(root, name, new Color(0.012f, 0.011f, 0.010f, alpha),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: true);
            RectTransform vig = CretaceaUiFactory.CreatePanel(backdrop, "Vignette", new Color(0f, 0f, 0f, 0.55f),
                Vector2.zero, Vector2.one, new Vector2(-80f, -80f), new Vector2(80f, 80f));
            Image vi = vig.GetComponent<Image>();
            vi.sprite = VignetteSprite();
            return backdrop;
        }

        private static Sprite s_MenuVignette;
        private static Sprite VignetteSprite()
        {
            if (s_MenuVignette != null) return s_MenuVignette;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI_MenuVignette", wrapMode = TextureWrapMode.Clamp };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half, ny = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(nx * nx * 0.8f + ny * ny * 1.1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, CretaceaUiFactory.Smooth(0.45f, 1.25f, r)));
                }
            tex.Apply();
            s_MenuVignette = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_MenuVignette;
        }

        private static RectTransform CreateModalCard(RectTransform parent, string name, Vector2 size)
        {
            RectTransform card = CretaceaUiFactory.CreateAnchoredPanel(parent, name, CretaceaUiFactory.PanelBgSolid,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, raycastTarget: true);
            CretaceaUiFactory.StyleCard(card, 6f, 0.7f);
            CretaceaPanelGraphic g = card.GetComponent<CretaceaPanelGraphic>();
            g.shadowSpread = 28f;
            g.shadowOffset = new Vector2(0f, -10f);
            CretaceaUiFactory.DressKitCard(card, false, Vector2.one, Vector2.zero);   // worn grain, bevel, corner screws
            return card;
        }

        /// <summary>Card header: small amber kicker over a large condensed title, with a warm rule underneath.</summary>
        private static void CreateCardHeader(RectTransform card, string kicker, string title, float padX, float top)
        {
            TextMeshProUGUI k = CretaceaUiFactory.CreateText(card, "Kicker", kicker, 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            k.characterSpacing = 4f;
            k.rectTransform.anchorMin = new Vector2(0f, 1f); k.rectTransform.anchorMax = new Vector2(1f, 1f);
            k.rectTransform.offsetMin = new Vector2(padX, -top - 26f); k.rectTransform.offsetMax = new Vector2(-padX, -top);
            // round 4: the park-security kit's stencil face for the card title, the park badge in the top-right corner
            TextMeshProUGUI t = CretaceaUiFactory.CreateText(card, "Header", title, 50f, CretaceaUiFactory.TextPrimary, UiFont.Stencil, TextAlignmentOptions.TopLeft);
            t.characterSpacing = 3f;
            t.rectTransform.anchorMin = new Vector2(0f, 1f); t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(padX, -top - 86f); t.rectTransform.offsetMax = new Vector2(-padX, -top - 26f);
            CretaceaUiFactory.CreatePanel(card, "HeaderRule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(padX, -top - 100f), new Vector2(-padX, -top - 99f));
            CretaceaUiFactory.AddParkBadge(card, Vector2.one, new Vector2(-padX + 6f, -top + 6f), 84f);
        }

        private void BuildPauseMenu(RectTransform root)
        {
            RectTransform backdrop = CreateBackdrop(root, "PauseRoot", 0.62f);
            _pauseMenuRoot = backdrop.gameObject;

            RectTransform card = CreateModalCard(backdrop, "PauseCard", new Vector2(560f, 672f));
            CreateCardHeader(card, "CRETACEA: BLACKOUT", "PAUSED", 56f, 46f);

            _pauseChapterText = CretaceaUiFactory.CreateText(card, "Chapter", string.Empty, 24f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);   // the tablet's chapter style
            _pauseChapterText.characterSpacing = 2.5f;
            _pauseChapterText.rectTransform.anchorMin = new Vector2(0f, 1f); _pauseChapterText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _pauseChapterText.rectTransform.offsetMin = new Vector2(56f, -190f); _pauseChapterText.rectTransform.offsetMax = new Vector2(-56f, -160f);

            var stack = new GameObject("Buttons", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform srt = stack.GetComponent<RectTransform>();
            srt.SetParent(card, false);
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(56f, 70f);
            srt.offsetMax = new Vector2(-56f, -214f);
            VerticalLayoutGroup vlg = stack.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            _resumeButton = CretaceaUiFactory.CreateButton(srt, "BtnResume", "RESUME", new Vector2(448f, 60f), ResumeGame, 25f, accent: true);
            CretaceaUiFactory.CreateButton(srt, "BtnRestart", "RESTART CHECKPOINT", new Vector2(448f, 60f), OnRestartCheckpointClicked, 25f);
            CretaceaUiFactory.CreateButton(srt, "BtnSettings", "SETTINGS", new Vector2(448f, 60f), () => OpenSettings(_pauseMenuRoot), 25f);
            CretaceaUiFactory.CreateButton(srt, "BtnControls", "CONTROLS", new Vector2(448f, 60f), () => OpenControls(_pauseMenuRoot), 25f);
            CretaceaUiFactory.CreateButton(srt, "BtnMenu", "MAIN MENU", new Vector2(448f, 60f), OnReturnToMainMenuClicked, 25f);

            TextMeshProUGUI hint = CretaceaUiFactory.CreateText(card, "Hint", CretaceaUiFactory.FormatKeyTokens("[Esc]  RESUME"), 19f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.BottomLeft);
            hint.characterSpacing = 2f;
            hint.rectTransform.offsetMin = new Vector2(56f, 28f);
            hint.rectTransform.offsetMax = new Vector2(-56f, -40f);

            _pauseMenuRoot.SetActive(false);
        }

        private void BuildSettingsModal(RectTransform root)
        {
            RectTransform backdrop = CreateBackdrop(root, "SettingsRoot", 0.70f);
            _settingsRoot = backdrop.gameObject;

            RectTransform modal = CreateModalCard(backdrop, "SettingsCard", new Vector2(1360f, 820f));
            CreateCardHeader(modal, "CRETACEA: BLACKOUT", "SETTINGS", 60f, 44f);

            RectTransform leftCol = CreateSettingsColumn(modal, "Column_DisplayAudio", "DISPLAY & AUDIO",
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(60f, 122f), new Vector2(-36f, -176f));

            CretaceaUiFactory.CreatePanel(modal, "ColumnDivider", CretaceaUiFactory.BorderSubtle,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 136f), new Vector2(1f, -180f));

            RectTransform rightCol = CreateSettingsColumn(modal, "Column_ControlsAccess", "CONTROLS & ACCESSIBILITY",
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(36f, 122f), new Vector2(-60f, -176f));

            SettingsData s = SaveManager.Settings;
            _firstSettingsControl = CretaceaUiFactory.CreateSliderRow(leftCol, "Brightness", -1.5f, 1.5f, s.brightnessEV, v =>
            {
                v = Mathf.Round(v * 10f) / 10f;
                s.brightnessEV = v;
                Cretacea.Core.AtmosphereController.UserBrightnessEV = v;
                SaveManager.SaveSettings(s);
            }, v => { int r = Mathf.RoundToInt(v / 0.3f); return (r < 0 ? "\u2212" : "+") + Mathf.Abs(r).ToString("0"); });   // -5 .. +5, +0 = as designed
            CretaceaUiFactory.MakeSliderBipolar(_firstSettingsControl as Slider);
            CretaceaUiFactory.CreateSliderRow(leftCol, "Field of View (vertical)", CretaceaSettingsBootstrap.FovMin, CretaceaSettingsBootstrap.FovMax,
                s.fieldOfView, v => { s.fieldOfView = Mathf.Round(v); SaveManager.SaveSettings(s); }, v => $"{Mathf.RoundToInt(v)}°");
            CretaceaUiFactory.CreateToggleRow(leftCol, "Fullscreen", Screen.fullScreen, v => { Screen.fullScreen = v; });
            CretaceaUiFactory.CreateSliderRow(leftCol, "Master Volume", 0f, 1f, s.masterVolume, v => { s.masterVolume = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateSliderRow(leftCol, "Music", 0f, 1f, s.musicVolume, v => { s.musicVolume = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateSliderRow(leftCol, "Effects & Ambience", 0f, 1f, s.sfxVolume, v => { s.sfxVolume = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateSliderRow(leftCol, "Voice", 0f, 1f, s.voVolume, v => { s.voVolume = v; SaveManager.SaveSettings(s); });

            CretaceaUiFactory.CreateSliderRow(rightCol, "Mouse Sensitivity", 0.25f, 3.0f, s.mouseSensitivity, v => { s.mouseSensitivity = v; SaveManager.SaveSettings(s); }, v => $"{v:0.00}×");
            CretaceaUiFactory.CreateSliderRow(rightCol, "Gamepad Sensitivity", 0.25f, 3.0f, s.gamepadSensitivity, v => { s.gamepadSensitivity = v; SaveManager.SaveSettings(s); }, v => $"{v:0.00}×");
            CretaceaUiFactory.CreateToggleRow(rightCol, "Invert Look Y", s.invertY, v => { s.invertY = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateToggleRow(rightCol, "Subtitles", s.subtitlesEnabled, v => { s.subtitlesEnabled = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateToggleRow(rightCol, "Sound Captions", s.soundCaptionsEnabled, v => { s.soundCaptionsEnabled = v; SaveManager.SaveSettings(s); });
            CretaceaUiFactory.CreateToggleRow(rightCol, "Gamepad Rumble", s.gamepadRumble, v => { s.gamepadRumble = v; SaveManager.SaveSettings(s); });

            // Footer
            CretaceaUiFactory.CreatePanel(modal, "FooterRule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(60f, 104f), new Vector2(-60f, 105f));
            TextMeshProUGUI note = CretaceaUiFactory.CreateText(modal, "BrightnessNote",
                "Brightness +0 is the intended look. Raise it if nights hide too much.", 19f, CretaceaUiFactory.TextMuted, UiFont.Body, TextAlignmentOptions.MidlineLeft);
            note.rectTransform.anchorMin = new Vector2(0f, 0f); note.rectTransform.anchorMax = new Vector2(1f, 0f);
            note.rectTransform.offsetMin = new Vector2(60f, 58f); note.rectTransform.offsetMax = new Vector2(-620f, 92f);
            TextMeshProUGUI escHint = CretaceaUiFactory.CreateText(modal, "Hint", CretaceaUiFactory.FormatKeyTokens("[Esc]  BACK"), 19f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            escHint.characterSpacing = 2f;
            escHint.rectTransform.anchorMin = new Vector2(0f, 0f); escHint.rectTransform.anchorMax = new Vector2(1f, 0f);
            escHint.rectTransform.offsetMin = new Vector2(60f, 20f); escHint.rectTransform.offsetMax = new Vector2(-620f, 54f);

            Button controlsBtn = CretaceaUiFactory.CreateButton(modal, "BtnControls", "CONTROLS", new Vector2(220f, 58f), () => OpenControls(_settingsRoot), 24f);
            RectTransform cbrt = controlsBtn.GetComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(1f, 0f);
            cbrt.anchorMax = new Vector2(1f, 0f);
            cbrt.pivot = new Vector2(1f, 0f);
            cbrt.anchoredPosition = new Vector2(-360f, 30f);

            Button backBtn = CretaceaUiFactory.CreateButton(modal, "BtnBack", "SAVE & BACK", new Vector2(280f, 58f), () =>
            {
                SaveManager.SaveSettings(s);
                if (_returnMenuAfterSettings == _pauseMenuRoot) ShowPauseMenu();
                else ShowMainMenu();
            }, 24f, accent: true);
            RectTransform brt = backBtn.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-60f, 30f);

            _settingsRoot.SetActive(false);
        }

        // Action, keyboard keys, controller buttons. Key names with art in the key atlas show the keycap art;
        // the rest (WASD, MOUSE, sticks, CTRL) get a text keycap of the same style.
        private static readonly string[][] ControlRows =
        {
            new[] { "Move", "WASD", "L STICK" },
            new[] { "Look", "MOUSE", "R STICK" },
            new[] { "Sprint (hold)", "SHIFT", "LB|L3" },
            new[] { "Crouch / hide", "C|CTRL", "B" },
            new[] { "Interact / hold to use", "E", "X" },
            new[] { "Flashlight", "F", "Y" },
            new[] { "Field tablet", "TAB", "VIEW" },
            new[] { "Pause", "ESC", "MENU" },
            new[] { "Skip cutscene (hold)", "SPACE", "X" },
            new[] { "Menus: select / back", "ENTER|ESC", "A|B" },
        };

        private void BuildControlsModal(RectTransform root)
        {
            RectTransform backdrop = CreateBackdrop(root, "ControlsRoot", 0.70f);
            _controlsRoot = backdrop.gameObject;

            const float rowH = 54f, top = 214f;
            float cardH = top + ControlRows.Length * rowH + 130f;
            RectTransform modal = CreateModalCard(backdrop, "ControlsCard", new Vector2(1120f, cardH));
            CreateCardHeader(modal, "CRETACEA: BLACKOUT", "CONTROLS", 60f, 44f);

            float colKey = 520f, colPad = 800f;
            void ColHeader(string text, float x)
            {
                TextMeshProUGUI h = CretaceaUiFactory.CreateText(modal, "Col_" + text, text, 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
                h.characterSpacing = 4f;
                h.rectTransform.anchorMin = h.rectTransform.anchorMax = new Vector2(0f, 1f);
                h.rectTransform.pivot = new Vector2(0f, 1f);
                h.rectTransform.anchoredPosition = new Vector2(x, -170f);
                h.rectTransform.sizeDelta = new Vector2(300f, 30f);
            }
            ColHeader("ACTION", 60f);
            ColHeader("KEYBOARD & MOUSE", colKey);
            ColHeader("XBOX CONTROLLER", colPad);

            for (int i = 0; i < ControlRows.Length; i++)
            {
                float y = -top - i * rowH;
                if (i % 2 == 0)
                {
                    RectTransform band = CretaceaUiFactory.CreatePanel(modal, $"Band_{i}", new Color(0.094f, 0.088f, 0.078f, 1f),   // opaque (24,22,20) on the (13,13,13) card: about a quarter of the old band (the UI blends in linear, so 0.035 alpha read as solid tan 56,51,43)
                        new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, y - rowH), new Vector2(-44f, y));
                    CretaceaUiFactory.StyleRounded(band, 4f);
                }
                TextMeshProUGUI name = CretaceaUiFactory.CreateText(modal, $"Action_{i}", ControlRows[i][0], 23f, CretaceaUiFactory.TextPrimary, UiFont.BodyMedium, TextAlignmentOptions.MidlineLeft);
                name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 1f);
                name.rectTransform.pivot = new Vector2(0f, 1f);
                name.rectTransform.anchoredPosition = new Vector2(60f, y);
                name.rectTransform.sizeDelta = new Vector2(440f, rowH);
                PlaceKeys(modal, ControlRows[i][1], new Vector2(colKey, y - rowH * 0.5f));
                PlaceKeys(modal, ControlRows[i][2], new Vector2(colPad, y - rowH * 0.5f));
            }

            CretaceaUiFactory.CreatePanel(modal, "FooterRule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(60f, 104f), new Vector2(-60f, 105f));
            TextMeshProUGUI hint = CretaceaUiFactory.CreateText(modal, "Hint", CretaceaUiFactory.FormatKeyTokens("[Esc]  BACK"), 19f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            hint.characterSpacing = 2f;
            hint.rectTransform.anchorMin = new Vector2(0f, 0f); hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.offsetMin = new Vector2(60f, 30f); hint.rectTransform.offsetMax = new Vector2(-400f, 88f);

            _controlsBackButton = CretaceaUiFactory.CreateButton(modal, "BtnBack", "BACK", new Vector2(220f, 58f), CloseControls, 24f, accent: true);
            RectTransform brt = _controlsBackButton.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-60f, 30f);

            _controlsRoot.SetActive(false);
        }

        /// <summary>One or more keycaps ("C|CTRL") left-aligned at a row's centre line, with a muted slash between.</summary>
        private static void PlaceKeys(RectTransform parent, string keys, Vector2 leftCentre)
        {
            const float capH = 38f;
            float x = leftCentre.x;
            string[] parts = keys.Split('|');
            for (int k = 0; k < parts.Length; k++)
            {
                if (k > 0)
                {
                    TextMeshProUGUI slash = CretaceaUiFactory.CreateText(parent, "Slash", "/", 22f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.Center);
                    slash.rectTransform.anchorMin = slash.rectTransform.anchorMax = new Vector2(0f, 1f);
                    slash.rectTransform.pivot = new Vector2(0f, 0.5f);
                    slash.rectTransform.anchoredPosition = new Vector2(x + 4f, leftCentre.y);
                    slash.rectTransform.sizeDelta = new Vector2(20f, capH);
                    x += 28f;
                }
                CretaceaUiFactory.Keycap cap = CretaceaUiFactory.CreateKeycap(parent, "Key_" + parts[k], capH);
                cap.Root.anchorMin = cap.Root.anchorMax = new Vector2(0f, 1f);
                cap.Root.pivot = new Vector2(0f, 0.5f);
                cap.Root.anchoredPosition = new Vector2(x, leftCentre.y);
                // Keyboard letters must never pick up the Xbox face-button art ("A" is pad_a in the atlas).
                cap.SetKey(parts[k]);
                x += cap.Root.sizeDelta.x;
            }
        }

        private static RectTransform CreateSettingsColumn(
            RectTransform parent,
            string name,
            string sectionTitle,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            RectTransform colRoot = CretaceaUiFactory.CreatePanel(parent, name, Color.clear,
                anchorMin, anchorMax, offsetMin, offsetMax);

            TextMeshProUGUI secHdr = CretaceaUiFactory.CreateText(colRoot, "SectionHeader", sectionTitle, 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            secHdr.characterSpacing = 4f;
            secHdr.rectTransform.anchorMin = new Vector2(0f, 1f);
            secHdr.rectTransform.anchorMax = new Vector2(1f, 1f);
            secHdr.rectTransform.offsetMin = new Vector2(0f, -30f);
            secHdr.rectTransform.offsetMax = Vector2.zero;

            var list = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform lrt = list.GetComponent<RectTransform>();
            lrt.SetParent(colRoot, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = new Vector2(0f, -42f);
            VerticalLayoutGroup vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return lrt;
        }

        private void BuildChapterSelectModal(RectTransform root)
        {
            RectTransform backdrop = CreateBackdrop(root, "ChapterSelectRoot", 0.70f);
            _chapterSelectRoot = backdrop.gameObject;

            RectTransform modal = CreateModalCard(backdrop, "ChaptersCard", new Vector2(780f, 900f));
            CreateCardHeader(modal, "CRETACEA: BLACKOUT", "CHAPTER SELECT", 56f, 44f);

            var list = new GameObject("ChapterList", typeof(RectTransform), typeof(VerticalLayoutGroup));
            RectTransform lrt = list.GetComponent<RectTransform>();
            lrt.SetParent(modal, false);
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(56f, 118f);
            lrt.offsetMax = new Vector2(-56f, -172f);
            VerticalLayoutGroup vlg = list.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            CheckpointId[] all = (CheckpointId[])Enum.GetValues(typeof(CheckpointId));
            _chapterButtons = new Button[all.Length];
            for (int i = 0; i < all.Length; i++)
            {
                CheckpointId cp = all[i];
                string full = GameState.GetChapterTitle(cp);
                string kicker = full, name = string.Empty;
                int dash = full.IndexOf('—');
                if (dash > 0) { kicker = full.Substring(0, dash).Trim(); name = full.Substring(dash + 1).Trim(); }
                Button b = CretaceaUiFactory.CreateButton(lrt, $"Ch_{cp}", name.Length > 0 ? name : kicker, new Vector2(668f, 70f), () => OnChapterClicked(cp), 26f);
                TextMeshProUGUI lbl = b.GetComponentInChildren<TextMeshProUGUI>();
                lbl.rectTransform.offsetMin = new Vector2(176f, 2f);
                TextMeshProUGUI k = CretaceaUiFactory.CreateText(b.transform, "Kicker", name.Length > 0 ? kicker : string.Empty, 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.MidlineLeft);
                k.characterSpacing = 3f;
                k.textWrappingMode = TextWrappingModes.NoWrap;
                k.rectTransform.offsetMin = new Vector2(24f, 2f);
                k.rectTransform.offsetMax = new Vector2(-480f, -2f);
                _chapterButtons[i] = b;
            }

            Button back = CretaceaUiFactory.CreateButton(modal, "BtnBack", "BACK", new Vector2(220f, 56f), ShowMainMenu, 24f);
            RectTransform brt = back.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-56f, 36f);

            TextMeshProUGUI hint = CretaceaUiFactory.CreateText(modal, "Hint", "Every chapter can be replayed from its checkpoint.", 19f, CretaceaUiFactory.TextMuted, UiFont.Body, TextAlignmentOptions.MidlineLeft);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f); hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.offsetMin = new Vector2(56f, 36f); hint.rectTransform.offsetMax = new Vector2(-300f, 92f);

            _chapterSelectRoot.SetActive(false);
        }

        private void RefreshChapterButtons()
        {
            if (_chapterButtons == null) return;
            // All 7 chapters remain playable for testing & Chapter Select replayability
            for (int i = 0; i < _chapterButtons.Length; i++)
            {
                if (_chapterButtons[i] != null) _chapterButtons[i].interactable = true;
            }
        }

        private const float DocPageMaxW = 714f;
        private const float DocPageMaxH = 980f;
        private const float DocColumnW = 760f;
        private const float DocColumnH = 980f;
        private const float DocGap = 56f;
        private const float DocPadX = 52f;

        private void BuildDocumentModal(RectTransform root)
        {
            RectTransform backdrop = CreateBackdrop(root, "DocumentModalRoot", 0.86f);
            _documentModalRoot = backdrop.gameObject;

            // The page floats on the dimmed world with its own shadow (~980 px tall at 1080p, ~50 px margins);
            // only the reading column next to it is framed. OnShowDocumentRequested centres the pair.
            RectTransform pageHolder = CretaceaUiFactory.CreateAnchoredPanel(backdrop, "PageHolder", Color.clear,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-(DocGap + DocColumnW) * 0.5f, 0f), new Vector2(DocPageMaxW, DocPageMaxH));
            RectTransform pageShadow = CretaceaUiFactory.CreatePanel(pageHolder, "PageShadow", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleCard(pageShadow, 2f, 0.9f, new Color(0f, 0f, 0f, 0f));
            CretaceaPanelGraphic psg = pageShadow.GetComponent<CretaceaPanelGraphic>();
            psg.shadowSpread = 26f;
            psg.shadowOffset = new Vector2(0f, -8f);
            _docPageShadow = pageShadow;

            RectTransform imgRt = CretaceaUiFactory.CreatePanel(pageHolder, "DocPageImage", Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _docModalImage = imgRt.GetComponent<Image>();
            _docModalImage.preserveAspect = true;

            RectTransform card = CreateModalCard(backdrop, "DocColumn", new Vector2(DocColumnW, DocColumnH));
            card.anchoredPosition = new Vector2((DocPageMaxW + DocGap) * 0.5f, 0f);
            _docColumn = card;

            _docKickerText = CretaceaUiFactory.CreateText(card, "DocKicker", "DOCUMENT", 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docKickerText.characterSpacing = 4f;
            _docTitleText = CretaceaUiFactory.CreateText(card, "DocTitle", string.Empty, 44f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docTitleText.characterSpacing = 3f;
            _docTitleText.lineSpacing = -8f;
            _docRule = CretaceaUiFactory.CreatePanel(card, "Rule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _docModalSummaryText = CretaceaUiFactory.CreateText(card, "Summary", string.Empty, 24f, CretaceaUiFactory.TextPrimary, UiFont.Body, TextAlignmentOptions.TopLeft);
            _docModalSummaryText.lineSpacing = 8f;

            _docTranscriptRule = CretaceaUiFactory.CreatePanel(card, "TranscriptRule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            _docTranscriptKicker = CretaceaUiFactory.CreateText(card, "TranscriptKicker", "TRANSCRIPT", 19f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docTranscriptKicker.characterSpacing = 4f;
            _docTranscriptText = CretaceaUiFactory.CreateText(card, "Transcript", string.Empty, 22f, new Color(0.86f, 0.82f, 0.74f, 1f), UiFont.Mono, TextAlignmentOptions.TopLeft);
            _docTranscriptText.lineSpacing = 8f;
            _docTranscriptText.enableAutoSizing = true;
            _docTranscriptText.fontSizeMin = 18f;
            _docTranscriptText.fontSizeMax = 22f;
            _docTranscriptText.overflowMode = TextOverflowModes.Ellipsis;

            Button closeBtn = CretaceaUiFactory.CreateButton(card, "BtnCloseDoc", "CONTINUE", new Vector2(300f, 58f), CloseDocumentModal, 24f, accent: true);
            RectTransform crt = closeBtn.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(1f, 0f);
            crt.anchoredPosition = new Vector2(-DocPadX, 44f);
            _docCloseLabel = closeBtn.GetComponentInChildren<TextMeshProUGUI>();
            // Key first, like every other prompt: [E]  CONTINUE
            _docCloseKey = CretaceaUiFactory.CreateKeycap(closeBtn.transform, "Key", 36f);
            _docCloseKey.Root.anchorMin = _docCloseKey.Root.anchorMax = new Vector2(0f, 0.5f);
            _docCloseKey.Root.pivot = new Vector2(0f, 0.5f);
            _docCloseKey.Root.anchoredPosition = new Vector2(14f, 0f);
            _docCloseKey.SetKey("E");
            _docCloseLabel.alignment = TextAlignmentOptions.MidlineLeft;
            _docCloseLabel.rectTransform.offsetMin = new Vector2(14f + 36f + 16f, 2f);

            LayoutDocumentColumn(false);
            _documentModalRoot.SetActive(false);
        }

        /// <summary>Stacks kicker, title, rule, summary and transcript top-down in the reading column.</summary>
        private void LayoutDocumentColumn(bool hasTranscript)
        {
            if (_docColumn == null) return;
            float inner = DocColumnW - DocPadX * 2f;
            float y = -48f;
            void Place(RectTransform rt, float h)
            {
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(DocPadX, y - h); rt.offsetMax = new Vector2(-DocPadX, y);
                y -= h;
            }
            Place(_docKickerText.rectTransform, 28f); y -= 8f;
            float titleH = Mathf.Max(48f, _docTitleText.GetPreferredValues(_docTitleText.text, inner, 0f).y);
            Place(_docTitleText.rectTransform, titleH); y -= 22f;
            Place(_docRule, 1f); y -= 24f;
            float sumH = string.IsNullOrEmpty(_docModalSummaryText.text) ? 0f : _docModalSummaryText.GetPreferredValues(_docModalSummaryText.text, inner, 0f).y;
            Place(_docModalSummaryText.rectTransform, sumH);

            _docTranscriptRule.gameObject.SetActive(hasTranscript);
            _docTranscriptKicker.gameObject.SetActive(hasTranscript);
            _docTranscriptText.gameObject.SetActive(hasTranscript);
            if (!hasTranscript) return;
            if (sumH > 0f) { y -= 34f; Place(_docTranscriptRule, 1f); y -= 20f; }
            else _docTranscriptRule.gameObject.SetActive(false);
            Place(_docTranscriptKicker.rectTransform, 28f); y -= 10f;
            float bottom = -DocColumnH + 44f + 58f + 28f;   // above the CONTINUE button
            Place(_docTranscriptText.rectTransform, Mathf.Max(60f, y - bottom));
        }

        private void BuildDeathOverlay(RectTransform root)
        {
            RectTransform backdrop = CretaceaUiFactory.CreatePanel(root, "DeathRoot", new Color(0.02f, 0.008f, 0.006f, 0.80f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: true);
            _deathRoot = backdrop.gameObject;
            RectTransform vig = CretaceaUiFactory.CreatePanel(backdrop, "BloodVignette", new Color(0.26f, 0.02f, 0.014f, 0.72f),
                Vector2.zero, Vector2.one, new Vector2(-60f, -60f), new Vector2(60f, 60f));
            vig.GetComponent<Image>().sprite = VignetteSprite();

            RectTransform plate = CretaceaUiFactory.CreateAnchoredPanel(backdrop, "SoftPlate", new Color(0f, 0f, 0f, 0.6f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(1500f, 560f));
            plate.GetComponent<Image>().sprite = CretaceaUiFactory.GetSoftPlateSprite();

            TextMeshProUGUI title = CretaceaUiFactory.CreateText(backdrop, "DeathTitle", "YOU WERE KILLED", 96f, CretaceaUiFactory.AccentDanger, UiFont.Heading, TextAlignmentOptions.Bottom);
            title.characterSpacing = 14f;
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(1400f, 130f);
            title.rectTransform.anchoredPosition = new Vector2(0f, 150f);

            _deathSubText = CretaceaUiFactory.CreateText(backdrop, "DeathSub", string.Empty, 22f, CretaceaUiFactory.TextMuted, UiFont.Mono, TextAlignmentOptions.Top);
            _deathSubText.rectTransform.anchorMin = _deathSubText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _deathSubText.rectTransform.sizeDelta = new Vector2(1200f, 40f);
            _deathSubText.rectTransform.anchoredPosition = new Vector2(0f, 62f);

            _deathRetryButton = CretaceaUiFactory.CreateButton(backdrop, "BtnRetry", "RETRY CHECKPOINT", new Vector2(380f, 62f), OnRestartCheckpointClicked, 25f, accent: true);
            RectTransform rrt = _deathRetryButton.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0.5f, 0.5f);
            rrt.anchorMax = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(0f, -40f);

            Button menu = CretaceaUiFactory.CreateButton(backdrop, "BtnMenu", "MAIN MENU", new Vector2(380f, 58f), OnReturnToMainMenuClicked, 24f);
            RectTransform mrt = menu.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.5f, 0.5f);
            mrt.anchorMax = new Vector2(0.5f, 0.5f);
            mrt.anchoredPosition = new Vector2(0f, -116f);

            _deathRoot.SetActive(false);
        }

        private void BuildEndingOverlay(RectTransform root)
        {
            // The dawn coast is the game's payoff image: no flat dim over it. A light neutral gradient plate
            // (about 0.22 under the wordmark, 0.12 across the horizon, 0.3 at the bottom), a soft 0.28 vignette and
            // a darker pool only behind the stats card. Everything fades in after the clean shot has held 2.5 s.
            RectTransform backdrop = CretaceaUiFactory.CreatePanel(root, "EndingRoot", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: true);
            _endingRoot = backdrop.gameObject;
            _endingGroup = _endingRoot.AddComponent<CanvasGroup>();
            RectTransform plate = CretaceaUiFactory.CreatePanel(backdrop, "Plate", new Color(0.012f, 0.011f, 0.010f, 1f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            plate.GetComponent<Image>().sprite = EndingPlateSprite();
            RectTransform vig = CretaceaUiFactory.CreatePanel(backdrop, "Vignette", new Color(0f, 0f, 0f, 0.28f),
                Vector2.zero, Vector2.one, new Vector2(-80f, -80f), new Vector2(80f, 80f));
            vig.GetComponent<Image>().sprite = VignetteSprite();

            // A soft dark pool behind the badge and the wordmark: on the bright morning sky the amber 'BLACKOUT' had a
            // luminance contrast of 1.15:1 and read by hue alone (review 3).
            RectTransform titlePool = CretaceaUiFactory.CreatePanel(backdrop, "TitlePool", new Color(0f, 0f, 0f, 0.42f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            titlePool.pivot = new Vector2(0.5f, 1f);
            titlePool.anchoredPosition = new Vector2(0f, -20f);
            titlePool.sizeDelta = new Vector2(860f, 430f);
            titlePool.GetComponent<Image>().sprite = EndingPoolSprite();
            titlePool.GetComponent<Image>().raycastTarget = false;

            Texture2D logoTex = Resources.Load<Texture2D>("UI/Brand/park_logo");
            if (logoTex != null)
            {
                var logoGo = new GameObject("ParkLogo", typeof(RectTransform), typeof(RawImage));
                RectTransform lrt = logoGo.GetComponent<RectTransform>();
                lrt.SetParent(backdrop, false);
                lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 1f);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0f, -70f);
                lrt.sizeDelta = new Vector2(150f, 150f);
                RawImage logo = logoGo.GetComponent<RawImage>();
                logo.texture = logoTex;
                logo.raycastTarget = false;
            }

            TextMeshProUGUI title = CretaceaUiFactory.CreateText(backdrop, "EndTitle", "CRETACEA", 76f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.Top);
            title.characterSpacing = 10f;
            title.rectTransform.anchorMin = new Vector2(0f, 1f); title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(80f, -330f); title.rectTransform.offsetMax = new Vector2(-80f, -236f);
            TextMeshProUGUI sub = CretaceaUiFactory.CreateText(backdrop, "EndSub", "BLACKOUT", 46f, CretaceaUiFactory.AccentAmber, UiFont.Stencil, TextAlignmentOptions.Top);
            sub.characterSpacing = 18f;
            sub.rectTransform.anchorMin = new Vector2(0f, 1f); sub.rectTransform.anchorMax = new Vector2(1f, 1f);
            sub.rectTransform.offsetMin = new Vector2(80f, -392f); sub.rectTransform.offsetMax = new Vector2(-80f, -330f);

            // Stats card, low on the sea and well clear of the sun on the horizon: the headline is the largest
            // line on it, then three stat tiles. Credits live on their own card (CREDITS button).
            Vector2 cardPos = new Vector2(0f, 194f), cardSize = new Vector2(1000f, 290f);
            RectTransform pool = CretaceaUiFactory.CreatePanel(backdrop, "CardPool", new Color(0f, 0f, 0f, 0.5f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            pool.pivot = new Vector2(0.5f, 0f);
            pool.anchoredPosition = cardPos - new Vector2(0f, 110f);
            pool.sizeDelta = cardSize + new Vector2(520f, 220f);
            pool.GetComponent<Image>().sprite = EndingPoolSprite();

            RectTransform statsCard = CretaceaUiFactory.CreateAnchoredPanel(backdrop, "StatsCard", new Color(0.035f, 0.031f, 0.027f, 0.62f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), cardPos, cardSize);
            statsCard.pivot = new Vector2(0.5f, 0f);
            statsCard.anchoredPosition = cardPos;
            CretaceaUiFactory.StyleCard(statsCard, 6f, 0.6f);
            _endingStatsCard = statsCard;
            _endingHeadline = CretaceaUiFactory.CreateText(statsCard, "Headline", "YOU ESCAPED CRETACEA PARK", 54f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.Top);
            _endingHeadline.characterSpacing = 5f;
            _endingHeadline.rectTransform.offsetMin = new Vector2(40f, 0f);
            _endingHeadline.rectTransform.offsetMax = new Vector2(-40f, -26f);

            string[] statLabels = { "TIME", "DEATHS", "DOCUMENTS" };
            _endingStatValues = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                RectTransform tile = CretaceaUiFactory.CreateAnchoredPanel(statsCard, $"Stat_{i}", CretaceaUiFactory.PanelCard,
                    new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 300f, -112f), new Vector2(276f, 142f));
                tile.pivot = new Vector2(0.5f, 1f);
                CretaceaUiFactory.StyleCard(tile, 5f, 0f);
                TextMeshProUGUI lbl = CretaceaUiFactory.CreateText(tile, "Label", statLabels[i], 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.Top);
                lbl.characterSpacing = 4f;
                lbl.rectTransform.offsetMax = new Vector2(0f, -18f);
                TextMeshProUGUI val = CretaceaUiFactory.CreateText(tile, "Value", "-", 42f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.Bottom);
                val.characterSpacing = 2f;
                val.rectTransform.offsetMin = new Vector2(0f, 22f);
                _endingStatValues[i] = val;
            }

            // Credits card: same place as the stats card, shown instead of it.
            RectTransform creditsCard = CretaceaUiFactory.CreateAnchoredPanel(backdrop, "CreditsCard", new Color(0.035f, 0.031f, 0.027f, 0.72f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), cardPos, cardSize);
            creditsCard.pivot = new Vector2(0.5f, 0f);
            creditsCard.anchoredPosition = cardPos;
            CretaceaUiFactory.StyleCard(creditsCard, 6f, 0.6f);
            TextMeshProUGUI ck = CretaceaUiFactory.CreateText(creditsCard, "Kicker", "CREDITS", 22f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.Top);
            ck.characterSpacing = 6f;
            ck.rectTransform.offsetMax = new Vector2(0f, -26f);
            _endingStatsText = CretaceaUiFactory.CreateText(creditsCard, "Credits", string.Empty, 23f, CretaceaUiFactory.TextPrimary, UiFont.Body, TextAlignmentOptions.Center);
            _endingStatsText.lineSpacing = 10f;
            _endingStatsText.rectTransform.offsetMin = new Vector2(48f, 24f);
            _endingStatsText.rectTransform.offsetMax = new Vector2(-48f, -72f);
            _endingCreditsCard = creditsCard;
            creditsCard.gameObject.SetActive(false);

            _endingMenuButton = CretaceaUiFactory.CreateButton(backdrop, "BtnEndMenu", "RETURN TO MAIN MENU", new Vector2(420f, 62f), OnReturnToMainMenuClicked, 25f, accent: true);
            RectTransform mrt = _endingMenuButton.GetComponent<RectTransform>();
            mrt.anchorMin = new Vector2(0.5f, 0f);
            mrt.anchorMax = new Vector2(0.5f, 0f);
            mrt.pivot = new Vector2(0.5f, 0.5f);
            mrt.anchoredPosition = new Vector2(-116f, 130f);
            _endingCreditsButton = CretaceaUiFactory.CreateButton(backdrop, "BtnEndCredits", "CREDITS", new Vector2(200f, 62f), ToggleEndingCredits, 25f);
            RectTransform crt = _endingCreditsButton.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0.5f);
            crt.anchoredPosition = new Vector2(214f, 130f);
            Navigation nm = _endingMenuButton.navigation; nm.mode = Navigation.Mode.Explicit;
            nm.selectOnRight = _endingCreditsButton; nm.selectOnLeft = _endingCreditsButton; _endingMenuButton.navigation = nm;
            Navigation nc = _endingCreditsButton.navigation; nc.mode = Navigation.Mode.Explicit;
            nc.selectOnLeft = _endingMenuButton; nc.selectOnRight = _endingMenuButton; _endingCreditsButton.navigation = nc;

            _endingRoot.SetActive(false);
        }

        private void ToggleEndingCredits()
        {
            if (_endingCreditsCard == null || _endingStatsCard == null) return;
            bool show = !_endingCreditsCard.gameObject.activeSelf;
            _endingCreditsCard.gameObject.SetActive(show);
            _endingStatsCard.gameObject.SetActive(!show);
            TextMeshProUGUI lbl = _endingCreditsButton != null ? _endingCreditsButton.GetComponentInChildren<TextMeshProUGUI>() : null;
            if (lbl != null) lbl.text = show ? "STATS" : "CREDITS";
        }

        private const float EndingHoldSeconds = 2.5f, EndingFadeSeconds = 1.0f;

        /// <summary>Clean dawn shot first, then the plate, wordmark and card fade in and the card rises 28 px.</summary>
        private IEnumerator EndingRevealRoutine()
        {
            if (_endingGroup == null) yield break;
            _endingGroup.alpha = 0f;
            _endingGroup.interactable = false;
            Vector2 cardHome = _endingStatsCard != null ? _endingStatsCard.anchoredPosition : Vector2.zero;
            float t = 0f;
            while (t < EndingHoldSeconds) { t += Time.unscaledDeltaTime; yield return null; }
            t = 0f;
            while (t < EndingFadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                float k = CretaceaUiFactory.Smooth(0f, 1f, Mathf.Clamp01(t / EndingFadeSeconds));
                _endingGroup.alpha = k;
                if (_endingStatsCard != null) _endingStatsCard.anchoredPosition = cardHome + new Vector2(0f, -28f * (1f - k));
                yield return null;
            }
            _endingGroup.alpha = 1f;
            if (_endingStatsCard != null) _endingStatsCard.anchoredPosition = cardHome;
            _endingGroup.interactable = true;
            CretaceaUiFactory.Focus(_endingMenuButton);
            _endingRevealRoutine = null;
        }

        private static Sprite s_EndingPlate, s_EndingPool;

        /// <summary>Vertical alpha ramp for the ending plate: 0.22 at the top (wordmark), 0.12 across the horizon
        /// band (sun and its glint), 0.30 at the bottom (buttons).</summary>
        private static Sprite EndingPlateSprite()
        {
            if (s_EndingPlate != null) return s_EndingPlate;
            const int h = 128;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { name = "UI_EndingPlate", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;                  // 0 bottom, 1 top
                float a = v > 0.55f ? Mathf.Lerp(0.12f, 0.22f, CretaceaUiFactory.Smooth(0.55f, 1f, v))
                                    : Mathf.Lerp(0.30f, 0.12f, CretaceaUiFactory.Smooth(0f, 0.55f, v));
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            s_EndingPlate = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f));
            return s_EndingPlate;
        }

        /// <summary>Soft elliptical pool (1 in the middle, 0 at the rim) for the darker area behind the card.</summary>
        private static Sprite EndingPoolSprite()
        {
            if (s_EndingPool != null) return s_EndingPool;
            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UI_EndingPool", wrapMode = TextureWrapMode.Clamp };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f - half) / half, ny = (y + 0.5f - half) / half;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f - CretaceaUiFactory.Smooth(0.45f, 1f, r)));
                }
            tex.Apply();
            s_EndingPool = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return s_EndingPool;
        }

        /// <summary>Ending card: stats from GameState, credits from the summary text (lines after the stats line).</summary>
        private void FillEndingCard(string statsSummary)
        {
            GameState gs = GameState.Instance;
            if (_endingStatValues != null && gs != null)
            {
                _endingStatValues[0].text = GameState.FormatPlayTime(gs.PlayTimeSeconds);
                _endingStatValues[1].text = gs.DeathCount.ToString();
                _endingStatValues[2].text = $"{gs.CollectedDocumentCount} / {GameState.TotalDocumentCount}";
            }
            if (_endingStatsText == null) return;
            string summary = statsSummary ?? string.Empty;
            string[] paras = summary.Replace("\r", string.Empty).Split(new[] { "\n\n" }, StringSplitOptions.None);
            // Expected: headline / stats line / credits. Anything else is shown as it is.
            if (paras.Length >= 3)
            {
                if (_endingHeadline != null) _endingHeadline.text = paras[0].Trim().TrimEnd('.').ToUpperInvariant();
                _endingStatsText.text = string.Join("\n", paras, 2, paras.Length - 2).Trim()
                    .Replace("Built in Unity 6 HDRP for Isla Tormenta.", "Made with Unity 6 HDRP");
            }
            else _endingStatsText.text = summary;
        }

        // ---- Document titles (the event only carries ids) --------------------------------------------------

        private static string DocumentTitle(string docId)
        {
            switch (docId)
            {
                case "memo_switchover": return "Night Shift Switchover Memo";
                case "manual": return "1996 Preserve Evacuation Manual";
                case "keeper_note": return "Feed Prep Keeper's Note";
                case "jeep_radio": return "Ranger Jeep Radio Log";
                case "log_halvorsen": return "Halvorsen's Maintenance Log";
                case "id_badge": return "Staff ID Badge";
            }
            if (string.IsNullOrEmpty(docId)) return "Document";
            string t = docId.Replace('_', ' ');
            return char.ToUpperInvariant(t[0]) + t.Substring(1);
        }
    }
}
