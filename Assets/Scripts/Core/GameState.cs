using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Authoritative persistent game state singleton across scenes, checkpoints, and UI overlays.
    /// Enforces exact 5/5 document accounting (including jeep_radio) and Substation checklist progression.
    /// </summary>
    public sealed class GameState : MonoBehaviour
    {
        public static GameState Instance { get; private set; }

        public const int TotalDocumentCount = 5;

        public static readonly string[] CanonicalDocumentIds =
        {
            "memo_switchover",
            "manual",
            "keeper_note",
            "jeep_radio",
            "log_halvorsen"
        };

        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _documents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<CheckpointId> _unlockedCheckpoints = new HashSet<CheckpointId> { CheckpointId.Intro, CheckpointId.Night };
        private readonly bool[] _breakersReset = new bool[3];

        public CheckpointId CurrentCheckpoint { get; private set; } = CheckpointId.Intro;
        public string CurrentObjective { get; private set; } = string.Empty;
        public bool HasFlashlight { get; private set; }
        public bool HasMasterKey { get; private set; }
        public bool InCutscene { get; private set; }
        public bool IsPaused { get; private set; }
        public bool IsMainMenu { get; private set; }
        public bool IsGameOver { get; private set; }
        public bool IsDocumentOpen { get; private set; }
        public bool IsJournalOpen { get; private set; }
        public float PlayTimeSeconds { get; private set; }
        public int DeathCount { get; private set; }

        // Substation 3 checklist state
        public bool FuelPrimed { get; private set; }
        public int GeneratorAttempts { get; private set; }
        public bool GeneratorRunning { get; private set; }
        public bool TransferComplete { get; private set; }
        public float E0GateProgress { get; private set; }

        public int BreakersResetCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _breakersReset.Length; i++)
                {
                    if (_breakersReset[i]) count++;
                }
                return count;
            }
        }

        public bool AllBreakersReset => BreakersResetCount >= 3;
        public int CollectedDocumentCount => _documents.Count;
        public IReadOnlyCollection<string> CollectedDocuments => _documents;
        public IReadOnlyCollection<string> ActiveFlags => _flags;
        public IReadOnlyCollection<CheckpointId> UnlockedCheckpoints => _unlockedCheckpoints;

        /// <summary>Returns true when player movement/look input should be suppressed by UI or cutscenes.</summary>
        public bool IsGameplayInputBlocked => InCutscene || IsPaused || IsMainMenu || IsGameOver || IsDocumentOpen;

        public static GameState EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<GameState>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("[GameState]");
            Instance = go.AddComponent<GameState>();
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
        }

        private void Update()
        {
            if (!IsMainMenu && !IsPaused && !IsGameOver && !InCutscene)
            {
                PlayTimeSeconds += Time.deltaTime;
            }
        }

        public void SetMainMenuState(bool isMainMenu)
        {
            IsMainMenu = isMainMenu;
            if (isMainMenu)
            {
                SetPaused(false);
                SetCutscene(false);
                IsGameOver = false;
                IsDocumentOpen = false;
                IsJournalOpen = false;
            }
        }

        public void SetCutscene(bool inCutscene)
        {
            if (InCutscene == inCutscene) return;
            InCutscene = inCutscene;
            if (inCutscene)
            {
                IsJournalOpen = false;
            }
            GameEvents.RaiseCutsceneStateChanged(inCutscene);
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Cursor.lockState = paused || IsMainMenu || IsDocumentOpen || IsJournalOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused || IsMainMenu || IsDocumentOpen || IsJournalOpen;
            GameEvents.RaisePauseStateChanged(paused);
        }

        public void SetDocumentOpen(bool open)
        {
            IsDocumentOpen = open;
            RefreshCursor();
        }

        public void SetJournalOpen(bool open)
        {
            if (InCutscene || IsMainMenu || IsGameOver || IsDocumentOpen) open = false;
            IsJournalOpen = open;
            RefreshCursor();
        }

        public void RefreshCursor()
        {
            bool unlock = IsPaused || IsMainMenu || IsDocumentOpen || IsJournalOpen || IsGameOver;
            Cursor.lockState = unlock ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = unlock;
        }

        public void SetObjective(string objective)
        {
            CurrentObjective = objective ?? string.Empty;
            GameEvents.RaiseObjectiveChanged(CurrentObjective);
        }

        public void SetHasFlashlight(bool hasFlashlight)
        {
            HasFlashlight = hasFlashlight;
            if (hasFlashlight) SetFlag("flashlight");
            else ClearFlag("flashlight");
            GameEvents.RaiseFlashlightAcquiredChanged(hasFlashlight);
        }

        public void SetHasMasterKey(bool hasKey)
        {
            HasMasterKey = hasKey;
            if (hasKey) SetFlag("master_key");
            else ClearFlag("master_key");
            GameEvents.RaiseMasterKeyChanged(hasKey);
        }

        public bool GetFlag(string key, bool defaultValue = false)
        {
            if (string.IsNullOrWhiteSpace(key)) return defaultValue;
            return _flags.Contains(key) ? true : defaultValue;
        }

        public void SetFlag(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                _flags.Add(key);
        }

        public void ClearFlag(string key)
        {
            if (!string.IsNullOrWhiteSpace(key))
                _flags.Remove(key);
        }

        public bool HasDocument(string docId) => !string.IsNullOrWhiteSpace(docId) && _documents.Contains(docId);

        /// <summary>
        /// Registers any of the 5 collectible documents (including jeep_radio) and broadcasts updated X/5 progress.
        /// </summary>
        public bool AddDocument(string docId)
        {
            if (string.IsNullOrWhiteSpace(docId)) return false;
            bool added = _documents.Add(docId);
            if (added)
            {
                GameEvents.RaiseDocumentCollected(docId, _documents.Count, TotalDocumentCount);
                SaveManager.SaveCurrentState(this);
            }
            return added;
        }

        public bool IsBreakerReset(int zeroBasedIndex)
        {
            return zeroBasedIndex >= 0 && zeroBasedIndex < _breakersReset.Length && _breakersReset[zeroBasedIndex];
        }

        public void MarkBreakerReset(int zeroBasedIndex)
        {
            if (zeroBasedIndex < 0 || zeroBasedIndex >= _breakersReset.Length) return;
            if (_breakersReset[zeroBasedIndex]) return;
            _breakersReset[zeroBasedIndex] = true;
            BroadcastSubstationSnapshot();
        }

        public void MarkFuelPrimed()
        {
            if (FuelPrimed) return;
            FuelPrimed = true;
            SetFlag("fuel_primed");
            BroadcastSubstationSnapshot();
        }

        public int IncrementGeneratorAttempt()
        {
            GeneratorAttempts = Mathf.Clamp(GeneratorAttempts + 1, 0, 3);
            if (GeneratorAttempts >= 3)
            {
                GeneratorRunning = true;
                SetFlag("gen_running");
            }
            BroadcastSubstationSnapshot();
            return GeneratorAttempts;
        }

        public void MarkTransferComplete()
        {
            TransferComplete = true;
            GeneratorRunning = true;
            FuelPrimed = true;
            _breakersReset[0] = _breakersReset[1] = _breakersReset[2] = true;
            SetFlag("aux_power");
            BroadcastSubstationSnapshot();
        }

        public void SetE0GateProgress(float progress01)
        {
            float clamped = Mathf.Clamp01(progress01);
            if (clamped > E0GateProgress)
            {
                E0GateProgress = clamped;
                GameEvents.RaiseE0GateProgressChanged(E0GateProgress);
            }
        }

        public SubstationChecklistSnapshot GetSubstationSnapshot()
        {
            return new SubstationChecklistSnapshot(
                BreakersResetCount,
                _breakersReset.Length,
                FuelPrimed,
                GeneratorAttempts,
                GeneratorRunning,
                TransferComplete);
        }

        public void BroadcastSubstationSnapshot()
        {
            GameEvents.RaiseSubstationProgressChanged(GetSubstationSnapshot());
        }

        public void RecordDeath()
        {
            DeathCount++;
            IsGameOver = true;
            IsJournalOpen = false;
            RefreshCursor();
            SaveManager.SaveCurrentState(this);
            GameEvents.RaisePlayerDied();
        }

        public void ClearGameOver()
        {
            IsGameOver = false;
            RefreshCursor();
        }

        public void SaveCheckpoint(CheckpointId checkpoint)
        {
            CurrentCheckpoint = checkpoint;
            UnlockUpTo(checkpoint);
            SaveManager.SaveCurrentState(this);
            GameEvents.RaiseCheckpointReached(checkpoint);
        }

        public void UnlockUpTo(CheckpointId checkpoint)
        {
            for (int i = 0; i <= (int)checkpoint; i++)
            {
                _unlockedCheckpoints.Add((CheckpointId)i);
            }
        }

        public void ResetForNewGame(CheckpointId startCheckpoint = CheckpointId.Intro)
        {
            _flags.Clear();
            _documents.Clear();
            Array.Clear(_breakersReset, 0, _breakersReset.Length);
            FuelPrimed = false;
            GeneratorAttempts = 0;
            GeneratorRunning = false;
            TransferComplete = false;
            E0GateProgress = 0f;
            PlayTimeSeconds = 0f;
            DeathCount = 0;
            IsGameOver = false;
            IsDocumentOpen = false;
            IsJournalOpen = false;
            CurrentObjective = string.Empty;
            ApplyCheckpointPreset(startCheckpoint);
        }

        /// <summary>
        /// Sets clean baseline flags/equipment for chapter jump or checkpoint load.
        /// Strictly enforces HasFlashlight = false for Intro and Night!
        /// </summary>
        public void ApplyCheckpointPreset(CheckpointId checkpoint)
        {
            CurrentCheckpoint = checkpoint;
            UnlockUpTo(checkpoint);
            IsGameOver = false;

            switch (checkpoint)
            {
                case CheckpointId.Intro:
                case CheckpointId.Night:
                    ClearFlag("blackout");
                    ClearFlag("lobby_done");
                    ClearFlag("collapse");
                    ClearFlag("manual_read");
                    ClearFlag("aux_power");
                    ClearFlag("e0_open");
                    SetHasFlashlight(false);
                    SetHasMasterKey(false);
                    ResetSubstationProgress();
                    break;

                case CheckpointId.Records:
                    SetFlag("blackout");
                    SetFlag("lobby_done");
                    SetFlag("collapse");
                    SetHasFlashlight(true);
                    SetHasMasterKey(GetFlag("manual_read"));
                    ResetSubstationProgress();
                    break;

                case CheckpointId.Dock:
                case CheckpointId.Paddock:
                    SetFlag("blackout");
                    SetFlag("collapse");
                    SetFlag("manual_read");
                    SetHasFlashlight(true);
                    SetHasMasterKey(true);
                    ResetSubstationProgress();
                    break;

                case CheckpointId.Substation:
                    SetFlag("blackout");
                    SetFlag("collapse");
                    SetFlag("manual_read");
                    SetHasFlashlight(true);
                    SetHasMasterKey(true);
                    if (!GetFlag("aux_power"))
                        ResetSubstationProgress();
                    else
                        MarkTransferComplete();
                    break;

                case CheckpointId.Yard:
                    SetFlag("blackout");
                    SetFlag("collapse");
                    SetFlag("manual_read");
                    SetHasFlashlight(true);
                    SetHasMasterKey(true);
                    MarkTransferComplete();
                    break;
            }
        }

        public void ResetSubstationProgress()
        {
            Array.Clear(_breakersReset, 0, _breakersReset.Length);
            FuelPrimed = false;
            GeneratorAttempts = 0;
            GeneratorRunning = false;
            TransferComplete = false;
            ClearFlag("fuel_primed");
            ClearFlag("gen_running");
            ClearFlag("aux_power");
            BroadcastSubstationSnapshot();
        }

        public void LoadFromSaveData(SaveData data)
        {
            if (data == null) return;
            _flags.Clear();
            _documents.Clear();
            _unlockedCheckpoints.Clear();
            _unlockedCheckpoints.Add(CheckpointId.Intro);
            _unlockedCheckpoints.Add(CheckpointId.Night);

            if (data.flags != null)
            {
                foreach (string f in data.flags) _flags.Add(f);
            }
            if (data.documents != null)
            {
                foreach (string d in data.documents) _documents.Add(d);
            }
            if (data.unlockedCheckpoints != null)
            {
                foreach (int cp in data.unlockedCheckpoints)
                {
                    if (Enum.IsDefined(typeof(CheckpointId), cp))
                        _unlockedCheckpoints.Add((CheckpointId)cp);
                }
            }

            CurrentCheckpoint = Enum.IsDefined(typeof(CheckpointId), data.checkpoint)
                ? (CheckpointId)data.checkpoint
                : CheckpointId.Night;
            PlayTimeSeconds = Mathf.Max(0f, data.playTimeSeconds);
            DeathCount = Mathf.Max(0, data.deathCount);
            HasFlashlight = data.hasFlashlight;
            HasMasterKey = data.hasMasterKey;
            E0GateProgress = Mathf.Clamp01(data.e0GateProgress);

            if (GetFlag("aux_power"))
                MarkTransferComplete();
            else
                ResetSubstationProgress();
        }

        public SaveData ExportSaveData()
        {
            var unlocked = new List<int>();
            foreach (CheckpointId cp in _unlockedCheckpoints) unlocked.Add((int)cp);

            return new SaveData
            {
                version = 1,
                checkpoint = (int)CurrentCheckpoint,
                playTimeSeconds = PlayTimeSeconds,
                deathCount = DeathCount,
                hasFlashlight = HasFlashlight,
                hasMasterKey = HasMasterKey,
                e0GateProgress = E0GateProgress,
                flags = new List<string>(_flags),
                documents = new List<string>(_documents),
                unlockedCheckpoints = unlocked
            };
        }

        public string BuildCompletionStatsSummary()
        {
            string timeFormatted = FormatPlayTime(PlayTimeSeconds);
            return $"You escaped Cretacea Park.\n\n" +
                   $"Time  {timeFormatted}     Deaths  {DeathCount}     Documents  {CollectedDocumentCount} / {TotalDocumentCount}\n\n" +
                   "Made with Unity 6 HDRP\n" +
                   "Textures, skies, plants, rocks: Poly Haven (CC0)\n" +
                   "Fonts: Barlow, Barlow Condensed, Courier Prime, Caveat, Kalam, Stardos Stencil (SIL OFL)\n" +
                   "Audio: VARCO Sound & Fish Audio";
        }

        public static string FormatPlayTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            int mins = total / 60;
            int secs = total % 60;
            int hrs = mins / 60;
            mins %= 60;
            return hrs > 0 ? $"{hrs}:{mins:00}:{secs:00}" : $"{mins:00}:{secs:00}";
        }

        public static string GetChapterTitle(CheckpointId cp)
        {
            return cp switch
            {
                CheckpointId.Intro => "PROLOGUE — CRETACEA PARK",
                CheckpointId.Night => "CHAPTER 1 — NIGHT SHIFT",
                CheckpointId.Records => "CHAPTER 2 — RECORDS ANNEX",
                CheckpointId.Dock => "CHAPTER 3 — SERVICE PATHS",
                CheckpointId.Paddock => "CHAPTER 4 — TYRANT VALLEY",
                CheckpointId.Substation => "CHAPTER 5 — SUBSTATION 3",
                CheckpointId.Yard => "CHAPTER 6 — OLD MAINTENANCE YARD",
                _ => "CRETACEA: BLACKOUT"
            };
        }
    }
}
