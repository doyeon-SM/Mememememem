using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace PotaToon
{
    public class OITDepthPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("OITDepth");

        private RTHandle m_OITDepthRT;
        private ProfilingSampler m_ProfilingSampler;
        private FilteringSettings m_FilteringSettings;

        public OITDepthPass(string featureName)
        {
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            m_OITDepthRT?.Release();
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            var descriptor = new RenderTextureDescriptor(cameraDescriptor.width, cameraDescriptor.height, RenderTextureFormat.RFloat, 0);
            descriptor.sRGB = false;
            descriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateIfNeeded(ref m_OITDepthRT, descriptor, FilterMode.Point, name:"_OITDepthTexture");
            cmd.SetGlobalTexture(ShaderIDs._OITDepthTexture, m_OITDepthRT);

            ConfigureTarget(m_OITDepthRT);
            ConfigureClear(ClearFlag.All, Color.black);
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
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
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var lightData = frameData.Get<UniversalLightData>();
            var descriptor = new RenderTextureDescriptor(cameraData.cameraTargetDescriptor.width, cameraData.cameraTargetDescriptor.height, RenderTextureFormat.RFloat, 0);
            descriptor.sRGB = false;
            descriptor.autoGenerateMips = false;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            TextureHandle output = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_OITDepthTexture", true);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] OIT Depth", out var passData, m_ProfilingSampler))
            {
                builder.SetRenderAttachment(output, 0);

                if (output.IsValid())
                    builder.SetGlobalTextureAfterPass(output, ShaderIDs._OITDepthTexture);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, m_FilteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }
        }
#endregion
#endif
    }
}