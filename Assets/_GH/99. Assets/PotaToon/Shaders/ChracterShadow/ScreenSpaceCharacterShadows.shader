Shader "PotaToon/Hidden/ScreenSpaceCharacterShadows"
{
    SubShader
    {
        Tags{ "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "./DeclareCharacterShadowTexture.hlsl"

        TEXTURE2D_X(_PotaToonCharMask);

        ENDHLSL

        Pass
        {
            Name "ScreenSpaceCharacterShadows"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma target      4.5
            #pragma vertex      Vert
            #pragma fragment    Fragment

            half2 EvaluateCharShadow(float2 sampleValue, float z)
            {
    #if UNITY_REVERSED_Z
                z += _CharShadowBias.x;
                return half2(sampleValue.x > z ? 1 : 0, sampleValue.y > z ? 1 : 0);
    #else
                z -= _CharShadowBias.x;
                return half2(sampleValue.x < z ? 1 : 0, sampleValue.y < z ? 1 : 0);
    #endif
            }

            half2 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Skip if not char area
                if (SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, input.texcoord.xy).r < HALF_MIN)
                    return 0;

    #if UNITY_REVERSED_Z
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
    #else
                float deviceDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, input.texcoord.xy).r;
                deviceDepth = deviceDepth * 2.0 - 1.0;
    #endif

                float3 wpos = ComputeWorldSpacePosition(input.texcoord.xy, deviceDepth, unity_MatrixInvVP);
                if (IfCharShadowCulled(TransformWorldToView(wpos).z))
                    return 0;
                
                // Create SS Shadow texture
                float3 coord = TransformWorldToCharShadowCoord(wpos);
                if (coord.x < 0 || coord.x > 1 || coord.y < 0 || coord.y > 1)
                    return 0;

                const float o = _CharShadowmapSize.x * 0.67;

                half2 center = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy, 0).rg, coord.z);

                half2 a1 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(+2 * o, -2 * o), 0).rg, coord.z);
                half2 a2 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(+1 * o, +1 * o), 0).rg, coord.z);
                half2 a4 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(-1 * o, -1 * o), 0).rg, coord.z);
                half2 a5 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(-2 * o, +2 * o), 0).rg, coord.z);
                half2 shadow = a1 * 0.192 + a2 * 0.195 + center * 0.226 + a4 * 0.195 + a5 * 0.192;

                a1 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(+2 * o, +2 * o), 0).rg, coord.z);
                a2 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(+1 * o, -1 * o), 0).rg, coord.z);
                a4 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(-1 * o, +1 * o), 0).rg, coord.z);
                a5 = EvaluateCharShadow(SAMPLE_TEXTURE2D_LOD(_CharShadowMap, sampler_LinearClamp, coord.xy + float2(-2 * o, -2 * o), 0).rg, coord.z);
                shadow *= a1 * 0.192 + a2 * 0.195 + center * 0.226 + a4 * 0.195 + a5 * 0.192;
                shadow = saturate(shadow);

                return shadow;
            }
            
            ENDHLSL
        }

        Pass
        {
            Name "Contact Shadow"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma target      4.5
            #pragma vertex      Vert
            #pragma fragment    FragmentContactShadow

            #define SAMPLE_COUNT 3
            #define RAY_LENGTH 0.1
            #define CS_BIAS 0.000001

            half ComputeContactShadow(Varyings input)
            {
                const float rcp = 1.0 / SAMPLE_COUNT;
                
                float sceneZ = SampleSceneDepth(input.texcoord.xy);

            #if UNITY_REVERSED_Z
                float deviceDepth = sceneZ;
            #else
                float deviceDepth = sceneZ * 2.0 - 1.0;
            #endif

                float3 rayStartWS = ComputeWorldSpacePosition(input.texcoord.xy, deviceDepth, unity_MatrixInvVP);
                float3 rayEndWS = rayStartWS + _BrightestLightDirection.xyz * RAY_LENGTH;

                float4 rayStartCS = TransformWorldToHClip(rayStartWS);
                float4 rayEndCS = TransformWorldToHClip(rayEndWS);

                rayStartCS.xyz = rayStartCS.xyz / rayStartCS.w;
                rayEndCS.xyz = rayEndCS.xyz / rayEndCS.w;

                // Pixel to light ray in clip space.
                float2 rayDirCS = (rayEndCS.xy - rayStartCS.xy) * 0.5;
                float2 startUV = rayStartCS.xy * 0.5 + 0.5;
                float2 rayDir = rayDirCS.xy;
            #if UNITY_UV_STARTS_AT_TOP
                startUV.y = 1.0 - startUV.y;
                rayDir.y = -rayDir.y;
            #endif

                UNITY_UNROLL
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float t = (i + 1) * rcp;
                    float2 sampleAlongRay = startUV + t * rayDir;
                    if (any(sampleAlongRay < 0) || any(sampleAlongRay > 1))
                        return 0;
                    
                    // Depth buffer depth for this sample
                    float sampleDepth = SampleSceneDepth(saturate(sampleAlongRay));

                #if UNITY_REVERSED_Z
                    if (sampleDepth - sceneZ > CS_BIAS)
                #else
                    if (sceneZ - sampleDepth > CS_BIAS)
                #endif
                    {
                        return 1;
                    }
                }
                return 0;
            }

            half FragmentContactShadow(Varyings input) : SV_Target
            {
                // Skip if not char area
                if (SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, input.texcoord.xy).r < HALF_MIN)
                   return 0;
                
                return ComputeContactShadow(input);
            }
            
            ENDHLSL
        }
    }
}