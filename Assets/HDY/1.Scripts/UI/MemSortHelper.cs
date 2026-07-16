using System;
using System.Collections.Generic;
using System.Linq;
using MemSystem.Data;

namespace HDY.UI
{
    /// <summary>
    /// 정렬에 필요한 memId/탐험 스탯 + 원본 항목(T)을 함께 담는 래퍼.
    /// 정렬 후 다시 원본 항목(T) 목록으로 꺼낼 수 있도록 한다.
    /// </summary>
    public readonly struct SortableMemItem<T>
    {
        public readonly string MemId;
        public readonly int ExplorationStat;
        public readonly T Item;

        public SortableMemItem(string memId, int explorationStat, T item)
        {
            MemId = memId;
            ExplorationStat = explorationStat;
            Item = item;
        }
    }

    /// <summary>
    /// 멤 정렬/카탈로그 조회 공용 헬퍼.
    /// 멤창고(MemStorageUI, CapturedMemEntry 기반)와 도감(MemDexUI, MemData 기반)이 서로 다른 데이터
    /// 타입을 다루지만 정렬 로직(비교 기준, Tier/생산스탯 조회)은 완전히 동일해서 제네릭으로 뽑아 공유한다.
    /// 호출 쪽은 각 원본 항목을 SortableMemItem(memId, 탐험스탯, 원본항목)으로 감싸서 넘기기만 하면 된다.
    /// </summary>
    public static class MemSortHelper
    {
        /// <summary>MemData 목록을 memId 기준 딕셔너리로 만든다(정렬 비교 시 Tier/생산스탯 조회용). memId가 중복되면 먼저 나온 항목을 유지한다.</summary>
        public static Dictionary<string, MemData> BuildMemDataLookup(IEnumerable<MemData> allData)
        {
            var lookup = new Dictionary<string, MemData>();

            foreach (var data in allData)
            {
                if (data != null && !string.IsNullOrEmpty(data.memId) && !lookup.ContainsKey(data.memId))
                {
                    lookup[data.memId] = data;
                }
            }

            return lookup;
        }

        /// <summary>
        /// 정렬 기준에 따라 항목들을 정렬해서 원본 타입(T) 목록으로 반환한다.
        /// MemId는 오름차순, 나머지(Tier/스탯 5종/탐험)는 내림차순. LINQ OrderBy/OrderByDescending은
        /// 안정 정렬(Stable Sort)이라 값이 같은 항목끼리는 원래 순서를 유지한다(여러 번 눌러도 결과가 흔들리지 않음).
        /// </summary>
        public static List<T> SortEntries<T>(List<SortableMemItem<T>> entries, MemSortCriteria criteria, Dictionary<string, MemData> memDataLookup)
        {
            switch (criteria)
            {
                case MemSortCriteria.MemId:
                    return entries.OrderBy(e => e.MemId, StringComparer.Ordinal).Select(e => e.Item).ToList();

                case MemSortCriteria.Tier:
                    return entries.OrderByDescending(e => GetTier(e.MemId, memDataLookup)).Select(e => e.Item).ToList();

                case MemSortCriteria.Crafting:
                    return entries.OrderByDescending(e => GetProductionStat(e.MemId, memDataLookup, ProductionStatType.Crafting)).Select(e => e.Item).ToList();

                case MemSortCriteria.Logging:
                    return entries.OrderByDescending(e => GetProductionStat(e.MemId, memDataLookup, ProductionStatType.Logging)).Select(e => e.Item).ToList();

                case MemSortCriteria.Mining:
                    return entries.OrderByDescending(e => GetProductionStat(e.MemId, memDataLookup, ProductionStatType.Mining)).Select(e => e.Item).ToList();

                case MemSortCriteria.Transport:
                    return entries.OrderByDescending(e => GetProductionStat(e.MemId, memDataLookup, ProductionStatType.Transport)).Select(e => e.Item).ToList();

                case MemSortCriteria.Farming:
                    return entries.OrderByDescending(e => GetProductionStat(e.MemId, memDataLookup, ProductionStatType.Farming)).Select(e => e.Item).ToList();

                case MemSortCriteria.Exploration:
                    return entries.OrderByDescending(e => e.ExplorationStat).Select(e => e.Item).ToList();

                default:
                    return entries.Select(e => e.Item).ToList();
            }
        }

        /// <summary>카탈로그에서 멤의 등급(Tier)을 조회한다. 카탈로그에 없으면 가장 낮은 값으로 취급해 뒤로 보낸다.</summary>
        public static int GetTier(string memId, Dictionary<string, MemData> lookup)
        {
            return lookup.TryGetValue(memId, out var data) ? (int)data.tier : -1;
        }

        /// <summary>카탈로그에서 멤의 생산 스탯 한 종류를 조회한다. 카탈로그에 없으면 가장 낮은 값으로 취급해 뒤로 보낸다.</summary>
        public static int GetProductionStat(string memId, Dictionary<string, MemData> lookup, ProductionStatType type)
        {
            return lookup.TryGetValue(memId, out var data) ? data.productionStats.GetStat(type) : -1;
        }

        /// <summary>티어의 앞글자를 대문자로 반환한다 (Rare->R, Epic->E, Unique->U, Legendary->L, Mythic->M).</summary>
        public static string GetTierLetter(MemTier tier)
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
    }
}
