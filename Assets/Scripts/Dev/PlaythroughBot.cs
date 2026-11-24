using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using Cretacea.Core;
using Cretacea.Interact;
using Cretacea.Player;

namespace Cretacea.Dev
{
    /// <summary>
    /// Dev-only playthrough bot. Plays the game like a player: it reads the objective text, finds what the text names
    /// in the story's own data (interactables, trigger volumes, anchors), walks there on NavMesh paths by feeding the
    /// game's move and look input through <see cref="CretaceaInput.BotOverride"/>, aims at what it uses and presses or
    /// holds Interact, crouches in the tall grass, switches the flashlight, sprints in the chase and presses the
    /// death screen's RETRY button when it dies. No teleports and no direct trigger or interactable calls.
    ///
    /// Started from the shell (Play mode):  Cretacea.Dev.PlaythroughBot.Begin("&lt;log path&gt;", passiveDinos:false, fromMenu:true)
    /// Polled with:                         Cretacea.Dev.PlaythroughBot.Status()
    /// </summary>
    public sealed class PlaythroughBot : MonoBehaviour
    {
        public static PlaythroughBot Active { get; private set; }

        // ---------------------------------------------------------------- configuration
        private string _logPath;
        private bool _passiveDinos;
        private bool _fromMenu;

        // ---------------------------------------------------------------- run state
        private readonly CretaceaInput.BotInput _in = new CretaceaInput.BotInput();
        private readonly List<string> _events = new List<string>();
        private int _problemSerial;
        private string _lastProblem = "";
        private float _t0;
        private bool _done;
        private string _result = "running";
        private int _deaths;

        private string _objective = "";
        private float _objectiveSince;
        private CheckpointId _chapter;
        private float _chapterSince;
        private readonly HashSet<string> _seenObjectives = new HashSet<string>();

        // goal
        private enum GoalKind { Wait, Reach, Interact }
        private GoalKind _goalKind;
        private string _goalId = "";
        private Vector3 _goalPos;
        private IInteractable _goalTarget;
        private bool _wantSprint;
        private bool _crouchZone;

        // navigation
        private NavMeshPath _path;
        private Vector3[] _corners = new Vector3[0];
        private int _corner;
        private float _repathAt;
        private int _agentType = int.MinValue;
        private bool _pathPartialLogged;

        // stuck / progress
        private Vector3 _lastMovePos;
        private float _lastMoveCheck;
        private float _bestGoalDist = float.MaxValue;
        private float _lastProgressTime;
        private int _unstickStage;
        private float _strafeUntil;
        private float _strafeSign = 1f;
        private bool _stuckLogged;

        // interaction
        private float _aimSince = -1f;
        private float _interactCooldownUntil;
        private bool _holding;
        private bool _promptMissLogged;
        private float _stopDist = 1.6f;

        // modal handling
        private float _docOpenedAt = -1f;
        private float _deadSince = -1f;
        private float _menuClickAt = -1f;
        private float _crouchToggleCooldown;
        private float _flashCooldown;

        private float _doorCheckAt;
        private float _worldRebuildAt;
        private string _navScene = "";
        private float _lookGain = 0.1f;   // degrees per mouse unit (PlayerController.baseMouseSpeed x sensitivity)

        // ================================================================ shell entry points
        public static string Begin(string logPath, bool passiveDinos = false, bool fromMenu = false)
        {
            if (Active != null) { Destroy(Active.gameObject); Active = null; }
            var go = new GameObject("[PlaythroughBot]");
            DontDestroyOnLoad(go);
            var bot = go.AddComponent<PlaythroughBot>();
            bot._logPath = logPath;
            bot._passiveDinos = passiveDinos;
            bot._fromMenu = fromMenu;
            Active = bot;
            return "started";
        }

        public static string Stop()
        {
            if (Active == null) return "none";
            Active.Finish("stopped from shell");
            Destroy(Active.gameObject);
            Active = null;
            return "stopped";
        }

        /// <summary>One line: done|result|t|chapter|objective|goal|pos|deaths|problemSerial|lastProblem</summary>
        public static string Status()
        {
            PlaythroughBot b = Active;
            if (b == null) return "none";
            PlayerController p = PlayerController.Instance;
            string pos = p != null ? p.transform.position.ToString("F1") : "none";
            return $"done={(b._done ? 1 : 0)}|result={b._result}|t={Time.time - b._t0:F0}|cp={b._chapter}|obj={b._objective}|goal={b._goalKind}:{b._goalId}|pos={pos}|deaths={b._deaths}|problem={b._problemSerial}|last={b._lastProblem}";
        }

