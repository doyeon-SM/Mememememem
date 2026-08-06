using UnityEngine;
using UnityEngine.SceneManagement;
using MemSystem.Events;
using MemSystem.Data;

using WorldMem = MemSystem.Core.Mem;

namespace HDY.Tutorial
{
    /// <summary>
    /// MemSystem.Events.MemEvents.OnMemCaptured(static 이벤트 버스)를 구독해서 TutorialManager에
    /// MemCaptured 트리거로 중계하는 바인더.
    ///
    /// [씬 전환 대응] MemEvents에는 ClearAll()이라는 전체 구독 해제 메서드가 있고("씬 매니저의
    /// OnSceneUnloaded 등에서 호출해주세요"라고 안내되어 있어 언젠가 호출될 수 있음), OnEnable 1회
    /// 구독에만 의존하면 그 시점 이후로 구독이 끊길 위험이 있다. 그래서 SceneManager.sceneLoaded
    /// 때마다 재구독(먼저 해제 후 다시 구독이라 중복 구독도 방지됨)해서 안전하게 유지한다.
    ///
    /// [WorldMem 별칭 사용 이유] namespace HDY.Tutorial 안에서 HDY.Mem이라는 네임스페이스와 이름이
    /// 겹쳐 그냥 "Mem"이라고 쓰면 컴파일러가 타입이 아니라 네임스페이스로 해석해버린다
    /// (TutorialSightDetector에서 실제로 겪은 문제와 동일) - 그래서 별칭으로 구분해서 쓴다.
    /// </summary>
    public class TutorialMemCaptureWatcher : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        private void OnEnable()
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            Subscribe();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Unsubscribe();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // ClearAll()로 구독이 끊겼을 수 있으니 씬이 바뀔 때마다 다시 구독한다.
            Unsubscribe();
            Subscribe();
        }

        private void Subscribe()
        {
            MemEvents.OnMemCaptured += HandleMemCaptured;
        }

        private void Unsubscribe()
        {
            MemEvents.OnMemCaptured -= HandleMemCaptured;
        }

        private void HandleMemCaptured(WorldMem mem, MemSnapshot snapshot)
        {
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            tutorialManager?.NotifyTriggerFired(TutorialTriggerType.MemCaptured, string.Empty);
        }
    }
}
