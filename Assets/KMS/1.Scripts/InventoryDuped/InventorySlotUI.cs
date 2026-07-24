using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HDY.Item;

namespace KMS.InventoryDuped
{

/// <summary>
/// 인벤토리/퀵슬롯/창고 공용 슬롯 UI. [HDY 요청] owner를 IInventorySlotOwner 인터페이스로 일반화하고
/// isQuickSlot(bool) 대신 SlotGroup(enum)을 사용해서, InventoryUI(플레이어 전용)와 WarehouseUI(창고+인벤토리
/// 통합) 양쪽 모두에서 이 컴포넌트를 그대로 재사용할 수 있게 했다.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public Image itemIcon;
    public TMP_Text amountText;
    public TMP_Text keyText;
    public GameObject selectedFrame;

    public SlotGroup group;
    public int slotIndex;

    // [HDY 요청] ItemStack.itemId(string)로 실제 ItemData(아이콘 등)를 조회하기 위한 참조.
    [SerializeField] private ItemCatalogManager catalogManager;

    private IInventorySlotOwner owner;
    private ItemStack currentStack;

    private void Awake()
    {
        catalogManager = ItemCatalogManager.Resolve(catalogManager);
    }

    public void Initialize(IInventorySlotOwner newOwner, SlotGroup newGroup, int index)
    {
        owner = newOwner;
        group = newGroup;
        slotIndex = index;

        if (newGroup == SlotGroup.Trash && keyText != null)
        {
            keyText.gameObject.SetActive(false);
        }

        SetSelected(false);
    }

    public void SetStack(ItemStack stack)
    {
        currentStack = stack;

        bool hasItem = stack != null && !stack.IsEmpty;
        if (hasItem && catalogManager == null)
        {
            catalogManager = ItemCatalogManager.Resolve(null);
        }

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
        if (owner is IInventorySlotClickOwner) return;
        owner?.BeginSlotDrag(this, currentStack, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner) return;
        owner?.MoveSlotDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner) return;

        InventorySlotUI target = eventData.pointerCurrentRaycast.gameObject != null ?
            eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlotUI>() : null;

        owner?.EndSlotDrag(target);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner clickOwner)
        {
            clickOwner.ClickSlot(this, eventData.button, eventData.position);
        }
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
