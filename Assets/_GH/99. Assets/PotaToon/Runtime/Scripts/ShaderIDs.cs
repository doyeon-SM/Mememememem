using UnityEngine;
using UnityEngine.Rendering;

namespace PotaToon
{
    public static class ShaderIDs
    {
        public static readonly int _CharShadowMap = Shader.PropertyToID("_CharShadowMap");
        public static readonly int _TransparentShadowMap = Shader.PropertyToID("_TransparentShadowMap");
        public static readonly int _TransparentAlphaSum = Shader.PropertyToID("_TransparentAlphaSum");
        public static readonly int _CharTransparentShadowmapSize = Shader.PropertyToID("_CharTransparentShadowmapSize");
        public static readonly int _CharShadowmapIndex = Shader.PropertyToID("_CharShadowmapIndex");
        public static readonly int _ScreenSpaceCharShadowmapTexture = Shader.PropertyToID("_ScreenSpaceCharShadowmapTexture");
        public static readonly int _CharContactShadowTexture = Shader.PropertyToID("_CharContactShadowTexture");
        public static readonly int _ToonCharacterShadow = Shader.PropertyToID("ToonCharacterShadow");
        public static readonly int _OITDepthTexture = Shader.PropertyToID("_OITDepthTexture");
        public static readonly int _PotaToonCharMask = Shader.PropertyToID("_PotaToonCharMask");
        public static readonly int _BlitTexture = Shader.PropertyToID("_BlitTexture");
        public static readonly int _BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        public static readonly int _FallbackMaxToonBrightness = Shader.PropertyToID("_MaxToonBrightness");
        // Post Processing
        public static readonly int _ScreenOutlineThickness = Shader.PropertyToID("_ScreenOutlineThickness");
        public static readonly int _ScreenOutlineEdgeStrength = Shader.PropertyToID("_ScreenOutlineEdgeStrength");
        public static readonly int _ScreenOutlineColor = Shader.PropertyToID("_ScreenOutlineColor");
        public static readonly int _TonyMcMapfaceLut = Shader.PropertyToID("_TonyMcMapfaceLut");
        public static readonly int _CharGammaAdjust = Shader.PropertyToID("_CharGammaAdjust");
        public static readonly int _PostExposure = Shader.PropertyToID("_PostExposure");
        public static readonly int _ColorFilter = Shader.PropertyToID("_ColorFilter");
        public static readonly int _HueSatCon = Shader.PropertyToID("_HueSatCon");
        public static readonly int _ColorBalance = Shader.PropertyToID("_ColorBalance");
        public static readonly int _ScreenRimWidth = Shader.PropertyToID("_ScreenRimWidth");
        public static readonly int _ScreenRimColor = Shader.PropertyToID("_ScreenRimColor");
        public static readonly int _CustomToneCurve = Shader.PropertyToID("_CustomToneCurve");
        public static readonly int _ToeSegmentA = Shader.PropertyToID("_ToeSegmentA");
        public static readonly int _ToeSegmentB = Shader.PropertyToID("_ToeSegmentB");
        public static readonly int _MidSegmentA = Shader.PropertyToID("_MidSegmentA");
        public static readonly int _MidSegmentB = Shader.PropertyToID("_MidSegmentB");
        public static readonly int _ShoSegmentA = Shader.PropertyToID("_ShoSegmentA");
        public static readonly int _ShoSegmentB = Shader.PropertyToID("_ShoSegmentB");
        
    }

    public static class PotaToonGlobalKeywords
    {
        public static readonly GlobalKeyword OIT = GlobalKeyword.Create("_POTA_TOON_OIT");
        public static readonly GlobalKeyword Debug = GlobalKeyword.Create("_DEBUG_POTA_TOON");
    }
}
