#ifndef POTA_TOON_GEM_LIGHTING_INCLUDED
#define POTA_TOON_GEM_LIGHTING_INCLUDED

#include "../PotaToonGlitter.hlsl"
#include "../PotaToonRim.hlsl"

struct GemAdditionalLightParams
{
    float2 ssUV;
    float3 positionWS;
    half alpha;
    half charShadowSmoothness;
    half3 albedo;
    float3 normalWS;
    float3 viewDir;
};

struct GemAdditionalLightAccum
{
    half3 diffuse;
    half3 clearcoat;
};

inline bool HasNormalMap()
{
    return _UseNormalMap > 0;
}

inline bool HasMatCap(const uint mode, const half weight)
{
    return mode > 0 && weight > 1e-5h;
}

inline bool HasAnyMatCap()
{
    return HasMatCap(_MatCapMode, _MatCapWeight)
        || HasMatCap(_MatCapMode2, _MatCapWeight2)
        || HasMatCap(_MatCapMode3, _MatCapWeight3)
        || HasMatCap(_MatCapMode4, _MatCapWeight4);
}

float Pow5(float x)
{
    float x2 = x * x;
    return x2 * x2 * x;
}

float3 FresnelSchlick(float3 F0, float cosTheta)
{
    return F0 + (1.0 - F0) * Pow5(1.0 - cosTheta);
}

float EvaluateClearcoatFactor(float roughness, float3 N, float3 V, float3 L, float intensity)
{
    float3 H = normalize(V + L);
    float NdotL = saturate(dot(N, L));
    float NdotH = saturate(dot(N, H));
    float VdotH = saturate(dot(V, H));

    float alpha = max(roughness * roughness, 0.002);
    float D = D_GGX(NdotH, alpha);
    float3 F = FresnelSchlick(0.04, VdotH);
    float specFactor = saturate(D * F.r * intensity);
    return specFactor * NdotL;
}

float GetGemIOR()
{
    return lerp(1.02, 2.42, saturate(_GemShine));
}

half EvaluateGemViewNormalLobe(float3 viewNormalWS)
{
    half2 viewNormalXY = viewNormalWS.xy;
    half2 viewNormalDir = normalize(viewNormalXY + half2(1e-4, 1e-4));
    return saturate(0.5 + 0.5 * dot(viewNormalDir, half2(0.7071, 0.7071)));
}

inline half GetGemChromaticAberrationRemap()
{
    return _ChromaticAberration * 0.1;
}

void GetGemChromaticFactors(half NdotV, out half caBlend, out half dispersion)
{
    half edge = 1.0 - NdotV;
    half ca = GetGemChromaticAberrationRemap() * edge * edge;
    caBlend = saturate(ca * 12.0);
    dispersion = min(ca, 0.08);
}

half3 GemRefractionColor(float3 normalVS, float2 screenSpaceUV, half NdotV, half caBlend, half dispersion)
{
    half edgePow = pow(saturate(1.0 - NdotV), max(_RefractionFresnelPower, 0.01));
    float distortionWeight = lerp(0.25, 1.0, edgePow);
    float offset = _RefractionStrength * distortionWeight;
    half refractionDispersion = min(dispersion * 2.0, 0.2);
    half signedRefractionDispersion = refractionDispersion * (_RefractionStrength < 0.0 ? -1.0 : 1.0);
    float2 ref = clamp(normalVS.xy, -1, 1);

    float2 refractedUV = saturate(screenSpaceUV + offset * ref);
    float2 uvG = saturate(screenSpaceUV + (offset + signedRefractionDispersion) * ref);
    float2 uvB = saturate(screenSpaceUV + (offset + signedRefractionDispersion * 2.0) * ref);

    float2 blurStep = _ScreenSize.zw * 16 * _RefractionBlurWeight;
    half3 sceneColor = SampleSceneColor(refractedUV);
    if (_RefractionBlurWeight > 0)
    {
        sceneColor *= 0.32;
        sceneColor += SampleSceneColor(saturate(refractedUV + float2(blurStep.x, blurStep.y))) * 0.14;
        sceneColor += SampleSceneColor(saturate(refractedUV + float2(-blurStep.x, blurStep.y))) * 0.2;
        sceneColor += SampleSceneColor(saturate(refractedUV + float2(blurStep.x, -blurStep.y))) * 0.2;
        sceneColor += SampleSceneColor(saturate(refractedUV + float2(-blurStep.x, -blurStep.y))) * 0.14;
    }

    if (_ChromaticAberration > 0)
    {
        sceneColor.g = lerp(sceneColor.g, SampleSceneColor(uvG).g, caBlend);
        sceneColor.b = lerp(sceneColor.b, SampleSceneColor(uvB).b, caBlend);
    }
    
    return sceneColor;
}

