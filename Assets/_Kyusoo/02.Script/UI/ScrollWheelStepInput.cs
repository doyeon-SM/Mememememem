using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// [멤] 상점 팝업창/제작대/요리시설(주방, 모닥불) 등 수량 조절 UI에서, 숫자 표시나 +/- 버튼 위에
/// 마우스 커서가 있을 때만 휠 스크롤로 수량을 조절할 수 있게 해주는 공용 입력 어댑터.
/// 화면 아무데서나 반응하는 게 아니라, 이 컴포넌트가 붙어있는 GameObject(숫자 텍스트, 버튼) 위에서만
/// UnityEngine.EventSystems를 통해 OnScroll이 호출된다.
///
/// [사용법 - 멤] 각 패널의 Awake()에서 숫자 텍스트/버튼 GameObject에 AddComponent로 붙이고
/// OnWheelStep 이벤트를 구독한다. 이 컴포넌트 자체는 "휠이 어느 방향으로 굴러갔는지"(+1/-1)만
/// 알려줄 뿐, 실제 수량 계산(최대치 대비 스텝 크기, clamp 등)은 각 패널이 담당한다 - 패널마다
/// 최대 수량 필드 이름과 clamp 로직이 서로 달라서(maxQuantity/maxCraftableQuantity/maxCookableQuantity)
/// 계산까지 이 컴포넌트에 넣으면 오히려 결합도가 높아지기 때문.
/// </summary>
public class ScrollWheelStepInput : MonoBehaviour, IScrollHandler
{
    /// <summary>휠을 위로 굴리면 +1, 아래로 굴리면 -1이 전달된다.</summary>
    public event System.Action<int> OnWheelStep;

    private void Awake()
    {
        // [멤] Graphic(Text/Image 등)의 raycastTarget이 꺼져 있으면 이 GameObject 위에서
        // 포인터 이벤트 자체가 감지되지 않아 휠 스크롤이 동작하지 않는다. 이 컴포넌트가 붙는
        // 시점에 강제로 켜서, 프리팹 설정과 무관하게 항상 동작하도록 보장한다.
        var graphic = GetComponent<UnityEngine.UI.Graphic>();
        if (graphic != null && !graphic.raycastTarget)
        {
            graphic.raycastTarget = true;
        }
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (eventData.scrollDelta.y == 0f) return;

        int direction = eventData.scrollDelta.y > 0f ? 1 : -1;
        OnWheelStep?.Invoke(direction);
    }
}
