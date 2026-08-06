// ============================================================================
// MemCaptureVisualTester.cs
// 포획 연출 단독 테스트 스크립트 (플레이어/캡슐 없이 멤 연출만 확인)
//
// [사용 방법]
// 1. 씬에 이 컴포넌트를 부착한 빈 GameObject를 생성합니다.
// 2. Inspector에서 테스트할 Mem 오브젝트를 targetMem에 드래그&드롭합니다.
//    (씬에 이미 스폰된 멤이 있어야 합니다)
// 3. Play Mode에서 아래 단축키로 각 연출을 테스트합니다:
//
//    [키 입력]
//    숫자 1 — 포획 흡수 연출 (빛남 + 캡슐 방향으로 축소)
//    숫자 2 — 포획 성공 연출 전체 흐름 (흡수 → 성공 이벤트 발행)
//    숫자 3 — 포획 실패 연출 전체 흐름 (흡수 → 실패 → 멤 탈출)
//    숫자 4 — 멤 상태 강제 리셋 (연출 도중 초기화할 때 사용)
//
// [Inspector 설정]
//    targetMem         : 연출을 재생할 Mem 오브젝트
//    fakeCapsuleOffset : 멤 기준 캡슐이 있다고 가정할 위치 오프셋
//    judgeDelay        : 흡수 시작 → 판정 사이의 대기 시간 (초)
//    testCapsuleTier   : 테스트에 사용할 캡슐 등급 (0=Rare ~ 4=Mythic)
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Interface;
using MemSystem.Events;

