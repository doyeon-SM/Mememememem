using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;

namespace HDY.Recipe
{
    public class RequireMaterialItemUI : MonoBehaviour
    {
        [Header("요구 재료 슬롯 UI 컴포넌트")]
        [SerializeField] private Image materialIcon;     
        [SerializeField] private TextMeshProUGUI materialName; 
        [SerializeField] private TextMeshProUGUI amountText;   

        /// <summary>
        /// 재료의 정보와 유저 설정 수량 배수를 계산하여 슬롯 정보 동기화
        /// </summary>
        public void SetupMaterialSlot(ItemData data, int requiredUnitAmount, int craftQuantity)
        {
            if (data == null) return;

            if (materialIcon != null) materialIcon.sprite = data.ItemIcon;
            if (materialName != null) materialName.text = data.ItemName;

            int totalRequiredAmount = requiredUnitAmount * craftQuantity;

            int currentInventoryOwned = GetInventoryItemCountMock(data.Item_ID);

            if (amountText != null)
            {
                amountText.text = $"{currentInventoryOwned} / {totalRequiredAmount}";

                if (currentInventoryOwned < totalRequiredAmount)
                {
                    amountText.color = Color.red;
                }
                else
                {
                    amountText.color = Color.white;
                }
            }
        }

        /// <summary>
        /// 인벤토리 아이템 보유 개수 탐색용 임시코드 추후 수정필요.
        /// </summary>
        private int GetInventoryItemCountMock(string itemId)
        {
            if (itemId == "item_wood") return 62;
            if (itemId == "item_irongemstone") return 39;
            return 50; 
        }
    }
}