#ifndef CHARACTER_SHADOW_DEPTH_INPUT_INCLUDED
#define CHARACTER_SHADOW_DEPTH_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
#include "./CharacterShadowTransforms.hlsl"
#if _USE_2D_FACE_SHADOW
#include "../Common/PotaToonGlobalInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#endif

// We use custom samplers for 2021
#if UNITY_VERSION >= 60000023
#define sampler_PotaToonPointClamp sampler_PointClamp
#define sampler_PotaToonLinearClamp sampler_LinearClamp
#else
SAMPLER(sampler_PotaToonPointClamp);
SAMPLER(sampler_PotaToonLinearClamp);
#endif

#define MAX_CHAR_SHADOWMAPS 1

uint LocalLightIndexToShadowmapIndex(uint lightindex)
{
    if (_UseBrightestLight > 0 && lightindex == _BrightestLightIndex)
        return 0;

    return MAX_CHAR_SHADOWMAPS;
}

#define ADDITIONAL_CHARSHADOW_CHECK(i, lightIndex) { \
        i = LocalLightIndexToShadowmapIndex(lightIndex); \
        if (i >= MAX_CHAR_SHADOWMAPS) \
            return 0; }

float3 TransformWorldToCharShadowCoord(float3 worldPos)
{
    float4 clipPos = CharShadowWorldToHClipWithoutBias(worldPos);
#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#endif
    float3 ndc = clipPos.xyz / clipPos.w;
    float2 ssUV = ndc.xy * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    ssUV.y = 1.0 - ssUV.y;
#endif
    return float3(ssUV, ndc.z);
}

void ScaleUVForCascadeCharShadow(inout float2 uv)
{
    uv *= _CharShadowUVScale;
    uv = (1.0 - _CharShadowUVScale) * 0.5 + uv;
}

half SampleCharShadowTexture(TEXTURE2D_PARAM(tex, samplerTex), float2 uv, half isFace = 0)
{
    float2 value = SAMPLE_TEXTURE2D_LOD(tex, samplerTex, uv, 0).rg;
    return lerp(value.r, value.g, isFace);
}

half SampleScreenSpaceCharacterShadowTexture(float2 ssUV, half isFace = 0)
{
    half2 value = SAMPLE_TEXTURE2D_LOD(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, ssUV, 0).rg;
    return lerp(value.r, value.g, isFace);
}

half2 SpiralBlurScreenSpaceCharacterShadow(float2 uv, float distance, float distanceSteps, float radialSteps, float radialOffset, float kernelPower)
{
    float2 newUV = uv;
    int i = 0;
    float stepSize = distance / (int)distanceSteps;
    float curDistance = 0;
    float2 curOffset = 0;
    float subOffset = 0;
    float accumdist = 0;

    // half2 value = SAMPLE_TEXTURE2D_LOD(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, uv, 0).rg;
    // if (distanceSteps < 1)
    //     return value;

    half2 value = 0;
    while (i < (int)distanceSteps)
    {
        curDistance += stepSize;
        for (int j = 0; j < (int)radialSteps; j++)
        {
            subOffset += 1;
            curOffset.x = cos(TWO_PI * (subOffset / radialSteps));
            curOffset.y = sin(TWO_PI * (subOffset / radialSteps));
            newUV = uv + curOffset * curDistance.xx;
            float distpow = PositivePow(curDistance, kernelPower);
            value += SAMPLE_TEXTURE2D_LOD(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, newUV, 0).rg * distpow;		
            accumdist += distpow;
        }
        subOffset += radialOffset;
        i++;
    }
    value /= accumdist;
    return value;
}

