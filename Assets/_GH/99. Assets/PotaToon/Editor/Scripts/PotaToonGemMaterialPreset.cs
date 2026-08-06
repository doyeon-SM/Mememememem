using UnityEngine;
using UnityEngine.Rendering;

namespace PotaToon.Editor
{
    [CreateAssetMenu(menuName = "PotaToon/Gem Material Preset", fileName = "PotaToonGemMaterialPreset")]
    internal class PotaToonGemMaterialPreset : PotaToonMaterialPresetBase
    {
        // Global Settings
        public SurfaceType _SurfaceType                 = SurfaceType.Refraction;
        public CullMode _Cull                           = CullMode.Back;
        public float _Cutoff                            = 0.5f;
        public int _ZWriteMode                          = 0;
        public CompareFunction _ZTest                   = CompareFunction.LessEqual;
        public int _AutoRenderQueue                     = 1;
        public int _RenderQueue                         = 2900;
        public int _DisableOIT                          = 0;
        public AlphaMode _Blend                         = AlphaMode.Premultiply;
        public BlendMode _SrcBlend                      = BlendMode.One;
        public BlendMode _DstBlend                      = BlendMode.OneMinusSrcAlpha;
        public BlendMode _SrcBlendAlpha                 = BlendMode.One;
        public BlendMode _DstBlendAlpha                 = BlendMode.OneMinusSrcAlpha;

        // Stencil
        public CompareFunction _StencilComp;
        public float _StencilRef;
        public StencilOp _StencilPass;
        public StencilOp _StencilFail;
        public StencilOp _StencilZFail;

        // Pass Control
        public bool _EnableTransparentShadowPass       = true;

        // Main
        public Color _BaseColor                         = Color.white;
        public float _BaseMapHue                        = 0f;
        public float _BaseMapSaturation                 = 0f;
        public float _BaseMapContrast                   = 0f;
        public AlphaMaskMode _AlphaMaskMode             = AlphaMaskMode.Clipping;
        public float _ClippingMaskCutoff                = 0.5f;
        public float _AlphaMaskScale                    = 1f;
        public float _AlphaMaskValue                    = 0f;
        public MaskChannel _ClippingMaskCH              = MaskChannel.G;
        public int _UseNormalMap                        = 0;
        public float _BumpScale                         = 0f;
        public float _Roughness                         = 0.2f;
        public float _IndirectDimmer                    = 1f;
        public int _ReceiveLightShadow                  = 1;

        // Gem
        public int _GemType                             = 0;
        public float _GemShine                          = 0.5f;
        public float _ClearcoatIntensity                = 0.4f;
        public float _ClearcoatRoughness                = 0.05f;
        public float _TransmissionStrength              = 0.6f;
        public Color _AbsorptionColor                   = new Color(0.2f, 0.4f, 0.6f, 1f);
        public float _Thickness                         = 1f;
        public float _BaseStrength                     = 0f;
        public float _ChromaticAberration               = 0f;
        public float _RefractionStrength                = 0.2f;
        public float _RefractionBlurWeight             = 0f;
        public float _RefractionFresnelPower            = 1f;
        public float _ParticleIntensity                 = 0f;
        public float _ParticleLightingDimmer            = 0f;
        public float _ParticleLoop                      = 8f;
        public Color _ParticleColor                     = Color.white;

        // Glitter
        public int _UseGlitter                          = 0;
        public Color _GlitterColor                      = Color.white;
        public float _GlitterMainStrength               = 0f;
        public float _GlitterEnableLighting             = 1f;
        public int _GlitterBackfaceMask                 = 0;
        public int _GlitterApplyTransparency            = 1;
        public float _GlitterShadowMask                 = 0f;
        public float _GlitterParticleSize               = 0.16f;
        public float _GlitterScaleRandomize             = 0f;
        public float _GlitterContrast                   = 50f;
        public float _GlitterSensitivity                = 100f;
        public float _GlitterBlinkSpeed                 = 0.1f;
        public float _GlitterAngleLimit                 = 0f;
        public float _GlitterLightDirection             = 0f;
        public float _GlitterColorRandomness            = 0f;
        public float _GlitterNormalStrength             = 1f;
        public float _GlitterPostContrast               = 1f;

