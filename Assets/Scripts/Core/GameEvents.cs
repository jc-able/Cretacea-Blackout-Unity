using System;
using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Snapshot of currently focused interactable broadcast to HUD reticle & prompt widgets.
    /// </summary>
    public readonly struct InteractionFocusInfo
    {
        public readonly bool HasTarget;
        public readonly string InteractId;
        public readonly string PromptText;
        public readonly string LockedHint;
        public readonly bool IsLocked;
        public readonly bool CanInteract;
        public readonly float HoldDuration;
        public readonly float HoldProgress01;
        public readonly bool PreserveHoldProgress;

        public InteractionFocusInfo(
            bool hasTarget,
            string interactId,
            string promptText,
            string lockedHint,
            bool isLocked,
            bool canInteract,
            float holdDuration,
            float holdProgress01,
            bool preserveHoldProgress)
        {
            HasTarget = hasTarget;
            InteractId = interactId;
            PromptText = promptText;
            LockedHint = lockedHint;
            IsLocked = isLocked;
            CanInteract = canInteract;
            HoldDuration = holdDuration;
            HoldProgress01 = holdProgress01;
            PreserveHoldProgress = preserveHoldProgress;
        }

        public static InteractionFocusInfo Empty => new InteractionFocusInfo(false, null, null, null, false, false, 0f, 0f, false);
    }

    public readonly struct SubstationChecklistSnapshot
    {
        public readonly int BreakersReset;
        public readonly int BreakersTotal;
        public readonly bool FuelPrimed;
        public readonly int GeneratorAttempts;
        public readonly bool GeneratorRunning;
        public readonly bool TransferComplete;

        public SubstationChecklistSnapshot(
            int breakersReset,
            int breakersTotal,
            bool fuelPrimed,
            int generatorAttempts,
            bool generatorRunning,
            bool transferComplete)
        {
            BreakersReset = breakersReset;
            BreakersTotal = breakersTotal;
            FuelPrimed = fuelPrimed;
            GeneratorAttempts = generatorAttempts;
            GeneratorRunning = generatorRunning;
            TransferComplete = transferComplete;
        }
    }

    /// <summary>
    /// Strongly-typed central event bus decoupling Core, Player, Interact, Story, AI, UI, and Audio.
    /// </summary>
    public static class GameEvents
    {
        // Progression, Objectives & Checkpoints
        public static event Action<CheckpointId> OnCheckpointReached;
        public static event Action<string> OnObjectiveChanged;
        public static event Action<string, string, float> OnTitleCardRequested; // title, subtitle, duration
        public static event Action<float, float> OnScreenFadeRequested;         // targetAlpha, duration
        public static event Action<bool, float> OnLetterboxRequested;           // enabled, duration
        public static event Action<bool> OnCutsceneStateChanged;                // inCutscene
        public static event Action<bool> OnPauseStateChanged;                   // isPaused
        public static event Action<bool, Action> OnCutsceneSkipPromptChanged;   // visible, onSkipCallback
        public static event Action<string> OnGameFinished;                      // formatted stats summary
        public static event Action OnPlayerDied;

        // Player, Flashlight & Stealth
        public static event Action<bool> OnFlashlightAcquiredChanged;           // hasFlashlight
        public static event Action<bool> OnFlashlightToggled;                   // beamOn
        public static event Action OnFlashlightPickedUp;                        // world pickup completed
        public static event Action<float, bool> OnStealthVisibilityChanged;     // visibility (0..2+), isConcealed
        public static event Action<float, bool> OnStaminaChanged;               // stamina01, exhausted
        public static event Action<bool> OnCrouchChanged;                       // isCrouching
        public static event Action<Vector3, float, string> OnNoiseEmitted;      // worldPos, radiusMeters, category
        public static event Action<SurfaceType, Vector3, bool, bool> OnPlayerFootstep; // surface, pos, sprinting, crouching
        public static event Action<InteractionFocusInfo> OnInteractionFocusChanged;
        public static event Action<InputDeviceType> OnInputDeviceChanged;

        // Documents & Inventory (5/5 tracking)
        public static event Action<string, int, int> OnDocumentCollected;       // docId, count, total (5)
        public static event Action<string, string, string, Action> OnShowDocumentRequested; // docId, assetKey, summary, onClosed
        public static event Action<bool> OnMasterKeyChanged;                    // hasKey

        // Doors, Substation & World State
        public static event Action<string, bool> OnDoorStateChanged;            // doorId, isOpen
        public static event Action<string> OnDoorBroken;                        // doorId
        public static event Action<SubstationChecklistSnapshot> OnSubstationProgressChanged;
        public static event Action<float> OnE0GateProgressChanged;              // progress01
        public static event Action<LightGroupId, bool, float> OnLightGroupChanged; // id, enabled, fadeSeconds
        public static event Action<AtmospherePreset, float> OnAtmosphereChanged;   // preset, blendSeconds
        public static event Action<float, bool> OnLightningFlash;               // intensity01, withThunder
        public static event Action<bool, string> OnInteriorChanged;             // isIndoors, zoneId
        public static event Action<string> OnStoryTriggerEntered;               // triggerId

        // Camera & Feedback
        public static event Action<float, float> OnCameraTraumaRequested;       // trauma01, decayRate
        public static event Action<float> OnDamageFlashRequested;               // intensity01

        // Subtitles, Toasts & Accessibility Captions
        public static event Action<string, string, float> OnSubtitleRequested;  // speaker, text, duration
        public static event Action<string, float> OnToastMessageRequested;      // text, duration
        public static event Action<string, float> OnSoundCaptionRequested;      // caption, duration

        // Dinosaurs
        public static event Action<string, string, DinoState> OnDinoStateChanged; // dinoId, species, state
        public static event Action<Vector3, float, Quaternion> OnTRexFootprint;   // pos, strength, rot

        // Audio Requests (consumed by Cretacea.Audio)
        public static event Action<string, Vector3?, float> OnPlaySoundRequested;     // soundId, optionalWorldPos, volumeScale
        public static event Action<string, Transform, float> OnPlayLoopRequested;     // soundId, attachTarget, fadeInSec
        public static event Action<string, float> OnStopLoopRequested;                // soundId, fadeOutSec
        public static event Action<string, float> OnMusicStateRequested;              // trackKey, fadeSeconds
        public static event Action<string, bool, float> OnAmbienceZoneRequested;      // zoneKey, indoors, fadeSeconds
        public static event Action<string, string, string, float, bool> OnVoiceRequested; // voId, speaker, text, fallbackDur, radioSquelch

        // Save / Load
        public static event Action OnSaveCompleted;
        public static event Action OnSettingsChanged;

        // ---- Invocation Helpers ----
        public static void RaiseCheckpointReached(CheckpointId id) => OnCheckpointReached?.Invoke(id);
        public static void RaiseObjectiveChanged(string objective) => OnObjectiveChanged?.Invoke(objective ?? string.Empty);
        public static void RaiseTitleCard(string title, string subtitle, float duration) => OnTitleCardRequested?.Invoke(title, subtitle, duration);
        public static void RaiseScreenFade(float targetAlpha, float duration) => OnScreenFadeRequested?.Invoke(targetAlpha, duration);
        public static void RaiseLetterbox(bool enabled, float duration) => OnLetterboxRequested?.Invoke(enabled, duration);
        public static void RaiseCutsceneStateChanged(bool inCutscene) => OnCutsceneStateChanged?.Invoke(inCutscene);
        public static void RaisePauseStateChanged(bool isPaused) => OnPauseStateChanged?.Invoke(isPaused);
        public static void RaiseCutsceneSkipPrompt(bool visible, Action onSkip = null) => OnCutsceneSkipPromptChanged?.Invoke(visible, onSkip);
        public static void RaiseGameFinished(string statsSummary) => OnGameFinished?.Invoke(statsSummary);
        public static void RaisePlayerDied() => OnPlayerDied?.Invoke();

        public static void RaiseFlashlightAcquiredChanged(bool hasFlashlight) => OnFlashlightAcquiredChanged?.Invoke(hasFlashlight);
        public static void RaiseFlashlightToggled(bool beamOn) => OnFlashlightToggled?.Invoke(beamOn);
        public static void RaiseFlashlightPickedUp() => OnFlashlightPickedUp?.Invoke();
        public static void RaiseStealthVisibilityChanged(float visibility, bool concealed) => OnStealthVisibilityChanged?.Invoke(visibility, concealed);
        public static void RaiseStaminaChanged(float stamina01, bool exhausted) => OnStaminaChanged?.Invoke(stamina01, exhausted);
        public static void RaiseCrouchChanged(bool crouching) => OnCrouchChanged?.Invoke(crouching);
        public static void RaiseNoiseEmitted(Vector3 pos, float radius, string category) => OnNoiseEmitted?.Invoke(pos, radius, category);
        public static void RaisePlayerFootstep(SurfaceType surface, Vector3 pos, bool sprinting, bool crouching) => OnPlayerFootstep?.Invoke(surface, pos, sprinting, crouching);
        public static void RaiseInteractionFocusChanged(InteractionFocusInfo info) => OnInteractionFocusChanged?.Invoke(info);
        public static void RaiseInputDeviceChanged(InputDeviceType device) => OnInputDeviceChanged?.Invoke(device);

        public static void RaiseDocumentCollected(string docId, int count, int total) => OnDocumentCollected?.Invoke(docId, count, total);
        public static void RaiseShowDocument(string docId, string assetKey, string summary, Action onClosed = null) => OnShowDocumentRequested?.Invoke(docId, assetKey, summary, onClosed);
        public static void RaiseMasterKeyChanged(bool hasKey) => OnMasterKeyChanged?.Invoke(hasKey);

        public static void RaiseDoorStateChanged(string doorId, bool isOpen) => OnDoorStateChanged?.Invoke(doorId, isOpen);
        public static void RaiseDoorBroken(string doorId) => OnDoorBroken?.Invoke(doorId);
        public static void RaiseSubstationProgressChanged(SubstationChecklistSnapshot snapshot) => OnSubstationProgressChanged?.Invoke(snapshot);
        public static void RaiseE0GateProgressChanged(float progress01) => OnE0GateProgressChanged?.Invoke(progress01);
        public static void RaiseLightGroupChanged(LightGroupId id, bool enabled, float fadeSeconds = 0f) => OnLightGroupChanged?.Invoke(id, enabled, fadeSeconds);
        public static void RaiseAtmosphereChanged(AtmospherePreset preset, float blendSeconds = 0f) => OnAtmosphereChanged?.Invoke(preset, blendSeconds);
        public static void RaiseLightningFlash(float intensity01, bool withThunder = true) => OnLightningFlash?.Invoke(intensity01, withThunder);
        public static void RaiseInteriorChanged(bool isIndoors, string zoneId) => OnInteriorChanged?.Invoke(isIndoors, zoneId);
        public static void RaiseStoryTriggerEntered(string triggerId) => OnStoryTriggerEntered?.Invoke(triggerId);

        public static void RaiseCameraTrauma(float trauma01, float decayRate = 1.6f) => OnCameraTraumaRequested?.Invoke(trauma01, decayRate);
        public static void RaiseDamageFlash(float intensity01) => OnDamageFlashRequested?.Invoke(intensity01);

        public static void RaiseSubtitle(string speaker, string text, float duration) => OnSubtitleRequested?.Invoke(speaker, text, duration);
        public static void RaiseToastMessage(string text, float duration = 3.0f) => OnToastMessageRequested?.Invoke(text, duration);
        public static void RaiseSoundCaption(string caption, float duration = 2.5f) => OnSoundCaptionRequested?.Invoke(caption, duration);

        public static void RaiseDinoStateChanged(string dinoId, string species, DinoState state) => OnDinoStateChanged?.Invoke(dinoId, species, state);
        public static void RaiseTRexFootprint(Vector3 pos, float strength, Quaternion rot) => OnTRexFootprint?.Invoke(pos, strength, rot);

        public static void RaisePlaySound(string soundId, Vector3? worldPos = null, float volumeScale = 1f) => OnPlaySoundRequested?.Invoke(soundId, worldPos, volumeScale);
        public static void RaisePlayLoop(string soundId, Transform attachTarget = null, float fadeInSec = 0.5f) => OnPlayLoopRequested?.Invoke(soundId, attachTarget, fadeInSec);
        public static void RaiseStopLoop(string soundId, float fadeOutSec = 0.5f) => OnStopLoopRequested?.Invoke(soundId, fadeOutSec);
        public static void RaiseMusicState(string trackKey, float fadeSeconds = 1.5f) => OnMusicStateRequested?.Invoke(trackKey, fadeSeconds);
        public static void RaiseAmbienceZone(string zoneKey, bool indoors = false, float fadeSeconds = 1.5f) => OnAmbienceZoneRequested?.Invoke(zoneKey, indoors, fadeSeconds);
        public static void RaiseVoice(string voId, string speaker, string text, float fallbackDuration, bool radioSquelch = false) => OnVoiceRequested?.Invoke(voId, speaker, text, fallbackDuration, radioSquelch);

        public static void RaiseSaveCompleted() => OnSaveCompleted?.Invoke();
        public static void RaiseSettingsChanged() => OnSettingsChanged?.Invoke();

        /// <summary>
        /// Clears all static delegates (call only on domain teardown or full test harness reset).
        /// </summary>
        public static void ClearAllListeners()
        {
            OnCheckpointReached = null;
            OnObjectiveChanged = null;
            OnTitleCardRequested = null;
            OnScreenFadeRequested = null;
            OnLetterboxRequested = null;
            OnCutsceneStateChanged = null;
            OnPauseStateChanged = null;
            OnCutsceneSkipPromptChanged = null;
            OnGameFinished = null;
            OnPlayerDied = null;
            OnFlashlightAcquiredChanged = null;
            OnFlashlightToggled = null;
            OnFlashlightPickedUp = null;
            OnStealthVisibilityChanged = null;
            OnStaminaChanged = null;
            OnCrouchChanged = null;
            OnNoiseEmitted = null;
            OnPlayerFootstep = null;
            OnInteractionFocusChanged = null;
            OnInputDeviceChanged = null;
            OnDocumentCollected = null;
            OnShowDocumentRequested = null;
            OnMasterKeyChanged = null;
            OnDoorStateChanged = null;
            OnDoorBroken = null;
            OnSubstationProgressChanged = null;
            OnE0GateProgressChanged = null;
            OnLightGroupChanged = null;
            OnAtmosphereChanged = null;
            OnLightningFlash = null;
            OnInteriorChanged = null;
            OnStoryTriggerEntered = null;
            OnCameraTraumaRequested = null;
            OnDamageFlashRequested = null;
            OnSubtitleRequested = null;
            OnToastMessageRequested = null;
            OnSoundCaptionRequested = null;
            OnDinoStateChanged = null;
            OnTRexFootprint = null;
            OnPlaySoundRequested = null;
            OnPlayLoopRequested = null;
            OnStopLoopRequested = null;
            OnMusicStateRequested = null;
            OnAmbienceZoneRequested = null;
            OnVoiceRequested = null;
            OnSaveCompleted = null;
            OnSettingsChanged = null;
        }
    }
}
