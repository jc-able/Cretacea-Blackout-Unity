using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;

namespace Cretacea.Interact
{
    /// <summary>
    /// Contextual Substation 3 Station controller covering all 6 compound interactables:
    /// Breaker1, Breaker2 (rotated inward!), Breaker3, FuelPump (4 s hold), GeneratorStarter (3 pulls), TransferLever (2.2 s hold).
    ///
    /// Fixes TWO major Godot UX bugs:
    /// 1. UX-4: Every station is ALWAYS focusable from arrival and displays a helpful LockedHint explaining
    ///    which prerequisite step is needed first (never feels broken or non-interactive).
    /// 2. Sticky Starter Prompt Fix: GeneratorStarter disables cleanly the instant pull #3 starts the diesel engine!
    /// Drives diegetic LED lamps + power conduit emissives from Blinking Red -> Pulsing Amber (active) -> Solid Green (done).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SubstationStation : MonoBehaviour, IInteractable
    {
        [Header("Station Role")]
        [SerializeField] private SubstationStationType stationType = SubstationStationType.Breaker1;
        [SerializeField] private Transform focusAnchor;

        [Header("Animated Hardware Pivot")]
        [Tooltip("Lever arm, breaker handle, or pump crank animated during hold/activation.")]
        [SerializeField] private Transform animatedPivot;
        [SerializeField] private Vector3 restLocalEuler = new Vector3(35f, 0f, 0f);
        [SerializeField] private Vector3 activeLocalEuler = new Vector3(-55f, 0f, 0f);

        [Header("Diegetic Status Indicators & Conduits")]
        [SerializeField] private Light statusLight;
        [SerializeField] private Renderer[] statusLedRenderers;
        [SerializeField] private Renderer[] powerConduitRenderers;
        [SerializeField] private Color faultRedColor = new Color(1.0f, 0.16f, 0.12f);
        [SerializeField] private Color readyAmberColor = new Color(1.0f, 0.68f, 0.14f);
        [SerializeField] private Color poweredGreenColor = new Color(0.24f, 1.0f, 0.36f);

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        private MaterialPropertyBlock _mpb;
        private HDAdditionalLightData _hdStatusLight;
        private float _lastPumpStrokeProgress;
        private bool _completed;

        public SubstationStationType StationType => stationType;
        public string InteractId => $"substation_{stationType}";
        public Transform FocusAnchor => focusAnchor != null ? focusAnchor : transform;
        public bool PreserveHoldProgress => false;
        public float SavedHoldProgress => 0f;

        public event Action<SubstationStation> OnStationActivated;
        public event Action<SubstationStation, int> OnGeneratorAttempted;

        public bool IsEnabled => !_completed && gameObject.activeInHierarchy;

        public bool IsLocked => !ArePrerequisitesMet();
        public bool CanInteract => IsEnabled && ArePrerequisitesMet();

        public float HoldDuration
        {
            get
            {
                return stationType switch
                {
                    SubstationStationType.Breaker1 => 1.2f,
                    SubstationStationType.Breaker2 => 1.2f,
                    SubstationStationType.Breaker3 => 1.2f,
                    SubstationStationType.FuelPump => 4.0f,
                    SubstationStationType.GeneratorStarter => 0.0f,
                    SubstationStationType.TransferLever => 2.2f,
                    _ => 0f
                };
            }
        }

        public string PromptText
        {
            get
            {
                return stationType switch
                {
                    SubstationStationType.Breaker1 => "Hold {interact} Reset Circuit Breaker #1",
                    SubstationStationType.Breaker2 => "Hold {interact} Reset Circuit Breaker #2",
                    SubstationStationType.Breaker3 => "Hold {interact} Reset Circuit Breaker #3",
                    SubstationStationType.FuelPump => "Hold {interact} Prime Diesel Fuel Pump",
                    SubstationStationType.GeneratorStarter => "Pull Diesel Generator Starter {interact}",
                    SubstationStationType.TransferLever => "Hold {interact} Throw TRANSFER Lever to Circuit B",
                    _ => "Activate {interact}"
                };
            }
        }

        public string LockedHint
        {
            get
            {
                GameState gs = GameState.Instance;
                int breakers = gs != null ? gs.BreakersResetCount : 0;

                return stationType switch
                {
                    SubstationStationType.FuelPump =>
                        $"No control power — reset all 3 tripped breakers first ({breakers}/3).",
                    SubstationStationType.GeneratorStarter =>
                        breakers < 3
                            ? $"Starter panel dead — reset all 3 tripped breakers first ({breakers}/3)."
                            : "Fuel line dry — prime the manual diesel fuel pump first.",
                    SubstationStationType.TransferLever =>
                        breakers < 3
                            ? $"No auxiliary voltage — reset all 3 breakers first ({breakers}/3)."
                            : (gs != null && !gs.FuelPrimed)
                                ? "Generator offline — prime the fuel pump and start the diesel generator first."
                                : "Generator offline — start the diesel generator before throwing TRANSFER.",
                    _ => string.Empty
                };
            }
        }

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Interactable;
            _mpb = new MaterialPropertyBlock();
            if (statusLight != null) _hdStatusLight = statusLight.GetComponent<HDAdditionalLightData>();
            if (animatedPivot != null) animatedPivot.localRotation = Quaternion.Euler(restLocalEuler);
        }

