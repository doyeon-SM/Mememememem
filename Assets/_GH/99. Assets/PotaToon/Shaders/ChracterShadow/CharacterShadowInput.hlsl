#ifndef CHARACTER_SHADOW_INPUT_INCLUDED
#define CHARACTER_SHADOW_INPUT_INCLUDED

#include "./ToonCharacterShadow.cs.hlsl"

#define _CharShadowBias                 _CharShadowParams.xy
#define _CharShadowStepSmoothness       _CharShadowParams.z
#define _CharShadowUVScale              _CharShadowCascadeParams.y
#define _CharShadowSampleQuality        _CharShadowCascadeParams.z // Currently unused
#define _RcpCharShadowMaxBoundSize      _CharShadowCascadeParams.w
#define _CharShadowCullingDist          -(_CharShadowCascadeParams.x - 0.01) // should be less than culling distance 

TEXTURE2D(_CharShadowMap);
TEXTURE2D(_TransparentShadowMap);
TEXTURE2D(_TransparentAlphaSum);
TEXTURE2D(_ScreenSpaceCharShadowmapTexture);
TEXTURE2D(_CharContactShadowTexture);

#endif