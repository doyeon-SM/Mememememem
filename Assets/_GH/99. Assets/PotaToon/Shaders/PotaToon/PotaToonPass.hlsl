#ifndef POTA_TOON_PASS_INCLUDED
#define POTA_TOON_PASS_INCLUDED

#include "../ChracterShadow/DeclareCharacterShadowTexture.hlsl"
#include "../Common/PotaToonColorGrading.hlsl"
#include "./PotaToonLighting.hlsl"
#if _POTA_TOON_OIT
#include "../OIT/LinkedListCreation.hlsl"
#endif

struct VertexInput
{
    float4 vertex               : POSITION;
    float4 color                : COLOR;
    float3 normal               : NORMAL;
    float4 tangent              : TANGENT;
    float4 texcoord0            : TEXCOORD0;
    float4 texcoord1            : TEXCOORD1;
    float4 texcoord2            : TEXCOORD2;
    float4 texcoord3            : TEXCOORD3;
    float2 staticLightmapUV     : TEXCOORD4;
    float2 dynamicLightmapUV    : TEXCOORD5;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 positionCS           : SV_POSITION;
    float4 color                : COLOR;
    float2 uv0                  : TEXCOORD0;
    float2 uv1                  : TEXCOORD1;
    float2 uv2                  : TEXCOORD2;
    float2 uv3                  : TEXCOORD3;
    float3 positionWS           : TEXCOORD4;
    float3 normalWS             : TEXCOORD5;
    float3 tangentWS            : TEXCOORD6;
    float3 bitangentWS          : TEXCOORD7;
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 8);
    half  fogFactor            	: TEXCOORD9;
    
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord          : TEXCOORD10;
#endif

#ifdef DYNAMICLIGHTMAP_ON
    float2  dynamicLightmapUV   : TEXCOORD11; // Dynamic lightmap UVs
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion : TEXCOORD12;
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
    o.uv1 = v.texcoord1.xy;
    o.uv2 = v.texcoord2.xy;
    o.uv3 = v.texcoord3.xy;
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
    
    return o;
}

InputData InitializeInputData(VertexOutput input)
{
    InputData inputData;
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

#if _POTA_TOON_OIT
[earlydepthstencil]
#endif
half4 frag(VertexOutput i, half facing : VFACE, uint uSampleIdx : SV_SampleIndex) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
    half alpha = 0;
#ifdef _LIGHT_LAYERS
    uint meshRenderingLayers = GetMeshRenderingLayer();
#endif

    // Setup Input Data
    const float2 uvArray[4] = { i.uv0, i.uv1, i.uv2, i.uv3 };
    const float2 baseUV = SelectUV(_BaseMapUV, uvArray);
    const bool hasBaseColorGrading = HasTextureColorAdjustments(_BaseMapHue, _BaseMapSaturation, _BaseMapContrast);
    const bool hasShadeColorGrading = HasTextureColorAdjustments(_ShadeMapHue, _ShadeMapSaturation, _ShadeMapContrast);
    const bool hasColorGradingMask = hasBaseColorGrading || hasShadeColorGrading;
    const float3 positionWS = i.positionWS;
    i.normalWS = normalize(i.normalWS);
    float3 normalWS = i.normalWS;
    if (HasNormalMap())
    {
        const float3 normalMap = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_MainTex, TRANSFORM_TEX(SelectUV(_NormalMapUV, uvArray), _NormalMap)), _BumpScale);
        float3x3 tangentTransform = float3x3(i.tangentWS, i.bitangentWS, i.normalWS);
        normalWS = normalize(mul(normalMap.rgb, tangentTransform));
    }

    const float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS).xyz;
    const float3 viewNormal = TransformWorldToViewDir(normalWS);
    
    InputData inputData = InitializeInputData(i);
    const float2 normalizedScreenSpaceUV = inputData.normalizedScreenSpaceUV;
    half colorGradingMask = 0;
    if (hasColorGradingMask)
    {
        const float2 colorGradingMaskUV = TRANSFORM_TEX(SelectUV(_ColorGradingMaskUV, uvArray), _ColorGradingMask);
        colorGradingMask = saturate(abs(_ColorGradingMaskReversed - SAMPLE_TEXTURE2D(_ColorGradingMask, sampler_MainTex, colorGradingMaskUV).r));
    }
    half shadowExclude = 0;
    if (HasShadowExclusionMask())
    {
        const float2 shadowExclusionMaskUV = TRANSFORM_TEX(baseUV, _ShadowExclusionMask);
        shadowExclude = saturate(abs((1.0 - _ShadowExclusionMaskReversed) - SAMPLE_TEXTURE2D(_ShadowExclusionMask, sampler_MainTex, shadowExclusionMaskUV).r));
    }

    half4 vertexColor = lerp(1, i.color, _UseVertexColor);
    half4 baseMap = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(baseUV, _MainTex)) * vertexColor;
    half4 shadeMap = baseMap;
    if (HasShadeMap())
        shadeMap = SAMPLE_TEXTURE2D(_ShadeMap, sampler_MainTex, TRANSFORM_TEX(baseUV, _ShadeMap)) * vertexColor;
    if (hasBaseColorGrading)
        baseMap.rgb = PotaToonApplyTextureHSV(baseMap.rgb, _BaseMapHue, _BaseMapSaturation, _BaseMapContrast, colorGradingMask);
    if (hasShadeColorGrading)
        shadeMap.rgb = PotaToonApplyTextureHSV(shadeMap.rgb, _ShadeMapHue, _ShadeMapSaturation, _ShadeMapContrast, colorGradingMask);
    half opacity = baseMap.a * _BaseColor.a;
    const float clippingMask = SelectMask(SAMPLE_TEXTURE2D(_ClippingMask, sampler_MainTex, TRANSFORM_TEX(SelectUV(_ClippingMaskUV, uvArray), _ClippingMask)), _ClippingMaskCH);
    PotaToonApplyClippingMask(clippingMask, _AlphaMaskMode, _ClippingMaskCutoff);
    opacity = PotaToonApplyAlphaMask(opacity, clippingMask, _AlphaMaskMode, _AlphaMaskScale, _AlphaMaskValue);
    half3 finalColor = 0;
    float totalAttenuation = 1;

    // Ambient Occlusion
    half aoMap = SelectMask(SAMPLE_TEXTURE2D_LOD(_ShadowBorderMask, sampler_MainTex, baseUV, 0), _AOMapCH);
    aoMap = LinearStep(_BaseStep - _StepSmoothness, _BaseStep + _StepSmoothness, aoMap);

