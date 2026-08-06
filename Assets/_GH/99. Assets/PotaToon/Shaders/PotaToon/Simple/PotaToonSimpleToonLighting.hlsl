#ifndef SIMPLE_TOON_LIGHTING_INCLUDED
#define SIMPLE_TOON_LIGHTING_INCLUDED

#include "./PotaToonSimpleToonUtility.hlsl"
#include "./PotaToonSimpleToonRim.hlsl"

void HalfLambert(float3 lightDirection, float3 normalWS, float step, float smoothness, out float halfLambert, out float halfLambertStep)
{
    float M = step + smoothness;
    float m = step - smoothness;
    halfLambert = dot(lightDirection, normalWS) * 0.5 + 0.5;
    halfLambertStep = LinearStep(m, M, halfLambert);
}

inline bool HasShadeMap()
{
    return _UseShadeMap > 0;
}

inline bool HasNormalMap()
{
    return _UseNormalMap > 0;
}

inline bool HasSpecular()
{
    return _SpecularColor.a > 0 && any(abs(_SpecularColor.rgb) > 1e-5h);
}

inline bool HasEmission()
{
    return _EmissionColor.a > 0 && any(abs(_EmissionColor.rgb) > 1e-5h);
}

inline bool HasMatCap(const uint mode, const half weight)
{
    return mode > 0 && weight > 1e-5h;
}

inline bool HasAnyMatCap()
{
    return HasMatCap(_MatCapMode, _MatCapWeight)
        || HasMatCap(_MatCapMode2, _MatCapWeight2);
}

inline bool HasTextureColorAdjustments(const half hue, const half satOffset, const half contrastOffset)
{
    return abs(hue) + abs(satOffset) + abs(contrastOffset) > 1e-6h;
}

half3 Specular(float3 lightDirection, half lightStrength, float3 normalWS, float3 viewDirection, float2 uv)
{
    if (lightStrength <= HALF_MIN)
        return 0;

    float3 halfVector = viewDirection + lightDirection;
    float invHalfLength = rsqrt(max(dot(halfVector, halfVector), 1e-4));
    float NdotH = saturate(dot(halfVector, normalWS) * invHalfLength);

    float smoothness = exp2(10 * _SpecularPower + 1);
    float modifier = PositivePow(NdotH, smoothness);
    modifier = LinearStep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, modifier);
    if (modifier <= HALF_MIN)
        return 0;

    float4 specularMap = SAMPLE_TEXTURE2D_LOD(_SpecularMap, sampler_MainTex, uv, 0);
    float specularMask = SelectMask(SAMPLE_TEXTURE2D_LOD(_SpecularMask, sampler_MainTex, uv, 0), _SpecularMaskCH);
    return _SpecularColor.rgb * specularMap.rgb * (specularMask * lightStrength * _SpecularColor.a * modifier);
}

half3 MatCap(TEXTURE2D_PARAM(tex, smp), TEXTURE2D(mask), const half4 color, const float2 matcapUV, const float2 uv, const uint maskChannel)
{
    half3 matcapMap = SAMPLE_TEXTURE2D_LOD(tex, smp, matcapUV, 0).rgb;
    float matcapMask = SelectMask(SAMPLE_TEXTURE2D_LOD(mask, smp, uv, 0), maskChannel);
    return matcapMask > 0 ? matcapMap * color.rgb * color.a : -1;
}

half3 Emission(float2 uv)
{
    half3 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap, sampler_MainTex, TRANSFORM_TEX(uv, _EmissionMap)).rgb;
    return _EmissionColor.rgb * emissionMap * _EmissionColor.a;
}