half SampleScreenSpaceCharacterShadowTextureFiltered(float2 ssUV, half isFace = 0)
{
    half2 attenuation = SAMPLE_TEXTURE2D_LOD(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, ssUV, 0).rg;
    half4 gatherR = GATHER_RED_TEXTURE2D(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, ssUV);
    half4 gatherG = GATHER_GREEN_TEXTURE2D(_ScreenSpaceCharShadowmapTexture, sampler_PotaToonPointClamp, ssUV);
    
    float avg = lerp(gatherR.x + gatherR.y + gatherR.z + gatherR.w, gatherG.x + gatherG.y + gatherG.z + gatherG.w, isFace) * 0.25;
    if (avg < 0.001 || avg > 0.999)
        return lerp(attenuation.r, attenuation.g, isFace);
    
    attenuation = SpiralBlurScreenSpaceCharacterShadow(ssUV, max(_ScreenSize.z, _ScreenSize.w) * 2, 2, 2, 0.62, 0.1);
    return lerp(attenuation.r, attenuation.g, isFace);
}

#if _USE_2D_FACE_SHADOW
float PotaToonSampleSceneDepthLOD(float2 uv)
{
#if defined(UNITY_DYNAMIC_SCALING_CLAMPING_INCLUDED)
    uv = ClampAndScaleUVForBilinear(UnityStereoTransformScreenSpaceTex(uv), _CameraDepthTexture_TexelSize.xy);
#else
    uv = UnityStereoTransformScreenSpaceTex(uv);
#endif
    return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, sampler_CameraDepthTexture, uv, 0).r;
}

half Sample2DFaceShadow(float2 ssUV, float3 worldPos)
{
    // Skip if any transparent mesh overlapped
    if (SAMPLE_TEXTURE2D_X_LOD(_PotaToonCharMask, sampler_PotaToonLinearClamp, ssUV, 0).g > 0)
        return 0;
    
    float sceneDepth = PotaToonSampleSceneDepthLOD(ssUV);
#if UNITY_REVERSED_Z
    half depthMultiplier = 1.0 - sceneDepth;
#else
    half depthMultiplier = sceneDepth;
#endif
    half width = _2DFaceShadowWidth * (0.25 + saturate(dot(_FaceForward.xyz, GetWorldSpaceViewDir(worldPos))) * 0.75) * depthMultiplier;

    float4 positionCS = TransformWorldToHClip(worldPos + _BrightestLightDirection.xyz * width * 0.04);
    positionCS.xyz /= positionCS.w;
    
    float2 sampleDepthUV = positionCS.xy * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    sampleDepthUV.y = 1.0 - sampleDepthUV.y;
#endif
    if (any(sampleDepthUV) < 0 || any(sampleDepthUV) > 1)
        return 0;

    half charMask = SAMPLE_TEXTURE2D_X_LOD(_PotaToonCharMask, sampler_PotaToonPointClamp, sampleDepthUV, 0).g;
    // If the character mask samples a transparent area, assume the transparent area is front (because transparent objects are always rendered later).
    if (charMask < 1.0)
        return charMask;

    // If the character mask samples an opaque area, test the depth to check if the mask area is front.
    float sampleDepth = PotaToonSampleSceneDepthLOD(sampleDepthUV);
#if UNITY_REVERSED_Z
    if (sampleDepth - sceneDepth > 0.000001)
#else
    if (sceneDepth - sampleDepth > 0.000001)
#endif
        return charMask;
    return 0;
}
#endif

half SampleCharacterShadowmapFiltered(float2 uv, float z, SamplerState s)
{
    // UV must be the scaled value with ScaleUVForCascadeCharShadow()
    z += 0.00001;
    float ow = _CharShadowmapSize.x * _CharShadowCascadeParams.y;
    float attenuation = SampleCharShadowTexture(TEXTURE2D_ARGS(_CharShadowMap, s), uv)
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_CharShadowMap, s), uv + float2(ow, ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_CharShadowMap, s), uv + float2(ow, -ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_CharShadowMap, s), uv + float2(-ow, ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_CharShadowMap, s), uv + float2(-ow, -ow));
    attenuation *= 0.2f;

    return attenuation - (z + _CharShadowBias.x);
}

