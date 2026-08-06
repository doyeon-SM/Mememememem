using System;
using System.Collections.Generic;
using System.Linq;
using HDY.Item;
using UnityEngine;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// 플레이어 일반 인벤토리에서 사용할 아이템 정렬 기준.
    ///
    /// [HDY 요청 - 카테고리 우선순위 정렬] ToolPriority/MaterialPriority/FoodPriority는 특정 카테고리
    /// 몇 개를 앞으로 배치하고 나머지는 원래 카테고리(enum 선언) 순서를 그대로 따른다:
    /// - ToolPriority(도구우선): 도구 -> 캡슐 -> 설계도 -> 이후 카테고리순(음식, 재료, 굿즈)
    /// - MaterialPriority(재료우선): 굿즈 -> 재료 -> 이후 카테고리순(음식, 캡슐, 도구, 설계도)
    /// - FoodPriority(음식우선): 음식 -> 이후 카테고리순(재료, 굿즈, 캡슐, 도구, 설계도)
    /// </summary>
    public enum InventorySortCriteria
    {
        ItemId,
        Category,
        ToolPriority,
        MaterialPriority,
        FoodPriority
    }

    /// <summary>
    /// 같은 아이템 스택을 합쳐 MaxStack 단위로 다시 나눈 뒤 정렬하는 공용 유틸리티.
    /// 퀵슬롯 여부는 호출자가 결정하며, 이 클래스는 전달받은 컨테이너만 변경한다.
    /// </summary>
    public static class InventorySortUtility
    {
        public static bool SortAndCompact(
            InventoryContainer container,
            InventorySortCriteria criteria,
            ItemCatalogManager catalogManager)
        {
            if (container == null || container.slots == null) return false;
            if (catalogManager == null)
            {
                Debug.LogWarning("[InventorySortUtility] Sort cancelled because ItemCatalogManager is unavailable.");
                return false;
            }

            var totals = new Dictionary<string, long>();
            var sortTotals = new Dictionary<string, long>();
            var unresolvedStacks = new List<ItemStack>();
            var unresolvedIds = new HashSet<string>();

            foreach (var slot in container.slots)
            {
                if (slot == null || slot.IsEmpty) continue;

                sortTotals.TryGetValue(slot.itemId, out long sortTotal);
                sortTotals[slot.itemId] = sortTotal + slot.amount;

                ItemData itemData = catalogManager.FindItemData(slot.itemId);
                if (itemData == null)
                {
                    unresolvedStacks.Add(new ItemStack { itemId = slot.itemId, amount = slot.amount });
                    unresolvedIds.Add(slot.itemId);
                    continue;
                }

                totals.TryGetValue(slot.itemId, out long current);
                totals[slot.itemId] = current + slot.amount;
            }

            var compacted = new List<ItemStack>();
            foreach (var pair in totals)
            {
                long remaining = pair.Value;
                int maxStack = GetMaxStack(pair.Key, catalogManager);

                while (remaining > 0)
                {
                    int amount = (int)Math.Min((long)maxStack, remaining);
                    compacted.Add(new ItemStack { itemId = pair.Key, amount = amount });
                    remaining -= amount;
                }
            }

            compacted.AddRange(unresolvedStacks);

            List<ItemStack> sorted;
            switch (criteria)
            {
                case InventorySortCriteria.ItemId:
                    sorted = compacted
                        .OrderBy(stack => stack.itemId, StringComparer.Ordinal)
                        .ThenByDescending(stack => stack.amount)
                        .ToList();
                    break;

                case InventorySortCriteria.Category:
                    sorted = compacted
                        .OrderBy(stack => GetCategoryOrder(stack.itemId, catalogManager))
                        .ThenByDescending(stack => sortTotals[stack.itemId])
                        .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
                        .ThenByDescending(stack => stack.amount)
                        .ToList();
                    break;

                // [HDY 요청 - 카테고리 우선순위 정렬] 특정 카테고리 몇 개만 앞으로 배치하고 나머지는
                // 원래 카테고리 순서를 그대로 따르는 정렬 3종. 2차 정렬 기준은 위 Category 케이스와 동일.
                case InventorySortCriteria.ToolPriority:
                case InventorySortCriteria.MaterialPriority:
                case InventorySortCriteria.FoodPriority:
                    sorted = compacted
                        .OrderBy(stack => GetPriorityOrder(stack.itemId, catalogManager, criteria))
                        .ThenByDescending(stack => sortTotals[stack.itemId])
                        .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
                        .ThenByDescending(stack => stack.amount)
                        .ToList();
                    break;

                default:
                    sorted = compacted;
                    break;
            }

            if (sorted.Count > container.slots.Length)
            {
                Debug.LogWarning(
                    $"[InventorySortUtility] Sort cancelled because {sorted.Count} slots are required " +
                    $"but the inventory only has {container.slots.Length}. No items were changed.");
                return false;
            }

            if (unresolvedIds.Count > 0)
            {
                Debug.LogWarning(
                    $"[InventorySortUtility] Preserved unresolved item IDs without compacting: " +
                    $"{string.Join(", ", unresolvedIds)}");
            }

            for (int i = 0; i < container.slots.Length; i++)
            {
                if (container.slots[i] == null)
                {
                    container.slots[i] = new ItemStack();
                }

                if (i < sorted.Count)
                {
                    container.slots[i].Set(sorted[i].itemId, sorted[i].amount);
                }
                else
                {
                    container.slots[i].Clear();
                }
            }

            return true;
        }

        private static int GetMaxStack(string itemId, ItemCatalogManager catalogManager)
        {
            ItemData data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            return data != null ? Mathf.Max(1, data.MaxStack) : 1;
        }

        private static int GetCategoryOrder(string itemId, ItemCatalogManager catalogManager)
        {
            ItemData data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            return data != null ? (int)data.Category : int.MaxValue;
        }

        /// <summary>
        /// [HDY 요청 - 카테고리 우선순위 정렬] criteria에 맞는 카테고리 순위표로 itemId의 카테고리를
        /// 매핑한다. 카탈로그에서 못 찾으면 가장 낮은 우선순위(맨 뒤)로 취급.
        /// </summary>
        private static int GetPriorityOrder(string itemId, ItemCatalogManager catalogManager, InventorySortCriteria criteria)
        {
            ItemData data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            if (data == null) return int.MaxValue;

            switch (criteria)
            {
                case InventorySortCriteria.ToolPriority: return GetToolPriorityOrder(data.Category);
                case InventorySortCriteria.MaterialPriority: return GetMaterialPriorityOrder(data.Category);
                case InventorySortCriteria.FoodPriority: return GetFoodPriorityOrder(data.Category);
                default: return int.MaxValue;
            }
        }

        /// <summary>도구우선: 도구 -> 캡슐 -> 설계도 -> 이후 카테고리순(음식, 재료, 굿즈).</summary>
        private static int GetToolPriorityOrder(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Tool: return 0;
                case ItemCategory.Capsule: return 1;
                case ItemCategory.BluePrint: return 2;
                case ItemCategory.Food: return 3;
                case ItemCategory.Material: return 4;
                case ItemCategory.Goods: return 5;
                default: return int.MaxValue;
            }
        }

        /// <summary>재료우선: 굿즈 -> 재료 -> 이후 카테고리순(음식, 캡슐, 도구, 설계도).</summary>
        private static int GetMaterialPriorityOrder(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Goods: return 0;
                case ItemCategory.Material: return 1;
                case ItemCategory.Food: return 2;
                case ItemCategory.Capsule: return 3;
                case ItemCategory.Tool: return 4;
                case ItemCategory.BluePrint: return 5;
                default: return int.MaxValue;
            }
        }

        /// <summary>음식우선: 음식 -> 이후 카테고리순(재료, 굿즈, 캡슐, 도구, 설계도).</summary>
        private static int GetFoodPriorityOrder(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Food: return 0;
                case ItemCategory.Material: return 1;
                case ItemCategory.Goods: return 2;
                case ItemCategory.Capsule: return 3;
                case ItemCategory.Tool: return 4;
                case ItemCategory.BluePrint: return 5;
                default: return int.MaxValue;
            }
        }
    }
}
