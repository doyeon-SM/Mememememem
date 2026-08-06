// Copied from URP Blit.shader
Shader "Hidden/PotaToon/Blit"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend       ("SrcBlendRGB", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend       ("DstBlendRGB", Int) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendAlpha  ("SrcBlendAlpha", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendAlpha  ("DstBlendAlpha", Int) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100

        Pass
        {
            Name "Blit"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend [_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment

            // Core.hlsl for XR dependencies
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(sampler_BlitTexture);

            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv);
                return col;
            }
            ENDHLSL
        }
    }
}
