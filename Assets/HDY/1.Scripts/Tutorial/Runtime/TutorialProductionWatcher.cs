using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HDY.Tutorial
{
    /// <summary>
    /// 씬에 배치된 모든 ProductionCraftRuntime(_Kyusoo, 제작대)을 폴링으로 감시해서, "제작 완료된
    /// 아이템을 실제로 수령한 순간"을 감지해 TutorialManager.NotifyObjectiveProgress로 알려주는 바인더.
    ///
    /// [별도 이벤트가 없는 이유] ProductionCraftRuntime.CollectCraftedItems()는 수령 버튼을 누르는 즉시
    /// PlayerInventory.AddItem(...)만 호출할 뿐 "수령됨" 이벤트를 전혀 발행하지 않는다. 크로스팀 파일이라
    /// 이벤트를 새로 추가해달라고 요청할 수도 있지만, 다행히 아래 두 필드가 이미 public이라 구독 없이
    /// 읽기(폴링)만으로 정확히 감지할 수 있다:
    /// - currentCraftingItem: 지금 만들고 있는(또는 마지막으로 만들던) 아이템 ID
    /// - currentStorageCount: 제작 완료돼서 수령 대기 중인 수량
    ///
    /// [감지 원리] "어떤 시설의 currentStorageCount가 0보다 컸다가 다음 틱에 0으로 돌아간 순간" =
    /// "플레이어가 수령 버튼을 눌러 인벤토리로 가져간 순간"이다. 이 전이가 감지되면 그 직전의
    /// currentCraftingItem/currentStorageCount 값을 그대로 NotifyObjectiveProgress(itemId, amount)로
    /// 넘긴다. 어떤 목표 아이템인지는 이 바인더가 알 필요 없다 - TutorialManager가 현재 활성 스텝의
    /// objectives에 그 아이템 키가 있는지 스스로 걸러내므로(없으면 조용히 무시), 이 바인더는 "제작대에서
    /// 뭔가 수령됐다"는 사실만 그대로 전달하면 된다(느슨한 결합, 다른 생산 퀘스트가 추가돼도 이 파일을
    /// 다시 손댈 필요 없음).
    ///
    /// [새로 배치되는 제작대 대응] 일정 주기(scanInterval)마다 FindObjectsByType으로 다시 훑어서, 튜토리얼
    /// 시작 이후에 새로 지어진 제작대도 자동으로 감시 대상에 포함한다. 파괴된 시설은 다음 스캔에서
    /// 자연히 목록에서 빠진다.
    /// </summary>
    public class TutorialProductionWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [SerializeField] private float scanInterval = 0.5f;

        /// <summary>제작대 인스턴스별로 직전 틱에 관찰한 (아이템 ID, 수량)을 기억해두는 상태.</summary>
        private class ObservedState
        {
            public string itemId;
            public int storageCount;
        }

        private readonly Dictionary<ProductionCraftRuntime, ObservedState> observedStates =
            new Dictionary<ProductionCraftRuntime, ObservedState>();

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            StartCoroutine(ScanLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
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
            var runtimes = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);

            // 씬에서 사라진(파괴된) 제작대의 이전 상태는 정리한다.
            var stale = new List<ProductionCraftRuntime>();
            foreach (var key in observedStates.Keys)
            {
                if (key == null) stale.Add(key);
            }
            foreach (var key in stale) observedStates.Remove(key);

            foreach (var runtime in runtimes)
            {
                if (runtime == null) continue;

                bool hasPrevious = observedStates.TryGetValue(runtime, out var previous);

                // 직전에 수량이 있었는데(0보다 큼) 이번엔 0으로 돌아왔다면 "방금 수령했다"고 판단한다.
                if (hasPrevious && previous.storageCount > 0 && runtime.currentStorageCount == 0 &&
                    !string.IsNullOrEmpty(previous.itemId))
                {
                    tutorialManager = TutorialManager.Resolve(tutorialManager);
                    tutorialManager?.NotifyObjectiveProgress(previous.itemId, previous.storageCount);
                }

                if (!hasPrevious)
                {
                    previous = new ObservedState();
                    observedStates[runtime] = previous;
                }

                previous.itemId = runtime.currentCraftingItem;
                previous.storageCount = runtime.currentStorageCount;
            }
        }
    }
}
