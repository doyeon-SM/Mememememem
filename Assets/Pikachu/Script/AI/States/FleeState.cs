// ============================================================================
// FleeState.cs
// 도주 상태 — 플레이어 반대 방향으로 도망, 일정 거리 달성 시 디스폰
//
// [담당자 안내]
// - 온순 멤: 피격 즉시 이 상태로 전환됩니다.
// - 평범/난폭 멤: HP가 도주 임계치 이하일 때 이 상태로 전환됩니다.
// - 도주 거리(MemMovement.FleeDistance) 달성 또는 10초 타임아웃 시 디스폰 처리합니다.
// ============================================================================
using UnityEngine;

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

            // Flee 애니메이션 (빠른 바운스 + 좌우 흔들림)
            if (ai.Visual != null)
                ai.Visual.PlayFlee();

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
