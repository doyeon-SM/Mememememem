Shader "GH/World/Crack Overlay"
{
    Properties
    {
        [NoScaleOffset] _CrackTex ("Crack Mask", 2D) = "black" {}
        _CrackColor ("Crack Color", Color) = (0.07, 0.045, 0.025, 0.9)
        _Severity ("Damage Severity", Range(0, 1)) = 0
        _Tiling ("UV Tiling", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            Name "CrackOverlay"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CrackTex);
            SAMPLER(sampler_CrackTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _CrackColor;
                half _Severity;
                float _Tiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * max(0.01, _Tiling);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv).r;
                half threshold = lerp(0.96h, 0.18h, saturate(_Severity));
                half revealed = smoothstep(threshold - 0.045h, threshold + 0.025h, mask);
                clip(revealed - 0.01h);
                return half4(_CrackColor.rgb, _CrackColor.a * revealed);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
