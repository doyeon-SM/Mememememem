using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace PotaToon
{
    public class PotaToonDitherFadePass : ScriptableRenderPass
    {
        private static class Property
        {
            public static readonly string SrcBlend = "_SrcBlend";
            public static readonly string DstBlend = "_DstBlend";
            public static readonly string SrcBlendAlpha = "_SrcBlendAlpha";
            public static readonly string DstBlendAlpha = "_DstBlendAlpha";
        }
        private static class ShaderTagIds
        {
            public static readonly ShaderTagId DitherFade = new ShaderTagId("OpaqueDitherFade");
            public static readonly ShaderTagId Outline = new ShaderTagId("OpaqueDitherFadeOutline");
        }
        private RTHandle m_PotaToonDitherFadeRT;
        private Material m_ResolveMaterial;
        private ProfilingSampler m_ProfilingSampler;
#if !UNITY_6000_4_OR_NEWER
    #if UNITY_2021_3
        private RenderTargetIdentifier m_CameraColor;
        private RenderTargetIdentifier m_CameraDepth;
    #else
        private RTHandle m_CameraColor;
        private RTHandle m_CameraDepth;
    #endif
#endif

        public PotaToonDitherFadePass(string featureName)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            m_ProfilingSampler = new ProfilingSampler(featureName);
            var blitShader = Shader.Find("Hidden/PotaToon/Blit");
            if (blitShader != null)
                m_ResolveMaterial = CoreUtils.CreateEngineMaterial(blitShader);
        }

        public void Dispose()
        {
            m_PotaToonDitherFadeRT?.Release();
            CoreUtils.Destroy(m_ResolveMaterial);
        }

#if !UNITY_6000_4_OR_NEWER
        public void SetCameraTargetHandles(ScriptableRenderer renderer)
        {
#if !UNITY_2021_3
        #if UNITY_6000_0_OR_NEWER
            #pragma warning disable CS0618
        #endif
            m_CameraColor = renderer.cameraColorTargetHandle;
            m_CameraDepth = renderer.cameraDepthTargetHandle;
        #if UNITY_6000_0_OR_NEWER
            #pragma warning restore CS0618
        #endif
#endif
        }
#endif
        
        private RenderTextureDescriptor GetCompatibleDescriptor(ref RenderTextureDescriptor cameraTargetDescriptor)
        {
            var descriptor = cameraTargetDescriptor;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            return descriptor;
        }

        private void SetResolveMaterialBlendingMode(Material mat)
        {
            mat.SetInt(Property.SrcBlend, (int)BlendMode.One);
            mat.SetInt(Property.DstBlend, (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt(Property.SrcBlendAlpha, (int)BlendMode.One);
            mat.SetInt(Property.DstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = GetCompatibleDescriptor(ref renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref m_PotaToonDitherFadeRT, descriptor, FilterMode.Bilinear, name:"PotaToonDitherFadeTexture");
#if UNITY_2021_3
            m_CameraColor = renderingData.cameraData.renderer.cameraColorTarget;
            m_CameraDepth = renderingData.cameraData.renderer.cameraDepthTarget;
#endif
        }
        
#if !UNITY_2021_3
        private static void ExecutePass(CommandBuffer cmd, RendererList rendererList)
        {
            cmd.DrawRendererList(rendererList);
        }
#endif

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                CoreUtils.SetRenderTarget(cmd, m_PotaToonDitherFadeRT, m_CameraDepth, ClearFlag.Color, 0, CubemapFace.Unknown, 0);
                
#if UNITY_2021_3
                var rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(ShaderTagIds.Outline, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                rendererListDesc.rendererConfiguration = renderingData.perObjectData;
                cmd.DrawRendererList(context.CreateRendererList(rendererListDesc));
#else
                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                var drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds.Outline, ref renderingData, SortingCriteria.CommonOpaque);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                ExecutePass(cmd, context.CreateRendererList(ref param));
#endif
                
#if UNITY_2021_3
                rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(ShaderTagIds.DitherFade, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererListDesc.renderQueueRange = RenderQueueRange.opaque;
                rendererListDesc.rendererConfiguration = renderingData.perObjectData;
                cmd.DrawRendererList(context.CreateRendererList(rendererListDesc));
#else
                drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds.DitherFade, ref renderingData, SortingCriteria.CommonOpaque);
                param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                ExecutePass(cmd, context.CreateRendererList(ref param));
#endif
                
                // Resolve
                if (m_ResolveMaterial != null)
                {
                    CoreUtils.SetRenderTarget(cmd, m_CameraColor);
                    SetResolveMaterialBlendingMode(m_ResolveMaterial);
    #if UNITY_2021_3
                    Blitter2021.BlitTexture(cmd, m_PotaToonDitherFadeRT, Vector2.one, m_ResolveMaterial, 0);
    #else
                    Blitter.BlitTexture(cmd, m_PotaToonDitherFadeRT, Vector2.one, m_ResolveMaterial, 0);
    #endif
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private class PassData
        {
            public RendererListHandle rendererList;
        }
        
        private class BlitPassData
        {
            public TextureHandle ditherFadeHandle;
            public Material resolveMaterial;
        }

        private static void ExecutePass(RasterCommandBuffer cmd, RendererList rendererList)
        {
            cmd.DrawRendererList(rendererList);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            
            var descriptor = GetCompatibleDescriptor(ref cameraData.cameraTargetDescriptor);
            TextureHandle ditherFadeHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "PotaToonDitherFadeTexture", true, FilterMode.Bilinear);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Dither Fade", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(ditherFadeHandle, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds.Outline, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ExecutePass(context.cmd, data.rendererList);
                });
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Dither Fade", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(ditherFadeHandle, 0);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.ReadWrite);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(ShaderTagIds.DitherFade, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ExecutePass(context.cmd, data.rendererList);
                });
            }
            
            if (m_ResolveMaterial != null)
            {
                using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>("[PotaToon] Dither Fade Resolve", out var passData, m_ProfilingSampler))
                {
                    builder.SetRenderAttachment(resourceData.cameraColor, 0);
                    passData.ditherFadeHandle = ditherFadeHandle;
                    passData.resolveMaterial = m_ResolveMaterial;
                    
                    if (ditherFadeHandle.IsValid())
                        builder.UseTexture(ditherFadeHandle);
                    
                    builder.SetRenderFunc((BlitPassData data, RasterGraphContext context) =>
                    {
                        SetResolveMaterialBlendingMode(passData.resolveMaterial);
                        Blitter.BlitTexture(context.cmd, data.ditherFadeHandle, Vector2.one, passData.resolveMaterial, 0);
                    });
                }
            }
        }
#endregion
#endif
    }
}