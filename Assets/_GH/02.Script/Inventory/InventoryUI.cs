using UnityEngine;
using UnityEngine.InputSystem;

public class InventoryUI : MonoBehaviour
{
    public PlayerInventory playerInventory;

    public GameObject inventoryPanel;
    public Transform inventoryGrid;
    public Transform quickSlotRoot;
    public ItemDragUI itemDragUI;
    public ItemTooltipUI itemTooltipUI;

    private InventorySlotUI[] inventorySlots;
    private InventorySlotUI[] quickSlots;

    private InventorySlotUI dragSource;
    private bool isInventoryOpen;

    private void SelectQuickSlot(int index)
    {
        playerInventory.SelectQuickSlot(index);
    }

    private void Start()
    {
        BindSlots();
        SubscribeEvents();

        //SetInventoryOpen(false);
        isInventoryOpen = false; // SetInventoryOpen 없애면서 Start로 따로 뺌
        inventoryPanel.SetActive(false);// SetInventoryOpen 없애면서 Start로 따로 뺌
        HideItemTooltip(); // SetInventoryOpen 없애면서 Start로 따로 뺌
        RefreshAll();
        InputManager.Instance.OnInventoryPressed += ToggleInventory;
        InputManager.Instance.OnQuickSlotPressed += SelectQuickSlot;
    }
    private void OnDestroy()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshInventorySlots;
            playerInventory.OnQuickSlotChanged -= RefreshQuickSlot;
            playerInventory.OnSelectedQuickSlotChanged -= RefreshSelectedQuickSlot;
            InputManager.Instance.OnInventoryPressed -= ToggleInventory;
            InputManager.Instance.OnQuickSlotPressed -= SelectQuickSlot;
        }
        
    }

    public void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position)
    {
        if (!isInventoryOpen || source == null || stack == null || stack.IsEmpty) return;
        if (IsLockedQuickSlot(source)) return;

        HideItemTooltip();

        dragSource = source;
        itemDragUI.Show(stack, position);
    }

    public void MoveSlotDrag(Vector2 position)
    {
        if (dragSource == null) return;

        itemDragUI.Move(position);
    }

    public void EndSlotDrag(InventorySlotUI target)
    {
        if (dragSource != null && target != null && dragSource != target)
        {
            MoveBetweenSlots(dragSource, target);
        }

        dragSource = null;
        itemDragUI.Hide();
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

    private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
    {
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
        isInventoryOpen = open;

        inventoryPanel.SetActive(open);

        if (!open) HideItemTooltip();

        //Cursor.visible = open;
        //Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
/*        if(UIStateManager.Instance != null)
        {
            if (open) UIStateManager.Instance.OpenUI(UIType.Inventory);
            else UIStateManager.Instance.CloseUI(UIType.Inventory);
        }*/
    }

    private void BindSlots()
    {
        inventorySlots = BindSlotGroup(inventoryGrid, playerInventory.inventory.slots.Length, false);
        quickSlots = BindSlotGroup(quickSlotRoot, playerInventory.quickSlots.slots.Length, true);
    }

    private InventorySlotUI[] BindSlotGroup(Transform root, int count, bool quickSlot)
    {
        InventorySlotUI[] result = new InventorySlotUI[count];

        for (int i = 0; i < count && i < root.childCount; i++)
        {
            InventorySlotUI slotUI = root.GetChild(i).GetComponent<InventorySlotUI>();
            result[i] = slotUI;

            if (slotUI != null) slotUI.Initialize(this, quickSlot, i);
        }

        return result;
    }

    private void SubscribeEvents()
    {
        playerInventory.OnInventoryChanged += RefreshInventorySlots;
        playerInventory.OnQuickSlotChanged += RefreshQuickSlot;
        playerInventory.OnSelectedQuickSlotChanged += RefreshSelectedQuickSlot;
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

    // 잠긴 슬롯인지 확인
    private bool IsLockedQuickSlot(InventorySlotUI slot)
    {
        return slot != null && slot.isQuickSlot && playerInventory.IsQuickSlotLocked(slot.slotIndex);
    }
    public void OnCloseButtonClick()
    {
        isInventoryOpen = false;
    }


    public void Open()
    {
        inventoryPanel.SetActive(true);
    }

    public void Close()
    {
        inventoryPanel.SetActive(false);
        isInventoryOpen = false;
        HideItemTooltip();
    }

}
