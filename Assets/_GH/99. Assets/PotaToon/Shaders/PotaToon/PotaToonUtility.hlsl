#ifndef POTA_TOON_UTILITY_INCLUDED
#define POTA_TOON_UTILITY_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "../../Shaders/ChracterShadow/CharacterShadowInput.hlsl"
#include "../../Shaders/ChracterShadow/DeclareCharacterShadowTexture.hlsl"
#include "../Common/PotaToonCommon.hlsl"
#include "./PotaToonGlitter.hlsl"

// Reference: UE5 SpiralBlur-Texture
half SpiralBlur(TEXTURE2D_PARAM(tex, samplerTex), float2 UV, uint maskCH, float Distance, float DistanceSteps, float RadialSteps, float RadialOffset, float KernelPower)
{
    half CurColor = 0;
    float2 NewUV = UV;
    int i = 0;
    float StepSize = Distance / (int)DistanceSteps;
    float CurDistance = 0;
    float2 CurOffset = 0;
    float SubOffset = 0;
    float accumdist = 0;

    while (i < (int)DistanceSteps)
    {
        CurDistance += StepSize;
        for (int j = 0; j < (int)RadialSteps; j++)
        {
            SubOffset +=1;
            CurOffset.x = cos(TWO_PI * (SubOffset / RadialSteps));
            CurOffset.y = sin(TWO_PI * (SubOffset / RadialSteps));
            NewUV.x = UV.x + CurOffset.x * CurDistance;
            NewUV.y = UV.y + CurOffset.y * CurDistance;
            float distpow = pow(CurDistance, KernelPower);
            CurColor += SelectMask(SAMPLE_TEXTURE2D(tex, samplerTex, NewUV), maskCH) * distpow;		
            accumdist += distpow;
        }
        SubOffset += RadialOffset;
        i++;
    }
    CurColor /= accumdist;
    return DistanceSteps < 1 ? SelectMask(SAMPLE_TEXTURE2D(tex, samplerTex, UV), maskCH) : CurColor;
}

float GetFaceSDFAtten(float2 uv)
{
    const float3 lightDir = _BrightestLightDirection.xyz;
    // Construct TBN based on face forward & up
    // Transform lightDir to TBN space
    const float3 N = _FaceUp.xyz;
    const float3 T = _FaceForward.xyz;
    const float3 B = cross(T, N);
    const float3x3 TBN = float3x3(T, B, N);
    const float3 lightT = mul(TBN, lightDir);

    float3 forwardT = mul(TBN, _FaceForward.xyz);
    float2 l = normalize(lightT.xy);
    float2 n = normalize(forwardT.xy);
    half NoL = dot(l, n);

    bool isBack = false;
    if (NoL < 0)
    {
        isBack = true;
    }

    bool flipped = 1.0 - l.y > COS_45;
    uv.x = lerp(uv.x, 1 - uv.x, flipped);   // Assume the sdf texture is symmetry.
    
    // Reverse if need
    uv.x = lerp(uv.x, 1.0 - uv.x, _SDFReverse);
    
    // Sample
    float atten = SpiralBlur(TEXTURE2D_ARGS(_FaceSDFTex, sampler_FaceSDFTex), uv, _FaceSDFTexCH, _SDFBlur * 0.01 + 0.01, _CharShadowSampleQuality * 4, 8, 0.62, 1) + _SDFOffset;

    NoL = 1.0 - NoL;
    return isBack ? -1 : atten - NoL;
}


float GetCharMainShadow(float2 ssUV, float3 worldPos, half opacity, float sdfAtten = 1, half sdfMask = 0)
{
    float faceSDF = 0;
#if _USE_FACE_SDF
    faceSDF = 1.0 - sdfAtten;
    // if (sdfMask > 0.01) // Ignore if masked
    // {
    //     return faceSDF;
    // }
#endif
    const float isFace = _ToonType == FACE_TYPE ? 1.0 : 0.0;
    return max(faceSDF, SampleCharacterAndTransparentShadow(ssUV, worldPos, opacity, isFace));
}