#if _ALPHATEST_ON
    float cutoff = PotaToonGetAlphaCutoff(_Cutoff, _SurfaceType, REFRACTION_SURFACE);
    clip(opacity - cutoff - 0.001);
#endif

    ///////////////////////////////////////////////////////////////////
    /// 0. Brightest Light                                          ///
    ///////////////////////////////////////////////////////////////////

    half3 midTone = 0;
    half3 directLighting = 0;
    
    // Face SDF Shadow
    float faceSdfAtten = 1;
#if _USE_FACE_SDF
    faceSdfAtten = GetFaceSDFAtten(TRANSFORM_TEX(SelectUV(_FaceSDFUV, uvArray), _FaceSDFTex));
    if (_UseMidTone > 0)
    {
        // Only calculate MidTone for the main light.
        if (_UseBrightestLight == 0 || _IsBrightestLightMain > 0)
        {
            midTone += GetMidTone(faceSdfAtten, 0, _StepSmoothness * _MidWidth);
        }
    }
    faceSdfAtten = LinearStep(-_StepSmoothness, _StepSmoothness, faceSdfAtten);
    faceSdfAtten = lerp(faceSdfAtten, 1.0, shadowExclude);
    totalAttenuation = faceSdfAtten;
#endif
    totalAttenuation *= aoMap;
    
    ///////////////////////////////////////////////////////////////////
    /// 1. Direct Lighting                                          ///
    ///  - Apply step for the 'most powerful' light                 ///
    ///////////////////////////////////////////////////////////////////

    float charShadowAtten = 0;
	half4 shadowMask = CalculateShadowMask(inputData);
    float2 specularUV = 0;
    if (HasSpecular())
        specularUV = SelectUV(_SpecularMapUV, uvArray);
    Light mainLight = GetMainLight(inputData.shadowCoord, i.positionWS, shadowMask);
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        directLighting += MainLighting(mainLight, positionWS, normalWS, viewDir, normalizedScreenSpaceUV, specularUV, opacity, charShadowAtten, faceSdfAtten, totalAttenuation, midTone, aoMap, shadowExclude);
    }

#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    half3 additionalLightsColor = 0;

    // Directional Lights
#if USE_FORWARD_PLUS
    for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additionalLightsColor += AdditionalLighting(light, normalWS, viewDir, normalizedScreenSpaceUV, specularUV, positionWS, lightIndex, opacity, charShadowAtten, faceSdfAtten, totalAttenuation, midTone, shadowExclude);
        }
    }
#endif

    // Local Lights
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, shadowMask);

    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additionalLightsColor += AdditionalLighting(light, normalWS, viewDir, normalizedScreenSpaceUV, specularUV, positionWS, lightIndex, opacity, charShadowAtten, faceSdfAtten, totalAttenuation, midTone, shadowExclude);
        }
    LIGHT_LOOP_END

    directLighting += additionalLightsColor;
