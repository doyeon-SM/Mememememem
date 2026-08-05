#ifndef CHARACTER_SHADOW_TRANSFORMS_INCLUDED
#define CHARACTER_SHADOW_TRANSFORMS_INCLUDED

#include "./CharacterShadowInput.hlsl"

float3 ApplyCharShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection, float depthBias, float normalBias)
{
    // Depth Bias
    positionWS = lightDirection * depthBias + positionWS;

    // Normal Bias
    float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    float scale = invNdotL * -normalBias;
    positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

float4 CharShadowWorldToHClipWithoutBias(float3 positionWS)
{
    return mul(_CharShadowViewProjM, float4(positionWS, 1.0));
}

float4 CharShadowObjectToHClip(float3 positionOS, float3 normalWS, float depthBias, float normalBias)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    positionWS = ApplyCharShadowBias(positionWS, normalWS, _BrightestLightDirection.xyz, _CharShadowBias.x + depthBias, _CharShadowBias.y + normalBias);

    return CharShadowWorldToHClipWithoutBias(positionWS);
}

float4 CharShadowObjectToHClipWithoutBias(float3 positionOS)
{
    float3 positionWS = TransformObjectToWorld(positionOS);
    return CharShadowWorldToHClipWithoutBias(positionWS);
}

// Skip if too far (since we don't use mipmap for charshadowmap, manually cull this calculation based on view depth.)
bool IfCharShadowCulled(float viewPosZ)
{
    return viewPosZ < _CharShadowCullingDist;
}

float GetCharShadowFade(float viewPosZ)
{
    const float fadeStart = _CharShadowCullingDist * 0.9;
    const float div = _CharShadowCullingDist * 0.1;
    return saturate((viewPosZ - fadeStart) / div);
}

#endif