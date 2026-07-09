using System;
using HDY.Item;
using UnityEngine;

namespace KMS.InventoryDuped
{

/// <summary>
/// [HDY 요청] 슬롯 하나의 데이터. ItemData(SO 레퍼런스)를 직접 들고 있지 않고 Item_ID(string)만 저장한다.
/// 아이콘/이름 등 실제 표시가 필요한 쪽(InventorySlotUI/ItemDragUI/ItemTooltipUI)은 itemId로
/// ItemCatalogManager.FindItemData를 호출해서 그때그때 조회한다.
/// </summary>
[Serializable]
public class ItemStack
{
    public string itemId;
    public int amount;

    public bool IsEmpty => string.IsNullOrEmpty(itemId) || amount <= 0;

    public void Set(string newItemId, int newAmount)
    {
        itemId = newItemId;
        amount = newAmount;
    }

    public void Clear()
    {
        itemId = null;
        amount = 0;
    }
}

[Serializable]
public class InventoryContainer
{
    public int width;
    public int height;
    public ItemStack[] slots;

    public void Initialize()
    {
        int slotCount = width * height;

        if (slots == null || slots.Length != slotCount)
        {
            slots = new ItemStack[slotCount];
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = new ItemStack();
            }

            if (slots[i].IsEmpty)
            {
                slots[i].Clear();
            }
        }
    }

    public bool IsValidIndex(int index)
    {
        return slots != null && index >= 0 && index < slots.Length;
    }
}

/// <summary>퀵슬롯 사용 중 임시 예약 상태. itemId(string)로 어떤 아이템을 예약했는지 기억한다.</summary>
public class QuickSlotUseReservation
{
    public int slotIndex;
    public string itemId;
    public int reservedAmount;
    public bool committed;
}

public class PlayerInventory : MonoBehaviour
{
    // [HDY 요청] 창고 UI 설계에 맞춰 인벤토리 기본 크기를 10x6으로 통일(퀵슬롯 10칸은 기존과 동일).
    public InventoryContainer inventory = new InventoryContainer { width = 10, height = 6 };
    public InventoryContainer quickSlots = new InventoryContainer { width = 10, height = 1 };

    public int selectedQuickSlotIndex;
    private QuickSlotUseReservation quickSlotUseReservation;
    private int pendingQuickSlotIndex = -1;

    // [HDY 요청] Item_ID 문자열만으로 아이템을 지급할 수 있도록 카탈로그 조회 경로를 추가하기 위한 참조.
    // MergeStack(공용 헬퍼)에서 MaxStack 조회 시에도 사용된다.
    [Header("아이템 카탈로그 (Item_ID로 조회할 때 사용)")]
    [SerializeField] private ItemCatalogManager catalogManager;

    public event Action OnInventoryChanged;
    public event Action<ItemData,int> OnItemObtained;
    public event Action<int> OnQuickSlotChanged;
    public event Action<int> OnSelectedQuickSlotChanged;

    private void Awake()
    {
        inventory.Initialize();
        quickSlots.Initialize();

        catalogManager = ItemCatalogManager.Resolve(catalogManager);
    }

    // 아이템 추가
    public int AddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return amount;

        int remaining = amount;

        // 퀵슬롯에 아이템이 있으면 추가 없으면 인벤토리에 아이템이 있으면 추가 없으면 인벤토리에 빈슬롯에 추가 빈슬롯없으면 퀵슬롯 빈슬롯에 추가
        remaining = AddToExistingStacks(quickSlots, item, remaining);
        remaining = AddToExistingStacks(inventory, item, remaining);
        remaining = AddToEmptySlots(inventory, item, remaining);
        remaining = AddToEmptySlots(quickSlots, item, remaining);

        int addedAmount = amount - remaining;

        if (addedAmount > 0)
        {
            OnInventoryChanged?.Invoke();
            OnItemObtained?.Invoke(item, addedAmount);
            NotifyAllQuickSlotsChanged();
        }