        // ================================================================ lifecycle
        private void Start()
        {
            _t0 = Time.time;
            CretaceaInput.EnsureInstance();
            CretaceaInput.BotOverride = _in;
            _path = new NavMeshPath();
            PlayerNavMesh.EnsureBuilt();
            try { if (!string.IsNullOrEmpty(_logPath)) File.WriteAllText(_logPath, ""); } catch (Exception) { }
            Log("BOT", $"start (fromMenu={_fromMenu}, passiveDinos={_passiveDinos})");
            if (_fromMenu) _menuClickAt = Time.time + 2f;
        }

        private void OnDestroy()
        {
            if (CretaceaInput.BotOverride == _in) CretaceaInput.BotOverride = null;
            if (Active == this) Active = null;
        }

        private void Finish(string result)
        {
            if (_done) return;
            _done = true;
            _result = result;
            ClearInput();
            Log("END", $"{result} after {Time.time - _t0:F0} s, deaths {_deaths}");
        }

        // ================================================================ main loop
        private void Update()
        {
            if (_done) { ClearInput(); return; }
            CretaceaInput.BotOverride = _in;
            ClearFrameInput();

            GameState gs = GameState.Instance;
            PlayerController player = PlayerController.Instance;

            // the pause menu stops game time: the menu flow runs on unscaled time
            if (_flowStep > 0) { if (_flowSince <= 0f) _flowSince = Time.unscaledTime; HandleFlow(gs); return; }
            if (_fromMenu && HandleMainMenu(gs)) return;
            if (gs == null || player == null) return;
            string sceneNow = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneNow != _navScene)
            {
                _navScene = sceneNow;
                PlayerNavMesh.ResetForScene();
                PlayerNavMesh.EnsureBuilt();
                _agentType = -1;
                Log("NAV", "scene " + sceneNow + ": rebuilding the player NavMesh from it");
            }
            if (!PlayerNavMesh.Poll()) { ClearInput(); return; }
            if (_agentType != PlayerNavMesh.AgentTypeId)
            {
                _agentType = PlayerNavMesh.AgentTypeId;
                Log("NAV", $"player NavMesh built in {PlayerNavMesh.BuildSeconds:F1} s from {PlayerNavMesh.Info}");
            }
            if (Time.time > _doorCheckAt)
            {
                _doorCheckAt = Time.time + 1f;
                if (PlayerNavMesh.RefreshDoors()) _repathAt = Time.time + 0.6f;
            }
            if (TryEndScreen()) return;
            if (HandleDeath(gs, player)) return;
            if (HandleDocument(gs)) return;
            if (gs.IsPaused) { _in.Press(CretaceaInput.BotButton.Pause); return; }

            TrackStory(gs);
            if (_passiveDinos) PacifyDinos();
            if (gs.InCutscene || gs.IsGameplayInputBlocked) { ResetProgress(player); return; }

            ResolveGoal(gs, player);
            Stance(gs, player);
            if (_goalKind == GoalKind.Wait) { ResetProgress(player); LookIdle(player); return; }

            if (HandleDoor(player)) return;
            Drive(gs, player);
        }

        // ================================================================ menus, death, documents, ending
        private bool HandleMainMenu(GameState gs)
        {
            if (gs != null && !gs.IsMainMenu) { _fromMenu = false; Log("MENU", "gameplay started from the main menu"); return false; }
            if (Time.time < _menuClickAt) return true;
            _menuClickAt = Time.time + 3f;
            if (ClickButton("BtnNewGame")) Log("MENU", "clicked NEW GAME");
            return true;
        }

        private bool TryEndScreen()
        {
            if (_flowStep > 0) return false;
            Button end = FindActiveButton("BtnEndMenu");
            if (end == null) return false;
            Log("END_SCREEN", $"end screen shown after {Time.time - _t0:F0} s, deaths {_deaths}");
            _flowStep = 1;
            _flowAt = Time.unscaledTime + 6f;   // let the stats card reveal, as a player reads it
            return true;
        }

        // After the end screen: RETURN TO MAIN MENU, CONTINUE, pause, RESTART CHECKPOINT - the menu flow a player uses.
        private int _flowStep;
        private float _flowAt;

