using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HDY.Item;
using KMS.InventoryDuped;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고(Warehouse) 데이터. KMS.PlayerInventory와 비슷한 구조(InventoryContainer + ItemStack 재사용)로,
    /// 단일 컨테이너만 다루는 더 단순한 버전이다 - 퀵슬롯이나 "사용 중 임시 예약" 같은 플레이어 전용 개념은 없다.
    ///
    /// [재사용] ItemStack/InventoryContainer(KMS.InventoryDuped)와 InventorySlotMoveHelper(슬롯 이동/병합
    /// 공용 로직)를 그대로 가져다 쓴다. 새로 만든 건 창고 전용 정렬(ApplySort)뿐이다.
    ///
    /// [정렬] 같은 Item_ID 스택이 여러 칸에 나뉘어 있으면(예: MaxStack 99인데 99+51로 흩어짐) 먼저 합쳐서
    /// MaxStack 기준으로 다시 압축한 뒤(99, 51 형태로), 기준(Item_ID 또는 카테고리)으로 정렬하고 동순위는
    /// 수량이 많은 순으로 배치한다.
    /// </summary>
    public class WarehouseInventory : MonoBehaviour
    {
        [Header("창고 그리드 (10 x n - n(세로 칸 수)은 스크롤로 확장되는 행 수, 인스펙터에서 조정 가능)")]
        public InventoryContainer storage = new InventoryContainer { width = 10, height = 10 };

        [Header("아이템 카탈로그 (Item_ID로 조회할 때 사용)")]
        [SerializeField] private ItemCatalogManager catalogManager;

        /// <summary>창고 내용이 바뀔 때마다(추가/제거/이동/정렬) 발행. UI(WarehouseUI)가 구독해서 갱신한다.</summary>
        public event Action OnStorageChanged;

        private void Awake()
        {
            storage.Initialize();
            catalogManager = ItemCatalogManager.Resolve(catalogManager);
        }

        // 아이템 추가
        public int AddItem(ItemData item, int amount)
        {
            if (item == null || amount <= 0) return amount;

            int remaining = amount;
            remaining = AddToExistingStacks(item, remaining);
            remaining = AddToEmptySlots(item, remaining);

            int added = amount - remaining;
            if (added > 0) OnStorageChanged?.Invoke();

            return remaining;
        }

        /// <summary>Item_ID로 아이템을 추가한다. 카탈로그에서 찾아 AddItem(ItemData, int)에 위임한다.</summary>
        public int AddItem(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return amount;

            var itemData = catalogManager != null ? catalogManager.FindItemData(itemId) : null;

            if (itemData == null)
            {
                Debug.LogWarning($"[WarehouseInventory] Item_ID '{itemId}'에 해당하는 ItemData를 카탈로그에서 찾을 수 없습니다.");
                return amount;
            }

            return AddItem(itemData, amount);
        }

        /// <summary>ID가 일치하는 아이템의 전체 수량을 확인한다.</summary>
        public int GetItemAmount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;

            int total = 0;
            foreach (var slot in storage.slots)
            {
                if (slot.IsEmpty || slot.itemId != itemId) continue;
                total += slot.amount;
            }

            return total;
        }

        /// <summary>ID가 일치하는 아이템을 지정한 수량만큼 제거한다.</summary>
        public bool RemoveItem(string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
            if (GetItemAmount(itemId) < amount) return false;

            int remaining = amount;

            foreach (var slot in storage.slots)
            {
                if (slot.IsEmpty || slot.itemId != itemId) continue;

                int removed = Mathf.Min(slot.amount, remaining);
                slot.amount -= removed;
                remaining -= removed;

                if (slot.amount <= 0) slot.Clear();
                if (remaining <= 0) break;
            }

            OnStorageChanged?.Invoke();
            return true;
        }

        /// <summary>창고 내에서 슬롯 하나를 옮긴다(자리 교환/병합). 공용 헬퍼(InventorySlotMoveHelper)에 위임.</summary>
        public bool MoveSlot(int fromIndex, int toIndex)
        {
            bool moved = InventorySlotMoveHelper.MoveSlot(storage, fromIndex, storage, toIndex, catalogManager);

            if (moved) OnStorageChanged?.Invoke();

            return moved;
        }

        /// <summary>
        /// 창고를 정렬한다. 1) 같은 Item_ID 스택을 전부 합산한 뒤 MaxStack 기준으로 다시 압축(가능한 한 꽉 채운
        /// 스택 + 나머지 하나)하고, 2) 기준(Item_ID/카테고리)으로 정렬하며 동순위는 수량 내림차순으로 배치한다.
        /// </summary>
        public void ApplySort(ItemSortCriteria criteria)
        {
            var compacted = BuildCompactedStacks();
            var sorted = SortCompactedStacks(compacted, criteria);

            for (int i = 0; i < storage.slots.Length; i++)
            {
                if (i < sorted.Count)
                {
                    storage.slots[i].Set(sorted[i].itemId, sorted[i].amount);
                }
                else
                {
                    storage.slots[i].Clear();
                }
            }

            Debug.Log($"[WarehouseInventory] 정렬 완료: 기준={criteria}, 압축 후 스택 {sorted.Count}개 / 전체 {storage.slots.Length}칸");

            OnStorageChanged?.Invoke();
        }

        /// <summary>같은 Item_ID를 모두 합산한 뒤, MaxStack 기준으로 [꽉 찬 스택, ..., 나머지] 형태로 다시 나눈다.</summary>
        private List<ItemStack> BuildCompactedStacks()
        {
            var totals = new Dictionary<string, int>();

            foreach (var slot in storage.slots)
            {
                if (slot.IsEmpty) continue;

                totals.TryGetValue(slot.itemId, out int current);
                totals[slot.itemId] = current + slot.amount;
            }

            var compacted = new List<ItemStack>();

            foreach (var kvp in totals)
            {
                int remaining = kvp.Value;
                int maxStack = GetMaxStack(kvp.Key);

                while (remaining > 0)
                {
                    int take = Mathf.Min(maxStack, remaining);
                    compacted.Add(new ItemStack { itemId = kvp.Key, amount = take });
                    remaining -= take;
                }
            }

            return compacted;
        }

        /// <summary>기준에 따라 압축된 스택 목록을 정렬한다. 동순위는 수량 내림차순.</summary>
        private List<ItemStack> SortCompactedStacks(List<ItemStack> stacks, ItemSortCriteria criteria)
        {
            switch (criteria)
            {
                case ItemSortCriteria.ItemId:
                    return stacks
                        .OrderBy(s => s.itemId, StringComparer.Ordinal)
                        .ThenByDescending(s => s.amount)
                        .ToList();

                case ItemSortCriteria.Category:
                    return stacks
                        .OrderBy(s => GetCategoryOrder(s.itemId))
                        .ThenByDescending(s => s.amount)
                        .ToList();

                default:
                    return stacks;
            }
        }

        /// <summary>카탈로그에서 MaxStack을 조회한다. 못 찾으면 안전하게 1(스택 불가)로 취급.</summary>
        private int GetMaxStack(string itemId)
        {
            var data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            return data != null ? Mathf.Max(1, data.MaxStack) : 1;
        }

        /// <summary>카탈로그에서 카테고리를 조회한다. 못 찾으면 가장 낮은 우선순위(맨 뒤)로 취급.</summary>
        private int GetCategoryOrder(string itemId)
        {
            var data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            return data != null ? (int)data.Category : int.MaxValue;
        }

        private int AddToExistingStacks(ItemData item, int amount)
        {
            int remaining = amount;

            foreach (var slot in storage.slots)
            {
                if (slot.IsEmpty || slot.itemId != item.Item_ID) continue;

                int maxStack = Mathf.Max(1, item.MaxStack);
                int space = maxStack - slot.amount;

                if (space <= 0) continue;

                int added = Mathf.Min(space, remaining);
                slot.amount += added;
                remaining -= added;

                if (remaining <= 0) break;
            }

            return remaining;
        }

        private int AddToEmptySlots(ItemData item, int amount)
        {
            int remaining = amount;
            int maxStack = Mathf.Max(1, item.MaxStack);

            foreach (var slot in storage.slots)
            {
                if (!slot.IsEmpty) continue;

                int added = Mathf.Min(maxStack, remaining);
                slot.Set(item.Item_ID, added);
                remaining -= added;

                if (remaining <= 0) break;
            }

            return remaining;
        }
    }
}
