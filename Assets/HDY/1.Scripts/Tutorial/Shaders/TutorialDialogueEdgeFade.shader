Shader "HDY/Tutorial/DialogueEdgeFade"
{
    // [HDY 요청] 대화창 배경 이미지에 이 셰이더를 쓰는 Material을 연결하면, 가로(UV.x) 기준 좌우
    // 끝에서 _FadeWidth만큼이 부드럽게 투명해진다(가운데는 원래 알파 그대로, 좌우 끝은 알파 0).
    // 그 외에는 Unity 기본 UI/Default 셰이더와 동일하게 동작하도록 만들었다 - Stencil/Mask,
    // RectMask2D(_ClipRect), Image.color 틴트, CanvasGroup 알파(정점 컬러로 전달됨)를 전부
    // 지원한다. 그래서 배경 Image 하나의 Material만 이걸로 바꿔주면, 그 위에 올라가는 자식
    // 텍스트 등은 평소처럼 아무 영향 없이 그대로 동작한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _FadeWidth ("가장자리 흐림 폭 (UV 0~0.5, 클수록 넓게 흐려짐)", Range(0, 0.5)) = 0.15
        _FadePower ("흐림 곡선 (1=선형, 클수록 가운데는 진하고 끝에서 급격히 사라짐)", Range(0.1, 5)) = 1

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            sampler2D _MainTex;

            float _FadeWidth;
            float _FadePower;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // [HDY 요청 - 좌우 흐림] UV.x가 왼쪽 끝(0)~_FadeWidth 구간, 또는 오른쪽 끝(1)~(1-_FadeWidth)
                // 구간에서 알파를 0까지 부드럽게 줄인다. 그 사이(가운데) 구간은 원래 알파를 그대로 쓴다.
                float leftFade = saturate(IN.texcoord.x / max(_FadeWidth, 0.0001));
                float rightFade = saturate((1 - IN.texcoord.x) / max(_FadeWidth, 0.0001));
                float edgeFade = pow(min(leftFade, rightFade), max(_FadePower, 0.0001));

                color.a *= edgeFade;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
