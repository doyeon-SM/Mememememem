using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GH.Loading
{
    /// <summary>
    /// 씬 Canvas 아래에 생성되는 로딩 패널의 UI 참조와 표시 갱신만 담당합니다.
    /// 이 컴포넌트가 붙는 프리팹에는 Canvas를 넣지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingPanelView : MonoBehaviour
    {
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text percentText;
        [SerializeField] private TMP_Text tipText;

        /// <summary>별도 팁 텍스트 영역이 연결되어 있는지 나타냅니다.</summary>
        public bool HasTipText => tipText != null;

        /// <summary>기존 내장 로딩 UI를 런타임 패널 방식으로 이전할 때 참조를 연결합니다.</summary>
        internal void Configure(
            Slider slider,
            TMP_Text description,
            TMP_Text percent,
            TMP_Text tip)
        {
            progressSlider = slider;
            descriptionText = description;
            percentText = percent;
            tipText = tip;
        }

        /// <summary>현재 진행 설명과 0~1 진행률을 화면에 반영합니다.</summary>
        public void SetProgress(string description, float progress, bool showDescription)
        {
            float clampedProgress = Mathf.Clamp01(progress);

            if (progressSlider != null)
            {
                progressSlider.value = clampedProgress;
            }

            if (descriptionText != null && showDescription)
            {
                descriptionText.text = description;
            }

            if (percentText != null)
            {
                percentText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
            }
        }

        /// <summary>랜덤 팁을 별도 영역 또는 설명 영역에 표시합니다.</summary>
        public void SetTip(string tip, bool useDescriptionText)
        {
            if (tipText != null)
            {
                tipText.text = tip;
            }
            else if (useDescriptionText && descriptionText != null)
            {
                descriptionText.text = tip;
            }
        }
    }
}