half EvaluateGemRefractShare(float3 F0, half NdotV, half transmissionWeight)
{
    float3 fresnelView = FresnelSchlick(F0, NdotV);
    half Fv = saturate(dot(fresnelView, float3(0.2126, 0.7152, 0.0722)));
    half fresnelTransmit = 1.0 - Fv;
    return transmissionWeight * lerp(0.45, 1.0, fresnelTransmit);
}

half EvaluateGemPathLength(half NdotV)
{
    half edge = 1.0 - saturate(NdotV);
    half edgeBoost = lerp(1.0, 2.5, pow(edge, 1.35));
    return max(_Thickness, 0.0) * edgeBoost;
}

half3 EvaluateGemTransmittance(half pathLength)
{
    float3 absorption = max(_AbsorptionColor.rgb, 0.0);
    float d = max(pathLength, 0.0);
    return exp2(-absorption * d * 1.442695);
}

half3 SampleGemEnvironmentReflection(float3 reflectVector, float3 positionWS, float roughness, float2 normalizedScreenSpaceUV)
{
#if UNITY_VERSION >= 202230
    return GlossyEnvironmentReflection(reflectVector, positionWS, roughness, 1.0, normalizedScreenSpaceUV);
#else
    return GlossyEnvironmentReflection(reflectVector, roughness, 1.0);
#endif
}

half3 EvaluateGemEnvSpec(float3 normalWS, float3 viewDir, float3 positionWS, float2 normalizedScreenSpaceUV, half facing, half NdotV, float f0, float roughness, half indirectDimmer, half caBlend, half dispersion)
{
    half3 envSpec = 0;
#if !defined(_ENVIRONMENTREFLECTIONS_OFF)
    if (indirectDimmer > 0.0001)
    {
        float3 reflDir = reflect(-viewDir, normalWS);
        half3 envBase = SampleGemEnvironmentReflection(reflDir, positionWS, roughness, normalizedScreenSpaceUV);
        half3 envColor = envBase;
        if (caBlend > 0.0001)
        {
            half invnv = 1.0 - NdotV;
            float3 nG = normalWS;
            float3 nB = normalWS;
            if (facing < 0)
            {
                nG = normalize(normalWS + viewDir * invnv * dispersion);
                nB = normalize(normalWS + viewDir * invnv * dispersion * 2.0);
            }
            half3 envG = SampleGemEnvironmentReflection(reflect(-viewDir, nG), positionWS, roughness, normalizedScreenSpaceUV);
            half3 envB = SampleGemEnvironmentReflection(reflect(-viewDir, nB), positionWS, roughness, normalizedScreenSpaceUV);
            envColor = lerp(envBase, half3(envBase.r, envG.g, envB.b), caBlend);
        }

        half smoothness = 1.0 - saturate(roughness);
        half grazingTerm = saturate(smoothness + 0.04);
        half fresnelLerp = lerp(f0, grazingTerm, Pow5(1.0 - NdotV));
        half roughness2 = roughness * roughness;
        half roughness4 = roughness2 * roughness2;
        half surfaceReduction = 1.0 / (roughness4 + 1.0);
        envSpec = max(0, envColor * (surfaceReduction * fresnelLerp) * indirectDimmer);
    }
#endif
    return envSpec;
}

