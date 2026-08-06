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
    public class CharacterScreenSpaceShadowPass : ScriptableRenderPass
    {
        private Material m_Material;
        private RTHandle m_RTHandle;
        private RTHandle m_ContactShadowRTHandle;
        private ProfilingSampler m_ProfilingSampler;
        private bool m_NeedCharShadowUpdate;
        
        public CharacterScreenSpaceShadowPass(string featureName)
        {
            m_ProfilingSampler = new ProfilingSampler(featureName);
            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            
            var material = Resources.Load<Material>("PotaToonSSShadow");
            if (material != null)
                m_Material = CoreUtils.CreateEngineMaterial(material.shader);
        }

        public void Setup(bool needCharShadowUpdate)
        {
            m_NeedCharShadowUpdate = needCharShadowUpdate;
        }
        
        public void Dispose()
        {
            m_RTHandle?.Release();
            CoreUtils.Destroy(m_Material);
        }
        
#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (m_Material == null)
                return;
            
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UNorm, FormatUsage.Blend)
                ? GraphicsFormat.R8G8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;

            RenderingUtils.ReAllocateIfNeeded(ref m_RTHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceCharShadowmapTexture");
            cmd.SetGlobalTexture(ShaderIDs._ScreenSpaceCharShadowmapTexture, m_RTHandle.nameID);
            
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = GraphicsFormat.R8_UNorm;
            RenderingUtils.ReAllocateIfNeeded(ref m_ContactShadowRTHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_CharContactShadowTexture");
            cmd.SetGlobalTexture(ShaderIDs._CharContactShadowTexture, m_ContactShadowRTHandle.nameID);

            ConfigureTarget(m_RTHandle);
            ConfigureClear(ClearFlag.None, Color.black);
        }
        
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material == null)
                return;

            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                if (m_NeedCharShadowUpdate)
                    Blitter.BlitTexture(cmd, m_RTHandle, Vector2.one, m_Material, 0);
#if UNITY_2021_3
                var customRTHandleProperties = new RTHandleProperties();
                customRTHandleProperties.rtHandleScale = Vector4.one;
                m_RTHandle.SetCustomHandleProperties(customRTHandleProperties);
#endif
                Blitter.BlitCameraTexture(cmd, m_RTHandle, m_ContactShadowRTHandle, m_Material, 1);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private class PassData
        {
            internal TextureHandle target;
            internal Material material;
        }
        
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_Material == null)
                return;
            
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            var desc = cameraData.cameraTargetDescriptor;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.msaaSamples = 1;
            desc.graphicsFormat = SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UNorm, GraphicsFormatUsage.Blend)
                ? GraphicsFormat.R8G8_UNorm
                : GraphicsFormat.B8G8R8A8_UNorm;
            TextureHandle color = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_ScreenSpaceCharShadowmapTexture", true);

            if (m_NeedCharShadowUpdate)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
                {
                    passData.target = color;
                    passData.material = m_Material;
                    builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);

                    if (color.IsValid())
                        builder.SetGlobalTextureAfterPass(color, ShaderIDs._ScreenSpaceCharShadowmapTexture);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.target, Vector2.one, data.material, 0);
                    });
                }
            }

            desc.depthStencilFormat = GraphicsFormat.None;
            desc.graphicsFormat = GraphicsFormat.R8_UNorm;
            TextureHandle contactShadowColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_CharContactShadowTexture", true);
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, profilingSampler))
            {
                passData.target = contactShadowColor;
                passData.material = m_Material;
            
                builder.AllowPassCulling(false);
                builder.SetRenderAttachment(contactShadowColor, 0, AccessFlags.Write);
                if (color.IsValid())
                    builder.UseTexture(color);
                
                if (contactShadowColor.IsValid())
                    builder.SetGlobalTextureAfterPass(contactShadowColor, ShaderIDs._CharContactShadowTexture);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.target, Vector2.one, data.material, 1);
                });
            }
        }
#endregion
#endif
    }
}

