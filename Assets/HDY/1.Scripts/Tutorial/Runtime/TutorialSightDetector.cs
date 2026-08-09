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
    /// 1. 매 스캔(scanInterval)마다, 지금 튜토리얼이 대기 중인 트리거 종류가 이 카테고리와 일치하는지
    ///    먼저 확인한다(TutorialManager.GetPendingStepTriggerType()) - 일치하지 않으면 그 카테고리는
    ///    아예 스캔하지 않는다.
    /// 2. 일치하면 성능을 위해 플레이어 주변 반경(scanRadius)을 OverlapSphere로 먼저 추린다.
    /// 3. 후보 중 대상 컴포넌트(WorldObject / MemSystem.Core.Mem / WayPointObject / Chest)를 가진
    ///    것만 남긴다.
    /// 4. 카메라 시야(뷰포트 0~1, 카메라 앞쪽)에 들어와 있는지 확인한다.
    /// 5. (선택) 카메라 -> 대상 레이캐스트로 벽 등에 가려져 있으면 제외한다.
    /// 6. 찾으면 TutorialManager.NotifyTriggerFired(...)를 호출하고, 그 대상의 Transform을
    ///    TutorialManager.SetPendingHighlightTarget(...)으로 넘겨 하이라이트에 쓰게 한다.
    ///
    /// [HDY 요청 - 조기 소모 버그 수정] 예전엔 세션 시작부터 4종류를 전부 스캔해서, 한 카테고리가
    /// "한 번이라도" 화면에 스쳐 지나가면(지금 그 트리거를 기다리는 스텝이 아니어도) 그 즉시
    /// seenCategories에 소모돼버렸다. 그래서 예를 들어 채집 스텝(ObjectSighted)에 도달하기 한참
    /// 전에 배경의 나무 하나가 잠깐 화면에 잡히기만 해도, 정작 채집 스텝에 도달했을 때는 다시
    /// 감지되지 않는 문제가 있었다(하이라이트가 아예 작동하지 않는 것처럼 보임). 이제는 "지금 이
    /// 카테고리를 기다리는 스텝이 맞는지"를 매번 다시 확인하고 맞을 때만 스캔하므로, 상관없는 시점에
    /// 미리 소모될 일이 없다. 같은 이유로 더 이상 전역 1회성 HashSet이 필요 없어 제거했다.
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
        [Tooltip("플레이어 기준 이 반경(유닛) 안에 있는 대상만 후보로 본다. 카메라 시야에 들어와 있어도\n" +
                 "이 반경 밖이면 후보에도 오르지 못한다 - 멀리서도 감지되길 원하면 이 값을 키운다.")]
        [SerializeField] private float scanRadius = 40f;
        [SerializeField] private float scanInterval = 0.25f;
        [SerializeField] private LayerMask scanLayerMask = ~0;

        [Header("가림 확인 (선택)")]
        [SerializeField] private bool useOcclusionCheck = true;
        [SerializeField] private LayerMask occlusionLayerMask = ~0;

        // [HDY 요청 - 버퍼 부족으로 후보 누락 수정] scanRadius를 넓힐수록 반경 안의 콜라이더 수도
        // 늘어난다. 32칸이었을 때 실측 반경(40유닛) 안에서 콜라이더가 60개 넘게 잡혀 버퍼가 꽉 차
        // 넘치고, 정작 찾아야 할 대상이 잘려나간 32개 밖으로 밀려나 후보에서 통째로 빠지는 문제를
        // 실제로 재현했다(OverlapSphereNonAlloc은 버퍼가 차면 나머지를 그냥 잘라버림 - 가까운 순 정렬
        // 보장 없음). 여유 있게 128로 늘렸다.
        private readonly Collider[] scanBuffer = new Collider[128];

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

            // 매번 4종류 다 시도한다 - 실제로 상관없는 카테고리는 TryDetectSight 안에서 즉시
            // 걸러지므로(지금 대기 중인 트리거와 다르면 스캔 자체를 안 함) 비용이 크지 않다.
            while (true)
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
            tutorialManager = TutorialManager.Resolve(tutorialManager);
            if (tutorialManager == null) return;

            // 지금 대기 중인 스텝이 이 트리거를 기다리는 게 아니면 스캔할 필요가 없다(조기 소모 방지).
            if (tutorialManager.GetPendingStepTriggerType() != triggerType) return;

            var target = FindSightedCandidate<T>();
            if (target == null) return;

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