void AccumulateAdditionalGemLight(uint lightIndex, Light light, bool isBrightestAdditional, GemAdditionalLightParams params, inout GemAdditionalLightAccum accum, inout float totalAttenuation)
{
    half addShadowFactor = 1.0;
    if (_DisableCharShadow == 0 && isBrightestAdditional)
    {
        half rawShadow = SampleAdditionalCharacterAndTransparentShadow(params.ssUV, params.positionWS, params.alpha, 0, lightIndex);
        half addShadowAtten = LinearStep(0.3 - params.charShadowSmoothness, 0.7 + params.charShadowSmoothness, rawShadow);
        addShadowFactor = 1.0 - addShadowAtten;
        totalAttenuation = min(totalAttenuation, addShadowFactor);
    }

    float3 addDir = normalize(light.direction);
    half NdotLAdd = saturate(dot(params.normalWS, addDir));
    half lightShadowAtten = (_ReceiveLightShadow > 0 && isBrightestAdditional) ? light.shadowAttenuation : 1.0;
    if (_ReceiveLightShadow > 0 && isBrightestAdditional)
        totalAttenuation = min(totalAttenuation, lightShadowAtten);
    half3 addColor = light.color * light.distanceAttenuation * lightShadowAtten * addShadowFactor;
    accum.diffuse += params.albedo * NdotLAdd * addColor;

    if (isBrightestAdditional)
    {
        half addClear = EvaluateClearcoatFactor(_ClearcoatRoughness, params.normalWS, params.viewDir, addDir, _ClearcoatIntensity);
        accum.clearcoat += addClear * addColor;
    }
}

void ApplyGemRotationBoost(inout half3 clearcoat, inout half3 envSpec, half viewNormalLobe)
{
    half clearcoatRotationBoost = lerp(0.85, 1.35, viewNormalLobe);
    half envRotationBoost = lerp(0.90, 1.40, viewNormalLobe);
    clearcoat *= clearcoatRotationBoost;
    envSpec *= envRotationBoost;
}

half3 EvaluateGemMatCap(TEXTURE2D_PARAM(tex, smp), TEXTURE2D(mask), const half4 color, float2 matcapUV, float2 uv, uint maskChannel)
{
    half3 matcapMap = SAMPLE_TEXTURE2D_LOD(tex, smp, matcapUV, 0).rgb;
    float matcapMask = SelectMask(SAMPLE_TEXTURE2D_LOD(mask, smp, uv, 0), maskChannel);
    return matcapMask > 0 ? matcapMap * color.rgb * color.a : -1;
}

void ApplyGemMatCap(half3 lighting, half3 matcapLighting, uint mode, half weight, half lightingDimmer, inout half3 totalMatcapAddColor, inout half3 totalMatcapMultiplyColor)
{
    if (mode == 0 || matcapLighting.r < 0)
        return;

    half modeLerp = mode - 1.0;
    half3 matcapAddColor = lerp(0.0, lerp(matcapLighting, 0.0, modeLerp), weight);
    totalMatcapAddColor += lerp(matcapAddColor, matcapAddColor * lighting, lightingDimmer);
    totalMatcapMultiplyColor *= lerp(1.0, lerp(1.0, matcapLighting, modeLerp), weight);
}

