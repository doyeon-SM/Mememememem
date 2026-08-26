using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS.Combat
{
    /// <summary>
    /// [멤] 스킬 등록 UI 하단의 "스킬 정보" 표시 패널. 보유 스킬 카드를 클릭하거나(정보만 확인하는
    /// 경우 포함) 장착/해제 칸을 클릭했을 때, 그 대상 스킬 하나의 상세 정보를 보여준다.
    /// </summary>
    public class SkillInfoPanelUI : MonoBehaviour
    {
        [Header("표시용 UI 요소 (root는 선택사항 - 없으면 항상 텍스트만 갱신)")]
        [SerializeField] private GameObject root;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private TMP_Text gradeText;
        [SerializeField] private TMP_Text formTypeText;

        /// <summary>data의 상세 정보를 표시한다. data가 null이면 Hide()와 동일하다.</summary>
        public void Show(SkillData data)
        {
            if (data == null)
            {
                Hide();
                return;
            }

            if (root != null) root.SetActive(true);
            if (iconImage != null) iconImage.sprite = data.SkillIcon;
            if (nameText != null) nameText.text = data.SkillName;
            if (descriptionText != null) descriptionText.text = data.Description;
            if (damageText != null) damageText.text = data.Damage.ToString();
            if (cooldownText != null) cooldownText.text = $"{data.Cooldown:F1}초";
            if (gradeText != null) gradeText.text = $"{data.Grade}등급";
            if (formTypeText != null) formTypeText.text = GetFormTypeLabel(data.FormType);
        }

        /// <summary>정보 패널을 비운다(root가 지정되어 있으면 그 GameObject를 비활성화한다).</summary>
        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private static string GetFormTypeLabel(SkillFormType formType)
        {
            switch (formType)
            {
                case SkillFormType.Instant: return "즉발형";
                case SkillFormType.Stack: return "스택형";
                case SkillFormType.Buff: return "버프";
                default: return formType.ToString();
            }
        }
    }
}
