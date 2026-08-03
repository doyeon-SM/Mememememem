using System.Collections;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// WayPointManager(_GH, 싱글톤).OnWayPointUnlocked를 구독해서 TutorialManager에
    /// WaypointUnlocked 트리거로 중계하는 바인더.
    ///
    /// [싱글톤 인스턴스 변경 대응] WayPointManager는 TerritoryData/TutorialManager처럼
    /// Resolve(existing) 폴백 헬퍼가 없는 순수 싱글톤(Instance)이라, 씬 전환으로 인스턴스 자체가
    /// 바뀌었을 가능성을 배제할 수 없다. 그래서 일정 주기로 "지금 구독 중인 인스턴스 ==
    /// WayPointManager.Instance"인지 확인해서, 다르면 이전 구독을 해제하고 새 인스턴스에 다시
    /// 구독한다.
    /// </summary>
    public class TutorialWaypointWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [SerializeField] private float recheckInterval = 0.5f;

        private WayPointManager subscribedManager;

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

        private void EnsureSubscribed()
        {
            if (subscribedManager == WayPointManager.Instance) return;

            Unsubscribe();

            subscribedManager = WayPointManager.Instance;
            if (subscribedManager != null)
            {
                subscribedManager.OnWayPointUnlocked += HandleWaypointUnlocked;
            }
        }

        private void Unsubscribe()
        {
            if (subscribedManager != null)
            {
                subscribedManager.OnWayPointUnlocked -= HandleWaypointUnlocked;
            }
            subscribedManager = null;
        }

        private void HandleWaypointUnlocked(WayPointRunTime state)
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyTriggerFired(TutorialTriggerType.WaypointUnlocked, string.Empty);
        }
    }
}
