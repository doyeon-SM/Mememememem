#ifndef OIT_OUTLINE_UTILS_INCLUDED
#define OIT_OUTLINE_UTILS_INCLUDED

TEXTURE2D(_OITDepthTexture);
SAMPLER(sampler_OITDepthTexture);

bool SampleOITDepth(float2 uv, float z)
{
#if UNITY_REVERSED_Z
    return SAMPLE_TEXTURE2D(_OITDepthTexture, sampler_OITDepthTexture, uv).r > z;
#else
    return SAMPLE_TEXTURE2D(_OITDepthTexture, sampler_OITDepthTexture, uv).r < z;
#endif
}

#endif // OIT_OUTLINE_UTILS_INCLUDED