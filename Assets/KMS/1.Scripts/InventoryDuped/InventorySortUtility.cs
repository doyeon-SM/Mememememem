using System;
using System.Collections.Generic;
using System.Linq;
using HDY.Item;
using UnityEngine;

namespace KMS.InventoryDuped
{
    /// <summary>플레이어 일반 인벤토리에서 사용할 아이템 정렬 기준.</summary>
    public enum InventorySortCriteria
    {
        ItemId,
        Category
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
            var unresolvedStacks = new List<ItemStack>();
            var unresolvedIds = new HashSet<string>();

            foreach (var slot in container.slots)
            {
                if (slot == null || slot.IsEmpty) continue;

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
    }
}
