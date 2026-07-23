using System;
using System.Collections.Generic;
using HDY.Item;
using HDY.Inventory;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>
    /// 대장간 UI의 연마 탭 전용 패널. ForgeUI가 탭 전환 시 이 패널의 GameObject를 켜고 끈다
    /// (MemStorageUI_Grid/Info와 동일하게 - ForgeUI 본체는 탭 전환만 담당하고, 각 탭의 세부 로직은
    /// 별도 패널 컴포넌트가 책임진다).
    ///
    /// [잠금] lockToggle이 켜진 칸은 이번 연마 시도에서 보호되어 바뀌지 않는다. 잠금 상태는 저장되지 않고
    /// 이 패널 세션 안에서만 유지되며, 도구를 다시 선택하거나(HandleListSlotClicked) 패널이 비활성화되면
    /// (OnDisable, 즉 탭 전환 시) 자동으로 초기화된다.
    /// </summary>
    public class ForgeUI_RefinementPanel : MonoBehaviour
    {
        [Serializable]
        public class SlotRowUI
        {
            [Tooltip("이 칸에 실제 슬롯 데이터가 있을 때만 활성화되는 루트")]
            public GameObject root;
            public TMP_Text infoText;
            public Toggle lockToggle;
        }

        [Header("하단 목록")]
        [SerializeField] private Transform slotListContent;
        [SerializeField] private ForgeToolSlotUI slotPrefab;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("가운데 - 선택된 도구")]
        [SerializeField] private ForgeToolSlotUI selectedSlotDisplay;
        [SerializeField] private GameObject selectedEmptyHint;

        [Header("연마칸 표시 (최대 5칸, 배열 인덱스 = 슬롯 인덱스)")]
        [SerializeField] private SlotRowUI[] slotRows = new SlotRowUI[5];

        [Header("비용 / 실행")]
        [SerializeField] private TMP_Text stoneCostText;
        [SerializeField] private TMP_Text goldCostText;
        [SerializeField] private Button executeButton;
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color shortageTextColor = Color.red;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;

        private ItemStack selectedStack;
        private readonly bool[] lockedSlots = new bool[5];
        private readonly List<ForgeToolSlotUI> spawnedSlots = new List<ForgeToolSlotUI>();

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (executeButton != null) executeButton.onClick.AddListener(HandleExecuteClicked);

            for (int i = 0; i < slotRows.Length; i++)
            {
                int capturedIndex = i; // 클로저 캡처 방지용 지역 변수
                if (slotRows[i]?.lockToggle != null)
                {
                    slotRows[i].lockToggle.onValueChanged.AddListener(value => HandleLockToggleChanged(capturedIndex, value));
                }
            }
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents(true);
            ClearLockState();
            RefreshList();
            RefreshSelection();
        }

        private void OnDisable()
        {
            SubscribeInventoryEvents(false);
        }

        private void SubscribeInventoryEvents(bool subscribe)
        {
            if (playerInventory != null)
            {
                if (subscribe) playerInventory.OnInventoryChanged += HandleContainersChanged;
                else playerInventory.OnInventoryChanged -= HandleContainersChanged;
            }

            if (warehouseInventory != null)
            {
                if (subscribe) warehouseInventory.OnStorageChanged += HandleContainersChanged;
                else warehouseInventory.OnStorageChanged -= HandleContainersChanged;
            }
        }

        private void HandleContainersChanged()
        {
            RefreshList();
            RefreshSelection();
        }

        private void ClearLockState()
        {
            for (int i = 0; i < lockedSlots.Length; i++) lockedSlots[i] = false;
        }

        private void RefreshList()
        {
            var entries = CollectRefinableTools();

            for (int i = 0; i < entries.Count; i++)
            {
                var slot = GetOrCreateSlot(i);
                var displayData = catalogManager != null ? catalogManager.FindItemData(entries[i].itemId) : null;
                slot.Bind(entries[i], displayData);
                slot.gameObject.SetActive(true);
            }

            for (int i = entries.Count; i < spawnedSlots.Count; i++)
            {
                spawnedSlots[i].Clear();
                spawnedSlots[i].gameObject.SetActive(false);
            }
        }

        private ForgeToolSlotUI GetOrCreateSlot(int index)
        {
            if (index < spawnedSlots.Count) return spawnedSlots[index];

            var slot = Instantiate(slotPrefab, slotListContent);
            slot.Clicked += HandleListSlotClicked;
            spawnedSlots.Add(slot);
            return slot;
        }

        /// <summary>인벤토리(일반+퀵슬롯) + 창고에서 연마 가능 도구(도끼/곡괭이/괭이)만 모은다.</summary>
        private List<ItemStack> CollectRefinableTools()
        {
            var results = new List<ItemStack>();
            if (forgeManager == null) return results;

            void CollectFrom(InventoryContainer container)
            {
                if (container?.slots == null) return;

                foreach (var slot in container.slots)
                {
                    if (slot == null || slot.IsEmpty) continue;
                    if (!forgeManager.IsForgeableItem(slot.itemId)) continue;

                    results.Add(slot);
                }
            }

            if (playerInventory != null)
            {
                CollectFrom(playerInventory.inventory);
                CollectFrom(playerInventory.quickSlots);
            }

            if (warehouseInventory != null)
            {
                CollectFrom(warehouseInventory.storage);
            }

            return results;
        }

        private void HandleListSlotClicked(ForgeToolSlotUI slot)
        {
            if (slot == null || slot.BoundStack == null) return;

            selectedStack = slot.BoundStack;
            ClearLockState(); // 도구를 새로 선택하면 잠금은 초기화된다.
            RefreshSelection();
        }

        private void HandleLockToggleChanged(int index, bool isLocked)
        {
            if (index < 0 || index >= lockedSlots.Length) return;
            lockedSlots[index] = isLocked;
            RefreshCostPreview();
        }

        private void RefreshSelection()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;
            if (selectedEmptyHint != null) selectedEmptyHint.SetActive(!hasSelection);

            if (!hasSelection)
            {
                selectedSlotDisplay?.Clear();
                SetAllSlotRowsHidden();
                SetExecuteInteractable(false);
                if (stoneCostText != null) stoneCostText.text = "-";
                if (goldCostText != null) goldCostText.text = "-";
                return;
            }

            var displayData = catalogManager != null ? catalogManager.FindItemData(selectedStack.itemId) : null;
            selectedSlotDisplay?.Bind(selectedStack, displayData);

            if (forgeManager != null && forgeManager.TryPeekRefinementSlots(selectedStack, out var slots))
            {
                BindSlotRows(slots);
            }
            else
            {
                SetAllSlotRowsHidden();
            }

            RefreshCostPreview();
        }

        private void BindSlotRows(ForgeRefinementSlotData[] slots)
        {
            for (int i = 0; i < slotRows.Length; i++)
            {
                var row = slotRows[i];
                if (row == null) continue;

                bool hasSlot = slots != null && i < slots.Length && slots[i] != null;
                if (row.root != null) row.root.SetActive(hasSlot);
                if (!hasSlot) continue;

                var slotData = slots[i];

                if (row.infoText != null)
                {
                    row.infoText.text = $"{slotData.Grade} / {slotData.OptionType} +{slotData.Value:0.#}";
                }

                if (row.lockToggle != null)
                {
                    row.lockToggle.SetIsOnWithoutNotify(lockedSlots[i]);
                }
            }
        }

        private void SetAllSlotRowsHidden()
        {
            foreach (var row in slotRows)
            {
                if (row?.root != null) row.root.SetActive(false);
            }
        }

        private void RefreshCostPreview()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;
            if (!hasSelection || forgeManager == null)
            {
                SetExecuteInteractable(false);
                return;
            }

            var preview = forgeManager.GetRefinementPreview(selectedStack, lockedSlots);

            bool stoneShortage = preview.MaterialOwned < preview.MaterialCost;
            if (stoneCostText != null)
            {
                stoneCostText.text = $"{preview.MaterialOwned} / {preview.MaterialCost}";
                stoneCostText.color = stoneShortage ? shortageTextColor : normalTextColor;
            }

            bool goldShortage = preview.GoldOwned < preview.GoldCost;
            if (goldCostText != null)
            {
                goldCostText.text = $"{preview.GoldOwned} / {preview.GoldCost}";
                goldCostText.color = goldShortage ? shortageTextColor : normalTextColor;
            }

            bool canExecute = preview.BlockReason == RefinementFailReason.None && !stoneShortage && !goldShortage;
            SetExecuteInteractable(canExecute);
        }

        private void SetExecuteInteractable(bool value)
        {
            if (executeButton != null) executeButton.interactable = value;
        }

        private void HandleExecuteClicked()
        {
            if (selectedStack == null || selectedStack.IsEmpty || forgeManager == null) return;

            forgeManager.TryRefine(selectedStack, lockedSlots);

            RefreshList();
            RefreshSelection();
        }
    }
}
