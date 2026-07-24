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

        private int selectedIndex;

        private void Awake()
        {
            for (int i = 0; i < filterButtons.Length; i++)
            {
                int index = i;
                if (filterButtons[i] != null)
                    filterButtons[i].onClick.AddListener(() => Select(index));
            }
            if (menuButton != null) menuButton.onClick.AddListener(ToggleSortControls);
            Select(0);
            if (existingSortControls != null) existingSortControls.SetActive(false);
        }

        private void OnDestroy()
        {
            if (menuButton != null) menuButton.onClick.RemoveListener(ToggleSortControls);
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

        private void ToggleSortControls()
        {
            if (existingSortControls != null)
                existingSortControls.SetActive(!existingSortControls.activeSelf);
        }
    }
}
