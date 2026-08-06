#ifndef POTA_TOON_GEM_KEYWORDS_INCLUDED
#define POTA_TOON_GEM_KEYWORDS_INCLUDED

#pragma target 4.5

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS
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
#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING

#pragma multi_compile _ LIGHTMAP_ON DIRLIGHTMAP_COMBINED
#pragma multi_compile _ PROBE_VOLUMES_L1 PROBE_VOLUMES_L2
#pragma multi_compile_fog

#pragma shader_feature_local_fragment _ALPHATEST_ON
#pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
#pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
#pragma shader_feature_local_fragment _USE_GLITTER

#pragma multi_compile_fragment _ _POTA_TOON_OIT

#endif