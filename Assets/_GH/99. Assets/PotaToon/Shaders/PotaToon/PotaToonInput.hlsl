#ifndef POTA_TOON_INPUT_INCLUDED
#define POTA_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
half4 _ShadeColor;
uint _UseShadeMap;
uint _DisableCharShadow;
uint _UseDarknessMode;
uint _UseNormalMap;
half _BaseStep;
half _StepSmoothness;
half _RimPower;
half _RimSmoothness;
half _BaseMapHue;
half _BaseMapSaturation;
half _BaseMapContrast;
half _ShadeMapHue;
half _ShadeMapSaturation;
half _ShadeMapContrast;
half _OutlineLightingDimmer;
half _ClippingMaskCutoff;
half _AlphaMaskScale;
half _AlphaMaskValue;
half _DitherFadeMaxZ;
half _DitherFadeMinZ;
half4 _RimColor;
half4 _ScreenRimTint;

half4 _MidColor;
half4 _OutlineColor;
uint _OutlineMode;
uint _UseOutlineNormalMap;
uint _UseMidTone;
uint _BlendOutlineMainTex;

half _OutlineWidth;
half _IndirectDimmer;
half _SpecularPower;
half _SpecularSmoothness;
half4 _SpecularColor;

float _Cutoff;
float _MidWidth;
float _RefractionWeight;
float _RefractionBlurStep;

uint _MatCapMode;
uint _MatCapMode2;
uint _MatCapMode3;
uint _MatCapMode4;
uint _MatCapMode5;
uint _MatCapMode6;
uint _MatCapMode7;
uint _MatCapMode8;
half _MatCapWeight;
half _MatCapWeight2;
half _MatCapWeight3;
half _MatCapWeight4;
half _MatCapWeight5;
half _MatCapWeight6;
half _MatCapWeight7;
half _MatCapWeight8;
half _MatCapLightingDimmer;
half _MatCapLightingDimmer2;
half _MatCapLightingDimmer3;
half _MatCapLightingDimmer4;
half _MatCapLightingDimmer5;
half _MatCapLightingDimmer6;
half _MatCapLightingDimmer7;
half _MatCapLightingDimmer8;
half4 _MatCapColor;
half4 _MatCapColor2;
half4 _MatCapColor3;
half4 _MatCapColor4;
half4 _MatCapColor5;
half4 _MatCapColor6;
half4 _MatCapColor7;
half4 _MatCapColor8;

half4 _EmissionColor;

half _OutlineOffsetZ;
half _GlitterMainStrength;
half _GlitterEnableLighting;
half _GlitterBackfaceMask;
half _GlitterApplyTransparency;
half _GlitterShadowMask;
half _GlitterParticleSize;
half _GlitterScaleRandomize;
half _GlitterContrast;
half _GlitterSensitivity;
half _GlitterBlinkSpeed;
half _GlitterAngleLimit;
half _GlitterLightDirection;
half _GlitterColorRandomness;
half _GlitterNormalStrength;
half _GlitterPostContrast;
half4 _GlitterColor;

float _BumpScale;
float _DepthBias;
float _NormalBias;
float _CharShadowSmoothnessOffset;
half  _2DFaceShadowWidth;
half  _ScreenRimWidthMultiplier;
half  _ScreenRimLightingDimmer;
uint  _DebugFaceSDF;

float4 _MainTex_ST;
float4 _ShadeMap_ST;
float4 _ColorGradingMask_ST;
float4 _ShadowExclusionMask_ST;
float4 _NormalMap_ST;
float4 _ClippingMask_ST;
float4 _FaceSDFTex_ST;
float4 _OutlineWidthMask_ST;
float4 _HairHighLightTex_ST;

float4 _FaceForward;
float4 _FaceUp;
uint _UseVertexColor;
uint _SDFReverse;
uint _UseHairHighLight;
uint _ReverseHairHighLightTex;

float _SDFOffset;
float _SDFBlur;
float _HairHiStrength;
float _HairHiUVOffset;
float4 _HeadWorldPos;

uint _BaseMapUV;
uint _ColorGradingMaskUV;
uint _NormalMapUV;
uint _ClippingMaskUV;
uint _FaceSDFUV;
uint _SpecularMapUV;
uint _RimMaskUV;
uint _HairHiMapUV;
uint _GlitterMapUV;
uint _EmissionMapUV;
uint _OutlineMaskUV;
uint _MatCapUV1;
uint _MatCapUV2;
uint _MatCapUV3;
uint _MatCapUV4;
uint _MatCapUV5;
uint _MatCapUV6;
uint _MatCapUV7;
uint _MatCapUV8;
uint _DisableOIT;

uint _ToonType;
uint _SurfaceType;
uint _AlphaMaskMode;
uint _ClippingMaskCH;
uint _SpecularMaskCH;
uint _RimMaskCH;
uint _EmissionMaskCH;
uint _OutlineMaskCH;
uint _MatCapMaskCH1;
uint _MatCapMaskCH2;
uint _MatCapMaskCH3;
uint _MatCapMaskCH4;
uint _MatCapMaskCH5;
uint _MatCapMaskCH6;
uint _MatCapMaskCH7;
uint _MatCapMaskCH8;
uint _AOMapCH;
uint _FaceSDFTexCH;
uint _ReceiveLightShadow;
uint _ColorGradingMaskReversed;
uint _ShadowExclusionMaskReversed;

uint _UseDitherFade;
uint _ScreenRimTintMode;
uint _ScreenRimShadowFade;
CBUFFER_END

float4 _BaseMap_ST;
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_ShadeMap);
TEXTURE2D(_ColorGradingMask);
TEXTURE2D(_ShadowExclusionMask);
TEXTURE2D(_ShadowBorderMask);
TEXTURE2D(_NormalMap);
TEXTURE2D(_ClippingMask);
TEXTURE2D(_SpecularMap);
TEXTURE2D(_SpecularMask);
TEXTURE2D(_RimMask);
TEXTURE2D(_MatCapTex); SAMPLER(sampler_MatCapTex);
TEXTURE2D(_MatCapMask);
TEXTURE2D(_MatCapTex2);
TEXTURE2D(_MatCapMask2);
TEXTURE2D(_MatCapTex3);
TEXTURE2D(_MatCapMask3);
TEXTURE2D(_MatCapTex4);
TEXTURE2D(_MatCapMask4);
TEXTURE2D(_MatCapTex5);
TEXTURE2D(_MatCapMask5);
TEXTURE2D(_MatCapTex6);
TEXTURE2D(_MatCapMask6);
TEXTURE2D(_MatCapTex7);
TEXTURE2D(_MatCapMask7);
TEXTURE2D(_MatCapTex8);
TEXTURE2D(_MatCapMask8);
TEXTURE2D(_EmissionMask);
TEXTURE2D(_GlitterColorTex);
TEXTURE2D(_FaceSDFTex); SAMPLER(sampler_FaceSDFTex);
TEXTURE2D(_HairHighLightTex);
TEXTURE2D(_OutlineNormalMap);
TEXTURE2D(_OutlineWidthMask); SAMPLER(sampler_OutlineWidthMask);

#define TRANSPARENT_SURFACE         3
#define REFRACTION_SURFACE          2
#define OIT_SURFACE                 2
#define FACE_TYPE                   1

#if _DEBUG_POTA_TOON
// Debug
# define DEBUG_FACE_SDF_LIGHTING    1
# define DEBUG_FACE_SDF_TEXTURE     2
#endif

#endif