using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
#endif
using Utils = PotaToon.PotaToonPostProcessUtils;

namespace PotaToon
{
    public class PotaToonPostProcessPass : ScriptableRenderPass
    {
        private class PostProcessData
        {
            public bool enabled;
            public bool screenOutline;
            public bool screenOutlineExcludeInnerLines;
            public Color screenOutlineColor;
            public float screenOutlineThickness;
            public float screenOutlineEdgeStrength;
            public bool gammaAdjust;
            public PotaToonToneMapping toneMappingMode;
            public HableCurve hableCurve;
            public float postExposure;  // Brightness
            public Vector4 hueSatCon;
            public Color colorFilter;
            public Vector3 lmsColorBalance;
            public float screenRimWidth;
            public Color screenRimColor;
        }
        
        private struct BloomData
        {
            public bool enabled;
            public float threshold;
            public float intensity;
            public float scatter;
            public float clamp;
            public Color tint;
            public bool highQualityFiltering;
            public BloomDownscaleMode downscale;
            public int maxIterations;
            public Texture dirtTexture;
            public float dirtIntensity;

            public void SetBloomDataForCharacter(PotaToon volume)
            {
                enabled = volume.charBloom.value && volume.intensity.value > 0;
                threshold = volume.threshold.value;
                intensity = volume.intensity.value;
                scatter = volume.scatter.value;
                clamp = volume.clamp.value;
                tint = volume.tint.value;
                highQualityFiltering = volume.highQualityFiltering.value;
                downscale = volume.downscale.value;
                maxIterations = volume.maxIterations.value;
                dirtTexture = volume.dirtTexture.value;
                dirtIntensity = volume.dirtIntensity.value;
            }
            
            public void SetBloomDataForEnvironment(PotaToon volume)
            {
                enabled = volume.envBloom.value && volume.envBloomIntensity.value > 0;
                threshold = volume.envBloomThreshold.value;
                intensity = volume.envBloomIntensity.value;
                scatter = volume.envBloomScatter.value;
                clamp = volume.envBloomClamp.value;
                tint = volume.envBloomTint.value;
                highQualityFiltering = volume.envBloomHighQualityFiltering.value;
                downscale = volume.envBloomDownscale.value;
                maxIterations = volume.envBloomMaxIterations.value;
                dirtTexture = volume.envBloomDirtTexture.value;
                dirtIntensity = volume.envBloomDirtIntensity.value;
            }
        }
        
        private static class ShaderConstants
        {
            public static readonly int _Params = Shader.PropertyToID("_Params");
            public static readonly int _SourceTexLowMip = Shader.PropertyToID("_SourceTexLowMip");
            public static readonly int _Bloom_Params = Shader.PropertyToID("_Bloom_Params");
            public static readonly int _Bloom_Texture = Shader.PropertyToID("_Bloom_Texture");
            public static readonly int _LensDirt_Texture = Shader.PropertyToID("_LensDirt_Texture");
            public static readonly int _LensDirt_Params = Shader.PropertyToID("_LensDirt_Params");
            public static readonly int _LensDirt_Intensity = Shader.PropertyToID("_LensDirt_Intensity");

            public static int[] _BloomMipUp;
            public static int[] _BloomMipDown;
        }

        private static class CustomShaderKeywordStrings
        {
            public const string PotaToonCharacterBloom = "_POTA_TOON_CHARACTER_BLOOM";
        }

        private static class ShaderPassIDs
        {
            public static readonly int character = 0;
            public static readonly int environment = 1;
            public static readonly int screenOutline = 2;
            public static readonly int screenRim = 3;
        }
        
        private const int k_MaxPyramidSize = 16;

        private ProfilingSampler[] m_ProfilingSamplers = new ProfilingSampler[2];
        private Material m_CharMaterial;
        private Material m_EnvMaterial;
        private MaterialPropertyBlock m_PropertyBlock;
        private Texture m_TonyMcMapfaceTexture;
        private RTHandle m_CopiedColor;
        private PostProcessData m_CharPostProcessData;
        private PostProcessData m_EnvPostProcessData;
        private HableCurve m_CharHableCurve;
        private HableCurve m_EnvHableCurve;
        internal static HableCurve s_GTHableCurve;
        private RenderTextureDescriptor m_Descriptor;
        private readonly GraphicsFormat m_DefaultColorFormat;   // The default format for post-processing, follows back-buffer format in URP.
        
        private BloomData m_CharBloom;
        private BloomData m_EnvBloom;
        private RTHandle[] m_BloomMipDown;
        private RTHandle[] m_BloomMipUp;
        private Material m_CharBloomMaterial;
        private Material m_EnvBloomMaterial;
        private Material[] m_CharBloomUpsample;
        private Material[] m_EnvBloomUpsample;
#if UNITY_6000_0_OR_NEWER
        private TextureHandle[] _BloomMipUp;
        private TextureHandle[] _BloomMipDown;
        private BloomMaterialParams m_BloomParamsPrev;
#endif

        public PotaToonPostProcessPass(string featureName)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            var uberPostMat = Resources.Load<Material>("PotaToonUberPost");
            var bloomMat = Resources.Load<Material>("PotaToonBloom");
            var uberPostShader = uberPostMat != null ? uberPostMat.shader : Shader.Find("Hidden/Universal Render Pipeline/UberPost");
            var bloomShader = bloomMat != null ? bloomMat.shader : Shader.Find("Hidden/Universal Render Pipeline/Bloom");
            m_CharMaterial = Utils.Load(uberPostShader);
            m_EnvMaterial = Utils.Load(uberPostShader);
            m_TonyMcMapfaceTexture = Resources.Load<Texture>("TonyMcMapface");
            m_ProfilingSamplers[0] = new ProfilingSampler(featureName);
            m_ProfilingSamplers[1] = new ProfilingSampler("PotaToon Copy Color");
            m_PropertyBlock = new MaterialPropertyBlock();
            m_CharPostProcessData = new PostProcessData();
            m_EnvPostProcessData = new PostProcessData();
            m_CharHableCurve = new HableCurve();
            m_EnvHableCurve = new HableCurve();
            s_GTHableCurve = new HableCurve();
            s_GTHableCurve.Init(0f, 0.312f, 0.532f, 2.136752f, 0f, 1.15f);
            
