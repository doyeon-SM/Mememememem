// ============================================================================
// WanderState.cs
// 배회 상태 — 랜덤 지점으로 이동 후 Idle로 복귀
// ============================================================================
using UnityEngine;
using MemSystem.Visual;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 배회 상태.
    /// NavMesh 위의 랜덤 지점으로 이동하며, 목적지 도달 시
    /// 확률(ReturnToIdleChance)에 따라 Idle로 복귀하거나 다시 Wander합니다.
    /// 5초간 이동 못하면 Stuck으로 판정하고 새 목적지를 설정합니다.
    /// </summary>
    public class WanderState : IMemState
    {
        private float stuckTimer;
        private const float STUCK_THRESHOLD = 5f; // 5초간 도착 못하면 재시도

        public void Enter(MemAI ai)
        {
            // 랜덤 지점으로 이동 시작
            if (ai.Movement != null)
                ai.Movement.Wander();

            stuckTimer = 0f;
        }

        public void Update(MemAI ai)
        {
            if (ai.Movement == null) return;

            // 실제 이동 속도가 낮으면(막힘, 경로 대기 중) 대기 모션, 이동 중이면 걷기 모션
            if (ai.Movement.CurrentSpeed > 0.1f)
            {
                if (ai.Visual != null && ai.Visual.CurrentAnimState != MemVisual.AnimState.Walk)
                    ai.Visual.PlayWalk();
            }
            else
            {
                if (ai.Visual != null && ai.Visual.CurrentAnimState != MemVisual.AnimState.Idle)
                    ai.Visual.PlayIdle();
            }

            // 목적지 도달 체크
            if (ai.Movement.HasReachedDestination())
            {
                // 확률에 따라 Idle 복귀 또는 재배회
                if (Random.value < ai.ReturnToIdleChance)
                {
                    ai.TransitionTo(ai.IdleState);
                }
                else
                {
                    // 새 목적지로 다시 배회
                    ai.Movement.Wander();
                    stuckTimer = 0f;
                }
                return;
            }

            // Stuck 방지 — 일정 시간 못 도착하면 새 목적지로 재시도
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= STUCK_THRESHOLD)
            {
                ai.Movement.Wander();
                stuckTimer = 0f;
            }
        }

        public void Exit(MemAI ai)
        {
            // 정리 로직 없음
        }
    }
}
