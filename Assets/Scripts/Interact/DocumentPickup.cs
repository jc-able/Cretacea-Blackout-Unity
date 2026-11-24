using System;
using System.Collections.Generic;
using UnityEngine;
using Cretacea.Core;

namespace Cretacea.Interact
{
    /// <summary>
    /// Handles all 5/5 collectible story documents:
    /// 1. memo_switchover (Security Desk)
    /// 2. manual (Records Annex — 2-page Evacuation Procedures 1996 + grants Master Key)
    /// 3. keeper_note (Feed Prep Hall counter)
    /// 4. jeep_radio (Overturned Ranger Jeep audio log — FIXED to register AddDocument("jeep_radio")!)
    /// 5. log_halvorsen (Old Yard Foreman's Office desk)
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DocumentPickup : MonoBehaviour, IInteractable, IDocumentSource
    {
        [Header("Document Identity")]
        [SerializeField] private string documentId = "memo_switchover";
        [SerializeField] private string displayTitle = "Night Shift Switchover Memo";
        [SerializeField] private string promptText = "Read shift memo {interact}";
        [SerializeField] private bool isAudioLog = false;
        [SerializeField] private bool oneShot = false;
        [SerializeField] private Transform focusAnchor;

        [Header("Pages (single or multi-page binder)")]
        [SerializeField] private string[] pageAssetKeys = { "memo_switchover" };
        [TextArea(2, 4)]
        [SerializeField] private string[] pageSummaries = { "Grid switchover scheduled for 00:00 — Substation 1 over to Substation 5." };

        [Header("Optional Audio Log Settings (jeep_radio)")]
        [SerializeField] private string audioLogIntroSound = "radio_ranger_log";

        private bool _consumed;
        private bool _isShowing;

        public string InteractId => $"doc_{documentId}";
        public string DocumentId => documentId;
        public string DisplayTitle => displayTitle;
        public IReadOnlyList<string> PageAssetKeys => pageAssetKeys;
        public IReadOnlyList<string> PageSummaries => pageSummaries;
        public bool IsAudioLog => isAudioLog;
        public bool IsCollected => GameState.Instance != null && GameState.Instance.HasDocument(documentId);

        public string PromptText => promptText;
        public string LockedHint => string.Empty;
        public bool CanInteract => !_consumed && !_isShowing;
        public bool IsEnabled => !_consumed && gameObject.activeInHierarchy;
        public bool IsLocked => false;
        public float HoldDuration => 0f;
        public bool PreserveHoldProgress => false;
        public float SavedHoldProgress => 0f;
        public Transform FocusAnchor => focusAnchor != null ? focusAnchor : transform;

        public event Action<DocumentPickup> OnReadingCompleted;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Interactable;
        }

        private void OnEnable()
        {
            WorldRegistry.EnsureInstance().RegisterInteractable(InteractId, this);
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance != null)
                WorldRegistry.Instance.UnregisterInteractable(InteractId, this);
        }

        public void Configure(
            string docId,
            string title,
            string prompt,
            string[] assetKeys,
            string[] summaries,
            bool audioLog = false,
            bool singleUse = false)
        {
            documentId = docId;
            displayTitle = title;
            promptText = prompt;
            pageAssetKeys = assetKeys ?? Array.Empty<string>();
            pageSummaries = summaries ?? Array.Empty<string>();
            isAudioLog = audioLog;
            oneShot = singleUse;
            _consumed = false;
        }

        public void OnInteract(GameObject interactor)
        {
            if (_consumed || _isShowing) return;

            // Always register the document cleanly (fixes Godot's missing jeep_radio 4/5 cap!)
            GameState gs = GameState.EnsureInstance();
            bool firstPickup = gs.AddDocument(documentId);

            if (isAudioLog)
            {
                if (!string.IsNullOrEmpty(audioLogIntroSound))
                {
                    GameEvents.RaisePlaySound(audioLogIntroSound, FocusAnchor.position);
                }
                if (oneShot) _consumed = true;
                if (firstPickup)
                {
                    GameEvents.RaiseToastMessage($"Audio log recorded ({gs.CollectedDocumentCount}/{GameState.TotalDocumentCount})", 3.0f);
                }
                OnReadingCompleted?.Invoke(this);
                return;
            }

            if (pageAssetKeys == null || pageAssetKeys.Length == 0)
            {
                OnReadingCompleted?.Invoke(this);
                return;
            }

            GameEvents.RaisePlaySound("foley_paper_rustle", FocusAnchor.position, 0.85f);
            _isShowing = true;
            gs.SetDocumentOpen(true);
            ShowPageRecursive(0);
        }

        private void ShowPageRecursive(int pageIndex)
        {
            if (pageIndex >= pageAssetKeys.Length)
            {
                _isShowing = false;
                GameState.EnsureInstance().SetDocumentOpen(false);
                if (oneShot) _consumed = true;
                OnReadingCompleted?.Invoke(this);
                return;
            }

            string assetKey = pageAssetKeys[pageIndex];
            string summary = (pageSummaries != null && pageIndex < pageSummaries.Length)
                ? pageSummaries[pageIndex]
                : displayTitle;

            GameEvents.RaiseShowDocument(documentId, assetKey, summary, () => ShowPageRecursive(pageIndex + 1));
        }

        public void OnHoldProgress(GameObject interactor, float normalized01) { }
        public void OnLockedInteract(GameObject interactor) { }
    }
}
