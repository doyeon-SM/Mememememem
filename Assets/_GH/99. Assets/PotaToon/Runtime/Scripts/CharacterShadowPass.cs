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
    public class CharacterShadowPass : ScriptableRenderPass
    {
        /* ID */
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("CharacterDepth");

        /* Member Variables */
        private RTHandle m_CharShadowRT;
        private ProfilingSampler m_ProfilingSampler;
        private PassData m_PassData;
        private static int[] s_TextureSize = new int[2] { 1, 1 };
#if !UNITY_6000_4_OR_NEWER
        private ToonCharacterShadow m_CBuffer;
#endif

        public CharacterShadowPass(string featureName)
        {
            m_PassData = new PassData();
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
            m_ProfilingSampler = new ProfilingSampler(featureName);
        }

        public void Dispose()
        {
            m_CharShadowRT?.Release();
#if UNITY_2021_3
            ConstantBuffer.ReleaseAll();
#endif
        }

        public void Setup(in RenderingData renderingData, PotaToon volume)
        {
#if !UNITY_6000_4_OR_NEWER
            m_CBuffer = new ToonCharacterShadow();
#endif
            m_PassData.enabled = true;
            m_PassData.bias = new Vector3(volume.bias.value * PotaToon.k_BiasScale, volume.normalBias.value * 0.01f, 0f);
            var scale = (int)volume.textureScale.value;
            m_PassData.cascadeResolutionScale = 1.0f;
            m_PassData.cascadeMaxDistance = CharacterShadowUtils.GetCullingDistance(renderingData.cameraData.camera, volume.shadowCullingDistance.value);
            s_TextureSize[0] = 1024 * scale;
            s_TextureSize[1] = 1024 * scale;
            m_PassData.useBrightestLight = volume.mode.value == PotaToonMode.Concert;
            m_PassData.followLightLayer = volume.followLayerMask.value;
            m_PassData.maxToonBrightness = volume.maxToonBrightness.value;
            CharacterShadowUtils.shadowCamera.lightDirectionOffset = volume.mode.value == PotaToonMode.Normal ? volume.charShadowDirOffset.value : Vector3.zero;
            CharacterShadowUtils.shadowCamera.overrideLightDirection = volume.mode.value == PotaToonMode.Normal && volume.charShadowDirOffsetMode.value == LightOffsetMode.World;
        }

        private static void UpdateConstantBuffer_Internal(ref ToonCharacterShadow cb, PassData passData, in CharacterShadowUtils.BrightestLightData brightestLightData)
        {
            var shadowCamera = CharacterShadowUtils.shadowCamera;
            var textureScale = s_TextureSize[0] / 1024;
            
            float softShadowSamples = Mathf.Clamp((int)Mathf.Log(textureScale, 2) + 1, 2, 4);
            
            if (shadowCamera != null)
            {
                passData.projectM = shadowCamera.projectionMatrix;
                passData.viewM = shadowCamera.GetViewMatrix();
                
                if (shadowCamera.distanceCameraToNearestRenderer > 0.5f)
                    softShadowSamples = Mathf.Max(softShadowSamples - 1f, 2f);
            }
            
            float invShadowMapWidth = 1.0f / s_TextureSize[0];
            float invShadowMapHeight = 1.0f / s_TextureSize[1];
            float invHalfShadowMapWidth = 0.5f * invShadowMapWidth;
            float invHalfShadowMapHeight = 0.5f * invShadowMapHeight;
            
            cb._BrightestLightDirection = brightestLightData.lightDirection;
            cb._BrightestLightIndex = (uint)brightestLightData.lightIndex;

            cb._CharShadowParams = new Vector4(passData.bias.x, passData.bias.y, 0, 0);
            cb._CharShadowViewProjM = passData.projectM * passData.viewM;
            cb._CharShadowOffset0 = new Vector4(-invHalfShadowMapWidth, -invHalfShadowMapHeight, invHalfShadowMapWidth, -invHalfShadowMapHeight);
            cb._CharShadowOffset1 = new Vector4(-invHalfShadowMapWidth, invHalfShadowMapHeight, invHalfShadowMapWidth, invHalfShadowMapHeight);
            cb._CharShadowmapSize = new Vector4(invShadowMapWidth, invShadowMapHeight, s_TextureSize[0], s_TextureSize[1]);
            
            invShadowMapWidth = 1.0f / TransparentShadowPass.s_TextureSize[0];
            invShadowMapHeight = 1.0f / TransparentShadowPass.s_TextureSize[1];
            cb._CharTransparentShadowmapSize = new Vector4(invShadowMapWidth, invShadowMapHeight, TransparentShadowPass.s_TextureSize[0], TransparentShadowPass.s_TextureSize[1]);
            
            var rcpMaxBoundSize = 1.0f / CharacterShadowUtils.shadowCamera.maxBoundSize;
            cb._CharShadowCascadeParams = new Vector4(passData.cascadeMaxDistance, passData.cascadeResolutionScale, softShadowSamples, rcpMaxBoundSize);
            cb._UseBrightestLight = passData.useBrightestLight ? 1u : 0u;
            cb._IsBrightestLightMain = brightestLightData.isMainLight ? 1u : 0u;
            cb._MaxToonBrightness = passData.maxToonBrightness;
        }

        private static void UpdateConstantBuffer(ref ToonCharacterShadow cb, PassData passData, ref RenderingData renderingData)
        {
            CharacterShadowUtils.GetBrightestLightData(ref renderingData, passData.useBrightestLight, passData.followLightLayer, out var brightestLightData);
            UpdateConstantBuffer_Internal(ref cb, passData, brightestLightData);
        }

        private RenderTextureDescriptor GetRenderTextureDescriptor()
        {
            var descriptor = new RenderTextureDescriptor(s_TextureSize[0], s_TextureSize[1], RenderTextureFormat.RG32, 0);
            descriptor.dimension = TextureDimension.Tex2D;
            descriptor.sRGB = false;
            descriptor.depthStencilFormat = GraphicsFormat.None;
            descriptor.msaaSamples = 1;
            return descriptor;
        }
        
        private class PassData
        {
            public bool enabled;
            public Matrix4x4 viewM;
            public Matrix4x4 projectM;
            public float maxToonBrightness;
            public Vector3 bias;
            public bool useBrightestLight;
            public LayerMask followLightLayer;
            public float cascadeMaxDistance;
            public float cascadeResolutionScale;

#if UNITY_6000_0_OR_NEWER
            // RenderGraph Path
            public RendererListHandle rendererList;
            public ToonCharacterShadow cb;
#endif
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Allocate Char Shadowmap
            RenderingUtils.ReAllocateIfNeeded(ref m_CharShadowRT, GetRenderTextureDescriptor(), FilterMode.Bilinear, name:"CharShadowMap");
            cmd.SetGlobalTexture(ShaderIDs._CharShadowMap, m_CharShadowRT);
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
                var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, ref renderingData, SortingCriteria.CommonOpaque);
                
                UpdateConstantBuffer(ref m_CBuffer, m_PassData, ref renderingData);
                ConstantBuffer.PushGlobal(cmd, m_CBuffer, ShaderIDs._ToonCharacterShadow);
                CoreUtils.SetRenderTarget(cmd, m_CharShadowRT, ClearFlag.Color, 0, CubemapFace.Unknown, 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                
#if UNITY_2021_3
                var rendererListDesc = new UnityEngine.Rendering.RendererUtils.RendererListDesc(k_ShaderTagId, renderingData.cullResults, renderingData.cameraData.camera);
                rendererListDesc.sortingCriteria = SortingCriteria.CommonOpaque;
                rendererListDesc.renderQueueRange = RenderQueueRange.opaque;
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
        private static void UpdateConstantBuffer(ref ToonCharacterShadow cb, PassData passData, UniversalLightData lightData)
        {
            CharacterShadowUtils.GetBrightestLightData(lightData, passData.useBrightestLight, passData.followLightLayer, out var brightestLightData);
            UpdateConstantBuffer_Internal(ref cb, passData, brightestLightData);
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
            var drawSettings = RenderingUtils.CreateDrawingSettings(k_ShaderTagId, renderingData, cameraData, lightData, SortingCriteria.CommonOpaque);
            TextureHandle output = UniversalRenderer.CreateRenderGraphTexture(renderGraph, GetRenderTextureDescriptor(), "CharShadowMap", true, FilterMode.Bilinear);
            
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Character Shadow", out var passData, m_ProfilingSampler))
            {
                builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(output, 0);
                
                var param = new RendererListParams(renderingData.cullResults, drawSettings, new FilteringSettings(RenderQueueRange.opaque));
                passData.rendererList = renderGraph.CreateRendererList(param);
                builder.UseRendererList(passData.rendererList);
                
                if (output.IsValid())
                    builder.SetGlobalTextureAfterPass(output, ShaderIDs._CharShadowMap);
                
                passData.viewM = m_PassData.viewM;
                passData.projectM = m_PassData.projectM;
                passData.bias = m_PassData.bias;
                passData.useBrightestLight = m_PassData.useBrightestLight;
                passData.followLightLayer = m_PassData.followLightLayer;
                passData.enabled = m_PassData.enabled;
                passData.cascadeMaxDistance = m_PassData.cascadeMaxDistance;
                passData.cascadeResolutionScale = m_PassData.cascadeResolutionScale;
                passData.maxToonBrightness = m_PassData.maxToonBrightness;
                passData.cb = new ToonCharacterShadow();
                UpdateConstantBuffer(ref passData.cb, passData, lightData);
                
                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    ConstantBuffer.PushGlobal(data.cb, ShaderIDs._ToonCharacterShadow);
                    ExecutePass(context.cmd, data.rendererList);
                });
            }
        }
#endregion
#endif
    }
}