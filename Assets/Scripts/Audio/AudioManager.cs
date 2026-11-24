using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Cretacea.Core;

namespace Cretacea.Audio
{
    /// <summary>
    /// Master AAA Audio Manager for Cretacea: Blackout.
    /// - Wires seamlessly into Assets/Audio/CretaceaMixer.mixer buses (Master, Music, Ambience, AmbOut,
    ///   SFX_World, SFX_Foley, SFX_Dinos, VO, UI) + dynamic volume sliders from SaveManager.Settings.
    /// - Pooled 3D spatial AudioSources with AudioOcclusion3D wall muffling.
    /// - Dual-deck equal-power crossfading Music controller (skips mus_chase_01's 22.5 s quiet intro!).
    /// - Multi-layer Ambience controller smoothly low-passing outdoor storm rain (1.4 kHz indoors <-> 20 kHz outdoors).
    /// - Sequential Voice playback with automatic -6 dB ducking on Music & Ambience and pause handling.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer & Bank")]
        [SerializeField] private AudioMixer masterMixer;
        [SerializeField] private SoundBank soundBank;
        [SerializeField] private int spatialPoolSize = 28;

        [Header("Low-Pass & Ducking Tuning")]
        [SerializeField] private float outdoorCutoffIndoorsHz = 1400f;
        [SerializeField] private float outdoorCutoffOutdoorsHz = 22000f;
        [SerializeField] private float voDuckMultiplier = 0.50f; // -6 dB under Voice lines
        [SerializeField] private float chase01IntroSkipSeconds = 22.5f;

        // Canonical Assets/Audio/CretaceaMixer.mixer exposed parameter & snapshot names
        public const string ParamMasterVol = "MasterVol";
        public const string ParamMusVol = "MusVol";
        public const string ParamAmbVol = "AmbVol";
        public const string ParamAmbOutCutoff = "AmbOutCutoff";
        public const string ParamVoVol = "VoVol";
        public const string ParamUiVol = "UiVol";
        public const string ParamSfxVol = "SfxVol";
        public const string ParamSfxWorldVol = "SfxWorldVol";
        public const string ParamSfxFoleyVol = "SfxFoleyVol";
        public const string ParamSfxDinosVol = "SfxDinosVol";

        private AudioMixerGroup _grpMusic;
        private AudioMixerGroup _grpAmbience;
        private AudioMixerGroup _grpAmbOut;
        private AudioMixerGroup _grpSfxWorld;
        private AudioMixerGroup _grpSfxFoley;
        private AudioMixerGroup _grpSfxDinos;
        private AudioMixerGroup _grpVo;
        private AudioMixerGroup _grpUi;

        private AudioMixerSnapshot _snapOutdoors;
        private AudioMixerSnapshot _snapIndoors;
        private AudioMixerSnapshot _snapVoDucking;
        private AudioMixerSnapshot _snapIndoorsVoDucking;
        private bool _mixerHasAmbOutCutoff;

        // Dual music decks for equal-power crossfades
        private AudioSource _musicDeckA;
        private AudioSource _musicDeckB;
        private bool _deckAActive = true;
        private string _activeMusicState = string.Empty;
        private int _musicVariantToggle;
        private Coroutine _musicFadeRoutine;

        // Ambience layer sources (outdoor rain/wind has dedicated LowPass filter for interior muffling!)
        private readonly Dictionary<string, AudioSource> _activeAmbienceLayers = new Dictionary<string, AudioSource>(StringComparer.OrdinalIgnoreCase);
        private readonly List<AudioLowPassFilter> _outdoorAmbFilters = new List<AudioLowPassFilter>();
        private string _currentAmbZone = string.Empty;
        private bool _playerIndoors;
        private float _currentOutdoorCutoff = 22000f;

        // Voice & 2D UI sources
        private AudioSource _voSource;
        private AudioSource _uiSource;
        private float _currentDuckFactor = 1f;

