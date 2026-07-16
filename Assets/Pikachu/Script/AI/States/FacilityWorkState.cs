// ============================================================================
// FacilityWorkState.cs
// 시설 작업 FSM 상태
//
// [담당자 안내]
// - 멤이 생산 시설(벌목장, 채굴장, 밭 등)에 배치되면 이 상태로 진입합니다.
// - FacilityEventBridge에서 AI.FacilityWorkState.SetFacility() 설정 후
//   AI.TransitionTo(AI.FacilityWorkState)를 호출합니다.
// - 배치 직후(MemAdded)에는 Idle로 대기, 시설 가동(FacilityStarted) 시 작업 애니 재생.
// - 시설 중지(FacilityStopped) 이유에 따라 다음 상태로 전환됩니다.
//
// [애니메이션 임시 매핑 - 전용 리소스 추가 시 PlayWorkAnimation() 교체]
//   Workshop          (제작대)   → PlayInteract()
//   LoggingCamp       (벌목장)   → PlayInteract()
//   MiningCamp        (채굴장)   → PlayInteract()
//   Farm              (밭)       → PlayInteract()
//   Ranch             (목장)     → Wander() + PlayWalk()
//   TransportFacility (운반시설) → ChaseTo(warehouseTarget) + PlayWalk()
//   Generator         (발전기)   → PlayInteract()
// ============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 생산 시설에 배치된 멤의 작업 FSM 상태.
    /// Enter() 직후에는 Idle로 대기하며, FacilityEventBridge에서
    /// OnFacilityStarted() / OnFacilityStopped()를 명시적으로 호출하여 전환합니다.
    /// </summary>
    public class FacilityWorkState : IMemState
    {
        // ---------------------------------------------------------------
        // 상태 설정 (FacilityEventBridge가 SetFacility()로 주입)
        // ---------------------------------------------------------------

        /// <summary>배치된 시설 종류.</summary>
        private BuildingType facilityType;

        /// <summary>시설이 현재 가동 중인지 여부.</summary>
        private bool isWorking = false;

        // ---------------------------------------------------------------
        // 목장(Ranch) 배회 관련
        // ---------------------------------------------------------------

        private float wanderTimer    = 0f;
        private float wanderInterval = 5f;

        // ---------------------------------------------------------------
        // 운반시설(TransportFacility) 왕복 관련
        // ---------------------------------------------------------------

        /// <summary>운반시설 오브젝트의 Transform (왕복 출발지).</summary>
        private Transform facilityTransform;

        /// <summary>창고 오브젝트의 Transform (왕복 목적지).</summary>
        private Transform warehouseTarget;

        /// <summary>현재 창고 방향으로 이동 중인지 여부.</summary>
        private bool isHeadingToWarehouse = false;

        private const float ReachThreshold = 1.5f;

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------

        /// <summary>
        /// 이 상태에 진입하기 전 반드시 호출하여 시설 정보를 주입합니다.
        /// </summary>
        /// <param name="type">배치된 시설의 BuildingType</param>
        /// <param name="facilityTrans">시설 오브젝트의 Transform (운반시설 전용, 없으면 null)</param>
        /// <param name="warehouseTrans">창고 오브젝트의 Transform (운반시설 전용, 없으면 null)</param>
        public void SetFacility(BuildingType type,
                                Transform facilityTrans  = null,
                                Transform warehouseTrans = null)
        {
            facilityType      = type;
            facilityTransform = facilityTrans;
            warehouseTarget   = warehouseTrans;
            isWorking         = false;
        }

        /// <summary>
        /// FacilityStarted 이벤트 수신 시 FacilityEventBridge에서 호출.
        /// 시설이 가동되면 작업 애니메이션을 재생합니다.
        /// </summary>
        public void OnFacilityStarted(MemAI ai)
        {
            isWorking    = true;
            wanderTimer  = 0f;
            wanderInterval = Random.Range(3f, 7f);
            isHeadingToWarehouse = false;

            PlayWorkAnimation(ai);
            Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 시작 ({facilityType})");
        }

        /// <summary>
        /// FacilityStopped 이벤트 수신 시 FacilityEventBridge에서 호출.
        /// 중지 이유에 따라 다음 상태로 전환합니다.
        /// </summary>
        public void OnFacilityStopped(MemAI ai, FacilityStopReason reason)
        {
            isWorking = false;

            switch (reason)
            {
                case FacilityStopReason.Starvation:
                    // 기아 → HungryState 전환
                    Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 기아로 작업 중단 → HungryState");
                    ai.TransitionTo(ai.HungryState);
                    break;

                case FacilityStopReason.CompleteCrafting:
                    // 제작 완료(제작대 전용) → Idle 대기
                    Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 제작 완료 → Idle 대기");
                    ReturnToIdleAnim(ai);
                    break;

                case FacilityStopReason.CancelCrafting:
                    // 제작 취소(제작대 전용) → Idle 대기
                    Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 제작 취소 → Idle 대기");
                    ReturnToIdleAnim(ai);
                    break;
            }
        }

        // ---------------------------------------------------------------
        // IMemState 구현
        // ---------------------------------------------------------------

        public void Enter(MemAI ai)
        {
            // 배치 직후 FacilityStarted가 올 때까지 Idle로 대기
            isWorking    = false;
            wanderTimer  = 0f;
            wanderInterval = Random.Range(3f, 7f);
            isHeadingToWarehouse = false;

            ReturnToIdleAnim(ai);

            Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 배치 대기 ({facilityType})");
        }

        public void Update(MemAI ai)
        {
            if (!isWorking) return;

            switch (facilityType)
            {
                case BuildingType.Ranch:
                    UpdateRanchWander(ai);
                    break;

                case BuildingType.TransportFacility:
                    UpdateTransportMove(ai);
                    break;

                // 나머지 시설: 제자리 애니메이션 → Update에서 별도 처리 없음
                default:
                    break;
            }
        }

        public void Exit(MemAI ai)
        {
            isWorking = false;

            if (ai.Movement != null)
                ai.Movement.Stop();

            Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 상태 종료 ({facilityType})");
        }

        // ---------------------------------------------------------------
        // 애니메이션 헬퍼
        // ---------------------------------------------------------------

        /// <summary>시설별 작업 애니메이션 재생.</summary>
        private void PlayWorkAnimation(MemAI ai)
        {
            if (ai.Visual == null) return;

            switch (facilityType)
            {
                case BuildingType.Ranch:
                case BuildingType.TransportFacility:
                    // 이동 기반 시설: Walk 애니 (이동 시작은 Update에서)
                    ai.Visual.PlayWalk();
                    break;

                case BuildingType.Workshop:    // 제작대: 무언가를 만드는 동작
                case BuildingType.LoggingCamp: // 벌목장: 나무를 베는 동작
                case BuildingType.MiningCamp:  // 채굴장: 광석을 캐는 동작
                case BuildingType.Farm:        // 밭: 밭을 관리하는 동작
                case BuildingType.Generator:   // 발전기: 전기를 생산하는 동작
                default:
                    // [임시] Interact 애니 사용. 전용 리소스 추가 후 교체 예정.
                    ai.Visual.PlayInteract();
                    break;
            }
        }

        /// <summary>이동 정지 + Idle 애니메이션 복귀.</summary>
        private void ReturnToIdleAnim(MemAI ai)
        {
            if (ai.Movement != null)
                ai.Movement.Stop();

            if (ai.Visual != null)
                ai.Visual.PlayIdle();
        }

        // ---------------------------------------------------------------
        // 목장(Ranch): 시설 주변 자유 배회
        // ---------------------------------------------------------------

        private void UpdateRanchWander(MemAI ai)
        {
            wanderTimer += Time.deltaTime;

            bool arrived = ai.Movement != null && ai.Movement.HasReachedDestination();

            if (arrived || wanderTimer >= wanderInterval)
            {
                wanderTimer    = 0f;
                wanderInterval = Random.Range(3f, 7f);

                // MemMovement.Wander()는 내부적으로 wanderRadius 범위 내 랜덤 목적지 설정
                ai.Movement?.Wander();
                ai.Visual?.PlayWalk();
            }
        }

        // ---------------------------------------------------------------
        // 운반시설(TransportFacility): 시설 ↔ 창고 왕복
        // ---------------------------------------------------------------

        private void UpdateTransportMove(MemAI ai)
        {
            if (ai.Movement == null) return;

            Vector3 targetPos = isHeadingToWarehouse
                ? (warehouseTarget   != null ? warehouseTarget.position   : ai.Owner.transform.position)
                : (facilityTransform != null ? facilityTransform.position : ai.Owner.transform.position);

            float dist = Vector3.Distance(ai.Owner.transform.position, targetPos);

            if (dist <= ReachThreshold)
            {
                // 목적지 도착 → 방향 전환 후 다음 목적지로 이동
                isHeadingToWarehouse = !isHeadingToWarehouse;
                Vector3 nextTarget = isHeadingToWarehouse
                    ? (warehouseTarget   != null ? warehouseTarget.position   : ai.Owner.transform.position)
                    : (facilityTransform != null ? facilityTransform.position : ai.Owner.transform.position);

                MoveToPosition(ai, nextTarget);
            }
            else if (ai.Movement.HasReachedDestination())
            {
                // 경로가 끊긴 경우 재시작
                MoveToPosition(ai, targetPos);
            }
        }

        /// <summary>
        /// NavMeshAgent로 지정 위치로 이동 명령.
        /// MemMovement에는 MoveTo(Vector3)가 없으므로 임시 더미 Transform을 사용하지 않고,
        /// NavMesh.SetDestination을 직접 사용합니다.
        /// </summary>
        private void MoveToPosition(MemAI ai, Vector3 destination)
        {
            if (ai.Movement == null || ai.Owner == null) return;

            // NavMeshAgent에 직접 접근 (MemMovement가 RequireComponent로 보장)
            var agent = ai.Owner.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(destination);
            }

            ai.Visual?.PlayWalk();
        }
    }
}
