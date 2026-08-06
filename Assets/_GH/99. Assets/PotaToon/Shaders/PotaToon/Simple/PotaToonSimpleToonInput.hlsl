#ifndef SIMPLE_TOON_INPUT_INCLUDED
#define SIMPLE_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
half4 _BaseColor;
half4 _ShadeColor;
half4 _MidColor;
half4 _OutlineColor;
half4 _RimColor;
half4 _ScreenRimTint;
half4 _SpecularColor;
half4 _MatCapColor;
half4 _MatCapColor2;
half4 _EmissionColor;

uint _UseShadeMap;
uint _DisableCharShadow;
uint _CharShadowType;
uint _UseDarknessMode;
uint _UseNormalMap;
uint _OutlineMode;
uint _UseOutlineNormalMap;
uint _UseMidTone;
uint _BlendOutlineMainTex;

half _BaseStep;
half _StepSmoothness;
half _OutlineLightingDimmer;
half _RimPower;
half _RimSmoothness;
half _MatCapWeight;
half _MatCapWeight2;
half _MatCapLightingDimmer;
half _MatCapLightingDimmer2;
half _SpecularPower;
half _SpecularSmoothness;
half _IndirectDimmer;
half _ClippingMaskCutoff;
half _AlphaMaskScale;
half _AlphaMaskValue;

float _Cutoff;
float _MidWidth;
float _BumpScale;
float _DepthBias;
float _NormalBias;
float _CharShadowSmoothnessOffset;
half _2DFaceShadowWidth;
half _OutlineWidth;
half _OutlineOffsetZ;
half _ScreenRimWidthMultiplier;
half _ScreenRimLightingDimmer;
half _DitherFadeMaxZ;
half _DitherFadeMinZ;
half _BaseMapHue;
half _BaseMapSaturation;
half _BaseMapContrast;
half _ShadeMapHue;
half _ShadeMapSaturation;
half _ShadeMapContrast;

uint _MatCapMode;
uint _MatCapMode2;
uint _UseVertexColor;
uint _ToonType;
uint _SurfaceType;
uint _AlphaMaskMode;
uint _ClippingMaskCH;
uint _SpecularMaskCH;
uint _RimMaskCH;
uint _OutlineMaskCH;
uint _MatCapMaskCH1;
uint _MatCapMaskCH2;
uint _AOMapCH;
uint _ReceiveLightShadow;
uint _UseDitherFade;
uint _ScreenRimTintMode;
uint _ScreenRimShadowFade;

float4 _MainTex_ST;
float4 _ShadeMap_ST;
float4 _EmissionMap_ST;
float4 _NormalMap_ST;
float4 _ClippingMask_ST;
float4 _OutlineWidthMask_ST;
float4 _HeadWorldPos;
float4 _FaceForward;
float4 _FaceUp;
CBUFFER_END

float4 _BaseMap_ST;
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_ShadeMap);
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
TEXTURE2D(_OutlineNormalMap);
TEXTURE2D(_OutlineWidthMask); SAMPLER(sampler_OutlineWidthMask);

#define TRANSPARENT_SURFACE         3
#define REFRACTION_SURFACE          2
#define OIT_SURFACE                 2
#define FACE_TYPE                   1

#endif