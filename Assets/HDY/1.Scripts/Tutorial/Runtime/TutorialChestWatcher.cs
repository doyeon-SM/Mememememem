using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 씬에 배치된 모든 Chest(_GH, 상자)를 주기적으로 재탐색해서 아직 구독하지 않은 상자의
    /// OpenChestId 이벤트를 구독하고, 열리면 TutorialManager에 ChestOpened 트리거로 중계하는 바인더.
    ///
    /// [왜 재스캔이 필요한가] Chest는 싱글톤이 아니라 씬에 배치된 개별 인스턴스마다 있고, 튜토리얼
    /// 진행 중에 새로 스폰되는 상자도 있을 수 있어 한 번만 스캔해서는 놓칠 수 있다.
    ///
    /// [파괴 시점] Chest는 열리면 이벤트 발행 직후 곧바로 Destroy(gameObject)된다 - 이벤트 발행이
    /// Destroy보다 먼저(동기적으로) 일어나므로 감지에는 문제가 없다. 구독 목록에는 이미 파괴된
    /// (열려서 사라진) 상자가 남을 수 있으므로 재스캔 때마다 정리한다.
    /// </summary>
    public class TutorialChestWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [SerializeField] private float scanInterval = 0.5f;

        private readonly HashSet<Chest> subscribedChests = new HashSet<Chest>();

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            StartCoroutine(ScanLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();

            foreach (var chest in subscribedChests)
            {
                if (chest != null) chest.OpenChestId -= HandleChestOpened;
            }
            subscribedChests.Clear();
        }

        private IEnumerator ScanLoop()
        {
            var wait = new WaitForSeconds(scanInterval);
            while (true)
            {
                ScanOnce();
                yield return wait;
            }
        }

        private void ScanOnce()
        {
            // 파괴된(열려서 사라진) 상자는 목록에서 정리한다.
            subscribedChests.RemoveWhere(c => c == null);

            var chests = FindObjectsByType<Chest>(FindObjectsSortMode.None);
            foreach (var chest in chests)
            {
                if (chest == null || subscribedChests.Contains(chest)) continue;

                chest.OpenChestId += HandleChestOpened;
                subscribedChests.Add(chest);
            }
        }

        private void HandleChestOpened(string chestId)
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyTriggerFired(TutorialTriggerType.ChestOpened, chestId ?? string.Empty);
        }
    }
}