            // Bloom - Copied from URP
            ShaderConstants._BloomMipUp = new int[k_MaxPyramidSize];
            ShaderConstants._BloomMipDown = new int[k_MaxPyramidSize];
            m_BloomMipUp = new RTHandle[k_MaxPyramidSize];
            m_BloomMipDown = new RTHandle[k_MaxPyramidSize];
#if UNITY_6000_0_OR_NEWER
            _BloomMipUp = new TextureHandle[k_MaxPyramidSize];
            _BloomMipDown = new TextureHandle[k_MaxPyramidSize];
#endif

            for (int i = 0; i < k_MaxPyramidSize; i++)
            {
                ShaderConstants._BloomMipUp[i] = Shader.PropertyToID("_BloomMipUp" + i);
                ShaderConstants._BloomMipDown[i] = Shader.PropertyToID("_BloomMipDown" + i);
                // Get name, will get Allocated with descriptor later
                m_BloomMipUp[i] = RTHandles.Alloc(ShaderConstants._BloomMipUp[i], name: "_BloomMipUp" + i);
                m_BloomMipDown[i] = RTHandles.Alloc(ShaderConstants._BloomMipDown[i], name: "_BloomMipDown" + i);
            }

            m_CharBloomMaterial = Utils.Load(bloomShader);
            m_EnvBloomMaterial = Utils.Load(bloomShader);
            m_CharBloomUpsample = new Material[k_MaxPyramidSize];
            m_EnvBloomUpsample = new Material[k_MaxPyramidSize];
            for (uint i = 0; i < k_MaxPyramidSize; ++i)
            {
                m_CharBloomUpsample[i] = Utils.Load(bloomShader);
                m_EnvBloomUpsample[i] = Utils.Load(bloomShader);
            }

            m_DefaultColorFormat = Utils.GetDefaultColorFormat();
        }

        public void Setup(PotaToon volume)
        {
            m_CharPostProcessData.enabled = volume.charPostProcessing.value;
            m_CharPostProcessData.screenOutline = volume.charScreenOutline.value;
            m_CharPostProcessData.screenOutlineExcludeInnerLines = volume.charScreenOutlineExcludeInnerLines.value;
            m_CharPostProcessData.screenOutlineColor = volume.charScreenOutlineColor.value;
            m_CharPostProcessData.screenOutlineThickness = volume.charScreenOutlineThickness.value;
            m_CharPostProcessData.screenOutlineEdgeStrength = volume.charScreenOutlineEdgeStrength.value;
            m_CharPostProcessData.gammaAdjust = volume.charGammaAdjust.value;
            m_CharPostProcessData.toneMappingMode = volume.charToneMapping.value;
            m_CharPostProcessData.hableCurve = m_CharHableCurve;
            m_CharPostProcessData.hableCurve.Init(
                volume.charToeStrength.value,
                volume.charToeLength.value,
                volume.charShoulderStrength.value,
                volume.charShoulderLength.value,
                volume.charShoulderAngle.value,
                volume.charGamma.value
            );
            m_CharPostProcessData.postExposure = volume.charPostExposure.value;
            m_CharPostProcessData.hueSatCon = new Vector4(volume.charHueShift.value / 360f, volume.charSaturation.value / 100f + 1f, volume.charContrast.value / 100f + 1f, 0f);
            m_CharPostProcessData.colorFilter = volume.charColorFilter.value;
            m_CharPostProcessData.lmsColorBalance = ColorUtils.ColorBalanceToLMSCoeffs(volume.whiteBalanceTemperature.value, volume.whiteBalanceTint.value);
            m_CharPostProcessData.screenRimWidth = volume.screenRimWidth.value * 0.025f;
            m_CharPostProcessData.screenRimColor = volume.screenRimColor.value;
            m_CharPostProcessData.screenRimColor.a = Mathf.Max(1f, CharacterShadowUtils.shadowCamera.maxScreenRimDistance);
            m_EnvPostProcessData.enabled = volume.envPostProcessing.value;
            m_EnvPostProcessData.toneMappingMode = volume.envToneMapping.value;
            m_EnvPostProcessData.hableCurve = m_EnvHableCurve;
            m_EnvPostProcessData.hableCurve.Init(
                volume.envToeStrength.value,
                volume.envToeLength.value,
                volume.envShoulderStrength.value,
                volume.envShoulderLength.value,
                volume.envShoulderAngle.value,
                volume.envGamma.value
            );
            m_EnvPostProcessData.postExposure = volume.envPostExposure.value;
            m_EnvPostProcessData.hueSatCon = new Vector4(volume.envHueShift.value / 360f, volume.envSaturation.value / 100f + 1f, volume.envContrast.value / 100f + 1f, 0f);
            m_EnvPostProcessData.colorFilter = volume.envColorFilter.value;
            m_EnvPostProcessData.lmsColorBalance = ColorUtils.ColorBalanceToLMSCoeffs(volume.envWhiteBalanceTemperature.value, volume.envWhiteBalanceTint.value);
            m_CharBloom.SetBloomDataForCharacter(volume);
            m_EnvBloom.SetBloomDataForEnvironment(volume);
            
            Shader.SetGlobalFloat(ShaderIDs._ScreenRimWidth, m_CharPostProcessData.screenRimWidth);
            Shader.SetGlobalVector(ShaderIDs._ScreenRimColor, m_CharPostProcessData.screenRimColor);
        }

        public void Dispose()
        {
            m_CopiedColor?.Release();
            CoreUtils.Destroy(m_CharMaterial);
            CoreUtils.Destroy(m_EnvMaterial);
            
            CoreUtils.Destroy(m_CharBloomMaterial);
            CoreUtils.Destroy(m_EnvBloomMaterial);
            foreach (var handle in m_BloomMipDown)
                handle?.Release();
            foreach (var handle in m_BloomMipUp)
                handle?.Release();
            for (uint i = 0; i < k_MaxPyramidSize; ++i)
            {
                CoreUtils.Destroy(m_CharBloomUpsample[i]);
                CoreUtils.Destroy(m_EnvBloomUpsample[i]);
            }
        }

#if !UNITY_6000_4_OR_NEWER
    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (!m_CharPostProcessData.enabled && !m_EnvPostProcessData.enabled)
                return;
            