        private bool HandleFlow(GameState gs)
        {
            if (_flowStep == 0) return false;
            if (Time.unscaledTime < _flowAt) return true;
            _flowAt = Time.unscaledTime + 1f;
            switch (_flowStep)
            {
                case 1:
                    if (ClickButton("BtnEndMenu")) { Log("FLOW", "clicked RETURN TO MAIN MENU"); _flowStep = 2; _flowAt = Time.unscaledTime + 4f; }
                    break;
                case 2:
                    if (gs != null && gs.IsMainMenu && FindActiveButton("BtnContinue") != null)
                    {
                        Log("FLOW", "main menu shown, CONTINUE available");
                        ClickButton("BtnContinue");
                        Log("FLOW", "clicked CONTINUE");
                        _flowStep = 3; _flowAt = Time.unscaledTime + 8f;
                    }
                    else if (gs != null && gs.IsMainMenu && FindActiveButton("BtnNewGame") != null && FindActiveButton("BtnContinue") == null)
                    {
                        Problem("FLOW", "main menu shown but no CONTINUE button after finishing");
                        Finish("finished: end screen and main menu (no Continue)");
                    }
                    break;
                case 3:
                    if (gs != null && !gs.IsMainMenu && PlayerController.Instance != null)
                    {
                        Log("FLOW", $"CONTINUE loaded checkpoint {gs.CurrentCheckpoint}, objective '{gs.CurrentObjective}'");
                        _in.Press(CretaceaInput.BotButton.Pause);
                        _flowStep = 4; _flowAt = Time.unscaledTime + 2f;
                    }
                    break;
                case 4:
                    if (gs != null && gs.IsPaused && ClickButton("BtnRestart"))
                    {
                        Log("FLOW", "paused, clicked RESTART CHECKPOINT");
                        _flowStep = 5; _flowAt = Time.unscaledTime + 8f;
                    }
                    else if (gs != null && !gs.IsPaused) _in.Press(CretaceaInput.BotButton.Pause);
                    break;
                case 5:
                    if (gs != null && !gs.IsPaused && !gs.IsMainMenu && PlayerController.Instance != null)
                    {
                        Log("FLOW", $"restarted at checkpoint {gs.CurrentCheckpoint}, objective '{gs.CurrentObjective}'");
                        Finish("finished: end screen, main menu, Continue, pause and restart checkpoint all worked");
                    }
                    break;
            }
            if (_flowStep > 0 && _flowStep < 6 && _flowSince > 0f && Time.unscaledTime - _flowSince > 90f)
                Finish("finished the game; menu flow stalled at step " + _flowStep);
            return true;
        }

        private float _flowSince;

        private bool HandleDeath(GameState gs, PlayerController player)
        {
            if (!(player.IsDead || gs.IsGameOver)) { _deadSince = -1f; return false; }
            if (_deadSince < 0f)
            {
                _deadSince = Time.time;
                _deaths++;
                Problem("DEATH", $"died at {player.transform.position:F1}, objective '{_objective}', goal {_goalId}");
            }
            if (Time.time - _deadSince > 4f && ClickButton("BtnRetry"))
            {
                Log("RETRY", $"pressed RETRY CHECKPOINT (checkpoint {gs.CurrentCheckpoint})");
                _deadSince = Time.time + 6f;
                _corners = new Vector3[0];
                _worldRebuildAt = Time.time + 3f;   // the retry reloads the scene: collect the new one
            }
            return true;
        }

        private bool HandleDocument(GameState gs)
        {
            if (!gs.IsDocumentOpen) { _docOpenedAt = -1f; return false; }
            if (_docOpenedAt < 0f) { _docOpenedAt = Time.time; Log("DOC", "document open: reading"); }
            if (Time.time - _docOpenedAt > 2.5f && (int)((Time.time - _docOpenedAt) * 2f) % 2 == 0)
                _in.Press(CretaceaInput.BotButton.Interact);
            return true;
        }

        // ================================================================ story tracking
        private void TrackStory(GameState gs)
        {
            string obj = gs.CurrentObjective ?? "";
            if (obj != _objective)
            {
                _objective = obj;
                _objectiveSince = Time.time;
                if (obj.Length > 0)
                {
                    bool repeat = !_seenObjectives.Add(obj);
                    Log("OBJECTIVE", (repeat ? "(again) " : "") + obj);
                }
                ResetInteraction();
                _corners = new Vector3[0];
                _worldRebuildAt = Time.time + 1.5f;   // after the beat has switched its objects
            }
            if (_worldRebuildAt > 0f && Time.time > _worldRebuildAt && !PlayerNavMesh.Building)
            {
                _worldRebuildAt = 0f;
                PlayerNavMesh.RebuildWorld();
                _repathAt = Time.time + 0.6f;
            }
            if (gs.CurrentCheckpoint != _chapter)
            {
                Log("CHAPTER", $"checkpoint {gs.CurrentCheckpoint} (previous {_chapter} took {Time.time - _chapterSince:F0} s)");
                _chapter = gs.CurrentCheckpoint;
                _chapterSince = Time.time;
            }
            if (obj.Length == 0 && !gs.InCutscene && Time.time - _objectiveSince > 30f && _goalKind == GoalKind.Wait)
            {
                Problem("NO_OBJECTIVE", "no objective on screen for 30 s outside a cutscene");
                _objectiveSince = Time.time;
            }
        }

