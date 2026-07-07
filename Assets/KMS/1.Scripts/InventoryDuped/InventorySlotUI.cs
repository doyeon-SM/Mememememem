using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

    private InventoryUI owner;
    private ItemStack currentStack;

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

        if (itemIcon != null)
        {
            itemIcon.enabled = hasItem && stack.item.icon != null;
            itemIcon.sprite = hasItem ? stack.item.icon : null;
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