half3 MainLighting(Light mainLight, float3 positionWS, float3 normalWS, float3 viewDirection, float2 ssUV, float2 uv, half opacity, inout float charShadowAtten, inout float totalAttenuation, inout half3 midTone, half aoMap)
{
    half3 currLighting = 0;
    half3 lightColor = mainLight.color * mainLight.distanceAttenuation;
    half lightStrength = 0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b;
    const bool isBrightestLight = _UseBrightestLight == 0 || _IsBrightestLightMain > 0;
    const half3 shadeColor = lerp(_ShadeColor.rgb, 0, _UseDarknessMode);

    const bool needDefaultShadow = _ReceiveLightShadow > 0 && isBrightestLight;
    half mainLightShadowAtten = needDefaultShadow ? LinearStep(0, _StepSmoothness * 2, mainLight.shadowAttenuation) : 1;

    bool isMidToneArea = false;
    const half midToneAtten = aoMap * lightStrength * mainLightShadowAtten;

    if (_ToonType == FACE_TYPE)
    {
        if (!isBrightestLight)
            normalWS = _FaceForward.xyz;
    }

    if (_DisableCharShadow == 0 && isBrightestLight)
    {
        charShadowAtten = GetCharMainShadow(ssUV, positionWS, opacity);
        float smoothnessOffset = _CharShadowSmoothnessOffset * 0.3;
        float stepCharShadowAtten = LinearStep(0.3 - smoothnessOffset, 0.7 + smoothnessOffset, charShadowAtten);

        if (_UseMidTone > 0)
        {
            if (stepCharShadowAtten > 0)
            {
                float midToneStrength = 1.0 - LinearStep(0, _MidWidth, charShadowAtten);
                if (midToneStrength > 0)
                {
                    isMidToneArea = true;
                    midTone = _MidColor.rgb * midToneStrength * midToneAtten;
                }
            }
        }

        charShadowAtten = stepCharShadowAtten;
        totalAttenuation = min(totalAttenuation, 1.0 - charShadowAtten);
    }

    half3 lightDirection = isBrightestLight ? _BrightestLightDirection.xyz : mainLight.direction;

    float halfLambert, halfLambertStep;
    HalfLambert(lightDirection, normalWS, _BaseStep, _StepSmoothness, halfLambert, halfLambertStep);

    if (_UseMidTone > 0)
    {
        if (charShadowAtten < HALF_MIN)
        {
            isMidToneArea = true;
            float midToneSmoothness = _StepSmoothness * _MidWidth;
            midTone += GetMidTone(halfLambert, _BaseStep, midToneSmoothness) * midToneAtten;
            halfLambertStep = LinearStep(_BaseStep - midToneSmoothness, _BaseStep + midToneSmoothness, halfLambert);
        }

        if (needDefaultShadow)
        {
            float midToneSmoothness = _MidWidth * 0.5;
            midTone += GetMidTone(mainLightShadowAtten, 0.5, midToneSmoothness) * (aoMap * lightStrength);
            mainLightShadowAtten = LinearStep(0.5 - midToneSmoothness, 0.5 + midToneSmoothness, mainLightShadowAtten);
        }

        if (halfLambertStep < HALF_MIN)
            isMidToneArea = false;

        if (isMidToneArea == false)
            midTone = 0;
    }

    if (isBrightestLight)
        totalAttenuation = min(totalAttenuation, halfLambertStep);

    currLighting = lerp(shadeColor, _BaseColor.rgb, halfLambertStep);
    if (HasSpecular())
        currLighting += Specular(lightDirection, lightStrength, normalWS, viewDirection, uv);

    if (_DisableCharShadow == 0 && isBrightestLight)
        currLighting = lerp(currLighting, shadeColor, charShadowAtten);

    if (needDefaultShadow)
    {
        currLighting = lerp(shadeColor, currLighting, mainLightShadowAtten);
        totalAttenuation = min(totalAttenuation, mainLightShadowAtten);
    }

    return currLighting * lightColor;
}

half3 AdditionalLighting(Light light, float3 normalWS, float3 viewDirection, float2 ssUV, float2 uv, float3 positionWS, uint lightIndex, half opacity, inout float charShadowAtten, inout float totalAttenuation, inout half3 midTone)
{
    half3 currLighting = 0;
    half3 lightColor = light.color * light.distanceAttenuation;
    half lightStrength = 0.299 * lightColor.r + 0.587 * lightColor.g + 0.114 * lightColor.b;
    const bool isBrightestLight = _UseBrightestLight > 0 && _IsBrightestLightMain == 0 && _BrightestLightIndex == lightIndex;
    const half3 shadeColor = lerp(_ShadeColor.rgb, 0, _UseDarknessMode);

    if (_ToonType == FACE_TYPE)
    {
        if (!isBrightestLight)
            normalWS = _FaceForward.xyz;
    }

    float stepLocalCharShadowAtten = 0;
    if (_DisableCharShadow == 0 && isBrightestLight)
    {
        float localCharShadowAtten = GetCharAdditionalShadow(ssUV, positionWS, opacity, lightIndex);
        float smoothnessOffset = _CharShadowSmoothnessOffset * 0.3;
        stepLocalCharShadowAtten = LinearStep(0.3 - smoothnessOffset, 0.7 + smoothnessOffset, localCharShadowAtten);
        charShadowAtten = stepLocalCharShadowAtten;
        totalAttenuation = min(totalAttenuation, 1.0 - charShadowAtten);
    }

    float halfLambert, halfLambertStep;
    HalfLambert(light.direction, normalWS, _BaseStep, _StepSmoothness * 2, halfLambert, halfLambertStep);
    if (isBrightestLight)
        totalAttenuation = min(totalAttenuation, halfLambertStep);
    currLighting = lerp(shadeColor, _BaseColor.rgb, halfLambertStep);

    if (halfLambertStep < HALF_MIN || halfLambertStep > (1.0 - HALF_MIN))
        midTone *= 1.0 - saturate(lightStrength);

    if (HasSpecular())
        currLighting += Specular(light.direction, lightStrength, normalWS, viewDirection, uv);

    if (_DisableCharShadow == 0 && isBrightestLight)
        currLighting = lerp(currLighting, shadeColor, stepLocalCharShadowAtten);

    if (_ReceiveLightShadow > 0 && isBrightestLight)
    {
        half lightShadowAtten = LinearStep(0, _StepSmoothness * 2, light.shadowAttenuation);
        currLighting = lerp(shadeColor, currLighting, lightShadowAtten);
        totalAttenuation = min(totalAttenuation, lightShadowAtten);
    }

    return currLighting * lightColor;
}

#endif