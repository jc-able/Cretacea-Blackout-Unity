using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cretacea.Core;
using Cretacea.Player;
using Cretacea.Interact;

namespace Cretacea.Story
{
    /// <summary>
    /// Complete 7-chapter story director for Cretacea: Blackout (Unity 6 HDRP).
    /// Faithfully orchestrates every narrative beat, all 17 Fish Audio voice lines (queued sequentially
    /// with radio squelch), checkpoint save/resume, chapter-select jumps, and upgraded AAA set-pieces:
    /// - Prologue (Opening 6-shot golden-hour -> dusk -> night -> real Security Office west window push-in)
    /// - Ch. 1 Night Shift (North-facing CCTV spawn, HasFlashlight=false, desk pickup, clock-in, Control Room
    ///   17 m window Talon Ridge fail-open breakout, Lobby skylight crack & shutter, Staff Exit real collapse)
    /// - Ch. 2 Records Annex (2-page 1996 Manual + Master Key, Feed Prep raptor stealth, visible dock_roller)
    /// - Ch. 3 Service Paths (Open North T-branches, wrecked kiosk, timed lightning raptor sprint crossing, bent gate)
    /// - Ch. 4 Tyrant Valley (T. rex stream lightning reveal, Jeep 5/5 radio log, Substation 3 arrival)
    /// - Ch. 5 Substation 3 (Contextual 4-stage Circuit B restoration, east-only green beacon ripple X >= 286,
    ///   T. rex fence smash & rubber-banded chase to Old Yard squeeze gap)
    /// - Ch. 6 Old Maintenance Yard & Dawn Coast Ending (2-raptor hunt, 1996 site plan, 9 s saved E-0 gate
    ///   swinging outward +X, gate slam on raptors, elevated Cinemachine crane shot & open dawn ocean vista!)
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        public static StoryDirector Instance { get; private set; }

        [Header("Core References (Auto-Resolved if Empty)")]
        [SerializeField] private PlayerController player;
        [SerializeField] private FlashlightController flashlight;
        [SerializeField] private CameraDirector cameraDirector;
        [SerializeField] private LightGroupController lightGroups;
        [SerializeField] private AtmosphereController atmosphere;
        [SerializeField] private WorldRegistry world;

        [Header("Startup Mode")]
        [SerializeField] private bool autoStartOnAwake = true;
        [SerializeField] private CheckpointId editorStartCheckpoint = CheckpointId.Intro;

        [Header("Optional VFX Spark Anchors")]
        [SerializeField] private ParticleSystem shutterSparksVfx;
        [SerializeField] private ParticleSystem pathLampSparksVfx;

        private readonly Queue<VoiceRequest> _voiceQueue = new Queue<VoiceRequest>();
        private Coroutine _voicePumpRoutine;
        private Coroutine _activeSequenceRoutine;

        private IDinoController _trex;
        private readonly List<IDinoController> _activeRaptors = new List<IDinoController>();
        private readonly HashSet<string> _firedBeats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, GameObject> _namedObjectCache = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

        private bool _chaseActive;
        private bool _endingStarted;
        private bool _skipRequested;
        private float _skipHoldTimer;
        private float _e0LastGrindAudioProgress;
        private float _sparkTimer = 1.2f;

        private readonly struct VoiceRequest
        {
            public readonly string VoId;
            public readonly string Speaker;
            public readonly string Text;
            public readonly float Duration;
            public readonly bool RadioSquelch;

            public VoiceRequest(string voId, string speaker, string text, float duration, bool radioSquelch)
            {
                VoId = voId;
                Speaker = speaker;
                Text = text;
                Duration = duration;
                RadioSquelch = radioSquelch;
            }
        }

        // Canonical Tyrant Valley T. rex 9-point patrol loop (Unity Space: X east, Z north)
        private static readonly Vector3[] TrexPatrolPointsXZ =
        {
            new Vector3(176f, 0f, 58f),
            new Vector3(212f, 0f, 40f),
            new Vector3(238f, 0f, 82f),
            new Vector3(300f, 0f, 58f),
            new Vector3(334f, 0f, -26f),
            new Vector3(284f, 0f, -62f),
            new Vector3(222f, 0f, -52f),
            new Vector3(168f, 0f, -34f),
            new Vector3(150f, 0f, 18f)
        };

        private void Awake()
        {
            Instance = this;
            ResolveDependencies();
        }

        private void OnEnable()
        {
            GameEvents.OnStoryTriggerEntered += OnStoryTriggerEntered;
            GameEvents.OnFlashlightPickedUp += OnFlashlightPickedUp;
            GameEvents.OnInteriorChanged += OnInteriorChanged;
            GameEvents.OnDinoStateChanged += OnDinoStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnStoryTriggerEntered -= OnStoryTriggerEntered;
            GameEvents.OnFlashlightPickedUp -= OnFlashlightPickedUp;
            GameEvents.OnInteriorChanged -= OnInteriorChanged;
            GameEvents.OnDinoStateChanged -= OnDinoStateChanged;
        }

