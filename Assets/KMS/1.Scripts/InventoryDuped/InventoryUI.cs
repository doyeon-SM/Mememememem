using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// [HDY 요청] IInventorySlotOwner를 구현해서, InventorySlotUI가 owner 타입과 무관하게 이 컨트롤러도
    /// 그대로 사용할 수 있게 했다(WarehouseUI와 동일한 인터페이스 계약). 기존 동작(인벤토리+퀵슬롯 전용
    /// 화면)은 변경 없음 - SlotGroup enum으로 bool(isQuickSlot)을 대체한 것뿐이다.
    /// </summary>
    public class InventoryUI : MonoBehaviour, IInventorySlotOwner, IInventorySlotClickOwner
    {
        public PlayerInventory playerInventory;

        public GameObject inventoryPanel;
        public Transform inventoryGrid;
        public Transform quickSlotRoot;
        public ItemDragUI itemDragUI;
        public ItemTooltipUI itemTooltipUI;

        [Header("정렬")]
        [SerializeField] private InventorySortUI sortUI;

        [Header("KMS References")]
        [SerializeField] private KMS.PlayerInput playerInput;
        [SerializeField] private KMS.PlayerMovement playerMovement;
        [SerializeField] private KMS.PlayerCameraController cameraController;
        [SerializeField] private KMS.PlayerHUD playerHud;
        [SerializeField] private KMS.KMSMemDexLauncher memDexLauncher;

        // [HDY 요청] Item_ID만으로 테스트 지급을 할 수 있는 디버그 UI 훅.
        [Header("디버그 - Item_ID로 아이템 지급 (테스트용)")]
        [SerializeField] private TMP_InputField debugItemIdInput;
        [SerializeField] private TMP_InputField debugAmountInput;
        [SerializeField] private Button debugGiveItemButton;

        private InventorySlotUI[] inventorySlots;
        private InventorySlotUI[] quickSlots;
        private InventoryQuantityPopupUI quantityPopup;

        private InventorySlotUI dragSource;
        private ItemStack heldStack;
        private SlotGroup heldOriginGroup;
        private int heldOriginIndex = -1;
        private bool isInventoryOpen;
        private bool previousMovementEnabled = true;
        private bool previousGameplayInputBlocked;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (playerInventory == null)
            {
                Debug.LogWarning("[InventoryUI] PlayerInventory reference is missing.");
                enabled = false;
                return;
            }

            BindSlots();
            EnsureQuantityPopup();
            SubscribeInventoryEvents();
            SubscribeInputEvents();
            SubscribeDebugGiveItemButton();
            SubscribeSortUI();

            isInventoryOpen = false;
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (playerHud != null) playerHud.SetSurvivalStatusVisible(true);
            HideItemTooltip();
            RefreshAll();
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
            UnsubscribeInputEvents();
            UnsubscribeDebugGiveItemButton();
            UnsubscribeSortUI();
        }

        private void OnDisable()
        {
            if (isInventoryOpen) SetInventoryOpen(false);
        }

        private void Update()
        {
            if (heldStack == null || heldStack.IsEmpty || itemDragUI == null || Mouse.current == null) return;
            itemDragUI.Move(Mouse.current.position.ReadValue());
        }

        public void ClickSlot(InventorySlotUI slot, PointerEventData.InputButton button, Vector2 position)
        {
            if (!isInventoryOpen || slot == null) return;
            if (quantityPopup != null && quantityPopup.IsOpen) return;
            if (button != PointerEventData.InputButton.Left &&
                button != PointerEventData.InputButton.Right &&
                button != PointerEventData.InputButton.Middle) return;
            if (IsLockedQuickSlot(slot)) return;

            HideItemTooltip();

            if (button == PointerEventData.InputButton.Middle)
            {
                ShowQuantityPopup(slot, position);
                return;
            }

            if (heldStack == null || heldStack.IsEmpty)
            {
                bool taken = button == PointerEventData.InputButton.Left
                    ? playerInventory.TryTakeSlot(slot.group, slot.slotIndex, int.MaxValue, out ItemStack takenStack)
                    : playerInventory.TryTakeHalfSlot(slot.group, slot.slotIndex, out takenStack);

                if (!taken) return;

                heldStack = takenStack;
                heldOriginGroup = slot.group;
                heldOriginIndex = slot.slotIndex;
            }
            else
            {
                bool placed = button == PointerEventData.InputButton.Left
                    ? playerInventory.TryPlaceHeldStack(slot.group, slot.slotIndex, heldStack)
                    : playerInventory.TryPlaceHeldAmount(slot.group, slot.slotIndex, heldStack, 1);

                if (!placed) return;
            }

            RefreshHeldItem(position);
        }

        public void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position)
        {
            if (!isInventoryOpen || source == null || stack == null || stack.IsEmpty) return;
            if (IsLockedQuickSlot(source)) return;

            HideItemTooltip();

            dragSource = source;
            if (itemDragUI != null) itemDragUI.Show(stack, position);
        }

        public void MoveSlotDrag(Vector2 position)
        {
            if (dragSource == null || itemDragUI == null) return;

            itemDragUI.Move(position);
        }

        public void EndSlotDrag(InventorySlotUI target)
        {
            if (dragSource != null && target != null && dragSource != target)
            {
                MoveBetweenSlots(dragSource, target);
            }

            dragSource = null;
            if (itemDragUI != null) itemDragUI.Hide();
        }

        public void ShowItemTooltip(ItemStack stack, Vector2 position)
        {
            if (dragSource != null ||
                (heldStack != null && !heldStack.IsEmpty) ||
                (quantityPopup != null && quantityPopup.IsOpen) ||
                itemTooltipUI == null) return;

            itemTooltipUI.Show(stack, position);
        }

        public void MoveItemTooltip(Vector2 position)
        {
            if (dragSource != null ||
                (heldStack != null && !heldStack.IsEmpty) ||
                (quantityPopup != null && quantityPopup.IsOpen) ||
                itemTooltipUI == null) return;

            itemTooltipUI.Move(position);
        }

        public void HideItemTooltip()
        {
            if (itemTooltipUI == null) return;

            itemTooltipUI.Hide();
        }

        public void OnCloseButtonClick()
        {
            Close();
        }

        public void Open()
        {
            SetInventoryOpen(true);
        }

        public void Close()
        {
            SetInventoryOpen(false);
        }

        public void Toggle()
        {
            SetInventoryOpen(!isInventoryOpen);
        }

        private void ResolveReferences()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInput == null) playerInput = FindFirstObjectByType<KMS.PlayerInput>();
            if (playerMovement == null) playerMovement = FindFirstObjectByType<KMS.PlayerMovement>();
            if (cameraController == null) cameraController = FindFirstObjectByType<KMS.PlayerCameraController>();
            if (playerHud == null) playerHud = FindFirstObjectByType<KMS.PlayerHUD>();
            if (memDexLauncher == null) memDexLauncher = FindFirstObjectByType<KMS.KMSMemDexLauncher>();
            if (sortUI == null && inventoryPanel != null)
            {
                sortUI = inventoryPanel.GetComponentInChildren<InventorySortUI>(true);
            }
        }

        private void SubscribeSortUI()
        {
            if (sortUI != null) sortUI.OnSortRequested += HandleSortRequested;
        }

        private void UnsubscribeSortUI()
        {
            if (sortUI != null) sortUI.OnSortRequested -= HandleSortRequested;
        }

        private void HandleSortRequested(InventorySortCriteria criteria)
        {
            if (!isInventoryOpen || playerInventory == null) return;

            bool isHoldingItem = heldStack != null && !heldStack.IsEmpty;
            bool isDragging = dragSource != null;
            bool isChoosingQuantity = quantityPopup != null && quantityPopup.IsOpen;
            if (isHoldingItem || isDragging || isChoosingQuantity)
            {
                Debug.Log("[InventoryUI] 아이템 이동 또는 수량 선택 중에는 정렬할 수 없습니다.", this);
                return;
            }

            HideItemTooltip();
            playerInventory.ApplyInventorySort(criteria);
        }

        private void SelectQuickSlot(int index)
        {
            if (playerInventory == null) return;

            playerInventory.SelectQuickSlot(index);
        }

        private void SelectQuickSlotOffset(int direction)
        {
            if (QuickSlotScrollBlockerUI.isPointerOver) return;
            if (playerInventory == null || playerInventory.quickSlots.slots == null) return;

            int count = playerInventory.quickSlots.slots.Length;
            if (count <= 0) return;

            int nextIndex = (playerInventory.selectedQuickSlotIndex + direction) % count;
            if (nextIndex < 0) nextIndex += count;

            SelectQuickSlot(nextIndex);
        }

        /// <summary>
        /// [HDY 요청] SlotGroup 조합에 따라 PlayerInventory의 알맞은 이동 메서드를 호출한다.
        /// 이 컨트롤러는 인벤토리/퀵슬롯 2그룹만 다루므로 Storage 그룹은 여기 나타나지 않는다(방어적으로 무시).
        /// </summary>
        private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
        {
            if (playerInventory == null) return;
            if (IsLockedQuickSlot(from) || IsLockedQuickSlot(to)) return;

            if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Inventory) playerInventory.MoveInventorySlot(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.Inventory && to.group == SlotGroup.QuickSlot) playerInventory.MoveInventoryToQuickSlot(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.QuickSlot && to.group == SlotGroup.Inventory) playerInventory.MoveQuickSlotToInventory(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.QuickSlot && to.group == SlotGroup.QuickSlot) playerInventory.MoveQuickSlot(from.slotIndex, to.slotIndex);
            // else: Storage가 섞인 조합 - 이 컨트롤러 범위 밖이므로 무시(WarehouseUI에서만 발생해야 함)
        }

        private void SetInventoryOpen(bool open)
        {
            if (open)
            {
                if (memDexLauncher == null)
                {
                    memDexLauncher = FindFirstObjectByType<KMS.KMSMemDexLauncher>();
                }

                if (memDexLauncher != null && memDexLauncher.IsOpen) return;
            }

            if (isInventoryOpen == open) return;

            if (!open && quantityPopup != null && quantityPopup.IsOpen)
            {
                quantityPopup.Cancel();
            }

            if (!open && heldStack != null && !heldStack.IsEmpty)
            {
                if (playerInventory == null || !playerInventory.TryReturnHeldStack(heldStack, heldOriginGroup, heldOriginIndex))
                {
                    Debug.LogWarning("[InventoryUI] 커서에 든 아이템을 반환할 공간이 없어 인벤토리를 닫지 못했습니다.");
                    return;
                }

                ClearHeldItem();
            }

            if (open)
            {
                previousMovementEnabled = playerMovement == null || playerMovement.IsMovementEnabled;
                previousGameplayInputBlocked = playerInput != null && playerInput.IsGameplayInputBlocked;
            }

            isInventoryOpen = open;

            if (inventoryPanel != null) inventoryPanel.SetActive(open);
            if (playerHud != null) playerHud.SetSurvivalStatusVisible(!open);
            if (playerInput != null)
            {
                playerInput.SetCursorReleased(open);
                playerInput.SetGameplayInputBlocked(open ? true : previousGameplayInputBlocked);
            }
            if (playerMovement != null) playerMovement.IsMovementEnabled = open ? false : previousMovementEnabled;

            if (cameraController != null)
            {
                cameraController.SetCursorLocked(!open);
            }
            else
            {
                Cursor.visible = open;
                Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            }

            if (!open)
            {
                dragSource = null;
                if (itemDragUI != null) itemDragUI.Hide();
                HideItemTooltip();
            }
        }

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

        private void EnsureQuantityPopup()
        {
            if (quantityPopup != null) return;

            Canvas canvas = inventoryPanel != null ? inventoryPanel.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();
            TMP_FontAsset font = itemTooltipUI != null &&
                                 itemTooltipUI.tagTemplate != null &&
                                 itemTooltipUI.tagTemplate.labelText != null
                ? itemTooltipUI.tagTemplate.labelText.font
                : null;

            for (int i = 0; i < inventorySlots.Length && font == null; i++)
            {
                if (inventorySlots[i] != null && inventorySlots[i].amountText != null)
                {
                    font = inventorySlots[i].amountText.font;
                }
            }

            quantityPopup = InventoryQuantityPopupUI.Create(canvas, font);
            if (quantityPopup == null)
            {
                Debug.LogWarning("[InventoryUI] 수량 선택 팝업을 생성할 Canvas를 찾지 못했습니다.");
            }
        }

        private void ShowQuantityPopup(InventorySlotUI slot, Vector2 position)
        {
            if (heldStack != null && !heldStack.IsEmpty) return;
            if (quantityPopup == null) EnsureQuantityPopup();
            if (quantityPopup == null) return;
            if (!playerInventory.TryGetSlotSnapshot(slot.group, slot.slotIndex, out ItemStack snapshot)) return;

            HDY.Item.ItemData itemData = playerInventory.FindItemData(snapshot.itemId);
            if (itemData == null) return;

            SlotGroup group = slot.group;
            int index = slot.slotIndex;
            quantityPopup.Show(itemData, snapshot.amount, position,
                amount => ConfirmQuantityPick(group, index, amount, position), null);
        }

        private void ConfirmQuantityPick(SlotGroup group, int index, int amount, Vector2 position)
        {
            if (!isInventoryOpen || heldStack != null && !heldStack.IsEmpty) return;
            if (!playerInventory.TryTakeSlot(group, index, amount, out ItemStack takenStack)) return;

            heldStack = takenStack;
            heldOriginGroup = group;
            heldOriginIndex = index;
            RefreshHeldItem(position);
        }

        private void BindSlots()
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

        private void SubscribeInventoryEvents()
        {
            playerInventory.OnInventoryChanged += RefreshInventorySlots;
            playerInventory.OnQuickSlotChanged += RefreshQuickSlot;
            playerInventory.OnSelectedQuickSlotChanged += RefreshSelectedQuickSlot;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (playerInventory == null) return;

            playerInventory.OnInventoryChanged -= RefreshInventorySlots;
            playerInventory.OnQuickSlotChanged -= RefreshQuickSlot;
            playerInventory.OnSelectedQuickSlotChanged -= RefreshSelectedQuickSlot;
        }

        private void SubscribeInputEvents()
        {
            if (playerInput == null) return;

            playerInput.InventoryPressed += Toggle;
            playerInput.QuickSlotPressed += SelectQuickSlot;
            playerInput.QuickSlotScrolled += SelectQuickSlotOffset;
        }

        private void UnsubscribeInputEvents()
        {
            if (playerInput == null) return;

            playerInput.InventoryPressed -= Toggle;
            playerInput.QuickSlotPressed -= SelectQuickSlot;
            playerInput.QuickSlotScrolled -= SelectQuickSlotOffset;
        }

        private void SubscribeDebugGiveItemButton()
        {
            if (debugGiveItemButton != null)
            {
                debugGiveItemButton.onClick.AddListener(HandleDebugGiveItemClicked);
            }
        }

        private void UnsubscribeDebugGiveItemButton()
        {
            if (debugGiveItemButton != null)
            {
                debugGiveItemButton.onClick.RemoveListener(HandleDebugGiveItemClicked);
            }
        }

        private void HandleDebugGiveItemClicked()
        {
            if (playerInventory == null || debugItemIdInput == null) return;

            string itemId = debugItemIdInput.text != null ? debugItemIdInput.text.Trim() : string.Empty;

            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[InventoryUI] 디버그 지급: Item_ID를 입력해주세요.");
                return;
            }

            int amount = 1;
            if (debugAmountInput != null && !string.IsNullOrEmpty(debugAmountInput.text))
            {
                int.TryParse(debugAmountInput.text, out amount);
            }
            if (amount <= 0) amount = 1;

            int remaining = playerInventory.AddItem(itemId, amount);
            int added = amount - remaining;

            Debug.Log($"[InventoryUI] 디버그 지급: '{itemId}' x{amount} 시도 -> {added}개 추가됨 (미추가분 {remaining}개)");
        }

        private void RefreshAll()
        {
            RefreshInventorySlots();
            RefreshQuickSlots();
            RefreshSelectedQuickSlot(playerInventory.selectedQuickSlotIndex);
        }

        private void RefreshInventorySlots()
        {
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
            for (int i = 0; i < quickSlots.Length; i++)
            {
                RefreshQuickSlot(i);
            }
        }

        private void RefreshQuickSlot(int index)
        {
            if (index < 0 || index >= quickSlots.Length || quickSlots[index] == null) return;

            quickSlots[index].SetStack(playerInventory.quickSlots.slots[index]);
            quickSlots[index].SetSelected(index == playerInventory.selectedQuickSlotIndex);
        }

        private void RefreshSelectedQuickSlot(int index)
        {
            for (int i = 0; i < quickSlots.Length; i++)
            {
                if (quickSlots[i] != null) quickSlots[i].SetSelected(i == index);
            }
        }

        private bool IsLockedQuickSlot(InventorySlotUI slot)
        {
            return slot != null && slot.group == SlotGroup.QuickSlot && playerInventory.IsQuickSlotLocked(slot.slotIndex);
        }
    }
}
