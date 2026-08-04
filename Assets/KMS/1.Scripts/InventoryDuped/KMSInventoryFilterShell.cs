using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// Connects the visible inventory category buttons to the hidden sort controls.
    /// Button order: category, tool, material, food.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSInventoryFilterShell : MonoBehaviour
    {
        [SerializeField] private Button[] filterButtons;
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject existingSortControls;
        private InventorySortUI sortUI;

        private void Awake()
        {
            sortUI = existingSortControls != null
                ? existingSortControls.GetComponent<InventorySortUI>()
                : null;

            if (sortUI != null)
            {
                if (menuButton != null)
                    menuButton.onClick.AddListener(sortUI.RequestItemIdSort);
                if (filterButtons.Length > 0 && filterButtons[0] != null)
                    filterButtons[0].onClick.AddListener(sortUI.RequestCategorySort);
                if (filterButtons.Length > 1 && filterButtons[1] != null)
                    filterButtons[1].onClick.AddListener(sortUI.RequestToolPrioritySort);
                if (filterButtons.Length > 2 && filterButtons[2] != null)
                    filterButtons[2].onClick.AddListener(sortUI.RequestMaterialPrioritySort);
                if (filterButtons.Length > 3 && filterButtons[3] != null)
                    filterButtons[3].onClick.AddListener(sortUI.RequestFoodPrioritySort);
            }
            else
            {
                Debug.LogWarning("[KMSInventoryFilterShell] InventorySortUI reference is missing.", this);
            }

            if (existingSortControls != null) existingSortControls.SetActive(false);
        }

        private void OnDestroy()
        {
            if (sortUI == null) return;

            if (menuButton != null)
                menuButton.onClick.RemoveListener(sortUI.RequestItemIdSort);
            if (filterButtons.Length > 0 && filterButtons[0] != null)
                filterButtons[0].onClick.RemoveListener(sortUI.RequestCategorySort);
            if (filterButtons.Length > 1 && filterButtons[1] != null)
                filterButtons[1].onClick.RemoveListener(sortUI.RequestToolPrioritySort);
            if (filterButtons.Length > 2 && filterButtons[2] != null)
                filterButtons[2].onClick.RemoveListener(sortUI.RequestMaterialPrioritySort);
            if (filterButtons.Length > 3 && filterButtons[3] != null)
                filterButtons[3].onClick.RemoveListener(sortUI.RequestFoodPrioritySort);
        }

    }
}
