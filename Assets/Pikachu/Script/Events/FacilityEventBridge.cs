// ============================================================================
// FacilityEventBridge.cs
// 영지 생산 시설 이벤트 → 멤 AI 연결 브릿지
//
// [역할]
// - 영지 생산 시설 7종의 static 이벤트를 구독합니다.
// - 이벤트 수신 시 씬에서 활성화된 Mem 인스턴스를 MemData로 찾아 AI 상태를 전환합니다.
// - 이벤트로 함께 넘어오는 작업 위치 목록(List<Transform>, 시설 프리팹의 "MemPos*")에서
//   멤 1마리당 슬롯 하나를 배정해, 그 지점으로 이동해 작업하게 합니다.
//
// [씬 배치]
// - 영지(Territory) 씬에 빈 GameObject를 하나 만들고 이 컴포넌트를 붙이세요.
// - [선택] warehouseTransform: 운반시설(TransportFacility)의 왕복 창고 위치.
//   없으면 운반 멤은 시설 근처를 배회합니다.
//
// [이벤트 흐름]
//   MemAdded(type, data, true,  positions) → 멤 배치 → 슬롯 배정 → FacilityWorkState 진입 (Idle 대기)
//   FacilityStarted(type, list, positions) → 시설 가동 → 지정 위치로 이동 후 작업 애니 재생
//   FacilityStopped(type, list, r, pos)    → 시설 중지 → 이유별 상태 전환
//   MemAdded(type, data, false, positions) → 멤 해제 → 슬롯 반납 → IdleState 복귀
//
// [영지 시설 이벤트 시그니처 - 영지 담당자 제공]
//   public static event Action<BuildingType, MemData, bool, List<Transform>>                     MemAdded;
//   public static event Action<BuildingType, List<MemData>, List<Transform>>                     FacilityStarted;
//   public static event Action<BuildingType, List<MemData>, FacilityStopReason, List<Transform>> FacilityStopped;
//
// [시설 7종]
//   CampFireRuntime(모닥불) / GeneratorRuntime(발전기) / KitchenRuntime(주방) /
//   ProductionCraftRuntime(제작대) / ProductionFacilityRuntime(벌목장·채석장) /
//   RanchFacilityRuntime(목장) / TransportRuntime(운송)
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using MemSystem.AI;
using MemSystem.AI.States;
using MemSystem.Core;
using MemSystem.Data;

