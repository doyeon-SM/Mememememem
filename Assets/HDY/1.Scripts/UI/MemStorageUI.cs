using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;
using HDY.Upgrade;
using HDY.Exploration;

namespace HDY.UI
{
    /// <summary>
    /// 멤 창고 UI 컨트롤러.
    /// MemCaptureManager / MemCatalogManager에서 데이터를 가져와 MemStorageUI_Grid, MemStorageUI_Info,
    /// MemStorageUI_Sort에 전달/연결하는 역할만 한다. 실제 그리드 표시는 MemStorageUI_Grid, 정보 패널 표시는
    /// MemStorageUI_Info, 정렬 버튼 감지는 MemStorageUI_Sort가 담당한다.
    ///
    /// 그리드에서 드래그앤드롭으로 슬롯 위치 교체가 요청되면, 이 컨트롤러가 MemCaptureManager에 실제 데이터 반영을 지시한다.
    /// 정렬 버튼이 클릭되면, 이 컨트롤러가 카탈로그(MemData)를 조회해 실제 비교/정렬을 수행하고, 그 결과를
    /// MemCaptureManager.ApplySortedOrder로 반영한다(MemCaptureManager는 정렬 기준을 모르고 결과만 적용).
    ///
    /// [정렬 로직 공유] 실제 비교/조회 로직(MemSortHelper.SortEntries/GetTier/GetProductionStat/GetTierLetter)은
    /// 도감(MemDexUI, MemData 기반)과 완전히 동일해서 MemSortHelper로 뽑아 공유한다. 이 클래스는 CapturedMemEntry를
    /// SortableMemItem&lt;CapturedMemEntry&gt;로 감싸서 넘기고, MemSortHelper는 memId/탐험 스탯만 보고 정렬한다.
    ///
    /// [멤창고 업그레이드] 업그레이드 버튼을 누르면 공용 업그레이드 팝업(UpgradePopupUI)에 storageUpgrade(멤창고
    /// 페이지 확장을 IUpgradable로 감싼 어댑터)를 넘겨 보여준다. 실제 비용 확인/차감과 업그레이드 적용은 팝업과
    /// storageUpgrade가 처리한다.
    ///
    /// [그리드 자동 갱신은 이제 Grid 자신의 책임] 예전에는 이 컨트롤러가 captureManager.OnCapturedMemsChanged /
    /// OnStorageCapacityChanged를 구독해서 grid.NotifyDataChanged를 직접 호출해줬는데, 그러면 MemStorageUI_Grid를
    /// 이 컨트롤러 없이 단독으로 쓰는 화면에서는 자동 갱신이 전혀 안 되는 문제가 있었다. 그래서 이제
    /// MemStorageUI_Grid가 captureManager 이벤트를 직접 구독해서 스스로 다시 그리도록 바꿨고, 이 컨트롤러는
    /// OnEnable에서 ShowInitial로 최초 1회 데이터/콜백만 넘겨주면 된다(findMemData, GetStatDisplayInfo는 아래
    /// 참고처럼 항상 최신 상태를 즉석에서 읽는 안정적인 메서드라 Grid가 캐싱해서 계속 재사용해도 안전하다).
    ///
    /// [IsActive(시설 배치) 변경도 Grid가 직접 구독] 생산/제작 시설에 멤이 배치/해제되면 entry.IsActive만 직접
    /// 바뀌고 captureManager의 위 두 이벤트는 발행되지 않는다. _Kyusoo 쪽에 이미
    /// ProductionFacilityRuntime.OnMemDeploymentChanged / ProductionCraftRuntime.OnMemDeploymentChanged라는
    /// 정적(static) 이벤트가 각각 존재해서, MemStorageUI_Grid가 이 두 이벤트를 직접 구독해서 자동으로 다시
    /// 그리도록 처리했다. 탐험(ExplorationRuntime.OnMemDeploymentChanged)도 동일한 방식으로 Grid가 직접 구독한다.
    /// 그래서 이 컨트롤러는 더 이상 이 부분을 신경 쓸 필요가 없다.
    ///
    /// [배치 해제] 그리드에서 활성 멤을 우클릭 -> 해제하기 버튼 클릭 시 grid.OnReleaseRequested가 발생한다.
    /// 이 창고 UI는 그 멤이 어느 시설(_Kyusoo의 ProductionFacilityRuntime/ProductionCraftRuntime)에 배치되어
    /// 있는지 전혀 모르므로, 씬에 있는 모든 BuildingRuntime을 훑어 이 entry를 갖고 있는 시설을 찾아
    /// TryReleaseDeployedMem()을 호출한다. CapturedMemEntry는 참조 타입이라 시설 쪽 리스트와
    /// MemCaptureManager 쪽 리스트가 같은 객체를 가리키므로, 해제 처리만으로 IsActive도 자동으로 false가
    /// 된다(별도로 손댈 필요 없음). 그 다음 grid.NotifyDataChanged로 한 번 더 직접 갱신해주는데, 이는 Grid의
    /// 자동 구독과 별개로 "어느 시설에도 등록되어 있지 않아 방어적으로 entry.IsActive만 직접 되돌리는" 예외
    /// 상황(이 경우 시설 쪽 정적 이벤트가 발행되지 않음)까지 안전하게 커버하기 위한 것이다.
    ///
    /// [탐험 중인 멤은 이 해제 경로를 타지 않음] 탐험 중인 멤은 BuildingRuntime(생산/제작 전용, Kyusoo 소유) 어디에도
    /// 등록되어 있지 않다. 그대로 두면 위 방어 코드가 "어느 시설에도 없다"고 오판해 IsActive만 강제로 false로
    /// 되돌리는데, 실제로는 탐험 슬롯에서 빠지지 않아 창고 표시와 탐험 진행 상태가 어긋난다. 그래서
    /// HandleReleaseRequested 맨 앞에서 ExplorationRuntime.TryGetExplorationInfo로 먼저 확인하고, 탐험 중이면
    /// 경고 로그만 남기고 그대로 무시한다(탐험 취소/완료는 오직 탐험 패널의 버튼으로만 가능).
    ///
    /// [Mem스탯/티어 표시] 현재 어떤 기준으로 정렬되어 있는지(activeSortCriteria)를 여기서 기억해두고,
    /// - Mem스탯(제작/벌목/채광/이동/생산/탐험) 기준이면 그 스탯의 아이콘 + 숫자를,
    /// - 티어 기준이면 티어 아이콘 + 등급 앞글자 대문자(R/E/U/L/M)를
    /// 그리드의 각 슬롯에 표시하도록 GetStatDisplayInfo에서 계산해서 Grid에 넘겨준다.
    /// MemId로 정렬 중이거나 아직 정렬한 적이 없으면 표시를 감춘다 - 단, 아래 [활성 멤 = 시설/탐험 아이콘 우선]
    /// 항목이 그보다 우선한다.
    ///
    /// [활성 멤 = 시설/탐험 아이콘 우선] 아직 이번 세션에 정렬한 적이 없는(activeSortCriteria == null) 상태에서
    /// 활성화(IsActive)된 멤은, "지금 어디서 일하고 있는지"를 한눈에 보여주기 위해 아이콘을 우선 표시한다.
    /// 먼저 탐험 중인지 확인해서(ExplorationRuntime) 맞으면 탐험 아이콘 + 실제 탐험레벨 숫자를 보여주고,
    /// 아니면 기존처럼 생산/제작 시설이 요구하는 스탯의 아이콘(FacilityDeploymentLookup)을 보여준다. 정렬
    /// 버튼을 누르는 순간(activeSortCriteria가 값을 갖는 순간)부터는 활성 여부와 무관하게 전부 정렬 기준
    /// 아이콘으로 전환되고, 그리드를 다시 열면(OnEnable에서 activeSortCriteria를 다시 null로 리셋) 활성 멤은
    /// 다시 시설/탐험 아이콘으로 돌아간다. 활성 표시 자체(MemSlotUI.activeImage)는 이 우선순위와 완전히
    /// 별개로 entry.IsActive만 보고 항상 켜지므로 영향받지 않는다.
    ///
    /// [씬 이동 대응] MemCaptureManager/MemCatalogManager/ExplorationRuntime은 파괴불가 싱글톤이라 씬을
    /// 이동해도 유지되지만, 이 컴포넌트는 씬에 배치된 오브젝트라서 씬이 다시 로드되면 인스펙터 참조가 끊길 수
    /// 있다. (씬 파일에 같이 저장된 매니저 오브젝트는 재로드 시 새로 생기지만, 싱글톤 중복 검사로 즉시
    /// 파괴되기 때문) Awake/OnEnable에서 참조가 끊겨 있으면(null) 싱글톤 Instance로 자동 재할당한다.
    /// </summary>
    public class MemStorageUI : MonoBehaviour
    {
        [Header("데이터 참조")]
        [SerializeField] private MemCaptureManager captureManager;
        [SerializeField] private MemCatalogManager catalogManager;
        [SerializeField] private ExplorationRuntime explorationRuntime;

