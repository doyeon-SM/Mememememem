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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.direction = RotateAroundY(input.positionOS.xyz, _Rotation);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 dayColor = texCUBE(_DayTex, input.direction).rgb;
                half3 nightColor = texCUBE(_NightTex, input.direction).rgb;
                half3 blendedColor = lerp(dayColor, nightColor, saturate(_Blend));
                return half4(blendedColor * _Tint.rgb * _Exposure, 1.0h);
            }
            ENDCG
        }
    }

    Fallback Off
}
