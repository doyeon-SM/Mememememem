using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PotaToon
{
    internal static class CharacterShadowUtils
    {
        private static List<VisibleLight> s_vSpotLights = new List<VisibleLight>(256);
        private static List<int> s_vSpotLightIndices = new List<int>(256);
        private static int[] s_SpotLightIndices = new int[2]; // Reusable array to prevent GC Alloc
        private static List<KeyValuePair<float, int>> s_SortedSpotLights = new List<KeyValuePair<float, int>>(256);
        private static Vector3 s_DefaultLightDirection = new Vector3(0.43f, 0.5f, -0.75f).normalized; // (30, -30, 0)
        
        private static VirtualShadowCamera s_ShadowCamera = new VirtualShadowCamera();
        internal static VirtualShadowCamera shadowCamera => s_ShadowCamera;
        internal static bool isCharShadowValid => shadowCamera.culledRendererCount > 0;

        public struct BrightestLightData
        {
            public Vector3 lightDirection;
            public int lightIndex;
            public bool isMainLight;
        }

        public class VirtualShadowCamera
        {
            public Renderer closestRenderer;
            public int              culledRendererCount => m_CulledRenderers.Count;
            public float            distanceCameraToNearestRenderer;
            public Vector3          lightDirectionOffset;
            public bool             overrideLightDirection;
            public float            maxBoundSize => Mathf.Max(m_TargetBounds.size.x, m_TargetBounds.size.y, m_TargetBounds.size.z);
            public float            maxScreenRimDistance;
            
            private Bounds          m_TargetBounds;
            private List<Renderer>  m_CulledRenderers = new List<Renderer>();
            private float           m_NearClipPlane = 0.01f;
            private float           m_FarClipPlane = 100f;
            private Vector3         m_Position;
            private Quaternion      m_Rotation = Quaternion.identity;
            private Vector3[]       m_BoundsCorners = new Vector3[8];
            
            private Vector3         m_PrevPosition;
            private Quaternion      m_PrevRotation;
            private bool            m_IsTweening;
            private int             m_PrevActiveCharacters;
            private int             m_TweenElapsedFrame;
            private const int       k_TweenTargetFrame = 7;
            private const float     k_RcpTweenTargetFrame = 1.0f / k_TweenTargetFrame;
            private Vector3         m_TweenDestPosition;
            private Quaternion      m_TweenDestRotation;
            
            public Matrix4x4 projectionMatrix => Matrix4x4.Perspective(45f, 1.0f, m_NearClipPlane, m_FarClipPlane);
            
            internal void Prepare(Camera camera, float cullingDistance)
            {
                m_TargetBounds = new Bounds();
                distanceCameraToNearestRenderer = 0f;
                
                if (PotaToonCharacter.activeRenderers == null)
                    return;
                
                m_FarClipPlane = GetCullingDistance(camera, cullingDistance);
                
                m_CulledRenderers.Clear();
                closestRenderer = null;
                distanceCameraToNearestRenderer = float.MaxValue;
                
                foreach (var renderer in PotaToonCharacter.activeRenderers)
                {
                    if (renderer != null)
                    {
                        if (IntersectTest(renderer, camera, out var dist))
                        {
                            if (dist < distanceCameraToNearestRenderer)
                            {
                                closestRenderer = renderer;
                                distanceCameraToNearestRenderer = dist;
                            }
                            m_CulledRenderers.Add(renderer);
                        }
                    }
                }
            }

            public Matrix4x4 GetViewMatrix()
            {
                var viewMatrix = Matrix4x4.TRS(m_Position, m_Rotation, Vector3.one).inverse;
                if (SystemInfo.usesReversedZBuffer)
                {
                    viewMatrix.m20 = -viewMatrix.m20;
                    viewMatrix.m21 = -viewMatrix.m21;
                    viewMatrix.m22 = -viewMatrix.m22;
                    viewMatrix.m23 = -viewMatrix.m23;
                }

                return viewMatrix;
            }
            
            public void UpdateCameraTransform(Light light)
            {
                var currPosition = m_Position;
                var currRotation = m_Rotation;
                
                // Ignore z axis since z+ axis in light is used for projection.
                var eulerAngles = light.transform.rotation.eulerAngles;
                eulerAngles.z = 0f;
                m_Rotation = overrideLightDirection ? Quaternion.Euler(lightDirectionOffset) : Quaternion.Euler(eulerAngles + lightDirectionOffset);
                
                if (culledRendererCount <= 0)
                    return;

                m_TargetBounds = new Bounds();
                
                // Initialize
                var firstRotatedBoundsCorners = GetAABBCorners(m_CulledRenderers[0].bounds, m_Rotation);
                m_TargetBounds.min = firstRotatedBoundsCorners[0];
                m_TargetBounds.max = firstRotatedBoundsCorners[0];
                foreach (var point in firstRotatedBoundsCorners)
                    m_TargetBounds.Encapsulate(point);
                
                for (int i = 1; i < m_CulledRenderers.Count; i++)
                {
                    foreach (var point in GetAABBCorners(m_CulledRenderers[i].bounds, m_Rotation))
                        m_TargetBounds.Encapsulate(point);
                }
                
                // 1. Calculate position to cover all renderers
                var dir = m_Rotation * Vector3.forward;
                var targetBoundsExtents = m_TargetBounds.extents;
                var dest = m_TargetBounds.center;
                var maxXY = Mathf.Max(targetBoundsExtents.x, targetBoundsExtents.y);
                var distance = Mathf.Max(maxXY, targetBoundsExtents.z) + maxXY * 2.0f;
                var offset = -dir * distance;

                m_Position = dest + offset;

#if UNITY_EDITOR
                if (PotaToon.guideWarningEnabled && (m_TargetBounds.size.y < 0.2f || m_TargetBounds.size.y > 3.0f))
                {
                    Debug.LogWarning("<color=#FFDD80>[PotaToon] It looks like the size of the characters is either too small or too big. We recommend that the character's height is greater than 0.5 meters and less than 3 meters (Unity units). Check the Scale Factor in the FBX import settings, the Transform Scale, or the Bounds Size of the Mesh Renderers.\nTo disable this warning, click 'Toolbar/PotaToon/Toggle Debug Warning'. </color>");
                }
#endif
                
                // 2. If number of active character has changed, update the view matrix immediately.
                if (!Application.isPlaying || PotaToonCharacter.activeCharacters != m_PrevActiveCharacters || PotaToonCharacter.activeCharacters == 1)
                {
                    m_PrevActiveCharacters = PotaToonCharacter.activeCharacters;
                    ResetTweenVariables(m_Position, m_Rotation);
                    return;
                }

                // 3. Otherwise, soft lerp
                const float positionThreshold = 1.0f;
                const float rotationThreshold = 5.0f;
                if (m_IsTweening)
                {
                    bool suddenMovement = Vector3.Distance(m_TweenDestPosition, m_Position) > positionThreshold || Quaternion.Angle(m_TweenDestRotation, m_Rotation) > rotationThreshold;
                    if (suddenMovement)
                        ResetTweenVariables(currPosition, currRotation);
                }
                
                if (!m_IsTweening)
                {
                    m_TweenDestPosition = m_Position;
                    m_TweenDestRotation = m_Rotation;
                    m_Position = m_PrevPosition;
                    m_Rotation = m_PrevRotation;
                    m_TweenElapsedFrame = 0;
                    m_IsTweening = true;
                }
                
                TweenTransformIfNeeded();
                if (m_TweenElapsedFrame >= k_TweenTargetFrame)
                    ResetTweenVariables(m_Position, m_Rotation);
            }

            private void ResetTweenVariables(Vector3 prevPosition, Quaternion prevRotation)
            {
                m_TweenElapsedFrame = 0;
                m_IsTweening = false;
                m_PrevPosition = prevPosition;
                m_PrevRotation = prevRotation;
            }

            private void TweenTransformIfNeeded()
            {
                if (m_IsTweening && m_TweenElapsedFrame < k_TweenTargetFrame)
                {
                    var t = Mathf.Clamp01(m_TweenElapsedFrame * k_RcpTweenTargetFrame);

                    m_Position = Vector3.Lerp(m_PrevPosition, m_TweenDestPosition, t);
                    m_Rotation = Quaternion.Lerp(m_PrevRotation, m_TweenDestRotation, t);

                    m_TweenElapsedFrame++;
                }
            }

            private Vector3[] GetAABBCorners(Bounds aabb, Quaternion rotation)
            {
                var corners = m_BoundsCorners;
                Vector3 extents = aabb.extents;

                corners[0] = new Vector3(extents.x, extents.y, extents.z);
                corners[1] = new Vector3(extents.x, extents.y, -extents.z);
                corners[2] = new Vector3(extents.x, -extents.y, extents.z);
                corners[3] = new Vector3(extents.x, -extents.y, -extents.z);
                corners[4] = new Vector3(-extents.x, extents.y, extents.z);
                corners[5] = new Vector3(-extents.x, extents.y, -extents.z);
                corners[6] = new Vector3(-extents.x, -extents.y, extents.z);
                corners[7] = new Vector3(-extents.x, -extents.y, -extents.z);

                for (int i = 0; i < 8; i++)
                {
                    corners[i] = rotation * corners[i] + aabb.center;
                }

                Vector3 min = corners[0];
                Vector3 max = corners[0];
                for (int i = 1; i < 8; i++)
                {
                    min = Vector3.Min(min, corners[i]);
                    max = Vector3.Max(max, corners[i]);
                }
                return corners;
            }

            // Frustum Culling + Distance Culling
            private bool IntersectTest(Renderer renderer, Camera camera, out float dist)
            {
                var bounds = renderer.bounds;
                var cameraPosition = camera.transform.position;
                dist = Vector3.Distance(cameraPosition, bounds.center);
                if (dist > m_FarClipPlane)
                    return false;

                var originalFov = camera.fieldOfView;
                camera.fieldOfView = Mathf.Min(179f, originalFov * 1.2f);
                var frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
                camera.fieldOfView = originalFov;
                var intersected = GeometryUtility.TestPlanesAABB(frustumPlanes, bounds);
                return intersected;
            }
        }
        
        internal static bool IfCharShadowUpdateNeeded(in RenderingData renderingData, float cullingDistance)
        {
            shadowCamera.Prepare(renderingData.cameraData.camera, cullingDistance);
            
            return isCharShadowValid;
        }

        internal static float GetCullingDistance(Camera camera, float cullingDistance)
        {
            // [Max FOV / current FOV(1-e5 to 179)]
            var dist = 179f / camera.fieldOfView;
            
            // Set the screen rim range to max.
            var maxDistance = 4.0f * dist; 
            shadowCamera.maxScreenRimDistance = maxDistance;
            
            // Always set to max if there's only one active character.
            if (PotaToonCharacter.activeCharacters <= 1)
                return maxDistance;
            return 2.0f * cullingDistance * dist;
        }
        
        private static void GetBrightestLightData_Internal(ref NativeArray<VisibleLight> visibleLights, int mainLightIndex, VirtualShadowCamera shadowCamera, bool useBrighestLight, LayerMask followLightLayer, ref BrightestLightData data)
        {
            data.isMainLight = true;
            s_SpotLightIndices[0] = s_SpotLightIndices[1] = -1;
            var spotLightIndices = s_SpotLightIndices;
            var jobSucceed = CalculateMostIntensiveLightIndices(ref visibleLights, mainLightIndex, followLightLayer, spotLightIndices);
            var overridenLightDirection = Quaternion.Euler(shadowCamera.lightDirectionOffset) * Vector3.back;
            if (!useBrighestLight || !jobSucceed || (spotLightIndices[0] < 0 && spotLightIndices[1] < 0) || shadowCamera.closestRenderer == null)
            {
                if (mainLightIndex != -1)
                {
                    shadowCamera.UpdateCameraTransform(visibleLights[mainLightIndex].light);
                    data.lightDirection = shadowCamera.overrideLightDirection ? overridenLightDirection : -(Quaternion.Euler(shadowCamera.lightDirectionOffset) * visibleLights[mainLightIndex].light.transform.forward);
                }
                else
                {
                    data.lightDirection = s_DefaultLightDirection;
                }
                data.lightIndex = mainLightIndex;
                return;
            }

            var lightCount = visibleLights.Length;
            var lightOffset = 0;
            while (lightOffset < lightCount && visibleLights[lightOffset].lightType == LightType.Directional)
            {
                lightOffset++;
            }
            var hasMainLight = 0;

            float mainLightStrength = 0f;
            int brightestLightIndex = -1;
            int brightestSpotLightIndex = -1;
            var brightestLightDirection = s_DefaultLightDirection;

            // Find stronger light among mainLight & brightest spot light
            if (mainLightIndex != -1 && lightOffset != 0)
            {
                hasMainLight = 1;
                brightestSpotLightIndex = spotLightIndices[0] + hasMainLight;
                brightestLightIndex = mainLightIndex;
                
                var mainLightColor = visibleLights[mainLightIndex].finalColor;
                mainLightStrength = mainLightColor.r * 0.299f + mainLightColor.g * 0.587f + mainLightColor.b * 0.114f;
            }
            else
            {
                if (spotLightIndices[0] >= 0)
                {
                    brightestLightIndex = brightestSpotLightIndex = spotLightIndices[0] + hasMainLight;
                }
                else
                {
                    brightestLightIndex = brightestSpotLightIndex = spotLightIndices[1] + hasMainLight;
                }
            }

            // Replace with the brightest spot light
            if (brightestSpotLightIndex >= 0)
            {
                var spotLight = visibleLights[brightestSpotLightIndex].light;
                var target = PotaToonCharacter.headFromActiveRenderers[shadowCamera.closestRenderer];
                var dest = target != null ? target.position : shadowCamera.closestRenderer.bounds.center;
                var distance = (dest - spotLight.transform.position).magnitude;
                var atten = 1f - distance / spotLight.range;
                var brightestSpotLightColor = visibleLights[brightestSpotLightIndex].finalColor;
                var brightestSpotLightStrength = (brightestSpotLightColor.r * 0.299f + brightestSpotLightColor.g * 0.587f + brightestSpotLightColor.b * 0.114f) * atten * Mathf.Cos(spotLight.spotAngle * Mathf.Deg2Rad);
                // Mainlight weight = 10
                if (hasMainLight == 1 && mainLightStrength * 10f >= brightestSpotLightStrength)
                {
                    brightestLightIndex = mainLightIndex;
                }
                else
                {
                    brightestLightIndex = brightestSpotLightIndex;
                    data.isMainLight = false;
                }
            }

            // Update Light Camera transform (Main or Brighest light)
            if (brightestLightIndex >= 0 && brightestLightIndex < visibleLights.Length)
            {
                shadowCamera.UpdateCameraTransform(visibleLights[brightestLightIndex].light);
                brightestLightDirection = shadowCamera.overrideLightDirection ? overridenLightDirection : -(Quaternion.Euler(shadowCamera.lightDirectionOffset) * visibleLights[brightestLightIndex].light.transform.forward);
            }
            
            data.lightDirection = brightestLightDirection;
            data.lightIndex = brightestSpotLightIndex >= 0 ? brightestSpotLightIndex - hasMainLight : -1;
        }

        ///<returns>
        /// [0]: FollowLight, [1]: Additional SpotLight
        ///</returns>
        private static bool CalculateMostIntensiveLightIndices(ref NativeArray<VisibleLight> visibleLights, int mainLightIndex, LayerMask followLayer, int[] charSpotLightIndices)
        {
            if (!isCharShadowValid || shadowCamera.closestRenderer == null)
            {
                return false;
            }
            
            var lightCount = visibleLights.Length;
            var lightOffset = 0;
            while (lightOffset < lightCount && visibleLights[lightOffset].lightType == LightType.Directional)
            {
                lightOffset++;
            }
            lightCount -= lightOffset;
            var directionalLightCount = lightOffset;
            if (mainLightIndex != -1 && directionalLightCount != 0) directionalLightCount -= 1;
            var subVisibleLights = visibleLights.GetSubArray(lightOffset, lightCount);

            s_vSpotLights.Clear();
            s_vSpotLightIndices.Clear();
            s_SortedSpotLights.Clear();
            
            // Extract spot lights
            for (int i = 0; i < subVisibleLights.Length; i++)
            {
                if (subVisibleLights[i].lightType == LightType.Spot)
                {
                    s_vSpotLightIndices.Add(i + directionalLightCount);
                    s_vSpotLights.Add(subVisibleLights[i]);
                }
            }

            // Calculate light intensity
            for (int i = 0; i < s_vSpotLights.Count; i++)
            {
                var light = s_vSpotLights[i].light;
                var target = PotaToonCharacter.headFromActiveRenderers[shadowCamera.closestRenderer];
                var dest = target != null ? target.position : shadowCamera.closestRenderer.bounds.center;
                var diff = dest - light.transform.position;
                var dirToTarget = Vector3.Normalize(diff);
                var L = light.transform.rotation * Vector3.forward;
                var dotL = Vector3.Dot(dirToTarget, L);
                var distance = diff.magnitude;
                var cos = Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad);
                if (dotL <= cos || distance > light.range)
                {
                    continue;
                }

                var finalColor = s_vSpotLights[i].finalColor;
                var atten = 1f - distance / light.range;
                var strength = (finalColor.r * 0.229f + finalColor.g * 0.587f + finalColor.b * 0.114f) * atten * cos;
                if (strength > 0.01f)
                {
                    s_SortedSpotLights.Add(new KeyValuePair<float, int>(strength, s_vSpotLightIndices[i]));
                }
            }
            // Sort
            s_SortedSpotLights.Sort((x, y) => y.Key.CompareTo(x.Key));
            
            for (int i = 0; i < s_SortedSpotLights.Count; i++)
            {
                var curr = s_SortedSpotLights[i].Value;
                if (curr < subVisibleLights.Length && (followLayer.value & (int)Mathf.Pow(2, subVisibleLights[curr].light.gameObject.layer)) > 0)
                {
                    if (charSpotLightIndices[0] < 0)
                        charSpotLightIndices[0] = curr;
                }
                else
                {
                    if (charSpotLightIndices[1] < 0)
                        charSpotLightIndices[1] = curr;
                }
            }

            return true;
        }
        
        /// <summary>
        /// 1. if (useBrighestLightOnly == true) : LightIndex = spot or mainLight.
        /// 2. if (useBrighestLightOnly == false) : LightIndex = mainLight
        /// </summary>
        internal static void GetBrightestLightData(ref RenderingData renderingData, bool useBrightestLight, LayerMask followLightLayer, out BrightestLightData data)
        {
            data = new BrightestLightData();
            GetBrightestLightData_Internal(ref renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, shadowCamera, useBrightestLight, followLightLayer, ref data);
        }
        
#if UNITY_6000_0_OR_NEWER
#region RenderGraph
        /// <summary>
        /// 1. if (useBrighestLightOnly == true) : LightIndex = spot or mainLight.
        /// 2. if (useBrighestLightOnly == false) : LightIndex = mainLight
        /// </summary>
        internal static void GetBrightestLightData(UniversalLightData lightData, bool useBrightestLight, LayerMask followLightLayer, out BrightestLightData data)
        {
            data = new BrightestLightData();
            GetBrightestLightData_Internal(ref lightData.visibleLights, lightData.mainLightIndex, shadowCamera, useBrightestLight, followLightLayer, ref data);
        }
#endregion
#endif
    }
}
