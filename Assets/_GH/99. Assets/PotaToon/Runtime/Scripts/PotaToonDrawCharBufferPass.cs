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
    public class PotaToonDrawCharBufferPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("PotaToonCharacterMask");
        private RTHandle m_PotaToonCharMaskRT;
        private ProfilingSampler m_ProfilingSampler;

        public PotaToonDrawCharBufferPass(string featureName)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingGbuffer;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            m_PotaToonCharMaskRT?.Release();
        }

        private RenderTextureDescriptor GetCompatibleDescriptor(ref RenderTextureDescriptor cameraTargetDescriptor)
        {
            var descriptor = cameraTargetDescriptor;
            descriptor.colorFormat = RenderTextureFormat.RG16;
            descriptor.sRGB = false;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            return descriptor;
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = GetCompatibleDescriptor(ref renderingData.cameraData.cameraTargetDescriptor);
            RenderingUtils.ReAllocateIfNeeded(ref m_PotaToonCharMaskRT, descriptor, FilterMode.Bilinear, name:"PotaToonCharMask");
            cmd.SetGlobalTexture(ShaderIDs._PotaToonCharMask, m_PotaToonCharMaskRT);
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
                var filteringSettings = new FilteringSettings(RenderQueueRange.all);
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, ref renderingData, SortingCriteria.CommonOpaque | SortingCriteria.CommonTransparent);
                CoreUtils.SetRenderTarget(cmd, m_PotaToonCharMaskRT, ClearFlag.Color, 0, CubemapFace.Unknown, 0);
#if UNITY_2021_3
                var rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(k_ShaderTagId, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonOpaque | SortingCriteria.CommonTransparent;
                rendererListDesc.renderQueueRange = RenderQueueRange.all;
                cmd.DrawRendererList(context.CreateRendererList(rendererListDesc));
#else
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                ExecutePass(cmd, context.CreateRendererList(ref param));
#endif
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
            var filteringSettings = new FilteringSettings(RenderQueueRange.all);

            var descriptor = GetCompatibleDescriptor(ref cameraData.cameraTargetDescriptor);
            TextureHandle potaToonCharMask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "PotaToonCharMask", true, FilterMode.Bilinear);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Char Mask", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(potaToonCharMask, 0);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque | SortingCriteria.CommonTransparent);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                if (potaToonCharMask.IsValid())
                    builder.SetGlobalTextureAfterPass(potaToonCharMask, ShaderIDs._PotaToonCharMask);
                
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