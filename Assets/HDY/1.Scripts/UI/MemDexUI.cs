using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MemSystem.Data;
using HDY.Mem;
using HDY.Capture;

namespace HDY.UI
{
    /// <summary>
    /// 멤 도감 UI 컨트롤러.
    /// MemCatalogManager.MemDataList 전체(포획 여부와 무관하게 카탈로그에 등록된 모든 멤)를 도감
    /// 그리드(MemDexUI_Grid)에 채우고, 정렬 버튼(MemStorageUI_Sort 재사용)과 정보 패널(MemStorageUI_Info
    /// 재사용)을 연결한다.
    ///
    /// [정렬은 표시 전용, 저장 안 함] 멤창고(MemStorageUI)와 달리 도감은 정렬 결과를 어디에도 저장하지 않는다 -
    /// MemCatalogManager는 읽기 전용 데이터라 정렬 순서를 영구히 바꿀 이유가 없다. 정렬 버튼을 누르면 그때그때
    /// MemSortHelper로 다시 정렬해서 그리드 슬롯 순서만 다시 채운다(창고와 완전히 동일한 비교 로직 재사용,
    /// SortableMemItem&lt;MemData&gt;로 감싸서 넘긴다).
    ///
    /// [탐험 스탯 정렬 - MemTierTable 참조] MemData.explorationStat 하나만으로는 개체마다 랜덤하게 다른
    /// 실제 탐험 스탯 범위를 반영하지 못한다(포획된 개체가 없는 도감에서는 애초에 "하나의 값"이 존재하지 않음).
    /// 그래서 탐험 기준 정렬/슬롯 숫자 표시는 MemTierTable에서 해당 등급의 explorationMax(최댓값)를 찾아 사용한다.
    /// 정보 패널의 "최소~최대" 범위 표시는 MemStorageUI_Info 쪽이 자체적으로 MemTierTable을 참조해서 처리한다.
    ///
    /// [Mem스탯/티어 표시] MemStorageUI와 동일한 개념 - 정렬 기준이 스탯/티어면 그 아이콘+값을, 아니면(MemId
    /// 또는 정렬 전) 감춘다. 아이콘 Sprite 필드는 MemStorageUI와 별개로 이 컨트롤러에 따로 있다(같은 스프라이트를
    /// 인스펙터에서 한 번 더 연결해야 함).
    ///
    /// [HDY 요청 - 최초 포획 정보] MemDexRecordManager에서 각 멤 종의 최초 포획 기록을 조회해서 두 군데에 반영한다:
    /// 1) 그리드 슬롯(MemDexSlotUI) - 발견 여부를 isDiscoveredProvider로 넘겨 미발견 종은 아이콘을 검게 틴트.
    /// 2) 정보 패널(MemStorageUI_Info) - 슬롯 클릭 시 최초 포획 시각(날짜+시간)을 함께 넘겨 표시한다.
    ///    이 최초 포획 정보 줄은 도감에서만 보이도록 MemStorageUI_Info.ShowInfo(MemData, long?) 오버로드로만
    ///    전달한다 - 창고(MemStorageUI)가 쓰는 ShowInfo(entry, data) 오버로드는 건드리지 않는다.
    /// 새로운 멤이 최초로 포획되어 MemDexRecordManager.OnFirstCaptureRecorded가 발행되면, 도감이 열려있는
    /// 동안이라도 그리드를 다시 그려 실루엣이 바로 풀리도록 구독해둔다.
    /// </summary>
    public class MemDexUI : MonoBehaviour
    {
        [Header("데이터 참조")]
        [SerializeField] private MemCatalogManager catalogManager;
        [Tooltip("등급별 탐험 스탯 범위(최소~최대) 조회용. 탐험 기준 정렬 시 등급의 explorationMax(최댓값)를 사용한다.")]
        [SerializeField] private MemTierTable tierTable;
        [Tooltip("각 멤 종의 최초 포획 기록(발견 여부/최초 포획 시각) 조회용. 비어두면 MemDexRecordManager.Instance로 자동 보정한다.")]
        [SerializeField] private MemDexRecordManager dexRecordManager;

        [Header("하위 UI (그리드 / 정보 패널 / 정렬 버튼)")]
        [SerializeField] private MemDexUI_Grid grid;
        [SerializeField] private MemStorageUI_Info info;
        [SerializeField] private MemStorageUI_Sort sort;

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

