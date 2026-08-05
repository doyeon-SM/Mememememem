#ifndef POTA_TOON_GEM_PASS_INCLUDED
#define POTA_TOON_GEM_PASS_INCLUDED

#include "../../Common/PotaToonCommon.hlsl"
#include "../../Common/PotaToonColorGrading.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
#include "../../ChracterShadow/CharacterShadowInput.hlsl"
#include "../../ChracterShadow/DeclareCharacterShadowTexture.hlsl"
#if _POTA_TOON_OIT
#include "../../OIT/LinkedListCreation.hlsl"
#endif

struct VertexInput
{
    float4 vertex               : POSITION;
    float3 normal               : NORMAL;
    float4 tangent              : TANGENT;
    float2 texcoord0            : TEXCOORD0;
    float2 texcoord1            : TEXCOORD1;
    float2 texcoord2            : TEXCOORD2;
    float2 texcoord3            : TEXCOORD3;
    float2 staticLightmapUV     : TEXCOORD4;
    float2 dynamicLightmapUV    : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 positionCS           : SV_POSITION;
    float2 uv0                  : TEXCOORD0;
    float2 uv1                  : TEXCOORD1;
    float2 uv2                  : TEXCOORD2;
    float2 uv3                  : TEXCOORD3;
    float3 positionWS           : TEXCOORD4;
    float3 normalWS             : TEXCOORD5;
    float3 tangentWS            : TEXCOORD6;
    float3 bitangentWS          : TEXCOORD7;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
    half fogFactor              : TEXCOORD9;
    
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord          : TEXCOORD10;
#endif

#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV    : TEXCOORD11;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion       : TEXCOORD12;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutput vert(VertexInput v)
{
    VertexOutput o = (VertexOutput)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
    o.positionCS = vertexInput.positionCS;
    o.positionWS = vertexInput.positionWS;
    o.uv0 = v.texcoord0.xy;
    o.uv1 = v.texcoord1.xy;
    o.uv2 = v.texcoord2.xy;
    o.uv3 = v.texcoord3.xy;

    o.normalWS = TransformObjectToWorldDir(v.normal);
    o.tangentWS = TransformObjectToWorldDir(v.tangent.xyz);
    o.bitangentWS = normalize(cross(o.normalWS, o.tangentWS) * v.tangent.w);

    OUTPUT_LIGHTMAP_UV(v.staticLightmapUV, unity_LightmapST, o.staticLightmapUV);
#ifdef DYNAMICLIGHTMAP_ON
    o.dynamicLightmapUV = v.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif
    OUTPUT_SH(o.normalWS, o.vertexSH);
    o.fogFactor = ComputeFogFactor(o.positionCS.z);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    o.shadowCoord = GetShadowCoord(vertexInput);
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    o.probeOcclusion = 1;
#endif

    return o;
}

InputData InitializeInputData(VertexOutput input)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
    inputData.positionCS = input.positionCS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
    inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

#if defined(DYNAMICLIGHTMAP_ON)
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.shadowMask = 1;
#else
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif

    return inputData;
}

#include "./PotaToonGemLighting.hlsl"

#if _POTA_TOON_OIT
[earlydepthstencil]
#endif
half4 frag(VertexOutput i, half facing : VFACE, uint uSampleIdx : SV_SampleIndex) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
    const float2 uvArray[4] = { i.uv0, i.uv1, i.uv2, i.uv3 };
    const float2 baseUV = SelectUV(_BaseMapUV, uvArray);
    const float2 normalUV = SelectUV(_NormalMapUV, uvArray);
    const float2 clippingUV = SelectUV(_ClippingMaskUV, uvArray);
    const float2 rimUV = SelectUV(_RimMaskUV, uvArray);
    const float2 glitterUV = SelectUV(_GlitterMapUV, uvArray);
    const float2 matCapUV1 = SelectUV(_MatCapUV1, uvArray);
    const float2 matCapUV2 = SelectUV(_MatCapUV2, uvArray);
    const float2 matCapUV3 = SelectUV(_MatCapUV3, uvArray);
    const float2 matCapUV4 = SelectUV(_MatCapUV4, uvArray);
    float2 baseMapUV = TRANSFORM_TEX(baseUV, _MainTex);
    float3 normalWS = normalize(i.normalWS);
    float3 tangentWS = normalize(i.tangentWS);
    float3 bitangentWS = normalize(i.bitangentWS);

    if (facing < 0)
        normalWS = -normalWS;
    float3 baseNormalWS = normalWS;

    if (HasNormalMap())
    {
        const float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_MainTex, TRANSFORM_TEX(normalUV, _NormalMap)), _BumpScale);
        float3x3 tangentTransform = float3x3(tangentWS, bitangentWS, normalWS);
        normalWS = normalize(mul(normalTS, tangentTransform));
    }

    float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
    if (facing < 0)
    {
        float bend = (1.0 - saturate(_Roughness)) * 0.15;
        normalWS = normalize(normalWS - viewDir * bend);
    }

    // Matcap-like view-space normal term to react to camera rotation.
    float3 normalVS = TransformWorldToViewDir(normalWS);
    half viewNormalLobe = EvaluateGemViewNormalLobe(normalVS);

    InputData inputData = InitializeInputData(i);
    float2 ssUV = inputData.normalizedScreenSpaceUV;
    half4 shadowMask = CalculateShadowMask(inputData);
    Light mainLight = GetMainLight(inputData.shadowCoord, i.positionWS, shadowMask);

