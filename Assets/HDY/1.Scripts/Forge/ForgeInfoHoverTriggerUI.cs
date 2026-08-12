using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HDY.Forge
{
    /// <summary>
    /// [HDY 요청 - 도움말 안내 패널용 별개 컴포넌트] 마우스가 올라오고/벗어나는 것만 이벤트로 알려주는
    /// 아주 단순한 트리거. 어떤 안내 패널을 보여줄지, 컨테이너를 어떻게 켜고 끌지 같은 Forge 관련 로직은
    /// 전혀 모른다 - 그건 전부 ForgeUI(구독하는 쪽)가 판단한다. 그래서 강화/승급처럼 "같은 아이콘인데 지금
    /// 탭에 따라 보여줄 안내가 달라지는" 경우와 연마/전승처럼 "항상 고정된 안내만 보여주는" 경우를 전부
    /// ForgeUI 쪽에서 자유롭게 처리할 수 있다(이 컴포넌트를 나눌 필요가 없음).
    ///
    /// [기존 ItemTooltipTriggerUI와 별개인 이유] ItemTooltipTriggerUI(KMS)는 ItemStack/ItemData 기반의
    /// 아이템 툴팁 전용이라 이번처럼 고정 안내 문구를 미리 배치해둔 패널을 켜고 끄는 용도와는 맞지 않아서,
    /// 아무 데이터도 모르는 이 컴포넌트를 새로 만들었다.
    /// </summary>
    public class ForgeInfoHoverTriggerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action OnHoverEnter;
        public event Action OnHoverExit;

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHoverEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnHoverExit?.Invoke();
        }
    }
}
