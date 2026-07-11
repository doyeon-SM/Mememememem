// ============================================================================
// CombatState.cs
// 전투 상태 — 플레이어를 향해 추적 + 공격
// ============================================================================
using UnityEngine;
using MemSystem.Events;
using MemSystem.Visual;

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
        // 추적 포기 거리는 Update에서 DetectionRange에 비례하여 동적으로 계산됩니다.

        public void Enter(MemAI ai)
        {
            attackTimer = 0f;

            // Run 애니메이션 (추적 중 달리기 모션)
            if (ai.Visual != null)
                ai.Visual.PlayRun();

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

            var stats = ai.Owner.Stats;

            // 추적 포기 거리: 기본 50f, 감지 범위가 더 크면 감지 범위의 1.5배까지 추적
            float currentLeashRange = Mathf.Max(50f, stats.DetectionRange * 1.5f);

            // 추적 포기: 플레이어가 너무 멀어지면 Idle로 복귀
            if (distance > currentLeashRange)
            {
                Debug.Log($"[CombatState] {ai.Owner.Stats.MemName} 추적 포기 — 플레이어 이탈 (거리: {distance:F1} > {currentLeashRange:F1})");
                ai.TransitionTo(ai.IdleState);
                return;
            }

            // 사거리 체크 (콜라이더 두께를 고려하여 0.8f의 여유 반경을 둡니다)
            // 에디터에서 사거리를 0.8로 설정했더라도, 콜라이더 때문에 1.0 거리에서 막히는 현상 방지
            bool inRange = distance <= (stats.AttackRange + 0.8f);

            // [개선된 로직] 다른 멤에게 밀려 사거리 밖(2열)에서 길막 당한 경우,
            // 멍때리는 어색함을 없애기 위해 조금 멀어도 함께 공격하도록 사거리를 늘려줍니다.
            if (!inRange && distance <= (stats.AttackRange + 2.5f))
            {
                if (ai.Movement != null && ai.Movement.CurrentSpeed <= 0.1f)
                {
                    inRange = true; 
                }
            }

            if (!inRange)
            {
                // 사거리 밖: 무조건 플레이어를 향해 달립니다.
                if (ai.Movement != null)
                    ai.Movement.ChaseTo(ai.PlayerTransform);
                
                if (ai.Visual != null)
                {
                    // 거리가 먼데 길막을 당했다면 제자리 뛰기 대신 대기
                    if (ai.Movement != null && ai.Movement.CurrentSpeed <= 0.1f)
                    {
                        if (ai.Visual.CurrentAnimState != MemVisual.AnimState.Idle)
                            ai.Visual.PlayIdle();
                    }
                    else
                    {
                        if (ai.Visual.CurrentAnimState != MemVisual.AnimState.Run)
                            ai.Visual.PlayRun();
                    }
                }
            }
            else
            {
                // 사거리 안: 정지 + 플레이어 방향 회전
                if (ai.Movement != null)
                {
                    ai.Movement.Stop();
                    ai.Movement.LookAt(ai.PlayerTransform.position);
                }

                // 멈춰서 공격 대기 중일 때는 대기 모션
                if (ai.Visual != null && ai.Visual.CurrentAnimState != MemVisual.AnimState.Idle && ai.Visual.CurrentAnimState != MemVisual.AnimState.Attack)
                    ai.Visual.PlayIdle();

                // 공격 쿨타임 처리
                attackTimer -= Time.deltaTime;
                
                // 쿨타임이 지났더라도, 현재 공격 모션이 완전히 끝나지 않았다면 새 공격을 덮어씌우지 않고 기다립니다.
                if (attackTimer <= 0f && (ai.Visual == null || ai.Visual.CurrentAnimState != MemVisual.AnimState.Attack))
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
            
            if (ai.Visual != null)
                ai.Visual.CancelAttack();
        }

        /// <summary>
        /// 공격 실행.
        /// 모션 재생: MemVisual.PlayAttack()
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
