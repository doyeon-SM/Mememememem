using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace PotaToon
{
    public class OITPreRenderPass : ScriptableRenderPass
    {
        public OitLinkedList orderIndependentTransparency;
        private ProfilingSampler m_ProfilingSampler;

        public OITPreRenderPass(string featureName)
        {
            renderPassEvent = RenderPassEvent.BeforeRendering;
            orderIndependentTransparency = new OitLinkedList(Resources.Load<ComputeShader>("OITComputeUtils"));
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }
        
#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            
            var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                orderIndependentTransparency.PreRender(cmd, colorCopyDescriptor);
            }
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif

        public void Cleanup()
        {
            orderIndependentTransparency?.Release();
        }
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private class PassData
        {
            public RenderTextureDescriptor descriptor;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            using (var builder = renderGraph.AddUnsafePass<PassData>("[PotaToon] OIT PreRender", out var passData, m_ProfilingSampler))
            {
                builder.AllowPassCulling(false);
                passData.descriptor = cameraData.cameraTargetDescriptor;
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    orderIndependentTransparency.PreRender(cmd, data.descriptor);
                });
            }
        }

#endregion
#endif
        
    }
}