        private void ResolveGoal(GameState gs, PlayerController player)
        {
            string o = _objective.ToLowerInvariant();
            _wantSprint = false;
            _crouchZone = false;

            if (o.Length == 0) { SetWait("no objective"); return; }
            if (o.Contains("flashlight from")) { SetInteract("pickup_flashlight"); return; }
            if (o.Contains("clock in")) { SetInteract("terminal"); return; }
            if (o.Contains("control room")) { SetInteract("control_console"); return; }
            if (o.Contains("main entrance")) { SetReachTrigger("lobby_beat"); return; }
            if (o.Contains("staff exit")) { SetReachTrigger("staff_exit_collapse"); return; }
            if (o.Contains("records annex") || o.Contains("evacuation manual")) { SetInteract("doc_manual"); return; }
            if (o.Contains("restore auxiliary power")) { SetReachTrigger("reach_dock"); return; }
            if (o.Contains("service path east")) { SetReachTrigger("paths_end"); return; }
            if (o.Contains("jammed") || o.Contains("crouch")) { SetReachTrigger("enter_paddock"); _crouchZone = true; return; }
            if (o.Contains("cross tyrant valley")) { SetReachTrigger("enter_substation"); return; }
            if (o.Contains("breaker"))
            {
                IInteractable best = null; float bestD = float.MaxValue;
                for (int i = 1; i <= 3; i++)
                {
                    IInteractable c = WorldRegistry.Instance.GetInteractable("substation_Breaker" + i);
                    if (c == null || !c.IsEnabled) continue;
                    float d = (c.FocusAnchor.position - player.transform.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = c; }
                }
                if (best != null) SetInteract(best.InteractId); else SetWait("no breaker left");
                return;
            }
            if (o.Contains("fuel pump")) { SetInteract("substation_FuelPump"); return; }
            if (o.Contains("diesel generator") || o.Contains("start the")) { SetInteract("substation_GeneratorStarter"); return; }
            if (o.Contains("transfer")) { SetInteract("substation_TransferLever"); return; }
            if (o.Contains("run!")) { SetReachTrigger("reach_yard"); _wantSprint = true; return; }
            if (o.Contains("e-0") && (o.Contains("find") || o.Contains("holding"))) { SetInteract("gate_box"); return; }
            if (o.Contains("coast road")) { SetReachTrigger("coast_ending"); return; }
            if (o.Contains("sprint out")) { SetReachTrigger(TriggerFired("through_e0_gate") ? "coast_ending" : "through_e0_gate"); _wantSprint = true; return; }
            SetWait("objective not understood");
            if (Time.time - _objectiveSince > 5f && _lastProblem.IndexOf(_objective, StringComparison.Ordinal) < 0)
                Problem("UNKNOWN_OBJECTIVE", "the bot cannot map this objective to a place: " + _objective);
        }

        private void SetWait(string why)
        {
            if (_goalKind != GoalKind.Wait) { _goalKind = GoalKind.Wait; _goalId = why; ResetInteraction(); }
        }

        private void SetInteract(string id)
        {
            IInteractable it = WorldRegistry.Instance != null ? WorldRegistry.Instance.GetInteractable(id) : null;
            if (it == null || it.FocusAnchor == null)
            {
                SetWait("missing interactable " + id);
                if (Time.time - _objectiveSince > 3f) Problem("MISSING", "objective names " + id + " but it is not in the world");
                return;
            }
            if (_goalKind != GoalKind.Interact || _goalId != id) { _goalKind = GoalKind.Interact; _goalId = id; ResetInteraction(); _corners = new Vector3[0]; }
            _goalTarget = it;
            _goalPos = it.FocusAnchor.position;
        }

        private void SetReachTrigger(string triggerId)
        {
            StoryTriggerVolume v = FindTrigger(triggerId);
            Vector3 p;
            if (v != null)
            {
                Collider c = v.GetComponent<Collider>();
                p = c != null ? c.bounds.center : v.transform.position;
            }
            else if (WorldRegistry.Instance != null && WorldRegistry.Instance.TryGetAnchor(triggerId, out Transform a)) p = a.position;
            else { SetWait("missing trigger " + triggerId); Problem("MISSING", "no trigger or anchor " + triggerId); return; }
            if (_goalKind != GoalKind.Reach || _goalId != triggerId) { _goalKind = GoalKind.Reach; _goalId = triggerId; _corners = new Vector3[0]; ResetInteraction(); }
            _goalPos = p;
        }

        private static readonly Dictionary<string, StoryTriggerVolume> TriggerCache = new Dictionary<string, StoryTriggerVolume>();

        private static StoryTriggerVolume FindTrigger(string id)
        {
            if (TriggerCache.TryGetValue(id, out StoryTriggerVolume v) && v != null) return v;
            foreach (StoryTriggerVolume t in FindObjectsByType<StoryTriggerVolume>(FindObjectsInactive.Include))
                if (t.TriggerId == id) { TriggerCache[id] = t; return t; }
            return null;
        }

