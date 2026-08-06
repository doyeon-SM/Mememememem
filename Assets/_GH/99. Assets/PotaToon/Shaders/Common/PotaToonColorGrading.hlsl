#ifndef POTA_TOON_COLOR_GRADING_INCLUDED
#define POTA_TOON_COLOR_GRADING_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

half3 PotaToonApplyHSV(half3 rgb, half hue, half satOffset, half contrastOffset)
{
    if (abs(hue) + abs(satOffset) + abs(contrastOffset) < 1e-6)
        return rgb;

    float3 color = max(0.0, (float3)rgb);
    // Match URP _HueSatCon semantics while keeping UI range [-1, 1].
    float hueShift = (float)hue * 0.5;
    float satMult = max(0.0, 1.0 + (float)satOffset);
    float contrastMult = max(0.0, 1.0 + (float)contrastOffset);

    float3 hsv = RgbToHsv(color);
    hsv.x = RotateHue(hsv.x + hueShift, 0.0, 1.0);
    color = HsvToRgb(hsv);

    float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
    color = luma.xxx + satMult * (color - luma.xxx);

    // Contrast in LogC space, similar to URP post color adjustments.
    float3 colorLog = LinearToLogC(max(0.0, color));
    colorLog = (colorLog - 0.4135884) * contrastMult + 0.4135884;
    color = LogCToLinear(colorLog);

    return (half3)max(0.0, color);
}

half3 PotaToonApplyTextureHSV(half3 rgb, half hue, half satOffset, half contrastOffset, half maskValue)
{
    if (maskValue <= 0)
        return rgb;

    return lerp(rgb, PotaToonApplyHSV(rgb, hue, satOffset, contrastOffset), maskValue);
}

#endif
