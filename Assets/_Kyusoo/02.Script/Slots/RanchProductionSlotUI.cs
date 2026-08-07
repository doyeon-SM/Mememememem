using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanchProductionSlotUI : MonoBehaviour
{
    [Header("Ranch_Item_Slot 하이러키 컴포넌트")]
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI storageCountText;
    [SerializeField] private Slider progressBar;

    [Header("개별 허기 경고 아이콘")]
    [SerializeField] private RanchWarningIconUI warningIconUI;

    public void RefreshSlot(RanchSlotRuntime slotData)
    {
        if (slotData == null) return;

        if (!slotData.isUnlocked)
        {
            if (backgroundImage != null) backgroundImage.color = Color.black;
            if (itemIcon != null) itemIcon.gameObject.SetActive(false);
            if (storageCountText != null) storageCountText.text = "";
            if (progressBar != null) progressBar.value = 1f;
            if (warningIconUI != null) warningIconUI.UpdateWarningStatus(null);
            return;
        }

        if (backgroundImage != null) backgroundImage.color = new Color(0f, 0f, 0f, 0f);

        if (slotData.deployedMem == null || string.IsNullOrEmpty(slotData.craftingItemId))
        {
            if (itemIcon != null) itemIcon.gameObject.SetActive(false);
            if (storageCountText != null) storageCountText.text = "0";
            if (progressBar != null) progressBar.value = 1f;
            if (warningIconUI != null) warningIconUI.UpdateWarningStatus(null);
        }
        else
        {
            ItemData itemData = FindItemDataInCatalog(slotData.craftingItemId);
            if (itemData != null && itemIcon != null)
            {
                itemIcon.sprite = itemData.ItemIcon;
                itemIcon.color = Color.white;
                itemIcon.gameObject.SetActive(true);
            }

            UpdateDynamicProgress(slotData);
        }
    }

    public void UpdateDynamicProgress(RanchSlotRuntime slotData)
    {
        if (slotData == null) return;

        // 개별 멤의 허기 상태 실시간 전달
        if (warningIconUI != null)
        {
            warningIconUI.UpdateWarningStatus(slotData.deployedMemEntry);
        }

        if (storageCountText != null)
        {
            storageCountText.text = slotData.currentStorageCount.ToString();
        }

        if (progressBar != null)
        {
            if (slotData.isProducing && slotData.totalRequiredTime > 0f)
            {
                float progressNormalized = slotData.currentProgressTime / slotData.totalRequiredTime;
                progressBar.value = Mathf.Clamp01(1f - progressNormalized);
            }
            else
            {
                progressBar.value = 1f;
            }
        }
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (ItemCatalogManager.Instance == null) return null;
        return ItemCatalogManager.Instance.FindItemData(itemId);
    }
}