#endif
    
    half3 textureAlbedo = lerp(shadeMap.rgb, baseMap.rgb, totalAttenuation);
    directLighting *= textureAlbedo;
    // Set directLighting result to zero if there's no light.
    if (_BrightestLightIndex < 0)
    {
        directLighting = 0;
    }

    if (_UseMidTone > 0)
    {
        midTone *= textureAlbedo;
        finalColor += midTone;
    }
    finalColor += directLighting;
    
    ///////////////////////////////////////////////////////////////////
    /// 2. Indirect Lighting (We don't apply step)                  ///
    ///////////////////////////////////////////////////////////////////

    BRDFData brdfData;
    half3 finalBaseColor = lerp(shadeMap.rgb * _ShadeColor.rgb, baseMap.rgb * _BaseColor.rgb, totalAttenuation);
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
    indirectLighting = max(0, indirectLighting * _IndirectDimmer); // Prevent NaN
    finalColor += indirectLighting;

    ///////////////////////////////////////////////////////////////////////////////////////////////
    /// 3. Global Lighting (Rim, Matcap, etc.) from accumulated light (Direct + Indirect)       ///
    ///////////////////////////////////////////////////////////////////////////////////////////////

    half3 lighting = directLighting + indirectLighting;
    
    // Composition before global lighting
    finalColor = min(finalColor, finalBaseColor * _MaxToonBrightness + midTone);

    // Hair High Light
    if (_UseHairHighLight > 0)
    {
        half3 hairHighLight = AnisotropicHairHighlight(viewDir, SelectUV(_HairHiMapUV, uvArray), positionWS, totalAttenuation) * lighting;
        finalColor += hairHighLight;
    }

    // Matcap
    half3 matcapAddColor = 0;
    half3 totalMatcapAddColor = 0;
    half3 totalMatcapMultiplyColor = 1;
    if (HasAnyMatCap())
    {
        const float2 matcapUV = viewNormal.xy * 0.5 + 0.5;
        if (HasMatCap(_MatCapMode, _MatCapWeight))
        {
            const half3 matcapLighting = MatCap(TEXTURE2D_ARGS(_MatCapTex, sampler_MatCapTex), _MatCapMask, _MatCapColor, matcapUV, SelectUV(_MatCapUV1, uvArray), _MatCapMaskCH1);
            if (matcapLighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcapLighting, 0, _MatCapMode - 1), _MatCapWeight);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcapLighting, _MatCapMode - 1), _MatCapWeight);
            }
        }
        if (HasMatCap(_MatCapMode2, _MatCapWeight2))
        {
            const half3 matcap2Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex2, sampler_MatCapTex), _MatCapMask2, _MatCapColor2, matcapUV, SelectUV(_MatCapUV2, uvArray), _MatCapMaskCH2);
            if (matcap2Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap2Lighting, 0, _MatCapMode2 - 1), _MatCapWeight2);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer2);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap2Lighting, _MatCapMode2 - 1), _MatCapWeight2);
            }
        }
        if (HasMatCap(_MatCapMode3, _MatCapWeight3))
        {
            const half3 matcap3Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex3, sampler_MatCapTex), _MatCapMask3, _MatCapColor3, matcapUV, SelectUV(_MatCapUV3, uvArray), _MatCapMaskCH3);
            if (matcap3Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap3Lighting, 0, _MatCapMode3 - 1), _MatCapWeight3);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer3);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap3Lighting, _MatCapMode3 - 1), _MatCapWeight3);
            }
        }
        if (HasMatCap(_MatCapMode4, _MatCapWeight4))
        {
            const half3 matcap4Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex4, sampler_MatCapTex), _MatCapMask4, _MatCapColor4, matcapUV, SelectUV(_MatCapUV4, uvArray), _MatCapMaskCH4);
            if (matcap4Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap4Lighting, 0, _MatCapMode4 - 1), _MatCapWeight4);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer4);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap4Lighting, _MatCapMode4 - 1), _MatCapWeight4);
            }
        }
        if (HasMatCap(_MatCapMode5, _MatCapWeight5))
        {
            const half3 matcap5Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex5, sampler_MatCapTex), _MatCapMask5, _MatCapColor5, matcapUV, SelectUV(_MatCapUV5, uvArray), _MatCapMaskCH5);
            if (matcap5Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap5Lighting, 0, _MatCapMode5 - 1), _MatCapWeight5);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer5);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap5Lighting, _MatCapMode5 - 1), _MatCapWeight5);
            }
        }
        if (HasMatCap(_MatCapMode6, _MatCapWeight6))
        {
            const half3 matcap6Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex6, sampler_MatCapTex), _MatCapMask6, _MatCapColor6, matcapUV, SelectUV(_MatCapUV6, uvArray), _MatCapMaskCH6);
            if (matcap6Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap6Lighting, 0, _MatCapMode6 - 1), _MatCapWeight6);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer6);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap6Lighting, _MatCapMode6 - 1), _MatCapWeight6);
            }
        }
        if (HasMatCap(_MatCapMode7, _MatCapWeight7))
        {
            const half3 matcap7Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex7, sampler_MatCapTex), _MatCapMask7, _MatCapColor7, matcapUV, SelectUV(_MatCapUV7, uvArray), _MatCapMaskCH7);
            if (matcap7Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap7Lighting, 0, _MatCapMode7 - 1), _MatCapWeight7);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer7);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap7Lighting, _MatCapMode7 - 1), _MatCapWeight7);
            }
        }
        if (HasMatCap(_MatCapMode8, _MatCapWeight8))
        {
            const half3 matcap8Lighting = MatCap(TEXTURE2D_ARGS(_MatCapTex8, sampler_MatCapTex), _MatCapMask8, _MatCapColor8, matcapUV, SelectUV(_MatCapUV8, uvArray), _MatCapMaskCH8);
            if (matcap8Lighting.r >= 0)
            {
                matcapAddColor = lerp(0, lerp(matcap8Lighting, 0, _MatCapMode8 - 1), _MatCapWeight8);
                totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, _MatCapLightingDimmer8);
                totalMatcapMultiplyColor *= lerp(1, lerp(1, matcap8Lighting, _MatCapMode8 - 1), _MatCapWeight8);
            }
        }
    }
    finalColor *= totalMatcapMultiplyColor;
    finalColor += totalMatcapAddColor;

    // Rim Light
    half3 rimLight = RimLighting(normalWS, viewDir, lighting, facing, SelectUV(_RimMaskUV, uvArray), charShadowAtten);
    finalColor += rimLight;
    bool hasBrightestDir = dot(_BrightestLightDirection.xyz, _BrightestLightDirection.xyz) > 1e-6;
    ScreenRimLighting(finalColor, normalizedScreenSpaceUV, positionWS, charShadowAtten, lighting, hasBrightestDir ? _BrightestLightDirection.xyz : mainLight.direction);