        private static bool TriggerFired(string id)
        {
            StoryTriggerVolume v = FindTrigger(id);
            return v != null && v.HasFired;
        }

        // ================================================================ stance: crouch, flashlight, sprint
        private void Stance(GameState gs, PlayerController player)
        {
            float trexDist = NearestDino("trex", player.transform.position, out _);
            float raptorDist = NearestDino("raptor", player.transform.position, out bool raptorHunting);
            bool valley = _objective.ToLowerInvariant().Contains("cross tyrant valley");

            // Flashlight: on in the dark buildings, off in the valley while the T. rex is near (the ranger's advice).
            // A raptor close by and not yet hunting: sneak (crouched, light off), as the HUD's stealth meter teaches.
            bool sneak = raptorDist < 16f && !raptorHunting && !_wantSprint;
            bool wantLight = gs.HasFlashlight && trexDist >= 80f && !sneak;
            FlashlightController fl = player.Flashlight;
            if (fl != null && gs.HasFlashlight && Time.time > _flashCooldown && fl.IsBeamOn != wantLight)
            {
                _in.Press(CretaceaInput.BotButton.Flashlight);
                _flashCooldown = Time.time + 1.0f;
            }

            // Crouch in the tall grass of the valley when the T. rex is within 60 m, and under the jammed gate leaf.
            bool nearGap = _crouchZone && HorizontalDistance(player.transform.position, _goalPos) < 7f;
            bool wantCrouch = (valley && player.IsInGrass && trexDist < 60f) || nearGap || sneak || _forceCrouchUntil > Time.time;
            if (sneak) LogOnce("STEALTH", "raptor within 16 m: crouched, light off (" + gs.CurrentCheckpoint + ")");
            if (_wantSprint || raptorHunting && raptorDist < 14f) wantCrouch = false;
            if (wantCrouch != player.IsCrouching && Time.time > _crouchToggleCooldown)
            {
                _in.Press(CretaceaInput.BotButton.Crouch);
                _crouchToggleCooldown = Time.time + 0.6f;
                if (wantCrouch && valley && player.IsInGrass) LogOnce("STEALTH", "crouched in tall grass, light off (T. rex " + trexDist.ToString("F0") + " m)");
            }

            bool flee = raptorHunting && raptorDist < 14f;
            _in.Held[(int)CretaceaInput.BotButton.Sprint] = (_wantSprint || flee) && !player.IsCrouching;
        }

        private float _forceCrouchUntil;

        // ================================================================ movement
        private void Drive(GameState gs, PlayerController player)
        {
            Vector3 pos = player.transform.position;
            Vector3 dest = _goalPos;

            if (_goalKind == GoalKind.Interact)
            {
                float d = HorizontalDistance(player.EyePosition, dest);
                bool focused = ReferenceEquals(Interactor(player)?.FocusedInteractable, _goalTarget);
                if (d < _stopDist || (focused && d < 2.3f))
                {
                    _in.Move = Vector2.zero;
                    DoInteract(gs, player);
                    ResetProgress(player);
                    return;
                }
                _aimSince = -1f;
            }

            if (Time.time >= _repathAt || _corners.Length == 0) Repath(pos, dest);

            // advance along the corners
            while (_corner < _corners.Length - 1 && HorizontalDistance(pos, _corners[_corner]) < 0.7f) _corner++;
            Vector3 next = _corners.Length > 0 ? _corners[Mathf.Min(_corner, _corners.Length - 1)] : dest;

            Vector3 to = next - pos; to.y = 0f;
            float yawErr = to.sqrMagnitude > 0.01f ? Mathf.DeltaAngle(player.transform.eulerAngles.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg) : 0f;
            float pitchErr = -Pitch(player) + 4f;   // eyes level, slightly down: as a player walks
            Steer(yawErr, pitchErr, 300f);

            if (LowCeilingAhead(player, to)) _forceCrouchUntil = Time.time + 1.2f;
            float fwd = Mathf.Abs(yawErr) < 40f ? 1f : (Mathf.Abs(yawErr) < 80f ? 0.35f : 0f);
            Vector2 move = new Vector2(0f, fwd);
            if (fwd > 0f && to.sqrMagnitude > 0.01f) move = SlideAround(player, to.normalized, fwd);
            if (Time.time < _strafeUntil) move = new Vector2(_strafeSign, 0.3f);
            _in.Move = move;

            CheckProgress(player, dest);
        }

