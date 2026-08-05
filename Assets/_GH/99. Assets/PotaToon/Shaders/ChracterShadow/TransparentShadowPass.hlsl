#ifndef TRANSPARENT_SHADOW_PASS_INCLUDED
#define TRANSPARENT_SHADOW_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "DeclareCharacterShadowTexture.hlsl"
#include "../Common/PotaToonCommon.hlsl"

// Below material properties must be declared in seperate shader input to make compatible with SRP Batcher.
// CBUFFER_START(UnityPerMaterial)
// float4 _BaseColor;
// float4 _MainTex_ST;
// float4 _ClippingMask_ST;
// CBUFFER_END
// TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
// TEXTURE2D(_ClippingMask);

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord0    : TEXCOORD0;
    float2 texcoord1    : TEXCOORD1;
    float2 texcoord2    : TEXCOORD2;
    float2 texcoord3    : TEXCOORD3;
};

struct ShadowVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    float2 uv3          : TEXCOORD3;
    float3 positionOS   : TEXCOORD4;
};

struct AlphaSumVaryings
{
    float4 positionCS   : SV_POSITION;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    float2 uv3          : TEXCOORD3;
    float3 positionWS   : TEXCOORD4;
    float3 positionOS   : TEXCOORD5;
};

ShadowVaryings TransparentShadowVert(Attributes input)
{
    ShadowVaryings output = (ShadowVaryings)0;
    output.uv0 = input.texcoord0;
    output.uv1 = input.texcoord1;
    output.uv2 = input.texcoord2;
    output.uv3 = input.texcoord3;
    output.positionCS = CharShadowObjectToHClipWithoutBias(input.position.xyz);
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS.xy *= _CharShadowCascadeParams.y;

    output.positionOS = input.position.xyz;

    return output;
}

AlphaSumVaryings TransparentAlphaSumVert(Attributes input)
{
    AlphaSumVaryings output = (AlphaSumVaryings)0;
    output.uv0 = input.texcoord0;
    output.uv1 = input.texcoord1;
    output.uv2 = input.texcoord2;
    output.uv3 = input.texcoord3;
    output.positionCS = CharShadowObjectToHClipWithoutBias(input.position.xyz);
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS.xy *= _CharShadowCascadeParams.y;
    output.positionWS = TransformObjectToWorld(input.position.xyz);

    output.positionOS = input.position.xyz;

    return output;
}

float TransparentShadowFragment(ShadowVaryings input) : SV_Target
{
    // Use A Channel for alpha sum
    const float2 uvArray[4] = { input.uv0, input.uv1, input.uv2, input.uv3 };
    float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(SelectUV(_BaseMapUV, uvArray), _MainTex)).a * _BaseColor.a;
    alpha = PotaToonApplyAlphaMask(alpha, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE);
    clip(alpha - cutoff - 0.001);

    return input.positionCS.z;   // Depth
}


float TransparentAlphaSumFragment(AlphaSumVaryings input) : SV_Target
{
    // Use A Channel for alpha sum
    const float2 uvArray[4] = { input.uv0, input.uv1, input.uv2, input.uv3 };
    float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(SelectUV(_BaseMapUV, uvArray), _MainTex)).a * _BaseColor.a;
    alpha = PotaToonApplyAlphaMask(alpha, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE);
    clip(alpha - cutoff - 0.001);

    float4 clipPos = CharShadowWorldToHClipWithoutBias(input.positionWS);
    clipPos.xy *= _CharShadowCascadeParams.y;
    clipPos.z = 1.0;
    float3 ndc = clipPos.xyz / clipPos.w;
    float2 ssUV = ndc.xy * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    ssUV.y = 1.0 - ssUV.y;
#endif

    // Discard behind fragment
    // We assume that 'isFace' is always false. (The face should not be transparent by design)
    return SampleCharacterShadowmapFiltered(ssUV, ndc.z, sampler_PotaToonLinearClamp) > 0 ? 1 : alpha;
}
#endif