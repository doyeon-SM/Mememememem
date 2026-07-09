using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    public class InventoryUI : MonoBehaviour
    {
        public PlayerInventory playerInventory;

        public GameObject inventoryPanel;
        public Transform inventoryGrid;
        public Transform quickSlotRoot;
        public ItemDragUI itemDragUI;
        public ItemTooltipUI itemTooltipUI;

        [Header("KMS References")]
        [SerializeField] private KMS.PlayerInput playerInput;
        [SerializeField] private KMS.PlayerMovement playerMovement;
        [SerializeField] private KMS.PlayerCameraController cameraController;

        // [HDY 요청] Item_ID만으로 테스트 지급을 할 수 있는 디버그 UI 훅.
        // 다른 씬에서 테스트용 인벤토리/창고를 구성할 때, 월드에 픽업 오브젝트를 따로 안 놓아도
        // Item_ID 입력만으로 바로 지급해볼 수 있도록 하기 위함. 필드를 비워두면 그냥 동작하지 않는다(선택 사항).
        [Header("디버그 - Item_ID로 아이템 지급 (테스트용)")]
        [SerializeField] private TMP_InputField debugItemIdInput;
        [SerializeField] private TMP_InputField debugAmountInput;
        [SerializeField] private Button debugGiveItemButton;

        private InventorySlotUI[] inventorySlots;
        private InventorySlotUI[] quickSlots;

        private InventorySlotUI dragSource;
        private bool isInventoryOpen;
        private bool previousMovementEnabled = true;
        private bool previousGameplayInputBlocked;
        private bool previousCursorVisible;
        private CursorLockMode previousCursorLockState;

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
            SubscribeInventoryEvents();
            SubscribeInputEvents();
            SubscribeDebugGiveItemButton();

            isInventoryOpen = false;
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            HideItemTooltip();
            RefreshAll();
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
            UnsubscribeInputEvents();
            UnsubscribeDebugGiveItemButton();
        }

        private void OnDisable()
        {
            if (isInventoryOpen) SetInventoryOpen(false);
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
            if (!isInventoryOpen || dragSource != null || itemTooltipUI == null) return;

            itemTooltipUI.Show(stack, position);
        }

        public void MoveItemTooltip(Vector2 position)
        {
            if (!isInventoryOpen || dragSource != null || itemTooltipUI == null) return;

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

        private void ResolveReferences()
        {
            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (playerInput == null) playerInput = FindFirstObjectByType<KMS.PlayerInput>();
            if (playerMovement == null) playerMovement = FindFirstObjectByType<KMS.PlayerMovement>();
            if (cameraController == null) cameraController = FindFirstObjectByType<KMS.PlayerCameraController>();
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

        private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
        {
            if (playerInventory == null) return;
            if (IsLockedQuickSlot(from) || IsLockedQuickSlot(to)) return;

            if (!from.isQuickSlot && !to.isQuickSlot) playerInventory.MoveInventorySlot(from.slotIndex, to.slotIndex);
            else if (!from.isQuickSlot && to.isQuickSlot) playerInventory.MoveInventoryToQuickSlot(from.slotIndex, to.slotIndex);
            else if (from.isQuickSlot && !to.isQuickSlot) playerInventory.MoveQuickSlotToInventory(from.slotIndex, to.slotIndex);
            else playerInventory.MoveQuickSlot(from.slotIndex, to.slotIndex);
        }

        private void ToggleInventory()
        {
            SetInventoryOpen(!isInventoryOpen);
        }

        private void SetInventoryOpen(bool open)
        {
            if (isInventoryOpen == open) return;

            if (open)
            {
                previousMovementEnabled = playerMovement == null || playerMovement.IsMovementEnabled;
                previousGameplayInputBlocked = playerInput != null && playerInput.IsGameplayInputBlocked;
                previousCursorVisible = Cursor.visible;
                previousCursorLockState = Cursor.lockState;
            }

            isInventoryOpen = open;

            if (inventoryPanel != null) inventoryPanel.SetActive(open);
            if (playerInput != null) playerInput.SetGameplayInputBlocked(open ? true : previousGameplayInputBlocked);
            if (playerMovement != null) playerMovement.IsMovementEnabled = open ? false : previousMovementEnabled;

            if (cameraController != null)
            {
                cameraController.SetCursorLocked(!open && previousCursorLockState == CursorLockMode.Locked);
            }
            else
            {
                Cursor.visible = open ? true : previousCursorVisible;
                Cursor.lockState = open ? CursorLockMode.None : previousCursorLockState;
            }

            if (!open)
            {
                dragSource = null;
                if (itemDragUI != null) itemDragUI.Hide();
                HideItemTooltip();
            }
        }

        private void BindSlots()
        {
            inventorySlots = BindSlotGroup(inventoryGrid, playerInventory.inventory.slots.Length, false);
            quickSlots = BindSlotGroup(quickSlotRoot, playerInventory.quickSlots.slots.Length, true);
        }

        private InventorySlotUI[] BindSlotGroup(Transform root, int count, bool quickSlot)
        {
            InventorySlotUI[] result = new InventorySlotUI[count];

            if (root == null) return result;

            for (int i = 0; i < count && i < root.childCount; i++)
            {
                InventorySlotUI slotUI = root.GetChild(i).GetComponent<InventorySlotUI>();
                result[i] = slotUI;

                if (slotUI != null) slotUI.Initialize(this, quickSlot, i);
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

            playerInput.InventoryPressed += ToggleInventory;
            playerInput.QuickSlotPressed += SelectQuickSlot;
            playerInput.QuickSlotScrolled += SelectQuickSlotOffset;
        }

        private void UnsubscribeInputEvents()
        {
            if (playerInput == null) return;

            playerInput.InventoryPressed -= ToggleInventory;
            playerInput.QuickSlotPressed -= SelectQuickSlot;
            playerInput.QuickSlotScrolled -= SelectQuickSlotOffset;
        }

        /// <summary>[HDY 요청] 디버그 지급 버튼 클릭을 구독한다. 필드가 비어있으면 아무 것도 하지 않는다(선택 사항 기능).</summary>
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

        /// <summary>
        /// [HDY 요청] 입력창의 Item_ID/수량을 읽어 PlayerInventory.AddItem(string, int)로 지급을 시도한다.
        /// 수량 입력이 비어있거나 잘못되면 1개로 처리한다. 카탈로그에 없는 ID면 PlayerInventory가 경고 로그를 남기고
        /// 아무 것도 추가하지 않는다(여기서는 결과만 로그로 남김).
        /// </summary>
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
            return slot != null && slot.isQuickSlot && playerInventory.IsQuickSlotLocked(slot.slotIndex);
        }
    }
}