void GemMatCap(inout half3 finalColor, float3 normalVS, float2 uv1, float2 uv2, float2 uv3, float2 uv4, half3 lighting)
{
    half3 totalMatcapAddColor = 0;
    half3 totalMatcapMultiplyColor = 1;
    float2 matcapUV = normalVS.xy * 0.5 + 0.5;
    if (HasMatCap(_MatCapMode, _MatCapWeight))
    {
        half3 matcapLighting = EvaluateGemMatCap(TEXTURE2D_ARGS(_MatCapTex, sampler_MatCapTex), _MatCapMask, _MatCapColor, matcapUV, uv1, _MatCapMaskCH);
        ApplyGemMatCap(lighting, matcapLighting, _MatCapMode, _MatCapWeight, _MatCapLightingDimmer, totalMatcapAddColor, totalMatcapMultiplyColor);
    }
    if (HasMatCap(_MatCapMode2, _MatCapWeight2))
    {
        half3 matcapLighting2 = EvaluateGemMatCap(TEXTURE2D_ARGS(_MatCapTex2, sampler_MatCapTex), _MatCapMask2, _MatCapColor2, matcapUV, uv2, _MatCapMaskCH2);
        ApplyGemMatCap(lighting, matcapLighting2, _MatCapMode2, _MatCapWeight2, _MatCapLightingDimmer2, totalMatcapAddColor, totalMatcapMultiplyColor);
    }
    if (HasMatCap(_MatCapMode3, _MatCapWeight3))
    {
        half3 matcapLighting3 = EvaluateGemMatCap(TEXTURE2D_ARGS(_MatCapTex3, sampler_MatCapTex), _MatCapMask3, _MatCapColor3, matcapUV, uv3, _MatCapMaskCH3);
        ApplyGemMatCap(lighting, matcapLighting3, _MatCapMode3, _MatCapWeight3, _MatCapLightingDimmer3, totalMatcapAddColor, totalMatcapMultiplyColor);
    }
    if (HasMatCap(_MatCapMode4, _MatCapWeight4))
    {
        half3 matcapLighting4 = EvaluateGemMatCap(TEXTURE2D_ARGS(_MatCapTex4, sampler_MatCapTex), _MatCapMask4, _MatCapColor4, matcapUV, uv4, _MatCapMaskCH4);
        ApplyGemMatCap(lighting, matcapLighting4, _MatCapMode4, _MatCapWeight4, _MatCapLightingDimmer4, totalMatcapAddColor, totalMatcapMultiplyColor);
    }

    finalColor *= totalMatcapMultiplyColor;
    finalColor += totalMatcapAddColor;
}

void GemParticleSparkle(inout half3 finalColor, float3 viewDir, float3 normalVS, half viewNormalLobe, half edge2, half3 lighting)
{
    if (_ParticleIntensity <= 0.0001)
        return;

    half loop = max(_ParticleLoop, 1.0);
    
    half nv1 = abs(dot(normalVS, viewDir));
    half nv2 = abs(dot(normalVS, viewDir.yzx));
    half nv3 = abs(dot(normalVS, viewDir.zxy));
    half sparkle = step(0.5, frac(nv1 * loop)) * step(0.5, frac(nv2 * loop)) * step(0.5, frac(nv3 * loop));
    half2 viewNormal = normalVS.xy * 0.5 + 0.5;
    half viewSparkle = step(0.5, frac(viewNormal.x * loop)) * step(0.5, frac(viewNormal.y * loop));
    sparkle *= lerp(1.0, 1.5, viewSparkle);
    half particleViewWeight = lerp(1.0, 2.0, edge2) * lerp(1.0, 1.5, viewNormalLobe);
    half3 baseSparkle = sparkle * _ParticleIntensity * _ParticleColor.rgb * particleViewWeight;
    half3 particleLighting = lerp(1.0, lighting, _ParticleLightingDimmer);
    finalColor += baseSparkle * particleLighting;
}

#if _USE_GLITTER
void GemGlitter(inout half3 finalColor, half alpha, float3 viewDir, float3 baseNormalWS, float3 normalWS, float2 uv, half3 albedo, half shadowAtten, float3 lightDir, half3 lighting)
{
    half3 glitterColor = Glitter(finalColor, alpha, viewDir, baseNormalWS, normalWS, uv, albedo, shadowAtten, lightDir, lighting);
    finalColor += glitterColor;
}
#endif

#endif