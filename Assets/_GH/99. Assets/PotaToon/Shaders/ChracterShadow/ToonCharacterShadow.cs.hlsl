//
// This file was automatically generated. Please don't edit by hand. Execute Editor command [ Edit > Rendering > Generate Shader Includes ] instead
//

#ifndef TOON_CHARACTER_SHADOW_CS_HLSL
#define TOON_CHARACTER_SHADOW_CS_HLSL
// Generated from PotaToon.ToonCharShadow
// PackingRules = Exact
CBUFFER_START(ToonCharacterShadow)
    float4 _CharShadowParams;
    float4x4 _CharShadowViewProjM;
    float4 _CharShadowOffset0;
    float4 _CharShadowOffset1;
    float4 _CharShadowmapSize;
    float4 _CharTransparentShadowmapSize;
    float4 _CharShadowCascadeParams;
    float4 _BrightestLightDirection;
    uint _BrightestLightIndex;
    uint _UseBrightestLight;
    uint _IsBrightestLightMain;
    float _MaxToonBrightness;
CBUFFER_END


#endif