/// <summary>
/// 영지 생산 시설의 이벤트를 수신하여 멤 AI를 전환하는 브릿지 컴포넌트.
/// 영지(Territory) 씬의 빈 오브젝트에 단 하나만 배치하세요.
/// </summary>
public class FacilityEventBridge : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector 설정
    // ---------------------------------------------------------------

    [Header("운반시설(TransportFacility) 전용")]
    [Tooltip("운반 멤이 왕복할 창고 오브젝트의 Transform. 없으면 시설 주변 배회.")]
    [SerializeField] private Transform warehouseTransform;

    [Header("배치 처리")]
    [Tooltip("아직 영지에 소환되지 않은(창고에만 있는) 멤이 시설에 배치되면 그 자리에서 소환합니다. " +
             "끄면 이미 영지를 돌아다니던 멤만 시설 근무로 전환됩니다.")]
    [SerializeField] private bool autoSpawnMissingWorker = true;

    [Tooltip("멤을 소환할 스포너. 비어 있으면 씬에서 자동 탐색합니다.")]
    [SerializeField] private TerritoryWanderSpawner wanderSpawner;

    [Tooltip("시설의 실제 가동 상태를 주기적으로 확인해 멤의 작업 상태를 맞춥니다. " +
             "가동 이벤트가 오지 않는 경로(외부 시스템이 시설 상태를 직접 바꾸는 경우 등)를 보정합니다.")]
    [SerializeField] private bool reconcileWorkState = true;

    [Header("디버그")]
    [Tooltip("배치/가동 흐름을 콘솔에 자세히 남깁니다. 문제 추적이 끝나면 꺼두세요. " +
             "끄더라도 실제 오류(경고)는 계속 출력됩니다.")]
    [SerializeField] private bool verboseLogging = true;

    [Tooltip("화면 좌상단에 배치 멤 목록을 표시합니다. (에디터 전용)")]
    [SerializeField] private bool showDebugOverlay = false;

    // ---------------------------------------------------------------
    // 내부 레지스트리
    // ---------------------------------------------------------------

    /// <summary>
    /// 시설에 배치된 멤의 레지스트리.
    /// Key: MemData.memId, Value: 씬의 Mem 인스턴스
    /// </summary>
    private readonly Dictionary<string, Mem> facilityMemRegistry = new Dictionary<string, Mem>();

    /// <summary>멤이 배정받은 작업 위치. Key: MemData.memId, Value: MemPos Transform</summary>
    private readonly Dictionary<string, Transform> memWorkSlots = new Dictionary<string, Transform>();

    /// <summary>작업 위치의 현재 점유자. Key: MemPos Transform, Value: MemData.memId</summary>
    private readonly Dictionary<Transform, string> slotOccupants = new Dictionary<Transform, string>();

    /// <summary>배치된 멤이 어느 시설에 속했고 지금 작업 중인지. 가동 상태 보정에 사용합니다.</summary>
    private class WorkContext
    {
        public BuildingType BuildingType;
        public Transform    Facility;
        public Transform    WorkSlot;
        /// <summary>우리가 이 멤에게 "작업 시작"을 지시한 상태인지.</summary>
        public bool         Working;

        /// <summary>가동 없이 대기한 시간(초). 진단 로그를 한 번만 남기기 위해 사용.</summary>
        public float        WaitingSeconds;
        public bool         WaitDiagnosed;
    }

    /// <summary>배치 멤별 시설 컨텍스트. Key: MemData.memId</summary>
    private readonly Dictionary<string, WorkContext> workContexts = new Dictionary<string, WorkContext>();

    /// <summary>가동 상태 보정 주기(초)와 타이머.</summary>
    private const float ReconcileInterval = 0.5f;
    private float reconcileTimer;

    /// <summary>이 시간(초) 넘게 가동 없이 대기하면 시설의 실제 값을 한 번 로그로 남깁니다.</summary>
    private const float WaitDiagnoseAfterSeconds = 3f;

    /// <summary>정리 대상 memId를 모으는 재사용 버퍼. (순회 중 Dictionary 수정 방지)</summary>
    private readonly List<string> staleMemIds = new List<string>();

    /// <summary>흐름 추적용 로그. verboseLogging이 꺼져 있으면 남기지 않습니다.</summary>
    private void LogVerbose(string message)
    {
        if (verboseLogging) Debug.Log(message);
    }

    // ---------------------------------------------------------------
    // Unity 이벤트 구독/해제
    // ---------------------------------------------------------------

    private void OnEnable()
    {
        // 벌목장 · 채석장 등 일반 생산 시설
        ProductionFacilityRuntime.MemAdded        += OnMemAdded;
        ProductionFacilityRuntime.FacilityStarted += OnFacilityStarted;
        ProductionFacilityRuntime.FacilityStopped += OnFacilityStopped;

        // 제작대
        ProductionCraftRuntime.MemAdded           += OnMemAdded;
        ProductionCraftRuntime.FacilityStarted    += OnFacilityStarted;
        ProductionCraftRuntime.FacilityStopped    += OnFacilityStopped;

        // 주방
        KitchenRuntime.MemAdded                   += OnMemAdded;
        KitchenRuntime.FacilityStarted            += OnFacilityStarted;
        KitchenRuntime.FacilityStopped            += OnFacilityStopped;

        // 모닥불
        CampFireRuntime.MemAdded                  += OnMemAdded;
        CampFireRuntime.FacilityStarted           += OnFacilityStarted;
        CampFireRuntime.FacilityStopped           += OnFacilityStopped;

        // 발전기
        GeneratorRuntime.MemAdded                 += OnMemAdded;
        GeneratorRuntime.FacilityStarted          += OnFacilityStarted;
        GeneratorRuntime.FacilityStopped          += OnFacilityStopped;

        // 목장
        RanchFacilityRuntime.MemAdded             += OnMemAdded;
        RanchFacilityRuntime.FacilityStarted      += OnFacilityStarted;
        RanchFacilityRuntime.FacilityStopped      += OnFacilityStopped;

        // 운송 시설
        TransportRuntime.MemAdded                 += OnMemAdded;
        TransportRuntime.FacilityStarted          += OnFacilityStarted;
        TransportRuntime.FacilityStopped          += OnFacilityStopped;

        Debug.Log($"[FacilityEventBridge] 이벤트 구독 완료. (시설 7종) 가동 상태 보정={reconcileWorkState}");
    }

    private void OnDisable()
    {
        ProductionFacilityRuntime.MemAdded        -= OnMemAdded;
        ProductionFacilityRuntime.FacilityStarted -= OnFacilityStarted;
        ProductionFacilityRuntime.FacilityStopped -= OnFacilityStopped;

        ProductionCraftRuntime.MemAdded           -= OnMemAdded;
        ProductionCraftRuntime.FacilityStarted    -= OnFacilityStarted;
        ProductionCraftRuntime.FacilityStopped    -= OnFacilityStopped;

        KitchenRuntime.MemAdded                   -= OnMemAdded;
        KitchenRuntime.FacilityStarted            -= OnFacilityStarted;
        KitchenRuntime.FacilityStopped            -= OnFacilityStopped;

        CampFireRuntime.MemAdded                  -= OnMemAdded;
        CampFireRuntime.FacilityStarted           -= OnFacilityStarted;
        CampFireRuntime.FacilityStopped           -= OnFacilityStopped;

        GeneratorRuntime.MemAdded                 -= OnMemAdded;
        GeneratorRuntime.FacilityStarted          -= OnFacilityStarted;
        GeneratorRuntime.FacilityStopped          -= OnFacilityStopped;

        RanchFacilityRuntime.MemAdded             -= OnMemAdded;
        RanchFacilityRuntime.FacilityStarted      -= OnFacilityStarted;
        RanchFacilityRuntime.FacilityStopped      -= OnFacilityStopped;

        TransportRuntime.MemAdded                 -= OnMemAdded;
        TransportRuntime.FacilityStarted          -= OnFacilityStarted;
        TransportRuntime.FacilityStopped          -= OnFacilityStopped;

        Debug.Log("[FacilityEventBridge] 이벤트 구독 해제 완료.");
    }

    // ---------------------------------------------------------------
    // 가동 상태 보정 (이벤트 유실 방어)
    // ---------------------------------------------------------------

    private void Update()
    {
        if (!reconcileWorkState) return;

        reconcileTimer += Time.deltaTime;
        if (reconcileTimer < ReconcileInterval) return;
        reconcileTimer = 0f;

        ReconcileWorkStates();
    }

    /// <summary>
    /// 배치된 멤의 작업 상태를 시설의 실제 가동 상태에 맞춥니다.
    ///
    /// 시설 가동은 여러 경로로 시작/정지됩니다(배치, 제작 지시, 아사 복구, 전력 등).
    /// 그중 하나라도 이벤트를 놓치면 멤이 자리에 선 채로 영영 대기하게 되므로,
    /// 주기적으로 실제 플래그를 확인해 어긋난 것만 바로잡습니다.
    ///
    /// 시설 근무 상태(FacilityWorkState)인 멤만 대상으로 합니다. 허기·전투 등 다른 사정으로
    /// 시설을 떠난 멤을 억지로 끌어오지 않기 위함입니다.
    /// </summary>
    private void ReconcileWorkStates()
    {
        staleMemIds.Clear();

        foreach (var pair in workContexts)
        {
            WorkContext ctx = pair.Value;

            facilityMemRegistry.TryGetValue(pair.Key, out Mem mem);

            // 멤이나 시설이 사라졌으면(디스폰·철거) 관리 대상에서 뺀다.
            // 그대로 두면 죽은 항목을 계속 검사하며 슬롯도 점유된 채로 남는다.
            if (mem == null || ctx.Facility == null)
            {
                staleMemIds.Add(pair.Key);
                continue;
            }

            MemAI ai         = mem.AI;
            bool inWorkState = ai != null && ai.CurrentState == ai.FacilityWorkState;
            bool producing   = IsFacilityProducing(ctx.BuildingType, ctx.Facility);

            // 진단: 가동이 안 되거나 근무 상태가 아니면 대기 시간을 누적하고, 한 번만 상태를 찍는다.
            // (어떤 이유로 걸러지든 반드시 남도록 보정 로직보다 먼저 수행한다)
            if (!producing || !inWorkState)
            {
                ctx.WaitingSeconds += ReconcileInterval;

                if (!ctx.WaitDiagnosed && ctx.WaitingSeconds >= WaitDiagnoseAfterSeconds)
                {
                    ctx.WaitDiagnosed = true;

                    string memName   = mem != null ? (mem.Stats?.MemName ?? pair.Key) : "(씬에서 사라짐)";
                    string stateName = ai?.CurrentState != null ? ai.CurrentState.GetType().Name : "(AI/상태 없음)";

                    Debug.LogWarning(
                        $"[FacilityEventBridge] '{memName}'이(가) {ctx.WaitingSeconds:0}초째 가동 대기 중입니다.\n" +
                        $"  멤 상태: {stateName} (시설 근무 상태인가={inWorkState})\n" +
                        $"  시설 가동중={producing}, {DescribeFacilityState(ctx.Facility)}\n" +
                        $"  음식 부족으로 정지: {(ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation.ToString() : "ConsumeFoodSystem 없음")}");
                }
            }
            else
            {
                ctx.WaitingSeconds = 0f;
                ctx.WaitDiagnosed  = false;
            }

            // 여기서부터는 실제 보정. 근무 상태인 멤만 건드린다.
            // (허기·전투 등 다른 사정으로 시설을 떠난 멤을 억지로 끌어오지 않기 위함)
            if (!inWorkState) continue;

            if (producing && !ctx.Working)
            {
                ai.FacilityWorkState.OnFacilityStarted(ai);
                ctx.Working        = true;
                ctx.WaitingSeconds = 0f;
                ctx.WaitDiagnosed  = false;

                LogVerbose($"[FacilityEventBridge] 가동 감지(이벤트 누락 보정) → '{mem.Stats?.MemName}' 작업 시작 ({ctx.BuildingType}).");
            }
            else if (!producing && ctx.Working)
            {
                // 정지 사유를 모르므로 자리를 지킨 채 대기 상태로만 되돌린다.
                ai.FacilityWorkState.OnFacilityStopped(ai, FacilityStopReason.CompleteCrafting);
                ctx.Working = false;

                LogVerbose($"[FacilityEventBridge] 정지 감지(이벤트 누락 보정) → '{mem.Stats?.MemName}' 가동 대기 ({ctx.BuildingType}).");
            }
        }

        // 순회가 끝난 뒤에 정리한다. (순회 중 Dictionary 수정 금지)
        foreach (string memId in staleMemIds)
        {
            ReleaseWorkSlot(memId);
            workContexts.Remove(memId);
            facilityMemRegistry.Remove(memId);

            LogVerbose($"[FacilityEventBridge] 사라진 배치 항목 정리: {memId}");
        }
    }

    /// <summary>시설의 가동 관련 실제 값을 사람이 읽을 수 있게 요약합니다. (대기 원인 진단용)</summary>
    private string DescribeFacilityState(Transform facilityTrans)
    {
        if (facilityTrans == null) return "시설 오브젝트를 찾지 못함";

        if (facilityTrans.TryGetComponent(out ProductionFacilityRuntime production))
            return $"{facilityTrans.name}(생산시설) isProducing={production.isProducing}, " +
                   $"craftingItem='{production.craftingItem}', 배치멤={production.DeployedMems.Count}";

        if (facilityTrans.TryGetComponent(out ProductionCraftRuntime craft))
            return $"{facilityTrans.name}(제작대) isProducing={craft.isProducing}, " +
                   $"currentCraftingItem='{craft.currentCraftingItem}', 배치멤={craft.DeployedMems.Count}";

        if (facilityTrans.TryGetComponent(out KitchenRuntime kitchen))
            return $"{facilityTrans.name}(주방) isCooking={kitchen.isCooking}, isPowerPaused={kitchen.isPowerPaused}, " +
                   $"currentCookingItem='{kitchen.currentCookingItem}', 배치멤={kitchen.DeployedMems.Count}";

        if (facilityTrans.TryGetComponent(out CampFireRuntime campFire))
            return $"{facilityTrans.name}(모닥불) isCooking={campFire.isCooking}, " +
                   $"currentCookingItem='{campFire.currentCookingItem}', 배치멤={campFire.DeployedMems.Count}";

        if (facilityTrans.TryGetComponent(out GeneratorRuntime generator))
            return $"{facilityTrans.name}(발전기) isPowerGenerating={generator.isPowerGenerating}, 배치멤={generator.DeployedMems.Count}";

        if (facilityTrans.TryGetComponent(out RanchFacilityRuntime ranch))
            return $"{facilityTrans.name}(목장) isProducing={ranch.isProducing}";

        if (facilityTrans.TryGetComponent(out TransportRuntime transport))
            return $"{facilityTrans.name}(운송) isWorking={transport.isWorking}, 배치멤={transport.DeployedMems.Count}";

        return $"{facilityTrans.name}: 시설 런타임 컴포넌트를 찾지 못함";
    }

    /// <summary>배치 멤의 시설 컨텍스트를 갱신합니다.</summary>
    private void SetWorkContext(string memId, BuildingType buildingType, Transform facility, Transform workSlot)
    {
        if (!workContexts.TryGetValue(memId, out WorkContext ctx))
        {
            ctx = new WorkContext();
            workContexts[memId] = ctx;
        }

        ctx.BuildingType = buildingType;
        ctx.Facility     = facility;
        ctx.WorkSlot     = workSlot;
    }

    /// <summary>이 멤의 "작업 중" 표시를 갱신합니다. (없으면 무시)</summary>
    private void MarkWorking(string memId, bool working)
    {
        if (workContexts.TryGetValue(memId, out WorkContext ctx)) ctx.Working = working;
    }

    // ---------------------------------------------------------------
    // 이벤트 핸들러
    // ---------------------------------------------------------------

    /// <summary>
    /// 멤 배치(true) / 해제(false) 이벤트 처리.
    /// 영지 담당자 안내: 작업 위치 목록은 배치(true)일 때만 사용합니다.
    /// </summary>
    private void OnMemAdded(BuildingType buildingType, MemData memData, bool isAdded, List<Transform> memPositions)
    {
        if (memData == null)
        {
            Debug.LogWarning("[FacilityEventBridge] OnMemAdded: memData가 null입니다.");
            return;
        }

        if (isAdded)
        {
            HandleMemDeployed(buildingType, memData, memPositions);
        }
        else
        {
            HandleMemRemoved(buildingType, memData, memPositions);
        }
    }

    /// <summary>
    /// 시설 가동 시작 이벤트 처리. 배치된 모든 멤을 각자의 작업 위치로 보내 작업시킵니다.
    /// </summary>
    private void OnFacilityStarted(BuildingType buildingType, List<MemData> deployedMems, List<Transform> memPositions)
    {
        if (deployedMems == null || deployedMems.Count == 0) return;

        Transform facilityTrans = ResolveFacilityTransform(buildingType, memPositions);

        foreach (MemData memData in deployedMems)
        {
            if (memData == null) continue;

            Mem mem = FindMemInRegistry(memData);
            if (mem == null)
            {
                // 시설 런타임은 TryAddMem 안에서 가동 판정을 먼저 하므로, 배치로 가동이 시작되는 순간엔
                // FacilityStarted가 MemAdded보다 먼저 온다. 그때 이 멤은 아직 등록 전이라 여기서 걸러진다.
                // (이어서 오는 MemAdded 처리에서 '이미 가동 중' 판정으로 작업을 시작시키므로 정상 흐름이다)
                LogVerbose($"[FacilityEventBridge] FacilityStarted: '{memData.memName}'이(가) 아직 등록 전이라 건너뜀 " +
                          $"(뒤이어 오는 배치 이벤트에서 처리됨).");
                continue;
            }

            MemAI ai = mem.AI;
            if (ai == null)
            {
                Debug.LogWarning($"[FacilityEventBridge] FacilityStarted: '{memData.memName}'의 MemAI가 null입니다.");
                continue;
            }

            // 배치 때 슬롯을 못 받았거나(이벤트 유실) 시설이 바뀐 경우를 대비해 여기서도 보정한다.
            Transform workSlot = AssignWorkSlot(memData.memId, memPositions);

            // 현재 FacilityWorkState 상태인 경우에만 OnFacilityStarted 전달
            if (ai.CurrentState == ai.FacilityWorkState)
            {
                // 슬롯/시설 정보를 최신값으로 갱신 (상태 재진입 없이 주입만)
                ai.FacilityWorkState.SetFacility(
                    buildingType,
                    facilityTrans,
                    buildingType == BuildingType.TransportFacility ? warehouseTransform : null,
                    workSlot,
                    ResolveWorkAnim(facilityTrans, buildingType));

                ai.FacilityWorkState.OnFacilityStarted(ai);
            }
            else
            {
                // 혹시 상태가 달라진 경우: 다시 FacilityWorkState로 재진입
                SetupAndTransitionToFacilityWork(ai, buildingType, facilityTrans, workSlot);
                ai.FacilityWorkState.OnFacilityStarted(ai);
            }

            SetWorkContext(memData.memId, buildingType, facilityTrans, workSlot);
            MarkWorking(memData.memId, true);
        }

        LogVerbose($"[FacilityEventBridge] FacilityStarted: {buildingType}, 대상 {deployedMems.Count}마리, " +
                  $"작업 위치 {(memPositions != null ? memPositions.Count : 0)}개");
    }

    /// <summary>
    /// 시설 가동 중지 이벤트 처리. 이유에 따라 상태를 전환합니다.
    /// (작업 위치 배정은 유지 — 멤은 여전히 이 시설에 배치된 상태이기 때문)
    /// </summary>
    private void OnFacilityStopped(BuildingType buildingType, List<MemData> deployedMems,
                                   FacilityStopReason reason, List<Transform> memPositions)
    {
        if (deployedMems == null || deployedMems.Count == 0) return;

        foreach (MemData memData in deployedMems)
        {
            if (memData == null) continue;

            Mem mem = FindMemInRegistry(memData);
            if (mem == null) continue;

            MemAI ai = mem.AI;
            if (ai == null) continue;

            // FacilityWorkState에 있는 경우에만 처리
            if (ai.CurrentState == ai.FacilityWorkState)
            {
                ai.FacilityWorkState.OnFacilityStopped(ai, reason);
            }

            MarkWorking(memData.memId, false);
        }

        LogVerbose($"[FacilityEventBridge] FacilityStopped: {buildingType}, 이유: {reason}, 대상 {deployedMems.Count}마리");
    }

    /// <summary>
    /// [영지 입장 시 배치 멤 복원용] 이미 스폰된 Mem을 해당 시설의 근무 멤으로 등록하고 즉시 근무 상태로 만듭니다.
    /// 영지에 재입장하면 시설에 배치돼 있던 멤(IsActive)을 시설 위치에 다시 소환한 뒤 이 메서드로 근무를 이어갑니다.
    /// (이벤트로 배치되는 일반 흐름은 OnMemAdded가 처리하며, 이 메서드는 그 스폰-후-등록 부분만 노출한 것)
    /// </summary>
    /// <param name="memPositions">시설의 작업 위치 목록(Runtime.MemPositions). 넘기면 멤에게 슬롯을 배정합니다.</param>
    public void RegisterExistingWorker(Mem mem, BuildingType buildingType,
                                       Transform facilityTransform = null,
                                       List<Transform> memPositions = null)
    {
        if (mem == null) { Debug.LogWarning("[FacilityEventBridge] RegisterExistingWorker: mem이 null입니다."); return; }

        MemAI ai = mem.AI;
        if (ai == null || mem.Stats == null)
        {
            Debug.LogWarning("[FacilityEventBridge] RegisterExistingWorker: MemAI/Stats가 없습니다.");
            return;
        }

        facilityMemRegistry[mem.Stats.MemId] = mem;

        // 특정 시설 Transform을 넘겨받으면 그것을 쓰고(같은 타입 시설이 여러 개일 때 정확), 없으면 타입으로 탐색.
        Transform facilityTrans = facilityTransform != null
            ? facilityTransform
            : ResolveFacilityTransform(buildingType, memPositions);

        Transform workSlot = AssignWorkSlot(mem.Stats.MemId, memPositions);

        SetupAndTransitionToFacilityWork(ai, buildingType, facilityTrans, workSlot);
        SetWorkContext(mem.Stats.MemId, buildingType, facilityTrans, workSlot);

        // 시설이 이미 가동 중이면 바로 작업 애니 시작(아니면 FacilityWorkState에서 Idle 대기).
        if (IsFacilityProducing(buildingType, facilityTrans))
        {
            ai.FacilityWorkState.OnFacilityStarted(ai);
            MarkWorking(mem.Stats.MemId, true);
        }

        LogVerbose($"[FacilityEventBridge] 입장 복원: '{mem.Stats.MemName}' → {buildingType} 근무 상태로 등록.");
    }

    // ---------------------------------------------------------------
    // 핵심 처리 메서드
    // ---------------------------------------------------------------

    /// <summary>
    /// 멤이 시설에 배치될 때 처리.
    /// 씬에서 Mem 인스턴스를 찾아 작업 위치를 배정하고 FacilityWorkState로 전환합니다.
    /// </summary>
    private void HandleMemDeployed(BuildingType buildingType, MemData memData, List<Transform> memPositions)
    {
        // 1. 시설 Transform 확보 (없는 멤을 소환할 위치로도 쓴다)
        Transform facilityTrans = ResolveFacilityTransform(buildingType, memPositions);

        // 2. 씬에서 이 MemData에 해당하는 Mem 인스턴스 탐색
        Mem mem = FindMemByData(memData);

        // 창고에만 있고 영지에 소환되지 않은 멤을 배치한 경우: 여기서 시설 옆에 소환한다.
        // (이 처리가 없으면 영지 UI에서 배치는 성공하는데 씬에는 아무 일도 일어나지 않는다)
        if (mem == null && autoSpawnMissingWorker)
        {
            mem = SpawnWorkerForFacility(memData, facilityTrans);
        }

        if (mem == null)
        {
            Debug.LogWarning($"[FacilityEventBridge] 씬에서 '{memData.memName}'({memData.memId}) Mem 인스턴스를 찾지 못했고 소환도 실패했습니다. " +
                             $"TerritoryWanderSpawner/MemPool이 씬에 있는지, NavMesh가 구워졌는지 확인하세요.");
            return;
        }

        // 3. 레지스트리 등록 + 작업 위치 배정
        facilityMemRegistry[memData.memId] = mem;

        Transform workSlot = AssignWorkSlot(memData.memId, memPositions);

        // 4. FacilityWorkState 설정 후 전환
        MemAI ai = mem.AI;
        if (ai == null)
        {
            Debug.LogWarning($"[FacilityEventBridge] '{memData.memName}'의 MemAI가 null입니다.");
            return;
        }

        SetupAndTransitionToFacilityWork(ai, buildingType, facilityTrans, workSlot);
        SetWorkContext(memData.memId, buildingType, facilityTrans, workSlot);

        // 이미 가동 중인 시설에 배정된 경우: FacilityStarted는 false→true 전환 시에만
        // 발동하므로 이 멤은 이벤트를 놓친다. 시설이 이미 가동 중이면 즉시 작업을 시작한다.
        if (IsFacilityProducing(buildingType, facilityTrans))
        {
            ai.FacilityWorkState.OnFacilityStarted(ai);
            MarkWorking(memData.memId, true);
            LogVerbose($"[FacilityEventBridge] '{memData.memName}' → {buildingType} 가동 중 → 즉시 작업 시작.");
        }
        else
        {
            LogVerbose($"[FacilityEventBridge] '{memData.memName}' → {buildingType} 시설이 가동 중이 아님 → 자리에서 가동 대기. " +
                      $"(제작대·주방·모닥불은 멤을 해제하면 제작/요리 지시가 사라지므로 다시 지시해야 가동됩니다)");
        }

        string slotName = workSlot != null ? workSlot.name : "(지정 위치 없음)";
        LogVerbose($"[FacilityEventBridge] '{memData.memName}' → {buildingType} 배치 완료, 작업 위치 '{slotName}', FacilityWorkState 진입.");
    }

    /// <summary>
    /// 시설에 배치됐지만 아직 영지에 없는 멤을 시설 옆에 소환합니다.
    /// 소환 위치는 시설 좌표이며, 스포너가 시설 칸을 피해 NavMesh 위로 스냅합니다.
    /// (시설이 가동되면 FacilityWorkState가 지정 작업 위치로 걸어 들어갑니다)
    /// </summary>
    private Mem SpawnWorkerForFacility(MemData memData, Transform facilityTrans)
    {
        if (wanderSpawner == null)
        {
            wanderSpawner = TerritoryWanderSpawner.Instance != null
                ? TerritoryWanderSpawner.Instance
                : FindFirstObjectByType<TerritoryWanderSpawner>();
        }

        if (wanderSpawner == null)
        {
            Debug.LogWarning("[FacilityEventBridge] TerritoryWanderSpawner가 씬에 없어 배치 멤을 소환하지 못했습니다.");
            return null;
        }

        Vector3 spawnPos = facilityTrans != null ? facilityTrans.position : transform.position;

        Mem spawned = wanderSpawner.SpawnWorker(memData, spawnPos);

        if (spawned != null)
            LogVerbose($"[FacilityEventBridge] '{memData.memName}'이(가) 영지에 없어 시설 옆에 소환했습니다.");

        return spawned;
    }

    /// <summary>
    /// 멤이 시설에서 해제될 때 처리.
    /// 레지스트리·작업 위치에서 제거하고 IdleState로 복귀합니다.
    /// </summary>
    private void HandleMemRemoved(BuildingType buildingType, MemData memData, List<Transform> memPositions)
    {
        // 같은 멤을 다른 시설로 옮기면 새 시설의 배치 이벤트가 먼저 오고 옛 시설의 해제 이벤트가
        // 나중에 도착할 수 있다. 그때 이 해제를 그대로 처리하면 방금 등록한 새 배치가 지워져,
        // 멤이 새 자리로 가지 못한 채 등록만 사라진다. → 해제된 시설이 현재 근무지와 다르면 무시한다.
        Transform removedFrom = ResolveFacilityTransform(buildingType, memPositions);

        if (workContexts.TryGetValue(memData.memId, out WorkContext current) &&
            current.Facility != null && removedFrom != null && current.Facility != removedFrom)
        {
            LogVerbose($"[FacilityEventBridge] '{memData.memName}' 해제 이벤트가 이전 시설('{removedFrom.name}') 것이라 무시합니다. " +
                      $"현재 근무지: '{current.Facility.name}'");
            return;
        }

        ReleaseWorkSlot(memData.memId);
        workContexts.Remove(memData.memId);

        if (!facilityMemRegistry.TryGetValue(memData.memId, out Mem mem))
        {
            Debug.LogWarning($"[FacilityEventBridge] 레지스트리에 '{memData.memName}'({memData.memId})이 없습니다.");
            return;
        }

        facilityMemRegistry.Remove(memData.memId);

        if (mem == null) return;

        MemAI ai = mem.AI;
        if (ai == null) return;

        // FacilityWorkState에서만 Idle로 복귀 (이미 다른 상태면 유지)
        if (ai.CurrentState == ai.FacilityWorkState)
        {
            ai.TransitionTo(ai.IdleState);
        }

        LogVerbose($"[FacilityEventBridge] '{memData.memName}' 시설 해제 → IdleState 복귀.");
    }

    // ---------------------------------------------------------------
    // 작업 위치(MemPos) 슬롯 배정
    // ---------------------------------------------------------------

    /// <summary>
    /// 시설의 작업 위치 목록에서 이 멤이 쓸 슬롯 하나를 배정합니다.
    /// 이미 같은 시설의 슬롯을 갖고 있으면 그대로 유지하고, 없으면 비어 있는 첫 슬롯을 잡습니다.
    /// 남는 슬롯이 없으면 null (→ 시설 칸 중심에서 작업하는 기존 동작으로 폴백).
    /// </summary>
    private Transform AssignWorkSlot(string memId, List<Transform> memPositions)
    {
        if (string.IsNullOrEmpty(memId) || memPositions == null || memPositions.Count == 0)
            return null;

        // 이미 이 시설의 슬롯을 배정받았으면 유지 (가동/중지 반복 시 자리 이동 방지)
        if (memWorkSlots.TryGetValue(memId, out Transform current) &&
            current != null && memPositions.Contains(current))
        {
            return current;
        }

        // 다른 시설의 슬롯을 갖고 있었다면 반납
        ReleaseWorkSlot(memId);

        foreach (Transform slot in memPositions)
        {
            if (slot == null) continue;

            // 점유자가 아직 시설에 남아 있으면 건너뛴다. (떠난 멤의 슬롯은 회수)
            if (slotOccupants.TryGetValue(slot, out string occupantId) &&
                occupantId != memId && facilityMemRegistry.ContainsKey(occupantId))
            {
                continue;
            }

            slotOccupants[slot] = memId;
            memWorkSlots[memId] = slot;
            return slot;
        }

        Debug.LogWarning($"[FacilityEventBridge] 작업 위치가 모두 사용 중입니다. ({memId}) → 시설 중심에서 작업합니다.");
        return null;
    }

    /// <summary>배정된 작업 위치를 반납합니다.</summary>
    private void ReleaseWorkSlot(string memId)
    {
        if (string.IsNullOrEmpty(memId)) return;

        if (memWorkSlots.TryGetValue(memId, out Transform slot))
        {
            // 시설이 철거되어 슬롯 Transform이 파괴됐어도 Dictionary 키로는 남아 있으므로
            // null 검사 없이 지운다. (검사하면 죽은 키가 영원히 쌓인다)
            if (slotOccupants.TryGetValue(slot, out string occupantId) && occupantId == memId)
            {
                slotOccupants.Remove(slot);
            }

            memWorkSlots.Remove(memId);
        }
    }

    // ---------------------------------------------------------------
    // 유틸리티
    // ---------------------------------------------------------------

    /// <summary>
    /// FacilityWorkState 설정 및 전환 공통 로직.
    /// </summary>
    private void SetupAndTransitionToFacilityWork(MemAI ai, BuildingType buildingType,
                                                  Transform facilityTrans, Transform workSlot)
    {
        ai.FacilityWorkState.SetFacility(
            buildingType,
            facilityTrans,
            buildingType == BuildingType.TransportFacility ? warehouseTransform : null,
            workSlot,
            ResolveWorkAnim(facilityTrans, buildingType)
        );

        ai.TransitionTo(ai.FacilityWorkState);
    }

    /// <summary>
    /// 시설에서 재생할 작업 애니메이션을 결정합니다.
    ///
    /// BuildingType은 7종뿐이라 제작대·주방·모닥불이 전부 Workshop(0)으로 들어옵니다.
    /// (BuildingData 확인: Building_Crafting_Table / Building_Kitchen_Table / Building_Camp_Fire 모두 0)
    /// 그래서 타입이 아니라 "어떤 시설 런타임이 붙어 있는지"로 판별합니다.
    /// </summary>
    private FacilityWorkAnim ResolveWorkAnim(Transform facilityTrans, BuildingType buildingType)
    {
        if (facilityTrans != null)
        {
            if (facilityTrans.TryGetComponent<ProductionCraftRuntime>(out _)) return FacilityWorkAnim.Craft;
            if (facilityTrans.TryGetComponent<KitchenRuntime>(out _))         return FacilityWorkAnim.Cook;
            if (facilityTrans.TryGetComponent<CampFireRuntime>(out _))        return FacilityWorkAnim.Cook;
            if (facilityTrans.TryGetComponent<GeneratorRuntime>(out _))       return FacilityWorkAnim.Run;
            if (facilityTrans.TryGetComponent<RanchFacilityRuntime>(out _))   return FacilityWorkAnim.Move;
            if (facilityTrans.TryGetComponent<TransportRuntime>(out _))       return FacilityWorkAnim.Move;

            // 일반 생산 시설(벌목장·채석장·밭)은 BuildingType이 정확히 구분되므로 그대로 추론시킨다.
            if (facilityTrans.TryGetComponent<ProductionFacilityRuntime>(out _)) return FacilityWorkAnim.Auto;
        }

        // 시설 오브젝트를 못 찾은 경우: BuildingType 추론에 맡긴다.
        return FacilityWorkAnim.Auto;
    }

    /// <summary>
    /// 이벤트로 받은 작업 위치에서 시설 오브젝트를 역추적합니다.
    /// 같은 타입 시설이 여러 개여도 정확히 "이벤트를 보낸 그 시설"을 얻을 수 있습니다.
    /// 실패하면 BuildingType으로 씬을 탐색합니다.
    /// </summary>
    private Transform ResolveFacilityTransform(BuildingType buildingType, List<Transform> memPositions)
    {
        if (memPositions != null)
        {
            foreach (Transform slot in memPositions)
            {
                if (slot == null) continue;

                Transform root = FindFacilityRoot(slot);
                if (root != null) return root;
            }
        }

        return FindFacilityTransform(buildingType);
    }

    /// <summary>작업 위치의 부모를 거슬러 올라가 시설 런타임 컴포넌트가 붙은 오브젝트를 찾습니다.</summary>
    private Transform FindFacilityRoot(Transform slot)
    {
        var production = slot.GetComponentInParent<ProductionFacilityRuntime>();
        if (production != null) return production.transform;

        var craft = slot.GetComponentInParent<ProductionCraftRuntime>();
        if (craft != null) return craft.transform;

        var kitchen = slot.GetComponentInParent<KitchenRuntime>();
        if (kitchen != null) return kitchen.transform;

        var campFire = slot.GetComponentInParent<CampFireRuntime>();
        if (campFire != null) return campFire.transform;

        var generator = slot.GetComponentInParent<GeneratorRuntime>();
        if (generator != null) return generator.transform;

        var ranch = slot.GetComponentInParent<RanchFacilityRuntime>();
        if (ranch != null) return ranch.transform;

        var transport = slot.GetComponentInParent<TransportRuntime>();
        if (transport != null) return transport.transform;

        return null;
    }

    /// <summary>
    /// 해당 시설이 현재 가동 중인지 확인합니다.
    /// 시설 오브젝트를 알고 있으면 그 시설만, 모르면 같은 BuildingType 시설 전체를 검사합니다.
    /// (가동 플래그 이름이 시설마다 다릅니다: isProducing / isCooking / isPowerGenerating / isWorking)
    /// </summary>
    private bool IsFacilityProducing(BuildingType buildingType, Transform facilityTrans)
    {
        if (facilityTrans != null)
        {
            if (facilityTrans.TryGetComponent(out ProductionFacilityRuntime production)) return production.isProducing;
            if (facilityTrans.TryGetComponent(out ProductionCraftRuntime craft))         return craft.isProducing;
            if (facilityTrans.TryGetComponent(out KitchenRuntime kitchen))               return kitchen.isCooking;
            if (facilityTrans.TryGetComponent(out CampFireRuntime campFire))             return campFire.isCooking;
            if (facilityTrans.TryGetComponent(out GeneratorRuntime generator))           return generator.isPowerGenerating;
            if (facilityTrans.TryGetComponent(out RanchFacilityRuntime ranch))           return ranch.isProducing;
            if (facilityTrans.TryGetComponent(out TransportRuntime transport))           return transport.isWorking;
        }

        foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
            if (f != null && f.buildingData != null && f.buildingData.buildingType == buildingType && f.isProducing) return true;

        foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
            if (c != null && c.buildingData != null && c.buildingData.buildingType == buildingType && c.isProducing) return true;

        foreach (var k in FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None))
            if (k != null && k.buildingData != null && k.buildingData.buildingType == buildingType && k.isCooking) return true;

        foreach (var cf in FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None))
            if (cf != null && cf.buildingData != null && cf.buildingData.buildingType == buildingType && cf.isCooking) return true;

        foreach (var g in FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None))
            if (g != null && g.buildingData != null && g.buildingData.buildingType == buildingType && g.isPowerGenerating) return true;

        foreach (var r in FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None))
            if (r != null && r.buildingData != null && r.buildingData.buildingType == buildingType && r.isProducing) return true;

        foreach (var t in FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None))
            if (t != null && t.buildingData != null && t.buildingData.buildingType == buildingType && t.isWorking) return true;

        return false;
    }

    /// <summary>
    /// 씬에서 활성화된 Mem 중 MemData.memId가 일치하는 인스턴스를 반환합니다.
    /// </summary>
    private Mem FindMemByData(MemData memData)
    {
        if (memData == null) return null;

        // 씬의 모든 활성 Mem 탐색
        Mem[] allMems = FindObjectsByType<Mem>(FindObjectsSortMode.None);

        foreach (Mem mem in allMems)
        {
            if (mem == null || !mem.IsActive) continue;

            // Mem.Stats.MemId와 MemData.memId 비교
            if (mem.Stats != null && mem.Stats.MemId == memData.memId)
            {
                return mem;
            }
        }

        return null;
    }

    /// <summary>
    /// 레지스트리에서 MemData에 해당하는 Mem을 반환합니다.
    /// </summary>
    private Mem FindMemInRegistry(MemData memData)
    {
        if (memData == null) return null;

        facilityMemRegistry.TryGetValue(memData.memId, out Mem mem);

        if (mem == null && facilityMemRegistry.ContainsKey(memData.memId))
        {
            // 레지스트리에는 있지만 오브젝트가 파괴된 경우 정리
            facilityMemRegistry.Remove(memData.memId);
        }

        return mem;
    }

    /// <summary>
    /// 씬에서 해당 BuildingType의 시설 Transform을 찾아 반환합니다.
    /// 같은 타입이 여러 개면 첫 번째를 반환합니다. (작업 위치로 역추적하지 못했을 때의 폴백)
    /// </summary>
    private Transform FindFacilityTransform(BuildingType buildingType)
    {
        foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
            if (f != null && f.buildingData != null && f.buildingData.buildingType == buildingType) return f.transform;

        foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
            if (c != null && c.buildingData != null && c.buildingData.buildingType == buildingType) return c.transform;

        foreach (var k in FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None))
            if (k != null && k.buildingData != null && k.buildingData.buildingType == buildingType) return k.transform;

        foreach (var cf in FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None))
            if (cf != null && cf.buildingData != null && cf.buildingData.buildingType == buildingType) return cf.transform;

        foreach (var g in FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None))
            if (g != null && g.buildingData != null && g.buildingData.buildingType == buildingType) return g.transform;

        foreach (var r in FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None))
            if (r != null && r.buildingData != null && r.buildingData.buildingType == buildingType) return r.transform;

        foreach (var t in FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None))
            if (t != null && t.buildingData != null && t.buildingData.buildingType == buildingType) return t.transform;

        return null;
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------
    // 디버그 (에디터 전용)
    // ---------------------------------------------------------------

    private void OnGUI()
    {
        if (!showDebugOverlay || !Application.isPlaying) return;

        int y = 10;
        GUI.Label(new Rect(10, y, 500, 20), $"[FacilityEventBridge] 등록된 멤: {facilityMemRegistry.Count}");
        y += 20;

        foreach (var pair in facilityMemRegistry)
        {
            string memName = pair.Value != null ? pair.Value.Stats?.MemName ?? "?" : "(null)";
            string slotName = memWorkSlots.TryGetValue(pair.Key, out Transform slot) && slot != null
                ? slot.name
                : "-";

            GUI.Label(new Rect(10, y, 500, 20), $"  · {pair.Key} → {memName} @ {slotName}");
            y += 18;
        }
    }
#endif
}