            m_Descriptor = renderingData.cameraData.cameraTargetDescriptor;
            var colorCopyDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
            RenderingUtils.ReAllocateIfNeeded(ref m_CopiedColor, colorCopyDescriptor, name: "_PotaToonTempCopiedColor", filterMode:FilterMode.Bilinear);
        }

    #if UNITY_6000_0_OR_NEWER
        [Obsolete]
    #endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!m_CharPostProcessData.enabled && !m_EnvPostProcessData.enabled)
                return;
            
            var cmd = CommandBufferPool.Get();
#if UNITY_2021_3
            var src = renderingData.cameraData.renderer.cameraColorTarget;
            m_CharMaterial?.EnableKeyword("_USE_DRAW_PROCEDURAL");
            m_EnvMaterial?.EnableKeyword("_USE_DRAW_PROCEDURAL");
#else
            var src = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
            bool characterPostProcessEnabled = m_CharMaterial != null && m_CharPostProcessData.enabled;
            bool environmentPostProcessEnabled = m_EnvMaterial != null && m_EnvPostProcessData.enabled;
            bool screenOutlineEnabled = characterPostProcessEnabled && m_CharPostProcessData.screenOutline;

            using (new ProfilingScope(cmd, m_ProfilingSamplers[0]))
            {
                DisableAllBloomKeywords(m_EnvMaterial);
                if (environmentPostProcessEnabled)
                {
                    if (m_EnvBloom.enabled)
                    {
                        using (new ProfilingScope(cmd, new ProfilingSampler("PotaToon Environment Bloom")))
    #if UNITY_6000_0_OR_NEWER
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, m_EnvMaterial, in m_EnvBloom, renderingData.cameraData.isAlphaOutputEnabled, false, false);
    #else
        #if UNITY_2021_3
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTarget, m_EnvMaterial, in m_EnvBloom, false, false, false);
        #else
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, m_EnvMaterial, in m_EnvBloom, false, false, false);
        #endif
    #endif
                    }
                    
                    using (new ProfilingScope(cmd, m_ProfilingSamplers[0]))
                    {
    #if UNITY_2021_3
                            Blitter2021.BlitCameraTexture(cmd, src, m_CopiedColor);
    #else
                            Blitter.BlitCameraTexture(cmd, src, m_CopiedColor);
    #endif
                            
                            CoreUtils.SetRenderTarget(cmd, src);
                            m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, m_CopiedColor);
                            m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));
                            m_PropertyBlock.SetTexture(ShaderIDs._TonyMcMapfaceLut, m_TonyMcMapfaceTexture);
                            
                            Utils.SetMaterialToneMappingKeywords(m_EnvMaterial, m_EnvPostProcessData.toneMappingMode, m_EnvPostProcessData.hableCurve);
                            m_EnvMaterial.SetFloat(ShaderIDs._PostExposure, m_EnvPostProcessData.postExposure);
                            m_EnvMaterial.SetVector(ShaderIDs._HueSatCon, m_EnvPostProcessData.hueSatCon);
                            m_EnvMaterial.SetColor(ShaderIDs._ColorFilter, m_EnvPostProcessData.colorFilter);
                            m_EnvMaterial.SetVector(ShaderIDs._ColorBalance, m_EnvPostProcessData.lmsColorBalance);
    #if UNITY_2021_3
                            CoreUtils2021.DrawFullScreen(cmd, m_EnvMaterial, m_PropertyBlock, ShaderPassIDs.environment);
    #else
                            CoreUtils.DrawFullScreen(cmd, m_EnvMaterial, m_PropertyBlock, ShaderPassIDs.environment);
    #endif
                    }
                }

                DisableAllBloomKeywords(m_CharMaterial);
                if (characterPostProcessEnabled)
                {
                    if (screenOutlineEnabled)
                    {
                        m_CharMaterial.SetFloat(ShaderIDs._ScreenOutlineThickness, m_CharPostProcessData.screenOutlineThickness);
                        m_CharMaterial.SetFloat(ShaderIDs._ScreenOutlineEdgeStrength, m_CharPostProcessData.screenOutlineEdgeStrength);
                        m_CharMaterial.SetColor(ShaderIDs._ScreenOutlineColor, m_CharPostProcessData.screenOutlineColor);
                        m_CharMaterial.SetKeyword(new LocalKeyword(m_CharMaterial.shader, "_EXCLUDE_INNER_SCREEN_OUTLINES"), m_CharPostProcessData.screenOutlineExcludeInnerLines);
#if UNITY_2021_3
                        CoreUtils2021.DrawFullScreen(cmd, m_CharMaterial, m_PropertyBlock, ShaderPassIDs.screenOutline);
#else
                        CoreUtils.DrawFullScreen(cmd, m_CharMaterial, m_PropertyBlock, ShaderPassIDs.screenOutline);
#endif
                    }
                    
                    if (m_CharBloom.enabled)
                    {
                        using (new ProfilingScope(cmd, new ProfilingSampler("PotaToon Character Bloom")))
        #if UNITY_6000_0_OR_NEWER
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, m_CharMaterial, in m_CharBloom, renderingData.cameraData.isAlphaOutputEnabled, true, environmentPostProcessEnabled && m_EnvBloom.enabled);
        #else
            #if UNITY_2021_3
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTarget, m_CharMaterial, in m_CharBloom, false, true, m_EnvBloom.enabled);
            #else
                            SetupBloom(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, m_CharMaterial, in m_CharBloom, false, true, m_EnvBloom.enabled);
            #endif
        #endif
                    }
                    
                    using (new ProfilingScope(cmd, m_ProfilingSamplers[0]))
                    {
        #if UNITY_2021_3
                        Blitter2021.BlitCameraTexture(cmd, src, m_CopiedColor);
        #else
                        Blitter.BlitCameraTexture(cmd, src, m_CopiedColor);
        #endif
                        
                        CoreUtils.SetRenderTarget(cmd, src);
                        m_PropertyBlock.SetTexture(ShaderIDs._BlitTexture, m_CopiedColor);
                        m_PropertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));
                        m_PropertyBlock.SetTexture(ShaderIDs._TonyMcMapfaceLut, m_TonyMcMapfaceTexture);
                        
                        Utils.SetMaterialToneMappingKeywords(m_CharMaterial, m_CharPostProcessData.toneMappingMode, m_CharPostProcessData.hableCurve);
                        m_CharMaterial.SetFloat(ShaderIDs._CharGammaAdjust, m_CharPostProcessData.gammaAdjust ? 1f : 0f);
                        m_CharMaterial.SetFloat(ShaderIDs._PostExposure, m_CharPostProcessData.postExposure);
                        m_CharMaterial.SetVector(ShaderIDs._HueSatCon, m_CharPostProcessData.hueSatCon);
                        m_CharMaterial.SetColor(ShaderIDs._ColorFilter, m_CharPostProcessData.colorFilter);
                        m_CharMaterial.SetVector(ShaderIDs._ColorBalance, m_CharPostProcessData.lmsColorBalance);
                        m_CharMaterial.SetFloat(ShaderIDs._ScreenRimWidth, m_CharPostProcessData.screenRimWidth);
                        m_CharMaterial.SetVector(ShaderIDs._ScreenRimColor, m_CharPostProcessData.screenRimColor);
    #if UNITY_2021_3
                        CoreUtils2021.DrawFullScreen(cmd, m_CharMaterial, m_PropertyBlock, ShaderPassIDs.character);
    #else
                        CoreUtils.DrawFullScreen(cmd, m_CharMaterial, m_PropertyBlock, ShaderPassIDs.character);
    #endif
                    }
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
#endif
        
