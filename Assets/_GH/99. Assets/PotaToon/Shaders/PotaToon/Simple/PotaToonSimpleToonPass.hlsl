#ifndef SIMPLE_TOON_PASS_INCLUDED
#define SIMPLE_TOON_PASS_INCLUDED

#include "../../ChracterShadow/DeclareCharacterShadowTexture.hlsl"
#include "../../Common/PotaToonColorGrading.hlsl"
#include "./PotaToonSimpleToonLighting.hlsl"

struct VertexInput
{
    float4 vertex               : POSITION;
    float4 color                : COLOR;
    float3 normal               : NORMAL;
    float4 tangent              : TANGENT;
    float4 texcoord0            : TEXCOORD0;
    float2 staticLightmapUV     : TEXCOORD4;
    float2 dynamicLightmapUV    : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 positionCS           : SV_POSITION;
    float4 color                : COLOR;
    float2 uv0                  : TEXCOORD0;
    float3 positionWS           : TEXCOORD4;
    float3 normalWS             : TEXCOORD5;
    float3 tangentWS            : TEXCOORD6;
    float3 bitangentWS          : TEXCOORD7;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
    half  fogFactor             : TEXCOORD9;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord          : TEXCOORD10;
#endif

#ifdef DYNAMICLIGHTMAP_ON
    float2 dynamicLightmapUV    : TEXCOORD11;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion       : TEXCOORD12;
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    half3 vertexLighting        : TEXCOORD13;
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
    o.color = v.color;
    o.uv0 = v.texcoord0.xy;
    o.normalWS = TransformObjectToWorldDir(v.normal);
    o.positionWS = vertexInput.positionWS;
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

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    o.vertexLighting = VertexLighting(vertexInput.positionWS, normalize(o.normalWS));
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

half4 frag(VertexOutput i, half facing : VFACE) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    half alpha = 0;
#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
#endif
    const float2 baseUV = i.uv0;
    const float3 positionWS = i.positionWS;
    i.normalWS = normalize(i.normalWS);
    float3 normalWS = i.normalWS;
    if (HasNormalMap())
    {
        const float3 normalMap = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_MainTex, TRANSFORM_TEX(baseUV, _NormalMap)), _BumpScale);
        float3x3 tangentTransform = float3x3(i.tangentWS, i.bitangentWS, i.normalWS);
        normalWS = normalize(mul(normalMap.rgb, tangentTransform));
    }

    const float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS).xyz;
    const float3 viewNormal = TransformWorldToViewDir(normalWS);
    InputData inputData = InitializeInputData(i);
    const float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;

    half4 vertexColor = lerp(1, i.color, _UseVertexColor);
    half4 baseMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(baseUV, _MainTex)) * vertexColor;
    half4 shadeMap = HasShadeMap() ? SAMPLE_TEXTURE2D(_ShadeMap, sampler_MainTex, TRANSFORM_TEX(baseUV, _ShadeMap)) * vertexColor : baseMap;

    if (HasTextureColorAdjustments(_BaseMapHue, _BaseMapSaturation, _BaseMapContrast))
        baseMap.rgb = PotaToonApplyHSV(baseMap.rgb, _BaseMapHue, _BaseMapSaturation, _BaseMapContrast);
    if (HasTextureColorAdjustments(_ShadeMapHue, _ShadeMapSaturation, _ShadeMapContrast))
        shadeMap.rgb = PotaToonApplyHSV(shadeMap.rgb, _ShadeMapHue, _ShadeMapSaturation, _ShadeMapContrast);

    half opacity = baseMap.a * _BaseColor.a;
    const float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(baseUV, _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    opacity = PotaToonApplyAlphaMask(opacity, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    half3 finalColor = 0;
    float totalAttenuation = 1;

    half aoMap = SelectMask(SAMPLE_TEXTURE2D_LOD(_ShadowBorderMask, sampler_MainTex, baseUV, 0), _AOMapCH);
    aoMap = LinearStep(_BaseStep - _StepSmoothness, _BaseStep + _StepSmoothness, aoMap);

#if _ALPHATEST_ON
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, TRANSPARENT_SURFACE);
    clip(opacity - cutoff - 0.001);
#endif

    half3 midTone = 0;
    totalAttenuation *= aoMap;

    half4 shadowMask = CalculateShadowMask(inputData);
    Light mainLight = GetMainLight(inputData.shadowCoord, i.positionWS, shadowMask);
    float charShadowAtten = 0;
    half3 directLighting = 0;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        directLighting += MainLighting(mainLight, positionWS, normalWS, viewDir, normalizedScreenSpaceUV, baseUV, opacity, charShadowAtten, totalAttenuation, midTone, aoMap);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    half3 additionalLightsColor = 0;

#if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additionalLightsColor += AdditionalLighting(light, normalWS, viewDir, normalizedScreenSpaceUV, baseUV, positionWS, lightIndex, opacity, charShadowAtten, totalAttenuation, midTone);
        }
    }
#endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additionalLightsColor += AdditionalLighting(light, normalWS, viewDir, normalizedScreenSpaceUV, baseUV, positionWS, lightIndex, opacity, charShadowAtten, totalAttenuation, midTone);
        }
    LIGHT_LOOP_END

    directLighting += additionalLightsColor;
#endif

    half3 textureAlbedo = lerp(shadeMap.rgb, baseMap.rgb, totalAttenuation);
    directLighting *= textureAlbedo;

    if (_UseMidTone > 0)
    {
        midTone *= textureAlbedo;
        finalColor += midTone;
    }
    finalColor += directLighting;

    half3 finalBaseColor = lerp(shadeMap.rgb * _ShadeColor.rgb, baseMap.rgb * _BaseColor.rgb, totalAttenuation);

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    finalColor += i.vertexLighting * finalBaseColor;
#endif

    BRDFData brdfData;
    InitializeBRDFData(finalBaseColor, 0, 0, 0, alpha, brdfData);
#if defined(DYNAMICLIGHTMAP_ON)
    half3 bakedGI = SAMPLE_GI(i.staticLightmapUV, i.dynamicLightmapUV, i.vertexSH, normalWS);
#elif UNITY_VERSION >= 60000023 && !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    half3 bakedGI = SAMPLE_GI(i.vertexSH, GetAbsolutePositionWS(positionWS), normalWS, viewDir, i.positionCS.xy, i.probeOcclusion, inputData.shadowMask);
#else
    half3 bakedGI = SAMPLE_GI(i.staticLightmapUV, i.vertexSH, normalWS);
#endif
    MixRealtimeAndBakedGI(mainLight, normalWS, bakedGI);
#if UNITY_VERSION >= 202230
    half3 indirectLighting = GlobalIllumination(brdfData, (BRDFData)0, 0, bakedGI, 1, positionWS, normalWS, viewDir, normalizedScreenSpaceUV);
#else
    half3 indirectLighting = GlobalIllumination(brdfData, (BRDFData)0, 0, bakedGI, 1, positionWS, normalWS, viewDir);
#endif
    indirectLighting = max(0, indirectLighting * _IndirectDimmer);
    finalColor += indirectLighting;

    half3 lighting = directLighting + indirectLighting;
    finalColor = min(finalColor, finalBaseColor * _MaxToonBrightness + midTone);

    half3 totalMatcapAddColor = 0;
    half3 totalMatcapMultiplyColor = 1;
    if (HasAnyMatCap())
    {
        const float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
        if (HasMatCap(_MatCapMode, _MatCapWeight))
        {
            const half3 matcapLighting = MatCap(TEXTURE2D_ARGS(_MatCapTex, sampler_MatCapTex), _MatCapMask, _MatCapColor, matcapUV, baseUV, _MatCapMaskCH1);
            if (matcapLighting.r >= 0)
            {
                half3 matcapAddColor = lerp(0, lerp(matcapLighting, 0, _MatCapMode - 1), _MatCapWeight);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcapLighting, _MatCapMode - 1), _MatCapWeight);
            }
        }
        if (HasMatCap(_MatCapMode2, _MatCapWeight2))
        {
            const half3 matcapLighting = MatCap(TEXTURE2D_ARGS(_MatCapTex2, sampler_MatCapTex), _MatCapMask2, _MatCapColor2, matcapUV, baseUV, _MatCapMaskCH2);
            if (matcapLighting.r >= 0)
            {
                half3 matcapAddColor = lerp(0, lerp(matcapLighting, 0, _MatCapMode2 - 1), _MatCapWeight2);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer2);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcapLighting, _MatCapMode2 - 1), _MatCapWeight2);
            }
        }
    }
    finalColor *= totalMatcapMultiplyColor;
    finalColor += totalMatcapAddColor;
    half3 rimLight = RimLighting(normalWS, viewDir, lighting, facing, i.uv0, charShadowAtten);
    finalColor += rimLight;
    bool hasBrightestDir = dot(_BrightestLightDirection.xyz, _BrightestLightDirection.xyz) > 1e-6;
    ScreenRimLighting(finalColor, normalizedScreenSpaceUV, positionWS, charShadowAtten, lighting, hasBrightestDir ? _BrightestLightDirection.xyz : mainLight.direction);

    if (HasEmission())
        finalColor += Emission(baseUV);

    finalColor = MixFog(finalColor, inputData.fogCoord);
    alpha = OutputAlpha(opacity, _SurfaceType >= OIT_SURFACE);

    if (_UseDitherFade > 0
#if !_USE_DITHER_FADE
        && _SurfaceType >= OIT_SURFACE
#endif
        )
    {
        DitherFade(alpha, abs(TransformWorldToView(_HeadWorldPos.xyz).z), _DitherFadeMinZ, _DitherFadeMaxZ, inputData.positionCS.xy);
    }

    return half4(finalColor, alpha);
}

#endif