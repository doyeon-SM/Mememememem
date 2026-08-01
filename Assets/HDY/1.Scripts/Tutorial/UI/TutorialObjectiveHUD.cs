using TMPro;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 튜토리얼 목표(퀘스트) 진행 상황을 표시하는 HUD 텍스트에 붙이는 바인더.
    /// GameTimeTextBinder와 완전히 동일한 이유/방식으로 동작한다.
    ///
    /// [왜 필요한가] TutorialManager는 DontDestroyOnLoad라 씬이 바뀌어도 파괴되지 않지만, 실제로 화면에
    /// 보이는 이 Text 오브젝트는 각 씬에 있는 평범한 UI라 씬이 바뀌면 파괴된다. Inspector로 직접
    /// 연결해두면 다음 씬으로 넘어가는 순간 그 참조가 끊긴다.
    ///
    /// [해결 방식] "매니저가 씬을 뒤져서 Text를 찾는" 방식 대신, "Text 쪽이 스스로 등록하는" 방식을 쓴다.
    /// 씬이 바뀔 때마다 이 오브젝트의 OnEnable이 새로 실행되므로, 그 시점에 TutorialManager.Instance에
    /// 자기 자신(TMP_Text)을 등록한다 - 씬이 몇 번을 바뀌어도 항상 그 씬에 있는 Text로 자동 재연결된다.
    ///
    /// [사용법] 목표 텍스트를 표시할 TMP_Text 오브젝트에 이 컴포넌트만 붙이면 끝 - TutorialManager 쪽
    /// 인스펙터에 Text를 직접 연결할 필요가 없다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TutorialObjectiveHUD : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        private TMP_Text text;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);

            if (tutorialManager == null)
            {
                Debug.LogWarning("[TutorialObjectiveHUD] TutorialManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            tutorialManager.RegisterObjectiveText(text);
        }

        private void OnDisable()
        {
            if (tutorialManager == null || text == null) return;
            tutorialManager.UnregisterObjectiveText(text);
        }
    }
}
