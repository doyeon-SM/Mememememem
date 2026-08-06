#ifndef POTA_TOON_GLITTER_INCLUDED
#define POTA_TOON_GLITTER_INCLUDED

// Glitter - Source: lilToon
#if _USE_GLITTER
void HashRGB4(float2 pos, out float3 noise0, out float3 noise1, out float3 noise2, out float3 noise3)
{
    // Hash
    // https://www.shadertoy.com/view/MdcfDj
    #define M1 1597334677U
    #define M2 3812015801U
    #define M3 2912667907U
    uint2 q = (uint2)pos;
    uint4 q2 = uint4(q.x, q.y, q.x+1, q.y+1) * uint4(M1, M2, M1, M2);
    uint3 n0 = (q2.x ^ q2.y) * uint3(M1, M2, M3);
    uint3 n1 = (q2.z ^ q2.y) * uint3(M1, M2, M3);
    uint3 n2 = (q2.x ^ q2.w) * uint3(M1, M2, M3);
    uint3 n3 = (q2.z ^ q2.w) * uint3(M1, M2, M3);
    noise0 = float3(n0) * (1.0/float(0xffffffffU));
    noise1 = float3(n1) * (1.0/float(0xffffffffU));
    noise2 = float3(n2) * (1.0/float(0xffffffffU));
    noise3 = float3(n3) * (1.0/float(0xffffffffU));
    #undef M1
    #undef M2
    #undef M3
}

float NsqDistance(float2 a, float2 b)
{
    return dot(a-b,a-b);
}

float4 Voronoi(float2 pos, out float2 nearoffset, float scaleRandomize)
{
    #if defined(SHADER_API_D3D9) || defined(SHADER_API_D3D11_9X)
        #define M1 46203.4357
        #define M2 21091.5327
        #define M3 35771.1966
        float2 q = trunc(pos);
        float4 q2 = float4(q.x, q.y, q.x+1, q.y+1);
        float3 noise0 = frac(sin(dot(q2.xy,float2(12.9898,78.233))) * float3(M1, M2, M3));
        float3 noise1 = frac(sin(dot(q2.zy,float2(12.9898,78.233))) * float3(M1, M2, M3));
        float3 noise2 = frac(sin(dot(q2.xw,float2(12.9898,78.233))) * float3(M1, M2, M3));
        float3 noise3 = frac(sin(dot(q2.zw,float2(12.9898,78.233))) * float3(M1, M2, M3));
        #undef M1
        #undef M2
        #undef M3
    #else
        float3 noise0, noise1, noise2, noise3;
        HashRGB4(pos, noise0, noise1, noise2, noise3);
    #endif

    // Get the nearest position
    float4 fracpos = frac(pos).xyxy + float4(0.5,0.5,-0.5,-0.5);
    float4 dist4 = float4(NsqDistance(fracpos.xy,noise0.xy), NsqDistance(fracpos.zy,noise1.xy), NsqDistance(fracpos.xw,noise2.xy), NsqDistance(fracpos.zw,noise3.xy));
    dist4 = lerp(dist4, dist4 / max(float4(noise0.z, noise1.z, noise2.z, noise3.z), 0.001), scaleRandomize);

    float3 nearoffset0 = dist4.x < dist4.y ? float3(0,0,dist4.x) : float3(1,0,dist4.y);
    float3 nearoffset1 = dist4.z < dist4.w ? float3(0,1,dist4.z) : float3(1,1,dist4.w);
    nearoffset = nearoffset0.z < nearoffset1.z ? nearoffset0.xy : nearoffset1.xy;

    float4 near0 = dist4.x < dist4.y ? float4(noise0,dist4.x) : float4(noise1,dist4.y);
    float4 near1 = dist4.z < dist4.w ? float4(noise2,dist4.z) : float4(noise3,dist4.w);
    return near0.w < near1.w ? near0 : near1;
}

