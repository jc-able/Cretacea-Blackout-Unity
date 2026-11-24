using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// Ensures the attached Main Camera excludes the 'Viewmodel' layer (Layer 12) from main opaque
    /// rendering and verifies a global HDRP CustomPassVolume running ViewmodelCustomPass exists.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class ViewmodelCameraConfig : MonoBehaviour
    {
        public const string ViewmodelLayerName = "Viewmodel";
        public const int FallbackViewmodelLayer = 12;

        [Tooltip("Automatically spawn a global Viewmodel CustomPassVolume if none exists in the scene.")]
        [SerializeField] private bool ensureCustomPassVolume = true;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        public void Apply()
        {
            var cam = GetComponent<Camera>();
            ExcludeViewmodelLayer(cam);

            if (ensureCustomPassVolume && Application.isPlaying)
                EnsureGlobalViewmodelCustomPassVolume();
        }

        /// <summary>Removes the Viewmodel layer from the given camera's culling mask.</summary>
        public static void ExcludeViewmodelLayer(Camera cam)
        {
            if (cam == null) return;
            int layer = GetViewmodelLayerIndex();
            cam.cullingMask &= ~(1 << layer);
        }

        /// <summary>Recursively assigns a GameObject hierarchy (e.g., FPS flashlight rig) to the Viewmodel layer.</summary>
        public static void SetHierarchyToViewmodelLayer(GameObject root)
        {
            if (root == null) return;
            int layer = GetViewmodelLayerIndex();
            SetLayerRecursive(root.transform, layer);
        }

        public static int GetViewmodelLayerIndex()
        {
            int idx = LayerMask.NameToLayer(ViewmodelLayerName);
            return idx >= 0 ? idx : FallbackViewmodelLayer;
        }

        /// <summary>Finds or creates a Global CustomPassVolume with ViewmodelCustomPass at BeforePostProcess.</summary>
        public static CustomPassVolume EnsureGlobalViewmodelCustomPassVolume()
        {
            var volumes = Object.FindObjectsByType<CustomPassVolume>();
            foreach (var vol in volumes)
            {
                if (vol == null || vol.customPasses == null) continue;
                foreach (var pass in vol.customPasses)
                {
                    if (pass is ViewmodelCustomPass)
                        return vol;
                }
            }

            var go = new GameObject("HDRP_ViewmodelCustomPassVolume");
            var newVol = go.AddComponent<CustomPassVolume>();
            newVol.isGlobal = true;
            newVol.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            newVol.priority = 100f;

            var vmPass = new ViewmodelCustomPass
            {
                enabled = true,
                viewmodelLayerMask = 1 << GetViewmodelLayerIndex()
            };
            newVol.customPasses.Add(vmPass);
            return newVol;
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
