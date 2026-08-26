using UnityEngine;
using UnityEngine.UI;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 등록 UI 우측 상단의 장착 칸 하나(1~4등급 칸 또는 5등급 특수 칸 공용으로 사용).
    /// 비어있으면 흐린 아이콘, 스킬이 있으면 정상 아이콘을 보여준다. 클릭 시에는 "이 칸을
    /// 클릭했다"만 알리고, 실제로 해제할지 판단하는 것은 호출부(SkillRegistrationPanelUI)가
    /// 담당한다 - 빈 칸을 눌러도 해제할 스킬이 없으므로 호출부에서 자연히 무시된다.
    /// </summary>
    public class SkillEquipSlotUI : MonoBehaviour
    {
        [Header("표시용 UI 요소")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Color emptyIconColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color filledIconColor = Color.white;
        [SerializeField] private Button clickButton;

        private SkillData boundSkillData;
        public SkillData BoundSkillData => boundSkillData;
        public bool IsEmpty => boundSkillData == null;

        private void Awake()
        {
            SetSkill(null, null);
        }

        /// <summary>이 칸에 표시할 스킬 데이터를 갱신한다(null이면 빈 칸으로 표시). 클릭 콜백은 매번 새로 연결한다.</summary>
        public void SetSkill(SkillData data, System.Action onClicked)
        {
            boundSkillData = data;

            if (iconImage != null)
            {
                iconImage.sprite = data != null ? data.SkillIcon : null;
                iconImage.color = data != null ? filledIconColor : emptyIconColor;
            }

            if (clickButton != null)
            {
                clickButton.onClick.RemoveAllListeners();
                if (onClicked != null) clickButton.onClick.AddListener(() => onClicked.Invoke());
            }
        }
    }
}
