using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MemSystem.Data;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 그리드(6x8, 48칸) 담당.
    /// 슬롯과 페이지 점(dot)은 씬에 미리 배치되어 있으며, 이 스크립트는 그것들을 수집해 데이터만 채운다(런타임 Instantiate 없음).
    /// 슬롯 간 드래그앤드롭 요청을 받아 전체 목록 기준 인덱스로 변환해 상위(MemStorageUI)로 전달하는 역할도 한다.
    /// 정보 패널 표시는 MemStorageUI_Info가, 데이터 조회/전달 및 실제 위치 교체(데이터 반영)는 MemStorageUI(컨트롤러)가 담당한다.
    ///
    /// [빈 칸] MemCaptureManager의 목록은 항상 최대치만큼 미리 채워져 있고 빈 칸은 CapturedMemEntry.IsEmpty로 구분된다.
    ///
    /// [페이지 언락 (멤창고 업그레이드)] 데이터 목록 자체는 항상 전체 최대치(예: 10페이지)만큼 채워져 있지만,
    /// 실제로 넘어갈 수 있는 페이지 수는 unlockedPageCount로 제한한다(ShowInitial/NotifyDataChanged 호출 시
    /// MemStorageUI가 MemCaptureManager.UnlockedPageCount를 전달해준다). 총 페이지 수 계산 시 데이터 길이 기준
    /// 페이지 수와 unlockedPageCount 중 더 작은 값을 사용해서, 아직 언락되지 않은 페이지로는 이동/점 표시가 되지 않는다.
    ///
    /// [마우스 휠 페이지 이동] 그리드 영역 위에서 휠을 아래로 내리면 다음 페이지, 위로 올리면 이전 페이지로 이동한다
    /// (IScrollHandler). 이 GameObject(또는 부모)에 Raycast Target이 켜진 Graphic이 있어야 휠 이벤트가 감지된다.
    ///
    /// [드래그 도중 페이지 이동] 멤을 드래그하는 도중 휠로 페이지를 넘기면, 슬롯 48개가 전부 새 페이지 데이터로
    /// 다시 채워지면서 드래그를 시작한 슬롯의 cachedEntry도 함께 덮어써진다. 그래서 "지금 옮기는 항목이 전체 목록
    /// 기준 몇 번째인지"를 드래그가 시작되는 시점(OnSlotDragBegan)에 미리 기억해두고(draggingSourceGlobalIndex),
    /// 실제 교체(OnSlotSwapRequested) 시점에는 그 기억해둔 값을 사용한다 - 그래야 드래그 중 페이지가 바뀌어도
    /// 엉뚱한 항목이 교체되지 않는다.
    ///
    /// [Mem스탯 표시] 슬롯에 표시할 스탯 아이콘/숫자(MemStatDisplayInfo)는 원칙적으로 이 클래스가 직접
    /// 계산하지 않는다. 카탈로그(MemData) 조회와 "현재 정렬 기준이 무엇인지" 판단은 MemStorageUI가 하고, 그
    /// 결과를 statDisplayProvider 콜백으로 받아 슬롯마다 그대로 넘겨주기만 한다(findMemData와 동일한 방식).
    /// 다만 MemStorageUI(컨트롤러) 없이 이 그리드만 단독으로 쓰이는 화면(예: 생산/제작 시설의 멤 배치용
    /// 선택 그리드)에서는 statDisplayProvider 자체가 없으므로, 아래 [활성 멤 = 시설 아이콘 (독립 그리드 폴백)]
    /// 을 대신 사용한다.
    ///
    /// [활성 멤 = 시설 아이콘 (독립 그리드 폴백)] statDisplayProvider가 없을 때(=MemStorageUI 없이 단독 사용),
    /// 활성화(IsActive)된 멤은 배치된 시설이 요구하는 생산 스탯의 아이콘을 스탯 아이콘 자리에 대신 표시한다
    /// (FacilityDeploymentLookup으로 배치된 시설을 찾아 필요 스탯 종류를 조회, 아이콘은 아래 필드에서 매핑).
    /// 이 화면엔 정렬 기능이 없어 "정렬 전/후" 구분이 필요 없으므로 항상 이 규칙을 적용한다.
    /// findMemData 콜백도 없어(cachedFindMemData가 null) 그 멤의 실제 스탯 숫자를 조회할 수 없는 경우가
    /// 많으므로, 그럴 땐 아이콘만 표시하고 숫자는 생략한다.
    ///
    /// [스탯 아이콘 6종 중 탐험 아이콘은 아직 미사용] MemStorageUI의 정렬 기준 아이콘 세트와 맞춰 아이콘
    /// 필드를 6종(제작/벌목/채광/이동/생산/탐험) 준비해두지만, "탐험 시설"에 멤을 배치하는 기능 자체가
    /// 아직 게임에 없어서(ProductionStatType에도 Exploration 값이 없음) explorationStatIcon은 현재
    /// GetStatIcon(ProductionStatType) 매핑에서 실제로 쓰이지는 않는다. 나중에 탐험 관련 배치 기능이
    /// 추가되면 그때 매핑 로직만 이어붙이면 되도록 필드만 미리 만들어둔 것이다.
    ///
    /// [자동 갱신 - 데이터 변경 이벤트 직접 구독] 이 그리드는 MemStorageUI(컨트롤러) 없이 여러 화면(멤창고,
    /// 제작대/생산시설의 "멤 배치용 창고 선택 그리드" 등)에 독립적으로 배치되어 재사용된다. 그래서 이 그리드가
    /// 아래 4개 이벤트를 전부 직접 구독한다(OnEnable/OnDisable):
    /// - MemCaptureManager.OnCapturedMemsChanged (포획/스왑/정렬 등으로 목록 자체가 바뀔 때)
    /// - MemCaptureManager.OnStorageCapacityChanged (멤창고 업그레이드로 언락 페이지 수가 바뀔 때)
    /// - ProductionFacilityRuntime.OnMemDeploymentChanged (정적 이벤트 - 생산 시설에 멤이 배치/해제되어
    ///   entry.IsActive가 바뀔 때. _Kyusoo 쪽 코드라 목록 자체는 안 바뀌므로 위 두 이벤트로는 감지되지 않는다)
    /// - ProductionCraftRuntime.OnMemDeploymentChanged (정적 이벤트 - 제작 시설 버전. 클래스가 달라 별도 이벤트다)
    /// 네 이벤트 모두 같은 방식(RefreshFromCaptureManager)으로 처리한다 - 캐싱해둔 findMemData/statDisplayProvider
    /// (cachedFindMemData/cachedStatDisplayProvider)를 그대로 재사용해 captureManager.CapturedMems/
    /// UnlockedPageCount를 다시 읽어와 다시 그린다. 이 캐싱이 안전하려면 findMemData/statDisplayProvider가
    /// "호출될 때마다 최신 상태를 즉석에서 읽어오는" 안정적인(stable) 메서드여야 한다(예:
    /// MemStorageUI.GetStatDisplayInfo는 정렬 기준을 매번 새로 읽으므로 안전하게 캐싱해도 된다).
    ///
    /// [captureManager 지연 확보 + 활성화 시 즉시 동기화] 이 그리드가 MemStorageUI 없이 씬에 항상 켜져 있는
    /// 채로(예: 생산 패널 안의 멤 선택 그리드) 재사용되는 경우, OnEnable은 씬 시작 시 딱 한 번만 호출된다.
    /// 그런데 Unity는 "모든 오브젝트의 Awake가 끝나야 어느 오브젝트든 Start가 호출된다"는 것만 보장하고,
    /// 서로 다른 오브젝트 간 Awake/OnEnable의 실행 순서 자체는 보장하지 않는다 - 그래서 이 그리드의
    /// Awake/OnEnable이 MemCaptureManager의 Awake보다 먼저 실행되면 MemCaptureManager.Instance가 아직
    /// null이라 captureManager 확보 및 이벤트 구독에 실패할 수 있고, 이 그리드가 그 뒤로 다시는
    /// 비활성화/재활성화되지 않으면 그 실패 상태가 영구히 남아 자동 갱신이 전혀 동작하지 않게 된다.
    /// 이를 막기 위해 세 겹으로 방어한다:
    /// 1) EnsureCaptureManager()를 OnEnable/Start/RefreshFromCaptureManager 세 곳 모두에서 호출해서, 아직
    ///    확보 못 했으면 호출될 때마다 다시 시도한다.
    /// 2) Start()(모든 오브젝트의 Awake가 끝난 뒤 호출되는 것이 보장됨)에서 구독을 한 번 더 시도한다 -
    ///    이 시점이면 MemCaptureManager.Instance는 반드시 준비되어 있다. isSubscribedToCaptureEvents 플래그로
    ///    이미 구독됐으면 중복 구독하지 않는다.
    /// 3) OnEnable과 Start 마지막에 RefreshFromCaptureManager()를 즉시 한 번 호출해서, 이벤트를 기다리지 않고
    ///    그 시점의 최신 데이터로 바로 동기화한다(놓친 변경사항이 있어도 즉시 따라잡는다).
    /// ProductionFacilityRuntime/ProductionCraftRuntime의 정적 이벤트는 captureManager와 무관하게(클래스
    /// 자체의 정적 이벤트라 인스턴스 타이밍 문제가 없음) OnEnable에서 항상 구독한다.
    ///
    /// [배치 해제 버튼] 활성화(IsActive)된 멤을 우클릭하면 releaseButton 하나를 그 슬롯의 아이콘 위치로 옮겨
    /// 보여준다(슬롯마다 버튼을 두지 않고 하나를 재사용). 버튼을 클릭하면 OnReleaseRequested로 (entry, data)를
    /// 올리고, 실제로 어느 시설에서 해제할지/IsActive를 어떻게 되돌릴지는 MemStorageUI(컨트롤러)가 처리한다.
    /// MemStorageUI 없이 이 그리드만 단독으로 배치된 화면에서는 OnReleaseRequested를 구독하는 쪽이 없다면
    /// 해제하기 버튼을 눌러도 아무 효과가 없다(버튼 자체는 감춰지지만 실제 배치 해제는 일어나지 않는다) -
    /// 그런 화면에서는 별도로 OnReleaseRequested를 구독해 처리해줘야 한다.
    /// 페이지 이동이나 다른 슬롯 좌클릭 시, 또는 해제 버튼 자체를 누른 뒤에는 버튼을 다시 숨긴다.
    /// releaseButton 프리팹의 pivot은 좌상단(0, 1)으로 설정해야 "멤 아이콘 중앙 = 버튼의 왼쪽 위 모서리"로
    /// 배치된다(에디터에서 설정 필요, 코드에서는 위치만 아이콘 중앙으로 맞춘다).
    ///
    /// [Populate 진입점마다 슬롯 재수집 방어] 이 컴포넌트가 프리팹으로 매번 새로 Instantiate되면서(UIManager),
    /// 같은 Instantiate 호출 안에서 부모(MemStorageUI)의 OnEnable이 이 컴포넌트의 Awake(CollectSlots)보다
    /// 먼저 실행될 수 있는 순서 문제가 있었다 - 그러면 Populate가 "슬롯 0개"로 조기 종료되어 화면을
    /// 전혀 갱신하지 못하고, 프리팹에 저장되어 있던 편집 시점의 잔상이 그대로 보이는 버그가 생겼다(마우스
    /// 휠로 페이지를 넘기면 그 시점엔 이미 Awake가 끝나 있어서 정상 갱신되는 것도 같은 이유). 그래서
    /// Populate 맨 앞에서 CollectSlots/CollectPageDots를 다시 호출한다 - 두 메서드 다 이미 수집된 경우
    /// 그냥 반환하므로(slots.Count > 0 이면 바로 리턴) 여러 번 호출해도 안전하고, Awake가 아직 실행되지
    /// 않은 경우에도 여기서 대신 수집을 완료시켜 항상 최신 상태로 그려지게 한다.
    /// </summary>
    public class MemStorageUI_Grid : MonoBehaviour, IScrollHandler
    {
        private const int Columns = 6;
        private const int Rows = 8;
        public const int PageSize = Columns * Rows;
        private const int MaxPageDots = 10;

        [Header("그리드 (6x8 - 미리 배치된 슬롯의 부모)")]
        [SerializeField] private Transform gridParent;

        [Header("페이지 이동 (48마리 초과 시 사용)")]
        [SerializeField] private Button prevPageButton;
        [SerializeField] private Button nextPageButton;

        [Header("페이지 점 표시 (점 10개, 미리 배치된 부모)")]
        [SerializeField] private Transform pageDotsParent;
        [SerializeField] private float dotNormalScale = 1f;
        [SerializeField] private float dotActiveScale = 1.4f;

        [Header("배치 해제 버튼 (우클릭 시 활성 멤 위에 표시, 슬롯마다 두지 않고 하나를 재사용)")]
        [SerializeField] private Button releaseButton;

        [Header("데이터 참조 (이 그리드만 단독으로 사용할 때도 자동 갱신되도록 직접 구독)")]
        [Tooltip("비워두면 MemCaptureManager.Instance로 자동 보정한다. MemStorageUI(컨트롤러) 없이 이 그리드만 사용하는 화면에서도, 포획/스왑/정렬/업그레이드/시설 배치 등으로 데이터가 바뀌면 스스로 다시 그려지도록 여기서 직접 구독한다.")]
        [SerializeField] private MemCaptureManager captureManager;

        [Header("스탯 아이콘 (MemStorageUI 없이 단독 사용 시 - 활성 멤이 배치된 시설의 필요 스탯 표시용)")]
        [Tooltip("이 그리드가 MemStorageUI(정렬 기능 포함) 없이 단독으로 쓰일 때, 활성화된 멤이 배치된 시설이 요구하는 스탯의 아이콘을 여기서 매핑한다. MemStorageUI를 통해 쓰이는 화면에서는 MemStorageUI가 이미 자체적으로 처리하므로 이 필드들은 쓰이지 않는다.")]
        [SerializeField] private Sprite craftingStatIcon;
        [SerializeField] private Sprite loggingStatIcon;
        [SerializeField] private Sprite miningStatIcon;
        [SerializeField] private Sprite transportStatIcon;
        [SerializeField] private Sprite farmingStatIcon;
        [Tooltip("탐험(Exploration) 스탯 아이콘. '탐험 시설' 배치 기능 자체가 아직 없어(ProductionStatType에 Exploration이 없음) GetStatIcon(ProductionStatType) 매핑에서는 아직 쓰이지 않는다. 나중에 관련 기능이 생기면 이어붙일 수 있도록 필드만 미리 만들어둔다.")]
        [SerializeField] private Sprite explorationStatIcon;

        private readonly List<MemSlotUI> slots = new List<MemSlotUI>();
        private readonly List<RectTransform> pageDots = new List<RectTransform>();
        private int currentPageIndex;

        // 언락된 페이지 수(멤창고 업그레이드로 늘어남). 기본값은 "제한 없음"이지만, 실제로는 항상
        // ShowInitial/NotifyDataChanged 호출 시 MemStorageUI가 MemCaptureManager.UnlockedPageCount를 넘겨준다.
        private int unlockedPageCount = int.MaxValue;

        // 드래그 시작 시점에 기억해두는 "지금 옮기는 항목"의 전체 목록 기준 인덱스. 드래그 중이 아니면 -1.
        private int draggingSourceGlobalIndex = -1;

        // 해제하기 버튼이 현재 가리키고 있는 대상. 버튼이 꺼져있으면(눌리지 않은 상태) 둘 다 null.
        private CapturedMemEntry pendingReleaseEntry;
        private MemData pendingReleaseData;

        // 페이지 이동(이전/다음) 및 데이터 변경 이벤트로 인한 자동 갱신 시 다시 그리기 위해
        // 마지막으로 받은 데이터/콜백을 캐싱해둔다.
        private IReadOnlyList<CapturedMemEntry> cachedCapturedMems;
        private Func<string, MemData> cachedFindMemData;
        private Func<CapturedMemEntry, MemStatDisplayInfo> cachedStatDisplayProvider;

        // captureManager.OnCapturedMemsChanged/OnStorageCapacityChanged 구독 완료 여부. Start()에서 재시도할 때
        // 이미 OnEnable에서 성공했다면 중복 구독하지 않기 위한 플래그.
        private bool isSubscribedToCaptureEvents;

        /// <summary>슬롯이 클릭되었을 때 발생. MemStorageUI(컨트롤러)가 구독해서 정보 패널로 전달한다.</summary>
        public event Action<CapturedMemEntry, MemData> OnSlotClicked;

        /// <summary>
        /// 드래그앤드롭으로 두 슬롯의 위치를 바꿔달라는 요청. 전체 목록 기준 인덱스(페이지 오프셋 포함) 2개를 전달한다.
        /// 대상이 빈 칸인 경우도 포함된다(빈 칸으로 이동). MemStorageUI(컨트롤러)가 구독해서 실제 데이터(MemCaptureManager)에 반영한다.
        /// </summary>
        public event Action<int, int> OnSwapRequested;

        /// <summary>
        /// 해제하기 버튼이 클릭되어 배치 해제가 요청되었을 때 발생. MemStorageUI(컨트롤러)가 구독해서 실제로
        /// 어느 시설(ProductionFacilityRuntime)에서 해제할지 찾아 처리한다.
        /// </summary>
        public event Action<CapturedMemEntry, MemData> OnReleaseRequested;

        private void Awake()
        {
            if (gridParent == null) Debug.LogWarning("[MemStorageUI_Grid] gridParent가 비어있습니다. 미리 배치된 슬롯을 찾을 수 없습니다.", this);
            if (pageDotsParent == null) Debug.LogWarning("[MemStorageUI_Grid] pageDotsParent가 비어있습니다. 페이지 점이 표시되지 않습니다.", this);
            if (releaseButton == null) Debug.LogWarning("[MemStorageUI_Grid] releaseButton이 비어있습니다. 배치 해제 버튼이 동작하지 않습니다.", this);

            EnsureCaptureManager();

            CollectSlots();
            CollectPageDots();

            if (prevPageButton != null) prevPageButton.onClick.AddListener(GoToPrevPage);
            if (nextPageButton != null) nextPageButton.onClick.AddListener(GoToNextPage);

            if (releaseButton != null)
            {
                releaseButton.onClick.AddListener(HandleReleaseButtonClicked);
                releaseButton.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            TrySubscribeToCaptureManagerEvents();

            // [IsActive 변경 감지] 생산/제작 시설에 멤이 배치/해제되면 entry.IsActive만 직접 바뀌고
            // captureManager의 두 이벤트는 발행되지 않으므로, 정적 이벤트를 별도로 구독해야 한다.
            // 두 클래스(생산/제작)가 각자 독립된 정적 이벤트를 가지고 있어 둘 다 구독한다. 정적 이벤트라
            // captureManager 확보 여부와 무관하게 항상 구독 가능하다.
            ProductionFacilityRuntime.OnMemDeploymentChanged += RefreshFromCaptureManager;
            ProductionCraftRuntime.OnMemDeploymentChanged += RefreshFromCaptureManager;

            // 활성화되는 시점의 최신 데이터로 즉시 한 번 맞춘다 - 이벤트를 기다리지 않고 그 사이 놓친
            // 변경사항(예: 이 그리드가 비활성 상태이던 동안 포획된 멤)까지 바로 반영한다.
            RefreshFromCaptureManager();
        }

        private void OnDisable()
        {
            if (captureManager != null && isSubscribedToCaptureEvents)
            {
                captureManager.OnCapturedMemsChanged -= RefreshFromCaptureManager;
                captureManager.OnStorageCapacityChanged -= RefreshFromCaptureManager;
            }
            isSubscribedToCaptureEvents = false;

            ProductionFacilityRuntime.OnMemDeploymentChanged -= RefreshFromCaptureManager;
            ProductionCraftRuntime.OnMemDeploymentChanged -= RefreshFromCaptureManager;
        }

        /// <summary>
        /// 모든 오브젝트의 Awake가 끝난 뒤 반드시 호출되는 것이 보장되는 Start에서, 혹시 OnEnable 시점에
        /// MemCaptureManager.Instance가 아직 준비되지 않아 captureManager 확보/구독에 실패했다면 한 번 더
        /// 시도한다. isSubscribedToCaptureEvents 플래그 덕분에 OnEnable에서 이미 성공했다면 여기서는
        /// 아무 일도 하지 않는다.
        /// </summary>
        private void Start()
        {
            TrySubscribeToCaptureManagerEvents();
            RefreshFromCaptureManager();
        }

        /// <summary>captureManager가 비어있으면 파괴불가 싱글톤에서 다시 가져온다. 호출 시점과 무관하게 안전하게 여러 번 호출해도 된다.</summary>
        private void EnsureCaptureManager()
        {
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
        }

        /// <summary>captureManager를 확보한 뒤 아직 구독 전이면 OnCapturedMemsChanged/OnStorageCapacityChanged를 구독한다. 이미 구독했으면 아무 것도 하지 않는다(중복 구독 방지).</summary>
        private void TrySubscribeToCaptureManagerEvents()
        {
            if (isSubscribedToCaptureEvents) return;

            EnsureCaptureManager();
            if (captureManager == null)
            {
                Debug.LogWarning("[MemStorageUI_Grid] captureManager를 찾을 수 없어 자동 갱신 구독을 하지 못했습니다. ShowInitial/NotifyDataChanged를 직접 호출해줘야 합니다.", this);
                return;
            }

            captureManager.OnCapturedMemsChanged += RefreshFromCaptureManager;
            captureManager.OnStorageCapacityChanged += RefreshFromCaptureManager;
            isSubscribedToCaptureEvents = true;
        }

        /// <summary>
        /// captureManager의 목록 변경, 언락 페이지 변경, 시설(생산/제작) 배치 변경 - 네 이벤트가 전부 이
        /// 메서드 하나로 모인다(그리고 OnEnable/Start에서 활성화 시점 동기화용으로도 직접 호출된다).
        /// 호출될 때마다 EnsureCaptureManager()로 한 번 더 확보를 시도한 뒤, 마지막으로 ShowInitial/
        /// NotifyDataChanged에 전달됐던 콜백(cachedFindMemData/cachedStatDisplayProvider)을 그대로
        /// 재사용해서 다시 그린다 - 아직 한 번도 호출된 적이 없으면(콜백이 null) Populate가 알아서 안전하게
        /// 처리한다(findMemData/statDisplayProvider가 null이어도 각각 데이터 없음/기본 폴백으로 대체됨).
        /// </summary>
        private void RefreshFromCaptureManager()
        {
            EnsureCaptureManager();
            if (captureManager == null) return;

            NotifyDataChanged(captureManager.CapturedMems, cachedFindMemData, cachedStatDisplayProvider, captureManager.UnlockedPageCount);
        }

        /// <summary>씬(프리팹)에 미리 배치된 슬롯들을 gridParent 하위에서 찾아 수집한다(더 이상 런타임 Instantiate하지 않음). 이미 수집되어 있으면 바로 반환한다(여러 진입점에서 방어적으로 호출해도 안전).</summary>
        private void CollectSlots()
        {
            if (gridParent == null) return;
            if (slots.Count > 0) return; // 이미 수집됨

            gridParent.GetComponentsInChildren<MemSlotUI>(true, slots);

            foreach (var slot in slots)
            {
                slot.OnSlotClicked += HandleSlotClicked;
                slot.OnSlotSwapRequested += HandleSlotSwapRequested;
                slot.OnSlotDragBegan += HandleSlotDragBegan;
                slot.OnSlotDragEnded += HandleSlotDragEnded;
                slot.OnSlotRightClicked += HandleSlotRightClicked;
            }

            if (slots.Count != PageSize)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 미리 배치된 슬롯 개수({slots.Count})가 예상 개수({PageSize})와 다릅니다. gridParent 하위에 슬롯 {Columns}x{Rows}개가 배치되어 있는지 확인하세요.", this);
            }

            Debug.Log($"[MemStorageUI_Grid] 슬롯 {slots.Count}개 수집 완료 (부모: {gridParent.name})");
        }

        /// <summary>씬(프리팹)에 미리 배치된 페이지 점(dot)들을 pageDotsParent 하위에서 찾아 수집한다. 이미 수집되어 있으면 바로 반환한다.</summary>
        private void CollectPageDots()
        {
            if (pageDotsParent == null) return;
            if (pageDots.Count > 0) return; // 이미 수집됨

            var images = pageDotsParent.GetComponentsInChildren<Image>(true);
            foreach (var image in images)
            {
                pageDots.Add(image.rectTransform);
            }

            if (pageDots.Count != MaxPageDots)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 페이지 점 개수({pageDots.Count})가 예상 개수({MaxPageDots})와 다릅니다.", this);
            }

            Debug.Log($"[MemStorageUI_Grid] 페이지 점 {pageDots.Count}개 수집 완료 (부모: {pageDotsParent.name})");
        }

        private void HandleSlotClicked(CapturedMemEntry entry, MemData data)
        {
            // 다른 슬롯을 좌클릭하면 열려있던 해제하기 버튼은 의미가 없으므로 감춘다.
            HideReleaseButton();
            OnSlotClicked?.Invoke(entry, data);
        }

        /// <summary>
        /// 슬롯 우클릭 처리. 채워져 있고 활성화(IsActive)된 멤일 때만 해제하기 버튼을 그 슬롯의 아이콘
        /// 위치에 띄운다. 빈 칸이거나 비활성 상태인 멤이면 버튼을 감춘다(해제할 대상이 아니므로).
        /// </summary>
        private void HandleSlotRightClicked(MemSlotUI slot, CapturedMemEntry entry, MemData data)
        {
            if (entry == null || entry.IsEmpty || !entry.IsActive)
            {
                HideReleaseButton();
                return;
            }

            pendingReleaseEntry = entry;
            pendingReleaseData = data;

            PositionReleaseButtonAtSlot(slot);

            if (releaseButton != null) releaseButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// 해제하기 버튼을 우클릭된 슬롯의 아이콘 위치로 옮긴다. 버튼 프리팹의 pivot을 좌상단(0,1)으로
        /// 설정해두면 "멤 아이콘 중앙 = 버튼의 왼쪽 위 모서리"가 되도록 배치된다(에디터 설정 필요).
        /// </summary>
        private void PositionReleaseButtonAtSlot(MemSlotUI slot)
        {
            if (releaseButton == null || slot == null) return;

            var iconRect = slot.IconRectTransform;
            if (iconRect == null) return;

            if (releaseButton.transform is RectTransform buttonRect)
            {
                buttonRect.position = iconRect.position;
            }
        }

        /// <summary>확인(해제하기) 버튼 클릭 시 실제 해제 요청을 상위(MemStorageUI)로 올린다. 처리 결과와 무관하게 버튼은 숨긴다.</summary>
        private void HandleReleaseButtonClicked()
        {
            if (pendingReleaseEntry != null)
            {
                OnReleaseRequested?.Invoke(pendingReleaseEntry, pendingReleaseData);
            }

            HideReleaseButton();
        }

        /// <summary>해제하기 버튼을 감추고 기억해둔 대상을 정리한다.</summary>
        private void HideReleaseButton()
        {
            pendingReleaseEntry = null;
            pendingReleaseData = null;

            if (releaseButton != null) releaseButton.gameObject.SetActive(false);
        }

        /// <summary>드래그가 시작된 시점의 전체 인덱스를 기억해둔다(드래그 도중 페이지가 바뀌어도 잃지 않기 위함).</summary>
        private void HandleSlotDragBegan(MemSlotUI sourceSlot)
        {
            draggingSourceGlobalIndex = GetGlobalIndexOfSlot(sourceSlot);
        }

        /// <summary>드래그가 끝나면(성공/실패 무관) 기억해둔 인덱스를 정리한다.</summary>
        private void HandleSlotDragEnded(MemSlotUI sourceSlot)
        {
            draggingSourceGlobalIndex = -1;
        }

        /// <summary>
        /// 슬롯 드래그앤드롭 결과를 받아, 전체 목록 기준 인덱스로 변환해 상위로 전달한다.
        /// source 쪽은 드래그 시작 시점에 기억해둔 인덱스(draggingSourceGlobalIndex)를 우선 사용한다 -
        /// 드래그 도중 휠로 페이지가 바뀌면 sourceSlot의 현재 위치로 다시 계산했을 때 틀린 값이 나오기 때문이다.
        /// </summary>
        private void HandleSlotSwapRequested(MemSlotUI sourceSlot, MemSlotUI targetSlot)
        {
            int sourceGlobalIndex = draggingSourceGlobalIndex >= 0
                ? draggingSourceGlobalIndex
                : GetGlobalIndexOfSlot(sourceSlot);

            int targetGlobalIndex = GetGlobalIndexOfSlot(targetSlot);

            if (sourceGlobalIndex < 0 || targetGlobalIndex < 0)
            {
                Debug.LogWarning("[MemStorageUI_Grid] 교체 요청된 슬롯을 목록에서 찾을 수 없습니다.", this);
                return;
            }

            OnSwapRequested?.Invoke(sourceGlobalIndex, targetGlobalIndex);
        }

        /// <summary>현재 페이지 기준으로 슬롯의 로컬 인덱스를 전체 목록 기준 인덱스로 변환한다.</summary>
        private int GetGlobalIndexOfSlot(MemSlotUI slot)
        {
            int localIndex = slots.IndexOf(slot);
            if (localIndex < 0) return -1;
            return currentPageIndex * PageSize + localIndex;
        }

        /// <summary>
        /// 창고 UI가 처음 열릴 때 호출. 실제로 채워진 멤이 있는 가장 마지막 페이지부터 보여준다(전부 비어있으면 첫 페이지).
        /// unlockedPageCount: 멤창고 업그레이드로 언락된 페이지 수(MemCaptureManager.UnlockedPageCount). 이 페이지 수까지만
        /// 이동/점 표시를 허용한다.
        /// </summary>
        public void ShowInitial(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData, Func<CapturedMemEntry, MemStatDisplayInfo> statDisplayProvider, int unlockedPageCount)
        {
            this.unlockedPageCount = unlockedPageCount;
            currentPageIndex = GetLastNonEmptyPageIndex(capturedMems);
            Populate(capturedMems, findMemData, statDisplayProvider);
        }

        /// <summary>
        /// 새로 멤이 포획되거나 슬롯 위치/정렬 순서가 바뀌는 등 데이터가 바뀌었을 때, 또는 페이지가 새로 언락되었을 때 호출.
        /// 보고 있던 페이지를 그대로 유지한 채 다시 채운다.
        /// </summary>
        public void NotifyDataChanged(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData, Func<CapturedMemEntry, MemStatDisplayInfo> statDisplayProvider, int unlockedPageCount)
        {
            this.unlockedPageCount = unlockedPageCount;
            Populate(capturedMems, findMemData, statDisplayProvider);
        }

        private int GetTotalPages(int count)
        {
            return Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize));
        }

        /// <summary>실제 이동 가능한 총 페이지 수. 데이터 길이 기준 페이지 수와 언락된 페이지 수 중 더 작은 값을 사용한다.</summary>
        private int GetEffectiveTotalPages(int count)
        {
            int dataBasedPages = GetTotalPages(count);
            int unlockedClamped = Mathf.Max(1, unlockedPageCount);
            return Mathf.Min(dataBasedPages, unlockedClamped);
        }

        private int GetLastPageIndex(int count)
        {
            return GetEffectiveTotalPages(count) - 1;
        }

        /// <summary>실제 멤(비어있지 않은 항목)이 존재하는 페이지 중 가장 마지막 페이지의 인덱스를 찾는다. 없으면 0(첫 페이지).</summary>
        private int GetLastNonEmptyPageIndex(IReadOnlyList<CapturedMemEntry> capturedMems)
        {
            if (capturedMems == null || capturedMems.Count == 0) return 0;

            int totalPages = GetEffectiveTotalPages(capturedMems.Count);

            for (int page = totalPages - 1; page >= 0; page--)
            {
                int start = page * PageSize;
                int end = Mathf.Min(start + PageSize, capturedMems.Count);

                for (int i = start; i < end; i++)
                {
                    if (!capturedMems[i].IsEmpty) return page;
                }
            }

            return 0;
        }

        /// <summary>
        /// 데이터를 캐싱하고 현재 페이지 기준으로 그리드를 다시 채운다. 목록에 저장된 순서(빈 칸 포함) 그대로 표시.
        /// 맨 앞에서 CollectSlots/CollectPageDots를 다시 호출한다 - 이 컴포넌트가 프리팹으로 매번 새로
        /// Instantiate되면서(UIManager) 부모(MemStorageUI)의 OnEnable이 이 컴포넌트의 Awake보다 먼저
        /// 실행되는 경우가 있어, Awake에서의 최초 수집을 여기서도 보장해야 "슬롯 0개"로 조기 종료되어
        /// 화면이 갱신되지 않는 문제를 막을 수 있다(이미 수집되어 있으면 두 메서드 다 즉시 반환하므로
        /// 매번 호출해도 비용이 거의 없다).
        /// </summary>
        private void Populate(IReadOnlyList<CapturedMemEntry> capturedMems, Func<string, MemData> findMemData, Func<CapturedMemEntry, MemStatDisplayInfo> statDisplayProvider)
        {
            CollectSlots();
            CollectPageDots();

            // 페이지/데이터가 다시 그려지면 이전에 우클릭해서 띄워둔 해제하기 버튼은 엉뚱한 슬롯을 가리키게 되므로 감춘다.
            HideReleaseButton();

            cachedCapturedMems = capturedMems;
            cachedFindMemData = findMemData;
            cachedStatDisplayProvider = statDisplayProvider;

            if (capturedMems == null)
            {
                Debug.LogWarning("[MemStorageUI_Grid] Populate 중단: capturedMems가 없습니다.", this);
                return;
            }

            if (slots.Count == 0)
            {
                Debug.LogWarning("[MemStorageUI_Grid] Populate 중단: 수집된 슬롯이 0개입니다 (CollectSlots 실패 여부 확인 필요).", this);
                return;
            }

            int totalPages = GetEffectiveTotalPages(capturedMems.Count);
            currentPageIndex = Mathf.Clamp(currentPageIndex, 0, totalPages - 1);

            int startIndex = currentPageIndex * PageSize;

            for (int i = 0; i < slots.Count; i++)
            {
                int globalIndex = startIndex + i;
                if (globalIndex < capturedMems.Count && !capturedMems[globalIndex].IsEmpty)
                {
                    var entry = capturedMems[globalIndex];
                    var data = findMemData != null ? findMemData(entry.MemId) : null;
                    var statInfo = statDisplayProvider != null
                        ? statDisplayProvider(entry)
                        : ComputeDefaultActiveFacilityDisplay(entry);
                    slots[i].SetData(entry, data, statInfo);
                }
                else
                {
                    slots[i].Clear();
                }
            }

            UpdatePageDots(currentPageIndex, totalPages);

            if (prevPageButton != null) prevPageButton.interactable = currentPageIndex > 0;
            if (nextPageButton != null) nextPageButton.interactable = currentPageIndex < totalPages - 1;

            Debug.Log($"[MemStorageUI_Grid] Populate 완료: 슬롯={slots.Count}, 전체 칸수={capturedMems.Count}, 페이지={currentPageIndex + 1}/{totalPages} (언락={unlockedPageCount})");
        }

        /// <summary>
        /// [독립 그리드 폴백] statDisplayProvider가 없을 때(=MemStorageUI 없이 단독 사용)만 쓰인다. 이 화면엔
        /// 정렬 기능이 없어 "정렬 전/후" 구분이 필요 없으므로, 활성화(IsActive)된 멤은 항상 배치된 시설이
        /// 요구하는 스탯의 아이콘을 보여준다(FacilityDeploymentLookup으로 조회). findMemData가 없어서
        /// (cachedFindMemData가 null) 실제 스탯 숫자를 조회할 수 없으면 아이콘만 표시하고 숫자는 생략한다.
        /// </summary>
        private MemStatDisplayInfo ComputeDefaultActiveFacilityDisplay(CapturedMemEntry entry)
        {
            if (entry == null || entry.IsEmpty || !entry.IsActive) return MemStatDisplayInfo.Hidden;

            if (!FacilityDeploymentLookup.TryGetRequiredStatType(entry, out var statType))
            {
                return MemStatDisplayInfo.Hidden;
            }

            var icon = GetStatIcon(statType);
            if (icon == null) return MemStatDisplayInfo.Hidden;

            var data = cachedFindMemData != null ? cachedFindMemData(entry.MemId) : null;
            string displayText = data != null ? data.productionStats.GetStat(statType).ToString() : string.Empty;

            return new MemStatDisplayInfo(true, icon, displayText);
        }

        /// <summary>
        /// ProductionStatType -> 아이콘 매핑. ProductionStatType에는 Exploration이 없어(탐험 시설 배치 기능
        /// 자체가 아직 없음) explorationStatIcon 필드는 여기서 참조되지 않는다 - 나중에 관련 기능이 생기면
        /// 이어붙일 수 있도록 필드만 미리 만들어둔 상태다.
        /// </summary>
        private Sprite GetStatIcon(ProductionStatType statType)
        {
            switch (statType)
            {
                case ProductionStatType.Crafting: return craftingStatIcon;
                case ProductionStatType.Logging: return loggingStatIcon;
                case ProductionStatType.Mining: return miningStatIcon;
                case ProductionStatType.Transport: return transportStatIcon;
                case ProductionStatType.Farming: return farmingStatIcon;
                default: return null;
            }
        }

        /// <summary>
        /// 페이지 점들의 활성/비활성 및 크기(현재 페이지 강조)를 갱신한다.
        /// 점은 최대 10개까지만 지원한다고 가정 - 총 페이지가 10개를 넘으면 넘는 페이지는 점으로 표시되지 않는다(경고 로그만 남김).
        /// </summary>
        private void UpdatePageDots(int pageIndex, int totalPages)
        {
            if (pageDots.Count == 0) return;

            int visibleCount = Mathf.Min(totalPages, pageDots.Count);

            for (int i = 0; i < pageDots.Count; i++)
            {
                bool isVisible = i < visibleCount;
                pageDots[i].gameObject.SetActive(isVisible);

                if (!isVisible) continue;

                bool isCurrent = i == pageIndex;
                pageDots[i].localScale = Vector3.one * (isCurrent ? dotActiveScale : dotNormalScale);
            }

            if (totalPages > pageDots.Count)
            {
                Debug.LogWarning($"[MemStorageUI_Grid] 총 페이지({totalPages})가 점 개수({pageDots.Count})보다 많습니다. {pageDots.Count}페이지를 초과하는 페이지는 점으로 표시되지 않습니다.", this);
            }
        }

        public void GoToPrevPage()
        {
            currentPageIndex = Mathf.Max(0, currentPageIndex - 1);
            Populate(cachedCapturedMems, cachedFindMemData, cachedStatDisplayProvider);
        }

        public void GoToNextPage()
        {
            int count = cachedCapturedMems != null ? cachedCapturedMems.Count : 0;
            currentPageIndex = Mathf.Min(GetLastPageIndex(count), currentPageIndex + 1);
            Populate(cachedCapturedMems, cachedFindMemData, cachedStatDisplayProvider);
        }

        /// <summary>
        /// 마우스 휠 입력을 받아 페이지를 이동한다(편의 기능). 휠을 아래로 내리면 다음 페이지, 위로 올리면 이전 페이지.
        /// 멤을 드래그하는 도중에도 그대로 동작해서, 다른 페이지로 넘어가 그 페이지의 슬롯에 놓을 수 있다
        /// (드래그 중인 항목의 인덱스는 draggingSourceGlobalIndex로 별도 보존되므로 안전하다).
        /// 이미 첫/마지막(언락된 범위 기준) 페이지인 경우 GoToPrevPage/GoToNextPage가 알아서 그 자리에서 멈춘다.
        /// </summary>
        public void OnScroll(PointerEventData eventData)
        {
            if (eventData.scrollDelta.y < 0f)
            {
                GoToNextPage();
            }
            else if (eventData.scrollDelta.y > 0f)
            {
                GoToPrevPage();
            }
        }
    }
}
