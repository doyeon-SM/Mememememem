using HDY.Territory;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GH.World
{
    /// <summary>
    /// GameTimeManager의 공개 시간 값을 읽어 하늘, 주광, 환경광을 연출합니다.
    /// 시간의 누적·저장·날짜 변경에는 관여하지 않습니다.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public sealed class GHWorldDayNightSkyController : MonoBehaviour
    {
        private const string DefaultDaySkyboxPath =
            "Assets/_GH/05.Prefeb/AssetsMesh/GH_DayNightSky/GH_DaySky.mat";
        private const string DefaultNightSkyboxPath =
            "Assets/_GH/05.Prefeb/AssetsMesh/GH_DayNightSky/GH_NightSky.mat";
        private const string BlendShaderName = "GH/Skybox/Cubemap Blend";
        private const string StarryNightBlendShaderName =
            "GH/Skybox/Cubemap Starry Night Blend";
        private const string DynamicStarrySkyShaderName = "Funly/Sky/StarrySky";

        private static readonly int DayTextureId = Shader.PropertyToID("_DayTex");
        private static readonly int NightTextureId = Shader.PropertyToID("_NightTex");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int RotationId = Shader.PropertyToID("_Rotation");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int DaySkyScaleId = Shader.PropertyToID("_DaySkyScale");
        private static readonly int NightSkyScaleId = Shader.PropertyToID("_NightSkyScale");
        private static readonly int DayVerticalOffsetId =
            Shader.PropertyToID("_DayVerticalOffset");
        private static readonly int NightVerticalOffsetId =
            Shader.PropertyToID("_NightVerticalOffset");
        private static readonly int DayLowerSkyColorId =
            Shader.PropertyToID("_DayLowerSkyColor");
        private static readonly int LowerSkyWorldFadeId =
            Shader.PropertyToID("_LowerSkyWorldFade");
        private static readonly int LowerSkySourceFadeId =
            Shader.PropertyToID("_LowerSkySourceFade");
        private static readonly int SourceCubemapId = Shader.PropertyToID("_Tex");
        private static readonly int SourceMainTextureId = Shader.PropertyToID("_MainTex");

        [Header("읽기 전용 시간 연결")]
        [Tooltip(
            "현재 월드 시간을 읽어올 GameTimeManager입니다. 비워 두면 실행 중 Instance 또는 씬에서 자동으로 찾습니다. " +
            "이 컴포넌트는 DayLengthSeconds, InGameTimeOfDaySeconds, ElapsedTime만 읽으며 값을 변경하지 않습니다.")]
        [SerializeField] private GameTimeManager gameTimeManager;

        [Tooltip(
            "켜면 GameTimeManager 대신 아래 Debug Normalized Time으로 하늘만 미리 확인합니다. " +
            "월드 시간 자체는 바뀌지 않으므로 낮·밤 색상과 블렌딩 곡선을 테스트할 때 사용합니다.")]
        [SerializeField] private bool useDebugTimeOverride;

        [Tooltip(
            "디버그 시간입니다. 0은 하루 시작, 0.25는 하루의 25%, 0.5는 절반, 1은 다음 날 시작 직전입니다. " +
            "Use Debug Time Override가 켜진 동안에만 사용됩니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float debugNormalizedTime;

        [Tooltip(
            "GameTimeManager의 하루 진행도에 더할 시간 오프셋입니다. " +
            "예를 들어 게임 시간이 0일 때 하늘을 조금 더 아침 쪽으로 옮기고 싶다면 작은 양수 값을 사용합니다.")]
        [Range(-1f, 1f)]
        [SerializeField] private float normalizedTimeOffset;

        [Header("스카이박스 블렌딩")]
        [Tooltip(
            "낮에 사용할 원본 스카이박스 머터리얼입니다. 원본은 수정하지 않으며, 머터리얼의 _Tex Cubemap만 읽어 " +
            "실행 중 생성한 블렌딩 머터리얼에 연결합니다.")]
        [SerializeField] private Material daySkyboxSource;

        [Tooltip(
            "밤에 사용할 원본 스카이박스 머터리얼입니다. 별과 달이 포함된 AllSky Anime Night가 기본 선택입니다. " +
            "낮 머터리얼과 마찬가지로 원본 머터리얼 값은 변경하지 않습니다.")]
        [SerializeField] private Material nightSkyboxSource;

        [Tooltip(
            "낮 Cubemap과 밤 Cubemap을 실제로 섞는 GH 전용 스카이박스 셰이더입니다. " +
            "비워 두면 실행 중 'GH/Skybox/Cubemap Blend' 셰이더를 자동으로 찾습니다.")]
        [SerializeField] private Shader blendSkyboxShader;

        [Tooltip(
            "하루 진행도에 따른 밤 스카이박스 혼합 비율입니다. 세로값 0은 완전한 낮, 1은 완전한 밤입니다. " +
            "아래의 짧은 전환 모드를 끈 경우에만 이 고급 곡선을 사용합니다.")]
        [SerializeField] private AnimationCurve nightBlendOverDay = CreateDefaultNightBlendCurve();

        [Tooltip(
            "켜면 낮·밤 스카이박스가 오랫동안 반투명하게 겹치지 않도록 일출과 일몰 주변에서만 짧게 블렌딩합니다. " +
            "일반적인 사용에서는 켜 두는 것을 권장합니다.")]
        [SerializeField] private bool useCompactSkyboxTransition = true;

        [Tooltip(
            "하루 진행도에서 밤이 낮으로 바뀌는 중심 시점입니다. 0은 하루 시작, 1은 하루 끝입니다.")]
        [Range(0.01f, 0.45f)]
        [SerializeField] private float sunriseTransitionCenter = 0.06f;

        [Tooltip(
            "일출 스카이박스 블렌딩에 사용할 하루 비율입니다. 기본 0.045는 20분짜리 하루에서 약 54초 동안 전환됩니다.")]
        [Range(0.005f, 0.15f)]
        [SerializeField] private float sunriseTransitionDuration = 0.045f;

        [Tooltip(
            "하루 진행도에서 낮이 밤으로 바뀌는 중심 시점입니다. 일몰 시점을 앞뒤로 옮길 때 사용합니다.")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float sunsetTransitionCenter = 0.5f;

        [Tooltip(
            "일몰 스카이박스 블렌딩에 사용할 하루 비율입니다. 기본 0.045는 20분짜리 하루에서 약 54초 동안 전환됩니다.")]
        [Range(0.005f, 0.15f)]
        [SerializeField] private float sunsetTransitionDuration = 0.045f;

        [Tooltip(
            "시간이 건너뛰거나 디버그 슬라이더가 크게 움직여도 스카이박스 혼합값이 한 프레임에 바뀌지 않도록 하는 실제 시간입니다.")]
        [Min(0.05f)]
        [SerializeField] private float skyboxBlendResponseSeconds = 1.25f;

        [Tooltip(
            "낮 스카이박스가 완전히 보일 때 적용할 노출값입니다. 값을 높이면 하늘 전체가 밝아지고, " +
            "너무 높으면 구름의 밝은 부분이 하얗게 날아갈 수 있습니다.")]
        [Min(0f)]
        [SerializeField] private float daySkyboxExposure = 1.05f;

        [Tooltip(
            "밤 스카이박스가 완전히 보일 때 적용할 노출값입니다. 밤이 너무 검으면 값을 올리고, " +
            "별이나 달이 너무 강하면 값을 낮춥니다.")]
        [Min(0f)]
        [SerializeField] private float nightSkyboxExposure = 0.85f;

        [Tooltip(
            "낮 스카이박스에 곱해지는 색상입니다. 기본 흰색은 기존 낮 색감을 그대로 유지합니다.")]
        [FormerlySerializedAs("skyboxTint")]
        [SerializeField] private Color daySkyboxTint = Color.white;

        [Tooltip(
            "밤 스카이박스와 밤 환경광에 곱해지는 색상입니다. 기본값은 푸른색을 약간 줄이는 따뜻한 중성 보정입니다. " +
            "밤 오브젝트가 너무 파랗다면 파란색보다 빨강·초록 비율을 조금 높이세요.")]
        [SerializeField] private Color nightSkyboxTint = new Color(1f, 0.92f, 0.84f, 1f);

        [Header("Skybox Framing")]
        [Tooltip("Visual size of the baked day sky. Values below 1 make clouds appear smaller.")]
        [Range(0.4f, 1.25f)]
        [SerializeField] private float daySkyboxScale = 0.5f;

        [Tooltip("Visual size of the baked night sky. Values below 1 make clouds appear smaller.")]
        [Range(0.4f, 1.25f)]
        [SerializeField] private float nightSkyboxScale = 0.48f;

        [Tooltip("Moves the baked day sky upward in degrees when the value is positive.")]
        [Range(-25f, 25f)]
        [SerializeField] private float daySkyboxVerticalOffset = 20f;

        [Tooltip("Moves the baked night sky upward in degrees when the value is positive.")]
        [Range(-25f, 25f)]
        [SerializeField] private float nightSkyboxVerticalOffset = 24f;

        [Tooltip("Cloud-free color used below the world horizon. This prevents reflected clouds in the lower half of a panoramic cubemap from appearing on the ground.")]
        [SerializeField] private Color dayLowerSkyColor =
            new Color(0.10f, 0.305f, 0.491f, 1f);

        [Tooltip("Degrees above the world horizon over which the day cubemap fades in. The area below the horizon always uses Day Lower Sky Color.")]
        [Range(0.5f, 15f)]
        [SerializeField] private float lowerSkyWorldFadeDegrees = 5f;

        [Tooltip("Degrees used to hide the source cubemap's reflected lower hemisphere if framing moves it above the world horizon.")]
        [Range(0.5f, 15f)]
        [SerializeField] private float lowerSkySourceFadeDegrees = 6f;

        [Tooltip(
            "스카이박스의 시작 회전각입니다. 선택한 Cubemap에서 달, 구름, 밝은 부분이 보이는 방향을 " +
            "월드 지형에 맞추고 싶을 때 조절합니다.")]
        [Range(0f, 360f)]
        [SerializeField] private float skyboxRotationOffset;

        [Tooltip(
            "게임 하루가 지날 때 스카이박스가 추가로 회전하는 각도입니다. 기본값 45는 움직임을 방해하지 않으면서 " +
            "구름과 별이 천천히 흐르는 것을 확인할 수 있는 속도입니다. 0이면 회전하지 않습니다.")]
        [SerializeField] private float skyboxRotationDegreesPerGameDay = 45f;

        [Header("태양과 월드 밝기")]
        [Tooltip(
            "시간에 따라 회전하고 밝기가 변할 메인 Directional Light입니다. 비워 두면 RenderSettings.sun을 먼저 사용하고, " +
            "없으면 이름이 'Directional Light'인 조명 또는 가장 밝은 Directional Light를 자동으로 선택합니다.")]
        [SerializeField] private Light mainDirectionalLight;

        [Tooltip(
            "끄면 Directional Light의 회전, 색상, 밝기를 변경하지 않고 스카이박스와 환경광만 제어합니다.")]
        [SerializeField] private bool controlDirectionalLight = true;

        [Tooltip(
            "켜면 시간의 흐름에 따라 Directional Light의 방향도 회전합니다. " +
            "끄면 스카이박스는 계속 회전하지만 Light 방향은 씬에 배치된 각도로 유지하며, 색상과 밝기 변화는 계속 적용합니다.")]
        [SerializeField] private bool rotateDirectionalLightWithTime = true;

        [Tooltip(
            "하루 시작 시 태양의 X 회전각입니다. 이후 하루 동안 360도 회전합니다. " +
            "기본 -10도는 하루 시작을 해가 지평선 근처에 있는 아침으로 맞춥니다.")]
        [SerializeField] private float sunPitchAtDayStart = -10f;

        [Tooltip(
            "태양이 동쪽에서 떠서 서쪽으로 지는 경로의 방향을 돌리는 Y축 각도입니다. " +
            "지형의 산이나 주요 카메라 방향에 맞춰 일출·일몰 방향을 정할 때 조절합니다.")]
        [Range(-180f, 180f)]
        [SerializeField] private float sunYaw = -30f;

        [Tooltip(
            "하루 진행도에 따른 Directional Light 밝기 비율입니다. 세로값 0은 꺼진 상태, 1은 아래 Maximum Intensity입니다. " +
            "밤에도 실루엣이 보이도록 기본 곡선은 완전한 0보다 약간 높은 값을 사용합니다.")]
        [SerializeField] private AnimationCurve directionalLightIntensityOverDay =
            CreateDefaultDirectionalLightCurve();

        [Tooltip(
            "낮 정오 구간에서 사용할 Directional Light의 최대 Intensity입니다. 실제 밝기는 이 값과 위 곡선의 값을 곱해 계산합니다.")]
        [Min(0f)]
        [SerializeField] private float maximumDirectionalLightIntensity = 1.2f;

        [Tooltip(
            "하루 진행도에 따른 Directional Light 색상입니다. 기본값은 아침의 따뜻한 색, 낮의 중성색, " +
            "해질녘의 주황색, 밤의 푸른색을 부드럽게 연결합니다.")]
        [SerializeField] private Gradient directionalLightColorOverDay =
            CreateDefaultDirectionalLightGradient();

        [Header("연속형 월드 조명")]
        [Tooltip(
            "켜면 낮·전환·밤의 단계값을 사용하지 않고 낮 값과 밤 값을 하루 흐름에 따라 직접 연속 보간합니다. " +
            "밝기와 색상이 특정 시점에 다음 단계로 튀는 현상을 방지하므로 기본적으로 켜 두는 것을 권장합니다.")]
        [SerializeField] private bool useContinuousLightingFlow = true;

        [Tooltip("완전한 낮에서 사용할 Directional Light 밝기 비율입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float continuousDayLightRatio = 1f;

        [Tooltip(
            "완전한 밤에서 유지할 Directional Light 밝기 비율입니다. " +
            "밤에도 캐릭터와 지형 윤곽을 유지하기 위해 0보다 큰 값을 사용합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float continuousNightLightRatio = 0.42f;

        [Tooltip("낮에 사용할 중성 계열 조명 색상입니다.")]
        [SerializeField] private Color continuousDayLightColor =
            new Color(1f, 0.956f, 0.84f, 1f);

        [Tooltip(
            "밤에 사용할 조명 색상입니다. 낮 색상과 큰 차이가 없는 따뜻한 중성색을 사용해 화면 전체가 파랗게 변하지 않도록 합니다.")]
        [SerializeField] private Color continuousNightLightColor =
            new Color(0.78f, 0.84f, 1f, 1f);

        [Tooltip(
            "시간 점프나 디버그 값 변경 시 맵 조명값이 목표값까지 따라가는 실제 시간입니다. " +
            "일반적인 시간 흐름에서는 아래의 일출·일몰 조명 시간과 함께 자연스럽게 동작합니다.")]
        [Min(0.05f)]
        [SerializeField] private float lightingResponseSeconds = 2.5f;

        [Tooltip(
            "켜면 밤 스카이박스의 파란색이 지형과 캐릭터의 환경광으로 전달되지 않도록 Ambient Source를 중성 Trilight로 고정합니다. " +
            "하늘 자체의 낮·밤 변화는 그대로 유지됩니다.")]
        [SerializeField] private bool isolateWorldLightingFromSkyboxColor = true;

        [Tooltip("월드 표면의 위쪽에 적용할 중성 환경광 색상입니다.")]
        [SerializeField] private Color neutralAmbientSkyColor =
            new Color(0.30f, 0.29f, 0.27f, 1f);

        [Tooltip("월드 표면의 수평 방향에 적용할 중성 환경광 색상입니다.")]
        [SerializeField] private Color neutralAmbientEquatorColor =
            new Color(0.20f, 0.195f, 0.18f, 1f);

        [Tooltip("월드 표면의 아래쪽에 적용할 중성 환경광 색상입니다.")]
        [SerializeField] private Color neutralAmbientGroundColor =
            new Color(0.11f, 0.10f, 0.09f, 1f);

        [Tooltip(
            "밤에만 중성 환경광 색상의 밝기를 추가로 증폭하는 값입니다. " +
            "Directional Light가 지평선 아래로 내려가도 지형·캐릭터가 잘 보이도록 하며 낮 밝기에는 영향을 주지 않습니다.")]
        [Range(1f, 3f)]
        [SerializeField] private float nightAmbientColorMultiplier = 1.25f;

        [Header("환경광과 반사")]
        [Tooltip(
            "활성화하면 RenderSettings.ambientIntensity와 reflectionIntensity도 낮·밤에 맞춰 조절합니다. " +
            "스카이박스만 어두워지고 지형이 계속 밝게 보이는 현상을 줄이는 데 필요합니다.")]
        [SerializeField] private bool controlAmbientAndReflections = true;

        [Tooltip("낮의 환경광 강도입니다. 지형 그림자 영역의 기본 밝기에 영향을 줍니다.")]
        [Min(0f)]
        [SerializeField] private float dayAmbientIntensity = 1.15f;

        [Tooltip("밤의 환경광 강도입니다. 너무 낮으면 그림자 영역이 완전히 검게 뭉칠 수 있습니다.")]
        [Min(0f)]
        [SerializeField] private float nightAmbientIntensity = 1.25f;

        [Tooltip("낮의 스카이박스 반사 강도입니다. 금속 및 반사 재질이 낮 하늘을 얼마나 강하게 반영할지 결정합니다.")]
        [Min(0f)]
        [SerializeField] private float dayReflectionIntensity = 1.05f;

        [Tooltip("밤의 스카이박스 반사 강도입니다. 밤에도 재질 윤곽이 남도록 0보다 큰 값을 권장합니다.")]
        [Min(0f)]
        [SerializeField] private float nightReflectionIntensity = 0.85f;

        [Tooltip(
            "낮/밤 스카이박스가 섞이는 정확한 중간 지점의 환경광 밝기입니다. " +
            "낮과 밤 값을 단순 평균할 때 생길 수 있는 전환 중 밝기 튐을 제한합니다.")]
        [Min(0f)]
        [SerializeField] private float transitionAmbientIntensity = 1.1f;

        [Tooltip(
            "낮/밤 스카이박스가 섞이는 정확한 중간 지점의 반사 강도입니다. " +
            "전환 구간에서 지형과 금속 표면이 순간적으로 하얗게 뜨는 현상을 줄입니다.")]
        [Min(0f)]
        [SerializeField] private float transitionReflectionIntensity = 0.65f;

        [Tooltip(
            "스카이박스 전환 중 유지할 Directional Light의 최소 밝기 비율입니다. " +
            "해질녘 하늘이 바뀌는 동안 낮 조명이 먼저 너무 어두워지는 현상을 방지합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float minimumTransitionDirectionalLightRatio = 0.12f;

        [Tooltip(
            "일출 시 맵 조명·환경광이 밤에서 낮으로 변하는 하루 비율입니다. " +
            "스카이박스 전환보다 길게 설정하면 하늘은 비교적 빠르게 바뀌면서 맵 밝기는 천천히 이어집니다.")]
        [Range(0.02f, 0.4f)]
        [SerializeField] private float sunriseLightingTransitionDuration = 0.12f;

        [Tooltip(
            "일몰 시 맵 조명·환경광이 낮에서 밤으로 변하는 하루 비율입니다. " +
            "Directional Light와 환경광의 급격한 변화를 방지하려면 스카이박스 전환 시간보다 크게 설정하세요.")]
        [Range(0.02f, 0.4f)]
        [SerializeField] private float sunsetLightingTransitionDuration = 0.12f;

        [Tooltip(
            "활성화하면 바뀐 스카이박스를 환경 반사에 주기적으로 다시 반영합니다. " +
            "밤 Cubemap의 푸른색이 월드 재질에 번질 수 있으므로 중성 월드 조명을 원하면 꺼 두세요.")]
        [SerializeField] private bool refreshEnvironmentReflections;

        [Tooltip(
            "활성화하면 두 스카이박스가 섞이는 중간 프레임에서는 DynamicGI 환경 프로브를 갱신하지 않습니다. " +
            "중간 하늘색이 지형 전체에 번지는 밝기 튐을 막고, 전환 완료 시 즉시 다시 갱신합니다.")]
        [SerializeField] private bool deferEnvironmentRefreshDuringTransition;

        [Tooltip(
            "DynamicGI.UpdateEnvironment를 다시 호출하는 실제 시간 간격(초)입니다. " +
            "값이 작을수록 반사가 자주 갱신되지만 성능 비용이 증가합니다.")]
        [Min(0.5f)]
        [SerializeField] private float environmentRefreshInterval = 8f;

        [Tooltip(
            "스카이박스가 전환 중일 때 Dynamic GI를 갱신하는 간격입니다. " +
            "짧을수록 지형에 반영되는 하늘색이 부드럽지만 갱신 비용이 증가합니다.")]
        [Min(0.5f)]
        [SerializeField] private float transitionEnvironmentRefreshInterval = 0.5f;

        [Header("상태 확인")]
        [Tooltip(
            "현재 GameTimeManager에서 읽어 정규화한 하루 진행도입니다. 실행 중 디버깅용으로 표시되며 직접 수정하지 않습니다.")]
        [SerializeField, Range(0f, 1f)] private float currentNormalizedTime;

        [Tooltip(
            "현재 적용 중인 밤 스카이박스 혼합 비율입니다. 0이면 낮, 1이면 밤, 중간값이면 두 스카이박스가 블렌딩 중입니다.")]
        [SerializeField, Range(0f, 1f)] private float currentNightBlend;

        [Tooltip(
            "현재 맵 조명과 환경광에 적용 중인 밤 비율입니다. 스카이박스보다 긴 시간에 걸쳐 변화하도록 별도로 계산됩니다.")]
        [SerializeField, Range(0f, 1f)] private float currentLightingNightBlend;

        public float CurrentNightBlend => currentNightBlend;

        private Material runtimeSkybox;
        private Material originalSkybox;
        private Light originalSun;
        private Quaternion originalLightRotation;
        private Color originalLightColor;
        private float originalLightIntensity;
        private float originalAmbientIntensity;
        private float originalReflectionIntensity;
        private AmbientMode originalAmbientMode;
        private Color originalAmbientSkyColor;
        private Color originalAmbientEquatorColor;
        private Color originalAmbientGroundColor;
        private float nextEnvironmentRefreshTime;
        private float nextReferenceRetryTime;
        private int lastStableEnvironment = -1;
        private bool originalStateCaptured;
        private bool blendStateInitialized;
        private bool warnedMissingTimeManager;
        private bool warnedInvalidSkybox;

        private void Reset()
        {
#if UNITY_EDITOR
            daySkyboxSource = AssetDatabase.LoadAssetAtPath<Material>(DefaultDaySkyboxPath);
            nightSkyboxSource = AssetDatabase.LoadAssetAtPath<Material>(DefaultNightSkyboxPath);
#endif
            blendSkyboxShader = Shader.Find(BlendShaderName);
            ResolveDirectionalLight();
        }

        private void OnEnable()
        {
            ResolveReferences(true);
            CaptureOriginalState();
            CreateRuntimeSkybox();
            ApplyEnvironment(true);
        }

        private void LateUpdate()
        {
            if (gameTimeManager == null && Time.unscaledTime >= nextReferenceRetryTime)
            {
                nextReferenceRetryTime = Time.unscaledTime + 1f;
                ResolveTimeManager(false);
            }

            if (mainDirectionalLight == null && controlDirectionalLight)
            {
                ResolveDirectionalLight();
            }

            if (runtimeSkybox == null)
            {
                CreateRuntimeSkybox();
            }

            ApplyEnvironment(false);
        }

        private void OnDisable()
        {
            RestoreOriginalState();
            DestroyRuntimeSkybox();
        }

        private void OnDestroy()
        {
            DestroyRuntimeSkybox();
        }

        private void ResolveReferences(bool logWarnings)
        {
            ResolveTimeManager(logWarnings);
            ResolveDirectionalLight();

            if (blendSkyboxShader == null)
            {
                blendSkyboxShader = Shader.Find(BlendShaderName);
            }
        }

        private void ResolveTimeManager(bool logWarnings)
        {
            if (gameTimeManager == null)
            {
                gameTimeManager = GameTimeManager.Instance;
            }

            if (gameTimeManager == null)
            {
                gameTimeManager = FindFirstObjectByType<GameTimeManager>();
            }

            if (gameTimeManager != null)
            {
                warnedMissingTimeManager = false;
                return;
            }

            if (logWarnings && !warnedMissingTimeManager)
            {
                warnedMissingTimeManager = true;
                Debug.LogWarning(
                    "[GHWorldDayNightSkyController] GameTimeManager를 찾지 못했습니다. " +
                    "시간 매니저가 생성되면 자동으로 다시 연결합니다.",
                    this);
            }
        }

        private void ResolveDirectionalLight()
        {
            if (mainDirectionalLight != null)
            {
                return;
            }

            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional)
            {
                mainDirectionalLight = RenderSettings.sun;
                return;
            }

            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Light brightestDirectional = null;

            for (int i = 0; i < lights.Length; i++)
            {
                Light candidate = lights[i];
                if (candidate == null || candidate.type != LightType.Directional)
                {
                    continue;
                }

                if (candidate.name == "Directional Light")
                {
                    mainDirectionalLight = candidate;
                    return;
                }

                if (brightestDirectional == null
                    || candidate.intensity > brightestDirectional.intensity)
                {
                    brightestDirectional = candidate;
                }
            }

            mainDirectionalLight = brightestDirectional;
        }

        private void CaptureOriginalState()
        {
            if (originalStateCaptured)
            {
                return;
            }

            originalSkybox = RenderSettings.skybox;
            originalSun = RenderSettings.sun;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            originalAmbientGroundColor = RenderSettings.ambientGroundColor;
            if (mainDirectionalLight != null)
            {
                originalLightRotation = mainDirectionalLight.transform.rotation;
                originalLightColor = mainDirectionalLight.color;
                originalLightIntensity = mainDirectionalLight.intensity;
            }

            originalStateCaptured = true;
        }

        private void CreateRuntimeSkybox()
        {
            if (runtimeSkybox != null)
            {
                return;
            }

            if (blendSkyboxShader == null)
            {
                blendSkyboxShader = Shader.Find(BlendShaderName);
            }

            Texture dayTexture = GetSkyboxTexture(daySkyboxSource);
            bool usesProceduralStarryNight =
                IsProceduralStarrySkyMaterial(nightSkyboxSource);
            Texture nightTexture = usesProceduralStarryNight
                ? null
                : GetSkyboxTexture(nightSkyboxSource);
            Shader runtimeShader = usesProceduralStarryNight
                ? Shader.Find(StarryNightBlendShaderName)
                : blendSkyboxShader;

            if (runtimeShader == null
                || dayTexture == null
                || (!usesProceduralStarryNight && nightTexture == null))
            {
                if (!warnedInvalidSkybox)
                {
                    warnedInvalidSkybox = true;
                    Debug.LogWarning(
                        "[GHWorldDayNightSkyController] 블렌딩 셰이더 또는 낮/밤 Cubemap을 찾지 못했습니다. " +
                        "Blend Skybox Shader와 두 Skybox Source의 _Tex 연결을 확인하세요.",
                        this);
                }

                return;
            }

            warnedInvalidSkybox = false;
            runtimeSkybox = usesProceduralStarryNight
                ? new Material(nightSkyboxSource)
                : new Material(runtimeShader);
            runtimeSkybox.shader = runtimeShader;
            runtimeSkybox.name = "GH Runtime Day Night Skybox";
            runtimeSkybox.hideFlags = HideFlags.HideAndDontSave;

            runtimeSkybox.SetTexture(DayTextureId, dayTexture);
            if (!usesProceduralStarryNight)
            {
                runtimeSkybox.SetTexture(NightTextureId, nightTexture);
            }

            runtimeSkybox.SetColor(TintId, daySkyboxTint);
            RenderSettings.skybox = runtimeSkybox;
        }

        private static bool IsProceduralStarrySkyMaterial(Material source)
        {
            if (source == null || source.shader == null)
            {
                return false;
            }

            string shaderName = source.shader.name;
            return shaderName == DynamicStarrySkyShaderName
                || shaderName == StarryNightBlendShaderName;
        }

        private static Texture GetSkyboxTexture(Material source)
        {
            if (source == null)
            {
                return null;
            }

            if (source.HasProperty(SourceCubemapId))
            {
                Texture cubemap = source.GetTexture(SourceCubemapId);
                if (cubemap != null)
                {
                    return cubemap;
                }
            }

            return source.HasProperty(SourceMainTextureId)
                ? source.GetTexture(SourceMainTextureId)
                : null;
        }

        private void ApplyEnvironment(bool forceReflectionRefresh)
        {
            if (!TryGetTimeValues(out float normalizedTime, out float absoluteDay))
            {
                return;
            }

            currentNormalizedTime = normalizedTime;
            float targetNightBlend = EvaluateNightBlend(normalizedTime);
            float targetLightingNightBlend = EvaluateLightingNightBlend(normalizedTime);

            if (forceReflectionRefresh || !blendStateInitialized)
            {
                currentNightBlend = targetNightBlend;
                currentLightingNightBlend = targetLightingNightBlend;
                blendStateInitialized = true;
            }
            else
            {
                float unscaledDeltaTime = Mathf.Max(0f, Time.unscaledDeltaTime);
                currentNightBlend = Mathf.MoveTowards(
                    currentNightBlend,
                    targetNightBlend,
                    unscaledDeltaTime / Mathf.Max(0.05f, skyboxBlendResponseSeconds));
                currentLightingNightBlend = Mathf.MoveTowards(
                    currentLightingNightBlend,
                    targetLightingNightBlend,
                    unscaledDeltaTime / Mathf.Max(0.05f, lightingResponseSeconds));
            }

            if (runtimeSkybox != null)
            {
                float exposure = Mathf.Lerp(
                    Mathf.Max(0f, daySkyboxExposure),
                    Mathf.Max(0f, nightSkyboxExposure),
                    currentNightBlend);
                float rotation = Mathf.Repeat(
                    skyboxRotationOffset + absoluteDay * skyboxRotationDegreesPerGameDay,
                    360f);

                runtimeSkybox.SetFloat(BlendId, currentNightBlend);
                runtimeSkybox.SetFloat(ExposureId, exposure);
                runtimeSkybox.SetFloat(RotationId, rotation);
                runtimeSkybox.SetFloat(
                    DaySkyScaleId,
                    Mathf.Clamp(daySkyboxScale, 0.4f, 1.25f));
                runtimeSkybox.SetFloat(
                    NightSkyScaleId,
                    Mathf.Clamp(nightSkyboxScale, 0.4f, 1.25f));
                runtimeSkybox.SetFloat(
                    DayVerticalOffsetId,
                    Mathf.Clamp(daySkyboxVerticalOffset, -25f, 25f));
                runtimeSkybox.SetFloat(
                    NightVerticalOffsetId,
                    Mathf.Clamp(nightSkyboxVerticalOffset, -25f, 25f));
                if (runtimeSkybox.HasProperty(DayLowerSkyColorId)
                    && runtimeSkybox.HasProperty(LowerSkyWorldFadeId)
                    && runtimeSkybox.HasProperty(LowerSkySourceFadeId))
                {
                    runtimeSkybox.SetColor(DayLowerSkyColorId, dayLowerSkyColor);
                    runtimeSkybox.SetFloat(
                        LowerSkyWorldFadeId,
                        Mathf.Clamp(lowerSkyWorldFadeDegrees, 0.5f, 15f));
                    runtimeSkybox.SetFloat(
                        LowerSkySourceFadeId,
                        Mathf.Clamp(lowerSkySourceFadeDegrees, 0.5f, 15f));
                }
                runtimeSkybox.SetColor(
                    TintId,
                    Color.Lerp(daySkyboxTint, nightSkyboxTint, currentNightBlend));

                if (RenderSettings.skybox != runtimeSkybox)
                {
                    RenderSettings.skybox = runtimeSkybox;
                }
            }

            ApplyDirectionalLight(normalizedTime);
            ApplyAmbientAndReflections(forceReflectionRefresh);
        }

        private bool TryGetTimeValues(out float normalizedTime, out float absoluteDay)
        {
            if (useDebugTimeOverride)
            {
                normalizedTime = Mathf.Repeat(debugNormalizedTime + normalizedTimeOffset, 1f);
                absoluteDay = normalizedTime;
                return true;
            }

            if (gameTimeManager == null)
            {
                normalizedTime = 0f;
                absoluteDay = 0f;
                return false;
            }

            float dayLength = Mathf.Max(0.0001f, gameTimeManager.DayLengthSeconds);
            normalizedTime = Mathf.Repeat(
                gameTimeManager.InGameTimeOfDaySeconds / dayLength + normalizedTimeOffset,
                1f);
            absoluteDay = gameTimeManager.ElapsedTime / dayLength + normalizedTimeOffset;
            return true;
        }

        private float EvaluateNightBlend(float normalizedTime)
        {
            if (!useCompactSkyboxTransition)
            {
                return Mathf.Clamp01(nightBlendOverDay.Evaluate(normalizedTime));
            }

            return EvaluateCompactNightBlend(
                normalizedTime,
                sunriseTransitionDuration,
                sunsetTransitionDuration);
        }

        private float EvaluateLightingNightBlend(float normalizedTime)
        {
            return EvaluateCompactNightBlend(
                normalizedTime,
                sunriseLightingTransitionDuration,
                sunsetLightingTransitionDuration);
        }

        private float EvaluateCompactNightBlend(
            float normalizedTime,
            float sunriseDuration,
            float sunsetDuration)
        {
            float sunriseHalfDuration = Mathf.Max(0.0025f, sunriseDuration * 0.5f);
            float sunriseStart = sunriseTransitionCenter - sunriseHalfDuration;
            float sunriseEnd = sunriseTransitionCenter + sunriseHalfDuration;

            if (normalizedTime <= sunriseEnd)
            {
                return 1f - SmootherStep01(
                    Mathf.InverseLerp(sunriseStart, sunriseEnd, normalizedTime));
            }

            float sunsetHalfDuration = Mathf.Max(0.0025f, sunsetDuration * 0.5f);
            float sunsetStart = sunsetTransitionCenter - sunsetHalfDuration;
            float sunsetEnd = sunsetTransitionCenter + sunsetHalfDuration;

            if (normalizedTime < sunsetStart)
            {
                return 0f;
            }

            if (normalizedTime <= sunsetEnd)
            {
                return SmootherStep01(
                    Mathf.InverseLerp(sunsetStart, sunsetEnd, normalizedTime));
            }

            return 1f;
        }

        private void ApplyDirectionalLight(float normalizedTime)
        {
            if (!controlDirectionalLight || mainDirectionalLight == null)
            {
                return;
            }

            float intensityRatio;
            Color lightColor;

            if (useContinuousLightingFlow)
            {
                intensityRatio = Mathf.Lerp(
                    Mathf.Clamp01(continuousDayLightRatio),
                    Mathf.Clamp01(continuousNightLightRatio),
                    currentLightingNightBlend);
                lightColor = Color.Lerp(
                    continuousDayLightColor,
                    continuousNightLightColor,
                    currentLightingNightBlend);
            }
            else
            {
                intensityRatio = Mathf.Max(
                    0f,
                    directionalLightIntensityOverDay.Evaluate(normalizedTime));
                bool skyboxIsTransitioning =
                    currentNightBlend > 0.001f && currentNightBlend < 0.999f;
                if (skyboxIsTransitioning)
                {
                    float transitionWeight =
                        1f - Mathf.Abs(currentNightBlend * 2f - 1f);
                    transitionWeight = SmootherStep01(transitionWeight);
                    intensityRatio = Mathf.Max(
                        intensityRatio,
                        Mathf.Clamp01(minimumTransitionDirectionalLightRatio)
                        * transitionWeight);
                }

                lightColor = directionalLightColorOverDay.Evaluate(normalizedTime);
            }

            mainDirectionalLight.intensity =
                Mathf.Max(0f, maximumDirectionalLightIntensity) * intensityRatio;
            mainDirectionalLight.color = lightColor;
            if (rotateDirectionalLightWithTime)
            {
                mainDirectionalLight.transform.rotation = Quaternion.Euler(
                    sunPitchAtDayStart + normalizedTime * 360f,
                    sunYaw,
                    0f);
            }

            if (RenderSettings.sun != mainDirectionalLight)
            {
                RenderSettings.sun = mainDirectionalLight;
            }
        }

        private void ApplyAmbientAndReflections(bool forceRefresh)
        {
            if (!controlAmbientAndReflections)
            {
                return;
            }

            if (useContinuousLightingFlow)
            {
                if (isolateWorldLightingFromSkyboxColor)
                {
                    float ambientColorMultiplier = Mathf.Lerp(
                        1f,
                        Mathf.Max(1f, nightAmbientColorMultiplier),
                        currentLightingNightBlend);
                    RenderSettings.ambientMode = AmbientMode.Trilight;
                    RenderSettings.ambientSkyColor =
                        MultiplyRgb(neutralAmbientSkyColor, ambientColorMultiplier);
                    RenderSettings.ambientEquatorColor =
                        MultiplyRgb(neutralAmbientEquatorColor, ambientColorMultiplier);
                    RenderSettings.ambientGroundColor =
                        MultiplyRgb(neutralAmbientGroundColor, ambientColorMultiplier);
                }

                RenderSettings.ambientIntensity = Mathf.Lerp(
                    Mathf.Max(0f, dayAmbientIntensity),
                    Mathf.Max(0f, nightAmbientIntensity),
                    currentLightingNightBlend);
                RenderSettings.reflectionIntensity = Mathf.Lerp(
                    Mathf.Max(0f, dayReflectionIntensity),
                    Mathf.Max(0f, nightReflectionIntensity),
                    currentLightingNightBlend);
            }
            else
            {
                RenderSettings.ambientIntensity = EvaluateThreePointBlend(
                    Mathf.Max(0f, dayAmbientIntensity),
                    Mathf.Max(0f, transitionAmbientIntensity),
                    Mathf.Max(0f, nightAmbientIntensity),
                    currentLightingNightBlend);
                RenderSettings.reflectionIntensity = EvaluateThreePointBlend(
                    Mathf.Max(0f, dayReflectionIntensity),
                    Mathf.Max(0f, transitionReflectionIntensity),
                    Mathf.Max(0f, nightReflectionIntensity),
                    currentLightingNightBlend);
            }

            if (!refreshEnvironmentReflections)
            {
                return;
            }

            const float StableThreshold = 0.02f;
            int stableEnvironment = currentNightBlend <= StableThreshold
                ? 0
                : currentNightBlend >= 1f - StableThreshold
                    ? 1
                    : -1;

            if (deferEnvironmentRefreshDuringTransition && stableEnvironment < 0)
            {
                return;
            }

            bool enteredStableEnvironment =
                stableEnvironment >= 0 && stableEnvironment != lastStableEnvironment;
            float refreshInterval = stableEnvironment < 0
                ? Mathf.Max(0.5f, transitionEnvironmentRefreshInterval)
                : Mathf.Max(0.5f, environmentRefreshInterval);
            if (!forceRefresh
                && !enteredStableEnvironment
                && Time.unscaledTime < nextEnvironmentRefreshTime)
            {
                return;
            }

            lastStableEnvironment = stableEnvironment;
            nextEnvironmentRefreshTime = Time.unscaledTime + refreshInterval;
            DynamicGI.UpdateEnvironment();
        }

        private static float EvaluateThreePointBlend(
            float dayValue,
            float transitionValue,
            float nightValue,
            float nightBlend)
        {
            float blend = Mathf.Clamp01(nightBlend);
            return blend <= 0.5f
                ? Mathf.Lerp(dayValue, transitionValue, SmootherStep01(blend * 2f))
                : Mathf.Lerp(
                    transitionValue,
                    nightValue,
                    SmootherStep01((blend - 0.5f) * 2f));
        }

        private static float SmootherStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static Color MultiplyRgb(Color color, float multiplier)
        {
            return new Color(
                color.r * multiplier,
                color.g * multiplier,
                color.b * multiplier,
                color.a);
        }

        private void RestoreOriginalState()
        {
            if (!originalStateCaptured)
            {
                return;
            }

            if (RenderSettings.skybox == runtimeSkybox)
            {
                RenderSettings.skybox = originalSkybox;
            }

            RenderSettings.sun = originalSun;

            if (mainDirectionalLight != null)
            {
                mainDirectionalLight.transform.rotation = originalLightRotation;
                mainDirectionalLight.color = originalLightColor;
                mainDirectionalLight.intensity = originalLightIntensity;
            }

            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.reflectionIntensity = originalReflectionIntensity;
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientSkyColor = originalAmbientSkyColor;
            RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
            RenderSettings.ambientGroundColor = originalAmbientGroundColor;
            originalStateCaptured = false;
        }

        private void DestroyRuntimeSkybox()
        {
            if (runtimeSkybox == null)
            {
                return;
            }

            Destroy(runtimeSkybox);
            runtimeSkybox = null;
            blendStateInitialized = false;
        }

        private void OnValidate()
        {
            daySkyboxExposure = Mathf.Max(0f, daySkyboxExposure);
            nightSkyboxExposure = Mathf.Max(0f, nightSkyboxExposure);
            skyboxBlendResponseSeconds = Mathf.Max(0.05f, skyboxBlendResponseSeconds);
            daySkyboxScale = Mathf.Clamp(daySkyboxScale, 0.4f, 1.25f);
            nightSkyboxScale = Mathf.Clamp(nightSkyboxScale, 0.4f, 1.25f);
            daySkyboxVerticalOffset = Mathf.Clamp(daySkyboxVerticalOffset, -25f, 25f);
            nightSkyboxVerticalOffset = Mathf.Clamp(nightSkyboxVerticalOffset, -25f, 25f);
            lowerSkyWorldFadeDegrees = Mathf.Clamp(
                lowerSkyWorldFadeDegrees,
                0.5f,
                15f);
            lowerSkySourceFadeDegrees = Mathf.Clamp(
                lowerSkySourceFadeDegrees,
                0.5f,
                15f);
            maximumDirectionalLightIntensity = Mathf.Max(0f, maximumDirectionalLightIntensity);
            continuousDayLightRatio = Mathf.Clamp01(continuousDayLightRatio);
            continuousNightLightRatio = Mathf.Clamp01(continuousNightLightRatio);
            lightingResponseSeconds = Mathf.Max(0.05f, lightingResponseSeconds);
            nightAmbientColorMultiplier =
                Mathf.Clamp(nightAmbientColorMultiplier, 1f, 3f);
            dayAmbientIntensity = Mathf.Max(0f, dayAmbientIntensity);
            nightAmbientIntensity = Mathf.Max(0f, nightAmbientIntensity);
            dayReflectionIntensity = Mathf.Max(0f, dayReflectionIntensity);
            nightReflectionIntensity = Mathf.Max(0f, nightReflectionIntensity);
            transitionAmbientIntensity = Mathf.Max(0f, transitionAmbientIntensity);
            transitionReflectionIntensity = Mathf.Max(0f, transitionReflectionIntensity);
            minimumTransitionDirectionalLightRatio =
                Mathf.Clamp01(minimumTransitionDirectionalLightRatio);
            environmentRefreshInterval = Mathf.Max(0.5f, environmentRefreshInterval);
            transitionEnvironmentRefreshInterval =
                Mathf.Max(0.5f, transitionEnvironmentRefreshInterval);
            sunriseTransitionDuration = Mathf.Max(0.005f, sunriseTransitionDuration);
            sunsetTransitionDuration = Mathf.Max(0.005f, sunsetTransitionDuration);
            sunriseLightingTransitionDuration =
                Mathf.Clamp(sunriseLightingTransitionDuration, 0.02f, 0.4f);
            sunsetLightingTransitionDuration =
                Mathf.Clamp(sunsetLightingTransitionDuration, 0.02f, 0.4f);
        }

        private static AnimationCurve CreateDefaultNightBlendCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.05375f, 1f),
                new Keyframe(0.06625f, 0f),
                new Keyframe(0.49375f, 0f),
                new Keyframe(0.50625f, 1f),
                new Keyframe(1f, 1f));
        }

        private static AnimationCurve CreateDefaultDirectionalLightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.12f),
                new Keyframe(0.08f, 0.82f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.42f, 0.82f),
                new Keyframe(0.49f, 0.22f),
                new Keyframe(0.55f, 0.08f),
                new Keyframe(0.9f, 0.08f),
                new Keyframe(1f, 0.12f));
        }

        private static Gradient CreateDefaultDirectionalLightGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.72f, 0.74f, 0.78f), 0f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.92f), 0.08f),
                    new GradientColorKey(Color.white, 0.25f),
                    new GradientColorKey(new Color(1f, 0.97f, 0.92f), 0.42f),
                    new GradientColorKey(new Color(0.95f, 0.78f, 0.62f), 0.5f),
                    new GradientColorKey(new Color(0.74f, 0.75f, 0.78f), 0.56f),
                    new GradientColorKey(new Color(0.68f, 0.7f, 0.74f), 0.82f),
                    new GradientColorKey(new Color(0.72f, 0.74f, 0.78f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
