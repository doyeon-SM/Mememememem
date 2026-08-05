using UnityEngine;
using UnityEngine.Rendering;

namespace PotaToon
{
    [GenerateHLSL(needAccessors = false, generateCBuffer = true)]
    public struct ToonCharacterShadow
    {
        public Vector4 _CharShadowParams;
        public Matrix4x4 _CharShadowViewProjM;
        public Vector4 _CharShadowOffset0;
        public Vector4 _CharShadowOffset1;
        public Vector4 _CharShadowmapSize;              // rcp(width), rcp(height), width, height
        public Vector4 _CharTransparentShadowmapSize;   // rcp(width), rcp(height), width, height
        public Vector4 _CharShadowCascadeParams;        // x: cascadeMaxDistance, y: cascadeResolutionScale
        public Vector4 _BrightestLightDirection;
        public uint _BrightestLightIndex;
        public uint _UseBrightestLight;
        public uint _IsBrightestLightMain;
        public float _MaxToonBrightness;
    }
}