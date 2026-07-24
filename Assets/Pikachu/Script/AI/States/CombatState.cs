// ============================================================================
// CombatState.cs
// 전투 상태 — 플레이어를 향해 추적 + 공격
// ============================================================================
using UnityEngine;
using MemSystem.Core;
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
        // =================================================================
        // 튜닝 상수
        // =================================================================

        /// <summary>콜라이더 두께 보정. 사거리를 0.8로 잡아도 1.0에서 막히는 현상 방지.</summary>
        private const float RangePadding = 0.8f;

        /// <summary>사거리 안에 자리잡은 뒤 이만큼 벌어질 때까지는 추적을 재개하지 않습니다(떨림 방지).</summary>
        private const float RangeHysteresis = 0.5f;

        /// <summary>
        /// 길막 상태로 인정될 때, 사거리 밖이어도 공격을 허용하는 추가 거리.
        ///
        /// 실제로 이 판정이 걸리는 구간은 (AttackRange + RangePadding) ~ (AttackRange + 이 값) 이므로,
        /// 구간 폭 = BlockedExtraRange - RangePadding 입니다.
        /// 뒷줄(2열)에 밀린 멤은 몸통 지름(약 0.8m)만큼 뒤에 서므로,
        /// 2열 멤까지 같이 공격하게 하려면 이 값이 RangePadding + 0.8 이상이어야 합니다.
        /// </summary>
        private const float BlockedExtraRange = 1.0f;

        /// <summary>이 속도 이하면 사실상 멈춰 있는 것으로 봅니다.</summary>
        private const float BlockedSpeedThreshold = 0.1f;

        /// <summary>
        /// 추적을 지시했는데도 이 시간 이상 못 움직여야 '길막'으로 인정합니다.
        /// 경로 계산(pathPending)과 가속에 몇 프레임이 걸리므로 유예가 필요합니다.
        /// </summary>
        private const float BlockedGraceTime = 0.5f;

        /// <summary>
        /// 추적 포기 거리 = DetectionRange × 이 배수. 인식 범위를 벗어나 이만큼 멀어지면 흥미를 잃습니다.
        /// 1.0보다 커야 인식 범위 경계에서 Combat↔Idle이 떨리지 않습니다.
        /// </summary>
        private const float LeashMultiplier = 2f;

        /// <summary>추적 포기 거리의 하한. 인식 범위가 아주 짧은 멤이 즉시 포기하는 것을 막습니다.</summary>
        private const float MinLeashRange = 15f;

        /// <summary>NavMesh 밖으로 벗어났을 때 복귀 지점을 찾을 반경.</summary>
        private const float NavMeshRecoverRadius = 5f;

        /// <summary>이 시간 이상 NavMesh 복귀에 실패하면 전투를 포기하고 Idle로 넘깁니다.</summary>
        private const float OffMeshGiveUpTime = 2f;

        // =================================================================
        // 상태 변수
        // =================================================================

        private float attackTimer;

        /// <summary>사거리 안에 자리잡고 멈춰서 공격 중인지 (히스테리시스용)</summary>
        private bool isHolding;

        /// <summary>추적 지시 후에도 실제로 못 움직인 누적 시간</summary>
        private float blockedTimer;

        /// <summary>NavMesh 밖에 놓인 채 복귀하지 못한 누적 시간</summary>
        private float offMeshTimer;

        // 추적 포기 거리는 Update에서 DetectionRange에 비례하여 동적으로 계산됩니다.

        public void Enter(MemAI ai)
        {
            attackTimer  = 0f;
            isHolding    = false;
            blockedTimer = 0f;
            offMeshTimer = 0f;

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

            // NavMesh 밖으로 벗어났으면 먼저 복귀시킨다.
            // Idle/Wander와 달리 Combat에는 자가 복구가 없어서, 한번 벗어나면
            // ChaseTo()/Stop()이 전부 조용히 무시되어 제자리에 영영 굳는다.
            // (스폰 지점이 NavMesh 밖이었거나, 시설 카빙에 갇힌 경우)
            if (!RecoverNavMeshIfNeeded(ai)) return;

            float distance = ai.DistanceToPlayer;

            var stats = ai.Owner.Stats;

            // 추적 포기 거리: 인식 범위에 비례. 인식 범위를 충분히 벗어나면 흥미를 잃고
            // Idle → Wander로 돌아가 다시 평소처럼 돌아다닌다.
            float currentLeashRange = Mathf.Max(MinLeashRange, stats.DetectionRange * LeashMultiplier);

            // 추적 포기: 플레이어가 너무 멀어지면 Idle로 복귀
            if (distance > currentLeashRange)
            {
                Debug.Log($"[CombatState] {ai.Owner.Stats.MemName} 추적 포기 — 플레이어 이탈 (거리: {distance:F1} > {currentLeashRange:F1})");
                ai.TransitionTo(ai.IdleState);
                return;
            }

            // 멈춰 설 사거리 (콜라이더 두께를 고려한 여유 반경 포함).
            // 한번 자리를 잡으면 살짝 벌어져도 유지해서, 경계선에서 멈춤/추적이 떨리는 것을 막습니다.
            float stopRange = stats.AttackRange + RangePadding;
            if (isHolding) stopRange += RangeHysteresis;

            if (distance <= stopRange)
            {
                // =========================================================
                // 사거리 안: 멈춰서 공격
                // =========================================================
                bool justArrived = !isHolding;
                isHolding    = true;
                blockedTimer = 0f;

                if (ai.Movement != null)
                {
                    // Stop()은 사거리에 막 진입한 프레임에만 — 매 프레임 ResetPath()를
                    // 호출하면 개체 수만큼 불필요한 경로 초기화가 쌓인다.
                    if (justArrived) ai.Movement.Stop();

                    ai.Movement.LookAt(ai.PlayerTransform.position);
                }

                PlayIdleIfNeeded(ai);
                TickAttack(ai, stats);
            }
            else
            {
                // =========================================================
                // 사거리 밖: 추적
                // =========================================================
                isHolding = false;

                if (ai.Movement != null)
                {
                    // 반드시 추적을 '먼저' 지시한다.
                    // 그래야 아래 속도 판정이 "스스로 멈춰 있는 것"이 아니라
                    // "가려고 하는데 못 가는 것(길막)"만 잡아낸다.
                    ai.Movement.ChaseTo(ai.PlayerTransform);

                    bool stuck = ai.Movement.CurrentSpeed <= BlockedSpeedThreshold;
                    blockedTimer = stuck ? blockedTimer + Time.deltaTime : 0f;
                }

                // 길막 판정: 추적 중인데도 일정 시간 이상 못 움직였고, 조금만 더 가면 닿는 거리.
                // 이때는 제자리에서라도 공격해 멍때리는 어색함을 없앤다.
                // 단, 여기서 Stop()을 부르면 안 된다 — 멈추면 속도가 0으로 고정되어
                // "길막"이 영원히 참이 되고, 멤이 다시는 움직이지 못하는 상태로 굳는다.
                bool blockedButClose =
                    blockedTimer >= BlockedGraceTime &&
                    distance <= stats.AttackRange + BlockedExtraRange;

                if (blockedButClose)
                {
                    // 추적은 계속 유지한 채(빈틈이 생기면 바로 파고들도록) 공격만 허용
                    if (ai.Movement != null)
                        ai.Movement.LookAt(ai.PlayerTransform.position);

                    PlayIdleIfNeeded(ai);
                    TickAttack(ai, stats);
                }
                else if (blockedTimer >= BlockedGraceTime)
                {
                    // 아직 멀리 있는데 길막된 상태 — 제자리 뛰기 대신 대기 모션
                    PlayIdleIfNeeded(ai);
                }
                else
                {
                    PlayRunIfNeeded(ai);
                }
            }
        }

        public void Exit(MemAI ai)
        {
            isHolding    = false;
            blockedTimer = 0f;

            if (ai.Movement != null)
                ai.Movement.Stop();

            if (ai.Visual != null)
                ai.Visual.CancelAttack();
        }

        // =================================================================
        // 내부 헬퍼
        // =================================================================

        /// <summary>
        /// NavMesh 밖에 놓였으면 복귀를 시도합니다.
        /// 복귀에 실패하면 이번 프레임의 전투 로직은 건너뛰고(다음 프레임 재시도),
        /// 일정 시간 이상 실패가 계속되면 전투를 포기하고 Idle로 넘겨
        /// Idle/Wander의 기존 복구 루프가 처리하게 합니다.
        /// </summary>
        /// <returns>NavMesh 위에 있어 전투를 계속해도 되면 true</returns>
        private bool RecoverNavMeshIfNeeded(MemAI ai)
        {
            if (ai.Movement == null || ai.Movement.IsOnNavMesh)
            {
                offMeshTimer = 0f;
                return true;
            }

            if (ai.Movement.TryRecoverToNavMesh(NavMeshRecoverRadius))
            {
                offMeshTimer = 0f;
                return true;
            }

            offMeshTimer += Time.deltaTime;
            PlayIdleIfNeeded(ai);

            if (offMeshTimer >= OffMeshGiveUpTime)
            {
                Debug.LogWarning(
                    $"[CombatState] {ai.Owner?.Stats?.MemName} NavMesh 복귀 실패 — 전투 포기 후 Idle 전환 " +
                    $"(위치: {ai.transform.position})", ai);
                ai.TransitionTo(ai.IdleState);
            }

            return false;
        }

        /// <summary>공격 쿨타임을 진행시키고, 조건이 되면 공격합니다.</summary>
        private void TickAttack(MemAI ai, MemStats stats)
        {
            attackTimer -= Time.deltaTime;

            // 쿨타임이 지났더라도, 현재 공격 모션이 완전히 끝나지 않았다면
            // 새 공격을 덮어씌우지 않고 기다립니다.
            if (attackTimer <= 0f &&
                (ai.Visual == null || ai.Visual.CurrentAnimState != MemVisual.AnimState.Attack))
            {
                PerformAttack(ai);
                attackTimer = stats.AttackCooldown;
            }
        }

        private void PlayIdleIfNeeded(MemAI ai)
        {
            if (ai.Visual == null) return;
            if (ai.Visual.CurrentAnimState == MemVisual.AnimState.Idle) return;
            if (ai.Visual.CurrentAnimState == MemVisual.AnimState.Attack) return;

            ai.Visual.PlayIdle();
        }

        private void PlayRunIfNeeded(MemAI ai)
        {
            if (ai.Visual == null) return;
            if (ai.Visual.CurrentAnimState == MemVisual.AnimState.Run) return;

            ai.Visual.PlayRun();
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
