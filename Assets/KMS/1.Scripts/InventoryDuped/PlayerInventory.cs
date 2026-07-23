using System;
using HDY.Item;
using UnityEngine;
using KMS.Persistence;

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
    public event Action<int> OnQuickSlotSelectionRequested;

    private void Awake()
    {
        inventory.Initialize();
        quickSlots.Initialize();

        catalogManager = ItemCatalogManager.Resolve(catalogManager);
    }

    private void Start()
    {
        PlayerPersistenceManager.EnsureInstance().RegisterPlayer(this, GetComponent<KMS.PlayerStats>());
    }

    public PlayerInventorySaveData CaptureSaveData()
    {
        return new PlayerInventorySaveData
        {
            inventory = InventoryContainerSaveData.Capture(inventory),
            quickSlots = InventoryContainerSaveData.Capture(quickSlots),
            selectedQuickSlotIndex = selectedQuickSlotIndex
        };
    }

    public void RestoreSaveData(PlayerInventorySaveData data)
    {
        if (data == null) return;

        quickSlotUseReservation = null;
        pendingQuickSlotIndex = -1;

        RestoreContainer(inventory, data.inventory, "inventory");
        RestoreContainer(quickSlots, data.quickSlots, "quickSlots");

        selectedQuickSlotIndex = quickSlots.IsValidIndex(data.selectedQuickSlotIndex)
            ? data.selectedQuickSlotIndex
            : 0;

        OnInventoryChanged?.Invoke();
        NotifyAllQuickSlotsChanged();
    }

    private static void RestoreContainer(InventoryContainer target, InventoryContainerSaveData data, string containerName)
    {
        if (target == null || data == null) return;

        int savedCount = data.slots != null ? data.slots.Length : 0;
        int restoredWidth = Mathf.Max(1, target.width, data.width);
        int minimumHeightForSavedSlots = Mathf.CeilToInt(savedCount / (float)restoredWidth);
        int restoredHeight = Mathf.Max(1, target.height, data.height, minimumHeightForSavedSlots);

        target.width = restoredWidth;
        target.height = restoredHeight;
        target.Initialize();

        int copyCount = Mathf.Min(target.slots.Length, savedCount);

        for (int i = 0; i < target.slots.Length; i++)
        {
            if (i >= copyCount || data.slots[i] == null || data.slots[i].IsEmpty)
            {
                target.slots[i].Clear();
                continue;
            }

            target.slots[i].Set(data.slots[i].itemId, data.slots[i].amount);
        }

        if (savedCount > target.slots.Length)
        {
            Debug.LogError($"[PlayerInventory] {containerName} restore truncated slots: saved={savedCount}, target={target.slots.Length}.");
        }
    }

    // 아이템 추가
    public int AddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return amount;

        int remaining = amount;

        // 기존 스택을 먼저 채운 뒤, 새 스택은 퀵슬롯 빈 칸부터 만든다.
        remaining = AddToExistingStacks(quickSlots, item, remaining);
        remaining = AddToExistingStacks(inventory, item, remaining);
        remaining = AddToEmptySlots(quickSlots, item, remaining);
        remaining = AddToEmptySlots(inventory, item, remaining);

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

    /// <summary>
    /// 일반 인벤토리만 같은 아이템끼리 압축한 뒤 지정 기준으로 정렬한다.
    /// 장착 순서 의미가 있는 퀵슬롯은 변경하지 않는다.
    /// </summary>
    public bool ApplyInventorySort(InventorySortCriteria criteria)
    {
        bool sorted = InventorySortUtility.SortAndCompact(inventory, criteria, catalogManager);
        if (!sorted) return false;

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 사망 시 보호 대상인 도구를 제외한 일반/퀵슬롯 아이템을 모두 제거한다.
    /// 슬롯 위치는 유지하며, 전체 처리가 끝난 뒤 변경 이벤트를 한 번만 발행한다.
    /// </summary>
    public int ApplyDeathPenalty()
    {
        // 사용 도중 임시 차감된 퀵슬롯 아이템을 먼저 원상 복구한 뒤 사망 손실을 판정한다.
        // 이 순서를 지키지 않으면 사용 취소 롤백으로 제거된 아이템이 다시 생길 수 있다.
        EndQuickSlotUse();

        int lostAmount = 0;
        bool inventoryChanged = ClearDeathLossItems(inventory, ref lostAmount);
        bool quickSlotsChanged = ClearDeathLossItems(quickSlots, ref lostAmount);

        // 실제 손실이 0이어도 사용 예약 종료 결과와 현재 사망 스냅샷을 파일 저장 계층에 전달한다.
        OnInventoryChanged?.Invoke();
        if (inventoryChanged || quickSlotsChanged) NotifyAllQuickSlotsChanged();
        return lostAmount;
    }

    private bool ClearDeathLossItems(InventoryContainer container, ref int lostAmount)
    {
        if (container == null || container.slots == null) return false;

        bool changed = false;
        for (int i = 0; i < container.slots.Length; i++)
        {
            ItemStack stack = container.slots[i];
            if (stack == null || stack.IsEmpty) continue;

            ItemData item = FindItemData(stack.itemId);
            if (item == null)
            {
                // 카탈로그 누락 때문에 복구 불가능한 데이터 손실이 생기지 않도록 알 수 없는 아이템은 유지한다.
                Debug.LogWarning($"[PlayerInventory] 사망 손실 판정 중 Item_ID '{stack.itemId}'를 찾지 못해 유지합니다.", this);
                continue;
            }

            // TODO: ItemCategory.Armor가 추가되면 아래 보호 조건에
            //       || item.Category == ItemCategory.Armor 를 추가한다.
            // 현재 프로젝트에는 Armor 카테고리가 없으므로 Tool만 도구/방어구 보호 대상으로 취급한다.
            bool keepOnDeath = item.Category == ItemCategory.Tool;
            if (keepOnDeath) continue;

            lostAmount += stack.amount;
            stack.Clear();
            changed = true;
        }

        return changed;
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

    /// <summary>
    /// 클릭 이동용 API. 지정 슬롯에서 요청한 수량을 떼어 독립된 스택으로 반환한다.
    /// UI가 ItemStack 참조를 직접 옮기지 않도록 데이터 변경과 알림을 여기서 함께 처리한다.
    /// </summary>
    public bool TryTakeSlot(SlotGroup group, int index, int amount, out ItemStack takenStack)
    {
        takenStack = null;

        InventoryContainer container = GetContainer(group);
        if (container == null || !container.IsValidIndex(index)) return false;
        if (IsLockedQuickSlot(container, index)) return false;

        ItemStack slot = container.slots[index];
        if (slot == null || slot.IsEmpty || amount <= 0) return false;

        int takenAmount = Mathf.Min(amount, slot.amount);
        takenStack = new ItemStack { itemId = slot.itemId, amount = takenAmount };

        slot.amount -= takenAmount;
        if (slot.amount <= 0) slot.Clear();

        NotifySlotChanged(group, index);
        return true;
    }

    /// <summary>빈손 우클릭용. 홀수 스택은 플레이어가 더 많이 들도록 절반을 올림한다.</summary>
    public bool TryTakeHalfSlot(SlotGroup group, int index, out ItemStack takenStack)
    {
        takenStack = null;

        InventoryContainer container = GetContainer(group);
        if (container == null || !container.IsValidIndex(index)) return false;

        ItemStack slot = container.slots[index];
        if (slot == null || slot.IsEmpty) return false;

        int halfAmount = Mathf.CeilToInt(slot.amount * 0.5f);
        return TryTakeSlot(group, index, halfAmount, out takenStack);
    }

    /// <summary>수량 팝업 표시용으로 슬롯 데이터의 복사본을 반환한다.</summary>
    public bool TryGetSlotSnapshot(SlotGroup group, int index, out ItemStack snapshot)
    {
        snapshot = null;

        InventoryContainer container = GetContainer(group);
        if (container == null || !container.IsValidIndex(index)) return false;

        ItemStack slot = container.slots[index];
        if (slot == null || slot.IsEmpty) return false;

        snapshot = new ItemStack { itemId = slot.itemId, amount = slot.amount };
        return true;
    }

    public ItemData FindItemData(string itemId)
    {
        return catalogManager != null ? catalogManager.FindItemData(itemId) : null;
    }

    /// <summary>
    /// 커서가 들고 있는 스택 전체를 대상 슬롯에 놓는다. 빈 슬롯에는 이동하고,
    /// 같은 아이템에는 MaxStack까지 병합하며, 다른 아이템이면 두 스택을 교환한다.
    /// heldStack은 남은 수량 또는 교환되어 새로 들게 된 스택으로 갱신된다.
    /// </summary>
    public bool TryPlaceHeldStack(SlotGroup group, int index, ItemStack heldStack)
    {
        return TryPlaceHeldAmount(group, index, heldStack, heldStack != null ? heldStack.amount : 0, true);
    }

    /// <summary>
    /// 커서 스택에서 지정 수량만 대상 슬롯에 놓는다. 우클릭은 amount=1, allowSwap=false로 사용한다.
    /// 다른 아이템과의 교환은 전체 스택을 놓는 좌클릭에서만 허용한다.
    /// </summary>
    public bool TryPlaceHeldAmount(SlotGroup group, int index, ItemStack heldStack, int amount, bool allowSwap = false)
    {
        if (heldStack == null || heldStack.IsEmpty || amount <= 0) return false;

        InventoryContainer container = GetContainer(group);
        if (container == null || !container.IsValidIndex(index)) return false;
        if (IsLockedQuickSlot(container, index)) return false;

        ItemStack target = container.slots[index];
        int requestedAmount = Mathf.Min(amount, heldStack.amount);

        if (target.IsEmpty)
        {
            int placed = Mathf.Min(GetMaxStack(heldStack.itemId), requestedAmount);
            target.Set(heldStack.itemId, placed);
            heldStack.amount -= placed;
            if (heldStack.amount <= 0) heldStack.Clear();

            NotifySlotChanged(group, index);
            return true;
        }

        if (target.itemId == heldStack.itemId)
        {
            int space = GetMaxStack(target.itemId) - target.amount;
            if (space <= 0) return false;

            int placed = Mathf.Min(space, requestedAmount);
            target.amount += placed;
            heldStack.amount -= placed;
            if (heldStack.amount <= 0) heldStack.Clear();

            NotifySlotChanged(group, index);
            return true;
        }

        if (!allowSwap || requestedAmount != heldStack.amount) return false;

        string displacedItemId = target.itemId;
        int displacedAmount = target.amount;
        target.Set(heldStack.itemId, heldStack.amount);
        heldStack.Set(displacedItemId, displacedAmount);

        NotifySlotChanged(group, index);
        return true;
    }

    /// <summary>
    /// 인벤토리를 닫을 때 커서에 남은 아이템을 손실 없이 되돌린다.
    /// 원래 슬롯을 먼저 시도하고, 이후 일반 인벤토리와 사용 가능 퀵슬롯의 같은 스택/빈 슬롯을 찾는다.
    /// </summary>
    public bool TryReturnHeldStack(ItemStack heldStack, SlotGroup preferredGroup, int preferredIndex)
    {
        if (heldStack == null || heldStack.IsEmpty) return true;

        TryPlaceWithoutSwap(preferredGroup, preferredIndex, heldStack);
        TryPlaceWithoutSwap(inventory, SlotGroup.Inventory, heldStack);
        TryPlaceWithoutSwap(quickSlots, SlotGroup.QuickSlot, heldStack);

        return heldStack.IsEmpty;
    }

    // 퀵슬롯 아이템 선택. 사용중이면 마지막 입력 기록
    public void SelectQuickSlot(int index)
    {
        OnQuickSlotSelectionRequested?.Invoke(index);

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
        if (quickSlotUseReservation.committed) return false;

        quickSlotUseReservation.committed = true;

        int usedSlotIndex = quickSlotUseReservation.slotIndex;
        OnQuickSlotChanged?.Invoke(usedSlotIndex);

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

        bool wasCommitted = quickSlotUseReservation.committed;
        if (!quickSlotUseReservation.committed)
        {
            RollbackQuickSlotUse();
        }

        int usedSlotIndex = quickSlotUseReservation.slotIndex;

        quickSlotUseReservation = null;

        if (!wasCommitted)
        {
            OnQuickSlotChanged?.Invoke(usedSlotIndex);
        }

        int pendingIndex = pendingQuickSlotIndex;
        pendingQuickSlotIndex = -1;

        if (quickSlots.IsValidIndex(pendingIndex)) ApplyQuickSlotSelection(pendingIndex);
        else if (!wasCommitted) OnSelectedQuickSlotChanged?.Invoke(selectedQuickSlotIndex);
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

    private InventoryContainer GetContainer(SlotGroup group)
    {
        if (group == SlotGroup.Inventory) return inventory;
        if (group == SlotGroup.QuickSlot) return quickSlots;
        return null;
    }

    private int GetMaxStack(string itemId)
    {
        ItemData itemData = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
        return itemData != null ? Mathf.Max(1, itemData.MaxStack) : 1;
    }

    private void NotifySlotChanged(SlotGroup group, int index)
    {
        if (group == SlotGroup.Inventory)
        {
            OnInventoryChanged?.Invoke();
        }
        else if (group == SlotGroup.QuickSlot)
        {
            OnQuickSlotChanged?.Invoke(index);
            NotifySelectedQuickSlotIfChanged(index);
        }
    }

    private void TryPlaceWithoutSwap(SlotGroup group, int index, ItemStack heldStack)
    {
        InventoryContainer container = GetContainer(group);
        if (container == null || !container.IsValidIndex(index)) return;
        if (IsLockedQuickSlot(container, index)) return;

        ItemStack target = container.slots[index];
        if (!target.IsEmpty && target.itemId != heldStack.itemId) return;

        int space = target.IsEmpty ? GetMaxStack(heldStack.itemId) : GetMaxStack(target.itemId) - target.amount;
        if (space <= 0) return;

        int placed = Mathf.Min(space, heldStack.amount);
        if (target.IsEmpty) target.Set(heldStack.itemId, placed);
        else target.amount += placed;

        heldStack.amount -= placed;
        if (heldStack.amount <= 0) heldStack.Clear();
        NotifySlotChanged(group, index);
    }

    private void TryPlaceWithoutSwap(InventoryContainer container, SlotGroup group, ItemStack heldStack)
    {
        if (container == null || container.slots == null || heldStack == null || heldStack.IsEmpty) return;

        for (int i = 0; i < container.slots.Length && !heldStack.IsEmpty; i++)
        {
            ItemStack slot = container.slots[i];
            if (!slot.IsEmpty && slot.itemId == heldStack.itemId)
            {
                TryPlaceWithoutSwap(group, i, heldStack);
            }
        }

        for (int i = 0; i < container.slots.Length && !heldStack.IsEmpty; i++)
        {
            if (container.slots[i].IsEmpty)
            {
                TryPlaceWithoutSwap(group, i, heldStack);
            }
        }
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

        public void PublishInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
            NotifyAllQuickSlotsChanged(); 
        }
    }

}
