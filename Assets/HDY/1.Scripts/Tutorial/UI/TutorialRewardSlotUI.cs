using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// 보상 미리보기 팝업(TutorialRewardPreviewUI) 안에서 보상 하나(아이콘 + 개수 + 이름)를 보여주는 슬롯.
    /// ShopSlotUI와 동일하게 표시 전용이라 클릭/호버 등 상호작용은 없다.
    ///
    /// [HDY 요청 - 아이템 이름 표시] itemNameText가 추가되어, Setup 호출 시 아이콘/개수와 함께 아이템 이름도
    /// 함께 표시한다.
    /// </summary>
    public class TutorialRewardSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text itemNameText;

        /// <summary>아이콘/개수/이름을 채운다. icon이 null이면(예: 아이템 카탈로그에 없는 ID) 아이콘 이미지를 숨긴다.</summary>
        public void Setup(Sprite icon, int amount, string itemName)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }

            if (amountText != null) amountText.text = amount.ToString();

            if (itemNameText != null) itemNameText.text = itemName;
        }
    }
}
