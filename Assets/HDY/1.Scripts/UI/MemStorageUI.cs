using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MemSystem.Data;
using HDY.Capture;
using HDY.Mem;

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

            if (grid != null)
            {
                grid.OnSlotClicked += HandleSlotClicked;
                grid.OnSwapRequested += HandleSwapRequested;
            }

            if (sort != null)
            {
                sort.OnSortRequested += HandleSortRequested;
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
            }

            if (grid != null && captureManager != null)
            {
                grid.ShowInitial(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider());
            }
        }

        private void OnDisable()
        {
            if (captureManager != null)
            {
                captureManager.OnCapturedMemsChanged -= HandleCapturedMemsChanged;
            }

            if (grid != null)
            {
                grid.OnSlotClicked -= HandleSlotClicked;
                grid.OnSwapRequested -= HandleSwapRequested;
            }

            if (sort != null)
            {
                sort.OnSortRequested -= HandleSortRequested;
            }
        }

        /// <summary>새로 멤이 포획되거나 슬롯 위치/정렬 순서가 바뀌는 등 데이터가 바뀔 때마다 호출된다.</summary>
        private void HandleCapturedMemsChanged()
        {
            Debug.Log("[MemStorageUI] OnCapturedMemsChanged 수신 -> 그리드 갱신 시도");

            if (grid != null && captureManager != null)
            {
                grid.NotifyDataChanged(captureManager.CapturedMems, FindMemData, BuildStatDisplayProvider());
            }
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
        /// 정렬 버튼 클릭 요청을 받아 실제 정렬(카탈로그 조회 + 비교)을 수행하고, 결과를 MemCaptureManager에 반영한다.
        /// 빈 칸은 정렬 대상에서 제외한 뒤 ApplySortedOrder가 자동으로 뒤쪽에 채운다.
        /// 정렬 기준을 activeSortCriteria에 기억해서 이후 그리드 갱신 시 Mem스탯/티어 표시에 사용한다.
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

            // MemId -> MemData 캐시를 한 번만 만들어, 정렬 비교마다 카탈로그를 매번 선형 탐색하지 않도록 한다
            // (전처리 O(카탈로그 크기) 이후 비교마다 O(1) 조회).
            var memDataLookup = BuildMemDataLookup();

            var nonEmptyEntries = new List<CapturedMemEntry>();
            foreach (var entry in captureManager.CapturedMems)
            {
                if (!entry.IsEmpty) nonEmptyEntries.Add(entry);
            }

            var sortedEntries = SortEntries(nonEmptyEntries, criteria, memDataLookup);
            captureManager.ApplySortedOrder(sortedEntries);
        }

        /// <summary>MemId -> MemData 조회용 딕셔너리를 카탈로그 전체에서 한 번 만든다.</summary>
        private Dictionary<string, MemData> BuildMemDataLookup()
        {
            var lookup = new Dictionary<string, MemData>();
            if (catalogManager == null) return lookup;

            foreach (var data in catalogManager.MemDataList)
            {
                if (data != null && !string.IsNullOrEmpty(data.memId) && !lookup.ContainsKey(data.memId))
                {
                    lookup[data.memId] = data;
                }
            }

            return lookup;
        }

        /// <summary>
        /// 정렬 기준에 따라 채워진 항목들을 정렬한다. MemId는 오름차순, 나머지(Tier/스탯 5종/탐험)는 내림차순.
        /// LINQ OrderBy/OrderByDescending은 안정 정렬(Stable Sort)이라 값이 같은 항목끼리는 원래 순서를 유지한다
        /// (여러 번 눌러도 결과가 흔들리지 않음). 창고 최대치(기본 480칸) 규모에서는 O(n log n)으로 충분히 빠르다.
        /// </summary>
        private List<CapturedMemEntry> SortEntries(List<CapturedMemEntry> entries, MemSortCriteria criteria, Dictionary<string, MemData> memDataLookup)
        {
            switch (criteria)
            {
                case MemSortCriteria.MemId:
                    return entries.OrderBy(e => e.MemId, StringComparer.Ordinal).ToList();

                case MemSortCriteria.Tier:
                    return entries.OrderByDescending(e => GetTier(e, memDataLookup)).ToList();

                case MemSortCriteria.Crafting:
                    return entries.OrderByDescending(e => GetProductionStat(e, memDataLookup, ProductionStatType.Crafting)).ToList();

                case MemSortCriteria.Logging:
                    return entries.OrderByDescending(e => GetProductionStat(e, memDataLookup, ProductionStatType.Logging)).ToList();

                case MemSortCriteria.Mining:
                    return entries.OrderByDescending(e => GetProductionStat(e, memDataLookup, ProductionStatType.Mining)).ToList();

                case MemSortCriteria.Transport:
                    return entries.OrderByDescending(e => GetProductionStat(e, memDataLookup, ProductionStatType.Transport)).ToList();

                case MemSortCriteria.Farming:
                    return entries.OrderByDescending(e => GetProductionStat(e, memDataLookup, ProductionStatType.Farming)).ToList();

                case MemSortCriteria.Exploration:
                    // 탐험 스탯은 캡슐 카탈로그 조회 없이 CapturedMemEntry 자체 값으로 바로 정렬 가능하다.
                    return entries.OrderByDescending(e => e.ExplorationStat).ToList();

                default:
                    return entries;
            }
        }

        /// <summary>카탈로그에서 멤의 등급(Tier)을 조회한다. 카탈로그에 없으면 가장 낮은 값으로 취급해 뒤로 보낸다.</summary>
        private static int GetTier(CapturedMemEntry entry, Dictionary<string, MemData> lookup)
        {
            return lookup.TryGetValue(entry.MemId, out var data) ? (int)data.tier : -1;
        }

        /// <summary>카탈로그에서 멤의 생산 스탯 한 종류를 조회한다. 카탈로그에 없으면 가장 낮은 값으로 취급해 뒤로 보낸다.</summary>
        private static int GetProductionStat(CapturedMemEntry entry, Dictionary<string, MemData> lookup, ProductionStatType type)
        {
            return lookup.TryGetValue(entry.MemId, out var data) ? data.productionStats.GetStat(type) : -1;
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
            var memDataLookup = BuildMemDataLookup();

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
                : GetProductionStat(entry, lookup, ToProductionStatType(criteria));

            return new MemStatDisplayInfo(true, icon, value.ToString());
        }

        /// <summary>티어 정렬 중일 때 슬롯에 표시할 티어 아이콘 + 앞글자 대문자(R/E/U/L/M)를 만든다.</summary>
        private MemStatDisplayInfo BuildTierDisplayInfoForEntry(CapturedMemEntry entry, Dictionary<string, MemData> lookup)
        {
            if (entry == null || entry.IsEmpty) return MemStatDisplayInfo.Hidden;
            if (!lookup.TryGetValue(entry.MemId, out var data)) return MemStatDisplayInfo.Hidden;

            var icon = GetTierIcon(data.tier);
            var letter = GetTierLetter(data.tier);

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

        /// <summary>티어의 앞글자를 대문자로 반환한다 (Rare->R, Epic->E, Unique->U, Legendary->L, Mythic->M).</summary>
        private static string GetTierLetter(MemTier tier)
        {
            switch (tier)
            {
                case MemTier.Rare: return "R";
                case MemTier.Epic: return "E";
                case MemTier.Unique: return "U";
                case MemTier.Legendary: return "L";
                case MemTier.Mythic: return "M";
                default: return "-";
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
