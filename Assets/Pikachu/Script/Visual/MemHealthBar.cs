// ============================================================================
// MemHealthBar.cs
// 멤 머리 위 월드공간 HP 바
//
// [담당자 안내]
// - 멤 루트에 부착되는 컴포넌트입니다. (MemFactory.CreateInstance에서 자동 부착)
// - 월드공간 Canvas + HP 바를 "코드로" 생성하므로 프리팹/씬 편집이 필요 없습니다.
// - 멤의 자식으로 생성되어 멤 이동을 그대로 따라다니며, 매 프레임 카메라를 향합니다(빌보드).
// - MemStats.HpRatio를 매 프레임 반영합니다. (별도 이벤트 구독 불필요)
//
// [스프라이트/높이 커스터마이즈]
// - 스프라이트·높이·색상 등은 MemHealthBarStyle로 묶여 있습니다.
// - 이 컴포넌트는 런타임에 코드로 부착되므로 Inspector에 직접 안 뜹니다.
//   대신 MemFactory의 "HP 바 스타일" 슬롯에서 값을 넣으면 SetStyle()로 전달됩니다.
// - Background/Fill 스프라이트를 비워두면 단색 사각형으로 표시됩니다.
//
// [연동 메모]
// - 이름/등급/포획확률 패널은 플레이어(KMS) 쪽에서 조준 시 별도 표시합니다.
//   중복을 피하기 위해 이 컴포넌트는 "HP 바"만 담당합니다.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Core;
using MemSystem.Events;

