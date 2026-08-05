/*
 * This file includes modifications to original work licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Modified by: mseon
 * Date: 2025-04-06
 * Changes:
 *   1. Make it compatible with RenderGraph in Unity 6.0 or above.
 *   2. Separate the buffer initialization logic to PreRenderPass.
 *   3. Create a Depth buffer for OIT objects.
 *   4. Remove the interface and move the OITLinkedList class to the pass class.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif

namespace PotaToon
{
    public class OITPass : ScriptableRenderPass
    {
        private static readonly int k_BlitTextureShaderID = Shader.PropertyToID("_BlitTexture");
        private static readonly ShaderTagId k_OutlineShaderTagId = new ShaderTagId("TransparentOutline");
        private static class LocalShaderKeywordStrings
        {
            public static readonly string _OIT_ADDITIVE = "_OIT_ADDITIVE";
        }
        
        private Material m_Material;
        private RTHandle m_CopiedColor;
        private RTHandle m_OITCopyTextureRT;
        private ProfilingSampler m_ProfilingSampler;
        private bool m_UseTransparentOutline;
        private OitLinkedList m_OIT;
        private OITMode mode;

        public OITPass(OitLinkedList oit, string featureName)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            m_OIT = oit;
            var oitMaterial = Resources.Load<Material>("PotaToonOIT");
            if (oitMaterial != null)
                m_Material = CoreUtils.CreateEngineMaterial(oitMaterial.shader);
            m_UseTransparentOutline = true;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Setup(OITMode mode)
        {
            this.mode = mode;
        }
        
#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, colorCopyDescriptor, name: "_OITPassColorCopy", filterMode:FilterMode.Bilinear);
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
#if UNITY_2021_3
                var src = renderingData.cameraData.renderer.cameraColorTarget;
#else
                var src = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
                if (m_Material != null)
                {
                    m_OIT.SetMaterialData(cmd, m_Material);
#if UNITY_2021_3
                    Blitter2021.BlitCameraTexture(cmd, src, m_CopiedColor);
#else
                    Blitter.BlitCameraTexture(cmd, src, m_CopiedColor);
#endif
                    m_Material.SetTexture(k_BlitTextureShaderID, m_CopiedColor);
                    CoreUtils.SetKeyword(m_Material, LocalShaderKeywordStrings._OIT_ADDITIVE, mode == OITMode.Additive);
                    
                    CoreUtils.SetRenderTarget(cmd, src);
                    CoreUtils.DrawFullScreen(cmd, m_Material, null, 0);

                    if (m_UseTransparentOutline)
                    {
                        var drawSettings = RenderingUtils.CreateDrawingSettings(k_OutlineShaderTagId, ref renderingData, SortingCriteria.CommonTransparent);
                        var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filteringSettings);
                    }
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif

        public void Cleanup()
        {
            m_CopiedColor?.Release();
            m_OITCopyTextureRT?.Release();
            CoreUtils.Destroy(m_Material);
        }
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private class PassData
        {
            public RendererListHandle rendererList;
            public Material material;
            public TextureHandle cameraColor;
            public TextureHandle copiedColor;
            public OITMode mode;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var lightData = frameData.Get<UniversalLightData>();
            var colorCopyDescriptor = cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
            TextureHandle copiedColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor, "_OITPassColorCopy", true);
            
            using (var builder = renderGraph.AddUnsafePass<PassData>("[PotaToon] OIT PreRender", out var passData, m_ProfilingSampler))
            {
                builder.AllowPassCulling(false);
                passData.material = m_Material;
                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    m_OIT.SetMaterialData(cmd, data.material);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] OIT Outline", out var passData, m_ProfilingSampler))
            {
                builder.SetRenderAttachment(resourceData.cameraColor, 0);
                builder.SetRenderAttachmentDepth(resourceData.cameraDepth);
                
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_OutlineShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                var filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
                var param = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                passData.rendererList = renderGraph.CreateRendererList(param);
                passData.material = m_Material;
                builder.UseRendererList(passData.rendererList);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if (m_UseTransparentOutline)
                        context.cmd.DrawRendererList(data.rendererList);
                });
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] OIT", out var passData, m_ProfilingSampler))
            {
                builder.SetRenderAttachment(copiedColor, 0);
                builder.UseTexture(resourceData.cameraColor);
                passData.cameraColor = resourceData.cameraColor;
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.cameraColor, Vector2.one, 0, false);
                });
            }
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] OIT", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(resourceData.cameraColor, 0);

                passData.material = m_Material;
                passData.copiedColor = copiedColor;
                passData.mode = mode;
                builder.UseTexture(copiedColor);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    if (data.material != null)
                    {
                        data.material.SetTexture(k_BlitTextureShaderID, data.copiedColor);
                        CoreUtils.SetKeyword(data.material, LocalShaderKeywordStrings._OIT_ADDITIVE, data.mode == OITMode.Additive);
                        CoreUtils.DrawFullScreen(context.cmd, data.material, null, 0);
                    }
                });
            }
        }

#endregion
#endif
        
    }


    public class OitLinkedList
    {
        private int screenWidth, screenHeight;
        public ComputeBuffer fragmentLinkBuffer;
        public ComputeBuffer startOffsetBuffer;
        private readonly int fragmentLinkBufferId;
        private readonly int startOffsetBufferId;
        private readonly Material linkedListMaterial;
        private const int MAX_SORTED_PIXELS = 8;

        public ComputeShader oitComputeUtils;
        private readonly int clearStartOffsetBufferKernel;
        private int dispatchGroupSizeX, dispatchGroupSizeY;

        public OitLinkedList(ComputeShader oitComputeUtilsCS)
        {
            fragmentLinkBufferId = Shader.PropertyToID("FLBuffer");
            startOffsetBufferId = Shader.PropertyToID("StartOffsetBuffer");

            oitComputeUtils = oitComputeUtilsCS;
            if (oitComputeUtils != null)
                clearStartOffsetBufferKernel = oitComputeUtils.FindKernel("ClearStartOffsetBuffer");
        }

        public void PreRender(CommandBuffer command, RenderTextureDescriptor desc)
        {
            // validate the effect itself
            if (desc.width * 2 != screenWidth || desc.height * 2 != screenHeight)
            {
                SetupGraphicsBuffers(desc.width, desc.height);
            }

            //reset StartOffsetBuffer to zeros
            command.DispatchCompute(oitComputeUtils, clearStartOffsetBufferKernel, dispatchGroupSizeX, dispatchGroupSizeY, 1);

            // set buffers for rendering
            command.SetRandomWriteTarget(1, fragmentLinkBuffer);
            command.SetRandomWriteTarget(2, startOffsetBuffer);
        }

        public void SetMaterialData(CommandBuffer command, Material material)
        {
            command.ClearRandomWriteTargets();
            material.SetBuffer(fragmentLinkBufferId, fragmentLinkBuffer);
            material.SetBuffer(startOffsetBufferId, startOffsetBuffer);
        }

        public void Release()
        {
            fragmentLinkBuffer?.Dispose();
            startOffsetBuffer?.Dispose();
        }

        private void SetupGraphicsBuffers(int width, int height)
        {
            Release();
            screenWidth = width * 2;
            screenHeight = height * 2;

            int bufferSize = Mathf.Max(screenWidth * screenHeight * MAX_SORTED_PIXELS, 1);
            int bufferStride = sizeof(uint) * 4;
            //the structured buffer contains all information about the transparent fragments
            //this is the per pixel linked list on the gpu
            fragmentLinkBuffer = new ComputeBuffer(bufferSize, bufferStride, ComputeBufferType.Counter);

            int bufferSizeHead = Mathf.Max(screenWidth * screenHeight, 1);
            int bufferStrideHead = sizeof(uint);
            //create buffer for addresses, this is the head of the linked list
            startOffsetBuffer = new ComputeBuffer(bufferSizeHead, bufferStrideHead, ComputeBufferType.Raw);

            oitComputeUtils.SetBuffer(clearStartOffsetBufferKernel, startOffsetBufferId, startOffsetBuffer);
            oitComputeUtils.SetInt("screenWidth", screenWidth);
            dispatchGroupSizeX = Mathf.CeilToInt(screenWidth / 32.0f);
            dispatchGroupSizeY = Mathf.CeilToInt(screenHeight / 32.0f);
        }
    }
}