float GetCharAdditionalShadow(float2 ssUV, float3 worldPos, half opacity, uint lightIndex, float sdfAtten = 1, half sdfMask = 0)
{
    float faceSDF = 0;
#if _USE_FACE_SDF
    uint i;
    ADDITIONAL_CHARSHADOW_CHECK(i, lightIndex);
    faceSDF = 1.0 - sdfAtten;
    // if (sdfMask > 0.01) // Ignore if masked
    // {
    //     return faceSDF;
    // }
#endif
    const float isFace = _ToonType == FACE_TYPE ? 1.0 : 0.0;
    return max(faceSDF, SampleAdditionalCharacterAndTransparentShadow(ssUV, worldPos, opacity, isFace, lightIndex));
}


half3 GetMidTone(float atten, float step, float smoothness)
{
    half3 midTone = half3(0, 0, 0);
    if (_UseMidTone > 0)
    {
        if (abs(atten - step) < smoothness)
            midTone = _MidColor.rgb * (1.0 - abs(atten - step) * rcp(max(0.00001, smoothness)));
    }
    return midTone;
}


half3 AnisotropicHairHighlight(float3 viewDirection, float2 uv, float3 worldPos, float totalAtten)
{
    float dotViewUp = saturate(dot(viewDirection, _FaceUp.xyz));
    float sinVU = sqrt(1 - dotViewUp * dotViewUp);
    float2 hairUV = float2(uv.x, uv.y + sinVU * _HairHiUVOffset);
    half3 hairHiTex = SAMPLE_TEXTURE2D_LOD(_HairHighLightTex, sampler_MainTex, TRANSFORM_TEX(hairUV, _HairHighLightTex), 0).rgb;
    if (_ReverseHairHighLightTex > 0)
        hairHiTex = 1.0 - hairHiTex;
    hairHiTex *= _HairHiStrength * (totalAtten * 0.75 + 0.25);
    float3 hairDir = normalize(worldPos - _HeadWorldPos.xyz);
    float dotVH = dot(viewDirection, hairDir) * 0.5 + 0.5;
    return PositivePow(lerp(0, hairHiTex, dotVH), 2.2);
}

void ApplyRefraction(float3 viewDirection, float3 forward, float2 screenSpaceUV, half opacity, inout half3 color)
{
    float3 vWorld = TransformObjectToWorldDir(float3(0, 1, 0));
    float3 uWorld = cross(vWorld, forward);
    float2 offset = float2(dot(uWorld, viewDirection), dot(vWorld, viewDirection));
#if UNITY_UV_STARTS_AT_TOP
    offset.y = -offset.y;
#endif
    const float2 refractedUV = screenSpaceUV - offset * (_RefractionWeight * 0.01);

    const float2 o = _ScreenSize.zw * _RefractionBlurStep;

    // Gaussian Blur
    half3 sceneColor = SampleSceneColor(refractedUV) * 0.148;
    sceneColor += SampleSceneColor(refractedUV + float2(o.x, 0)) * 0.118;
    sceneColor += SampleSceneColor(refractedUV - float2(o.x, 0)) * 0.118;
    sceneColor += SampleSceneColor(refractedUV + float2(0, o.y)) * 0.118;
    sceneColor += SampleSceneColor(refractedUV - float2(0, o.y)) * 0.118;
    sceneColor += SampleSceneColor(refractedUV + float2(o.x, o.y)) * 0.095;
    sceneColor += SampleSceneColor(refractedUV - float2(o.x, o.y)) * 0.095;
    sceneColor += SampleSceneColor(refractedUV + float2(-o.x, o.y)) * 0.095;
    sceneColor += SampleSceneColor(refractedUV + float2(o.x, -o.y)) * 0.095;
    
    color = color * opacity + sceneColor * (1 - opacity);
}

#endif // UNIVERSAL_TOON_CUSTOM_UTILITY_INCLUDED