        private void Start()
        {
            ResolveDependencies();
            WireSceneInteractables();

            if (autoStartOnAwake)
            {
                GameState gs = GameState.EnsureInstance();
                CheckpointId startCp = gs.GetFlag("launch_from_menu") ? gs.CurrentCheckpoint : editorStartCheckpoint;
                gs.ClearFlag("launch_from_menu");
                StartFromCheckpoint(startCp);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void ResolveDependencies()
        {
            GameState.EnsureInstance();
            CretaceaInput.EnsureInstance();
            if (world == null) world = WorldRegistry.EnsureInstance();
            if (lightGroups == null) lightGroups = LightGroupController.EnsureInstance();
            if (atmosphere == null) atmosphere = AtmosphereController.EnsureInstance();
            if (player == null) player = FindAnyObjectByType<PlayerController>();
            if (flashlight == null && player != null) flashlight = player.Flashlight;
            if (cameraDirector == null) cameraDirector = FindAnyObjectByType<CameraDirector>();

            // P0-4 FIX: Auto-resolve optional spark VFX anchors cleanly so unassigned Inspector refs never throw!
            if (pathLampSparksVfx == null)
            {
                GameObject sparkObj = world != null ? world.GetObject("PathLampSparksVfx") : null;
                if (sparkObj == null) sparkObj = GameObject.Find("PathLampSparksVfx");
                if (sparkObj != null) pathLampSparksVfx = sparkObj.GetComponent<ParticleSystem>();
            }
            if (shutterSparksVfx == null)
            {
                GameObject shutterSparkObj = world != null ? world.GetObject("ShutterSparksVfx") : null;
                if (shutterSparkObj == null) shutterSparkObj = GameObject.Find("ShutterSparksVfx");
                if (shutterSparkObj != null) shutterSparksVfx = shutterSparkObj.GetComponent<ParticleSystem>();
            }

            // P1-10 FIX: Cache toggleable WorldAnchor objects BEFORE any SetActive(false) unregisters them!
            CacheNamedObject("lobby_shutter_closed");
            CacheNamedObject("lobby_damage");
            CacheNamedObject("staff_exit_collapse");
            CacheNamedObject("sub_blocker");
            CacheNamedObject("generator");
        }

        private void CacheNamedObject(string id)
        {
            if (world == null || string.IsNullOrWhiteSpace(id)) return;
            GameObject obj = world.GetObject(id);
            if (obj != null) _namedObjectCache[id] = obj;
        }

        private static bool IsAlive(IDinoController dino)
        {
            if (dino == null) return false;
            if (dino is UnityEngine.Object uo && uo == null) return false;
            return true;
        }

        private void Update()
        {
            GameState gs = GameState.Instance;
            if (gs != null && gs.IsPaused) return;

            UpdateCutsceneSkipHold();
            UpdateChaseRubberBand();
            UpdateElectricalSparks();
        }

        // =========================================================================
        // PUBLIC ENTRY POINTS (CHECKPOINT / CHAPTER SELECT / RELOAD)
        // =========================================================================

        public void StartFromCheckpoint(CheckpointId checkpoint, bool freshPreset = false)
        {
            ResolveDependencies();
            StopAllStoryRoutines();
            _firedBeats.Clear();
            _chaseActive = false;
            _endingStarted = false;
            _skipRequested = false;
            _skipHoldTimer = 0f;
            _e0LastGrindAudioProgress = 0f;

            if (world != null && world.DinoSpawner != null) world.DinoSpawner.DespawnAll();
            _trex = null;
            _activeRaptors.Clear();
            InteriorZoneVolume.ResetCounter();

            GameState gs = GameState.EnsureInstance();
            gs.SetMainMenuState(false);
            // P0-5 FIX: Always clear stale cutscene state, skip prompt, letterbox, and subtitles across chapter jumps!
            gs.SetCutscene(false);
            GameEvents.RaiseCutsceneSkipPrompt(false);
            GameEvents.RaiseLetterbox(false, 0.05f);
            GameEvents.RaiseSubtitle(string.Empty, string.Empty, 0.01f);

            if (freshPreset) gs.ApplyCheckpointPreset(checkpoint);
            else gs.UnlockUpTo(checkpoint);

            if (player != null) player.ReviveForCheckpoint();
            if (cameraDirector != null) cameraDirector.SwitchToFpsCamera();
            if (pathLampSparksVfx != null) pathLampSparksVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (shutterSparksVfx != null) shutterSparksVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            switch (checkpoint)
            {
                case CheckpointId.Intro:
                    ApplyNightFacilityState(blackout: false);
                    _activeSequenceRoutine = StartCoroutine(OpeningCutsceneRoutine());
                    break;

                case CheckpointId.Night:
                    ApplyNightFacilityState(blackout: false);
                    _activeSequenceRoutine = StartCoroutine(StartChapter1NightShiftRoutine(fromMenuOrJump: true));
                    break;

                case CheckpointId.Records:
                    gs.ApplyCheckpointPreset(CheckpointId.Records);
                    ApplyNightFacilityState(blackout: true);
                    SetupChapter2Records();
                    break;

                case CheckpointId.Dock:
                    gs.ApplyCheckpointPreset(CheckpointId.Dock);
                    ApplyNightFacilityState(blackout: true);
                    SetupChapter3DockAndPaths();
                    break;

                case CheckpointId.Paddock:
                    gs.ApplyCheckpointPreset(CheckpointId.Paddock);
                    ApplyNightFacilityState(blackout: true);
                    SetupChapter4Paddock();
                    break;

                case CheckpointId.Substation:
                    ApplyNightFacilityState(blackout: true);
                    SetupChapter5Substation();
                    break;

                case CheckpointId.Yard:
                    gs.ApplyCheckpointPreset(CheckpointId.Yard);
                    ApplyNightFacilityState(blackout: true);
                    SetupChapter6Yard();
                    break;
            }
        }

        public void RestartCurrentCheckpoint()
        {
            CheckpointId cp = GameState.EnsureInstance().CurrentCheckpoint;
            StartFromCheckpoint(cp, freshPreset: false);
        }

        private void StopAllStoryRoutines()
        {
            // P0-5 FIX: Stop ALL coroutines (including OpeningCutsceneRoutine, DawnCoastEndingRoutine, LobbyBeatRoutine, etc.)
            // so stale routines never leak across chapter jumps or touch despawned dinosaurs!
            StopAllCoroutines();
            _activeSequenceRoutine = null;
            _voicePumpRoutine = null;
            _voiceQueue.Clear();
            GameEvents.RaiseStopLoop("gen_engine_loop", 0.1f);
            GameEvents.RaiseCutsceneSkipPrompt(false);
            GameEvents.RaiseLetterbox(false, 0.05f);
        }

        // =========================================================================
        // WORLD & INTERACTABLE WIRING
        // =========================================================================

        private void ApplyNightFacilityState(bool blackout)
        {
            GameState gs = GameState.EnsureInstance();
            atmosphere.SetPreset(blackout ? AtmospherePreset.NightStormBlackout : AtmospherePreset.NightCalm, 0f);
            atmosphere.LightningEnabled = true;
            atmosphere.LightningRate = blackout ? 1.0f : 0.5f;
            atmosphere.SetLobbySkylightRain(blackout);

            lightGroups.SetGroup(LightGroupId.VCMain, !blackout);
            lightGroups.SetGroup(LightGroupId.Plaza, !blackout);
            lightGroups.SetGroup(LightGroupId.Paths, !blackout);
            lightGroups.SetGroup(LightGroupId.DockMain, !blackout);
            lightGroups.SetGroup(LightGroupId.Talon, !blackout);

            lightGroups.SetGroup(LightGroupId.VCEmergency, blackout);
            lightGroups.SetGroup(LightGroupId.VCExit, true);
            lightGroups.SetGroup(LightGroupId.PaddockBattery, true);
            lightGroups.SetGroup(LightGroupId.SubBattery, true);
            lightGroups.SetGroup(LightGroupId.YardBattery, true);

            bool auxOn = gs.GetFlag("aux_power");
            lightGroups.SetGroup(LightGroupId.AuxSub, auxOn);
            lightGroups.SetGroup(LightGroupId.AuxRoute, auxOn);
            lightGroups.SetGroup(LightGroupId.AuxYard, auxOn);

            world.SetAllCctvStatic(blackout);

            // Toggle blackout storm damage, lobby shutter state, Staff Exit collapse, and Substation DinoBlocker
            SetNamedObjectActive("lobby_damage", blackout);
            SetNamedObjectActive("lobby_shutter_closed", blackout);
            SetNamedObjectActive("staff_exit_collapse", blackout && gs.GetFlag("collapse"));
            SetNamedObjectActive("sub_blocker", !gs.GetFlag("aux_power"));

            // Talon Ridge gate slides open on blackout
            IDoor talonGate = world.GetDoor("talon_gate");
            if (talonGate != null) talonGate.SetNormalizedOpen(blackout ? 1f : 0f);

            // Feed Prep wing door unlocks once manual is read
            bool wingUnlocked = blackout && gs.GetFlag("manual_read");
            IDoor wingDoor = world.GetDoor("door_wing");
            if (wingDoor != null)
            {
                wingDoor.SetLocked(!wingUnlocked, !blackout
                    ? "Feed Prep - keycard access only."
                    : "Feed Prep - the maglock failed shut. Needs the master key.");
            }

            // E-0 gate initial position
            IDoor e0Gate = world.GetDoor("e0_gate_leaves");
            if (e0Gate != null) e0Gate.SetNormalizedOpen(gs.GetFlag("e0_open") ? 1f : 0f);

            if (shutterSparksVfx != null)
            {
                if (blackout) shutterSparksVfx.Play();
                else shutterSparksVfx.Stop();
            }

            // IRONCLAD Flashlight enforcement: strictly false until picked up in Ch. 1!
            bool hasFl = blackout || gs.HasFlashlight;
            if (flashlight != null)
            {
                flashlight.SetHasFlashlight(hasFl, autoTurnOn: blackout, playEquipAnim: false);
            }
        }

        private void WireSceneInteractables()
        {
            // Security Terminal (Ch. 1 clock-in)
            if (world.GetInteractable("terminal") is SimpleInteractable terminal)
            {
                terminal.OnActivated -= OnSecurityTerminalUsed;
                terminal.OnActivated += OnSecurityTerminalUsed;
            }

            // Control Room Primary Paddock Telemetry Console (UX-5 replacement for invisible floor trigger)
            if (world.GetInteractable("control_console") is SimpleInteractable controlConsole)
            {
                controlConsole.OnActivated -= OnControlConsoleUsed;
                controlConsole.OnActivated += OnControlConsoleUsed;
            }

            // Lobby burnt shutter control
            if (world.GetInteractable("shutter_control") is SimpleInteractable shutterCtrl)
            {
                shutterCtrl.OnActivated -= OnLobbyShutterControlUsed;
                shutterCtrl.OnActivated += OnLobbyShutterControlUsed;
            }

            // Dead Paddock 4 Service Gate box
            if (world.GetInteractable("service_gate_box") is SimpleInteractable svcGateBox)
            {
                svcGateBox.OnActivated -= OnServiceGateBoxUsed;
                svcGateBox.OnActivated += OnServiceGateBoxUsed;
            }

            // E-0 Gate Control Box (9.0 s cumulative hold preserving progress!)
            if (world.GetInteractable("gate_box") is SimpleInteractable e0Box)
            {
                e0Box.OnHoldUpdated -= OnE0GateHoldProgress;
                e0Box.OnHoldUpdated += OnE0GateHoldProgress;
                e0Box.OnActivated -= OnE0GateHoldCompleted;
                e0Box.OnActivated += OnE0GateHoldCompleted;
            }

            // 1996 Evacuation Manual binder
            if (world.GetInteractable("doc_manual") is DocumentPickup manualDoc)
            {
                manualDoc.OnReadingCompleted -= OnManualReadingCompleted;
                manualDoc.OnReadingCompleted += OnManualReadingCompleted;
            }

            // Overturned Jeep Radio Audio Log (5/5 document fix!)
            if (world.GetInteractable("doc_jeep_radio") is DocumentPickup jeepDoc)
            {
                jeepDoc.OnReadingCompleted -= OnJeepRadioActivated;
                jeepDoc.OnReadingCompleted += OnJeepRadioActivated;
            }

            // All 6 Substation Stations (always inspectable with LockedHint!)
            SubstationStation[] stations = FindObjectsByType<SubstationStation>();
            for (int i = 0; i < stations.Length; i++)
            {
                SubstationStation st = stations[i];
                st.OnStationActivated -= OnSubstationStationCompleted;
                st.OnStationActivated += OnSubstationStationCompleted;
                st.OnGeneratorAttempted -= OnSubstationGeneratorAttempted;
                st.OnGeneratorAttempted += OnSubstationGeneratorAttempted;
            }
        }

        // =========================================================================
        // PROLOGUE — OPENING CUTSCENE (6 SHOTS, GOLDEN HOUR -> DUSK -> NIGHT)
        // =========================================================================

        private IEnumerator OpeningCutsceneRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetCutscene(true);
            if (player != null) player.SetControlEnabled(false);
            if (flashlight != null)
            {
                flashlight.SetHasFlashlight(false, autoTurnOn: false, playEquipAnim: false);
                flashlight.SetCutsceneSuppressed(true);
            }

            // Daytime golden hour: retract lobby security shutters OPEN & light park naturally
            atmosphere.SetPreset(AtmospherePreset.GoldenDay, 0f);
            atmosphere.LightningEnabled = false;
            SetNamedObjectActive("lobby_shutter_closed", false);
            SetNamedObjectActive("lobby_damage", false);
            // Afternoon: interior mains only (seen through the glass); every outdoor lamp is off until dusk.
            lightGroups.SetGroup(LightGroupId.VCMain, true);
            lightGroups.SetGroup(LightGroupId.Talon, false);
            lightGroups.SetGroup(LightGroupId.Plaza, false);
            lightGroups.SetGroup(LightGroupId.Paths, false);
            lightGroups.SetGroup(LightGroupId.DockMain, false);
            lightGroups.SetGroup(LightGroupId.PaddockBattery, false);
            lightGroups.SetGroup(LightGroupId.SubBattery, false);
            lightGroups.SetGroup(LightGroupId.YardBattery, false);
            lightGroups.SetGroup(LightGroupId.VCEmergency, false);

            GameEvents.RaiseAmbienceZone("day", indoors: false, fadeSeconds: 1.0f);
            GameEvents.RaiseMusicState("calm_day", 2.0f);
            GameEvents.RaiseLetterbox(true, 0.05f);
            GameEvents.RaiseScreenFade(0f, 3.0f);
            GameEvents.RaiseCutsceneSkipPrompt(true, RequestSkipOpening);
            GameEvents.RaiseTitleCard("CRETACEA PARK", "Isla Tormenta  |  5:42 PM", 4.0f);

            // Spawn opening showcase herbivores & gallery T. rex if DinoSpawner is present
            List<IDinoController> introActors = SpawnOpeningShowcaseDinos();
            // ...and the park's afternoon crowd (groups along the facade, at the viewing rail and against the gallery glass)
            SpawnOpeningVisitors();

            // Shot 1: Establishing glide over Titan Meadows toward the sunlit Visitor Center. The glide stops short of the
            // herd (it used to fly through it) so the brachiosaurus crosses the middle of the frame with the Visitor
            // Center behind it.
            // It starts low over the lake. (Until round 3 it started at (-176, 1.5, -252), which is six metres inside
            // the bank: the game's first frame looked up through the ground into the grass.)
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(-157f, 2.8f, -231f), new Vector3(-118f, 7.5f, -186f),
                new Vector3(-104f, 6f, -168f), new Vector3(-50f, 7f, -82f),
                12.0f, 46f, 42f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Shot 2: Gliding along the bright sunlit Visitor Center glass facade. The rail actors only appear now, at the
            // cut (review 3, intro_03: waiting by the plaza during the opening glide, the rail brachiosaurus lined up with
            // the shot-1 animal into one sauropod with a forked neck). The brachiosaurus sets off at once (11 m at 1 m/s),
            // so shot 3 catches its last strides and it settles on its mark 2.5 s into that shot.
            SpawnRailShowcaseDinos(introActors);
            if (IsAlive(_introBrachioRail))
                StartCoroutine(IntroWalkToMark(_introBrachioRail, IntroBrachioRailStart, IntroBrachioRailMark, 1.0f, IntroBrachioRailHold));
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(-8f, 1.65f, -51f), new Vector3(-24f, 1.75f, -47.5f),
                new Vector3(-70f, 5.0f, -30f), new Vector3(-78f, 6.0f, -28f),
                8.5f, 50f, 46f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Shot 3: A brachiosaurus towering past the south viewing rail. The lens looks east-south-east over the rail:
            // the animal walks north-north-east across the frame in profile, its west flank to the lens and to the WSW
            // sun, and keeps its body square to its walk (no turn to the lens); the trike herd grazes in the lower right.
            Vector3 railBrachio = IsAlive(_introBrachioRail) ? _introBrachioRail.Transform.position : IntroBrachioRailMark;
            GameEvents.RaisePlaySound("sauropod_call_far", railBrachio + Vector3.up * 8f);
            // (round 4: the lens turns 10 degrees right and opens to 57 degrees, so the brachiosaurus holds the left third
            // and the three trikes stand apart on the right, each at its own depth, clear of its tail)
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(-49.0f, 2.8f, -90.3f), new Vector3(-48.0f, 2.9f, -90.5f),
                new Vector3(-21.1f, 8.6f, -109.2f), new Vector3(-20.6f, 9.2f, -109.2f),
                7.5f, 58f, 56f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Shot 4: T. rex stalking past the Viewing Gallery glass & roaring to crowd gasp/cheer
            // It walks out of the palms (~14.6 m at 3.4 m/s: on its mark at ~4.3 s), then turns 3/4 toward the crowd
            // and holds. A level lens (look targets at eye height) keeps the ceiling slab out of the frame; the glide
            // ends square on the middle pane (z -59.5), so the mullions at z -61 and -58 frame the animal.
            IDinoController introTrex = introActors.Count > 0 ? introActors[introActors.Count - 1] : null;
            if (IsAlive(introTrex) && introTrex.Species == "trex")
                StartCoroutine(IntroWalkToMark(introTrex, IntroTrexStart, IntroTrexMark, 3.4f, IntroTrexFaces));
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(45.2f, 1.65f, -55.0f), new Vector3(46.6f, 1.7f, -59.5f),
                new Vector3(64f, 2.0f, -50.5f), new Vector3(64.5f, 2.0f, -59.5f),
                6.0f, 50f, 46f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            if (IsAlive(introTrex) && introTrex.Species == "trex")
            {
                introTrex.PlayActionClip("Roar_Massive", 3.5f);
            }
            GameEvents.RaisePlaySound("trex_roar_close", IntroTrexMark + Vector3.up * 4f);
            GameEvents.RaisePlaySound("crowd_gasp_cheer", new Vector3(54f, 1.6f, -57f));
            GameEvents.RaiseCameraTrauma(0.28f, 1.4f);
            // Shot 4b: the roar, head and chest framed in the middle pane, the crowd's heads along the bottom of the frame
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(49.6f, 1.72f, -59.5f), new Vector3(50.6f, 1.78f, -59.5f),
                new Vector3(63f, 3.4f, -59.5f), new Vector3(63f, 3.8f, -59.5f),
                3.8f, 44f, 40f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Shot 5: Closing-time golden -> dusk -> storm night transition over the Plaza
            CloseOpeningVisitors();   // the park is closed: a few stragglers walk out for 3.5 s, everyone else has gone
            // ... and so have the animals of the day shots (review 3: a dark brachiosaurus neck rose through the bottom
            // edge of the closing aerial)
            for (int i = 0; i < introActors.Count; i++)
            {
                if (IsAlive(introActors[i])) introActors[i].Despawn();
            }
            introActors.Clear();
            GameEvents.RaiseTitleCard("CLOSING TIME", string.Empty, 2.4f);
            atmosphere.SetPreset(AtmospherePreset.DuskTransition, 4.0f);
            // It opens level on the west sky (the sun sinking under the storm deck at azimuth 262, the cloud front over the
            // hills) and swings down onto the plaza as the light goes (review 3: day turned to night with no sky in shot).
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(-10f, 34f, -150f), new Vector3(-20f, 30f, -136f),
                new Vector3(-160f, 58f, -112f), new Vector3(-104f, 24f, -70f),
                4.6f, 60f, 56f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Dusk: the park lights come on, a group at a time
            lightGroups.SetGroup(LightGroupId.Plaza, true, 1.2f);
            lightGroups.SetGroup(LightGroupId.Paths, true, 1.8f, electricalFlicker: true);
            lightGroups.SetGroup(LightGroupId.DockMain, true, 1.5f);
            lightGroups.SetGroup(LightGroupId.Talon, true, 2.2f, electricalFlicker: true);
            lightGroups.SetGroup(LightGroupId.PaddockBattery, true, 1.0f);
            lightGroups.SetGroup(LightGroupId.SubBattery, true, 1.0f);
            lightGroups.SetGroup(LightGroupId.YardBattery, true, 1.0f);
            GameEvents.RaiseMusicState("night_unease", 4.0f);
            GameEvents.RaiseAmbienceZone("paths", indoors: false, fadeSeconds: 4.0f);
            atmosphere.SetPreset(AtmospherePreset.NightCalm, 4.5f);
            yield return cameraDirector.GlideCutsceneShot(
                new Vector3(-20f, 30f, -136f), new Vector3(-26f, 26f, -120f),
                new Vector3(-104f, 24f, -70f), new Vector3(-40f, 2f, -30f),
                4.5f, 56f, 55f, () => _skipRequested);
            if (_skipRequested) { FinishOpeningCutscene(introActors); yield break; }

            // Shot 6: Push in on REAL recessed Security Office West exterior window (X=-72, Z=+28.5, DOOR-5 fix!)
            GameEvents.RaiseTitleCard("23:04", string.Empty, 2.0f);
            atmosphere.LightningEnabled = true;
            // The storm shows in the shot that sells it: a close strike 1.4 s in, its thunder caption while the bars are
            // still up (review 3: the flash fired as the view cut to the CCTV wall and its caption covered the flashlight).
            StartCoroutine(IntroStormFlash(1.4f));
            yield return cameraDirector.GlideCutsceneShot(
                // ends 4 m from the wall (it used to end 1.7 m out, inside the window frame: the dressed wall, the door
                // and the wall packs left the picture for the last two seconds)
                new Vector3(-86f, 3.2f, 27.0f), new Vector3(-76.2f, 1.9f, 28.4f),
                new Vector3(-65f, 1.55f, 28.5f), new Vector3(-65f, 1.45f, 28.5f),
                5.5f, 50f, 40f, () => _skipRequested);
            FinishOpeningCutscene(introActors);
        }

        private IEnumerator IntroStormFlash(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_skipRequested && GameState.EnsureInstance().InCutscene)
                // a fixed direction, a little left of the shot-6 lens: the roofline is a silhouette on the flash every run
                atmosphere.TriggerLightningFlashFrom(new Vector3(-1f, -1.6f, -0.5f), 0.9f, true);
        }