        [Header("하위 UI (그리드 / 정보 패널 / 정렬 버튼)")]
        [SerializeField] private MemStorageUI_Grid grid;
        [SerializeField] private MemStorageUI_Info info;
        [SerializeField] private MemStorageUI_Sort sort;

        [Header("멤창고 업그레이드 (페이지 확장)")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private MemStorageUpgrade storageUpgrade;

        [Header("Mem스탯 아이콘 (정렬 기준이 해당 스탯일 때, 또는 활성 멤이 그 스탯을 쓰는 시설에 배치됐을 때 슬롯에 표시)")]
        [SerializeField] private Sprite craftingStatIcon;
        [SerializeField] private Sprite loggingStatIcon;
        [SerializeField] private Sprite miningStatIcon;
        [SerializeField] private Sprite transportStatIcon;
        [SerializeField] private Sprite farmingStatIcon;
        [SerializeField] private Sprite explorationStatIcon;

        [Header("티어 아이콘 (정렬 기준이 티어일 때 슬롯에 표시)")]
        [SerializeField] private Sprite rareTierIcon;
        [SerializeField] private Sprite epicTierIcon;
        [SerializeField] private Sprite uniqueTierIcon;
        [SerializeField] private Sprite legendaryTierIcon;
        [SerializeField] private Sprite mythicTierIcon;

        // 현재 창고가 어떤 기준으로 정렬되어 있는지. 아직 한 번도 정렬하지 않았으면 null(스탯/티어 표시 없음,
        // 대신 활성 멤은 시설/탐험 아이콘 표시). OnEnable마다(그리드를 다시 열 때마다) null로 리셋된다.
        private MemSortCriteria? activeSortCriteria;

        private void Awake()
        {
            // 씬 재로드 등으로 인스펙터 참조가 끊겼으면 파괴불가 싱글톤에서 다시 가져온다.
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;
            explorationRuntime = ExplorationRuntime.Resolve(explorationRuntime);

            if (captureManager == null) Debug.LogWarning("[MemStorageUI] captureManager가 비어있습니다. 포획 데이터를 읽어올 수 없습니다.", this);
            if (catalogManager == null) Debug.LogWarning("[MemStorageUI] catalogManager가 비어있습니다. 멤 SO 정보를 찾을 수 없습니다.", this);
            if (grid == null) Debug.LogWarning("[MemStorageUI] grid가 비어있습니다.", this);
            if (info == null) Debug.LogWarning("[MemStorageUI] info가 비어있습니다.", this);
            if (sort == null) Debug.LogWarning("[MemStorageUI] sort가 비어있습니다. 정렬 버튼이 동작하지 않습니다.", this);
            if (upgradeButton == null) Debug.LogWarning("[MemStorageUI] upgradeButton이 비어있습니다. 업그레이드 버튼이 동작하지 않습니다.", this);
            if (storageUpgrade == null) Debug.LogWarning("[MemStorageUI] storageUpgrade가 비어있습니다. 업그레이드 팝업을 열 수 없습니다.", this);

            if (grid != null)
            {
                grid.OnSlotClicked += HandleSlotClicked;
                grid.OnSwapRequested += HandleSwapRequested;
                grid.OnReleaseRequested += HandleReleaseRequested;
            }

            if (sort != null)
            {
                sort.OnSortRequested += HandleSortRequested;
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeButtonClicked);
            }
        }

