#ifndef POTA_TOON_TONE_MAPPING_INCLUDED
#define POTA_TOON_TONE_MAPPING_INCLUDED

// Custom tonemapping settings
float4 _CustomToneCurve;
float4 _ToeSegmentA;
float4 _ToeSegmentB;
float4 _MidSegmentA;
float4 _MidSegmentB;
float4 _ShoSegmentA;
float4 _ShoSegmentB;

TEXTURE3D(_TonyMcMapfaceLut);

#if _POTA_TOON_TONEMAP_FILMIC
// Filmic Tonemapping Operators http://filmicworlds.com/blog/filmic-tonemapping-operators/
// Source:  https://github.com/dmnsgn/glsl-tone-map/blob/main/filmic.glsl
float3 Filmic(float3 x) {
    float3 X = max(0.0, x - 0.004);
    float3 result = (X * (6.2 * X + 0.5)) / (X * (6.2 * X + 1.7) + 0.06);
    return pow(result, 2.2);
}
#endif

#if _POTA_TOON_TONEMAP_UCHIMURA
// Uchimura 2017, "HDR theory and practice"
// Math:    https://www.desmos.com/calculator/gslcdxvipg
// Source:  https://www.slideshare.net/nikuque/hdr-theory-and-practicce-jp
//          https://github.com/dmnsgn/glsl-tone-map/blob/main/uchimura.glsl
float3 Uchimura(float3 x, float P, float a, float m, float l, float c, float b) {
    float l0 = ((P - m) * l) / a;
    float L0 = m - m / a;
    float L1 = m + (1.0 - m) / a;
    float S0 = m + l0;
    float S1 = m + a * l0;
    float C2 = (a * P) / (P - S1);
    float CP = -C2 / P;

    float3 w0 = float3(1.0 - smoothstep(0.0, m, x));
    float3 w2 = float3(step(m + l0, x));
    float3 w1 = float3(1.0 - w0 - w2);

    float3 T = float3(m * PositivePow(x / m, c) + b);
    float3 S = float3(P - (P - S1) * exp(CP * (x - S0)));
    float3 L = float3(m + a * (x - m));

    return T * w0 + L * w1 + S * w2;
}

float3 Uchimura(float3 x) {
    const float P = 1.0;  // max display brightness
    const float a = 1.0;  // contrast
    const float m = 0.22; // linear section start
    const float l = 0.4;  // linear section length
    const float c = 1.33; // black
    const float b = 0.0;  // pedestal

    return Uchimura(x, P, a, m, l, c, b);
}
#endif

#if _POTA_TOON_TONEMAP_TONY
// Source:  https://github.com/h3r2tic/tony-mc-mapface
SAMPLER(sampler_linear_clamp);

float3 TonyMcMapface(float3 stimulus) {
    // Apply a non-linear transform that the LUT is encoded with.
    const float3 encoded = stimulus / (stimulus + 1.0);

    // Align the encoded range to texel centers.
    const float LUT_DIMS = 48.0;
    float3 uv = encoded * ((LUT_DIMS - 1.0) / LUT_DIMS) + 0.5 / LUT_DIMS;

    // #if UNITY_UV_STARTS_AT_TOP
    //     uv.y = 1.0 - uv.y;
    // #endif

    return SAMPLE_TEXTURE3D_LOD(_TonyMcMapfaceLut, sampler_linear_clamp, uv, 0).rgb;
}
#endif

float3 ApplyPotaToonToneMap(float3 input)
{
    float3 output = input;
    
#if _POTA_TOON_TONEMAP_NEUTRAL
    output = NeutralTonemap(input);
#elif _POTA_TOON_TONEMAP_ACES
    float3 aces = unity_to_ACES(input);
    output = AcesTonemap(aces);
#elif _POTA_TOON_TONEMAP_FILMIC
    output = Filmic(input);
#elif _POTA_TOON_TONEMAP_UCHIMURA
    output = Uchimura(input);
#elif _POTA_TOON_TONEMAP_TONY
    output= TonyMcMapface(input);
#elif _POTA_TOON_TONEMAP_CUSTOM
    output = CustomTonemap(input, _CustomToneCurve.xyz, _ToeSegmentA, _ToeSegmentB.xy, _MidSegmentA, _MidSegmentB.xy, _ShoSegmentA, _ShoSegmentB.xy);
#endif

    return saturate(output);
}

#endif