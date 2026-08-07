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
        private static readonly int FogSkyCoverageId =
            Shader.PropertyToID("_GHFogSkyCoverage");
        private static readonly int FogSkyFullCoverageId =
            Shader.PropertyToID("_GHFogSkyFullCoverage");
        private static readonly int FogSkyOpacityId =
            Shader.PropertyToID("_GHFogSkyOpacity");
        private static readonly int FogTerrainCoverageId =
            Shader.PropertyToID("_GHFogTerrainCoverage");
        private static readonly int FogTerrainConcealStartId =
            Shader.PropertyToID("_GHFogTerrainConcealStart");
        private static readonly int FogTerrainConcealFullId =
            Shader.PropertyToID("_GHFogTerrainConcealFull");
        private static readonly int FogTerrainConcealStrengthId =
            Shader.PropertyToID("_GHFogTerrainConcealStrength");
        private static readonly int FogNoiseStrengthId =
            Shader.PropertyToID("_GHFogNoiseStrength");
        private static readonly int FogNoiseScaleId =
            Shader.PropertyToID("_GHFogNoiseScale");
        private static readonly int FogNoiseSpeedId =
            Shader.PropertyToID("_GHFogNoiseSpeed");
        private static readonly int FogSkyMatchId =
            Shader.PropertyToID("_GHFogSkyMatch");
        private static readonly int FogSkyHorizonColorId =
            Shader.PropertyToID("_GHFogSkyHorizonColor");
        private static readonly int FogSkyZenithColorId =
            Shader.PropertyToID("_GHFogSkyZenithColor");
        private static readonly int FogSkyGradientBeginId =
            Shader.PropertyToID("_GHFogSkyGradientBegin");
        private static readonly int FogSkyGradientEndId =
            Shader.PropertyToID("_GHFogSkyGradientEnd");
        private static readonly int FogSkyDayCubeId =
            Shader.PropertyToID("_GHFogDayCube");
        private static readonly int FogSkyNightCubeId =
            Shader.PropertyToID("_GHFogNightCube");
        private static readonly int FogSkyDayCubeAvailableId =
            Shader.PropertyToID("_GHFogDayCubeAvailable");
        private static readonly int FogSkyNightCubeAvailableId =
            Shader.PropertyToID("_GHFogNightCubeAvailable");
        private static readonly int FogSkyGradientAvailableId =
            Shader.PropertyToID("_GHFogGradientAvailable");
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
        private static readonly int FogNightSkyScaleId =
            Shader.PropertyToID("_GHFogNightSkyScale");
        private static readonly int FogDayVerticalOffsetId =
            Shader.PropertyToID("_GHFogDayVerticalOffset");
        private static readonly int FogNightVerticalOffsetId =
            Shader.PropertyToID("_GHFogNightVerticalOffset");
        private static readonly int SourceGradientHorizonColorId =
            Shader.PropertyToID("_GradientHorizonColor");
        private static readonly int SourceGradientSkyColorId =
            Shader.PropertyToID("_GradientSkyColor");
        private static readonly int SourceGradientFadeBeginId =
            Shader.PropertyToID("_GradientFadeBegin");
        private static readonly int SourceGradientFadeEndId =
            Shader.PropertyToID("_GradientFadeEnd");
        private static readonly int SourceTintId = Shader.PropertyToID("_Tint");
        private static readonly int SourceExposureId =
            Shader.PropertyToID("_Exposure");
        private static readonly int SourceDayCubeId =
            Shader.PropertyToID("_DayTex");
        private static readonly int SourceNightCubeId =
            Shader.PropertyToID("_NightTex");
        private static readonly int SourceRotationId =
            Shader.PropertyToID("_Rotation");
        private static readonly int SourceDaySkyScaleId =
            Shader.PropertyToID("_DaySkyScale");
        private static readonly int SourceNightSkyScaleId =
            Shader.PropertyToID("_NightSkyScale");
        private static readonly int SourceDayVerticalOffsetId =
            Shader.PropertyToID("_DayVerticalOffset");
        private static readonly int SourceNightVerticalOffsetId =
            Shader.PropertyToID("_NightVerticalOffset");

        [SerializeField] private GHWorldDayNightSkyController skyController;

        [Header("Editor Preview")]
        [Tooltip("Enable only when the distance fog must also be previewed in Scene View.")]
        [SerializeField] private bool previewInSceneView;

        [Header("Day Fog")]
        [Tooltip("Soft neutral haze entering the daytime fog.")]
        [SerializeField] private Color dayNearFogColor =
            new Color(0.70f, 0.72f, 0.74f, 1f);
        [Tooltip("Main daytime haze color. Keep this low-saturation so distant objects feel misty instead of tinted.")]
        [SerializeField] private Color dayMidFogColor =
            new Color(0.76f, 0.75f, 0.78f, 1f);
        [Tooltip("Bright far haze with a subtle lavender cast.")]
        [SerializeField] private Color dayFarFogColor =
            new Color(0.82f, 0.78f, 0.84f, 1f);
        [Min(0f)]
        [SerializeField] private float dayFogStartDistance = 12f;
        [Min(0.01f)]
        [SerializeField] private float dayFogEndDistance = 110f;
        [Range(0f, 1f)]
        [SerializeField] private float dayFogMaxOpacity = 1f;
        [Tooltip("Values below 1 make daytime fog build up earlier in the middle distance.")]
        [Range(0.4f, 1.5f)]
        [SerializeField] private float dayFogDistancePower = 1f;

        [Header("Night Fog")]
        [Tooltip("Color entering the nighttime fog.")]
        [SerializeField] private Color nightNearFogColor =
            new Color(0.20f, 0.18f, 0.24f, 1f);
        [Tooltip("Main nighttime haze color.")]
        [SerializeField] private Color nightMidFogColor =
            new Color(0.32f, 0.28f, 0.38f, 1f);
        [Tooltip("Far nighttime haze. Bright enough to read as illuminated mist instead of a dark color filter.")]
        [SerializeField] private Color nightFarFogColor =
            new Color(0.52f, 0.44f, 0.58f, 1f);
        [Min(0f)]
        [SerializeField] private float nightFogStartDistance = 8f;
        [Min(0.01f)]
        [SerializeField] private float nightFogEndDistance = 90f;
        [Range(0f, 1f)]
        [SerializeField] private float nightFogMaxOpacity = 1f;
        [Tooltip("Values below 1 make nighttime fog build up earlier in the middle distance.")]
        [Range(0.4f, 1.5f)]
        [SerializeField] private float nightFogDistancePower = 1f;

        [Header("Gradient Shape")]
        [Tooltip("Normalized fog distance where the middle color is strongest.")]
        [Range(0.2f, 0.8f)]
        [SerializeField] private float middleColorPoint = 0.52f;

        [Header("Horizon Haze")]
        [Tooltip("Vertical thickness of haze around the world horizon in degrees.")]
        [Range(0.5f, 12f)]
        [SerializeField] private float horizonWidthDegrees = 6f;

        [Tooltip("Moves the horizon haze center up or down in world-space degrees.")]
        [Range(-5f, 5f)]
        [SerializeField] private float horizonVerticalOffsetDegrees;

        [Tooltip("Strength of the daytime band that softens the sea/sky boundary.")]
        [Range(0f, 1f)]
        [SerializeField] private float dayHorizonIntensity = 0.18f;

        [Tooltip("Strength of the nighttime band that softens the sea/sky boundary.")]
        [Range(0f, 1f)]
        [SerializeField] private float nightHorizonIntensity = 0.25f;

        [Header("Skybox Integration")]
        [Tooltip("Angle where the dense ground-fog bank finishes fading out. The skybox remains fully visible above this angle.")]
        [Range(2f, 45f)]
        [SerializeField] private float lowerSkyFogCoverageDegrees = 32f;

        [Tooltip("Angle below which the ground-fog bank is fully dense. Distant terrain inside this area disappears into the same fog layer as the horizon.")]
        [Range(0f, 40f)]
        [SerializeField] private float lowerSkyFogFullCoverageDegrees = 18f;

        [Tooltip("Maximum opacity of the lower fog bank. A value of 1 hides distant terrain silhouettes while leaving the upper skybox visible.")]
        [Range(0f, 1f)]
        [SerializeField] private float lowerSkyFogOpacity = 1f;

        [Header("Distant Terrain Concealment")]
        [Tooltip("Vertical range in which distant geometry is blended completely into the current sky. This does not cover skybox pixels.")]
        [Range(10f, 60f)]
        [SerializeField] private float terrainConcealmentCoverageDegrees = 42f;

        [Tooltip("Normalized fog distance where extra skyline concealment starts. Nearby gameplay objects remain visible below this distance.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float terrainConcealmentStart = 0.12f;

        [Tooltip("Normalized fog distance where distant terrain is fully replaced by the matching sky color.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float terrainConcealmentFull = 0.42f;

        [Tooltip("Strength of the depth-only distant terrain concealment.")]
        [Range(0f, 1f)]
        [SerializeField] private float terrainConcealmentStrength = 1f;

        [Header("Fog Variation")]
        [Tooltip("Subtle brightness variation shared by the sky veil and fully fogged terrain. This breaks up the flat color without revealing terrain silhouettes.")]
        [Range(0f, 0.15f)]
        [SerializeField] private float fogNoiseStrength = 0.045f;

        [Tooltip("Size of the broad fog patches. Lower values make larger, softer patches.")]
        [Range(2f, 20f)]
        [SerializeField] private float fogNoiseScale = 7f;

        [Tooltip("Very slow drift speed of the fog variation.")]
        [Range(0f, 0.1f)]
        [SerializeField] private float fogNoiseSpeed = 0.012f;

        [Header("Sky Matching")]
        [Tooltip("Automatically matches fully fogged terrain and the lower-sky veil to the current day and night skyboxes.")]
        [SerializeField] private bool matchProceduralSkyGradient = true;

        [Tooltip("Strength of automatic sky matching during day, night, and their transition.")]
        [Range(0f, 1f)]
        [SerializeField] private float proceduralSkyMatchStrength = 1f;

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
            Shader.SetGlobalFloat(
                FogSkyCoverageId,
                Mathf.Clamp(lowerSkyFogCoverageDegrees, 2f, 45f));
            Shader.SetGlobalFloat(
                FogSkyFullCoverageId,
                Mathf.Clamp(
                    lowerSkyFogFullCoverageDegrees,
                    0f,
                    lowerSkyFogCoverageDegrees - 0.5f));
            Shader.SetGlobalFloat(
                FogSkyOpacityId,
                Mathf.Clamp01(lowerSkyFogOpacity));
            Shader.SetGlobalFloat(
                FogTerrainCoverageId,
                Mathf.Clamp(terrainConcealmentCoverageDegrees, 10f, 60f));
            Shader.SetGlobalFloat(
                FogTerrainConcealStartId,
                Mathf.Clamp(terrainConcealmentStart, 0f, 0.9f));
            Shader.SetGlobalFloat(
                FogTerrainConcealFullId,
                Mathf.Clamp(
                    terrainConcealmentFull,
                    terrainConcealmentStart + 0.01f,
                    1f));
            Shader.SetGlobalFloat(
                FogTerrainConcealStrengthId,
                Mathf.Clamp01(terrainConcealmentStrength));
            Shader.SetGlobalFloat(
                FogNoiseStrengthId,
                Mathf.Clamp(fogNoiseStrength, 0f, 0.15f));
            Shader.SetGlobalFloat(
                FogNoiseScaleId,
                Mathf.Clamp(fogNoiseScale, 2f, 20f));
            Shader.SetGlobalFloat(
                FogNoiseSpeedId,
                Mathf.Clamp(fogNoiseSpeed, 0f, 0.1f));
            ApplySkyboxMatch(nightBlend);
        }

        private void ApplySkyboxMatch(float nightBlend)
        {
            Material currentSkybox = RenderSettings.skybox;
            Texture dayCube = GetCubeTexture(currentSkybox, SourceDayCubeId);
            Texture nightCube = GetCubeTexture(currentSkybox, SourceNightCubeId);
            bool hasDayCube = dayCube != null;
            bool hasNightCube = nightCube != null;
            bool hasGradientProperties = currentSkybox != null
                && currentSkybox.HasProperty(SourceGradientHorizonColorId)
                && currentSkybox.HasProperty(SourceGradientSkyColorId)
                && currentSkybox.HasProperty(SourceGradientFadeBeginId)
                && currentSkybox.HasProperty(SourceGradientFadeEndId)
                && (currentSkybox.IsKeywordEnabled("GRADIENT_BACKGROUND")
                    || !hasNightCube);

            if (!matchProceduralSkyGradient
                || (!hasDayCube && !hasNightCube && !hasGradientProperties))
            {
                Shader.SetGlobalFloat(FogSkyMatchId, 0f);
                return;
            }

            Color tint = currentSkybox.HasProperty(SourceTintId)
                ? currentSkybox.GetColor(SourceTintId)
                : Color.white;
            float exposure = currentSkybox.HasProperty(SourceExposureId)
                ? Mathf.Max(0f, currentSkybox.GetFloat(SourceExposureId))
                : 1f;

            Shader.SetGlobalFloat(
                FogSkyMatchId,
                Mathf.Clamp01(proceduralSkyMatchStrength));
            Shader.SetGlobalFloat(
                FogSkyDayCubeAvailableId,
                hasDayCube ? 1f : 0f);
            Shader.SetGlobalFloat(
                FogSkyNightCubeAvailableId,
                hasNightCube ? 1f : 0f);
            Shader.SetGlobalFloat(
                FogSkyGradientAvailableId,
                hasGradientProperties ? 1f : 0f);
            Shader.SetGlobalFloat(FogSkyNightBlendId, nightBlend);
            Shader.SetGlobalColor(FogSkyTintId, tint);
            Shader.SetGlobalFloat(FogSkyExposureId, exposure);

            if (hasDayCube)
            {
                Shader.SetGlobalTexture(FogSkyDayCubeId, dayCube);
            }

            if (hasNightCube)
            {
                Shader.SetGlobalTexture(FogSkyNightCubeId, nightCube);
            }

            if (hasGradientProperties)
            {
                Shader.SetGlobalColor(
                    FogSkyHorizonColorId,
                    currentSkybox.GetColor(SourceGradientHorizonColorId));
                Shader.SetGlobalColor(
                    FogSkyZenithColorId,
                    currentSkybox.GetColor(SourceGradientSkyColorId));
                Shader.SetGlobalFloat(
                    FogSkyGradientBeginId,
                    currentSkybox.GetFloat(SourceGradientFadeBeginId));
                Shader.SetGlobalFloat(
                    FogSkyGradientEndId,
                    currentSkybox.GetFloat(SourceGradientFadeEndId));
            }

            Shader.SetGlobalFloat(
                FogSkyRotationId,
                GetFloat(currentSkybox, SourceRotationId, 0f));
            Shader.SetGlobalFloat(
                FogDaySkyScaleId,
                GetFloat(currentSkybox, SourceDaySkyScaleId, 1f));
            Shader.SetGlobalFloat(
                FogNightSkyScaleId,
                GetFloat(currentSkybox, SourceNightSkyScaleId, 1f));
            Shader.SetGlobalFloat(
                FogDayVerticalOffsetId,
                GetFloat(currentSkybox, SourceDayVerticalOffsetId, 0f));
            Shader.SetGlobalFloat(
                FogNightVerticalOffsetId,
                GetFloat(currentSkybox, SourceNightVerticalOffsetId, 0f));
        }

        private static Texture GetCubeTexture(Material material, int propertyId)
        {
            if (material == null || !material.HasProperty(propertyId))
            {
                return null;
            }

            Texture texture = material.GetTexture(propertyId);
            return texture != null && texture.dimension == TextureDimension.Cube
                ? texture
                : null;
        }

        private static float GetFloat(
            Material material,
            int propertyId,
            float fallback)
        {
            return material != null && material.HasProperty(propertyId)
                ? material.GetFloat(propertyId)
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
            lowerSkyFogCoverageDegrees = Mathf.Clamp(
                lowerSkyFogCoverageDegrees,
                2f,
                45f);
            lowerSkyFogFullCoverageDegrees = Mathf.Clamp(
                lowerSkyFogFullCoverageDegrees,
                0f,
                lowerSkyFogCoverageDegrees - 0.5f);
            lowerSkyFogOpacity = Mathf.Clamp01(lowerSkyFogOpacity);
            terrainConcealmentCoverageDegrees = Mathf.Clamp(
                terrainConcealmentCoverageDegrees,
                10f,
                60f);
            terrainConcealmentStart = Mathf.Clamp(
                terrainConcealmentStart,
                0f,
                0.9f);
            terrainConcealmentFull = Mathf.Clamp(
                terrainConcealmentFull,
                terrainConcealmentStart + 0.01f,
                1f);
            terrainConcealmentStrength = Mathf.Clamp01(
                terrainConcealmentStrength);
            fogNoiseStrength = Mathf.Clamp(fogNoiseStrength, 0f, 0.15f);
            fogNoiseScale = Mathf.Clamp(fogNoiseScale, 2f, 20f);
            fogNoiseSpeed = Mathf.Clamp(fogNoiseSpeed, 0f, 0.1f);
            proceduralSkyMatchStrength =
                Mathf.Clamp01(proceduralSkyMatchStrength);
        }
    }
}
