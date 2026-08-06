Shader "PotaToon/Eye"
{
    Properties
    {
        // Base Settings
        [HideInInspector] _ToonType("Toon Type", Int) = 2
        [Enum(Opaque, 0, Cutout, 1, Refraction, 2, Transparent, 3)] _SurfaceType("Surface Type", Int) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Int) = 2
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] [Enum(OFF, 0, ON, 1)] _ZWriteMode("_ZWriteMode", Int) = 1
        [HideInInspector] [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("_ZTest", Int) = 4
        [HideInInspector] _AutoRenderQueue("_AutoRenderQueue", Int) = 1
        
        // Stencil
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 0
        _StencilRef("Stencil Ref", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass("Stencil Pass Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilFail("Stencil Fail Operation", Float) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilZFail("Stencil ZFail Operation", Float) = 0
        
        // Settings
        [HDR] _BaseColor("Color", Color) = (1,1,1,1)
        _MainTex("Main Tex", 2D) = "white" {}
        _BaseMapHue ("BaseMap Hue", Range(-1, 1)) = 0
        _BaseMapSaturation ("BaseMap Saturation", Range(-1, 1)) = 0
        _BaseMapContrast ("BaseMap Contrast", Range(-1, 1)) = 0
        _ClippingMask ("Alpha Mask", 2D) = "white" {}
        [Enum(None, 0, Clipping, 1, Replace, 2, Multiply, 3, Add, 4, Subtract, 5)] _AlphaMaskMode ("Alpha Mask Mode", Int) = 1
        _ClippingMaskCutoff ("Clipping Cutoff", Range(0, 1)) = 0.5
        _AlphaMaskScale ("Alpha Mask Scale", Float) = 1
        _AlphaMaskValue ("Alpha Mask Offset", Float) = 0
        _Exposure("Exposure", Range(1, 10)) = 1
        _MinIntensity("Minium Intensity", Range(0, 1)) = 0.1
        _IndirectDimmer ("Indirect Dimmer", Range(0, 10)) = 0
        [Toggle] _UseRefraction("Use Refraction", Int) = 1
        _RefractionWeight("Refraction Weight", Range(-0.1, 0.1)) = 0
        [HideInInspector] [Toggle] _UseHiLight("_UseHiLight", Int) = 0
        _HiLightTex("HighLight Tex", 2D) = "black" {}
        [Toggle] _UseHiLightJitter("Use Jittering", Int) = 0
        [HDR] _HiLightColor("HighLight Color", Color) = (1,1,1,1)
        _HiLightPowerR("HighLight Power for R Channel", Range(1, 64)) = 1
        _HiLightPowerG("HighLight Power for G Channel", Range(1, 64)) = 1
        _HiLightPowerB("HighLight Power for B Channel", Range(1, 64)) = 1
        _HiLightIntensityR("HighLight Intensity for R Channel", Range(0, 1)) = 1
        _HiLightIntensityG("HighLight Intensity for G Channel", Range(0, 1)) = 1
        _HiLightIntensityB("HighLight Intensity for B Channel", Range(0, 1)) = 1
        
        [Enum(R, 0, G, 1, B, 2, A, 3)] _ClippingMaskCH ("ClippingMask Channel", Int) = 1
        
        [HideInInspector] _FaceForward ("_FaceForward", Vector) = (0,0,1,0)
        [HideInInspector] _FaceUp ("_FaceUp", Vector) = (0,1,0,0)
        [HideInInspector] _HeadWorldPos ("_HeadWorldPos", Vector) = (0,0,0,0)
        
        // Dither Fade
        [HideInInspector] _UseDitherFade ("_UseDitherFade", Int) = 0
        [HideInInspector] _DitherFadeMinZ ("_DitherFadeMinZ", Float) = 0
        [HideInInspector] _DitherFadeMaxZ ("_DitherFadeMaxZ", Float) = 0
        
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
            "RenderType"="Opaque"
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

            #include_with_pragmas "./PotaToonEyeForwardKeywords.hlsl"
            #include "./PotaToonEyeInput.hlsl"
            #include "./PotaToonEyePass.hlsl"

            ENDHLSL
        }
        // Skip Shadow Caster Pass
        Pass
        {
            Name "DepthOnly"
            Tags{"LightMode" = "DepthOnly"}

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            // Material Keywords
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "./PotaToonEyeInput.hlsl"
            #define _BaseMap _MainTex
            #define sampler_BaseMap sampler_linear_mirror
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
            #pragma target 2.0

            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _GLOSSINESS_FROM_BASE_ALPHA

            //--------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing

            #include "./PotaToonEyeInput.hlsl"
            #define _BaseMap _MainTex
            #define sampler_BaseMap sampler_linear_mirror
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"

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
            #pragma vertex vert
            #pragma fragment frag

            #define _USE_DITHER_FADE 1
            #define _BaseMapUV 0
            #define _ClippingMaskUV 0
            #include "./PotaToonEyeInput.hlsl"
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

            #include_with_pragmas "./PotaToonEyeDitherFadeForwardKeywords.hlsl"
            #define _USE_DITHER_FADE 1
            #include "./PotaToonEyeInput.hlsl"
            #include "./PotaToonEyePass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "PotaToon.Editor.PotaToonEyeShaderGUI"
}