        // Opening crowd: Resources/PF_OpeningCrowd (Assets/Prefabs/Characters/Resources, built by
        // 'cretacea build_character_prefabs'); its ParkVisitorCrowd component places the 16 people as children and
        // removes itself when the atmosphere leaves GoldenDay or the cutscene ends (skip, chapter jump).
        private GameObject _openingVisitors;

        private void SpawnOpeningVisitors()
        {
            DespawnOpeningVisitors();
            GameObject crowd = Resources.Load<GameObject>("PF_OpeningCrowd");
            if (crowd != null) _openingVisitors = Instantiate(crowd);
        }

        private void CloseOpeningVisitors()
        {
            // ParkVisitorCrowd.BeginClosing (Cretacea.AI is not referenced by this assembly)
            if (_openingVisitors != null)
                _openingVisitors.SendMessage("BeginClosing", 3.5f, SendMessageOptions.DontRequireReceiver);
        }

        private void DespawnOpeningVisitors()
        {
            if (_openingVisitors != null) Destroy(_openingVisitors);
            _openingVisitors = null;
        }

        // Intro staging (review 2, intro_02/09/13). The GoldenDay sun stands in the west-south-west at 27 degrees, so:
        //  - shot 1 looks north-east with the sun behind the lens: a brachiosaurus crosses the middle of the frame in
        //    profile at ~35 m, two triceratops graze nearer;
        //  - shot 3 looks south from the viewing rail: the second brachiosaurus walks out into the sunlit meadow ~24 m
        //    past the rail (clear of the lone tree's shadow band) and turns its head to the visitors, a trike herd of
        //    three grazes in the right-hand mid-ground;
        //  - shot 4 looks east through the gallery glass: the T. rex walks out of the palms into the middle pane, turns
        //    to face the crowd and roars framed between two mullions (pane z -61..-58), clear of the gallery's shadow.
        private static readonly Vector3 IntroBrachioShot1Start = new Vector3(-95f, 0f, -146f);
        private static readonly Vector3 IntroBrachioShot1End = new Vector3(-82f, 0f, -154.5f);
        // (round 4: 3 m further north and 11 m of walk, so its tail stays left of the trike herd for the whole shot)
        private static readonly Vector3 IntroBrachioRailStart = new Vector3(-14.3f, 0f, -111.6f);   // round 4 critic: 6 m east, 2.5 m south, off the visitor promenade
        private static readonly Vector3 IntroBrachioRailMark = new Vector3(-10.0f, 0f, -101.5f);
        // straight ahead of its walk: the body stays in profile to the shot-3 lens
        private static readonly Vector3 IntroBrachioRailHold = new Vector3(-6.0f, 9f, -92.2f);
        private static readonly Vector3 IntroTrexStart = new Vector3(71f, 0f, -45.5f);
        private static readonly Vector3 IntroTrexMark = new Vector3(66.8f, 0f, -57.5f);   // 2 m north: the head lands inside the pane, clear of the right mullion (review 3)
        // south-west, ~50 deg off the shot-4b lens axis: the roar reads 3/4 with the open jaw and the sunlit flank (facing
        // the lens head-on, the snout points up out of the frame and the chest is in its own shade: ~55/255)
        private static readonly Vector3 IntroTrexFaces = new Vector3(56.8f, 2.5f, -71f);
        private IDinoController _introBrachioShot1;
        private IDinoController _introBrachioRail;

