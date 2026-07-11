// ============================================================================
// FleeState.cs
// 도주 상태 — 플레이어 반대 방향으로 도망, 일정 거리 달성 시 디스폰
//
// [담당자 안내]
// - Visual: PlayRun() 사용 — Walk보다 빠르고 좌우 흔들림이 강한 달리기 모션
// - 온순 멤: 피격 즉시 이 상태로 전환됩니다.
// - 평범/난폭 멤: HP가 도주 임계치 이하일 때 이 상태로 전환됩니다.
// - 도주 거리(MemMovement.FleeDistance) 달성 또는 10초 타임아웃 시 디스폰 처리합니다.
// ============================================================================
using UnityEngine;
using MemSystem.Visual;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 멤 도주 상태.
    /// 
    /// [동작 흐름]
    /// 1. 플레이어 반대 방향 계산 → NavMesh 위의 유효한 도주 지점 탐색
    /// 2. 도주 속도(fleeSpeed)로 이동
    /// 3. 도주 거리 달성 → OnFleeComplete() → MemEvents.OnMemFled 발행 → 풀 반환
    /// 
    /// [안전장치]
    /// - 10초 타임아웃: 지형에 막혀서 도주 못하는 경우 강제 디스폰
    /// - 목적지 도달 시 도주 방향 재계산 (플레이어가 추적하는 경우)
    /// </summary>
    public class FleeState : IMemState
    {
        private Vector3 fleeStartPosition;
        private float fleeTimeout = 10f;
        private float fleeTimer;

        public void Enter(MemAI ai)
        {
            fleeStartPosition = ai.transform.position;
            fleeTimer = 0f;

            // 달리기 애니메이션 재생은 Update에서 속도 기반으로 자동 제어됩니다.
            // ai.Visual.PlayRun();

            // 플레이어 반대 방향으로 도주 시작
            if (ai.Movement != null && ai.PlayerTransform != null)
            {
                ai.Movement.FleeFrom(ai.PlayerTransform.position);
            }

            Debug.Log($"[FleeState] {ai.Owner?.Stats?.MemName} 도주 시작!");
        }

        public void Update(MemAI ai)
        {
            if (ai.Owner == null || ai.Movement == null) return;

            // 실제 이동 속도가 낮으면(지형에 막힘) 대기 모션, 이동 중이면 달리기 모션
            if (ai.Movement.CurrentSpeed > 0.1f)
            {
                if (ai.Visual != null && ai.Visual.CurrentAnimState != MemVisual.AnimState.Run)
                    ai.Visual.PlayRun();
            }
            else
            {
                if (ai.Visual != null && ai.Visual.CurrentAnimState != MemVisual.AnimState.Idle)
                    ai.Visual.PlayIdle();
            }

            fleeTimer += Time.deltaTime;

            // 도주 거리 달성 체크
            float distanceFromStart = Vector3.Distance(ai.transform.position, fleeStartPosition);
            if (distanceFromStart >= ai.Movement.FleeDistance)
            {
                OnFleeComplete(ai);
                return;
            }

            // 목적지 도달 → 도주 방향 재계산 (플레이어가 쫓아오는 경우)
            if (ai.Movement.HasReachedDestination())
            {
                if (ai.PlayerTransform != null)
                {
                    ai.Movement.FleeFrom(ai.PlayerTransform.position);
                }
                else
                {
                    OnFleeComplete(ai);
                    return;
                }
            }

            // 타임아웃: 지형에 막혀 도주 불가 시 강제 디스폰
            if (fleeTimer >= fleeTimeout)
            {
                Debug.LogWarning($"[FleeState] {ai.Owner.Stats?.MemName} 도주 타임아웃 — 강제 디스폰");
                OnFleeComplete(ai);
            }
        }

        public void Exit(MemAI ai)
        {
            if (ai.Movement != null)
                ai.Movement.Stop();
        }

        private void OnFleeComplete(MemAI ai)
        {
            Debug.Log($"[FleeState] {ai.Owner?.Stats?.MemName} 도주 완료 — 디스폰 처리");
            ai.Owner?.OnFleeComplete();
        }
    }
}
