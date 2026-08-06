using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace PotaToon.Editor
{
    public enum ToonType { General = 0, Face = 1, Eye = 2, Gem = 3 }
    public enum SurfaceType { Opaque = 0, Cutout = 1, Refraction = 2, Transparent = 3 }
    public enum ScreenRimTintMode { Multiply = 0, Override = 1 }
    public enum MatCapMode { None = 0, Add = 1, Multiply = 2 }
    public enum OutlineMode { Normal = 0, Position = 1 }
    public enum UVChannel { UV0 = 0, UV1 = 1, UV2 = 2, UV3 = 3 }
    public enum MaskChannel { R = 0, G = 1, B = 2, A = 3 }
    public enum AlphaMaskMode { None = 0, Clipping = 1, Replace = 2, Multiply = 3, Add = 4, Subtract = 5 }
    public enum MaterialPresetDisplayGroup { CUSTOM = 0, METALLIC = 1, BASE = 2 }

    /// <summary>
    /// NOTE) All character-dependent textures are excluded.
    /// </summary>
    internal class PotaToonMaterialPresetBase : ScriptableObject
    {
        internal static readonly string[] k_PresetIconNames = new string[]
        {
            "sv_icon_dot13_pix16_gizmo", "sv_icon_dot8_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", "sv_icon_dot10_pix16_gizmo",
            "sv_icon_dot11_pix16_gizmo", "sv_icon_dot12_pix16_gizmo", "sv_icon_dot14_pix16_gizmo", "sv_icon_dot15_pix16_gizmo",
            "sv_icon_dot5_pix16_gizmo", "sv_icon_dot0_pix16_gizmo", "sv_icon_dot1_pix16_gizmo", "sv_icon_dot2_pix16_gizmo",
            "sv_icon_dot3_pix16_gizmo", "sv_icon_dot4_pix16_gizmo", "sv_icon_dot6_pix16_gizmo", "sv_icon_dot7_pix16_gizmo",
            "d_Avatar Icon", "HeadZoomSilhouette", "d_animationvisibilitytoggleon@2x", "d_Favorite Icon",
            "d_Cloth Icon", "LensFlare Icon", "LensFlare Gizmo",
        };
        public static List<GUIContent> presetIconContents = new List<GUIContent>();

        /// <summary>
        /// Note) If new default preset added, need to set displayIndex manually.
        /// </summary>
        public MaterialPresetDisplayGroup displayGroup;
        public int presetIconIndex;
        public ToonType _ToonType;
        public int _PotaToonShaderMode;

        public GUIContent GetIconContent(string name)
        {
            return (presetIconIndex < 0 || presetIconIndex >= presetIconContents.Count) ?
                EditorGUIUtility.IconContent(k_PresetIconNames[0]) :
                new GUIContent(name, presetIconContents[presetIconIndex].image) ?? GUIContent.none;
        }

        public static void LoadPresetIconsIfNeeded()
        {
            if (presetIconContents.Count == 0)
            {
                for (int i = 0; i < k_PresetIconNames.Length; i++)
                    presetIconContents.Add(EditorGUIUtility.IconContent(k_PresetIconNames[i]));
            }
        }

        public static List<List<PotaToonMaterialPresetBase>> SplitByDisplayIndex(List<PotaToonMaterialPresetBase> items)
        {
            var result = new List<List<PotaToonMaterialPresetBase>>();
            if (items == null || items.Count == 0)
                return result;

            items.Sort((a, b) =>
            {
                int idxCmp = b.displayGroup.CompareTo(a.displayGroup);
                if (idxCmp != 0)
                    return idxCmp;
                return string.Compare(a.name, b.name, System.StringComparison.Ordinal);
            });

            int currentIndex = (int)items[0].displayGroup;
            var currentGroup = new List<PotaToonMaterialPresetBase>();

            foreach (var item in items)
            {
                if ((int)item.displayGroup != currentIndex)
                {
                    result.Add(currentGroup);
                    currentGroup = new List<PotaToonMaterialPresetBase>();
                    currentIndex = (int)item.displayGroup;
                }
                currentGroup.Add(item);
            }

            result.Add(currentGroup);
            return result;
        }

        public virtual void ApplyTo(Material mat) {}
        public virtual void SaveFrom(Material mat) {}
    }

    [CreateAssetMenu(menuName = "PotaToon/Material Preset", fileName = "PotaToonMaterialPreset")]
    internal class PotaToonMaterialPreset : PotaToonMaterialPresetBase
    {
        // Base Settings
        public SurfaceType _SurfaceType     = SurfaceType.Opaque;
        public CullMode _Cull               = CullMode.Back;
        public float _Cutoff                = 0.5f;
        public int _ZWriteMode              = 1;
        public CompareFunction _ZTest       = CompareFunction.LessEqual;
        public int _AutoRenderQueue         = 1;
        public int _RenderQueue             = 2000;
        public int _DisableOIT              = 0;

        // Stencil
        public CompareFunction _StencilComp;
        public float _StencilRef;
        public StencilOp _StencilPass;
        public StencilOp _StencilFail;
        public StencilOp _StencilZFail;

        // Pass Control
        public bool _EnableShadowCasterPass        = true;
        public bool _EnableCharacterDepthPass      = true;
        public bool _EnableTransparentShadowPass   = true;
        public bool _EnableCharacterMaskPass       = true;

        // Main Settings
        public Color _BaseColor             = Color.white;
        public Color _ShadeColor            = new Color(0.75f, 0.75f, 0.75f);
        public float _BaseMapHue            = 0f;
        public float _BaseMapSaturation     = 0f;
        public float _BaseMapContrast       = 0f;
        public float _ShadeMapHue           = 0f;
        public float _ShadeMapSaturation    = 0f;
        public float _ShadeMapContrast      = 0f;
        public int _ColorGradingMaskReversed = 0;
        public int _ShadowExclusionMaskReversed = 0;
        public float _BaseStep              = 0.5f;
        public float _StepSmoothness        = 0.01f;
        public int _UseMidTone              = 1;
        public Color _MidColor              = new Color(0.5f,0.2f,0.2f);
        public float _MidWidth              = 1f;
        public float _IndirectDimmer        = 1f;
        public int _ReceiveLightShadow      = 1;
        public int _UseVertexColor          = 0;
        public int _UseDarknessMode         = 0;
        public float _BumpScale             = 0f;
        public int _UseNormalMap            = 0;
        public AlphaMaskMode _AlphaMaskMode = AlphaMaskMode.Clipping;
        public float _ClippingMaskCutoff    = 0.5f;
        public float _AlphaMaskScale        = 1f;
        public float _AlphaMaskValue        = 0f;

        // High Light
        public Color _SpecularColor         = Color.black;
        public float _SpecularPower         = 0.5f;
        public float _SpecularSmoothness    = 0.25f;

        // Rim Light
        public Color _RimColor                      = Color.black;
        public float _RimPower                      = 0.5f;
        public float _RimSmoothness                 = 0.25f;
        public Color _ScreenRimTint                 = Color.white;
        public ScreenRimTintMode _ScreenRimTintMode = 0;
        public float _ScreenRimWidthMultiplier      = 1f;
        public float _ScreenRimLightingDimmer       = 0f;
        public int _ScreenRimShadowFade             = 1;

        // MatCap Layers
        public MatCapMode _MatCapMode       = MatCapMode.None;
        public Color _MatCapColor           = Color.white;
        public Texture2D _MatCapTex;
        public float _MatCapWeight          = 1f;
        public float _MatCapLightingDimmer  = 1f;
        public MatCapMode _MatCapMode2      = MatCapMode.None;
        public Color _MatCapColor2          = Color.white;
        public Texture2D _MatCapTex2;
        public float _MatCapWeight2         = 1f;
        public float _MatCapLightingDimmer2 = 1f;
        public MatCapMode _MatCapMode3      = MatCapMode.None;
        public Color _MatCapColor3          = Color.white;
        public Texture2D _MatCapTex3;
        public float _MatCapWeight3         = 1f;
        public float _MatCapLightingDimmer3 = 1f;
        public MatCapMode _MatCapMode4      = MatCapMode.None;
        public Color _MatCapColor4          = Color.white;
        public Texture2D _MatCapTex4;
        public float _MatCapWeight4         = 1f;
        public float _MatCapLightingDimmer4 = 1f;
        public MatCapMode _MatCapMode5      = MatCapMode.None;
        public Color _MatCapColor5          = Color.white;
        public Texture2D _MatCapTex5;
        public float _MatCapWeight5         = 1f;
        public float _MatCapLightingDimmer5 = 1f;
        public MatCapMode _MatCapMode6      = MatCapMode.None;
        public Color _MatCapColor6          = Color.white;
        public Texture2D _MatCapTex6;
        public float _MatCapWeight6         = 1f;
        public float _MatCapLightingDimmer6 = 1f;
        public MatCapMode _MatCapMode7      = MatCapMode.None;
        public Color _MatCapColor7          = Color.white;
        public Texture2D _MatCapTex7;
        public float _MatCapWeight7         = 1f;
        public float _MatCapLightingDimmer7 = 1f;
        public MatCapMode _MatCapMode8      = MatCapMode.None;
        public Color _MatCapColor8          = Color.white;
        public Texture2D _MatCapTex8;
        public float _MatCapWeight8         = 1f;
        public float _MatCapLightingDimmer8 = 1f;

        // Emission
        public Color _EmissionColor         = Color.black;

        // Glitter
        public int _UseGlitter                      = 0;
        public Color _GlitterColor                  = Color.white;
        public float _GlitterMainStrength           = 0f;
        public float _GlitterEnableLighting         = 1f;
        public int _GlitterBackfaceMask             = 0;
        public int _GlitterApplyTransparency        = 1;
        public float _GlitterShadowMask             = 0f;
        public float _GlitterParticleSize           = 0.16f;
        public float _GlitterScaleRandomize         = 0f;
        public float _GlitterContrast               = 50f;
        public float _GlitterSensitivity            = 100f;
        public float _GlitterBlinkSpeed             = 0.1f;
        public float _GlitterAngleLimit             = 0f;
        public float _GlitterLightDirection         = 0f;
        public float _GlitterColorRandomness        = 0f;
        public float _GlitterNormalStrength         = 1f;
        public float _GlitterPostContrast           = 1f;

        // Outline
        public OutlineMode _OutlineMode             = OutlineMode.Normal;
        public int _UseOutlineNormalMap             = 0;
        public int _BlendOutlineMainTex             = 1;
        public Color _OutlineColor                  = Color.black;
        public float _OutlineWidth                  = 0.1f;
        public float _OutlineOffsetZ                = 0f;
        public float _OutlineLightingDimmer         = 1f;

        // Refraction
        public float _RefractionWeight              = 0f;
        public float _RefractionBlurStep            = 0f;

        // Character Shadow
        public int   _DisableCharShadow             = 0;
        public float _DepthBias                     = 0f;
        public float _NormalBias                    = 0f;
        public float _CharShadowSmoothnessOffset    = 0f;
        public int   _CharShadowType                = 0;
        public float _2DFaceShadowWidth             = 0.1f;

        // Face SDF
        public int _UseFaceSDFShadow                = 0;
        public int _SDFReverse                      = 0;
        public float _SDFOffset                     = 0f;
        public float _SDFBlur                       = 0f;

        // Hair High Light
        public int _UseHairHighLight                = 0;
        public int _ReverseHairHighLightTex         = 0;
        public float _HairHiStrength                = 1f;
        public float _HairHiUVOffset                = 0f;

        // UV Channels
        public UVChannel _BaseMapUV                 = UVChannel.UV0;
        public UVChannel _NormalMapUV               = UVChannel.UV0;
        public UVChannel _ClippingMaskUV            = UVChannel.UV0;
        public UVChannel _ColorGradingMaskUV        = UVChannel.UV0;
        public UVChannel _FaceSDFUV                 = UVChannel.UV0;
        public UVChannel _SpecularMapUV             = UVChannel.UV0;
        public UVChannel _RimMaskUV                 = UVChannel.UV0;
        public UVChannel _HairHiMapUV               = UVChannel.UV0;
        public UVChannel _GlitterMapUV              = UVChannel.UV0;
        public UVChannel _EmissionMapUV             = UVChannel.UV0;
        public UVChannel _OutlineMaskUV             = UVChannel.UV0;
        public UVChannel _MatCapUV1                 = UVChannel.UV0;
        public UVChannel _MatCapUV2                 = UVChannel.UV0;
        public UVChannel _MatCapUV3                 = UVChannel.UV0;
        public UVChannel _MatCapUV4                 = UVChannel.UV0;
        public UVChannel _MatCapUV5                 = UVChannel.UV0;
        public UVChannel _MatCapUV6                 = UVChannel.UV0;
        public UVChannel _MatCapUV7                 = UVChannel.UV0;
        public UVChannel _MatCapUV8                 = UVChannel.UV0;

        // Mask Channels
        public MaskChannel _ClippingMaskCH          = MaskChannel.G;
        public MaskChannel _SpecularMaskCH          = MaskChannel.G;
        public MaskChannel _RimMaskCH               = MaskChannel.G;
        public MaskChannel _EmissionMaskCH          = MaskChannel.G;
        public MaskChannel _OutlineMaskCH           = MaskChannel.G;
        public MaskChannel _MatCapMaskCH1           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH2           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH3           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH4           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH5           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH6           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH7           = MaskChannel.R;
        public MaskChannel _MatCapMaskCH8           = MaskChannel.R;
        public MaskChannel _AOMapCH                 = MaskChannel.G;
        public MaskChannel _FaceSDFTexCH            = MaskChannel.R;

        private void SetMaterialTextureIfNeeded(Material mat, string property, Texture tex)
        {
            if (tex != null)
                mat.SetTexture(property, tex);
            else
                mat.SetTexture(property, null);
        }

        public override void ApplyTo(Material mat)
        {
            // Base Settings
            mat.SetInt("_ToonType", (int)_ToonType);
            if (mat.HasProperty("_PotaToonShaderMode"))
                mat.SetInt("_PotaToonShaderMode", PotaToonGUIUtility.NormalizeShaderMode((int)_ToonType, _PotaToonShaderMode));
            mat.SetInt("_SurfaceType", (int)_SurfaceType);
            mat.SetInt("_Cull", (int)_Cull);
            mat.SetFloat("_Cutoff", _Cutoff);
            mat.SetInt("_ZWriteMode", _ZWriteMode);
            mat.SetInt("_ZTest", (int)_ZTest);
            mat.SetInt("_AutoRenderQueue", _AutoRenderQueue);
            if (mat.HasProperty("_DisableOIT"))
                mat.SetInt("_DisableOIT", _DisableOIT);

            PotaToonShaderGUI.SetRenderQueueAndKeywords(new Material[] { mat }, true, _AutoRenderQueue > 0, false, _RenderQueue);

            // Stencil
            mat.SetInt("_StencilComp", (int)_StencilComp);
            mat.SetFloat("_StencilRef", _StencilRef);
            mat.SetInt("_StencilPass", (int)_StencilPass);
            mat.SetInt("_StencilFail", (int)_StencilFail);
            mat.SetInt("_StencilZFail", (int)_StencilZFail);

            // Pass Control
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.ShadowCaster, _EnableShadowCasterPass);
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.CharacterDepth, _EnableCharacterDepthPass);
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentShadow, _EnableTransparentShadowPass);
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentAlphaSum, _EnableTransparentShadowPass);
            mat.SetShaderPassEnabled(PotaToonShaderGUIBase.Pass.PotaToonCharacterMask, _EnableCharacterMaskPass);

            // Main Settings
            mat.SetColor("_BaseColor", _BaseColor);
            mat.SetColor("_ShadeColor", _ShadeColor);
            mat.SetFloat("_BaseMapHue", _BaseMapHue);
            mat.SetFloat("_BaseMapSaturation", _BaseMapSaturation);
            mat.SetFloat("_BaseMapContrast", _BaseMapContrast);
            mat.SetFloat("_ShadeMapHue", _ShadeMapHue);
            mat.SetFloat("_ShadeMapSaturation", _ShadeMapSaturation);
            mat.SetFloat("_ShadeMapContrast", _ShadeMapContrast);
            mat.SetInt("_ColorGradingMaskReversed", _ColorGradingMaskReversed);
            mat.SetInt("_ShadowExclusionMaskReversed", _ShadowExclusionMaskReversed);
            mat.SetFloat("_BaseStep", _BaseStep);
            mat.SetFloat("_StepSmoothness", _StepSmoothness);
            mat.SetInt("_ReceiveLightShadow", _ReceiveLightShadow);
            mat.SetInt("_UseMidTone", _UseMidTone);
            mat.SetColor("_MidColor", _MidColor);
            mat.SetFloat("_MidWidth", _MidWidth);
            mat.SetFloat("_IndirectDimmer", _IndirectDimmer);
            mat.SetInt("_UseVertexColor", _UseVertexColor);
            mat.SetInt("_UseDarknessMode", _UseDarknessMode);
            mat.SetFloat("_BumpScale", _BumpScale);
            mat.SetInt("_UseNormalMap", _UseNormalMap);
            mat.SetInt("_AlphaMaskMode", (int)_AlphaMaskMode);
            mat.SetFloat("_ClippingMaskCutoff", _ClippingMaskCutoff);
            mat.SetFloat("_AlphaMaskScale", _AlphaMaskScale);
            mat.SetFloat("_AlphaMaskValue", _AlphaMaskValue);

            // High Light
            mat.SetColor("_SpecularColor", _SpecularColor);
            mat.SetFloat("_SpecularPower", _SpecularPower);
            mat.SetFloat("_SpecularSmoothness", _SpecularSmoothness);

            // Rim Light
            mat.SetColor("_RimColor", _RimColor);
            mat.SetFloat("_RimPower", _RimPower);
            mat.SetFloat("_RimSmoothness", _RimSmoothness);
            mat.SetColor("_ScreenRimTint", _ScreenRimTint);
            mat.SetInt("_ScreenRimTintMode", (int)_ScreenRimTintMode);
            mat.SetFloat("_ScreenRimWidthMultiplier", _ScreenRimWidthMultiplier);
            mat.SetFloat("_ScreenRimLightingDimmer", _ScreenRimLightingDimmer);
            mat.SetInt("_ScreenRimShadowFade", _ScreenRimShadowFade);

            // MatCap Layers
            mat.SetInt("_MatCapMode", (int)_MatCapMode);
            mat.SetColor("_MatCapColor", _MatCapColor);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex", _MatCapTex);
            mat.SetFloat("_MatCapWeight", _MatCapWeight);
            mat.SetFloat("_MatCapLightingDimmer", _MatCapLightingDimmer);
            mat.SetInt("_MatCapMode2", (int)_MatCapMode2);
            mat.SetColor("_MatCapColor2", _MatCapColor2);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex2", _MatCapTex2);
            mat.SetFloat("_MatCapWeight2", _MatCapWeight2);
            mat.SetFloat("_MatCapLightingDimmer2", _MatCapLightingDimmer2);
            mat.SetInt("_MatCapMode3", (int)_MatCapMode3);
            mat.SetColor("_MatCapColor3", _MatCapColor3);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex3", _MatCapTex3);
            mat.SetFloat("_MatCapWeight3", _MatCapWeight3);
            mat.SetFloat("_MatCapLightingDimmer3", _MatCapLightingDimmer3);
            mat.SetInt("_MatCapMode4", (int)_MatCapMode4);
            mat.SetColor("_MatCapColor4", _MatCapColor4);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex4", _MatCapTex4);
            mat.SetFloat("_MatCapWeight4", _MatCapWeight4);
            mat.SetFloat("_MatCapLightingDimmer4", _MatCapLightingDimmer4);
            mat.SetInt("_MatCapMode5", (int)_MatCapMode5);
            mat.SetColor("_MatCapColor5", _MatCapColor5);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex5", _MatCapTex5);
            mat.SetFloat("_MatCapWeight5", _MatCapWeight5);
            mat.SetFloat("_MatCapLightingDimmer5", _MatCapLightingDimmer5);
            mat.SetInt("_MatCapMode6", (int)_MatCapMode6);
            mat.SetColor("_MatCapColor6", _MatCapColor6);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex6", _MatCapTex6);
            mat.SetFloat("_MatCapWeight6", _MatCapWeight6);
            mat.SetFloat("_MatCapLightingDimmer6", _MatCapLightingDimmer6);
            mat.SetInt("_MatCapMode7", (int)_MatCapMode7);
            mat.SetColor("_MatCapColor7", _MatCapColor7);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex7", _MatCapTex7);
            mat.SetFloat("_MatCapWeight7", _MatCapWeight7);
            mat.SetFloat("_MatCapLightingDimmer7", _MatCapLightingDimmer7);
            mat.SetInt("_MatCapMode8", (int)_MatCapMode8);
            mat.SetColor("_MatCapColor8", _MatCapColor8);
            SetMaterialTextureIfNeeded(mat, "_MatCapTex8", _MatCapTex8);
            mat.SetFloat("_MatCapWeight8", _MatCapWeight8);
            mat.SetFloat("_MatCapLightingDimmer8", _MatCapLightingDimmer8);

            // Emission
            mat.SetColor("_EmissionColor", _EmissionColor);

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

            // Outline
            mat.SetInt("_OutlineMode", (int)_OutlineMode);
            mat.SetInt("_UseOutlineNormalMap", _UseOutlineNormalMap);
            mat.SetInt("_BlendOutlineMainTex", _BlendOutlineMainTex);
            mat.SetColor("_OutlineColor", _OutlineColor);
            mat.SetFloat("_OutlineWidth", _OutlineWidth);
            mat.SetFloat("_OutlineOffsetZ", _OutlineOffsetZ);
            mat.SetFloat("_OutlineLightingDimmer", _OutlineLightingDimmer);

            // Refraction
            mat.SetFloat("_RefractionWeight", _RefractionWeight);
            mat.SetFloat("_RefractionBlurStep", _RefractionBlurStep);

            // Character Shadow
            mat.SetInt("_DisableCharShadow", _DisableCharShadow);
            mat.SetFloat("_DepthBias", _DepthBias);
            mat.SetFloat("_NormalBias", _NormalBias);
            mat.SetFloat("_CharShadowSmoothnessOffset", _CharShadowSmoothnessOffset);
            mat.SetInt("_CharShadowType", _CharShadowType);
            mat.SetFloat("_2DFaceShadowWidth", _2DFaceShadowWidth);

            // Face SDF
            mat.SetInt("_UseFaceSDFShadow", _UseFaceSDFShadow);
            mat.SetInt("_SDFReverse", _SDFReverse);
            mat.SetFloat("_SDFOffset", _SDFOffset);
            mat.SetFloat("_SDFBlur", _SDFBlur);

            // Hair High Light
            mat.SetInt("_UseHairHighLight", _UseHairHighLight);
            mat.SetInt("_ReverseHairHighLightTex", _ReverseHairHighLightTex);
            mat.SetFloat("_HairHiStrength", _HairHiStrength);
            mat.SetFloat("_HairHiUVOffset", _HairHiUVOffset);

            // UV Channels
            mat.SetInt("_BaseMapUV", (int)_BaseMapUV);
            mat.SetInt("_NormalMapUV", (int)_NormalMapUV);
            mat.SetInt("_ClippingMaskUV", (int)_ClippingMaskUV);
            mat.SetInt("_ColorGradingMaskUV", (int)_ColorGradingMaskUV);
            mat.SetInt("_FaceSDFUV", (int)_FaceSDFUV);
            mat.SetInt("_SpecularMapUV", (int)_SpecularMapUV);
            mat.SetInt("_RimMaskUV", (int)_RimMaskUV);
            mat.SetInt("_HairHiMapUV", (int)_HairHiMapUV);
            mat.SetInt("_GlitterMapUV", (int)_GlitterMapUV);
            mat.SetInt("_EmissionMapUV", (int)_EmissionMapUV);
            mat.SetInt("_OutlineMaskUV", (int)_OutlineMaskUV);
            mat.SetInt("_MatCapUV1", (int)_MatCapUV1);
            mat.SetInt("_MatCapUV2", (int)_MatCapUV2);
            mat.SetInt("_MatCapUV3", (int)_MatCapUV3);
            mat.SetInt("_MatCapUV4", (int)_MatCapUV4);
            mat.SetInt("_MatCapUV5", (int)_MatCapUV5);
            mat.SetInt("_MatCapUV6", (int)_MatCapUV6);
            mat.SetInt("_MatCapUV7", (int)_MatCapUV7);
            mat.SetInt("_MatCapUV8", (int)_MatCapUV8);

            // Mask Channels
            mat.SetInt("_ClippingMaskCH", (int)_ClippingMaskCH);
            mat.SetInt("_SpecularMaskCH", (int)_SpecularMaskCH);
            mat.SetInt("_RimMaskCH", (int)_RimMaskCH);
            mat.SetInt("_EmissionMaskCH", (int)_EmissionMaskCH);
            mat.SetInt("_OutlineMaskCH", (int)_OutlineMaskCH);
            mat.SetInt("_MatCapMaskCH1", (int)_MatCapMaskCH1);
            mat.SetInt("_MatCapMaskCH2", (int)_MatCapMaskCH2);
            mat.SetInt("_MatCapMaskCH3", (int)_MatCapMaskCH3);
            mat.SetInt("_MatCapMaskCH4", (int)_MatCapMaskCH4);
            mat.SetInt("_MatCapMaskCH5", (int)_MatCapMaskCH5);
            mat.SetInt("_MatCapMaskCH6", (int)_MatCapMaskCH6);
            mat.SetInt("_MatCapMaskCH7", (int)_MatCapMaskCH7);
            mat.SetInt("_MatCapMaskCH8", (int)_MatCapMaskCH8);
            mat.SetInt("_AOMapCH", (int)_AOMapCH);
            mat.SetInt("_FaceSDFTexCH", (int)_FaceSDFTexCH);

            PotaToonShaderGUIBase.SyncOITDepthPass(mat);
        }

        public override void SaveFrom(Material mat)
        {
            // Base Settings
            _ToonType                   = (ToonType) mat.GetInt("_ToonType");
            _PotaToonShaderMode         = mat.HasProperty("_PotaToonShaderMode") ? PotaToonGUIUtility.NormalizeShaderMode((int)_ToonType, mat.GetInt("_PotaToonShaderMode")) : 0;
            _SurfaceType                = (SurfaceType) mat.GetInt("_SurfaceType");
            _Cull                       = (CullMode) mat.GetInt("_Cull");
            _Cutoff                     = mat.GetFloat("_Cutoff");
            _ZWriteMode                 = mat.GetInt("_ZWriteMode");
            _ZTest                      = (CompareFunction) mat.GetInt("_ZTest");
            _AutoRenderQueue            = mat.GetInt("_AutoRenderQueue");
            _RenderQueue                = mat.renderQueue;
            _DisableOIT                 = mat.HasProperty("_DisableOIT") ? mat.GetInt("_DisableOIT") : 0;

            // Stencil
            _StencilComp                = (CompareFunction) mat.GetInt("_StencilComp");
            _StencilRef                 = mat.GetFloat("_StencilRef");
            _StencilPass                = (StencilOp) mat.GetInt("_StencilPass");
            _StencilFail                = (StencilOp) mat.GetInt("_StencilFail");
            _StencilZFail               = (StencilOp) mat.GetInt("_StencilZFail");

            // Pass Control
            _EnableShadowCasterPass     = mat.GetShaderPassEnabled(PotaToonShaderGUIBase.Pass.ShadowCaster);
            _EnableCharacterDepthPass   = mat.GetShaderPassEnabled(PotaToonShaderGUIBase.Pass.CharacterDepth);
            _EnableTransparentShadowPass = mat.GetShaderPassEnabled(PotaToonShaderGUIBase.Pass.TransparentShadow);
            _EnableCharacterMaskPass    = mat.GetShaderPassEnabled(PotaToonShaderGUIBase.Pass.PotaToonCharacterMask);

            // Main Settings
            _BaseColor                  = mat.GetColor("_BaseColor");
            _ShadeColor                 = mat.GetColor("_ShadeColor");
            _BaseMapHue                 = mat.GetFloat("_BaseMapHue");
            _BaseMapSaturation          = mat.GetFloat("_BaseMapSaturation");
            _BaseMapContrast            = mat.GetFloat("_BaseMapContrast");
            _ShadeMapHue                = mat.GetFloat("_ShadeMapHue");
            _ShadeMapSaturation         = mat.GetFloat("_ShadeMapSaturation");
            _ShadeMapContrast           = mat.GetFloat("_ShadeMapContrast");
            _ColorGradingMaskReversed   = mat.GetInt("_ColorGradingMaskReversed");
            _ShadowExclusionMaskReversed = mat.GetInt("_ShadowExclusionMaskReversed");
            _BaseStep                   = mat.GetFloat("_BaseStep");
            _StepSmoothness             = mat.GetFloat("_StepSmoothness");
            _ReceiveLightShadow         = mat.GetInt("_ReceiveLightShadow");
            _UseMidTone                 = mat.GetInt("_UseMidTone");
            _MidColor                   = mat.GetColor("_MidColor");
            _MidWidth                   = mat.GetFloat("_MidWidth");
            _IndirectDimmer             = mat.GetFloat("_IndirectDimmer");
            _UseVertexColor             = mat.GetInt("_UseVertexColor");
            _UseDarknessMode            = mat.GetInt("_UseDarknessMode");
            _BumpScale                  = mat.GetFloat("_BumpScale");
            _UseNormalMap               = mat.GetInt("_UseNormalMap");
            _AlphaMaskMode              = mat.HasProperty("_AlphaMaskMode") ? (AlphaMaskMode)mat.GetInt("_AlphaMaskMode") : AlphaMaskMode.Clipping;
            _ClippingMaskCutoff         = mat.HasProperty("_ClippingMaskCutoff") ? mat.GetFloat("_ClippingMaskCutoff") : 0.5f;
            _AlphaMaskScale             = mat.HasProperty("_AlphaMaskScale") ? mat.GetFloat("_AlphaMaskScale") : 1f;
            _AlphaMaskValue             = mat.HasProperty("_AlphaMaskValue") ? mat.GetFloat("_AlphaMaskValue") : 0f;

            // High Light
            _SpecularColor              = mat.GetColor("_SpecularColor");
            _SpecularPower              = mat.GetFloat("_SpecularPower");
            _SpecularSmoothness         = mat.GetFloat("_SpecularSmoothness");

            // Rim Light
            _RimColor                   = mat.GetColor("_RimColor");
            _RimPower                   = mat.GetFloat("_RimPower");
            _RimSmoothness              = mat.GetFloat("_RimSmoothness");
            _ScreenRimTint              = mat.GetColor("_ScreenRimTint");
            _ScreenRimTintMode          = (ScreenRimTintMode) mat.GetInt("_ScreenRimTintMode");
            _ScreenRimWidthMultiplier   = mat.GetFloat("_ScreenRimWidthMultiplier");
            _ScreenRimLightingDimmer    = mat.GetFloat("_ScreenRimLightingDimmer");
            _ScreenRimShadowFade        = mat.GetInt("_ScreenRimShadowFade");

            // MatCap Layers (1-8)
            _MatCapMode                 = (MatCapMode) mat.GetInt("_MatCapMode");
            _MatCapColor                = mat.GetColor("_MatCapColor");
            _MatCapTex                  = mat.GetTexture("_MatCapTex")          as Texture2D;
            _MatCapWeight               = mat.GetFloat("_MatCapWeight");
            _MatCapLightingDimmer       = mat.GetFloat("_MatCapLightingDimmer");
            _MatCapMode2                = (MatCapMode) mat.GetInt("_MatCapMode2");
            _MatCapColor2               = mat.GetColor("_MatCapColor2");
            _MatCapTex2                 = mat.GetTexture("_MatCapTex2")         as Texture2D;
            _MatCapWeight2              = mat.GetFloat("_MatCapWeight2");
            _MatCapLightingDimmer2      = mat.GetFloat("_MatCapLightingDimmer2");
            _MatCapMode3                = (MatCapMode) mat.GetInt("_MatCapMode3");
            _MatCapColor3               = mat.GetColor("_MatCapColor3");
            _MatCapTex3                 = mat.GetTexture("_MatCapTex3")         as Texture2D;
            _MatCapWeight3              = mat.GetFloat("_MatCapWeight3");
            _MatCapLightingDimmer3      = mat.GetFloat("_MatCapLightingDimmer3");
            _MatCapMode4                = (MatCapMode) mat.GetInt("_MatCapMode4");
            _MatCapColor4               = mat.GetColor("_MatCapColor4");
            _MatCapTex4                 = mat.GetTexture("_MatCapTex4")         as Texture2D;
            _MatCapWeight4              = mat.GetFloat("_MatCapWeight4");
            _MatCapLightingDimmer4      = mat.GetFloat("_MatCapLightingDimmer4");
            _MatCapMode5                = (MatCapMode) mat.GetInt("_MatCapMode5");
            _MatCapColor5               = mat.GetColor("_MatCapColor5");
            _MatCapTex5                 = mat.GetTexture("_MatCapTex5")         as Texture2D;
            _MatCapWeight5              = mat.GetFloat("_MatCapWeight5");
            _MatCapLightingDimmer5      = mat.GetFloat("_MatCapLightingDimmer5");
            _MatCapMode6                = (MatCapMode) mat.GetInt("_MatCapMode6");
            _MatCapColor6               = mat.GetColor("_MatCapColor6");
            _MatCapTex6                 = mat.GetTexture("_MatCapTex6")         as Texture2D;
            _MatCapWeight6              = mat.GetFloat("_MatCapWeight6");
            _MatCapLightingDimmer6      = mat.GetFloat("_MatCapLightingDimmer6");
            _MatCapMode7                = (MatCapMode) mat.GetInt("_MatCapMode7");
            _MatCapColor7               = mat.GetColor("_MatCapColor7");
            _MatCapTex7                 = mat.GetTexture("_MatCapTex7")         as Texture2D;
            _MatCapWeight7              = mat.GetFloat("_MatCapWeight7");
            _MatCapLightingDimmer7      = mat.GetFloat("_MatCapLightingDimmer7");
            _MatCapMode8                = (MatCapMode) mat.GetInt("_MatCapMode8");
            _MatCapColor8               = mat.GetColor("_MatCapColor8");
            _MatCapTex8                 = mat.GetTexture("_MatCapTex8")         as Texture2D;
            _MatCapWeight8              = mat.GetFloat("_MatCapWeight8");
            _MatCapLightingDimmer8      = mat.GetFloat("_MatCapLightingDimmer8");

            // Emission
            _EmissionColor              = mat.GetColor("_EmissionColor");

            // Glitter
            _UseGlitter                 = mat.GetInt("_UseGlitter");
            _GlitterColor               = mat.GetColor("_GlitterColor");
            _GlitterMainStrength        = mat.GetFloat("_GlitterMainStrength");
            _GlitterEnableLighting      = mat.GetFloat("_GlitterEnableLighting");
            _GlitterBackfaceMask        = mat.GetInt("_GlitterBackfaceMask");
            _GlitterApplyTransparency   = mat.GetInt("_GlitterApplyTransparency");
            _GlitterShadowMask          = mat.GetFloat("_GlitterShadowMask");
            _GlitterParticleSize        = mat.GetFloat("_GlitterParticleSize");
            _GlitterScaleRandomize      = mat.GetFloat("_GlitterScaleRandomize");
            _GlitterContrast            = mat.GetFloat("_GlitterContrast");
            _GlitterSensitivity         = mat.GetFloat("_GlitterSensitivity");
            _GlitterBlinkSpeed          = mat.GetFloat("_GlitterBlinkSpeed");
            _GlitterAngleLimit          = mat.GetFloat("_GlitterAngleLimit");
            _GlitterLightDirection      = mat.GetFloat("_GlitterLightDirection");
            _GlitterColorRandomness     = mat.GetFloat("_GlitterColorRandomness");
            _GlitterNormalStrength      = mat.GetFloat("_GlitterNormalStrength");
            _GlitterPostContrast        = mat.GetFloat("_GlitterPostContrast");

            // Outline
            _OutlineMode                = (OutlineMode) mat.GetInt("_OutlineMode");
            _UseOutlineNormalMap        = mat.GetInt("_UseOutlineNormalMap");
            _BlendOutlineMainTex        = mat.GetInt("_BlendOutlineMainTex");
            _OutlineColor               = mat.GetColor("_OutlineColor");
            _OutlineWidth               = mat.GetFloat("_OutlineWidth");
            _OutlineOffsetZ             = mat.GetFloat("_OutlineOffsetZ");
            _OutlineLightingDimmer      = mat.GetFloat("_OutlineLightingDimmer");

            // Refraction
            _RefractionWeight           = mat.GetFloat("_RefractionWeight");
            _RefractionBlurStep         = mat.GetFloat("_RefractionBlurStep");

            // Character Shadow
            _DisableCharShadow          = mat.GetInt("_DisableCharShadow");
            _DepthBias                  = mat.GetFloat("_DepthBias");
            _NormalBias                 = mat.GetFloat("_NormalBias");
            _CharShadowSmoothnessOffset = mat.GetFloat("_CharShadowSmoothnessOffset");
            _CharShadowType             = mat.GetInt("_CharShadowType");
            _2DFaceShadowWidth          = mat.GetFloat("_2DFaceShadowWidth");

            // Face SDF
            _UseFaceSDFShadow           = mat.GetInt("_UseFaceSDFShadow");
            _SDFReverse                 = mat.GetInt("_SDFReverse");
            _SDFOffset                  = mat.GetFloat("_SDFOffset");
            _SDFBlur                    = mat.GetFloat("_SDFBlur");

            // Hair High Light
            _UseHairHighLight           = mat.GetInt("_UseHairHighLight");
            _ReverseHairHighLightTex    = mat.GetInt("_ReverseHairHighLightTex");
            _HairHiStrength             = mat.GetFloat("_HairHiStrength");
            _HairHiUVOffset             = mat.GetFloat("_HairHiUVOffset");

            // UV Channels
            _BaseMapUV                  = (UVChannel)      mat.GetInt("_BaseMapUV");
            _NormalMapUV                = (UVChannel)      mat.GetInt("_NormalMapUV");
            _ClippingMaskUV             = (UVChannel)      mat.GetInt("_ClippingMaskUV");
            _ColorGradingMaskUV         = (UVChannel)      mat.GetInt("_ColorGradingMaskUV");
            _FaceSDFUV                  = (UVChannel)      mat.GetInt("_FaceSDFUV");
            _SpecularMapUV              = (UVChannel)      mat.GetInt("_SpecularMapUV");
            _RimMaskUV                  = (UVChannel)      mat.GetInt("_RimMaskUV");
            _HairHiMapUV                = (UVChannel)      mat.GetInt("_HairHiMapUV");
            _GlitterMapUV               = (UVChannel)      mat.GetInt("_GlitterMapUV");
            _EmissionMapUV              = (UVChannel)      mat.GetInt("_EmissionMapUV");
            _OutlineMaskUV              = (UVChannel)      mat.GetInt("_OutlineMaskUV");
            _MatCapUV1                  = (UVChannel)      mat.GetInt("_MatCapUV1");
            _MatCapUV2                  = (UVChannel)      mat.GetInt("_MatCapUV2");
            _MatCapUV3                  = (UVChannel)      mat.GetInt("_MatCapUV3");
            _MatCapUV4                  = (UVChannel)      mat.GetInt("_MatCapUV4");
            _MatCapUV5                  = (UVChannel)      mat.GetInt("_MatCapUV5");
            _MatCapUV6                  = (UVChannel)      mat.GetInt("_MatCapUV6");
            _MatCapUV7                  = (UVChannel)      mat.GetInt("_MatCapUV7");
            _MatCapUV8                  = (UVChannel)      mat.GetInt("_MatCapUV8");

            // Mask Channels
            _ClippingMaskCH             = (MaskChannel)    mat.GetInt("_ClippingMaskCH");
            _SpecularMaskCH             = (MaskChannel)    mat.GetInt("_SpecularMaskCH");
            _RimMaskCH                  = (MaskChannel)    mat.GetInt("_RimMaskCH");
            _EmissionMaskCH             = (MaskChannel)    mat.GetInt("_EmissionMaskCH");
            _OutlineMaskCH              = (MaskChannel)    mat.GetInt("_OutlineMaskCH");
            _MatCapMaskCH1              = (MaskChannel)    mat.GetInt("_MatCapMaskCH1");
            _MatCapMaskCH2              = (MaskChannel)    mat.GetInt("_MatCapMaskCH2");
            _MatCapMaskCH3              = (MaskChannel)    mat.GetInt("_MatCapMaskCH3");
            _MatCapMaskCH4              = (MaskChannel)    mat.GetInt("_MatCapMaskCH4");
            _MatCapMaskCH5              = (MaskChannel)    mat.GetInt("_MatCapMaskCH5");
            _MatCapMaskCH6              = (MaskChannel)    mat.GetInt("_MatCapMaskCH6");
            _MatCapMaskCH7              = (MaskChannel)    mat.GetInt("_MatCapMaskCH7");
            _MatCapMaskCH8              = (MaskChannel)    mat.GetInt("_MatCapMaskCH8");
            _AOMapCH                    = (MaskChannel)    mat.GetInt("_AOMapCH");
            _FaceSDFTexCH               = (MaskChannel)    mat.GetInt("_FaceSDFTexCH");
        }
    }
}
