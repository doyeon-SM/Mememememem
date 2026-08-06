/*
 * This file includes modifications to original work licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Modified by: mseon
 * Date: 2025-04-06
 * Changes:
 *   1. Change to use _ScreenSize
 */

#ifndef OIT_LINKED_LIST_INCLUDED
#define OIT_LINKED_LIST_INCLUDED

#include "./OitUtils.hlsl"

struct FragmentAndLinkBuffer_STRUCT
{
    uint pixelColorLo;
    uint pixelColorHi;
    uint uDepthSampleIdx;
    uint next;
};

float Linear01Depth_(float depth)
{
    return 1.0 / (_ZBufferParams.x * depth + _ZBufferParams.y);
}

RWStructuredBuffer<FragmentAndLinkBuffer_STRUCT> FLBuffer;
RWByteAddressBuffer StartOffsetBuffer;

void createFragmentEntry(half4 col, float3 pos, uint uSampleIdx) {
    //Retrieve current Pixel count and increase counter
    uint uPixelCount = FLBuffer.IncrementCounter();

    //calculate bufferAddress
    uint uStartOffsetAddress = 4 * (_ScreenSize.x * (pos.y - 0.5) + (pos.x - 0.5));
    uint uOldStartOffset;
    StartOffsetBuffer.InterlockedExchange(uStartOffsetAddress, uPixelCount, uOldStartOffset);

    //add new Fragment Entry in FragmentAndLinkBuffer
    FragmentAndLinkBuffer_STRUCT Element;
    uint2 packedColor = PackRGBA64(col);
    Element.pixelColorLo = packedColor.x;
    Element.pixelColorHi = packedColor.y;
    Element.uDepthSampleIdx = PackDepthSampleIdx(Linear01Depth_(pos.z), uSampleIdx);
    Element.next = uOldStartOffset;
    FLBuffer[uPixelCount] = Element;
}

#endif // OIT_LINKED_LIST_INCLUDED