        // Rim
        public int _UseRim                              = 0;
        public Color _RimColor                          = new Color(1f, 1f, 1f, 0f);
        public float _RimPower                          = 0.25f;
        public float _RimSmoothness                     = 0.05f;
        public MaskChannel _RimMaskCH                   = MaskChannel.R;
        public Color _ScreenRimTint                     = Color.white;
        public ScreenRimTintMode _ScreenRimTintMode     = ScreenRimTintMode.Multiply;
        public float _ScreenRimWidthMultiplier          = 1f;
        public float _ScreenRimLightingDimmer           = 0f;
        public int _ScreenRimShadowFade                 = 1;

        // MatCap 1
        public MatCapMode _MatCapMode                   = MatCapMode.None;
        public Color _MatCapColor                       = new Color(1f, 1f, 1f, 0f);
        public float _MatCapWeight                      = 1f;
        public float _MatCapLightingDimmer              = 0f;
        public MaskChannel _MatCapMaskCH                = MaskChannel.R;
        public UVChannel _MatCapUV1                     = UVChannel.UV0;
        public Texture2D _MatCapTex;

        // MatCap 2
        public MatCapMode _MatCapMode2                  = MatCapMode.None;
        public Color _MatCapColor2                      = new Color(1f, 1f, 1f, 0f);
        public float _MatCapWeight2                     = 1f;
        public float _MatCapLightingDimmer2             = 0f;
        public MaskChannel _MatCapMaskCH2               = MaskChannel.R;
        public UVChannel _MatCapUV2                     = UVChannel.UV0;
        public Texture2D _MatCapTex2;

        // MatCap 3
        public MatCapMode _MatCapMode3                  = MatCapMode.None;
        public Color _MatCapColor3                      = new Color(1f, 1f, 1f, 0f);
        public float _MatCapWeight3                     = 1f;
        public float _MatCapLightingDimmer3             = 0f;
        public MaskChannel _MatCapMaskCH3               = MaskChannel.R;
        public UVChannel _MatCapUV3                     = UVChannel.UV0;
        public Texture2D _MatCapTex3;

        // MatCap 4
        public MatCapMode _MatCapMode4                  = MatCapMode.None;
        public Color _MatCapColor4                      = new Color(1f, 1f, 1f, 0f);
        public float _MatCapWeight4                     = 1f;
        public float _MatCapLightingDimmer4             = 0f;
        public MaskChannel _MatCapMaskCH4               = MaskChannel.R;
        public UVChannel _MatCapUV4                     = UVChannel.UV0;
        public Texture2D _MatCapTex4;

        // Character Shadow
        public int _DisableCharShadow                   = 0;
        public float _CharShadowSmoothnessOffset        = 0f;

        // UV
        public UVChannel _BaseMapUV                     = UVChannel.UV0;
        public UVChannel _NormalMapUV                   = UVChannel.UV0;
        public UVChannel _ClippingMaskUV                = UVChannel.UV0;
        public UVChannel _RimMaskUV                     = UVChannel.UV0;
        public UVChannel _GlitterMapUV                  = UVChannel.UV0;

        private void SetMaterialTextureIfNeeded(Material mat, string property, Texture tex)
        {
            if (tex != null)
                mat.SetTexture(property, tex);
        }

        public override void ApplyTo(Material mat)
        {
            // Global Settings
            mat.SetInt("_ToonType", (int)_ToonType);
            mat.SetInt("_SurfaceType", (int)_SurfaceType);
            mat.SetInt("_Cull", (int)_Cull);
            mat.SetFloat("_Cutoff", _Cutoff);
            mat.SetInt("_ZWriteMode", _ZWriteMode);
            mat.SetInt("_ZTest", (int)_ZTest);
            mat.SetInt("_AutoRenderQueue", _AutoRenderQueue);
            mat.renderQueue = _RenderQueue;
            if (mat.HasProperty("_DisableOIT"))
                mat.SetInt("_DisableOIT", _DisableOIT);
            mat.SetInt(PotaToonShaderGUIBase.Property.BlendMode, (int)_Blend);
            mat.SetInt(PotaToonShaderGUIBase.Property.SrcBlend, (int)_SrcBlend);
            mat.SetInt(PotaToonShaderGUIBase.Property.DstBlend, (int)_DstBlend);
            mat.SetInt(PotaToonShaderGUIBase.Property.SrcBlendAlpha, (int)_SrcBlendAlpha);
            mat.SetInt(PotaToonShaderGUIBase.Property.DstBlendAlpha, (int)_DstBlendAlpha);

            // Stencil
            mat.SetInt("_StencilComp", (int)_StencilComp);
            mat.SetFloat("_StencilRef", _StencilRef);
            mat.SetInt("_StencilPass", (int)_StencilPass);
            mat.SetInt("_StencilFail", (int)_StencilFail);
            mat.SetInt("_StencilZFail", (int)_StencilZFail);

            // Pass Control
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentShadow, _EnableTransparentShadowPass);
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentAlphaSum, _EnableTransparentShadowPass);

