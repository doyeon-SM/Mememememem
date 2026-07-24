using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using HDY.Inventory;
using HDY.Upgrade;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// [HDY 요청] IInventorySlotOwner를 구현해서, InventorySlotUI가 owner 타입과 무관하게 이 컨트롤러도
    /// 그대로 사용할 수 있게 했다(WarehouseUI와 동일한 인터페이스 계약). IInventorySlotClickOwner도 함께
    /// 구현하므로 실제 조작은 "클릭 앤 캐리 + 분할" 방식이고, 드래그 3종(BeginSlotDrag/MoveSlotDrag/
    /// EndSlotDrag)은 인터페이스 계약을 만족시키기 위한 빈 구현으로만 남아있다(InventorySlotUI가 owner를
    /// IInventorySlotClickOwner로 감지하면 드래그 이벤트 자체를 호출하지 않으므로 실제로 실행되지 않는다.
    /// WarehouseUI와 동일한 패턴).
    ///
    /// [HDY 요청 - 그리드 통일] 인벤토리/퀵슬롯 그리드의 바인딩/갱신/칸 잠금 표시/정렬·업그레이드 연결은
    /// PlayerInventoryGridController(HDY.Inventory, 공용)로 위임한다. WarehouseUI(창고 패널 안의 인벤토리
    /// 부분)도 완전히 동일한 컨트롤러를 사용하므로, 두 화면 모두 인벤토리 업그레이드(5칸씩 확장)와 칸 잠금
    /// 표시가 항상 같은 방식으로 동작한다. 이 클래스는 자기만의 커서(heldStack) 상태와 인벤토리+퀵슬롯+
    /// 트래시 사이의 클릭 라우팅만 직접 담당한다(창고가 없으므로 WarehouseUI보다 그룹 종류가 적다).
    ///
    /// [트래시 슬롯] WarehouseUI와 동일하게 TrashSlotController(공용)로 위임한다 - 병합 없이 무조건
    /// 덮어쓰는 임시 1칸이며, 손에 든 아이템을 놓을 자리가 전혀 없을 때 최종적으로 강제 수납된다.
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

        [Header("인벤토리 업그레이드 ([HDY 요청] 5칸씩 확장)")]
        [SerializeField] private Button upgradeButton;
        [SerializeField] private InventoryUpgrade inventoryUpgrade;
        [Tooltip("아직 언락되지 않은 인벤토리 칸의 표시 투명도(0~1). 낮을수록 더 흐리게(회색처럼) 보인다.")]
        [SerializeField] [Range(0f, 1f)] private float lockedSlotAlpha = 0.35f;

        [Header("트래시 ([HDY 요청] 덮어쓰기 전용 임시 1칸, 씬에 미리 배치)")]
        [SerializeField] private InventorySlotUI trashSlotUI;
        private readonly TrashSlotController trashController = new TrashSlotController();

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

        /// <summary>[HDY 요청 - 그리드 통일] 인벤토리/퀵슬롯 그리드 관리는 공용 컨트롤러에 위임한다(WarehouseUI와 동일 클래스 재사용).</summary>
        private PlayerInventoryGridController gridController;

        private InventoryQuantityPopupUI quantityPopup;

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

            if (playerInventory != null)
            {
                gridController = new PlayerInventoryGridController(playerInventory) { LockedSlotAlpha = lockedSlotAlpha };
            }
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
            trashController.Initialize(this, trashSlotUI);
            EnsureQuantityPopup();
            SubscribeInventoryEvents();
            SubscribeInputEvents();
            SubscribeDebugGiveItemButton();
            SubscribeSortUI();
            SubscribeUpgradeButton();

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
            UnsubscribeUpgradeButton();
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
            if (gridController.IsLocked(slot)) return;

            HideItemTooltip();

            if (button == PointerEventData.InputButton.Middle)
            {
                ShowQuantityPopup(slot, position);
                return;
            }

            if (heldStack == null || heldStack.IsEmpty)
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

        private void SubscribeUpgradeButton()
        {
            if (upgradeButton != null) upgradeButton.onClick.AddListener(HandleUpgradeButtonClicked);
        }

        private void UnsubscribeUpgradeButton()
        {
            if (upgradeButton != null) upgradeButton.onClick.RemoveListener(HandleUpgradeButtonClicked);
        }

        private void HandleUpgradeButtonClicked()
        {
            gridController.HandleUpgradeButtonClicked(inventoryUpgrade);
        }

        private void HandleSortRequested(InventorySortCriteria criteria)
        {
            if (!isInventoryOpen || playerInventory == null) return;

            bool isHoldingItem = heldStack != null && !heldStack.IsEmpty;
            bool isChoosingQuantity = quantityPopup != null && quantityPopup.IsOpen;
            if (isHoldingItem || isChoosingQuantity)
            {
                Debug.Log("[InventoryUI] 아이템 이동 또는 수량 선택 중에는 정렬할 수 없습니다.", this);
                return;
            }

            HideItemTooltip();
            gridController.HandleSortRequested(criteria);
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

        // ===================== 그룹별 라우팅 (Trash는 자체 처리, 나머지는 gridController에 위임) =====================

        private bool TryTakeFull(SlotGroup group, int index, out ItemStack taken)
        {
            if (group == SlotGroup.Trash) return trashController.TryTakeAmount(trashController.CurrentAmount, out taken);
            return gridController.TryTakeFull(group, index, out taken);
        }

        private bool TryTakeHalf(SlotGroup group, int index, out ItemStack taken)
        {
            if (group == SlotGroup.Trash) return trashController.TryTakeAmount(Mathf.CeilToInt(trashController.CurrentAmount * 0.5f), out taken);
            return gridController.TryTakeHalf(group, index, out taken);
        }

        private bool TryTakeAmount(SlotGroup group, int index, int amount, out ItemStack taken)
        {
            if (group == SlotGroup.Trash) return trashController.TryTakeAmount(amount, out taken);
            return gridController.TryTakeAmount(group, index, amount, out taken);
        }

        private bool TryPlaceFull(SlotGroup group, int index, ItemStack held)
        {
            if (group == SlotGroup.Trash) return trashController.Place(held, held.amount);
            return gridController.TryPlaceFull(group, index, held);
        }

        private bool TryPlaceOne(SlotGroup group, int index, ItemStack held)
        {
            if (group == SlotGroup.Trash) return trashController.Place(held, 1);
            return gridController.TryPlaceOne(group, index, held);
        }

        private bool TryGetSnapshot(SlotGroup group, int index, out ItemStack snapshot)
        {
            if (group == SlotGroup.Trash)
            {
                snapshot = trashController.Snapshot();
                return snapshot != null;
            }

            return gridController.TryGetSnapshot(group, index, out snapshot);
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

            // [HDY 요청 - 그리드 통일] 트래시 슬롯이 추가되며, 커서에 남은 아이템은 원래 있던 자리(트래시
            // 포함)를 우선 시도하고 그래도 자리가 없으면 트래시에 강제로 넣어 유실만은 막는다(WarehouseUI와
            // 동일한 안전장치). 예전에는 반환할 자리가 없으면 닫기 자체를 거부했지만, 트래시가 항상
            // 받아주는 최종 목적지가 된 지금은 그럴 필요가 없어졌다.
            if (!open && heldStack != null && !heldStack.IsEmpty)
            {
                if (heldOriginGroup == SlotGroup.Trash)
                {
                    trashController.Place(heldStack, heldStack.amount);
                }
                else if (playerInventory != null)
                {
                    playerInventory.TryReturnHeldStack(heldStack, heldOriginGroup, heldOriginIndex);
                }

                if (!heldStack.IsEmpty)
                {
                    trashController.ForcePlace(heldStack);
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

            var invSlots = gridController.InventorySlots;
            for (int i = 0; i < invSlots.Length && font == null; i++)
            {
                if (invSlots[i] != null && invSlots[i].amountText != null)
                {
                    font = invSlots[i].amountText.font;
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
            if (!TryGetSnapshot(slot.group, slot.slotIndex, out ItemStack snapshot)) return;

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
            if (!TryTakeAmount(group, index, amount, out ItemStack takenStack)) return;

            heldStack = takenStack;
            heldOriginGroup = group;
            heldOriginIndex = index;
            RefreshHeldItem(position);
        }

        private void BindSlots()
        {
            gridController.BindSlots(this, inventoryGrid, quickSlotRoot);
        }

        private void SubscribeInventoryEvents()
        {
            playerInventory.OnInventoryChanged += gridController.RefreshInventorySlots;
            playerInventory.OnQuickSlotChanged += gridController.RefreshQuickSlot;
            playerInventory.OnSelectedQuickSlotChanged += gridController.RefreshSelectedQuickSlot;
            playerInventory.OnInventorySlotCountChanged += gridController.RefreshInventorySlotLocks;
        }

        private void UnsubscribeInventoryEvents()
        {
            if (playerInventory == null || gridController == null) return;

            playerInventory.OnInventoryChanged -= gridController.RefreshInventorySlots;
            playerInventory.OnQuickSlotChanged -= gridController.RefreshQuickSlot;
            playerInventory.OnSelectedQuickSlotChanged -= gridController.RefreshSelectedQuickSlot;
            playerInventory.OnInventorySlotCountChanged -= gridController.RefreshInventorySlotLocks;
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
            gridController.RefreshInventorySlots();
            gridController.RefreshQuickSlots();
            gridController.RefreshSelectedQuickSlot(playerInventory.selectedQuickSlotIndex);
            trashController.Refresh();
            gridController.RefreshInventorySlotLocks();
        }
    }
}
