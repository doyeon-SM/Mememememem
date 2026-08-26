using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KMS
{
    /// <summary>
    /// [멤] 스킬 로드아웃 한 칸(등급 1~4칸 중 하나)의 표시를 담당하는 uGUI 뷰. 아이콘/쿨타임 오버레이/
    /// "지금 해제하면 큐에 들어갈 단계"(장전 완료) 강조까지 이 컴포넌트 하나가 전부 그린다.
    /// 데이터는 전부 KMSPlayerHudView(PlayerHUD가 오케스트레이션)가 넘겨준다 - 이 컴포넌트는 순수
    /// 프레젠테이션(그리기)만 담당하고 아무 게임플레이 상태도 직접 들고 있지 않는다.
    /// </summary>
    public class KMSSkillSlotView : MonoBehaviour
    {
        [Header("아이콘")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Color emptySlotIconColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color equippedIconColor = Color.white;

        [Header("쿨타임 오버레이 (Image.Type = Filled 권장, 예: Radial 360 / Top - 남은 시간 비율만큼 채워짐)")]
        [SerializeField] private Image cooldownFillImage;
        [SerializeField] private TMP_Text cooldownText;

        [Header("장전 완료 표시 (해제 시 큐에 들어갈 마지막 유효 단계 - 테두리 이미지 활성/비활성으로 표시)")]
        [SerializeField] private GameObject bankedHighlight;

        private void Awake()
        {
            SetCooldown(0f, 0f);
            SetBankedHighlight(false);
        }

        /// <summary>이 칸에 등록된 스킬 아이콘을 표시한다. icon이 null이면 빈 칸 색상으로 표시한다.</summary>
        public void SetSkill(Sprite icon)
        {
            if (iconImage == null) return;

            iconImage.sprite = icon;
            iconImage.color = icon != null ? equippedIconColor : emptySlotIconColor;
        }

        /// <summary>
        /// 남은 쿨타임(초)을 표시한다. remaining이 0 이하이거나 total이 0이면 오버레이/텍스트를 숨긴다.
        /// cooldownFillImage는 remaining/total(1 -> 0)로 fillAmount를 채우므로, Image.Type=Filled +
        /// Fill Method=Radial360(Origin=Top) 등으로 설정해두면 가득 찬 상태에서 원하는 방향으로 줄어드는
        /// 형태로 자연스럽게 표시된다(코드는 fillAmount 0~1만 설정하고, 실제 채워지는 모양/방향은
        /// Inspector의 Image.Type/Fill Method/Fill Origin 설정을 따른다).
        /// </summary>
        public void SetCooldown(float remaining, float total)
        {
            bool onCooldown = remaining > 0.01f && total > 0f;

            if (cooldownFillImage != null)
            {
                cooldownFillImage.gameObject.SetActive(onCooldown);
                if (onCooldown) cooldownFillImage.fillAmount = Mathf.Clamp01(remaining / total);
            }

            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(onCooldown);
                if (onCooldown)
                {
                    cooldownText.text = remaining >= 9.95f
                        ? Mathf.CeilToInt(remaining).ToString()
                        : remaining.ToString("0.0");
                }
            }
        }

        /// <summary>지금 우클릭을 떼면 큐에 들어갈(장전 완료된) "마지막으로 유효했던 단계"인지 테두리 이미지를 켜고 끈다.</summary>
        public void SetBankedHighlight(bool banked)
        {
            if (bankedHighlight != null) bankedHighlight.SetActive(banked);
        }
    }
}