namespace MemSystem.Visual
{
    /// <summary>
    /// 멤 머리 위 HP 바의 외형/배치 설정. MemFactory에서 Inspector로 지정하여 주입합니다.
    /// [System.Serializable]이라 MemFactory의 필드로 노출되면 스프라이트를 드래그로 넣을 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class MemHealthBarStyle
    {
        [Header("배치")]
        [Tooltip("멤 발밑 기준 바의 높이(월드 유닛). 머리 바로 위로 오도록 모델 키에 맞춰 조절하세요.")]
        public float heightOffset = 1.3f;

        [Tooltip("바의 월드 크기 (가로 x 세로, 미터).")]
        public Vector2 worldSize = new Vector2(1.0f, 0.14f);

        [Tooltip("배경과 채움(Fill) 사이 여백(픽셀, 내부 100단위 기준). 0이면 배경과 채움이 같은 크기. " +
                 "값이 크면 배경이 테두리처럼 더 두껍게 보입니다.")]
        public float fillPadding = 1f;

        [Header("스프라이트 (비우면 단색 사각형)")]
        [Tooltip("바 배경 스프라이트. 비우면 backgroundColor 단색으로 그립니다.")]
        public Sprite backgroundSprite;

        [Tooltip("HP 채움 스프라이트. 지정 시 fillAmount(가로 채움)로 표현합니다. 비우면 단색 사각형을 가로 스케일로 줄입니다.")]
        public Sprite fillSprite;

        [Header("표시 조건")]
        [Tooltip("플레이어가 조준(포커싱)한 멤만 바를 표시합니다. 끄면 항상 표시. " +
                 "조준 판정은 MemFocusTracker가 KMS와 동일한 방식으로 수행합니다.")]
        public bool showOnlyWhenFocused = true;

        [Tooltip("조준이 풀려도 이 시간(초)만큼 바를 유지합니다. 조준이 순간적으로 빗나가도 " +
                 "깜빡이지 않게 합니다. 피해를 입으면(=때리는 중) 타이머가 갱신되어 계속 유지됩니다. " +
                 "Show Only When Focused가 켜져 있을 때만 적용됩니다.")]
        [Min(0f)]
        public float focusLingerSeconds = 1.5f;

        [Tooltip("체력이 가득 찼을 때는 바를 숨깁니다. (피해 입은 멤만 표시)")]
        public bool hideWhenFull = false;

        [Tooltip("체력이 0(사망/포획/도주 직전)일 때 바를 숨깁니다.")]
        public bool hideWhenDead = true;

        [Header("색상")]
        [Tooltip("체력에 따라 색을 초록↔빨강으로 바꿉니다. " +
                 "직접 만든 색 스프라이트를 원본 그대로 쓰려면 끄세요(끄면 흰색 틴트=원본색).")]
        public bool tintByHealth = true;

        [Tooltip("체력 100%일 때 색 (tintByHealth=off면 이 색으로 고정 틴트)")]
        public Color fullColor = new Color(0.30f, 0.85f, 0.30f, 1f);

        [Tooltip("체력 0%일 때 색 (사이는 자동 보간)")]
        public Color emptyColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        [Tooltip("바 배경색")]
        public Color backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.72f);
    }

    /// <summary>
    /// 멤 머리 위에 HP 바를 표시하는 월드공간 UI 컴포넌트.
    /// UI 계층 전체를 런타임에 코드로 생성한다(프리팹/씬 수정 불필요).
    /// </summary>
    public class MemHealthBar : MonoBehaviour
    {
        // 외형/배치 설정. MemFactory.SetStyle()로 주입되며, 없으면 기본값을 사용합니다.
        private MemHealthBarStyle style = new MemHealthBarStyle();

        // =================================================================
        // 내부 참조
        // =================================================================

        private MemStats stats;
        private Mem mem;                // 포커스 판정 시 소유 멤 식별용

        private Transform barRoot;      // 빌보드 회전 + 위치 이동 대상
        private RectTransform fillRect; // 채움 표현 (스케일 또는 fillAmount)
        private Image fillImage;
        private Canvas canvas;

        private Transform cam;          // 빌보드 기준 카메라(캐싱)
        private bool built;
        private bool usesFillAmount;    // fillSprite가 있으면 fillAmount, 없으면 가로 스케일

        // 마지막으로 "활성"(조준 중이거나 피격) 상태였던 시각. 이후 focusLingerSeconds 동안 바를 유지.
        // 아주 과거로 초기화해 스폰 직후에는 숨겨진 상태로 시작한다.
        private float lastActiveTime = -999f;

        // 전투가 없는 씬(영지 등)에서 HP 바를 통째로 숨기기 위한 스위치. SetHidden()으로 제어.
        private bool forceHidden;

        // =================================================================
        // 외부 주입 API
        // =================================================================

        /// <summary>
        /// 외형/배치 스타일을 주입합니다. BuildUI(Awake) 전에 호출해야 반영됩니다.
        /// MemFactory가 컴포넌트를 AddComponent한 직후(비활성 상태) 호출합니다.
        /// </summary>
        public void SetStyle(MemHealthBarStyle newStyle)
        {
            if (newStyle != null) style = newStyle;
        }

        /// <summary>
        /// HP 바를 완전히 숨깁니다. 영지처럼 전투가 없는 씬에서 사용합니다.
        /// 스타일 설정(showOnlyWhenFocused 등)보다 우선하며, 숨긴 동안은 갱신도 하지 않습니다.
        /// 풀에서 재사용될 때마다 소환 측(TerritoryWanderSpawner 등)이 매번 지정합니다.
        /// </summary>
        public void SetHidden(bool hidden)
        {
            forceHidden = hidden;

            if (canvas != null)
                canvas.enabled = !hidden && canvas.enabled;
        }

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void Awake()
        {
            stats = GetComponent<MemStats>();
            if (stats == null)
            {
                Debug.LogError("[MemHealthBar] 같은 GameObject에 MemStats가 없습니다. 멤 루트에 부착되어야 합니다.", this);
                enabled = false;
                return;
            }

            mem = GetComponent<Mem>();

            BuildUI();
        }

        private void OnEnable()
        {
            // 풀에서 재사용될 때 이전 상태(유지 타이머)가 남지 않도록 리셋 → 재등장 시 숨김부터 시작
            lastActiveTime = -999f;

            // 피격 시 유지 타이머를 갱신하기 위해 구독 (때리는 중엔 조준이 빗나가도 바 유지)
            MemEvents.OnMemDamaged += HandleDamaged;

            if (built) Refresh();
        }

        private void OnDisable()
        {
            MemEvents.OnMemDamaged -= HandleDamaged;
        }

        /// <summary>이 멤이 피해를 입으면 유지 타이머를 갱신한다.</summary>
        private void HandleDamaged(Mem damaged, int amount)
        {
            if (damaged == mem)
                lastActiveTime = Time.time;
        }

        private void LateUpdate()
        {
            if (!built) return;

            if (forceHidden)
            {
                if (canvas.enabled) canvas.enabled = false;
                return;
            }

            Refresh();

            if (!canvas.enabled) return;

            // 위치: 멤 머리 위 (부모 스케일 영향을 피하려고 월드 위치를 직접 지정)
            barRoot.position = transform.position + Vector3.up * style.heightOffset;

            // 빌보드: 카메라와 같은 방향을 바라보게 회전
            var camT = ResolveCamera();
            if (camT != null)
                barRoot.forward = camT.forward;
        }

        // =================================================================
        // 내부 구현
        // =================================================================

        /// <summary>HP 비율에 맞춰 바 길이/색/표시여부를 갱신합니다.</summary>
        private void Refresh()
        {
            float ratio = stats.MaxHp > 0 ? Mathf.Clamp01(stats.HpRatio) : 0f;

            bool visible = true;

            if (style.showOnlyWhenFocused)
            {
                // 조준 중이면 타이머 갱신. 조준이 풀려도 마지막 활성(조준/피격) 이후
                // focusLingerSeconds 동안은 유지해 깜빡임을 막는다.
                if (MemFocusTracker.Current == mem) lastActiveTime = Time.time;
                if (Time.time - lastActiveTime > style.focusLingerSeconds) visible = false;
            }

            if (style.hideWhenDead && stats.CurrentHp <= 0) visible = false;
            if (style.hideWhenFull && ratio >= 0.999f) visible = false;

            canvas.enabled = visible;
            if (!visible) return;

            if (usesFillAmount)
                fillImage.fillAmount = ratio;                      // 스프라이트: 가로 채움
            else
                fillRect.localScale = new Vector3(ratio, 1f, 1f);  // 단색: 좌측 피벗 가로 스케일

            // 색상 결정. 틴트를 끄면 스프라이트는 원본색(흰 틴트), 단색은 fullColor 고정.
            Color c;
            if (style.tintByHealth)
                c = Color.Lerp(style.emptyColor, style.fullColor, ratio);
            else
                c = usesFillAmount ? Color.white : style.fullColor;

            c.a = 1f; // 알파는 항상 불투명 (반투명하게 보이던 문제 방지)
            fillImage.color = c;
        }

        /// <summary>메인 카메라를 캐싱하되, 씬 전환 등으로 사라지면 다시 찾습니다.</summary>
        private Transform ResolveCamera()
        {
            if (cam != null) return cam;
            var main = Camera.main;
            cam = main != null ? main.transform : null;
            return cam;
        }

        /// <summary>월드공간 Canvas + 배경/채움 이미지를 코드로 생성합니다.</summary>
        private void BuildUI()
        {
            // RectTransform을 처음부터 포함해 생성한다. new GameObject() 후 AddComponent<Canvas>()를
            // 하면 Canvas가 요구하는 RectTransform이 기존 Transform을 교체(파괴)하는데,
            // 그 전에 캐싱한 Transform 참조는 파괴되어 MissingReferenceException이 난다.
            var canvasGo = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            barRoot = canvasRect;
            barRoot.SetParent(transform, worldPositionStays: false);
            barRoot.localPosition = Vector3.up * style.heightOffset;
            barRoot.localRotation = Quaternion.identity;

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            // 100 유닛 = worldSize 미터가 되도록 스케일. (픽셀 정밀도 확보용 관례)
            const float refPixels = 100f;
            float aspect = style.worldSize.x > 0f ? style.worldSize.y / style.worldSize.x : 0.14f;
            canvasRect.sizeDelta = new Vector2(refPixels, refPixels * aspect);
            barRoot.localScale = Vector3.one * (style.worldSize.x / refPixels);

            // ---- 배경 ----
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.SetParent(canvasRect, false);
            Stretch(bgRect);
            var bgImage = bgGo.GetComponent<Image>();
            bgImage.color = style.backgroundColor;
            bgImage.raycastTarget = false;
            if (style.backgroundSprite != null)
            {
                bgImage.sprite = style.backgroundSprite;
                bgImage.type = Image.Type.Sliced; // 9슬라이스 지원(테두리 없으면 Simple처럼 동작)
            }

            // ---- 채움 ----
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.SetParent(bgRect, false);
            Stretch(fillRect);
            // 배경과 채움 사이 여백 (0이면 배경=채움 크기)
            float pad = Mathf.Max(0f, style.fillPadding);
            fillRect.offsetMin = new Vector2(pad, pad);
            fillRect.offsetMax = new Vector2(-pad, -pad);

            fillImage = fillGo.GetComponent<Image>();
            fillImage.color = style.fullColor;
            fillImage.raycastTarget = false;

            usesFillAmount = style.fillSprite != null;
            if (usesFillAmount)
            {
                // 스프라이트 채움: 왜곡 없이 좌→우로 채워지는 Filled 방식
                fillImage.sprite = style.fillSprite;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                fillRect.pivot = new Vector2(0.5f, 0.5f);
            }
            else
            {
                // 단색 사각형: 좌측 피벗 기준 가로 스케일로 채움 표현 (스프라이트 불필요)
                fillRect.pivot = new Vector2(0f, 0.5f);
            }

            built = true;
            Refresh();
        }

        /// <summary>RectTransform을 부모에 꽉 채우도록 앵커/오프셋 설정.</summary>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
