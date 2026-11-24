using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Cretacea.Core;

namespace Cretacea.UI
{
    /// <summary>
    /// Toggleable [Tab] / [View] Tactical Park Tablet / Clipboard Journal with 3 crisp tabs:
    /// 1. Map & Objectives: Displays hud_minimap_route.png with live normalized player dot across X: -101.5..+548
    ///    plus pulsing next-objective beacon & current objective breakdown.
    /// 2. Substation & Systems Checklist: Live 4-stage Auxiliary Circuit B schematic (Breaker #1/#2/#3,
    ///    Fuel Pump, Diesel Generator, Transfer Lever) + Exit E-0 Gate progress bar.
    /// 3. Documents (X/5): Complete readable archive for all 5 canonical documents (including jeep_radio transcript!).
    /// </summary>
    public sealed class JournalController : MonoBehaviour
    {
        public static JournalController Instance { get; private set; }

        [Header("Optional Art Overrides")]
        [SerializeField] private Sprite routeMapSprite;

        private Canvas _canvas;
        private GameObject _panelRoot;
        private GameObject[] _tabPages;
        private Button[] _tabButtons;
        private int _activeTabIndex;

        // Tab 1: Map & Objectives
        private ParkMapView _parkMap;
        private ParkMapView _vcMap;
        private TextMeshProUGUI _mapHeaderText;
        private Button[] _mapViewButtons;
        private int _mapView;                 // 0 floor plan, 1 park map
        private TextMeshProUGUI _objectivePlaceText;
        private RectTransform _objectivePlacePositionRow;
        private TextMeshProUGUI _routeSubstationLabel;
        private TextMeshProUGUI _routeE0Label;
        private GameObject _routeE0Track;
        private TextMeshProUGUI _mapZoneTitleText;
        private TextMeshProUGUI _mapObjectiveBodyText;
        private TextMeshProUGUI _routeSubstationValue;
        private TextMeshProUGUI _routeE0Value;
        private Image _routeE0Fill;
        private TextMeshProUGUI _routeKeyValue;
        private TextMeshProUGUI _routeKeyLabel, _routeDocsLabel;
        private RectTransform _routeStatusRect;
        private TextMeshProUGUI _systemsKicker;
        private TextMeshProUGUI _routeDocsValue;
        private Button[] _docButtons;

        // Tab 2: Substation & Systems
        private TextMeshProUGUI[] _substationStepTexts;
        private Image[] _substationStepDots;
        private TextMeshProUGUI[] _substationStepStates;
        private TextMeshProUGUI _e0StatusText;
        private TextMeshProUGUI _systemsHeader;
        private GameObject[] _systemsBody;
        private GameObject _systemsNoData;
        private Image _e0BarFill;

        // Tab 3: Documents (5/5)
        private TextMeshProUGUI _docsHeaderText;
        private TextMeshProUGUI[] _docButtonLabels;
        private TextMeshProUGUI _docDetailTitle;
        private TextMeshProUGUI _docDetailBody;
        private TextMeshProUGUI _docDetailKicker;
        private RectTransform _docDetailRule;
        private TextMeshProUGUI[] _docRowHints;
        private RectTransform[] _docRowIcons;
        private GameObject _docRecovered;
        private GameObject _docMissing;
        private TextMeshProUGUI _docMissingHint;
        private Image _docThumb;
        private int _selectedDocIndex;

        private bool _isOpen;

        private readonly struct DocArchiveEntry
        {
            public readonly string Id;
            public readonly string Title;
            public readonly string Location;    // where it is, as a hint ("Security Office, on the desk")
            public readonly string AssetKey;    // first page art in Resources/UI/Docs
            public readonly string Transcript;

            public DocArchiveEntry(string id, string title, string location, string assetKey, string transcript)
            {
                Id = id;
                Title = title;
                Location = location;
                AssetKey = assetKey;
                Transcript = transcript;
            }
        }

        private static readonly DocArchiveEntry[] ArchiveDocs =
        {
            new DocArchiveEntry(
                "memo_switchover",
                "Night Shift Switchover Memo",
                "Security Office, on the desk",
                "memo_switchover",
                "OPERATIONS MEMO \u2014 14 NOV\nTO: Night Staff\nFROM: D. Okafor, Ops Supervisor\nRE: GRID SWITCHOVER \u2014 TONIGHT 00:00\n\nAt midnight the park transfers from Substation 1 to the new Substation 5. Expect a brief flicker of up to 3 seconds. Containment gates will run on UPS during the transfer. Do NOT open any paddock service gate between 23:45 and 00:15. Storm warning in effect after 22:00 \u2014 secure loose equipment.\n\nCaretaker rounds: log Paddock 2 (Talon Ridge) and Paddock 4 (Tyrant Valley) camera feeds from the Control Room before 23:30."),
            new DocArchiveEntry(
                "manual",
                "1996 Preserve Evacuation Manual",
                "Records Annex, at the far end of the Staff Corridor past the Control Room",
                "manual_p1",
                ManualPage1 + "\n\n" + ManualPage2),
            new DocArchiveEntry(
                "keeper_note",
                "Feed Prep Keeper's Note",
                "Feed Prep, on the hall counter",
                "keeper_note",
                "Talon Ridge, 9 Nov \u2014\n\nThey tested the pen gate again tonight. Three of them, working the lock in turns. They learn the cycle. I swear they know when the power dips."),
            new DocArchiveEntry(
                "jeep_radio",
                "Ranger Jeep Radio Log",
                "Tyrant Valley, at the overturned ranger jeep",
                "jeep_radio",
                "RANGER JEEP #04 \u2014 RADIO LOG\nPaddock 4 \u2014 Tyrant Valley (North Perimeter Fence)\n\n00:04:19  RANGER UNIT 4 \u2014 OPEN MIC:\n\"...Unit Four, it's at the fence line... no \u2014 the fence is dead, it's DEAD\u2014\"\n00:04:22  HEAVY IMPACT \u2014 SIGNAL STATIC BURST\n\n00:04:24  RANGER UNIT 4 \u2014 FINAL TRANSMISSION:\n\"...if anyone hears this, stay in the grass. It tracks the lights...\"\n00:04:28  CARRIER LOST\n\nFIELD RECOVERY NOTE \u2014 PADDOCK 4:\nPerimeter containment power is offline across Tyrant Valley. Extinguish your flashlight beam and crouch in tall grass cover whenever the apex predator sweeps the road. Proceed east toward Substation 3 to restore Auxiliary Circuit B."),
            new DocArchiveEntry(
                "log_halvorsen",
                "Halvorsen's 1997 Maintenance Log",
                "Old Maintenance Yard, in the foreman's office",
                "log_halvorsen",
                "Maintenance log \u2014 R. Halvorsen, Aug '97.\n\nThey are building the new park right over Service Road 3. Once the T. rex paddock goes in, nobody will remember E-0 exists.\nThe gate motor is on Circuit B; if it ever sticks, keep holding the OPEN button and it grinds through.\nI told them. Nobody listens.")
        };

        // Word-for-word copies of the two scanned manual pages. Numbered lines: number, 3em tab stop, 3em hanging indent.
        private const string ManualPage1 =
            "SECTION 7: EMERGENCY EGRESS\n\n" +
            "<indent=3em><line-indent=-3em>7.1<pos=3em>Primary egress is via the Main Gate.\n" +
            "7.2<pos=3em>If the Main Gate and staff exits are unusable, staff shall proceed to SERVICE EXIT E-0 at the Maintenance Yard (Warehouse B), east sector.\n" +
            "7.3<pos=3em>Route: Feed Prep loading dock \u2014 service path \u2014 Paddock 4 Service Gate \u2014 SERVICE ROAD 3 \u2014 Maintenance Yard.\n" +
            "7.4<pos=3em>WARNING: The E-0 gate motor and yard lighting are fed by AUXILIARY CIRCUIT B from SUBSTATION 3 (Paddock 4). In a grid failure, auxiliary power must be restored manually at Substation 3 before E-0 can be opened.</line-indent></indent>";

        private const string ManualPage2 =
            "SECTION 7: EMERGENCY EGRESS (CONT'D)\n\n" +
            "FIG. 7-A  EGRESS ROUTE TO SERVICE EXIT E-0\nV.C. \u2014 Paddock 4 Service Gate \u2014 Sub 3 (Aux. Ckt B) \u2014 Maint. Yard (Warehouse B) \u2014 E-0\n\n" +
            "<indent=3em><line-indent=-3em>7.5<pos=3em>Restoring Auxiliary Circuit B:</line-indent>\n" +
            "(1)  Reset the three tripped breakers.\n" +
            "(2)  Prime the generator fuel pump.\n" +
            "(3)  Start the generator.\n" +
            "(4)  Throw the TRANSFER lever to CIRCUIT B.</indent>\n\n" +
            "<i>Handwritten:</i> Still works \u2014 R.H. '97";

