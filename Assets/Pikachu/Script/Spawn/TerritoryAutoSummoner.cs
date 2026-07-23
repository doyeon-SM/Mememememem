// ============================================================================
// TerritoryAutoSummoner.cs
// 영지(Territory) 씬 전용 — 입장 시 멤 창고의 멤들을 자동으로 소환한다.
//
// [기획 요구]
// 1. 월드에서 잡아 멤 창고에 있는 멤은 영지 입장 시 자동 소환되어 배회한다.
// 2. 자동 배회 소환 수 = 영지 레벨별 최대치(인스펙터 표, 예: Lv1=5, Lv2=7 …).
// 3. 창고 멤이 최대치보다 많으면, 영지에 들어올 때마다 랜덤하게 다른 멤들이 소환된다.
// 4. 단, 생산 시설에 배치된 멤(CapturedMemEntry.IsActive)은 이 "배회 수"에 포함되지 않는다.
//    → 대신 시설 위치에 "근무 상태"로 복원 소환된다(영지를 나갔다 와도 계속 근무 중).
//
// [연동 지점 (전부 기존 API, 타 담당자 파일은 읽기만)]
// - 멤 창고 목록 : HDY.Capture.MemCaptureManager.Instance.CapturedMems (CapturedMemEntry)
// - 시설 배치 여부 : CapturedMemEntry.IsActive (시설 배치 시 자동 true → 배회 후보에서 제외)
// - MemId→MemData : HDY.Mem.MemCatalogManager.Instance.FindMemData(memId)
// - 영지 레벨     : HDY.Territory.TerritoryData.Instance.Level
// - 시설 배치 멤  : ProductionFacilityRuntime / ProductionCraftRuntime 의 DeployedMems (List<MemData>)
// - 실제 소환    : TerritoryWanderSpawner (배회) / FacilityEventBridge (근무 상태 구동)
//
// [씬 설정] 영지 씬에 빈 GameObject를 만들고 이 컴포넌트를 붙이세요. (같은 씬에
//           TerritoryWanderSpawner, FacilityEventBridge, MemPool, NavMesh 베이커가 있어야 함)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Core;
using MemSystem.Data;
using HDY.Capture;
using HDY.Territory;
using Pikachu.Test; // TerritoryTestNavMeshBaker
// 주의: HDY.Mem 네임스페이스는 MemSystem.Core.Mem 클래스와 이름이 겹칠 수 있어 using 대신
//       MemCatalogManager를 전체 경로(HDY.Mem.MemCatalogManager)로 참조한다.

/// <summary>
/// 영지 입장 시 멤 창고의 멤을 자동 소환하는 매니저.
/// (배회 멤은 레벨별 최대치만큼 랜덤 소환, 시설 배치 멤은 시설에 근무 상태로 복원)
/// </summary>
public class TerritoryAutoSummoner : MonoBehaviour
{
    [Header("참조 (비어있으면 자동 탐색)")]
    [SerializeField] private TerritoryWanderSpawner wanderSpawner;
    [SerializeField] private FacilityEventBridge facilityBridge;
    [Tooltip("NavMesh 베이커. 입장 시 현재 시설 위치를 넘겨 '시설 칸'을 다시 구워 순찰 멤이 시설을 통과 못 하게 함.")]
    [SerializeField] private TerritoryTestNavMeshBaker navMeshBaker;

    [Header("영지 레벨별 최대 배회 수 (index 0 = Lv1, Lv2, Lv3 …)")]
    [Tooltip("영지 레벨에 따라 자동 배회 소환되는 최대 멤 수. 레벨이 표 범위를 넘으면 마지막 값을 사용합니다.")]
    [SerializeField] private List<int> maxWanderersPerLevel = new List<int> { 5, 7, 9, 11, 13 };

    [Header("타이밍")]
    [Tooltip("영지 입장 후 그리드/시설 복원 + NavMesh 베이크가 끝나길 기다렸다가 소환하는 지연(초). NavMesh 베이커 지연보다 커야 합니다.")]
    [SerializeField] private float initialDelay = 1.0f;

    [Header("옵션")]
    [Tooltip("시설에 배치된 멤을 시설 위치에 근무 상태로 복원 소환할지 여부.")]
    [SerializeField] private bool summonFacilityWorkers = true;

    [Header("테스트 폴백 (격리 테스트 씬 전용)")]
    [Tooltip("멤 창고(MemCaptureManager)가 씬에 없을 때, 대신 이 목록에서 배회 멤을 소환한다. " +
             "실제 빌드에선 멤 창고를 사용하므로 이 목록은 무시된다. 격리 테스트 씬에서 자동 소환을 확인할 때만 채워라.")]
    [SerializeField] private List<MemData> testFallbackMemData = new List<MemData>();

    private IEnumerator Start()
    {
        // 그리드 생성 + (있다면)세이브 복원 + NavMesh 베이크가 끝나길 기다린다.
        yield return new WaitForSeconds(initialDelay);
        Populate();
    }

