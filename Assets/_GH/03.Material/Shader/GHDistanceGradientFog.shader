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
            TEXTURECUBE(_GHFogNightCube);
            SAMPLER(sampler_GHFogNightCube);

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
            float _GHFogSkyCoverage;
            float _GHFogSkyFullCoverage;
            float _GHFogSkyOpacity;
            float _GHFogTerrainCoverage;
            float _GHFogTerrainConcealStart;
            float _GHFogTerrainConcealFull;
            float _GHFogTerrainConcealStrength;
            float _GHFogNoiseStrength;
            float _GHFogNoiseScale;
            float _GHFogNoiseSpeed;
            float _GHFogSkyMatch;
            half4 _GHFogSkyHorizonColor;
            half4 _GHFogSkyZenithColor;
            float _GHFogSkyGradientBegin;
            float _GHFogSkyGradientEnd;
            float _GHFogDayCubeAvailable;
            float _GHFogNightCubeAvailable;
            float _GHFogGradientAvailable;
            float _GHFogSkyNightBlend;
            half4 _GHFogSkyTint;
            float _GHFogSkyExposure;
            float _GHFogSkyRotation;
            float _GHFogDaySkyScale;
            float _GHFogNightSkyScale;
            float _GHFogDayVerticalOffset;
            float _GHFogNightVerticalOffset;

            float HashFogCell(float2 position)
            {
                return frac(
                    sin(dot(position, float2(127.1, 311.7)))
                    * 43758.5453);
            }

            float SmoothFogNoise(float2 position)
            {
                float2 cell = floor(position);
                float2 local = frac(position);
                local = local * local * (3.0 - 2.0 * local);

                float lower = lerp(
                    HashFogCell(cell),
                    HashFogCell(cell + float2(1.0, 0.0)),
                    local.x);
                float upper = lerp(
                    HashFogCell(cell + float2(0.0, 1.0)),
                    HashFogCell(cell + float2(1.0, 1.0)),
                    local.x);
                return lerp(lower, upper, local.y);
            }

            float3 RotateFogDirection(float3 direction, float degrees)
            {
                float angle = radians(degrees);
                float sine;
                float cosine;
                sincos(angle, sine, cosine);
                return float3(
                    cosine * direction.x - sine * direction.z,
                    direction.y,
                    sine * direction.x + cosine * direction.z);
            }

            float3 RemapFogSkyDirection(
                float3 direction,
                float visualScale,
                float verticalOffsetDegrees)
            {
                direction = normalize(direction);
                float horizontalLength = max(length(direction.xz), 0.0001);
                float2 horizontalDirection = direction.xz / horizontalLength;
                float pitch = atan2(direction.y, horizontalLength);
                float samplePitch = pitch / max(visualScale, 0.01)
                    - radians(verticalOffsetDegrees);
                samplePitch = clamp(
                    samplePitch,
                    -PI * 0.499,
                    PI * 0.499);

                float pitchCosine = cos(samplePitch);
                return float3(
                    horizontalDirection.x * pitchCosine,
                    sin(samplePitch),
                    horizontalDirection.y * pitchCosine);
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
                float2 fogNoiseCoordinates = float2(
                    atan2(viewDirection.x, viewDirection.z),
                    viewDirection.y) * max(2.0, _GHFogNoiseScale);
                fogNoiseCoordinates += float2(
                    _Time.y * _GHFogNoiseSpeed,
                    _Time.y * _GHFogNoiseSpeed * 0.37);
                float fogNoise = SmoothFogNoise(fogNoiseCoordinates);
                float fogBrightness = lerp(
                    1.0 - _GHFogNoiseStrength,
                    1.0 + _GHFogNoiseStrength,
                    fogNoise);
                half3 atmosphericFarColor =
                    _GHFogFarColor.rgb * fogBrightness;
                float skyGradientAmount = smoothstep(
                    _GHFogSkyGradientBegin,
                    _GHFogSkyGradientEnd,
                    viewDirection.y);
                half3 proceduralSkyColor = lerp(
                    _GHFogSkyHorizonColor.rgb,
                    _GHFogSkyZenithColor.rgb,
                    skyGradientAmount);
                float3 rotatedSkyDirection = RotateFogDirection(
                    viewDirection,
                    _GHFogSkyRotation);
                half3 daySkyColor = atmosphericFarColor;
                if (_GHFogDayCubeAvailable > 0.5)
                {
                    float3 dayDirection = RemapFogSkyDirection(
                        rotatedSkyDirection,
                        _GHFogDaySkyScale,
                        _GHFogDayVerticalOffset);
                    daySkyColor = SAMPLE_TEXTURECUBE(
                        _GHFogDayCube,
                        sampler_GHFogDayCube,
                        dayDirection).rgb;
                }

                half3 nightSkyColor = atmosphericFarColor;
                if (_GHFogGradientAvailable > 0.5)
                {
                    nightSkyColor = proceduralSkyColor;
                }
                else if (_GHFogNightCubeAvailable > 0.5)
                {
                    float3 nightDirection = RemapFogSkyDirection(
                        rotatedSkyDirection,
                        _GHFogNightSkyScale,
                        _GHFogNightVerticalOffset);
                    nightSkyColor = SAMPLE_TEXTURECUBE(
                        _GHFogNightCube,
                        sampler_GHFogNightCube,
                        nightDirection).rgb;
                }

                half3 currentSkyColor = lerp(
                    daySkyColor,
                    nightSkyColor,
                    saturate(_GHFogSkyNightBlend));
                currentSkyColor *=
                    _GHFogSkyTint.rgb * max(0.0, _GHFogSkyExposure);
                half3 matchedFarColor = lerp(
                    atmosphericFarColor,
                    currentSkyColor,
                    saturate(_GHFogSkyMatch));
                // The fog bank has its own atmospheric color, but borrows
                // enough from the active skybox to stay coherent at every
                // time of day. Both sky and distant geometry use this exact
                // same color, which prevents a terrain silhouette seam.
                half3 fogBankColor = lerp(
                    atmosphericFarColor,
                    currentSkyColor,
                    saturate(_GHFogSkyMatch) * 0.35);
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
                float skyCoverage = max(
                    0.0001,
                    sin(radians(max(0.01, _GHFogSkyCoverage))));
                float skyFullCoverage = sin(radians(clamp(
                    _GHFogSkyFullCoverage,
                    0.0,
                    max(0.01, _GHFogSkyCoverage - 0.01))));
                float lowerSkyHeight = viewDirection.y - horizonCenter;
                float lowerSkyVeil = 1.0 - smoothstep(
                    skyFullCoverage,
                    skyCoverage,
                    lowerSkyHeight);
                lowerSkyVeil *= saturate(_GHFogSkyOpacity);
                // The lower sky receives the dense bank, but the broad fade
                // leaves the upper portion of the skybox completely visible.
                float skyFogOpacity = max(horizonOpacity, lowerSkyVeil);
                float skyDensityVariation = lerp(
                    1.0 - _GHFogNoiseStrength * 0.35,
                    1.0 + _GHFogNoiseStrength * 0.35,
                    fogNoise);
                skyFogOpacity = saturate(
                    skyFogOpacity
                    * lerp(
                        skyDensityVariation,
                        1.0,
                        saturate(lowerSkyVeil)));

                // The fog bank hides the lower horizon, while the skybox above
                // its fade-out angle remains completely untouched.
                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001)
                    {
                        return half4(
                            lerp(
                                sourceColor.rgb,
                                fogBankColor,
                                skyFogOpacity),
                            sourceColor.a);
                    }
                #else
                    if (rawDepth >= 0.99999)
                    {
                        return half4(
                            lerp(
                                sourceColor.rgb,
                                fogBankColor,
                                skyFogOpacity),
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
                // Match Unity's Linear Fog response: haze accumulates steadily
                // instead of suddenly turning distant geometry into a color mask.
                float fogAmount = fogDistance;

                float middlePoint = clamp(_GHFogMidPoint, 0.01, 0.99);
                float nearToMiddle = smoothstep(0.0, middlePoint, fogDistance);
                float middleToFar = smoothstep(middlePoint, 1.0, fogDistance);
                // Pull near and middle haze gently toward the current skybox.
                // This removes the pasted-on solid-color look while retaining
                // enough neutral mist to keep foreground depth readable.
                float skyMatchStrength = saturate(_GHFogSkyMatch);
                half3 skyCoupledNearColor = lerp(
                    _GHFogNearColor.rgb,
                    currentSkyColor,
                    skyMatchStrength * 0.18);
                half3 skyCoupledMidColor = lerp(
                    _GHFogMidColor.rgb,
                    currentSkyColor,
                    skyMatchStrength * 0.38);
                half3 fogColor = lerp(
                    skyCoupledNearColor,
                    skyCoupledMidColor,
                    nearToMiddle);
                fogColor = lerp(
                    fogColor,
                    matchedFarColor,
                    middleToFar);

                float fogDensityVariation = lerp(
                    1.0 - _GHFogNoiseStrength * 0.55,
                    1.0 + _GHFogNoiseStrength * 0.55,
                    fogNoise);
                float opacity = saturate(
                    fogAmount
                    * saturate(_GHFogMaxOpacity)
                    * fogDensityVariation);

                // Conceal distant skyline geometry independently from the sky.
                // This lets cliffs and terrain disappear into the sampled
                // skybox while the actual sky pixels stay clear and detailed.
                float terrainCoverage = max(
                    skyCoverage,
                    sin(radians(max(0.01, _GHFogTerrainCoverage))));
                float terrainLowerRegion = 1.0 - smoothstep(
                    skyFullCoverage,
                    terrainCoverage,
                    lowerSkyHeight);
                float terrainDistanceMask = smoothstep(
                    _GHFogTerrainConcealStart,
                    max(
                        _GHFogTerrainConcealStart + 0.01,
                        _GHFogTerrainConcealFull),
                    fogDistance);
                float terrainConcealment = saturate(
                    terrainLowerRegion
                    * terrainDistanceMask
                    * _GHFogTerrainConcealStrength);
                // Below the fog-bank top, geometry converges to the same
                // opaque bank color as the sky. Higher mountain peaks converge
                // to the sampled skybox instead, so the upper sky can stay
                // visible without leaving a flat fog-colored silhouette.
                half3 terrainConcealmentColor = lerp(
                    matchedFarColor,
                    fogBankColor,
                    saturate(lowerSkyVeil));
                opacity = 1.0 - (1.0 - opacity)
                    * (1.0 - terrainConcealment);
                fogColor = lerp(
                    fogColor,
                    terrainConcealmentColor,
                    terrainConcealment);

                float distantHorizonOpacity = horizonOpacity
                    * smoothstep(0.28, 0.82, fogDistance);
                opacity = 1.0 - (1.0 - opacity)
                    * (1.0 - distantHorizonOpacity);
                fogColor = lerp(
                    fogColor,
                    matchedFarColor,
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
