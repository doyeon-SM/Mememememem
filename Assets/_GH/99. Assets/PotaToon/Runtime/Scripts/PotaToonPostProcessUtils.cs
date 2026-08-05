using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace PotaToon
{
    internal static class PotaToonPostProcessUtils
    {
        private static class TonemapKeywords
        {
            public const string _Neutral = "_POTA_TOON_TONEMAP_NEUTRAL";
            public const string _ACES = "_POTA_TOON_TONEMAP_ACES";
            public const string _Filmic = "_POTA_TOON_TONEMAP_FILMIC";
            public const string _Uchimura = "_POTA_TOON_TONEMAP_UCHIMURA";
            public const string _Tony = "_POTA_TOON_TONEMAP_TONY";
            public const string _Custom = "_POTA_TOON_TONEMAP_CUSTOM";
        }
        
        internal static Material Load(Shader shader)
        {
            if (shader == null)
            {
                Debug.LogErrorFormat($"Missing shader. PostProcessing render passes will not execute. Check for missing reference in the renderer resources or restart the editor.");
                return null;
            }
            else if (!shader.isSupported)
            {
                return null;
            }

            return CoreUtils.CreateEngineMaterial(shader);
        }

        internal static GraphicsFormat GetDefaultColorFormat()
        {
            GraphicsFormat output;
            var requestColorFormat = GraphicsFormat.B10G11R11_UFloatPack32;
            var asset = UniversalRenderPipeline.asset;
            if (asset)
#if UNITY_2021_3
                requestColorFormat = MakeRenderTextureGraphicsFormat(asset.supportsHDR, HDRColorBufferPrecision._32Bits, false);
#else
                requestColorFormat = MakeRenderTextureGraphicsFormat(asset.supportsHDR, asset.hdrColorBufferPrecision, false);
#endif
            bool requestHDR = IsHDRFormat(requestColorFormat);

            if (requestHDR)
            {
#if UNITY_6000_0_OR_NEWER
                const GraphicsFormatUsage usage = GraphicsFormatUsage.Blend;
#else
                const FormatUsage usage = FormatUsage.Blend;
#endif
                if (SystemInfo.IsFormatSupported(requestColorFormat, usage))    // Typically, RGBA16Float.
                    output = requestColorFormat;
                else if (SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, usage)) // HDR fallback
                    output = GraphicsFormat.B10G11R11_UFloatPack32;
                else
                    output = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
            }
            else // SDR
            {
                output = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
            }

            return output;
        }
        
        private static bool IsHDRFormat(GraphicsFormat format)
        {
            return format == GraphicsFormat.B10G11R11_UFloatPack32 ||
                   GraphicsFormatUtility.IsHalfFormat(format) ||
                   GraphicsFormatUtility.IsFloatFormat(format);
        }
        
#if UNITY_2021_3
        /// <summary>
        /// The default color buffer format in HDR (only).
        /// Affects camera rendering and postprocessing color buffers.
        /// </summary>
        public enum HDRColorBufferPrecision
        {
            /// <summary> Typically R11G11B10f for faster rendering. Recommend for mobile.
            /// R11G11B10f can cause a subtle blue/yellow banding in some rare cases due to lower precision of the blue component.</summary>
            [Tooltip("Use 32-bits per pixel for HDR rendering.")]
            _32Bits,
            /// <summary>Typically R16G16B16A16f for better quality. Can reduce banding at the cost of memory and performance.</summary>
            [Tooltip("Use 64-bits per pixel for HDR rendering.")]
            _64Bits,
        }
#endif
        
        private static GraphicsFormat MakeRenderTextureGraphicsFormat(bool isHdrEnabled, HDRColorBufferPrecision requestHDRColorBufferPrecision, bool needsAlpha)
        {
            if (isHdrEnabled)
            {
                // TODO: we need a proper format scoring system. Score formats, sort, pick first or pick first supported (if not in score).
                // UUM-41070: We require `Linear | Render` but with the deprecated FormatUsage this was checking `Blend`
                // For now, we keep checking for `Blend` until the performance hit of doing the correct checks is evaluated
#if UNITY_6000_0_OR_NEWER
                if (!needsAlpha && requestHDRColorBufferPrecision != HDRColorBufferPrecision._64Bits && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, GraphicsFormatUsage.Blend))
                    return GraphicsFormat.B10G11R11_UFloatPack32;
                if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Blend))
                    return GraphicsFormat.R16G16B16A16_SFloat;
