Shader "PotaToon/Gem"
{
    Properties
    {
        // Base Settings
        [HideInInspector] _ToonType("Toon Type", Int) = 3
        [Enum(Opaque, 0, Cutout, 1, Refraction, 2, Transparent, 3)] _SurfaceType("Surface Type", Int) = 2
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Int) = 2
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] [Enum(OFF, 0, ON, 1)] _ZWriteMode("_ZWriteMode", Int) = 0
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("_ZTest", Int) = 4
        [HideInInspector] _AutoRenderQueue("_AutoRenderQueue", Int) = 1
        [Toggle] _DisableOIT("Disable OIT", Int) = 0
        
        // Stencil
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 0
        _StencilRef("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil ZFail Operation", Float) = 0
        
        // Base
        _MainTex ("Base Map", 2D) = "white" {}
        [HDR] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _BaseMapHue ("BaseMap Hue", Range(-1, 1)) = 0
        _BaseMapSaturation ("BaseMap Saturation", Range(-1, 1)) = 0
        _BaseMapContrast ("BaseMap Contrast", Range(-1, 1)) = 0
        _ClippingMask ("Alpha Mask", 2D) = "white" {}
        [Enum(None, 0, Clipping, 1, Replace, 2, Multiply, 3, Add, 4, Subtract, 5)] _AlphaMaskMode ("Alpha Mask Mode", Int) = 1
        _ClippingMaskCutoff ("Clipping Cutoff", Range(0, 1)) = 0.5
        _AlphaMaskScale ("Alpha Mask Scale", Float) = 1
        _AlphaMaskValue ("Alpha Mask Offset", Float) = 0
        [Enum(R, 0, G, 1, B, 2, A, 3)] _ClippingMaskCH ("Clipping Mask Channel", Int) = 1
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 1)) = 0
        [Toggle] _UseNormalMap ("Use Normal Map", Int) = 0
        _Roughness ("Roughness", Range(0, 1)) = 0.2
        _IndirectDimmer ("Indirect Dimmer", Range(0, 10)) = 1
        [Toggle] _ReceiveLightShadow ("Receive Light Shadow", Int) = 1
        
        // Specular
        _GemType ("Gem Type", Int) = 0
        [HideInInspector] _GemTypeVersion ("_GemTypeVersion", Int) = 0
        _GemShine ("Shine", Range(0, 1)) = 0.5

        // Clearcoat
        _ClearcoatIntensity ("Clearcoat Intensity", Range(0, 1)) = 0.4
        _ClearcoatRoughness ("Clearcoat Roughness", Range(0, 1)) = 0.05

        // Transmission
        _TransmissionStrength ("Transmission Strength", Range(0, 1)) = 0.6
        [HDR] _AbsorptionColor ("Absorption Color", Color) = (0.2,0.4,0.6,1)
        [HideInInspector] _Thickness ("Thickness", Range(0, 5)) = 1
        _BaseStrength ("Base Strength", Range(0, 1)) = 0
        _ChromaticAberration ("Chromatic Aberration", Range(0, 1)) = 0
        
        // Refraction
        _RefractionStrength ("Refraction Strength", Range(-1, 1)) = 0.2
        _RefractionBlurWeight ("Refraction Blur Weight", Range(0, 1)) = 0
        _RefractionFresnelPower ("Refraction Fresnel Power", Range(0.01, 10)) = 1.0

        // Gem
        _ParticleIntensity ("Particle Intensity", Range(0, 1)) = 0
        _ParticleLightingDimmer ("Particle Lighting Dimmer", Range(0, 1)) = 0
        _ParticleLoop ("Particle Loop", Range(1, 32)) = 8
        [HDR] _ParticleColor ("Particle Color", Color) = (1,1,1,1)

        // Glitter
        [Toggle] _UseGlitter ("Use Glitter", Int) = 0
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

        // Rim
        [Toggle] _UseRim ("Use Rim", Int) = 0
        [HDR] _RimColor ("Rim Color", Color) = (1,1,1,0)
        _RimPower ("Rim Power", Range(0, 1)) = 0.25
        _RimSmoothness ("Rim Smoothness", Range(0, 1)) = 0.05
        _RimMask ("Rim Mask", 2D) = "white" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _RimMaskCH ("Rim Mask Channel", Int) = 0
        [HDR] _ScreenRimTint ("Screen Rim Tint", Color) = (1,1,1,1)
        [Enum(Multiply, 0, Override, 1)] _ScreenRimTintMode ("Screen Rim Tint Mode", Int) = 0
        _ScreenRimWidthMultiplier ("Screen Rim Width Multiplier", Range(0, 1)) = 1
        _ScreenRimLightingDimmer ("Screen Rim Lighting Dimmer", Range(0, 1)) = 0
        [Toggle] _ScreenRimShadowFade ("Screen Rim Shadow Fade", Int) = 1

        // MatCap
        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode ("MatCap Mode", Int) = 0
        _MatCapTex ("MatCap", 2D) = "white" {}
        _MatCapMask ("MatCap Mask", 2D) = "white" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH ("MatCap Mask Channel", Int) = 0
        [HDR] _MatCapColor ("MatCap Color", Color) = (1,1,1,0)
        _MatCapWeight ("MatCap Weight", Range(0, 1)) = 1
        _MatCapLightingDimmer ("MatCap Lighting Dimmer", Range(0, 1)) = 0

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode2 ("MatCap Mode 2", Int) = 0
        _MatCapTex2 ("MatCap 2", 2D) = "white" {}
        _MatCapMask2 ("MatCap Mask 2", 2D) = "white" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH2 ("MatCap Mask Channel 2", Int) = 0
        [HDR] _MatCapColor2 ("MatCap Color 2", Color) = (1,1,1,0)
        _MatCapWeight2 ("MatCap Weight 2", Range(0, 1)) = 1
        _MatCapLightingDimmer2 ("MatCap Lighting Dimmer 2", Range(0, 1)) = 0

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode3 ("MatCap Mode 3", Int) = 0
        _MatCapTex3 ("MatCap 3", 2D) = "white" {}
        _MatCapMask3 ("MatCap Mask 3", 2D) = "white" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH3 ("MatCap Mask Channel 3", Int) = 0
        [HDR] _MatCapColor3 ("MatCap Color 3", Color) = (1,1,1,0)
        _MatCapWeight3 ("MatCap Weight 3", Range(0, 1)) = 1
        _MatCapLightingDimmer3 ("MatCap Lighting Dimmer 3", Range(0, 1)) = 0

        [Enum(None, 0, Add, 1, Multiply, 2)] _MatCapMode4 ("MatCap Mode 4", Int) = 0
        _MatCapTex4 ("MatCap 4", 2D) = "white" {}
        _MatCapMask4 ("MatCap Mask 4", 2D) = "white" {}
        [Enum(R, 0, G, 1, B, 2, A, 3)] _MatCapMaskCH4 ("MatCap Mask Channel 4", Int) = 0
        [HDR] _MatCapColor4 ("MatCap Color 4", Color) = (1,1,1,0)
        _MatCapWeight4 ("MatCap Weight 4", Range(0, 1)) = 1
        _MatCapLightingDimmer4 ("MatCap Lighting Dimmer 4", Range(0, 1)) = 0

        // Character Shadow
        [Toggle] _DisableCharShadow ("Disable Cast Shadow", Int) = 0
        _CharShadowSmoothnessOffset ("Smoothness", Range(0, 1)) = 0
        
        // Dither Fade
        [HideInInspector] _HeadWorldPos ("_HeadWorldPos", Vector) = (0,0,0,0)
        [HideInInspector] _UseDitherFade ("_UseDitherFade", Int) = 0
        [HideInInspector] _DitherFadeMinZ ("_DitherFadeMinZ", Float) = 0
        [HideInInspector] _DitherFadeMaxZ ("_DitherFadeMaxZ", Float) = 0

        // UV Channels
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _BaseMapUV ("Base UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _NormalMapUV ("NormalMap UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _ClippingMaskUV ("ClippingMask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _RimMaskUV ("RimMask UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _GlitterMapUV ("Glitter UV", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV1 ("MatCap UV1", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV2 ("MatCap UV2", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV3 ("MatCap UV3", Int) = 0
        [Enum(UV0, 0, UV1, 1, UV2, 2, UV3, 3)] _MatCapUV4 ("MatCap UV4", Int) = 0

        // Blend
        [Enum(PotaToon.AlphaMode)] _Blend("__blend", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend       ("SrcBlendRGB", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend       ("DstBlendRGB", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha  ("SrcBlendAlpha", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha  ("DstBlendAlpha", Int) = 10
    }
    SubShader
    {
        PackageRequirements
        {
             "com.unity.render-pipelines.universal": "12.0.0"
        }    
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
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

            #include_with_pragmas "./PotaToonGemKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "./PotaToonGemInput.hlsl"
            #include "./PotaToonGemPass.hlsl"
            ENDHLSL
        }
        Pass
        {
           Name "TransparentShadow"
           Tags {"LightMode" = "TransparentShadow"}

           ZWrite Off
           ZTest Off
           Cull Off
           Blend One One
           BlendOp Max

           HLSLPROGRAM
           #pragma target 4.5

           #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
           #pragma shader_feature_local _ALPHATEST_ON

           #pragma vertex TransparentShadowVert
           #pragma fragment TransparentShadowFragment

           #include "./PotaToonGemInput.hlsl"
           #include "../../ChracterShadow/TransparentShadowPass.hlsl"
           ENDHLSL
        }
        Pass
        {
           Name "TransparentAlphaSum"
           Tags {"LightMode" = "TransparentAlphaSum"}

           ZWrite Off
           ZTest Off
           Cull Off
           Blend One One
           BlendOp Add

           HLSLPROGRAM
           #pragma target 4.5

           #pragma shader_feature_local _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
           #pragma shader_feature_local _ALPHATEST_ON

           #pragma vertex TransparentAlphaSumVert
           #pragma fragment TransparentAlphaSumFragment

           #include "./PotaToonGemInput.hlsl"
           #include "../../ChracterShadow/TransparentShadowPass.hlsl"
           ENDHLSL
        }
        Pass
        {
           Name "OITDepth"
           Tags {
               "LightMode" = "OITDepth"
           }
           ZWrite Off
           ZTest Always
           Cull OFF
           ColorMask R
           BlendOp Max

           HLSLPROGRAM
           #pragma target 4.5
           #pragma vertex vert
           #pragma fragment frag

           #include "./PotaToonGemInput.hlsl"

           struct Attributes
           {
               float4 position     : POSITION;
           };
           struct Varyings
           {
               float4 positionCS   : SV_POSITION;
           };

           Varyings vert(Attributes input)
           {
               Varyings output = (Varyings)0;
               output.positionCS = TransformObjectToHClip(input.position.xyz);
               return output;
           }

           float frag(Varyings input) : SV_TARGET
           {
               if (_DisableOIT > 0)
                   clip(-1);
               return input.positionCS.z;
           }
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
            #include "./PotaToonGemInput.hlsl"
            #include "../PotaToonCharMaskPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "PotaToon.Editor.PotaToonGemShaderGUI"
}