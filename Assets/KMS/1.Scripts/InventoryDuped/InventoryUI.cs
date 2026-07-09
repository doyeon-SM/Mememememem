using UnityEngine;

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

            isInventoryOpen = false;
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            HideItemTooltip();
            RefreshAll();
        }

        private void OnDestroy()
        {
            UnsubscribeInventoryEvents();
            UnsubscribeInputEvents();
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
