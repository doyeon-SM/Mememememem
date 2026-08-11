using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace HDY.UI
{
    /// <summary>
    /// 탐험 지역 카드의 보상 아이콘 한 칸(프리팹). ExplorationPanelUI가 지역의 보상 개수만큼 GridLayoutGroup가
    /// 붙은 부모(스크롤 뷰의 Content, HDY 요청) 밑에 이 프리팹을 그만큼 Instantiate해서 채운다 - 예전에는
    /// 9개를 넘으면 마지막 한 칸을 "..."  오버플로우 표시로 바꾸고 마우스를 올리면 별도 팝업에 나머지를
    /// 보여줬지만, 지금은 그런 제한 없이 전부 채우고 스크롤로 넘겨보는 방식으로 바뀌었다(오버플로우 관련
    /// 코드/이벤트는 전부 제거됨).
    ///
    /// [HDY 요청 - 아이템 이름 표시] itemNameText가 추가되어, SetItem 호출 시 아이콘/수량과 함께 아이템 이름도
    /// 함께 표시한다.
    /// </summary>
    public class ExplorationRewardIconUI : MonoBehaviour
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text amountText;

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
    }
}
