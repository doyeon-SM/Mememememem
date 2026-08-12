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

        /// <summary>
        /// [HDY 요청 - 동적 슬롯 하이라이트] 런타임에 AddComponent로 붙이면서 키를 지정할 때 쓴다
        /// (예: GoddessStatueUI_LevelRow가 여러 슬롯 중 영지 확장 슬롯 하나에만 붙이는 경우). Inspector로
        /// 미리 배치된 정적 UI는 targetKey를 직접 채워두면 되니 이 메서드를 쓸 필요가 없다.
        /// AddComponent 시점엔 Awake→OnEnable이 먼저 실행되는데, 그때는 targetKey가 비어있어 등록이
        /// 스킵된다. 그래서 여기서 직접 등록까지 해준다. 이미 다른 키로 등록되어 있던 상태였다면 그 키로
        /// 먼저 해제한 뒤 새 키로 다시 등록한다.
        /// </summary>
        public void Configure(string key)
        {
            if (isActiveAndEnabled && tutorialManager != null && !string.IsNullOrEmpty(targetKey))
            {
                tutorialManager.UnregisterUIHighlightTarget(targetKey, rectTransform);
            }

            targetKey = key;

            if (isActiveAndEnabled)
            {
                tutorialManager = TutorialManager.Resolve(tutorialManager);
                if (tutorialManager != null && !string.IsNullOrEmpty(targetKey))
                {
                    tutorialManager.RegisterUIHighlightTarget(targetKey, rectTransform);
                }
            }
        }
    }
}