    /// <summary>영지 입장 시 1회 호출: 시설 근무 멤 복원 + 배회 멤 랜덤 소환.</summary>
    public void Populate()
    {
        if (wanderSpawner == null)
            wanderSpawner = TerritoryWanderSpawner.Instance != null
                ? TerritoryWanderSpawner.Instance
                : FindFirstObjectByType<TerritoryWanderSpawner>();

        if (wanderSpawner == null)
        {
            Debug.LogWarning("[TerritoryAutoSummoner] TerritoryWanderSpawner가 없어 소환할 수 없습니다.");
            return;
        }

        if (facilityBridge == null)
            facilityBridge = FindFirstObjectByType<FacilityEventBridge>();

        if (navMeshBaker == null)
            navMeshBaker = FindFirstObjectByType<TerritoryTestNavMeshBaker>();

        // 깨끗한 상태에서 시작 (재입장/재호출 대비)
        wanderSpawner.RecallAllWanderers();

        // 0) 현재 씬의 시설 위치를 NavMesh 베이커에 넘겨 '시설 칸'을 다시 굽는다(스폰 전에).
        //    → 순찰 멤이 시설 칸을 통과하지 못하게 하고, 배치 멤만 진입 가능하게 함.
        FeedNavMeshBaker();

        // 1) 시설 배치 멤을 근무 상태로 복원 소환 (배회 최대치에 미포함)
        if (summonFacilityWorkers)
            SummonFacilityWorkers();

        // 2) 창고의 놀고 있는(비배치) 멤을 레벨별 최대치만큼 랜덤 소환
        SummonWanderers();
    }

    // ---------------------------------------------------------------
    // NavMesh 시설 칸 표시 (순찰 차단)
    // ---------------------------------------------------------------

    /// <summary>씬의 모든 생산 시설 위치를 NavMesh 베이커에 넘겨 '시설 칸'으로 다시 굽는다.</summary>
    private void FeedNavMeshBaker()
    {
        if (navMeshBaker == null)
        {
            Debug.LogWarning("[TerritoryAutoSummoner] NavMesh 베이커가 없어 시설 칸 표시를 하지 못했습니다. " +
                             "(순찰 멤이 시설을 통과할 수 있음)");
            return;
        }

        var centers = new List<Vector3>();
        foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
            if (f != null) centers.Add(f.transform.position);
        foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
            if (c != null) centers.Add(c.transform.position);

        navMeshBaker.SetFacilityCells(centers); // 내부에서 리베이크
    }

    // ---------------------------------------------------------------
    // 시설 배치 멤 → 근무 상태로 복원
    // ---------------------------------------------------------------

    private void SummonFacilityWorkers()
    {
        if (facilityBridge == null)
        {
            Debug.LogWarning("[TerritoryAutoSummoner] FacilityEventBridge가 없어 시설 근무 멤을 복원하지 못했습니다.");
            return;
        }

        int count = 0;

        foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
            if (f != null) count += SummonWorkersOf(f.DeployedMems, f.buildingData, f.transform);

        foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
            if (c != null) count += SummonWorkersOf(c.DeployedMems, c.buildingData, c.transform);

        if (count > 0)
            Debug.Log($"[TerritoryAutoSummoner] 시설 근무 멤 {count}마리 복원 소환.");
    }

    private int SummonWorkersOf(List<MemData> deployed, BuildingData buildingData, Transform facilityTransform)
    {
        if (deployed == null || deployed.Count == 0 || buildingData == null || facilityTransform == null)
            return 0;

        int n = 0;
        foreach (var memData in deployed)
        {
            if (memData == null) continue;

            Mem worker = wanderSpawner.SpawnWorker(memData, facilityTransform.position);
            if (worker == null) continue;

            facilityBridge.RegisterExistingWorker(worker, buildingData.buildingType, facilityTransform);
            n++;
        }
        return n;
    }

    // ---------------------------------------------------------------
    // 창고의 비배치 멤 → 배회 (레벨별 최대치, 랜덤)
    // ---------------------------------------------------------------

