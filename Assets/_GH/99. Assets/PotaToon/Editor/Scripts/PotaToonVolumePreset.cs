using UnityEngine;
using UnityEngine.Rendering.Universal;
using PotaToon;

namespace PotaToon.Editor
{
    [CreateAssetMenu(menuName = "PotaToon/Volume Preset", fileName = "PotaToonVolumePreset")]
    internal class PotaToonVolumePreset : ScriptableObject
    {
        // Main
        public PotaToonMode mode = PotaToonMode.Normal;
        public bool transparentShadow = false;
        public LightOffsetMode charShadowDirOffsetMode = LightOffsetMode.Local;
        public Vector3 charShadowDirOffset = Vector3.zero;
        public LayerMask followLayerMask = 0;
        public float maxToonBrightness = 10f;
        public float bias = 1f;
        public float normalBias = 0.1f;
        public PotaToonQuality quality = PotaToonQuality.High;
        public CharShadowMapSize textureScale = CharShadowMapSize.X8;
        public CharShadowMapSize transparentTextureScale = CharShadowMapSize.X8;
        public float shadowCullingDistance = 1.5f;
        public bool oit = false;
        public OITMode oitMode = OITMode.SrcBlend;

        // Character Post Process
        public bool charPostProcessing = false;
        public bool charScreenOutline = false;
        public bool charScreenOutlineExcludeInnerLines = false;
        public Color charScreenOutlineColor = Color.black;
        public float charScreenOutlineThickness = 1f;
        public float charScreenOutlineEdgeStrength = 1f;
        public float charPostExposure = 1f;
        public bool charGammaAdjust = false;
        public PotaToonToneMapping charToneMapping = PotaToonToneMapping.None;
        public float charContrast = 0f;
        public Color charColorFilter = Color.white;
        public float charHueShift = 0f;
        public float charSaturation = 0f;
        public float whiteBalanceTemperature = 0f;
        public float whiteBalanceTint = 0f;
        public float screenRimWidth = 0f;
        public Color screenRimColor = Color.white;

        // Environment Post Process
        public bool envPostProcessing = false;
        public PotaToonToneMapping envToneMapping = PotaToonToneMapping.None;
        public float envPostExposure = 1f;
        public float envContrast = 0f;
        public Color envColorFilter = Color.white;
        public float envHueShift = 0f;
        public float envSaturation = 0f;
        public float envWhiteBalanceTemperature = 0f;
        public float envWhiteBalanceTint = 0f;
        
        // Custom Tonemapping Curve
        public float charToeStrength = 0f;
        public float charToeLength = 0.5f;
        public float charShoulderStrength = 0f;
        public float charShoulderLength = 0.5f;
        public float charShoulderAngle = 0f;
        public float charGamma = 1f;
        
        public float envToeStrength = 0f;
        public float envToeLength = 0.5f;
        public float envShoulderStrength = 0f;
        public float envShoulderLength = 0.5f;
        public float envShoulderAngle = 0f;
        public float envGamma = 1f;

        // Bloom
        public bool charBloom = false;
        public float threshold = 0.9f;
        public float intensity = 0f;
        public float scatter = 0.7f;
        public float clamp = 65472f;
        public Color tint = Color.white;
        public bool highQualityFiltering = false;
        public BloomDownscaleMode downscale = BloomDownscaleMode.Half;
        public int maxIterations = 6;
        public Texture dirtTexture = null;
        public float dirtIntensity = 0f;
        
        public bool envBloom = false;
        public float envBloomThreshold = 0.9f;
        public float envBloomIntensity = 0f;
        public float envBloomScatter = 0.7f;
        public float envBloomClamp = 65472f;
        public Color envBloomTint = Color.white;
        public bool envBloomHighQualityFiltering = false;
        public BloomDownscaleMode envBloomDownscale = BloomDownscaleMode.Half;
        public int envBloomMaxIterations = 6;
        public Texture envBloomDirtTexture = null;
        public float envBloomDirtIntensity = 0f;