            // Main
            mat.SetColor("_BaseColor", _BaseColor);
            mat.SetFloat("_BaseMapHue", _BaseMapHue);
            mat.SetFloat("_BaseMapSaturation", _BaseMapSaturation);
            mat.SetFloat("_BaseMapContrast", _BaseMapContrast);
            mat.SetInt("_AlphaMaskMode", (int)_AlphaMaskMode);
            mat.SetFloat("_ClippingMaskCutoff", _ClippingMaskCutoff);
            mat.SetFloat("_AlphaMaskScale", _AlphaMaskScale);
            mat.SetFloat("_AlphaMaskValue", _AlphaMaskValue);
            mat.SetInt("_ClippingMaskCH", (int)_ClippingMaskCH);
            mat.SetInt("_UseNormalMap", _UseNormalMap);
            mat.SetFloat("_BumpScale", _BumpScale);
            mat.SetFloat("_Roughness", _Roughness);
            mat.SetFloat("_IndirectDimmer", _IndirectDimmer);
            mat.SetInt("_ReceiveLightShadow", _ReceiveLightShadow);

            // Gem
            mat.SetInt("_GemType", _GemType);
            mat.SetFloat("_GemShine", _GemShine);
            mat.SetFloat("_ClearcoatIntensity", _ClearcoatIntensity);
            mat.SetFloat("_ClearcoatRoughness", _ClearcoatRoughness);
            mat.SetFloat("_TransmissionStrength", _TransmissionStrength);
            mat.SetColor("_AbsorptionColor", _AbsorptionColor);
            mat.SetFloat("_Thickness", _Thickness);
            mat.SetFloat("_BaseStrength", _BaseStrength);
            mat.SetFloat("_ChromaticAberration", _ChromaticAberration);
            mat.SetFloat("_RefractionStrength", _RefractionStrength);
            mat.SetFloat("_RefractionBlurWeight", _RefractionBlurWeight);
            mat.SetFloat("_RefractionFresnelPower", _RefractionFresnelPower);
            mat.SetFloat("_ParticleIntensity", _ParticleIntensity);
            mat.SetFloat("_ParticleLightingDimmer", _ParticleLightingDimmer);
            mat.SetFloat("_ParticleLoop", _ParticleLoop);
            mat.SetColor("_ParticleColor", _ParticleColor);

            // Glitter
            mat.SetInt("_UseGlitter", _UseGlitter);
            mat.SetColor("_GlitterColor", _GlitterColor);
            mat.SetFloat("_GlitterMainStrength", _GlitterMainStrength);
            mat.SetFloat("_GlitterEnableLighting", _GlitterEnableLighting);
            mat.SetInt("_GlitterBackfaceMask", _GlitterBackfaceMask);
            mat.SetInt("_GlitterApplyTransparency", _GlitterApplyTransparency);
            mat.SetFloat("_GlitterShadowMask", _GlitterShadowMask);
            mat.SetFloat("_GlitterParticleSize", _GlitterParticleSize);
            mat.SetFloat("_GlitterScaleRandomize", _GlitterScaleRandomize);
            mat.SetFloat("_GlitterContrast", _GlitterContrast);
            mat.SetFloat("_GlitterSensitivity", _GlitterSensitivity);
            mat.SetFloat("_GlitterBlinkSpeed", _GlitterBlinkSpeed);
            mat.SetFloat("_GlitterAngleLimit", _GlitterAngleLimit);
            mat.SetFloat("_GlitterLightDirection", _GlitterLightDirection);
            mat.SetFloat("_GlitterColorRandomness", _GlitterColorRandomness);
            mat.SetFloat("_GlitterNormalStrength", _GlitterNormalStrength);
            mat.SetFloat("_GlitterPostContrast", _GlitterPostContrast);

