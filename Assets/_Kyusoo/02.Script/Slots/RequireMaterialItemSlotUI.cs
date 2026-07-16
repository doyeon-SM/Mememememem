using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;
using KMS.InventoryDuped;
using HDY.Inventory;

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

            int currentInventoryOwned = GetRealTotalItemCount(data.Item_ID);

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
        /// PlayerInventory와 WarehouseInventory의 존재하는 아이템을 itemId로 찾아 실제 보유량 갱신
        /// </summary>
        private int GetRealTotalItemCount(string itemId)
        {
            int totalOwned = 0;

            var inventory = FindFirstObjectByType<PlayerInventory>();
            var warehouse = FindFirstObjectByType<WarehouseInventory>();

            if (inventory != null) totalOwned += inventory.GetItemAmount(itemId);
            if (warehouse != null) totalOwned += warehouse.GetItemAmount(itemId);

            return totalOwned;
        }
    }
}