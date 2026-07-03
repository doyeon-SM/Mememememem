// ============================================================================
// CombatState.cs
// 전투 상태 — 플레이어를 향해 추적 + 공격 (모션 없이 판정)
//
// [담당자 안내]
// 현재 FBX 애니메이션이 없으므로 공격 모션을 프로시저럴로 대체합니다:
// - 플레이어 방향으로 회전 → 짧은 전방 돌진(Lunge) → 복귀
// - 돌진 타이밍에 데미지 이벤트(MemEvents.OnMemAttackPlayer) 발행
// - 나중에 아티스트 애니메이션이 준비되면 MemVisual에서 교체하면 됩니다.
// ============================================================================
using UnityEngine;
using MemSystem.Events;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 전투 상태.
    /// 
    /// [동작 흐름]
    /// 1. 플레이어를 향해 추적 (NavMesh)
    /// 2. 사거리 안 도달 → 이동 정지 + 플레이어 방향 회전
    /// 3. 공격 쿨타임 체크 → 쿨타임 완료 시 공격 실행
    /// 4. 공격 연출: MemVisual.PlayAttack() (전방 돌진)
    /// 5. 데미지 이벤트 발행: MemEvents.OnMemAttackPlayer
    /// 
    /// [전환 조건]
    /// - HP가 도주 임계치 이하 → Flee 상태로 전환
    /// - 플레이어가 추적 포기 거리(50m) 초과 → Idle로 복귀
    /// </summary>
    public class CombatState : IMemState
    {
        private float attackTimer;
        private float leashRange = 50f; // 추적 포기 거리

        public void Enter(MemAI ai)
        {
            attackTimer = 0f;

            // Walk 애니메이션 (추적 중)
            if (ai.Visual != null)
                ai.Visual.PlayWalk();

            Debug.Log($"[CombatState] {ai.Owner?.Stats?.MemName} 전투 돌입!");
        }

        public void Update(MemAI ai)
        {
            if (ai.Owner == null || ai.Owner.Stats == null) return;
            if (ai.PlayerTransform == null) return;

            // HP 체크: 도주 조건 충족 시 Flee로 전환
            if (ai.Owner.Stats.ShouldFlee)
            {
                ai.TransitionTo(ai.FleeState);
                return;
            }

            float distance = ai.DistanceToPlayer;

            // 추적 포기: 플레이어가 너무 멀어지면 Idle로 복귀
            if (distance > leashRange)
            {
                Debug.Log($"[CombatState] {ai.Owner.Stats.MemName} 추적 포기 — 플레이어 이탈");
                ai.TransitionTo(ai.IdleState);
                return;
            }

            var stats = ai.Owner.Stats;

            // 사거리 밖: 플레이어를 향해 추적
            if (distance > stats.AttackRange)
            {
                if (ai.Movement != null)
                    ai.Movement.ChaseTo(ai.PlayerTransform);
            }
            else
            {
                // 사거리 안: 정지 + 플레이어 방향 회전 + 공격
                if (ai.Movement != null)
                {
                    ai.Movement.Stop();
                    ai.Movement.LookAt(ai.PlayerTransform.position);
                }

                // 공격 쿨타임 처리
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    PerformAttack(ai);
                    attackTimer = stats.AttackCooldown;
                }
            }
        }

        public void Exit(MemAI ai)
        {
            if (ai.Movement != null)
                ai.Movement.Stop();
        }

        /// <summary>
        /// 공격 실행.
        /// 모션 대체: MemVisual.PlayAttack() → 전방 돌진 연출
        /// 판정: MemEvents.OnMemAttackPlayer 이벤트로 데미지 전달
        /// </summary>
        private void PerformAttack(MemAI ai)
        {
            var stats = ai.Owner.Stats;

            // 공격 연출 (전방 돌진 → 복귀)
            if (ai.Visual != null)
                ai.Visual.PlayAttack();

            // 데미지 이벤트 발행 → 플레이어 시스템이 수신하여 HP 감소 처리
            MemEvents.OnMemAttackPlayer?.Invoke(ai.Owner, stats.AttackDamage);

            Debug.Log($"[CombatState] {stats.MemName} 공격! 데미지: {stats.AttackDamage}");
        }
    }
}