        public void ApplyTo(PotaToon target)
        {
            target.mode.value = mode;
            target.transparentShadow.value = transparentShadow;
            target.charShadowDirOffsetMode.value = charShadowDirOffsetMode;
            target.charShadowDirOffset.value = charShadowDirOffset;
            target.followLayerMask.value = followLayerMask;
            target.maxToonBrightness.value = maxToonBrightness;
            target.bias.value = bias;
            target.normalBias.value = normalBias;
            target.quality.value = quality;
            target.textureScale.value = textureScale;
            target.transparentTextureScale.value = transparentTextureScale;
            target.shadowCullingDistance.value = shadowCullingDistance;
            target.oit.value = oit;
            target.oitMode.value = oitMode;

            target.charPostProcessing.value = charPostProcessing;
            target.charScreenOutline.value = charScreenOutline;
            target.charScreenOutlineExcludeInnerLines.value = charScreenOutlineExcludeInnerLines;
            target.charScreenOutlineColor.value = charScreenOutlineColor;
            target.charScreenOutlineThickness.value = charScreenOutlineThickness;
            target.charScreenOutlineEdgeStrength.value = charScreenOutlineEdgeStrength;
            target.charPostExposure.value = charPostExposure;
            target.charGammaAdjust.value = charGammaAdjust;
            target.charToneMapping.value = charToneMapping;
            target.charContrast.value = charContrast;
            target.charColorFilter.value = charColorFilter;
            target.charHueShift.value = charHueShift;
            target.charSaturation.value = charSaturation;
            target.whiteBalanceTemperature.value = whiteBalanceTemperature;
            target.whiteBalanceTint.value = whiteBalanceTint;
            target.screenRimWidth.value = screenRimWidth;
            target.screenRimColor.value = screenRimColor;

            target.envPostProcessing.value = envPostProcessing;
            target.envToneMapping.value = envToneMapping;
            target.envPostExposure.value = envPostExposure;
            target.envContrast.value = envContrast;
            target.envColorFilter.value = envColorFilter;
            target.envHueShift.value = envHueShift;
            target.envSaturation.value = envSaturation;
            target.envWhiteBalanceTemperature.value = envWhiteBalanceTemperature;
            target.envWhiteBalanceTint.value = envWhiteBalanceTint;
            
            target.charToeStrength.value = charToeStrength;
            target.charToeLength.value = charToeLength;
            target.charShoulderStrength.value = charShoulderStrength;
            target.charShoulderLength.value = charShoulderLength;
            target.charShoulderAngle.value = charShoulderAngle;
            target.charGamma.value = charGamma;
            target.envToeStrength.value = envToeStrength;
            target.envToeLength.value = envToeLength;
            target.envShoulderStrength.value = envShoulderStrength;
            target.envShoulderLength.value = envShoulderLength;
            target.envShoulderAngle.value = envShoulderAngle;
            target.envGamma.value = envGamma;

            target.charBloom.value = charBloom;
            target.threshold.value = threshold;
            target.intensity.value = intensity;
            target.scatter.value = scatter;
            target.clamp.value = clamp;
            target.tint.value = tint;
            target.highQualityFiltering.value = highQualityFiltering;
            target.downscale.value = downscale;
            target.maxIterations.value = maxIterations;
            target.dirtTexture.value = dirtTexture;
            target.dirtIntensity.value = dirtIntensity;
            
            target.envBloom.value = envBloom;
            target.envBloomThreshold.value = envBloomThreshold;
            target.envBloomIntensity.value = envBloomIntensity;
            target.envBloomScatter.value = envBloomScatter;
            target.envBloomClamp.value = envBloomClamp;
            target.envBloomTint.value = envBloomTint;
            target.envBloomHighQualityFiltering.value = envBloomHighQualityFiltering;
            target.envBloomDownscale.value = envBloomDownscale;
            target.envBloomMaxIterations.value = envBloomMaxIterations;
            target.envBloomDirtTexture.value = envBloomDirtTexture;
            target.envBloomDirtIntensity.value = envBloomDirtIntensity;
        }
        
