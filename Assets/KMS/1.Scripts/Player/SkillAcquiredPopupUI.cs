using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    // [멤] 스킬북 사용으로 스킬을 획득했을 때 잠깐 떴다가 사라지는 안내 팝업.
    // 정해진 시간(기본 1초) 동안만 활성화되었다가 자동으로 비활성화된다. 큐/페이드 없이 단순 표시.
    public sealed class SkillAcquiredPopupUI : MonoBehaviour
    {
        [Header("표시 요소")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;

        [Header("표시 시간")]
        [Tooltip("팝업이 화면에 보여지는 시간(초). 이 시간이 지나면 자동으로 비활성화된다.")]
        [SerializeField, Min(0.1f)] private float visibleDuration = 1f;

        private Coroutine hideRoutine;

        private void Awake()
        {
            // [멤] 씬 시작 시 실수로 켜진 상태로 저장되어 있어도 항상 닫힌 채로 시작한다.
            gameObject.SetActive(false);
        }

        // [멤] 스킬 획득 시 PlayerSkillBookController -> PlayerHUD -> KMSPlayerHudView를 거쳐 호출된다.
        public void Show(KMS.Combat.SkillData skill)
        {
            if (skill == null) return;

            if (iconImage != null)
            {
                iconImage.sprite = skill.SkillIcon;
                iconImage.enabled = skill.SkillIcon != null;
            }

            if (nameText != null)
            {
                nameText.text = skill.SkillName;
            }

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
            }

            gameObject.SetActive(true);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private System.Collections.IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, visibleDuration));
            hideRoutine = null;
            gameObject.SetActive(false);
        }
    }
}
