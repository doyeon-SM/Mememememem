// ============================================================================
// HappyState.cs
// 행복 상태 — 특별한 이벤트 발생 시 행복한 모션 재생
//
// [담당자 안내]
// - MemAI.TriggerHappy()를 외부에서 호출하면 이 상태로 전환됩니다.
// - 일정 시간(happyDuration) 후 자동으로 Idle로 복귀합니다.
// - 행복 모션 중에는 이동하지 않습니다.
//
// [트리거 예시]
// - 먹이를 받은 직후 (허기 회복)
// - 플레이어와 상호작용 완료
// - 채집/작업 완료 보상 등
// ============================================================================
using UnityEngine;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 행복 상태.
    ///
    /// [동작 흐름]
    /// 1. Enter: 이동 정지 → PlayHappy() (점프 + 흔들림 모션)
    /// 2. Update: happyDuration 경과 → Idle로 복귀
    ///
    /// [전환 조건]
    /// - happyDuration 초 경과 → Idle
    /// </summary>
    public class HappyState : IMemState
    {
        private float happyTimer;
        private float happyDuration;

        /// <summary>
        /// 기본 행복 모션 지속 시간 (초).
        /// MemAI.TriggerHappy()에서 인자로 재정의 가능합니다.
        /// </summary>
        private const float DEFAULT_DURATION = 2.5f;

        public HappyState(float duration = DEFAULT_DURATION)
        {
            happyDuration = duration;
        }

        public void Enter(MemAI ai)
        {
            happyTimer = 0f;

            // 이동 정지 (행복 모션 중에는 제자리)
            if (ai.Movement != null)
                ai.Movement.Stop();

            // 행복 애니메이션 (점프 + 좌우 흔들림)
            if (ai.Visual != null)
                ai.Visual.PlayHappy();

            Debug.Log($"[HappyState] {ai.Owner?.Stats?.MemName} 행복 모션 시작!");
        }

        public void Update(MemAI ai)
        {
            happyTimer += Time.deltaTime;

            // 행복 시간 경과 → Idle 복귀
            if (happyTimer >= happyDuration)
            {
                ai.TransitionTo(ai.IdleState);
            }
        }

        public void Exit(MemAI ai)
        {
            // 정리 로직 없음
        }
    }
}