        /// <summary>Handwritten pages (the keeper's note, Halvorsen's log) are transcribed in the hand font.</summary>
        public static bool IsHandwritten(string docId) => docId == "keeper_note" || docId == "log_halvorsen";

        /// <summary>The transcript of one page as the reader shows it (falls back to the whole document's).</summary>
        public static string GetPageTranscript(string docId, string assetKey)
        {
            if (string.Equals(docId, "manual", StringComparison.OrdinalIgnoreCase))
            {
                if (assetKey != null && assetKey.EndsWith("_p2", StringComparison.OrdinalIgnoreCase)) return ManualPage2;
                return ManualPage1;
            }
            return GetTranscript(docId);
        }

        public bool IsOpen => _isOpen;

        /// <summary>The typed / transcribed text of an archive document (empty when there is none).</summary>
        public static string GetTranscript(string docId)
        {
            if (string.IsNullOrEmpty(docId)) return string.Empty;
            for (int i = 0; i < ArchiveDocs.Length; i++)
                if (string.Equals(ArchiveDocs[i].Id, docId, StringComparison.OrdinalIgnoreCase)) return ArchiveDocs[i].Transcript;
            return string.Empty;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildJournalUi();
        }

        private void OnEnable()
        {
            GameEvents.OnObjectiveChanged += HandleObjectiveChanged;
            GameEvents.OnSubstationProgressChanged += HandleSubstationChanged;
            GameEvents.OnE0GateProgressChanged += HandleE0Changed;
            GameEvents.OnDocumentCollected += HandleDocumentCollected;
            GameEvents.OnCutsceneStateChanged += HandleCutsceneStateChanged;
            GameEvents.OnPauseStateChanged += HandlePauseStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnObjectiveChanged -= HandleObjectiveChanged;
            GameEvents.OnSubstationProgressChanged -= HandleSubstationChanged;
            GameEvents.OnE0GateProgressChanged -= HandleE0Changed;
            GameEvents.OnDocumentCollected -= HandleDocumentCollected;
            GameEvents.OnCutsceneStateChanged -= HandleCutsceneStateChanged;
            GameEvents.OnPauseStateChanged -= HandlePauseStateChanged;
        }

        private void HandleObjectiveChanged(string _) => RefreshAllTabs();
        private void HandleSubstationChanged(SubstationChecklistSnapshot _) => RefreshSystemsTab();
        private void HandleE0Changed(float _) => RefreshSystemsTab();
        private void HandleDocumentCollected(string _, int __, int ___) => RefreshDocumentsTab();
        private void HandleCutsceneStateChanged(bool inCutscene) { if (inCutscene) CloseJournal(); }
        private void HandlePauseStateChanged(bool paused) { if (paused) CloseJournal(); }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            GameState gs = GameState.Instance;
            CretaceaInput input = CretaceaInput.Instance;
            if (gs == null || input == null) return;

            if (gs.IsMainMenu || gs.InCutscene || gs.IsPaused || gs.IsGameOver || gs.IsDocumentOpen)
            {
                if (_isOpen) CloseJournal();
                return;
            }

            if (input.JournalPressedThisFrame)
            {
                if (_isOpen) CloseJournal();
                else OpenJournal();
            }
            else if (_isOpen && input.CancelPressedThisFrame)
            {
                CloseJournal();
            }

            if (_isOpen)
            {
                UpdateLiveMapMarkers();
                HandleTabletFocusInput(input);
            }
        }

