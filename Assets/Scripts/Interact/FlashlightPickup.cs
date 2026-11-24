using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Cretacea.Core;
using Cretacea.Player;

namespace Cretacea.Interact
{
    /// <summary>
    /// Security Desk Tactical Flashlight pickup (flashlight_pickup.fbx) replacing Godot's invisible black cylinder (FL-4).
    /// Features a subtle pulsing amber/red charging LED beacon right in front of the Ch. 1 North-facing spawn.
    /// Picking it up disables the desk mesh, unlocks FlashlightController (equip raise + auto-ON), and advances Ch. 1.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FlashlightPickup : MonoBehaviour, IInteractable
    {
        [Header("Identity & Prompt")]
        [SerializeField] private string interactId = "pickup_flashlight";
        [SerializeField] private string promptText = "Pick up Tactical Flashlight {interact}";
        [SerializeField] private Transform focusAnchor;

        [Header("Diegetic Charging Beacon (FL-4 Fix)")]
        [SerializeField] private Light chargingLedLight;
        [SerializeField] private Renderer chargingLedRenderer;
        [SerializeField] private Color ledColor = new Color(1.0f, 0.48f, 0.12f);
        [SerializeField] private float pulseSpeedHz = 1.4f;
        [SerializeField] private float ledLumens = 4.5f;

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");

        private bool _collected;
        private MaterialPropertyBlock _mpb;
        private HDAdditionalLightData _hdLight;

        public string InteractId => interactId;
        public string PromptText => promptText;
        public string LockedHint => string.Empty;
        public bool CanInteract => !_collected;
        public bool IsEnabled => !_collected && gameObject.activeInHierarchy;
        public bool IsLocked => false;
        public float HoldDuration => 0f;
        public bool PreserveHoldProgress => false;
        public float SavedHoldProgress => 0f;
        public Transform FocusAnchor => focusAnchor != null ? focusAnchor : transform;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            gameObject.layer = GameLayers.Interactable;
            _mpb = new MaterialPropertyBlock();

            if (chargingLedLight != null)
            {
                chargingLedLight.type = LightType.Point;
                chargingLedLight.lightUnit = LightUnit.Lumen;
                chargingLedLight.color = ledColor;
                chargingLedLight.range = 0.85f;
                chargingLedLight.intensity = ledLumens;
                chargingLedLight.shadows = LightShadows.None;
                _hdLight = chargingLedLight.GetComponent<HDAdditionalLightData>();
            }
        }

        private void OnEnable()
        {
            WorldRegistry.EnsureInstance().RegisterInteractable(interactId, this);
            if (GameState.Instance != null && GameState.Instance.HasFlashlight)
            {
                MarkCollectedSilent();
            }
        }

        private void OnDisable()
        {
            if (WorldRegistry.Instance != null)
                WorldRegistry.Instance.UnregisterInteractable(interactId, this);
        }

        private void Update()
        {
            if (_collected) return;
            // round 4 (review 3, office_dark): the torch stayed on the desk, glowing, when the player got it some other way
            // (Continue, jump_chapter, a checkpoint): once the player holds a flashlight, the desk copy is gone
            if (GameState.Instance != null && GameState.Instance.HasFlashlight)
            {
                MarkCollectedSilent();
                return;
            }
            float pulse = 0.4f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeedHz * Mathf.PI * 2f));

            if (chargingLedLight != null)
            {
                if (_hdLight != null) _hdLight.SetLightDimmer(pulse, 0.25f * pulse);
                else chargingLedLight.intensity = ledLumens * pulse;
            }

            if (chargingLedRenderer != null)
            {
                chargingLedRenderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissiveColorId, ledColor * (6f * pulse));
                chargingLedRenderer.SetPropertyBlock(_mpb);
            }
        }

        public void ResetForChapter1()
        {
            _collected = false;
            gameObject.SetActive(true);
            Collider col = GetComponent<Collider>();
            if (col != null) col.enabled = true;
        }

        public void MarkCollectedSilent()
        {
            _collected = true;
            gameObject.SetActive(false);
        }

        public void OnInteract(GameObject interactor)
        {
            if (_collected) return;
            _collected = true;

            FlashlightController fl = interactor != null
                ? interactor.GetComponentInChildren<FlashlightController>(true)
                : FindAnyObjectByType<FlashlightController>();

            if (fl != null)
            {
                fl.SetHasFlashlight(true, autoTurnOn: true, playEquipAnim: true);
            }
            else
            {
                GameState.EnsureInstance().SetHasFlashlight(true);
            }

            GameEvents.RaisePlaySound("foley_pickup", transform.position);
            string hint = CretaceaInput.Instance != null
                ? CretaceaInput.Instance.FormatPrompt("{flashlight} Toggle Tactical Flashlight")
                : "[F] Toggle Tactical Flashlight";
            GameEvents.RaiseToastMessage(hint, 3.5f);
            GameEvents.RaiseFlashlightPickedUp();

            gameObject.SetActive(false);
        }

        public void OnHoldProgress(GameObject interactor, float normalized01) { }
        public void OnLockedInteract(GameObject interactor) { }
    }
}