#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
#endif

    half4 baseMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, baseMapUV);
    half3 albedo = baseMap.rgb * _BaseColor.rgb;
    albedo = PotaToonApplyTextureHSV(albedo, _BaseMapHue, _BaseMapSaturation, _BaseMapContrast, 1.0);
    half baseAlpha = baseMap.a * _BaseColor.a;
    float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(clippingUV, _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    baseAlpha = PotaToonApplyAlphaMask(baseAlpha, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    half alpha = baseAlpha;

    // Gem surface
    float ior = GetGemIOR();
    float f0 = (ior - 1.0) / (ior + 1.0);
    f0 *= f0;
    float3 F0 = f0.xxx;
    float roughnessSat = saturate(_Roughness);
    float clearcoatRoughness = saturate(_ClearcoatRoughness);
    
    half NdotV = saturate(dot(normalWS, viewDir));
    half edge = 1.0 - NdotV;
    half edge2 = edge * edge;
    half caBlend = 0;
    half dispersion = 0;
    GetGemChromaticFactors(NdotV, caBlend, dispersion);

    // Transmission / Refraction
    half refractShare = EvaluateGemRefractShare(F0, NdotV, _TransmissionStrength);
    half pathLength = EvaluateGemPathLength(NdotV);
    half3 transmittance = EvaluateGemTransmittance(pathLength);
    half3 refractColor = 0;
    if (refractShare > 0.0001)
    {
        refractColor = GemRefractionColor(normalVS, inputData.normalizedScreenSpaceUV, NdotV, caBlend, dispersion);
        refractColor *= saturate(facing + 1.1); // attenuate if backface
    }
    half transLuma = saturate(dot(transmittance, half3(0.2126, 0.7152, 0.0722)));
    half transmissionCut = saturate(refractShare * transLuma) * 0.9;
    alpha = saturate(baseAlpha * (1.0 - transmissionCut));
    
    const bool isBrightestMain = _UseBrightestLight == 0 || _IsBrightestLightMain > 0;
    float charShadowSmoothness = _CharShadowSmoothnessOffset * 0.3;
    float charShadowAtten = 0;
    if (_DisableCharShadow == 0 && isBrightestMain)
    {
        float rawShadow = SampleCharacterAndTransparentShadow(ssUV, i.positionWS, alpha, 0);
        charShadowAtten = LinearStep(0.3 - charShadowSmoothness, 0.7 + charShadowSmoothness, rawShadow);
    }
    float charShadowFactor = 1.0 - charShadowAtten;
    float totalAttenuation = 1.0;
    if (_DisableCharShadow == 0 && isBrightestMain)
        totalAttenuation = min(totalAttenuation, charShadowFactor);

    ///////////////////////////////////////////////////////////////////
    /// 1. Direct Lighting                                          ///
    ///////////////////////////////////////////////////////////////////

    bool hasBrightestDir = dot(_BrightestLightDirection.xyz, _BrightestLightDirection.xyz) > 1e-6;
    float3 lightDir = normalize((isBrightestMain && hasBrightestDir) ? _BrightestLightDirection.xyz : mainLight.direction);
    half mainLightShadowAtten = (_ReceiveLightShadow > 0 && isBrightestMain) ? mainLight.shadowAttenuation : 1.0;
    if (_ReceiveLightShadow > 0 && isBrightestMain)
        totalAttenuation = min(totalAttenuation, mainLightShadowAtten);

    half3 lightColor = 0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        lightColor = mainLight.color * mainLight.distanceAttenuation * mainLightShadowAtten * charShadowFactor;
    }

    half NdotL = saturate(dot(normalWS, lightDir));
    half3 directDiffuse = albedo * NdotL * lightColor;

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    GemAdditionalLightParams additionalLightParams;
    additionalLightParams.ssUV = ssUV;
    additionalLightParams.positionWS = i.positionWS;
    additionalLightParams.alpha = alpha;
    additionalLightParams.charShadowSmoothness = charShadowSmoothness;
    additionalLightParams.albedo = albedo;
    additionalLightParams.normalWS = normalWS;
    additionalLightParams.viewDir = viewDir;

    GemAdditionalLightAccum additionalLightAccum = (GemAdditionalLightAccum)0;

#if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            const bool isBrightestAdditional = _UseBrightestLight > 0 && _IsBrightestLightMain == 0 && _BrightestLightIndex == lightIndex;
            AccumulateAdditionalGemLight(lightIndex, light, isBrightestAdditional, additionalLightParams, additionalLightAccum, totalAttenuation);
        }
    }
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            const bool isBrightestAdditional = _UseBrightestLight > 0 && _IsBrightestLightMain == 0 && _BrightestLightIndex == lightIndex;
            AccumulateAdditionalGemLight(lightIndex, light, isBrightestAdditional, additionalLightParams, additionalLightAccum, totalAttenuation);
        }
    LIGHT_LOOP_END

    directDiffuse += additionalLightAccum.diffuse;
