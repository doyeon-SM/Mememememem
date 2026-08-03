using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼에서 하이라이트할 UI 버튼/요소에 붙이는 등록용 컴포넌트. GameTimeTextBinder와 동일한
    /// "자기 자신이 스스로 등록/해제" 패턴이라, 씬이 바뀌거나 UI가 다시 생성돼도 자동으로 재연결된다.
    ///
    /// [사용법] 하이라이트하고 싶은 버튼/이미지 등에 이 컴포넌트를 붙이고 Target Key에 CSV의
    /// Highlight_Key 컬럼과 똑같은 문자열을 적으면 된다. 예: "goddess_statue_button"
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class TutorialUIHighlightTarget : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("CSV의 Highlight_Key 컬럼과 동일한 문자열이어야 한다.")]
        [SerializeField] private string targetKey;

        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (tutorialManager == null || string.IsNullOrEmpty(targetKey)) return;

            tutorialManager.RegisterUIHighlightTarget(targetKey, rectTransform);
        }

        private void OnDisable()
        {
            if (tutorialManager == null || string.IsNullOrEmpty(targetKey)) return;

            tutorialManager.UnregisterUIHighlightTarget(targetKey, rectTransform);
        }
    }
}