        return remaining;
    }

    /// <summary>
    /// [HDY 요청] Item_ID 문자열로 아이템을 추가한다. ItemCatalogManager에서 실제 ItemData를 찾아
    /// 기존 AddItem(ItemData, int)에 그대로 위임한다. 카탈로그에 없는 ID면 아무 것도 추가하지 않고
    /// amount를 그대로 반환한다(경고 로그).
    /// </summary>
    public int AddItem(string itemId, int amount)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return amount;

        var itemData = catalogManager != null ? catalogManager.FindItemData(itemId) : null;

        if (itemData == null)
        {
            Debug.LogWarning($"[PlayerInventory] Item_ID '{itemId}'에 해당하는 ItemData를 카탈로그에서 찾을 수 없습니다.");
            return amount;
        }

        return AddItem(itemData, amount);
    }

    // 인벤토리 내에서 아이템 이동
    public bool MoveInventorySlot(int fromIndex, int toIndex)
    {
        bool moved = MoveSlot(inventory, fromIndex, inventory, toIndex);

        if (moved) OnInventoryChanged?.Invoke();

        return moved;
    }

    // 퀵슬롯 내에서 아이템 이동
    public bool MoveQuickSlot(int fromIndex, int toIndex)
    {
        bool moved = MoveSlot(quickSlots, fromIndex, quickSlots, toIndex);

        if (moved)
        {
            OnQuickSlotChanged?.Invoke(fromIndex);
            OnQuickSlotChanged?.Invoke(toIndex);
            NotifySelectedQuickSlotIfChanged(fromIndex, toIndex);
        }

        return moved;
    }

    // 인벤토리에서 퀵슬롯으로 아이템 이동
    public bool MoveInventoryToQuickSlot(int inventoryIndex, int quickSlotIndex)
    {
        bool moved = MoveSlot(inventory, inventoryIndex, quickSlots, quickSlotIndex);

        if (moved)
        {
            OnInventoryChanged?.Invoke();
            OnQuickSlotChanged?.Invoke(quickSlotIndex);
            NotifySelectedQuickSlotIfChanged(quickSlotIndex);
        }

        return moved;
    }

    // 퀵슬롯에서 인벤토리로 아이템 이동
    public bool MoveQuickSlotToInventory(int quickSlotIndex, int inventoryIndex)
    {
        bool moved = MoveSlot(quickSlots, quickSlotIndex, inventory, inventoryIndex);

        if (moved)
        {
            OnQuickSlotChanged?.Invoke(quickSlotIndex);
            OnInventoryChanged?.Invoke();
            NotifySelectedQuickSlotIfChanged(quickSlotIndex);
        }

        return moved;
    }

    // 퀵슬롯 아이템 선택. 사용중이면 마지막 입력 기록
    public void SelectQuickSlot(int index)
    {
        if (!quickSlots.IsValidIndex(index)) return;

        if (quickSlotUseReservation != null)
        {
            pendingQuickSlotIndex = index;
            return;
        }

        ApplyQuickSlotSelection(index);
    }

    // 슬롯 변경 및 이벤트 호출
    private void ApplyQuickSlotSelection(int index)
    {
        selectedQuickSlotIndex = index;
        OnSelectedQuickSlotChanged?.Invoke(index);
    }

    // 퀵슬롯 아이템 확인
    public ItemStack GetSelectedQuickSlot()
    {
        if (!quickSlots.IsValidIndex(selectedQuickSlotIndex)) return null;

        return quickSlots.slots[selectedQuickSlotIndex];
    }

    // 퀵슬롯을 사용중 상태로 잠금, 슬롯 번호와 아이템 정보 기록
    public bool BeginQuickSlotUse()
    {
        if (quickSlotUseReservation != null) return false;

        ItemStack slot = GetSelectedQuickSlot();

        if (slot == null) return false;

        string useItemId = null;

        if (slot.IsEmpty)
        {
            slot.Clear();
        }
        else
        {
            useItemId = slot.itemId;
        }

        quickSlotUseReservation = new QuickSlotUseReservation
        {
            slotIndex = selectedQuickSlotIndex,
            itemId = useItemId
        };

        pendingQuickSlotIndex = -1;

        return true;
    }

    // 사용중인 아이템을 수량만큼 임시 차감 및 기록 (실패시 되돌리기 위해서)
    public bool TryReserveQuickSlotItem(int amount)
    {
        if (quickSlotUseReservation == null || amount <= 0) return false;
        if (quickSlotUseReservation.committed) return false;
        if (quickSlotUseReservation.reservedAmount > 0) return false;

        ItemStack slot = quickSlots.slots[quickSlotUseReservation.slotIndex];

        if (slot.IsEmpty || slot.itemId != quickSlotUseReservation.itemId) return false;
        if (slot.amount < amount) return false;

        slot.amount -= amount;
        quickSlotUseReservation.reservedAmount = amount;

        if (slot.amount <= 0) slot.Clear();

        return true;
    }

    // 임시 차감을 확정
    public bool CommitQuickSlotUse()
    {
        if (quickSlotUseReservation == null) return false;

        quickSlotUseReservation.committed = true;

        return true;
    }

    // 임시 차감을 되돌림
    public bool RollbackQuickSlotUse()
    {
        if (quickSlotUseReservation == null) return false;
        if (quickSlotUseReservation.committed) return false;

        int amount = quickSlotUseReservation.reservedAmount;

        if (amount <= 0) return true;

        ItemStack slot = quickSlots.slots[quickSlotUseReservation.slotIndex];

        if (!slot.IsEmpty && slot.itemId != quickSlotUseReservation.itemId) return false;

        if (slot.IsEmpty) slot.Set(quickSlotUseReservation.itemId, amount);
        else slot.amount += amount;

        quickSlotUseReservation.reservedAmount = 0;

        return true;
    }

    // 사용 상태를 종료.
    public void EndQuickSlotUse()
    {
        if (quickSlotUseReservation == null) return;
        if (!quickSlotUseReservation.committed)
        {
            RollbackQuickSlotUse();
        }

        int usedSlotIndex = quickSlotUseReservation.slotIndex;

        quickSlotUseReservation = null;

        OnQuickSlotChanged?.Invoke(usedSlotIndex);

        int pendingIndex = pendingQuickSlotIndex;
        pendingQuickSlotIndex = -1;

        if (quickSlots.IsValidIndex(pendingIndex)) ApplyQuickSlotSelection(pendingIndex);
        else OnSelectedQuickSlotChanged?.Invoke(selectedQuickSlotIndex);
    }

    // 해당 퀵슬롯이 사용중이라 잠겨있는지 확인
    public bool IsQuickSlotLocked(int index)
    {
        return quickSlotUseReservation != null && quickSlotUseReservation.slotIndex == index;
    }

    /// <summary>현재 사용중인 아이템의 Item_ID를 반환한다(효과 목록 조회 등에 사용).</summary>
    public string GetQuickSlotUseItemId()
    {
        return quickSlotUseReservation != null ? quickSlotUseReservation.itemId : null;
    }

    // 퀵슬롯 아이템 사용
    public bool ConsumeQuickSlot(int quickSlotIndex, int amount)
    {
        if (!quickSlots.IsValidIndex(quickSlotIndex)) return false;
        if (IsQuickSlotLocked(quickSlotIndex)) return false;

        ItemStack slot = quickSlots.slots[quickSlotIndex];

        if (slot.IsEmpty || amount <= 0 || slot.amount < amount) return false;

        slot.amount -= amount;

        if (slot.amount <= 0) slot.Clear();

        OnQuickSlotChanged?.Invoke(quickSlotIndex);
        NotifySelectedQuickSlotIfChanged(quickSlotIndex);

        return true;
    }

    // ID가 일치하는 아이템의 전체 수량 확인 (인벤토리 + 퀵슬롯)
    public int GetItemAmount(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return 0;

        return GetItemAmount(inventory, itemId, false) + GetItemAmount(quickSlots, itemId, false);
    }

    // ID가 일치하는 아이템을 지정한 수량만큼 제거
    public bool RemoveItem(string itemId, int amount)
    {
        // 해당 아이템이 존재하지 않거나 지정한 수량보다 적으면 false 리턴
        if (string.IsNullOrEmpty(itemId) || amount <= 0) return false;
        
        int removableAmount = GetItemAmount(inventory, itemId, false) + GetItemAmount(quickSlots, itemId, true);

        if (removableAmount < amount) return false;
        
        int remaining = amount;

        // 인벤토리 안에 있는 것 부터 제거 제거수량이 남으면 퀵슬롯에 등록된것 제거
        bool inventoryChanged = RemoveItem(inventory, itemId, ref remaining);
        bool quickSlotChanged = remaining > 0 && RemoveItem(quickSlots, itemId, ref remaining);

        if (inventoryChanged) OnInventoryChanged?.Invoke();
        if (quickSlotChanged) NotifyAllQuickSlotsChanged();

        return true;
    }

    // 아이템 수량 확인 (컨테이너 하나만) (오버로드)
    private int GetItemAmount(InventoryContainer container, string itemId, bool skipLockedQuickSlot)
    {
        int totalAmount = 0;

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (skipLockedQuickSlot && IsLockedQuickSlot(container, i)) continue;

            ItemStack slot = container.slots[i];

            if (slot.IsEmpty || slot.itemId != itemId) continue;

            totalAmount += slot.amount;
        }

        return totalAmount;
    }

    // 아이템 제거
    private bool RemoveItem(InventoryContainer container, string itemId, ref int remaining)
    {
        bool changed = false;

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (IsLockedQuickSlot(container, i)) continue;

            ItemStack slot = container.slots[i];

            if (slot.IsEmpty || slot.itemId != itemId) continue;

            int removed = Mathf.Min(slot.amount, remaining);

            slot.amount -= removed;
            remaining -= removed;
            changed = true;

            if (slot.amount <= 0) slot.Clear();
            if (remaining <= 0) break;
        }

        return changed;
    }

    // 있는 아이템 개수 추가
    private int AddToExistingStacks(InventoryContainer container, ItemData item, int amount)
    {
        int remaining = amount;

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (IsLockedQuickSlot(container, i)) continue;

            ItemStack slot = container.slots[i];

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

    // 빈슬롯에 아이템 추가
    private int AddToEmptySlots(InventoryContainer container, ItemData item, int amount)
    {
        int remaining = amount;
        int maxStack = Mathf.Max(1, item.MaxStack);

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (IsLockedQuickSlot(container, i)) continue;

            ItemStack slot = container.slots[i];

            if (!slot.IsEmpty) continue;

            int added = Mathf.Min(maxStack, remaining);
            slot.Set(item.Item_ID, added);
            remaining -= added;

            if (remaining <= 0) break;
        }

        return remaining;
    }

    // 잠긴 퀵슬롯 인덱스인지 확인
    private bool IsLockedQuickSlot(InventoryContainer container, int index)
    {
        return container == quickSlots && IsQuickSlotLocked(index);
    }

    /// <summary>
    /// [HDY 요청] 슬롯 이동/병합의 실제 규칙은 InventorySlotMoveHelper(공용)에 위임한다 - WarehouseUI의
    /// 창고↔인벤토리 이동도 완전히 동일한 규칙을 써야 해서 로직을 한 곳으로 모았다. 여기서는 잠긴 퀵슬롯
    /// 여부만 미리 걸러낸다(이 규칙은 PlayerInventory에만 있는 개념이라 공용 헬퍼가 알 필요 없음).
    /// </summary>
    private bool MoveSlot(InventoryContainer fromContainer, int fromIndex, InventoryContainer toContainer, int toIndex)
    {
        if (IsLockedQuickSlot(fromContainer, fromIndex)) return false;
        if (IsLockedQuickSlot(toContainer, toIndex)) return false;

        return InventorySlotMoveHelper.MoveSlot(fromContainer, fromIndex, toContainer, toIndex, catalogManager);
    }

    // 모든 퀵슬롯 변화 알림
    private void NotifyAllQuickSlotsChanged()
    {
        for (int i = 0; i < quickSlots.slots.Length; i++)
        {
            OnQuickSlotChanged?.Invoke(i);
        }

        if (quickSlotUseReservation == null)
        {
            OnSelectedQuickSlotChanged?.Invoke(selectedQuickSlotIndex);
        }
    }

    // 변경된 퀵슬롯 변화 알림
    private void NotifySelectedQuickSlotIfChanged(params int[] changedIndices)
    {
        if (quickSlotUseReservation != null) return;

        for (int i = 0; i < changedIndices.Length; i++)
        {
            if (changedIndices[i] == selectedQuickSlotIndex)
            {
                OnSelectedQuickSlotChanged?.Invoke(selectedQuickSlotIndex);
                return;
            }
        }
    }
}

}
