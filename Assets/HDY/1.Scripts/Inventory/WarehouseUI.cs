using UnityEngine;
using UnityEngine.UI;
using KMS.InventoryDuped;
using HDY.Item;
using HDY.Upgrade;
using System;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고(왼쪽) + 인벤토리/퀵슬롯(오른쪽) 통합 화면 컨트롤러.
    /// IInventorySlotOwner를 구현해서 InventoryUI가 하던 역할(드래그/툴팁 위임 대상)을 그대로 대체하되,
    /// Storage/Inventory/QuickSlot 3그룹을 모두 다룬다.
    ///
    /// [재사용] InventorySlotUI(슬롯 하나), ItemDragUI, ItemTooltipUI, InventorySlotMoveHelper(이동/병합
    /// 공용 로직)를 그대로 가져다 쓴다. 창고 전용으로 새로 만든 건 스크롤 그리드 채우기(런타임 Instantiate)와
    /// WarehouseSortUI/업그레이드 버튼 연결뿐이다.
    ///
    /// [슬롯 배치 방식 차이] 인벤토리(10x6)/퀵슬롯(10칸)은 기존 컨벤션대로 씬에 미리 배치된 슬롯을 그대로
    /// 수집한다(BindSlotGroup). 창고(10 x n, 스크롤)는 개수가 유동적이라 도감 그리드와 동일하게 런타임에
    /// Instantiate하고, 업그레이드로 행이 늘어나면 그만큼 슬롯을 추가로 Instantiate한다(EnsureStorageSlotCount).
    ///
    /// [창고 ↔ 인벤토리/퀵슬롯 교차 이동] PlayerInventory와 WarehouseInventory 둘 다 서로의 컨테이너를 모르므로,
    /// 이 조합은 InventorySlotMoveHelper로 직접 처리하고 관련된 슬롯 2개만 수동으로 갱신한다(각자의 변경
    /// 이벤트가 이 조합까지 커버하지 않기 때문). 창고↔창고, 인벤토리/퀵슬롯 내부 이동은 기존처럼 각 매니저의
    /// 메서드를 그대로 호출해서 그쪽 이벤트가 알아서 갱신하도록 한다.
    ///
    /// [창고 업그레이드] 업그레이드 버튼을 누르면 공용 업그레이드 팝업(UpgradePopupUI)에 warehouseUpgrade(창고
    /// 행 확장을 IUpgradable로 감싼 어댑터)를 넘겨 보여준다. 비용 확인/차감과 업그레이드 적용은 팝업과
    /// warehouseUpgrade가 처리하고, 이 컨트롤러는 WarehouseInventory.OnRowCountChanged를 구독해뒀다가
    /// 행이 늘어나면 그만큼 슬롯 UI를 추가로 만들고 다시 그려주기만 한다.
    ///
    /// [PlayerInventory 임시 배치] 아직 씬 간 데이터 전달 시스템이 없어서, 이 씬에도 PlayerInventory를
    /// 임시로 배치해서 참조한다. 나중에 씬 이동 시 데이터를 넘겨받는 방식이 생기면 이 참조 연결 부분만 바뀌면 된다.
    ///
    /// [열기/닫기 없음] TestScene_KMS의 InventoryUI와 달리, 이 화면은 항상 떠 있는 영지(Territory) 화면이라고
    /// 가정하고 패널 열기/닫기, 커서 잠금, 플레이어 입력 차단 로직은 넣지 않았다. 필요하면 나중에 추가하면 된다.
    /// </summary>
    public class WarehouseUI : MonoBehaviour, IInventorySlotOwner
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

        [Header("공용 (드래그 고스트 / 툴팁)")]
        [SerializeField] private ItemDragUI itemDragUI;
        [SerializeField] private ItemTooltipUI itemTooltipUI;

        private InventorySlotUI[] storageSlots;
        private InventorySlotUI[] inventorySlots;
        private InventorySlotUI[] quickSlots;

        private InventorySlotUI dragSource;

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
        }

        // ===================== IInventorySlotOwner =====================

        public void BeginSlotDrag(InventorySlotUI source, ItemStack stack, Vector2 position)
        {
            if (source == null || stack == null || stack.IsEmpty) return;
            if (IsLocked(source)) return;

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
            if (itemTooltipUI == null) return;

            itemTooltipUI.Hide();
        }

        // ===================== 창고 업그레이드 =====================

        /// <summary>업그레이드 버튼 클릭 처리. 공용 업그레이드 팝업에 창고 확장 어댑터를 넘겨 보여준다.</summary>
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

        /// <summary>창고 행이 늘어났을 때(업그레이드 성공) 호출. 늘어난 만큼 슬롯 UI를 추가로 만들고 다시 그린다.</summary>
        private void HandleRowCountChanged()
        {
            Debug.Log("[WarehouseUI] OnRowCountChanged 수신 -> 창고 슬롯 확장 시도");

            EnsureStorageSlotCount();
            RefreshStorageSlots();
        }

        // ===================== 슬롯 이동 =====================

        /// <summary>
        /// from/to의 SlotGroup 조합에 따라 알맞은 경로로 이동시킨다.
        /// - 창고↔창고: WarehouseInventory.MoveSlot (자체 이벤트로 갱신)
        /// - 인벤토리/퀵슬롯 내부 조합: PlayerInventory의 기존 메서드 (자체 이벤트로 갱신)
        /// - 창고 ↔ 인벤토리/퀵슬롯: 두 매니저 다 모르는 조합이라 공용 헬퍼로 직접 처리 후 관련 슬롯 2개만 수동 갱신
        /// </summary>
        private void MoveBetweenSlots(InventorySlotUI from, InventorySlotUI to)
        {
            if (playerInventory == null || warehouseInventory == null) return;
            if (IsLocked(from) || IsLocked(to)) return;

            bool bothStorage = from.group == SlotGroup.Storage && to.group == SlotGroup.Storage;
            bool bothPlayerSide = from.group != SlotGroup.Storage && to.group != SlotGroup.Storage;

            if (bothStorage)
            {
                warehouseInventory.MoveSlot(from.slotIndex, to.slotIndex);
                return;
            }

            if (bothPlayerSide)
            {
                MovePlayerInventorySlots(from, to);
                return;
            }

            // 창고 <-> 인벤토리/퀵슬롯 교차 이동
            var fromContainer = GetContainer(from.group);
            var toContainer = GetContainer(to.group);

            bool moved = InventorySlotMoveHelper.MoveSlot(fromContainer, from.slotIndex, toContainer, to.slotIndex, catalogManager);

            if (moved)
            {
                RefreshSlotByGroup(from.group, from.slotIndex);
                RefreshSlotByGroup(to.group, to.slotIndex);

                warehouseInventory.PublishWarehouseChanged();
                playerInventory.PublishInventoryChanged();
            }
        }

        private void MovePlayerInventorySlots(InventorySlotUI from, InventorySlotUI to)
        {
            if (from.group == SlotGroup.Inventory && to.group == SlotGroup.Inventory) playerInventory.MoveInventorySlot(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.Inventory && to.group == SlotGroup.QuickSlot) playerInventory.MoveInventoryToQuickSlot(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.QuickSlot && to.group == SlotGroup.Inventory) playerInventory.MoveQuickSlotToInventory(from.slotIndex, to.slotIndex);
            else if (from.group == SlotGroup.QuickSlot && to.group == SlotGroup.QuickSlot) playerInventory.MoveQuickSlot(from.slotIndex, to.slotIndex);
        }

        private InventoryContainer GetContainer(SlotGroup group)
        {
            switch (group)
            {
                case SlotGroup.Inventory: return playerInventory.inventory;
                case SlotGroup.QuickSlot: return playerInventory.quickSlots;
                case SlotGroup.Storage: return warehouseInventory.storage;
                default: return null;
            }
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

        /// <summary>창고↔인벤토리/퀵슬롯 교차 이동 후, 관련된 슬롯 하나만 그룹에 맞게 다시 그린다.</summary>
        private void RefreshSlotByGroup(SlotGroup group, int index)
        {
            switch (group)
            {
                case SlotGroup.Inventory:
                    if (inventorySlots != null && index >= 0 && index < inventorySlots.Length)
                        inventorySlots[index]?.SetStack(playerInventory.inventory.slots[index]);
                    break;

                case SlotGroup.QuickSlot:
                    RefreshQuickSlot(index);
                    break;

                case SlotGroup.Storage:
                    if (storageSlots != null && index >= 0 && index < storageSlots.Length)
                        storageSlots[index]?.SetStack(warehouseInventory.storage.slots[index]);
                    break;
            }
        }
    }
}
