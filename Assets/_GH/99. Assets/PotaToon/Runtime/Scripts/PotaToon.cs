using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotaToon
{
    public enum PotaToonMode
    {
        Normal,
        Concert
    }
    
    public enum CharShadowMapSize
    {
        X2 = 2,
        X4 = 4,
        X8 = 8,
        X16 = 16,
    }

    public enum CharShadowMapPrecision
    {
        RFloat = 14,
        RHalf = 15,
    }
    
    public enum PotaToonQuality
    {
        Low,
        Medium,
        High,
        Cinematic,
        Custom
    }

    public enum PotaToonToneMapping
    {
        None,
        Neutral,
        ACES,
        Filmic,
        Uchimura,
        Tony,
        GT,
        Custom = 10
    }
    
    public enum OITMode
    {
        SrcBlend,
        Additive
    }
    
    public enum LightOffsetMode
    {
        Local,
        World
    }
    
    public enum SurfaceType
    {
        Opaque,
        Cutout,
        Refraction,
        Transparent
    }
    
    public enum AlphaMode
    {
        Alpha,
        Premultiply,
        Additive,
        Multiply,
        Custom
    }
    
    [Serializable]
    public sealed class LayerMaskParameter : VolumeParameter<LayerMask>
    {
        public LayerMaskParameter(LayerMask value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class PotaToonModeParameter : VolumeParameter<PotaToonMode>
    {
        public PotaToonModeParameter(PotaToonMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class CharShadowMapSizeParameter : VolumeParameter<CharShadowMapSize>
    {
        public CharShadowMapSizeParameter(CharShadowMapSize value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class CharShadowMapPrecisionParameter : VolumeParameter<CharShadowMapPrecision>
    {
        public CharShadowMapPrecisionParameter(CharShadowMapPrecision value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class PotaToonQualityParameter : VolumeParameter<PotaToonQuality>
    {
        public PotaToonQualityParameter(PotaToonQuality value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class PotaToonToneMappingParameter : VolumeParameter<PotaToonToneMapping>
    {
        public PotaToonToneMappingParameter(PotaToonToneMapping value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class OITModeParameter : VolumeParameter<OITMode>
    {
        public OITModeParameter(OITMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    
    [Serializable]
    public sealed class LightOffsetModeParameter : VolumeParameter<LightOffsetMode>
    {
        public LightOffsetModeParameter(LightOffsetMode value, bool overrideState = false) : base(value, overrideState) { }
    }
    
#if UNITY_2021_3
    /// <summary>
    /// This controls the size of the bloom texture.
    /// </summary>
    public enum BloomDownscaleMode
    {
        /// <summary>
        /// Use this to select half size as the starting resolution.
        /// </summary>
        Half,

        /// <summary>
        /// Use this to select quarter size as the starting resolution.
        /// </summary>
        Quarter,
    }
    
    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="BloomDownscaleMode"/> value.
    /// </summary>
    [Serializable]
    public sealed class DownscaleParameter : VolumeParameter<BloomDownscaleMode>
    {
        /// <summary>
        /// Creates a new <see cref="DownscaleParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public DownscaleParameter(BloomDownscaleMode value, bool overrideState = false) : base(value, overrideState) { }
    }
#endif
    
    [Serializable, VolumeComponentMenu("PotaToon")]
#if UNITY_6000_0_OR_NEWER
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
#endif
    public sealed class PotaToon : VolumeComponent
    {
#if UNITY_EDITOR
        public static bool guideWarningEnabled = true;
#endif
        public readonly static float k_BiasScale = 0.0001f;
        
        [Tooltip("We recommend to use Normal mode unless you don't use Main Directional Light for the main lighting.\nIf concert mode enabled, it detects the stronger light between [Main Directional Light, Brightest Spot Light(See the FollowLayerMask)] automatically, so this will help you to get more natural look in the dark scene (in this case, you're probably using a very low intensity for main light or no main light).\nThe Concert mode is only available in Forward+.")]
        public PotaToonModeParameter mode = new PotaToonModeParameter(PotaToonMode.Normal);
        [Tooltip("Sets if Transparent objects should also renders shadow. If enabled, it uses more memory and the performance will be slower.")]
        public BoolParameter transparentShadow = new BoolParameter(false);
        [Tooltip("[If Normal Mode] Sets the mode of character shadow direction control.")]
        public LightOffsetModeParameter charShadowDirOffsetMode = new LightOffsetModeParameter(LightOffsetMode.Local);
        [Tooltip("[If Normal Mode] Adjusts the character shadow direction. This rotates the main light to avoid ugly character shadow.")]
        public Vector3Parameter charShadowDirOffset = new Vector3Parameter(Vector3.zero);
        [Tooltip("[If Concert Mode] The spot lights corresponding to this layer will be prioritized over the Main Directional Light to determine shadow direction if stronger than the Main Directional Light at the current frame.")]
        public LayerMaskParameter followLayerMask = new LayerMaskParameter(0);
        [Tooltip("Sets the max brightness of the toon material to prevent being a god. This is applied before the below Character/Environment post processing.")]
        public ClampedFloatParameter maxToonBrightness = new ClampedFloatParameter(10f, 1f, 10f);
        [Tooltip("Depth bias for the global character shadow.")]
        public ClampedFloatParameter bias = new ClampedFloatParameter(1f, 0f, 10f);
        [Tooltip("Normal bias for the global character shadow.")]
        [HideInInspector] public ClampedFloatParameter normalBias = new ClampedFloatParameter(0.1f, 0f, 1f);
        public PotaToonQualityParameter quality = new PotaToonQualityParameter(PotaToonQuality.High);
        public CharShadowMapSizeParameter textureScale = new CharShadowMapSizeParameter(CharShadowMapSize.X8);
        public CharShadowMapSizeParameter transparentTextureScale = new CharShadowMapSizeParameter(CharShadowMapSize.X8);
        [Tooltip("Sets the character shadow distance for culling if there are more than 2 characters. The greater the distance, the wider the area covered, but the quality of the shadow decreases. If there is only one active character, the culling distance is always 2.")]
        public ClampedFloatParameter shadowCullingDistance = new ClampedFloatParameter(1.5f, 1f, 2f);
        
        [Tooltip("Sets if complex transparent toon objects(i.e. clothes) are rendered correctly. This will also let you have the outline for transparent objects. However, please note that this will render transparent toon objects after all other transparent objects are rendered, so you must be familiar with the rendering order to use this properly.")]
        public BoolParameter oit = new BoolParameter(false);
        public OITModeParameter oitMode = new OITModeParameter(OITMode.SrcBlend);
        
        // Post Process
        [Tooltip("Sets if Character post processing is enabled. This is rendered before URP post processing.")]
        public BoolParameter charPostProcessing = new BoolParameter(false);
        [Tooltip("Sets if screen outline for character is enabled. Note that transparent objects are excluded from screen outline.")]
        public BoolParameter charScreenOutline = new BoolParameter(false);
        [Tooltip("Draw outline only on the outer silhouette, not inside the character.")]
        public BoolParameter charScreenOutlineExcludeInnerLines = new BoolParameter(false);
        [Tooltip("Color for the Screen Outline targeting character area.")]
        public ColorParameter charScreenOutlineColor = new ColorParameter(Color.black, true, false, true);
        [Tooltip("Sets the thickness of the screen outline.")]
        public ClampedFloatParameter charScreenOutlineThickness = new ClampedFloatParameter(1f, 0f, 5f);
        [Tooltip("Sets the edge strength of the screen outline.")]
        public ClampedFloatParameter charScreenOutlineEdgeStrength = new ClampedFloatParameter(1f, 0f, 5f);
        [Tooltip("Sets the brightness for the character. This is applied before tone mapping and color grading.")]
        public ClampedFloatParameter charPostExposure = new ClampedFloatParameter(1f, 0f, 2f);
        [Tooltip("Sets if the character color needs to have more contrast. This is basically remapping the input color by SRGBToLinear.")]
        public BoolParameter charGammaAdjust = new BoolParameter(false);
        [Tooltip("Uses a custom tone mapping for the character area. Note that this tonemapping does not support HDR output.")]
        public PotaToonToneMappingParameter charToneMapping = new PotaToonToneMappingParameter(PotaToonToneMapping.None);
        [Tooltip("Expands or shrinks the overall range of tonal values.")]
        public ClampedFloatParameter charContrast = new ClampedFloatParameter(0f, -100f, 100f);
        [Tooltip("Tint the render by multiplying a color.")]
        public ColorParameter charColorFilter = new ColorParameter(Color.white, true, false, true);
        [Tooltip("Shift the hue of all colors.")]
        public ClampedFloatParameter charHueShift = new ClampedFloatParameter(0f, -180f, 180f);
        [Tooltip("Pushes the intensity of all colors.")]
        public ClampedFloatParameter charSaturation = new ClampedFloatParameter(0f, -100f, 100f);
        [Tooltip("Sets the white balance to a custom color temperature.")]
        public ClampedFloatParameter whiteBalanceTemperature = new ClampedFloatParameter(0f, -100, 100f);
        [Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
        public ClampedFloatParameter whiteBalanceTint = new ClampedFloatParameter(0f, -100, 100f);
        
        [Tooltip("Width for the additional Screen Rim Light targeting character area. This will not disable material rim light.")]
        public ClampedFloatParameter screenRimWidth = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Color for the additional Screen Rim Light targeting character area.")]
        public ColorParameter screenRimColor = new ColorParameter(Color.white, true, false, true);
        
        [Tooltip("Sets if Environment(Exclude character area) post processing is enabled. This is rendered before URP post processing.")]
        public BoolParameter envPostProcessing = new BoolParameter(false);
        [Tooltip("Uses a custom tone mapping except for the character area. Note that this tonemapping does not support HDR output.")]
        public PotaToonToneMappingParameter envToneMapping = new PotaToonToneMappingParameter(PotaToonToneMapping.None);
        [Tooltip("Sets the brightness except for the character area. This is applied right before tone mapping.")]
        public ClampedFloatParameter envPostExposure = new ClampedFloatParameter(1f, 0f, 2f);
        [Tooltip("Expands or shrinks the overall range of tonal values.")]
        public ClampedFloatParameter envContrast = new ClampedFloatParameter(0f, -100f, 100f);
        [Tooltip("Tint the render by multiplying a color.")]
        public ColorParameter envColorFilter = new ColorParameter(Color.white, true, false, true);
        [Tooltip("Shift the hue of all colors.")]
        public ClampedFloatParameter envHueShift = new ClampedFloatParameter(0f, -180f, 180f);
        [Tooltip("Pushes the intensity of all colors.")]
        public ClampedFloatParameter envSaturation = new ClampedFloatParameter(0f, -100f, 100f);
        [Tooltip("Sets the white balance to a custom color temperature.")]
        public ClampedFloatParameter envWhiteBalanceTemperature = new ClampedFloatParameter(0f, -100, 100f);
        [Tooltip("Sets the white balance to compensate for a green or magenta tint.")]
        public ClampedFloatParameter envWhiteBalanceTint = new ClampedFloatParameter(0f, -100, 100f);
        
        // Custom Tonemapping Curve
        [Tooltip("Controls the transition between the toe and the mid section of the curve. A value of 0 results in no transition and a value of 1 results in a very hard transition.")]
        public ClampedFloatParameter charToeStrength = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Controls how much of the dynamic range is in the toe. Higher values result in longer toes and therefore contain more of the dynamic range.")]
        public ClampedFloatParameter charToeLength = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Controls the transition between the midsection and the shoulder of the curve. A value of 0 results in no transition and a value of 1 results in a very hard transition.")]
        public ClampedFloatParameter charShoulderStrength = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Sets how many F-stops (EV) to add to the dynamic range of the curve.")]
        public MinFloatParameter charShoulderLength = new MinFloatParameter(0.5f, 0f);
        [Tooltip("Controls how much overshoot to add to the shoulder.")]
        public ClampedFloatParameter charShoulderAngle = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Sets a gamma correction value that URP applies to the whole curve.")]
        public MinFloatParameter charGamma = new MinFloatParameter(1f, 0.001f);
        
        [Tooltip("Controls the transition between the toe and the mid section of the curve. A value of 0 results in no transition and a value of 1 results in a very hard transition.")]
        public ClampedFloatParameter envToeStrength = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Controls how much of the dynamic range is in the toe. Higher values result in longer toes and therefore contain more of the dynamic range.")]
        public ClampedFloatParameter envToeLength = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Controls the transition between the midsection and the shoulder of the curve. A value of 0 results in no transition and a value of 1 results in a very hard transition.")]
        public ClampedFloatParameter envShoulderStrength = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Sets how many F-stops (EV) to add to the dynamic range of the curve.")]
        public MinFloatParameter envShoulderLength = new MinFloatParameter(0.5f, 0f);
        [Tooltip("Controls how much overshoot to add to the shoulder.")]
        public ClampedFloatParameter envShoulderAngle = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Sets a gamma correction value that URP applies to the whole curve.")]
        public MinFloatParameter envGamma = new MinFloatParameter(1f, 0.001f);
        
        // Bloom
        [Tooltip("Sets if the additional bloom for character area is enabled.")]
        public BoolParameter charBloom = new BoolParameter(false);
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        [Tooltip("Set the radius of the bloom effect.")]
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        [Tooltip("Set the maximum intensity that Unity uses to calculate Bloom. If pixels in your Scene are more intense than this, URP renders them at their current intensity, but uses this intensity value for the purposes of Bloom calculations.")]
        public MinFloatParameter clamp = new MinFloatParameter(65472f, 0f);
        [Tooltip("Use the color picker to select a color for the Bloom effect to tint to.")]
        public ColorParameter tint = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Use bicubic sampling instead of bilinear sampling for the upsampling passes. This is slightly more expensive but helps getting smoother visuals.")]
        public BoolParameter highQualityFiltering = new BoolParameter(false);
        [Tooltip("The starting resolution that this effect begins processing."), AdditionalProperty]
        public DownscaleParameter downscale = new DownscaleParameter(BloomDownscaleMode.Half);
        [Tooltip("The maximum number of iterations in the effect processing sequence."), AdditionalProperty]
        public ClampedIntParameter maxIterations = new ClampedIntParameter(6, 2, 8);
        [Tooltip("Dirtiness texture to add smudges or dust to the bloom effect.")]
        public TextureParameter dirtTexture = new TextureParameter(null);
        [Tooltip("Amount of dirtiness.")]
        public MinFloatParameter dirtIntensity = new MinFloatParameter(0f, 0f);
        
        // Env Bloom
        [Tooltip("Sets if the additional bloom for environment area is enabled.")]
        public BoolParameter envBloom = new BoolParameter(false);
        [Tooltip("Filters out pixels under this level of brightness. Value is in gamma-space.")]
        public MinFloatParameter envBloomThreshold = new MinFloatParameter(0.9f, 0f);
        [Tooltip("Strength of the bloom filter.")]
        public MinFloatParameter envBloomIntensity = new MinFloatParameter(0f, 0f);
        [Tooltip("Set the radius of the bloom effect.")]
        public ClampedFloatParameter envBloomScatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        [Tooltip("Set the maximum intensity that Unity uses to calculate Bloom. If pixels in your Scene are more intense than this, URP renders them at their current intensity, but uses this intensity value for the purposes of Bloom calculations.")]
        public MinFloatParameter envBloomClamp = new MinFloatParameter(65472f, 0f);
        [Tooltip("Use the color picker to select a color for the Bloom effect to tint to.")]
        public ColorParameter envBloomTint = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Use bicubic sampling instead of bilinear sampling for the upsampling passes. This is slightly more expensive but helps getting smoother visuals.")]
        public BoolParameter envBloomHighQualityFiltering = new BoolParameter(false);
        [Tooltip("The starting resolution that this effect begins processing."), AdditionalProperty]
        public DownscaleParameter envBloomDownscale = new DownscaleParameter(BloomDownscaleMode.Half);
        [Tooltip("The maximum number of iterations in the effect processing sequence."), AdditionalProperty]
        public ClampedIntParameter envBloomMaxIterations = new ClampedIntParameter(6, 2, 8);
        [Tooltip("Dirtiness texture to add smudges or dust to the bloom effect.")]
        public TextureParameter envBloomDirtTexture = new TextureParameter(null);
        [Tooltip("Amount of dirtiness.")]
        public MinFloatParameter envBloomDirtIntensity = new MinFloatParameter(0f, 0f);
    }
}
