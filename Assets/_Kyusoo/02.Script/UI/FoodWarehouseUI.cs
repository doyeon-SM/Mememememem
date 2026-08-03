using HDY.Inventory;
using HDY.Item;
using HDY.Upgrade;
using KMS.InventoryDuped;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// [HDY 요청] 음식 창고(왼쪽, 밥통) + 퀵슬롯/인벤토리/일반 창고(오른쪽) 통합 화면 컨트롤러.
///
/// [마우스 편의 기능 통일] WarehouseUI(HDY)/InventoryUI(KMS)와 동일한 "클릭 앤 캐리 + 분할" 모델로
/// 교체했다. IInventorySlotClickOwner를 구현하면 InventorySlotUI가 드래그 이벤트를 호출하지 않고
/// 클릭만 위임하므로(슬롯 위 드래그는 자동으로 ScrollRect 스크롤로 넘어간다), 기존 드래그 전용 로직
/// (BeginSlotDrag/MoveSlotDrag/EndSlotDrag)은 인터페이스 계약만 만족시키는 빈 구현으로 남겼다.
///
/// - 좌클릭: 커서 비었으면 전체를 집고, 커서에 있으면 전체를 놓는다(빈칸=이동, 같은아이템=병합, 다른아이템=교환)
/// - 우클릭: 커서 비었으면 절반을 집고, 커서에 있으면 1개만 놓는다(교환 불가)
/// - 중클릭: 수량 팝업(InventoryQuantityPopupUI, KMS 범용 컴포넌트 재사용)을 열어 정확한 수량만 집는다
/// - Shift+좌클릭(커서 비어있을 때): 반대쪽으로 스택 전체를 옮긴다. 밥통(음식 창고)에서 누르면
///   퀵슬롯 -> 인벤토리 -> 창고 순으로 자리를 찾고, 밥통이 아닌 곳에서 누르면 밥통 하나가 목적지다
///   (밥통으로 들어가는 이동은 Food 카테고리가 아니면 아무 일도 하지 않는다).
/// - Ctrl+좌클릭(커서 비어있을 때): 클릭한 슬롯이 속한 쪽 전체에서 같은 Item_ID를 모아 반대쪽에
///   낮은 index부터 채운다(반대쪽이 퀵슬롯+인벤토리+창고 묶음이면 퀵슬롯 -> 인벤토리 -> 창고 순).
///
/// [SlotGroup.Storage 중복 태깅 주의] 이 패널은 창고류 슬롯이 두 종류다 - 좌측 "음식 창고"
/// (FoodStorageContainer)와 우측 "일반 창고"(warehouseInventory.storage) - 인데 둘 다 SlotGroup.Storage로
/// 초기화되어 있어(EnsureFoodStorageSlotCount/EnsureRightWarehouseSlotCount 참고) group 값만으로는
/// 구분할 수 없다. 그래서 모든 라우팅은 slot.group이 아니라 GetContainerAndIndex()로 얻은 실제
/// InventoryContainer 참조 동일성으로 구분한다.
///
/// [슬롯 생성 방식 통일] 퀵슬롯/인벤토리도 음식 창고/일반 창고와 동일하게 전부 런타임 Instantiate
/// 방식이다. 인벤토리는 playerInventory.UnlockedInventorySlotCount만큼만 생성하고, 업그레이드로 더
/// 언락되면(OnInventorySlotCountChanged) 모자란 만큼만 추가로 Instantiate한다.
///
/// [스크롤뷰 1개로 통합 - HDY 요청] 예전에는 퀵슬롯/인벤토리/창고 영역마다 각자 ScrollRect를 두려고
/// 했는데, 스크롤바가 여러 개 겹쳐 보여서 이동이 불편하다는 피드백을 받고 구조를 바꿨다. 이제 인벤토리
/// 그리드와 창고 그리드는 (Text 라벨 + GridLayoutGroup + ContentSizeFitter)만 있는 "순수 그리드"이고,
/// 실제 스크롤은 그 둘을 함께 담는 하나의 마스터 ScrollView(Vertical Layout Group Content)가 담당한다.
/// 그래서 창고 쪽 스크롤 높이를 직접 계산해주던 UpdateWarehouseScrollHeight()/warehouseScrollViewRect는
/// 제거했다 - 각 그리드의 ContentSizeFitter(Vertical: Preferred Size)가 자기 높이를 보고하면 마스터
/// Content의 Vertical Layout Group이 알아서 전체 높이를 재계산한다.
///
/// [트래시 슬롯 없음] WarehouseUI/InventoryUI와 달리 이 패널은 트래시(휴지통) 칸을 두지 않는다
/// ([HDY 요청]). ESC 닫기 안전장치가 커서에 남은 아이템을 반환할 곳을 못 찾는 경우(밥통/창고/인벤토리/
/// 퀵슬롯이 전부 꽉 찬 극단적인 경우)는 경고 로그만 남기고 넘어간다.
///
/// [ESC 닫기 안전장치] 이 패널은 PanelManager가 SetActive(false)로 직접 닫는 구조라
/// WarehouseUI/InventoryUI처럼 "닫기 자체를 거부"할 수 없다. 대신 OnDisable에서 커서에 남은 아이템을
/// 원래 있던 자리 우선, 그다음 반대쪽까지 확장해서 최대한 되돌린다.
///
/// [음식 카테고리 제약 유지] 밥통(음식 창고)에는 Food 카테고리 아이템만 들어갈 수 있다는 기존 규칙은
/// 클릭 배치/Shift/Ctrl/안전장치 반환 전 과정에서 동일하게 유지한다(IsFoodItem 게이트).
///
/// [재사용] InventorySlotUI, ItemDragUI, ItemTooltipUI, InventoryQuantityPopupUI를 그대로 가져다 쓴다.
/// warehouseInventory/playerInventory 쪽 컨테이너는 각자의 Try* API를 그대로 호출하고(내부에서 자체
/// 변경 이벤트를 발행하므로 이 클래스가 따로 갱신을 부르지 않아도 된다), FoodStorageContainer는 래퍼
/// 클래스가 없는 원시 컨테이너라 동일한 알고리즘을 이 클래스 안에 직접 복제해서 쓰고, 성공할 때마다
/// RefreshStorageSlots()를 직접 호출해 갱신한다.
/// </summary>
public class FoodWarehouseUI : MonoBehaviour, IInventorySlotOwner, IInventorySlotClickOwner
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

    [Header("음식 창고 업그레이드 (1칸 확장)")]
    [SerializeField] private Button upgradeButton;
    [SerializeField] private FoodWarehouseUpgrade foodWarehouseUpgrade;

    private int extraUpgradedSlotCount = 0;

    [Header("우측 퀵슬롯 (고정 10칸, 슬롯은 런타임 생성 - [HDY 요청])")]
    [SerializeField] private Transform quickSlotGrid;
    [SerializeField] private InventorySlotUI quickSlotPrefab;

    [Header("우측 인벤토리 (마스터 스크롤 안 순수 그리드 - 언락된 칸만큼만 런타임 생성, [HDY 요청])")]
    [SerializeField] private Transform inventoryGrid;
    [SerializeField] private InventorySlotUI inventorySlotPrefab;

    [Header("우측 일반 창고 (마스터 스크롤 안 순수 그리드 - 슬롯은 런타임 생성)")]
    [SerializeField] private Transform warehouseGrid;
    [SerializeField] private InventorySlotUI warehouseSlotPrefab;

    [Header("공용 (드래그 고스트, 툴팁, 수량 팝업, 텍스트)")]
    [SerializeField] private ItemDragUI itemDragUI;
    [SerializeField] private ItemTooltipUI itemTooltipUI;
    [SerializeField] private TextMeshProUGUI totalHungerText;



    private InventorySlotUI[] storageSlots;   
    private InventorySlotUI[] quickSlots;     
    private InventorySlotUI[] inventorySlots; 
    private InventorySlotUI[] warehouseSlots; 

    /// <summary>[HDY 요청 - 클릭 앤 캐리] 커서(손)에 든 스택과 원래 있던 위치.</summary>
    private ItemStack heldStack;
    private InventoryContainer heldOriginContainer;
    private int heldOriginIndex = -1;

    private InventoryQuantityPopupUI quantityPopup;

    public InventoryContainer FoodStorageContainer => ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.FoodStorageContainer : null;
    public ItemCatalogManager CatalogManager => catalogManager;

    public static event Action OnFoodDataChanged;

    private void Awake()
    {
        if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
        if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();
        catalogManager = ItemCatalogManager.Resolve(catalogManager);

        if (upgradeButton != null && foodWarehouseUpgrade != null)
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

        EnsureQuickSlotCount();
        EnsureInventorySlotCount();
        EnsureFoodStorageSlotCount();
        EnsureRightWarehouseSlotCount();
        EnsureQuantityPopup();
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
            playerInventory.OnInventorySlotCountChanged += HandleInventorySlotCountChanged;
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
            playerInventory.OnInventorySlotCountChanged -= HandleInventorySlotCountChanged;
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

        // [HDY 요청 - ESC 닫기 안전장치] PanelManager가 SetActive(false)로 직접 닫으므로 닫기 자체를
        // 거부할 수 없다. 열려있는 수량 팝업을 취소하고, 커서에 남은 아이템은 원래 있던 자리 우선,
        // 그다음 반대쪽까지 확장해서 최대한 되돌린다(트래시가 없으므로 정말 자리가 없으면 경고만 남긴다).
        if (quantityPopup != null && quantityPopup.IsOpen)
        {
            quantityPopup.Cancel();
        }

        if (heldStack != null && !heldStack.IsEmpty)
        {
            TryReturnHeldStackAnywhere(heldStack, heldOriginContainer, heldOriginIndex);

            if (!heldStack.IsEmpty)
            {
                Debug.LogWarning($"[FoodWarehouseUI] 패널이 닫히는데 커서에 남은 '{heldStack.itemId}' x{heldStack.amount}을(를) 되돌릴 자리를 찾지 못했습니다.");
            }

            ClearHeldItem();
        }
    }

    private void Update()
    {
        if (heldStack == null || heldStack.IsEmpty || itemDragUI == null || Mouse.current == null) return;
        itemDragUI.Move(Mouse.current.position.ReadValue());
    }

    /// <summary>
    /// [HDY 요청] 우측 퀵슬롯 개수를 playerInventory.quickSlots 크기에 맞춰 런타임 생성합니다.
    /// 퀵슬롯은 업그레이드 개념이 없어 보통 한 번만 필요한 만큼 생성되고 이후 변하지 않습니다.
    /// </summary>
    private void EnsureQuickSlotCount()
    {
        if (quickSlotPrefab == null || quickSlotGrid == null || playerInventory == null || playerInventory.quickSlots == null) return;

        int required = playerInventory.quickSlots.slots != null ? playerInventory.quickSlots.slots.Length : 0;
        int current = quickSlots != null ? quickSlots.Length : 0;

        if (required <= current) return;

        var grown = new InventorySlotUI[required];
        for (int i = 0; i < current; i++) grown[i] = quickSlots[i];

        for (int i = current; i < required; i++)
        {
            var slot = Instantiate(quickSlotPrefab, quickSlotGrid);
            slot.Initialize(this, SlotGroup.QuickSlot, i);
            grown[i] = slot;
        }
        quickSlots = grown;

        Canvas.ForceUpdateCanvases();
        if (quickSlotGrid is RectTransform quickRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(quickRect);
        }
    }

    /// <summary>
    /// [HDY 요청] 우측 인벤토리 슬롯 개수를 playerInventory.UnlockedInventorySlotCount(언락된 칸 수)에
    /// 맞춰 런타임 생성합니다. 아직 언락되지 않은 칸은 애초에 생성하지 않으므로 별도의 잠금 표시가 필요 없습니다.
    /// 업그레이드로 더 언락되면(OnInventorySlotCountChanged) 모자란 만큼만 추가로 Instantiate합니다.
    /// inventoryGrid는 이제 자체 스크롤 없는 순수 GridLayoutGroup이라, 레이아웃 재계산이 마스터
    /// ScrollView의 Vertical Layout Group까지 전파되도록 부모 체인까지 강제로 재빌드한다.
    /// </summary>
    private void EnsureInventorySlotCount()
    {
        if (inventorySlotPrefab == null || inventoryGrid == null || playerInventory == null) return;

        int required = playerInventory.UnlockedInventorySlotCount;
        int current = inventorySlots != null ? inventorySlots.Length : 0;

        if (required <= current) return;

        var grown = new InventorySlotUI[required];
        for (int i = 0; i < current; i++) grown[i] = inventorySlots[i];

        for (int i = current; i < required; i++)
        {
            var slot = Instantiate(inventorySlotPrefab, inventoryGrid);
            slot.Initialize(this, SlotGroup.Inventory, i);
            grown[i] = slot;
        }
        inventorySlots = grown;

        RebuildGridAndAncestorLayout(inventoryGrid);
    }

    /// <summary>
    /// 우측 일반 창고 슬롯 개수를 warehouseInventory.storage 크기에 맞춰 동적으로 확장/생성합니다.
    /// warehouseGrid도 이제 자체 스크롤 없는 순수 GridLayoutGroup이라, 레이아웃 재계산이 마스터
    /// ScrollView까지 전파되도록 부모 체인까지 강제로 재빌드한다(예전의 수동 높이 계산은 제거).
    /// </summary>
    private void EnsureRightWarehouseSlotCount()
    {
        if (warehouseInventory == null || warehouseInventory.storage == null || warehouseGrid == null) return;

        var container = warehouseInventory.storage;
        int required = container.slots != null ? container.slots.Length : 0;
        int current = warehouseSlots != null ? warehouseSlots.Length : 0;

        if (required <= current) return;

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

        RebuildGridAndAncestorLayout(warehouseGrid);
    }

    /// <summary>
    /// [HDY 요청] 순수 그리드(자체 ScrollRect 없음)에 슬롯을 추가한 뒤, 그리드 자신과 그 위의 모든
    /// 조상(마스터 ScrollView의 Content 등)까지 즉시 레이아웃을 재계산한다. ContentSizeFitter는 보통
    /// 다음 프레임에 알아서 반영되지만, 슬롯이 막 늘어난 그 프레임에 한 칸 잘려 보이는 걸 방지하기 위해
    /// Instantiate 직후 강제로 밀어준다.
    /// </summary>
    private void RebuildGridAndAncestorLayout(Transform grid)
    {
        Canvas.ForceUpdateCanvases();

        if (!(grid is RectTransform gridRect)) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);

        Transform parent = gridRect.parent;
        while (parent != null)
        {
            if (parent is RectTransform parentRect && parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
            parent = parent.parent;
        }
    }

    /// <summary>
    /// 좌측 음식 창고 슬롯 개수를 업그레이드 단계에 맞춰 동적으로 확장/생성합니다.
    /// </summary>
    private void EnsureFoodStorageSlotCount()
    {
        var storageContainer = FoodStorageContainer;
        if (storageSlotPrefab == null || storageContentParent == null || storageContainer == null) return;

        storageContainer.width = 10;

        if (storageContainer.slots == null || storageContainer.slots.Length == 0)
        {
            ItemStack[] initialSlots = new ItemStack[5];
            for (int i = 0; i < 5; i++)
            {
                initialSlots[i] = new ItemStack();
            }
            storageContainer.slots = initialSlots;
        }

        int required = storageContainer.slots.Length; 
        int current = storageSlots != null ? storageSlots.Length : 0;

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

        RebuildGridAndAncestorLayout(storageContentParent);
    }

    public void RefreshAllPanelsAndSlots()
    {
        RefreshAll();
    }

    private void RefreshAll()
    {
        EnsureQuickSlotCount();
        EnsureInventorySlotCount();
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

    private void HandleInventorySlotCountChanged()
    {
        EnsureInventorySlotCount();
        RefreshInventorySlots();
    }

    /// <summary>
    /// UI 슬롯 객체로부터 연동된 원본 InventoryContainer 및 인덱스를 자동 추출합니다.
    /// [HDY 요청] SlotGroup.Storage가 음식 창고/일반 창고 양쪽에 쓰이므로, group이 아니라 이 배열
    /// 소속 여부로 컨테이너를 구분한다. 모든 클릭/Shift/Ctrl 라우팅이 이 메서드를 거친다.
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

    /// <summary>slot이 속한 InventoryContainer를 반환한다(찾지 못하면 null).</summary>
    private InventoryContainer ResolveContainer(InventorySlotUI slot)
    {
        return GetContainerAndIndex(slot, out var container, out _) ? container : null;
    }

    #region IInventorySlotClickOwner - 클릭 앤 캐리 + 분할

    public void ClickSlot(InventorySlotUI slot, PointerEventData.InputButton button, Vector2 position)
    {
        if (slot == null) return;
        if (quantityPopup != null && quantityPopup.IsOpen) return;
        if (button != PointerEventData.InputButton.Left &&
            button != PointerEventData.InputButton.Right &&
            button != PointerEventData.InputButton.Middle) return;
        if (IsSlotLocked(slot)) return;

        HideItemTooltip();

        bool cursorEmpty = heldStack == null || heldStack.IsEmpty;

        // 밥통(음식 창고) <-> 퀵슬롯+인벤토리+창고 단축 이동: Shift/Ctrl+좌클릭 (커서 비었을 때)
        if (cursorEmpty && button == PointerEventData.InputButton.Left &&
            IsModifierClick(out bool isShift, out bool isCtrl))
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
                ? TryTakeFullFromSlot(slot, out ItemStack takenStack)
                : TryTakeHalfFromSlot(slot, out takenStack);

            if (!taken) return;

            heldStack = takenStack;
            heldOriginContainer = ResolveContainer(slot);
            heldOriginIndex = slot.slotIndex;
        }
        else
        {
            bool placed = button == PointerEventData.InputButton.Left
                ? TryPlaceFullIntoSlot(slot, heldStack)
                : TryPlaceOneIntoSlot(slot, heldStack);

            if (!placed) return;

            NotifyFoodBoundaryIfNeeded(heldOriginContainer, ResolveContainer(slot));
        }

        RefreshHeldItem(position);
    }

    /// <summary>[HDY 요청] 퀵슬롯의 "사용중 임시 예약" 잠금만 확인한다. 인벤토리는 언락된 칸만 애초에
    /// 생성되므로 잠금 판정이 필요 없다(밥통/일반 창고도 잠금 개념이 없다).</summary>
    private bool IsSlotLocked(InventorySlotUI slot)
    {
        if (slot.group == SlotGroup.QuickSlot) return playerInventory != null && playerInventory.IsQuickSlotLocked(slot.slotIndex);
        return false;
    }

    private static bool IsModifierClick(out bool isShift, out bool isCtrl)
    {
        Keyboard keyboard = Keyboard.current;

        isShift = keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        isCtrl = keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);

        return isShift || isCtrl;
    }

    #endregion

    #region Shift/Ctrl 단축 이동 (밥통 <-> 퀵슬롯+인벤토리+창고, 이 순서로 우선순위)

    /// <summary>Shift+좌클릭: 반대쪽으로 스택 전체를 옮긴다.</summary>
    private void HandleShiftClick(InventorySlotUI slot)
    {
        if (!GetContainerAndIndex(slot, out var fromContainer, out int fromIndex)) return;

        if (fromContainer == FoodStorageContainer)
        {
            MoveWholeStackToLowestIndex(fromContainer, fromIndex, playerInventory?.quickSlots, playerInventory?.inventory, warehouseInventory?.storage);
        }
        else
        {
            // Food 카테고리가 아니면 MoveWholeStackToLowestIndex 안에서 걸러진다.
            MoveWholeStackToLowestIndex(fromContainer, fromIndex, FoodStorageContainer);
        }
    }

    /// <summary>Ctrl+좌클릭: 클릭한 슬롯이 속한 쪽 전체에서 같은 Item_ID를 모아 반대쪽에 채운다.</summary>
    private void HandleCtrlClick(InventorySlotUI slot)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) return;
        if (!TryGetSnapshot(container, index, out ItemStack snapshot)) return;

        if (container == FoodStorageContainer)
        {
            CollectAndFill(new[] { FoodStorageContainer }, snapshot.itemId,
                new[] { playerInventory?.quickSlots, playerInventory?.inventory, warehouseInventory?.storage });
        }
        else
        {
            if (!IsFoodItem(snapshot)) return; // Food가 아니면 밥통으로 모을 수 없다.

            CollectAndFill(new[] { playerInventory?.quickSlots, playerInventory?.inventory, warehouseInventory?.storage },
                snapshot.itemId, new[] { FoodStorageContainer });
        }
    }

    /// <summary>fromContainer[fromIndex]의 스택 전체를, 목적지 후보들(순서대로) 중 가장 먼저 발견되는
    /// "병합 가능하거나 비어있는" 칸으로 옮긴다. 목적지가 없으면 아무 일도 하지 않는다.</summary>
    private void MoveWholeStackToLowestIndex(InventoryContainer fromContainer, int fromIndex, params InventoryContainer[] destinationsInOrder)
    {
        if (fromContainer == null || !fromContainer.IsValidIndex(fromIndex)) return;

        ItemStack fromSlot = fromContainer.slots[fromIndex];
        if (fromSlot == null || fromSlot.IsEmpty) return;

        bool destinationIsFoodStorageOnly = destinationsInOrder.Length == 1 && destinationsInOrder[0] == FoodStorageContainer;
        if (destinationIsFoodStorageOnly && !IsFoodItem(fromSlot)) return;

        foreach (var destination in destinationsInOrder)
        {
            if (destination == null) continue;

            int targetIndex = FindBestDestinationIndex(destination, fromSlot.itemId);
            if (targetIndex < 0) continue;

            bool moved = InventorySlotMoveHelper.MoveSlot(fromContainer, fromIndex, destination, targetIndex, catalogManager);

            if (moved)
            {
                if (fromContainer == FoodStorageContainer || destination == FoodStorageContainer) RefreshStorageSlots();
                warehouseInventory?.PublishWarehouseChanged();
                playerInventory?.PublishInventoryChanged();
                NotifyFoodBoundaryIfNeeded(fromContainer, destination);
                return;
            }
        }
    }

    /// <summary>
    /// sourceContainers 전체에서 itemId와 일치하는 수량을 모두 모아, destinationContainers를 순서대로
    /// 낮은 index부터 채운다(병합 우선, 그다음 빈 칸). 다 못 채우면 나머지는 원래 자리에 그대로 남긴다.
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
        bool destinationTouchesFoodStorage = false;

        foreach (var destination in destinationContainers)
        {
            if (destination?.slots == null || remainingToMove <= 0) continue;
            remainingToMove = FillDestinationWithAmount(destination, itemId, remainingToMove);
            if (destination == FoodStorageContainer) destinationTouchesFoodStorage = true;
        }

        int actuallyMoved = totalAvailable - remainingToMove;
        if (actuallyMoved <= 0) return;

        RemoveAmountFromSources(sourceContainers, itemId, actuallyMoved);

        bool sourceTouchesFoodStorage = Array.IndexOf(sourceContainers, FoodStorageContainer) >= 0;

        if (sourceTouchesFoodStorage || destinationTouchesFoodStorage) RefreshStorageSlots();
        warehouseInventory?.PublishWarehouseChanged();
        playerInventory?.PublishInventoryChanged();

        if (sourceTouchesFoodStorage && destinationTouchesFoodStorage)
        {
            ConsumeFoodSystem.Instance?.OnStorageToStorageMove();
        }
        else if (!sourceTouchesFoodStorage && destinationTouchesFoodStorage)
        {
            ConsumeFoodSystem.Instance?.OnRightToLeftMove();
            OnFoodDataChanged?.Invoke();
        }
        else if (sourceTouchesFoodStorage && !destinationTouchesFoodStorage)
        {
            ConsumeFoodSystem.Instance?.OnLeftToRightMove();
            OnFoodDataChanged?.Invoke();
        }
    }

    /// <summary>destination을 낮은 index부터(병합 우선 -> 빈 칸) amount만큼 채운다. 채우지 못한 나머지를 반환한다.</summary>
    private int FillDestinationWithAmount(InventoryContainer destination, string itemId, int amount)
    {
        int maxStack = GetMaxStackSafe(itemId);
        int remaining = amount;

        for (int i = 0; i < destination.slots.Length && remaining > 0; i++)
        {
            if (IsDestinationIndexLocked(destination, i)) continue;

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
            if (IsDestinationIndexLocked(destination, i)) continue;

            ItemStack s = destination.slots[i];
            if (!s.IsEmpty) continue;

            int add = Mathf.Min(maxStack, remaining);
            s.Set(itemId, add);
            remaining -= add;
        }

        return remaining;
    }

    /// <summary>sourceContainers에서 itemId를 총 amount만큼 제거한다.</summary>
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

    /// <summary>itemId를 병합할 수 있는 가장 낮은 index 칸을 찾고, 없으면 가장 낮은 index의 빈 칸을 찾는다. 없으면 -1.</summary>
    private int FindBestDestinationIndex(InventoryContainer container, string itemId)
    {
        if (container?.slots == null) return -1;

        int maxStack = GetMaxStackSafe(itemId);

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (IsDestinationIndexLocked(container, i)) continue;

            ItemStack s = container.slots[i];
            if (!s.IsEmpty && s.itemId == itemId && s.amount < maxStack) return i;
        }

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (IsDestinationIndexLocked(container, i)) continue;

            if (container.slots[i].IsEmpty) return i;
        }

        return -1;
    }

    /// <summary>목적지가 플레이어 인벤토리/퀵슬롯일 때만 잠금 여부를 확인한다(밥통/일반 창고는 잠금 개념이 없다).</summary>
    private bool IsDestinationIndexLocked(InventoryContainer container, int index)
    {
        if (container == playerInventory?.inventory) return playerInventory.IsInventorySlotLocked(index);
        if (container == playerInventory?.quickSlots) return playerInventory.IsQuickSlotLocked(index);
        return false;
    }

    /// <summary>source/destination 중 하나라도 밥통(음식 창고)이면 ConsumeFoodSystem에 알리고 OnFoodDataChanged를 발행한다.</summary>
    private void NotifyFoodBoundaryIfNeeded(InventoryContainer sourceContainer, InventoryContainer destinationContainer)
    {
        bool sourceIsFood = sourceContainer == FoodStorageContainer;
        bool destIsFood = destinationContainer == FoodStorageContainer;

        if (sourceIsFood && destIsFood)
        {
            ConsumeFoodSystem.Instance?.OnStorageToStorageMove();
        }
        else if (!sourceIsFood && destIsFood)
        {
            ConsumeFoodSystem.Instance?.OnRightToLeftMove();
            OnFoodDataChanged?.Invoke();
        }
        else if (sourceIsFood && !destIsFood)
        {
            ConsumeFoodSystem.Instance?.OnLeftToRightMove();
            OnFoodDataChanged?.Invoke();
        }
    }

    private int GetMaxStackSafe(string itemId)
    {
        var data = catalogManager != null ? catalogManager.FindItemData(itemId) : null;
        return data != null ? Mathf.Max(1, data.MaxStack) : 1;
    }

    #endregion

    #region 슬롯 단위 Take/Place 디스패치

    private bool TryTakeFullFromSlot(InventorySlotUI slot, out ItemStack taken)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) { taken = null; return false; }
        return TryTakeAmount(container, index, int.MaxValue, out taken);
    }

    private bool TryTakeHalfFromSlot(InventorySlotUI slot, out ItemStack taken)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) { taken = null; return false; }
        return TryTakeHalf(container, index, out taken);
    }

    private bool TryTakeAmountFromSlot(InventorySlotUI slot, int amount, out ItemStack taken)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) { taken = null; return false; }
        return TryTakeAmount(container, index, amount, out taken);
    }

    private bool TryPlaceFullIntoSlot(InventorySlotUI slot, ItemStack held)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) return false;
        return TryPlaceAmount(container, index, held, held.amount, true);
    }

    private bool TryPlaceOneIntoSlot(InventorySlotUI slot, ItemStack held)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) return false;
        return TryPlaceAmount(container, index, held, 1, false);
    }

    private bool TryGetSnapshotFromSlot(InventorySlotUI slot, out ItemStack snapshot)
    {
        if (!GetContainerAndIndex(slot, out var container, out int index)) { snapshot = null; return false; }
        return TryGetSnapshot(container, index, out snapshot);
    }

    #endregion

    #region 컨테이너 단위 Take/Place/Snapshot (밥통 / 일반 창고 / 인벤토리 / 퀵슬롯 라우팅)

    private bool TryTakeHalf(InventoryContainer container, int index, out ItemStack taken)
    {
        taken = null;
        if (container == null || !container.IsValidIndex(index)) return false;

        ItemStack slot = container.slots[index];
        if (slot == null || slot.IsEmpty) return false;

        int halfAmount = Mathf.CeilToInt(slot.amount * 0.5f);
        return TryTakeAmount(container, index, halfAmount, out taken);
    }

    private bool TryTakeAmount(InventoryContainer container, int index, int amount, out ItemStack taken)
    {
        taken = null;

        if (container == warehouseInventory?.storage) return warehouseInventory.TryTakeSlot(index, amount, out taken);
        if (container == playerInventory?.inventory) return playerInventory.TryTakeSlot(SlotGroup.Inventory, index, amount, out taken);
        if (container == playerInventory?.quickSlots) return playerInventory.TryTakeSlot(SlotGroup.QuickSlot, index, amount, out taken);

        if (container == FoodStorageContainer)
        {
            bool ok = TryTakeFoodStorageAmount(index, amount, out taken);
            if (ok) RefreshStorageSlots();
            return ok;
        }

        return false;
    }

    private bool TryPlaceAmount(InventoryContainer container, int index, ItemStack held, int amount, bool allowSwap)
    {
        if (held == null || held.IsEmpty) return false;

        if (container == warehouseInventory?.storage) return warehouseInventory.TryPlaceHeldAmount(index, held, amount, allowSwap);
        if (container == playerInventory?.inventory) return playerInventory.TryPlaceHeldAmount(SlotGroup.Inventory, index, held, amount, allowSwap);
        if (container == playerInventory?.quickSlots) return playerInventory.TryPlaceHeldAmount(SlotGroup.QuickSlot, index, held, amount, allowSwap);

        if (container == FoodStorageContainer)
        {
            // 🌟 음식 창고엔 Food 카테고리 아이템만 들어갈 수 있다(기존 규칙 유지).
            if (!IsFoodItem(held)) return false;

            bool ok = TryPlaceFoodStorageAmount(index, held, amount, allowSwap);
            if (ok) RefreshStorageSlots();
            return ok;
        }

        return false;
    }

    private bool TryGetSnapshot(InventoryContainer container, int index, out ItemStack snapshot)
    {
        snapshot = null;

        if (container == warehouseInventory?.storage) return warehouseInventory.TryGetSlotSnapshot(index, out snapshot);
        if (container == playerInventory?.inventory) return playerInventory.TryGetSlotSnapshot(SlotGroup.Inventory, index, out snapshot);
        if (container == playerInventory?.quickSlots) return playerInventory.TryGetSlotSnapshot(SlotGroup.QuickSlot, index, out snapshot);

        if (container == FoodStorageContainer)
        {
            if (!container.IsValidIndex(index)) return false;
            ItemStack slot = container.slots[index];
            if (slot == null || slot.IsEmpty) return false;

            snapshot = new ItemStack { itemId = slot.itemId, amount = slot.amount };
            return true;
        }

        return false;
    }

    /// <summary>밥통(FoodStorageContainer) 전용 - WarehouseInventory.TryTakeSlot과 동일한 알고리즘을 래퍼 없는 원시 컨테이너에 직접 적용한다.</summary>
    private bool TryTakeFoodStorageAmount(int index, int amount, out ItemStack taken)
    {
        taken = null;
        var container = FoodStorageContainer;
        if (container == null || !container.IsValidIndex(index)) return false;

        ItemStack slot = container.slots[index];
        if (slot == null || slot.IsEmpty || amount <= 0) return false;

        int takenAmount = Mathf.Min(amount, slot.amount);
        taken = new ItemStack { itemId = slot.itemId, amount = takenAmount };

        slot.amount -= takenAmount;
        if (slot.amount <= 0) slot.Clear();

        return true;
    }

    /// <summary>밥통(FoodStorageContainer) 전용 - WarehouseInventory.TryPlaceHeldAmount와 동일한 알고리즘을 래퍼 없는 원시 컨테이너에 직접 적용한다.</summary>
    private bool TryPlaceFoodStorageAmount(int index, ItemStack heldStackToPlace, int amount, bool allowSwap)
    {
        var container = FoodStorageContainer;
        if (container == null || !container.IsValidIndex(index)) return false;

        ItemStack target = container.slots[index];
        int requestedAmount = Mathf.Min(amount, heldStackToPlace.amount);

        if (target.IsEmpty)
        {
            int placed = Mathf.Min(GetMaxStackSafe(heldStackToPlace.itemId), requestedAmount);
            target.Set(heldStackToPlace.itemId, placed);
            heldStackToPlace.amount -= placed;
            if (heldStackToPlace.amount <= 0) heldStackToPlace.Clear();
            return true;
        }

        if (target.itemId == heldStackToPlace.itemId)
        {
            int space = GetMaxStackSafe(target.itemId) - target.amount;
            if (space <= 0) return false;

            int placed = Mathf.Min(space, requestedAmount);
            target.amount += placed;
            heldStackToPlace.amount -= placed;
            if (heldStackToPlace.amount <= 0) heldStackToPlace.Clear();
            return true;
        }

        if (!allowSwap || requestedAmount != heldStackToPlace.amount) return false;

        string displacedItemId = target.itemId;
        int displacedAmount = target.amount;
        target.Set(heldStackToPlace.itemId, heldStackToPlace.amount);
        heldStackToPlace.Set(displacedItemId, displacedAmount);
        return true;
    }

    #endregion

    #region 커서(held) 아이템 관리 / 안전 반환

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
        heldOriginContainer = null;
        heldOriginIndex = -1;
        if (itemDragUI != null) itemDragUI.Hide();
    }

    /// <summary>[안전장치] 원래 있던 자리 우선, 그다음 반대쪽(밥통 <-> 퀵슬롯+인벤토리+창고)까지 확장해서 반환을 시도한다.</summary>
    private bool TryReturnHeldStackAnywhere(ItemStack held, InventoryContainer originContainer, int originIndex)
    {
        if (held == null || held.IsEmpty) return true;
        if (originContainer == null) return false; // 원래 자리를 알 수 없으면 안전하게 포기한다.

        bool isFood = IsFoodItem(held);
        var candidates = new List<InventoryContainer> { originContainer };

        if (isFood && !candidates.Contains(FoodStorageContainer)) candidates.Add(FoodStorageContainer);
        if (playerInventory != null)
        {
            if (!candidates.Contains(playerInventory.quickSlots)) candidates.Add(playerInventory.quickSlots);
            if (!candidates.Contains(playerInventory.inventory)) candidates.Add(playerInventory.inventory);
        }
        if (warehouseInventory != null && !candidates.Contains(warehouseInventory.storage)) candidates.Add(warehouseInventory.storage);

        bool touchedFoodStorage = false;

        foreach (var candidate in candidates)
        {
            if (candidate == null || held.IsEmpty) continue;

            int preferredIndex = candidate == originContainer ? originIndex : -1;

            if (candidate == FoodStorageContainer)
            {
                TryReturnToFoodStorageContainer(held, preferredIndex);
                touchedFoodStorage = true;
            }
            else if (candidate == warehouseInventory?.storage)
            {
                warehouseInventory.TryReturnStack(held, preferredIndex);
            }
            else if (candidate == playerInventory?.inventory)
            {
                playerInventory.TryReturnHeldStack(held, SlotGroup.Inventory, preferredIndex);
            }
            else if (candidate == playerInventory?.quickSlots)
            {
                playerInventory.TryReturnHeldStack(held, SlotGroup.QuickSlot, preferredIndex);
            }
        }

        if (touchedFoodStorage) RefreshStorageSlots();

        return held.IsEmpty;
    }

    /// <summary>[안전장치] 밥통(FoodStorageContainer) 전용 반환 시도. 선호 슬롯 -> 병합 가능한 슬롯 -> 빈 슬롯 순서.</summary>
    private void TryReturnToFoodStorageContainer(ItemStack held, int preferredIndex)
    {
        var container = FoodStorageContainer;
        if (held == null || held.IsEmpty || container?.slots == null) return;

        if (container.IsValidIndex(preferredIndex))
        {
            TryPlaceFoodStorageAmount(preferredIndex, held, held.amount, false);
            if (held.IsEmpty) return;
        }

        for (int i = 0; i < container.slots.Length && !held.IsEmpty; i++)
        {
            ItemStack slot = container.slots[i];
            if (!slot.IsEmpty && slot.itemId == held.itemId)
            {
                TryPlaceFoodStorageAmount(i, held, held.amount, false);
            }
        }

        for (int i = 0; i < container.slots.Length && !held.IsEmpty; i++)
        {
            if (container.slots[i].IsEmpty)
            {
                TryPlaceFoodStorageAmount(i, held, held.amount, false);
            }
        }
    }

    #endregion

    #region IInventorySlotOwner - 드래그 부분은 클릭 방식 전환으로 더 이상 호출되지 않음, 툴팁만 실제 사용

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
        if (itemTooltipUI != null) itemTooltipUI.Hide();
    }

    #endregion

    #region 수량 팝업 (중클릭)

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
            Debug.LogWarning("[FoodWarehouseUI] 수량 선택 팝업을 생성할 Canvas를 찾지 못했습니다.");
        }
    }

    private void ShowQuantityPopup(InventorySlotUI slot, Vector2 position)
    {
        if (heldStack != null && !heldStack.IsEmpty) return;
        if (quantityPopup == null) EnsureQuantityPopup();
        if (quantityPopup == null) return;
        if (!TryGetSnapshotFromSlot(slot, out ItemStack snapshot)) return;

        ItemData itemData = catalogManager != null ? catalogManager.FindItemData(snapshot.itemId) : null;
        if (itemData == null) return;

        InventorySlotUI capturedSlot = slot;
        quantityPopup.Show(itemData, snapshot.amount, position,
            amount => ConfirmQuantityPick(capturedSlot, amount, position), null);
    }

    private void ConfirmQuantityPick(InventorySlotUI slot, int amount, Vector2 position)
    {
        if (heldStack != null && !heldStack.IsEmpty) return;
        if (!TryTakeAmountFromSlot(slot, amount, out ItemStack takenStack)) return;

        heldStack = takenStack;
        heldOriginContainer = ResolveContainer(slot);
        heldOriginIndex = slot.slotIndex;
        RefreshHeldItem(position);
    }

    #endregion

    private void HandleUpgradeButtonClicked()
    {
        if (foodWarehouseUpgrade != null && UpgradePopupUI.Instance != null)
        {
            UpgradePopupUI.Instance.Show(foodWarehouseUpgrade);
        }
    }

    /// <summary>
    /// 현재까지 업그레이드로 추가된 슬롯 개수 반환
    /// </summary>
    public int GetCurrentUpgradedSlotCount()
    {
        return extraUpgradedSlotCount;
    }

    /// <summary>
    /// 현재 음식 창고의 전체 슬롯 개수 반환
    /// </summary>
    public int GetTotalFoodStorageSlotCount()
    {
        var container = FoodStorageContainer;
        return container != null && container.slots != null ? container.slots.Length : 0;
    }

    /// <summary>
    /// 음식 창고 슬롯 1개 추가 함수
    /// </summary>
    public void AddSingleFoodStorageSlot()
    {
        var container = FoodStorageContainer;
        if (container == null) return;

        int currentLength = container.slots != null ? container.slots.Length : 0;
        int newLength = currentLength + 1;

        ItemStack[] newSlots = new ItemStack[newLength];
        for (int i = 0; i < newLength; i++)
        {
            newSlots[i] = (i < currentLength && container.slots[i] != null) ? container.slots[i] : new ItemStack();
        }
        container.slots = newSlots;

        extraUpgradedSlotCount++;

        EnsureFoodStorageSlotCount();
        RefreshStorageSlots();

        OnFoodDataChanged?.Invoke();

        Debug.Log($"[FoodWarehouseUI] 음식 창고 슬롯 1개 확장 완료! 현재 총 슬롯 수: {container.slots.Length}개");
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
