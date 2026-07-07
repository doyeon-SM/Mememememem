using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.InventoryDuped
{

public class ItemDragUI : MonoBehaviour
{
    public RectTransform rectTransform;
    public Image itemIcon;
    public TMP_Text amountText;

    public void Show(ItemStack stack, Vector2 screenPosition)
    {
        if (stack == null || stack.IsEmpty) return;

        gameObject.SetActive(true);

        itemIcon.enabled = stack.item.ItemIcon != null;
        itemIcon.sprite = stack.item.ItemIcon;
        
        amountText.gameObject.SetActive(stack.amount > 1);
        amountText.text = stack.amount.ToString();
        
        Move(screenPosition);
    }

    public void Move(Vector2 screenPosition)
    {
        rectTransform.position = screenPosition;
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}

}
