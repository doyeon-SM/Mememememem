#ifndef SIMPLE_TOON_UTILITY_INCLUDED
#define SIMPLE_TOON_UTILITY_INCLUDED

#include "../../ChracterShadow/DeclareCharacterShadowTexture.hlsl"
#include "../../Common/PotaToonCommon.hlsl"

float GetCharMainShadow(float2 ssUV, float3 worldPos, half opacity)
{
    const float isFace = _ToonType == FACE_TYPE ? 1.0 : 0.0;
    return SampleCharacterAndTransparentShadow(ssUV, worldPos, opacity, isFace);
}

float GetCharAdditionalShadow(float2 ssUV, float3 worldPos, half opacity, uint lightIndex)
{
    const float isFace = _ToonType == FACE_TYPE ? 1.0 : 0.0;
    return SampleAdditionalCharacterAndTransparentShadow(ssUV, worldPos, opacity, isFace, lightIndex);
}

half3 GetMidTone(float atten, float step, float smoothness)
{
    half3 midTone = 0;
    if (_UseMidTone > 0)
    {
        if (abs(atten - step) < smoothness)
            midTone = _MidColor.rgb * (1.0 - abs(atten - step) * rcp(max(0.00001, smoothness)));
    }
    return midTone;
}

#endif