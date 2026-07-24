using System.Collections.Generic;
using HDY.Forge;
using HDY.Item;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KMS.InventoryDuped
{

public class ItemTooltipTriggerUI : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    public ItemTooltipUI itemTooltipUI;

    private ItemData currentItem;
    private IReadOnlyList<ForgeRefinementSlotData> refinementOverride;
    private bool isPointerInside;

    // [HDY 요청] itemTooltipUI를 프리팹에서 직접 연결하기 어려운 경우(다른 씬/캔버스에 걸쳐 재사용되는
    // 공용 프리팹 등)를 위해, 비어있으면 씬에서 자동으로 찾아 연결한다(ItemCatalogManager.Resolve와 동일한 패턴).
    private void Awake()
    {
        if (itemTooltipUI == null)
        {
            itemTooltipUI = FindFirstObjectByType<ItemTooltipUI>();
        }
    }

    // 툴팁으로 표시할 아이템 데이터를 설정
    public void SetItem(ItemData item)
    {
        currentItem = item;
        refinementOverride = null; // 일반 바인딩이면 미리보기 오버라이드는 해제한다.
    }

    /// <summary>
    /// [HDY 요청] 미리보기 전용 - 표시 아이템(이름/종류/아이콘 등)은 item 그대로 쓰되, 연마 효과
    /// 태그만 overrideSlots로 대체한다(전승 결과 미리보기처럼 아직 실제로 일어나지 않은 결과를
    /// 보여줄 때 사용 - ForgeToolSlotUI.BindPreview에서 호출됨).
    /// </summary>
    public void SetItemWithRefinementOverride(ItemData item, IReadOnlyList<ForgeRefinementSlotData> overrideSlots)
    {
        currentItem = item;
        refinementOverride = overrideSlots;
    }

    // 마우스가 올라오면 현재 아이템 툴팁을 표시
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentItem == null || itemTooltipUI == null) return;

        isPointerInside = true;

        if (refinementOverride != null)
        {
            itemTooltipUI.ShowWithRefinementOverride(currentItem, refinementOverride, eventData.position);
        }
        else
        {
            itemTooltipUI.Show(currentItem, eventData.position);
        }
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

}
