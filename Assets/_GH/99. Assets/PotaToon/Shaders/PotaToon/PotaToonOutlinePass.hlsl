#ifndef POTA_TOON_OUTLINE_PASS_INCLUDED
#define POTA_TOON_OUTLINE_PASS_INCLUDED

#include "../Common/PotaToonCommon.hlsl"
#include "../Common/PotaToonColorGrading.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonLighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

#if _POTA_TOON_OIT
#include "../OIT/OITOutlineUtils.hlsl"
#endif

struct VertexInput
{
    float4 vertex       : POSITION;
    float3 normal       : NORMAL;
    float4 tangent      : TANGENT;
    float2 texcoord0    : TEXCOORD0;
    float2 texcoord1    : TEXCOORD1;
    float2 texcoord2    : TEXCOORD2;
    float2 texcoord3    : TEXCOORD3;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct VertexOutput
{
    float4 pos          : SV_POSITION;
    float2 uv0          : TEXCOORD0;
    float2 uv1          : TEXCOORD1;
    float2 uv2          : TEXCOORD2;
    float2 uv3          : TEXCOORD3;
    float3 positionWS   : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutput vert (VertexInput v)
{
    VertexOutput o = (VertexOutput)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.uv0 = v.texcoord0;
    o.uv1 = v.texcoord1;
    o.uv2 = v.texcoord2;
    o.uv3 = v.texcoord3;

    const float2 uv = SelectUVVertex(_OutlineMaskUV, o.uv0, o.uv1, o.uv2, o.uv3);
    float3 normal = v.normal;
    if (_UseOutlineNormalMap > 0)
    {
        float3 normalWS = TransformObjectToWorldDir(v.normal);
        float3 tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
        float3 bitangentWS = normalize(cross(normalWS, tangentWS) * v.tangent.w);
        float3x3 tangentTransform = float3x3(tangentWS, bitangentWS, normalWS);
        float4 normalMap = SAMPLE_TEXTURE2D_LOD(_OutlineNormalMap, sampler_OutlineWidthMask, uv, 0) * 2 - 1;
        normal = normalize(mul(normalMap.rgb, tangentTransform));
    }

    float outlineMask = SelectMask(SAMPLE_TEXTURE2D_LOD(_OutlineWidthMask, sampler_OutlineWidthMask, TRANSFORM_TEX(uv, _OutlineWidthMask), 0), _OutlineMaskCH);
    float outlineWidth = _OutlineWidth * 0.001 * outlineMask;

    float3 positionDir = SafeNormalize(v.vertex.xyz);
    float positionOutlineWidth = outlineWidth * 2;
    float signVar = dot(positionDir, normalize(v.normal)) < 0 ? -1 : 1;
    
    float3 outlinePos = v.vertex.xyz + lerp(normal * outlineWidth, (signVar * positionOutlineWidth) * positionDir, _OutlineMode);
    o.pos = TransformObjectToHClip(outlinePos);
    o.positionWS = TransformObjectToWorld(o.pos.xyz);

    #if defined(UNITY_REVERSED_Z)
    const float outlineOffsetZ = _OutlineOffsetZ * -0.01;
    #else
    const float outlineOffsetZ = _OutlineOffsetZ * 0.01;
    #endif
    o.pos.z += outlineOffsetZ * TransformWorldToHClip(_WorldSpaceCameraPos).z;
    
    return o;
}

#if _POTA_TOON_OIT
[earlydepthstencil]
#endif
half4 frag(VertexOutput i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#if !_POTA_TOON_OIT
    // Skip outline for transparent objects if OIT not enabled.
    if (_SurfaceType >= OIT_SURFACE)
    {
        clip(-1);
    }
#else
    if (_SurfaceType >= OIT_SURFACE && _DisableOIT > 0)
    {
        clip(-1);
    }

    float4 clipPos = TransformWorldToHClip(i.positionWS);
    float3 ndc = clipPos.xyz / clipPos.w;
    float2 ssUV = ndc.xy * 0.5 + 0.5;
#if UNITY_UV_STARTS_AT_TOP
    ssUV.y = 1.0 - ssUV.y;
#endif

    if (_SurfaceType >= OIT_SURFACE && SampleOITDepth(ssUV, ndc.z))
    {
        return 0;
    }
#endif

    const float2 uvArray[4] = { i.uv0, i.uv1, i.uv2, i.uv3 };
    const float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    
    half3 finalColor = _OutlineColor.rgb;
    if (_BlendOutlineMainTex > 0)
    {
        half4 baseMap = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, TRANSFORM_TEX(SelectUV(_BaseMapUV, uvArray), _MainTex), 0);
        const float2 colorGradingMaskUV = TRANSFORM_TEX(SelectUV(_ColorGradingMaskUV, uvArray), _ColorGradingMask);
        const half colorGradingMask = saturate(abs(_ColorGradingMaskReversed - SAMPLE_TEXTURE2D_LOD(_ColorGradingMask, sampler_MainTex, colorGradingMaskUV, 0).r));
        baseMap.rgb = PotaToonApplyTextureHSV(baseMap.rgb, _BaseMapHue, _BaseMapSaturation, _BaseMapContrast, colorGradingMask);
        finalColor *= baseMap.rgb;
    }

    // Keep outline based on Base Color alpha, then let Alpha Mask affect that alpha.
    half outlineAlpha = PotaToonApplyAlphaMask(_BaseColor.a, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    half alpha = OutputAlpha(outlineAlpha, _SurfaceType >= OIT_SURFACE);
#if _ALPHATEST_ON
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE);
    half alphaForClip = PotaToonIsAlphaMaskMode(_AlphaMaskMode) ? outlineAlpha : alpha;
    clip(alphaForClip - cutoff - 0.001);
#endif
    
#if _USE_DITHER_FADE
    if (_UseDitherFade > 0)
        DistanceFade(alpha, abs(TransformWorldToView(_HeadWorldPos.xyz).z), _DitherFadeMinZ, _DitherFadeMaxZ);
#endif

    // Lighting Attenuation
    InputData inputData;
    inputData.positionWS = i.positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
#endif
    Light light = GetMainLight();
    half3 lightColor = light.color.rgb;
    half lightIntensity = 0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b;
    
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
#if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        light = GetAdditionalLight(lightIndex, i.positionWS);

        #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
        #endif
        {
            lightColor = light.color * light.distanceAttenuation;
            lightIntensity += 0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b;
        }
    }
#endif

    // Local Lights
    LIGHT_LOOP_BEGIN(pixelLightCount)
        light = GetAdditionalLight(lightIndex, i.positionWS);

    #ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
    {
        lightColor = light.color * light.distanceAttenuation;
        lightIntensity += 0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b;
    }
    LIGHT_LOOP_END
#endif
    finalColor = lerp(finalColor, finalColor * (_OutlineLightingDimmer * saturate(lightIntensity)), _OutlineLightingDimmer);
    
    return half4(finalColor, alpha);
}

#endif