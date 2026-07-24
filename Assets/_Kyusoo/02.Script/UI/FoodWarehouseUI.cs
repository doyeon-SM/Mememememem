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

    [Header("우측 인벤토리 영역들 (QuickSlotArea, InventoryArea, WarehouseArea)")]
    [SerializeField] private Transform quickSlotGrid;         
    [SerializeField] private Transform inventoryGrid;         
    [SerializeField] private Transform warehouseGrid;         
    [SerializeField] private InventorySlotUI warehouseSlotPrefab; 
    [SerializeField] private RectTransform warehouseScrollViewRect;// 우측 창고 Height 조절용

    [Header("공용 (드래그, 툴팁, 텍스트)")]
    [SerializeField] private ItemDragUI itemDragUI;
    [SerializeField] private ItemTooltipUI itemTooltipUI;
    [SerializeField] private TextMeshProUGUI totalHungerText;

    private InventorySlotUI[] storageSlots;   
    private InventorySlotUI[] quickSlots;     
    private InventorySlotUI[] inventorySlots; 
    private InventorySlotUI[] warehouseSlots; 

    private InventorySlotUI dragSource;

    public InventoryContainer FoodStorageContainer => ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.FoodStorageContainer : null;
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

        BindRightPreplacedSlots();
        EnsureFoodStorageSlotCount();
        EnsureRightWarehouseSlotCount();
        HideItemTooltip();

        RefreshAll();

        if (TotalHungerManager.Instance != null)
        {
            UpdateHungerText(TotalHungerManager.Instance.TotalHungerPerMinute);
        }
    }

    private void OnEnable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged += RefreshAll;
            playerInventory.OnQuickSlotChanged += HandleQuickSlotChanged;
        }

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

        RefreshAll();
    }

    private void OnDisable()
    {
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshAll;
            playerInventory.OnQuickSlotChanged -= HandleQuickSlotChanged;
        }

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
    /// 씬에 미리 배치되어 있는 퀵슬롯과 기본 인벤토리 슬롯들을 바인딩합니다.
    /// </summary>
    private void BindRightPreplacedSlots()
    {
        // 1. 퀵슬롯 바인딩 (SlotGroup.QuickSlot 으로 지정)
        if (quickSlotGrid != null)
        {
            int count = quickSlotGrid.childCount;
            quickSlots = new InventorySlotUI[count];
            for (int i = 0; i < count; i++)
            {
                var slotUI = quickSlotGrid.GetChild(i).GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.Initialize(this, SlotGroup.QuickSlot, i);
                    quickSlots[i] = slotUI;
                }
            }
        }

        // 2. 인벤토리 바인딩 (SlotGroup.Inventory 로 지정)
        if (inventoryGrid != null)
        {
            int count = inventoryGrid.childCount;
            inventorySlots = new InventorySlotUI[count];
            for (int i = 0; i < count; i++)
            {
                var slotUI = inventoryGrid.GetChild(i).GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.Initialize(this, SlotGroup.Inventory, i);
                    inventorySlots[i] = slotUI;
                }
            }
        }
    }

    /// <summary>
    /// 우측 일반 창고 슬롯 개수를 warehouseInventory.storage 크기에 맞춰 동적으로 확장/생성합니다.
    /// </summary>
    private void EnsureRightWarehouseSlotCount()
    {
        if (warehouseInventory == null || warehouseInventory.storage == null || warehouseGrid == null) return;

        var container = warehouseInventory.storage;
        int required = container.slots != null ? container.slots.Length : 0;
        int current = warehouseSlots != null ? warehouseSlots.Length : 0;

        if (required <= current)
        {
            UpdateWarehouseScrollHeight();
            return;
        }

        var grown = new InventorySlotUI[required];
        for (int i = 0; i < current; i++) grown[i] = warehouseSlots[i];

        InventorySlotUI prefabToUse = warehouseSlotPrefab != null ? warehouseSlotPrefab : storageSlotPrefab;

        for (int i = current; i < required; i++)
        {
            if (prefabToUse != null)
            {
                var slot = Instantiate(prefabToUse, warehouseGrid);
                slot.Initialize(this, SlotGroup.Storage, i);
                grown[i] = slot;
            }
        }
        warehouseSlots = grown;

        UpdateWarehouseScrollHeight();

        // 슬롯 생성 후 ScrollRect Content 레이아웃 즉시 재계산
        Canvas.ForceUpdateCanvases();
        if (warehouseGrid is RectTransform rectTransform)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
        }
    }

    /// <summary>
    /// 창고 슬롯 갯수(10개당 75)에 맞춰 우측 창고 스크롤 뷰의 높이 조절
    /// </summary>
    private void UpdateWarehouseScrollHeight()
    {
        if (warehouseScrollViewRect == null || warehouseSlots == null) return;

        int slotCount = warehouseSlots.Length;

        float targetHeight = (slotCount / 10f) * 75f;

        targetHeight = Mathf.Max(75f, targetHeight);

        Vector2 sizeDelta = warehouseScrollViewRect.sizeDelta;
        sizeDelta.y = targetHeight;
        warehouseScrollViewRect.sizeDelta = sizeDelta;
    }

    /// <summary>
    /// 좌측 음식 창고 슬롯 개수를 업그레이드 단계에 맞춰 동적으로 확장/생성합니다.
    /// </summary>
    private void EnsureFoodStorageSlotCount()
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

    public void RefreshAllPanelsAndSlots()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        EnsureFoodStorageSlotCount();
        EnsureRightWarehouseSlotCount();

        RefreshStorageSlots();
        RefreshQuickSlots();
        RefreshInventorySlots();
        RefreshWarehouseSlots();
    }

    private void RefreshStorageSlots()
    {
        var container = FoodStorageContainer;
        if (storageSlots == null || container == null || container.slots == null) return;

        for (int i = 0; i < storageSlots.Length; i++)
        {
            if (storageSlots[i] == null) continue;
            ItemStack stack = (i < container.slots.Length) ? container.slots[i] : null;
            storageSlots[i].SetStack(stack);
        }
    }

    private void RefreshQuickSlots()
    {
        if (quickSlots == null || playerInventory == null || playerInventory.quickSlots == null) return;
        var container = playerInventory.quickSlots;

        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (quickSlots[i] == null) continue;
            ItemStack stack = (container.slots != null && i < container.slots.Length) ? container.slots[i] : null;
            quickSlots[i].SetStack(stack);
        }
    }

    private void RefreshInventorySlots()
    {
        if (inventorySlots == null || playerInventory == null || playerInventory.inventory == null) return;
        var container = playerInventory.inventory;

        for (int i = 0; i < inventorySlots.Length; i++)
        {
            if (inventorySlots[i] == null) continue;
            ItemStack stack = (container.slots != null && i < container.slots.Length) ? container.slots[i] : null;
            inventorySlots[i].SetStack(stack);
        }
    }

    private void RefreshWarehouseSlots()
    {
        if (warehouseSlots == null || warehouseInventory == null || warehouseInventory.storage == null) return;
        var container = warehouseInventory.storage;

        for (int i = 0; i < warehouseSlots.Length; i++)
        {
            if (warehouseSlots[i] == null) continue;
            ItemStack stack = (container.slots != null && i < container.slots.Length) ? container.slots[i] : null;
            warehouseSlots[i].SetStack(stack);
        }
    }

    /// <summary>
    /// UI 슬롯 객체로부터 연동된 원본 InventoryContainer 및 인덱스를 자동 추출합니다.
    /// </summary>
    private bool GetContainerAndIndex(InventorySlotUI slot, out InventoryContainer container, out int index)
    {
        container = null;
        index = -1;
        if (slot == null) return false;

        // 1. 좌측 음식 창고
        if (storageSlots != null)
        {
            int idx = Array.IndexOf(storageSlots, slot);
            if (idx >= 0)
            {
                container = FoodStorageContainer;
                index = idx;
                return true;
            }
        }

        // 2. 우측 퀵슬롯
        if (quickSlots != null)
        {
            int idx = Array.IndexOf(quickSlots, slot);
            if (idx >= 0)
            {
                container = playerInventory?.quickSlots;
                index = idx;
                return true;
            }
        }

        // 3. 우측 일반 인벤토리
        if (inventorySlots != null)
        {
            int idx = Array.IndexOf(inventorySlots, slot);
            if (idx >= 0)
            {
                container = playerInventory?.inventory;
                index = idx;
                return true;
            }
        }

        // 4. 우측 일반 창고
        if (warehouseSlots != null)
        {
            int idx = Array.IndexOf(warehouseSlots, slot);
            if (idx >= 0)
            {
                container = warehouseInventory?.storage;
                index = idx;
                return true;
            }
        }

        return false;
    }

    private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
    {
        if (from == null || to == null) return;

        if (!GetContainerAndIndex(from, out var srcContainer, out int srcIndex)) return;
        if (!GetContainerAndIndex(to, out var dstContainer, out int dstIndex)) return;

        if (srcContainer == null || dstContainer == null || srcIndex < 0 || dstIndex < 0) return;

        // 🌟 퀵슬롯 사용중 잠금 검사
        if (srcContainer == playerInventory?.quickSlots && playerInventory.IsQuickSlotLocked(srcIndex)) return;
        if (dstContainer == playerInventory?.quickSlots && playerInventory.IsQuickSlotLocked(dstIndex)) return;

        bool isSourceFoodStorage = (srcContainer == FoodStorageContainer);
        bool isTargetFoodStorage = (dstContainer == FoodStorageContainer);

        // 🌟 음식 창고(좌측)로 들어오는 아이템은 반드시 Food 카테고리여야 함
        if (isTargetFoodStorage)
        {
            if (srcIndex >= srcContainer.slots.Length) return;
            ItemStack srcStack = srcContainer.slots[srcIndex];

            if (srcStack == null || srcStack.IsEmpty) return;

            if (!IsFoodItem(srcStack))
            {
                Debug.LogWarning("[FoodWarehouseUI] 음식 카테고리의 아이템만 음식 창고에 넣을 수 있습니다.");
                return;
            }
        }

        // 슬롯 실질 이동 실행
        bool moved = InventorySlotMoveHelper.MoveSlot(srcContainer, srcIndex, dstContainer, dstIndex, catalogManager);

        if (moved)
        {
            RefreshAll();

            // 음식이 들어오거나 빠졌을 때 소모 시스템 및 데이터 변경 이벤트 발행
            if (isSourceFoodStorage && isTargetFoodStorage)
            {
                ConsumeFoodSystem.Instance?.OnStorageToStorageMove();
            }
            else if (!isSourceFoodStorage && isTargetFoodStorage)
            {
                ConsumeFoodSystem.Instance?.OnRightToLeftMove();
                OnFoodDataChanged?.Invoke();
            }
            else if (isSourceFoodStorage && !isTargetFoodStorage)
            {
                ConsumeFoodSystem.Instance?.OnLeftToRightMove();
                OnFoodDataChanged?.Invoke();
            }

            // 🌟 [수정]: PlayerInventory & WarehouseInventory 실제 이벤트 발행 메서드 호출
            if (srcContainer == playerInventory?.inventory || srcContainer == playerInventory?.quickSlots ||
                dstContainer == playerInventory?.inventory || dstContainer == playerInventory?.quickSlots)
            {
                playerInventory?.PublishInventoryChanged();
            }

            if (srcContainer == warehouseInventory?.storage || dstContainer == warehouseInventory?.storage)
            {
                warehouseInventory?.PublishWarehouseChanged();
            }
        }
    }

    #region Drag & Drop & Tooltip Interface Handlers

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

    #endregion

    private void HandleUpgradeButtonClicked()
    {
        if (warehouseUpgrade != null && UpgradePopupUI.Instance != null)
        {
            UpgradePopupUI.Instance.Show(warehouseUpgrade);
        }
    }

    private void HandleRowCountChanged()
    {
        EnsureFoodStorageSlotCount();
        EnsureRightWarehouseSlotCount();
        RefreshAll();
    }

    private void HandleQuickSlotChanged(int slotIndex)
    {
        RefreshQuickSlots();
    }

    private void HandleSortRequested(ItemSortCriteria criteria)
    {
        warehouseInventory?.ApplySort(criteria);
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