#ifndef POTA_TOON_EYE_INPUT_INCLUDED
#define POTA_TOON_EYE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
half4   _BaseColor;
half4   _HiLightColor;
half    _BaseStep;
half    _StepSmoothness;
half    _Exposure;
half    _MinIntensity;
half    _IndirectDimmer;
half    _HiLightPowerR;
half    _HiLightPowerG;
half    _HiLightPowerB;
half    _HiLightIntensityR;
half    _HiLightIntensityG;
half    _HiLightIntensityB;
half    _RefractionWeight;
half    _Cutoff;
half    _ClippingMaskCutoff;
half    _AlphaMaskScale;
half    _AlphaMaskValue;
half    _DitherFadeMaxZ;
half    _DitherFadeMinZ;
half    _BaseMapHue;
half    _BaseMapSaturation;
half    _BaseMapContrast;
uint    _UseHiLightJitter;
uint    _UseRefraction;
uint    _AlphaMaskMode;
uint    _ClippingMaskCH;
uint    _UseDitherFade;
uint    _ToonType;
uint    _SurfaceType;
float4  _FaceForward;
float4  _FaceUp;
float4  _HeadWorldPos;
float4  _MainTex_ST;
float4  _ClippingMask_ST;
float4  _HiLightTex_ST;
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_linear_mirror);
TEXTURE2D(_ClippingMask);
TEXTURE2D(_HiLightTex);

#define TRANSPARENT_SURFACE         3
#define REFRACTION_SURFACE          2
#define OIT_SURFACE                 2
#define FACE_TYPE                   1

#endif