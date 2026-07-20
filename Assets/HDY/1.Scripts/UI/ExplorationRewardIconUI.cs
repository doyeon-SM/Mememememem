using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace HDY.UI
{
    /// <summary>
    /// 탐험 지역 카드의 보상 아이콘 한 칸(프리팹). ExplorationPanelUI가 지역의 보상 개수만큼 GridLayoutGroup가
    /// 붙은 부모 밑에 이 프리팹을 그만큼 Instantiate해서 채운다(9개 초과 시 마지막 한 칸은 "..." 오버플로우
    /// 표시로 대체되고, 남은 보상은 같은 프리팹으로 채워지는 팝업 그리드에서 확인한다).
    /// </summary>
    public class ExplorationRewardIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;

        /// <summary>이 칸이 "..." 오버플로우 표시일 때 마우스가 올라오면 발생. ExplorationPanelUI가 구독해서 남은 목록 팝업을 띄운다.</summary>
        public event Action OnPointerEntered;

        /// <summary>마우스가 벗어나면 발생(팝업을 감추는 데 사용).</summary>
        public event Action OnPointerExited;

        /// <summary>실제 보상 아이템 1건을 표시한다. amountLabel에는 보너스 배율이 반영된 최대수량 문자열이 들어온다.</summary>
        public void SetItem(Sprite sprite, string amountLabel)
        {
            if (icon != null)
            {
                icon.sprite = sprite;
                var color = icon.color;
                color.a = 1f;
                icon.color = color;
            }

            if (amountText != null)
            {
                amountText.text = amountLabel;
            }
        }

        /// <summary>남은 보상이 더 있음을 나타내는 "..." 오버플로우 칸으로 표시를 바꾼다. 아이콘은 투명하게 감춘다.</summary>
        public void SetOverflowIndicator()
        {
            if (icon != null)
            {
                icon.sprite = null;
                var color = icon.color;
                color.a = 0f;
                icon.color = color;
            }

            if (amountText != null)
            {
                amountText.text = "...";
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnPointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnPointerExited?.Invoke();
        }
    }
}
