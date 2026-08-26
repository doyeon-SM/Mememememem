using UnityEngine;
using UnityEngine.UI;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 등록 UI의 "보유 스킬" 그리드 한 칸. RecipeSlotUI와 동일한 패턴(SetupSlot에 클릭 콜백을
    /// 넘겨받아 Button.onClick에 연결)을 스킬 데이터에 맞게 사용한다. 클릭 시 실제 동작(즉시 장착 /
    /// 정보만 표시)은 이 칸이 아니라 호출부(SkillRegistrationPanelUI)가 결정한다 - 이 칸은 표시와
    /// 클릭 이벤트 전달만 담당한다. 이름 텍스트는 두지 않는다(이름은 정보 패널에서만 표시) - 대신
    /// 장착됨 강조와, 지금 정보 패널에 표시 중인 카드인지(선택됨) 강조 2가지를 보여준다.
    /// </summary>
    public class SkillGridSlotUI : MonoBehaviour
    {
        [Header("표시용 UI 요소")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Button clickButton;

        [Header("장착됨 표시 (현재 로드아웃 어딘가에 등록된 스킬인 경우)")]
        [SerializeField] private GameObject equippedHighlight;

        [Header("선택됨 표시 (지금 정보 패널에 표시 중인 스킬인 경우)")]
        [SerializeField] private GameObject selectedHighlight;

        private SkillData boundSkillData;
        public SkillData BoundSkillData => boundSkillData;

        /// <summary>이 칸에 스킬 데이터를 채우고, 클릭 시 onClicked에 이 칸의 SkillData를 전달하도록 연결한다.</summary>
        public void SetupSlot(SkillData data, System.Action<SkillData> onClicked)
        {
            boundSkillData = data;
            if (data == null) return;

            if (iconImage != null) iconImage.sprite = data.SkillIcon;

            if (clickButton != null)
            {
                clickButton.onClick.RemoveAllListeners();
                clickButton.onClick.AddListener(() => onClicked?.Invoke(boundSkillData));
            }

            SetEquippedHighlight(false);
            SetSelectedHighlight(false);
        }

        /// <summary>이 칸의 스킬이 현재 로드아웃(4칸 + 특수 칸)에 등록되어 있는지에 따라 강조 표시를 켜고 끈다.</summary>
        public void SetEquippedHighlight(bool equipped)
        {
            if (equippedHighlight != null) equippedHighlight.SetActive(equipped);
        }

        /// <summary>이 칸의 스킬이 지금 정보 패널에 표시 중인 스킬인지에 따라 강조 표시를 켜고 끈다.</summary>
        public void SetSelectedHighlight(bool selected)
        {
            if (selectedHighlight != null) selectedHighlight.SetActive(selected);
        }
    }
}
