using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>
    /// Temporary visual shell for the future inventory filters. Buttons only
    /// change their selected appearance; filtering is deliberately not applied.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSInventoryFilterShell : MonoBehaviour
    {
        [SerializeField] private Button[] filterButtons;
        [SerializeField] private Button menuButton;
        [SerializeField] private GameObject existingSortControls;
        [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.22f);
        [SerializeField] private Color selectedColor = new Color(1f, 1f, 1f, 0.55f);

        private int selectedIndex = -1;
        private InventorySortUI sortUI;

        private void Awake()
        {
            // Filter0 used to be the visual-only ALL filter. It is now the
            // category sort button, so only the remaining future filters keep
            // the temporary selection behaviour.
            for (int i = 1; i < filterButtons.Length; i++)
            {
                int index = i;
                if (filterButtons[i] != null)
                    filterButtons[i].onClick.AddListener(() => Select(index));
            }

            sortUI = existingSortControls != null
                ? existingSortControls.GetComponent<InventorySortUI>()
                : null;

            SetButtonLabel(menuButton, "ID");
            if (filterButtons.Length > 0) SetButtonLabel(filterButtons[0], "C");

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

        private void Select(int index)
        {
            selectedIndex = Mathf.Clamp(index, 0, Mathf.Max(0, filterButtons.Length - 1));
            for (int i = 0; i < filterButtons.Length; i++)
            {
                if (filterButtons[i] == null || filterButtons[i].targetGraphic == null) continue;
                filterButtons[i].targetGraphic.color = i == selectedIndex ? selectedColor : normalColor;
            }
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = label;

            Transform icon = button.transform.Find("ModernUIIcon");
            if (icon != null) icon.gameObject.SetActive(false);
        }
    }
}
