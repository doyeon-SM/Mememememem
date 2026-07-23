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
//   Generator         (발전기)   → PlayRun() (제자리 뛰기)
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
        // 제자리 작업 시설(제작대·밭·채굴장·발전기 등): 시설로 이동 후 작업
        // ---------------------------------------------------------------

        /// <summary>시설 근처 작업 지점에 도착했는지 여부.</summary>
        private bool arrivedAtWorkSpot = false;

        /// <summary>시설 근처의 실제 작업 지점(멤이 서서 작업할 위치).</summary>
        private Vector3 workSpot;

        /// <summary>시설로 이동 시작 후 경과 시간(도착 판정 실패 대비 타임아웃용).</summary>
        private float moveToFacilityTimer;

        private const float WorkArriveThreshold = 0.2f;  // 작업 지점 도착 판정 거리(칸 안까지 들어가도록 좁게)
        private const float MoveToFacilityTimeout = 4f;   // 도착 못해도 이 시간 후 작업 시작

        /// <summary>시설 칸 안까지 파고들도록 접근 시 정지 거리를 거의 0으로.</summary>
        private const float FacilityApproachStopDistance = 0.05f;

        /// <summary>시설 칸 중심에서 작업 지점을 흩뿌릴 최대 반경(1×1 칸을 벗어나지 않게 작게).</summary>
        private const float CellInsetRadius = 0.2f;

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

            // 이동 기반 시설(목장·운반)은 기존 로직 그대로 (Update에서 처리)
            if (facilityType == BuildingType.Ranch || facilityType == BuildingType.TransportFacility)
            {
                PlayWorkAnimation(ai);
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 시작 ({facilityType})");
                return;
            }

            // 제자리 작업 시설: 시설 칸(Area)으로 걸어 들어간 뒤 작업 애니메이션을 재생한다.
            if (facilityTransform != null && ai.Movement != null)
            {
                workSpot = ComputeWorkSpot();
                arrivedAtWorkSpot = false;
                moveToFacilityTimer = 0f;

                // 이 멤만 시설 칸(Area)을 밟을 수 있게 허용한 뒤 칸 중심으로 이동.
                // (순찰 멤은 이 Area가 areaMask에서 제외돼 있어 시설 칸을 통과하지 못함)
                ai.Movement.SetFacilityAreaAllowed(true);
                ai.Movement.MoveTo(workSpot, FacilityApproachStopDistance);
                ai.Visual?.PlayWalk();

                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 칸으로 이동 → 작업 예정 ({facilityType})");
            }
            else
            {
                // 시설 위치를 모르면 제자리에서 바로 작업 (기존 동작)
                arrivedAtWorkSpot = true;
                PlayWorkAnimation(ai);
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 시작 ({facilityType})");
            }
        }

        /// <summary>
        /// 시설이 설치된 "그리드 칸 내부"의 작업 지점을 계산합니다.
        /// 시설 메쉬는 무시하고(추후 교체 예정), 시설 pivot = 칸 중심(1×1 칸에 배치)이므로
        /// 그 중심 근처(칸을 벗어나지 않는 작은 오프셋)로 들어가 작업하게 합니다.
        /// 여러 멤이 같은 시설에 배치돼도 완전히 겹치지 않도록 소량 분산합니다.
        /// </summary>
        private Vector3 ComputeWorkSpot()
        {
            Vector3 cellCenter = facilityTransform.position;

            // 1×1 칸(반칸=0.5m) 안에 머무르도록 중심에서 살짝만 흩뿌린다.
            Vector2 offset = Random.insideUnitCircle * CellInsetRadius;

            return new Vector3(
                cellCenter.x + offset.x,
                cellCenter.y,
                cellCenter.z + offset.y);
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
            arrivedAtWorkSpot = false;

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

                // 제자리 작업 시설: 시설 칸으로 이동 중이면 도착 판정 후 작업 시작
                default:
                    UpdateMoveToFacility(ai);
                    break;
            }
        }

        /// <summary>시설 칸으로 걸어가는 중이면 도착을 감지해 작업 애니메이션을 시작합니다.</summary>
        private void UpdateMoveToFacility(MemAI ai)
        {
            if (arrivedAtWorkSpot) return; // 이미 작업 중

            moveToFacilityTimer += Time.deltaTime;

            float dist = Vector3.Distance(ai.transform.position, workSpot);
            bool reached = dist <= WorkArriveThreshold
                        || (ai.Movement != null && ai.Movement.HasReachedDestination())
                        || moveToFacilityTimer >= MoveToFacilityTimeout; // 막혀도 시작

            if (reached)
            {
                arrivedAtWorkSpot = true;
                ai.Movement?.Stop();
                if (facilityTransform != null)
                    ai.Movement?.LookAt(facilityTransform.position); // 시설을 바라보게
                PlayWorkAnimation(ai);

                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 도착 → 작업 시작 ({facilityType})");
            }
        }

        public void Exit(MemAI ai)
        {
            isWorking = false;

            if (ai.Movement != null)
            {
                // 시설 칸(Area) 위에 서 있었다면 일반 칸으로 옮긴 뒤, 시설 칸 진입 권한을 회수한다.
                // → 이 멤도 이제 순찰 멤처럼 시설 칸을 통과하지 못하게 되고, 정상 배회가 가능해진다.
                ai.Movement.WarpToNonFacilityArea();
                ai.Movement.SetFacilityAreaAllowed(false);
                ai.Movement.Stop();
            }

            Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 상태 종료 ({facilityType})");
        }

        // ---------------------------------------------------------------
        // 애니메이션 헬퍼
        // ---------------------------------------------------------------

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

                case BuildingType.Workshop:    // 제작대: 망치질
                    ai.Visual.PlayCraft();
                    break;
                case BuildingType.LoggingCamp: // 벌목장: 도끼질
                    ai.Visual.PlayChop();
                    break;
                case BuildingType.Farm:        // 밭: 낫질
                    ai.Visual.PlayFarm();
                    break;
                case BuildingType.Generator:   // 발전기: 런닝머신 달리기 (제자리 뛰기)
                    ai.Visual.PlayRun();
                    break;
                case BuildingType.MiningCamp:  // 채굴장: 곡괭이질
                    ai.Visual.PlayMine();
                    break;

                default:
                    // 요리 등 아직 추가 안된 시설들
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