    private void SummonWanderers()
    {
        int level = TerritoryData.Instance != null ? TerritoryData.Instance.Level : 1;
        int max = GetMaxWanderersForLevel(level);

        // 스포너의 안전 상한도 레벨 최대치에 맞춰 올려둔다.
        if (wanderSpawner.MaxWanderers < max)
            wanderSpawner.MaxWanderers = max;

        // 1) 실제 멤 창고에서 배회 후보 수집(비어있지 않고 시설배치 아닌 멤).
        var pool = BuildWarehousePool();
        string source = "창고";

        // 2) 창고가 없거나 "비어있으면" 테스트 폴백 목록 사용.
        //    (실제 빌드에선 testFallbackMemData를 비워두므로 폴백은 격리 테스트에서만 동작한다)
        if (pool.Count == 0 && testFallbackMemData != null && testFallbackMemData.Count > 0)
        {
            pool = BuildTestPool();
            source = "테스트 폴백";
        }

        // 3) 후보가 최대치보다 많으면 랜덤 셔플로 max개만(매 입장마다 다른 조합 - 기획 요구 3).
        Shuffle(pool);

        // 소환 위치/배회 경계를 "그리드"에 맞춘다. → Mem_Territory 오브젝트 위치나 BoxCollider 유무와 무관하게
        //   항상 그리드(월드 원점 근처) 위에 소환되고 그리드 안에서 배회한다.
        Bounds grid = navMeshBaker != null ? navMeshBaker.GridWorldBounds : default;
        bool hasGrid = grid.size.x >= 0.5f && grid.size.z >= 0.5f;

        int spawned = 0;
        for (int i = 0; i < pool.Count && spawned < max; i++)
        {
            if (pool[i].data == null) continue;

            Vector3 pos = hasGrid ? RandomPointInGrid(grid) : default; // default면 스포너가 자체 위치로 결정
            Mem mem = wanderSpawner.SpawnWanderer(pool[i].data, pool[i].key, pos);
            if (mem == null) continue;

            // 배회 경계도 그리드로 고정(BoxCollider가 없거나 어긋나 있어도 그리드 안에서만 배회).
            if (hasGrid && mem.Movement != null)
                mem.Movement.SetWanderBounds(grid);

            spawned++;
        }

        Debug.Log($"[TerritoryAutoSummoner] 배회 멤 소환({source}): 레벨 {level} 최대 {max}, 후보 {pool.Count}마리 중 {spawned}마리 소환. " +
                  (hasGrid ? $"(그리드 {grid.center}±{grid.extents})" : "(그리드 범위 미확인 → 스포너 기본 위치)"));
    }

    /// <summary>멤 창고에서 배회 후보(MemData + 고유키 KeyId)를 수집. 창고/카탈로그 없거나 비면 빈 목록.</summary>
    private List<(MemData data, string key)> BuildWarehousePool()
    {
        var result = new List<(MemData data, string key)>();

        var capture = MemCaptureManager.Instance;
        if (capture == null) return result;

        var catalog = HDY.Mem.MemCatalogManager.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("[TerritoryAutoSummoner] MemCatalogManager가 없어 창고 멤을 조회하지 못했습니다.");
            return result;
        }

        // 배회 후보 = !IsEmpty && !IsActive (시설 배치 멤은 IsActive라 자동 제외 - 기획 요구 4).
        foreach (var e in capture.CapturedMems)
        {
            if (e == null || e.IsEmpty || e.IsActive) continue;
            MemData data = catalog.FindMemData(e.MemId);
            if (data == null)
            {
                Debug.LogWarning($"[TerritoryAutoSummoner] MemId '{e.MemId}'에 해당하는 MemData를 카탈로그에서 찾지 못했습니다.");
                continue;
            }
            result.Add((data, e.KeyId)); // 개체 고유키(KeyId) → 같은 종족 여러 마리도 각각 소환
        }
        return result;
    }

    /// <summary>[격리 테스트 폴백] testFallbackMemData에서 배회 후보를 만든다(인덱스로 고유키 부여).</summary>
    private List<(MemData data, string key)> BuildTestPool()
    {
        var result = new List<(MemData data, string key)>();
        if (testFallbackMemData == null) return result;

        for (int i = 0; i < testFallbackMemData.Count; i++)
            if (testFallbackMemData[i] != null)
                result.Add((testFallbackMemData[i], "test_" + i));

        return result;
    }

    /// <summary>그리드 범위 안의 랜덤 XZ 지점(가장자리 여백을 둬 NavMesh 안쪽에 들어오게). 스포너가 NavMesh에 스냅.</summary>
    private Vector3 RandomPointInGrid(Bounds grid)
    {
        float mx = Mathf.Max(0f, grid.extents.x - 0.6f);
        float mz = Mathf.Max(0f, grid.extents.z - 0.6f);
        return new Vector3(
            grid.center.x + Random.Range(-mx, mx),
            grid.center.y,
            grid.center.z + Random.Range(-mz, mz));
    }

    /// <summary>영지 레벨(1부터)에 해당하는 최대 배회 수. 표 범위를 넘으면 마지막 값을 사용.</summary>
    private int GetMaxWanderersForLevel(int level)
    {
        if (maxWanderersPerLevel == null || maxWanderersPerLevel.Count == 0)
            return 0;

        int idx = Mathf.Clamp(level - 1, 0, maxWanderersPerLevel.Count - 1);
        return Mathf.Max(0, maxWanderersPerLevel[idx]);
    }

    /// <summary>Fisher–Yates 셔플. (Random.Range는 결정적 시드 없이 매 입장마다 다른 조합을 만든다)</summary>
    private void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
