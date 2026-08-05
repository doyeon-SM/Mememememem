#ifndef POTA_TOON_RIM_INCLUDED
#define POTA_TOON_RIM_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../Common/PotaToonGlobalInput.hlsl"

inline bool HasRimLighting()
{
    return _RimColor.a > 0 && any(abs(_RimColor.rgb) > 1e-5h);
}

half3 RimLighting(float3 normalWS, float3 viewDirection, half3 lighting, float facing, float2 uv, float charShadowAtten)
{
#if _USE_FACE_SDF
    return 0;
#else
    if (!HasRimLighting() || facing < 0.1 || charShadowAtten > 0)
        return 0;

    if (max(lighting.r, max(lighting.g, lighting.b)) <= HALF_MIN)
        return 0;

    float fresnel = 1 - saturate(dot(viewDirection, normalWS));
    if (fresnel <= HALF_MIN)
        return 0;

    float emission = LinearStep(0.5 - _RimSmoothness, 0.5 + _RimSmoothness, pow(fresnel, _RimPower * 8));
    if (emission <= HALF_MIN)
        return 0;

    float rimMask = SelectMask(SAMPLE_TEXTURE2D(_RimMask, sampler_MainTex, uv), _RimMaskCH);
    if (rimMask <= HALF_MIN)
        return 0;

    half3 rimColor = _RimColor.rgb * (emission * _RimColor.a);
    return rimColor * lighting * rimMask;
#endif
}

void ScreenRimLighting(inout half3 color, float2 ssUV, float3 positionWS, float charShadowAtten, half3 lighting, float3 lightDirection)
{
    const float sampleWidth = _ScreenRimWidth * _ScreenRimWidthMultiplier;
    const half shadowFade = lerp(1.0, (1.0 - charShadowAtten), _ScreenRimShadowFade);
    if (sampleWidth < HALF_MIN || shadowFade <= HALF_MIN)
        return;

    half3 targetColor = lerp(_ScreenRimColor.rgb * _ScreenRimTint.rgb, _ScreenRimTint.rgb, _ScreenRimTintMode);
    half3 litFactor = lerp(1, max(lighting, 0), _ScreenRimLightingDimmer);
    if (all(abs(targetColor * shadowFade * litFactor) <= 1e-5h))
        return;

    if (dot(lightDirection, lightDirection) <= 1e-6)
        return;

    const float sceneDepth = SampleSceneDepth(ssUV);
    float3 lightDirCS = TransformWorldToHClipDir(lightDirection);

#if UNITY_UV_STARTS_AT_TOP
    lightDirCS.y = -lightDirCS.y;
#endif

    float3 rimPositionVS = TransformWorldToView(positionWS) + float3(normalize(lightDirCS).xy * sampleWidth, 0);
    float4 rimPositionCS = TransformWViewToHClip(rimPositionVS);
#if UNITY_REVERSED_Z
    rimPositionCS.z = min(rimPositionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    rimPositionCS.z = max(rimPositionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    float2 rimUV = (rimPositionCS.xyz / rimPositionCS.w).xy * 0.5 + 0.5; // ndc.xy -> ssUV
#if UNITY_UV_STARTS_AT_TOP
    rimUV.y = 1.0 - rimUV.y;
#endif

    const float2 o = _ScreenSize.zw * 2;
    float maskSample = SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_PotaToonPointClamp, rimUV).r
                       + SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_PotaToonPointClamp, rimUV + float2(o.x, o.y)).r
                       + SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_PotaToonPointClamp, rimUV + float2(o.x, -o.y)).r
                       + SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_PotaToonPointClamp, rimUV + float2(-o.x, o.y)).r
                       + SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_PotaToonPointClamp, rimUV + float2(-o.x, -o.y)).r;

    if (maskSample < HALF_MIN)
    {
#if UNITY_REVERSED_Z
        bool isBehindChar = SampleSceneDepth(rimUV) < sceneDepth + 0.00001;
#else
        bool isBehindChar = SampleSceneDepth(rimUV) > sceneDepth + 0.00001;
#endif
        if (isBehindChar)
        {
            half3 screenRimColor = targetColor * shadowFade;
            color += screenRimColor * litFactor;
        }
    }
}

#endif