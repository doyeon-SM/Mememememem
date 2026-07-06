using UnityEngine;
using UnityEngine.EventSystems;

public class ItemTooltipTriggerUI : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    public ItemTooltipUI itemTooltipUI;

    private ItemDefinition currentItem;
    private bool isPointerInside;

    // 툴팁으로 표시할 아이템 데이터를 설정
    public void SetItem(ItemDefinition item)
    {
        currentItem = item;
    }

    // 마우스가 올라오면 현재 아이템 툴팁을 표시
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItem == null || itemTooltipUI == null) return;

        isPointerInside = true;
        itemTooltipUI.Show(currentItem, eventData.position);
    }

    // 마우스 이동에 맞춰 툴팁 위치를 갱신
    public void OnPointerMove(PointerEventData eventData)
    {
        if (currentItem == null || itemTooltipUI == null) return;

        itemTooltipUI.Move(eventData.position);
    }

    // 마우스가 벗어나면 툴팁을 숨김
    public void OnPointerExit(PointerEventData eventData)
    {
        itemTooltipUI.Hide();
    }

    private void OnDisable()
    {
        HideTooltip();
    }

    // 현재 툴팁을 숨김
    private void HideTooltip()
    {
        if (!isPointerInside) return;

        isPointerInside = false;

        if (itemTooltipUI == null) return;

        itemTooltipUI.Hide();
    }
}
