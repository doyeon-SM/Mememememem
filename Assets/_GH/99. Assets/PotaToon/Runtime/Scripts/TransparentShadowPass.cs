/// NOTE)
/// This feature should be used only for character's cloth.
/// Otherwise, the shadow will cast to far object as well.
/// Limitations: Not will behave natural if more than 2 transparent clothes overlapped.

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
    public class TransparentShadowPass : ScriptableRenderPass
    {
        /* Static Variables */
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("TransparentShadow");
        private static readonly ShaderTagId k_AlphaSumShaderTagId = new ShaderTagId("TransparentAlphaSum");
        internal static int[] s_TextureSize = new int[2] { 1, 1 };


        /* Member Variables */
        private RTHandle m_TransparentShadowRT;
        private RTHandle m_TransparentAlphaSumRT;
        private ProfilingSampler m_ProfilingSampler;

        public TransparentShadowPass(string featureName)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            m_TransparentShadowRT?.Release();
            m_TransparentAlphaSumRT?.Release();
        }

        public void Setup(PotaToon volume)
        {
            var scale = (int)volume.transparentTextureScale.value;
            s_TextureSize[0] = volume.transparentShadow.value ? 1024 * scale : 1;
            s_TextureSize[1] = volume.transparentShadow.value ? 1024 * scale : 1;
        }
        
        private RenderTextureDescriptor GetRenderTextureDescriptor()
        {
            var descriptor = new RenderTextureDescriptor(s_TextureSize[0], s_TextureSize[1], RenderTextureFormat.R16, 0);
            descriptor.dimension = TextureDimension.Tex2D;
            descriptor.sRGB = false;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            return descriptor;
        }
        
        private class PassData
        {
#if UNITY_6000_0_OR_NEWER
            // RenderGraph Path
            public RendererListHandle rendererList;
#endif
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Depth
            var descriptor = GetRenderTextureDescriptor();
            RenderingUtils.ReAllocateIfNeeded(ref m_TransparentShadowRT, descriptor, FilterMode.Bilinear, name:"TransparentShadowMap");
            cmd.SetGlobalTexture(ShaderIDs._TransparentShadowMap, m_TransparentShadowRT);

            // Alpha Sum
            descriptor.graphicsFormat = GraphicsFormat.R16_SFloat;
            RenderingUtils.ReAllocateIfNeeded(ref m_TransparentAlphaSumRT, descriptor, FilterMode.Bilinear, name:"TransparentAlphaSum");
            cmd.SetGlobalTexture(ShaderIDs._TransparentAlphaSum, m_TransparentAlphaSumRT);
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
            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                cmd.SetGlobalFloat(ShaderIDs._CharShadowmapIndex, 0);
                CoreUtils.SetRenderTarget(cmd, m_TransparentShadowRT, ClearFlag.Color, 0, CubemapFace.Unknown, 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Depth & Alpha Sum
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
#if UNITY_2021_3
                var rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(k_ShaderTagId, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonTransparent;
                rendererListDesc.renderQueueRange = RenderQueueRange.transparent;
                cmd.DrawRendererList(context.CreateRendererList(rendererListDesc));
#else
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                ExecutePass(cmd, context.CreateRendererList(ref param));
#endif

                CoreUtils.SetRenderTarget(cmd, m_TransparentAlphaSumRT, ClearFlag.Color, 0, CubemapFace.Unknown, 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var alphaDrawSettings = RenderingUtils.CreateDrawingSettings(k_AlphaSumShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
#if UNITY_2021_3
                rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(k_AlphaSumShaderTagId, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonTransparent;
                rendererListDesc.renderQueueRange = RenderQueueRange.transparent;
                cmd.DrawRendererList(context.CreateRendererList(rendererListDesc));
#else
                param = new RendererListParams(renderingData.cullResults, alphaDrawSettings, filteringSettings);
                ExecutePass(cmd, context.CreateRendererList(ref param));
#endif
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var renderer = renderingData.cameraData.renderer;
#if UNITY_2021_3
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTarget, renderer.cameraDepthTarget);
#else
                CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);
#endif
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private static void ExecutePass(RasterCommandBuffer cmd, RendererList rendererList)
        {
            cmd.DrawRendererList(rendererList);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            
            // Depth
            var descriptor = GetRenderTextureDescriptor();
            TextureHandle transparentShadowMap = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "TransparentShadowMap", true, FilterMode.Bilinear);

            // Alpha Sum
            descriptor.graphicsFormat = GraphicsFormat.R16_SFloat;
            TextureHandle transparentAlphaSum = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "TransparentAlphaSum", true, FilterMode.Bilinear);
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Character Transparent Shadow", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(transparentShadowMap, 0);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                if (transparentShadowMap.IsValid())
                    builder.SetGlobalTextureAfterPass(transparentShadowMap, ShaderIDs._TransparentShadowMap);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.SetGlobalFloat(ShaderIDs._CharShadowmapIndex, 0);
                    ExecutePass(context.cmd, data.rendererList);
                });
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Character Transparent Alpha Sum", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(transparentAlphaSum, 0);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_AlphaSumShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                if (transparentAlphaSum.IsValid())
                    builder.SetGlobalTextureAfterPass(transparentAlphaSum, ShaderIDs._TransparentAlphaSum);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ExecutePass(context.cmd, data.rendererList);
                });
            }
        }
#endregion
#endif
    }
}