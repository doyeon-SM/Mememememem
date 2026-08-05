#ifndef POTA_TOON_GEM_INPUT_INCLUDED
#define POTA_TOON_GEM_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
half4   _BaseColor;
half4   _AbsorptionColor;
half4   _ParticleColor;
half4   _GlitterColor;
half4   _RimColor;
half4   _MatCapColor;
half4   _MatCapColor2;
half4   _MatCapColor3;
half4   _MatCapColor4;
half4   _ScreenRimTint;
half    _Roughness;
half    _IndirectDimmer;
half    _GemShine;
half    _ClearcoatIntensity;
half    _ClearcoatRoughness;
half    _TransmissionStrength;
half    _Thickness;
half    _BaseStrength;
half    _ChromaticAberration;
half    _RefractionStrength;
half    _RefractionBlurWeight;
half    _RefractionFresnelPower;
half    _ParticleIntensity;
half    _ParticleLightingDimmer;
half    _ParticleLoop;
half    _GlitterMainStrength;
half    _GlitterEnableLighting;
half    _GlitterBackfaceMask;
half    _GlitterApplyTransparency;
half    _GlitterShadowMask;
half    _GlitterParticleSize;
half    _GlitterScaleRandomize;
half    _GlitterContrast;
half    _GlitterSensitivity;
half    _GlitterBlinkSpeed;
half    _GlitterAngleLimit;
half    _GlitterLightDirection;
half    _GlitterColorRandomness;
half    _GlitterNormalStrength;
half    _GlitterPostContrast;
half    _RimPower;
half    _RimSmoothness;
half    _ScreenRimWidthMultiplier;
half    _ScreenRimLightingDimmer;
half    _MatCapWeight;
half    _MatCapWeight2;
half    _MatCapWeight3;
half    _MatCapWeight4;
half    _MatCapLightingDimmer;
half    _MatCapLightingDimmer2;
half    _MatCapLightingDimmer3;
half    _MatCapLightingDimmer4;
half    _Cutoff;
half    _ClippingMaskCutoff;
half    _AlphaMaskScale;
half    _AlphaMaskValue;
half    _BumpScale;
half    _CharShadowSmoothnessOffset;
half    _DitherFadeMaxZ;
half    _DitherFadeMinZ;
half    _BaseMapHue;
half    _BaseMapSaturation;
half    _BaseMapContrast;
uint    _DisableOIT;
uint    _ToonType;
uint    _SurfaceType;
uint    _ReceiveLightShadow;
uint    _UseNormalMap;
uint    _UseRim;
uint    _MatCapMode;
uint    _MatCapMode2;
uint    _MatCapMode3;
uint    _MatCapMode4;
uint    _RimMaskCH;
uint    _MatCapMaskCH;
uint    _MatCapMaskCH2;
uint    _MatCapMaskCH3;
uint    _MatCapMaskCH4;
uint    _AlphaMaskMode;
uint    _ClippingMaskCH;
uint    _ScreenRimTintMode;
uint    _ScreenRimShadowFade;
uint    _DisableCharShadow;
uint    _BaseMapUV;
uint    _NormalMapUV;
uint    _ClippingMaskUV;
uint    _RimMaskUV;
uint    _GlitterMapUV;
uint    _MatCapUV1;
uint    _MatCapUV2;
uint    _MatCapUV3;
uint    _MatCapUV4;
uint    _UseDitherFade;
uint    _PotaToon_Pad_00_;
float4  _MainTex_ST;
float4  _ClippingMask_ST;
float4  _NormalMap_ST;
float4  _HeadWorldPos;
CBUFFER_END

TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
TEXTURE2D(_ClippingMask);
TEXTURE2D(_NormalMap);
TEXTURE2D(_GlitterColorTex);
TEXTURE2D(_RimMask);
TEXTURE2D(_MatCapTex); SAMPLER(sampler_MatCapTex);
TEXTURE2D(_MatCapMask);
TEXTURE2D(_MatCapTex2);
TEXTURE2D(_MatCapMask2);
TEXTURE2D(_MatCapTex3);
TEXTURE2D(_MatCapMask3);
TEXTURE2D(_MatCapTex4);
TEXTURE2D(_MatCapMask4);

#define TRANSPARENT_SURFACE         3
#define REFRACTION_SURFACE          2
#define OIT_SURFACE                 2
#define FACE_TYPE                   1

#endif