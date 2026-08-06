#ifndef SIMPLE_TOON_DITHER_FADE_FORWARD_KEYWORDS_INCLUDED
#define SIMPLE_TOON_DITHER_FADE_FORWARD_KEYWORDS_INCLUDED

#pragma target 4.5
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
#pragma multi_compile _ _SHADOWS_SOFT
#if UNITY_VERSION >= 60030000
#pragma multi_compile _ _CLUSTER_LIGHT_LOOP
#define USE_FORWARD_PLUS USE_CLUSTER_LIGHT_LOOP
#define FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
#else
#pragma multi_compile _ _FORWARD_PLUS
#endif
#pragma multi_compile _ _LIGHT_LAYERS
#pragma multi_compile_fragment _ _LIGHT_COOKIES
#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
#pragma multi_compile _ DIRLIGHTMAP_COMBINED
#pragma multi_compile _ LIGHTMAP_ON
#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma multi_compile_fog

#pragma shader_feature_local_fragment _ALPHATEST_ON
#pragma multi_compile_local_fragment _ _USE_2D_FACE_SHADOW

#endif