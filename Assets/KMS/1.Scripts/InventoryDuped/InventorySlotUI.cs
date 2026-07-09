using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HDY.Item;

namespace KMS.InventoryDuped
{

public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public Image itemIcon;
    public TMP_Text amountText;
    public TMP_Text keyText;
    public GameObject selectedFrame;

    public bool isQuickSlot;
    public int slotIndex;

    // [HDY 요청] ItemStack.itemId(string)로 실제 ItemData(아이콘 등)를 조회하기 위한 참조.
    [SerializeField] private ItemCatalogManager catalogManager;

    private InventoryUI owner;
    private ItemStack currentStack;

    private void Awake()
    {
        catalogManager = ItemCatalogManager.Resolve(catalogManager);
    }

    public void Initialize(InventoryUI newOwner, bool quickSlot, int index)
    {
        owner = newOwner;
        isQuickSlot = quickSlot;
        slotIndex = index;

        SetSelected(false);
    }

    public void SetStack(ItemStack stack)
    {
        currentStack = stack;

        bool hasItem = stack != null && !stack.IsEmpty;

        // [HDY 요청] 슬롯에는 itemId만 있으므로 표시를 위해 카탈로그에서 ItemData를 다시 조회한다.
        ItemData data = (hasItem && catalogManager != null) ? catalogManager.FindItemData(stack.itemId) : null;

        if (itemIcon != null)
        {
            itemIcon.enabled = data != null && data.ItemIcon != null;
            itemIcon.sprite = data != null ? data.ItemIcon : null;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(hasItem && stack.amount > 1);
            amountText.text = hasItem ? stack.amount.ToString() : string.Empty;
        }
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.BeginSlotDrag(this, currentStack, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.MoveSlotDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        InventorySlotUI target = eventData.pointerCurrentRaycast.gameObject != null ?
            eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlotUI>() : null;

        owner?.EndSlotDrag(target);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowItemTooltip(currentStack, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.MoveItemTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideItemTooltip();
    }
}

}