        /// <summary>A crawl gap (jammed roller door, bent gate leaf): something between crouch and standing head height.</summary>
        private static bool LowCeilingAhead(PlayerController player, Vector3 to)
        {
            Vector3 dir = to.sqrMagnitude > 0.01f ? to.normalized : player.transform.forward;
            for (int i = 0; i < 3; i++)
            {
                Vector3 o = player.transform.position + dir * (0.5f * i) + Vector3.up * 1.0f;
                if (Physics.Raycast(o, Vector3.up, 0.85f, GameLayers.PlayerCollisionMask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        /// <summary>
        /// What a player does at an open door leaf or a table corner the NavMesh does not know about: keep looking
        /// where you are going and side-step along the obstacle. Returns the stick value (x strafe, y forward).
        /// </summary>
        private static Vector2 SlideAround(PlayerController player, Vector3 want, float speed)
        {
            Vector3 chest = player.transform.position + Vector3.up * 1.0f;
            if (Physics.SphereCast(chest, 0.3f, want, out RaycastHit hit, 0.75f, GameLayers.PlayerCollisionMask, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.normal.y) < 0.5f)
            {
                Vector3 n = hit.normal; n.y = 0f; n.Normalize();
                Vector3 slide = Vector3.ProjectOnPlane(want, n);
                if (slide.sqrMagnitude < 0.04f) slide = Vector3.Cross(Vector3.up, n);   // head-on: pick a side
                slide = (slide.normalized + n * 0.25f).normalized;
                Transform t = player.transform;
                return new Vector2(Vector3.Dot(slide, t.right), Vector3.Dot(slide, t.forward)) * speed;
            }
            return new Vector2(0f, speed);
        }

        private void Repath(Vector3 from, Vector3 to)
        {
            _repathAt = Time.time + 1.0f;
            var filter = new NavMeshQueryFilter { agentTypeID = _agentType, areaMask = NavMesh.AllAreas };
            bool okA = NavMesh.SamplePosition(from, out NavMeshHit a, 3f, filter);
            bool okB = NavMesh.SamplePosition(to, out NavMeshHit b, 6f, filter);
            if (okA && okB && NavMesh.CalculatePath(a.position, b.position, filter, _path) && _path.corners.Length > 0)
            {
                _corners = _path.corners;
                _corner = _corners.Length > 1 ? 1 : 0;
                if (_path.status == NavMeshPathStatus.PathPartial && !_pathPartialLogged)
                {
                    _pathPartialLogged = true;
                    Log("NAV", $"partial NavMesh path to {_goalId} (ends {HorizontalDistance(_corners[_corners.Length - 1], to):F1} m short): walking the rest straight");
                }
                if (_path.status == NavMeshPathStatus.PathPartial)
                {
                    var list = new List<Vector3>(_corners) { to };
                    _corners = list.ToArray();
                }
            }
            else
            {
                _corners = new[] { to };
                _corner = 0;
                LogOnce("NAV", $"no NavMesh path to {_goalId} (from ok {okA}, to ok {okB}): walking straight");
            }
        }

        private void CheckProgress(PlayerController player, Vector3 dest)
        {
            Vector3 pos = player.transform.position;
            float goalDist = RemainingPath(pos, dest);
            if (goalDist < _bestGoalDist - 1.0f) { _bestGoalDist = goalDist; _lastProgressTime = Time.time; _unstickStage = 0; _stuckLogged = false; }

            if (Time.time - _lastMoveCheck > 1.5f)
            {
                float moved = HorizontalDistance(pos, _lastMovePos);
                _lastMoveCheck = Time.time;
                _lastMovePos = pos;
                if (moved < 0.4f && _in.Move.sqrMagnitude > 0.1f) Unstick(player);
            }

            if (!_stuckLogged && Time.time - _lastProgressTime > 20f)
            {
                _stuckLogged = true;
                Problem("STUCK", $"no progress toward {_goalId} for 20 s at {pos:F1} ({goalDist:F1} m left)");
            }
        }

        /// <summary>Distance still to walk along the current path (a detour round a fence is progress too).</summary>
        private float RemainingPath(Vector3 pos, Vector3 dest)
        {
            if (_corners.Length == 0) return HorizontalDistance(pos, dest);
            int i = Mathf.Clamp(_corner, 0, _corners.Length - 1);
            float d = HorizontalDistance(pos, _corners[i]);
            for (; i < _corners.Length - 1; i++) d += HorizontalDistance(_corners[i], _corners[i + 1]);
            return d + HorizontalDistance(_corners[_corners.Length - 1], dest);
        }

        private void Unstick(PlayerController player)
        {
            _unstickStage++;
            // 1. a closed, unlocked door in front: open it with the real interaction
            DoorController door = NearestClosedDoor(player.transform.position, 3.2f);
            if (door != null && _unstickStage <= 3)
            {
                if (door.IsLocked) { LogOnce("DOOR", $"blocked by locked door {door.DoorId}: '{door.LockedHint}'"); }
                else
                {
                    Log("DOOR", $"opening {door.DoorId}");
                    _goalOverrideDoor = door;
                    _doorUntil = Time.time + 3f;
                    return;
                }
            }
            if (_unstickStage == 2 || _unstickStage == 5) { _forceCrouchUntil = Time.time + 4f; Log("UNSTICK", "trying crouched"); return; }
            _strafeSign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _strafeUntil = Time.time + 0.9f;
            _repathAt = 0f;
        }

        private DoorController _goalOverrideDoor;
        private float _doorUntil;

        /// <summary>Opens the door the bot is stuck at: look at its handle, press Interact when the prompt shows.</summary>
        private bool HandleDoor(PlayerController player)
        {
            DoorController door = _goalOverrideDoor;
            if (door == null) return false;
            if (door.IsOpen || Time.time > _doorUntil)
            {
                if (!door.IsOpen) Problem("DOOR", $"could not open {door.DoorId} by looking at it and pressing Interact");
                _goalOverrideDoor = null;
                _repathAt = 0f;
                ResetProgress(player);
                return false;
            }
            Vector3 aim = door.FocusAnchor != null ? door.FocusAnchor.position : door.transform.position + Vector3.up;
            AimAt(player, aim);
            PlayerInteractor pi = Interactor(player);
            if (pi != null && ReferenceEquals(pi.FocusedInteractable, door) && Time.time > _interactCooldownUntil)
            {
                _in.Press(CretaceaInput.BotButton.Interact);
                _interactCooldownUntil = Time.time + 0.8f;
            }
            else if (HorizontalDistance(player.EyePosition, aim) > 1.8f) _in.Move = new Vector2(0f, 0.5f);
            return true;
        }

        // ================================================================ interaction
        private void DoInteract(GameState gs, PlayerController player)
        {
            PlayerInteractor pi = Interactor(player);
            AimAt(player, _goalTarget.FocusAnchor.position);
            if (_aimSince < 0f) _aimSince = Time.time;
            bool focused = pi != null && ReferenceEquals(pi.FocusedInteractable, _goalTarget);

            if (!focused)
            {
                _holding = false;
                if (Time.time - _aimSince > 4f && !_promptMissLogged)
                {
                    _promptMissLogged = true;
                    IInteractable other = pi != null ? pi.FocusedInteractable : null;
                    Problem("PROMPT", $"aiming at {_goalId} from {HorizontalDistance(player.EyePosition, _goalPos):F1} m for 4 s but no prompt (focus: {(other != null ? other.InteractId : "none")})");
                }
                if (Time.time - _aimSince > 4f) _stopDist = Mathf.Max(0.6f, _stopDist - 0.3f * Time.deltaTime);
                return;
            }

            if (_goalTarget.IsLocked || !_goalTarget.CanInteract)
            {
                if (Time.time > _interactCooldownUntil)
                {
                    _in.Press(CretaceaInput.BotButton.Interact);
                    _interactCooldownUntil = Time.time + 3f;
                    LogOnce("LOCKED", $"{_goalId} not usable yet: '{_goalTarget.LockedHint}'");
                }
                return;
            }

            if (_goalTarget.HoldDuration > 0.001f)
            {
                if (!_holding) Log("USE", $"holding Interact on {_goalId} ({_goalTarget.HoldDuration:F1} s)");
                _holding = true;
                _in.Held[(int)CretaceaInput.BotButton.Interact] = true;
                return;
            }

            if (Time.time > _interactCooldownUntil)
            {
                _in.Press(CretaceaInput.BotButton.Interact);
                _interactCooldownUntil = Time.time + 2.5f;
                Log("USE", $"pressed Interact on {_goalId} (prompt '{_goalTarget.PromptText}')");
            }
        }

        private void AimAt(PlayerController player, Vector3 target)
        {
            Vector3 to = target - player.EyePosition;
            float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
            Steer(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), Mathf.DeltaAngle(Pitch(player), pitch), 220f);
        }

        private void LookIdle(PlayerController player)
        {
            _in.Move = Vector2.zero;
            Steer(0f, -Pitch(player) + 2f, 60f);
        }

        /// <summary>Turns the view through the mouse-delta input, rate limited like a hand on a mouse.</summary>
        private void Steer(float yawErr, float pitchErr, float maxDegPerSec)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            float k = 1f - Mathf.Exp(-10f * dt);
            float yawStep = Mathf.Clamp(yawErr * k, -maxDegPerSec * dt, maxDegPerSec * dt);
            float pitchStep = Mathf.Clamp(pitchErr * k, -maxDegPerSec * dt, maxDegPerSec * dt);
            float invSign = SaveManager.Settings.invertY ? 1f : -1f;
            _in.LookDelta = new Vector2(yawStep / _lookGain, pitchStep / (_lookGain * invSign));
        }

        // ================================================================ helpers
        private void ClearFrameInput()
        {
            _in.Move = Vector2.zero;
            _in.LookDelta = Vector2.zero;
            _in.Held[(int)CretaceaInput.BotButton.Interact] = false;
            _in.Held[(int)CretaceaInput.BotButton.Sprint] = false;
            RefreshLookGain();
        }

        private void ClearInput()
        {
            _in.Move = Vector2.zero;
            _in.LookDelta = Vector2.zero;
            for (int i = 0; i < _in.Held.Length; i++) _in.Held[i] = false;
        }

        private void RefreshLookGain()
        {
            PlayerController p = PlayerController.Instance;
            if (p == null) return;
            FieldInfo f = typeof(PlayerController).GetField("baseMouseSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
            float baseSpeed = f != null ? (float)f.GetValue(p) : 0.1f;
            _lookGain = Mathf.Max(0.0001f, baseSpeed * SaveManager.Settings.mouseSensitivity);
        }

        private void ResetProgress(PlayerController player)
        {
            _bestGoalDist = float.MaxValue;
            _lastProgressTime = Time.time;
            _lastMoveCheck = Time.time;
            if (player != null) _lastMovePos = player.transform.position;
        }

        private void ResetInteraction()
        {
            _aimSince = -1f;
            _holding = false;
            _promptMissLogged = false;
            _stopDist = 1.6f;
            _pathPartialLogged = false;
            _bestGoalDist = float.MaxValue;
            _lastProgressTime = Time.time;
        }

        private static PlayerInteractor _cachedInteractor;
        private static PlayerInteractor Interactor(PlayerController p)
        {
            if (_cachedInteractor == null && p != null) _cachedInteractor = p.GetComponentInChildren<PlayerInteractor>();
            return _cachedInteractor;
        }

        private static float Pitch(PlayerController p)
        {
            float x = p.CameraPivot != null ? p.CameraPivot.localEulerAngles.x : 0f;
            return x > 180f ? x - 360f : x;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f; b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float NearestDino(string species, Vector3 pos, out bool hunting)
        {
            hunting = false;
            float best = float.MaxValue;
            IDinoSpawner sp = WorldRegistry.Instance != null ? WorldRegistry.Instance.DinoSpawner : null;
            if (sp == null) return best;
            foreach (IDinoController d in sp.GetActiveDinos(species))
            {
                if (d == null || d.Transform == null) continue;
                float dist = Vector3.Distance(d.Transform.position, pos);
                if (dist < best)
                {
                    best = dist;
                    hunting = d.CurrentState == DinoState.Hunt || d.CurrentState == DinoState.Attack;
                }
            }
            return best;
        }

        private void PacifyDinos()
        {
            IDinoSpawner sp = WorldRegistry.Instance != null ? WorldRegistry.Instance.DinoSpawner : null;
            if (sp == null) return;
            foreach (IDinoController d in sp.GetActiveDinos()) if (d != null) d.Aggression = 0f;
        }

        private static DoorController NearestClosedDoor(Vector3 pos, float radius)
        {
            DoorController best = null; float bestD = radius;
            foreach (DoorController d in FindObjectsByType<DoorController>())
            {
                if (d.IsOpen || d.IsBroken) continue;
                Vector3 c = d.FocusAnchor != null ? d.FocusAnchor.position : d.transform.position;
                float dist = HorizontalDistance(pos, c);
                if (dist < bestD) { bestD = dist; best = d; }
            }
            return best;
        }

        private static int SmallestAgentType()
        {
            int best = 0; float r = float.MaxValue;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
            {
                NavMeshBuildSettings s = NavMesh.GetSettingsByIndex(i);
                if (s.agentRadius < r) { r = s.agentRadius; best = s.agentTypeID; }
            }
            return best;
        }

        private static Button FindActiveButton(string name)
        {
            foreach (Button b in FindObjectsByType<Button>())
                if (b.name == name && b.isActiveAndEnabled && b.interactable) return b;
            return null;
        }

        private static bool ClickButton(string name)
        {
            Button b = FindActiveButton(name);
            if (b == null) return false;
            b.onClick.Invoke();
            return true;
        }

        private readonly HashSet<string> _onceKeys = new HashSet<string>();
        private void LogOnce(string kind, string msg)
        {
            if (_onceKeys.Add(kind + msg)) Log(kind, msg);
        }

        private void Problem(string kind, string msg)
        {
            _problemSerial++;
            _lastProblem = kind + ": " + msg;
            Log("PROBLEM " + kind, msg);
        }

        private void Log(string kind, string msg)
        {
            GameState gs = GameState.Instance;
            string line = $"[{Time.time - _t0,7:F1}s] [{(gs != null ? gs.CurrentCheckpoint.ToString() : "-")}] {kind}: {msg}";
            _events.Add(line);
            try { if (!string.IsNullOrEmpty(_logPath)) File.AppendAllText(_logPath, line + "\n"); } catch (Exception) { }
        }
    }
}