#else
                if (!needsAlpha && requestHDRColorBufferPrecision != HDRColorBufferPrecision._64Bits && SystemInfo.IsFormatSupported(GraphicsFormat.B10G11R11_UFloatPack32, FormatUsage.Blend))
                    return GraphicsFormat.B10G11R11_UFloatPack32;
                if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Blend))
                    return GraphicsFormat.R16G16B16A16_SFloat;
#endif
                return SystemInfo.GetGraphicsFormat(DefaultFormat.HDR); // This might actually be a LDR format on old devices.
            }

            return SystemInfo.GetGraphicsFormat(DefaultFormat.LDR);
        }
        
        internal static RenderTextureDescriptor GetCompatibleDescriptor(RenderTextureDescriptor desc, int width, int height, GraphicsFormat format, GraphicsFormat depthStencilFormat = GraphicsFormat.None)
        {
            desc.depthStencilFormat = depthStencilFormat;
            desc.msaaSamples = 1;
            desc.width = width;
            desc.height = height;
            desc.graphicsFormat = format;
            return desc;
        }
        
        internal static void SetMaterialToneMappingKeywords(Material material, PotaToonToneMapping toneMappingMode, HableCurve hableCurve)
        {
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._Neutral), toneMappingMode == PotaToonToneMapping.Neutral);
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._ACES), toneMappingMode == PotaToonToneMapping.ACES);
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._Filmic), toneMappingMode == PotaToonToneMapping.Filmic);
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._Uchimura), toneMappingMode == PotaToonToneMapping.Uchimura);
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._Tony), toneMappingMode == PotaToonToneMapping.Tony);
            material.SetKeyword(new LocalKeyword(material.shader, TonemapKeywords._Custom), toneMappingMode == PotaToonToneMapping.GT || toneMappingMode == PotaToonToneMapping.Custom);

            if (toneMappingMode == PotaToonToneMapping.GT)
            {
                var gtHableCurve = PotaToonPostProcessPass.s_GTHableCurve;
                material.SetVector(ShaderIDs._CustomToneCurve, gtHableCurve.uniforms.curve);
                material.SetVector(ShaderIDs._ToeSegmentA, gtHableCurve.uniforms.toeSegmentA);
                material.SetVector(ShaderIDs._ToeSegmentB, gtHableCurve.uniforms.toeSegmentB);
                material.SetVector(ShaderIDs._MidSegmentA, gtHableCurve.uniforms.midSegmentA);
                material.SetVector(ShaderIDs._MidSegmentB, gtHableCurve.uniforms.midSegmentB);
                material.SetVector(ShaderIDs._ShoSegmentA, gtHableCurve.uniforms.shoSegmentA);
                material.SetVector(ShaderIDs._ShoSegmentB, gtHableCurve.uniforms.shoSegmentB);
            }
            else if (toneMappingMode == PotaToonToneMapping.Custom)
            {
                material.SetVector(ShaderIDs._CustomToneCurve, hableCurve.uniforms.curve);
                material.SetVector(ShaderIDs._ToeSegmentA, hableCurve.uniforms.toeSegmentA);
                material.SetVector(ShaderIDs._ToeSegmentB, hableCurve.uniforms.toeSegmentB);
                material.SetVector(ShaderIDs._MidSegmentA, hableCurve.uniforms.midSegmentA);
                material.SetVector(ShaderIDs._MidSegmentB, hableCurve.uniforms.midSegmentB);
                material.SetVector(ShaderIDs._ShoSegmentA, hableCurve.uniforms.shoSegmentA);
                material.SetVector(ShaderIDs._ShoSegmentB, hableCurve.uniforms.shoSegmentB);
            }
        }
    }
}
