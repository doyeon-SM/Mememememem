using System;
using System.Linq;
using HDY.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// Keeps the existing sixty-slot inventory model and presents it as two
    /// thirty-slot pages. No keyboard navigation is intentionally provided.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSPagedInventoryView : MonoBehaviour
    {
        private const int PageSize = 30;
        private const int ColumnCount = 5;

        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private Transform inventoryGrid;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text pageLabel;
        [SerializeField] private RectTransform upgradeButtonRect;
        [SerializeField] private float firstRowY = -176f;
        [SerializeField] private float rowStep = 66f;

        private InventorySlotUI[] slots = Array.Empty<InventorySlotUI>();
        private PlayerInventory playerInventory;
        private int currentPage;

        private void Awake()
        {
            ResolveReferences();
            CacheSlots();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CacheSlots();
            Subscribe();
            Refresh();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (previousPageButton != null) previousPageButton.onClick.RemoveListener(ShowPreviousPage);
            if (nextPageButton != null) nextPageButton.onClick.RemoveListener(ShowNextPage);
            if (closeButton != null) closeButton.onClick.RemoveListener(CloseInventory);
        }

        private void ResolveReferences()
        {
            if (inventoryUI == null) inventoryUI = GetComponentInParent<InventoryUI>(true);
            if (playerInventory == null && inventoryUI != null) playerInventory = inventoryUI.playerInventory;
            if (inventoryGrid == null && inventoryUI != null) inventoryGrid = inventoryUI.inventoryGrid;

            if (previousPageButton != null)
            {
                previousPageButton.onClick.RemoveListener(ShowPreviousPage);
                previousPageButton.onClick.AddListener(ShowPreviousPage);
            }
            if (nextPageButton != null)
            {
                nextPageButton.onClick.RemoveListener(ShowNextPage);
                nextPageButton.onClick.AddListener(ShowNextPage);
            }
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseInventory);
                closeButton.onClick.AddListener(CloseInventory);
            }
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
                playerInventory.OnInventorySlotCountChanged += Refresh;
        }

        private void Unsubscribe()
        {
            if (playerInventory != null)
                playerInventory.OnInventorySlotCountChanged -= Refresh;
        }

        public void ShowPreviousPage()
        {
            if (currentPage == 0) return;
            currentPage--;
            Refresh();
        }

        public void ShowNextPage()
        {
            if (!CanAccessSecondPage() || currentPage >= 1) return;
            currentPage++;
            Refresh();
        }

        public void CloseInventory() => inventoryUI?.Close();

        public void Refresh()
        {
            ResolveReferences();
            if (playerInventory == null) return;

            if (currentPage > 0 && !CanAccessSecondPage()) currentPage = 0;

            int unlocked = playerInventory.UnlockedInventorySlotCount;
            int firstIndex = currentPage * PageSize;
            int lastIndexExclusive = firstIndex + PageSize;
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlotUI slot = slots[i];
                bool visible = slot.slotIndex >= firstIndex
                               && slot.slotIndex < lastIndexExclusive
                               && slot.slotIndex < unlocked;
                if (slot.gameObject.activeSelf != visible) slot.gameObject.SetActive(visible);
            }

            int totalPages = Mathf.Max(1, Mathf.CeilToInt(playerInventory.MaxInventorySlotCount / (float)PageSize));
            if (pageLabel != null) pageLabel.text = $"{currentPage + 1}/{totalPages}";
            if (previousPageButton != null) previousPageButton.interactable = currentPage > 0;
            if (nextPageButton != null) nextPageButton.interactable = currentPage < totalPages - 1 && CanAccessSecondPage();

            bool canUpgrade = unlocked < playerInventory.MaxInventorySlotCount;
            int upgradePage = unlocked < PageSize ? 0 : 1;
            if (upgradeButton != null)
                upgradeButton.gameObject.SetActive(canUpgrade && currentPage == upgradePage);

            if (upgradeButtonRect != null && canUpgrade && currentPage == upgradePage)
            {
                int pageUnlocked = Mathf.Clamp(unlocked - currentPage * PageSize, 0, PageSize);
                int rows = Mathf.CeilToInt(pageUnlocked / (float)ColumnCount);
                Vector2 position = upgradeButtonRect.anchoredPosition;
                position.y = firstRowY - rows * rowStep;
                upgradeButtonRect.anchoredPosition = position;
            }
        }

        private bool CanAccessSecondPage()
        {
            return playerInventory != null
                   && playerInventory.UnlockedInventorySlotCount >= PageSize;
        }
    }
}
