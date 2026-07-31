using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Cook
{
    /// <summary>
    /// 모닥불 요리 재료 전용 아이콘/수량 슬롯 컴포넌트 (BG_Ingredient 하위에 생성)
    /// </summary>
    public class CookingRecipeSlotIconUI : MonoBehaviour
    {
        [Header("UI 요소 참조")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText; 

        /// <summary>
        /// 재료 데이터 및 보유/요구 수량을 세팅합니다.
        /// </summary>
        public void SetupSlot(ItemData materialItem, int ownedAmount = -1, int requiredAmount = -1)
        {
            if (iconImage == null) iconImage = GetComponentInChildren<Image>();

            if (materialItem != null && iconImage != null)
            {
                iconImage.sprite = materialItem.ItemIcon;
                iconImage.gameObject.SetActive(materialItem.ItemIcon != null);
            }

            if (amountText != null)
            {
                if (ownedAmount >= 0 && requiredAmount >= 0)
                {
                    amountText.text = $"{ownedAmount}/{requiredAmount}";
                    amountText.color = ownedAmount >= requiredAmount ? Color.white : Color.red;
                    amountText.gameObject.SetActive(true);
                }
                else
                {
                    amountText.gameObject.SetActive(false);
                }
            }
        }
    }
}