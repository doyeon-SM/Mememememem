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
    /// 공용 로직)를 그대로 가져다 쓴다. 새로 만든 건 창고 전용 정렬(ApplySort)과 행 추가(AddRow)뿐이다.
    ///
    /// [정렬] 같은 Item_ID 스택이 여러 칸에 나뉘어 있으면(예: MaxStack 99인데 99+51로 흩어짐) 먼저 합쳐서
    /// MaxStack 기준으로 다시 압축한 뒤(99, 51 형태로), 기준(Item_ID 또는 카테고리)으로 정렬하고 동순위는
    /// 수량이 많은 순으로 배치한다.
    ///
    /// [행 추가 (업그레이드)] KMS.InventoryContainer.Initialize()는 배열 크기가 바뀌면 통째로 새 배열을
    /// 만들어서(기존 아이템 손실) "시작할 때 한 번만 크기를 정하는" 용도로만 설계되어 있다. 창고는 런타임에
    /// 행이 늘어나야 하고 기존 아이템은 그대로 유지되어야 하므로, InventoryContainer(KMS 파일)를 건드리는 대신
    /// public 필드(width/height/slots)만 갖고 이 클래스 안에서 직접 배열을 늘려서 기존 값을 복사한다(AddRow).
    /// </summary>
    public class WarehouseInventory : MonoBehaviour
    {
        [Header("창고 그리드 (10 x n - n(세로 칸 수)은 업그레이드로 늘어나는 행 수)")]
        public InventoryContainer storage = new InventoryContainer { width = 10, height = 2 };

        [Header("행 추가 (업그레이드로 늘어날 수 있는 시작 행 수)")]
        [Tooltip("업그레이드 없이 시작할 때 기본으로 사용 가능한 행(세로 칸) 수")]
        [SerializeField] private int startingRows = 2;

        [Header("아이템 카탈로그 (Item_ID로 조회할 때 사용)")]
        [SerializeField] private ItemCatalogManager catalogManager;

        /// <summary>업그레이드 없이 시작할 때 기본 행 수(현재 몇 번째 업그레이드 단계인지 계산할 때 기준으로 쓰임).</summary>
        public int StartingRows => startingRows;

        /// <summary>현재 창고의 행(세로 칸) 수.</summary>
        public int RowCount => storage.height;

        /// <summary>창고 내용이 바뀔 때마다(추가/제거/이동/정렬) 발행. UI(WarehouseUI)가 구독해서 갱신한다.</summary>
        public event Action OnStorageChanged;

        /// <summary>행 수(창고 크기)가 바뀔 때(업그레이드 성공 시) 발행. UI가 슬롯 UI를 추가로 만들어야 할 때 사용.</summary>
        public event Action OnRowCountChanged;

        private void Awake()
        {
            if (storage.height <= 0) storage.height = startingRows;
            if (storage.width <= 0) storage.width = 10;

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

        /// <summary>
        /// [HDY 요청 아님 - 탐험 시스템용 신규 추가] 여러 아이템(ItemData, 수량) 묶음 전부가 실제로 창고에
        /// 들어갈 수 있는지 사전 확인한다(실제로 추가하지 않는다). AddItem을 순서대로 호출했을 때와 동일하게
        /// (기존 스택 우선 채움 -> 빈 슬롯 사용) 시뮬레이션하며, 여러 아이템이 같은 빈 슬롯을 나눠 쓰는 경쟁까지
        /// 반영한다(항목별로 따로 HasSpaceFor를 부르면 이 경쟁을 놓쳐서 과대평가할 수 있다).
        /// 탐험 보상처럼 "전부 들어갈 수 있을 때만 일괄 지급"하는 상황(ExplorationRuntime.TryComplete)에서 사용한다.
        /// </summary>
        public bool CanFitAll(IReadOnlyList<(ItemData item, int amount)> requests)
        {
            if (requests == null || requests.Count == 0) return true;

            var existingAmounts = new Dictionary<string, int>();
            int freeEmptySlots = 0;

            foreach (var slot in storage.slots)
            {
                if (slot.IsEmpty) { freeEmptySlots++; continue; }
                existingAmounts.TryGetValue(slot.itemId, out int cur);
                existingAmounts[slot.itemId] = cur + slot.amount;
            }

            foreach (var (item, amount) in requests)
            {
                if (item == null || amount <= 0) continue;

                int maxStack = Mathf.Max(1, item.MaxStack);
                int remaining = amount;

                existingAmounts.TryGetValue(item.Item_ID, out int currentTotal);
                int usedStacks = currentTotal > 0 ? Mathf.CeilToInt(currentTotal / (float)maxStack) : 0;
                int lastStackSpace = usedStacks > 0 ? (usedStacks * maxStack - currentTotal) : 0;

                int fittedIntoExisting = Mathf.Min(lastStackSpace, remaining);
                remaining -= fittedIntoExisting;
                currentTotal += fittedIntoExisting;

                if (remaining > 0)
                {
                    int neededSlots = Mathf.CeilToInt(remaining / (float)maxStack);
                    if (neededSlots > freeEmptySlots) return false;

                    freeEmptySlots -= neededSlots;
                    currentTotal += remaining;
                }

                existingAmounts[item.Item_ID] = currentTotal;
            }

            return true;
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
        /// 창고에 한 줄(가로 폭만큼, 10칸)을 추가한다. 기존 아이템 배치는 그대로 유지된다.
        /// 비용 확인/차감은 이 클래스의 책임이 아니다 - WarehouseUpgrade(IUpgradable 구현체)가 계산하고,
        /// 공용 업그레이드 팝업(UpgradePopupUI)이 비용을 다 낸 뒤에만 이 메서드를 호출해준다.
        ///
        /// [KMS 파일 미수정] InventoryContainer.Initialize()는 배열이 바뀌면 통째로 새로 만들어서 기존 데이터를
        /// 잃어버리므로, 여기서는 그 메서드를 쓰지 않고 public 필드만으로 직접 배열을 늘려 기존 값을 복사한다.
        /// </summary>
        public void AddRow()
        {
            int oldLength = storage.slots != null ? storage.slots.Length : 0;

            storage.height += 1;
            int newLength = storage.width * storage.height;

            var newSlots = new KMS.InventoryDuped.ItemStack[newLength];

            for (int i = 0; i < newLength; i++)
            {
                newSlots[i] = (i < oldLength && storage.slots[i] != null) ? storage.slots[i] : new KMS.InventoryDuped.ItemStack();
            }

            storage.slots = newSlots;

            Debug.Log($"[WarehouseInventory] 창고 행 추가: 현재 {storage.height}줄 ({storage.slots.Length}칸)");

            OnRowCountChanged?.Invoke();
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

        public void PublishWarehouseChanged()
        {
            OnStorageChanged?.Invoke();
        }
    }

}
