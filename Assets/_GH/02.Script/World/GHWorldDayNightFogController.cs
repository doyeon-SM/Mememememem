using UnityEngine;
using UnityEngine.Rendering;

namespace GH.World
{
    /// <summary>
    /// Controls the world's distance fog independently from the sky controller.
    /// Attach this component only to scenes that need fog.
    /// </summary>
    [DefaultExecutionOrder(510)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GHWorldDayNightSkyController))]
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
        private static readonly int FogHorizonWidthId =
            Shader.PropertyToID("_GHFogHorizonWidth");
        private static readonly int FogHorizonOffsetId =
            Shader.PropertyToID("_GHFogHorizonOffset");
        private static readonly int FogHorizonIntensityId =
            Shader.PropertyToID("_GHFogHorizonIntensity");

        [SerializeField] private GHWorldDayNightSkyController skyController;

        [Header("Editor Preview")]
        [Tooltip("Enable only when the distance fog must also be previewed in Scene View.")]
        [SerializeField] private bool previewInSceneView;

        [Header("Day Fog")]
        [Tooltip("Color entering the daytime fog. Sampled from the lower blue sky.")]
        [SerializeField] private Color dayNearFogColor =
            new Color(0.105f, 0.342f, 0.584f, 1f);
        [Tooltip("Main daytime haze color.")]
        [SerializeField] private Color dayMidFogColor =
            new Color(0.227f, 0.497f, 0.723f, 1f);
        [Tooltip("Far daytime haze color. Sampled from the bright horizon.")]
        [SerializeField] private Color dayFarFogColor =
            new Color(0.539f, 0.716f, 0.839f, 1f);
        [Min(0f)]
        [SerializeField] private float dayFogStartDistance = 65f;
        [Min(0.01f)]
        [SerializeField] private float dayFogEndDistance = 520f;
        [Range(0f, 1f)]
        [SerializeField] private float dayFogMaxOpacity = 0.88f;
        [Tooltip("Values below 1 make daytime fog build up earlier in the middle distance.")]
        [Range(0.4f, 1.5f)]
        [SerializeField] private float dayFogDistancePower = 0.78f;

        [Header("Night Fog")]
        [Tooltip("Color entering the nighttime fog.")]
        [SerializeField] private Color nightNearFogColor =
            new Color(0.0056f, 0.0103f, 0.0319f, 1f);
        [Tooltip("Main nighttime haze color.")]
        [SerializeField] private Color nightMidFogColor =
            new Color(0.0137f, 0.0273f, 0.0823f, 1f);
        [Tooltip("Far nighttime haze color. Sampled from the moonlit cloud horizon.")]
        [SerializeField] private Color nightFarFogColor =
            new Color(0.0343f, 0.0666f, 0.159f, 1f);
        [Min(0f)]
        [SerializeField] private float nightFogStartDistance = 50f;
        [Min(0.01f)]
        [SerializeField] private float nightFogEndDistance = 400f;
        [Range(0f, 1f)]
        [SerializeField] private float nightFogMaxOpacity = 0.92f;
        [Tooltip("Values below 1 make nighttime fog build up earlier in the middle distance.")]
        [Range(0.4f, 1.5f)]
        [SerializeField] private float nightFogDistancePower = 0.72f;

        [Header("Gradient Shape")]
        [Tooltip("Normalized fog distance where the middle color is strongest.")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float middleColorPoint = 0.52f;

        [Header("Horizon Haze")]
        [Tooltip("Vertical thickness of haze around the world horizon in degrees.")]
        [Range(0.5f, 12f)]
        [SerializeField] private float horizonWidthDegrees = 4.5f;

        [Tooltip("Moves the horizon haze center up or down in world-space degrees.")]
        [Range(-5f, 5f)]
        [SerializeField] private float horizonVerticalOffsetDegrees;

        [Tooltip("Strength of the daytime band that softens the sea/sky boundary.")]
        [Range(0f, 1f)]
        [SerializeField] private float dayHorizonIntensity = 0.58f;

        [Tooltip("Strength of the nighttime band that softens the sea/sky boundary.")]
        [Range(0f, 1f)]
        [SerializeField] private float nightHorizonIntensity = 0.66f;

        private bool originalFogEnabled;
        private FogMode originalFogMode;
        private Color originalFogColor;
        private float originalFogDensity;
        private float originalFogStartDistance;
        private float originalFogEndDistance;
        private bool originalStateCaptured;

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
            ApplyFog();
        }

        private void Update()
        {
            ResolveSkyController();
            ApplyFog();
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
            bool isGameCamera = camera != null
                && camera.cameraType == CameraType.Game;
            bool isAllowedSceneView = previewInSceneView
                && camera != null
                && camera.cameraType == CameraType.SceneView;
            Shader.SetGlobalFloat(
                FogEnabledId,
                isActiveAndEnabled
                    && skyController != null
                    && (isGameCamera || isAllowedSceneView)
                    ? 1f
                    : 0f);
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

        private void ApplyFog()
        {
            if (skyController == null)
            {
                Shader.SetGlobalFloat(FogEnabledId, 0f);
                return;
            }

            float nightBlend = Mathf.Clamp01(skyController.CurrentNightBlend);
            float startDistance = Mathf.Lerp(
                Mathf.Max(0f, dayFogStartDistance),
                Mathf.Max(0f, nightFogStartDistance),
                nightBlend);
            float endDistance = Mathf.Lerp(
                Mathf.Max(dayFogStartDistance + 0.01f, dayFogEndDistance),
                Mathf.Max(nightFogStartDistance + 0.01f, nightFogEndDistance),
                nightBlend);

            // The built-in fog only supports one color. Disable it while the
            // full-screen distance gradient is active to avoid double fogging.
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
            Shader.SetGlobalFloat(FogStartDistanceId, startDistance);
            Shader.SetGlobalFloat(
                FogEndDistanceId,
                Mathf.Max(startDistance + 0.01f, endDistance));
            Shader.SetGlobalFloat(
                FogMidPointId,
                Mathf.Clamp(middleColorPoint, 0.2f, 0.8f));
            Shader.SetGlobalFloat(
                FogMaxOpacityId,
                Mathf.Lerp(
                    Mathf.Clamp01(dayFogMaxOpacity),
                    Mathf.Clamp01(nightFogMaxOpacity),
                    nightBlend));
            Shader.SetGlobalFloat(
                FogDistancePowerId,
                Mathf.Lerp(
                    Mathf.Clamp(dayFogDistancePower, 0.4f, 1.5f),
                    Mathf.Clamp(nightFogDistancePower, 0.4f, 1.5f),
                    nightBlend));
            Shader.SetGlobalFloat(
                FogHorizonWidthId,
                Mathf.Clamp(horizonWidthDegrees, 0.5f, 12f));
            Shader.SetGlobalFloat(
                FogHorizonOffsetId,
                Mathf.Clamp(horizonVerticalOffsetDegrees, -5f, 5f));
            Shader.SetGlobalFloat(
                FogHorizonIntensityId,
                Mathf.Lerp(
                    Mathf.Clamp01(dayHorizonIntensity),
                    Mathf.Clamp01(nightHorizonIntensity),
                    nightBlend));
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
            nightFogStartDistance = Mathf.Max(0f, nightFogStartDistance);
            nightFogEndDistance = Mathf.Max(
                nightFogStartDistance + 0.01f,
                nightFogEndDistance);
            dayFogMaxOpacity = Mathf.Clamp01(dayFogMaxOpacity);
            nightFogMaxOpacity = Mathf.Clamp01(nightFogMaxOpacity);
            dayFogDistancePower = Mathf.Clamp(dayFogDistancePower, 0.4f, 1.5f);
            nightFogDistancePower = Mathf.Clamp(nightFogDistancePower, 0.4f, 1.5f);
            middleColorPoint = Mathf.Clamp(middleColorPoint, 0.2f, 0.8f);
            horizonWidthDegrees = Mathf.Clamp(horizonWidthDegrees, 0.5f, 12f);
            horizonVerticalOffsetDegrees = Mathf.Clamp(
                horizonVerticalOffsetDegrees,
                -5f,
                5f);
            dayHorizonIntensity = Mathf.Clamp01(dayHorizonIntensity);
            nightHorizonIntensity = Mathf.Clamp01(nightHorizonIntensity);
        }
    }
}
