using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cretacea.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public int checkpoint = (int)CheckpointId.Night;
        public float playTimeSeconds;
        public int deathCount;
        public bool hasFlashlight;
        public bool hasMasterKey;
        public float e0GateProgress;
        public List<string> flags = new List<string>();
        public List<string> documents = new List<string>();
        public List<int> unlockedCheckpoints = new List<int>();
    }

    [Serializable]
    public sealed class SettingsData
    {
        public int version = 1;
        public float fieldOfView = 68f;
        public float mouseSensitivity = 1.0f;
        public float gamepadSensitivity = 1.0f;
        public bool invertY = false;
        public bool gamepadRumble = true;
        public bool subtitlesEnabled = true;
        public bool soundCaptionsEnabled = true;
        public float masterVolume = 0.9f;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.95f;
        public float voVolume = 1.0f;
        public int qualityTier = 2;      // Not read anywhere yet (Settings has no quality row). QualitySettings order: 0 = High Fidelity, 1 = Balanced, 2 = Performant
        public float stpRenderScale = 0.85f;
        /// <summary>Player brightness in EV stops (-1.5..+1.5), applied as AtmosphereController.UserBrightnessEV.</summary>
        public float brightnessEV = 0f;
        /// <summary>Settings schema revision for one-time migrations (2 = look pass: vertical FOV default 68).</summary>
        public int settingsRevision = 0;
    }

    /// <summary>
    /// Handles atomic JSON persistence for game saves and user settings.
    /// </summary>
    public static class SaveManager
    {
        private const string SaveFileName = "cretacea_save.json";
        private const string SettingsFileName = "cretacea_settings.json";

        private static SettingsData _cachedSettings;

        /// <summary>
        /// Where saves live. Normally the player's persistent data folder. Automated test runs point it somewhere
        /// else (environment variable CRETACEA_SAVE_DIR, or in the editor the SessionState key "Cretacea.SaveDir")
        /// so a Play-mode test never overwrites the owner's real save.
        /// </summary>
        public static string SaveDirectory
        {
            get
            {
                string dir = Environment.GetEnvironmentVariable("CRETACEA_SAVE_DIR");
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(dir)) dir = UnityEditor.SessionState.GetString("Cretacea.SaveDir", "");
#endif
                if (string.IsNullOrEmpty(dir)) return Application.persistentDataPath;
                try { Directory.CreateDirectory(dir); } catch (Exception) { return Application.persistentDataPath; }
                return dir;
            }
        }

        public static string SavePath => Path.Combine(SaveDirectory, SaveFileName);
        public static string SettingsPath => Path.Combine(SaveDirectory, SettingsFileName);

        public static SettingsData Settings
        {
            get
            {
                if (_cachedSettings == null)
                {
                    _cachedSettings = LoadSettings();
                }
                return _cachedSettings;
            }
        }

        public static bool HasSaveFile() => File.Exists(SavePath);

        public static void SaveCurrentState(GameState state)
        {
            if (state == null) return;
            try
            {
                SaveData data = state.ExportSaveData();
                string json = JsonUtility.ToJson(data, true);
                WriteAtomic(SavePath, json);
                GameEvents.RaiseSaveCompleted();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to save game state: {ex.Message}");
            }
        }

        public static SaveData LoadSaveData()
        {
            if (!File.Exists(SavePath)) return null;
            try
            {
                string json = File.ReadAllText(SavePath);
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Corrupt or unreadable save file: {ex.Message}");
                return null;
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to delete save file: {ex.Message}");
            }
        }

        public static SettingsData LoadSettings()
        {
            if (!File.Exists(SettingsPath))
            {
                _cachedSettings = new SettingsData();
                return _cachedSettings;
            }

            try
            {
                string json = File.ReadAllText(SettingsPath);
                _cachedSettings = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to load settings, using defaults: {ex.Message}");
                _cachedSettings = new SettingsData();
            }

            return _cachedSettings;
        }

        public static void SaveSettings(SettingsData settings = null)
        {
            if (settings != null) _cachedSettings = settings;
            if (_cachedSettings == null) _cachedSettings = new SettingsData();

            try
            {
                string json = JsonUtility.ToJson(_cachedSettings, true);
                WriteAtomic(SettingsPath, json);
                GameEvents.RaiseSettingsChanged();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveManager] Failed to save settings: {ex.Message}");
            }
        }

        private static void WriteAtomic(string destinationPath, string content)
        {
            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tempPath = destinationPath + ".tmp";
            File.WriteAllText(tempPath, content);
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
            File.Move(tempPath, destinationPath);
        }
    }
}
