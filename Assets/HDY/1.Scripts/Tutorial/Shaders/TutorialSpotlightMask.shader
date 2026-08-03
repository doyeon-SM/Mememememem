Shader "HDY/Tutorial/SpotlightMask"
{
    // 화면 전체를 덮는 UI Image에 이 셰이더를 쓰는 Material을 연결하면,
    // _Center를 기준으로 반경 _Radius 안쪽은 완전히 투명(원래 화면이 보임),
    // 바깥쪽은 _Color(반투명 어두운 색)로 덮이는 원형 스포트라이트 효과를 낸다.
    // _Center/_Radius/_Softness는 TutorialHighlightUI.cs가 매 프레임 갱신한다.
    Properties
    {
        _Color ("Dim Color", Color) = (0,0,0,0.65)
        _Center ("Center (viewport 0-1)", Vector) = (0.5,0.5,0,0)
        _Radius ("Radius (viewport, y-normalized)", Float) = 0.15
        _Softness ("Edge Softness", Range(0.001,0.5)) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _Softness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Image가 화면 전체를 덮으면 uv는 곧 뷰포트 좌표(0~1)와 같다.
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 화면 가로세로 비율 보정 - 그냥 uv 거리로만 비교하면 원이 아니라 타원으로 보인다.
                float aspect = _ScreenParams.x / _ScreenParams.y;

                float2 diff = i.uv - _Center.xy;
                diff.x *= aspect;
                float dist = length(diff);

                // 반경 안쪽 = 0(완전 투명, 원래 화면 그대로), 바깥쪽 = _Color 알파(어둡게)
                float alpha = smoothstep(_Radius, _Radius + _Softness, dist);
                return fixed4(_Color.rgb, _Color.a * alpha);
            }
            ENDCG
        }
    }
}
