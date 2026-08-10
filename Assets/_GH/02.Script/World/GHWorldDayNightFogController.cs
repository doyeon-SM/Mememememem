using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace GH.World
{
    /// <summary>
    /// Drives the bounded full-screen distance fog used by the world renderer.
    /// Sky pixels are excluded in the shader and the maximum opacity is capped,
    /// so distant geometry keeps detail instead of becoming a solid cutout.
    /// </summary>
    [DefaultExecutionOrder(510)]
    [DisallowMultipleComponent]
    public sealed class GHWorldDayNightFogController : MonoBehaviour
    {
        private static readonly int FogEnabledId =
            Shader.PropertyToID("_GHDistanceFogEnabled");
        private static readonly int FogNearColorId =
            Shader.PropertyToID("_GHFogNearColor");
        private static readonly int FogMidColorId =
            Shader.PropertyToID("_GHFogMidColor");
        private static readonly int FogFarColorId =
            Shader.PropertyToID("_GHFogFarColor");
        private static readonly int FogStartDistanceId =
            Shader.PropertyToID("_GHFogStartDistance");
        private static readonly int FogEndDistanceId =
            Shader.PropertyToID("_GHFogEndDistance");
        private static readonly int FogMidPointId =
            Shader.PropertyToID("_GHFogMidPoint");
        private static readonly int FogMaxOpacityId =
            Shader.PropertyToID("_GHFogMaxOpacity");
        private static readonly int FogDistancePowerId =
            Shader.PropertyToID("_GHFogDistancePower");
        private static readonly int FogSkyMatchId =
            Shader.PropertyToID("_GHFogSkyMatch");
        private static readonly int FogDayCubeId =
            Shader.PropertyToID("_GHFogDayCube");
        private static readonly int FogDayCubeAvailableId =
            Shader.PropertyToID("_GHFogDayCubeAvailable");
        private static readonly int FogGradientAvailableId =
            Shader.PropertyToID("_GHFogGradientAvailable");
        private static readonly int FogGradientHorizonColorId =
            Shader.PropertyToID("_GHFogGradientHorizonColor");
        private static readonly int FogGradientSkyColorId =
            Shader.PropertyToID("_GHFogGradientSkyColor");
        private static readonly int FogGradientFadeBeginId =
            Shader.PropertyToID("_GHFogGradientFadeBegin");
        private static readonly int FogGradientFadeEndId =
            Shader.PropertyToID("_GHFogGradientFadeEnd");
        private static readonly int FogSkyNightBlendId =
            Shader.PropertyToID("_GHFogSkyNightBlend");
        private static readonly int FogSkyTintId =
            Shader.PropertyToID("_GHFogSkyTint");
        private static readonly int FogSkyExposureId =
            Shader.PropertyToID("_GHFogSkyExposure");
        private static readonly int FogSkyRotationId =
            Shader.PropertyToID("_GHFogSkyRotation");
        private static readonly int FogDaySkyScaleId =
            Shader.PropertyToID("_GHFogDaySkyScale");
        private static readonly int FogDayVerticalOffsetId =
            Shader.PropertyToID("_GHFogDayVerticalOffset");
        private static readonly int FogSkyHazeOpacityId =
            Shader.PropertyToID("_GHFogSkyHazeOpacity");
        private static readonly int FogSkyHazeHeightId =
            Shader.PropertyToID("_GHFogSkyHazeHeight");
        private static readonly int FogHorizonStrengthId =
            Shader.PropertyToID("_GHFogHorizonStrength");
        private static readonly int FogHorizonHeightId =
            Shader.PropertyToID("_GHFogHorizonHeight");
        private static readonly int FogHorizonColorInfluenceId =
            Shader.PropertyToID("_GHFogHorizonColorInfluence");

        private static readonly int SourceDayCubeId = Shader.PropertyToID("_DayTex");
        private static readonly int SourceGradientHorizonColorId =
            Shader.PropertyToID("_GradientHorizonColor");
        private static readonly int SourceGradientSkyColorId =
            Shader.PropertyToID("_GradientSkyColor");
        private static readonly int SourceGradientFadeBeginId =
            Shader.PropertyToID("_GradientFadeBegin");
        private static readonly int SourceGradientFadeEndId =
            Shader.PropertyToID("_GradientFadeEnd");
        private static readonly int SourceBlendId = Shader.PropertyToID("_Blend");
        private static readonly int SourceTintId = Shader.PropertyToID("_Tint");
        private static readonly int SourceExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int SourceRotationId = Shader.PropertyToID("_Rotation");
        private static readonly int SourceDaySkyScaleId =
            Shader.PropertyToID("_DaySkyScale");
        private static readonly int SourceDayVerticalOffsetId =
            Shader.PropertyToID("_DayVerticalOffset");

        [Header("Optional Day/Night Link")]
        [Tooltip("Optional. When assigned, fog colors follow the day/night blend. The fog still works independently when this is empty.")]
        [SerializeField] private GHWorldDayNightSkyController skyController;
        [Tooltip("Night blend used when no day/night sky controller is assigned. 0 is day and 1 is night.")]
        [Range(0f, 1f)]
        [SerializeField] private float fallbackNightBlend;

        [Header("Day Fog")]
        [SerializeField] private Color dayNearFogColor =
            new Color(0.62f, 0.70f, 0.78f, 1f);
        [SerializeField] private Color dayMidFogColor =
            new Color(0.58f, 0.67f, 0.76f, 1f);
        [SerializeField] private Color dayFarFogColor =
            new Color(0.54f, 0.62f, 0.72f, 1f);
        [Min(0f)]
        [SerializeField] private float dayFogStartDistance = 60f;
        [Min(0.01f)]
        [SerializeField] private float dayFogEndDistance = 500f;
        [Range(0f, 0.88f)]
        [SerializeField] private float dayFogMaxOpacity = 0.78f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float dayFogDistancePower = 0.82f;

        [Header("Night Fog")]
        [SerializeField] private Color nightNearFogColor =
            new Color(0.20f, 0.10f, 0.28f, 1f);
        [SerializeField] private Color nightMidFogColor =
            new Color(0.25f, 0.12f, 0.34f, 1f);
        [SerializeField] private Color nightFarFogColor =
            new Color(0.31f, 0.16f, 0.42f, 1f);
        [Min(0f)]
        [SerializeField] private float nightFogStartDistance = 50f;
        [Min(0.01f)]
        [SerializeField] private float nightFogEndDistance = 420f;
        [Range(0f, 0.88f)]
        [SerializeField] private float nightFogMaxOpacity = 0.82f;
        [Range(0.5f, 1.5f)]
        [SerializeField] private float nightFogDistancePower = 0.80f;

        [Header("Fog Shape")]
        [Tooltip("0 is near color, 1 is far color.")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float middleColorPoint = 0.52f;
        [Tooltip("Scales the final fog opacity without changing any fog colors. Lower this when the scene looks washed out.")]
        [Range(0f, 1f)]
        [SerializeField] private float fogStrength = 1f;

        [Header("Sky Boundary Blending")]
        [Tooltip("Matches distant terrain fog to the active skybox direction.")]
        [Range(0f, 1f)]
        [SerializeField] private float skyColorMatchStrength = 0.9f;
        [Tooltip("A subtle shared haze on the daytime sky near the horizon.")]
        [Range(0f, 0.35f)]
        [SerializeField] private float daySkyHazeOpacity = 0.12f;
        [Tooltip("A subtle shared haze on the nighttime sky near the horizon.")]
        [Range(0f, 0.35f)]
        [SerializeField] private float nightSkyHazeOpacity = 0.18f;
        [Tooltip("Height of the soft sky/terrain transition above the horizon.")]
        [Range(3f, 45f)]
        [SerializeField] private float skyHazeHeightDegrees = 24f;
        [Header("Horizon Fog Bank")]
        [Tooltip("Strength of the dedicated fog layer connecting distant water, terrain and the skybox horizon.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("horizonSurfaceBlend")]
        [SerializeField] private float horizonFogStrength = 0.65f;
        [Tooltip("Vertical angle over which the horizon fog layer fades out.")]
        [Range(1f, 20f)]
        [FormerlySerializedAs("horizonSurfaceDepthDegrees")]
        [SerializeField] private float horizonFogHeightDegrees = 10f;
        [Tooltip("How strongly Mid/Far Fog Color affects the horizon layer instead of using only the skybox color.")]
        [Range(0f, 1f)]
        [SerializeField] private float horizonFogColorInfluence = 0.35f;

        private bool originalFogEnabled;
        private FogMode originalFogMode;
        private Color originalFogColor;
        private float originalFogDensity;
        private float originalFogStartDistance;
        private float originalFogEndDistance;
        private bool originalStateCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetGlobalFogState()
        {
            Shader.SetGlobalFloat(FogEnabledId, 0f);
        }

        private void Reset()
        {
            skyController = GetComponent<GHWorldDayNightSkyController>();
        }

        private void OnEnable()
        {
            ResolveSkyController();
            CaptureOriginalState();
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            Shader.SetGlobalFloat(FogEnabledId, 0f);
            ApplyFogParameters();
        }

        private void LateUpdate()
        {
            ResolveSkyController();
            ApplyFogParameters();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            Shader.SetGlobalFloat(FogEnabledId, 0f);
            RestoreOriginalState();
        }

        private void HandleBeginCameraRendering(
            ScriptableRenderContext context,
            Camera camera)
        {
            bool shouldRender = isActiveAndEnabled
                && camera != null
                && camera.cameraType == CameraType.Game;
            Shader.SetGlobalFloat(FogEnabledId, shouldRender ? 1f : 0f);
        }

        private void ResolveSkyController()
        {
            if (skyController == null)
            {
                skyController = GetComponent<GHWorldDayNightSkyController>();
            }
        }

        private void CaptureOriginalState()
        {
            if (originalStateCaptured)
            {
                return;
            }

            originalFogEnabled = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            originalFogStartDistance = RenderSettings.fogStartDistance;
            originalFogEndDistance = RenderSettings.fogEndDistance;
            originalStateCaptured = true;
        }

        private void ApplyFogParameters()
        {
            float nightBlend = skyController != null
                ? Mathf.Clamp01(skyController.CurrentNightBlend)
                : Mathf.Clamp01(fallbackNightBlend);
            float startDistance = Mathf.Lerp(
                dayFogStartDistance,
                nightFogStartDistance,
                nightBlend);
            float endDistance = Mathf.Lerp(
                dayFogEndDistance,
                nightFogEndDistance,
                nightBlend);

            // The full-screen pass covers custom shaders consistently. Native
            // fog must stay off or compatible materials would receive fog twice.
            RenderSettings.fog = false;

            Shader.SetGlobalColor(
                FogNearColorId,
                Color.Lerp(dayNearFogColor, nightNearFogColor, nightBlend));
            Shader.SetGlobalColor(
                FogMidColorId,
                Color.Lerp(dayMidFogColor, nightMidFogColor, nightBlend));
            Shader.SetGlobalColor(
                FogFarColorId,
                Color.Lerp(dayFarFogColor, nightFarFogColor, nightBlend));
            Shader.SetGlobalFloat(FogStartDistanceId, Mathf.Max(0f, startDistance));
            Shader.SetGlobalFloat(
                FogEndDistanceId,
                Mathf.Max(startDistance + 0.01f, endDistance));
            Shader.SetGlobalFloat(
                FogMidPointId,
                Mathf.Clamp(middleColorPoint, 0.2f, 0.8f));
            Shader.SetGlobalFloat(
                FogMaxOpacityId,
                Mathf.Lerp(dayFogMaxOpacity, nightFogMaxOpacity, nightBlend)
                    * Mathf.Clamp01(fogStrength));
            Shader.SetGlobalFloat(
                FogDistancePowerId,
                Mathf.Lerp(dayFogDistancePower, nightFogDistancePower, nightBlend));
            Shader.SetGlobalFloat(
                FogSkyHazeOpacityId,
                Mathf.Lerp(daySkyHazeOpacity, nightSkyHazeOpacity, nightBlend));
            Shader.SetGlobalFloat(
                FogSkyHazeHeightId,
                Mathf.Clamp(skyHazeHeightDegrees, 3f, 45f));
            Shader.SetGlobalFloat(
                FogHorizonStrengthId,
                Mathf.Clamp01(horizonFogStrength));
            Shader.SetGlobalFloat(
                FogHorizonHeightId,
                Mathf.Clamp(horizonFogHeightDegrees, 1f, 20f));
            Shader.SetGlobalFloat(
                FogHorizonColorInfluenceId,
                Mathf.Clamp01(horizonFogColorInfluence));
            ApplySkyboxMatch(nightBlend);
        }

        private void ApplySkyboxMatch(float fallbackNightBlend)
        {
            Material skybox = RenderSettings.skybox;
            if (skybox == null)
            {
                Shader.SetGlobalFloat(FogSkyMatchId, 0f);
                return;
            }

            Texture dayCube = GetTexture(skybox, SourceDayCubeId);
            bool hasDayCube = dayCube != null
                && dayCube.dimension == TextureDimension.Cube;
            bool hasGradient = skybox.HasProperty(SourceGradientHorizonColorId)
                && skybox.HasProperty(SourceGradientSkyColorId)
                && skybox.HasProperty(SourceGradientFadeBeginId)
                && skybox.HasProperty(SourceGradientFadeEndId);

            if (!hasDayCube && !hasGradient)
            {
                Shader.SetGlobalFloat(FogSkyMatchId, 0f);
                return;
            }

            Shader.SetGlobalFloat(
                FogSkyMatchId,
                Mathf.Clamp01(skyColorMatchStrength));
            Shader.SetGlobalFloat(
                FogDayCubeAvailableId,
                hasDayCube ? 1f : 0f);
            Shader.SetGlobalFloat(
                FogGradientAvailableId,
                hasGradient ? 1f : 0f);

            if (hasDayCube)
            {
                Shader.SetGlobalTexture(FogDayCubeId, dayCube);
            }

            if (hasGradient)
            {
                Shader.SetGlobalColor(
                    FogGradientHorizonColorId,
                    skybox.GetColor(SourceGradientHorizonColorId));
                Shader.SetGlobalColor(
                    FogGradientSkyColorId,
                    skybox.GetColor(SourceGradientSkyColorId));
                Shader.SetGlobalFloat(
                    FogGradientFadeBeginId,
                    skybox.GetFloat(SourceGradientFadeBeginId));
                Shader.SetGlobalFloat(
                    FogGradientFadeEndId,
                    skybox.GetFloat(SourceGradientFadeEndId));
            }

            Shader.SetGlobalFloat(
                FogSkyNightBlendId,
                GetFloat(skybox, SourceBlendId, fallbackNightBlend));
            Shader.SetGlobalColor(
                FogSkyTintId,
                GetColor(skybox, SourceTintId, Color.white));
            Shader.SetGlobalFloat(
                FogSkyExposureId,
                Mathf.Max(0f, GetFloat(skybox, SourceExposureId, 1f)));
            Shader.SetGlobalFloat(
                FogSkyRotationId,
                GetFloat(skybox, SourceRotationId, 0f));
            Shader.SetGlobalFloat(
                FogDaySkyScaleId,
                Mathf.Max(0.001f, GetFloat(skybox, SourceDaySkyScaleId, 1f)));
            Shader.SetGlobalFloat(
                FogDayVerticalOffsetId,
                GetFloat(skybox, SourceDayVerticalOffsetId, 0f));
        }

        private static Texture GetTexture(Material material, int propertyId)
        {
            return material.HasProperty(propertyId)
                ? material.GetTexture(propertyId)
                : null;
        }

        private static float GetFloat(
            Material material,
            int propertyId,
            float fallback)
        {
            return material.HasProperty(propertyId)
                ? material.GetFloat(propertyId)
                : fallback;
        }

        private static Color GetColor(
            Material material,
            int propertyId,
            Color fallback)
        {
            return material.HasProperty(propertyId)
                ? material.GetColor(propertyId)
                : fallback;
        }

        private void RestoreOriginalState()
        {
            if (!originalStateCaptured)
            {
                return;
            }

            RenderSettings.fog = originalFogEnabled;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fogStartDistance = originalFogStartDistance;
            RenderSettings.fogEndDistance = originalFogEndDistance;
            originalStateCaptured = false;
        }

        private void OnValidate()
        {
            dayFogStartDistance = Mathf.Max(0f, dayFogStartDistance);
            dayFogEndDistance = Mathf.Max(
                dayFogStartDistance + 0.01f,
                dayFogEndDistance);
            dayFogMaxOpacity = Mathf.Clamp01(dayFogMaxOpacity);
            dayFogDistancePower = Mathf.Clamp(dayFogDistancePower, 0.5f, 1.5f);

            nightFogStartDistance = Mathf.Max(0f, nightFogStartDistance);
            nightFogEndDistance = Mathf.Max(
                nightFogStartDistance + 0.01f,
                nightFogEndDistance);
            nightFogMaxOpacity = Mathf.Clamp01(nightFogMaxOpacity);
            nightFogDistancePower = Mathf.Clamp(nightFogDistancePower, 0.5f, 1.5f);
            middleColorPoint = Mathf.Clamp(middleColorPoint, 0.2f, 0.8f);
            fogStrength = Mathf.Clamp01(fogStrength);
            skyColorMatchStrength = Mathf.Clamp01(skyColorMatchStrength);
            daySkyHazeOpacity = Mathf.Clamp(daySkyHazeOpacity, 0f, 0.35f);
            nightSkyHazeOpacity = Mathf.Clamp(nightSkyHazeOpacity, 0f, 0.35f);
            skyHazeHeightDegrees = Mathf.Clamp(skyHazeHeightDegrees, 3f, 45f);
            horizonFogStrength = Mathf.Clamp01(horizonFogStrength);
            horizonFogHeightDegrees = Mathf.Clamp(
                horizonFogHeightDegrees,
                1f,
                20f);
            horizonFogColorInfluence = Mathf.Clamp01(
                horizonFogColorInfluence);
        }
    }
}
