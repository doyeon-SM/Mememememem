Shader "GH/World/Crack Overlay"
{
    Properties
    {
        [NoScaleOffset] _CrackTex ("Crack Mask", 2D) = "black" {}
        _CrackColor ("Crack Color", Color) = (0.07, 0.045, 0.025, 0.9)
        _CrackHighlightColor ("Crack Highlight Color", Color) = (1, 0.58, 0.16, 0.72)
        _CrackHighlightWidth ("Crack Highlight Width", Range(0.5, 4)) = 2
        _CrackHighlightStrength ("Crack Highlight Strength", Range(0, 1)) = 0.72
        _ImpactFlashColor ("Impact Flash Color", Color) = (1, 0.72, 0.34, 0.42)
        _ImpactFlash ("Impact Flash", Range(0, 1)) = 0
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
            float4 _CrackTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _CrackColor;
                half4 _CrackHighlightColor;
                half _CrackHighlightWidth;
                half _CrackHighlightStrength;
                half4 _ImpactFlashColor;
                half _ImpactFlash;
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
                float2 texel = _CrackTex_TexelSize.xy * max(0.5h, _CrackHighlightWidth);
                half expandedMask = mask;
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv + float2(texel.x, 0)).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv - float2(texel.x, 0)).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv + float2(0, texel.y)).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv - float2(0, texel.y)).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv + texel).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(_CrackTex, sampler_CrackTex, input.uv - texel).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(
                        _CrackTex,
                        sampler_CrackTex,
                        input.uv + float2(texel.x, -texel.y)).r);
                expandedMask = max(
                    expandedMask,
                    SAMPLE_TEXTURE2D(
                        _CrackTex,
                        sampler_CrackTex,
                        input.uv + float2(-texel.x, texel.y)).r);

                half threshold = lerp(0.96h, 0.18h, saturate(_Severity));
                half core = smoothstep(threshold - 0.045h, threshold + 0.025h, mask);
                half expanded =
                    smoothstep(threshold - 0.045h, threshold + 0.025h, expandedMask);
                half highlight = saturate(expanded - core) * _CrackHighlightStrength;
                half crackAlpha =
                    max(_CrackColor.a * core, _CrackHighlightColor.a * highlight);
                half3 crackVisual = lerp(
                    _CrackHighlightColor.rgb,
                    _CrackColor.rgb,
                    saturate(core));
                half flashAlpha = _ImpactFlashColor.a * saturate(_ImpactFlash);
                half alpha = max(crackAlpha, flashAlpha);
                half flashWeight = saturate(flashAlpha / max(0.001h, alpha));
                half3 color = lerp(crackVisual, _ImpactFlashColor.rgb, flashWeight);
                clip(alpha - 0.01h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
