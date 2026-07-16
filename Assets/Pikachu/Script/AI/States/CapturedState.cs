// ============================================================================
// CapturedState.cs
// 포획 상태 — 포획 성공 연출 후 풀 반환 준비
//
// [담당자 안내]
// - Mem.OnCaptureSuccess()에서 MemSnapshot 생성 + 이벤트 발행이
//   먼저 처리된 후 이 상태로 전환됩니다.
// - 축소 연출(0.5초) 후 풀에 반환될 준비를 합니다.
// - 실제 풀 반환은 MemSpawner가 IsActive 체크로 처리합니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 포획 상태.
    /// EaseInBack 커브로 멤이 점점 축소되며 사라지는 연출을 재생합니다.
    /// </summary>
    public class CapturedState : IMemState
    {
        private float captureAnimDuration = 0.5f; // 포획 연출 시간 (초)
        private float timer;
        private Vector3 originalScale;

        public void Enter(MemAI ai)
        {
            timer = 0f;
            originalScale = ai.transform.localScale;

            // 이동 정지
            if (ai.Movement != null)
                ai.Movement.Stop();

            Debug.Log($"[CapturedState] {ai.Owner?.Stats?.MemName} 포획 연출 시작");
        }

        public void Update(MemAI ai)
        {
            timer += Time.deltaTime;

            // 축소 연출 (EaseInBack: 살짝 당긴 후 빠르게 수축)
            float progress = Mathf.Clamp01(timer / captureAnimDuration);
            float scale = Mathf.Lerp(1f, 0f, EaseInBack(progress));
            ai.transform.localScale = originalScale * Mathf.Max(scale, 0.01f);

            // 연출 완료 → 스케일 복원 (풀 반환 시 재사용을 위해)
            if (timer >= captureAnimDuration)
            {
                ai.transform.localScale = originalScale;
                // 풀 반환은 MemSpawner/MemPool에서 IsActive == false 체크로 자동 처리
            }
        }

        public void Exit(MemAI ai)
        {
            // 스케일 복원 (안전장치)
            ai.transform.localScale = originalScale;
        }

        /// <summary>
        /// EaseInBack 커브 — 살짝 뒤로 당긴 후 빠르게 수축하는 느낌.
        /// 캡슐에 빨려 들어가는 연출에 적합합니다.
        /// </summary>
        private float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }
    }
}