        public void SaveFrom(PotaToon source)
        {
            mode = source.mode.value;
            transparentShadow = source.transparentShadow.value;
            charShadowDirOffsetMode = source.charShadowDirOffsetMode.value;
            charShadowDirOffset = source.charShadowDirOffset.value;
            followLayerMask = source.followLayerMask.value;
            maxToonBrightness = source.maxToonBrightness.value;
            bias = source.bias.value;
            normalBias = source.normalBias.value;
            quality = source.quality.value;
            textureScale = source.textureScale.value;
            transparentTextureScale = source.transparentTextureScale.value;
            shadowCullingDistance = source.shadowCullingDistance.value;
            oit = source.oit.value;
            oitMode = source.oitMode.value;

            charPostProcessing = source.charPostProcessing.value;
            charScreenOutline = source.charScreenOutline.value;
            charScreenOutlineExcludeInnerLines = source.charScreenOutlineExcludeInnerLines.value;
            charScreenOutlineColor = source.charScreenOutlineColor.value;
            charScreenOutlineThickness = source.charScreenOutlineThickness.value;
            charScreenOutlineEdgeStrength = source.charScreenOutlineEdgeStrength.value;
            charPostExposure = source.charPostExposure.value;
            charGammaAdjust = source.charGammaAdjust.value;
            charToneMapping = source.charToneMapping.value;
            charContrast = source.charContrast.value;
            charColorFilter = source.charColorFilter.value;
            charHueShift = source.charHueShift.value;
            charSaturation = source.charSaturation.value;
            whiteBalanceTemperature = source.whiteBalanceTemperature.value;
            whiteBalanceTint = source.whiteBalanceTint.value;
            screenRimWidth = source.screenRimWidth.value;
            screenRimColor = source.screenRimColor.value;

            envPostProcessing = source.envPostProcessing.value;
            envToneMapping = source.envToneMapping.value;
            envPostExposure = source.envPostExposure.value;
            envContrast = source.envContrast.value;
            envColorFilter = source.envColorFilter.value;
            envHueShift = source.envHueShift.value;
            envSaturation = source.envSaturation.value;
            envWhiteBalanceTemperature = source.envWhiteBalanceTemperature.value;
            envWhiteBalanceTint = source.envWhiteBalanceTint.value;
            
            charToeStrength = source.charToeStrength.value;
            charToeLength = source.charToeLength.value;
            charShoulderStrength = source.charShoulderStrength.value;
            charShoulderLength = source.charShoulderLength.value;
            charShoulderAngle = source.charShoulderAngle.value;
            charGamma = source.charGamma.value;
            envToeStrength = source.envToeStrength.value;
            envToeLength = source.envToeLength.value;
            envShoulderStrength = source.envShoulderStrength.value;
            envShoulderLength = source.envShoulderLength.value;
            envShoulderAngle = source.envShoulderAngle.value;
            envGamma = source.envGamma.value;

            charBloom = source.charBloom.value;
            threshold = source.threshold.value;
            intensity = source.intensity.value;
            scatter = source.scatter.value;
            clamp = source.clamp.value;
            tint = source.tint.value;
            highQualityFiltering = source.highQualityFiltering.value;
            downscale = source.downscale.value;
            maxIterations = source.maxIterations.value;
            dirtTexture = source.dirtTexture.value;
            dirtIntensity = source.dirtIntensity.value;
            
            envBloom = source.envBloom.value;
            envBloomThreshold = source.envBloomThreshold.value;
            envBloomIntensity = source.envBloomIntensity.value;
            envBloomScatter = source.envBloomScatter.value;
            envBloomClamp = source.envBloomClamp.value;
            envBloomTint = source.envBloomTint.value;
            envBloomHighQualityFiltering = source.envBloomHighQualityFiltering.value;
            envBloomDownscale = source.envBloomDownscale.value;
            envBloomMaxIterations = source.envBloomMaxIterations.value;
            envBloomDirtTexture = source.envBloomDirtTexture.value;
            envBloomDirtIntensity = source.envBloomDirtIntensity.value;
        }
    }
}
