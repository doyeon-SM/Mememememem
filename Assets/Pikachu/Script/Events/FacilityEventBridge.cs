// ============================================================================
// FacilityEventBridge.cs
// 영지 생산 시설 이벤트 → 멤 AI 연결 브릿지
//
// [역할]
// - ProductionFacilityRuntime / ProductionCraftRuntime 의 static 이벤트를 구독합니다.
// - 이벤트 수신 시 씬에서 활성화된 Mem 인스턴스를 MemData로 찾아 AI 상태를 전환합니다.
//
// [씬 배치]
// - 영지(Territory) 씬에 빈 GameObject를 하나 만들고 이 컴포넌트를 붙이세요.
// - [선택] warehouseTransform: 운반시설(TransportFacility)의 왕복 창고 위치.
//   없으면 운반 멤은 시설 근처를 배회합니다.
//
// [이벤트 흐름]
//   MemAdded(type, data, true)     → 멤 배치 → FacilityWorkState 진입 (Idle 대기)
//   FacilityStarted(type, list)    → 시설 가동 → 작업 애니 재생
//   FacilityStopped(type, list, r) → 시설 중지 → 이유별 상태 전환
//   MemAdded(type, data, false)    → 멤 해제 → IdleState 복귀
//
// [주의] ProductionFacilityRuntime과 ProductionCraftRuntime 모두 동일 이름의
//        static 이벤트를 가지므로, 두 클래스 모두 구독합니다.
//
// ⚠️ [영지 담당자 업데이트 필요]
// 아래 이벤트 시그니처로 변경 후 이 파일이 컴파일됩니다:
//   public static event Action<BuildingType, MemData, bool>              MemAdded;
//   public static event Action<BuildingType, List<MemData>>             FacilityStarted;
//   public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;
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

    // ---------------------------------------------------------------
    // 내부 레지스트리: MemData.memId → Mem 인스턴스
    // ---------------------------------------------------------------

    /// <summary>
    /// 시설에 배치된 멤의 레지스트리.
    /// Key: MemData.memId, Value: 씬의 Mem 인스턴스
    /// </summary>
    private readonly Dictionary<string, Mem> facilityMemRegistry = new Dictionary<string, Mem>();

    // ---------------------------------------------------------------
    // Unity 이벤트 구독/해제
    // ---------------------------------------------------------------

    private void OnEnable()
    {
        // ProductionFacilityRuntime (일반 생산 시설: 벌목/채굴/밭/목장/운반/발전기)
        ProductionFacilityRuntime.MemAdded       += OnMemAdded;
        ProductionFacilityRuntime.FacilityStarted += OnFacilityStarted;
        ProductionFacilityRuntime.FacilityStopped += OnFacilityStopped;

        // ProductionCraftRuntime (제작대 전용)
        ProductionCraftRuntime.MemAdded          += OnMemAdded;
        ProductionCraftRuntime.FacilityStarted   += OnFacilityStarted;
        ProductionCraftRuntime.FacilityStopped   += OnFacilityStopped;

        Debug.Log("[FacilityEventBridge] 이벤트 구독 완료.");
    }

    private void OnDisable()
    {
        ProductionFacilityRuntime.MemAdded       -= OnMemAdded;
        ProductionFacilityRuntime.FacilityStarted -= OnFacilityStarted;
        ProductionFacilityRuntime.FacilityStopped -= OnFacilityStopped;

        ProductionCraftRuntime.MemAdded          -= OnMemAdded;
        ProductionCraftRuntime.FacilityStarted   -= OnFacilityStarted;
        ProductionCraftRuntime.FacilityStopped   -= OnFacilityStopped;

        Debug.Log("[FacilityEventBridge] 이벤트 구독 해제 완료.");
    }

    // ---------------------------------------------------------------
    // 이벤트 핸들러
    // ---------------------------------------------------------------

    /// <summary>
    /// 멤 배치(true) / 해제(false) 이벤트 처리.
    /// </summary>
    private void OnMemAdded(BuildingType buildingType, MemData memData, bool isAdded)
    {
        if (memData == null)
        {
            Debug.LogWarning("[FacilityEventBridge] OnMemAdded: memData가 null입니다.");
            return;
        }

        if (isAdded)
        {
            HandleMemDeployed(buildingType, memData);
        }
        else
        {
            HandleMemRemoved(memData);
        }
    }

    /// <summary>
    /// 시설 가동 시작 이벤트 처리. 배치된 모든 멤의 작업 애니메이션을 재생합니다.
    /// </summary>
    private void OnFacilityStarted(BuildingType buildingType, List<MemData> deployedMems)
    {
        if (deployedMems == null || deployedMems.Count == 0) return;

        // 시설에 배치된 시설 Transform 가져오기 (운반시설 왕복용)
        Transform facilityTrans = FindFacilityTransform(buildingType);

        foreach (MemData memData in deployedMems)
        {
            if (memData == null) continue;

            Mem mem = FindMemInRegistry(memData);
            if (mem == null) continue;

            MemAI ai = mem.AI;
            if (ai == null) continue;

            // 현재 FacilityWorkState 상태인 경우에만 OnFacilityStarted 전달
            if (ai.CurrentState == ai.FacilityWorkState)
            {
                ai.FacilityWorkState.OnFacilityStarted(ai);
            }
            else
            {
                // 혹시 상태가 달라진 경우: 다시 FacilityWorkState로 재진입
                SetupAndTransitionToFacilityWork(ai, buildingType, facilityTrans);
                ai.FacilityWorkState.OnFacilityStarted(ai);
            }
        }

        Debug.Log($"[FacilityEventBridge] FacilityStarted: {buildingType}, 대상 {deployedMems.Count}마리");
    }

    /// <summary>
    /// 시설 가동 중지 이벤트 처리. 이유에 따라 상태를 전환합니다.
    /// </summary>
    private void OnFacilityStopped(BuildingType buildingType, List<MemData> deployedMems, FacilityStopReason reason)
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
        }

        Debug.Log($"[FacilityEventBridge] FacilityStopped: {buildingType}, 이유: {reason}, 대상 {deployedMems.Count}마리");
    }

    // ---------------------------------------------------------------
    // 핵심 처리 메서드
    // ---------------------------------------------------------------

    /// <summary>
    /// 멤이 시설에 배치될 때 처리.
    /// 씬에서 Mem 인스턴스를 찾아 FacilityWorkState로 전환합니다.
    /// </summary>
    private void HandleMemDeployed(BuildingType buildingType, MemData memData)
    {
        // 1. 씬에서 이 MemData에 해당하는 Mem 인스턴스 탐색
        Mem mem = FindMemByData(memData);

        if (mem == null)
        {
            Debug.LogWarning($"[FacilityEventBridge] 씬에서 '{memData.memName}'({memData.memId}) Mem 인스턴스를 찾지 못했습니다. " +
                             $"멤이 이미 스폰되어 있어야 합니다.");
            return;
        }

        // 2. 레지스트리에 등록
        facilityMemRegistry[memData.memId] = mem;

        // 3. 시설 Transform 가져오기
        Transform facilityTrans = FindFacilityTransform(buildingType);

        // 4. FacilityWorkState 설정 후 전환
        MemAI ai = mem.AI;
        if (ai == null)
        {
            Debug.LogWarning($"[FacilityEventBridge] '{memData.memName}'의 MemAI가 null입니다.");
            return;
        }

        SetupAndTransitionToFacilityWork(ai, buildingType, facilityTrans);

        // 이미 가동 중인 시설에 배정된 경우: FacilityStarted는 false→true 전환 시에만
        // 발동하므로 이 멤은 이벤트를 놓친다. 시설이 이미 가동 중이면 즉시 작업을 시작한다.
        if (IsFacilityProducing(buildingType))
        {
            ai.FacilityWorkState.OnFacilityStarted(ai);
            Debug.Log($"[FacilityEventBridge] '{memData.memName}' → {buildingType} 이미 가동 중 → 즉시 작업 시작.");
        }

        Debug.Log($"[FacilityEventBridge] '{memData.memName}' → {buildingType} 배치 완료, FacilityWorkState 진입.");
    }

    /// <summary>
    /// 해당 BuildingType의 시설이 현재 가동(isProducing) 중인지 확인합니다.
    /// 일반 생산시설(ProductionFacilityRuntime)과 제작대(ProductionCraftRuntime) 모두 검사.
    /// </summary>
    private bool IsFacilityProducing(BuildingType buildingType)
    {
        foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
            if (f != null && f.buildingData != null &&
                f.buildingData.buildingType == buildingType && f.isProducing)
                return true;

        foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
            if (c != null && c.buildingData != null &&
                c.buildingData.buildingType == buildingType && c.isProducing)
                return true;

        return false;
    }

    /// <summary>
    /// 멤이 시설에서 해제될 때 처리.
    /// 레지스트리에서 제거하고 IdleState로 복귀합니다.
    /// </summary>
    private void HandleMemRemoved(MemData memData)
    {
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

        Debug.Log($"[FacilityEventBridge] '{memData.memName}' 시설 해제 → IdleState 복귀.");
    }

    // ---------------------------------------------------------------
    // 유틸리티
    // ---------------------------------------------------------------

    /// <summary>
    /// FacilityWorkState 설정 및 전환 공통 로직.
    /// </summary>
    private void SetupAndTransitionToFacilityWork(MemAI ai, BuildingType buildingType, Transform facilityTrans)
    {
        ai.FacilityWorkState.SetFacility(
            buildingType,
            facilityTrans,
            buildingType == BuildingType.TransportFacility ? warehouseTransform : null
        );

        ai.TransitionTo(ai.FacilityWorkState);
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
    /// 같은 타입이 여러 개면 첫 번째를 반환합니다.
    /// </summary>
    private Transform FindFacilityTransform(BuildingType buildingType)
    {
        // ProductionFacilityRuntime에서 탐색
        ProductionFacilityRuntime[] facilityRuntimes =
            FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);

        foreach (ProductionFacilityRuntime runtime in facilityRuntimes)
        {
            if (runtime.buildingData != null &&
                runtime.buildingData.buildingType == buildingType)
            {
                return runtime.transform;
            }
        }

        // ProductionCraftRuntime에서 탐색 (제작대)
        ProductionCraftRuntime[] craftRuntimes =
            FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);

        foreach (ProductionCraftRuntime runtime in craftRuntimes)
        {
            if (runtime.buildingData != null &&
                runtime.buildingData.buildingType == buildingType)
            {
                return runtime.transform;
            }
        }

        return null;
    }

#if UNITY_EDITOR
    // ---------------------------------------------------------------
    // 디버그 (에디터 전용)
    // ---------------------------------------------------------------

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        int y = 10;
        GUI.Label(new Rect(10, y, 400, 20), $"[FacilityEventBridge] 등록된 멤: {facilityMemRegistry.Count}");
        y += 20;

        foreach (var pair in facilityMemRegistry)
        {
            string memName = pair.Value != null ? pair.Value.Stats?.MemName ?? "?" : "(null)";
            GUI.Label(new Rect(10, y, 400, 20), $"  · {pair.Key} → {memName}");
            y += 18;
        }
    }
#endif
}
