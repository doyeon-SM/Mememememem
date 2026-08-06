// ============================================================================
// CapturedState.cs
// 포획 상태 — 멤이 캡슐에 빨려들어가는 연출 후 풀 반환 준비
//
// [담당자 안내]
// - Mem.NotifyCaptureBallHit()에서 SetCaptureTarget() 호출 후 이 상태로 전환됩니다.
// - Enter() 시 MemVisual.PlayCaptureAbsorb()를 호출하여 연출을 위임합니다.
//   (빛남+축소+이동 연출은 MemVisual 코루틴이 독립적으로 처리)
// - 기존의 Update()에서 직접 스케일을 조작하는 로직은 MemVisual로 이전되었습니다.
// - 실제 풀 반환은 MemSpawner/MemPool에서 IsActive == false 체크로 자동 처리합니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 포획 상태.
    /// Enter() 시 MemVisual.PlayCaptureAbsorb()를 호출하여 빛남+축소 연출을 위임합니다.
    /// 연출의 실제 구현은 MemVisual의 코루틴에서 처리되며,
    /// 이 상태는 "이동 정지 + 연출 위임"만 담당합니다.
    /// </summary>
    public class CapturedState : IMemState
    {
        // -----------------------------------------------------------------
        // 캡슐 위치 — Mem.NotifyCaptureBallHit()에서 설정
        // -----------------------------------------------------------------

        /// <summary>
        /// 멤이 빨려들어갈 캡슐의 월드 좌표.
        /// Mem.NotifyCaptureBallHit()에서 SetCaptureTarget()으로 설정됩니다.
        /// </summary>
        private Vector3 captureTargetPosition;

        /// <summary>
        /// 포획 대상 위치를 설정합니다.
        /// TransitionTo(CapturedState) 직전에 반드시 호출해야 합니다.
        /// </summary>
        /// <param name="capsuleWorldPos">캡슐의 월드 좌표</param>
        public void SetCaptureTarget(Vector3 capsuleWorldPos)
        {
            captureTargetPosition = capsuleWorldPos;
        }

        public void Enter(MemAI ai)
        {
            // 이동 즉시 정지
            if (ai.Movement != null)
                ai.Movement.Stop();

            // 포획 흡수 연출 시작 — MemVisual에 위임
            // (빛남 플래시 → 캡슐 방향 이동 + EaseInBack 축소)
            if (ai.Visual != null)
                ai.Visual.PlayCaptureAbsorb(captureTargetPosition);

            Debug.Log($"[CapturedState] {ai.Owner?.Stats?.MemName} 포획 연출 시작 → 캡슐 위치: {captureTargetPosition}");
        }

        public void Update(MemAI ai)
        {
            // 연출은 MemVisual 코루틴이 독립적으로 처리합니다.
            // 이 Update에서 추가 처리가 필요 없습니다.
            // 풀 반환은 MemSpawner/MemPool이 IsActive == false를 감지하여 자동 처리합니다.
        }

        public void Exit(MemAI ai)
        {
            // 안전장치: 코루틴 중단 및 스케일 복원은
            // ResetForPool() → MemVisual.ResetVisual()에서 자동 처리됩니다.
        }
    }
}