namespace Pikachu.Player
{
    /// <summary>
    /// 포획 연출 단독 테스트 도구.
    /// 플레이어/캡슐 오브젝트 없이 Mem의 시각 연출만 확인합니다.
    /// </summary>
    public class MemCaptureVisualTester : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("테스트 대상")]
        [Tooltip("연출을 테스트할 Mem 오브젝트. 씬에 있는 멤을 드래그&드롭하세요.")]
        [SerializeField] private Mem targetMem;

        [Header("가짜 캡슐 위치")]
        [Tooltip("멤 기준으로 캡슐이 있다고 가정할 위치 오프셋 (월드 방향).\n" +
                 "예: (0, 0.5, -2) → 멤 앞에서 약간 위")]
        [SerializeField] private Vector3 fakeCapsuleOffset = new Vector3(0f, 0.5f, -2f);

        [Header("테스트 파라미터")]
        [Tooltip("흡수 시작(NotifyCaptureBallHit) 후 판정(OnCaptureSuccess/Fail) 까지 대기 시간 (초).\n" +
                 "실제 캡슐 흔들림 연출 시간에 맞게 설정합니다.")]
        [SerializeField] private float judgeDelay = 2.5f;

        [Tooltip("테스트에 사용할 캡슐 등급. 0=Rare, 1=Epic, 2=Unique, 3=Legendary, 4=Mythic(무조건 성공)")]
        [Range(0, 4)]
        [SerializeField] private int testCapsuleTier = 0;

        // =================================================================
        // 내부 상태
        // =================================================================

        /// <summary>테스트 코루틴이 실행 중인지 여부 (중복 실행 방지)</summary>
        private bool isTesting = false;

        /// <summary>시각화를 위한 임시 가짜 캡슐 오브젝트</summary>
        private GameObject fakeCapsuleObj;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private void OnEnable()
        {
            // 이벤트 구독 — 콘솔 로그로 이벤트 수신 여부 확인
            MemEvents.OnMemCaptureStarted += OnCaptureStarted;
            MemEvents.OnMemCaptured       += OnCaptureSuccess;
            MemEvents.OnMemCaptureFailed  += OnCaptureFailed;
        }

        private void OnDisable()
        {
            MemEvents.OnMemCaptureStarted -= OnCaptureStarted;
            MemEvents.OnMemCaptured       -= OnCaptureSuccess;
            MemEvents.OnMemCaptureFailed  -= OnCaptureFailed;
        }

        private void Update()
        {
            // 키보드가 없으면 동작 불가
            if (Keyboard.current == null) return;

            // 타겟 멤 미설정 시 자동 검색 시도 (스포너가 멤을 생성한 경우 대응)
            if (targetMem == null)
            {
                targetMem = FindObjectOfType<Mem>();
                
                // 검색 후에도 없으면 경고
                if (targetMem == null)
                {
                    if (Keyboard.current.anyKey.wasPressedThisFrame)
                        Debug.LogWarning("[MemCaptureVisualTester] 씬에 활성화된 Mem 오브젝트가 없습니다! 스포너가 멤을 생성하기를 기다리거나, 미리 씬에 배치하세요.");
                    return;
                }
                else
                {
                    Debug.Log($"[Tester] 씬에서 활성화된 '{targetMem.name}'을(를) 테스트 대상으로 자동 지정했습니다.");
                }
            }

            // ------------------------------------------------------------------
            // 가짜 캡슐 시각화 (임시 3D 구체)
            // ------------------------------------------------------------------
            if (fakeCapsuleObj == null && targetMem != null)
            {
                fakeCapsuleObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fakeCapsuleObj.name = "FakeCapsule_Visualizer";
                fakeCapsuleObj.transform.localScale = Vector3.one * 0.3f; // 캡슐 크기 정도

                // 충돌체 제거 (테스트 방해 방지)
                Destroy(fakeCapsuleObj.GetComponent<Collider>());

                // 파란색 재질 적용
                var renderer = fakeCapsuleObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = new Color(0.2f, 0.5f, 1f, 0.6f);

                    // 반투명 설정 (Standard 셰이더)
                    mat.SetFloat("_Mode", 3);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = 3000;

                    renderer.material = mat;
                }
            }

            // 가짜 캡슐 위치 실시간 갱신
            if (fakeCapsuleObj != null && targetMem != null)
            {
                fakeCapsuleObj.transform.position = targetMem.transform.position + fakeCapsuleOffset;
            }

            // ------------------------------------------------------------------
            // 키 입력 처리 (새로운 Input System)
            // ------------------------------------------------------------------

            // [1] 흡수 연출만 단독 확인 (판정 없음)
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                if (isTesting) { Debug.LogWarning("[Tester] 이미 테스트 실행 중입니다. 4번으로 리셋 후 재시도하세요."); return; }
                Debug.Log("[Tester] ▶ 1번: 흡수 연출 단독 테스트 시작");
                TestAbsorbOnly();
            }

            // [2] 포획 성공 전체 흐름 테스트
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                if (isTesting) { Debug.LogWarning("[Tester] 이미 테스트 실행 중입니다. 4번으로 리셋 후 재시도하세요."); return; }
                Debug.Log("[Tester] ▶ 2번: 포획 성공 전체 흐름 테스트 시작");
                StartCoroutine(TestCaptureFlow(success: true));
            }

            // [3] 포획 실패 전체 흐름 테스트
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                if (isTesting) { Debug.LogWarning("[Tester] 이미 테스트 실행 중입니다. 4번으로 리셋 후 재시도하세요."); return; }
                Debug.Log("[Tester] ▶ 3번: 포획 실패 전체 흐름 테스트 시작");
                StartCoroutine(TestCaptureFlow(success: false));
            }

            // [4] 강제 리셋
            else if (Keyboard.current.digit4Key.wasPressedThisFrame)
            {
                Debug.Log("[Tester] ▶ 4번: 멤 상태 강제 리셋");
                ForceReset();
            }
        }

        // =================================================================
        // 테스트 케이스 구현
        // =================================================================

        /// <summary>
        /// [1번] NotifyCaptureBallHit만 호출하여 흡수 연출 단독 확인.
        /// 판정(성공/실패)은 하지 않습니다.
        /// </summary>
        private void TestAbsorbOnly()
        {
            ICapturable capturable = targetMem;
            Vector3 fakeCapsulePos = targetMem.transform.position + fakeCapsuleOffset;

            // 포획 확률 먼저 콘솔에 출력
            float rate = capturable.GetCaptureRate(testCapsuleTier);
            Debug.Log($"[Tester] 현재 포획 확률: {rate * 100f:F1}% (캡슐 등급: {testCapsuleTier})");

            // 캡슐 명중 알림 → 빛남+축소 연출 시작
            capturable.NotifyCaptureBallHit(fakeCapsulePos);
        }

        /// <summary>
        /// [2/3번] 전체 포획 흐름 코루틴.
        /// NotifyCaptureBallHit → (딜레이) → OnCaptureSuccess 또는 OnCaptureFail
        /// </summary>
        /// <param name="success">true=성공, false=실패</param>
        private IEnumerator TestCaptureFlow(bool success)
        {
            isTesting = true;

            ICapturable capturable = targetMem;
            Vector3 fakeCapsulePos = targetMem.transform.position + fakeCapsuleOffset;

            // 포획 확률 콘솔 출력
            float rate = capturable.GetCaptureRate(testCapsuleTier);
            Debug.Log($"[Tester] 현재 포획 확률: {rate * 100f:F1}% (캡슐 등급: {testCapsuleTier})");
            Debug.Log($"[Tester] 가짜 캡슐 위치: {fakeCapsulePos}");

            // ------------------------------------------------------------------
            // STEP 1: 캡슐 명중 알림 → 흡수 연출 시작 + OnMemCaptureStarted 발행
            // ------------------------------------------------------------------
            Debug.Log("[Tester] [STEP 1] NotifyCaptureBallHit 호출 → 흡수 연출 시작");
            capturable.NotifyCaptureBallHit(fakeCapsulePos);

            // ------------------------------------------------------------------
            // STEP 2: 흡수+흔들림 연출 대기 (judgeDelay)
            // ------------------------------------------------------------------
            Debug.Log($"[Tester] [STEP 2] 판정 대기 중... ({judgeDelay}초)");
            yield return new WaitForSeconds(judgeDelay);

            // ------------------------------------------------------------------
            // STEP 3: 판정 결과 처리
            // ------------------------------------------------------------------
            if (success)
            {
                Debug.Log("[Tester] [STEP 3] OnCaptureSuccess 호출 → 포획 성공 처리");
                capturable.OnCaptureSuccess();
            }
            else
            {
                Debug.Log($"[Tester] [STEP 3] OnCaptureFail 호출 → 포획 실패 처리");
                capturable.OnCaptureFail();
            }

            isTesting = false;
        }

        /// <summary>
        /// [4번] 멤 상태를 강제로 리셋합니다.
        /// 연출 도중 중단하거나, 성공/실패 처리 후 다시 테스트하고 싶을 때 사용합니다.
        ///
        /// ⚠️ 주의: 성공 처리(OnCaptureSuccess) 후에는 IsActive=false가 되므로,
        ///          씬에서 멤 오브젝트를 다시 활성화해야 재테스트가 가능합니다.
        /// </summary>
        private void ForceReset()
        {
            StopAllCoroutines();
            isTesting = false;

            if (targetMem != null)
                targetMem.ResetForPool();

            // 강제 리셋 후 멤을 다시 활성 상태로 복원
            // ResetForPool은 IsActive=false로 만들기 때문에 테스트용으로 다시 true 설정
            // (실제 게임에서는 MemPool이 처리하지만 테스트 환경에서는 수동 처리)
            var activeField = typeof(Mem).GetProperty(
                "IsActive",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance
            );
            // IsActive는 private set이므로 리플렉션 없이는 직접 설정 불가
            // → ResetForPool 이후 멤을 직접 씬에서 다시 Enable해도 됩니다.
            Debug.Log("[Tester] 강제 리셋 완료. 멤 오브젝트를 Inspector에서 다시 Enable하거나 씬을 재시작하세요.");
        }

        // =================================================================
        // MemEvents 수신 확인용 로그 (캡슐 담당자 역할 시뮬레이션)
        // =================================================================

        /// <summary>OnMemCaptureStarted 수신 확인 — 캡슐 흔들림 연출 시뮬레이션</summary>
        private void OnCaptureStarted(Mem mem, Vector3 capsulePos)
        {
            Debug.Log($"[Tester][이벤트 수신] OnMemCaptureStarted — 멤: {mem.Stats?.MemName}, 캡슐 위치: {capsulePos}");
            Debug.Log($"[Tester]  → 실제 게임에서는 여기서 캡슐 흔들림 애니메이션을 재생합니다.");
        }

        /// <summary>OnMemCaptured 수신 확인 — 캡슐 반짝임+사라짐 연출 시뮬레이션</summary>
        private void OnCaptureSuccess(Mem mem, MemSnapshot snapshot)
        {
            Debug.Log($"[Tester][이벤트 수신] OnMemCaptured — 멤: {snapshot?.memName}, 등급: {snapshot?.tier}");
            Debug.Log($"[Tester]  → 실제 게임에서는 여기서 캡슐 반짝임 + 사라짐 연출을 재생합니다.");
        }

        /// <summary>OnMemCaptureFailed 수신 확인 — 캡슐 파열 연출 시뮬레이션</summary>
        private void OnCaptureFailed(Mem mem)
        {
            Debug.Log($"[Tester][이벤트 수신] OnMemCaptureFailed — 멤: {mem.Stats?.MemName}");
            Debug.Log($"[Tester]  → 실제 게임에서는 여기서 캡슐 파열 이펙트를 재생합니다.");
        }

        // =================================================================
        // 에디터 시각화 (Gizmos)
        // =================================================================
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (targetMem == null) return;

            // 가짜 캡슐 위치 표시 (파란색 구)
            Vector3 fakeCapsulePos = targetMem.transform.position + fakeCapsuleOffset;
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
            Gizmos.DrawSphere(fakeCapsulePos, 0.2f);

            // 멤 → 가짜 캡슐 방향 화살표
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f);
            Gizmos.DrawLine(targetMem.transform.position, fakeCapsulePos);

            // 레이블
            UnityEditor.Handles.Label(
                fakeCapsulePos + Vector3.up * 0.3f,
                "Fake Capsule\n(테스트 캡슐 위치)"
            );
        }
#endif
    }
}