        // 현재 도감이 어떤 기준으로 정렬되어 있는지. 아직 한 번도 정렬하지 않았으면 null(카탈로그 등록 순서 그대로, 스탯/티어 표시 없음).
        private MemSortCriteria? activeSortCriteria;

        private void Awake()
        {
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;
            dexRecordManager = MemDexRecordManager.Resolve(dexRecordManager);

            if (catalogManager == null) Debug.LogWarning("[MemDexUI] catalogManager가 비어있습니다. 도감 데이터를 읽어올 수 없습니다.", this);
            if (dexRecordManager == null) Debug.LogWarning("[MemDexUI] dexRecordManager가 비어있습니다. 최초 포획(실루엣/정보) 표시가 동작하지 않습니다.", this);
            if (tierTable == null) Debug.LogWarning("[MemDexUI] tierTable이 비어있습니다. 탐험 기준 정렬이 MemData.explorationStat으로 대체됩니다.", this);
            if (grid == null) Debug.LogWarning("[MemDexUI] grid가 비어있습니다.", this);
            if (info == null) Debug.LogWarning("[MemDexUI] info가 비어있습니다.", this);
            if (sort == null) Debug.LogWarning("[MemDexUI] sort가 비어있습니다. 정렬 버튼이 동작하지 않습니다.", this);

        }

        private void OnEnable()
        {
            if (catalogManager == null) catalogManager = MemCatalogManager.Instance;
            dexRecordManager = MemDexRecordManager.Resolve(dexRecordManager);

            if (grid != null)
            {
                grid.OnSlotClicked -= HandleSlotClicked;
                grid.OnSlotClicked += HandleSlotClicked;
            }

            if (sort != null)
            {
                sort.OnSortRequested -= HandleSortRequested;
                sort.OnSortRequested += HandleSortRequested;
            }

            // [HDY 요청 - 최초 포획 정보] 도감이 열려있는 동안 새로 발견되는 멤이 생기면(예: 도감을 백그라운드에
            // 둔 채 포획) 실루엣이 바로 풀리도록 그리드를 다시 그린다.
            if (dexRecordManager != null)
            {
                dexRecordManager.OnFirstCaptureRecorded -= HandleFirstCaptureRecorded;
                dexRecordManager.OnFirstCaptureRecorded += HandleFirstCaptureRecorded;
            }

            RefreshGrid();
        }

        private void OnDisable()
        {
            if (grid != null)
            {
                grid.OnSlotClicked -= HandleSlotClicked;
            }

            if (sort != null)
            {
                sort.OnSortRequested -= HandleSortRequested;
            }

            if (dexRecordManager != null)
            {
                dexRecordManager.OnFirstCaptureRecorded -= HandleFirstCaptureRecorded;
            }
        }

        private void HandleSlotClicked(MemData data)
        {
            Debug.Log($"[MemDexUI] 슬롯 클릭 처리: MemId={data.memId}");

            if (info != null)
            {
                // [HDY 요청 - 최초 포획 정보, 도감에서만 표시] 발견된 종이면 최초 포획 시각을 함께 넘겨서
                // 정보 패널에 표시한다. ShowInfo(MemData, long?) 오버로드는 도감 전용이라 창고 쪽
                // ShowInfo(entry, data)에는 영향이 없다.
                long? firstCapturedTimestamp = null;
                if (dexRecordManager != null && dexRecordManager.TryGetFirstCaptureInfo(data.memId, out var record))
                {
                    firstCapturedTimestamp = record.FirstCapturedTimestamp;
                }

                info.ShowInfo(data, firstCapturedTimestamp);
            }
        }

        /// <summary>정렬 버튼 클릭 요청을 받아 기준을 기억해두고 그리드를 다시 채운다.</summary>
        private void HandleSortRequested(MemSortCriteria criteria)
        {
            Debug.Log($"[MemDexUI] 정렬 요청 수신: {criteria}");

            activeSortCriteria = criteria;
            RefreshGrid();
        }

        /// <summary>새로운 멤 종이 최초로 발견될 때 호출된다(MemDexRecordManager.OnFirstCaptureRecorded). 그리드를 다시 그려 실루엣을 갱신한다.</summary>
        private void HandleFirstCaptureRecorded(string memId, long firstCapturedTimestamp)
        {
            Debug.Log($"[MemDexUI] 최초 포획 알림 수신: MemId={memId} - 그리드 갱신");
            RefreshGrid();
        }

