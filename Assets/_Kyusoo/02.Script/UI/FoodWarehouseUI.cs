using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using KMS.InventoryDuped;
using HDY.Item;
using HDY.Upgrade;
using HDY.Inventory;
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

    [Header("공용 (드래그 고스트 / 툴팁)")]
    [SerializeField] private ItemDragUI itemDragUI;
    [SerializeField] private ItemTooltipUI itemTooltipUI;

    private class FilteredFoodSource
    {
        public SlotGroup originalGroup; 
        public int originalIndex;       
        public ItemStack stack;         
    }

    private InventorySlotUI[] storageSlots;
    private InventorySlotUI[] inventorySlots;

    private InventoryContainer foodStorageContainer = new InventoryContainer();

    private List<FilteredFoodSource> rightFilteredFoods = new List<FilteredFoodSource>();

    private InventorySlotUI dragSource;

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

        RefreshAll();
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
        if (from.group == SlotGroup.Storage && to.group == SlotGroup.Storage)
        {
            InventorySlotMoveHelper.MoveSlot(foodStorageContainer, from.slotIndex, foodStorageContainer, to.slotIndex, catalogManager);
            RefreshAll();
            return;
        }

        if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Inventory) return;

        if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Storage)
        {
            if (from.slotIndex >= rightFilteredFoods.Count) return;

            FilteredFoodSource src = rightFilteredFoods[from.slotIndex];
            InventoryContainer realFromContainer = (src.originalGroup == SlotGroup.Inventory) ? playerInventory.inventory : warehouseInventory.storage;

            InventorySlotMoveHelper.MoveSlot(realFromContainer, src.originalIndex, foodStorageContainer, to.slotIndex, catalogManager);
            RefreshAll();
            return;
        }

        if (from.group == SlotGroup.Storage && to.group == SlotGroup.Inventory)
        {
            ItemStack leftStack = foodStorageContainer.slots[from.slotIndex];
            if (leftStack == null || leftStack.IsEmpty) return;

            int initialAmount = leftStack.amount;

            int remaining = playerInventory.AddItem(leftStack.itemId, initialAmount);
            int added = initialAmount - remaining;

            if (added > 0)
            {
                leftStack.amount -= added;
                if (leftStack.amount <= 0) leftStack.Clear();
            }

            RefreshAll();
            return;
        }
    }

    /// <summary>
    /// 가로 5칸 고정, 업그레이드 행 수에 맞춰 슬롯을 개설
    /// </summary>
    private void EnsureStorageSlotCount()
    {
        if (storageSlotPrefab == null || storageContentParent == null || warehouseInventory == null) return;

        int required = 5 * warehouseInventory.storage.height;
        int current = storageSlots != null ? storageSlots.Length : 0;

        ItemStack[] oldSlots = foodStorageContainer.slots;
        foodStorageContainer.slots = new ItemStack[required];
        for (int i = 0; i < required; i++)
        {
            if (oldSlots != null && i < oldSlots.Length) foodStorageContainer.slots[i] = oldSlots[i];
            else foodStorageContainer.slots[i] = new ItemStack();
        }
        foodStorageContainer.width = 5;
        foodStorageContainer.height = required / 5;

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

    /// <summary>
    /// 우측 인벤토리 70칸 격자 생성
    /// </summary>
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


    private void RefreshAll()
    {
        BuildRightFilteredFoodList();

        RefreshStorageSlots();

        RefreshInventorySlots();
    }

    /// <summary>
    /// 가방과 창고를 순회하여 'Food' 카테고리만 찾아내 우측에 가져오기
    /// </summary>
    private void BuildRightFilteredFoodList()
    {
        rightFilteredFoods.Clear();

        if (playerInventory != null && playerInventory.inventory.slots != null)
        {
            for (int i = 0; i < playerInventory.inventory.slots.Length; i++)
            {
                ItemStack stack = playerInventory.inventory.slots[i];
                if (IsFoodItem(stack))
                {
                    rightFilteredFoods.Add(new FilteredFoodSource { originalGroup = SlotGroup.Inventory, originalIndex = i, stack = stack });
                }
            }
        }

        if (warehouseInventory != null && warehouseInventory.storage.slots != null)
        {
            for (int i = 0; i < warehouseInventory.storage.slots.Length; i++)
            {
                ItemStack stack = warehouseInventory.storage.slots[i];
                if (IsFoodItem(stack))
                {
                    rightFilteredFoods.Add(new FilteredFoodSource { originalGroup = SlotGroup.Storage, originalIndex = i, stack = stack });
                }
            }
        }
    }

    private void RefreshStorageSlots()
    {
        if (storageSlots == null) return;

        for (int i = 0; i < storageSlots.Length; i++)
        {
            if (storageSlots[i] == null) continue;
            storageSlots[i].SetStack(foodStorageContainer.slots[i]);
        }
    }

    /// <summary>
    /// 우측 인벤토리 슬롯에 가방+창고에서 수집된 통합 음식 리스트를 차례대로 순서대로 가져오기
    /// </summary>
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
}
