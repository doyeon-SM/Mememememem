#if UNITY_2021_3
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#nullable disable

namespace PotaToon
{
    public class RenderingUtils
    {
        internal static bool RTHandleNeedsReAlloc(
        RTHandle handle,
        in RenderTextureDescriptor descriptor,
        bool scaled)
        {
            if (handle == null || handle.rt == null)
                return true;
            if (handle.useScaling != scaled)
                return true;
            if (!scaled && (handle.rt.width != descriptor.width || handle.rt.height != descriptor.height))
                return true;

            var rtHandleFormat = (handle.rt.descriptor.depthStencilFormat != GraphicsFormat.None) ? handle.rt.descriptor.depthStencilFormat : handle.rt.descriptor.graphicsFormat;

            return
                rtHandleFormat != descriptor.graphicsFormat ||
                handle.rt.descriptor.dimension != descriptor.dimension ||
                handle.rt.descriptor.enableRandomWrite != descriptor.enableRandomWrite ||
                handle.rt.descriptor.useMipMap != descriptor.useMipMap ||
                handle.rt.descriptor.autoGenerateMips != descriptor.autoGenerateMips ||
                handle.rt.descriptor.msaaSamples != descriptor.msaaSamples ||
                handle.rt.descriptor.bindMS != descriptor.bindMS ||
                handle.rt.descriptor.useDynamicScale != descriptor.useDynamicScale ||
                handle.rt.descriptor.memoryless != descriptor.memoryless;
        }

        internal static void ReAllocateIfNeeded(ref RTHandle handle, RenderTextureDescriptor descriptor, FilterMode filterMode = FilterMode.Point, TextureWrapMode wrapMode = TextureWrapMode.Repeat, string name = "")
        {
            if (RTHandleNeedsReAlloc(handle, descriptor, false))
            {
                if (handle != null && handle.rt != null)
                    RTHandles.Release(handle);
                
                handle = RTHandles.Alloc(descriptor.width, descriptor.height, descriptor.volumeDepth, (DepthBits)descriptor.depthBufferBits, descriptor.graphicsFormat, filterMode: filterMode, name: name);
            }
        }
    
        internal static DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            Camera camera = renderingData.cameraData.camera;
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortingCriteria };
            DrawingSettings settings = new DrawingSettings(shaderTagId, sortingSettings)
            {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                enableDynamicBatching = renderingData.supportsDynamicBatching,

                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
            };
            return settings;
        }
    }

    public static class CoreUtils2021
    {
        private static class ShaderPropertyId
        {
            public static readonly int scaleBias = Shader.PropertyToID("_ScaleBias");
        }
        
        public static void DrawFullScreen(CommandBuffer commandBuffer, Material material,
            MaterialPropertyBlock properties = null, int shaderPassId = 0)
        {
            if (material.passCount <= shaderPassId || material.shader.passCount <= shaderPassId)
                return;
            
            // Assume we only render to the render texture, not the back buffer.
            Vector4 scaleBias = new Vector4(1, 1, 0, 0);
            commandBuffer.SetGlobalVector(ShaderPropertyId.scaleBias, scaleBias);
            commandBuffer.DrawProcedural(Matrix4x4.identity, material, shaderPassId, MeshTopology.Quads, 4, 1, properties);
        }
    }

    public static class Blitter2021
    {
        // Built-in URP Blit shader (still available in 2021)
        static Material s_BlitMat;
        static readonly int _BlitTex       = Shader.PropertyToID("_BlitTexture");
        static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        static readonly int _BlitMipLevel  = Shader.PropertyToID("_BlitMipLevel");

        private static Material BlitMat
        {
            get
            {
                if (s_BlitMat == null)
                    s_BlitMat = CoreUtils.CreateEngineMaterial("Hidden/Universal/CoreBlit");
                return s_BlitMat;
            }
        }

        /// <summary>
        /// Unity 2021 / URP 12.x version: uses RenderTargetIdentifier instead of RTHandle.
        /// viewportScale defaults to (1,1) when source and destination have the same size.
        /// Pass your own scale if needed.
        /// </summary>
        internal static void BlitCameraTexture(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RTHandle destination,
            RenderBufferLoadAction loadAction = RenderBufferLoadAction.Load,
            RenderBufferStoreAction storeAction = RenderBufferStoreAction.Store,
            float mipLevel = 0.0f,
            Vector2? viewportScaleOverride = null,
            Material material = null,
            int pass = 0)
        {
            // In 2021, when camera target and RT have the same resolution, this is (1,1)
            Vector2 viewportScale = viewportScaleOverride ?? Vector2.one;

            // Set the target (also sets the correct camera viewport when called inside a URP pass)
            CoreUtils.SetRenderTarget(cmd, destination, loadAction, storeAction, ClearFlag.None, Color.clear);

            // Equivalent to BlitTexture
            BlitTexture(cmd, source, viewportScale, mipLevel, material, pass);
        }

        internal static void BlitTexture(CommandBuffer cmd, RTHandle source, Vector2 viewportScale, Material material, int pass)
        {
            BlitTexture(cmd, source, viewportScale, 0f, material, pass);
        }

        // RenderTargetIdentifier version of BlitTexture (fullscreen triangle draw)
        private static void BlitTexture(CommandBuffer cmd, RenderTargetIdentifier source, Vector2 viewportScale, float mipLevel, Material material, int pass)
        {
            // Bind required shader parameters
            var targetMaterial = material ?? BlitMat;
            if (targetMaterial.passCount <= pass || targetMaterial.shader.passCount <= pass)
                return;
            
            cmd.SetGlobalTexture(_BlitTex, source);
            targetMaterial.SetVector(_BlitScaleBias, new Vector4(viewportScale.x, viewportScale.y, 0, 0));
            targetMaterial.SetFloat(_BlitMipLevel, mipLevel);

            // Draw fullscreen
            if (material == null)
            {
                targetMaterial.EnableKeyword("_USE_DRAW_PROCEDURAL");
                cmd.DrawProcedural(Matrix4x4.identity, targetMaterial, pass, MeshTopology.Quads, 4, 1, null);
            }
            else
            {
                CoreUtils.DrawFullScreen(cmd, targetMaterial, null, pass);
            }
        }
    }
}
#endif