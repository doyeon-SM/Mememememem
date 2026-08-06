// ============================================================================
// IdleState.cs
// 대기 상태 — 제자리에서 일정 시간 머무른 후 Wander로 전환
// ============================================================================
using UnityEngine;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 대기 상태.
    /// 제자리에서 프로시저럴 바운스 애니메이션을 재생하며,
    /// 랜덤 시간(2~5초) 경과 후 Wander 상태로 전환합니다.
    /// </summary>
    public class IdleState : IMemState
    {
        private float idleTimer;
        private float idleDuration;

        public void Enter(MemAI ai)
        {
            // 이동 정지
            if (ai.Movement != null)
                ai.Movement.Stop();

            // Idle 애니메이션 (상하 바운스)
            if (ai.Visual != null)
                ai.Visual.PlayIdle();

            // 대기 시간 랜덤 설정
            idleDuration = Random.Range(ai.IdleDurationRange.x, ai.IdleDurationRange.y);
            idleTimer = 0f;
        }

        public void Update(MemAI ai)
        {
            // 시설 카빙에 갇혀 NavMesh 밖에 놓였으면 즉시 Wander로 전환해 복구한다.
            if (ai.Movement != null && !ai.Movement.IsOnNavMesh)
            {
                ai.TransitionTo(ai.WanderState);
                return;
            }

            idleTimer += Time.deltaTime;

            // 대기 시간 경과 → Wander로 전환
            if (idleTimer >= idleDuration)
            {
                ai.TransitionTo(ai.WanderState);
            }
        }

        public void Exit(MemAI ai)
        {
            // 정리 로직 없음
        }
    }
}