#region Bloom
        private void DisableAllBloomKeywords(Material material)
        {
            if (material != null)
            {
                material.DisableKeyword(ShaderKeywordStrings.BloomLQ);
                material.DisableKeyword(ShaderKeywordStrings.BloomHQ);
                material.DisableKeyword(ShaderKeywordStrings.BloomLQDirt);
                material.DisableKeyword(ShaderKeywordStrings.BloomHQDirt);
            }
        }

#if UNITY_2021_3
        private void SetupBloom(CommandBuffer cmd, RenderTargetIdentifier source, Material uberMaterial, in BloomData bloomData, bool enableAlphaOutput, bool isCharacterBloom, bool hasResourcesCreated)
#else
        private void SetupBloom(CommandBuffer cmd, RTHandle source, Material uberMaterial, in BloomData bloomData, bool enableAlphaOutput, bool isCharacterBloom, bool hasResourcesCreated)
#endif
        {
            // Start at half-res
            int downres = 1;
            switch (bloomData.downscale)
            {
                case BloomDownscaleMode.Half:
                    downres = 1;
                    break;
                case BloomDownscaleMode.Quarter:
                    downres = 2;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException();
            }
            int tw = m_Descriptor.width >> downres;
            int th = m_Descriptor.height >> downres;

            // Determine the iteration count
            int maxSize = Mathf.Max(tw, th);
            int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
            int mipCount = Mathf.Clamp(iterations, 1, bloomData.maxIterations);

            // Pre-filtering parameters
            float clamp = bloomData.clamp;
            float threshold = Mathf.GammaToLinearSpace(bloomData.threshold);
            float thresholdKnee = threshold * 0.5f; // Hardcoded soft knee

            // Material setup
            float scatter = Mathf.Lerp(0.05f, 0.95f, bloomData.scatter);
            var bloomMaterial = isCharacterBloom ? m_CharBloomMaterial : m_EnvBloomMaterial;
            bloomMaterial.SetVector(ShaderConstants._Params, new Vector4(scatter, clamp, threshold, thresholdKnee));
            CoreUtils.SetKeyword(bloomMaterial, ShaderKeywordStrings.BloomHQ, bloomData.highQualityFiltering);
#if UNITY_6000_0_OR_NEWER
            CoreUtils.SetKeyword(bloomMaterial, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT, enableAlphaOutput);
#endif

            // Prefilter
            CoreUtils.SetKeyword(bloomMaterial, CustomShaderKeywordStrings.PotaToonCharacterBloom, isCharacterBloom);
            if (!hasResourcesCreated)
            {
                var desc = Utils.GetCompatibleDescriptor(m_Descriptor, tw, th, m_DefaultColorFormat);
                for (int i = 0; i < mipCount; i++)
                {
    #if UNITY_6000_0_OR_NEWER
                    RenderingUtils.ReAllocateHandleIfNeeded(ref m_BloomMipUp[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomMipUp[i].name);
                    RenderingUtils.ReAllocateHandleIfNeeded(ref m_BloomMipDown[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomMipDown[i].name);
    #else
                    RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipUp[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomMipUp[i].name);
                    RenderingUtils.ReAllocateIfNeeded(ref m_BloomMipDown[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: m_BloomMipDown[i].name);
    #endif
                    desc.width = Mathf.Max(1, desc.width >> 1);
                    desc.height = Mathf.Max(1, desc.height >> 1);
                }
            }

#if UNITY_2021_3
            Blitter2021.BlitCameraTexture(cmd, source, m_BloomMipDown[0], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material:bloomMaterial, pass:0);
#else
            Blitter.BlitCameraTexture(cmd, source, m_BloomMipDown[0], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 0);
#endif

            // Downsample - gaussian pyramid
            var lastDown = m_BloomMipDown[0];
            for (int i = 1; i < mipCount; i++)
            {
                // Classic two pass gaussian blur - use mipUp as a temporary target
                //   First pass does 2x downsampling + 9-tap gaussian
                //   Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
#if UNITY_2021_3
                Blitter2021.BlitCameraTexture(cmd, lastDown, m_BloomMipUp[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material:bloomMaterial, pass:1);
                Blitter2021.BlitCameraTexture(cmd, m_BloomMipUp[i], m_BloomMipDown[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material:bloomMaterial, pass:2);
#else
                Blitter.BlitCameraTexture(cmd, lastDown, m_BloomMipUp[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 1);
                Blitter.BlitCameraTexture(cmd, m_BloomMipUp[i], m_BloomMipDown[i], RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 2);
#endif

                lastDown = m_BloomMipDown[i];
            }

            // Upsample (bilinear by default, HQ filtering does bicubic instead
            for (int i = mipCount - 2; i >= 0; i--)
            {
                var lowMip = (i == mipCount - 2) ? m_BloomMipDown[i + 1] : m_BloomMipUp[i + 1];
                var highMip = m_BloomMipDown[i];
                var dst = m_BloomMipUp[i];

                cmd.SetGlobalTexture(ShaderConstants._SourceTexLowMip, lowMip);
#if UNITY_2021_3
                Blitter2021.BlitCameraTexture(cmd, highMip, dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material:bloomMaterial, pass:3);
#else
                Blitter.BlitCameraTexture(cmd, highMip, dst, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, bloomMaterial, 3);
#endif
            }

            // Setup bloom on uber
            var tint = bloomData.tint.linear;
            var luma = ColorUtils.Luminance(tint);
            tint = luma > 0f ? tint * (1f / luma) : Color.white;

            var bloomParams = new Vector4(bloomData.intensity, tint.r, tint.g, tint.b);
            uberMaterial.SetVector(ShaderConstants._Bloom_Params, bloomParams);

            cmd.SetGlobalTexture(ShaderConstants._Bloom_Texture, m_BloomMipUp[0]);

            // Setup lens dirtiness on uber
            // Keep the aspect ratio correct & center the dirt texture, we don't want it to be
            // stretched or squashed
            var dirtTexture = bloomData.dirtTexture == null ? Texture2D.blackTexture : bloomData.dirtTexture;
            float dirtRatio = dirtTexture.width / (float)dirtTexture.height;
            float screenRatio = m_Descriptor.width / (float)m_Descriptor.height;
            var dirtScaleOffset = new Vector4(1f, 1f, 0f, 0f);
            float dirtIntensity = bloomData.dirtIntensity;

            if (dirtRatio > screenRatio)
            {
                dirtScaleOffset.x = screenRatio / dirtRatio;
                dirtScaleOffset.z = (1f - dirtScaleOffset.x) * 0.5f;
            }
            else if (screenRatio > dirtRatio)
            {
                dirtScaleOffset.y = dirtRatio / screenRatio;
                dirtScaleOffset.w = (1f - dirtScaleOffset.y) * 0.5f;
            }

            uberMaterial.SetVector(ShaderConstants._LensDirt_Params, dirtScaleOffset);
            uberMaterial.SetFloat(ShaderConstants._LensDirt_Intensity, dirtIntensity);
            uberMaterial.SetTexture(ShaderConstants._LensDirt_Texture, dirtTexture);

            // Keyword setup - a bit convoluted as we're trying to save some variants in Uber...
            if (bloomData.highQualityFiltering)
                uberMaterial.EnableKeyword(dirtIntensity > 0f ? ShaderKeywordStrings.BloomHQDirt : ShaderKeywordStrings.BloomHQ);
            else
                uberMaterial.EnableKeyword(dirtIntensity > 0f ? ShaderKeywordStrings.BloomLQDirt : ShaderKeywordStrings.BloomLQ);
        }
#endregion
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        private class PassData
        {
            public Material charMaterial;
            public Material envMaterial;
            public MaterialPropertyBlock propertyBlock;
            public TextureHandle cameraColor;
            public Texture tonyMcMapfaceLut;
            public PostProcessData charPostProcessData;
            public PostProcessData envPostProcessData;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!m_CharPostProcessData.enabled && !m_EnvPostProcessData.enabled)
                return;
            
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            
            m_Descriptor = cameraData.cameraTargetDescriptor;
            var colorCopyDescriptor = cameraData.cameraTargetDescriptor;
            colorCopyDescriptor.depthBufferBits = (int)DepthBits.None;
            TextureHandle copiedColor = UniversalRenderer.CreateRenderGraphTexture(renderGraph, colorCopyDescriptor, "_PotaToonTempCopiedColor", true);
            bool characterPostProcessEnabled = m_CharMaterial != null && m_CharPostProcessData.enabled;
            bool environmentPostProcessEnabled = m_EnvMaterial != null && m_EnvPostProcessData.enabled;
            bool screenOutlineEnabled = characterPostProcessEnabled && m_CharPostProcessData.screenOutline;

            if (environmentPostProcessEnabled || !screenOutlineEnabled)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Copy Color", out var passData, m_ProfilingSamplers[1]))
                {
                    builder.SetRenderAttachment(copiedColor, 0);
                    builder.UseTexture(resourceData.cameraColor);
                    passData.cameraColor = resourceData.cameraColor;
                    
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.cameraColor, Vector2.one, 0, false);
                    });
                }
            }
            
            // Env PP
            DisableAllBloomKeywords(m_EnvMaterial);
            if (environmentPostProcessEnabled)
            {
                if (m_EnvBloom.enabled)
                {
                    TextureHandle currentSource = resourceData.activeColorTexture;
                    RenderBloomTexture(renderGraph, in currentSource, out var BloomTexture, in m_EnvBloom, cameraData.isAlphaOutputEnabled, false, false);
                    UberPostSetupBloomPass(renderGraph, in BloomTexture, in m_EnvBloom, m_EnvMaterial);
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Environment", out var passData, m_ProfilingSamplers[0]))
                {
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderAttachment(resourceData.cameraColor, 0);
                    builder.UseTexture(copiedColor);
                    passData.envMaterial = m_EnvMaterial;
                    passData.propertyBlock = m_PropertyBlock;
                    passData.cameraColor = copiedColor;
                    passData.tonyMcMapfaceLut = m_TonyMcMapfaceTexture;
                    passData.envPostProcessData = m_EnvPostProcessData;

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.propertyBlock.SetTexture(ShaderIDs._BlitTexture, data.cameraColor);
                        data.propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));
                        data.propertyBlock.SetTexture(ShaderIDs._TonyMcMapfaceLut, data.tonyMcMapfaceLut);

                        Utils.SetMaterialToneMappingKeywords(data.envMaterial, data.envPostProcessData.toneMappingMode, data.envPostProcessData.hableCurve);
                        data.envMaterial.SetFloat(ShaderIDs._PostExposure, data.envPostProcessData.postExposure);
                        data.envMaterial.SetVector(ShaderIDs._HueSatCon, data.envPostProcessData.hueSatCon);
                        data.envMaterial.SetColor(ShaderIDs._ColorFilter, data.envPostProcessData.colorFilter);
                        data.envMaterial.SetVector(ShaderIDs._ColorBalance, data.envPostProcessData.lmsColorBalance);
                        CoreUtils.DrawFullScreen(context.cmd, data.envMaterial, data.propertyBlock, ShaderPassIDs.environment);
                    });
                }

                if (characterPostProcessEnabled && !screenOutlineEnabled)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Copy Color", out var passData, m_ProfilingSamplers[1]))
                    {
                        builder.SetRenderAttachment(copiedColor, 0);
                        builder.UseTexture(resourceData.cameraColor);
                        passData.cameraColor = resourceData.cameraColor;
                    
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(context.cmd, data.cameraColor, Vector2.one, 0, false);
                        });
                    }
                }
            }
            
            // Char PP
            DisableAllBloomKeywords(m_CharMaterial);
            if (characterPostProcessEnabled)
            {
                if (screenOutlineEnabled)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Character Screen Outline", out var passData, m_ProfilingSamplers[0]))
                    {
                        builder.SetRenderAttachment(resourceData.cameraColor, 0);
                        passData.charMaterial = m_CharMaterial;
                        passData.propertyBlock = m_PropertyBlock;
                        passData.charPostProcessData = m_CharPostProcessData;
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            data.charMaterial.SetFloat(ShaderIDs._ScreenOutlineThickness, data.charPostProcessData.screenOutlineThickness);
                            data.charMaterial.SetFloat(ShaderIDs._ScreenOutlineEdgeStrength, data.charPostProcessData.screenOutlineEdgeStrength);
                            data.charMaterial.SetColor(ShaderIDs._ScreenOutlineColor, data.charPostProcessData.screenOutlineColor);
                            data.charMaterial.SetKeyword(new LocalKeyword(data.charMaterial.shader, "_EXCLUDE_INNER_SCREEN_OUTLINES"), data.charPostProcessData.screenOutlineExcludeInnerLines);
                            CoreUtils.DrawFullScreen(context.cmd, data.charMaterial, data.propertyBlock, ShaderPassIDs.screenOutline);
                        });
                    }
                    
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Copy Color", out var passData, m_ProfilingSamplers[1]))
                    {
                        builder.SetRenderAttachment(copiedColor, 0);
                        builder.UseTexture(resourceData.cameraColor);
                        passData.cameraColor = resourceData.cameraColor;
                    
                        builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                        {
                            Blitter.BlitTexture(context.cmd, data.cameraColor, Vector2.one, 0, false);
                        });
                    }
                }
                
                if (m_CharBloom.enabled)
                {
                    TextureHandle currentSource = resourceData.activeColorTexture;
                    RenderBloomTexture(renderGraph, in currentSource, out var BloomTexture, in m_CharBloom, cameraData.isAlphaOutputEnabled, true, environmentPostProcessEnabled && m_EnvBloom.enabled);
                    UberPostSetupBloomPass(renderGraph, in BloomTexture, in m_CharBloom, m_CharMaterial);
                }
                
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("[PotaToon] Post Process - Character", out var passData, m_ProfilingSamplers[0]))
                {
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderAttachment(resourceData.cameraColor, 0);
                    builder.UseTexture(copiedColor);
                    passData.charMaterial = m_CharMaterial;
                    passData.envMaterial = m_EnvMaterial;
                    passData.propertyBlock = m_PropertyBlock;
                    passData.cameraColor = copiedColor;
                    passData.tonyMcMapfaceLut = m_TonyMcMapfaceTexture;
                    passData.charPostProcessData = m_CharPostProcessData;
                    passData.envPostProcessData = m_EnvPostProcessData;
                    
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        data.propertyBlock.SetTexture(ShaderIDs._BlitTexture, data.cameraColor);
                        data.propertyBlock.SetVector(ShaderIDs._BlitScaleBias, new Vector4(1, 1, 0, 0));
                        data.propertyBlock.SetTexture(ShaderIDs._TonyMcMapfaceLut, data.tonyMcMapfaceLut);

                        Utils.SetMaterialToneMappingKeywords(data.charMaterial, data.charPostProcessData.toneMappingMode, data.charPostProcessData.hableCurve);
                        data.charMaterial.SetFloat(ShaderIDs._CharGammaAdjust, data.charPostProcessData.gammaAdjust ? 1f : 0f);
                        data.charMaterial.SetFloat(ShaderIDs._PostExposure, data.charPostProcessData.postExposure);
                        data.charMaterial.SetVector(ShaderIDs._HueSatCon, data.charPostProcessData.hueSatCon);
                        data.charMaterial.SetColor(ShaderIDs._ColorFilter, data.charPostProcessData.colorFilter);
                        data.charMaterial.SetVector(ShaderIDs._ColorBalance, data.charPostProcessData.lmsColorBalance);
                        data.charMaterial.SetFloat(ShaderIDs._ScreenRimWidth, data.charPostProcessData.screenRimWidth);
                        data.charMaterial.SetVector(ShaderIDs._ScreenRimColor, data.charPostProcessData.screenRimColor);
                        CoreUtils.DrawFullScreen(context.cmd, data.charMaterial, data.propertyBlock, ShaderPassIDs.character);
                    });
                }
            }
        }
        
        #region Bloom
        private class UberSetupBloomPassData
        {
            internal Vector4 bloomParams;
            internal Vector4 dirtScaleOffset;
            internal float dirtIntensity;
            internal Texture dirtTexture;
            internal bool highQualityFilteringValue;
            internal TextureHandle bloomTexture;
            internal Material uberMaterial;
        }

        private void UberPostSetupBloomPass(RenderGraph rendergraph, in TextureHandle bloomTexture, in BloomData bloomData, Material uberMaterial)
        {
            using (var builder = rendergraph.AddRasterRenderPass<UberSetupBloomPassData>("[PotaToon] Setup Bloom Post Processing", out var passData, new ProfilingSampler("RG_UberPostSetupBloomPass")))
            {
                // Setup bloom on uber
                var tint = bloomData.tint.linear;
                var luma = ColorUtils.Luminance(tint);
                tint = luma > 0f ? tint * (1f / luma) : Color.white;
                var bloomParams = new Vector4(bloomData.intensity, tint.r, tint.g, tint.b);

                // Setup lens dirtiness on uber
                // Keep the aspect ratio correct & center the dirt texture, we don't want it to be
                // stretched or squashed
                var dirtTexture = bloomData.dirtTexture == null ? Texture2D.blackTexture : bloomData.dirtTexture;
                float dirtRatio = dirtTexture.width / (float)dirtTexture.height;
                float screenRatio = m_Descriptor.width / (float)m_Descriptor.height;
                var dirtScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                float dirtIntensity = bloomData.dirtIntensity;

                if (dirtRatio > screenRatio)
                {
                    dirtScaleOffset.x = screenRatio / dirtRatio;
                    dirtScaleOffset.z = (1f - dirtScaleOffset.x) * 0.5f;
                }
                else if (screenRatio > dirtRatio)
                {
                    dirtScaleOffset.y = dirtRatio / screenRatio;
                    dirtScaleOffset.w = (1f - dirtScaleOffset.y) * 0.5f;
                }

                passData.bloomParams = bloomParams;
                passData.dirtScaleOffset = dirtScaleOffset;
                passData.dirtIntensity = dirtIntensity;
                passData.dirtTexture = dirtTexture;
                passData.highQualityFilteringValue = bloomData.highQualityFiltering;

                passData.bloomTexture = bloomTexture;
                builder.UseTexture(bloomTexture, AccessFlags.Read);
                passData.uberMaterial = uberMaterial;

                // TODO RENDERGRAPH: properly setup dependencies between passes
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (UberSetupBloomPassData data, RasterGraphContext context) =>
                {
                    var uberMaterial = data.uberMaterial;
                    uberMaterial.SetVector(ShaderConstants._Bloom_Params, data.bloomParams);
                    uberMaterial.SetVector(ShaderConstants._LensDirt_Params, data.dirtScaleOffset);
                    uberMaterial.SetFloat(ShaderConstants._LensDirt_Intensity, data.dirtIntensity);
                    uberMaterial.SetTexture(ShaderConstants._LensDirt_Texture, data.dirtTexture);

                    // Keyword setup - a bit convoluted as we're trying to save some variants in Uber...
                    if (data.highQualityFilteringValue)
                        uberMaterial.EnableKeyword(data.dirtIntensity > 0f ? ShaderKeywordStrings.BloomHQDirt : ShaderKeywordStrings.BloomHQ);
                    else
                        uberMaterial.EnableKeyword(data.dirtIntensity > 0f ? ShaderKeywordStrings.BloomLQDirt : ShaderKeywordStrings.BloomLQ);

                    uberMaterial.SetTexture(ShaderConstants._Bloom_Texture, data.bloomTexture);
                });
            }
        }

        private class BloomPassData
        {
            internal int mipCount;
            internal Material material;
            internal Material[] upsampleMaterials;
            internal TextureHandle sourceTexture;
            internal TextureHandle[] bloomMipUp;
            internal TextureHandle[] bloomMipDown;
            internal bool isCharacterBloom;
        }

        internal struct BloomMaterialParams
        {
            internal Vector4 parameters;
            internal bool highQualityFiltering;
            internal bool enableAlphaOutput;

            internal bool Equals(ref BloomMaterialParams other)
            {
                return parameters == other.parameters &&
                       highQualityFiltering == other.highQualityFiltering &&
                       enableAlphaOutput == other.enableAlphaOutput;
            }
        }

        private void RenderBloomTexture(RenderGraph renderGraph, in TextureHandle source, out TextureHandle destination, in BloomData bloomData, bool enableAlphaOutput, bool isCharacterBloom, bool hasResourcesCreated)
        {
            // Start at half-res
            int downres = 1;
            switch (bloomData.downscale)
            {
                case BloomDownscaleMode.Half:
                    downres = 1;
                    break;
                case BloomDownscaleMode.Quarter:
                    downres = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            //We should set the limit the downres result to ensure we dont turn 1x1 textures, which should technically be valid
            //into 0x0 textures which will be invalid
            int tw = Mathf.Max(1, m_Descriptor.width >> downres);
            int th = Mathf.Max(1, m_Descriptor.height >> downres);

            // Determine the iteration count
            int maxSize = Mathf.Max(tw, th);
            int iterations = Mathf.FloorToInt(Mathf.Log(maxSize, 2f) - 1);
            int mipCount = Mathf.Clamp(iterations, 1, bloomData.maxIterations);

            // Setup
            using(new ProfilingScope(new ProfilingSampler("BloomSetup")))
            {
                // Pre-filtering parameters
                float clamp = bloomData.clamp;
                float threshold = Mathf.GammaToLinearSpace(bloomData.threshold);
                float thresholdKnee = threshold * 0.5f; // Hardcoded soft knee

                // Material setup
                float scatter = Mathf.Lerp(0.05f, 0.95f, bloomData.scatter);

                BloomMaterialParams bloomParams = new BloomMaterialParams();
                bloomParams.parameters = new Vector4(scatter, clamp, threshold, thresholdKnee);
                bloomParams.highQualityFiltering = bloomData.highQualityFiltering;
                bloomParams.enableAlphaOutput = enableAlphaOutput;

                // Setting keywords can be somewhat expensive on low-end platforms.
                // Previous params are cached to avoid setting the same keywords every frame.
                var material = isCharacterBloom ? m_CharBloomMaterial : m_EnvBloomMaterial;
                bool bloomParamsDirty = !m_BloomParamsPrev.Equals(ref bloomParams);
                bool isParamsPropertySet = material.HasProperty(ShaderConstants._Params);
                if (bloomParamsDirty || !isParamsPropertySet)
                {
                    material.SetVector(ShaderConstants._Params, bloomParams.parameters);
                    CoreUtils.SetKeyword(material, ShaderKeywordStrings.BloomHQ, bloomParams.highQualityFiltering);
                    CoreUtils.SetKeyword(material, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT, bloomParams.enableAlphaOutput);

                    // These materials are duplicate just to allow different bloom blits to use different textures.
                    for (uint i = 0; i < k_MaxPyramidSize; ++i)
                    {
                        var materialPyramid = isCharacterBloom ? m_CharBloomUpsample[i] : m_EnvBloomUpsample[i];
                        materialPyramid.SetVector(ShaderConstants._Params, bloomParams.parameters);
                        CoreUtils.SetKeyword(materialPyramid, ShaderKeywordStrings.BloomHQ, bloomParams.highQualityFiltering);
                        CoreUtils.SetKeyword(materialPyramid, ShaderKeywordStrings._ENABLE_ALPHA_OUTPUT, bloomParams.enableAlphaOutput);
                    }

                    m_BloomParamsPrev = bloomParams;
                }

                // Create bloom mip pyramid textures
                if (!hasResourcesCreated)
                {
                    var desc = Utils.GetCompatibleDescriptor(m_Descriptor, tw, th, m_DefaultColorFormat);
                    _BloomMipDown[0] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, m_BloomMipDown[0].name, false, FilterMode.Bilinear);
                    _BloomMipUp[0] = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, m_BloomMipUp[0].name, false, FilterMode.Bilinear);

                    for (int i = 1; i < mipCount; i++)
                    {
                        tw = Mathf.Max(1, tw >> 1);
                        th = Mathf.Max(1, th >> 1);
                        ref TextureHandle mipDown = ref _BloomMipDown[i];
                        ref TextureHandle mipUp = ref _BloomMipUp[i];

                        desc.width = tw;
                        desc.height = th;

                        // NOTE: Reuse RTHandle names for TextureHandles
                        mipDown = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, m_BloomMipDown[i].name, false, FilterMode.Bilinear);
                        mipUp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, m_BloomMipUp[i].name, false, FilterMode.Bilinear);
                    }
                }
            }

            using (var builder = renderGraph.AddUnsafePass<BloomPassData>("[PotaToon] Blit Bloom Mipmaps", out var passData, new ProfilingSampler("PotaToon Bloom")))
            {
                passData.mipCount = mipCount;
                passData.material = isCharacterBloom ? m_CharBloomMaterial : m_EnvBloomMaterial;
                passData.upsampleMaterials = isCharacterBloom ? m_CharBloomUpsample : m_EnvBloomUpsample;
                passData.sourceTexture = source;
                passData.bloomMipDown = _BloomMipDown;
                passData.bloomMipUp = _BloomMipUp;
                passData.isCharacterBloom = isCharacterBloom;

                // TODO RENDERGRAPH: properly setup dependencies between passes
                builder.AllowPassCulling(false);

                builder.UseTexture(source, AccessFlags.Read);
                for (int i = 0; i < mipCount; i++)
                {
                    builder.UseTexture(_BloomMipDown[i], AccessFlags.ReadWrite);
                    builder.UseTexture(_BloomMipUp[i], AccessFlags.ReadWrite);
                }

                builder.SetRenderFunc(static (BloomPassData data, UnsafeGraphContext context) =>
                {
                    // TODO: can't call BlitTexture with unsafe command buffer
                    var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    var material = data.material;
                    int mipCount = data.mipCount;

                    var loadAction = RenderBufferLoadAction.DontCare;   // Blit - always write all pixels
                    var storeAction = RenderBufferStoreAction.Store;    // Blit - always read by then next Blit

                    // Prefilter
                    using(new ProfilingScope(cmd, new ProfilingSampler("RG_BloomPrefilter")))
                    {
                        CoreUtils.SetKeyword(material, CustomShaderKeywordStrings.PotaToonCharacterBloom, data.isCharacterBloom);
                        Blitter.BlitCameraTexture(cmd, data.sourceTexture, data.bloomMipDown[0], loadAction, storeAction, material, 0);
                    }

                    // Downsample - gaussian pyramid
                    // Classic two pass gaussian blur - use mipUp as a temporary target
                    //   First pass does 2x downsampling + 9-tap gaussian
                    //   Second pass does 9-tap gaussian using a 5-tap filter + bilinear filtering
                    using(new ProfilingScope(cmd, new ProfilingSampler("RG_BloomDownsample")))
                    {
                        TextureHandle lastDown = data.bloomMipDown[0];
                        for (int i = 1; i < mipCount; i++)
                        {
                            TextureHandle mipDown = data.bloomMipDown[i];
                            TextureHandle mipUp = data.bloomMipUp[i];

                            Blitter.BlitCameraTexture(cmd, lastDown, mipUp, loadAction, storeAction, material, 1);
                            Blitter.BlitCameraTexture(cmd, mipUp, mipDown, loadAction, storeAction, material, 2);

                            lastDown = mipDown;
                        }
                    }

                    using (new ProfilingScope(cmd, new ProfilingSampler("RG_BloomUpsample")))
                    {
                        // Upsample (bilinear by default, HQ filtering does bicubic instead
                        for (int i = mipCount - 2; i >= 0; i--)
                        {
                            TextureHandle lowMip = (i == mipCount - 2) ? data.bloomMipDown[i + 1] : data.bloomMipUp[i + 1];
                            TextureHandle highMip = data.bloomMipDown[i];
                            TextureHandle dst = data.bloomMipUp[i];

                            // We need a separate material for each upsample pass because setting the low texture mip source
                            // gets overriden by the time the render func is executed.
                            // Material is a reference, so all the blits would share the same material state in the cmdbuf.
                            // NOTE: another option would be to use cmd.SetGlobalTexture().
                            var upMaterial = data.upsampleMaterials[i];
                            upMaterial.SetTexture(ShaderConstants._SourceTexLowMip, lowMip);

                            Blitter.BlitCameraTexture(cmd, highMip, dst, loadAction, storeAction, upMaterial, 3);
                        }
                    }
                });

                destination = passData.bloomMipUp[0];
            }
        }
        #endregion
#endregion
#endif
    }
}