#endif

    ///////////////////////////////////////////////////////////////////
    /// 2. Indirect Lighting                                        ///
    ///////////////////////////////////////////////////////////////////
    
#if defined(DYNAMICLIGHTMAP_ON)
    half3 bakedGI = SAMPLE_GI(i.staticLightmapUV, i.dynamicLightmapUV, i.vertexSH, normalWS);
#elif UNITY_VERSION >= 60000023 && !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    half3 bakedGI = SAMPLE_GI(i.vertexSH, GetAbsolutePositionWS(i.positionWS), normalWS, viewDir, i.positionCS.xy, i.probeOcclusion, inputData.shadowMask);
#else
    half3 bakedGI = SAMPLE_GI(i.staticLightmapUV, i.vertexSH, normalWS);
#endif
    MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI);
    half3 indirectDiffuse = max(0, bakedGI * albedo * totalAttenuation * _IndirectDimmer);
    half3 diffuse = directDiffuse + indirectDiffuse;
    
    half3 indirectSpecular = EvaluateGemEnvSpec(normalWS, viewDir, i.positionWS, inputData.normalizedScreenSpaceUV, facing, NdotV, f0, roughnessSat, _IndirectDimmer, caBlend, dispersion);
    
    half clearFactor = isBrightestMain ? EvaluateClearcoatFactor(clearcoatRoughness, normalWS, viewDir, lightDir, _ClearcoatIntensity) : 0;
    half3 clearcoat = clearFactor * lightColor;

#if defined(_ADDITIONAL_LIGHTS)
    clearcoat += additionalLightAccum.clearcoat;
#endif

    ApplyGemRotationBoost(clearcoat, indirectSpecular, viewNormalLobe);
    half3 specular = clearcoat + indirectSpecular;
    half3 directLighting = directDiffuse + clearcoat;

    half3 bodyColor = diffuse * _BaseStrength + specular;
    half3 absorptionTint = lerp(1.0, transmittance, _TransmissionStrength);
    bodyColor *= absorptionTint;
    
    half3 refractedTint = refractColor * transmittance * albedo;
    half refractBlend = saturate(1.0 - alpha / max(baseAlpha, 0.0001));
    half3 finalColor = lerp(bodyColor, refractedTint, refractBlend);
    half3 lighting = directLighting + indirectDiffuse + indirectSpecular;

    ///////////////////////////////////////////////////////////////////
    /// 3. Global Lighting (Rim, Matcap, etc.)                      ///
    ///////////////////////////////////////////////////////////////////

    if (HasAnyMatCap())
    {
        GemMatCap(finalColor, normalVS, matCapUV1, matCapUV2, matCapUV3, matCapUV4, lighting);
    }
    
    if (_UseRim > 0)
    {
        finalColor += RimLighting(normalWS, viewDir, lighting, facing, rimUV, charShadowAtten);
        ScreenRimLighting(finalColor, inputData.normalizedScreenSpaceUV, i.positionWS, charShadowAtten, lighting, hasBrightestDir ? _BrightestLightDirection.xyz : mainLight.direction);
    }

#if _USE_GLITTER
    GemGlitter(finalColor, alpha, viewDir, baseNormalWS, normalWS, glitterUV, albedo, totalAttenuation, lightDir, lighting);
#endif

    GemParticleSparkle(finalColor, viewDir, normalVS, viewNormalLobe, edge2, lighting);
    finalColor = MixFog(finalColor, inputData.fogCoord);

    half alphaOut = OutputAlpha(alpha, _SurfaceType >= OIT_SURFACE);
    if (_UseDitherFade > 0
#if !_USE_DITHER_FADE
        && _SurfaceType >= OIT_SURFACE
#endif
        )
        DitherFade(alphaOut, abs(TransformWorldToView(_HeadWorldPos.xyz).z), _DitherFadeMinZ, _DitherFadeMaxZ, inputData.positionCS.xy);

#if _POTA_TOON_OIT
    if (_SurfaceType >= OIT_SURFACE && _DisableOIT == 0)
    {
        createFragmentEntry(half4(finalColor, alphaOut), i.positionCS.xyz, uSampleIdx);
        clip(-1);
    }
#endif
    
    return half4(finalColor, alphaOut);
}

#endif