        // Pooled 3D spatial sources + named loops (e.g. gen_engine_loop)
        private readonly List<PooledEmitter> _spatialPool = new List<PooledEmitter>();
        private readonly Dictionary<string, PooledEmitter> _activeLoops = new Dictionary<string, PooledEmitter>(StringComparer.OrdinalIgnoreCase);

        private sealed class PooledEmitter
        {
            public GameObject Go;
            public AudioSource Source;
            public AudioOcclusion3D Occlusion;
            public Transform FollowTarget;
            public bool IsLoop;
            public string LoopId;
        }

        public SoundBank Bank => soundBank;
        public AudioMixer MasterMixer => masterMixer;

        public void SetMasterMixer(AudioMixer mixer)
        {
            masterMixer = mixer;
            ResolveMixerGroups();
            RebindActiveSourceGroups();
            ApplySettingsVolumes();
        }

        public void TransitionToSnapshot(string snapshotName, float transitionSeconds = 0.35f)
        {
            if (masterMixer == null || string.IsNullOrWhiteSpace(snapshotName)) return;
            AudioMixerSnapshot snap = snapshotName switch
            {
                "Outdoors" => _snapOutdoors ?? masterMixer.FindSnapshot("Outdoors"),
                "Indoors" => _snapIndoors ?? masterMixer.FindSnapshot("Indoors"),
                "VO_Ducking" => _snapVoDucking ?? masterMixer.FindSnapshot("VO_Ducking"),
                "Indoors_VO_Ducking" => _snapIndoorsVoDucking ?? masterMixer.FindSnapshot("Indoors_VO_Ducking"),
                _ => masterMixer.FindSnapshot(snapshotName)
            };
            snap?.TransitionTo(Mathf.Max(0f, transitionSeconds));
        }

        public static AudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<AudioManager>();
            if (existing != null)
            {
                Instance = existing;
                return Instance;
            }

            var go = new GameObject("[AudioManager]");
            Instance = go.AddComponent<AudioManager>();
            go.AddComponent<SurfaceAudioDetector>();
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