        // Walks an intro actor from 'start' to 'mark' and holds it there looking at 'holdLook'. The scripted move stops
        // 'config.stoppingDistance' short of its target on the NavMesh (2.6 m for the rex, 3 m for the brachiosaurus),
        // so it aims past the mark along the approach and this routine holds the animal once it reaches the mark.
        private IEnumerator IntroWalkToMark(IDinoController dino, Vector3 start, Vector3 mark, float speed, Vector3 holdLook)
        {
            Vector3 dir = mark - start; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.01f) { dino.ScriptHoldAndLookAt(holdLook); yield break; }
            dir /= len;
            dino.ScriptMoveTo(IntroGround(mark + dir * 4f), speed);
            float timeout = Time.time + len / Mathf.Max(0.4f, speed) * 2f + 2f;
            while (IsAlive(dino) && Time.time < timeout)
            {
                Vector3 d = dino.Transform.position - start; d.y = 0f;
                if (Vector3.Dot(d, dir) >= len - 0.1f) break;
                yield return null;
            }
            if (IsAlive(dino)) dino.ScriptHoldAndLookAt(holdLook);
        }

        private Vector3 IntroGround(Vector3 p)
        {
            p.y = world.SampleGroundHeight(p.x, p.z, p.y);
            return p;
        }

        private static float YawTowards(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            return d.sqrMagnitude < 0.0001f ? 0f : Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        private List<IDinoController> SpawnOpeningShowcaseDinos()
        {
            var list = new List<IDinoController>();
            _introBrachioShot1 = null;
            _introBrachioRail = null;
            if (world.DinoSpawner == null) return list;

            // Shot 1: brachiosaurus crossing in profile, two triceratops grazing in front of it
            IDinoController s1 = world.DinoSpawner.Spawn("brachiosaurus", IntroGround(IntroBrachioShot1Start),
                YawTowards(IntroBrachioShot1Start, IntroBrachioShot1End), "intro_sauropod_b");
            if (s1 != null)
            {
                s1.AiEnabled = false; list.Add(s1); _introBrachioShot1 = s1;
                s1.ScriptMoveTo(IntroGround(IntroBrachioShot1End), 1.15f);
            }
            Vector3[] meadowTrikes = { new Vector3(-104f, 0f, -161f), new Vector3(-96.5f, 0f, -166f) };
            float[] meadowTrikeYaw = { 75f, 128f };
            for (int i = 0; i < meadowTrikes.Length; i++)
            {
                IDinoController tr = world.DinoSpawner.Spawn("triceratops", IntroGround(meadowTrikes[i]), meadowTrikeYaw[i], $"intro_trike_m{i}");
                if (tr != null) { tr.AiEnabled = false; list.Add(tr); }
            }

            // Shot 4: the T. rex waits in the palms north-east of the gallery (last in the list: OpeningCutsceneRoutine reads it there)
            IDinoController introTrex = world.DinoSpawner.Spawn("trex", IntroGround(IntroTrexStart), YawTowards(IntroTrexStart, IntroTrexMark), "intro_trex");
            if (introTrex != null) { introTrex.AiEnabled = false; list.Add(introTrex); }

            return list;
        }

        // Shot 3's actors, spawned at the cut to shot 2 (out of every earlier frame). They go in front of the T. rex, which
        // OpeningCutsceneRoutine reads as the last entry of the list.
        private void SpawnRailShowcaseDinos(List<IDinoController> list)
        {
            if (world.DinoSpawner == null) return;
            int at = list.Count > 0 && list[list.Count - 1] != null && list[list.Count - 1].Species == "trex" ? list.Count - 1 : list.Count;
            IDinoController s2 = world.DinoSpawner.Spawn("brachiosaurus", IntroGround(IntroBrachioRailStart),
                YawTowards(IntroBrachioRailStart, IntroBrachioRailMark), "intro_sauropod_a");
            if (s2 != null) { s2.AiEnabled = false; list.Insert(at++, s2); _introBrachioRail = s2; }
            // Three trikes at three depths on the right of the shot-3 frame (26, 19 and 15 m from the lens), each in its own
            // slot with air between them and right of the brachiosaurus's tail: the far one grazes 3/4 away to the right,
            // the middle one walks away, the near one turns 3/4 toward the lens and the brachiosaurus (review 3, intro_09:
            // side-on in a row, two of them overlapped into one long animal with a head at each end).
            Vector3[] railTrikes = { new Vector3(-28.3f, 0f, -106.8f), new Vector3(-36.9f, 0f, -105.4f), new Vector3(-43.5f, 0f, -104.5f) };
            float[] railTrikeYaw = { 174f, 152f, 5.5f };
            for (int i = 0; i < railTrikes.Length; i++)
            {
                IDinoController tr = world.DinoSpawner.Spawn("triceratops", IntroGround(railTrikes[i]), railTrikeYaw[i], $"intro_trike_{i}");
                if (tr != null) { tr.AiEnabled = false; list.Insert(at++, tr); }
            }
        }

        private void RequestSkipOpening() => _skipRequested = true;

        private void FinishOpeningCutscene(List<IDinoController> introActors)
        {
            GameEvents.RaiseCutsceneSkipPrompt(false);
            for (int i = 0; i < introActors.Count; i++)
            {
                if (IsAlive(introActors[i])) introActors[i].Despawn();
            }
            introActors.Clear();

            // NOTE (FL-2 FIX): NEVER enable flashlight viewmodel when finishing Opening Cutscene!
            GameEvents.RaiseLetterbox(false, 0.5f);
            GameState.EnsureInstance().SetCutscene(false);
            if (cameraDirector != null) cameraDirector.SwitchToFpsCamera();
            _activeSequenceRoutine = StartCoroutine(StartChapter1NightShiftRoutine(fromMenuOrJump: false));
            // The first gameplay view looks down at the desk and the flashlight the objective names, not at the ceiling
            // grid and its troffers (review 3, intro_20). StartChapter1NightShiftRoutine has placed the player by now.
            if (player != null) player.Teleport(world.GetPoint("start"), yawDegrees: 0f, pitchDegrees: IntroFirstViewPitch);
        }

        private const float IntroFirstViewPitch = 15f;

        // =========================================================================
        // CHAPTER 1 — NIGHT SHIFT, BLACKOUT & TALON RIDGE BREAKOUT
        // =========================================================================

        private IEnumerator StartChapter1NightShiftRoutine(bool fromMenuOrJump)
        {
            GameState gs = GameState.EnsureInstance();
            gs.ApplyCheckpointPreset(CheckpointId.Night);
            gs.SaveCheckpoint(CheckpointId.Night);
            ApplyNightFacilityState(blackout: false);

            // POS-9 FIX: Spawn facing 0 deg NORTH (+Z) directly toward the live CCTV desk & glowing flashlight pickup!
            Vector3 startPos = world.GetPoint("start");
            if (player != null)
            {
                player.Teleport(startPos, yawDegrees: 0f, pitchDegrees: -6f);
                player.SetControlEnabled(true);
            }

            // IRONCLAD FL-1 FIX: Explicitly enforce HasFlashlight = false and desk pickup visible!
            if (flashlight != null)
            {
                flashlight.SetCutsceneSuppressed(false);
                flashlight.SetHasFlashlight(false, autoTurnOn: false, playEquipAnim: false);
            }
            // The pickup deactivates (and unregisters) itself once collected, so look it up directly:
            // a registry lookup returned null after a restart / Continue / jump and left the desk empty (soft-lock).
            FlashlightPickup deskFl = world.GetInteractable("pickup_flashlight") as FlashlightPickup;
            if (deskFl == null) deskFl = FindAnyObjectByType<FlashlightPickup>(FindObjectsInactive.Include);
            if (deskFl != null)
            {
                deskFl.ResetForChapter1();
            }

            // Security terminal starts disabled until flashlight is picked up (guides clean opening rhythm)
            if (world.GetInteractable("terminal") is SimpleInteractable term)
            {
                term.SetEnabledState(true);
                term.SetLockedState(true, "Grab your tactical flashlight from the CCTV desk first.");
            }
            if (world.GetInteractable("control_console") is SimpleInteractable ctrl)
            {
                ctrl.SetEnabledState(false);
            }

            world.GetDoor("door_security")?.Open(null, instant: true);
            world.GetDoor("door_control")?.Close(instant: true);
            // Found by the playthrough bot: swung the full 95 degrees into the Staff Corridor, the 1.6 m Control Room
            // leaf ends 0.6 m from the utility cart on the opposite wall, and a player coming from Security cannot get
            // round it into the room (the CharacterController needs 0.68 m). At 72 degrees the gap is 1.1 m.
            if (world.GetDoor("door_control") is DoorController controlDoor) controlDoor.SetOpenAngle(72f);

            GameEvents.RaiseAmbienceZone("vc_night", indoors: true, fadeSeconds: 1.5f);
            GameEvents.RaiseMusicState("night_unease", 2.0f);
            GameEvents.RaiseScreenFade(0f, fromMenuOrJump ? 1.5f : 2.0f);
            GameEvents.RaiseTitleCard("23:05", "Night Shift - Cretacea Park Visitor Center", 3.0f);

            yield return new WaitForSeconds(2.8f);
            if (!gs.HasFlashlight)
            {
                gs.SetObjective("Take your tactical flashlight from the security desk.");
            }
            EnqueueVoice("vo_dana_01", "RADIO - DANA (OPS)", "Evening, Vance. Storm's rolling in early. Grab your light and clock in.", 4.5f, radio: true);
        }

        private void OnFlashlightPickedUp()
        {
            if (world.GetInteractable("terminal") is SimpleInteractable term)
            {
                term.SetLockedState(false);
                term.SetEnabledState(true);
            }
            GameState.EnsureInstance().SetObjective("Clock in at the security terminal.");
        }

        private void OnSecurityTerminalUsed(GameObject interactor)
        {
            GameEvents.RaisePlaySound("ui_terminal_beep", world.GetPoint("terminal"));
            GameEvents.RaiseToastMessage("SHIFT START 23:07 - J. VANCE, NIGHT CARETAKER", 3.5f);
            StartCoroutine(BeginNightRoundsRoutine());
        }

        private IEnumerator BeginNightRoundsRoutine()
        {
            yield return new WaitForSeconds(1.2f);
            EnqueueVoice("vo_dana_02", "RADIO - DANA (OPS)", "Got you. Grid switchover's at midnight - Substation 1 over to the new Five.", 4.5f, radio: true);
            EnqueueVoice("vo_dana_03", "RADIO - DANA (OPS)", "Before that, log the Talon Ridge and Tyrant Valley feeds from the Control Room.", 5.0f, radio: false);
            GameState.EnsureInstance().SetObjective("Walk east to the Control Room and log the paddock feeds.");
            world.GetDoor("door_control")?.Open(null, instant: false);

            if (world.GetInteractable("control_console") is SimpleInteractable ctrl)
            {
                ctrl.SetLockedState(false);
                ctrl.SetEnabledState(true);
            }
        }

        private void OnControlConsoleUsed(GameObject interactor)
        {
            TryStartSwitchoverBlackout();
        }

        private void TryStartSwitchoverBlackout()
        {
            if (!_firedBeats.Add("switchover")) return;
            if (world.GetInteractable("control_console") is SimpleInteractable ctrl) ctrl.SetEnabledState(false);
            StartCoroutine(SwitchoverBlackoutRoutine());
        }

        private IEnumerator SwitchoverBlackoutRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetObjective(string.Empty);
            if (player != null) player.SpeedMultiplier = 0.78f;

            yield return new WaitForSeconds(0.8f);
            EnqueueVoice("vo_dana_04", "RADIO - DANA (OPS)", "You're early. Fine - I'll start the transfer now. Watch the board.", 3.5f, radio: true);
            yield return new WaitForSeconds(4.0f);

            GameEvents.RaisePlaySound("pa_chime");
            yield return new WaitForSeconds(1.3f);
            EnqueueVoice("vo_pa_01", "PA SYSTEM", "Grid transfer in progress. Containment on UPS.", 3.0f);
            yield return new WaitForSeconds(2.0f);

            // Distant Substation 5 explosion to the North (+Z = +240)
            atmosphere.TriggerLightningFlash(1.0f, withThunder: false);
            GameEvents.RaisePlaySound("amb_thunder_close");
            GameEvents.RaiseCameraTrauma(0.32f, 1.5f);
            yield return new WaitForSeconds(0.6f);

            GameEvents.RaisePlaySound("power_transformer_explosion", new Vector3(-60f, 10f, 200f));
            GameEvents.RaiseSoundCaption("[DISTANT TRANSFORMER EXPLOSION]", 2.8f);
            yield return new WaitForSeconds(0.35f);
            GameEvents.RaiseCameraTrauma(0.50f, 1.3f);

            // Rolling facility blackout — VCMain dies (including map_park_modern & status_wall!) + CCTV cuts to static
            GameEvents.RaisePlaySound("power_failure_wind_down");
            lightGroups.SetGroup(LightGroupId.VCMain, false, 1.2f, electricalFlicker: true);
            lightGroups.SetGroup(LightGroupId.Plaza, false, 2.0f, electricalFlicker: true);
            lightGroups.SetGroup(LightGroupId.Paths, false, 2.0f, electricalFlicker: true);
            lightGroups.SetGroup(LightGroupId.DockMain, false, 1.0f, electricalFlicker: true);
            world.SetAllCctvStatic(true);
            if (flashlight != null) flashlight.TriggerFlicker(1.5f, 0.75f);

            GameEvents.RaiseAmbienceZone("vc_dark", indoors: true, fadeSeconds: 0.4f);
            GameEvents.RaiseMusicState(string.Empty, 0.5f);
            yield return new WaitForSeconds(2.6f);

            // Talon Ridge floodlights drop out right before red emergency relays kick in
            lightGroups.SetGroup(LightGroupId.Talon, false, 0.4f, electricalFlicker: true);
            yield return new WaitForSeconds(1.4f);

            GameEvents.RaisePlaySound("power_emergency_on");
            lightGroups.SetGroup(LightGroupId.VCEmergency, true, 0.3f);
            gs.SetFlag("blackout");
            atmosphere.SetPreset(AtmospherePreset.NightStormBlackout, 8.0f);
            atmosphere.LightningRate = 1.0f;

            yield return new WaitForSeconds(1.0f);
            GameEvents.RaisePlaySound("pa_alarm_short");
            yield return new WaitForSeconds(1.2f);
            EnqueueVoice("vo_pa_02", "PA SYSTEM", "Warning. Grid failure. Containment systems on fail-safe.", 3.5f);
            yield return new WaitForSeconds(3.6f);

            GameEvents.RaiseMusicState("tension", 2.0f);

            // Talon Ridge Raptor Pen Gate slides +9.5 m East visibly through the un-occluded 17 m Control Room window!
            if (player != null) player.LookTowards(world.GetPoint("talon_gate_view"), 2.4f);
            yield return new WaitForSeconds(1.3f);

            atmosphere.TriggerLightningFlash(1.0f, withThunder: true);
            GameEvents.RaisePlaySound("gate_heavy_slide", new Vector3(-36f, 2f, 42f));
            if (world.GetDoor("talon_gate") is DoorController talonDoor)
            {
                talonDoor.AnimateOpenTo(1f, 6.0f);
            }
            else
            {
                world.GetDoor("talon_gate")?.Open(null, instant: false);
            }

            EnqueueVoice("vo_pa_03", "PA SYSTEM", "Containment fault. Paddock two. Paddock four.", 3.5f);
            yield return new WaitForSeconds(2.2f);

            atmosphere.TriggerLightningFlash(0.85f, withThunder: false);
            StartCoroutine(SpawnTalonBreakoutRaptorsRoutine());
            yield return new WaitForSeconds(2.4f);

            if (player != null)
            {
                player.ReleaseLook();
                player.SpeedMultiplier = 1.0f;
            }
            EnqueueVoice("vo_dana_05", "RADIO - DANA (OPS)", "Vance! The gates failed OPEN - the pens are open! Get out - main entrance, now!", 4.5f, radio: true);

            // Lock Feed Prep maglock & activate Lobby storm damage / jammed shutter
            IDoor wingDoor = world.GetDoor("door_wing");
            if (wingDoor != null) wingDoor.SetLocked(true, "Feed Prep - the maglock failed shut. Needs the old master key.");
            SetNamedObjectActive("lobby_damage", true);
            SetNamedObjectActive("lobby_shutter_closed", true);
            atmosphere.SetLobbySkylightRain(true);
            if (shutterSparksVfx != null) shutterSparksVfx.Play();

            yield return new WaitForSeconds(1.0f);
            gs.SetObjective("Get to the main entrance in the Lobby Atrium.");
        }

        private IEnumerator SpawnTalonBreakoutRaptorsRoutine()
        {
            if (world.DinoSpawner == null) yield break;
            for (int i = 0; i < 2; i++)
            {
                Vector3 spawnPos = new Vector3(-36f + i * 2.5f, 0f, 56f + i * 3f);
                IDinoController r = world.DinoSpawner.Spawn("raptor", spawnPos, 180f, $"breakout_raptor_{i}");
                if (r != null)
                {
                    r.AiEnabled = false;
                    Vector3 exitPt = new Vector3(-36f + i * 2f, 0f, 36f);
                    Vector3 fleePt = new Vector3(-80f + i * 120f, 0f, 30f);
                    GameEvents.RaisePlaySound("raptor_screech", spawnPos);
                    r.ScriptMoveTo(exitPt, 6.5f, () => r.ScriptMoveTo(fleePt, 6.5f, () => r.Despawn()));
                }
                yield return new WaitForSeconds(0.55f);
            }
        }

        private void OnLobbyShutterControlUsed(GameObject interactor)
        {
            GameEvents.RaisePlaySound("ui_denied");
            GameEvents.RaiseToastMessage("No power. The shutter motor is burnt out.", 3.0f);
        }

        private IEnumerator LobbyBeatRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetFlag("lobby_done");   // the Staff Exit beat waits for this: Dana sends you there after the Lobby
            gs.SetObjective(string.Empty);
            yield return new WaitForSeconds(0.45f);

            GameEvents.RaisePlaySound("glass_crack_large", new Vector3(-24f, 9f, 0f));
            GameEvents.RaiseSoundCaption("[ATRIUM SKYLIGHT GLASS CRACKS OVERHEAD]", 2.5f);
            if (player != null) player.LookTowards(world.GetPoint("lobby_shutter_look"), 2.8f);
            yield return new WaitForSeconds(1.5f);
            if (player != null) player.ReleaseLook();

            EnqueueVoice("vo_vance_01", "", "The security shutters are down - and the motor is burnt out.", 3.5f);
            yield return new WaitForSeconds(1.8f);

            // Scripted raptor stalks outside the south glass facade (Z = -38 -> -36)
            if (world.DinoSpawner != null)
            {
                IDinoController r = world.DinoSpawner.Spawn("raptor", new Vector3(-58f, 0f, -38f), 90f, "lobby_glass_raptor");
                if (IsAlive(r))
                {
                    r.AiEnabled = false;
                    GameEvents.RaisePlaySound("raptor_breath_close", r.Transform.position);
                    r.ScriptMoveTo(new Vector3(-36f, 0f, -36f), 1.6f, () => { if (IsAlive(r)) StartCoroutine(LobbyRaptorPauseAndFlee(r)); });
                }
            }

            yield return new WaitForSeconds(8.5f);
            gs.SetObjective("Try the Staff Exit at the west end of the Lobby.");
            EnqueueVoice("vo_dana_06", "RADIO - DANA (OPS)", "...main gate's sealed on fail-secure... try the staff exit, west corridor past Security...", 4.5f, radio: true);
        }

        private IEnumerator LobbyRaptorPauseAndFlee(IDinoController r)
        {
            if (!IsAlive(r)) yield break;
            if (player != null) r.ScriptHoldAndLookAt(player.EyePosition);
            GameEvents.RaisePlaySound("raptor_sniff", r.Transform.position);
            yield return new WaitForSeconds(2.2f);
            if (!IsAlive(r)) yield break;
            atmosphere.TriggerLightningFlash(0.65f, withThunder: false);
            GameEvents.RaisePlaySound("raptor_screech", r.Transform.position);
            r.ScriptMoveTo(new Vector3(10f, 0f, -60f), 6.5f, () => { if (IsAlive(r)) r.Despawn(); });
        }

        private IEnumerator StaffExitCollapseRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetObjective(string.Empty);
            Vector3 collapsePos = world.GetPoint("staff_exit_look");

            GameEvents.RaisePlaySound("structure_groan", collapsePos);
            GameEvents.RaiseCameraTrauma(0.26f, 1.5f);
            yield return new WaitForSeconds(1.0f);

            GameEvents.RaisePlaySound("ceiling_collapse", collapsePos);
            GameEvents.RaiseSoundCaption("[CEILING COLLAPSES!]", 2.5f);
            SetNamedObjectActive("staff_exit_collapse", true);
            gs.SetFlag("collapse");
            GameEvents.RaiseCameraTrauma(0.82f, 1.15f);
            GameEvents.RaiseDamageFlash(0.28f);
            if (flashlight != null) flashlight.TriggerFlicker(1.1f, 0.8f);

            yield return new WaitForSeconds(2.5f);
            EnqueueVoice("vo_dana_07", "RADIO - DANA (OPS)", "Vance? ...the old preserve had another way out... evac binder... records annex, east end...", 5.0f, radio: true);
            yield return new WaitForSeconds(5.4f);

            GameEvents.RaisePlaySound("radio_dies");
            GameEvents.RaiseSubtitle("", "The radio dies.", 2.2f);
            gs.SetObjective("Search the Records Annex (east end of Staff Corridor) for the 1996 evacuation binder.");
            SpawnWingRaptor();
        }

        // =========================================================================
        // CHAPTER 2 — RECORDS ANNEX & FEED PREP WING STEALTH
        // =========================================================================

        private void SetupChapter2Records()
        {
            GameState gs = GameState.EnsureInstance();
            if (player != null) player.Teleport(world.GetPoint("spawn_records"), yawDegrees: 140f);
            GameEvents.RaiseAmbienceZone("vc_dark", indoors: true, fadeSeconds: 1.0f);
            GameEvents.RaiseMusicState("tension", 1.5f);
            GameEvents.RaiseScreenFade(0f, 1.5f);
            GameEvents.RaiseTitleCard(GameState.GetChapterTitle(CheckpointId.Records), "", 2.5f);

            if (gs.GetFlag("manual_read"))
            {
                OnManualUnlockedRoute(freshPickup: false);
            }
            else
            {
                gs.SetObjective("Search the Records Annex for the 1996 Evacuation Manual.");
            }
            SpawnWingRaptor();
        }

        private void OnManualReadingCompleted(DocumentPickup manual)
        {
            if (GameState.EnsureInstance().GetFlag("manual_read")) return;
            OnManualUnlockedRoute(freshPickup: true);
        }

        private void OnManualUnlockedRoute(bool freshPickup)
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetFlag("manual_read");
            gs.SetHasMasterKey(true);
            world.GetDoor("door_wing")?.SetLocked(false);

            if (freshPickup)
            {
                GameEvents.RaisePlaySound("foley_pickup");
                GameEvents.RaiseToastMessage("Master key acquired - unlocks Feed Prep. Route map added to Journal {journal}.", 4.5f);
                gs.SaveCheckpoint(CheckpointId.Records);
                EnqueueVoice("vo_vance_02", "", "Through the feed prep wing. Something's moving in there.", 3.5f);
                // review 4: the first lethal encounter came before the game taught stealth
                StartCoroutine(StealthHintAfter(5.0f));
            }

            gs.SetObjective("Restore auxiliary power at Substation 3 (Paddock 4), then reach Exit E-0. Route: Feed Prep -> Loading Dock -> Service Path.");
        }

        private IEnumerator StealthHintAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            GameEvents.RaiseToastMessage("A raptor hunts the Feed Prep wing. It sees light and movement: crouch {crouch}, light off {flashlight}, keep to the dark.", 6.0f);
        }

        private void SpawnWingRaptor()
        {
            if (world.DinoSpawner == null || _activeRaptors.Count > 0) return;
            Vector3 spawn = world.GetPoint("wing_cold");
            IDinoController r = world.DinoSpawner.Spawn("raptor", spawn, 0f, "wing_raptor");
            if (r == null) return;

            // 0.85 until the round 5 playthrough: the jammed dock door must be crawled at 1.35 m/s and the wing raptor
            // noticed a crouching player there too fast to be fair (6 of 8 bot deaths). Sight gain scales with this.
            r.Aggression = 0.5f;
            r.SetPatrolWaypoints(new[]
            {
                world.GetPoint("wing_hall") + new Vector3(-5f, 0f, -2f),
                world.GetPoint("wing_hall") + new Vector3(5f, 0f, 4f),
                world.GetPoint("wing_cold"),
                world.GetPoint("wing_vet")
                // wing_south (0, -11) is dropped: it parked the raptor 8 m from the jammed dock roller door, the one
                // place the player must crawl at 1.35 m/s. The playthrough bot died there in 6 of 8 attempts; the
                // raptor still sweeps the hall, the cold store and the vet room the route runs through.
            });
            _activeRaptors.Add(r);
        }

        // =========================================================================
        // CHAPTER 3 — LOADING DOCK & ENCLOSED SERVICE PATHS
        // =========================================================================

        private void SetupChapter3DockAndPaths()
        {
            ClearActiveRaptors();
            if (player != null) player.Teleport(world.GetPoint("dock"), yawDegrees: 90f);
            GameState.EnsureInstance().SetObjective("Follow the concrete service path east to the Paddock 4 service gate.");
            GameEvents.RaiseAmbienceZone("paths", indoors: false, fadeSeconds: 1.5f);
            GameEvents.RaiseMusicState("tension", 1.5f);
            GameEvents.RaiseScreenFade(0f, 1.5f);
            GameEvents.RaiseTitleCard(GameState.GetChapterTitle(CheckpointId.Dock), "", 2.5f);
            if (pathLampSparksVfx != null) pathLampSparksVfx.Play();
        }

        private void OnReachedLoadingDock()
        {
            ClearActiveRaptors();
            GameState gs = GameState.EnsureInstance();
            gs.SaveCheckpoint(CheckpointId.Dock);
            gs.SetObjective("Follow the concrete service path east to the Paddock 4 service gate.");
            GameEvents.RaiseAmbienceZone("paths", indoors: false, fadeSeconds: 1.5f);
            if (pathLampSparksVfx != null) pathLampSparksVfx.Play();
        }

        private void OnServiceGateBoxUsed(GameObject interactor)
        {
            GameEvents.RaisePlaySound("ui_denied");
            GameEvents.RaiseToastMessage("Dead. No power to the gate - crouch {crouch} under the bent north leaf.", 3.0f);
        }

        private void TriggerServicePathRaptorCrossing()
        {
            if (world.DinoSpawner == null) return;
            Vector3 startA = world.GetPoint("raptor_cross_a");
            Vector3 endB = world.GetPoint("raptor_cross_b");
            IDinoController r = world.DinoSpawner.Spawn("raptor", startA, 180f, "paths_cross_raptor");
            if (!IsAlive(r)) return;

            r.AiEnabled = false;
            atmosphere.TriggerLightningFlash(0.75f, withThunder: true);
            GameEvents.RaisePlaySound("raptor_screech", startA);
            // straight across the junction in front of the player: the NavMesh path went round it
            r.ScriptMoveDirect(endB, 7.0f, () => { if (IsAlive(r)) r.Despawn(); });
            StartCoroutine(AutoDespawnAfter(r, 6.0f));   // with acceleration the 36 m run takes about 5.4 s (round 5): it must not vanish in view
        }

        // =========================================================================
        // CHAPTER 4 - TYRANT VALLEY (PADDOCK 4)
        // =========================================================================

        private void SetupChapter4Paddock()
        {
            if (player != null) player.Teleport(world.GetPoint("paddock_entry"), yawDegrees: 85f);
            GameState.EnsureInstance().SetObjective("Cross Tyrant Valley to Substation 3. Stay low in tall grass - keep your light off near it.");
            GameEvents.RaiseAmbienceZone("paddock", indoors: false, fadeSeconds: 1.5f);
            GameEvents.RaiseMusicState("night_unease", 2.0f);
            GameEvents.RaiseScreenFade(0f, 1.5f);
            GameEvents.RaiseTitleCard(GameState.GetChapterTitle(CheckpointId.Paddock), "", 2.5f);
            SpawnValleyTrex(nearSubstation: false);
        }

        private void OnEnteredPaddock()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SaveCheckpoint(CheckpointId.Paddock);
            gs.SetObjective("Cross Tyrant Valley to Substation 3. Stay low in tall grass - keep your light off near it.");
            GameEvents.RaiseAmbienceZone("paddock", indoors: false, fadeSeconds: 1.5f);
            GameEvents.RaiseMusicState("night_unease", 2.0f);
            SpawnValleyTrex(nearSubstation: false);
        }

        private void SpawnValleyTrex(bool nearSubstation)
        {
            if (IsAlive(_trex) || world.DinoSpawner == null) return;
            Vector3 startPos = nearSubstation
                ? new Vector3(300f, world.SampleGroundHeight(300f, 55f), 55f)
                : world.GetPoint("trex_drink");

            _trex = world.DinoSpawner.Spawn("trex", startPos, 160f, "valley_trex");
            if (!IsAlive(_trex)) return;

            _trex.SetPatrolWaypoints(BuildValleyTrexPatrol(), nearSubstation ? 4 : 1);
            _trex.Aggression = 0.9f;
        }

        private Vector3[] BuildValleyTrexPatrol()
        {
            var patrol = new Vector3[TrexPatrolPointsXZ.Length];
            for (int i = 0; i < TrexPatrolPointsXZ.Length; i++)
            {
                Vector3 p = TrexPatrolPointsXZ[i];
                p.y = world.SampleGroundHeight(p.x, p.z, 0f);
                patrol[i] = p;
            }
            return patrol;
        }

        // The reveal crossing (review 3: "the reveal shows no T. rex": the animal was 75-93 m away behind a tree). It
        // walks out of the road ~46 m ahead of the trigger, across the open gap straight down Service Road 3 (clear sky
        // between the two central groves), stops on the far verge and looks back at the player, then goes on north to
        // patrol point 0 (176, 58) and its usual loop. Bearings from the trigger (128, 0): 85 -> 69 degrees.
        private static readonly Vector3 TrexRevealFromXZ = new Vector3(174f, 0f, 4f);
        private static readonly Vector3 TrexRevealPauseXZ = new Vector3(170f, 0f, 16f);
        private static readonly Vector3 TrexRevealExitXZ = new Vector3(166f, 0f, 40f);
        private const float TrexRevealWalkSpeed = 2.6f;   // TrexAI walk speed (the stride clip matches it)
        private const float TrexRevealHoldAt = 4.6f;      // s after the trigger: the stop and the look back
        private const float TrexRevealHoldFor = 2.8f;

        private Vector3 GroundAt(Vector3 xz) => new Vector3(xz.x, world.SampleGroundHeight(xz.x, xz.z, 0f), xz.z);

        private bool TrexInPlayersView(float maxDistance)
        {
            Camera cam = Camera.main;
            if (cam == null || !IsAlive(_trex)) return false;
            Vector3 p = _trex.Transform.position + Vector3.up * 3f;
            if (Vector3.Distance(cam.transform.position, p) > maxDistance) return false;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }

        private IEnumerator TrexStreamRevealRoutine()
        {
            if (!IsAlive(_trex)) SpawnValleyTrex(nearSubstation: false);

            // Stage the animal in the open gap down the road, unless the player is already looking at it nearby.
            bool staged = false;
            if (IsAlive(_trex) && player != null && !TrexInPlayersView(90f))
            {
                Vector3 from = GroundAt(TrexRevealFromXZ), pause = GroundAt(TrexRevealPauseXZ);
                _trex.Teleport(from, Mathf.Atan2(pause.x - from.x, pause.z - from.z) * Mathf.Rad2Deg);
                _trex.ScriptMoveTo(pause, TrexRevealWalkSpeed);
                staged = true;
            }

            // The strikes come from behind the animal: it stands as a black shape against lit rain haze.
            Vector3 backlight = Vector3.zero;
            if (IsAlive(_trex) && player != null)
            {
                Vector3 toPlayer = player.transform.position - _trex.Transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 1f) backlight = (toPlayer.normalized + Vector3.down * 0.42f).normalized;
            }

            float t0 = Time.time;
            atmosphere.TriggerLightningFlashFrom(backlight, 1.0f, withThunder: false);
            if (IsAlive(_trex)) GameEvents.RaisePlaySound("trex_roar_far", _trex.Transform.position);
            GameEvents.RaiseSoundCaption("[A MASSIVE ROAR ECHOES ACROSS THE VALLEY]", 3.0f);
            yield return new WaitForSeconds(0.45f);
            atmosphere.TriggerLightningFlashFrom(backlight, 0.8f, withThunder: true);
            EnqueueVoice("vo_vance_03", "", "Something massive is moving by the stream.", 3.0f);
            GameEvents.RaiseMusicState("tension", 2.0f);
            yield return new WaitForSeconds(1.6f);
            atmosphere.TriggerLightningFlashFrom(backlight, 0.9f, withThunder: true);   // a second look

            if (!staged) yield break;

            // It stops on the far verge and turns its head to the player; a flickering multi-stroke strike holds it
            // in the light for most of a second (eyes in the torch beam).
            while (Time.time - t0 < TrexRevealHoldAt) yield return null;
            if (!IsAlive(_trex) || _trex.CurrentState != DinoState.Scripted) yield break;
            if (player != null) _trex.ScriptHoldAndLookAt(player.EyePosition);
            yield return new WaitForSeconds(0.3f);
            atmosphere.TriggerLightningFlashFrom(backlight, 1.0f, withThunder: false);
            yield return new WaitForSeconds(0.26f);
            atmosphere.TriggerLightningFlashFrom(backlight, 0.7f, withThunder: false);
            yield return new WaitForSeconds(0.3f);
            atmosphere.TriggerLightningFlashFrom(backlight, 0.9f, withThunder: true);
            yield return new WaitForSeconds(Mathf.Max(0.1f, TrexRevealHoldFor - 0.86f));

            // ...and walks on into the grass toward its patrol, then the stealth loop takes over.
            if (!IsAlive(_trex) || _trex.CurrentState != DinoState.Scripted) yield break;
            bool arrived = false;
            _trex.ScriptMoveTo(GroundAt(TrexRevealExitXZ), TrexRevealWalkSpeed, () => arrived = true);
            float limit = Time.time + 16f;
            while (!arrived && Time.time < limit && IsAlive(_trex) && _trex.CurrentState == DinoState.Scripted) yield return null;
            if (!IsAlive(_trex) || _trex.CurrentState != DinoState.Scripted) yield break;
            _trex.SetPatrolWaypoints(BuildValleyTrexPatrol(), 0);
            _trex.ReleaseToAI(DinoState.Patrol);
        }

        private void OnJeepRadioActivated(DocumentPickup doc)
        {
            StartCoroutine(RangerAudioLogRoutine());
        }

        private IEnumerator RangerAudioLogRoutine()
        {
            yield return new WaitForSeconds(0.55f);
            EnqueueVoice("vo_ranger_01", "RADIO - RANGER (RECORDED)", "...Unit Four, it's at the fence line... no - the fence is dead, it's DEAD-", 4.5f);
            yield return new WaitForSeconds(0.7f);
            EnqueueVoice("vo_ranger_02", "RADIO - RANGER (RECORDED)", "...if anyone hears this, stay in the grass. It tracks the lights...", 4.5f);
        }

        // =========================================================================
        // CHAPTER 5 — SUBSTATION 3 & RUBBER-BANDED T. REX CHASE
        // =========================================================================

        private void SetupChapter5Substation()
        {
            GameState gs = GameState.EnsureInstance();
            GameEvents.RaiseAmbienceZone("paddock", indoors: false, fadeSeconds: 1.0f);
            GameEvents.RaiseScreenFade(0f, 1.5f);
            GameEvents.RaiseTitleCard(GameState.GetChapterTitle(CheckpointId.Substation), "", 2.5f);

            SyncAllSubstationStations();

            if (gs.GetFlag("aux_power"))
            {
                // Power already restored: restart directly at the chase exit
                Vector3 chaseStart = world.GetPoint("substation_center") + new Vector3(17f, 0.05f, -4f);
                if (player != null) player.Teleport(chaseStart, yawDegrees: 90f);
                SpawnValleyTrex(nearSubstation: true);
                if (IsAlive(_trex))
                {
                    Vector3 smashPos = new Vector3(258f, world.SampleGroundHeight(258f, 22f), 22f);
                    _trex.Teleport(smashPos, 115f);
                }
                GameObject genObj = ResolveNamedObject("generator");
                GameEvents.RaisePlayLoop("gen_engine_loop", genObj != null ? genObj.transform : null, 0.5f);
                gs.SetObjective("RUN! Follow the green route lights east to the Old Yard!");
                StartCoroutine(DelayedStartChaseRoutine(1.4f));
            }
            else
            {
                if (player != null) player.Teleport(world.GetPoint("sub_checkpoint"), yawDegrees: 90f);
                GameEvents.RaiseMusicState("tension", 1.5f);
                SpawnValleyTrex(nearSubstation: true);
                EnterSubstationCompound();
            }
        }

        private void EnterSubstationCompound()
        {
            GameState gs = GameState.EnsureInstance();
            if (gs.CurrentCheckpoint != CheckpointId.Substation)
            {
                gs.SaveCheckpoint(CheckpointId.Substation);
            }

            if (IsAlive(_trex))
            {
                _trex.LeashCenter = world.GetPoint("substation_center");
            }

            SyncAllSubstationStations();
            UpdateSubstationObjectiveText();
            gs.BroadcastSubstationSnapshot();
        }

        private void SyncAllSubstationStations()
        {
            SubstationStation[] stations = FindObjectsByType<SubstationStation>();
            for (int i = 0; i < stations.Length; i++) stations[i].SyncFromGameState();
        }

        private void OnSubstationGeneratorAttempted(SubstationStation station, int attempt)
        {
            if (attempt == 2 && IsAlive(_trex))
            {
                _trex.Awareness = 0.9f;
                _trex.LastNoisePosition = world.GetPoint("generator");
                _trex.ForceState(DinoState.Investigate);
                StartCoroutine(DelayedTrexInvestigateRoar());
            }
            else if (attempt >= 3)
            {
                GameObject genObj = ResolveNamedObject("generator");
                GameEvents.RaisePlayLoop("gen_engine_loop", genObj != null ? genObj.transform : null, 3.5f);
                if (IsAlive(_trex))
                {
                    _trex.HuntPlayer();
                    _trex.ChaseRubberBand = 0.75f;
                }
            }
        }

        private IEnumerator DelayedTrexInvestigateRoar()
        {
            yield return new WaitForSeconds(2.3f);
            if (IsAlive(_trex) && _trex.CurrentState != DinoState.Hunt)
            {
                GameEvents.RaisePlaySound("trex_roar_mid", _trex.Transform.position);
                GameEvents.RaiseSoundCaption("[T. REX ROARS OUTSIDE THE FENCE]", 2.6f);
            }
        }

        private void OnSubstationStationCompleted(SubstationStation station)
        {
            UpdateSubstationObjectiveText();
            if (station.StationType == SubstationStationType.TransferLever)
            {
                StartCoroutine(RestoreAuxiliaryPowerAndChaseRoutine());
            }
        }

        private void UpdateSubstationObjectiveText()
        {
            GameState gs = GameState.EnsureInstance();
            if (gs.TransferComplete)
            {
                gs.SetObjective("RUN! Follow the green route lights east to the Old Maintenance Yard!");
            }
            else if (gs.GeneratorRunning)
            {
                gs.SetObjective("Throw the TRANSFER lever to Circuit B (hold)!");
            }
            else if (gs.FuelPrimed)
            {
                gs.SetObjective("Start the diesel generator.");
            }
            else if (gs.AllBreakersReset)
            {
                gs.SetObjective("Prime the diesel generator fuel pump (hold).");
            }
            else
            {
                gs.SetObjective($"Restore Auxiliary Circuit B - reset all three tripped breakers ({gs.BreakersResetCount}/3).");
            }
        }

        private IEnumerator RestoreAuxiliaryPowerAndChaseRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetFlag("aux_power");

            // Ensure T. rex attacks from the west/northwest fence rather than blocking the east escape gate
            if (IsAlive(_trex))
            {
                Vector3 smashOrigin = new Vector3(258f, world.SampleGroundHeight(258f, 22f), 22f);
                _trex.Teleport(smashOrigin, 125f);
            }

            yield return new WaitForSeconds(0.4f);
            lightGroups.SetGroup(LightGroupId.AuxSub, true, 0.35f, electricalFlicker: true);
            GameEvents.RaisePlaySound("floodlights_on", world.GetPoint("substation_center"));

            // Ripple green escape lights strictly EAST of Substation 3 (X >= 286 -> Old Yard X = 401)
            yield return lightGroups.RippleGroupSequential(LightGroupId.AuxRoute, 0.12f, surgeEffect: true);
            lightGroups.SetGroup(LightGroupId.AuxYard, true, 0.5f);

            gs.SaveCheckpoint(CheckpointId.Substation);
            StartTrexChase();
        }

        private IEnumerator DelayedStartChaseRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            StartTrexChase();
        }

        private void StartTrexChase()
        {
            _chaseActive = true;
            if (player != null)
            {
                player.ResetStaminaFull();
                player.StaminaDrainMultiplier = 0.15f; // Tense, thrilling, and consistently winnable sprint!
            }

            GameEvents.RaiseMusicState("chase", 0.3f);
            GameState.EnsureInstance().SetObjective("RUN! Follow the green route lights east to the Old Maintenance Yard!");

            // Destroy Substation DinoBlocker as T. rex smashes through the fence!
            SetNamedObjectActive("sub_blocker", false);

            Vector3 smashPos = world.GetPoint("substation_center") + new Vector3(-18f, 0f, 0f);
            GameEvents.RaisePlaySound("fence_smash", smashPos);
            GameEvents.RaiseSoundCaption("[THE T. REX SMASHES THROUGH THE FENCE!]", 3.0f);
            GameEvents.RaiseCameraTrauma(0.65f, 1.2f);

            if (IsAlive(_trex))
            {
                _trex.PlayActionClip("Fence_Bash", 1.4f);
                _trex.HuntPlayer();
                GameEvents.RaisePlaySound("trex_roar_close", _trex.Transform.position);
                _trex.Aggression = 3.0f;
            }
        }

        private void UpdateChaseRubberBand()
        {
            if (!_chaseActive || !IsAlive(_trex) || player == null || player.IsDead) return;

            float dist = Vector3.Distance(_trex.Transform.position, player.transform.position);
            if (player.HorizontalSpeed > 3.0f)
            {
                float t = Mathf.InverseLerp(13.0f, 30.0f, dist);
                _trex.ChaseRubberBand = Mathf.Lerp(0.72f, 1.30f, t);
            }
            else
            {
                _trex.ChaseRubberBand = 1.12f;
            }

            _trex.LastSeenPosition = player.transform.position;
            _trex.UnseenTimer = 0f;
            if (_trex.CurrentState != DinoState.Hunt && _trex.CurrentState != DinoState.Attack)
            {
                _trex.HuntPlayer();
            }
        }

        // =========================================================================
        // CHAPTER 6 - OLD MAINTENANCE YARD, E-0 GATE & DAWN COAST ENDING
        // =========================================================================

        private void OnReachedOldYard()
        {
            _chaseActive = false;
            if (player != null) player.StaminaDrainMultiplier = 1.0f;

            Vector3 gapPos = world.GetPoint("yard_gap");
            GameEvents.RaisePlaySound("wall_impact_heavy", gapPos);
            GameEvents.RaiseCameraTrauma(0.55f, 1.3f);

            if (IsAlive(_trex))
            {
                Vector3 outsideStop = world.GetPoint("yard_gate_outside") + new Vector3(-6f, 0f, 0f);
                _trex.AiEnabled = false;
                _trex.ScriptMoveTo(outsideStop, 4.2f, () => { if (IsAlive(_trex)) StartCoroutine(TrexRoarAtYardWallAndRetreat()); });
            }

            GameState.EnsureInstance().SaveCheckpoint(CheckpointId.Yard);
            GameEvents.RaiseAmbienceZone("yard", indoors: false, fadeSeconds: 1.5f);
            GameEvents.RaiseMusicState("tension", 3.0f);
            SpawnYardRaptorsAndSetupE0();
        }

        private IEnumerator TrexRoarAtYardWallAndRetreat()
        {
            if (!IsAlive(_trex)) yield break;
            if (player != null) _trex.ScriptHoldAndLookAt(player.EyePosition);
            _trex.PlayActionClip("Roar_Massive", 3.2f);
            GameEvents.RaisePlaySound("trex_roar_close", _trex.Transform.position);
            GameEvents.RaiseCameraTrauma(0.70f, 1.1f);
            yield return new WaitForSeconds(4.0f);
            if (!IsAlive(_trex)) yield break;
            Vector3 retreat = new Vector3(330f, world.SampleGroundHeight(330f, 40f), 40f);
            _trex.ScriptMoveTo(retreat, 2.6f, () => { if (IsAlive(_trex)) _trex.ReleaseToAI(DinoState.Patrol); });
        }

        private void SetupChapter6Yard()
        {
            if (player != null) player.Teleport(world.GetPoint("yard_gap") + new Vector3(3.0f, 0.05f, 0f), yawDegrees: 90f);
            GameEvents.RaiseAmbienceZone("yard", indoors: false, fadeSeconds: 1.0f);
            GameEvents.RaiseMusicState("tension", 1.5f);
            GameEvents.RaiseScreenFade(0f, 1.5f);
            GameEvents.RaiseTitleCard(GameState.GetChapterTitle(CheckpointId.Yard), "", 2.5f);
            SpawnYardRaptorsAndSetupE0();
        }

        private void SpawnYardRaptorsAndSetupE0()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetObjective("Find Service Exit E-0 on the east side of the Old Maintenance Yard.");

            if (world.GetInteractable("gate_box") is SimpleInteractable e0Box)
            {
                e0Box.SetEnabledState(true);
                e0Box.SetLockedState(!gs.GetFlag("aux_power"), "E-0 gate control is dead - restore Auxiliary Circuit B at Substation 3 first.");
                e0Box.SetSavedHoldProgress(gs.E0GateProgress);
            }

            if (world.DinoSpawner == null || _activeRaptors.Count >= 2) return;

            IDinoController rA = world.DinoSpawner.Spawn("raptor", world.GetPoint("yard_raptor_a"), 90f, "yard_raptor_a");
            if (rA != null)
            {
                rA.Aggression = 0.8f;
                rA.SetPatrolWaypoints(new[]
                {
                    new Vector3(445f, 1f, 12f),
                    new Vector3(472f, 1f, 20f),
                    new Vector3(480f, 1f, -2f),
                    new Vector3(452f, 1f, -4f)
                });
                _activeRaptors.Add(rA);
            }

            IDinoController rB = world.DinoSpawner.Spawn("raptor", world.GetPoint("yard_raptor_b"), 180f, "yard_raptor_b");
            if (rB != null)
            {
                rB.Aggression = 0.8f;
                rB.SetPatrolWaypoints(new[]
                {
                    new Vector3(470f, 1f, -30f),
                    new Vector3(486f, 1f, -52f),
                    new Vector3(472f, 1f, -78f),
                    new Vector3(452f, 1f, -40f)
                });
                _activeRaptors.Add(rB);
            }
        }

        private void OnE0GateHoldProgress(GameObject interactor, float progress01)
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetE0GateProgress(progress01);

            // Strain E-0 leaves slightly open (up to 18%) while motor grinds loud enough to draw raptors
            world.GetDoor("e0_gate_leaves")?.SetNormalizedOpen(Mathf.Min(progress01, 0.18f));

            if (progress01 - _e0LastGrindAudioProgress >= 0.08f)
            {
                _e0LastGrindAudioProgress = progress01;
                Vector3 e0Pos = world.GetPoint("e0_gate");
                GameEvents.RaisePlaySound("gate_motor_grind", e0Pos);
                GameEvents.RaiseNoiseEmitted(e0Pos, 25f, "machine");
            }

            if (progress01 > 0.05f && !gs.GetFlag("gate_started"))
            {
                gs.SetFlag("gate_started");
                gs.SetObjective("Keep holding OPEN on E-0 - progress is saved if you hide. They can hear the motor!");
            }
        }

        private void OnE0GateHoldCompleted(GameObject interactor)
        {
            GameState gs = GameState.EnsureInstance();
            if (gs.GetFlag("e0_open")) return;
            gs.SetFlag("e0_open");
            gs.SetE0GateProgress(1f);

            // Swing BOTH E-0 leaves OUTWARD EAST (+X) onto Coast Road (POS-16 fix: clear sprint path!)
            Vector3 e0Pos = world.GetPoint("e0_gate");
            if (world.GetDoor("e0_gate_leaves") is DoorController e0Door)
            {
                e0Door.AnimateOpenTo(1f, 1.0f, () => GameEvents.RaisePlaySound("gate_open_clunk", e0Pos));
            }
            else
            {
                world.GetDoor("e0_gate_leaves")?.Open(null, instant: false);
                GameEvents.RaisePlaySound("gate_open_clunk", e0Pos);
            }

            gs.SetObjective("GO! Sprint out through Service Exit E-0!");
        }

        private IEnumerator ThroughE0GateRoutine()
        {
            _endingStarted = true;
            Vector3 e0Pos = world.GetPoint("e0_gate");
            GameState.EnsureInstance().SetObjective(string.Empty);

            // Slam E-0 shut behind the player
            if (world.GetDoor("e0_gate_leaves") is DoorController e0Door)
            {
                e0Door.AnimateOpenTo(0f, 1.35f, () => GameEvents.RaisePlaySound("gate_close_slam", e0Pos));
            }
            else
            {
                world.GetDoor("e0_gate_leaves")?.Close(instant: false);
            }

            yield return new WaitForSeconds(1.3f);

            // Any chasing yard raptors slam furiously into the closed chainlink gate
            for (int i = 0; i < _activeRaptors.Count; i++)
            {
                IDinoController r = _activeRaptors[i];
                if (!IsAlive(r)) continue;
                r.AiEnabled = false;
                Vector3 hitPoint = e0Pos + (r.Transform.position.x > 505f ? new Vector3(2.4f, 0f, 0f) : new Vector3(-2.4f, 0f, 0f));
                r.ScriptMoveTo(hitPoint, 7.0f, () =>
                {
                    if (!IsAlive(r)) return;
                    GameEvents.RaisePlaySound("chainlink_impact", e0Pos);
                    r.PlayActionClip("Bite_Lunge", 1.0f);
                });
            }

            GameEvents.RaiseMusicState("ending", 4.0f);

            // The playthrough bot stood behind the slammed gate with no objective: the ending only starts 20 m further
            // down the coast road, and nothing said so.
            yield return new WaitForSeconds(1.2f);
            if (!_firedBeats.Contains("coast_ending"))
                GameState.EnsureInstance().SetObjective("Follow the coast road east, away from the park.");
        }

        private IEnumerator DawnCoastEndingRoutine()
        {
            GameState gs = GameState.EnsureInstance();
            gs.SetObjective(string.Empty);
            gs.SetCutscene(true);
            if (player != null) player.SetControlEnabled(false);

            // IRONCLAD FL-3 FIX: Hide flashlight hand & beam completely during Dawn Coast sunrise ending!
            if (flashlight != null)
            {
                flashlight.SetBeamEnabled(false, silent: true);
                flashlight.SetCutsceneSuppressed(true);
            }

            GameEvents.RaiseMusicState("ending", 3.0f);
            GameEvents.RaiseAmbienceZone("dawn", indoors: false, fadeSeconds: 5.5f);
            atmosphere.LightningEnabled = false;

            // Hours later: the night goes to black and the morning fades in already settled. (A storm night blended
            // into a sunrise in 8.5 s played the first shot with the night's exposure against the rising sun: a
            // clipped cream sky and a lemon sun, review 2.)
            GameEvents.RaiseScreenFade(1.0f, 1.1f);
            yield return new WaitForSeconds(1.25f);
            atmosphere.SetPreset(AtmospherePreset.DawnCoast, 0f);
            GameEvents.RaiseLetterbox(true, 0.1f);

            // Sunrise: no night lamp, beacon or emergency light is left burning
            LightGroupId[] nightGroups =
            {
                LightGroupId.VCEmergency, LightGroupId.Plaza, LightGroupId.Paths, LightGroupId.DockMain, LightGroupId.Talon,
                LightGroupId.PaddockBattery, LightGroupId.SubBattery, LightGroupId.YardBattery,
                LightGroupId.AuxSub, LightGroupId.AuxRoute, LightGroupId.AuxYard
            };
            for (int i = 0; i < nightGroups.Length; i++) lightGroups.SetGroup(nightGroups[i], false, 0.1f);

            Vector3 overlook = world.GetPoint("ending_view");
            // one beat in black: the look, the exposure and the reflection probes settle before the first frame shows
            yield return new WaitForSeconds(0.85f);
            GameEvents.RaiseScreenFade(0.0f, 2.4f);
            GameEvents.RaiseTitleCard("", "Isla Tormenta  |  5:51 AM", 3.4f);

            if (cameraDirector != null)
            {
                yield return cameraDirector.PlayDawnCoastCraneShot(overlook, () =>
                {
                    GameEvents.RaisePlaySound("trex_roar_far", new Vector3(380f, 10f, -20f));
                    GameEvents.RaiseSoundCaption("[A DISTANT T. REX ROARS OVER THE MISTY ISLAND]", 3.2f);
                    GameEvents.RaiseCameraTrauma(0.20f, 1.2f);
                });
            }
            else
            {
                yield return new WaitForSeconds(10f);
            }

            GameEvents.RaiseScreenFade(1.0f, 3.0f);
            yield return new WaitForSeconds(3.2f);

            GameEvents.RaiseLetterbox(false, 0.1f);
            GameEvents.RaiseTitleCard("CRETACEA: BLACKOUT", "You made it out.", 3.5f);
            yield return new WaitForSeconds(4.8f);

            gs.SetCutscene(false);
            gs.RefreshCursor();
            GameEvents.RaiseGameFinished(gs.BuildCompletionStatsSummary());
        }

        // =========================================================================
        // STORY TRIGGER DISPATCH & HELPERS
        // =========================================================================

        private void OnStoryTriggerEntered(string triggerId)
        {
            if (string.IsNullOrWhiteSpace(triggerId)) return;
            GameState gs = GameState.EnsureInstance();

            switch (triggerId.ToLowerInvariant())
            {
                case "control_switchover":
                    if (gs.HasFlashlight && !gs.GetFlag("blackout"))
                        TryStartSwitchoverBlackout();
                    else if (!gs.GetFlag("blackout"))
                        RearmTrigger(triggerId);
                    break;

                case "lobby_beat":
                    // Chapter 1 beats only: after the collapse (a Records restart, Continue) walking through the Lobby
                    // replayed the shutter beat and sent the player back to the Staff Exit and the binder (bot run 4).
                    if (gs.GetFlag("blackout") && !gs.GetFlag("collapse") && _firedBeats.Add("lobby_beat"))
                        StartCoroutine(LobbyBeatRoutine());
                    else if (!gs.GetFlag("blackout"))
                        RearmTrigger(triggerId);
                    break;

                case "staff_exit_collapse":
                    if (gs.GetFlag("blackout") && gs.GetFlag("lobby_done") && !gs.GetFlag("collapse") && _firedBeats.Add("staff_exit_collapse"))
                        StartCoroutine(StaffExitCollapseRoutine());
                    else if (!_firedBeats.Contains("staff_exit_collapse") && !gs.GetFlag("collapse"))
                        RearmTrigger(triggerId);
                    break;

                case "dock_crouch_hint":
                    if (player != null && !player.IsCrouching)
                        GameEvents.RaiseToastMessage("The roller door is jammed half-open. Crouch {crouch} to crawl underneath.", 4.0f);
                    break;

                case "reach_dock":
                    if (_firedBeats.Add("reach_dock"))
                        OnReachedLoadingDock();
                    break;

                case "paths_corner2":
                    if (_firedBeats.Add("paths_corner2"))
                        TriggerServicePathRaptorCrossing();
                    break;

                case "paths_mid":
                    if (_firedBeats.Add("paths_mid"))
                        StartCoroutine(ServicePathBushScareRoutine());
                    break;

                case "paths_end":
                    if (_firedBeats.Add("paths_end"))
                        gs.SetObjective("The Paddock 4 service gate is jammed. Crouch {crouch} under the bent north leaf.");
                    break;

                case "enter_paddock":
                    if (_firedBeats.Add("enter_paddock"))
                        OnEnteredPaddock();
                    break;

                case "trex_reveal":
                    if (_firedBeats.Add("trex_reveal"))
                        StartCoroutine(TrexStreamRevealRoutine());
                    break;

                case "enter_substation":
                    if (_firedBeats.Add("enter_substation"))
                        EnterSubstationCompound();
                    break;

                case "yard_early_warn":
                    if (!gs.GetFlag("aux_power") && _firedBeats.Add("yard_early_warn"))
                        EnqueueVoice("vo_vance_04", "", "E-0 is dead without auxiliary power. Substation 3 first.", 3.5f);
                    if (!gs.GetFlag("aux_power")) RearmTrigger(triggerId);
                    break;

                case "reach_yard":
                    // Only with auxiliary power: arriving early used to save the Yard checkpoint, so a retry skipped
                    // Substation 3 and left E-0 dead. The volume re-arms, so the real arrival still counts.
                    if (gs.GetFlag("aux_power") && _firedBeats.Add("reach_yard"))
                        OnReachedOldYard();
                    else if (!gs.GetFlag("aux_power"))
                        RearmTrigger(triggerId);
                    break;

                case "yard_site_map":
                    if (_firedBeats.Add("yard_site_map"))
                        EnqueueVoice("vo_vance_05", "", "The old site plan. E-0 is circled in red - through the workshop, east side.", 4.0f);
                    break;

                case "through_e0_gate":
                    if (!_endingStarted)
                        StartCoroutine(ThroughE0GateRoutine());
                    break;

                case "coast_ending":
                    if (_firedBeats.Add("coast_ending"))
                        StartCoroutine(DawnCoastEndingRoutine());
                    break;
            }
        }

        /// <summary>
        /// A one-shot story volume whose beat was refused (its precondition is not met yet) fires again on the next
        /// entry. Before, it stayed spent: walking into the Lobby before the blackout, or reaching the Yard before
        /// Substation 3, left the beat unreachable later (a soft-lock for a player who explores).
        /// </summary>
        private void RearmTrigger(string triggerId)
        {
            foreach (StoryTriggerVolume v in FindObjectsByType<StoryTriggerVolume>())
                if (v.TriggerId == triggerId) v.ResetTrigger();
        }

        private IEnumerator ServicePathBushScareRoutine()
        {
            Vector3 mid = world.GetPoint("paths_mid");
            GameEvents.RaisePlaySound("bush_rustle_close", mid + new Vector3(4f, 4f, 3f));
            GameEvents.RaiseSoundCaption("[SOMETHING CRASHES THROUGH THE JUNGLE ABOVE]", 2.4f);
            yield return new WaitForSeconds(1.2f);
            GameEvents.RaisePlaySound("raptor_call_distant", mid + new Vector3(30f, 5f, 30f));
        }

        private void OnInteriorChanged(bool isIndoors, string zoneId)
        {
            GameState gs = GameState.Instance;
            if (gs == null || gs.InCutscene || _endingStarted) return;

            if (gs.GetFlag("blackout"))
            {
                float px = player != null ? player.transform.position.x : 0f;
                if (isIndoors)
                {
                    GameEvents.RaiseAmbienceZone(px < 30f ? "vc_dark" : "yard", indoors: true, fadeSeconds: 1.0f);
                }
                else
                {
                    string extZone = px < 92f ? "paths" : (px < 400f ? "paddock" : "yard");
                    GameEvents.RaiseAmbienceZone(extZone, indoors: false, fadeSeconds: 1.0f);
                }
            }
            else
            {
                GameEvents.RaiseAmbienceZone(isIndoors ? "vc_night" : "paths", indoors: isIndoors, fadeSeconds: 1.0f);
            }
        }

        private void OnDinoStateChanged(string dinoId, string species, DinoState state)
        {
            if (_chaseActive || _endingStarted) return;

            if (state == DinoState.Hunt || state == DinoState.Attack)
            {
                GameEvents.RaiseMusicState("chase", 0.6f);
                return;
            }

            // Return to tension music only once NO active dinosaur is hunting
            if (world.DinoSpawner != null)
            {
                IReadOnlyList<IDinoController> all = world.DinoSpawner.GetActiveDinos();
                for (int i = 0; i < all.Count; i++)
                {
                    if (IsAlive(all[i]) && (all[i].CurrentState == DinoState.Hunt || all[i].CurrentState == DinoState.Attack))
                        return;
                }
            }
            GameEvents.RaiseMusicState("tension", 3.0f);
        }

        private void EnqueueVoice(string voId, string speaker, string text, float duration, bool radio = false)
        {
            _voiceQueue.Enqueue(new VoiceRequest(voId, speaker, text, duration, radio));
            if (_voicePumpRoutine == null)
            {
                _voicePumpRoutine = StartCoroutine(VoiceQueuePumpRoutine());
            }
        }

        private IEnumerator VoiceQueuePumpRoutine()
        {
            while (_voiceQueue.Count > 0)
            {
                VoiceRequest req = _voiceQueue.Dequeue();
                if (GameState.Instance != null && GameState.Instance.IsGameOver) break;

                if (req.RadioSquelch)
                {
                    GameEvents.RaisePlaySound("radio_static_burst");
                    yield return new WaitForSeconds(0.22f);
                }

                GameEvents.RaiseVoice(req.VoId, req.Speaker, req.Text, req.Duration, req.RadioSquelch);
                GameEvents.RaiseSubtitle(req.Speaker, req.Text, req.Duration + 0.35f);
                yield return new WaitForSeconds(Mathf.Max(1.0f, req.Duration + 0.3f));
            }
            _voicePumpRoutine = null;
        }

        private void UpdateCutsceneSkipHold()
        {
            GameState gs = GameState.Instance;
            if (gs == null || !gs.InCutscene || _endingStarted || _skipRequested)
            {
                _skipHoldTimer = 0f;
                return;
            }

            CretaceaInput input = CretaceaInput.Instance;
            if (input != null && input.SkipCutsceneHeld)
            {
                _skipHoldTimer += Time.unscaledDeltaTime;
                if (_skipHoldTimer >= 1.15f)
                {
                    _skipRequested = true;
                    _skipHoldTimer = 0f;
                }
            }
            else
            {
                _skipHoldTimer = 0f;
            }
        }

        private void UpdateElectricalSparks()
        {
            _sparkTimer -= Time.deltaTime;
            if (_sparkTimer > 0f) return;
            _sparkTimer = UnityEngine.Random.Range(0.45f, 2.2f);

            if (shutterSparksVfx != null && shutterSparksVfx.isPlaying)
            {
                shutterSparksVfx.Emit(24);
                GameEvents.RaisePlaySound("electric_spark", shutterSparksVfx.transform.position, 0.7f);
            }
            if (pathLampSparksVfx != null && pathLampSparksVfx.isPlaying)
            {
                pathLampSparksVfx.Emit(24);
                GameEvents.RaisePlaySound("electric_spark", pathLampSparksVfx.transform.position, 0.7f);
            }
        }

        private GameObject ResolveNamedObject(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            GameObject obj = world != null ? world.GetObject(id) : null;
            if (obj != null)
            {
                _namedObjectCache[id] = obj;
                return obj;
            }
            if (_namedObjectCache.TryGetValue(id, out GameObject cached) && cached != null)
            {
                return cached;
            }
            return null;
        }

        private void SetNamedObjectActive(string id, bool active)
        {
            GameObject obj = ResolveNamedObject(id);
            if (obj != null) obj.SetActive(active);
        }

        private void ClearActiveRaptors()
        {
            for (int i = 0; i < _activeRaptors.Count; i++)
            {
                if (IsAlive(_activeRaptors[i])) _activeRaptors[i].Despawn();
            }
            _activeRaptors.Clear();
        }

        private IEnumerator AutoDespawnAfter(IDinoController dino, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (IsAlive(dino)) dino.Despawn();
        }
    }
}
