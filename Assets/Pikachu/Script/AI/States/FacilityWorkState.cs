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
// [작업 위치(MemPos)]
// - 영지 담당자가 시설 프리팹에 "MemPos*" 자식 오브젝트로 멤 작업 위치를 지정해 두었고,
//   시설 이벤트에 List<Transform>으로 함께 실려 옵니다.
// - FacilityEventBridge가 멤 1마리당 슬롯 하나를 배정해 SetFacility(..., workPos)로 넘겨줍니다.
// - 지정 위치가 있으면 그 지점으로 걸어가 작업하고, 없으면 기존처럼 시설 칸 중심에서 작업합니다.
//
// [작업 애니메이션 매핑]
//   제작대   → PlayCraft()    (망치)
//   벌목장   → PlayChop()     (도끼)
//   채석장   → PlayMine()     (곡괭이)
//   밭       → PlayFarm()     (낫)
//   주방     → PlayCook()     (팬)
//   모닥불   → PlayCook()     (팬)
//   발전기   → PlayRun()      (런닝머신 제자리 뛰기)
//   목장     → 지정 위치 주변 소폭 배회 (없으면 자유 배회) + PlayWalk()
//   운송시설 → 지정 위치 ↔ 창고 왕복 + PlayWalk()
//
// ⚠️ [BuildingType만으로는 시설을 구분할 수 없다]
// 영지의 BuildingType은 7종뿐인데 제작대·주방·모닥불의 BuildingData가 모두
// Workshop(0)으로 되어 있습니다. 그래서 BuildingType으로 애니메이션을 고르면
// 주방·모닥불에서도 망치질이 나옵니다.
// → FacilityEventBridge가 "이벤트를 보낸 시설 런타임 컴포넌트"를 보고 FacilityWorkAnim을
//   정해서 넘겨줍니다. 여기서는 받은 값을 그대로 재생만 합니다.
//   (넘어오지 않으면 Auto로 BuildingType 기반 추론 — 테스트 도구용 폴백)
// ============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace MemSystem.AI.States
{
    /// <summary>
    /// 시설에서 재생할 작업 애니메이션 종류.
    /// 영지의 BuildingType으로는 제작대·주방·모닥불이 구분되지 않아(모두 Workshop),
    /// 시설 런타임 컴포넌트를 보고 결정한 값을 FacilityWorkState에 주입합니다.
    /// </summary>
    public enum FacilityWorkAnim
    {
        /// <summary>지정 없음 — BuildingType으로 추론합니다.</summary>
        Auto = 0,
        /// <summary>제작대 — 망치질.</summary>
        Craft,
        /// <summary>벌목장 — 도끼질.</summary>
        Chop,
        /// <summary>채석장 — 곡괭이질.</summary>
        Mine,
        /// <summary>밭 — 낫질.</summary>
        Farm,
        /// <summary>주방·모닥불 — 요리.</summary>
        Cook,
        /// <summary>발전기 — 런닝머신 제자리 뛰기.</summary>
        Run,
        /// <summary>목장·운송 — 이동 기반이라 걷기.</summary>
        Move,
    }


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

        /// <summary>배치된 시설 종류. (이동/왕복 등 "행동"을 가르는 데 사용)</summary>
        private BuildingType facilityType;

        /// <summary>재생할 작업 애니메이션. Auto면 facilityType으로 추론합니다.</summary>
        private FacilityWorkAnim workAnim = FacilityWorkAnim.Auto;

        /// <summary>시설이 현재 가동 중인지 여부.</summary>
        private bool isWorking = false;

        // ---------------------------------------------------------------
        // 목장(Ranch) 배회 관련
        // ---------------------------------------------------------------

        private float wanderTimer    = 0f;
        private float wanderInterval = 5f;

        /// <summary>지정 작업 위치가 있을 때, 그 위치를 중심으로 배회할 반경(목장 전용).</summary>
        private const float AnchoredWanderRadius = 1.5f;

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

        /// <summary>workSpot이 계산되어 있고 그리로 이동 중/도착한 상태인지 여부.</summary>
        private bool hasWorkSpot = false;

        /// <summary>시설 근처의 실제 작업 지점(멤이 서서 작업할 위치).</summary>
        private Vector3 workSpot;

        /// <summary>
        /// 영지 담당자가 시설 프리팹에 지정한 이 멤 전용 작업 위치(MemPos).
        /// FacilityEventBridge가 시설 이벤트의 List&lt;Transform&gt;에서 하나를 배정해 넘겨줍니다.
        /// null이면 시설 칸 중심에서 작업합니다.
        /// </summary>
        private Transform workPosition;

        /// <summary>시설로 이동 시작 후 경과 시간(도착 판정 실패 대비 타임아웃용).</summary>
        /// <summary>작업 위치로 가는 동안 "가까워지지 않은" 시간(초).</summary>
        private float approachStuckTimer;

        /// <summary>지금까지 기록한 작업 위치까지의 최단 거리. 이보다 줄어들면 진전으로 본다.</summary>
        private float lastApproachDistance;

        /// <summary>이번 접근에서 이동 명령을 다시 보냈는지.</summary>
        private bool retriedApproach;

        private const float WorkArriveThreshold = 0.2f;  // 작업 지점 도착 판정 거리(칸 안까지 들어가도록 좁게)

        // [도착 판정] 예전엔 "4초 지나면 도착으로 간주"였는데, 걷기 속도가 0.8m/s라 4초면 3.2m밖에
        // 못 갑니다. 시설을 옮기는 등 먼 거리를 걸어갈 때 도중에 타임아웃이 터져 엉뚱한 자리에서
        // 작업을 시작했습니다. → 시간이 아니라 "가까워지고 있는가"로 판정합니다.
        private const float ApproachProgressEpsilon = 0.05f; // 이만큼 줄면 진전으로 인정
        private const float ApproachRetrySeconds    = 1.5f;  // 진전이 없으면 이동 명령을 한 번 재전송
        private const float ApproachGiveUpSeconds   = 4f;    // 그래도 못 가면 그 자리에서 작업 시작

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
        /// <param name="facilityTrans">시설 오브젝트의 Transform (없으면 null)</param>
        /// <param name="warehouseTrans">창고 오브젝트의 Transform (운반시설 전용, 없으면 null)</param>
        /// <param name="workPos">영지 담당자가 지정한 작업 위치(MemPos). 없으면 시설 칸 중심에서 작업.</param>
        /// <param name="anim">
        /// 재생할 작업 애니메이션. Auto면 BuildingType으로 추론합니다.
        /// BuildingType은 제작대·주방·모닥불을 구분하지 못하므로, 실제 운영에서는
        /// FacilityEventBridge가 시설 런타임을 보고 명시적으로 넘겨줍니다.
        /// </param>
        public void SetFacility(BuildingType type,
                                Transform facilityTrans  = null,
                                Transform warehouseTrans = null,
                                Transform workPos        = null,
                                FacilityWorkAnim anim    = FacilityWorkAnim.Auto)
        {
            facilityType      = type;
            facilityTransform = facilityTrans;
            warehouseTarget   = warehouseTrans;
            workPosition      = workPos;
            workAnim          = anim;
            isWorking         = false;

            // 시설/작업 위치가 바뀌었으므로 이전 위치 기준 도착 판정을 버린다.
            hasWorkSpot       = false;
            arrivedAtWorkSpot = false;
        }

        /// <summary>
        /// 지정 작업 위치(없으면 시설 칸 중심)로 걸어가기 시작합니다.
        /// 배치 직후 대기할 때와 가동이 시작될 때 모두 이 경로를 씁니다.
        /// 갈 곳을 모르면 hasWorkSpot이 false로 남습니다.
        /// </summary>
        private void BeginMoveToWorkSpot(MemAI ai)
        {
            hasWorkSpot       = false;
            arrivedAtWorkSpot = false;

            if ((workPosition == null && facilityTransform == null) || ai.Movement == null) return;

            workSpot             = ComputeWorkSpot();
            hasWorkSpot          = true;
            approachStuckTimer   = 0f;
            lastApproachDistance = float.MaxValue;
            retriedApproach      = false;

            // 이 멤만 시설 칸의 이동 비용을 정상화해 칸 안으로 곧장 들어가게 한다.
            // (순찰 멤은 시설 칸 비용이 높아 우회한다)
            ai.Movement.SetFacilityAreaAllowed(true);
            ai.Movement.MoveTo(workSpot, FacilityApproachStopDistance);
            ai.Visual?.PlayWalk();
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

            // 운반시설: 시설(지정 위치) ↔ 창고 왕복 (Update에서 처리)
            if (facilityType == BuildingType.TransportFacility)
            {
                PlayWorkAnimation(ai);
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 시작 ({facilityType})");
                return;
            }

            // 목장에 지정 위치가 없으면 기존처럼 시설 주변 자유 배회 (Update에서 처리)
            if (facilityType == BuildingType.Ranch && workPosition == null)
            {
                PlayWorkAnimation(ai);
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 시작 ({facilityType})");
                return;
            }

            // 배치 직후 이미 자기 자리에 걸어와 대기 중이었다면, 그 자리에서 바로 작업을 시작한다.
            if (hasWorkSpot && arrivedAtWorkSpot)
            {
                PlayWorkAnimation(ai);
                wanderTimer = wanderInterval; // 목장은 즉시 주변 배회 시작
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 자리에서 작업 시작 ({facilityType})");
                return;
            }

            // 그 외: 지정 작업 위치(MemPos)로, 없으면 시설 칸(Area) 중심으로 걸어간 뒤 작업한다.
            BeginMoveToWorkSpot(ai);

            if (hasWorkSpot)
            {
                string where = workPosition != null ? $"지정 위치 '{workPosition.name}'" : "시설 칸";
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} {where}(으)로 이동 → 작업 예정 ({facilityType})");
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
        /// 멤이 서서 작업할 지점을 계산합니다.
        ///
        /// 1) 영지 담당자가 지정한 작업 위치(MemPos)가 배정돼 있으면 그 지점을 그대로 사용합니다.
        ///    (멤마다 서로 다른 슬롯이 배정되므로 추가 분산이 필요 없습니다)
        /// 2) 지정 위치가 없으면 기존처럼 시설이 설치된 "그리드 칸 내부"를 사용합니다.
        ///    시설 pivot = 칸 중심(1×1 칸에 배치)이므로 그 중심 근처(칸을 벗어나지 않는 작은 오프셋)로
        ///    들어가 작업하게 하고, 여러 멤이 겹치지 않도록 소량 분산합니다.
        /// </summary>
        private Vector3 ComputeWorkSpot()
        {
            if (workPosition != null)
                return workPosition.position;

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
            isWorking    = false;
            wanderTimer  = 0f;
            wanderInterval = Random.Range(3f, 7f);
            isHeadingToWarehouse = false;

            // 배치되면 곧바로 자기 자리로 걸어가서 대기한다.
            // (가동은 FacilityStarted가 와야 시작. 제자리에 굳어 있으면 배치가 안 된 것처럼 보인다)
            BeginMoveToWorkSpot(ai);

            if (!hasWorkSpot) ReturnToIdleAnim(ai);

            Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 시설 배치 — 자리로 이동 후 가동 대기 ({facilityType})");
        }

        public void Update(MemAI ai)
        {
            // 가동 대기 중이라도 자기 자리까지는 걸어간다. 도착하면 Idle로 대기.
            if (!isWorking)
            {
                if (hasWorkSpot && !arrivedAtWorkSpot) UpdateApproach(ai, playWorkAnimOnArrive: false);
                return;
            }

            switch (facilityType)
            {
                case BuildingType.Ranch:
                    // 지정 위치가 없으면 기존처럼 자유 배회
                    if (workPosition == null)
                    {
                        UpdateRanchWander(ai);
                    }
                    else if (!arrivedAtWorkSpot)
                    {
                        UpdateApproach(ai, playWorkAnimOnArrive: true);   // 지정 위치까지 먼저 이동
                    }
                    else
                    {
                        UpdateAnchoredWander(ai);   // 지정 위치 주변에서만 소폭 배회
                    }
                    break;

                case BuildingType.TransportFacility:
                    UpdateTransportMove(ai);
                    break;

                // 제자리 작업 시설: 작업 위치로 이동 중이면 도착 판정 후 작업 시작
                default:
                    UpdateApproach(ai, playWorkAnimOnArrive: true);
                    break;
            }
        }

        /// <summary>작업 위치로 걸어가는 중이면 도착을 감지합니다.</summary>
        /// <param name="playWorkAnimOnArrive">
        /// true면 도착 시 작업 애니메이션을 재생(가동 중), false면 Idle로 대기(가동 대기 중).
        /// </param>
        private void UpdateApproach(MemAI ai, bool playWorkAnimOnArrive)
        {
            if (arrivedAtWorkSpot) return; // 이미 도착

            float dist = Vector3.Distance(ai.transform.position, workSpot);

            bool reached = dist <= WorkArriveThreshold
                        || (ai.Movement != null && ai.Movement.HasReachedDestination());

            if (!reached)
            {
                // 가까워지고 있으면 얼마가 걸리든 계속 걷게 둔다. (영지가 넓어 이동이 오래 걸릴 수 있음)
                if (dist < lastApproachDistance - ApproachProgressEpsilon)
                {
                    lastApproachDistance = dist;
                    approachStuckTimer   = 0f;
                    return;
                }

                approachStuckTimer += Time.deltaTime;

                // 이동 명령이 먹지 않았을 수 있으니 한 번 다시 보낸다.
                if (!retriedApproach && approachStuckTimer >= ApproachRetrySeconds)
                {
                    retriedApproach = true;

                    ai.Movement?.SetFacilityAreaAllowed(true);
                    ai.Movement?.MoveTo(workSpot, FacilityApproachStopDistance);
                    ai.Visual?.PlayWalk();

                    Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 이동이 멈춰 작업 위치로 다시 보냅니다 (남은 거리 {dist:0.0}m).");
                    return;
                }

                if (approachStuckTimer < ApproachGiveUpSeconds) return;

                Debug.LogWarning($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 위치까지 가지 못했습니다 " +
                                 $"(남은 거리 {dist:0.0}m). 그 자리에서 작업을 시작합니다 — NavMesh 연결을 확인하세요.");
            }

            arrivedAtWorkSpot = true;
            ai.Movement?.Stop();
            if (facilityTransform != null)
                ai.Movement?.LookAt(facilityTransform.position); // 시설을 바라보게

            if (playWorkAnimOnArrive)
            {
                PlayWorkAnimation(ai);

                // 목장은 도착 직후 바로 주변 배회를 시작하도록 타이머를 만료시켜 둔다.
                wanderTimer = wanderInterval;

                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 위치 도착 → 작업 시작 ({facilityType})");
            }
            else
            {
                ai.Visual?.PlayIdle();
                Debug.Log($"[FacilityWorkState] {ai.Owner?.Stats?.MemName} 작업 위치 도착 → 가동 대기 ({facilityType})");
            }
        }

        public void Exit(MemAI ai)
        {
            isWorking = false;

            if (ai.Movement != null)
            {
                // 시설 칸 진입 권한(=이동 비용 정상화)을 회수한다.
                // → 이 멤도 이제 순찰 멤처럼 시설 칸을 우회하며, 다음 배회부터 정상 동작한다.
                //   (칸 위에 서 있어도 밀어내지 않는다. 억지로 옮기면 스윽 미끄러져 보인다)
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

            switch (ResolveWorkAnim())
            {
                case FacilityWorkAnim.Craft:    ai.Visual.PlayCraft();    break; // 제작대: 망치
                case FacilityWorkAnim.Chop:     ai.Visual.PlayChop();     break; // 벌목장: 도끼
                case FacilityWorkAnim.Mine:     ai.Visual.PlayMine();     break; // 채석장: 곡괭이
                case FacilityWorkAnim.Farm:     ai.Visual.PlayFarm();     break; // 밭: 낫
                case FacilityWorkAnim.Cook:     ai.Visual.PlayCook();     break; // 주방·모닥불: 팬
                case FacilityWorkAnim.Run:      ai.Visual.PlayRun();      break; // 발전기: 런닝머신 제자리 뛰기
                case FacilityWorkAnim.Move:     ai.Visual.PlayWalk();     break; // 목장·운송: 걷기

                default:
                    ai.Visual.PlayInteract();
                    break;
            }
        }

        /// <summary>
        /// 재생할 애니메이션을 결정합니다.
        /// 브릿지가 명시적으로 지정했으면 그것을 쓰고, Auto면 BuildingType으로 추론합니다.
        /// (추론은 제작대·주방·모닥불을 구분하지 못하므로 테스트 도구용 폴백입니다)
        /// </summary>
        private FacilityWorkAnim ResolveWorkAnim()
        {
            if (workAnim != FacilityWorkAnim.Auto) return workAnim;

            switch (facilityType)
            {
                case BuildingType.LoggingCamp:       return FacilityWorkAnim.Chop;
                case BuildingType.MiningCamp:        return FacilityWorkAnim.Mine;
                case BuildingType.Farm:              return FacilityWorkAnim.Farm;
                case BuildingType.Generator:         return FacilityWorkAnim.Run;
                case BuildingType.Ranch:             return FacilityWorkAnim.Move;
                case BuildingType.TransportFacility: return FacilityWorkAnim.Move;
                default:                             return FacilityWorkAnim.Craft; // Workshop 등
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

        /// <summary>
        /// 지정 작업 위치(workSpot)를 중심으로 좁은 반경 안에서만 배회합니다. (목장 전용)
        /// 시설 밖으로 나가지 않으면서도 가만히 서 있지 않게 합니다.
        /// </summary>
        private void UpdateAnchoredWander(MemAI ai)
        {
            wanderTimer += Time.deltaTime;

            bool arrived = ai.Movement != null && ai.Movement.HasReachedDestination();

            if (!arrived && wanderTimer < wanderInterval) return;

            wanderTimer    = 0f;
            wanderInterval = Random.Range(3f, 7f);

            Vector2 offset = Random.insideUnitCircle * AnchoredWanderRadius;
            Vector3 destination = new Vector3(
                workSpot.x + offset.x,
                workSpot.y,
                workSpot.z + offset.y);

            ai.Movement?.MoveTo(destination);
            ai.Visual?.PlayWalk();
        }

        // ---------------------------------------------------------------
        // 운반시설(TransportFacility): 시설 ↔ 창고 왕복
        // ---------------------------------------------------------------

        private void UpdateTransportMove(MemAI ai)
        {
            if (ai.Movement == null) return;

            Vector3 targetPos = isHeadingToWarehouse
                ? (warehouseTarget != null ? warehouseTarget.position : ai.Owner.transform.position)
                : GetFacilitySideEndpoint(ai);

            float dist = Vector3.Distance(ai.Owner.transform.position, targetPos);

            if (dist <= ReachThreshold)
            {
                // 목적지 도착 → 방향 전환 후 다음 목적지로 이동
                isHeadingToWarehouse = !isHeadingToWarehouse;
                Vector3 nextTarget = isHeadingToWarehouse
                    ? (warehouseTarget != null ? warehouseTarget.position : ai.Owner.transform.position)
                    : GetFacilitySideEndpoint(ai);

                MoveToPosition(ai, nextTarget);
            }
            else if (ai.Movement.HasReachedDestination())
            {
                // 경로가 끊긴 경우 재시작
                MoveToPosition(ai, targetPos);
            }
        }

        /// <summary>
        /// 운반 왕복의 시설 쪽 도착 지점.
        /// 지정 작업 위치(MemPos)가 배정돼 있으면 그 지점, 없으면 시설 중심을 사용합니다.
        /// </summary>
        private Vector3 GetFacilitySideEndpoint(MemAI ai)
        {
            if (workPosition      != null) return workPosition.position;
            if (facilityTransform != null) return facilityTransform.position;
            return ai.Owner.transform.position;
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
