using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Cretacea.Rendering
{
    /// <summary>
    /// HDRP CustomPass that clears the depth buffer and renders the FPS Viewmodel layer
    /// (Layer 12: Viewmodel) right before Post-Processing (BeforePostProcess).
    /// Guarantees the held flashlight/hand NEVER clips into walls or doors while receiving
    /// full HDRP scene lighting, reflection probes, flashlight bounce, bloom, and ACES tonemapping.
    /// </summary>
    [Serializable]
    public sealed class ViewmodelCustomPass : CustomPass
    {
        [Tooltip("Layer mask containing first-person viewmodel meshes (default: Layer 12 'Viewmodel').")]
        public LayerMask viewmodelLayerMask = 1 << 12;

        [Tooltip("Also render viewmodels in the Scene View camera when editing.")]
        public bool renderInSceneView = false;

        [Tooltip("Render transparent submeshes (e.g., flashlight glass lens) after opaques.")]
        public bool renderTransparentPass = true;

        private ShaderTagId[] _forwardLitTags;

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            name = "Cretacea Viewmodel Anti-Clip Pass";
            targetColorBuffer = TargetBuffer.Camera;
            targetDepthBuffer = TargetBuffer.Camera;
            clearFlags = ClearFlag.None; // We clear ONLY Depth explicitly inside Execute for Game cameras.

            _forwardLitTags = new[]
            {
                new ShaderTagId("Forward"),
                new ShaderTagId("ForwardOnly"),
                new ShaderTagId("SRPDefaultUnlit")
            };

            if (viewmodelLayerMask.value == 0)
            {
                int vmLayer = LayerMask.NameToLayer("Viewmodel");
                viewmodelLayerMask = vmLayer >= 0 ? (1 << vmLayer) : (1 << 12);
            }
        }

        protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
        {
            if (!ShouldRenderForCamera(hdCamera))
                return;

            // Critical: Main Camera excludes Layer 12 so the main pass skips it, while this pass
            // opts Layer 12 into CustomPass culling so HDRP culls viewmodel renderers for us.
            cullingParameters.cullingMask |= (uint)viewmodelLayerMask.value;
        }

        protected override void Execute(CustomPassContext ctx)
        {
            if (!ShouldRenderForCamera(ctx.hdCamera))
                return;

            if (viewmodelLayerMask.value == 0)
                return;

            // Clear ONLY Depth so viewmodel draws over any nearby wall/door without erasing scene color.
            CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ctx.cameraDepthBuffer, ClearFlag.Depth);

            // 1. Opaque Viewmodel meshes (hands, flashlight body, metal bezel)
            // HDRP Lit opaque Forward passes use ZTest Equal (_ZTestDepthEqualForOpaque) because they expect
            // a depth prepass. We just cleared depth, so force LessEqual + depth write for the viewmodel draw,
            // otherwise every opaque fragment fails the depth test and only transparent submeshes appear.
            var opaqueDepthOverride = new RenderStateBlock(RenderStateMask.Depth)
            {
                depthState = new DepthState(true, CompareFunction.LessEqual)
            };
            CustomPassUtils.DrawRenderers(
                ctx,
                _forwardLitTags,
                viewmodelLayerMask,
                RenderQueueType.AllOpaque,
                overrideMaterial: null,
                overrideMaterialIndex: 0,
                overrideRenderState: opaqueDepthOverride,
                sorting: SortingCriteria.CommonOpaque);

            // 2. Transparent Viewmodel meshes (glass flashlight lens, emissive indicator cover)
            if (renderTransparentPass)
            {
                CustomPassUtils.DrawRenderers(
                    ctx,
                    _forwardLitTags,
                    viewmodelLayerMask,
                    RenderQueueType.AllTransparent,
                    overrideMaterial: null,
                    overrideMaterialIndex: 0,
                    overrideRenderState: default,
                    sorting: SortingCriteria.CommonTransparent);
            }
        }

        private bool ShouldRenderForCamera(HDCamera hdCamera)
        {
            if (hdCamera == null || hdCamera.camera == null)
                return false;

            CameraType camType = hdCamera.camera.cameraType;
            if (camType == CameraType.Game)
                return true;

            return renderInSceneView && camType == CameraType.SceneView;
        }
    }
}
