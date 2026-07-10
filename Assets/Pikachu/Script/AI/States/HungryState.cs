// ============================================================================
// HungryState.cs
// 허기 고갈 상태 — 배고픔이 0일 때 옆으로 누워있기
//
// [담당자 안내]
// - MemStats.IsStarving == true 일 때 MemAI에서 이 상태로 전환합니다.
// - 이 상태에서는 이동/공격이 불가합니다 (쓰러져 있으므로).
// - 허기가 회복되면 (외부 시스템이 CurrentHunger를 올려주면) Idle로 복귀합니다.
// ============================================================================
using UnityEngine;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 허기 고갈 상태.
    ///
    /// [동작 흐름]
    /// 1. Enter: 이동 정지 → PlayHungry() (옆으로 쓰러지는 모션)
    /// 2. Update: IsStarving 해소 감지 → Idle로 복귀
    ///
    /// [전환 조건]
    /// - IsStarving이 false가 되면 → Idle
    /// - Captured 상태 전환은 외부(MemAI.OnDamageTaken 등)에서 직접 처리
    /// </summary>
    public class HungryState : IMemState
    {
        public void Enter(MemAI ai)
        {
            // 이동 정지 (쓰러져 있으므로 움직이면 안 됨)
            if (ai.Movement != null)
                ai.Movement.Stop();

            // 허기 고갈 애니메이션 (옆으로 쓰러져 누워있기)
            if (ai.Visual != null)
                ai.Visual.PlayHungry();

            Debug.Log($"[HungryState] {ai.Owner?.Stats?.MemName} 허기 고갈 — 쓰러짐!");
        }

        public void Update(MemAI ai)
        {
            if (ai.Owner == null || ai.Owner.Stats == null) return;

            // 허기가 회복되면 Idle로 복귀
            if (!ai.Owner.Stats.IsStarving)
            {
                Debug.Log($"[HungryState] {ai.Owner.Stats.MemName} 허기 회복 — 기상!");
                ai.TransitionTo(ai.IdleState);
            }
        }

        public void Exit(MemAI ai)
        {
            // 정리 로직 없음 — MemVisual.StopLoopingCoroutines()는
            // 다음 PlayXXX() 호출 시 자동으로 처리됩니다.
        }
    }
}
