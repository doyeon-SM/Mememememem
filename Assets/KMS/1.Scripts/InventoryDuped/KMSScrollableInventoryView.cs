using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// Presents the existing sixty inventory slots as one continuous,
    /// vertically scrollable five-column grid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSScrollableInventoryView : MonoBehaviour
    {
        private const int ColumnCount = 5;

        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private Transform inventoryGrid;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private RectTransform gridRect;
        [SerializeField] private RectTransform upgradeButtonRect;
        [SerializeField] private float cellHeight = 60f;
        [SerializeField] private float rowSpacing = 6f;
        [SerializeField] private float upgradeGap = 10f;
        [SerializeField] private float upgradeHeight = 54f;
        [SerializeField] private float bottomPadding = 12f;

        private InventorySlotUI[] slots = Array.Empty<InventorySlotUI>();
        private PlayerInventory playerInventory;
        private int previousUnlockedCount = -1;

        private void Awake()
        {
            ResolveReferences();
            CacheSlots();
            BindCloseButton();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheSlots();
            Subscribe();
            RefreshLayout(false);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseInventory);
        }

        private void ResolveReferences()
        {
            if (inventoryUI == null) inventoryUI = GetComponentInParent<InventoryUI>(true);
            if (playerInventory == null && inventoryUI != null) playerInventory = inventoryUI.playerInventory;
            if (inventoryGrid == null && inventoryUI != null) inventoryGrid = inventoryUI.inventoryGrid;
            if (gridRect == null) gridRect = inventoryGrid as RectTransform;
        }

        private void BindCloseButton()
        {
            if (closeButton == null) return;
            closeButton.onClick.RemoveListener(CloseInventory);
            closeButton.onClick.AddListener(CloseInventory);
        }

        private void CacheSlots()
        {
            slots = inventoryGrid == null
                ? Array.Empty<InventorySlotUI>()
                : inventoryGrid.GetComponentsInChildren<InventorySlotUI>(true)
                    .Where(slot => slot.group == SlotGroup.Inventory)
                    .OrderBy(slot => slot.slotIndex)
                    .ToArray();
        }

        private void Subscribe()
        {
            Unsubscribe();
            if (playerInventory != null)
                playerInventory.OnInventorySlotCountChanged += HandleSlotCountChanged;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.OnInventorySlotCountChanged -= HandleSlotCountChanged;
        }

        private void HandleSlotCountChanged()
        {
            RefreshLayout(true);
        }

        public void CloseInventory() => inventoryUI?.Close();

        public void RefreshLayout(bool revealNewestSlots)
        {
            ResolveReferences();
            if (playerInventory == null || gridRect == null || contentRect == null) return;

            int unlocked = Mathf.Clamp(playerInventory.UnlockedInventorySlotCount, 0, slots.Length);
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlotUI slot = slots[i];
                bool visible = slot.slotIndex < unlocked;
                if (slot.gameObject.activeSelf != visible) slot.gameObject.SetActive(visible);
            }

            int rows = Mathf.CeilToInt(unlocked / (float)ColumnCount);
            float gridHeight = rows > 0
                ? rows * cellHeight + (rows - 1) * rowSpacing
                : 0f;

            Vector2 gridSize = gridRect.sizeDelta;
            gridSize.y = gridHeight;
            gridRect.sizeDelta = gridSize;

            bool canUpgrade = unlocked < playerInventory.MaxInventorySlotCount;
            if (upgradeButton != null) upgradeButton.gameObject.SetActive(canUpgrade);

            if (upgradeButtonRect != null)
            {
                upgradeButtonRect.anchoredPosition = new Vector2(0f, -(gridHeight + upgradeGap));
            }

            float contentHeight = gridHeight + bottomPadding;
            if (canUpgrade) contentHeight += upgradeGap + upgradeHeight;
            Vector2 contentSize = contentRect.sizeDelta;
            contentSize.y = contentHeight;
            contentRect.sizeDelta = contentSize;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            bool wasUpgraded = previousUnlockedCount >= 0 && unlocked > previousUnlockedCount;
            previousUnlockedCount = unlocked;
            if (revealNewestSlots && wasUpgraded && scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
