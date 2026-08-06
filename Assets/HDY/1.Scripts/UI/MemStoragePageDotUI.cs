using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HDY.UI
{
    /// <summary>
    /// [HDY 요청] 멤창고 페이지 점(dot) 하나를 나타내는 마커 컴포넌트.
    /// MemStorageUI_Grid가 pageDotsParent 하위에서 Image가 아니라 반드시 이 컴포넌트를 기준으로 점을
    /// 수집한다 - 같은 부모 밑에 배치된 업그레이드 버튼(역시 Image를 갖고 있음)이 실수로 페이지 점으로
    /// 오인식되어 페이지 점 표시/숨김 로직에 휘말리는 문제가 있었다(실제로 업그레이드 버튼을 페이지 점
    /// 부모 밑에 배치했을 때, 총 페이지 수 기준으로는 "안 보여야 할 점"으로 판정되어 버튼이 꺼져버렸다).
    /// 이 마커 컴포넌트가 없는 UI 요소(업그레이드 버튼 등)는 같은 부모 아래 있어도 점으로 취급되지 않는다.
    ///
    /// [클릭 이동] IPointerClickHandler를 직접 구현해서(Button 컴포넌트 불필요) 클릭 시 OnClicked를
    /// 발행한다. 몇 번째 점인지는 Grid가 리스트 인덱스로 판단해서 해당 페이지로 이동시킨다 - 이 컴포넌트
    /// 자신은 페이지 번호를 전혀 모른다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class MemStoragePageDotUI : MonoBehaviour, IPointerClickHandler
    {
        public RectTransform RectTransform => transform as RectTransform;

        /// <summary>이 점이 클릭되었을 때 발생.</summary>
        public event Action OnClicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            OnClicked?.Invoke();
        }
    }
}
