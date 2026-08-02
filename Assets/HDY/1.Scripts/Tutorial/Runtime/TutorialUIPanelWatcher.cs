using UnityEngine;
using HDY.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// UIManager.OnPanelOpened를 구독해서 TutorialManager에 UIPanelOpened 트리거로 중계하는 바인더.
    /// UIManager는 이 클래스의 존재를 전혀 모른다(느슨한 결합) - 아무 오브젝트에나 하나만 부착하면 된다
    /// (HUD/UIManager가 있는 씬에 배치).
    ///
    /// [CSV 사용법] Trigger_Type=UIPanelOpened로 지정한 스텝의 Trigger_Param에는 UIManager의
    /// HudEntry.PanelKey와 동일한 문자열을 적는다(PanelKey를 비워뒀다면 그 버튼이 여는 프리팹의
    /// GameObject 이름을 그대로 적어야 한다).
    /// </summary>
    public class TutorialUIPanelWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("비워두면 자동 탐색(UIManager.Instance).")]
        [SerializeField] private UIManager uiManager;

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);

            if (uiManager == null) uiManager = UIManager.Instance;
            if (uiManager == null)
            {
                Debug.LogWarning("[TutorialUIPanelWatcher] UIManager를 찾을 수 없어 등록하지 못했습니다.", this);
                return;
            }

            uiManager.OnPanelOpened -= HandlePanelOpened; // 중복 구독 방지
            uiManager.OnPanelOpened += HandlePanelOpened;
        }

        private void OnDisable()
        {
            if (uiManager != null) uiManager.OnPanelOpened -= HandlePanelOpened;
        }

        private void HandlePanelOpened(string panelKey)
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyTriggerFired(TutorialTriggerType.UIPanelOpened, panelKey);
        }
    }
}
