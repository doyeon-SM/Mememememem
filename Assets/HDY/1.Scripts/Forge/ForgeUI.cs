using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HDY.Item;
using HDY.Inventory;
using HDY.UI;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>대장간 UI의 현재 탭.</summary>
    public enum ForgeUITab
    {
        Enhance,
        Promotion,
        Refinement,
        Inheritance
    }

    /// <summary>
    /// 대장간(Forge) UI 컨트롤러.
    ///
    /// [슬롯 개념 - 중요] 하단 목록과 가운데 "선택 슬롯"은 전부 표시(view)일 뿐이다. 도구를 고른다고 해서
    /// 그 ItemStack이 인벤토리/창고에서 실제로 빠져나오지 않는다 - 원본 참조(selectedStack)를 그대로 들고
    /// 있다가, 강화/승급 시도 시 ForgeManager가 "그 자리에서" itemId만 갱신한다. 그래서 이 UI를 닫아도
    /// 잃어버릴 아이템이 없다(옮긴 적이 없으므로).
    ///
    /// [하단 목록은 4개 탭 공용] slotListContent/spawnedSlots는 강화/승급/연마/전승 4개 탭이 전부 같이
    /// 쓰는 단일 스크롤 목록이다(씬에서 4개 탭 버튼이 같은 Content Transform을 공유하도록 배치되어 있음).
    /// 그래서 목록의 스캔·인스턴스화·클릭 이벤트는 이 클래스 하나만 담당하고, 연마/전승 탭은 클릭된
    /// ItemStack만 넘겨받아 자기 가운데 패널(ForgeUI_RefinementPanel/ForgeUI_InheritancePanel)에 반영한다.
    ///
    /// [연마/전승 실행 후 목록 갱신] 연마·전승 실행 버튼은 각 패널이 직접 눌러서 ForgeManager를 호출하기
    /// 때문에, 하단 목록을 들고 있는 이 클래스는 실행 시점을 알 방법이 없다. 그래서 각 패널의
    /// RefinementExecuted/InheritanceExecuted 이벤트를 구독해서 실행될 때마다 RefreshList()를 다시 호출한다
    /// (특히 전승은 재료 도구가 소멸하므로 목록 갱신이 꼭 필요함).
    ///
    /// [툴팁 동기화] 이 화면에 있는 모든 ForgeToolSlotUI(하단 목록, 선택 슬롯, 연마/전승 패널의 슬롯들)는
    /// 전부 같은 itemTooltipUI 인스턴스를 써야 한다. ItemTooltipTriggerUI 자체는 비어있으면 씬에서
    /// 아무거나 하나 찾아 쓰지만(FindFirstObjectByType), 씬에 툴팁 UI가 여러 개 있거나 초기화 순서가
    /// 꼬이면 슬롯마다 다른 인스턴스를 붙잡아서 일부 슬롯만 툴팁이 안 뜨는 문제가 생길 수 있다. 그래서
    /// ForgeUI가 자신의 itemTooltipUI 하나를 자신이 관리하는 모든 슬롯에 명시적으로 강제 지정한다
    /// (자동 탐색에 기대지 않음 - Awake에서 이미 있는 슬롯들에, GetOrCreateSlot에서 새로 만든 슬롯에).
    ///
    /// [탭] 강화 탭은 CanEnhance=true인 도구만, 승급 탭은 지금 승급 가능한 상태(EligibleForPromotionNow)인
    /// 도구만 하단 목록에 보여준다. 연마/전승 탭은 연마 가능한 도구(도끼/곡괭이/괭이) 전부를 보여준다.
    /// [HDY 요청] 몽둥이(ForgeToolType.Club)는 4개 탭 전부에서 하단 목록 자체에 아예 나타나지 않는다
    /// (CollectForgeableTools에서 탭 분기보다 먼저 걸러냄) - 강화/승급/연마/전승 전부 대상이 아니다.
    /// 정렬은 티어 내림차순 -> 강화레벨 내림차순.
    ///
    /// [자동 전환] 강화로 10강을 찍으면 자동으로 승급 탭으로 전환하고 같은 아이템을 그대로 선택 상태로 둔다.
    /// 승급에 성공하면 선택을 해제한다(아이템 자체는 그 자리에서 다음 티어로 바뀐 채 남아있음).
    ///
    /// [탭 전환 시 선택 검증] selectedStack은 강화/승급 탭이 공유하는 필드라, 강화 탭에서 아이템을 고른
    /// 채로 승급 탭으로 수동 전환하면 그 아이템이 승급 조건을 만족하지 않아도 가운데 슬롯에 그대로 남아
    /// 보이는 문제가 있었다. SwitchTab에서 새 탭 기준으로 자격을 다시 확인해서, 자격이 안 되면 선택을
    /// 지운다(자동 전환 케이스는 애초에 자격을 만족할 때만 일어나므로 영향 없음).
    ///
    /// [HDY 요청 - 헤더/재료 라벨 텍스트] panelHeaderText("도구 강화"/"도구 승급"), materialLabelText
    /// ("강화 재료"/"승급 재료")를 탭 전환 시 SwitchTab에서 갱신한다. materialNameText는 선택된 재료
    /// 아이템의 이름을 RefreshMiddlePanel에서 표시한다.
    ///
    /// [HDY 요청 - 활성 탭 표시 이미지] 각 탭 버튼마다 활성 상태를 나타내는 이미지(enhanceTabActiveImage 등)를
    /// SetTabAlpha와 같은 타이밍에 SwitchTab에서 함께 켜고 끈다.
    ///
    /// [HDY 요청 - 과열 게이지 완전 교체] 기존 Slider(overheatSlider)를 제거하고 원형 게이지
    /// (overheatGaugeImage, Image의 Fill Type: Radial 360 / Clockwise, 인스펙터에서 직접 설정)로
    /// 완전히 대체했다. 값이 바뀔 때마다 DOTween(DOFillAmount)으로 overheatGaugeFillDuration(기본 0.3초)
    /// 동안 부드럽게 채워진다(SetOverheatGauge). 선택 해제 등 리셋 상황에서는 즉시(immediate) 값을 맞춘다.
    ///
    /// [HDY 요청 - 실패 시 과열 상승분 표시] 강화/승급 실패로 과열이 오르면 overheatGainText에
    /// "+50%"처럼 이번에 오른 수치를 표시하고, 페이드인 -> 위로 떠오름 -> 페이드아웃 연출로
    /// overheatGainPopupDuration(기본 1초) 동안 보여준다(ShowOverheatGainPopup). 기준 위치를 Awake에서
    /// 미리 캐싱해두고 매번 그 위치로 되돌린 뒤 시작하므로, 연속 실패해도 위치가 계속 위로 밀리지 않는다.
    /// 오른 수치는 시도 직전에 저장해둔 lastDisplayedOverheatPercent와 결과의 OverheatPercent 차이로 계산한다.
    ///
    /// [HDY 요청 - 하단 슬롯 상태 표시 이미지] RefreshList()에서 매 항목마다 IsStackInUse로 "지금 강화/
    /// 승급/연마 중이거나 전승 대기 중(재료/대상으로 선택됨)"인지 판단해서 ForgeToolSlotUI.SetInUseIndicator를
    /// 켜고 끈다. 강화/승급은 이 클래스의 selectedStack, 연마는 refinementPanel.SelectedStack, 전승은
    /// inheritancePanel.MaterialStack/TargetStack을 각각 기준으로 삼는다(선택 상태를 들고 있는 쪽이 서로
    /// 다른 클래스라 각 패널에 읽기 전용 프로퍼티를 노출해뒀다). 전승 탭에서는 추가로
    /// IsBlockedForInheritance로, 이미 재료가 선택된 상태에서 ObjectType이 달라 대상으로 고를 수 없는
    /// 도구에 SetInheritanceBlockedIndicator를 켠다(재료 자신은 "불가"가 아니라 "사용 중"으로 표시됨).
    ///
    /// [HDY 요청 - 버그 수정: 선택 취소 시 하단 목록 갱신] 강화/승급의 ClearSelection, 연마/전승 각 패널의
    /// 선택 취소(클릭으로 슬롯 비우기)가 전부 RefreshList()로 이어지도록 했다 - 예전에는 강화/승급은
    /// ClearSelection이 RefreshList를 아예 안 불렀고, 연마는 선택 취소 기능 자체가 없었고(추가함), 전승은
    /// 취소해도 ForgeUI에 알리지 않아서, 도구를 빼도 하단 목록의 "사용 중"/"전승불가" 표시 이미지가 그대로
    /// 남아있는 문제가 4개 탭 전부에 있었다. 연마/전승은 SelectionChanged 이벤트를 구독해서 처리한다.
    ///
    /// [HDY 요청 - 도움말 안내 패널] ContentSizeFitter가 붙은 공용 컨테이너(infoGuideContainer) 안에 강화/
    /// 승급/연마/전승 안내 패널 4개가 전부 미리 배치되어 있고 평소엔 꺼져 있다. 강화/승급은 같은 info
    /// 아이콘(enhancePromotionInfoTrigger)을 공유하며 지금 탭에 따라 강화/승급 안내 중 하나를 보여주고,
    /// 연마(refinementInfoTrigger)/전승(inheritanceInfoTrigger)은 각자 고정된 안내만 보여준다. 위치는
    /// 고정이라 호버 시 컨테이너와 해당 패널 하나만 켜고, 벗어나면 컨테이너를 끈다(ShowInfoGuide/
    /// HideInfoGuide). 각 트리거는 Forge를 전혀 모르는 범용 컴포넌트(ForgeInfoHoverTriggerUI)라 이 클래스가
    /// 이벤트를 구독해서 무엇을 보여줄지 직접 판단한다. 안내 문구 내용 자체는 도연님이 직접 채운다 - 이
    /// 클래스는 켜고 끄는 로직과 컨테이너의 ContentSizeFitter 강제 재계산(LayoutRebuilder)만 담당한다.
    /// </summary>
    public class ForgeUI : MonoBehaviour
    {
        [Header("탭")]
        [SerializeField] private Button enhanceTabButton;
        [SerializeField] private Button promotionTabButton;
        [SerializeField] private Button refinementTabButton;
        [SerializeField] private Button inheritanceTabButton;

        [Tooltip("각 탭 버튼에 붙은 CanvasGroup. 선택된 탭은 완전 불투명, 나머지는 반투명하게 표시한다 " +
                 "([HDY 요청] 예전에는 SetActive로 선택 안 된 탭 버튼 전체를 꺼버려서 클릭 자체가 막히는 " +
                 "문제가 있었다 - 버튼은 항상 활성 상태로 두고 투명도만 바꾸도록 변경).")]
        [SerializeField] private CanvasGroup enhanceTabGroup;
        [SerializeField] private CanvasGroup promotionTabGroup;
        [SerializeField] private CanvasGroup refinementTabGroup;
        [SerializeField] private CanvasGroup inheritanceTabGroup;

        [Tooltip("현재 선택되지 않은 탭 버튼의 투명도 (0=완전 투명, 1=완전 불투명)")]
        [Range(0f, 1f)]
        [SerializeField] private float unselectedTabAlpha = 0.5f;

        [Tooltip("각 탭 버튼의 활성 상태를 나타내는 별도 이미지(HDY 요청). 현재 열려있는 탭에 해당하는 것만 켜진다.")]
        [SerializeField] private GameObject enhanceTabActiveImage;
        [SerializeField] private GameObject promotionTabActiveImage;
        [SerializeField] private GameObject refinementTabActiveImage;
        [SerializeField] private GameObject inheritanceTabActiveImage;

        [Header("닫기 (선택)")]
        [SerializeField] private Button closeButton;

        [Header("하단 목록 (10 x n 스크롤 - 4개 탭 공용)")]
        [SerializeField] private Transform slotListContent;
        [SerializeField] private ForgeToolSlotUI slotPrefab;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("툴팁 (이 화면의 모든 슬롯이 공유하는 단일 인스턴스)")]
        [SerializeField] private ItemTooltipUI itemTooltipUI;

        [Header("강화/승급 전용 - 가운데 패널 루트 (탭 전환 시 이 루트를 통째로 켜고 끔)")]
        [SerializeField] private GameObject enhancePromotionPanelRoot;
        [Tooltip("\"도구 강화\"/\"도구 승급\" 헤더 텍스트(HDY 요청). 탭 전환 시 갱신된다.")]
        [SerializeField] private TMP_Text panelHeaderText;

        [Header("가운데 - 선택 슬롯")]
        [SerializeField] private ForgeToolSlotUI selectedSlotDisplay;
        [SerializeField] private GameObject selectedEmptyHint;

        [Tooltip("가운데에 현재 강화/승급 대상 도구의 이름을 표시하는 텍스트 (선택 없으면 비움)")]
        [SerializeField] private TMP_Text selectedItemNameText;

        [Header("가운데 - 모루 과열 (HDY 요청 - 원형 게이지로 완전 교체)")]
        [Tooltip("Image의 Fill Type: Radial 360 / Fill Method: Clockwise로 설정된 원형 게이지. 기존 Slider를 완전히 대체한다.")]
        [SerializeField] private Image overheatGaugeImage;
        [SerializeField] private TMP_Text overheatPercentText;
        [Tooltip("과열 게이지가 채워지는 데 걸리는 시간(초). DOTween DOFillAmount로 부드럽게 애니메이션된다.")]
        [SerializeField] private float overheatGaugeFillDuration = 0.3f;

        [Header("가운데 - 실패 시 과열 상승분 표시 (HDY 요청)")]
        [Tooltip("실패로 과열이 오르면 \"+50%\"처럼 표시되는 텍스트. 위치는 미리 배치해두면 된다(코드가 그 위치를 기준점으로 기억해서 매번 되돌아온다).")]
        [SerializeField] private TMP_Text overheatGainText;
        [Tooltip("오른 수치 텍스트가 화면에 보이는 총 시간(초). 페이드인/아웃 포함.")]
        [SerializeField] private float overheatGainPopupDuration = 1f;
        [Tooltip("오른 수치 텍스트가 위로 떠오르는 거리(픽셀).")]
        [SerializeField] private float overheatGainPopupRiseDistance = 30f;

        [Header("가운데 - 확률/재료/골드")]
        [SerializeField] private TMP_Text successRateText;
        [Tooltip("\"강화 재료\"/\"승급 재료\" 라벨 텍스트(HDY 요청). 탭 전환 시 갱신된다.")]
        [SerializeField] private TMP_Text materialLabelText;
        [SerializeField] private Image materialIconImage;
        [Tooltip("재료 아이템 이름 표시(HDY 요청).")]
        [SerializeField] private TMP_Text materialNameText;
        [SerializeField] private TMP_Text materialCountText;
        [SerializeField] private TMP_Text goldCostText;

        [Header("가운데 - 실행 버튼")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;
        [SerializeField] private CanvasGroup actionButtonGroup;
        [Range(0f, 1f)]
        [SerializeField] private float disabledButtonAlpha = 0.5f;

        [Header("부족 표시 색상")]
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color shortageTextColor = Color.red;

        [Header("도움말 - 안내 패널 (HDY 요청, 고정 위치에서 켜고 끔)")]
        [Tooltip("ContentSizeFitter가 붙은 공용 컨테이너 - 강화/승급/연마/전승 안내 패널 4개를 감싼다.")]
        [SerializeField] private GameObject infoGuideContainer;
        [SerializeField] private GameObject enhanceGuidePanel;
        [SerializeField] private GameObject promotionGuidePanel;
        [SerializeField] private GameObject refinementGuidePanel;
        [SerializeField] private GameObject inheritanceGuidePanel;
        [Tooltip("강화/승급 패널 쪽 info 아이콘 - 같은 아이콘을 공유하며, 지금 탭에 따라 강화/승급 안내 중 하나를 보여준다.")]
        [SerializeField] private ForgeInfoHoverTriggerUI enhancePromotionInfoTrigger;
        [Tooltip("연마 패널 쪽 info 아이콘 - 항상 연마 안내만 보여준다.")]
        [SerializeField] private ForgeInfoHoverTriggerUI refinementInfoTrigger;
        [Tooltip("전승 패널 쪽 info 아이콘 - 항상 전승 안내만 보여준다.")]
        [SerializeField] private ForgeInfoHoverTriggerUI inheritanceInfoTrigger;

        [Header("연마/전승 전용 - 패널 (탭 전환 시 GameObject를 켜고 끔, 목록 클릭은 이 클래스가 전달해줌)")]
        [SerializeField] private GameObject refinementPanelRoot;
        [SerializeField] private ForgeUI_RefinementPanel refinementPanel;
        [SerializeField] private GameObject inheritancePanelRoot;
        [SerializeField] private ForgeUI_InheritancePanel inheritancePanel;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;

        private ForgeUITab currentTab = ForgeUITab.Enhance;
        private ItemStack selectedStack;
        private readonly List<ForgeToolSlotUI> spawnedSlots = new List<ForgeToolSlotUI>();

        // [HDY 요청 - 과열 게이지 애니메이션] 진행 중인 트윈을 기억해뒀다가, 값이 또 바뀌면 이전 트윈부터
        // 죽이고 새로 시작한다(겹쳐서 어색하게 움직이는 것을 방지).
        private Tween overheatGaugeTween;
        private Tween overheatGainTween;
        private Vector2 overheatGainTextBasePosition;

        // [HDY 요청 - 실패 시 과열 상승분 계산용] 직전에 화면에 표시했던 과열 수치. 시도 직전 값을 여기서
        // 가져와서 결과값과 비교하면 "이번에 오른 만큼"을 알 수 있다.
        private float lastDisplayedOverheatPercent;

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (itemTooltipUI == null) itemTooltipUI = GetComponentInChildren<ItemTooltipUI>(true);

            if (enhanceTabButton != null) enhanceTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Enhance));
            if (promotionTabButton != null) promotionTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Promotion));
            if (refinementTabButton != null) refinementTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Refinement));
            if (inheritanceTabButton != null) inheritanceTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Inheritance));
            if (actionButton != null) actionButton.onClick.AddListener(HandleActionButtonClicked);
            if (closeButton != null) closeButton.onClick.AddListener(() => UIManager.Instance?.CloseCurrent());

            if (selectedSlotDisplay != null) selectedSlotDisplay.Clicked += _ => ClearSelection();

            // 이미 씬/프리팹에 존재하는 슬롯들(선택 슬롯, 연마/전승 패널 내부 슬롯)에 툴팁 UI를 동기화한다.
            selectedSlotDisplay?.SetTooltipUI(itemTooltipUI);
            refinementPanel?.SetTooltipUI(itemTooltipUI);
            inheritancePanel?.SetTooltipUI(itemTooltipUI);

            // 연마/전승은 각 패널이 직접 실행하므로, 실행 완료를 이벤트로 통보받아 하단 목록을 갱신한다.
            if (refinementPanel != null) refinementPanel.RefinementExecuted += RefreshList;
            if (inheritancePanel != null) inheritancePanel.InheritanceExecuted += RefreshList;

            // [HDY 요청 - 버그 수정] 선택/선택 취소가 바뀔 때마다도(실행과 무관하게) 하단 목록의 상태
            // 표시 이미지를 다시 계산해야 한다 - 특히 선택 취소는 이 패널들이 자체적으로 처리해서
            // ForgeUI가 모를 수 있었다.
            if (refinementPanel != null) refinementPanel.SelectionChanged += RefreshList;
            if (inheritancePanel != null) inheritancePanel.SelectionChanged += RefreshList;

            // [HDY 요청 - 도움말 안내 패널] 강화/승급은 같은 아이콘을 공유하며 지금 탭에 따라 보여줄 안내가
            // 다르고, 연마/전승은 각자 고정된 안내만 보여준다.
            if (enhancePromotionInfoTrigger != null)
            {
                enhancePromotionInfoTrigger.OnHoverEnter += HandleEnhancePromotionInfoHoverEnter;
                enhancePromotionInfoTrigger.OnHoverExit += HideInfoGuide;
            }

            if (refinementInfoTrigger != null)
            {
                refinementInfoTrigger.OnHoverEnter += HandleRefinementInfoHoverEnter;
                refinementInfoTrigger.OnHoverExit += HideInfoGuide;
            }

            if (inheritanceInfoTrigger != null)
            {
                inheritanceInfoTrigger.OnHoverEnter += HandleInheritanceInfoHoverEnter;
                inheritanceInfoTrigger.OnHoverExit += HideInfoGuide;
            }

            // [HDY 요청 - 실패 시 과열 상승분 표시] 미리 배치해둔 위치를 기준점으로 캐싱해서, 매번 그 위치로
            // 되돌린 뒤 애니메이션을 시작한다(연속 실패해도 위로 계속 밀리지 않도록).
            // [HDY 요청 - 버그 수정] 씬에 배치된 원래 알파값이 그대로 남아있으면 패널을 열자마자 이 텍스트가
            // 보여버리는 문제가 있었다 - 처음엔 반드시 꺼진(알파 0) 상태로 시작해야 하므로 여기서 명시적으로
            // 맞춰준다(ShowOverheatGainPopup이 호출되기 전까지는 계속 이 상태로 남는다).
            if (overheatGainText != null)
            {
                overheatGainTextBasePosition = overheatGainText.rectTransform.anchoredPosition;

                var initialColor = overheatGainText.color;
                initialColor.a = 0f;
                overheatGainText.color = initialColor;
            }
        }

        private void OnDestroy()
        {
            if (refinementPanel != null) refinementPanel.RefinementExecuted -= RefreshList;
            if (inheritancePanel != null) inheritancePanel.InheritanceExecuted -= RefreshList;
            if (refinementPanel != null) refinementPanel.SelectionChanged -= RefreshList;
            if (inheritancePanel != null) inheritancePanel.SelectionChanged -= RefreshList;

            if (enhancePromotionInfoTrigger != null)
            {
                enhancePromotionInfoTrigger.OnHoverEnter -= HandleEnhancePromotionInfoHoverEnter;
                enhancePromotionInfoTrigger.OnHoverExit -= HideInfoGuide;
            }

            if (refinementInfoTrigger != null)
            {
                refinementInfoTrigger.OnHoverEnter -= HandleRefinementInfoHoverEnter;
                refinementInfoTrigger.OnHoverExit -= HideInfoGuide;
            }

            if (inheritanceInfoTrigger != null)
            {
                inheritanceInfoTrigger.OnHoverEnter -= HandleInheritanceInfoHoverEnter;
                inheritanceInfoTrigger.OnHoverExit -= HideInfoGuide;
            }

            // [HDY 요청 - 과열 게이지/실패 팝업 애니메이션] 이 오브젝트가 파괴될 때 진행 중인 트윈이 남아있으면
            // 파괴된 대상을 계속 건드리려다 에러가 날 수 있어 확실히 정리한다.
            overheatGaugeTween?.Kill();
            overheatGainTween?.Kill();
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents(true);
            SwitchTab(ForgeUITab.Enhance);
        }

        private void OnDisable()
        {
            SubscribeInventoryEvents(false);
        }

        private void SubscribeInventoryEvents(bool subscribe)
        {
            if (playerInventory != null)
            {
                if (subscribe) playerInventory.OnInventoryChanged += HandleContainersChanged;
                else playerInventory.OnInventoryChanged -= HandleContainersChanged;
            }

            if (warehouseInventory != null)
            {
                if (subscribe) warehouseInventory.OnStorageChanged += HandleContainersChanged;
                else warehouseInventory.OnStorageChanged -= HandleContainersChanged;
            }
        }

        private void HandleContainersChanged()
        {
            // 하단 목록은 4개 탭 공용이라 어떤 탭이든 항상 다시 스캔한다.
            RefreshList();

            if (currentTab == ForgeUITab.Enhance || currentTab == ForgeUITab.Promotion)
            {
                RefreshMiddlePanel();
            }
        }

        /// <summary>
        /// [HDY 요청 - 탭 반투명 표시] 선택된 탭은 완전 불투명(1), 나머지는 unselectedTabAlpha로 표시한다.
        /// 예전의 SetActive(GameObject 전체 끄기)와 달리 버튼 자체는 항상 활성 상태로 유지되므로,
        /// 선택되지 않은 탭도 계속 클릭해서 전환할 수 있다(반투명해질 뿐 비활성화되지 않음).
        /// </summary>
        private void SetTabAlpha(CanvasGroup group, bool isSelected)
        {
            if (group == null) return;
            group.alpha = isSelected ? 1f : unselectedTabAlpha;
        }

        private void SwitchTab(ForgeUITab tab)
        {
            currentTab = tab;

            SetTabAlpha(enhanceTabGroup, tab == ForgeUITab.Enhance);
            SetTabAlpha(promotionTabGroup, tab == ForgeUITab.Promotion);
            SetTabAlpha(refinementTabGroup, tab == ForgeUITab.Refinement);
            SetTabAlpha(inheritanceTabGroup, tab == ForgeUITab.Inheritance);

            // [HDY 요청 - 활성 탭 표시 이미지] 지금 열려있는 탭에 해당하는 이미지만 켠다.
            if (enhanceTabActiveImage != null) enhanceTabActiveImage.SetActive(tab == ForgeUITab.Enhance);
            if (promotionTabActiveImage != null) promotionTabActiveImage.SetActive(tab == ForgeUITab.Promotion);
            if (refinementTabActiveImage != null) refinementTabActiveImage.SetActive(tab == ForgeUITab.Refinement);
            if (inheritanceTabActiveImage != null) inheritanceTabActiveImage.SetActive(tab == ForgeUITab.Inheritance);

            bool isEnhanceOrPromotion = tab == ForgeUITab.Enhance || tab == ForgeUITab.Promotion;

            // 강화/승급 전용 가운데 패널만 탭에 따라 켜고 끈다. 하단 목록(slotListContent)은
            // 4개 탭이 공유하는 영역이라 항상 켜둔 채로, 내용만 RefreshList()에서 새로 그린다.
            if (enhancePromotionPanelRoot != null) enhancePromotionPanelRoot.SetActive(isEnhanceOrPromotion);

            if (refinementPanelRoot != null) refinementPanelRoot.SetActive(tab == ForgeUITab.Refinement);
            if (inheritancePanelRoot != null) inheritancePanelRoot.SetActive(tab == ForgeUITab.Inheritance);

            if (isEnhanceOrPromotion)
            {
                bool isEnhanceTab = tab == ForgeUITab.Enhance;

                if (actionButtonLabel != null)
                {
                    actionButtonLabel.text = isEnhanceTab ? "강화하기" : "승급하기";
                }

                // [HDY 요청 - 헤더/재료 라벨 텍스트]
                if (panelHeaderText != null) panelHeaderText.text = isEnhanceTab ? "도구 강화" : "도구 승급";
                if (materialLabelText != null) materialLabelText.text = isEnhanceTab ? "강화 재료" : "승급 재료";

                // 강화<->승급 탭 전환 시, 현재 선택된 아이템이 "새 탭" 기준으로도 자격이 되는지 재검증한다.
                // 자격이 안 되면 선택을 지운다 - 10강 달성으로 인한 자동 전환은 그 시점에 이미 자격을
                // 만족하므로 이 검증에 영향받지 않는다.
                if (selectedStack != null && !selectedStack.IsEmpty && forgeManager != null)
                {
                    var descriptor = forgeManager.Describe(selectedStack);
                    bool stillEligible = tab == ForgeUITab.Enhance ? descriptor.CanEnhance : descriptor.EligibleForPromotionNow;

                    if (!descriptor.IsForgeable || !stillEligible)
                    {
                        selectedStack = null;
                    }
                }
            }

            RefreshList();

            if (isEnhanceOrPromotion)
            {
                RefreshMiddlePanel();
            }
        }

        /// <summary>하단 목록을 다시 스캔·필터링·정렬해서 그린다. 인벤토리/창고 변경 이벤트, 탭 전환, 연마/전승 실행 후에도 호출된다.</summary>
        private void RefreshList()
        {
            var entries = CollectForgeableTools();

            for (int i = 0; i < entries.Count; i++)
            {
                var slot = GetOrCreateSlot(i);
                var stack = entries[i].stack;
                var displayData = catalogManager != null ? catalogManager.FindItemData(stack.itemId) : null;
                slot.Bind(stack, displayData);
                slot.gameObject.SetActive(true);

                // [HDY 요청 - 하단 슬롯 상태 표시 이미지]
                slot.SetInUseIndicator(IsStackInUse(stack));
                slot.SetInheritanceBlockedIndicator(currentTab == ForgeUITab.Inheritance && IsBlockedForInheritance(stack));
            }

            for (int i = entries.Count; i < spawnedSlots.Count; i++)
            {
                spawnedSlots[i].Clear();
                spawnedSlots[i].gameObject.SetActive(false);
            }
        }

        private ForgeToolSlotUI GetOrCreateSlot(int index)
        {
            if (index < spawnedSlots.Count) return spawnedSlots[index];

            var slot = Instantiate(slotPrefab, slotListContent);
            slot.Clicked += HandleListSlotClicked;
            slot.SetTooltipUI(itemTooltipUI); // 새로 만든 슬롯도 반드시 같은 툴팁 UI를 쓰도록 동기화
            spawnedSlots.Add(slot);
            return slot;
        }

        /// <summary>
        /// 인벤토리(일반+퀵슬롯) + 창고에서 대장간 대상 도구만 모아, 현재 탭 조건으로 필터링하고 정렬한다.
        /// 연마/전승 탭은 종류(도끼/곡괭이/괭이) 전부를 대상으로 하므로 IsForgeable이면 통과시킨다.
        /// </summary>
        private List<(ItemStack stack, ForgeItemDescriptor descriptor)> CollectForgeableTools()
        {
            var results = new List<(ItemStack, ForgeItemDescriptor)>();
            if (forgeManager == null) return results;

            void CollectFrom(InventoryContainer container)
            {
                if (container?.slots == null) return;

                foreach (var slot in container.slots)
                {
                    if (slot == null || slot.IsEmpty) continue;
                    if (!forgeManager.IsForgeableItem(slot.itemId)) continue;

                    var descriptor = forgeManager.Describe(slot);
                    if (!descriptor.IsForgeable) continue;

                    // [HDY 요청] 몽둥이(Club)는 대장간 하단 목록에서 4개 탭 전부 제외한다(강화/승급/연마/
                    // 전승 전부 불가) - 탭별 분기보다 먼저 걸러낸다.
                    if (descriptor.ToolType == ForgeToolType.Club) continue;

                    bool matchesTab;
                    switch (currentTab)
                    {
                        case ForgeUITab.Enhance:
                            matchesTab = descriptor.CanEnhance;
                            break;
                        case ForgeUITab.Promotion:
                            matchesTab = descriptor.EligibleForPromotionNow;
                            break;
                        case ForgeUITab.Refinement:
                        case ForgeUITab.Inheritance:
                            matchesTab = true; // 몽둥이는 이미 위에서 걸러졌으므로 도끼/곡괭이/괭이만 남는다
                            break;
                        default:
                            matchesTab = false;
                            break;
                    }

                    if (!matchesTab) continue;

                    results.Add((slot, descriptor));
                }
            }

            if (playerInventory != null)
            {
                CollectFrom(playerInventory.inventory);
                CollectFrom(playerInventory.quickSlots);
            }

            if (warehouseInventory != null)
            {
                CollectFrom(warehouseInventory.storage);
            }

            // 높은 티어 > 강화순으로 정렬.
            return results
                .OrderByDescending(e => e.Item2.TierIndex)
                .ThenByDescending(e => e.Item2.EnhanceLevel)
                .ToList();
        }

        /// <summary>하단 목록 클릭은 현재 탭에 따라 처리 대상이 다르다 - 강화/승급은 이 클래스가, 연마/전승은 각 패널이 담당.</summary>
        private void HandleListSlotClicked(ForgeToolSlotUI slot)
        {
            if (slot == null || slot.BoundStack == null) return;

            switch (currentTab)
            {
                case ForgeUITab.Enhance:
                case ForgeUITab.Promotion:
                    selectedStack = slot.BoundStack;
                    RefreshMiddlePanel();
                    break;
                case ForgeUITab.Refinement:
                    refinementPanel?.HandleToolSelected(slot.BoundStack);
                    break;
                case ForgeUITab.Inheritance:
                    inheritancePanel?.HandleToolSelected(slot.BoundStack);
                    break;
            }

            // [HDY 요청 - 하단 슬롯 상태 표시 이미지] 연마/전승은 선택 상태를 각 패널이 들고 있어서 이 클래스가
            // 클릭만으로는 알 수 없다 - 위에서 넘겨준 뒤 다시 목록을 그려서 최신 선택 상태를 반영한다.
            RefreshList();
        }

        /// <summary>[HDY 요청 - 버그 수정] 선택 취소 시에도 하단 목록의 "사용 중" 표시 이미지가 갱신되도록 RefreshList()를 함께 호출한다.</summary>
        private void ClearSelection()
        {
            selectedStack = null;
            RefreshList();
            RefreshMiddlePanel();
        }

        /// <summary>가운데 패널(선택 슬롯/이름/과열/확률/재료/골드/버튼)을 현재 선택·탭 기준으로 다시 그린다. 강화/승급 탭 전용.</summary>
        private void RefreshMiddlePanel()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;

            if (selectedEmptyHint != null) selectedEmptyHint.SetActive(!hasSelection);

            if (!hasSelection)
            {
                selectedSlotDisplay?.Clear();
                if (selectedItemNameText != null) selectedItemNameText.text = string.Empty;
                SetActionButtonInteractable(false);

                SetOverheatGauge(0f, immediate: true);
                if (overheatPercentText != null) overheatPercentText.text = "0%";
                if (successRateText != null) successRateText.text = "-";
                if (materialIconImage != null) materialIconImage.enabled = false;
                if (materialNameText != null) materialNameText.text = "-";
                if (materialCountText != null) materialCountText.text = "-";
                if (goldCostText != null)
                {
                    goldCostText.text = "-";
                    LayoutRebuilder.ForceRebuildLayoutImmediate(goldCostText.rectTransform);
                }
                return;
            }

            var displayData = catalogManager != null ? catalogManager.FindItemData(selectedStack.itemId) : null;
            selectedSlotDisplay?.Bind(selectedStack, displayData);

            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = displayData != null ? displayData.ItemName : string.Empty;
            }

            var actionType = currentTab == ForgeUITab.Enhance ? ForgeActionType.Enhance : ForgeActionType.Promotion;
            var preview = forgeManager.GetPreview(selectedStack, actionType);

            SetOverheatGauge(preview.OverheatPercent, immediate: false);
            if (overheatPercentText != null) overheatPercentText.text = FormatPercent(preview.OverheatPercent);

            if (successRateText != null)
            {
                successRateText.text = preview.IsGuaranteed ? "100% (보장)" : FormatPercent(preview.SuccessRate);
            }

            var materialItemData = !string.IsNullOrEmpty(preview.MaterialItemId) && catalogManager != null
                ? catalogManager.FindItemData(preview.MaterialItemId)
                : null;

            if (materialIconImage != null)
            {
                materialIconImage.sprite = materialItemData != null ? materialItemData.ItemIcon : null;
                materialIconImage.enabled = materialItemData != null && materialItemData.ItemIcon != null;
            }

            // [HDY 요청 - 재료 이름 노출]
            if (materialNameText != null)
            {
                materialNameText.text = materialItemData != null ? materialItemData.ItemName : string.Empty;
            }

            bool materialShortage = preview.MaterialOwned < preview.MaterialCost;
            if (materialCountText != null)
            {
                materialCountText.text = $"{preview.MaterialOwned} / {preview.MaterialCost}";
                materialCountText.color = materialShortage ? shortageTextColor : normalTextColor;
            }

            bool goldShortage = preview.GoldOwned < preview.GoldCost;
            if (goldCostText != null)
            {
                goldCostText.text = $"{preview.GoldOwned} / {preview.GoldCost}";
                goldCostText.color = goldShortage ? shortageTextColor : normalTextColor;

                // [HDY 요청 - 버그 수정] goldCostText에 ContentSizeFitter가 붙어있어서, 특히 처음 값이
                // 채워질 때(그 전까지 비활성 상태였다가 켜지는 등) 다음 레이아웃 패스 전까지 압축된 옛
                // 크기로 남아 텍스트가 이상한 위치에 보이는 문제가 있었다 - 강제로 즉시 다시 계산한다.
                LayoutRebuilder.ForceRebuildLayoutImmediate(goldCostText.rectTransform);
            }

            bool canExecute = preview.BlockReason == ForgeFailReason.None && !materialShortage && !goldShortage;
            SetActionButtonInteractable(canExecute);
        }

        private void SetActionButtonInteractable(bool enabled)
        {
            if (actionButton != null) actionButton.interactable = enabled;

            if (actionButtonGroup != null)
            {
                actionButtonGroup.alpha = enabled ? 1f : disabledButtonAlpha;
                actionButtonGroup.interactable = enabled;
                actionButtonGroup.blocksRaycasts = enabled;
            }
        }

        private void HandleActionButtonClicked()
        {
            if (selectedStack == null || selectedStack.IsEmpty || forgeManager == null) return;

            // [HDY 요청 - 실패 시 과열 상승분 계산용] 시도 직전에 화면에 표시되어 있던 과열 수치를 기준으로 잡아둔다.
            float overheatBeforeAttempt = lastDisplayedOverheatPercent;

            var outcome = currentTab == ForgeUITab.Enhance
                ? forgeManager.TryEnhance(selectedStack)
                : forgeManager.TryPromote(selectedStack);

            if (!outcome.Attempted) return;

            // [HDY 요청 - 실패 시 과열 상승분 표시] 과열이 올랐다면(=이번 시도가 실패해서 충전됐다면) "+n%" 팝업을 띄운다.
            float overheatDelta = outcome.OverheatPercent - overheatBeforeAttempt;
            if (overheatDelta > 0f)
            {
                ShowOverheatGainPopup(overheatDelta);
            }

            if (currentTab == ForgeUITab.Enhance)
            {
                // 강화 성공/실패와 무관하게 아이템은 슬롯(=원래 있던 인벤토리/창고 칸)에 그대로 유지된다.
                if (outcome.Result == ForgeAttemptResult.Success && outcome.ReachedMaxEnhanceLevel)
                {
                    // 10강 달성 - 승급 탭으로 자동 전환하고 같은 아이템을 그대로 선택 상태로 유지한다.
                    SwitchTab(ForgeUITab.Promotion);
                    return;
                }
            }
            else
            {
                if (outcome.Result == ForgeAttemptResult.Success)
                {
                    // 승급 성공 - 아이템 자체가 다음 티어로 바뀌었으므로 선택을 해제한다.
                    selectedStack = null;
                }
            }

            RefreshList();
            RefreshMiddlePanel();
        }

        private static string FormatPercent(float value01)
        {
            return $"{value01 * 100f:0.#}%";
        }

        /// <summary>
        /// [HDY 요청 - 과열 게이지 완전 교체] Slider 대신 Image(Fill Type: Radial 360, Clockwise)로 교체했다.
        /// DOTween(DOFillAmount)으로 overheatGaugeFillDuration(기본 0.3초) 동안 부드럽게 채워지는 연출을
        /// 준다. immediate=true면(선택 해제 등 리셋 상황) 애니메이션 없이 즉시 값을 맞춘다. 값이 또 바뀌면
        /// 진행 중이던 트윈부터 죽이고 새로 시작한다.
        /// </summary>
        private void SetOverheatGauge(float value01, bool immediate)
        {
            lastDisplayedOverheatPercent = value01;

            if (overheatGaugeImage == null) return;

            overheatGaugeTween?.Kill();

            if (immediate)
            {
                overheatGaugeImage.fillAmount = value01;
            }
            else
            {
                overheatGaugeTween = overheatGaugeImage.DOFillAmount(value01, overheatGaugeFillDuration).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// [HDY 요청 - 실패 시 과열 상승분 표시] "+50%"처럼 이번에 오른 과열 수치를 overheatGainText에
        /// 표시하고, 페이드인 -> 위로 떠오름 -> 페이드아웃 연출로 overheatGainPopupDuration(기본 1초) 동안
        /// 보여준다. 매번 Awake에서 캐싱해둔 기준 위치(overheatGainTextBasePosition)로 되돌린 뒤 다시
        /// 시작하므로, 연속으로 실패해도 위치가 계속 위로 밀리지 않는다.
        /// </summary>
        private void ShowOverheatGainPopup(float delta01)
        {
            if (overheatGainText == null || delta01 <= 0f) return;

            overheatGainTween?.Kill();

            overheatGainText.text = $"+{delta01 * 100f:0.#}%";
            overheatGainText.rectTransform.anchoredPosition = overheatGainTextBasePosition;

            var color = overheatGainText.color;
            color.a = 0f;
            overheatGainText.color = color;

            const float fadeInDuration = 0.15f;
            const float fadeOutDuration = 0.3f;
            float fadeOutStart = Mathf.Max(fadeInDuration, overheatGainPopupDuration - fadeOutDuration);

            var sequence = DOTween.Sequence();
            sequence.Append(overheatGainText.DOFade(1f, fadeInDuration));
            sequence.Join(overheatGainText.rectTransform.DOAnchorPosY(
                overheatGainTextBasePosition.y + overheatGainPopupRiseDistance, overheatGainPopupDuration).SetEase(Ease.OutQuad));
            sequence.Insert(fadeOutStart, overheatGainText.DOFade(0f, fadeOutDuration));

            overheatGainTween = sequence;
        }

        // [HDY 요청 - 도움말 안내 패널] 강화/승급은 같은 아이콘을 공유하므로, 호버 시점의 currentTab을 보고
        // 강화/승급 안내 중 하나를 고른다. 연마/전승은 각자 고정된 안내만 보여준다.
        private void HandleEnhancePromotionInfoHoverEnter()
        {
            ShowInfoGuide(currentTab == ForgeUITab.Enhance ? enhanceGuidePanel : promotionGuidePanel);
        }

        private void HandleRefinementInfoHoverEnter()
        {
            ShowInfoGuide(refinementGuidePanel);
        }

        private void HandleInheritanceInfoHoverEnter()
        {
            ShowInfoGuide(inheritanceGuidePanel);
        }

        /// <summary>
        /// [HDY 요청 - 도움말 안내 패널] 공용 컨테이너(infoGuideContainer, ContentSizeFitter 붙음)를 켜고,
        /// 강화/승급/연마/전승 안내 패널 4개 중 panel로 넘어온 것 하나만 활성화한다(나머지는 비활성화).
        /// 컨테이너 활성화 직후 ContentSizeFitter가 자식 크기에 맞춰 다시 계산되도록 강제로 즉시 레이아웃을
        /// 다시 계산한다(MemSlotUI에 적용했던 것과 동일한 이유 - 다음 레이아웃 패스 전까지 옛 크기로 남아있는
        /// 문제 방지).
        /// </summary>
        private void ShowInfoGuide(GameObject panel)
        {
            if (infoGuideContainer != null) infoGuideContainer.SetActive(true);

            if (enhanceGuidePanel != null) enhanceGuidePanel.SetActive(enhanceGuidePanel == panel);
            if (promotionGuidePanel != null) promotionGuidePanel.SetActive(promotionGuidePanel == panel);
            if (refinementGuidePanel != null) refinementGuidePanel.SetActive(refinementGuidePanel == panel);
            if (inheritanceGuidePanel != null) inheritanceGuidePanel.SetActive(inheritanceGuidePanel == panel);

            if (infoGuideContainer != null && infoGuideContainer.transform is RectTransform containerRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
            }
        }

        private void HideInfoGuide()
        {
            if (infoGuideContainer != null) infoGuideContainer.SetActive(false);
        }

        /// <summary>
        /// [HDY 요청 - 하단 슬롯 상태 표시 이미지] 지금 탭 기준으로 이 stack이 "사용 중"(강화/승급 대상으로
        /// 선택됨, 연마 대상으로 선택됨, 전승 재료/대상으로 선택됨)인지 판단한다. 참조 동일성(같은 ItemStack
        /// 인스턴스)으로 비교한다 - 슬롯 표시는 원본 참조를 그대로 들고 있으므로 안전하다.
        /// </summary>
        private bool IsStackInUse(ItemStack stack)
        {
            switch (currentTab)
            {
                case ForgeUITab.Enhance:
                case ForgeUITab.Promotion:
                    return ReferenceEquals(stack, selectedStack);
                case ForgeUITab.Refinement:
                    return refinementPanel != null && ReferenceEquals(stack, refinementPanel.SelectedStack);
                case ForgeUITab.Inheritance:
                    return inheritancePanel != null &&
                           (ReferenceEquals(stack, inheritancePanel.MaterialStack) || ReferenceEquals(stack, inheritancePanel.TargetStack));
                default:
                    return false;
            }
        }

        /// <summary>
        /// [HDY 요청 - 전승불가 표시, 버그 수정] 전승 탭에서, 이미 대상(target, 왼쪽칸 - 먼저 선택됨)이
        /// 선택된 상태에서 이 stack의 ObjectType이 대상과 달라 재료로 선택할 수 없는 경우 true. 대상이
        /// 아직 없으면(비교 대상이 없으므로) 항상 false이고, 대상 자신도 false(그건 IsStackInUse가
        /// "사용 중"으로 따로 표시한다). ForgeUI_InheritancePanel의 IsSameObjectType과 같은 기준
        /// (ItemData.ObjectType)이다. [예전에는 재료가 먼저 선택되는 순서였어서 MaterialStack을 기준으로
        /// 봤는데, 선택 순서가 "대상 먼저"로 바뀌면서 기준도 TargetStack으로 바꿔야 했다 - 그렇지 않으면
        /// 아직 선택되지 않은 재료를 계속 비교하려다 항상 false만 반환해서 표시가 전혀 안 켜졌다.]
        /// </summary>
        private bool IsBlockedForInheritance(ItemStack stack)
        {
            if (inheritancePanel == null || catalogManager == null) return false;

            var target = inheritancePanel.TargetStack;
            if (target == null || target.IsEmpty) return false;
            if (ReferenceEquals(stack, target)) return false;

            var targetData = catalogManager.FindItemData(target.itemId);
            var candidateData = catalogManager.FindItemData(stack.itemId);
            if (targetData == null || candidateData == null) return false;

            return targetData.ObjectType != candidateData.ObjectType;
        }
    }
}
