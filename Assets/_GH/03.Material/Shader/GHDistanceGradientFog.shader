Shader "Hidden/GH/Distance Gradient Fog"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Distance Gradient Fog"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            SAMPLER(sampler_BlitTexture);

            float _GHDistanceFogEnabled;
            half4 _GHFogNearColor;
            half4 _GHFogMidColor;
            half4 _GHFogFarColor;
            float _GHFogStartDistance;
            float _GHFogEndDistance;
            float _GHFogMidPoint;
            float _GHFogMaxOpacity;
            float _GHFogDistancePower;
            float _GHFogHorizonWidth;
            float _GHFogHorizonOffset;
            float _GHFogHorizonIntensity;

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 sourceColor = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_BlitTexture,
                    uv);

                if (_GHDistanceFogEnabled < 0.5)
                {
                    return sourceColor;
                }

                float rawDepth = SampleSceneDepth(uv);

                float farDeviceDepth;
                #if UNITY_REVERSED_Z
                    farDeviceDepth = 0.0;
                #else
                    farDeviceDepth = 1.0;
                #endif

                float3 farWorldPosition = ComputeWorldSpacePosition(
                    uv,
                    farDeviceDepth,
                    UNITY_MATRIX_I_VP);
                float3 viewDirection = normalize(
                    farWorldPosition - _WorldSpaceCameraPos);
                float horizonCenter = sin(radians(_GHFogHorizonOffset));
                float horizonWidth = max(
                    0.0001,
                    sin(radians(max(0.01, _GHFogHorizonWidth))));
                float horizonDistance = abs(viewDirection.y - horizonCenter);
                float horizonShape = 1.0 - smoothstep(
                    horizonWidth * 0.18,
                    horizonWidth,
                    horizonDistance);
                float horizonOpacity = horizonShape
                    * saturate(_GHFogHorizonIntensity);

                // Sky pixels receive only the narrow horizon haze. This keeps
                // the upper sky clear while blending the sea/sky boundary.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001)
                    {
                        return half4(
                            lerp(
                                sourceColor.rgb,
                                _GHFogFarColor.rgb,
                                horizonOpacity),
                            sourceColor.a);
                    }
                #else
                    if (rawDepth >= 0.99999)
                    {
                        return half4(
                            lerp(
                                sourceColor.rgb,
                                _GHFogFarColor.rgb,
                                horizonOpacity),
                            sourceColor.a);
                    }
                #endif

                // World-space distance keeps the fog boundary spherical around
                // the camera. LinearEyeDepth alone measures only view-space Z,
                // which made geometry near the screen edges receive less fog.
                float3 sceneWorldPosition = ComputeWorldSpacePosition(
                    uv,
                    rawDepth,
                    UNITY_MATRIX_I_VP);
                float sceneDistance = distance(
                    sceneWorldPosition,
                    _WorldSpaceCameraPos);
                float fogRange = max(
                    0.01,
                    _GHFogEndDistance - _GHFogStartDistance);
                float fogDistance = saturate(
                    (sceneDistance - _GHFogStartDistance) / fogRange);
                fogDistance = pow(
                    max(fogDistance, 0.00001),
                    clamp(_GHFogDistancePower, 0.4, 1.5));
                float fogAmount = smoothstep(0.0, 1.0, fogDistance);

                float middlePoint = clamp(_GHFogMidPoint, 0.01, 0.99);
                float nearToMiddle = smoothstep(0.0, middlePoint, fogDistance);
                float middleToFar = smoothstep(middlePoint, 1.0, fogDistance);
                half3 fogColor = lerp(
                    _GHFogNearColor.rgb,
                    _GHFogMidColor.rgb,
                    nearToMiddle);
                fogColor = lerp(
                    fogColor,
                    _GHFogFarColor.rgb,
                    middleToFar);

                float opacity = fogAmount * saturate(_GHFogMaxOpacity);
                float distantHorizonOpacity = horizonOpacity
                    * smoothstep(0.28, 0.82, fogDistance);
                opacity = 1.0 - (1.0 - opacity)
                    * (1.0 - distantHorizonOpacity);
                fogColor = lerp(
                    fogColor,
                    _GHFogFarColor.rgb,
                    distantHorizonOpacity);
                return half4(
                    lerp(sourceColor.rgb, fogColor, opacity),
                    sourceColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