float3 CalcGlitter(float2 uv, float3 normalDirection, float3 viewDirection, float3 cameraDirection, float3 lightDirection, float4 glitterParams1, float4 glitterParams2, float glitterPostContrast, float glitterSensitivity, float glitterScaleRandomize, uint glitterAngleRandomize)
{
    // glitterParams1
    // x: Scale, y: Scale, z: Size, w: Contrast
    // glitterParams2
    // x: Speed, y: Angle, z: Light Direction, w:

    #define GLITTER_MIPMAP 1
    #define GLITTER_ANTIALIAS 1

    #if GLITTER_MIPMAP == 1
        float2 pos = uv * glitterParams1.xy;
        float2 dd = fwidth(pos);
        float factor = frac(sin(dot(floor(pos/floor(dd + 3.0)),float2(12.9898,78.233))) * 46203.4357) + 0.5;
        float2 factor2 = floor(dd + factor * 0.5);
        pos = pos/max(1.0,factor2) + glitterParams1.xy * factor2;
    #else
        float2 pos = uv * glitterParams1.xy + glitterParams1.xy;
    #endif
    float2 nearoffset;
    float4 near = Voronoi(pos, nearoffset, glitterScaleRandomize);
    

    // Glitter
    float3 glitterNormal = abs(frac(near.xyz*14.274 + _Time.x * glitterParams2.x) * 2.0 - 1.0);
    glitterNormal = normalize(glitterNormal * 2.0 - 1.0);
    float glitter = dot(glitterNormal, cameraDirection);
    glitter = abs(frac(glitter * glitterSensitivity + glitterSensitivity) - 0.5) * 4.0 - 1.0;
    glitter = saturate(1.0 - (glitter * glitterParams1.w + glitterParams1.w));
    glitter = pow(glitter, glitterPostContrast);
    // Circle
    #if GLITTER_ANTIALIAS == 1
        glitter *= saturate((glitterParams1.z-near.w) / fwidth(near.w));
    #else
        glitter = near.w < glitterParams1.z ? glitter : 0.0;
    #endif
    // Angle
    float3 halfDirection = normalize(viewDirection + lightDirection * glitterParams2.z);
    float nh = saturate(dot(normalDirection, halfDirection));
    glitter = saturate(glitter * saturate(nh * glitterParams2.y + 1.0 - glitterParams2.y));
    // Random Color
    float3 glitterColor = glitter - glitter * frac(near.xyz*278.436) * glitterParams2.w;
    return glitterColor;
}

float3 Glitter(inout float3 color, half alpha, float3 viewDirection, float3 normalWS, float3 normalDirection, float2 uv, float3 albedo, half shadowAtten, float3 lightDirection, float3 lightColor)
{
    float3 glitter = 0;
    float4 glitterParams1 = float4(256, 256, _GlitterParticleSize, _GlitterContrast);
    float4 glitterParams2 = float4(_GlitterBlinkSpeed, _GlitterAngleLimit, _GlitterLightDirection, _GlitterColorRandomness);
    float3 glitterCameraDirection = -GetViewForwardDir();

    // Normal
    float3 N = lerp(normalWS, normalDirection, _GlitterNormalStrength);

    // Color
    float4 glitterColor = _GlitterColor;
    float2 glitterPos = uv;
    float2 uvGlitterColor = uv; //fd.uv0;
    glitterColor *= SAMPLE_TEXTURE2D_LOD(_GlitterColorTex, sampler_MainTex, uvGlitterColor, 0);
    glitterColor.rgb *= CalcGlitter(glitterPos, N, viewDirection, glitterCameraDirection, lightDirection, glitterParams1, glitterParams2, _GlitterPostContrast, _GlitterSensitivity, _GlitterScaleRandomize, 0);
    glitterColor.rgb = lerp(glitterColor.rgb, glitterColor.rgb * albedo, _GlitterMainStrength);
#if _ALPHATEST_ON
    if (_GlitterApplyTransparency > 0)
        glitterColor.a *= alpha;
#endif

    // Blend
    glitterColor.a = lerp(glitterColor.a, glitterColor.a * shadowAtten, _GlitterShadowMask);
    glitterColor.rgb = lerp(glitterColor.rgb, glitterColor.rgb * lightColor, _GlitterEnableLighting);
    glitter = glitterColor.rgb * glitterColor.a;
    color.rgb += glitter;
    return glitter;
}
#endif

#endif