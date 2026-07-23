using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using KMS.InventoryDuped;
using HDY.Item;
using HDY.Upgrade;
using System;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고(왼쪽) + 인벤토리/퀵슬롯(오른쪽) 통합 화면 컨트롤러.
    ///
    /// [상호작용 방식] KMS InventoryUI와 동일한 "클릭 앤 캐리 + 분할" 모델을 쓴다.
    /// IInventorySlotClickOwner를 구현하면 InventorySlotUI가 드래그 이벤트를 아예 호출하지 않고 클릭만
    /// 위임하므로, 기존 드래그 전용 로직(BeginSlotDrag/MoveSlotDrag/EndSlotDrag, MoveBetweenSlots 등)은
    /// 전부 제거했다. IInventorySlotOwner 인터페이스 자체는 툴팁 위임 때문에 계속 구현해야 하므로,
    /// 드래그 3종 메서드는 인터페이스 계약을 만족시키기 위한 빈 구현으로만 남겨뒀다(호출되지 않는다).
    ///
    /// - 좌클릭: 커서 비었으면 전체를 집고, 커서에 있으면 전체를 놓는다(빈칸=이동, 같은아이템=병합, 다른아이템=교환)
    /// - 우클릭: 커서 비었으면 절반을 집고, 커서에 있으면 1개만 놓는다(교환 불가)
    /// - 중클릭: 수량 팝업(InventoryQuantityPopupUI, KMS 범용 컴포넌트 재사용)을 열어 정확한 수량만 집는다
    /// - Shift+좌클릭(커서 비어있을 때, 창고 그룹 슬롯에서만): 반대쪽 그룹(창고<->인벤토리+퀵슬롯)의 가장 낮은
    ///   index 칸(병합 가능한 칸 우선, 없으면 빈 칸)으로 스택 전체를 옮긴다. 자리가 없으면 아무 일도 안 한다.
    /// - Ctrl+좌클릭(커서 비어있을 때, 창고 그룹 슬롯에서만): 클릭한 슬롯이 속한 쪽 그룹 전체(창고 단독,
    ///   또는 인벤토리+퀵슬롯 통합)에서 같은 Item_ID를 전부 모아 반대쪽 그룹의 낮은 index부터 채운다.
    ///   다 못 채우면 나머지는 원래 그룹에 그대로 남는다.
    ///
    /// [트래시 슬롯] 병합 없이 무조건 덮어쓰는 임시 1칸. 손에 든 아이템을 놓을 자리가 전혀 없을 때
    /// (일반 조작이든 ESC 닫기 안전장치든) 최종적으로 강제 수납되는 곳이라 실패 케이스가 없다.
    ///
    /// [ESC 닫기 안전장치] 이 패널은 PanelManager가 SetActive(false)로 직접 닫는 구조라 InventoryUI처럼
    /// "닫기 자체를 거부"할 수 없다. 대신 OnDisable에서 커서에 남은 아이템을 최대한 되돌리고, 그래도 안
    /// 되면 트래시 슬롯에 강제로 넣어 유실만은 막는다.
    ///
    /// [재사용] InventorySlotUI(슬롯 하나), ItemDragUI, ItemTooltipUI, InventoryQuantityPopupUI,
    /// InventorySlotMoveHelper(창고 내부 드래그 정렬 등에서 여전히 쓰이는 이동/병합 공용 로직)를 그대로
    /// 가져다 쓴다.
    ///
    /// [PlayerInventory 임시 배치] 아직 씬 간 데이터 전달 시스템이 없어서, 이 씬에도 PlayerInventory를
    /// 임시로 배치해서 참조한다. 나중에 씬 이동 시 데이터를 넘겨받는 방식이 생기면 이 참조 연결 부분만 바뀌면 된다.
    /// </summary>
    public class WarehouseUI : MonoBehaviour, IInventorySlotOwner, IInventorySlotClickOwner
    {
        [Header("데이터 참조")]
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;
        [SerializeField] private ItemCatalogManager catalogManager;

        [Header("창고 (왼쪽, 10 x n 스크롤 - 슬롯은 런타임 생성)")]
        [SerializeField] private ScrollRect storageScrollRect;
        [SerializeField] private RectTransform storageContentParent;
        [SerializeField] private InventorySlotUI storageSlotPrefab;
        [SerializeField] private WarehouseSortUI sortUI;

        [Header("창고 업그레이드 (한 줄 확장)")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private WarehouseUpgrade warehouseUpgrade;

        [Header("인벤토리 (오른쪽 위, 10x6 - 슬롯은 씬에 미리 배치)")]
        [SerializeField] private Transform inventoryGrid;

        [Header("퀵슬롯 (오른쪽 맨 아래, 10칸 - 슬롯은 씬에 미리 배치)")]
        [SerializeField] private Transform quickSlotRoot;

        [Header("트래시 (덮어쓰기 전용 임시 1칸, 씬에 미리 배치)")]
        [SerializeField] private InventorySlotUI trashSlotUI;
        private readonly ItemStack trashStack = new ItemStack();

        [Header("공용 (드래그 고스트 / 툴팁 / 수량 팝업)")]
        [SerializeField] private ItemDragUI itemDragUI;
        [SerializeField] private ItemTooltipUI itemTooltipUI;

        private InventorySlotUI[] storageSlots;
        private InventorySlotUI[] inventorySlots;
        private InventorySlotUI[] quickSlots;

        private InventoryQuantityPopupUI quantityPopup;

        private ItemStack heldStack;
        private SlotGroup heldOriginGroup;
        private int heldOriginIndex = -1;

        private void Awake()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) Debug.LogWarning("[WarehouseUI] playerInventory를 찾을 수 없습니다.", this);
            if (warehouseInventory == null) Debug.LogWarning("[WarehouseUI] warehouseInventory를 찾을 수 없습니다.", this);
            if (storageSlotPrefab == null) Debug.LogWarning("[WarehouseUI] storageSlotPrefab이 비어있습니다. 창고 슬롯을 만들 수 없습니다.", this);
            if (upgradeButton == null) Debug.LogWarning("[WarehouseUI] upgradeButton이 비어있습니다. 창고 업그레이드 버튼이 동작하지 않습니다.", this);
            if (warehouseUpgrade == null) Debug.LogWarning("[WarehouseUI] warehouseUpgrade가 비어있습니다. 업그레이드 팝업을 열 수 없습니다.", this);
            if (trashSlotUI == null) Debug.LogWarning("[WarehouseUI] trashSlotUI가 비어있습니다. 트래시 슬롯이 동작하지 않습니다.", this);

            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(HandleUpgradeButtonClicked);
            }
        }

        private void Start()
        {
            if (playerInventory == null || warehouseInventory == null)
            {
                enabled = false;
                return;
            }

            BindPlayerSlots();
            EnsureStorageSlotCount();
            trashSlotUI?.Initialize(this, SlotGroup.Trash, 0);
            EnsureQuantityPopup();

            HideItemTooltip();
            RefreshAll();
        }

        private void OnEnable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged += RefreshInventorySlots;
                playerInventory.OnQuickSlotChanged += RefreshQuickSlot;
                playerInventory.OnSelectedQuickSlotChanged += RefreshSelectedQuickSlot;
            }

            if (warehouseInventory != null)
            {
                warehouseInventory.OnStorageChanged += RefreshStorageSlots;
                warehouseInventory.OnRowCountChanged += HandleRowCountChanged;
            }

            if (sortUI != null)
            {
                sortUI.OnSortRequested += HandleSortRequested;
            }
        }

        private void OnDisable()
        {
            if (playerInventory != null)
            {
                playerInventory.OnInventoryChanged -= RefreshInventorySlots;
                playerInventory.OnQuickSlotChanged -= RefreshQuickSlot;
                playerInventory.OnSelectedQuickSlotChanged -= RefreshSelectedQuickSlot;
            }

            if (warehouseInventory != null)
            {
                warehouseInventory.OnStorageChanged -= RefreshStorageSlots;
                warehouseInventory.OnRowCountChanged -= HandleRowCountChanged;
            }

            if (sortUI != null)
            {
                sortUI.OnSortRequested -= HandleSortRequested;
            }

            // [ESC 닫기 안전장치] 이 패널은 PanelManager가 SetActive(false)로 직접 닫으므로 닫기 자체를
            // 거부할 수 없다. 열려있는 팝업을 취소하고, 커서에 남은 아이템은 최대한 되돌리되 그래도 자리가
            // 없으면 트래시 슬롯에 강제로 넣어 유실만은 막는다.
            if (quantityPopup != null && quantityPopup.IsOpen)
            {
                quantityPopup.Cancel();
            }

            if (heldStack != null && !heldStack.IsEmpty)
            {
                TryReturnHeldStackAnywhere(heldStack, heldOriginGroup, heldOriginIndex);

                if (!heldStack.IsEmpty)
                {
                    ForcePlaceInTrash(heldStack);
                }

                ClearHeldItem();
            }
        }

        private void Update()
        {
            if (heldStack == null || heldStack.IsEmpty || itemDragUI == null || Mouse.current == null) return;
            itemDragUI.Move(Mouse.current.position.ReadValue());
        }

        // ===================== IInventorySlotOwner (드래그 부분은 클릭 방식 전환으로 더 이상 호출되지 않음) =====================

        public void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position) { }

        public void MoveSlotDrag(Vector2 position) { }

        public void EndSlotDrag(InventorySlotUI target) { }

        public void ShowItemTooltip(ItemStack stack, Vector2 position)
        {
            if ((heldStack != null && !heldStack.IsEmpty) ||
                (quantityPopup != null && quantityPopup.IsOpen) ||
                itemTooltipUI == null) return;

            itemTooltipUI.Show(stack, position);
        }

        public void MoveItemTooltip(Vector2 position)
        {
            if ((heldStack != null && !heldStack.IsEmpty) ||
                (quantityPopup != null && quantityPopup.IsOpen) ||
                itemTooltipUI == null) return;

            itemTooltipUI.Move(position);
        }

        public void HideItemTooltip()
        {
            if (itemTooltipUI == null) return;

            itemTooltipUI.Hide();
        }

        // ===================== IInventorySlotClickOwner =====================

        public void ClickSlot(InventorySlotUI slot, PointerEventData.InputButton button, Vector2 position)
        {
            if (slot == null) return;
            if (quantityPopup != null && quantityPopup.IsOpen) return;
            if (button != PointerEventData.InputButton.Left &&
                button != PointerEventData.InputButton.Right &&
                button != PointerEventData.InputButton.Middle) return;
            if (IsLocked(slot)) return;

            HideItemTooltip();

            bool cursorEmpty = heldStack == null || heldStack.IsEmpty;

            // 창고 전용 단축 조작: Shift/Ctrl+좌클릭 (커서가 비어있고, 창고/인벤토리/퀵슬롯 그룹일 때만)
            if (cursorEmpty && button == PointerEventData.InputButton.Left &&
                IsQuickMoveGroup(slot.group) && IsModifierClick(out bool isShift, out bool isCtrl))
            {
                if (isShift) HandleShiftClick(slot);
                else if (isCtrl) HandleCtrlClick(slot);
                return;
            }

            if (button == PointerEventData.InputButton.Middle)
            {
                if (!cursorEmpty) return;
                ShowQuantityPopup(slot, position);
                return;
            }

            if (cursorEmpty)
            {
                bool taken = button == PointerEventData.InputButton.Left
                    ? TryTakeFull(slot.group, slot.slotIndex, out ItemStack takenStack)
                    : TryTakeHalf(slot.group, slot.slotIndex, out takenStack);

                if (!taken) return;

                heldStack = takenStack;
                heldOriginGroup = slot.group;
                heldOriginIndex = slot.slotIndex;
            }
            else
            {
                bool placed = button == PointerEventData.InputButton.Left
                    ? TryPlaceFull(slot.group, slot.slotIndex, heldStack)
                    : TryPlaceOne(slot.group, slot.slotIndex, heldStack);

                if (!placed) return;
            }

            RefreshHeldItem(position);
        }

        // ===================== 그룹별 라우팅 (Storage/Trash는 신규 API, 나머지는 PlayerInventory 그대로) =====================

        private bool TryTakeFull(SlotGroup group, int index, out ItemStack taken)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryTakeSlot(index, int.MaxValue, out taken);
            if (group == SlotGroup.Trash) return TryTakeTrashAmount(trashStack.IsEmpty ? 0 : trashStack.amount, out taken);
            return playerInventory.TryTakeSlot(group, index, int.MaxValue, out taken);
        }

        private bool TryTakeHalf(SlotGroup group, int index, out ItemStack taken)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryTakeHalfSlot(index, out taken);
            if (group == SlotGroup.Trash) return TryTakeTrashAmount(trashStack.IsEmpty ? 0 : Mathf.CeilToInt(trashStack.amount * 0.5f), out taken);
            return playerInventory.TryTakeHalfSlot(group, index, out taken);
        }

        private bool TryTakeAmount(SlotGroup group, int index, int amount, out ItemStack taken)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryTakeSlot(index, amount, out taken);
            if (group == SlotGroup.Trash) return TryTakeTrashAmount(amount, out taken);
            return playerInventory.TryTakeSlot(group, index, amount, out taken);
        }

        private bool TryPlaceFull(SlotGroup group, int index, ItemStack held)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryPlaceHeldStack(index, held);
            if (group == SlotGroup.Trash) return PlaceTrash(held, held.amount);
            return playerInventory.TryPlaceHeldStack(group, index, held);
        }

        private bool TryPlaceOne(SlotGroup group, int index, ItemStack held)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryPlaceHeldAmount(index, held, 1, false);
            if (group == SlotGroup.Trash) return PlaceTrash(held, 1);
            return playerInventory.TryPlaceHeldAmount(group, index, held, 1, false);
        }

        private bool TryGetSnapshot(SlotGroup group, int index, out ItemStack snapshot)
        {
            if (group == SlotGroup.Storage) return warehouseInventory.TryGetSlotSnapshot(index, out snapshot);

            if (group == SlotGroup.Trash)
            {
                snapshot = trashStack.IsEmpty ? null : new ItemStack { itemId = trashStack.itemId, amount = trashStack.amount };
                return snapshot != null;
            }

            return playerInventory.TryGetSlotSnapshot(group, index, out snapshot);
        }

        // ===================== 트래시 슬롯 (병합 없이 무조건 덮어씀) =====================

        private bool TryTakeTrashAmount(int amount, out ItemStack taken)
        {
            taken = null;
            if (trashStack.IsEmpty || amount <= 0) return false;

            int takenAmount = Mathf.Min(amount, trashStack.amount);
            taken = new ItemStack { itemId = trashStack.itemId, amount = takenAmount };

            trashStack.amount -= takenAmount;
            if (trashStack.amount <= 0) trashStack.Clear();

            RefreshTrashSlot();
            return true;
        }

        /// <summary>트래시는 병합하지 않고 무조건 덮어쓴다 - 기존에 있던 아이템은 그대로 삭제되며 복구되지 않는다.</summary>
        private bool PlaceTrash(ItemStack held, int amount)
        {
            if (held == null || held.IsEmpty || amount <= 0) return false;

            int placeAmount = Mathf.Min(amount, held.amount);

            trashStack.Set(held.itemId, placeAmount);
            held.amount -= placeAmount;
            if (held.amount <= 0) held.Clear();

            RefreshTrashSlot();
            return true;
        }

        private void ForcePlaceInTrash(ItemStack held)
        {
            if (held == null || held.IsEmpty) return;

            string itemId = held.itemId;
            int amount = held.amount;

            PlaceTrash(held, held.amount);

            Debug.LogWarning($"[WarehouseUI] 반환할 공간이 없어 '{itemId}' x{amount}을(를) 트래시 슬롯에 강제로 넣었습니다.");
        }

        private void RefreshTrashSlot()
        {
            trashSlotUI?.SetStack(trashStack);
        }

        // ===================== Shift/Ctrl 창고 전용 단축 조작 =====================

        private static bool IsQuickMoveGroup(SlotGroup group)
        {
            return group == SlotGroup.Storage || group == SlotGroup.Inventory || group == SlotGroup.QuickSlot;
        }

        private static bool IsModifierClick(out bool isShift, out bool isCtrl)
        {
            Keyboard keyboard = Keyboard.current;

            isShift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
            isCtrl = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

            return isShift || isCtrl;
        }

        /// <summary>Shift+좌클릭: 반대쪽 그룹(창고&lt;-&gt;인벤토리+퀵슬롯)의 가장 낮은 index 칸으로 스택 전체를 옮긴다.</summary>
        private void HandleShiftClick(InventorySlotUI slot)
        {
            if (slot.group == SlotGroup.Storage)
            {
                MoveWholeStackToLowestIndex(warehouseInventory.storage, slot.slotIndex, playerInventory.inventory, playerInventory.quickSlots);
            }
            else
            {
                InventoryContainer fromContainer = slot.group == SlotGroup.Inventory ? playerInventory.inventory : playerInventory.quickSlots;
                MoveWholeStackToLowestIndex(fromContainer, slot.slotIndex, warehouseInventory.storage);
            }
        }

        /// <summary>
        /// Ctrl+좌클릭: 클릭한 슬롯이 속한 쪽 그룹 전체(창고 단독, 또는 인벤토리+퀵슬롯 통합)에서 같은 Item_ID를
        /// 전부 모아 반대쪽 그룹의 낮은 index부터 채운다. 다 못 채우면 나머지는 원래 그룹에 그대로 남는다.
        /// </summary>
        private void HandleCtrlClick(InventorySlotUI slot)
        {
            if (!TryGetSnapshot(slot.group, slot.slotIndex, out ItemStack snapshot)) return;

            if (slot.group == SlotGroup.Storage)
            {
                CollectAndFill(new[] { warehouseInventory.storage }, snapshot.itemId, new[] { playerInventory.inventory, playerInventory.quickSlots });
            }
            else
            {
                CollectAndFill(new[] { playerInventory.inventory, playerInventory.quickSlots }, snapshot.itemId, new[] { warehouseInventory.storage });
            }
        }

        /// <summary>fromContainer[fromIndex]의 스택 전체를, 목적지 후보들(순서대로) 중 가장 먼저 발견되는
        /// "병합 가능하거나 비어있는" 가장 낮은 index 칸으로 옮긴다. 목적지가 없으면 아무 일도 하지 않는다.</summary>
        private void MoveWholeStackToLowestIndex(InventoryContainer fromContainer, int fromIndex, params InventoryContainer[] destinationsInOrder)
        {
            if (fromContainer == null || !fromContainer.IsValidIndex(fromIndex)) return;

            ItemStack fromSlot = fromContainer.slots[fromIndex];
            if (fromSlot == null || fromSlot.IsEmpty) return;

            foreach (var destination in destinationsInOrder)
            {
                if (destination == null) continue;

                int targetIndex = FindBestDestinationIndex(destination, fromSlot.itemId);
                if (targetIndex < 0) continue;

                bool moved = InventorySlotMoveHelper.MoveSlot(fromContainer, fromIndex, destination, targetIndex, catalogManager);

                if (moved)
                {
                    warehouseInventory.PublishWarehouseChanged();
                    playerInventory.PublishInventoryChanged();
                    return;
                }
            }
        }

        /// <summary>itemId를 병합할 수 있는 가장 낮은 index 칸을 찾고, 없으면 가장 낮은 index의 빈 칸을 찾는다. 없으면 -1.</summary>
        private int FindBestDestinationIndex(InventoryContainer container, string itemId)
        {
            int maxStack = GetMaxStackSafe(itemId);

            for (int i = 0; i < container.slots.Length; i++)
            {
                ItemStack s = container.slots[i];
                if (!s.IsEmpty && s.itemId == itemId && s.amount < maxStack) return i;
            }

            for (int i = 0; i < container.slots.Length; i++)
            {
                if (container.slots[i].IsEmpty) return i;
            }

            return -1;
        }

        /// <summary>
        /// sourceContainers 전체에서 itemId와 일치하는 수량을 모두 모아, destinationContainers를 순서대로
        /// 낮은 index부터 채운다(병합 우선, 그다음 빈 칸). 다 못 채우면 나머지는 원래 있던 자리들에 그대로 남긴다.
        /// </summary>
        private void CollectAndFill(InventoryContainer[] sourceContainers, string itemId, InventoryContainer[] destinationContainers)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            int totalAvailable = 0;
            foreach (var src in sourceContainers)
            {
                if (src?.slots == null) continue;
                foreach (var s in src.slots)
                {
                    if (!s.IsEmpty && s.itemId == itemId) totalAvailable += s.amount;
                }
            }

            if (totalAvailable <= 0) return;

            int remainingToMove = totalAvailable;

            foreach (var destination in destinationContainers)
            {
                if (destination?.slots == null || remainingToMove <= 0) continue;
                remainingToMove = FillDestinationWithAmount(destination, itemId, remainingToMove);
            }

            int actuallyMoved = totalAvailable - remainingToMove;
            if (actuallyMoved <= 0) return;

            RemoveAmountFromSources(sourceContainers, itemId, actuallyMoved);

            warehouseInventory.PublishWarehouseChanged();
            playerInventory.PublishInventoryChanged();
        }

        /// <summary>destination을 낮은 index부터(병합 우선 -&gt; 빈 칸) amount만큼 채운다. 채우지 못한 나머지를 반환한다.</summary>
        private int FillDestinationWithAmount(InventoryContainer destination, string itemId, int amount)
        {
            int maxStack = GetMaxStackSafe(itemId);
            int remaining = amount;

            for (int i = 0; i < destination.slots.Length && remaining > 0; i++)
            {
                ItemStack s = destination.slots[i];
                if (s.IsEmpty || s.itemId != itemId) continue;

                int space = maxStack - s.amount;
                if (space <= 0) continue;

                int add = Mathf.Min(space, remaining);
                s.amount += add;
                remaining -= add;
            }

            for (int i = 0; i < destination.slots.Length && remaining > 0; i++)
            {
                ItemStack s = destination.slots[i];
                if (!s.IsEmpty) continue;

                int add = Mathf.Min(maxStack, remaining);
                s.Set(itemId, add);
                remaining -= add;
            }

            return remaining;
        }

        /// <summary>sourceContainers에서 itemId를 총 amount만큼 제거한다(각 컨테이너를 순서대로 훑으며 차감).</summary>
        private void RemoveAmountFromSources(InventoryContainer[] sourceContainers, string itemId, int amount)
        {
            int remaining = amount;

            foreach (var src in sourceContainers)
            {
                if (src?.slots == null) continue;

                for (int i = 0; i < src.slots.Length && remaining > 0; i++)
                {
                    ItemStack s = src.slots[i];
                    if (s.IsEmpty || s.itemId != itemId) continue;

                    int removed = Mathf.Min(s.amount, remaining);
                    s.amount -= removed;
                    remaining -= removed;
                    if (s.amount <= 0) s.Clear();
                }
            }
        }

        private int GetMaxStackSafe(string itemId)
        {
            var data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
            return data != null ? Mathf.Max(1, data.MaxStack) : 1;
        }

        // ===================== 커서(held) 아이템 관리 =====================

        private void RefreshHeldItem(Vector2 position)
        {
            if (heldStack == null || heldStack.IsEmpty)
            {
                ClearHeldItem();
                return;
            }

            if (itemDragUI != null) itemDragUI.Show(heldStack, position);
        }

        private void ClearHeldItem()
        {
            heldStack = null;
            heldOriginIndex = -1;
            if (itemDragUI != null) itemDragUI.Hide();
        }

        /// <summary>[안전장치] 원래 있던 그룹 우선, 그다음 반대쪽 그룹까지 확장해서 반환을 시도한다.</summary>
        private bool TryReturnHeldStackAnywhere(ItemStack held, SlotGroup originGroup, int originIndex)
        {
            if (held == null || held.IsEmpty) return true;

            if (originGroup == SlotGroup.Storage)
            {
                warehouseInventory.TryReturnStack(held, originIndex);
                if (!held.IsEmpty) playerInventory.TryReturnHeldStack(held, SlotGroup.Inventory, -1);
            }
            else if (originGroup == SlotGroup.Trash)
            {
                PlaceTrash(held, held.amount);
            }
            else
            {
                playerInventory.TryReturnHeldStack(held, originGroup, originIndex);
                if (!held.IsEmpty) warehouseInventory.TryReturnStack(held, -1);
            }

            return held.IsEmpty;
        }

        // ===================== 수량 팝업 (중클릭) =====================

        private void EnsureQuantityPopup()
        {
            if (quantityPopup != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            TMP_FontAsset font = itemTooltipUI != null &&
                                 itemTooltipUI.tagTemplate != null &&
                                 itemTooltipUI.tagTemplate.labelText != null
                ? itemTooltipUI.tagTemplate.labelText.font
                : null;

            quantityPopup = InventoryQuantityPopupUI.Create(canvas, font);
            if (quantityPopup == null)
            {
                Debug.LogWarning("[WarehouseUI] 수량 선택 팝업을 생성할 Canvas를 찾지 못했습니다.");
            }
        }

        private void ShowQuantityPopup(InventorySlotUI slot, Vector2 position)
        {
            if (heldStack != null && !heldStack.IsEmpty) return;
            if (quantityPopup == null) EnsureQuantityPopup();
            if (quantityPopup == null) return;
            if (!TryGetSnapshot(slot.group, slot.slotIndex, out ItemStack snapshot)) return;

            ItemData itemData = catalogManager != null ? catalogManager.FindItemData(snapshot.itemId) : null;
            if (itemData == null) return;

            SlotGroup group = slot.group;
            int index = slot.slotIndex;
            quantityPopup.Show(itemData, snapshot.amount, position,
                amount => ConfirmQuantityPick(group, index, amount, position), null);
        }

        private void ConfirmQuantityPick(SlotGroup group, int index, int amount, Vector2 position)
        {
            if (heldStack != null && !heldStack.IsEmpty) return;
            if (!TryTakeAmount(group, index, amount, out ItemStack takenStack)) return;

            heldStack = takenStack;
            heldOriginGroup = group;
            heldOriginIndex = index;
            RefreshHeldItem(position);
        }

        // ===================== 창고 업그레이드 =====================

        private void HandleUpgradeButtonClicked()
        {
            if (warehouseUpgrade == null)
            {
                Debug.LogWarning("[WarehouseUI] warehouseUpgrade가 비어있어 업그레이드 팝업을 열 수 없습니다.", this);
                return;
            }

            if (UpgradePopupUI.Instance == null)
            {
                Debug.LogWarning("[WarehouseUI] 씬에서 UpgradePopupUI를 찾을 수 없습니다.", this);
                return;
            }

            UpgradePopupUI.Instance.Show(warehouseUpgrade);
        }

        private void HandleRowCountChanged()
        {
            Debug.Log("[WarehouseUI] OnRowCountChanged 수신 -> 창고 슬롯 확장 시도");

            EnsureStorageSlotCount();
            RefreshStorageSlots();
        }

        private bool IsLocked(InventorySlotUI slot)
        {
            return slot != null && slot.group == SlotGroup.QuickSlot && playerInventory.IsQuickSlotLocked(slot.slotIndex);
        }

        private void HandleSortRequested(ItemSortCriteria criteria)
        {
            Debug.Log($"[WarehouseUI] 창고 정렬 요청: {criteria}");
            warehouseInventory?.ApplySort(criteria);
        }

        // ===================== 슬롯 바인딩 =====================

        /// <summary>
        /// 창고 슬롯은 개수가 유동적(10 x n)이라 도감 그리드와 동일하게 필요한 만큼 런타임에 Instantiate한다.
        /// 이미 만들어둔 슬롯은 그대로 재사용하고, 업그레이드로 행이 늘어나 슬롯이 더 필요해지면 모자란 만큼만
        /// 추가로 Instantiate한다(최초 생성도 "0개에서 필요한 만큼 늘리기"로 취급해서 동일한 메서드를 쓴다).
        /// </summary>
        private void EnsureStorageSlotCount()
        {
            if (storageSlotPrefab == null || storageContentParent == null || warehouseInventory == null) return;

            int required = warehouseInventory.storage.slots.Length;
            int current = storageSlots != null ? storageSlots.Length : 0;

            if (required <= current) return;

            var grown = new InventorySlotUI[required];

            for (int i = 0; i < current; i++)
            {
                grown[i] = storageSlots[i];
            }

            for (int i = current; i < required; i++)
            {
                var slot = Instantiate(storageSlotPrefab, storageContentParent);
                slot.Initialize(this, SlotGroup.Storage, i);
                grown[i] = slot;
            }

            storageSlots = grown;
        }

        /// <summary>인벤토리/퀵슬롯은 기존 컨벤션대로 씬에 미리 배치된 슬롯을 그대로 수집한다.</summary>
        private void BindPlayerSlots()
        {
            inventorySlots = BindSlotGroup(inventoryGrid, playerInventory.inventory.slots.Length, SlotGroup.Inventory);
            quickSlots = BindSlotGroup(quickSlotRoot, playerInventory.quickSlots.slots.Length, SlotGroup.QuickSlot);
        }

        private InventorySlotUI[] BindSlotGroup(Transform root, int count, SlotGroup group)
        {
            InventorySlotUI[] result = new InventorySlotUI[count];

            if (root == null) return result;

            for (int i = 0; i < count && i < root.childCount; i++)
            {
                InventorySlotUI slotUI = root.GetChild(i).GetComponent<InventorySlotUI>();
                result[i] = slotUI;

                if (slotUI != null) slotUI.Initialize(this, group, i);
            }

            return result;
        }

        // ===================== 갱신 =====================

        private void RefreshAll()
        {
            RefreshStorageSlots();
            RefreshInventorySlots();
            RefreshQuickSlots();
            RefreshSelectedQuickSlot(playerInventory.selectedQuickSlotIndex);
            RefreshTrashSlot();
        }

        private void RefreshStorageSlots()
        {
            if (storageSlots == null) return;

            for (int i = 0; i < storageSlots.Length; i++)
            {
                storageSlots[i]?.SetStack(warehouseInventory.storage.slots[i]);
            }
        }

        private void RefreshInventorySlots()
        {
            if (inventorySlots == null) return;

            for (int i = 0; i < inventorySlots.Length; i++)
            {
                if (inventorySlots[i] != null)
                {
                    inventorySlots[i].SetStack(playerInventory.inventory.slots[i]);
                }
            }
        }

        private void RefreshQuickSlots()
        {
            if (quickSlots == null) return;

            for (int i = 0; i < quickSlots.Length; i++)
            {
                RefreshQuickSlot(i);
            }
        }

        private void RefreshQuickSlot(int index)
        {
            if (quickSlots == null || index < 0 || index >= quickSlots.Length || quickSlots[index] == null) return;

            quickSlots[index].SetStack(playerInventory.quickSlots.slots[index]);
            quickSlots[index].SetSelected(index == playerInventory.selectedQuickSlotIndex);
        }

        private void RefreshSelectedQuickSlot(int index)
        {
            if (quickSlots == null) return;

            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null) quickSlots[i].SetSelected(i == index);
            }
        }
    }
}
