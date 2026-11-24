using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cretacea.Core
{
    /// <summary>
    /// Universal interaction contract for doors, pickups, Substation stations, terminals, and gates.
    /// Supports both active prompts AND prerequisite LockedHints so stations never feel dead or broken.
    /// </summary>
    public interface IInteractable
    {
        string InteractId { get; }
        string PromptText { get; }
        string LockedHint { get; }
        bool CanInteract { get; }
        bool IsEnabled { get; }
        bool IsLocked { get; }
        float HoldDuration { get; }
        bool PreserveHoldProgress { get; }
        float SavedHoldProgress { get; }
        Transform FocusAnchor { get; }

        void OnInteract(GameObject interactor);
        void OnHoldProgress(GameObject interactor, float normalized01);
        void OnLockedInteract(GameObject interactor);
    }

    /// <summary>
    /// Metadata contract for all 5 collectible documents (including jeep_radio audio log).
    /// </summary>
    public interface IDocumentSource
    {
        string DocumentId { get; }
        string DisplayTitle { get; }
        IReadOnlyList<string> PageAssetKeys { get; }
        IReadOnlyList<string> PageSummaries { get; }
        bool IsAudioLog { get; }
        bool IsCollected { get; }
    }

    /// <summary>
    /// Floor, ramp, grating, or water surface descriptor for accurate footstep audio & noise scaling.
    /// </summary>
    public interface ISurfaceProvider
    {
        SurfaceType Surface { get; }
        float NoiseMultiplier { get; }
    }

    /// <summary>
    /// Door & gate control interface used by StoryDirector and DinosaurAI without assembly cycles.
    /// </summary>
    public interface IDoor
    {
        string DoorId { get; }
        bool IsOpen { get; }
        bool IsLocked { get; }
        bool IsBroken { get; }
        float OpenProgress { get; }
        Transform Transform { get; }

        void SetLocked(bool locked, string lockedHint = null);
        void Open(GameObject opener = null, bool instant = false);
        void Close(bool instant = false);
        void SetNormalizedOpen(float normalized01);
        bool BashDoor(Vector3 impactWorldPos);
    }

    /// <summary>
    /// CCTV RenderTexture & blackout static controller interface.
    /// </summary>
    public interface ICctvController
    {
        string ControllerId { get; }
        bool IsStatic { get; }
        void SetStatic(bool staticEnabled);
        void SetActive(bool active);
    }

    /// <summary>
    /// Clean bridge allowing StoryDirector to direct Velociraptor & Tyrannosaurus instances
    /// implemented in Cretacea.AI with zero circular assembly dependencies.
    /// </summary>
    public interface IDinoController
    {
        string DinoId { get; }
        string Species { get; }
        DinoState CurrentState { get; }
        bool AiEnabled { get; set; }
        float Aggression { get; set; }
        float Awareness { get; set; }
        float ChaseRubberBand { get; set; }
        Vector3 LastSeenPosition { get; set; }
        Vector3 LastNoisePosition { get; set; }
        float UnseenTimer { get; set; }
        Vector3? LeashCenter { get; set; }
        Transform Transform { get; }

        void SetPatrolWaypoints(IReadOnlyList<Vector3> waypoints, int startIndex = 0);
        void ForceState(DinoState state);
        void HuntPlayer();
        void ScriptMoveTo(Vector3 targetWorldPos, float moveSpeed, Action onArrived = null);
        void ScriptMoveDirect(Vector3 targetWorldPos, float moveSpeed, Action onArrived = null);
        void ScriptHoldAndLookAt(Vector3 worldLookTarget);
        void PlayActionClip(string clipTrigger, float durationSec = 1.4f);
        void Teleport(Vector3 worldPos, float yawDegrees);
        void ReleaseToAI(DinoState resumeState = DinoState.Patrol);
        void Despawn();
    }

    /// <summary>
    /// Spawner bridge implemented by Cretacea.AI and registered in WorldRegistry.
    /// </summary>
    public interface IDinoSpawner
    {
        IDinoController Spawn(string species, Vector3 position, float yawDegrees, string customId = null);
        IReadOnlyList<IDinoController> GetActiveDinos(string speciesFilter = null);
        void DespawnAll(string speciesFilter = null);
    }
}
