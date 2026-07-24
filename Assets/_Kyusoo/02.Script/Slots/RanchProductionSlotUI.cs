using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanchProductionSlotUI : MonoBehaviour
{
    [Header("Ranch_Item_Slot 하이러키 컴포넌트")]
    [Tooltip("슬롯의 슬롯 배경 이미지 (잠김 시 검은색으로 변경)")]
    [SerializeField] private Image backgroundImage;

    [Tooltip("Crafting_Item (Image)")]
    [SerializeField] private Image itemIcon;

    [Tooltip("Crafting_Count (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI storageCountText;

    [Tooltip("ProgressBar (Slider)")]
    [SerializeField] private Slider progressBar;

    /// <summary>
    /// 슬롯의 해금 상태, 아이콘을 갱신합니다.
    /// </summary>
    public void RefreshSlot(RanchSlotRuntime slotData)
    {
        if (slotData == null) return;

        // 1. 잠금 상태 처리 
        if (!slotData.isUnlocked)
        {
            if (backgroundImage != null) backgroundImage.color = Color.black;
            if (itemIcon != null) itemIcon.gameObject.SetActive(false);
            if (storageCountText != null) storageCountText.text = "";
            if (progressBar != null) progressBar.value = 1f;
            return;
        }

        // 해금된 슬롯: 배경 색상 흰색 원복
        if (backgroundImage != null) backgroundImage.color = Color.white;

        // 2. 멤 미배치 / 생산 아이템 없음 처리
        if (slotData.deployedMem == null || string.IsNullOrEmpty(slotData.craftingItemId))
        {
            if (itemIcon != null) itemIcon.gameObject.SetActive(false);
            if (storageCountText != null) storageCountText.text = "0";
            if (progressBar != null) progressBar.value = 1f;
        }
        else
        {
            // 3. 정상 가동 / 수량 축적 상태
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

    /// <summary>
    /// 진행도, 수량 텍스트 갱신
    /// </summary>
    public void UpdateDynamicProgress(RanchSlotRuntime slotData)
    {
        if (slotData == null) return;

        // 개별 생산 수량 실시간 카운팅
        if (storageCountText != null)
        {
            storageCountText.text = slotData.currentStorageCount.ToString();
        }

        // Slider 진행도: 1에서 0으로 감소 처리
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

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }
}