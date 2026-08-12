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
            TEXTURECUBE(_GHFogDayCube);
            SAMPLER(sampler_GHFogDayCube);

            float _GHDistanceFogEnabled;
            half4 _GHFogNearColor;
            half4 _GHFogMidColor;
            half4 _GHFogFarColor;
            float _GHFogStartDistance;
            float _GHFogEndDistance;
            float _GHFogMidPoint;
            float _GHFogMaxOpacity;
            float _GHFogDistancePower;
            float _GHFogSkyMatch;
            float _GHFogDayCubeAvailable;
            float _GHFogGradientAvailable;
            half4 _GHFogGradientHorizonColor;
            half4 _GHFogGradientSkyColor;
            float _GHFogGradientFadeBegin;
            float _GHFogGradientFadeEnd;
            float _GHFogSkyNightBlend;
            half4 _GHFogSkyTint;
            float _GHFogSkyExposure;
            float _GHFogSkyRotation;
            float _GHFogDaySkyScale;
            float _GHFogDayVerticalOffset;
            half4 _GHFogDayLowerSkyColor;
            float _GHFogLowerSkyWorldFade;
            float _GHFogLowerSkySourceFade;
            float _GHFogLowerSkyProtection;
            float _GHFogSkyHazeOpacity;
            float _GHFogSkyHazeHeight;
            float _GHFogHorizonStrength;
            float _GHFogHorizonHeight;
            float _GHFogHorizonDownwardFade;
            float _GHFogHorizonColorInfluence;

            float2 RotateFogDirection(float2 value, float angleRadians)
            {
                float sineValue;
                float cosineValue;
                sincos(angleRadians, sineValue, cosineValue);
                return float2(
                    value.x * cosineValue - value.y * sineValue,
                    value.x * sineValue + value.y * cosineValue);
            }

            half3 SampleDirectionalSky(float3 viewDirection)
            {
                float3 direction = normalize(viewDirection);
                direction.xz = RotateFogDirection(
                    direction.xz,
                    radians(_GHFogSkyRotation));

                half3 dayColor = _GHFogFarColor.rgb;
                if (_GHFogDayCubeAvailable > 0.5)
                {
                    float3 dayDirection = direction;
                    dayDirection.xz *= max(_GHFogDaySkyScale, 0.001);
                    dayDirection.y += _GHFogDayVerticalOffset * 0.01;
                    dayDirection = normalize(dayDirection);
                    half skyExposure = max(0.0, _GHFogSkyExposure);

                    // Match the skybox's lower-hemisphere protection so cloud
                    // pixels cannot be reintroduced on distant geometry by the
                    // directional sky-color fog blend.
                    float worldFadeHeight = sin(radians(clamp(
                        _GHFogLowerSkyWorldFade,
                        0.5,
                        15.0)));
                    float sourceFadeHeight = sin(radians(clamp(
                        _GHFogLowerSkySourceFade,
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
                    float protectedDayTextureWeight = min(
                        worldUpperWeight,
                        sourceUpperWeight);
                    float lowerSkyProtection = saturate(
                        _GHFogLowerSkyProtection);
                    float dayTextureWeight = lerp(
                        1.0,
                        protectedDayTextureWeight,
                        lowerSkyProtection);
                    float3 safeDayDirection = dayDirection;
                    safeDayDirection.y = max(
                        safeDayDirection.y,
                        sourceFadeHeight);
                    safeDayDirection = normalize(safeDayDirection);
                    float3 selectedDayDirection = normalize(lerp(
                        dayDirection,
                        safeDayDirection,
                        lowerSkyProtection));
                    half3 sampledDayColor = SAMPLE_TEXTURECUBE(
                        _GHFogDayCube,
                        sampler_GHFogDayCube,
                        selectedDayDirection).rgb;
                    sampledDayColor *= _GHFogSkyTint.rgb * skyExposure;
                    half3 lowerDayColor = _GHFogDayLowerSkyColor.rgb
                        * _GHFogSkyTint.rgb
                        * skyExposure;
                    dayColor = lerp(
                        lowerDayColor,
                        sampledDayColor,
                        dayTextureWeight);
                }

                half3 nightColor = _GHFogFarColor.rgb;
                if (_GHFogGradientAvailable > 0.5)
                {
                    float gradientAmount = smoothstep(
                        _GHFogGradientFadeBegin,
                        max(
                            _GHFogGradientFadeEnd,
                            _GHFogGradientFadeBegin + 0.001),
                        direction.y);
                    nightColor = lerp(
                        _GHFogGradientHorizonColor.rgb,
                        _GHFogGradientSkyColor.rgb,
                        gradientAmount);
                    nightColor *= max(0.0, _GHFogSkyExposure);
                }

                half3 sampledSky = lerp(
                    dayColor,
                    nightColor,
                    saturate(_GHFogSkyNightBlend));
                return lerp(
                    _GHFogFarColor.rgb,
                    sampledSky,
                    saturate(_GHFogSkyMatch));
            }

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
                half3 directionalSkyColor = SampleDirectionalSky(viewDirection);

                float skyHazeHeight = sin(radians(max(3.0, _GHFogSkyHazeHeight)));
                float horizonDistance = abs(viewDirection.y);
                float horizonHaze = 1.0 - smoothstep(
                    skyHazeHeight * 0.08,
                    skyHazeHeight,
                    horizonDistance);
                float skyHazeOpacity = horizonHaze
                    * min(saturate(_GHFogSkyHazeOpacity), 0.35);
                half3 sharedHazeColor = lerp(
                    directionalSkyColor,
                    _GHFogFarColor.rgb,
                    0.18);
                float horizonFogHeight = sin(radians(
                    clamp(_GHFogHorizonHeight, 1.0, 20.0)));
                float horizonFogMask = 1.0 - smoothstep(
                    horizonFogHeight * 0.04,
                    horizonFogHeight,
                    abs(viewDirection.y));
                half3 horizonFogTint = lerp(
                    _GHFogMidColor.rgb,
                    _GHFogFarColor.rgb,
                    0.65);
                half3 horizonFogColor = lerp(
                    directionalSkyColor,
                    horizonFogTint,
                    saturate(_GHFogHorizonColorInfluence));
                float horizonFogOpacity = horizonFogMask
                    * saturate(_GHFogHorizonStrength);

                // Sky pixels keep their actual skybox details. Only a subtle
                // horizon haze is shared with distant geometry, removing the
                // hard terrain/sky seam without covering the upper skybox.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001)
                    {
                        half3 skyOrDepthlessSurface = lerp(
                            sourceColor.rgb,
                            sharedHazeColor,
                            skyHazeOpacity);
                        return half4(
                            lerp(
                                skyOrDepthlessSurface,
                                horizonFogColor,
                                horizonFogOpacity),
                            sourceColor.a);
                    }
                #else
                    if (rawDepth >= 0.99999)
                    {
                        half3 skyOrDepthlessSurface = lerp(
                            sourceColor.rgb,
                            sharedHazeColor,
                            skyHazeOpacity);
                        return half4(
                            lerp(
                                skyOrDepthlessSurface,
                                horizonFogColor,
                                horizonFogOpacity),
                            sourceColor.a);
                    }
                #endif

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
                    clamp(_GHFogDistancePower, 0.5, 1.5));

                // Smooth distance response: no screen-angle mask, horizon
                // strip, or forced terrain replacement is involved.
                float fogAmount = fogDistance * fogDistance
                    * (3.0 - 2.0 * fogDistance);

                float middlePoint = clamp(_GHFogMidPoint, 0.01, 0.99);
                float nearToMiddle = smoothstep(
                    0.0,
                    middlePoint,
                    fogDistance);
                float middleToFar = smoothstep(
                    middlePoint,
                    1.0,
                    fogDistance);
                half3 fogColor = lerp(
                    _GHFogNearColor.rgb,
                    _GHFogMidColor.rgb,
                    nearToMiddle);
                fogColor = lerp(
                    fogColor,
                    _GHFogFarColor.rgb,
                    middleToFar);

                // At long range the fog follows the exact skybox direction
                // behind that pixel. This is what dissolves mountain outlines
                // naturally instead of producing a flat, clipped silhouette.
                float skyColorWeight = smoothstep(0.20, 0.92, fogDistance)
                    * saturate(_GHFogSkyMatch);
                fogColor = lerp(
                    fogColor,
                    directionalSkyColor,
                    skyColorWeight);

                // Keep part of the original surface at every distance. A small
                // common horizon veil then softens both sides of the boundary.
                float opacity = fogAmount
                    * min(saturate(_GHFogMaxOpacity), 0.88);
                // The dedicated horizon bank is camera-relative. Applying its
                // symmetric angular mask to downward rays made distant valleys
                // look like a cloud layer whenever the camera reached high
                // ground. Fade only this extra bank below the visual horizon;
                // the regular world-space distance fog remains unchanged.
                float downwardFadeAngle = sin(radians(clamp(
                    _GHFogHorizonDownwardFade,
                    0.5,
                    12.0)));
                float geometryHorizonDirection = smoothstep(
                    -downwardFadeAngle,
                    0.0,
                    viewDirection.y);
                float geometryHorizonBlend = horizonFogOpacity
                    * geometryHorizonDirection
                    * smoothstep(0.55, 1.0, fogDistance);
                fogColor = lerp(
                    fogColor,
                    horizonFogColor,
                    geometryHorizonBlend);
                opacity = 1.0 - (1.0 - opacity)
                    * (1.0 - geometryHorizonBlend);
                float geometryHaze = skyHazeOpacity
                    * smoothstep(0.35, 1.0, fogDistance);
                opacity = 1.0 - (1.0 - opacity) * (1.0 - geometryHaze);
                fogColor = lerp(
                    fogColor,
                    sharedHazeColor,
                    geometryHaze);
                return half4(
                    lerp(sourceColor.rgb, fogColor, opacity),
                    sourceColor.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