        private void OnEnable()
        {
            WorldRegistry.EnsureInstance().RegisterInteractable(InteractId, this);
            SyncFromGameState();
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance != null)
                WorldRegistry.Instance.UnregisterInteractable(InteractId, this);
        }

        private void Update()
        {
            UpdateDiegeticIndicators();
        }

        public void Configure(SubstationStationType type)
        {
            stationType = type;
            SyncFromGameState();
        }

        public void SyncFromGameState()
        {
            GameState gs = GameState.Instance;
            if (gs == null)
            {
                _completed = false;
                return;
            }

            _completed = stationType switch
            {
                SubstationStationType.Breaker1 => gs.IsBreakerReset(0),
                SubstationStationType.Breaker2 => gs.IsBreakerReset(1),
                SubstationStationType.Breaker3 => gs.IsBreakerReset(2),
                SubstationStationType.FuelPump => gs.FuelPrimed,
                SubstationStationType.GeneratorStarter => gs.GeneratorRunning,
                SubstationStationType.TransferLever => gs.TransferComplete,
                _ => false
            };

            if (animatedPivot != null)
            {
                animatedPivot.localRotation = Quaternion.Euler(_completed ? activeLocalEuler : restLocalEuler);
            }
            UpdateDiegeticIndicators();
        }

        private bool ArePrerequisitesMet()
        {
            GameState gs = GameState.Instance;
            if (gs == null) return stationType <= SubstationStationType.Breaker3;

            return stationType switch
            {
                SubstationStationType.Breaker1 => !gs.IsBreakerReset(0),
                SubstationStationType.Breaker2 => !gs.IsBreakerReset(1),
                SubstationStationType.Breaker3 => !gs.IsBreakerReset(2),
                SubstationStationType.FuelPump => gs.AllBreakersReset && !gs.FuelPrimed,
                SubstationStationType.GeneratorStarter => gs.AllBreakersReset && gs.FuelPrimed && !gs.GeneratorRunning,
                SubstationStationType.TransferLever => gs.GeneratorRunning && !gs.TransferComplete,
                _ => false
            };
        }

        public void OnHoldProgress(GameObject interactor, float normalized01)
        {
            if (_completed || !ArePrerequisitesMet()) return;

            if (animatedPivot != null)
            {
                if (stationType == SubstationStationType.FuelPump)
                {
                    // Oscillate manual hand pump lever back and forth smoothly during prime hold
                    float pumpAngle = Mathf.Sin(normalized01 * Mathf.PI * 10f);
                    animatedPivot.localRotation = Quaternion.Euler(Vector3.Lerp(restLocalEuler, activeLocalEuler, 0.5f + 0.5f * pumpAngle));
                }
                else
                {
                    animatedPivot.localRotation = Quaternion.Euler(Vector3.Lerp(restLocalEuler, activeLocalEuler, normalized01));
                }
            }

            if (stationType == SubstationStationType.FuelPump && normalized01 - _lastPumpStrokeProgress >= 0.18f)
            {
                _lastPumpStrokeProgress = normalized01;
                GameEvents.RaisePlaySound("fuel_pump_stroke", FocusAnchor.position);
                GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 30f, "machine");
            }
        }

        public void OnInteract(GameObject interactor)
        {
            if (_completed) return;
            if (!ArePrerequisitesMet())
            {
                OnLockedInteract(interactor);
                return;
            }

            GameState gs = GameState.EnsureInstance();

            switch (stationType)
            {
                case SubstationStationType.Breaker1:
                case SubstationStationType.Breaker2:
                case SubstationStationType.Breaker3:
                    int idx = (int)stationType;
                    gs.MarkBreakerReset(idx);
                    _completed = true;
                    SetPivotCompleted();
                    GameEvents.RaisePlaySound("breaker_reset_clunk", FocusAnchor.position);
                    GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 55f, "machine");
                    GameEvents.RaiseCameraTrauma(0.10f, 2.5f);
                    OnStationActivated?.Invoke(this);
                    break;

                case SubstationStationType.FuelPump:
                    _lastPumpStrokeProgress = 0f;
                    gs.MarkFuelPrimed();
                    _completed = true;
                    SetPivotCompleted();
                    GameEvents.RaisePlaySound("fuel_pump_primed", FocusAnchor.position);
                    GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 35f, "machine");
                    OnStationActivated?.Invoke(this);
                    break;

                case SubstationStationType.GeneratorStarter:
                    int attempt = gs.IncrementGeneratorAttempt();
                    if (attempt < 3)
                    {
                        GameEvents.RaisePlaySound("gen_start_fail", FocusAnchor.position);
                        GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 90f, "machine");
                        GameEvents.RaiseCameraTrauma(0.16f, 2.0f);
                        GameEvents.RaiseToastMessage(attempt == 1 ? "It coughs and dies..." : "Come on — catch!", 2.2f);
                        OnGeneratorAttempted?.Invoke(this, attempt);
                    }
                    else
                    {
                        // Clears [E] Pull the starter prompt IMMEDIATELY (eliminates Godot sticky prompt bug!)
                        _completed = true;
                        SetPivotCompleted();
                        GameEvents.RaisePlaySound("gen_start_success", FocusAnchor.position);
                        GameEvents.RaiseNoiseEmitted(FocusAnchor.position, 140f, "machine");
                        GameEvents.RaiseCameraTrauma(0.24f, 1.8f);
                        OnGeneratorAttempted?.Invoke(this, attempt);
                        OnStationActivated?.Invoke(this);
                    }
                    break;

                case SubstationStationType.TransferLever:
                    gs.MarkTransferComplete();
                    _completed = true;
                    SetPivotCompleted();
                    GameEvents.RaisePlaySound("lever_transfer_clunk", FocusAnchor.position);
                    GameEvents.RaisePlaySound("power_restore_surge");
                    GameEvents.RaiseCameraTrauma(0.32f, 1.5f);
                    StartCoroutine(TransferArcFlash());
                    OnStationActivated?.Invoke(this);
                    break;
            }

            UpdateDiegeticIndicators();
        }

        public void OnLockedInteract(GameObject interactor)
        {
            GameEvents.RaisePlaySound("ui_denied", null, 0.75f);
            string hint = LockedHint;
            if (!string.IsNullOrEmpty(hint))
            {
                GameEvents.RaiseToastMessage(hint, 3.2f);
            }
        }

        // The knife switch closes on a live bus: a short blue-white arc at the contacts. Review 3: after the hold the
        // lever looked the same and only a small indicator changed, so the throw had no payoff.
        private System.Collections.IEnumerator TransferArcFlash()
        {
            Vector3 at = (animatedPivot != null ? animatedPivot.position : FocusAnchor.position) + Vector3.up * 0.12f;
            GameEvents.RaisePlaySound("electric_spark", at);
            var go = new GameObject("TransferArc");
            go.transform.position = at;
            Light arc = go.AddComponent<Light>();
            go.AddComponent<HDAdditionalLightData>();
            arc.type = LightType.Point;
            arc.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
            arc.range = 7f;
            arc.shadows = LightShadows.None;
            arc.useColorTemperature = false;
            arc.color = new Color(0.72f, 0.84f, 1f);
            float[] pulse = { 2600f, 300f, 1800f, 120f, 900f, 0f };
            for (int i = 0; i < pulse.Length; i++)
            {
                arc.intensity = pulse[i];
                yield return new WaitForSeconds(0.045f);
            }
            Destroy(go);
        }

        private void SetPivotCompleted()
        {
            if (animatedPivot != null)
            {
                animatedPivot.localRotation = Quaternion.Euler(activeLocalEuler);
            }
        }

        private void UpdateDiegeticIndicators()
        {
            Color targetColor;
            float intensityMult;

            if (_completed)
            {
                targetColor = poweredGreenColor;
                intensityMult = 1.0f;
            }
            else if (ArePrerequisitesMet())
            {
                // Active next objective step: clear bright amber/red rhythmic pulse
                bool blink = (Time.time % 0.9f) < 0.55f;
                targetColor = readyAmberColor;
                intensityMult = blink ? 1.1f : 0.25f;
            }
            else
            {
                // Waiting on prerequisites: steady dim fault red
                targetColor = faultRedColor;
                intensityMult = 0.35f;
            }

            if (statusLight != null)
            {
                statusLight.color = targetColor;
                if (_hdStatusLight != null) _hdStatusLight.SetLightDimmer(intensityMult, 0.6f * intensityMult);
                else statusLight.intensity = 18f * intensityMult;
            }

            ApplyEmissive(statusLedRenderers, targetColor * (10f * intensityMult));
            ApplyEmissive(powerConduitRenderers, (_completed ? poweredGreenColor : faultRedColor) * (_completed ? 4.5f : 0.6f));
        }

        private void ApplyEmissive(Renderer[] renderers, Color emissive)
        {
            if (renderers == null || renderers.Length == 0 || _mpb == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissiveColorId, emissive);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}
