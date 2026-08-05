using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotaToon
{
    public class PotaToonFeature : ScriptableRendererFeature
    {
        private CharacterShadowPass m_CharShadowPass;
        private TransparentShadowPass m_CharTransparentShadowPass;
        private CharacterScreenSpaceShadowPass m_CharScreenSpaceShadowPass;
        private OITPreRenderPass m_OITPreRenderPass;
        private OITPass m_OITPass;
        private OITDepthPass m_OITDepthPass;
        private PotaToonDrawCharBufferPass m_PotaToonDrawCharBufferPass;
        private PotaToonPostProcessPass m_PostProcessPass;
        private PotaToonDitherFadePass m_DitherFadePass;

        public override void Create()
        {
            m_OITPreRenderPass?.Cleanup();
            m_OITPass?.Cleanup();
            m_OITDepthPass?.Dispose();

            m_CharShadowPass = new CharacterShadowPass("Character ShadowMap");
            m_CharShadowPass.ConfigureInput(ScriptableRenderPassInput.None);
            m_CharTransparentShadowPass = new TransparentShadowPass("TransparentShadowMap");
            m_CharTransparentShadowPass.ConfigureInput(ScriptableRenderPassInput.None);
            m_CharScreenSpaceShadowPass = new CharacterScreenSpaceShadowPass("Character Screen Space Shadows");
            
            m_OITPreRenderPass = new OITPreRenderPass("OIT PreRender");
            m_OITPass = new OITPass(m_OITPreRenderPass.orderIndependentTransparency, "OIT");
            m_OITDepthPass = new OITDepthPass("OIT Depth");

            m_PotaToonDrawCharBufferPass = new PotaToonDrawCharBufferPass("PotaToon CharMask");
            m_PostProcessPass = new PotaToonPostProcessPass("PotaToon PostProcessing");
            m_DitherFadePass = new PotaToonDitherFadePass("PotaToon DitherFade");

#if UNITY_EDITOR
            Shader.SetKeyword(PotaToonGlobalKeywords.Debug, true);
#else
            Shader.SetKeyword(PotaToonGlobalKeywords.Debug, false);
#endif
        }
        
#if !UNITY_2021_3 && !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_3_OR_NEWER
        [Obsolete]
    #endif
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            m_DitherFadePass.SetCameraTargetHandles(renderer);
        }
#endif

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (VolumeManager.instance.stack == null)
                return;
            
            var volume = VolumeManager.instance.stack.GetComponent<PotaToon>();
            if (volume == null)
                return;

            if (renderingData.cameraData.cameraType != CameraType.SceneView && renderingData.cameraData.cameraType != CameraType.Game)
                return;

            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;

            var targetColorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
            if (targetColorFormat == RenderTextureFormat.Depth || targetColorFormat == RenderTextureFormat.Shadowmap)
                return;
            
            Shader.SetKeyword(PotaToonGlobalKeywords.OIT, volume.oit.value);

            var needCharShadowUpdate = CharacterShadowUtils.IfCharShadowUpdateNeeded(renderingData, volume.shadowCullingDistance.value);
            m_CharScreenSpaceShadowPass.Setup(needCharShadowUpdate);
            if (needCharShadowUpdate)
            {
                m_CharShadowPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal); // Color: For refraction
                m_CharShadowPass.Setup(renderingData, volume);
                m_CharTransparentShadowPass.Setup(volume);
                renderer.EnqueuePass(m_CharShadowPass);
                if (volume.transparentShadow.value)
                {
                    renderer.EnqueuePass(m_CharTransparentShadowPass);
                }
                renderer.EnqueuePass(m_CharScreenSpaceShadowPass);
            }
#if UNITY_EDITOR
            else
            {
                renderer.EnqueuePass(m_CharScreenSpaceShadowPass);
                Shader.SetGlobalFloat(ShaderIDs._FallbackMaxToonBrightness, volume.maxToonBrightness.value);
            }
#endif
            
            // Dither Fade
            if (PotaToonCharacter.requireDitherFade)
                renderer.EnqueuePass(m_DitherFadePass);
            
            // OIT
            if (volume.oit.value)
            {
                if (IsOITCompatibleDeviceType())
                {
                    m_OITPass.ConfigureInput(ScriptableRenderPassInput.Color);
                    m_OITPass.Setup(volume.oitMode.value);
                    renderer.EnqueuePass(m_OITPreRenderPass);
                    renderer.EnqueuePass(m_OITPass);
                    renderer.EnqueuePass(m_OITDepthPass);
                }
                else
                {
                    Debug.LogWarning("[PotaToon] OIT is only available in DirectX 11/12.");
                }
            }
            
            // Char mask
            m_PotaToonDrawCharBufferPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(m_PotaToonDrawCharBufferPass);
            
            // PP
            m_PostProcessPass.Setup(volume);
            renderer.EnqueuePass(m_PostProcessPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_CharShadowPass.Dispose();
            m_CharTransparentShadowPass.Dispose();
            m_CharScreenSpaceShadowPass.Dispose();
            m_OITPreRenderPass.Cleanup();
            m_OITPass.Cleanup();
            m_OITDepthPass.Dispose();
            m_PostProcessPass.Dispose();
            m_PotaToonDrawCharBufferPass.Dispose();
            m_DitherFadePass.Dispose();
        }

        private bool IsOITCompatibleDeviceType()
        {
            return SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;
        }
    }
}
