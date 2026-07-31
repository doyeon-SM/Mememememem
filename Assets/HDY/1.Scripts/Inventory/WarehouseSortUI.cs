using System;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Inventory
{
    /// <summary>
    /// 창고 정렬 버튼들을 감지해서 어떤 기준으로 정렬해야 하는지 이벤트로 올리는 역할만 한다.
    /// 실제 정렬(압축/비교)은 WarehouseInventory가 담당한다 - 이 클래스는 "무엇을 눌렀는지"만 안다.
    /// (MemStorageUI_Sort와 동일한 패턴)
    /// </summary>
    public class WarehouseSortUI : MonoBehaviour
    {
        [Header("정렬 버튼들")]
        [SerializeField] private Button itemIdButton;
        [SerializeField] private Button categoryButton;

        [Header("정렬 버튼들 - 카테고리 우선순위 (HDY 요청)")]
        [SerializeField] private Button toolPriorityButton;
        [SerializeField] private Button materialPriorityButton;
        [SerializeField] private Button foodPriorityButton;

        public event Action<ItemSortCriteria> OnSortRequested;

        private void Awake()
        {
            if (itemIdButton != null) itemIdButton.onClick.AddListener(() => OnSortRequested?.Invoke(ItemSortCriteria.ItemId));
            else Debug.LogWarning("[WarehouseSortUI] itemIdButton이 비어있습니다.", this);

            if (categoryButton != null) categoryButton.onClick.AddListener(() => OnSortRequested?.Invoke(ItemSortCriteria.Category));
            else Debug.LogWarning("[WarehouseSortUI] categoryButton이 비어있습니다.", this);

            if (toolPriorityButton != null) toolPriorityButton.onClick.AddListener(() => OnSortRequested?.Invoke(ItemSortCriteria.ToolPriority));
            else Debug.LogWarning("[WarehouseSortUI] toolPriorityButton이 비어있습니다.", this);

            if (materialPriorityButton != null) materialPriorityButton.onClick.AddListener(() => OnSortRequested?.Invoke(ItemSortCriteria.MaterialPriority));
            else Debug.LogWarning("[WarehouseSortUI] materialPriorityButton이 비어있습니다.", this);

            if (foodPriorityButton != null) foodPriorityButton.onClick.AddListener(() => OnSortRequested?.Invoke(ItemSortCriteria.FoodPriority));
            else Debug.LogWarning("[WarehouseSortUI] foodPriorityButton이 비어있습니다.", this);
        }
    }
}