#if _USE_GLITTER
    // Glitter
    half3 glitterColor = Glitter(finalColor, opacity, viewDir, i.normalWS, normalWS, SelectUV(_GlitterMapUV, uvArray), textureAlbedo, totalAttenuation, _BrightestLightDirection.xyz, lighting);
    finalColor += glitterColor;
#endif
    
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// 4. Emission                                                                                                     ///
    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    if (HasEmission())
        finalColor += Emission(SelectUV(_EmissionMapUV, uvArray));

    // Return
    finalColor = MixFog(finalColor, inputData.fogCoord);
    alpha = OutputAlpha(opacity, _SurfaceType >= OIT_SURFACE);
    
    if (_UseDitherFade > 0
#if !_USE_DITHER_FADE
        && _SurfaceType >= OIT_SURFACE
#endif
        )
        DitherFade(alpha, abs(TransformWorldToView(_HeadWorldPos.xyz).z), _DitherFadeMinZ, _DitherFadeMaxZ, inputData.positionCS.xy);

    // Refraction
    if (_SurfaceType == REFRACTION_SURFACE)
        ApplyRefraction(viewDir, TransformObjectToWorldDir(float3(0, 0, 1)), normalizedScreenSpaceUV, opacity, finalColor);

#if _POTA_TOON_OIT
    if (_SurfaceType >= OIT_SURFACE && _DisableOIT == 0)
    {
        createFragmentEntry(half4(finalColor, alpha), i.positionCS.xyz, uSampleIdx);
        clip(-1);
    }
#endif

#if _DEBUG_POTA_TOON
    // Apply Debug
    if (_DebugFaceSDF == DEBUG_FACE_SDF_LIGHTING)
    {
        finalColor = faceSdfAtten.rrr;
        alpha = 1;
    }
    if (_DebugFaceSDF == DEBUG_FACE_SDF_TEXTURE)
    {
        finalColor = SelectMask(SAMPLE_TEXTURE2D(_FaceSDFTex, sampler_FaceSDFTex, TRANSFORM_TEX(SelectUV(_FaceSDFUV, uvArray), _FaceSDFTex)), _FaceSDFTexCH);
        alpha = 1;
    }
#endif
    
    return half4(finalColor, alpha);
}

#endif