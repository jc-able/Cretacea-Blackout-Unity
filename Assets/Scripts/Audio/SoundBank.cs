using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Audio
{
    public enum AudioBusType
    {
        SfxWorld = 0,
        SfxFoley = 1,
        SfxDinos = 2,
        Ambience = 3,
        AmbienceOutdoor = 4,
        Music = 5,
        Voice = 6,
        Ui = 7
    }

    [Serializable]
    public sealed class SoundCueEntry
    {
        public string cueId;
        public AudioBusType bus = AudioBusType.SfxWorld;
        public bool spatial3D = true;
        public float volume = 1.0f;
        public float pitchMin = 0.94f;
        public float pitchMax = 1.06f;
        public float minDistance = 2.0f;
        public float maxDistance = 42.0f;
        public bool loop = false;
        public bool applyWallOcclusion = true;
        [Tooltip("Highest priority: populated automatically from Assets/VARCOSound/Generated/<cueId>*.wav")]
        public AudioClip[] varcoOverrideClips;
        [Tooltip("Baseline fallback clips migrated into Assets/Audio/")]
        public AudioClip[] defaultClips;

        public AudioClip PickRandomClip(ref int lastIndex)
        {
            AudioClip[] pool = GetActivePool();
            if (pool == null || pool.Length == 0) return null;
            if (pool.Length == 1)
            {
                lastIndex = 0;
                return pool[0];
            }

            int idx = UnityEngine.Random.Range(0, pool.Length);
            if (idx == lastIndex) idx = (idx + 1) % pool.Length;
            lastIndex = idx;
            return pool[idx];
        }

        public AudioClip[] GetActivePool()
        {
            if (varcoOverrideClips != null && HasValidClip(varcoOverrideClips))
                return varcoOverrideClips;
            return defaultClips;
        }

        public bool HasAnyClip()
        {
            return HasValidClip(varcoOverrideClips) || HasValidClip(defaultClips);
        }

        private static bool HasValidClip(AudioClip[] arr)
        {
            if (arr == null) return false;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Central ScriptableObject sound catalog for Cretacea: Blackout.
    /// Automatically prioritizes drop-in VARCO Sound clips from Assets/VARCOSound/Generated/
    /// (covering the 6 missing + 9 upgraded audio slots) over fallback clips in Assets/Audio/.
    /// Also supports runtime Resources lookup if a SoundBank asset has not been manually wired yet.
    /// </summary>
    [CreateAssetMenu(fileName = "SoundBank", menuName = "Cretacea/Audio/Sound Bank")]
    public sealed class SoundBank : ScriptableObject
    {
        public const string VarcoGeneratedFolder = "Assets/VARCOSound/Generated";
        public const string DefaultAudioFolder = "Assets/Audio";

        [SerializeField] private List<SoundCueEntry> cues = new List<SoundCueEntry>();

        private Dictionary<string, SoundCueEntry> _lookup;
        private readonly Dictionary<string, int> _lastClipIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, AudioClip> _runtimeResourceCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<SoundCueEntry> Cues => cues;

        public void RebuildLookup()
        {
            _lookup = new Dictionary<string, SoundCueEntry>(StringComparer.OrdinalIgnoreCase);
            if (cues == null) return;
            for (int i = 0; i < cues.Count; i++)
            {
                SoundCueEntry e = cues[i];
                if (e != null && !string.IsNullOrWhiteSpace(e.cueId))
                {
                    _lookup[e.cueId] = e;
                }
            }
        }

        public bool TryGetEntry(string cueId, out SoundCueEntry entry)
        {
            if (_lookup == null) RebuildLookup();
            if (!string.IsNullOrWhiteSpace(cueId) && _lookup.TryGetValue(cueId, out entry))
                return true;
            entry = null;
            return false;
        }

        public AudioClip ResolveClip(string cueId, out SoundCueEntry entry)
        {
            if (TryGetEntry(cueId, out entry))
            {
                int lastIdx = _lastClipIndex.TryGetValue(cueId, out int prev) ? prev : -1;
                AudioClip chosen = entry.PickRandomClip(ref lastIdx);
                _lastClipIndex[cueId] = lastIdx;
                if (chosen != null) return chosen;
            }

            // Runtime Resources fallback checking VARCOSound/Generated first, then Audio/*
            if (string.IsNullOrWhiteSpace(cueId)) return null;
            if (_runtimeResourceCache.TryGetValue(cueId, out AudioClip cached))
                return cached;

            string[] probePaths =
            {
                $"VARCOSound/Generated/{cueId}",
                $"Audio/VO/{cueId}",
                $"Audio/Music/{cueId}",
                $"Audio/Amb/{cueId}",
                $"Audio/SFX/{cueId}",
                $"Audio/UI/{cueId}",
                $"Audio/{cueId}"
            };

            for (int i = 0; i < probePaths.Length; i++)
            {
                AudioClip loaded = Resources.Load<AudioClip>(probePaths[i]);
                if (loaded != null)
                {
                    _runtimeResourceCache[cueId] = loaded;
                    return loaded;
                }
            }

            return null;
        }

        public float GetClipDuration(string cueId)
        {
            AudioClip clip = ResolveClip(cueId, out _);
            return clip != null ? clip.length : 0f;
        }

        /// <summary>
        /// Ensures every canonical Cretacea: Blackout cue slot exists in this SoundBank
        /// (including all 6 previously missing VARCO slots, 9 upgrade slots, 17 VO lines, 12 music tracks, and 7 footstep surfaces).
        /// </summary>
        public void PopulateCanonicalSlots()
        {
            EnsureSlot("foley_crouch", AudioBusType.SfxFoley, spatial: false, vol: 0.72f);
            EnsureSlot("foley_stand_up", AudioBusType.SfxFoley, spatial: false, vol: 0.70f);
            EnsureSlot("foley_grass_enter", AudioBusType.SfxFoley, spatial: true, vol: 0.85f, maxDist: 16f);
            EnsureSlot("glass_crack_large", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 55f);
            EnsureSlot("bush_rustle_close", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 35f);
            EnsureSlot("crowd_gasp_cheer", AudioBusType.SfxWorld, spatial: true, vol: 0.90f, maxDist: 45f);

            // 7 surface footstep slots (AUD-3 fix) — generic + posture-specific (crouch / walk / run)
            string[] surfaces = { "concrete", "tile", "gravel", "metal", "mud", "grass", "water" };
            float[] surfVols = { 0.68f, 0.70f, 0.74f, 0.82f, 0.75f, 0.64f, 0.80f };
            for (int i = 0; i < surfaces.Length; i++)
            {
                EnsureSlot($"foley_step_{surfaces[i]}", AudioBusType.SfxFoley, spatial: false, vol: surfVols[i]);
                EnsureSlot($"foley_step_{surfaces[i]}_crouch", AudioBusType.SfxFoley, spatial: false, vol: surfVols[i] * 0.65f);
                EnsureSlot($"foley_step_{surfaces[i]}_walk", AudioBusType.SfxFoley, spatial: false, vol: surfVols[i]);
                EnsureSlot($"foley_step_{surfaces[i]}_run", AudioBusType.SfxFoley, spatial: false, vol: surfVols[i] * 1.15f);
            }
            EnsureSlot("foley_land", AudioBusType.SfxFoley, spatial: false, vol: 0.85f);
            EnsureSlot("player_death_hit", AudioBusType.SfxFoley, spatial: false, vol: 1.0f);
            EnsureSlot("foley_pickup", AudioBusType.SfxFoley, spatial: false, vol: 0.85f);
            EnsureSlot("foley_paper_rustle", AudioBusType.SfxFoley, spatial: false, vol: 0.78f);
            EnsureSlot("foley_breath_exhausted", AudioBusType.SfxFoley, spatial: false, vol: 0.75f);
            EnsureSlot("flashlight_click_on", AudioBusType.SfxFoley, spatial: false, vol: 0.82f);
            EnsureSlot("flashlight_click_off", AudioBusType.SfxFoley, spatial: false, vol: 0.78f);

            // Ambience beds
            EnsureSlot("amb_park_day_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.65f, loop: true);
            EnsureSlot("amb_jungle_day_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.65f, loop: true);
            EnsureSlot("amb_jungle_night_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.60f, loop: true);
            EnsureSlot("amb_rain_exterior_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.72f, loop: true);
            EnsureSlot("amb_rain_menu_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.68f, loop: true);
            EnsureSlot("amb_wind_open_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.62f, loop: true);
            EnsureSlot("amb_coast_dawn_loop", AudioBusType.AmbienceOutdoor, spatial: false, vol: 0.75f, loop: true);
            EnsureSlot("amb_rain_on_roof_loop", AudioBusType.Ambience, spatial: false, vol: 0.70f, loop: true);
            EnsureSlot("amb_interior_hum_loop", AudioBusType.Ambience, spatial: false, vol: 0.42f, loop: true);
            EnsureSlot("amb_building_creak_loop", AudioBusType.Ambience, spatial: false, vol: 0.55f, loop: true);
            EnsureSlot("amb_metal_creak_loop", AudioBusType.Ambience, spatial: false, vol: 0.58f, loop: true);
            EnsureSlot("amb_alarm_distant_loop", AudioBusType.Ambience, spatial: false, vol: 0.40f, loop: true);
            EnsureSlot("amb_thunder_close", AudioBusType.SfxWorld, spatial: false, vol: 0.95f);
            EnsureSlot("amb_thunder_far", AudioBusType.SfxWorld, spatial: false, vol: 0.65f);

            // Dinosaurs
            EnsureSlot("trex_step", AudioBusType.SfxDinos, spatial: true, vol: 1.0f, minDist: 8f, maxDist: 140f);
            EnsureSlot("trex_roar_close", AudioBusType.SfxDinos, spatial: true, vol: 1.0f, minDist: 12f, maxDist: 220f);
            EnsureSlot("trex_roar_mid", AudioBusType.SfxDinos, spatial: true, vol: 0.95f, minDist: 14f, maxDist: 240f);
            EnsureSlot("trex_roar_far", AudioBusType.SfxDinos, spatial: true, vol: 0.90f, minDist: 20f, maxDist: 380f);
            EnsureSlot("trex_snort", AudioBusType.SfxDinos, spatial: true, vol: 0.85f, minDist: 6f, maxDist: 80f);
            EnsureSlot("trex_sniff", AudioBusType.SfxDinos, spatial: true, vol: 0.82f, minDist: 4f, maxDist: 65f);
            EnsureSlot("trex_bite", AudioBusType.SfxDinos, spatial: true, vol: 1.0f, minDist: 4f, maxDist: 65f);
            EnsureSlot("raptor_step", AudioBusType.SfxDinos, spatial: true, vol: 0.78f, minDist: 2f, maxDist: 32f);
            EnsureSlot("raptor_screech", AudioBusType.SfxDinos, spatial: true, vol: 0.95f, minDist: 4f, maxDist: 75f);
            EnsureSlot("raptor_chirp", AudioBusType.SfxDinos, spatial: true, vol: 0.82f, minDist: 3f, maxDist: 48f);
            EnsureSlot("raptor_sniff", AudioBusType.SfxDinos, spatial: true, vol: 0.80f, minDist: 2f, maxDist: 28f);
            EnsureSlot("raptor_breath_close", AudioBusType.SfxDinos, spatial: true, vol: 0.85f, minDist: 2f, maxDist: 22f);
            EnsureSlot("raptor_attack", AudioBusType.SfxDinos, spatial: true, vol: 1.0f, minDist: 3f, maxDist: 45f);
            EnsureSlot("raptor_call_distant", AudioBusType.SfxDinos, spatial: true, vol: 0.85f, minDist: 12f, maxDist: 150f);
            EnsureSlot("sauropod_call_far", AudioBusType.SfxDinos, spatial: true, vol: 0.90f, minDist: 20f, maxDist: 260f);

            // Doors, Gates, Substation & World SFX
            EnsureSlot("door_interior_open", AudioBusType.SfxWorld, spatial: true, vol: 0.80f, maxDist: 24f);
            EnsureSlot("door_interior_close", AudioBusType.SfxWorld, spatial: true, vol: 0.80f, maxDist: 24f);
            EnsureSlot("door_old_open", AudioBusType.SfxWorld, spatial: true, vol: 0.85f, maxDist: 28f);
            EnsureSlot("door_old_close", AudioBusType.SfxWorld, spatial: true, vol: 0.85f, maxDist: 28f);
            EnsureSlot("door_metal_open", AudioBusType.SfxWorld, spatial: true, vol: 0.88f, maxDist: 32f);
            EnsureSlot("door_metal_close", AudioBusType.SfxWorld, spatial: true, vol: 0.88f, maxDist: 32f);
            EnsureSlot("door_locked_rattle", AudioBusType.SfxWorld, spatial: true, vol: 0.82f, maxDist: 20f);
            EnsureSlot("door_bash_hit", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 45f);
            EnsureSlot("door_bash_metal", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 45f);
            EnsureSlot("door_break_open", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 55f);
            EnsureSlot("gate_heavy_slide", AudioBusType.SfxWorld, spatial: true, vol: 0.98f, maxDist: 85f);
            EnsureSlot("gate_motor_grind", AudioBusType.SfxWorld, spatial: true, vol: 0.92f, maxDist: 65f);
            EnsureSlot("gate_open_clunk", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 65f);
            EnsureSlot("gate_close_slam", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 70f);
            EnsureSlot("chainlink_impact", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 50f);
            EnsureSlot("fence_smash", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 120f);
            EnsureSlot("wall_impact_heavy", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 90f);
            EnsureSlot("breaker_reset_clunk", AudioBusType.SfxWorld, spatial: true, vol: 0.90f, maxDist: 55f);
            EnsureSlot("fuel_pump_stroke", AudioBusType.SfxWorld, spatial: true, vol: 0.85f, maxDist: 35f);
            EnsureSlot("fuel_pump_primed", AudioBusType.SfxWorld, spatial: true, vol: 0.90f, maxDist: 40f);
            EnsureSlot("gen_start_fail", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 90f);
            EnsureSlot("gen_start_success", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 130f);
            EnsureSlot("gen_engine_loop", AudioBusType.SfxWorld, spatial: true, vol: 0.82f, minDist: 5f, maxDist: 110f, loop: true);
            EnsureSlot("lever_transfer_clunk", AudioBusType.SfxWorld, spatial: true, vol: 0.95f, maxDist: 50f);
            EnsureSlot("power_restore_surge", AudioBusType.SfxWorld, spatial: false, vol: 0.95f);
            EnsureSlot("floodlights_on", AudioBusType.SfxWorld, spatial: true, vol: 0.92f, maxDist: 80f);
            EnsureSlot("power_transformer_explosion", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, minDist: 30f, maxDist: 400f);
            EnsureSlot("power_failure_wind_down", AudioBusType.SfxWorld, spatial: false, vol: 0.92f);
            EnsureSlot("power_emergency_on", AudioBusType.SfxWorld, spatial: false, vol: 0.85f);
            EnsureSlot("structure_groan", AudioBusType.SfxWorld, spatial: true, vol: 0.92f, maxDist: 45f);
            EnsureSlot("ceiling_collapse", AudioBusType.SfxWorld, spatial: true, vol: 1.0f, maxDist: 65f);
            EnsureSlot("electric_spark", AudioBusType.SfxWorld, spatial: true, vol: 0.72f, maxDist: 28f);
            EnsureSlot("pa_chime", AudioBusType.Ui, spatial: false, vol: 0.80f);
            EnsureSlot("pa_alarm_short", AudioBusType.Ui, spatial: false, vol: 0.82f);
            EnsureSlot("radio_static_burst", AudioBusType.Voice, spatial: false, vol: 0.70f);
            EnsureSlot("radio_ranger_log", AudioBusType.Voice, spatial: true, vol: 0.85f, maxDist: 24f);
            EnsureSlot("radio_dies", AudioBusType.Voice, spatial: false, vol: 0.85f);
            EnsureSlot("ui_terminal_beep", AudioBusType.Ui, spatial: false, vol: 0.80f);
            EnsureSlot("ui_denied", AudioBusType.Ui, spatial: false, vol: 0.78f);
            EnsureSlot("ui_hover", AudioBusType.Ui, spatial: false, vol: 0.55f);
            EnsureSlot("ui_click", AudioBusType.Ui, spatial: false, vol: 0.72f);
            EnsureSlot("ui_back", AudioBusType.Ui, spatial: false, vol: 0.70f);
            EnsureSlot("ui_pause", AudioBusType.Ui, spatial: false, vol: 0.72f);
            EnsureSlot("ui_objective", AudioBusType.Ui, spatial: false, vol: 0.76f);
            EnsureSlot("ui_document_open", AudioBusType.Ui, spatial: false, vol: 0.78f);
            EnsureSlot("ui_document_close", AudioBusType.Ui, spatial: false, vol: 0.76f);

            // All 17 Fish Audio VO lines
            string[] vos =
            {
                "vo_dana_01", "vo_dana_02", "vo_dana_03", "vo_dana_04", "vo_dana_05", "vo_dana_06", "vo_dana_07",
                "vo_pa_01", "vo_pa_02", "vo_pa_03",
                "vo_vance_01", "vo_vance_02", "vo_vance_03", "vo_vance_04", "vo_vance_05",
                "vo_ranger_01", "vo_ranger_02"
            };
            for (int i = 0; i < vos.Length; i++) EnsureSlot(vos[i], AudioBusType.Voice, spatial: false, vol: 1.0f);

            // All 12 Music Tracks (6 states x 2 variations)
            string[] music =
            {
                "mus_calm_day_01", "mus_calm_day_02",
                "mus_menu_01", "mus_menu_02",
                "mus_night_unease_01", "mus_night_unease_02",
                "mus_tension_01", "mus_tension_02",
                "mus_chase_01", "mus_chase_02",
                "mus_ending_01", "mus_ending_02"
            };
            for (int i = 0; i < music.Length; i++) EnsureSlot(music[i], AudioBusType.Music, spatial: false, vol: 0.72f, loop: true);

            RebuildLookup();
        }

        private void EnsureSlot(
            string id,
            AudioBusType bus,
            bool spatial,
            float vol = 1.0f,
            float minDist = 2.0f,
            float maxDist = 42.0f,
            bool loop = false)
        {
            if (cues == null) cues = new List<SoundCueEntry>();
            for (int i = 0; i < cues.Count; i++)
            {
                if (string.Equals(cues[i].cueId, id, StringComparison.OrdinalIgnoreCase)) return;
            }

            cues.Add(new SoundCueEntry
            {
                cueId = id,
                bus = bus,
                spatial3D = spatial,
                volume = vol,
                pitchMin = bus == AudioBusType.Voice || bus == AudioBusType.Music ? 1.0f : 0.94f,
                pitchMax = bus == AudioBusType.Voice || bus == AudioBusType.Music ? 1.0f : 1.06f,
                minDistance = minDist,
                maxDistance = maxDist,
                loop = loop,
                applyWallOcclusion = spatial
            });
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scans Assets/VARCOSound/Generated/ (highest priority) and Assets/Audio/ (baseline fallback)
        /// matching file names starting with each cueId (e.g. foley_step_metal_01.wav -> foley_step_metal).
        /// </summary>
        [ContextMenu("Auto-Bind Project Audio & VARCO Overrides")]
        public int AutoBindFromProjectAssets()
        {
            PopulateCanonicalSlots();
            var varcoClips = LoadClipsByStem(VarcoGeneratedFolder);
            var defaultClips = LoadClipsByStem(DefaultAudioFolder);
            int boundCount = 0;

            for (int i = 0; i < cues.Count; i++)
            {
                SoundCueEntry entry = cues[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.cueId)) continue;

                List<AudioClip> vMatches = MatchClipsForCue(varcoClips, entry.cueId, includeDefaultOnlyFallbacks: false);
                List<AudioClip> dMatches = MatchClipsForCue(defaultClips, entry.cueId, includeDefaultOnlyFallbacks: true);

                entry.varcoOverrideClips = vMatches.ToArray();
                entry.defaultClips = dMatches.ToArray();
                if (vMatches.Count > 0 || dMatches.Count > 0) boundCount++;
            }

            RebuildLookup();
            UnityEditor.EditorUtility.SetDirty(this);
            return boundCount;
        }

        private static List<(string stem, AudioClip clip)> LoadClipsByStem(string folder)
        {
            var list = new List<(string, AudioClip)>();
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder)) return list;

            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                AudioClip clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    string stem = System.IO.Path.GetFileNameWithoutExtension(path);
                    list.Add((stem, clip));
                }
            }
            return list;
        }

        private static readonly Dictionary<string, string[]> FallbackCueAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "foley_step_concrete", new[] { "foley_step_concrete_walk" } },
            { "foley_step_tile", new[] { "foley_step_tile_walk" } },
            { "foley_step_gravel", new[] { "foley_step_gravel_walk" } },
            { "foley_step_metal", new[] { "foley_step_metal_walk" } },
            { "foley_step_mud", new[] { "foley_step_mud_walk" } },
            { "foley_step_grass", new[] { "foley_step_grass_walk" } },
            { "foley_step_water", new[] { "foley_step_water_walk" } },
            { "foley_paper_rustle", new[] { "ui_document_open", "ui_document_close" } },
            { "flashlight_click_on", new[] { "foley_flashlight_click" } },
            { "flashlight_click_off", new[] { "foley_flashlight_click" } },
            { "trex_bite", new[] { "trex_attack" } },
            { "raptor_chirp", new[] { "raptor_chirp_alert" } },
            { "door_locked_rattle", new[] { "door_interior_locked", "door_metal_locked", "door_old_locked" } },
            { "door_bash_hit", new[] { "door_interior_bash", "door_old_bash" } },
            { "door_bash_metal", new[] { "door_metal_bash" } },
            { "door_break_open", new[] { "door_interior_slam_open", "door_old_slam_open", "door_metal_slam_open" } },
            { "ui_click", new[] { "ui_select" } }
        };

        /// <summary>
        /// Fallback aliases used only when binding baseline clips from Assets/Audio/.
        /// Kept separate from VARCO matching so any future clip in Assets/VARCOSound/Generated/
        /// automatically takes highest priority via varcoOverrideClips without cross-cue leakage.
        /// </summary>
        private static readonly Dictionary<string, string[]> DefaultOnlyFallbackAliases = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "foley_crouch", new[] { "foley_step_grass_crouch" } },
            { "foley_stand_up", new[] { "foley_pickup" } },
            { "foley_grass_enter", new[] { "foley_step_grass_walk" } },
            { "glass_crack_large", new[] { "ceiling_collapse" } },
            { "bush_rustle_close", new[] { "foley_step_grass_run" } },
            { "crowd_gasp_cheer", new[] { "pa_chime" } }
        };

        private static List<AudioClip> MatchClipsForCue(List<(string stem, AudioClip clip)> pool, string cueId, bool includeDefaultOnlyFallbacks)
        {
            var matches = MatchExactOrNumbered(pool, cueId);
            if (matches.Count > 0) return matches;

            AppendAliasMatches(pool, FallbackCueAliases, cueId, matches);
            if (matches.Count > 0) return matches;

            if (includeDefaultOnlyFallbacks)
            {
                AppendAliasMatches(pool, DefaultOnlyFallbackAliases, cueId, matches);
            }
            return matches;
        }

        private static void AppendAliasMatches(
            List<(string stem, AudioClip clip)> pool,
            Dictionary<string, string[]> aliasTable,
            string cueId,
            List<AudioClip> matches)
        {
            if (aliasTable.TryGetValue(cueId, out string[] aliases))
            {
                for (int a = 0; a < aliases.Length; a++)
                {
                    var aliasMatches = MatchExactOrNumbered(pool, aliases[a]);
                    for (int m = 0; m < aliasMatches.Count; m++)
                    {
                        if (!matches.Contains(aliasMatches[m]))
                            matches.Add(aliasMatches[m]);
                    }
                }
            }
        }

        private static List<AudioClip> MatchExactOrNumbered(List<(string stem, AudioClip clip)> pool, string prefix)
        {
            var matches = new List<AudioClip>();
            for (int i = 0; i < pool.Count; i++)
            {
                string stem = pool[i].stem;
                if (string.Equals(stem, prefix, StringComparison.OrdinalIgnoreCase)
                    || (stem.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase) && IsNumericTakeSuffix(stem, prefix.Length + 1)))
                {
                    matches.Add(pool[i].clip);
                }
            }
            return matches;
        }

        private static bool IsNumericTakeSuffix(string stem, int startIdx)
        {
            if (startIdx >= stem.Length) return false;
            for (int i = startIdx; i < stem.Length; i++)
            {
                if (!char.IsDigit(stem[i])) return false;
            }
            return true;
        }
#endif
    }
}
