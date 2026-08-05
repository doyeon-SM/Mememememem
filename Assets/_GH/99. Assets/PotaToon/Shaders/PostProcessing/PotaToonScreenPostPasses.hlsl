#ifndef POTA_TOON_SCREEN_POST_PASSES_INCLUDED
#define POTA_TOON_SCREEN_POST_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

half4 FragPotaToonScreenOutline(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if UNITY_VERSION >= 202230
    float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
#else
    float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.uv));
#endif

#if _EXCLUDE_INNER_SCREEN_OUTLINES
    bool isCharArea = SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, uv).r > 0.5;
    if (isCharArea)
        clip(-1);
#endif
    
    const half3 lumWeight = half3(0.299, 0.587, 0.114);
    const float2 sobelOffsets[9] = {
        float2(-1, -1), float2(0, -1), float2(1, -1),
        float2(-1,  0), float2(0,  0), float2(1,  0),
        float2(-1,  1), float2(0,  1), float2(1,  1)
    };
    const half sobelX[9] = { -1,  0,  1, -2, 0, 2, -1, 0, 1 };
    const half sobelY[9] = { -1, -2, -1,  0, 0, 0,  1, 2, 1 };

    // Sobel Filter
    half gx = 0;
    half gxN = 0;
    half gy = 0;
    half gyN = 0;

    UNITY_UNROLL
    for (int i = 0; i < 9; i++)
    {
        float2 offsetUV = uv + sobelOffsets[i] * _ScreenSize.zw * _ScreenOutlineThickness;
        half charArea = SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, offsetUV).r;
        float3 sceneColor = SampleSceneColor(offsetUV);
        float3 sceneNormal = SampleSceneNormals(offsetUV);
        
        // Color
        half lum = dot(sceneColor, lumWeight) * charArea;
        gx += sobelX[i] * lum;
        gy += sobelY[i] * lum;

        // Normal
        lum = dot(sceneNormal, lumWeight) * charArea;
        gxN += sobelX[i] * lum;
        gyN += sobelY[i] * lum;
    }
    
    half edge = sqrt(gx * gx + gy * gy);
    edge += sqrt(gxN * gxN + gyN * gyN);

    edge *= _ScreenOutlineEdgeStrength;
    edge = saturate(edge);

    return half4(_ScreenOutlineColor.rgb * edge, edge);
}

#endif