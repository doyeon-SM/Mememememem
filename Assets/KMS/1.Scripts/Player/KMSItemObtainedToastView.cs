using HDY.Item;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    public sealed class KMSItemObtainedToastView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text itemNameText;
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text missingIconText;
        [SerializeField] private CanvasGroup canvasGroup;

        public CanvasGroup CanvasGroup => canvasGroup;

        public void SetData(ItemData item, int amount)
        {
            if (item == null) return;

            bool hasIcon = item.ItemIcon != null;
            if (iconImage != null)
            {
                iconImage.sprite = item.ItemIcon;
                iconImage.color = hasIcon ? Color.white : new Color32(60, 60, 64, 255);
                iconImage.preserveAspect = true;
            }

            if (missingIconText != null)
            {
                missingIconText.text = hasIcon ? string.Empty : "?";
                missingIconText.gameObject.SetActive(!hasIcon);
            }

            if (itemNameText != null)
            {
                itemNameText.text = !string.IsNullOrEmpty(item.ItemName)
                    ? item.ItemName
                    : item.Item_ID;
            }

            if (amountText != null) amountText.text = $"X{Mathf.Max(1, amount)}";

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
