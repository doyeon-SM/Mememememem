using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// 보상 미리보기 팝업(TutorialRewardPreviewUI) 안에서 보상 하나(아이콘 + 개수)를 보여주는 슬롯.
    /// ShopSlotUI와 동일하게 표시 전용이라 클릭/호버 등 상호작용은 없다.
    /// </summary>
    public class TutorialRewardSlotUI : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text amountText;

        /// <summary>아이콘과 개수를 채운다. icon이 null이면(예: 아이템 카탈로그에 없는 ID) 아이콘 이미지를 숨긴다.</summary>
        public void Setup(Sprite icon, int amount)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(icon != null);
            }

            if (amountText != null) amountText.text = amount.ToString();
        }
    }
}