            // Rim
            mat.SetInt("_UseRim", _UseRim);
            mat.SetColor("_RimColor", _RimColor);
            mat.SetFloat("_RimPower", _RimPower);
            mat.SetFloat("_RimSmoothness", _RimSmoothness);
            mat.SetInt("_RimMaskCH", (int)_RimMaskCH);
            mat.SetColor("_ScreenRimTint", _ScreenRimTint);
            mat.SetInt("_ScreenRimTintMode", (int)_ScreenRimTintMode);
            mat.SetFloat("_ScreenRimWidthMultiplier", _ScreenRimWidthMultiplier);
            mat.SetFloat("_ScreenRimLightingDimmer", _ScreenRimLightingDimmer);
            mat.SetInt("_ScreenRimShadowFade", _ScreenRimShadowFade);

            // MatCap
            mat.SetInt("_MatCapMode", (int)_MatCapMode);
            mat.SetColor("_MatCapColor", _MatCapColor);
            mat.SetFloat("_MatCapWeight", _MatCapWeight);
            mat.SetFloat("_MatCapLightingDimmer", _MatCapLightingDimmer);
            mat.SetInt("_MatCapMaskCH", (int)_MatCapMaskCH);
            mat.SetInt("_MatCapUV1", (int)_MatCapUV1);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex", _MatCapTex);

            mat.SetInt("_MatCapMode2", (int)_MatCapMode2);
            mat.SetColor("_MatCapColor2", _MatCapColor2);
            mat.SetFloat("_MatCapWeight2", _MatCapWeight2);
            mat.SetFloat("_MatCapLightingDimmer2", _MatCapLightingDimmer2);
            mat.SetInt("_MatCapMaskCH2", (int)_MatCapMaskCH2);
            mat.SetInt("_MatCapUV2", (int)_MatCapUV2);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex2", _MatCapTex2);

            mat.SetInt("_MatCapMode3", (int)_MatCapMode3);
            mat.SetColor("_MatCapColor3", _MatCapColor3);
            mat.SetFloat("_MatCapWeight3", _MatCapWeight3);
            mat.SetFloat("_MatCapLightingDimmer3", _MatCapLightingDimmer3);
            mat.SetInt("_MatCapMaskCH3", (int)_MatCapMaskCH3);
            mat.SetInt("_MatCapUV3", (int)_MatCapUV3);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex3", _MatCapTex3);

            mat.SetInt("_MatCapMode4", (int)_MatCapMode4);
            mat.SetColor("_MatCapColor4", _MatCapColor4);
            mat.SetFloat("_MatCapWeight4", _MatCapWeight4);
            mat.SetFloat("_MatCapLightingDimmer4", _MatCapLightingDimmer4);
            mat.SetInt("_MatCapMaskCH4", (int)_MatCapMaskCH4);
            mat.SetInt("_MatCapUV4", (int)_MatCapUV4);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex4", _MatCapTex4);

            // Character Shadow
            mat.SetInt("_DisableCharShadow", _DisableCharShadow);
            mat.SetFloat("_CharShadowSmoothnessOffset", _CharShadowSmoothnessOffset);

