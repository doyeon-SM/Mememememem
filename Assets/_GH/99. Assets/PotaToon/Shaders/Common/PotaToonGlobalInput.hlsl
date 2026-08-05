#ifndef POTA_TOON_GLOBAL_INPUT_INCLUDED
#define POTA_TOON_GLOBAL_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

half    _ScreenRimWidth;
half4   _ScreenRimColor;

TEXTURE2D_X(_PotaToonCharMask);

#define MaxScreenRimDist    _ScreenRimColor.a

#endif