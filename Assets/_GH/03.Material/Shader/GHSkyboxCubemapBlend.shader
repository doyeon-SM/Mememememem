Shader "GH/Skybox/Cubemap Blend"
{
    Properties
    {
        [NoScaleOffset] _DayTex ("Day Cubemap", Cube) = "grey" {}
        [NoScaleOffset] _NightTex ("Night Cubemap", Cube) = "black" {}
        _Blend ("Night Blend", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
        _DaySkyScale ("Day Sky Scale", Range(0.4, 1.25)) = 0.5
        _NightSkyScale ("Night Sky Scale", Range(0.4, 1.25)) = 0.48
        _DayVerticalOffset ("Day Vertical Offset", Range(-25, 25)) = 20
        _NightVerticalOffset ("Night Vertical Offset", Range(-25, 25)) = 24
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            samplerCUBE _DayTex;
            samplerCUBE _NightTex;
            half _Blend;
            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _DaySkyScale;
            float _NightSkyScale;
            float _DayVerticalOffset;
            float _NightVerticalOffset;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float3 RotateAroundY(float3 direction, float degrees)
            {
                float radians = degrees * UNITY_PI / 180.0;
                float sine;
                float cosine;
                sincos(radians, sine, cosine);

                return float3(
                    cosine * direction.x - sine * direction.z,
                    direction.y,
                    sine * direction.x + cosine * direction.z);
            }

            float3 RemapSkyDirection(
                float3 direction,
                float visualScale,
                float verticalOffsetDegrees)
            {
                direction = normalize(direction);

                float horizontalLength = max(length(direction.xz), 0.0001);
                float2 horizontalDirection = direction.xz / horizontalLength;
                float pitch = atan2(direction.y, horizontalLength);

                // Smaller visualScale makes baked features appear smaller.
                // A positive vertical offset moves the sky image upward.
                float samplePitch = pitch / max(visualScale, 0.01)
                    - verticalOffsetDegrees * UNITY_PI / 180.0;
                samplePitch = clamp(
                    samplePitch,
                    -UNITY_PI * 0.499,
                    UNITY_PI * 0.499);

                float pitchCosine = cos(samplePitch);
                return float3(
                    horizontalDirection.x * pitchCosine,
                    sin(samplePitch),
                    horizontalDirection.y * pitchCosine);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.direction = RotateAroundY(input.positionOS.xyz, _Rotation);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dayDirection = RemapSkyDirection(
                    input.direction,
                    _DaySkyScale,
                    _DayVerticalOffset);
                float3 nightDirection = RemapSkyDirection(
                    input.direction,
                    _NightSkyScale,
                    _NightVerticalOffset);
                half3 dayColor = texCUBE(_DayTex, dayDirection).rgb;
                half3 nightColor = texCUBE(_NightTex, nightDirection).rgb;
                half3 blendedColor = lerp(dayColor, nightColor, saturate(_Blend));
                return half4(blendedColor * _Tint.rgb * _Exposure, 1.0h);
            }
            ENDCG
        }
    }

    Fallback Off
}
