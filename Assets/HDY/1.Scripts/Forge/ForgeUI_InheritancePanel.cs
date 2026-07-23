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
    /// 대장간 UI의 전승 탭 전용 패널.
    /// [선택 순서] 하단 목록에서 첫 클릭 = 재료 도구, 이후 클릭 = 전승받을 도구.
    /// 재료/대상이 모두 찬 상태에서 또 클릭하면 그 아이템을 새 재료로 다시 선택(처음부터 다시 시작)한다.
    /// 가운데 재료 슬롯을 클릭하면 선택을 전부 초기화하고, 대상 슬롯을 클릭하면 대상만 초기화한다.
    /// </summary>
    public class ForgeUI_InheritancePanel : MonoBehaviour
    {
        [Header("하단 목록")]
        [SerializeField] private Transform slotListContent;
        [SerializeField] private ForgeToolSlotUI slotPrefab;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("가운데 - 재료 / 전승받을 도구")]
        [SerializeField] private ForgeToolSlotUI materialSlotDisplay;
        [SerializeField] private GameObject materialEmptyHint;
        [SerializeField] private ForgeToolSlotUI targetSlotDisplay;
        [SerializeField] private GameObject targetEmptyHint;

        [Header("안내 / 실행")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button executeButton;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;

        private ItemStack materialStack;
        private ItemStack targetStack;
        private readonly List<ForgeToolSlotUI> spawnedSlots = new List<ForgeToolSlotUI>();

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (executeButton != null) executeButton.onClick.AddListener(HandleExecuteClicked);
            if (materialSlotDisplay != null) materialSlotDisplay.Clicked += _ => ClearSelection();
            if (targetSlotDisplay != null) targetSlotDisplay.Clicked += _ => ClearTargetOnly();
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents(true);
            ClearSelection();
            RefreshList();
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
            RefreshMiddlePanel();
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
            var stack = slot.BoundStack;

            if (materialStack == null)
            {
                materialStack = stack;
            }
            else if (targetStack == null)
            {
                if (ReferenceEquals(stack, materialStack)) return; // 같은 스택 중복 선택 방지
                targetStack = stack;
            }
            else
            {
                // 재료/대상이 모두 찬 상태에서 또 클릭 - 새 재료부터 다시 선택 시작
                materialStack = stack;
                targetStack = null;
            }

            RefreshMiddlePanel();
        }

        private void ClearSelection()
        {
            materialStack = null;
            targetStack = null;
            RefreshMiddlePanel();
        }

        private void ClearTargetOnly()
        {
            targetStack = null;
            RefreshMiddlePanel();
        }

        private void RefreshMiddlePanel()
        {
            bool hasMaterial = materialStack != null && !materialStack.IsEmpty;
            bool hasTarget = targetStack != null && !targetStack.IsEmpty;

            if (materialEmptyHint != null) materialEmptyHint.SetActive(!hasMaterial);
            if (targetEmptyHint != null) targetEmptyHint.SetActive(!hasTarget);

            if (hasMaterial)
            {
                var data = catalogManager != null ? catalogManager.FindItemData(materialStack.itemId) : null;
                materialSlotDisplay?.Bind(materialStack, data);
            }
            else
            {
                materialSlotDisplay?.Clear();
            }

            if (hasTarget)
            {
                var data = catalogManager != null ? catalogManager.FindItemData(targetStack.itemId) : null;
                targetSlotDisplay?.Bind(targetStack, data);
            }
            else
            {
                targetSlotDisplay?.Clear();
            }

            bool canExecute = hasMaterial && hasTarget;

            if (statusText != null)
            {
                statusText.text = !hasMaterial ? "재료 도구를 선택하세요"
                    : !hasTarget ? "전승받을 도구를 선택하세요"
                    : "실행 가능";
            }

            if (executeButton != null) executeButton.interactable = canExecute;
        }

        private void HandleExecuteClicked()
        {
            if (materialStack == null || targetStack == null || forgeManager == null) return;

            var outcome = forgeManager.TryInherit(materialStack, targetStack);

            if (outcome.Attempted)
            {
                ClearSelection();
            }

            RefreshList();
        }
    }
}