half SampleTransparentShadowmapFiltered(float2 uv, float z, SamplerState s)
{
    // UV must be the scaled value with ScaleUVForCascadeCharShadow()
    z += 0.00001;
    float ow = _CharTransparentShadowmapSize.x * _CharShadowCascadeParams.y;
    float attenuation = SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentShadowMap, s), uv)
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentShadowMap, s), uv + float2(ow, ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentShadowMap, s), uv + float2(ow, -ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentShadowMap, s), uv + float2(-ow, ow))
                        + SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentShadowMap, s), uv + float2(-ow, -ow));
    attenuation *= 0.2f;

    return attenuation - (z + _CharShadowBias.x);
}

half TransparentAttenuation(float2 uv, half opacity)
{
    // UV must be the scaled value with ScaleUVForCascadeCharShadow()
    // Saturate since texture could have value more than 1
    return saturate(SampleCharShadowTexture(TEXTURE2D_ARGS(_TransparentAlphaSum, sampler_PotaToonLinearClamp), uv) - opacity);    // Total alpha sum - current pixel's alpha
}

// For transparent shadow, we assume that 'isFace' is always false. (The face should not be transparent by design)
half GetTransparentShadow(float2 uv, float z, half opacity)
{
    // Ignore if transparent shadow is disabled.
    if (_CharTransparentShadowmapSize.x > 0.99)
        return 0;
    
    // UV must be the scaled value with ScaleUVForCascadeCharShadow()
    half hidden = SampleTransparentShadowmapFiltered(uv, z, sampler_PotaToonLinearClamp) > 0 ? 1 : 0; 
    half atten = TransparentAttenuation(uv, opacity);
    return hidden * atten;
}

half CharacterAndTransparentShadowmap(float2 ssUV, float3 worldPos, half opacity, half isFace)
{
    // TODO: Scale uv first for cascade char shadow map
    // ScaleUVForCascadeCharShadow(uv);
    float3 coord = TransformWorldToCharShadowCoord(worldPos);

#if _USE_2D_FACE_SHADOW
    // 2D Face shadow does not receive transparent shadows
    return Sample2DFaceShadow(ssUV, worldPos);
#else
#if _ALPHATEST_ON
    // We assume that 'isFace' is always false. (The face should not be transparent by design)
    half charShadowAtten = SampleCharacterShadowmapFiltered(coord.xy, coord.z, sampler_PotaToonLinearClamp);
    return max(smoothstep(0, 0.0002, charShadowAtten), GetTransparentShadow(coord.xy, coord.z, opacity));
#else
    return max(SampleScreenSpaceCharacterShadowTextureFiltered(ssUV, isFace), GetTransparentShadow(coord.xy, coord.z, opacity));
#endif
#endif
}

half SampleCharacterAndTransparentShadow(float2 ssUV, float3 worldPos, half opacity, half isFace)
{
    if (_IsBrightestLightMain == 0)
        return 0;

    half fallback = isFace ? 0 : SAMPLE_TEXTURE2D_LOD(_CharContactShadowTexture, sampler_PotaToonPointClamp, ssUV, 0).r;
    return lerp(CharacterAndTransparentShadowmap(ssUV, worldPos, opacity, isFace), fallback, GetCharShadowFade(TransformWorldToView(worldPos).z));
}

half SampleAdditionalCharacterAndTransparentShadow(float2 ssUV, float3 worldPos, half opacity, half isFace, uint lightIndex = 0)
{
#ifndef USE_FORWARD_PLUS
    return 0;
#endif
    uint i;
    ADDITIONAL_CHARSHADOW_CHECK(i, lightIndex)

    if (_IsBrightestLightMain == 1)
        return 0;

    half fallback = isFace ? 0 : SAMPLE_TEXTURE2D_LOD(_CharContactShadowTexture, sampler_PotaToonPointClamp, ssUV, 0).r;
    return lerp(CharacterAndTransparentShadowmap(ssUV, worldPos, opacity, isFace), fallback, GetCharShadowFade(TransformWorldToView(worldPos).z));
}

#endif