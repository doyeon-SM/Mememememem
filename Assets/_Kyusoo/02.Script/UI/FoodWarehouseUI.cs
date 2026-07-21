using HDY.Inventory;
using HDY.Item;
using HDY.Upgrade;
using KMS.InventoryDuped;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FoodWarehouseUI : MonoBehaviour, IInventorySlotOwner
{
    [Header("데이터 참조")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private WarehouseInventory warehouseInventory;
    [SerializeField] private ItemCatalogManager catalogManager;

    [Header("음식 창고 (왼쪽, 5 x n 스크롤 - 슬롯은 런타임 생성)")]
    [SerializeField] private ScrollRect storageScrollRect;
    [SerializeField] private RectTransform storageContentParent;
    [SerializeField] private InventorySlotUI storageSlotPrefab;
    [SerializeField] private WarehouseSortUI sortUI;

    [Header("음식 창고 업그레이드 (한 줄 - 5칸 확장)")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private WarehouseUpgrade warehouseUpgrade;

    [Header("통합 음식 가방 (오른쪽, 70칸 10x7 - 슬롯은 씬에 미리 배치)")]
    [SerializeField] private Transform inventoryGrid;

    [Header("공용 (드래그, 툴팁, 텍스트)")]
    [SerializeField] private ItemDragUI itemDragUI;
    [SerializeField] private ItemTooltipUI itemTooltipUI;
    [SerializeField] private TextMeshProUGUI totalHungerText;

    private class FilteredFoodSource
    {
        public SlotGroup originalGroup;
        public int originalIndex;
        public ItemStack stack;
    }

    private InventorySlotUI[] storageSlots;
    private InventorySlotUI[] inventorySlots;

    private List<FilteredFoodSource> rightFilteredFoods = new List<FilteredFoodSource>();
    private InventorySlotUI dragSource;
    public InventoryContainer FoodStorageContainer => ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.FoodStorageContainer : null;
    public InventoryContainer FoodBagContainer => ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.FoodBagContainer : null;
    public ItemCatalogManager CatalogManager => catalogManager;

    public static event Action OnFoodDataChanged;

    private void Awake()
    {
        if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
        catalogManager = ItemCatalogManager.Resolve(catalogManager);

        if (upgradeButton != null && warehouseUpgrade != null)
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
        HideItemTooltip();

        FetchFoodFromInventories();
        RefreshAll();

        if (TotalHungerManager.Instance != null)
        {
            UpdateHungerText(TotalHungerManager.Instance.TotalHungerPerMinute);
        }
    }

    private void OnEnable()
    {
        if (playerInventory != null) playerInventory.OnInventoryChanged += RefreshAll;
        if (warehouseInventory != null)
        {
            warehouseInventory.OnStorageChanged += RefreshAll;
            warehouseInventory.OnRowCountChanged += HandleRowCountChanged;
        }
        if (sortUI != null) sortUI.OnSortRequested += HandleSortRequested;

        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.OnTotalHungerChanged += UpdateHungerText;

            TotalHungerManager.Instance.RecalculateTotalHunger();
            UpdateHungerText(TotalHungerManager.Instance.TotalHungerPerMinute);
        }

        FetchFoodFromInventories();
        RefreshAll();
    }

    private void OnDisable()
    {
        if (playerInventory != null) playerInventory.OnInventoryChanged -= RefreshAll;
        if (warehouseInventory != null)
        {
            warehouseInventory.OnStorageChanged -= RefreshAll;
            warehouseInventory.OnRowCountChanged -= HandleRowCountChanged;
        }
        if (sortUI != null) sortUI.OnSortRequested -= HandleSortRequested;

        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.OnTotalHungerChanged -= UpdateHungerText;
        }
    }

    /// <summary>
    /// 외부 가방과 일반 창고의 원본 음식들을 완전히 긁어와 이관한 후 원본 칸을 정돈(Clear)시킵니다.
    /// </summary>
    private void FetchFoodFromInventories()
    {
        var bagContainer = FoodBagContainer;
        if (bagContainer == null || bagContainer.slots == null) return;

        if (playerInventory != null && playerInventory.inventory != null && playerInventory.inventory.slots != null)
        {
            for (int i = 0; i < playerInventory.inventory.slots.Length; i++)
            {
                ItemStack slot = playerInventory.inventory.slots[i];
                if (IsFoodItem(slot))
                {
                    int remaining = AddItemToContainer(bagContainer, slot.itemId, slot.amount);
                    if (remaining <= 0) slot.Clear(); 
                    else slot.amount = remaining;
                }
            }
        }

        if (warehouseInventory != null && warehouseInventory.storage != null && warehouseInventory.storage.slots != null)
        {
            for (int i = 0; i < warehouseInventory.storage.slots.Length; i++)
            {
                ItemStack slot = warehouseInventory.storage.slots[i];
                if (IsFoodItem(slot))
                {
                    int remaining = AddItemToContainer(bagContainer, slot.itemId, slot.amount);
                    if (remaining <= 0) slot.Clear(); 
                    else slot.amount = remaining;
                }
            }
        }

        if (ConsumeFoodSystem.Instance != null)
        {
            ConsumeFoodSystem.Instance.ProcessFoodConsumption(true);
        }

        OnFoodDataChanged?.Invoke();
    }

    private int AddItemToContainer(InventoryContainer container, string itemId, int amount)
    {
        int remaining = amount;
        for (int i = 0; i < container.slots.Length; i++)
        {
            if (container.slots[i] == null) container.slots[i] = new ItemStack();
            if (container.slots[i].itemId == itemId && !container.slots[i].IsEmpty)
            {
                container.slots[i].amount += remaining;
                return 0;
            }
        }
        for (int i = 0; i < container.slots.Length; i++)
        {
            if (container.slots[i] == null) container.slots[i] = new ItemStack();
            if (container.slots[i].IsEmpty)
            {
                container.slots[i].Set(itemId, remaining);
                return 0;
            }
        }
        return remaining;
    }

    public void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position)
    {
        if (source == null || stack == null || stack.IsEmpty) return;

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
        if (dragSource != null || itemTooltipUI == null) return;
        itemTooltipUI.Show(stack, position);
    }

    public void MoveItemTooltip(Vector2 position)
    {
        if (dragSource != null || itemTooltipUI == null) return;
        itemTooltipUI.Move(position);
    }

    public void HideItemTooltip()
    {
        if (itemTooltipUI != null) itemTooltipUI.Hide();
    }

    private void HandleUpgradeButtonClicked()
    {
        if (warehouseUpgrade != null && UpgradePopupUI.Instance != null)
        {
            UpgradePopupUI.Instance.Show(warehouseUpgrade);
        }
    }

    private void HandleRowCountChanged()
    {
        EnsureStorageSlotCount();
        RefreshAll();
    }

    private void HandleSortRequested(ItemSortCriteria criteria)
    {
        warehouseInventory?.ApplySort(criteria);
    }

    private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
    {
        var storageContainer = FoodStorageContainer;
        var bagContainer = FoodBagContainer;
        if (storageContainer == null || bagContainer == null) return;

        if (from.group == SlotGroup.Storage && to.group == SlotGroup.Storage)
        {
            bool moved = InventorySlotMoveHelper.MoveSlot(storageContainer, from.slotIndex, storageContainer, to.slotIndex, catalogManager);

            if (moved)
            {
                RefreshAll();
                ConsumeFoodSystem.Instance?.OnStorageToStorageMove();
            }
            return;
        }

        if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Inventory) return;

        if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Storage)
        {
            if (from.slotIndex >= rightFilteredFoods.Count) return;

            FilteredFoodSource src = rightFilteredFoods[from.slotIndex];

            bool moved = InventorySlotMoveHelper.MoveSlot(bagContainer, src.originalIndex, storageContainer, to.slotIndex, catalogManager);

            if (moved)
            {
                RefreshAll();
                if (ConsumeFoodSystem.Instance != null)
                {
                    ConsumeFoodSystem.Instance.OnRightToLeftMove();

                    OnFoodDataChanged?.Invoke();
                }
            }
            return;
        }

        if (from.group == SlotGroup.Storage && to.group == SlotGroup.Inventory)
        {
            ItemStack leftStack = storageContainer.slots[from.slotIndex];
            if (leftStack == null || leftStack.IsEmpty) return;

            int initialAmount = leftStack.amount;

            int remaining = AddItemToContainer(bagContainer, leftStack.itemId, initialAmount);
            int added = initialAmount - remaining;

            if (added > 0)
            {
                leftStack.amount -= added;
                if (leftStack.amount <= 0) leftStack.Clear();

                RefreshAll();
                ConsumeFoodSystem.Instance?.OnLeftToRightMove();
            }
            return;
        }
    }

    private void EnsureStorageSlotCount()
    {
        var storageContainer = FoodStorageContainer;
        if (storageSlotPrefab == null || storageContentParent == null || warehouseInventory == null || storageContainer == null) return;

        int upgradedRows = warehouseInventory.storage.height - warehouseInventory.StartingRows;
        int currentRows = 1 + Mathf.Max(0, upgradedRows);

        int required = 5 * currentRows;
        int current = storageSlots != null ? storageSlots.Length : 0;

        ItemStack[] oldSlots = storageContainer.slots;
        storageContainer.slots = new ItemStack[required];
        for (int i = 0; i < required; i++)
        {
            if (oldSlots != null && i < oldSlots.Length) storageContainer.slots[i] = oldSlots[i];
            else storageContainer.slots[i] = new ItemStack();
        }
        storageContainer.width = 5;
        storageContainer.height = required / 5;

        if (required <= current) return;
        var grown = new InventorySlotUI[required];
        for (int i = 0; i < current; i++) grown[i] = storageSlots[i];
        for (int i = current; i < required; i++)
        {
            var slot = Instantiate(storageSlotPrefab, storageContentParent);
            slot.Initialize(this, SlotGroup.Storage, i);
            grown[i] = slot;
        }
        storageSlots = grown;
    }

    private void BindPlayerSlots()
    {
        int maxBagCount = 70;
        inventorySlots = new InventorySlotUI[maxBagCount];

        if (inventoryGrid == null) return;

        for (int i = 0; i < maxBagCount && i < inventoryGrid.childCount; i++)
        {
            InventorySlotUI slotUI = inventoryGrid.GetChild(i).GetComponent<InventorySlotUI>();
            inventorySlots[i] = slotUI;

            if (slotUI != null) slotUI.Initialize(this, SlotGroup.Inventory, i);
        }
    }

    public void RefreshAllPanelsAndSlots()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        BuildRightFilteredFoodList();
        RefreshStorageSlots();
        RefreshInventorySlots();
    }

    private void BuildRightFilteredFoodList()
    {
        rightFilteredFoods.Clear();
        var bagContainer = FoodBagContainer;
        if (bagContainer == null || bagContainer.slots == null) return;

        for (int i = 0; i < bagContainer.slots.Length; i++)
        {
            ItemStack stack = bagContainer.slots[i];
            if (stack != null && !stack.IsEmpty)
            {
                rightFilteredFoods.Add(new FilteredFoodSource { originalGroup = SlotGroup.Inventory, originalIndex = i, stack = stack });
            }
        }
    }

    private void RefreshStorageSlots()
    {
        var storageContainer = FoodStorageContainer;
        if (storageSlots == null || storageContainer == null) return;

        for (int i = 0; i < storageSlots.Length; i++)
        {
            if (storageSlots[i] == null) continue;
            storageSlots[i].SetStack(storageContainer.slots[i]);
        }
    }

    private void RefreshInventorySlots()
    {
        if (inventorySlots == null) return;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;

            if (i < rightFilteredFoods.Count)
            {
                inventorySlots[i].SetStack(rightFilteredFoods[i].stack);
            }
            else
            {
                inventorySlots[i].SetStack(null);
            }
        }
    }

    private bool IsFoodItem(ItemStack stack)
    {
        if (stack == null || stack.IsEmpty) return false;
        if (catalogManager == null) return false;

        ItemData data = catalogManager.FindItemData(stack.itemId);
        return data != null && data.Category == ItemCategory.Food;
    }

    private void UpdateHungerText(int totalHunger)
    {
        if (totalHungerText != null)
        {
            totalHungerText.text = $"{totalHunger}";
        }
    }
}