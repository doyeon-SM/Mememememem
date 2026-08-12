Shader "GH/Skybox/Cubemap Starry Night Blend"
{
    Properties
    {
        [NoScaleOffset] _DayTex("Day Cubemap", Cube) = "grey" {}
        _Blend("Night Blend", Range(0, 1)) = 1
        _Tint("Tint", Color) = (1, 1, 1, 1)
        _Exposure("Exposure", Range(0, 8)) = 1
        _Rotation("Rotation", Range(0, 360)) = 0
        _DaySkyScale("Day Sky Scale", Range(0.4, 1.25)) = 1
        _DayVerticalOffset("Day Vertical Offset", Range(-25, 25)) = 0
        _DayLowerSkyColor("Day Lower Sky Color", Color) = (0.10, 0.305, 0.491, 1)
        _LowerSkyWorldFade("Lower Sky World Fade", Range(0.5, 15)) = 5
        _LowerSkySourceFade("Lower Sky Source Fade", Range(0.5, 15)) = 6

        _GradientSkyColor("Night Sky Color", Color) = (0.04, 0.10, 0.20, 1)
        _GradientHorizonColor("Night Horizon Color", Color) = (0.34, 0.19, 0.42, 1)
        _GradientFadeBegin("Horizon Fade Begin", Range(-1, 1)) = -0.25
        _GradientFadeEnd("Horizon Fade End", Range(-1, 1)) = 0.45

        _StarFadeBegin("Star Fade Begin", Range(-1, 1)) = 0
        _StarFadeEnd("Star Fade End", Range(-1, 1)) = 0.6
        _StarLayer1Color("Star Layer 1 Color", Color) = (0.85, 0.94, 1, 1)
        _StarLayer1Density("Star Layer 1 Density", Range(0, 0.05)) = 0.024
        _StarLayer1MaxRadius("Star Layer 1 Size", Range(0, 0.1)) = 0.007
        _StarLayer1TwinkleAmount("Star Layer 1 Twinkle", Range(0, 1)) = 0.65
        _StarLayer1TwinkleSpeed("Star Layer 1 Speed", Float) = 2
        _StarLayer1HDRBoost("Star Layer 1 Brightness", Range(0, 10)) = 1.3

        _StarLayer2Color("Star Layer 2 Color", Color) = (1, 0.55, 0.96, 1)
        _StarLayer2Density("Star Layer 2 Density", Range(0, 0.05)) = 0.019
        _StarLayer2MaxRadius("Star Layer 2 Size", Range(0, 0.1)) = 0.005
        _StarLayer2TwinkleAmount("Star Layer 2 Twinkle", Range(0, 1)) = 0.5
        _StarLayer2TwinkleSpeed("Star Layer 2 Speed", Float) = 4
        _StarLayer2HDRBoost("Star Layer 2 Brightness", Range(0, 10)) = 1.1

        _StarLayer3Color("Star Layer 3 Color", Color) = (1, 0.98, 0.72, 1)
        _StarLayer3Density("Star Layer 3 Density", Range(0, 0.05)) = 0.006
        _StarLayer3MaxRadius("Star Layer 3 Size", Range(0, 0.1)) = 0.014
        _StarLayer3TwinkleAmount("Star Layer 3 Twinkle", Range(0, 1)) = 0.35
        _StarLayer3TwinkleSpeed("Star Layer 3 Speed", Float) = 1.5
        _StarLayer3HDRBoost("Star Layer 3 Brightness", Range(0, 10)) = 1.1

        _MoonColor("Moon Color", Color) = (0.85, 0.86, 0.72, 1)
        _MoonHeight("Moon Height", Range(0, 1)) = 0.83
        _MoonAngle("Moon Angle", Range(0, 1)) = 0.33
        _MoonRadius("Moon Size", Range(0, 1)) = 0.076
        _MoonEdgeFade("Moon Edge Fade", Range(0.001, 1)) = 0.23
        _MoonHDRBoost("Moon Brightness", Range(0, 10)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            samplerCUBE _DayTex;
            half _Blend;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _DaySkyScale;
            float _DayVerticalOffset;
            half4 _DayLowerSkyColor;
            float _LowerSkyWorldFade;
            float _LowerSkySourceFade;

            half4 _GradientSkyColor;
            half4 _GradientHorizonColor;
            float _GradientFadeBegin;
            float _GradientFadeEnd;
            float _StarFadeBegin;
            float _StarFadeEnd;

            half4 _StarLayer1Color;
            float _StarLayer1Density;
            float _StarLayer1MaxRadius;
            float _StarLayer1TwinkleAmount;
            float _StarLayer1TwinkleSpeed;
            float _StarLayer1HDRBoost;

            half4 _StarLayer2Color;
            float _StarLayer2Density;
            float _StarLayer2MaxRadius;
            float _StarLayer2TwinkleAmount;
            float _StarLayer2TwinkleSpeed;
            float _StarLayer2HDRBoost;

            half4 _StarLayer3Color;
            float _StarLayer3Density;
            float _StarLayer3MaxRadius;
            float _StarLayer3TwinkleAmount;
            float _StarLayer3TwinkleSpeed;
            float _StarLayer3HDRBoost;

            half4 _MoonColor;
            float _MoonHeight;
            float _MoonAngle;
            float _MoonRadius;
            float _MoonEdgeFade;
            float _MoonHDRBoost;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float2 Rotate2D(float2 value, float radians)
            {
                float sineValue;
                float cosineValue;
                sincos(radians, sineValue, cosineValue);
                return float2(
                    value.x * cosineValue - value.y * sineValue,
                    value.x * sineValue + value.y * cosineValue);
            }

            float Hash21(float2 value)
            {
                value = frac(value * float2(123.34, 456.21));
                value += dot(value, value + 45.32);
                return frac(value.x * value.y);
            }

            float2 Hash22(float2 value)
            {
                float first = Hash21(value);
                return float2(first, Hash21(value + first + 19.19));
            }

            float2 DirectionToUv(float3 direction)
            {
                const float inverseTwoPi = 0.15915494309;
                const float inversePi = 0.31830988618;
                return float2(
                    atan2(direction.z, direction.x) * inverseTwoPi + 0.5,
                    asin(clamp(direction.y, -1.0, 1.0)) * inversePi + 0.5);
            }

            float StarMask(
                float2 uv,
                float gridWidth,
                float density,
                float radius,
                float twinkleAmount,
                float twinkleSpeed,
                float seed)
            {
                float2 grid = float2(gridWidth, gridWidth * 0.5);
                float2 scaledUv = uv * grid;
                float2 cell = floor(scaledUv);
                float2 localUv = frac(scaledUv);
                float randomValue = Hash21(cell + seed);
                // Material density is already a small 0..0.05 probability.
                // Multiplying it by 20 made almost half of all grid cells a
                // star and produced the giant checkerboard seen in Game View.
                float visible = step(1.0 - saturate(density * 1.25), randomValue);
                float2 starPosition = lerp(0.18, 0.82, Hash22(cell + seed * 3.71));
                float distanceToStar = length(localUv - starPosition);
                // Radius is expressed inside one grid cell. The previous
                // gridWidth multiplication expanded a star beyond its entire
                // cell, which turned circular points into large squares.
                float normalizedRadius = lerp(
                    0.055,
                    0.22,
                    saturate(radius / 0.05));
                float edgeWidth = max(fwidth(distanceToStar) * 0.65, 0.002);
                float star = 1.0 - smoothstep(
                    normalizedRadius - edgeWidth,
                    normalizedRadius + edgeWidth,
                    distanceToStar);
                float twinkle = 1.0 - twinkleAmount
                    + twinkleAmount * (0.55 + 0.45 * sin(_Time.y * twinkleSpeed + randomValue * 31.4));
                return star * visible * twinkle;
            }

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.direction = input.vertex.xyz;
                return output;
            }

            half4 frag(v2f input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                direction.xz = Rotate2D(direction.xz, radians(_Rotation));

                float3 dayDirection = direction;
                dayDirection.xz *= max(_DaySkyScale, 0.001);
                dayDirection.y += _DayVerticalOffset * 0.01;
                dayDirection = normalize(dayDirection);

                // Panoramic sky textures often contain water-reflected clouds
                // in their lower half. Never show those pixels below the world
                // horizon, and also guard against framing that moves the source
                // texture's lower hemisphere upward.
                float worldFadeHeight = sin(radians(clamp(
                    _LowerSkyWorldFade,
                    0.5,
                    15.0)));
                float sourceFadeHeight = sin(radians(clamp(
                    _LowerSkySourceFade,
                    0.5,
                    15.0)));
                float worldUpperWeight = smoothstep(
                    0.0,
                    worldFadeHeight,
                    direction.y);
                float sourceUpperWeight = smoothstep(
                    0.0,
                    sourceFadeHeight,
                    dayDirection.y);
                float dayTextureWeight = min(
                    worldUpperWeight,
                    sourceUpperWeight);
                float3 safeDayDirection = dayDirection;
                safeDayDirection.y = max(
                    safeDayDirection.y,
                    sourceFadeHeight);
                safeDayDirection = normalize(safeDayDirection);
                half3 sampledDayColor = texCUBE(
                    _DayTex,
                    safeDayDirection).rgb;
                half3 dayColor = lerp(
                    _DayLowerSkyColor.rgb,
                    sampledDayColor,
                    dayTextureWeight) * _Tint.rgb * _Exposure;

                float gradient = smoothstep(
                    _GradientFadeBegin,
                    max(_GradientFadeEnd, _GradientFadeBegin + 0.001),
                    direction.y);
                half3 nightColor = lerp(_GradientHorizonColor.rgb, _GradientSkyColor.rgb, gradient);

                float2 skyUv = DirectionToUv(direction);
                float horizonFade = smoothstep(
                    _StarFadeBegin,
                    max(_StarFadeEnd, _StarFadeBegin + 0.001),
                    direction.y);

                float star1 = StarMask(skyUv, 210.0, _StarLayer1Density, _StarLayer1MaxRadius,
                    _StarLayer1TwinkleAmount, _StarLayer1TwinkleSpeed, 7.0);
                float star2 = StarMask(skyUv + 0.137, 295.0, _StarLayer2Density, _StarLayer2MaxRadius,
                    _StarLayer2TwinkleAmount, _StarLayer2TwinkleSpeed, 23.0);
                float star3 = StarMask(skyUv + 0.371, 95.0, _StarLayer3Density, _StarLayer3MaxRadius,
                    _StarLayer3TwinkleAmount, _StarLayer3TwinkleSpeed, 51.0);

                nightColor += horizonFade * (
                    _StarLayer1Color.rgb * star1 * _StarLayer1HDRBoost
                    + _StarLayer2Color.rgb * star2 * _StarLayer2HDRBoost
                    + _StarLayer3Color.rgb * star3 * _StarLayer3HDRBoost);

                float moonY = lerp(-0.15, 0.92, _MoonHeight);
                float moonHorizontalLength = sqrt(saturate(1.0 - moonY * moonY));
                float moonRadians = _MoonAngle * UNITY_TWO_PI;
                float3 moonDirection = normalize(float3(
                    cos(moonRadians) * moonHorizontalLength,
                    moonY,
                    sin(moonRadians) * moonHorizontalLength));
                float moonAngle = acos(clamp(dot(direction, moonDirection), -1.0, 1.0));
                float moonAngularRadius = max(_MoonRadius * 0.42, 0.001);
                float moonSoftness = max(moonAngularRadius * _MoonEdgeFade, 0.0005);
                float moonMask = 1.0 - smoothstep(
                    moonAngularRadius - moonSoftness,
                    moonAngularRadius,
                    moonAngle);
                nightColor += _MoonColor.rgb * moonMask * _MoonHDRBoost;

                return half4(lerp(dayColor, nightColor * _Exposure, saturate(_Blend)), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
