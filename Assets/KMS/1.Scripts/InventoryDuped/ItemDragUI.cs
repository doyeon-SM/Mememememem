using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HDY.Item;

namespace KMS.InventoryDuped
{

public class ItemDragUI : MonoBehaviour
{
    public RectTransform rectTransform;
    public Image itemIcon;
    public TMP_Text amountText;

    // [HDY 요청] ItemStack.itemId(string)로 실제 ItemData(아이콘 등)를 조회하기 위한 참조.
    [SerializeField] private ItemCatalogManager catalogManager;

    private void Awake()
    {
        catalogManager = ItemCatalogManager.Resolve(catalogManager);
    }

    public void Show(ItemStack stack, Vector2 screenPosition)
    {
        if (stack == null || stack.IsEmpty) return;

        gameObject.SetActive(true);
        if (catalogManager == null)
        {
            catalogManager = ItemCatalogManager.Resolve(null);
        }

        ItemData data = catalogManager != null ? catalogManager.FindItemData(stack.itemId) : null;

        itemIcon.enabled = data != null && data.ItemIcon != null;
        itemIcon.sprite = data != null ? data.ItemIcon : null;

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
