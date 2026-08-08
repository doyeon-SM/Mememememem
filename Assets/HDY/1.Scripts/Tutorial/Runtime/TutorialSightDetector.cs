using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WorldMem = MemSystem.Core.Mem;

namespace HDY.Tutorial
{
    /// <summary>
    /// 플레이어 주변의 오브젝트/멤/웨이포인트/상자를 카메라 시야로 "처음 포착"했는지 감지해
    /// TutorialManager에 알려주는 바인더. 플레이어 오브젝트(또는 그 자식)에 부착한다.
    ///
    /// [감지 순서]
    /// 1. 성능을 위해 일정 주기(scanInterval)로 플레이어 주변 반경(scanRadius)을 OverlapSphere로
    ///    먼저 추린다.
    /// 2. 후보 중 대상 컴포넌트(WorldObject / MemSystem.Core.Mem / WayPointObject / Chest)를 가진
    ///    것만 남긴다.
    /// 3. 카메라 시야(뷰포트 0~1, 카메라 앞쪽)에 들어와 있는지 확인한다.
    /// 4. (선택) 카메라 -> 대상 레이캐스트로 벽 등에 가려져 있으면 제외한다.
    /// 5. 카테고리별로 최초 1회만 TutorialManager.NotifyTriggerFired(...)를 호출하고, 그 대상의
    ///    Transform을 TutorialManager.SetPendingHighlightTarget(...)으로 넘겨 하이라이트에 쓰게 한다.
    ///    이후 그 카테고리는 다시 검사하지 않는다.
    ///
    /// [MemSystem.Core.Mem 별칭 사용 이유] 이 파일은 namespace HDY.Tutorial 안에 있는데, HDY 밑에
    /// HDY.Mem이라는 네임스페이스가 이미 존재해서 그냥 "Mem"이라고 쓰면 컴파일러가 타입이 아니라
    /// 그 네임스페이스로 해석해버린다(실제로 겪은 컴파일 에러). 그래서 WorldMem이라는 별칭으로 확실히
    /// 구분해서 쓴다.
    ///
    /// [크로스팀 이슈 없음] WorldObject/Mem/WayPointObject/Chest를 전혀 수정하지 않고, 이미 씬에 있는
    /// 컴포넌트를 GetComponent로 "관찰"만 한다.
    /// </summary>
    public class TutorialSightDetector : MonoBehaviour
    {
        [Tooltip("비워두면 자동 탐색(TutorialManager.Resolve).")]
        [SerializeField] private TutorialManager tutorialManager;

        [Tooltip("비워두면 Camera.main 사용.")]
        [SerializeField] private Camera viewCamera;

        [Header("스캔 설정")]
        [SerializeField] private float scanRadius = 15f;
        [SerializeField] private float scanInterval = 0.25f;
        [SerializeField] private LayerMask scanLayerMask = ~0;

        [Header("가림 확인 (선택)")]
        [SerializeField] private bool useOcclusionCheck = true;
        [SerializeField] private LayerMask occlusionLayerMask = ~0;

        private readonly HashSet<TutorialTriggerType> seenCategories = new HashSet<TutorialTriggerType>();
        private readonly Collider[] scanBuffer = new Collider[32];

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

            // 4종류(오브젝트/멤/웨이포인트/상자)를 전부 감지했으면 더 스캔할 필요 없이 코루틴만 대기한다.
            while (seenCategories.Count < 4)
            {
                TryDetectSight<WorldObject>(TutorialTriggerType.ObjectSighted);
                TryDetectSight<WorldMem>(TutorialTriggerType.MemSighted);
                TryDetectSight<WayPointObject>(TutorialTriggerType.WaypointSighted);
                TryDetectSight<Chest>(TutorialTriggerType.ChestSighted);

                yield return wait;
            }
        }

        private void TryDetectSight<T>(TutorialTriggerType triggerType) where T : Component
        {
            if (seenCategories.Contains(triggerType)) return;

            var target = FindSightedCandidate<T>();
            if (target == null) return;

            seenCategories.Add(triggerType);

            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (tutorialManager == null) return;

            tutorialManager.SetPendingHighlightTarget(target.transform);
            tutorialManager.NotifyTriggerFired(triggerType, string.Empty);
        }

        private T FindSightedCandidate<T>() where T : Component
        {
            var cam = ResolveCamera();
            if (cam == null) return null;

            int count = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, scanBuffer, scanLayerMask);
            for (int i = 0; i < count; i++)
            {
                var component = scanBuffer[i].GetComponentInParent<T>();
                if (component == null) continue;

                Vector3 viewportPos = cam.WorldToViewportPoint(component.transform.position);
                if (viewportPos.z <= 0f) continue; // 카메라 뒤쪽
                if (viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f) continue;

                if (useOcclusionCheck && IsOccluded(cam, component.transform.position, component))
                {
                    continue;
                }

                return component;
            }
            return null;
        }

        private bool IsOccluded<T>(Camera cam, Vector3 targetPosition, T target) where T : Component
        {
            Vector3 origin = cam.transform.position;
            Vector3 offset = targetPosition - origin;
            float distance = offset.magnitude;
            if (distance <= 0.01f) return false;

            if (Physics.Raycast(origin, offset.normalized, out var hit, distance, occlusionLayerMask))
            {
                // 맞은 대상이 우리가 찾던 것 자신(혹은 그 자식 콜라이더)이 아니면 다른 물체에 가려진 것.
                return hit.collider.GetComponentInParent<T>() != target;
            }
            return false;
        }

        // [HDY 요청 - 카메라 참조 통합] viewCamera가 Inspector에 직접 지정돼 있으면 그걸 최우선으로
        // 쓰고, 없으면 TutorialManager.ResolveWorldCamera()(TutorialHighlightUI와 공유하는 지점)를 거쳐
        // Camera.main으로 폴백한다. 이전에는 이 컴포넌트와 TutorialHighlightUI가 각자 독립적으로
        // Camera.main을 조회해서 서로 다른 카메라를 참조할 수 있었다(감지는 되는데 하이라이트만 안 보이는
        // 버그의 유력한 원인).
        private Camera ResolveCamera()
        {
            if (viewCamera != null) return viewCamera;

            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (tutorialManager != null)
            {
                var shared = tutorialManager.ResolveWorldCamera();
                if (shared != null) return shared;
            }

            viewCamera = Camera.main;
            return viewCamera;
        }
    }
}
