using UnityEngine;
using UnityEngine.UI;
using TMPro;
using HDY.Item;

namespace HDY.Upgrade
{
    /// <summary>
    /// 업그레이드 팝업에서 재료 비용 한 줄(아이콘 + 이름 + 필요 수량)을 표시하는 행.
    /// 재료 비용 개수는 업그레이드마다 달라질 수 있어(0개~여러 개), 멤창고 그리드 슬롯처럼 씬에 미리 배치해두는
    /// 대신 UpgradePopupUI가 필요한 만큼만 런타임에 Instantiate해서 사용한다.
    /// </summary>
    public class UpgradeMaterialCostRowUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text amountText;

        /// <summary>itemData를 카탈로그에서 찾지 못하면(null) 이름 대신 Item_ID를 그대로 표시한다.</summary>
        public void SetData(ItemData itemData, string itemId, int amount)
        {
            if (iconImage != null)
            {
                iconImage.sprite = itemData != null ? itemData.ItemIcon : null;
                iconImage.gameObject.SetActive(itemData != null && itemData.ItemIcon != null);
            }

            if (nameText != null)
            {
                nameText.text = itemData != null ? itemData.ItemName : itemId;
            }

            if (amountText != null)
            {
                amountText.text = $"x{amount}";
            }
        }
    }
}
