#ifndef CHARACTER_SHADOW_DEPTH_PASS_INCLUDED
#define CHARACTER_SHADOW_DEPTH_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "CharacterShadowTransforms.hlsl"
#include "../Common/PotaToonCommon.hlsl"

// Below material properties must be declared in seperate shader input to make compatible with SRP Batcher.
// CBUFFER_START(UnityPerMaterial)
//     float4 _ClippingMask_ST;
// CBUFFER_END
// TEXTURE2D(_ClippingMask);

SAMPLER(sampler_ClippingMask);

struct Attributes
{
    float4 position     : POSITION;
    float2 texcoord0    : TEXCOORD0;
    float2 texcoord1    : TEXCOORD1;
    float2 texcoord2    : TEXCOORD2;
    float2 texcoord3    : TEXCOORD3;
    float3 normal       : NORMAL;
    // UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    float2 uv3          : TEXCOORD3;
    float3 positionOS   : TEXCOORD4;
    float4 positionCS   : SV_POSITION;
    // UNITY_VERTEX_INPUT_INSTANCE_ID
    // UNITY_VERTEX_OUTPUT_STEREO
};

Varyings CharShadowVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    // UNITY_SETUP_INSTANCE_ID(input);
    // UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.uv0 = input.texcoord0;
    output.uv1 = input.texcoord1;
    output.uv2 = input.texcoord2;
    output.uv3 = input.texcoord3;

    output.positionCS = CharShadowObjectToHClip(input.position.xyz, TransformObjectToWorldDir(input.normal), _DepthBias * 0.01, _NormalBias * 0.01);

#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
    output.positionCS.xy *= _CharShadowCascadeParams.y;

    output.positionOS = input.position.xyz;

    return output;
}

float2 CharShadowFragment(Varyings input) : SV_TARGET
{
    // UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    if (IfCharShadowCulled(TransformWorldToView(TransformObjectToWorld(input.positionOS)).z))
        clip(-1);

    const float2 uvArray[4] = { input.uv0, input.uv1, input.uv2, input.uv3 };
    float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_ClippingMask, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
#if defined(_ALPHATEST_ON)
    float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_ClippingMask, TRANSFORM_TEX(SelectUV(_BaseMapUV, uvArray), _MainTex)).a * _BaseColor.a;
    alpha = PotaToonApplyAlphaMask(alpha, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE);
    clip(alpha - cutoff - 0.001);
#endif

    float2 output = input.positionCS.z;
    if (_ToonType == FACE_TYPE)
        output.y = 0;
    
    return output;
}
#endif