// ============================================================================
// MemFocusTracker.cs
// 플레이어가 조준(포커싱) 중인 멤을 매 프레임 판정해 공유하는 트래커.
//
// [왜 필요한가]
// - 플레이어(KMS)의 포커스 시스템(KMSMemCaptureFocusController)은 조준된 멤을
//   지역변수로만 쓰고 외부에 노출(이벤트/프로퍼티)하지 않습니다.
// - 우리는 KMS를 수정할 수 없으므로, KMS와 "동일한 방식의 레이캐스트"를 여기서
//   한 번 더 돌려 조준 대상을 판정합니다. (감지 규칙을 맞춰야 KMS UI와 어긋나지 않음)
//
// [자동 생성]
// - RuntimeInitializeOnLoadMethod로 씬 로드 후 자동 생성됩니다. 씬/프리팹 세팅 불필요.
//
// [갱신 시점]
// - Update에서 판정합니다. HP바는 LateUpdate에서 Current를 읽으므로 항상 같은 프레임의
//   최신 값을 봅니다(1프레임 지연 없음).
// ============================================================================
using UnityEngine;
using MemSystem.Core;

namespace MemSystem.Visual
{
    /// <summary>
    /// 카메라 정면 레이캐스트로 조준된 멤을 판정해 <see cref="Current"/>로 공유한다.
    /// KMSMemCaptureFocusController.FindFocusedMem과 동일한 규칙을 복제한다.
    /// </summary>
    public class MemFocusTracker : MonoBehaviour
    {
        /// <summary>현재 플레이어가 조준 중인 멤. 없으면 null.</summary>
        public static Mem Current { get; private set; }

        // KMS 기본값과 동일: 정면 30m, 전 레이어, 트리거도 히트(트리거는 가림막 아님).
        private const float MaxFocusDistance = 30f;
        private static readonly LayerMask FocusLayers = ~0;

        private readonly RaycastHit[] hits = new RaycastHit[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            // 이미 있으면 중복 생성 방지 (도메인 리로드 비활성화 대비)
            if (FindFirstObjectByType<MemFocusTracker>() != null) return;

            var go = new GameObject("[MemFocusTracker]");
            go.AddComponent<MemFocusTracker>();
            DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            Current = null;
        }

        private void Update()
        {
            Current = FindFocused();
        }

        /// <summary>KMS와 동일한 규칙으로 조준된 멤을 찾는다.</summary>
        private Mem FindFocused()
        {
            var mainCamera = Camera.main;
            if (mainCamera == null) return null;

            Transform camT = mainCamera.transform;
            Ray ray = new Ray(camT.position, camT.forward);

            int count = Physics.RaycastNonAlloc(
                ray, hits, MaxFocusDistance, FocusLayers, QueryTriggerInteraction.Collide);
            if (count <= 0) return null;

            System.Array.Sort(hits, 0, count, DistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;

                // 플레이어 본인 콜라이더는 무시 (카메라가 플레이어 안쪽에 있을 수 있음).
                // KMS는 하이어라키로 판정하지만, 트래커는 플레이어 참조가 없어 태그로 대체한다.
                if (col.CompareTag("Player")) continue;

                Mem mem = col.GetComponentInParent<Mem>();
                if (mem != null) return mem.IsActive ? mem : null;

                // 감지용 트리거는 시야를 안 가리지만, 실제 콜라이더는 가림막으로 취급.
                if (!col.isTrigger) return null;
            }

            return null;
        }

        private sealed class DistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly DistanceComparer Instance = new DistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