        /// <summary>현재 정렬 기준으로 카탈로그 전체를 다시 정렬해서(또는 정렬 전이면 등록 순서 그대로) 그리드를 채운다.</summary>
        private void RefreshGrid()
        {
            if (grid == null || catalogManager == null) return;

            var allData = catalogManager.MemDataList;

            List<MemData> orderedData;

            if (activeSortCriteria == null)
            {
                orderedData = allData.ToList();
            }
            else
            {
                var memDataLookup = MemSortHelper.BuildMemDataLookup(allData);

                var sortableItems = allData
                    .Where(data => data != null)
                    .Select(data => new SortableMemItem<MemData>(data.memId, GetExplorationSortValue(data), data))
                    .ToList();

                orderedData = MemSortHelper.SortEntries(sortableItems, activeSortCriteria.Value, memDataLookup);
            }

            grid.Populate(orderedData, BuildStatDisplayProvider(), IsDiscovered);
        }

        /// <summary>
        /// [HDY 요청 - 최초 포획 실루엣] 이 멤 종이 최초 포획 기록이 있는지(도감에 발견되었는지) 여부.
        /// dexRecordManager가 없으면 안전한 기본값으로 전부 발견된 것으로 처리한다(전부 까맣게 가려버리는
        /// 것을 피하기 위함).
        /// </summary>
        private bool IsDiscovered(MemData data)
        {
            if (dexRecordManager == null || data == null) return true;
            return dexRecordManager.IsDiscovered(data.memId);
        }

        /// <summary>
        /// 정렬/슬롯 표시에 쓸 탐험 스탯 값. MemTierTable에서 해당 등급의 최댓값(explorationMax)을 찾아 사용한다 -
        /// MemData.explorationStat 하나만으로는 개체별 실제 범위를 반영하지 못해서다. 테이블/스펙이 없으면
        /// MemData.explorationStat으로 대체한다(경고 로그 남김).
        /// </summary>
        private int GetExplorationSortValue(MemData data)
        {
            var spec = tierTable != null ? tierTable.GetSpec(data.tier) : null;
            if (spec != null) return spec.explorationMax;

            Debug.LogWarning($"[MemDexUI] MemTierTable에서 '{data.tier}' 등급 스펙을 찾을 수 없어 MemData.explorationStat으로 대체합니다.", this);
            return data.explorationStat;
        }

        /// <summary>
        /// 현재 activeSortCriteria를 기준으로, 그리드에 넘겨줄 "슬롯마다 아이콘/텍스트를 계산하는 함수"를 만든다.
        /// MemStorageUI의 BuildStatDisplayProvider와 동일한 개념이며, 데이터 원본만 MemData로 다르다.
        /// </summary>
        private Func<MemData, MemStatDisplayInfo> BuildStatDisplayProvider()
        {
            if (activeSortCriteria == null || activeSortCriteria.Value == MemSortCriteria.MemId)
            {
                return _ => MemStatDisplayInfo.Hidden;
            }

            var criteria = activeSortCriteria.Value;

            if (criteria == MemSortCriteria.Tier)
            {
                return BuildTierDisplayInfo;
            }

            var icon = GetStatIcon(criteria);
            return data => BuildStatDisplayInfo(data, criteria, icon);
        }

        private MemStatDisplayInfo BuildStatDisplayInfo(MemData data, MemSortCriteria criteria, Sprite icon)
        {
            if (data == null) return MemStatDisplayInfo.Hidden;

            // 탐험 기준일 때는 정렬에 쓴 값(등급 최댓값)과 슬롯 숫자를 일치시킨다.
            int value = criteria == MemSortCriteria.Exploration
                ? GetExplorationSortValue(data)
                : data.productionStats.GetStat(ToProductionStatType(criteria));

            return new MemStatDisplayInfo(true, icon, value.ToString());
        }

        /// <summary>티어 정렬 중일 때 슬롯에 표시할 티어 아이콘 + 앞글자 대문자(R/E/U/L/M)를 만든다.</summary>
        private MemStatDisplayInfo BuildTierDisplayInfo(MemData data)
        {
            if (data == null) return MemStatDisplayInfo.Hidden;

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
    }
}
