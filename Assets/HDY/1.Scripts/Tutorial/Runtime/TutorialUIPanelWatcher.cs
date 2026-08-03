using System.Collections;
using UnityEngine;
using HDY.UI;

namespace HDY.Tutorial
{
    /// <summary>
    /// UIManager.OnPanelOpened를 구독해서 TutorialManager에 UIPanelOpened 트리거로 중계하는 바인더.
    /// UIManager는 이 클래스의 존재를 전혀 모른다(느슨한 결합) - 아무 오브젝트에나 하나만 부착하면 된다.
    ///
    /// [버그 수정 - UIManager가 늦게 생기는 문제] UIManager는 TutorialManager와 달리 DontDestroyOnLoad가
    /// 아니라서, HUD가 있는 특정 씬(Territory 등)에 들어가야 비로소 Instance가 생긴다. 이 바인더를
    /// TutorialManager와 함께 영구 오브젝트("TutorialSystem")에 붙여 Main_World_2에서부터 살아있게
    /// 하면, 그 시점엔 아직 UIManager.Instance가 없어 예전에는 OnEnable에서 딱 한 번만 확인하고
    /// 끝나버렸다(경고만 찍고 이후로 다시 확인하지 않음 - 실제로 겪은 문제). TutorialWaypointWatcher와
    /// 동일하게, 일정 주기로 "지금 구독 중인 UIManager == UIManager.Instance"인지 계속 재확인해서,
    /// Territory 씬 진입 등으로 나중에 UIManager가 생기거나 씬 전환으로 인스턴스가 바뀌어도 자동으로
    /// (다시) 구독한다.
    ///
    /// [CSV 사용법] Trigger_Type=UIPanelOpened로 지정한 스텝의 Trigger_Param에는 UIManager의
    /// HudEntry.PanelKey와 동일한 문자열을 적는다(PanelKey를 비워뒀다면 그 버튼이 여는 프리팹의
    /// GameObject 이름을 그대로 적어야 한다).
    /// </summary>
    public class TutorialUIPanelWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [SerializeField] private float recheckInterval = 0.5f;

        private UIManager subscribedManager;

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            StartCoroutine(WatchLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            Unsubscribe();
        }

        private IEnumerator WatchLoop()
        {
            var wait = new WaitForSeconds(recheckInterval);
            while (true)
            {
                EnsureSubscribed();
                yield return wait;
            }
        }

        /// <summary>
        /// UIManager.Instance가 아직 없거나(씬에 아직 안 생겼거나), 씬 전환으로 다른 인스턴스로
        /// 바뀌었으면 구독을 다시 맞춘다.
        /// </summary>
        private void EnsureSubscribed()
        {
            if (subscribedManager == UIManager.Instance) return;

            Unsubscribe();

            subscribedManager = UIManager.Instance;
            if (subscribedManager != null)
            {
                subscribedManager.OnPanelOpened += HandlePanelOpened;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedManager != null)
            {
                subscribedManager.OnPanelOpened -= HandlePanelOpened;
            }
            subscribedManager = null;
        }

        private void HandlePanelOpened(string panelKey)
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyTriggerFired(TutorialTriggerType.UIPanelOpened, panelKey);
        }
    }
}