            // UV
            mat.SetInt("_BaseMapUV", (int)_BaseMapUV);
            mat.SetInt("_NormalMapUV", (int)_NormalMapUV);
            mat.SetInt("_ClippingMaskUV", (int)_ClippingMaskUV);
            mat.SetInt("_RimMaskUV", (int)_RimMaskUV);
            mat.SetInt("_GlitterMapUV", (int)_GlitterMapUV);
        }

        public override void SaveFrom(Material mat)
        {
            // Global Settings
            _ToonType = (ToonType)mat.GetInt("_ToonType");
            _SurfaceType = (SurfaceType)mat.GetInt("_SurfaceType");
            _Cull = (CullMode)mat.GetInt("_Cull");
            _Cutoff = mat.GetFloat("_Cutoff");
            _ZWriteMode = mat.GetInt("_ZWriteMode");
            _ZTest = (CompareFunction)mat.GetInt("_ZTest");
            _AutoRenderQueue = mat.GetInt("_AutoRenderQueue");
            _RenderQueue = mat.renderQueue;
            _DisableOIT = mat.HasProperty("_DisableOIT") ? mat.GetInt("_DisableOIT") : 0;
            _Blend = (AlphaMode)mat.GetInt(PotaToonShaderGUIBase.Property.BlendMode);
            _SrcBlend = (BlendMode)mat.GetInt(PotaToonShaderGUIBase.Property.SrcBlend);
            _DstBlend = (BlendMode)mat.GetInt(PotaToonShaderGUIBase.Property.DstBlend);
            _SrcBlendAlpha = (BlendMode)mat.GetInt(PotaToonShaderGUIBase.Property.SrcBlendAlpha);
            _DstBlendAlpha = (BlendMode)mat.GetInt(PotaToonShaderGUIBase.Property.DstBlendAlpha);

            // Stencil
            _StencilComp = (CompareFunction)mat.GetInt("_StencilComp");
            _StencilRef = mat.GetFloat("_StencilRef");
            _StencilPass = (StencilOp)mat.GetInt("_StencilPass");
            _StencilFail = (StencilOp)mat.GetInt("_StencilFail");
            _StencilZFail = (StencilOp)mat.GetInt("_StencilZFail");

            // Pass Control
            _EnableTransparentShadowPass = mat.GetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentShadow);

            // Main
            _BaseColor = mat.GetColor("_BaseColor");
            _BaseMapHue = mat.GetFloat("_BaseMapHue");
            _BaseMapSaturation = mat.GetFloat("_BaseMapSaturation");
            _BaseMapContrast = mat.GetFloat("_BaseMapContrast");
            _AlphaMaskMode = mat.HasProperty("_AlphaMaskMode") ? (AlphaMaskMode)mat.GetInt("_AlphaMaskMode") : AlphaMaskMode.Clipping;
            _ClippingMaskCutoff = mat.HasProperty("_ClippingMaskCutoff") ? mat.GetFloat("_ClippingMaskCutoff") : 0.5f;
            _AlphaMaskScale = mat.HasProperty("_AlphaMaskScale") ? mat.GetFloat("_AlphaMaskScale") : 1f;
            _AlphaMaskValue = mat.HasProperty("_AlphaMaskValue") ? mat.GetFloat("_AlphaMaskValue") : 0f;
            _ClippingMaskCH = (MaskChannel)mat.GetInt("_ClippingMaskCH");
            _UseNormalMap = mat.GetInt("_UseNormalMap");
            _BumpScale = mat.GetFloat("_BumpScale");
            _Roughness = mat.GetFloat("_Roughness");
            _IndirectDimmer = mat.GetFloat("_IndirectDimmer");
            _ReceiveLightShadow = mat.GetInt("_ReceiveLightShadow");

            // Gem
            _GemType = mat.GetInt("_GemType");
            _GemShine = mat.GetFloat("_GemShine");
            _ClearcoatIntensity = mat.GetFloat("_ClearcoatIntensity");
            _ClearcoatRoughness = mat.GetFloat("_ClearcoatRoughness");
            _TransmissionStrength = mat.GetFloat("_TransmissionStrength");
            _AbsorptionColor = mat.GetColor("_AbsorptionColor");
            _Thickness = mat.GetFloat("_Thickness");
            _BaseStrength = mat.GetFloat("_BaseStrength");
            _ChromaticAberration = mat.GetFloat("_ChromaticAberration");
            _RefractionStrength = mat.GetFloat("_RefractionStrength");
            _RefractionBlurWeight = mat.GetFloat("_RefractionBlurWeight");
            _RefractionFresnelPower = mat.GetFloat("_RefractionFresnelPower");
            _ParticleIntensity = mat.GetFloat("_ParticleIntensity");
            _ParticleLightingDimmer = mat.GetFloat("_ParticleLightingDimmer");
            _ParticleLoop = mat.GetFloat("_ParticleLoop");
            _ParticleColor = mat.GetColor("_ParticleColor");

            // Glitter
            _UseGlitter = mat.GetInt("_UseGlitter");
            _GlitterColor = mat.GetColor("_GlitterColor");
            _GlitterMainStrength = mat.GetFloat("_GlitterMainStrength");
            _GlitterEnableLighting = mat.GetFloat("_GlitterEnableLighting");
            _GlitterBackfaceMask = mat.GetInt("_GlitterBackfaceMask");
            _GlitterApplyTransparency = mat.GetInt("_GlitterApplyTransparency");
            _GlitterShadowMask = mat.GetFloat("_GlitterShadowMask");
            _GlitterParticleSize = mat.GetFloat("_GlitterParticleSize");
            _GlitterScaleRandomize = mat.GetFloat("_GlitterScaleRandomize");
            _GlitterContrast = mat.GetFloat("_GlitterContrast");
            _GlitterSensitivity = mat.GetFloat("_GlitterSensitivity");
            _GlitterBlinkSpeed = mat.GetFloat("_GlitterBlinkSpeed");
            _GlitterAngleLimit = mat.GetFloat("_GlitterAngleLimit");
            _GlitterLightDirection = mat.GetFloat("_GlitterLightDirection");
            _GlitterColorRandomness = mat.GetFloat("_GlitterColorRandomness");
            _GlitterNormalStrength = mat.GetFloat("_GlitterNormalStrength");
            _GlitterPostContrast = mat.GetFloat("_GlitterPostContrast");

            // Rim
            _UseRim = mat.GetInt("_UseRim");
            _RimColor = mat.GetColor("_RimColor");
            _RimPower = mat.GetFloat("_RimPower");
            _RimSmoothness = mat.GetFloat("_RimSmoothness");
            _RimMaskCH = (MaskChannel)mat.GetInt("_RimMaskCH");
            _ScreenRimTint = mat.GetColor("_ScreenRimTint");
            _ScreenRimTintMode = (ScreenRimTintMode)mat.GetInt("_ScreenRimTintMode");
            _ScreenRimWidthMultiplier = mat.GetFloat("_ScreenRimWidthMultiplier");
            _ScreenRimLightingDimmer = mat.GetFloat("_ScreenRimLightingDimmer");
            _ScreenRimShadowFade = mat.GetInt("_ScreenRimShadowFade");

            // MatCap
            _MatCapMode = (MatCapMode)mat.GetInt("_MatCapMode");
            _MatCapColor = mat.GetColor("_MatCapColor");
            _MatCapWeight = mat.GetFloat("_MatCapWeight");
            _MatCapLightingDimmer = mat.GetFloat("_MatCapLightingDimmer");
            _MatCapMaskCH = (MaskChannel)mat.GetInt("_MatCapMaskCH");
            _MatCapUV1 = (UVChannel)mat.GetInt("_MatCapUV1");
            _MatCapTex = mat.GetTexture("_MatCapTex") as Texture2D;

            _MatCapMode2 = (MatCapMode)mat.GetInt("_MatCapMode2");
            _MatCapColor2 = mat.GetColor("_MatCapColor2");
            _MatCapWeight2 = mat.GetFloat("_MatCapWeight2");
            _MatCapLightingDimmer2 = mat.GetFloat("_MatCapLightingDimmer2");
            _MatCapMaskCH2 = (MaskChannel)mat.GetInt("_MatCapMaskCH2");
            _MatCapUV2 = (UVChannel)mat.GetInt("_MatCapUV2");
            _MatCapTex2 = mat.GetTexture("_MatCapTex2") as Texture2D;

            _MatCapMode3 = (MatCapMode)mat.GetInt("_MatCapMode3");
            _MatCapColor3 = mat.GetColor("_MatCapColor3");
            _MatCapWeight3 = mat.GetFloat("_MatCapWeight3");
            _MatCapLightingDimmer3 = mat.GetFloat("_MatCapLightingDimmer3");
            _MatCapMaskCH3 = (MaskChannel)mat.GetInt("_MatCapMaskCH3");
            _MatCapUV3 = (UVChannel)mat.GetInt("_MatCapUV3");
            _MatCapTex3 = mat.GetTexture("_MatCapTex3") as Texture2D;

            _MatCapMode4 = (MatCapMode)mat.GetInt("_MatCapMode4");
            _MatCapColor4 = mat.GetColor("_MatCapColor4");
            _MatCapWeight4 = mat.GetFloat("_MatCapWeight4");
            _MatCapLightingDimmer4 = mat.GetFloat("_MatCapLightingDimmer4");
            _MatCapMaskCH4 = (MaskChannel)mat.GetInt("_MatCapMaskCH4");
            _MatCapUV4 = (UVChannel)mat.GetInt("_MatCapUV4");
            _MatCapTex4 = mat.GetTexture("_MatCapTex4") as Texture2D;

            // Character Shadow
            _DisableCharShadow = mat.GetInt("_DisableCharShadow");
            _CharShadowSmoothnessOffset = mat.GetFloat("_CharShadowSmoothnessOffset");

            // UV
            _BaseMapUV = (UVChannel)mat.GetInt("_BaseMapUV");
            _NormalMapUV = (UVChannel)mat.GetInt("_NormalMapUV");
            _ClippingMaskUV = (UVChannel)mat.GetInt("_ClippingMaskUV");
            _RimMaskUV = (UVChannel)mat.GetInt("_RimMaskUV");
            _GlitterMapUV = (UVChannel)mat.GetInt("_GlitterMapUV");
        }
    }
}
