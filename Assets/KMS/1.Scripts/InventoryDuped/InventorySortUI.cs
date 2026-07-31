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

        [Header("카테고리 우선순위 정렬 버튼들 (HDY 요청)")]
        [SerializeField] private Button toolPriorityButton;
        [SerializeField] private Button materialPriorityButton;
        [SerializeField] private Button foodPriorityButton;

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

            if (toolPriorityButton != null)
            {
                toolPriorityButton.onClick.AddListener(RequestToolPrioritySort);
            }
            else
            {
                Debug.LogWarning("[InventorySortUI] toolPriorityButton이 비어있습니다.", this);
            }

            if (materialPriorityButton != null)
            {
                materialPriorityButton.onClick.AddListener(RequestMaterialPrioritySort);
            }
            else
            {
                Debug.LogWarning("[InventorySortUI] materialPriorityButton이 비어있습니다.", this);
            }

            if (foodPriorityButton != null)
            {
                foodPriorityButton.onClick.AddListener(RequestFoodPrioritySort);
            }
            else
            {
                Debug.LogWarning("[InventorySortUI] foodPriorityButton이 비어있습니다.", this);
            }
        }

        private void OnDestroy()
        {
            if (itemIdButton != null) itemIdButton.onClick.RemoveListener(RequestItemIdSort);
            if (categoryButton != null) categoryButton.onClick.RemoveListener(RequestCategorySort);
            if (toolPriorityButton != null) toolPriorityButton.onClick.RemoveListener(RequestToolPrioritySort);
            if (materialPriorityButton != null) materialPriorityButton.onClick.RemoveListener(RequestMaterialPrioritySort);
            if (foodPriorityButton != null) foodPriorityButton.onClick.RemoveListener(RequestFoodPrioritySort);
        }

        public void Configure(Button idButton, Button categorySortButton)
        {
            itemIdButton = idButton;
            categoryButton = categorySortButton;
        }

        public void RequestItemIdSort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.ItemId);
        }

        public void RequestCategorySort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.Category);
        }

        /// <summary>도구우선: 도구 -> 캡슐 -> 설계도 -> 이후 카테고리순.</summary>
        public void RequestToolPrioritySort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.ToolPriority);
        }

        /// <summary>재료우선: 굿즈 -> 재료 -> 이후 카테고리순.</summary>
        public void RequestMaterialPrioritySort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.MaterialPriority);
        }

        /// <summary>음식우선: 음식 -> 이후 카테고리순.</summary>
        public void RequestFoodPrioritySort()
        {
            OnSortRequested?.Invoke(InventorySortCriteria.FoodPriority);
        }
    }
}
