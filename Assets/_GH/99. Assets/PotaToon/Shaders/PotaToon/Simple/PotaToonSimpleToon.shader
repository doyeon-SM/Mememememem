Shader "PotaToon/SimpleToon"
{
    Properties
    {
        // Base Settings
        [HideInInspector] _ToonType("Toon Type", Int) = 0
        [HideInInspector] _PotaToonShaderMode("Shader Mode", Int) = 1
        [Enum(Opaque, 0, Cutout, 1, Refraction, 2, Transparent, 3)] _SurfaceType("Surface Type", Int) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Int) = 2
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] [Enum(OFF, 0, ON, 1)] _ZWriteMode("_ZWriteMode", Int) = 1
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("_ZTest", Int) = 4
        [HideInInspector] _AutoRenderQueue("_AutoRenderQueue", Int) = 1
        [Toggle] _DisableOIT("Disable OIT", Int) = 0

        // Stencil
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 0
        _StencilRef("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil ZFail Operation", Float) = 0

        // Main Settings
        _MainTex ("MainTex", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMapHue ("BaseMap Hue", Range(-1, 1)) = 0
        _BaseMapSaturation ("BaseMap Saturation", Range(-1, 1)) = 0
        _BaseMapContrast ("BaseMap Contrast", Range(-1, 1)) = 0
        [HideInInspector] _UseShadeMap ("_UseShadeMap", Int) = 0
        _ShadeMap ("ShadeMap", 2D) = "white" {}
        _ShadeColor ("Shade Color", Color) = (0.75,0.75,0.75,1)
        _ShadeMapHue ("ShadeMap Hue", Range(-1, 1)) = 0
        _ShadeMapSaturation ("ShadeMap Saturation", Range(-1, 1)) = 0
        _ShadeMapContrast ("ShadeMap Contrast", Range(-1, 1)) = 0
        _ColorGradingMask ("Color Grading Mask", 2D) = "white" {}
        [Toggle] _ColorGradingMaskReversed ("Color Grading Mask Reversed", Int) = 0
        _ShadowExclusionMask ("Shadow Exclusion Mask", 2D) = "white" {}
        [Toggle] _ShadowExclusionMaskReversed ("Shadow Exclusion Mask Reversed", Int) = 0
        _ShadowBorderMask ("AOMap", 2D) = "white" {}
        _BaseStep ("Base Step", Range(0, 1)) = 0.5
        _StepSmoothness ("Step Smoothness", Range(0, 0.2)) = 0.01
        [Toggle] _ReceiveLightShadow ("Receive Light Shadow", Int) = 1
        [Toggle] _UseMidTone ("Mid Tone", Int) = 1
        [HDR] _MidColor ("Mid Color", Color) = (0.5,0.2,0.2,1)
        _MidWidth ("Mid Thickness", Range(0, 1)) = 1
        _IndirectDimmer ("Indirect Dimmer", Range(0, 10)) = 1
        [Toggle] _UseVertexColor ("Vertex Color", Int) = 0
        [Toggle] _UseDarknessMode ("Use Darkness Mode", Int) = 0
        _NormalMap ("NormalMap", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 1)) = 0
        [Toggle] _UseNormalMap ("Use NormalMap", Int) = 0
        _ClippingMask ("Alpha Mask", 2D) = "white" {}
        [Enum(None, 0, Clipping, 1, Replace, 2, Multiply, 3, Add, 4, Subtract, 5)] _AlphaMaskMode ("Alpha Mask Mode", Int) = 1
        _ClippingMaskCutoff ("Clipping Cutoff", Range(0, 1)) = 0.5
        _AlphaMaskScale ("Alpha Mask Scale", Float) = 1
        _AlphaMaskValue ("Alpha Mask Offset", Float) = 0

        // High Light
        [HDR] _SpecularColor ("Specular Color", Color) = (0,0,0,1)
        _SpecularMap ("SpecularMap", 2D) = "white" {}
        _SpecularMask ("SpecularMask", 2D) = "white" {}
        _SpecularPower ("Specular Power", Range(0, 1)) = 0.5
        _SpecularSmoothness ("Specular Smoothness", Range(0, 0.5)) = 0.25

        // Rim Light
        [HDR] _RimColor ("RimLight Color", Color) = (0,0,0,1)
        _RimMask ("RimLight Mask", 2D) = "white" {}
        _RimPower ("Rim Power", Range(0, 1)) = 0.5
        _RimSmoothness ("Rim Smoothness", Range(0, 0.5)) = 0.25
        [HDR] _ScreenRimTint ("Screen Rim Tint", Color) = (1,1,1,1)
        [Enum(Multiply, 0, Override, 1)] _ScreenRimTintMode ("Screen Rim Tint Mode", Int) = 0
        _ScreenRimWidthMultiplier ("Screen Rim Width Multiplier", Range(0, 1)) = 1
        _ScreenRimLightingDimmer ("Screen Rim Lighting Dimmer", Range(0, 1)) = 0
        [Toggle] _ScreenRimShadowFade ("Screen Rim Shadow Fade", Int) = 1

        // MatCap
        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode ("MatCap Mode", Int) = 0
        [HDR] _MatCapColor ("MatCap Color", Color) = (1,1,1,1)
        _MatCapTex ("MatCap Tex", 2D) = "white" {}
        _MatCapMask ("MatcapMask", 2D) = "white" {}
        _MatCapWeight ("Matcap Weight", Range(0, 1)) = 1
        _MatCapLightingDimmer ("Matcap Lighting Dimmer", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode2 ("MatCap2 Mode", Int) = 0
        [HDR] _MatCapColor2 ("MatCap2 Color", Color) = (1,1,1,1)
        _MatCapTex2 ("MatCap Tex2", 2D) = "white" {}
        _MatCapMask2 ("MatcapMask2", 2D) = "white" {}
        _MatCapWeight2 ("Matcap Weight2", Range(0, 1)) = 1
        _MatCapLightingDimmer2 ("Matcap Lighting Dimmer2", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode3 ("MatCap3 Mode", Int) = 0
        [HDR] _MatCapColor3 ("MatCap2 Color", Color) = (1,1,1,1)
        _MatCapTex3 ("MatCap Tex3", 2D) = "white" {}
        _MatCapMask3 ("MatcapMask3", 2D) = "white" {}
        _MatCapWeight3 ("Matcap Weight3", Range(0, 1)) = 1
        _MatCapLightingDimmer3 ("Matcap Lighting Dimmer3", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode4 ("MatCap4 Mode", Int) = 0
        [HDR] _MatCapColor4 ("MatCap4 Color", Color) = (1,1,1,1)
        _MatCapTex4 ("MatCap Tex4", 2D) = "white" {}
        _MatCapMask4 ("MatcapMask4", 2D) = "white" {}
        _MatCapWeight4 ("Matcap Weight4", Range(0, 1)) = 1
        _MatCapLightingDimmer4 ("Matcap Lighting Dimmer4", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode5 ("MatCap5 Mode", Int) = 0
        [HDR] _MatCapColor5 ("MatCap5 Color", Color) = (1,1,1,1)
        _MatCapTex5 ("MatCap Tex5", 2D) = "white" {}
        _MatCapMask5 ("MatcapMask5", 2D) = "white" {}
        _MatCapWeight5 ("Matcap Weight5", Range(0, 1)) = 1
        _MatCapLightingDimmer5 ("Matcap Lighting Dimmer5", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode6 ("MatCap6 Mode", Int) = 0
        [HDR] _MatCapColor6 ("MatCap6 Color", Color) = (1,1,1,1)
        _MatCapTex6 ("MatCap Tex6", 2D) = "white" {}
        _MatCapMask6 ("MatcapMask6", 2D) = "white" {}
        _MatCapWeight6 ("Matcap Weight6", Range(0, 1)) = 1
        _MatCapLightingDimmer6 ("Matcap Lighting Dimmer6", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode7 ("MatCap7 Mode", Int) = 0
        [HDR] _MatCapColor7 ("MatCap7 Color", Color) = (1,1,1,1)
        _MatCapTex7 ("MatCap Tex7", 2D) = "white" {}
        _MatCapMask7 ("MatcapMask7", 2D) = "white" {}
        _MatCapWeight7 ("Matcap Weight7", Range(0, 1)) = 1
        _MatCapLightingDimmer7 ("Matcap Lighting Dimmer7", Range(0, 1)) = 1

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode8 ("MatCap8 Mode", Int) = 0
        [HDR] _MatCapColor8 ("MatCap8 Color", Color) = (1,1,1,1)
        _MatCapTex8 ("MatCap Tex8", 2D) = "white" {}
        _MatCapMask8 ("MatcapMask8", 2D) = "white" {}
        _MatCapWeight8 ("Matcap Weight8", Range(0, 1)) = 1
        _MatCapLightingDimmer8 ("Matcap Lighting Dimmer8", Range(0, 1)) = 1

        // Emission
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
        _EmissionMap ("EmissionMap", 2D) = "white" {}
        _EmissionMask ("EmissionMask", 2D) = "white" {}

        // Glitter
        [HideInInspector] [Toggle] _UseGlitter ("_UseGlitter", Int) = 0
        [HDR] _GlitterColor("Color", Color) = (1,1,1,1)
        _GlitterColorTex("Texture", 2D) = "white" {}
        _GlitterMainStrength("Main Color Strength", Range(0, 1)) = 0
        _GlitterEnableLighting("Enable Lighting", Range(0, 1)) = 1
        [Toggle] _GlitterBackfaceMask("Backface Mask", Int) = 0
        [Toggle] _GlitterApplyTransparency("Apply Transparency", Int) = 1
        _GlitterShadowMask("Shadow Mask", Range(0, 1)) = 0
        _GlitterParticleSize("Particle Size", Float) = 0.16
        _GlitterScaleRandomize("Scale Randomize", Range(0, 1)) = 0
        _GlitterContrast("Contrast", Float) = 50
        _GlitterSensitivity("Sensitivity", Float) = 100
        _GlitterBlinkSpeed("Blink Speed", Float) = 0.1
        _GlitterAngleLimit("Angle Limit", Float) = 0
        _GlitterLightDirection("Light Direction Strength", Float) = 0
        _GlitterColorRandomness("Color Randomness", Range(0, 1)) = 0
        _GlitterNormalStrength("NormalMap Strength", Range(0, 1)) = 1.0
        _GlitterPostContrast("Post Contrast", Float) = 1

        // Outline
        [Enum(Normal, 0, Position, 1)] _OutlineMode("Outline Mode", Int) = 0
        [HideInInspector] [Toggle] _UseOutlineNormalMap("_UseOutlineNormalMap", Int) = 0
        _OutlineNormalMap ("NormalMap", 2D) = "bump" {}
        [Toggle] _BlendOutlineMainTex("Blend MainTex", Int) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidthMask ("Outline Width Mask", 2D) = "white" {}
        _OutlineWidth ("Outline Width", Range(0, 10)) = 0.1
        _OutlineOffsetZ ("Depth Offset", Float) = 0
        _OutlineLightingDimmer ("Outline Lighting Dimmer", Range(0, 1)) = 1

        // Refraction
        _RefractionWeight ("Refraction Weight", Range(-1, 1)) = 0
        _RefractionBlurStep ("Refraction Blur Step", Range(0, 10)) = 0

        // Character Shadow
        [Toggle] _DisableCharShadow ("Disable Cast Shadow", Int) = 0
        _DepthBias ("Cast Shadow Bias", Range(-1, 1)) = 0
        _NormalBias ("Cast Shadow Normal Bias", Range(-1, 1)) = 0
        _CharShadowSmoothnessOffset ("Smoothness", Range(0, 1)) = 0
        [HideInInspector] [Enum(3D, 0, 2D Face, 1)] _CharShadowType ("_CharShadowType", Int) = 0
        _2DFaceShadowWidth ("2D Shadow Width", Range(0, 1)) = 0.1

        // Face SDF
        [HideInInspector] [Toggle] _UseFaceSDFShadow ("_UseFaceSDFShadow", Int) = 0
        _FaceSDFTex ("Face SDF", 2D) = "black" {}
        [Toggle] _SDFReverse("Reverse Face SDF", Int) = 0
        _SDFOffset("SDF_Offset", Range(-0.5, 0.5)) = 0
        _SDFBlur("SDF_Blur", Range(0, 1)) = 0
        [HideInInspector] _FaceForward ("_FaceForward", Vector) = (0,0,1,0)
        [HideInInspector] _FaceUp ("_FaceUp", Vector) = (0,1,0,0)

        // Hair High Light
        [Toggle] _UseHairHighLight ("Use Hair High Light", Int) = 0
        _HairHighLightTex ("Hair Highlight Tex", 2D) = "black" {}
        [Toggle] _ReverseHairHighLightTex ("Reverse Hair Highlight Tex", Int) = 0
        _HairHiStrength("Hair Hi Strength", Range(0, 2)) = 1
        _HairHiUVOffset("Hair Hi UV Offset", Range(-1, 1)) = 0
        [HideInInspector] _HeadWorldPos ("_HeadWorldPos", Vector) = (0,0,0,0)

        // Dither Fade
        [HideInInspector] _UseDitherFade ("_UseDitherFade", Int) = 0
        [HideInInspector] _DitherFadeMinZ ("_DitherFadeMinZ", Float) = 0
        [HideInInspector] _DitherFadeMaxZ ("_DitherFadeMaxZ", Float) = 0

        // UV Channels
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _BaseMapUV ("Base UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _NormalMapUV ("NormalMap UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _ClippingMaskUV ("ClippingMask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _ColorGradingMaskUV ("Color Grading Mask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _FaceSDFUV ("FaceSDF UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _SpecularMapUV ("Specular UV", Int) = 0
        [HideInInspector] _RimMaskUV ("RimMask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _HairHiMapUV ("HairHigh UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _GlitterMapUV ("Glitter UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _EmissionMapUV ("Emission UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _OutlineMaskUV ("OutlineMask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV1 ("MatCap UV1", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV2 ("MatCap UV2", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV3 ("MatCap UV3", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV4 ("MatCap UV4", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV5 ("MatCap UV5", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV6 ("MatCap UV6", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV7 ("MatCap UV7", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV8 ("MatCap UV8", Int) = 0

        // Mask Channels
        [Enum(R, 0, G, 1, B, 2, A, 3)] _ClippingMaskCH ("ClippingMask Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _SpecularMaskCH ("SpecularMask Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _RimMaskCH ("RimMask Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _EmissionMaskCH ("EmissionMask Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _OutlineMaskCH ("OutlineMask Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH1 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH2 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH3 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH4 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH5 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH6 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH7 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH8 ("MatCapMask Channel", Int) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _AOMapCH ("AOMap Channel", Int) = 1
        [Enum(R, 0, G, 1, B, 2, A, 3)] _FaceSDFTexCH ("FaceSDF Channel", Int) = 0

        // Blend
        [Enum(PotaToon.AlphaMode)] _Blend("__blend", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend       ("SrcBlendRGB", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend       ("DstBlendRGB", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha  ("SrcBlendAlpha", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha  ("DstBlendAlpha", Int) = 10

        // Debug
        [Enum(None, 0, Lighting, 1, Texture, 2)] _DebugFaceSDF ("Debug Face SDF", Int) = 0
    }
    SubShader
    {
        PackageRequirements
        {
             "com.unity.render-pipelines.universal": "12.0.0"
        }
        Tags
        {
            "RenderType"="Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "ForwardLit"
            Tags{"LightMode" = "UniversalForward"}
            ZWrite[_ZWriteMode]
            ZTest[_ZTest]
            Cull[_Cull]
            Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
                Pass[_StencilPass]
                Fail[_StencilFail]
                ZFail[_StencilZFail]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "./PotaToonSimpleToonForwardKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./PotaToonSimpleToonInput.hlsl"
            #include "./PotaToonSimpleToonPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags
            {
                "LightMode"="SRPDefaultUnlit"
            }
            Cull Front
            ZTest[_ZTest]
            Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
                Pass[_StencilPass]
                Fail[_StencilFail]
                ZFail[_StencilZFail]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "./PotaToonSimpleToonOutlineKeywords.hlsl"
            #include "./PotaToonSimpleToonInput.hlsl"
            #include "./PotaToonSimpleToonOutlinePass.hlsl"

            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "./PotaToonSimpleToonInput.hlsl"
            #define _BaseMap _MainTex
            #define sampler_BaseMap sampler_MainTex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "./PotaToonSimpleToonInput.hlsl"
            #define _BaseMap _MainTex
            #define sampler_BaseMap sampler_MainTex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        // This pass is used when drawing to a _CameraNormalsTexture texture
        Pass
        {
            Name "DepthNormals"
            Tags{"LightMode" = "DepthNormals"}

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

#if UNITY_VERSION >= 202230
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
#endif

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "./PotaToonSimpleToonInput.hlsl"
            #define _BaseMap _MainTex
            #define sampler_BaseMap sampler_MainTex
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"

            ENDHLSL
        }

        Pass
        {
           Name "CharacterDepth"
           Tags{"LightMode" = "CharacterDepth"}

           ZWrite On
           ZTest LEqual
           Cull Off
           BlendOp Max

           HLSLPROGRAM
           #pragma target 4.5

           #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
           #pragma shader_feature_local _ALPHATEST_ON

           #pragma vertex CharShadowVertex
           #pragma fragment CharShadowFragment

           #define _BaseMapUV 0
           #define _ClippingMaskUV 0
           #include "./PotaToonSimpleToonInput.hlsl"
           #include "../../ChracterShadow/CharacterShadowDepthPass.hlsl"
           ENDHLSL
        }

        Pass
        {
            Name "PotaToonCharacterMask"
            Tags {
               "LightMode" = "PotaToonCharacterMask"
            }
            ZWrite Off ZTest Always Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #define _USE_DITHER_FADE 1
            #define _BaseMapUV 0
            #define _ClippingMaskUV 0
            #include "./PotaToonSimpleToonInput.hlsl"
            #include "../PotaToonCharMaskPass.hlsl"

            ENDHLSL
        }
        Pass
        {
            // Must be sync with ForwardLit pass
            Name "OpaqueDitherFade"
            Tags{"LightMode" = "OpaqueDitherFade"}
            Cull[_Cull]
            Blend SrcAlpha Zero
            ZWrite[_ZWriteMode]
            ZTest[_ZTest]
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
                Pass[_StencilPass]
                Fail[_StencilFail]
                ZFail[_StencilZFail]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "./PotaToonSimpleToonDitherFadeForwardKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #define _USE_DITHER_FADE 1
            #include "./PotaToonSimpleToonInput.hlsl"
            #include "./PotaToonSimpleToonPass.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "OpaqueDitherFadeOutline"
            Tags {
               "LightMode" = "OpaqueDitherFadeOutline"
            }
            Cull Front
            ZWrite On
            ZTest[_ZTest]
            Blend SrcAlpha Zero
            ColorMask RGBA
            Stencil
            {
                Ref[_StencilRef]
                Comp[_StencilComp]
                Pass[_StencilOpPass]
                Fail[_StencilOpFail]
                ZFail[_StencilOpZFail]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include_with_pragmas "./PotaToonSimpleToonDitherFadeOutlineKeywords.hlsl"
            #define _USE_DITHER_FADE 1
            #include "./PotaToonSimpleToonInput.hlsl"
            #include "./PotaToonSimpleToonOutlinePass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "PotaToon.Editor.PotaToonSimpleShaderGUI"
}