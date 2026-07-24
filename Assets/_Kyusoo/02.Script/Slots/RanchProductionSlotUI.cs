using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanchProductionSlotUI : MonoBehaviour
{
    [Header("UI 상태 오브젝트")]
    [SerializeField] private GameObject lockOverlay;
    [SerializeField] private GameObject emptyStateObject;
    [SerializeField] private GameObject producingStateObject;

    [Header("생산 정보 컴포넌트")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI storageCountText;
    [SerializeField] private Slider progressBar;

    /// <summary>
    /// 슬롯의 정적 정보(해금 상태, 아이콘 등)를 갱신합니다.
    /// </summary>
    public void RefreshSlot(RanchSlotRuntime slotData)
    {
        if (slotData == null) return;

        if (!slotData.isUnlocked)
        {
            if (lockOverlay != null) lockOverlay.SetActive(true);
            if (emptyStateObject != null) emptyStateObject.SetActive(false);
            if (producingStateObject != null) producingStateObject.SetActive(false);
            return;
        }

        if (lockOverlay != null) lockOverlay.SetActive(false);

        if (slotData.deployedMem == null || string.IsNullOrEmpty(slotData.craftingItemId))
        {
            if (emptyStateObject != null) emptyStateObject.SetActive(true);
            if (producingStateObject != null) producingStateObject.SetActive(false);
        }
        else
        {
            if (emptyStateObject != null) emptyStateObject.SetActive(false);
            if (producingStateObject != null) producingStateObject.SetActive(true);

            // ItemCatalogManager에서 아이콘 바인딩
            ItemData itemData = FindItemDataInCatalog(slotData.craftingItemId);
            if (itemData != null && itemIcon != null)
            {
                itemIcon.sprite = itemData.ItemIcon;
                itemIcon.gameObject.SetActive(true);
            }

            UpdateDynamicProgress(slotData);
        }
    }

    /// <summary>
    /// Update() 루프용 동적 진행도 및 수량 텍스트 갱신
    /// </summary>
    public void UpdateDynamicProgress(RanchSlotRuntime slotData)
    {
        if (slotData == null || slotData.deployedMem == null) return;

        if (storageCountText != null)
        {
            storageCountText.text = slotData.currentStorageCount.ToString();
        }

        if (progressBar != null)
        {
            float progressNormalized = slotData.totalRequiredTime > 0f
                ? slotData.currentProgressTime / slotData.totalRequiredTime
                : 0f;

            progressBar.value = Mathf.Clamp01(progressNormalized);
        }
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }
}