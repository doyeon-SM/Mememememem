Shader "PotaToon/UberPost"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#if UNITY_VERSION >= 202230
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ScreenCoordOverride.hlsl"
#else
        #include "./PotaToonUtilsFor2021.hlsl"
        #pragma multi_compile _ _USE_DRAW_PROCEDURAL
        TEXTURE2D_X(_BlitTexture);
#endif
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        // #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "./PotaToonToneMapping.hlsl"
        #include "../ChracterShadow/CharacterShadowInput.hlsl"

        #pragma multi_compile_local_fragment _ _ENABLE_ALPHA_OUTPUT
        #pragma multi_compile_local_fragment _ _BLOOM_LQ _BLOOM_HQ _BLOOM_LQ_DIRT _BLOOM_HQ_DIRT
        #pragma multi_compile_local_fragment _ _POTA_TOON_TONEMAP_NEUTRAL _POTA_TOON_TONEMAP_ACES _POTA_TOON_TONEMAP_FILMIC _POTA_TOON_TONEMAP_UCHIMURA _POTA_TOON_TONEMAP_TONY _POTA_TOON_TONEMAP_CUSTOM

        TEXTURE2D_X(_Bloom_Texture);
        TEXTURE2D(_LensDirt_Texture);
        TEXTURE2D_X(_PotaToonCharMask);

        float _PostExposure;
        float _ScreenRimWidth;
        float _CharGammaAdjust;
        float _LensDirt_Intensity;
        float _ScreenOutlineThickness;
        float _ScreenOutlineEdgeStrength;
        float4 _ScreenOutlineColor;
        float4 _HueSatCon;
        float4 _ColorFilter;
        float4 _ColorBalance;
        float4 _ScreenRimColor;
        float4 _BloomTexture_TexelSize;
        float4 _Bloom_Texture_TexelSize;
        float4 _Bloom_Params;
        float4 _LensDirt_Params;

        #define BloomIntensity          _Bloom_Params.x
        #define BloomTint               _Bloom_Params.yzw
        #define LensDirtScale           _LensDirt_Params.xy
        #define LensDirtOffset          _LensDirt_Params.zw
        #define LensDirtIntensity       _LensDirt_Intensity.x
        #define MaxScreenRimDist        _ScreenRimColor.a
        #define AlphaScale              1.0
        #define AlphaBias               0.0

        // Hardcoded dependencies to reduce the number of variants
        #if _BLOOM_LQ || _BLOOM_HQ || _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
            #define BLOOM
            #if _BLOOM_LQ_DIRT || _BLOOM_HQ_DIRT
                #define BLOOM_DIRT
            #endif
        #endif

        void ApplyColorAdjustments(inout half3 colorLinear)
        {
            // White balance in LMS space
            float3 colorLMS = LinearToLMS(colorLinear);
            colorLMS *= _ColorBalance.xyz;
            colorLinear = LMSToLinear(colorLMS);
            
            // Do contrast in log after white balance
            float3 colorLog = LinearToLogC(colorLinear);
            colorLog = (colorLog - ACEScc_MIDGRAY) * _HueSatCon.z + ACEScc_MIDGRAY;
            colorLinear = LogCToLinear(colorLog);
            
            // Color filter is just an unclipped multiplier
            colorLinear *= _ColorFilter.xyz;
            
            // Do NOT feed negative values to the following color ops
            colorLinear = max(0.0, colorLinear);
            
            // HSV operations
            float satMult = 1.0;
            float3 hsv = RgbToHsv(colorLinear);
            {
                // Hue Shift & Hue Vs Hue
                float hue = hsv.x + _HueSatCon.x;
                hsv.x = RotateHue(hue, 0.0, 1.0);
            }
            colorLinear = HsvToRgb(hsv);
            
            // Global saturation
            float luma = GetLuminance(colorLinear);
            colorLinear = luma.xxx + (_HueSatCon.yyy * satMult) * (colorLinear - luma.xxx);

            // We don't saturate the output (To preserve HDR)
            colorLinear = max(0.0, colorLinear);
        }
        
        half4 PotaToonPostProcess(float2 uv)
        {
            bool isCharArea = SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, uv).r > 0.5;
            #if !defined(BLOOM)
            if (!isCharArea)
                clip(-1);
            #endif
            
            // NOTE: Hlsl specifies missing input.a to fill 1 (0 for .rgb).
            // InputColor is a "bottom" layer for alpha output.
            half4 inputColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            half3 color = inputColor.rgb;

            #if UNITY_COLORSPACE_GAMMA
            {
                color = GetSRGBToLinear(color);
                inputColor = GetSRGBToLinear(inputColor);   // Deadcode removal if no effect on output color
            }
            #endif

            // Bloom
            #if defined(BLOOM)
            {
                float2 uvBloom = uv;
                #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
                UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
                {
                    uvBloom = RemapFoveatedRenderingNonUniformToLinear(uvBloom);
                }
                #endif

                #if _BLOOM_HQ
                half3 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_Bloom_Texture, sampler_LinearClamp), SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom), _Bloom_Texture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex).xyz;
                #else
                half3 bloom = SAMPLE_TEXTURE2D_X(_Bloom_Texture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom)).xyz;
                #endif

                #if UNITY_COLORSPACE_GAMMA
                bloom *= bloom; // γ to linear
                #endif

                bloom *= BloomIntensity;
                color += bloom * BloomTint;

                #if defined(BLOOM_DIRT)
                {
                    // UVs for the dirt texture should be DistortUV(uv * DirtScale + DirtOffset) but
                    // considering we use a cover-style scale on the dirt texture the difference
                    // isn't massive so we chose to save a few ALUs here instead in case lens
                    // distortion is active.
                    half3 dirt = SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_LinearClamp, uv * LensDirtScale + LensDirtOffset).xyz;
                    dirt *= LensDirtIntensity;
                    color += dirt * bloom.xyz;
                }
                #endif

                #if _ENABLE_ALPHA_OUTPUT
                // Bloom should also spread in areas with zero alpha, so we save the image with bloom here to do the mixing at the end of the shader
                inputColor.xyz = color.xyz;
                #endif

                if (!isCharArea)
                {
                    half3 bloomOutput = bloom * BloomTint;
                    half bloomAlpha = saturate(bloomOutput.r * 0.299 + bloomOutput.g * 0.587 + bloomOutput.b * 0.114);    
                    return half4(color, bloomAlpha);
                }
            }
            #endif
            
            // Apply Post Exposure
            color *= _PostExposure;
            
            // Assume the input as srgb if more contrast required.
            color = lerp(color, GetSRGBToLinear(color), _CharGammaAdjust);
            
            // Tone Mapping
            color = ApplyPotaToonToneMap(color);

            // Color Adjustments
            ApplyColorAdjustments(color);

            // When Unity is configured to use gamma color encoding, we ignore the request to convert to gamma 2.0 and instead fall back to sRGB encoding
            #if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
            {
                color = LinearToGamma20(color);
                inputColor = LinearToGamma20(inputColor);
            }
            // Back to sRGB
            #elif UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
            {
                color = GetLinearToSRGB(color);
                inputColor = LinearToSRGB(inputColor);
            }
            #endif

            // Alpha mask
            #if _ENABLE_ALPHA_OUTPUT
            {
                // Post processing is not applied on pixels with zero alpha
                // The alpha scale and bias control how steep is the transition between the post-processed and plain regions
                half alpha = inputColor.a * AlphaScale + AlphaBias;
                // Saturate is necessary to avoid issues when additive blending pushes the alpha over 1.
                // NOTE: in UNITY_COLORSPACE_GAMMA we alpha blend in gamma here, linear otherwise.
                color.xyz = lerp(inputColor.xyz, color.xyz, saturate(alpha));
            }
            #endif
            
            #if _ENABLE_ALPHA_OUTPUT
            // Saturate is necessary to avoid issues when additive blending pushes the alpha over 1.
            return half4(color, saturate(inputColor.a));
            #else
            return half4(color, 1);
            #endif
        }

        half4 PotaToonPostProcessEnvironment(float2 uv)
        {
            bool isCharArea = SAMPLE_TEXTURE2D_X(_PotaToonCharMask, sampler_LinearClamp, uv).r > 0.5;
            if (isCharArea)
                clip(-1);
            
            // NOTE: Hlsl specifies missing input.a to fill 1 (0 for .rgb).
            // InputColor is a "bottom" layer for alpha output.
            half4 inputColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            half3 color = inputColor.rgb;

            #if UNITY_COLORSPACE_GAMMA
            {
                color = GetSRGBToLinear(color);
                inputColor = GetSRGBToLinear(inputColor);   // Deadcode removal if no effect on output color
            }
            #endif

            // Bloom
            #if defined(BLOOM)
            {
                float2 uvBloom = uv;
                #if defined(SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
                UNITY_BRANCH if (_FOVEATED_RENDERING_NON_UNIFORM_RASTER)
                {
                    uvBloom = RemapFoveatedRenderingNonUniformToLinear(uvBloom);
                }
                #endif

                #if _BLOOM_HQ
                half3 bloom = SampleTexture2DBicubic(TEXTURE2D_X_ARGS(_Bloom_Texture, sampler_LinearClamp), SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom), _Bloom_Texture_TexelSize.zwxy, (1.0).xx, unity_StereoEyeIndex).xyz;
                #else
                half3 bloom = SAMPLE_TEXTURE2D_X(_Bloom_Texture, sampler_LinearClamp, SCREEN_COORD_REMOVE_SCALEBIAS(uvBloom)).xyz;
                #endif

                #if UNITY_COLORSPACE_GAMMA
                bloom *= bloom; // γ to linear
                #endif

                bloom *= BloomIntensity;
                color += bloom * BloomTint;

                #if defined(BLOOM_DIRT)
                {
                    // UVs for the dirt texture should be DistortUV(uv * DirtScale + DirtOffset) but
                    // considering we use a cover-style scale on the dirt texture the difference
                    // isn't massive so we chose to save a few ALUs here instead in case lens
                    // distortion is active.
                    half3 dirt = SAMPLE_TEXTURE2D(_LensDirt_Texture, sampler_LinearClamp, uv * LensDirtScale + LensDirtOffset).xyz;
                    dirt *= LensDirtIntensity;
                    color += dirt * bloom.xyz;
                }
                #endif

                #if _ENABLE_ALPHA_OUTPUT
                // Bloom should also spread in areas with zero alpha, so we save the image with bloom here to do the mixing at the end of the shader
                inputColor.xyz = color.xyz;
                #endif
            }
            #endif
            
            // Apply Post Exposure
            color *= _PostExposure;
            
            // Tone Mapping
            color = ApplyPotaToonToneMap(color);

            // Color Adjustments
            ApplyColorAdjustments(color);

            // When Unity is configured to use gamma color encoding, we ignore the request to convert to gamma 2.0 and instead fall back to sRGB encoding
            #if _GAMMA_20 && !UNITY_COLORSPACE_GAMMA
            {
                color = LinearToGamma20(color);
                inputColor = LinearToGamma20(inputColor);
            }
            // Back to sRGB
            #elif UNITY_COLORSPACE_GAMMA || _LINEAR_TO_SRGB_CONVERSION
            {
                color = GetLinearToSRGB(color);
                inputColor = LinearToSRGB(inputColor);
            }
            #endif

            // Alpha mask
            #if _ENABLE_ALPHA_OUTPUT
            {
                // Post processing is not applied on pixels with zero alpha
                // The alpha scale and bias control how steep is the transition between the post-processed and plain regions
                half alpha = inputColor.a * AlphaScale + AlphaBias;
                // Saturate is necessary to avoid issues when additive blending pushes the alpha over 1.
                // NOTE: in UNITY_COLORSPACE_GAMMA we alpha blend in gamma here, linear otherwise.
                color.xyz = lerp(inputColor.xyz, color.xyz, saturate(alpha));
            }
            #endif
            
            #if _ENABLE_ALPHA_OUTPUT
            // Saturate is necessary to avoid issues when additive blending pushes the alpha over 1.
            return half4(color, saturate(inputColor.a));
            #else
            return half4(color, 1);
            #endif
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "POTAToonUberPostCharacter"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
            #if UNITY_VERSION >= 202230
                float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
            #else
                float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.uv));
            #endif
                
                return PotaToonPostProcess(uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "POTAToonUberPostEnvironment"
            
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
            #if UNITY_VERSION >= 202230
                float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.texcoord));
            #else
                float2 uv = SCREEN_COORD_APPLY_SCALEBIAS(UnityStereoTransformScreenSpaceTex(input.uv));
            #endif
                
                return PotaToonPostProcessEnvironment(uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "POTAToonScreenOutline"
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM
            #include "./PotaToonScreenPostPasses.hlsl"
            #pragma multi_compile_local_fragment _ _EXCLUDE_INNER_SCREEN_OUTLINES
            
            #pragma vertex Vert
            #pragma fragment FragPotaToonScreenOutline

            ENDHLSL
        }
    }
}
