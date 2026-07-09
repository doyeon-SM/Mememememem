using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;
using HDY.Upgrade;

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
    /// storageUpgrade가 처리하고, 이 컨트롤러는 MemCaptureManager.OnStorageCapacityChanged를 구독해뒀다가
    /// 언락된 페이지 수가 바뀌면 그리드를 다시 그려주기만 한다.
    ///
    /// [배치 해제] 그리드에서 활성 멤을 우클릭 -> 해제하기 버튼 클릭 시 grid.OnReleaseRequested가 발생한다.
    /// 이 창고 UI는 그 멤이 어느 시설(_Kyusoo의 ProductionFacilityRuntime)에 배치되어 있는지 전혀 모르므로,
    /// 씬에 있는 모든 ProductionFacilityRuntime을 훑어 이 entry를 DeployedMemEntries로 갖고 있는 시설을 찾아
    /// 그 시설의 RemoveMem()을 호출한다. CapturedMemEntry는 참조 타입이라 시설 쪽 리스트와 MemCaptureManager
    /// 쪽 리스트가 같은 객체를 가리키므로, RemoveMem() 호출만으로 IsActive도 자동으로 false가 된다(별도로
    /// 손댈 필요 없음). 다만 MemCaptureManager는 이 변경을 스스로 감지하지 못하므로, 그 다음 그리드를
    /// NotifyDataChanged로 직접 다시 그려서 활성 표시(activeImage)를 갱신해준다.
    ///
    /// [TODO - 시설 쪽 변경 감지 (Kyusoo 이벤트 필요)] 지금은 "창고에서 직접 해제"할 때만 그리드가 갱신되고,
    /// 반대로 시설 쪽 UI(ProductionMemSlotUI/ProductionPanelUI)를 통해 멤이 배치/해제될 때는 창고 그리드의
    /// 활성 표시가 갱신되지 않는 비대칭 문제가 있다. _Kyusoo/ProductionFacilityRuntime.cs에
    /// `public event Action OnDeployedMemsChanged;`를 추가해서 TryAddMem/RemoveMem 끝에서 호출해주면,
    /// 아래 SubscribeToFacilityChanges/UnsubscribeFromFacilityChanges/HandleFacilityDeployedMemsChanged
    /// (현재 주석 처리됨)의 주석을 풀고 OnEnable/OnDisable의 호출부 주석도 풀어서 바로 쓸 수 있다.
    ///
    /// [Mem스탯/티어 표시] 현재 어떤 기준으로 정렬되어 있는지(activeSortCriteria)를 여기서 기억해두고,
    /// - Mem스탯(제작/벌목/채광/이동/생산/탐험) 기준이면 그 스탯의 아이콘 + 숫자를,
    /// - 티어 기준이면 티어 아이콘 + 등급 앞글자 대문자(R/E/U/L/M)를
    /// 그리드의 각 슬롯에 표시하도록 MemStatDisplayInfo를 계산해서 Grid에 넘겨준다.
    /// MemId로 정렬 중이거나 아직 정렬한 적이 없으면 표시를 감춘다.
    ///
    /// [씬 이동 대응] MemCaptureManager/MemCatalogManager는 파괴불가 싱글톤이라 씬을 이동해도 유지되지만,
    /// 이 컴포넌트는 씬에 배치된 오브젝트라서 씬이 다시 로드되면 인스펙터 참조가 끊길 수 있다.
    /// (씬 파일에 같이 저장된 매니저 오브젝트는 재로드 시 새로 생기지만, 싱글톤 중복 검사로 즉시 파괴되기 때문)
    /// Awake/OnEnable에서 참조가 끊겨 있으면(null) 싱글톤 Instance로 자동 재할당한다.
    /// </summary>
    public class MemStorageUI : MonoBehaviour
    {
        [Header("데이터 참조")]
        [SerializeField] private MemCaptureManager captureManager;
        [SerializeField] private MemCatalogManager catalogManager;

        [Header("하위 UI (그리드 / 정보 패널 / 정렬 버튼)")]
        [SerializeField] private MemStorageUI_Grid grid;
        [SerializeField] private MemStorageUI_Info info;
        [SerializeField] private MemStorageUI_Sort sort;

        [Header("멤창고 업그레이드 (페이지 확장)")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private MemStorageUpgrade storageUpgrade;

        [Header("Mem스탯 아이콘 (정렬 기준이 해당 스탯일 때 슬롯에 표시)")]
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

        // 현재 창고가 어떤 기준으로 정렬되어 있는지. 아직 한 번도 정렬하지 않았으면 null(스탯/티어 표시 없음).
        private MemSortCriteria? activeSortCriteria;

        private void Awake()
        {
            // 씬 재로드 등으로 인스펙터 참조가 끊겼으면 파괴불가 싱글톤에서 다시 가져온다.
            if (captureManager == null) captureManager = MemCaptureManager.Instance;
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;

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

            if (captureManager != null)
            {
                captureManager.OnCapturedMemsChanged += HandleCapturedMemsChanged;
                captureManager.OnStorageCapacityChanged += HandleStorageCapacityChanged;
            }

            // [TODO - Kyusoo 이벤트 필요] _Kyusoo/ProductionFacilityRuntime.cs에 OnDeployedMemsChanged가
            // 추가되면 아래 주석을 풀어서, 시설 쪽에서 배치/해제가 일어날 때도 창고 그리드가 갱신되도록 한다.
            // SubscribeToFacilityChanges();

            if (grid != null && captureManager != null)
            {
                grid.ShowInitial(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider(), captureManager.UnlockedPageCount);
            }
        }

        private void OnDisable()
        {
            if (captureManager != null)
            {
                captureManager.OnCapturedMemsChanged -= HandleCapturedMemsChanged;
                captureManager.OnStorageCapacityChanged -= HandleStorageCapacityChanged;
            }

            // [TODO - Kyusoo 이벤트 필요] SubscribeToFacilityChanges()와 짝을 맞춰 구독 해제.
            // UnsubscribeFromFacilityChanges();

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

        /// <summary>새로 멤이 포획되거나 슬롯 위치/정렬 순서가 바뀌는 등 데이터가 바뀔 때마다 호출된다.</summary>
        private void HandleCapturedMemsChanged()
        {
            Debug.Log("[MemStorageUI] OnCapturedMemsChanged 수신 -> 그리드 갱신 시도");

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider(), captureManager.UnlockedPageCount);
            }
        }

        /// <summary>멤창고 페이지 언락(업그레이드)에 성공해서 사용 가능한 페이지 수가 바뀔 때 호출된다.</summary>
        private void HandleStorageCapacityChanged()
        {
            Debug.Log("[MemStorageUI] OnStorageCapacityChanged 수신 -> 그리드 갱신 시도");

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider(), captureManager.UnlockedPageCount);
            }
        }

        // ==========================================================================================
        // [TODO - _Kyusoo/ProductionFacilityRuntime.cs 수정 필요, 현재 미구현이라 전체 주석 처리]
        //
        // _Kyusoo 쪽에 다음을 추가했다고 가정한 코드:
        //   1) ProductionFacilityRuntime에 `public event Action OnDeployedMemsChanged;` 추가
        //   2) TryAddMem(...)과 RemoveMem(...) 끝에서 OnDeployedMemsChanged?.Invoke() 호출
        //
        // 이 이벤트가 실제로 생기면, 창고에서 직접 해제할 때뿐 아니라 시설 쪽 UI(ProductionMemSlotUI/
        // ProductionPanelUI)를 통해 멤이 배치/해제될 때도 창고 그리드의 활성 표시(activeImage)가 즉시
        // 갱신되도록 씬의 모든 시설을 구독해둔다. 지금은 컴파일되지 않으므로(OnDeployedMemsChanged가 실제로
        // 없음) 전체를 주석 처리해두고, 위 OnEnable/OnDisable의 호출부와 함께 이 블록의 주석을 풀면 바로
        // 동작한다.
        //
        // [한계] 씬에 이미 있는 시설만 구독한다 - 이후 새로 지어지는 시설은 이 목록에 없으므로 구독되지 않는다.
        // (시설이 "새로 지어졌다"를 알리는 이벤트도 아직 없어서, 필요하면 GridManager 쪽에 별도로 요청해야 한다.)
        //
        // private void SubscribeToFacilityChanges()
        // {
        //     var facilities = UnityEngine.Object.FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        //     foreach (var facility in facilities)
        //     {
        //         facility.OnDeployedMemsChanged += HandleFacilityDeployedMemsChanged;
        //     }
        // }
        //
        // private void UnsubscribeFromFacilityChanges()
        // {
        //     var facilities = UnityEngine.Object.FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        //     foreach (var facility in facilities)
        //     {
        //         facility.OnDeployedMemsChanged -= HandleFacilityDeployedMemsChanged;
        //     }
        // }
        //
        // private void HandleFacilityDeployedMemsChanged()
        // {
        //     Debug.Log("[MemStorageUI] 시설 쪽 배치 변경 감지 -> 그리드 갱신 시도");
        //
        //     if (grid != null && captureManager != null)
        //     {
        //         grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider(), captureManager.UnlockedPageCount);
        //     }
        // }
        // ==========================================================================================

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

        /// <summary>그리드에서 드래그앤드롭으로 슬롯 위치 교체가 요청되었을 때 호출. 실제 데이터(MemCaptureManager)에 반영한다.</summary>
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
        /// 배치되어 있는지 모르므로, 씬의 모든 ProductionFacilityRuntime을 훑어 DeployedMemEntries에 이
        /// entry를 갖고 있는 시설을 찾는다. CapturedMemEntry는 참조 타입이라 시설 쪽 리스트와
        /// MemCaptureManager 쪽 리스트가 같은 객체를 가리키므로, 시설의 RemoveMem() 호출만으로 IsActive도
        /// 자동으로 false가 된다. 이후 MemCaptureManager는 이 변경을 스스로 감지하지 못하므로 그리드를
        /// 직접 다시 그려서 활성 표시를 갱신한다.
        /// </summary>
        private void HandleReleaseRequested(CapturedMemEntry entry, MemData data)
        {
            if (entry == null) return;

            Debug.Log($"[MemStorageUI] 배치 해제 요청 수신: MemId={entry.MemId}");

            ProductionFacilityRuntime ownerFacility = null;
            var facilities = UnityEngine.Object.FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);

            foreach (var facility in facilities)
            {
                if (facility.DeployedMemEntries.Contains(entry))
                {
                    ownerFacility = facility;
                    break;
                }
            }

            if (ownerFacility != null)
            {
                int index = ownerFacility.DeployedMemEntries.IndexOf(entry);
                MemData matchedMemData = (index >= 0 && index < ownerFacility.DeployedMems.Count) ? ownerFacility.DeployedMems[index] : data;

                ownerFacility.RemoveMem(matchedMemData);
                Debug.Log($"[MemStorageUI] 배치 해제 완료: MemId={entry.MemId}, 시설={ownerFacility.name}");
            }
            else
            {
                // 방어 코드: 어느 시설에도 등록되어 있지 않은데 IsActive만 true인 비정상 상태라면,
                // 창고 UI가 계속 활성 표시로 막혀있지 않도록 여기서라도 직접 되돌린다.
                Debug.LogWarning($"[MemStorageUI] 배치 해제 대상을 가진 시설을 찾지 못했습니다. IsActive만 직접 되돌립니다: MemId={entry.MemId}", this);
                entry.IsActive = false;
            }

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider(), captureManager.UnlockedPageCount);
            }
        }

        /// <summary>
        /// 정렬 버튼 클릭 요청을 받아 실제 정렬(카탈로그 조회 + 비교, MemSortHelper 재사용)을 수행하고,
        /// 결과를 MemCaptureManager에 반영한다. 빈 칸은 정렬 대상에서 제외한 뒤 ApplySortedOrder가 자동으로
        /// 뒤쪽에 채운다. 정렬 기준을 activeSortCriteria에 기억해서 이후 그리드 갱신 시 Mem스탯/티어 표시에 사용한다.
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
        /// 현재 activeSortCriteria를 기준으로, 그리드에 넘겨줄 "슬롯마다 아이콘/텍스트를 계산하는 함수"를 만든다.
        /// - 정렬한 적이 없으면(null) 또는 MemId로 정렬 중이면: 전부 Hidden
        /// - 티어로 정렬 중이면: 티어 아이콘 + 앞글자 대문자(R/E/U/L/M)
        /// - 나머지 Mem스탯으로 정렬 중이면: 그 스탯 아이콘 + 숫자
        /// </summary>
        private Func<CapturedMemEntry, MemStatDisplayInfo> BuildStatDisplayProvider()
        {
            if (activeSortCriteria == null || activeSortCriteria.Value == MemSortCriteria.MemId)
            {
                return _ => MemStatDisplayInfo.Hidden;
            }

            var criteria = activeSortCriteria.Value;
            var memDataLookup = MemSortHelper.BuildMemDataLookup(catalogManager != null ? catalogManager.MemDataList : Array.Empty<MemData>());

            if (criteria == MemSortCriteria.Tier)
            {
                return entry => BuildTierDisplayInfoForEntry(entry, memDataLookup);
            }

            var icon = GetStatIcon(criteria);
            return entry => BuildStatDisplayInfoForEntry(entry, criteria, memDataLookup, icon);
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
