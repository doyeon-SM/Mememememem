using System;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{
    /// <summary>ID/카테고리 정렬 버튼을 이벤트로 전달하는 플레이어 인벤토리 전용 UI.</summary>
    public class InventorySortUI : MonoBehaviour
    {
        [SerializeField] private Button itemIdButton;
        [SerializeField] private Button categoryButton;

        public event Action<InventorySortCriteria> OnSortRequested;

        private void Awake()
        {
            if (itemIdButton != null)
            {
                itemIdButton.onClick.AddListener(RequestItemIdSort);
            }
            else
            {
                Debug.LogWarning("[InventorySortUI] itemIdButton이 비어있습니다.", this);
            }

            if (categoryButton != null)
            {
                categoryButton.onClick.AddListener(RequestCategorySort);
            }
            else
            {
                Debug.LogWarning("[InventorySortUI] categoryButton이 비어있습니다.", this);
            }
        }

        private void OnDestroy()
        {
            if (itemIdButton != null) itemIdButton.onClick.RemoveListener(RequestItemIdSort);
            if (categoryButton != null) categoryButton.onClick.RemoveListener(RequestCategorySort);
        }

        public void Configure(Button idButton, Button categorySortButton)
        {
            itemIdButton = idButton;
            categoryButton = categorySortButton;
        }

        private void RequestItemIdSort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.ItemId);
        }

        private void RequestCategorySort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.Category);
        }
    }
}