        /// <summary>Owner's playtest (Nov 21): E and the gamepad X (Interact, the prompt every document shows) did nothing
        /// in the tablet; only Enter, Space and the gamepad A submit a focused button. Interact now presses the focused
        /// tablet button as well, and moving the focus onto a document row shows that document.</summary>
        private void HandleTabletFocusInput(CretaceaInput input)
        {
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null || _panelRoot == null || !sel.activeInHierarchy || !sel.transform.IsChildOf(_panelRoot.transform)) return;
            if (_activeTabIndex == 2 && _docButtons != null)
            {
                for (int i = 0; i < _docButtons.Length; i++)
                {
                    if (_docButtons[i] != null && sel == _docButtons[i].gameObject && i != _selectedDocIndex) { SelectDocument(i); break; }
                }
            }
            if (input.InteractPressedThisFrame && sel.TryGetComponent(out Button button) && button.interactable)
                button.onClick.Invoke();
        }

        public void OpenJournal(int tabIndex = -1)
        {
            _isOpen = true;
            if (_panelRoot != null) _panelRoot.SetActive(true);
            GameState.EnsureInstance().SetJournalOpen(true);
            GameEvents.RaisePlaySound("ui_click");
            ChooseDefaultMapView();
            if (tabIndex >= 0 && tabIndex < 3) SelectTab(tabIndex);
            RefreshAllTabs();
            // Keyboard / gamepad focus on the active tab (left/right between tabs, down into the document list).
            if (_tabButtons != null) CretaceaUiFactory.Focus(_tabButtons[_activeTabIndex]);
        }

        public void CloseJournal()
        {
            if (!_isOpen) return;
            _isOpen = false;
            if (_panelRoot != null) _panelRoot.SetActive(false);
            if (GameState.Instance != null) GameState.Instance.SetJournalOpen(false);
        }

        private void BuildJournalUi()
        {
            _canvas = CretaceaUiFactory.CreateRootCanvas("[JournalCanvas]", sortOrder: 25);
            _canvas.transform.SetParent(transform, false);
            RectTransform root = _canvas.GetComponent<RectTransform>();

            // Full-screen subtle backdrop dim
            RectTransform backdrop = CretaceaUiFactory.CreatePanel(root, "JournalBackdrop", new Color(0.012f, 0.011f, 0.010f, 0.74f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, raycastTarget: true);
            _panelRoot = backdrop.gameObject;

            // Main Tablet Frame (centered 1380 x 820 with generous padding)
            RectTransform tablet = CretaceaUiFactory.CreateAnchoredPanel(backdrop, "TacticalTablet", CretaceaUiFactory.PanelBgSolid,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(1500f, 880f), raycastTarget: true);
            CretaceaUiFactory.StyleCard(tablet, 6f, 0.7f);
            tablet.GetComponent<CretaceaPanelGraphic>().shadowSpread = 28f;
            // round 4 art direction: the park-security kit (worn grain, pressed-steel bevel, corner screws, park badge
            // left of the title, stencil title)
            CretaceaUiFactory.DressKitCard(tablet, true, new Vector2(0f, 1f), new Vector2(34f, -22f), 78f);

            // Top Header Bar
            RectTransform headerBar = CretaceaUiFactory.CreatePanel(tablet, "HeaderBar", Color.clear,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -112f), Vector2.zero);
            CretaceaUiFactory.CreatePanel(headerBar, "HeaderUnderline", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(40f, 0f), new Vector2(-40f, 1f));

            TextMeshProUGUI kicker = CretaceaUiFactory.CreateText(headerBar, "Kicker", "CRETACEA PARK SECURITY", 19f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            kicker.characterSpacing = 4f;
            kicker.rectTransform.offsetMin = new Vector2(128f, 0f);
            kicker.rectTransform.offsetMax = new Vector2(-860f, -30f);
            TextMeshProUGUI title = CretaceaUiFactory.CreateText(headerBar, "Title", "FIELD TABLET", 46f, CretaceaUiFactory.TextPrimary, UiFont.Stencil, TextAlignmentOptions.TopLeft);
            title.characterSpacing = 3f;
            title.rectTransform.offsetMin = new Vector2(128f, 0f);
            title.rectTransform.offsetMax = new Vector2(-860f, -54f);

            // 3 Navigation Tab Buttons
            string[] tabLabels = { "MAP & OBJECTIVES", "SUBSTATION & SYSTEMS", "DOCUMENTS" };
            _tabButtons = new Button[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                Button btn = CretaceaUiFactory.CreateButton(headerBar, $"TabBtn_{i}", tabLabels[i], new Vector2(262f, 54f), () => SelectTab(idx), fontSize: 22f);
                RectTransform brt = btn.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(1f, 0.5f);
                brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.anchoredPosition = new Vector2(-40f - (2 - i) * 274f, 0f);
                TextMeshProUGUI tl = btn.GetComponentInChildren<TextMeshProUGUI>();
                tl.alignment = TextAlignmentOptions.Center;
                tl.rectTransform.offsetMin = new Vector2(10f, 2f);
                tl.rectTransform.offsetMax = new Vector2(-10f, -2f);
                tl.characterSpacing = 2f;
                _tabButtons[i] = btn;
            }

            // Content Area
            RectTransform content = CretaceaUiFactory.CreatePanel(tablet, "Content", Color.clear,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(40f, 66f), new Vector2(-40f, -134f));

            _tabPages = new GameObject[3];
            _tabPages[0] = BuildMapObjectivesPage(content);
            _tabPages[1] = BuildSystemsChecklistPage(content);
            _tabPages[2] = BuildDocumentsPage(content);

            // Bottom Close Legend
            TextMeshProUGUI footer = CretaceaUiFactory.CreateText(tablet, "Footer", CretaceaUiFactory.FormatKeyTokens("[Tab / Esc]  CLOSE TABLET"), 21f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineRight);
            footer.characterSpacing = 2f;
            footer.rectTransform.anchorMin = new Vector2(0f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.offsetMin = new Vector2(40f, 16f);
            footer.rectTransform.offsetMax = new Vector2(-40f, 52f);
            _footerNote = CretaceaUiFactory.CreateText(tablet, "FooterNote", string.Empty, 20f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            _footerNote.characterSpacing = 1.5f;
            _footerNote.rectTransform.anchorMin = new Vector2(0f, 0f);
            _footerNote.rectTransform.anchorMax = new Vector2(1f, 0f);
            _footerNote.rectTransform.offsetMin = new Vector2(40f, 16f);
            _footerNote.rectTransform.offsetMax = new Vector2(-520f, 52f);

            SelectTab(0);
            _panelRoot.SetActive(false);
        }

        private GameObject BuildMapObjectivesPage(RectTransform parent)
        {
            RectTransform page = CretaceaUiFactory.CreatePanel(parent, "Page_Map", Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Left: the map (68% width)
            RectTransform mapFrame = CretaceaUiFactory.CreatePanel(page, "MapFrame", CretaceaUiFactory.PanelCard,
                new Vector2(0f, 0f), new Vector2(0.68f, 1f), Vector2.zero, new Vector2(-12f, 0f));
            CretaceaUiFactory.StyleCard(mapFrame, 5f, 0.35f);

            // 24 px inside the card on every side, under a 56 px header
            RectTransform mapViewport = CretaceaUiFactory.CreatePanel(mapFrame, "MapViewport", new Color(0f, 0f, 0f, 1f),
                Vector2.zero, Vector2.one, new Vector2(24f, 24f), new Vector2(-24f, -74f));
            CretaceaUiFactory.StyleRounded(mapViewport, 3f);
            _parkMap = ParkMapView.Create(mapViewport, ParkMapView.Layout.Park);
            _vcMap = ParkMapView.Create(mapViewport, ParkMapView.Layout.VisitorCenter);
            RectTransform viewportRing = CretaceaUiFactory.CreatePanel(mapViewport, "Ring", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleCard(viewportRing, 3f, 0f, new Color(CretaceaUiFactory.BorderSubtle.r, CretaceaUiFactory.BorderSubtle.g, CretaceaUiFactory.BorderSubtle.b, 0.45f));

            _mapHeaderText = CretaceaUiFactory.CreateText(mapFrame, "MapHeader", "VISITOR CENTER  \u00B7  FLOOR PLAN", 21f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.MidlineLeft);
            _mapHeaderText.characterSpacing = 3f;
            _mapHeaderText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _mapHeaderText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _mapHeaderText.rectTransform.offsetMin = new Vector2(26f, -66f);
            _mapHeaderText.rectTransform.offsetMax = new Vector2(-300f, -12f);

            // Floor plan / park map switch (the park map exists once the manual is read)
            string[] views = { "FLOOR PLAN", "PARK MAP" };
            _mapViewButtons = new Button[2];
            for (int i = 0; i < 2; i++)
            {
                int idx = i;
                Button b = CretaceaUiFactory.CreateButton(mapFrame, $"View_{i}", views[i], new Vector2(128f, 40f), () => SetMapView(idx), fontSize: 19f);
                RectTransform brt = b.GetComponent<RectTransform>();
                brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(1f, 1f);
                brt.anchoredPosition = new Vector2(-24f - (1 - i) * 136f, -18f);
                TextMeshProUGUI bl = b.GetComponentInChildren<TextMeshProUGUI>();
                bl.alignment = TextAlignmentOptions.Center;
                bl.rectTransform.offsetMin = new Vector2(6f, 1f);
                bl.rectTransform.offsetMax = new Vector2(-6f, -1f);
                bl.characterSpacing = 1.5f;
                _mapViewButtons[i] = b;
            }

            // Right: chapter, objective and where it is (32% width)
            RectTransform side = CretaceaUiFactory.CreatePanel(page, "ObjectiveSide", CretaceaUiFactory.PanelCard,
                new Vector2(0.68f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), Vector2.zero);
            CretaceaUiFactory.StyleCard(side, 5f, 0.35f);

            _mapZoneTitleText = CretaceaUiFactory.CreateText(side, "ZoneTitle", "CHAPTER 1 \u2014 NIGHT SHIFT", 26f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _mapZoneTitleText.characterSpacing = 2.5f;
            _mapZoneTitleText.rectTransform.offsetMin = new Vector2(28f, 40f);
            _mapZoneTitleText.rectTransform.offsetMax = new Vector2(-28f, -26f);

            TextMeshProUGUI objKicker = CretaceaUiFactory.CreateText(side, "ObjKicker", "CURRENT OBJECTIVE", 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            objKicker.characterSpacing = 3f;
            objKicker.rectTransform.offsetMin = new Vector2(28f, 40f);
            objKicker.rectTransform.offsetMax = new Vector2(-28f, -84f);

            _mapObjectiveBodyText = CretaceaUiFactory.CreateText(side, "ObjectiveBody", "No active objective.", 25f, CretaceaUiFactory.TextPrimary, UiFont.Body, TextAlignmentOptions.TopLeft);
            _mapObjectiveBodyText.rectTransform.offsetMin = new Vector2(28f, 300f);
            _mapObjectiveBodyText.rectTransform.offsetMax = new Vector2(-28f, -116f);

            // Where: a ring like the map beacon and the place name
            RectTransform placeRow = CretaceaUiFactory.CreatePanel(side, "PlaceRow", Color.clear, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -300f), new Vector2(-28f, -266f));
            RectTransform placeRing = CretaceaUiFactory.CreateAnchoredPanel(placeRow, "Ring", CretaceaUiFactory.AccentAmber, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            placeRing.GetComponent<Image>().sprite = CretaceaUiFactory.GetRadialRingSprite();
            RectTransform placeCore = CretaceaUiFactory.CreateAnchoredPanel(placeRing, "Core", CretaceaUiFactory.AccentAmber, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6f, 6f));
            placeCore.GetComponent<Image>().sprite = CretaceaUiFactory.GetCircleSprite();
            _objectivePlaceText = CretaceaUiFactory.CreateText(placeRow, "Place", "SECURITY OFFICE", 22f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.MidlineLeft);
            _objectivePlaceText.characterSpacing = 2.5f;
            _objectivePlaceText.rectTransform.offsetMin = new Vector2(34f, 0f);
            _objectivePlacePositionRow = placeRow;

            // Route status: where the escape stands, at a glance
            RectTransform status = CretaceaUiFactory.CreatePanel(side, "RouteStatus", new Color(0.051f, 0.051f, 0.051f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(20f, 20f), new Vector2(-20f, 284f));
            CretaceaUiFactory.StyleRounded(status, 4f);
            _routeStatusRect = status;
            TextMeshProUGUI stK = CretaceaUiFactory.CreateText(status, "Kicker", "ROUTE STATUS", 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            stK.characterSpacing = 3f;
            stK.rectTransform.offsetMin = new Vector2(20f, 0f);
            stK.rectTransform.offsetMax = new Vector2(-20f, -16f);
            _routeSubstationValue = CreateStatusRow(status, 0, "SUBSTATION 3", out _routeSubstationLabel);
            _routeE0Value = CreateStatusRow(status, 1, "EXIT E-0 GATE", out _routeE0Label);
            _routeKeyValue = CreateStatusRow(status, 2, "MASTER KEY", out _routeKeyLabel);
            _routeDocsValue = CreateStatusRow(status, 3, "DOCUMENTS", out _routeDocsLabel);
            RectTransform e0Track = CretaceaUiFactory.CreatePanel(status, "E0Track", CretaceaUiFactory.TrackColor,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -50f - 1 * 52f - 44f), new Vector2(-20f, -50f - 1 * 52f - 38f));
            CretaceaUiFactory.StyleRounded(e0Track, 3f);
            _routeE0Track = e0Track.gameObject;
            RectTransform e0Fill = CretaceaUiFactory.CreatePanel(e0Track, "Fill", CretaceaUiFactory.AccentGreen,
                Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleRounded(e0Fill, 3f);
            _routeE0Fill = e0Fill.GetComponent<Image>();

            return page.gameObject;
        }

        /// <summary>Moves one status line (label + value) to row <paramref name="index"/>.</summary>
        private static void PlaceStatusRow(TextMeshProUGUI label, TextMeshProUGUI value, int index)
        {
            float top = -50f - index * 52f;
            foreach (TextMeshProUGUI t in new[] { label, value })
            {
                if (t == null) continue;
                t.rectTransform.offsetMin = new Vector2(20f, top - 34f);
                t.rectTransform.offsetMax = new Vector2(-20f, top);
            }
        }

        /// <summary>One "LABEL ........ value" line of the route status card (52 px pitch).</summary>
        private static TextMeshProUGUI CreateStatusRow(RectTransform parent, int index, string label, out TextMeshProUGUI labelText)
        {
            float top = -50f - index * 52f;
            TextMeshProUGUI l = CretaceaUiFactory.CreateText(parent, $"Lbl_{index}", label, 21f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineLeft);
            labelText = l;
            l.characterSpacing = 2f;
            l.rectTransform.anchorMin = new Vector2(0f, 1f); l.rectTransform.anchorMax = new Vector2(1f, 1f);
            l.rectTransform.offsetMin = new Vector2(20f, top - 34f); l.rectTransform.offsetMax = new Vector2(-20f, top);
            TextMeshProUGUI v = CretaceaUiFactory.CreateText(parent, $"Val_{index}", "-", 21f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.MidlineRight);
            v.characterSpacing = 1.5f;
            v.rectTransform.anchorMin = new Vector2(0f, 1f); v.rectTransform.anchorMax = new Vector2(1f, 1f);
            v.rectTransform.offsetMin = new Vector2(20f, top - 34f); v.rectTransform.offsetMax = new Vector2(-20f, top);
            return v;
        }

        private GameObject BuildSystemsChecklistPage(RectTransform parent)
        {
            RectTransform page = CretaceaUiFactory.CreatePanel(parent, "Page_Systems", CretaceaUiFactory.PanelCard,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleCard(page, 5f, 0.35f);

            TextMeshProUGUI hdr = CretaceaUiFactory.CreateText(page, "Header", "SUBSTATION 3  \u00B7  AUXILIARY CIRCUIT B STARTUP", 26f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _systemsHeader = hdr;
            hdr.characterSpacing = 2.5f;
            hdr.rectTransform.offsetMin = new Vector2(32f, 40f);
            hdr.rectTransform.offsetMax = new Vector2(-32f, -26f);

            // The manual's 7.5 procedure, with where each control is. The hold ring shows durations; none here.
            string[] steps =
            {
                "Reset breaker 1  \u00B7  north-west panel",
                "Reset breaker 2  \u00B7  south walkway panel",
                "Reset breaker 3  \u00B7  north-east panel",
                "Prime the generator fuel pump (hold)",
                "Start the generator (pull until it catches)",
                "Throw the transfer lever to Circuit B (hold)"
            };

            _substationStepTexts = new TextMeshProUGUI[steps.Length];
            _substationStepDots = new Image[steps.Length];
            _substationStepStates = new TextMeshProUGUI[steps.Length];

            for (int i = 0; i < steps.Length; i++)
            {
                RectTransform row = CretaceaUiFactory.CreatePanel(page, $"Step_{i}", SlotFill,
                    new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(32f, -88f - i * 70f - 58f), new Vector2(-32f, -88f - i * 70f));
                CretaceaUiFactory.StyleRounded(row, 4f);

                RectTransform dot = CretaceaUiFactory.CreateAnchoredPanel(row, "Dot", CretaceaUiFactory.AccentDanger,
                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(18f, 18f));
                _substationStepDots[i] = dot.GetComponent<Image>();
                _substationStepDots[i].sprite = CretaceaUiFactory.GetCircleSprite();

                TextMeshProUGUI num = CretaceaUiFactory.CreateText(row, "Num", (i + 1).ToString(), 22f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.MidlineLeft);
                num.rectTransform.offsetMin = new Vector2(52f, 0f);
                num.rectTransform.offsetMax = new Vector2(0f, 0f);
                TextMeshProUGUI txt = CretaceaUiFactory.CreateText(row, "Text", steps[i], 23f, CretaceaUiFactory.TextPrimary, UiFont.BodyMedium, TextAlignmentOptions.MidlineLeft);
                txt.rectTransform.offsetMin = new Vector2(80f, 0f);
                txt.rectTransform.offsetMax = new Vector2(-140f, 0f);
                _substationStepTexts[i] = txt;

                // State in words, not only by colour
                TextMeshProUGUI st = CretaceaUiFactory.CreateText(row, "State", "LOCKED", 21f, CretaceaUiFactory.TextMuted, UiFont.Label, TextAlignmentOptions.MidlineRight);
                st.characterSpacing = 3f;
                st.textWrappingMode = TextWrappingModes.NoWrap;
                st.rectTransform.offsetMin = new Vector2(0f, 0f);
                st.rectTransform.offsetMax = new Vector2(-24f, 0f);
                _substationStepStates[i] = st;
            }

            // E-0 Gate System Box
            RectTransform e0Box = CretaceaUiFactory.CreatePanel(page, "E0StatusBox", SlotFill,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(32f, 28f), new Vector2(-32f, 128f));
            CretaceaUiFactory.StyleCard(e0Box, 5f, 0f);
            _e0StatusText = CretaceaUiFactory.CreateText(e0Box, "E0Text", "SERVICE EXIT E-0 GATE \u2014 OFFLINE (AWAITING CIRCUIT B)", 22f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _e0StatusText.characterSpacing = 2f;
            _e0StatusText.rectTransform.offsetMin = new Vector2(24f, 44f);
            _e0StatusText.rectTransform.offsetMax = new Vector2(-24f, -14f);

            RectTransform e0Track = CretaceaUiFactory.CreatePanel(e0Box, "Track", CretaceaUiFactory.TrackColor,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(24f, 22f), new Vector2(-24f, 34f));
            CretaceaUiFactory.StyleRounded(e0Track, 6f);
            RectTransform e0Fill = CretaceaUiFactory.CreatePanel(e0Track, "Fill", CretaceaUiFactory.AccentGreen,
                new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleRounded(e0Fill, 6f);
            _e0BarFill = e0Fill.GetComponent<Image>();
            _systemsBody = new GameObject[] { e0Box.gameObject };

            // Before the manual: no circuit data at all (the manual is what explains Substation 3)
            RectTransform noData = CretaceaUiFactory.CreatePanel(page, "NoData", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            RectTransform slot = CretaceaUiFactory.CreateAnchoredPanel(noData, "Slot", new Color(0.051f, 0.051f, 0.051f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(900f, 420f));
            CretaceaUiFactory.StyleCard(slot, 5f, 0f, new Color(CretaceaUiFactory.BorderSubtle.r, CretaceaUiFactory.BorderSubtle.g, CretaceaUiFactory.BorderSubtle.b, 0.5f));
            TextMeshProUGUI nk = CretaceaUiFactory.CreateText(slot, "Kicker", "FIELD SYSTEMS", 21f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.Top);
            nk.characterSpacing = 4f;
            nk.rectTransform.offsetMax = new Vector2(0f, -110f);
            _systemsKicker = nk;
            TextMeshProUGUI nt = CretaceaUiFactory.CreateText(slot, "Title", "NO DATA", 56f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.Top);
            nt.characterSpacing = 8f;
            nt.rectTransform.offsetMax = new Vector2(0f, -146f);
            TextMeshProUGUI nb = CretaceaUiFactory.CreateText(slot, "Body", "Locate the 1996 evacuation manual in the Records\u00A0Annex.", 24f, CretaceaUiFactory.TextMuted, UiFont.Body, TextAlignmentOptions.Top);
            nb.rectTransform.offsetMin = new Vector2(60f, 0f);
            nb.rectTransform.offsetMax = new Vector2(-60f, -240f);
            _systemsNoData = noData.gameObject;

            return page.gameObject;
        }

        private static readonly Color PaperTone = new Color(0.86f, 0.80f, 0.68f, 1f);
        private static readonly Color SlotFill = new Color(0.051f, 0.051f, 0.051f, 1f);   // on the 8-bit linear UI steps

        private GameObject BuildDocumentsPage(RectTransform parent)
        {
            RectTransform page = CretaceaUiFactory.CreatePanel(parent, "Page_Documents", Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // Left: the five documents as two-line rows (38% width)
            RectTransform left = CretaceaUiFactory.CreatePanel(page, "DocList", CretaceaUiFactory.PanelCard,
                new Vector2(0f, 0f), new Vector2(0.38f, 1f), Vector2.zero, new Vector2(-12f, 0f));
            CretaceaUiFactory.StyleCard(left, 5f, 0.35f);

            _docsHeaderText = CretaceaUiFactory.CreateText(left, "DocsCount", "DOCUMENTS  \u00B7  0 / 5", 22f, CretaceaUiFactory.AccentAmber, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docsHeaderText.characterSpacing = 3f;
            _docsHeaderText.rectTransform.offsetMin = new Vector2(24f, 40f);
            _docsHeaderText.rectTransform.offsetMax = new Vector2(-24f, -24f);

            _docButtonLabels = new TextMeshProUGUI[ArchiveDocs.Length];
            _docRowHints = new TextMeshProUGUI[ArchiveDocs.Length];
            _docRowIcons = new RectTransform[ArchiveDocs.Length];
            _docButtons = new Button[ArchiveDocs.Length];
            for (int i = 0; i < ArchiveDocs.Length; i++)
            {
                int idx = i;
                Button btn = CretaceaUiFactory.CreateButton(left, $"DocBtn_{i}", string.Empty, new Vector2(480f, 92f), () => SelectDocument(idx), fontSize: 22f);
                RectTransform brt = btn.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(0f, 1f);
                brt.anchorMax = new Vector2(1f, 1f);
                brt.pivot = new Vector2(0.5f, 1f);
                brt.offsetMin = new Vector2(20f, -170f - i * 102f);
                brt.offsetMax = new Vector2(-20f, -78f - i * 102f);

                // line 1: title (or NOT FOUND) in label caps; line 2: where it is / was found
                TextMeshProUGUI bl = btn.GetComponentInChildren<TextMeshProUGUI>();
                CretaceaUiFactory.SetFont(bl, UiFont.Label);
                bl.fontSize = 23f;
                bl.characterSpacing = 1.5f;
                bl.alignment = TextAlignmentOptions.BottomLeft;
                bl.rectTransform.offsetMin = new Vector2(74f, 46f);
                bl.rectTransform.offsetMax = new Vector2(-16f, -10f);
                _docButtonLabels[i] = bl;

                TextMeshProUGUI hint = CretaceaUiFactory.CreateText(btn.transform, "Hint", string.Empty, 20f, CretaceaUiFactory.TextMuted, UiFont.Body, TextAlignmentOptions.TopLeft);
                hint.textWrappingMode = TextWrappingModes.NoWrap;
                hint.overflowMode = TextOverflowModes.Ellipsis;
                hint.rectTransform.offsetMin = new Vector2(74f, 8f);
                hint.rectTransform.offsetMax = new Vector2(-16f, -50f);
                _docRowHints[i] = hint;

                _docRowIcons[i] = BuildPageIcon(btn.transform, new Vector2(22f, 0f), new Vector2(36f, 48f));
                _docButtons[i] = btn;
            }

            // Right: the selected document (62% width), dark like every other card
            RectTransform right = CretaceaUiFactory.CreatePanel(page, "DocDetail", CretaceaUiFactory.PanelCard,
                new Vector2(0.38f, 0f), new Vector2(1f, 1f), new Vector2(12f, 0f), Vector2.zero);
            CretaceaUiFactory.StyleCard(right, 5f, 0.35f);

            // Recovered: the page itself (the reader's paper) with a shadow, the title and the transcript beside it
            _docRecovered = CretaceaUiFactory.CreatePanel(right, "Recovered", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            RectTransform thumbHolder = CretaceaUiFactory.CreateAnchoredPanel(_docRecovered.transform, "PageThumb", Color.clear,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(262f, 360f));
            RectTransform thumbShadow = CretaceaUiFactory.CreatePanel(thumbHolder, "Shadow", new Color(0f, 0f, 0f, 0f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            CretaceaUiFactory.StyleCard(thumbShadow, 2f, 0.85f, new Color(0f, 0f, 0f, 0f));
            thumbShadow.GetComponent<CretaceaPanelGraphic>().shadowSpread = 18f;
            _docThumb = CretaceaUiFactory.CreatePanel(thumbHolder, "Page", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<Image>();
            _docThumb.preserveAspect = true;

            _docDetailKicker = CretaceaUiFactory.CreateText(_docRecovered.transform, "Kicker", "RECOVERED", 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docDetailKicker.characterSpacing = 3f;
            _docDetailKicker.rectTransform.offsetMin = new Vector2(320f, 0f);
            _docDetailKicker.rectTransform.offsetMax = new Vector2(-30f, -32f);
            _docDetailTitle = CretaceaUiFactory.CreateText(_docRecovered.transform, "DetailTitle", string.Empty, 34f, CretaceaUiFactory.TextPrimary, UiFont.Heading, TextAlignmentOptions.TopLeft);
            _docDetailTitle.characterSpacing = 2f;
            _docDetailTitle.lineSpacing = -10f;
            _docDetailTitle.rectTransform.offsetMin = new Vector2(320f, 0f);
            _docDetailTitle.rectTransform.offsetMax = new Vector2(-30f, -60f);
            RectTransform rule = CretaceaUiFactory.CreatePanel(_docRecovered.transform, "Rule", CretaceaUiFactory.BorderSubtle,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(320f, -150f), new Vector2(-30f, -149f));
            _docDetailRule = rule;

            // Same type as the reader's transcript (Courier Prime Regular 23, +8 line spacing); long documents are
            // split into pages instead of shrinking the type.
            _docDetailBody = CretaceaUiFactory.CreateText(_docRecovered.transform, "DetailBody", string.Empty, 23f, new Color(0.86f, 0.82f, 0.74f, 1f), UiFont.Mono, TextAlignmentOptions.TopLeft);
            _docDetailBody.lineSpacing = 8f;
            _docDetailBody.enableAutoSizing = false;
            _docDetailBody.overflowMode = TextOverflowModes.Page;
            _docDetailBody.rectTransform.offsetMin = new Vector2(320f, 28f);
            _docDetailBody.rectTransform.offsetMax = new Vector2(-30f, -170f);

            // Page switch under the page art: "PAGE  [1] [2] [3]" (hidden for one-page documents)
            _docPageRow = CretaceaUiFactory.CreatePanel(_docRecovered.transform, "PageRow", Color.clear,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -462f), new Vector2(292f, -414f));
            TextMeshProUGUI pk = CretaceaUiFactory.CreateText(_docPageRow, "Kicker", "PAGE", 20f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.MidlineLeft);
            pk.characterSpacing = 3f;
            pk.rectTransform.offsetMax = new Vector2(-190f, 0f);
            _docPageButtons = new Button[MaxDocPages];
            for (int k = 0; k < MaxDocPages; k++)
            {
                int pageIndex = k;
                Button pb = CretaceaUiFactory.CreateButton(_docPageRow, $"Page_{k}", (k + 1).ToString(), new Vector2(46f, 44f), () => ShowDocPage(pageIndex), fontSize: 21f);
                RectTransform prt = pb.GetComponent<RectTransform>();
                prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
                prt.pivot = new Vector2(0f, 0.5f);
                prt.anchoredPosition = new Vector2(66f + k * 52f, 0f);
                TextMeshProUGUI pl = pb.GetComponentInChildren<TextMeshProUGUI>();
                pl.alignment = TextAlignmentOptions.Center;
                pl.rectTransform.offsetMin = pl.rectTransform.offsetMax = Vector2.zero;
                _docPageButtons[k] = pb;
            }

            // Not yet recovered: a faint page outline and one hint line; the screen stays dark
            _docMissing = CretaceaUiFactory.CreatePanel(right, "Missing", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            RectTransform outline = CretaceaUiFactory.CreateAnchoredPanel(_docMissing.transform, "PageOutline", new Color(0.051f, 0.051f, 0.051f, 1f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 70f), new Vector2(170f, 230f));
            CretaceaUiFactory.StyleCard(outline, 3f, 0f, new Color(CretaceaUiFactory.TextMuted.r, CretaceaUiFactory.TextMuted.g, CretaceaUiFactory.TextMuted.b, 0.35f));
            for (int k = 0; k < 6; k++)
            {
                float w = k == 0 ? 0.55f : (k == 5 ? 0.45f : 0.8f);
                CretaceaUiFactory.CreatePanel(outline, $"Line{k}", new Color(CretaceaUiFactory.TextMuted.r, CretaceaUiFactory.TextMuted.g, CretaceaUiFactory.TextMuted.b, 0.12f),
                    new Vector2(0.1f, 1f), new Vector2(0.1f + w, 1f), new Vector2(0f, -40f - k * 28f), new Vector2(0f, -32f - k * 28f));
            }
            TextMeshProUGUI mt = CretaceaUiFactory.CreateText(_docMissing.transform, "Title", "NOT FOUND", 30f, CretaceaUiFactory.TextMuted, UiFont.Heading, TextAlignmentOptions.Top);
            mt.characterSpacing = 5f;
            mt.rectTransform.anchorMin = new Vector2(0f, 0.5f); mt.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            mt.rectTransform.offsetMin = new Vector2(40f, -110f); mt.rectTransform.offsetMax = new Vector2(-40f, -70f);
            _docMissingHint = CretaceaUiFactory.CreateText(_docMissing.transform, "Hint", string.Empty, 23f, CretaceaUiFactory.TextMuted, UiFont.Body, TextAlignmentOptions.Top);
            _docMissingHint.rectTransform.anchorMin = new Vector2(0f, 0.5f); _docMissingHint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _docMissingHint.rectTransform.offsetMin = new Vector2(60f, -170f); _docMissingHint.rectTransform.offsetMax = new Vector2(-60f, -118f);

            return page.gameObject;
        }

        /// <summary>Small page icon: paper with text lines when collected, an empty outline when missing.</summary>
        private static RectTransform BuildPageIcon(Transform parent, Vector2 pos, Vector2 size)
        {
            RectTransform icon = CretaceaUiFactory.CreateAnchoredPanel(parent, "Icon", PaperTone, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), pos, size);
            CretaceaUiFactory.StyleCard(icon, 2f, 0f, new Color(0f, 0f, 0f, 0f));
            for (int k = 0; k < 4; k++)
            {
                float w = k == 3 ? 0.4f : 0.7f;
                CretaceaUiFactory.CreatePanel(icon, $"L{k}", new Color(0.30f, 0.25f, 0.19f, 0.85f),
                    new Vector2(0.15f, 1f), new Vector2(0.15f + w, 1f), new Vector2(0f, -11f - k * 8f), new Vector2(0f, -8f - k * 8f));
            }
            return icon;
        }

        private static void SetPageIcon(RectTransform icon, bool collected)
        {
            if (icon == null) return;
            CretaceaPanelGraphic g = icon.GetComponent<CretaceaPanelGraphic>();
            if (g != null)
            {
                g.color = collected ? PaperTone : SlotFill;
                g.SetBorder(collected ? new Color(0f, 0f, 0f, 0f) : new Color(CretaceaUiFactory.TextMuted.r, CretaceaUiFactory.TextMuted.g, CretaceaUiFactory.TextMuted.b, 0.55f), 1.5f);
            }
            for (int k = 0; k < icon.childCount; k++) icon.GetChild(k).gameObject.SetActive(collected);
        }

        /// <summary>The systems tab stays locked (dimmed, not selectable) until the manual has been read.</summary>
        private static bool SystemsUnlocked => GameState.Instance != null && GameState.Instance.GetFlag("manual_read");

        private bool TabUsable(int i) => i != 1 || SystemsUnlocked;

        private void SelectTab(int idx)
        {
            if (!TabUsable(idx)) idx = TabUsable(_activeTabIndex) ? _activeTabIndex : 0;
            _activeTabIndex = Mathf.Clamp(idx, 0, _tabPages.Length - 1);
            for (int i = 0; i < _tabPages.Length; i++)
            {
                if (_tabPages[i] != null) _tabPages[i].SetActive(i == _activeTabIndex);
                if (_tabButtons[i] != null) CretaceaUiFactory.SetButtonChosen(_tabButtons[i], i == _activeTabIndex);
            }
            RefreshAllTabs();
            WireNavigation();
        }

        /// <summary>Explicit keyboard / gamepad navigation: tabs left/right, down into the document list, list up/down.</summary>
        private void WireNavigation()
        {
            if (_tabButtons == null) return;
            bool docs = _activeTabIndex == 2 && _docButtons != null && _docButtons.Length > 0;
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                if (_tabButtons[i] == null) continue;
                Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                int l = (i + _tabButtons.Length - 1) % _tabButtons.Length, r = (i + 1) % _tabButtons.Length;
                if (!TabUsable(l)) l = (l + _tabButtons.Length - 1) % _tabButtons.Length;   // step over the locked tab
                if (!TabUsable(r)) r = (r + 1) % _tabButtons.Length;
                n.selectOnLeft = _tabButtons[l];
                n.selectOnRight = _tabButtons[r];
                bool views = _activeTabIndex == 0 && _mapViewButtons != null && _mapViewButtons[0] != null && _mapViewButtons[0].gameObject.activeSelf;
                n.selectOnDown = docs ? _docButtons[Mathf.Clamp(_selectedDocIndex, 0, _docButtons.Length - 1)]
                    : (views ? _mapViewButtons[Mathf.Clamp(_mapView, 0, 1)] : null);
                _tabButtons[i].navigation = n;
            }
            if (_mapViewButtons != null)
            {
                for (int i = 0; i < _mapViewButtons.Length; i++)
                {
                    if (_mapViewButtons[i] == null) continue;
                    Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                    n.selectOnLeft = _mapViewButtons[(i + 1) % 2];
                    n.selectOnRight = _mapViewButtons[(i + 1) % 2];
                    n.selectOnUp = _tabButtons[0];
                    _mapViewButtons[i].navigation = n;
                }
            }
            if (_docButtons == null) return;
            Button firstPage = _docPageButtons != null && _docPageButtons[0] != null && _docPageButtons[0].gameObject.activeInHierarchy ? _docPageButtons[Mathf.Clamp(_docPage, 0, MaxDocPages - 1)] : null;
            for (int i = 0; i < _docButtons.Length; i++)
            {
                if (_docButtons[i] == null) continue;
                Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = i > 0 ? _docButtons[i - 1] : _tabButtons[2];
                n.selectOnDown = i < _docButtons.Length - 1 ? _docButtons[i + 1] : null;
                n.selectOnRight = firstPage;
                _docButtons[i].navigation = n;
            }
            if (_docPageButtons == null) return;
            int shown = Mathf.Min(_docPages.Count, MaxDocPages);
            for (int k = 0; k < _docPageButtons.Length; k++)
            {
                if (_docPageButtons[k] == null) continue;
                Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnLeft = k > 0 ? _docPageButtons[k - 1] : _docButtons[Mathf.Clamp(_selectedDocIndex, 0, _docButtons.Length - 1)];
                n.selectOnRight = k + 1 < shown ? _docPageButtons[k + 1] : null;
                n.selectOnUp = _tabButtons[2];
                _docPageButtons[k].navigation = n;
            }
        }

        private void RefreshAllTabs()
        {
            RefreshMapTab();
            RefreshSystemsTab();
            RefreshDocumentsTab();
        }

        private void RefreshMapTab()
        {
            GameState gs = GameState.Instance;
            if (gs == null) return;
            bool manual = gs.GetFlag("manual_read");
            if (_mapZoneTitleText != null) _mapZoneTitleText.text = GameState.GetChapterTitle(gs.CurrentCheckpoint);

            string copy = ObjectiveGuide.Display(gs.CurrentObjective, PlayerXZ());
            bool hasObjective = !string.IsNullOrEmpty(copy);
            if (_mapObjectiveBodyText != null)
            {
                string obj = hasObjective ? copy : "No active objective.";
                if (CretaceaInput.Instance != null) obj = CretaceaInput.Instance.FormatPrompt(obj);
                _mapObjectiveBodyText.text = CretaceaUiFactory.FormatKeyTokens(obj);
                float bodyH = _mapObjectiveBodyText.GetPreferredValues(_mapObjectiveBodyText.text, 360f, 0f).y;
                if (_objectivePlacePositionRow != null)
                {
                    float top = -116f - Mathf.Max(30f, bodyH) - 16f;
                    _objectivePlacePositionRow.offsetMin = new Vector2(28f, top - 34f);
                    _objectivePlacePositionRow.offsetMax = new Vector2(-28f, top);
                }
            }
            ObjectiveGuide.Target(gs, PlayerXZ(), out string place);
            if (_objectivePlaceText != null) _objectivePlaceText.text = place;
            if (_objectivePlacePositionRow != null) _objectivePlacePositionRow.gameObject.SetActive(hasObjective && !string.IsNullOrEmpty(place));

            bool aux = gs.GetFlag("aux_power"), e0Open = gs.GetFlag("e0_open");
            float e0 = e0Open ? 1f : gs.E0GateProgress;
            // Before the manual the player knows of no way out: no substation, no E-0 on the tablet yet.
            if (_routeSubstationLabel != null) _routeSubstationLabel.text = manual ? "SUBSTATION 3" : "EXIT ROUTE";
            if (_routeSubstationValue != null)
            {
                _routeSubstationValue.text = !manual ? "UNKNOWN" : (aux ? "CIRCUIT B ON" : (gs.AllBreakersReset ? "STARTING UP" : "OFFLINE"));
                _routeSubstationValue.color = !manual ? CretaceaUiFactory.TextMuted : (aux ? CretaceaUiFactory.TextPrimary : CretaceaUiFactory.AccentAmber);
            }
            if (_routeE0Label != null) _routeE0Label.text = manual ? "EXIT E-0 GATE" : string.Empty;
            if (_routeE0Value != null)
            {
                _routeE0Value.text = !manual ? string.Empty : (e0Open ? "OPEN" : (aux ? $"{Mathf.RoundToInt(e0 * 100f)}% CYCLED" : "NO POWER"));
                _routeE0Value.color = e0Open ? CretaceaUiFactory.AccentGreen : (aux ? CretaceaUiFactory.TextPrimary : CretaceaUiFactory.TextMuted);
            }
            if (_routeE0Track != null) _routeE0Track.SetActive(manual);
            if (_routeE0Fill != null) _routeE0Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(e0), 1f);
            if (_routeKeyValue != null)
            {
                _routeKeyValue.text = gs.HasMasterKey ? "ACQUIRED" : "NOT FOUND";
                _routeKeyValue.color = gs.HasMasterKey ? CretaceaUiFactory.TextPrimary : CretaceaUiFactory.TextMuted;
            }
            if (_routeDocsValue != null) _routeDocsValue.text = $"{gs.CollectedDocumentCount} / {GameState.TotalDocumentCount}";
            // Before the manual there is no E-0 row and no master key (the key is clipped to the manual): the
            // documents row closes up under the route line.
            if (_routeKeyLabel != null) _routeKeyLabel.gameObject.SetActive(manual);
            if (_routeKeyValue != null) _routeKeyValue.gameObject.SetActive(manual);
            PlaceStatusRow(_routeKeyLabel, _routeKeyValue, 2);
            PlaceStatusRow(_routeDocsLabel, _routeDocsValue, manual ? 3 : 1);
            if (_routeStatusRect != null) _routeStatusRect.offsetMax = new Vector2(-20f, manual ? 284f : 180f);

            int stage = !manual ? 0 : (!aux ? 1 : (!e0Open ? 2 : 3));
            if (_parkMap != null) _parkMap.SetRouteStage(stage);
            if (_vcMap != null) _vcMap.SetRouteStage(stage);
            if (_mapViewButtons != null) foreach (Button b in _mapViewButtons) if (b != null) b.gameObject.SetActive(manual);
            if (!manual) _mapView = 0;
            ApplyMapView();
            UpdateLiveMapMarkers();
            // the view buttons appear with the manual: re-wire pad navigation (tab down -> view buttons)
            WireNavigation();
        }

        /// <summary>On opening: the floor plan inside the Visitor Center (and before the manual), the park map outside.</summary>
        private void ChooseDefaultMapView()
        {
            GameState gs = GameState.Instance;
            bool manual = gs != null && gs.GetFlag("manual_read");
            _mapView = manual && !ParkMapView.VisitorCenterBounds.Contains(PlayerXZ()) ? 1 : 0;
        }

        private void SetMapView(int view)
        {
            GameState gs = GameState.Instance;
            _mapView = gs != null && gs.GetFlag("manual_read") ? Mathf.Clamp(view, 0, 1) : 0;
            ApplyMapView();
            UpdateLiveMapMarkers();
        }

        private void ApplyMapView()
        {
            if (_parkMap != null) _parkMap.gameObject.SetActive(_mapView == 1);
            if (_vcMap != null) _vcMap.gameObject.SetActive(_mapView == 0);
            if (_mapHeaderText != null)
                _mapHeaderText.text = _mapView == 1 ? "ISLA TORMENTA  \u00B7  ROUTE TO SERVICE EXIT E-0" : "VISITOR CENTER  \u00B7  FLOOR PLAN";
            if (_mapViewButtons != null)
            {
                for (int i = 0; i < _mapViewButtons.Length; i++)
                {
                    Button b = _mapViewButtons[i];
                    if (b == null) continue;
                    CretaceaUiFactory.SetButtonChosen(b, i == _mapView);
                }
            }
        }

        private static Vector2 PlayerXZ()
        {
            Camera cam = Camera.main;
            if (cam == null) return new Vector2(-64.5f, 28f);
            Vector3 p = cam.transform.position;
            return new Vector2(p.x, p.z);
        }

        private void UpdateLiveMapMarkers()
        {
            GameState gs = GameState.Instance;
            if (gs == null) return;
            Camera cam = Camera.main;
            Vector2 player = PlayerXZ();
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            Vector2 target = ObjectiveGuide.Target(gs, player, out _);
            bool show = !string.IsNullOrEmpty(gs.CurrentObjective);
            ParkMapView v = _mapView == 1 ? _parkMap : _vcMap;
            if (v != null) v.UpdateMarkers(player, yaw, target, show);
        }

        private TextMeshProUGUI _footerNote;

        private void RefreshSystemsTab()
        {
            GameState gs = GameState.Instance;
            if (gs == null || _substationStepDots == null) return;

            bool manual = gs.GetFlag("manual_read");
            if (_systemsNoData != null) _systemsNoData.SetActive(!manual);
            // the locked tab says how it unlocks
            if (_footerNote != null)
            {
                string note = manual ? string.Empty : "SYSTEMS LOCKED  \u00B7  READ THE EVACUATION MANUAL TO LOAD THE SCHEMATIC";
                if (_footerNote.text != note) _footerNote.text = note;
            }
            // the tab names nothing the manual has not revealed yet
            if (_tabButtons != null && _tabButtons.Length > 1 && _tabButtons[1] != null)
            {
                TextMeshProUGUI tl = _tabButtons[1].GetComponentInChildren<TextMeshProUGUI>();
                string want = manual ? "SUBSTATION & SYSTEMS" : "SYSTEMS  \u00B7  LOCKED";
                if (tl != null && tl.text != want) tl.text = want;
                if (_tabButtons[1].interactable != manual)
                {
                    _tabButtons[1].interactable = manual;
                    CretaceaButtonFx tfx = _tabButtons[1].GetComponent<CretaceaButtonFx>();
                    if (tfx != null) tfx.SetBaseColor(tfx.baseColor);
                }
            }
            if (_systemsKicker != null) _systemsKicker.text = "FIELD SYSTEMS";
            if (_systemsHeader != null)
            {
                _systemsHeader.gameObject.SetActive(manual);
                _systemsHeader.text = $"SUBSTATION 3  \u00B7  AUXILIARY CIRCUIT B  \u00B7  BREAKERS {gs.BreakersResetCount} / 3";
            }
            if (_systemsBody != null) foreach (GameObject go in _systemsBody) if (go != null) go.SetActive(manual);
            for (int i = 0; i < _substationStepDots.Length; i++)
            {
                Transform row = _substationStepDots[i] != null ? _substationStepDots[i].transform.parent : null;
                if (row != null) row.gameObject.SetActive(manual);
            }
            if (!manual) return;

            bool[] done =
            {
                gs.IsBreakerReset(0),
                gs.IsBreakerReset(1),
                gs.IsBreakerReset(2),
                gs.FuelPrimed,
                gs.GeneratorRunning,
                gs.TransferComplete
            };

            for (int i = 0; i < done.Length && i < _substationStepDots.Length; i++)
            {
                bool activeNext = !done[i] && (i < 3 || (i == 3 && gs.AllBreakersReset) || (i == 4 && gs.FuelPrimed) || (i == 5 && gs.GeneratorRunning));
                bool parallel = i < 3;   // the three breakers can be reset in any order: TO DO, not NEXT
                // done: solid off-white dot, muted text, DONE; next: amber dot, NEXT; locked: hollow muted ring, LOCKED.
                // Green stays reserved for the escape route (E-0 bar); coral for danger.
                Image dot = _substationStepDots[i];
                Image rowBg = dot.transform.parent != null ? dot.transform.parent.GetComponent<Image>() : null;
                // only the single NEXT row gets the warm plate; parallel TO DO rows keep the neutral one
                if (rowBg != null) rowBg.color = activeNext && !parallel ? new Color(0.16f, 0.11f, 0.05f, 0.85f) : new Color(0.051f, 0.051f, 0.051f, 1f);
                dot.sprite = done[i] || activeNext ? CretaceaUiFactory.GetCircleSprite() : CretaceaUiFactory.GetRadialRingSprite();
                dot.color = done[i] ? CretaceaUiFactory.TextPrimary : (activeNext ? CretaceaUiFactory.AccentAmber : CretaceaUiFactory.TextMuted);
                _substationStepTexts[i].color = done[i] ? CretaceaUiFactory.TextMuted : (activeNext ? CretaceaUiFactory.TextPrimary : new Color(0.80f, 0.77f, 0.70f, 1f));
                if (_substationStepStates != null && _substationStepStates[i] != null)
                {
                    _substationStepStates[i].text = done[i] ? "DONE" : (activeNext ? (parallel ? "TO DO" : "NEXT") : "LOCKED");
                    _substationStepStates[i].color = done[i] ? CretaceaUiFactory.TextMuted : (activeNext ? CretaceaUiFactory.AccentAmber : new Color(0.55f, 0.52f, 0.46f, 1f));
                }
            }

            if (_e0StatusText != null)
            {
                if (gs.GetFlag("e0_open"))
                    _e0StatusText.text = "SERVICE EXIT E-0  \u00B7  OPEN";
                else if (gs.GetFlag("aux_power"))
                    _e0StatusText.text = $"SERVICE EXIT E-0  \u00B7  POWERED  \u00B7  {Mathf.RoundToInt(gs.E0GateProgress * 100f)}% CYCLED";
                else
                    _e0StatusText.text = "SERVICE EXIT E-0  \u00B7  NO POWER UNTIL CIRCUIT B IS ON";
            }

            if (_e0BarFill != null)
            {
                _e0BarFill.rectTransform.anchorMax = new Vector2(gs.GetFlag("e0_open") ? 1f : gs.E0GateProgress, 1f);
            }
        }

        // Small amber-muted caps tag in front of a missing document's hint line ("NOT FOUND  ·  Records Annex ...").
        private const string MissingTag = "<size=82%><color=#C9A15E><b>NOT FOUND</b></color></size>  \u00B7  ";

        private static string ShortPlace(string location)
        {
            int comma = string.IsNullOrEmpty(location) ? -1 : location.IndexOf(',');
            return comma > 0 ? location.Substring(0, comma) : location;
        }

        private void RefreshDocumentsTab()
        {
            GameState gs = GameState.Instance;
            int count = gs != null ? gs.CollectedDocumentCount : 0;
            if (_docsHeaderText != null) _docsHeaderText.text = $"DOCUMENTS  \u00B7  {count} / {GameState.TotalDocumentCount}";

            for (int i = 0; i < ArchiveDocs.Length && i < _docButtonLabels.Length; i++)
            {
                bool unlocked = gs != null && gs.HasDocument(ArchiveDocs[i].Id);
                bool known = unlocked || IsDocLocationKnown(gs, i);
                // One wording for the missing state everywhere (row tag, detail panel): NOT FOUND. Known entries keep
                // their title, so the list names the manual the objective asks for.
                _docButtonLabels[i].text = known ? ArchiveDocs[i].Title.ToUpperInvariant() : "UNKNOWN DOCUMENT";
                _docButtonLabels[i].color = unlocked ? CretaceaUiFactory.TextPrimary : CretaceaUiFactory.TextMuted;
                CretaceaButtonFx dfx = _docButtonLabels[i].GetComponentInParent<CretaceaButtonFx>();
                if (dfx != null) dfx.labelColor = _docButtonLabels[i].color;
                if (_docRowHints != null && _docRowHints[i] != null)
                {
                    _docRowHints[i].text = unlocked ? ArchiveDocs[i].Location
                        : (known ? MissingTag + ShortPlace(ArchiveDocs[i].Location) : MissingTag + "Turns up later tonight");
                    _docRowHints[i].color = unlocked ? CretaceaUiFactory.TextMuted : new Color(0.55f, 0.52f, 0.46f, 1f);
                }
                if (_docRowIcons != null) SetPageIcon(_docRowIcons[i], unlocked);
            }

            SelectDocument(_selectedDocIndex);
        }

        // Chapter in which each archive document's zone is reached (same order as ArchiveDocs). Before that the list
        // shows an unnamed entry, so the tablet never spoils the route or the T. rex valley.
        private static readonly CheckpointId[] DocRevealChapter =
        {
            CheckpointId.Night, CheckpointId.Records, CheckpointId.Dock, CheckpointId.Paddock, CheckpointId.Yard
        };

        private static bool IsDocLocationKnown(GameState gs, int index)
        {
            if (gs == null || index < 0 || index >= ArchiveDocs.Length) return false;
            if (gs.HasDocument(ArchiveDocs[index].Id)) return true;
            // Feed Prep opens with the manual's master key (chapter 2): its note is known from then on.
            if (ArchiveDocs[index].Id == "keeper_note" && gs.GetFlag("manual_read")) return true;
            CheckpointId reveal = index < DocRevealChapter.Length ? DocRevealChapter[index] : CheckpointId.Yard;
            return (int)gs.CurrentCheckpoint >= (int)reveal;
        }

        private void SelectDocument(int idx)
        {
            _selectedDocIndex = Mathf.Clamp(idx, 0, ArchiveDocs.Length - 1);
            DocArchiveEntry entry = ArchiveDocs[_selectedDocIndex];
            if (_docButtons != null)
                for (int i = 0; i < _docButtons.Length; i++) CretaceaUiFactory.SetButtonChosen(_docButtons[i], i == _selectedDocIndex);
            // SetButtonChosen recolours labels: keep missing rows muted
            GameState gs = GameState.Instance;
            if (_docButtonLabels != null)
            {
                for (int i = 0; i < _docButtonLabels.Length; i++)
                {
                    bool got = gs != null && gs.HasDocument(ArchiveDocs[i].Id);
                    CretaceaButtonFx fx = _docButtons != null && _docButtons[i] != null ? _docButtons[i].GetComponent<CretaceaButtonFx>() : null;
                    Color lc = got ? (i == _selectedDocIndex ? new Color(1f, 0.86f, 0.62f, 1f) : CretaceaUiFactory.TextPrimary) : CretaceaUiFactory.TextMuted;
                    if (fx != null) { fx.labelColor = lc; fx.SetBaseColor(fx.baseColor); }
                }
            }
            bool unlocked = gs != null && gs.HasDocument(entry.Id);
            if (_docRecovered != null) _docRecovered.SetActive(unlocked);
            if (_docMissing != null) _docMissing.SetActive(!unlocked);

            if (!unlocked)
            {
                if (_docMissingHint != null)
                    _docMissingHint.text = IsDocLocationKnown(gs, _selectedDocIndex)
                        ? "Last known location: " + entry.Location + "."
                        : "No record of this document yet. Keep moving: it turns up further along the night.";
                return;
            }
            if (_docDetailKicker != null) _docDetailKicker.text = "RECOVERED  \u00B7  " + entry.Location.ToUpperInvariant();
            if (_docDetailTitle != null) _docDetailTitle.text = entry.Title.ToUpperInvariant();
            BuildDocPages(entry);
            ShowDocPage(0);
        }

        // ---- document pages: real pages (the manual has two scans) split further where the type would overflow ----
        private const int MaxDocPages = 4;
        private RectTransform _docPageRow;
        private Button[] _docPageButtons;
        private readonly List<DocPage> _docPages = new List<DocPage>();
        private int _docPage;
        private struct DocPage { public string Thumb, Text; public int Sub; }

        private void BuildDocPages(DocArchiveEntry entry)
        {
            _docPages.Clear();
            if (_docDetailBody == null) return;
            bool hand = IsHandwritten(entry.Id);
            CretaceaUiFactory.SetFont(_docDetailBody, hand ? UiFont.Hand : UiFont.Mono);
            _docDetailBody.fontSize = hand ? 32f : 23f;
            _docDetailBody.lineSpacing = hand ? -2f : 8f;
            string[] thumbs = entry.Id == "manual" ? new[] { "manual_p1", "manual_p2" } : new[] { entry.AssetKey };
            foreach (string thumb in thumbs)
            {
                string text = GetPageTranscript(entry.Id, thumb);
                _docDetailBody.pageToDisplay = 1;
                _docDetailBody.text = text;
                _docDetailBody.ForceMeshUpdate(true, true);
                int n = Mathf.Max(1, _docDetailBody.textInfo.pageCount);
                for (int k = 1; k <= n && _docPages.Count < MaxDocPages; k++) _docPages.Add(new DocPage { Thumb = thumb, Text = text, Sub = k });
            }
        }

        private void ShowDocPage(int index)
        {
            if (_docPages.Count == 0 || _docDetailBody == null) return;
            _docPage = Mathf.Clamp(index, 0, _docPages.Count - 1);
            DocPage pg = _docPages[_docPage];
            if (_docThumb != null)
            {
                Sprite spr = CretaceaUiFactory.LoadSprite($"UI/Docs/{pg.Thumb}");
                _docThumb.sprite = spr;
                _docThumb.color = spr != null ? Color.white : PaperTone;
            }
            if (_docDetailBody.text != pg.Text) _docDetailBody.text = pg.Text;
            _docDetailBody.pageToDisplay = pg.Sub;
            bool multi = _docPages.Count > 1;
            if (_docPageRow != null) _docPageRow.gameObject.SetActive(multi);
            if (_docPageButtons != null)
                for (int k = 0; k < _docPageButtons.Length; k++)
                {
                    if (_docPageButtons[k] == null) continue;
                    _docPageButtons[k].gameObject.SetActive(multi && k < _docPages.Count);
                    CretaceaUiFactory.SetButtonChosen(_docPageButtons[k], k == _docPage);
                }
            WireNavigation();
        }
    }
}