            EnsureSoundBank();
            ResolveMixerGroups();
            CreateCoreSources();
            CreateSpatialPool();
            ApplySettingsVolumes();
        }

        private void OnEnable()
        {
            GameEvents.OnPlaySoundRequested += OnPlaySoundEvent;
            GameEvents.OnPlayLoopRequested += OnPlayLoopEvent;
            GameEvents.OnStopLoopRequested += OnStopLoopEvent;
            GameEvents.OnMusicStateRequested += OnMusicStateEvent;
            GameEvents.OnAmbienceZoneRequested += OnAmbienceZoneEvent;
            GameEvents.OnVoiceRequested += OnVoiceEvent;
            GameEvents.OnInteriorChanged += OnInteriorChanged;
            GameEvents.OnPauseStateChanged += OnPauseChanged;
            GameEvents.OnSettingsChanged += ApplySettingsVolumes;
        }

        private void OnDisable()
        {
            GameEvents.OnPlaySoundRequested -= OnPlaySoundEvent;
            GameEvents.OnPlayLoopRequested -= OnPlayLoopEvent;
            GameEvents.OnStopLoopRequested -= OnStopLoopEvent;
            GameEvents.OnMusicStateRequested -= OnMusicStateEvent;
            GameEvents.OnAmbienceZoneRequested -= OnAmbienceZoneEvent;
            GameEvents.OnVoiceRequested -= OnVoiceEvent;
            GameEvents.OnInteriorChanged -= OnInteriorChanged;
            GameEvents.OnPauseStateChanged -= OnPauseChanged;
            GameEvents.OnSettingsChanged -= ApplySettingsVolumes;
        }

        private void Update()
        {
            // Keep attached loops tracking moving targets (e.g. generator / vehicles)
            for (int i = 0; i < _spatialPool.Count; i++)
            {
                PooledEmitter e = _spatialPool[i];
                if (e.FollowTarget != null && e.Source.isPlaying)
                {
                    e.Go.transform.position = e.FollowTarget.position;
                }
            }

            // Smooth outdoor storm low-pass transition when stepping indoors / outdoors
            float targetCutoff = _playerIndoors ? outdoorCutoffIndoorsHz : outdoorCutoffOutdoorsHz;
            if (Mathf.Abs(_currentOutdoorCutoff - targetCutoff) > 2f)
            {
                _currentOutdoorCutoff = Mathf.Lerp(_currentOutdoorCutoff, targetCutoff, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
                if (masterMixer != null) masterMixer.SetFloat("AmbOutCutoff", _currentOutdoorCutoff);
                for (int i = 0; i < _outdoorAmbFilters.Count; i++)
                {
                    if (_outdoorAmbFilters[i] != null)
                        _outdoorAmbFilters[i].cutoffFrequency = _currentOutdoorCutoff;
                }
            }

            // Automatic -6 dB ducking under active Voice lines
            bool voActive = _voSource != null && _voSource.isPlaying;
            float targetDuck = voActive ? voDuckMultiplier : 1.0f;
            if (Mathf.Abs(_currentDuckFactor - targetDuck) > 0.005f)
            {
                _currentDuckFactor = Mathf.Lerp(_currentDuckFactor, targetDuck, 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
                ApplyDuckingToBuses();
            }
        }

        private void EnsureSoundBank()
        {
            if (soundBank == null)
            {
                soundBank = Resources.Load<SoundBank>("CretaceaSoundBank")
                            ?? Resources.Load<SoundBank>("SoundBank");
            }
            if (soundBank == null)
            {
                soundBank = ScriptableObject.CreateInstance<SoundBank>();
                soundBank.PopulateCanonicalSlots();
            }
            else
            {
                soundBank.RebuildLookup();
            }
        }

        private void ResolveMixerGroups()
        {
            if (masterMixer == null)
            {
                masterMixer = Resources.Load<AudioMixer>("CretaceaMixer");
            }
#if UNITY_EDITOR
            if (masterMixer == null)
            {
                masterMixer = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/CretaceaMixer.mixer");
            }
#endif
            _mixerHasAmbOutCutoff = false;
            if (masterMixer == null) return;

            _grpMusic = FindGroup("Music");
            _grpAmbience = FindGroup("Ambience");
            _grpAmbOut = FindGroup("AmbOut") ?? _grpAmbience;
            _grpSfxWorld = FindGroup("SFX_World") ?? FindGroup("SFX") ?? FindGroup("World");
            _grpSfxFoley = FindGroup("SFX_Foley") ?? FindGroup("Foley") ?? _grpSfxWorld;
            _grpSfxDinos = FindGroup("SFX_Dinos") ?? FindGroup("Dinos") ?? _grpSfxWorld;
            _grpVo = FindGroup("VO");
            _grpUi = FindGroup("UI");

            _snapOutdoors = masterMixer.FindSnapshot("Outdoors");
            _snapIndoors = masterMixer.FindSnapshot("Indoors");
            _snapVoDucking = masterMixer.FindSnapshot("VO_Ducking");
            _snapIndoorsVoDucking = masterMixer.FindSnapshot("Indoors_VO_Ducking");

            _mixerHasAmbOutCutoff = masterMixer.GetFloat(ParamAmbOutCutoff, out _);
            _currentOutdoorCutoff = _playerIndoors ? outdoorCutoffIndoorsHz : outdoorCutoffOutdoorsHz;
            if (_mixerHasAmbOutCutoff)
            {
                masterMixer.SetFloat(ParamAmbOutCutoff, _currentOutdoorCutoff);
            }
        }

        private void RebindActiveSourceGroups()
        {
            if (_musicDeckA != null) _musicDeckA.outputAudioMixerGroup = _grpMusic;
            if (_musicDeckB != null) _musicDeckB.outputAudioMixerGroup = _grpMusic;
            if (_voSource != null) _voSource.outputAudioMixerGroup = _grpVo;
            if (_uiSource != null) _uiSource.outputAudioMixerGroup = _grpUi;
        }

        private AudioMixerGroup FindGroup(string groupName)
        {
            if (masterMixer == null) return null;
            AudioMixerGroup[] matches = masterMixer.FindMatchingGroups(groupName);
            if (matches == null || matches.Length == 0) return null;
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i] != null && string.Equals(matches[i].name, groupName, StringComparison.OrdinalIgnoreCase))
                    return matches[i];
            }
            return matches[0];
        }

        private AudioMixerGroup ResolveGroupForBus(AudioBusType bus)
        {
            return bus switch
            {
                AudioBusType.Music => _grpMusic,
                AudioBusType.Ambience => _grpAmbience,
                AudioBusType.AmbienceOutdoor => _grpAmbOut,
                AudioBusType.SfxFoley => _grpSfxFoley,
                AudioBusType.SfxDinos => _grpSfxDinos,
                AudioBusType.Voice => _grpVo,
                AudioBusType.Ui => _grpUi,
                _ => _grpSfxWorld
            };
        }

        private void CreateCoreSources()
        {
            _musicDeckA = CreateChildSource("MusicDeck_A", _grpMusic, spatial: false, loop: true);
            _musicDeckB = CreateChildSource("MusicDeck_B", _grpMusic, spatial: false, loop: true);
            _musicDeckA.ignoreListenerPause = true;
            _musicDeckB.ignoreListenerPause = true;

            _voSource = CreateChildSource("VoiceChannel", _grpVo, spatial: false, loop: false);
            _uiSource = CreateChildSource("UiChannel", _grpUi, spatial: false, loop: false);
            _uiSource.ignoreListenerPause = true;
        }

        private AudioSource CreateChildSource(string name, AudioMixerGroup group, bool spatial, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.outputAudioMixerGroup = group;
            src.spatialBlend = spatial ? 1f : 0f;
            src.loop = loop;
            src.playOnAwake = false;
            return src;
        }

        private void CreateSpatialPool()
        {
            var poolRoot = new GameObject("SpatialPool");
            poolRoot.transform.SetParent(transform, false);

            for (int i = 0; i < spatialPoolSize; i++)
            {
                var go = new GameObject($"Emitter_{i:00}");
                go.transform.SetParent(poolRoot.transform, false);
                AudioSource src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                src.dopplerLevel = 0.15f;
                AudioOcclusion3D occ = go.AddComponent<AudioOcclusion3D>();
                _spatialPool.Add(new PooledEmitter { Go = go, Source = src, Occlusion = occ });
            }
        }

        public void ApplySettingsVolumes()
        {
            SettingsData s = SaveManager.Settings;
            if (masterMixer != null)
            {
                SetMixerLinearVolume(s.masterVolume, ParamMasterVol, "MasterVolume");
                SetMixerLinearVolume(s.musicVolume * _currentDuckFactor, ParamMusVol, "MusicVol", "MusicVolume");
                SetMixerLinearVolume(s.sfxVolume, ParamSfxVol, "SFXVolume");
                SetMixerLinearVolume(s.voVolume, ParamVoVol, "VOVolume");
                SetMixerLinearVolume(s.sfxVolume * _currentDuckFactor, ParamAmbVol, "AmbienceVolume");
                SetMixerLinearVolume(s.sfxVolume, ParamUiVol, "UIVolume");
            }
            AudioListener.volume = masterMixer != null ? 1f : Mathf.Clamp01(s.masterVolume);
        }

        private void SetMixerLinearVolume(float linear01, params string[] candidateParams)
        {
            float db = linear01 <= 0.0005f ? -80f : Mathf.Clamp(20f * Mathf.Log10(linear01), -80f, 0f);
            for (int i = 0; i < candidateParams.Length; i++)
            {
                if (masterMixer.SetFloat(candidateParams[i], db))
                    return;
            }
        }

        private void ApplyDuckingToBuses()
        {
            if (masterMixer != null)
            {
                SettingsData s = SaveManager.Settings;
                SetMixerLinearVolume(s.musicVolume * _currentDuckFactor, "MusVol", "MusicVol", "MusicVolume");
                SetMixerLinearVolume(s.sfxVolume * _currentDuckFactor, "AmbVol", "AmbienceVolume");
            }
        }

        private void OnPauseChanged(bool paused)
        {
            if (_voSource != null)
            {
                if (paused) _voSource.Pause();
                else _voSource.UnPause();
            }

            for (int i = 0; i < _spatialPool.Count; i++)
            {
                AudioSource s = _spatialPool[i].Source;
                if (paused && s.isPlaying) s.Pause();
                else if (!paused) s.UnPause();
            }
        }

        private void OnInteriorChanged(bool isIndoors, string zoneId)
        {
            _playerIndoors = isIndoors;
        }

        private void OnPlaySoundEvent(string soundId, Vector3? worldPos, float volumeScale)
        {
            PlayOneShot(soundId, worldPos, volumeScale);
        }

        private void OnPlayLoopEvent(string soundId, Transform attachTarget, float fadeInSec)
        {
            StartLoop(soundId, attachTarget, fadeInSec);
        }

        private void OnStopLoopEvent(string soundId, float fadeOutSec)
        {
            StopLoop(soundId, fadeOutSec);
        }

        private void OnMusicStateEvent(string trackKey, float fadeSeconds)
        {
            SetMusicState(trackKey, fadeSeconds);
        }

        private void OnAmbienceZoneEvent(string zoneKey, bool indoors, float fadeSeconds)
        {
            _playerIndoors = indoors;
            SetAmbienceZone(zoneKey, fadeSeconds);
        }

        private void OnVoiceEvent(string voId, string speaker, string text, float fallbackDur, bool radioSquelch)
        {
            PlayVoiceClip(voId);
        }

        // =========================================================================
        // PUBLIC PLAYBACK API
        // =========================================================================

        public void PlayOneShot(string cueId, Vector3? worldPos = null, float volumeScale = 1f, bool force2D = false)
        {
            if (string.IsNullOrWhiteSpace(cueId) || soundBank == null) return;
            AudioClip clip = soundBank.ResolveClip(cueId, out SoundCueEntry entry);
            if (clip == null) return;

            float vol = (entry != null ? entry.volume : 0.85f) * Mathf.Clamp(volumeScale, 0f, 1.5f);
            float pitch = entry != null ? UnityEngine.Random.Range(entry.pitchMin, entry.pitchMax) : 1f;
            bool spatial = !force2D && worldPos.HasValue && (entry == null || entry.spatial3D);

            if (!spatial)
            {
                if (entry != null && entry.bus == AudioBusType.Ui && _uiSource != null)
                {
                    _uiSource.pitch = pitch;
                    _uiSource.PlayOneShot(clip, vol);
                    return;
                }

                PooledEmitter e2d = AcquireSpatialEmitter();
                e2d.IsLoop = false;
                e2d.FollowTarget = null;
                e2d.Source.outputAudioMixerGroup = ResolveGroupForBus(entry != null ? entry.bus : AudioBusType.SfxFoley);
                e2d.Source.spatialBlend = 0f;
                e2d.Source.loop = false;
                e2d.Source.pitch = pitch;
                e2d.Source.clip = clip;
                e2d.Occlusion.Configure(vol, enableOcclusion: false);
                e2d.Source.Play();
                return;
            }

            PooledEmitter emitter = AcquireSpatialEmitter();
            emitter.IsLoop = false;
            emitter.LoopId = null;
            emitter.FollowTarget = null;
            emitter.Go.transform.position = worldPos.Value;

            AudioSource src = emitter.Source;
            src.outputAudioMixerGroup = ResolveGroupForBus(entry != null ? entry.bus : AudioBusType.SfxWorld);
            src.spatialBlend = 1f;
            src.loop = false;
            src.pitch = pitch;
            src.minDistance = entry != null ? entry.minDistance : 2f;
            src.maxDistance = entry != null ? entry.maxDistance : 45f;
            src.clip = clip;
            emitter.Occlusion.Configure(vol, entry == null || entry.applyWallOcclusion);
            src.Play();
        }

        public void StartLoop(string cueId, Transform attachTarget = null, float fadeInSec = 0.5f)
        {
            if (string.IsNullOrWhiteSpace(cueId) || soundBank == null) return;
            if (_activeLoops.ContainsKey(cueId)) return;

            AudioClip clip = soundBank.ResolveClip(cueId, out SoundCueEntry entry);
            if (clip == null) return;

            PooledEmitter emitter = AcquireSpatialEmitter();
            emitter.IsLoop = true;
            emitter.LoopId = cueId;
            emitter.FollowTarget = attachTarget;
            if (attachTarget != null) emitter.Go.transform.position = attachTarget.position;

            AudioSource src = emitter.Source;
            src.outputAudioMixerGroup = ResolveGroupForBus(entry != null ? entry.bus : AudioBusType.SfxWorld);
            src.spatialBlend = attachTarget != null ? 1f : 0f;
            src.loop = true;
            src.pitch = 1f;
            src.minDistance = entry != null ? entry.minDistance : 4f;
            src.maxDistance = entry != null ? entry.maxDistance : 95f;
            src.clip = clip;

            float targetVol = entry != null ? entry.volume : 0.8f;
            emitter.Occlusion.Configure(0f, attachTarget != null && (entry == null || entry.applyWallOcclusion));
            src.Play();
            _activeLoops[cueId] = emitter;
            StartCoroutine(FadeLoopRoutine(emitter, 0f, targetVol, Mathf.Max(0.05f, fadeInSec), stopWhenDone: false));
        }

        public void StopLoop(string cueId, float fadeOutSec = 0.5f)
        {
            if (string.IsNullOrWhiteSpace(cueId)) return;
            if (!_activeLoops.TryGetValue(cueId, out PooledEmitter emitter)) return;
            _activeLoops.Remove(cueId);
            StartCoroutine(FadeLoopRoutine(emitter, emitter.Occlusion.BaseVolume, 0f, Mathf.Max(0.05f, fadeOutSec), stopWhenDone: true));
        }

        public void PlayVoiceClip(string voId)
        {
            if (string.IsNullOrWhiteSpace(voId) || _voSource == null || soundBank == null) return;
            AudioClip clip = soundBank.ResolveClip(voId, out SoundCueEntry entry);
            if (clip == null) return;

            _voSource.Stop();
            _voSource.clip = clip;
            _voSource.volume = entry != null ? entry.volume : 1.0f;
            _voSource.Play();
        }

        public void SetMusicState(string stateKey, float fadeSeconds = 1.5f)
        {
            string normalized = stateKey ?? string.Empty;
            if (string.Equals(_activeMusicState, normalized, StringComparison.OrdinalIgnoreCase)) return;
            _activeMusicState = normalized;

            if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);

            if (string.IsNullOrEmpty(normalized))
            {
                _musicFadeRoutine = StartCoroutine(CrossfadeMusicRoutine(null, 0f, Mathf.Max(0.1f, fadeSeconds), 0f));
                return;
            }

            _musicVariantToggle = 1 - _musicVariantToggle;
            string suffix = _musicVariantToggle == 0 ? "01" : "02";
            string primaryCue = $"mus_{normalized}_{suffix}";
            string fallbackCue = $"mus_{normalized}_{(_musicVariantToggle == 0 ? "02" : "01")}";

            if (soundBank == null) return;

            AudioClip clip = soundBank.ResolveClip(primaryCue, out SoundCueEntry entry);
            string chosenId = primaryCue;
            if (clip == null)
            {
                clip = soundBank.ResolveClip(fallbackCue, out entry);
                chosenId = fallbackCue;
            }

            float targetVol = entry != null ? entry.volume : 0.72f;
            // Skip mus_chase_01's 22.5 s quiet intro so chase intensity hits immediately!
            float startOffset = string.Equals(chosenId, "mus_chase_01", StringComparison.OrdinalIgnoreCase) && clip != null && clip.length > chase01IntroSkipSeconds
                ? chase01IntroSkipSeconds
                : 0f;

            _musicFadeRoutine = StartCoroutine(CrossfadeMusicRoutine(clip, targetVol, Mathf.Max(0.15f, fadeSeconds), startOffset));
        }

        public void SetAmbienceZone(string zoneKey, float fadeSeconds = 1.5f)
        {
            if (string.Equals(_currentAmbZone, zoneKey, StringComparison.OrdinalIgnoreCase)) return;
            _currentAmbZone = zoneKey ?? string.Empty;

            string[] desiredLayers = GetAmbienceLayersForZone(_currentAmbZone);
            StartCoroutine(TransitionAmbienceRoutine(desiredLayers, Mathf.Max(0.2f, fadeSeconds)));
        }

        private static string[] GetAmbienceLayersForZone(string zone)
        {
            return zone.ToLowerInvariant() switch
            {
                "menu" => new[] { "amb_rain_menu_loop", "amb_jungle_night_loop" },
                "day" => new[] { "amb_park_day_loop", "amb_jungle_day_loop" },
                "vc_night" => new[] { "amb_interior_hum_loop", "amb_rain_on_roof_loop", "amb_rain_exterior_loop" },
                "vc_dark" => new[] { "amb_rain_on_roof_loop", "amb_alarm_distant_loop", "amb_building_creak_loop", "amb_rain_exterior_loop" },
                "paths" => new[] { "amb_rain_exterior_loop", "amb_jungle_night_loop" },
                "paddock" => new[] { "amb_rain_exterior_loop", "amb_wind_open_loop", "amb_jungle_night_loop" },
                "yard" => new[] { "amb_rain_exterior_loop", "amb_metal_creak_loop", "amb_jungle_night_loop" },
                "dawn" => new[] { "amb_coast_dawn_loop", "amb_jungle_day_loop" },
                _ => Array.Empty<string>()
            };
        }

        private IEnumerator CrossfadeMusicRoutine(AudioClip nextClip, float nextVol, float duration, float startTime)
        {
            AudioSource fromDeck = _deckAActive ? _musicDeckA : _musicDeckB;
            AudioSource toDeck = _deckAActive ? _musicDeckB : _musicDeckA;
            _deckAActive = !_deckAActive;

            float fromStartVol = fromDeck.isPlaying ? fromDeck.volume : 0f;

            if (nextClip != null)
            {
                toDeck.Stop();
                toDeck.clip = nextClip;
                toDeck.time = startTime;
                toDeck.volume = 0f;
                toDeck.Play();
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Equal-power crossfade preserves perceived loudness through transition
                float fadeOut = Mathf.Cos(t * Mathf.PI * 0.5f);
                float fadeIn = Mathf.Sin(t * Mathf.PI * 0.5f);

                if (fromDeck.isPlaying) fromDeck.volume = fromStartVol * fadeOut;
                if (nextClip != null) toDeck.volume = nextVol * fadeIn;
                yield return null;
            }

            fromDeck.Stop();
            fromDeck.volume = 0f;
            if (nextClip != null) toDeck.volume = nextVol;
            _musicFadeRoutine = null;
        }

        private IEnumerator TransitionAmbienceRoutine(string[] desiredCues, float duration)
        {
            if (soundBank == null) yield break;
            var desiredSet = new HashSet<string>(desiredCues, StringComparer.OrdinalIgnoreCase);

            // Start any missing desired ambience layers
            for (int i = 0; i < desiredCues.Length; i++)
            {
                string cue = desiredCues[i];
                if (_activeAmbienceLayers.ContainsKey(cue)) continue;
                AudioClip clip = soundBank.ResolveClip(cue, out SoundCueEntry entry);
                if (clip == null) continue;

                bool outdoor = entry != null && entry.bus == AudioBusType.AmbienceOutdoor;
                AudioSource src = CreateChildSource($"Amb_{cue}", ResolveGroupForBus(outdoor ? AudioBusType.AmbienceOutdoor : AudioBusType.Ambience), spatial: false, loop: true);
                src.clip = clip;
                src.volume = 0f;
                // Only add per-source AudioLowPassFilter as fallback when mixer's AmbOut Lowpass Simple isn't bound
                if (outdoor && !_mixerHasAmbOutCutoff)
                {
                    AudioLowPassFilter lpf = src.gameObject.AddComponent<AudioLowPassFilter>();
                    lpf.cutoffFrequency = _currentOutdoorCutoff;
                    _outdoorAmbFilters.Add(lpf);
                }
                src.Play();
                _activeAmbienceLayers[cue] = src;
            }

            var fadingOut = new List<string>();
            foreach (string existing in _activeAmbienceLayers.Keys)
            {
                if (!desiredSet.Contains(existing)) fadingOut.Add(existing);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);

                foreach (string cue in desiredSet)
                {
                    if (_activeAmbienceLayers.TryGetValue(cue, out AudioSource s) && s != null)
                    {
                        float target = soundBank.TryGetEntry(cue, out SoundCueEntry e) ? e.volume : 0.65f;
                        s.volume = Mathf.MoveTowards(s.volume, target, (target / duration) * Time.unscaledDeltaTime * 1.4f);
                    }
                }

                for (int i = 0; i < fadingOut.Count; i++)
                {
                    if (_activeAmbienceLayers.TryGetValue(fadingOut[i], out AudioSource s) && s != null)
                    {
                        s.volume = Mathf.Lerp(s.volume, 0f, k);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < fadingOut.Count; i++)
            {
                string key = fadingOut[i];
                if (_activeAmbienceLayers.TryGetValue(key, out AudioSource s) && s != null)
                {
                    AudioLowPassFilter lpf = s.GetComponent<AudioLowPassFilter>();
                    if (lpf != null) _outdoorAmbFilters.Remove(lpf);
                    Destroy(s.gameObject);
                }
                _activeAmbienceLayers.Remove(key);
            }
        }

        private IEnumerator FadeLoopRoutine(PooledEmitter emitter, float fromVol, float toVol, float duration, bool stopWhenDone)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float v = Mathf.Lerp(fromVol, toVol, Mathf.Clamp01(elapsed / duration));
                emitter.Occlusion.BaseVolume = v;
                yield return null;
            }

            emitter.Occlusion.BaseVolume = toVol;
            if (stopWhenDone)
            {
                emitter.Source.Stop();
                emitter.IsLoop = false;
                emitter.LoopId = null;
                emitter.FollowTarget = null;
            }
        }

        private PooledEmitter AcquireSpatialEmitter()
        {
            for (int i = 0; i < _spatialPool.Count; i++)
            {
                PooledEmitter e = _spatialPool[i];
                if (!e.IsLoop && !e.Source.isPlaying) return e;
            }
            // Fallback: recycle first non-looping emitter
            for (int i = 0; i < _spatialPool.Count; i++)
            {
                if (!_spatialPool[i].IsLoop) return _spatialPool[i];
            }
            return _spatialPool[0];
        }
    }
}
