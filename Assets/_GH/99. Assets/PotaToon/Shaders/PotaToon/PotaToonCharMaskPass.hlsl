#ifndef POTA_TOON_CHAR_MASK_PASS_INCLUDED
#define POTA_TOON_CHAR_MASK_PASS_INCLUDED

#include "../Common/PotaToonCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

#if UNITY_VERSION < 600000
SAMPLER(sampler_LinearClamp);
#endif

struct Attributes
{
   float4 position      : POSITION;
   float4 texcoord0     : TEXCOORD0;
   float4 texcoord1     : TEXCOORD1;
   float4 texcoord2     : TEXCOORD2;
   float4 texcoord3     : TEXCOORD3;
};
struct Varyings
{
    float4 positionCS   : SV_POSITION;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    float2 uv3          : TEXCOORD3;
};

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    output.uv0 = input.texcoord0.xy;
    output.uv1 = input.texcoord1.xy;
    output.uv2 = input.texcoord2.xy;
    output.uv3 = input.texcoord3.xy;
    return output;
}

half2 frag(Varyings input) : SV_TARGET
{
    const float2 ssUV = GetNormalizedScreenSpaceUV(input.positionCS.xy);
    const float sceneDepth = SampleSceneDepth(ssUV);
    const float linearDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
    const float inputLinearDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
    if (inputLinearDepth >= linearDepth + 0.01)
        clip(-1);

    const float2 uvArray[4] = { input.uv0, input.uv1, input.uv2, input.uv3 };
    const float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_LinearClamp, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    
    half alpha = 1;
#if _ALPHATEST_ON
    alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_LinearClamp, TRANSFORM_TEX(SelectUV(_BaseMapUV, uvArray), _MainTex)).a * _BaseColor.a;
    alpha = PotaToonApplyAlphaMask(alpha, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    float cutoff = PotaToonIsAlphaMaskMode(_AlphaMaskMode) ? PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE) : 0;
    clip(alpha - cutoff - 0.001);
    
    if (_SurfaceType < OIT_SURFACE)
        alpha = 1;
#endif
    
    if (_UseDitherFade > 0)
        DistanceFade(alpha, abs(TransformWorldToView(_HeadWorldPos.xyz).z), _DitherFadeMinZ, _DitherFadeMaxZ);

    // Force the alpha value is less than 1.0 to check if the area is transparent surface
    if (_SurfaceType >= OIT_SURFACE)
        alpha *= 0.99;
    
    // Save alpha to g channel for 2d face shadow attenuation
    return half2(1, _ToonType == FACE_TYPE ? 0 : alpha);
}

#endif