#ifndef OIT_UTILS_INCLUDED
#define OIT_UTILS_INCLUDED

// Unity's HLSL seems not to support dynamic array size, so we can only set this before compilation
#define MAX_SORTED_PIXELS 8
#define POW_2_24 16777216
#define HALF_MAX 65504.0

//https://github.com/GameTechDev/AOIT-Update/blob/master/OIT_DX11/AOIT%20Technique/AOIT.hlsl
// UnpackRGBA64 takes packed two uint values and converts them to float4 (RGBA16F).
float4 UnpackRGBA64(uint2 packedInput)
{
	uint4 packedHalf;
	packedHalf.x = packedInput.x & 0xFFFFUL;
	packedHalf.y = (packedInput.x >> 16UL) & 0xFFFFUL;
	packedHalf.z = packedInput.y & 0xFFFFUL;
	packedHalf.w = (packedInput.y >> 16UL) & 0xFFFFUL;

	return f16tof32(packedHalf);
}

// PackRGBA64 takes a half4 value and packs it into two UINTs (16 bits / channel).
uint2 PackRGBA64(half4 unpackedInput)
{
	float4 clampedInput;
	clampedInput.rgb = min(max(unpackedInput.rgb, 0.0), HALF_MAX);
	clampedInput.a = saturate(unpackedInput.a);

	uint4 packedHalf = f32tof16(clampedInput);
	uint2 packedOutput;
	packedOutput.x = (packedHalf.y << 16UL) | (packedHalf.x & 0xFFFFUL);
	packedOutput.y = (packedHalf.w << 16UL) | (packedHalf.z & 0xFFFFUL);
	return packedOutput;
}

float UnpackDepth(uint uDepthSampleIdx) {
	return (float)(uDepthSampleIdx >> 8UL) / (POW_2_24 - 1);
}

uint UnpackSampleIdx(uint uDepthSampleIdx) {
	return uDepthSampleIdx & 0xFFUL;
}

uint PackDepthSampleIdx(float depth, uint uSampleIdx) {
	uint d = (uint)(saturate(depth) * (POW_2_24 - 1));
	return d << 8UL | uSampleIdx;
}

#endif // OIT_UTILS_INCLUDED
