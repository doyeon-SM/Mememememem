using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// Connects the two implemented inventory sort actions. The material, food,
    /// and tool buttons remain visual placeholders until filtering is implemented.
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
        }

    }
}