        private void OnEnable()
        {
            // Awake 이후에도 혹시 끊겨 있다면(실행 순서 문제 등) 한 번 더 보정.
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;
            explorationRuntime = ExplorationRuntime.Resolve(explorationRuntime);

            // [그리드를 다시 열면 활성 멤은 다시 시설/탐험 아이콘으로] 이전에 골라뒀던 정렬 기준은 이 화면을
            // 닫았다 다시 열면 더 이상 유지하지 않는다 - 그래야 활성 멤이 다시 "어디서 일하는지" 아이콘부터
            // 보여준다. 정렬 버튼을 눌러야만(HandleSortRequested) 정렬 기준 아이콘으로 다시 전환된다.
            activeSortCriteria = null;

            if (grid != null && captureManager != null)
            {
                grid.ShowInitial(captureManager.CapturedMems, FindMemData, GetStatDisplayInfo, captureManager.UnlockedPageCount);
            }
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.OnSlotClicked -= HandleSlotClicked;
                grid.OnSwapRequested -= HandleSwapRequested;
                grid.OnReleaseRequested -= HandleReleaseRequested;
            }

            if (sort != null)
            {
                sort.OnSortRequested -= HandleSortRequested;
            }

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(HandleUpgradeButtonClicked);
            }
        }

        /// <summary>업그레이드 버튼 클릭 처리. 공용 업그레이드 팝업에 멤창고 페이지 업그레이드 어댑터를 넘겨 보여준다.</summary>
        private void HandleUpgradeButtonClicked()
        {
            if (storageUpgrade == null)
            {
                Debug.LogWarning("[MemStorageUI] storageUpgrade가 비어있어 업그레이드 팝업을 열 수 없습니다.", this);
                return;
            }

            if (UpgradePopupUI.Instance == null)
            {
                Debug.LogWarning("[MemStorageUI] 씬에서 UpgradePopupUI를 찾을 수 없습니다.", this);
                return;
            }

            UpgradePopupUI.Instance.Show(storageUpgrade);
        }

        private void HandleSlotClicked(CapturedMemEntry entry, MemData data)
        {
            Debug.Log($"[MemStorageUI] 슬롯 클릭 처리: MemId={entry.MemId}, Exploration={entry.ExplorationStat}");
            if (info != null)
            {
                info.ShowInfo(entry, data);
            }
        }

        /// <summary>그리드에서 드래그앤드롭으로 슬롯 위치 교체가 요청되었을 때 호출. 실제 데이터(MemCaptureManager)에 반영한다.
        /// captureManager.SwapEntries가 내부적으로 OnCapturedMemsChanged를 발행하므로, 그리드는 자체 구독으로 알아서 다시 그려진다.</summary>
        private void HandleSwapRequested(int indexA, int indexB)
        {
            Debug.Log($"[MemStorageUI] 슬롯 교체 요청 수신: index {indexA} <-> {indexB}");

            if (captureManager == null)
            {
                Debug.LogWarning("[MemStorageUI] captureManager가 비어있어 슬롯 교체를 처리할 수 없습니다.", this);
                return;
            }

            captureManager.SwapEntries(indexA, indexB);
        }

        /// <summary>
        /// 그리드에서 배치 해제(해제하기 버튼)가 요청되었을 때 호출된다. 이 창고 UI는 entry가 어느 시설에
        /// 배치되어 있는지 모르므로, 씬의 모든 BuildingRuntime을 훑어 이 entry를 갖고 있는 시설을 찾는다.
        /// CapturedMemEntry는 참조 타입이라 시설 쪽 리스트와 MemCaptureManager 쪽 리스트가 같은 객체를
        /// 가리키므로, 시설의 해제 처리만으로 IsActive도 자동으로 false가 된다(그리고 이 경로는
        /// ProductionFacilityRuntime/ProductionCraftRuntime의 정적 이벤트를 발행하므로 Grid도 자체 구독으로
        /// 알아서 다시 그려진다). 다만 "어느 시설에도 등록되어 있지 않은데 IsActive만 true인" 방어 코드
        /// 경로에서는 정적 이벤트가 발행되지 않으므로, 그 경우까지 안전하게 반영되도록 여기서 한 번 더
        /// 직접 그리드를 갱신한다.
        /// </summary>
        private void HandleReleaseRequested(CapturedMemEntry entry, MemData data)
        {
            if (entry == null) return;

            Debug.Log($"[MemStorageUI] 배치 해제 요청 수신: MemId={entry.MemId}");

            // [탐험 우선 확인] 탐험 중인 멤은 BuildingRuntime(생산/제작 전용, Kyusoo 소유) 어디에도 등록되어
            // 있지 않다. 아래 로직을 그대로 타면 "어느 시설에도 없다"고 오판해 방어 코드가 IsActive만 강제로
            // false로 되돌리는데, 실제 탐험 슬롯에서는 빠지지 않아 데이터가 어긋난다. 탐험 취소/완료는 오직
            // 탐험 패널의 버튼으로만 가능하므로, 여기서는 경고만 남기고 IsActive를 건드리지 않은 채 무시한다.
            if (explorationRuntime != null && explorationRuntime.TryGetExplorationInfo(entry, out var exploringZone))
            {
                Debug.LogWarning($"[MemStorageUI] {entry.MemId}은(는) 현재 '{exploringZone.zoneName}' 탐험 중이라 창고에서 바로 해제할 수 없습니다. 탐험 패널의 취소 버튼을 이용해주세요.");
                return;
            }

            // 바꾼 코드: 시설이늘어나도 BuildingRuntime에서 각각의 Runtime으로 보내 배치제거처리를 시도
            // 성공하면 isClearedFromFacility = true처리하는데 만약 시설에 문제가있어 배치제거처리가 안되어도 IsActive는 안전하게 false처리하기
            bool isClearedFromFacility = false;
            var allBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);

            foreach (var building in allBuildings)
            {
                if (building == null) continue;

                if (building.TryReleaseDeployedMem(entry, data))
                {
                    isClearedFromFacility = true;
                    break;
                }
            }

            // 방어 코드: 어느 시설에도 등록되어 있지 않은데 IsActive만 true인 비정상 상태라면,
            // 창고 UI가 계속 활성 표시로 막혀있지 않도록 여기서라도 직접 되돌린다.
            if (!isClearedFromFacility)
            {
                entry.IsActive = false;
            }

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, GetStatDisplayInfo, captureManager.UnlockedPageCount);
            }
        }


        /// <summary>
        /// 정렬 버튼 클릭 요청을 받아 실제 정렬(카탈로그 조회 + 비교, MemSortHelper 재사용)을 수행하고,
        /// 결과를 MemCaptureManager에 반영한다. 빈 칸은 정렬 대상에서 제외한 뒤 ApplySortedOrder가 자동으로
        /// 뒤쪽에 채운다. 정렬 기준을 activeSortCriteria에 기억해서 이후 그리드 갱신 시 Mem스탯/티어 표시에 사용한다
        /// (이 순간부터 활성 멤의 "시설/탐험 아이콘 우선 표시"는 꺼지고 전부 정렬 기준 아이콘으로 전환된다).
        /// captureManager.ApplySortedOrder가 내부적으로 OnCapturedMemsChanged를 발행하므로, 그리드는 자체
        /// 구독으로 알아서 다시 그려진다(이때 GetStatDisplayInfo가 방금 갱신한 activeSortCriteria를 즉석에서
        /// 다시 읽으므로 최신 정렬 기준이 정확히 반영된다).
        /// </summary>
        private void HandleSortRequested(MemSortCriteria criteria)
        {
            Debug.Log($"[MemStorageUI] 정렬 요청 수신: {criteria}");

            if (captureManager == null || catalogManager == null)
            {
                Debug.LogWarning("[MemStorageUI] captureManager 또는 catalogManager가 비어있어 정렬을 처리할 수 없습니다.", this);
                return;
            }

            activeSortCriteria = criteria;

            var memDataLookup = MemSortHelper.BuildMemDataLookup(catalogManager.MemDataList);

            var nonEmptyEntries = new List<CapturedMemEntry>();
            foreach (var entry in captureManager.CapturedMems)
            {
                if (!entry.IsEmpty) nonEmptyEntries.Add(entry);
            }

            var sortableItems = nonEmptyEntries
                .Select(e => new SortableMemItem<CapturedMemEntry>(e.MemId, e.ExplorationStat, e))
                .ToList();

            var sortedEntries = MemSortHelper.SortEntries(sortableItems, criteria, memDataLookup);
            captureManager.ApplySortedOrder(sortedEntries);
        }

        /// <summary>
        /// 슬롯에 표시할 스탯/티어 아이콘+텍스트를 계산한다.
        /// - [최우선] 아직 이번 세션에 정렬한 적이 없고(activeSortCriteria == null) entry가 활성화(IsActive)돼
        ///   있으면: 먼저 탐험 중인지 확인해서(ExplorationRuntime) 맞으면 탐험 아이콘 + 실제 탐험레벨 숫자를,
        ///   아니면 배치된 시설이 요구하는 생산 스탯의 아이콘 + 그 멤의 실제 스탯 숫자를 표시한다
        ///   (FacilityDeploymentLookup으로 조회). 둘 다 찾지 못하면(방어 코드로 IsActive만 true인 비정상
        ///   상태 등) 아래 일반 로직으로 폴백한다.
        /// - 정렬한 적이 없으면(null) 또는 MemId로 정렬 중이면: 전부 Hidden
        /// - 티어로 정렬 중이면: 티어 아이콘 + 앞글자 대문자(R/E/U/L/M)
        /// - 나머지 Mem스탯으로 정렬 중이면: 그 스탯 아이콘 + 숫자
        /// [항상-최신반영] 이 메서드는 클로저로 값을 고정하지 않고 호출될 때마다 activeSortCriteria를 그
        /// 자리에서 다시 읽는 안정적인 메서드다 - Grid는 이 메서드 참조 하나만 한 번 캐싱해두고 계속
        /// 재사용해도 항상 최신 정렬 기준(및 활성 멤의 최신 배치 상태)이 반영된다.
        /// </summary>
        private MemStatDisplayInfo GetStatDisplayInfo(CapturedMemEntry entry)
        {
            if (activeSortCriteria == null && entry != null && entry.IsActive)
            {
                var explorationDisplayInfo = TryGetActiveExplorationDisplayInfo(entry);
                if (explorationDisplayInfo.HasValue)
                {
                    return explorationDisplayInfo.Value;
                }

                var facilityDisplayInfo = TryGetActiveFacilityDisplayInfo(entry);
                if (facilityDisplayInfo.HasValue)
                {
                    return facilityDisplayInfo.Value;
                }
                // 둘 다 찾지 못하면(방어 코드 경로 등) 아래 일반 로직으로 계속 진행한다.
            }

            if (activeSortCriteria == null || activeSortCriteria.Value == MemSortCriteria.MemId)
            {
                return MemStatDisplayInfo.Hidden;
            }

            var criteria = activeSortCriteria.Value;
            var memDataLookup = MemSortHelper.BuildMemDataLookup(catalogManager != null ? catalogManager.MemDataList : Array.Empty<MemData>());

            if (criteria == MemSortCriteria.Tier)
            {
                return BuildTierDisplayInfoForEntry(entry, memDataLookup);
            }

            var icon = GetStatIcon(criteria);
            return BuildStatDisplayInfoForEntry(entry, criteria, memDataLookup, icon);
        }

        /// <summary>entry가 지금 탐험 중인지 확인해서, 맞다면 탐험 아이콘 + 실제 탐험레벨(CapturedMemEntry.ExplorationStat) 숫자를 계산한다. 탐험 중이 아니면 null(호출 쪽에서 다음 로직으로 폴백).</summary>
        private MemStatDisplayInfo? TryGetActiveExplorationDisplayInfo(CapturedMemEntry entry)
        {
            if (explorationRuntime == null) return null;
            if (!explorationRuntime.TryGetExplorationInfo(entry, out _)) return null;

            return new MemStatDisplayInfo(true, explorationStatIcon, entry.ExplorationStat.ToString());
        }

        /// <summary>
        /// entry가 배치된 시설을 찾아 그 시설이 요구하는 스탯의 아이콘 + 그 멤의 실제 스탯 숫자를 계산한다.
        /// 시설을 찾지 못하거나 카탈로그에서 MemData를 찾지 못하면 null(호출 쪽에서 일반 로직으로 폴백).
        /// </summary>
        private MemStatDisplayInfo? TryGetActiveFacilityDisplayInfo(CapturedMemEntry entry)
        {
            if (!FacilityDeploymentLookup.TryGetRequiredStatType(entry, out var statType))
            {
                return null;
            }

            var data = FindMemData(entry.MemId);
            if (data == null)
            {
                return null;
            }

            var sortCriteriaForIcon = ToMemSortCriteria(statType);
            var icon = GetStatIcon(sortCriteriaForIcon);
            var value = data.productionStats.GetStat(statType);

            return new MemStatDisplayInfo(true, icon, value.ToString());
        }

        /// <summary>ProductionStatType(생산/제작 시설 쪽 스탯 종류)을 이름이 동일한 MemSortCriteria로 변환한다(아이콘 조회용).</summary>
        private static MemSortCriteria ToMemSortCriteria(ProductionStatType statType)
        {
            switch (statType)
            {
                case ProductionStatType.Crafting: return MemSortCriteria.Crafting;
                case ProductionStatType.Logging: return MemSortCriteria.Logging;
                case ProductionStatType.Mining: return MemSortCriteria.Mining;
                case ProductionStatType.Transport: return MemSortCriteria.Transport;
                case ProductionStatType.Farming: return MemSortCriteria.Farming;
                default: return MemSortCriteria.Crafting;
            }
        }

        private MemStatDisplayInfo BuildStatDisplayInfoForEntry(CapturedMemEntry entry, MemSortCriteria criteria, Dictionary<string, MemData> lookup, Sprite icon)
        {
            if (entry == null || entry.IsEmpty) return MemStatDisplayInfo.Hidden;

            int value = criteria == MemSortCriteria.Exploration
                ? entry.ExplorationStat
                : MemSortHelper.GetProductionStat(entry.MemId, lookup, ToProductionStatType(criteria));

            return new MemStatDisplayInfo(true, icon, value.ToString());
        }

        /// <summary>티어 정렬 중일 때 슬롯에 표시할 티어 아이콘 + 앞글자 대문자(R/E/U/L/M)를 만든다.</summary>
        private MemStatDisplayInfo BuildTierDisplayInfoForEntry(CapturedMemEntry entry, Dictionary<string, MemData> lookup)
        {
            if (entry == null || entry.IsEmpty) return MemStatDisplayInfo.Hidden;
            if (!lookup.TryGetValue(entry.MemId, out var data)) return MemStatDisplayInfo.Hidden;

            var icon = GetTierIcon(data.tier);
            var letter = MemSortHelper.GetTierLetter(data.tier);

            return new MemStatDisplayInfo(true, icon, letter);
        }

        private static ProductionStatType ToProductionStatType(MemSortCriteria criteria)
        {
            switch (criteria)
            {
                case MemSortCriteria.Crafting: return ProductionStatType.Crafting;
                case MemSortCriteria.Logging: return ProductionStatType.Logging;
                case MemSortCriteria.Mining: return ProductionStatType.Mining;
                case MemSortCriteria.Transport: return ProductionStatType.Transport;
                case MemSortCriteria.Farming: return ProductionStatType.Farming;
                default: return ProductionStatType.Crafting; // Exploration은 별도 분기에서 처리되므로 여기로 오지 않음
            }
        }

        private Sprite GetStatIcon(MemSortCriteria criteria)
        {
            switch (criteria)
            {
                case MemSortCriteria.Crafting: return craftingStatIcon;
                case MemSortCriteria.Logging: return loggingStatIcon;
                case MemSortCriteria.Mining: return miningStatIcon;
                case MemSortCriteria.Transport: return transportStatIcon;
                case MemSortCriteria.Farming: return farmingStatIcon;
                case MemSortCriteria.Exploration: return explorationStatIcon;
                default: return null;
            }
        }

        private Sprite GetTierIcon(MemTier tier)
        {
            switch (tier)
            {
                case MemTier.Rare: return rareTierIcon;
                case MemTier.Epic: return epicTierIcon;
                case MemTier.Unique: return uniqueTierIcon;
                case MemTier.Legendary: return legendaryTierIcon;
                case MemTier.Mythic: return mythicTierIcon;
                default: return null;
            }
        }

        /// <summary>MemCatalogManager에 등록된 SO 목록에서 memId가 일치하는 MemData를 찾는다.</summary>
        private MemData FindMemData(string memId)
        {
            if (catalogManager == null) return null;
            return catalogManager.MemDataList.FirstOrDefaultByMemId(memId);
        }
    }

    /// <summary>MemCatalogManager의 SO 목록에서 memId로 탐색하기 위한 확장 메서드.</summary>
    internal static class MemDataListExtensions
    {
        public static MemData FirstOrDefaultByMemId(this IReadOnlyList<MemData> list, string memId)
        {
            if (list == null || string.IsNullOrEmpty(memId)) return null;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].memId == memId)
                {
                    return list[i];
                }
            }

            return null;
        }
    }
}
