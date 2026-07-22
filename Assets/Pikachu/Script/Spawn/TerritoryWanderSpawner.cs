// ============================================================================
// TerritoryWanderSpawner.cs
// 영지(Territory) 씬 전용 — 창고 멤을 영지에 소환하고 자유 배회시키는 컴포넌트
//
// [개요]
// - 영지 씬의 빈 GameObject에 이 컴포넌트를 붙이세요.
// - BoxCollider(isTrigger=false)를 같은 오브젝트에 추가하면 그 영역 안에서만 배회합니다.
//   BoxCollider가 없으면 MemMovement.wanderRadius(기본 15m) 반경으로 자유 배회합니다.
//
// [영지 담당자 연동 방법]
// - 창고 UI에서 멤을 영지에 드롭하거나 소환 버튼을 눌렀을 때:
//       TerritoryWanderSpawner.Instance.SpawnWanderer(memData, spawnPosition);
// - 멤을 영지에서 다시 창고로 회수할 때:
//       TerritoryWanderSpawner.Instance.RecallWanderer(memData);
// - 영지에 소환된 배회 멤 목록 접근:
//       TerritoryWanderSpawner.Instance.ActiveWanderers (IReadOnly)
//
// [씬 설정]
// 1. 영지 씬에 빈 GameObject 생성 → "TerritoryWanderSpawner"
// 2. 이 컴포넌트 부착
// 3. Inspector에서 memPool 슬롯에 MemPool 오브젝트 드래그
// 4. (선택) 같은 GameObject에 BoxCollider 추가 → 배회 경계로 자동 사용됨
// ============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using MemSystem.Core;
using MemSystem.Data;
using MemSystem.Spawn;

/// <summary>
/// 영지 씬에서 창고 멤을 소환하고 자유 배회시키는 매니저 컴포넌트.
/// 영지(Territory) 씬에 하나만 배치하세요.
/// </summary>
public class TerritoryWanderSpawner : MonoBehaviour
{
    // =================================================================
    // 싱글턴
    // =================================================================

    /// <summary>씬 내 단일 인스턴스 접근자. 없으면 null.</summary>
    public static TerritoryWanderSpawner Instance { get; private set; }

    // =================================================================
    // Inspector 설정
    // =================================================================

    [Header("필수 참조")]
    [Tooltip("멤을 스폰/디스폰할 MemPool 오브젝트")]
    [SerializeField] private MemPool memPool;

    [Header("영지 경계 (선택)")]
    [Tooltip("배회 범위를 제한할 BoxCollider. 없으면 wanderRadius 기본값 사용.")]
    [SerializeField] private BoxCollider territoryBounds;

    [Header("소환 설정")]
    [Tooltip("소환 위치가 지정되지 않을 때 사용할 기본 소환 지점")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Tooltip("영지에 동시에 배회할 수 있는 최대 멤 수")]
    [SerializeField] private int maxWanderers = 10;

    [Tooltip("소환 위치를 NavMesh 표면에 스냅할 때 탐색 반경(m). NavMesh는 바닥보다 살짝 위에 생기므로 필요.")]
    [SerializeField] private float navMeshSnapRadius = 2f;

    [Tooltip("영지에 소환되는 멤의 크기 배율. 1=원본, 0.5=절반. 영지 스케일에 맞춰 조절하세요.")]
    [SerializeField] private float memScale = 0.5f;

    // =================================================================
    // 내부 상태
    // =================================================================

    /// <summary>현재 영지에서 배회 중인 멤. Key: MemData.memId</summary>
    private readonly Dictionary<string, Mem> wandererRegistry = new Dictionary<string, Mem>();

    /// <summary>영지에서 배회 중인 멤 읽기 전용 뷰.</summary>
    public IReadOnlyDictionary<string, Mem> ActiveWanderers => wandererRegistry;

    /// <summary>현재 영지에 소환된 배회 멤 수.</summary>
    public int WandererCount => wandererRegistry.Count;

    // =================================================================
    // Unity 생명주기
    // =================================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TerritoryWanderSpawner] 씬에 인스턴스가 이미 있습니다. 중복 제거.");
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // BoxCollider 자동 탐색
        if (territoryBounds == null)
            territoryBounds = GetComponent<BoxCollider>();

        // MemPool 자동 탐색
        if (memPool == null)
            memPool = FindFirstObjectByType<MemPool>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =================================================================
    // Public API (영지 담당자가 호출)
    // =================================================================

    /// <summary>
    /// 창고에서 멤을 영지에 소환하여 자유 배회시킵니다.
    /// </summary>
    /// <param name="memData">소환할 멤의 MemData SO</param>
    /// <param name="spawnPosition">소환 위치. default이면 defaultSpawnPoint 사용.</param>
    /// <returns>소환된 Mem 인스턴스. 실패 시 null.</returns>
    public Mem SpawnWanderer(MemData memData, Vector3 spawnPosition = default)
    {
        if (memData == null)
        {
            Debug.LogWarning("[TerritoryWanderSpawner] memData가 null입니다.");
            return null;
        }

        if (memPool == null)
        {
            Debug.LogWarning("[TerritoryWanderSpawner] MemPool이 없습니다. Inspector에서 연결하거나 씬에 배치해주세요.");
            return null;
        }

        if (wandererRegistry.ContainsKey(memData.memId))
        {
            Debug.LogWarning($"[TerritoryWanderSpawner] '{memData.memName}'은(는) 이미 영지에 소환되어 있습니다.");
            return null;
        }

        if (wandererRegistry.Count >= maxWanderers)
        {
            Debug.LogWarning($"[TerritoryWanderSpawner] 최대 배회 멤 수({maxWanderers})에 도달했습니다.");
            return null;
        }

        // 소환 위치 결정
        Vector3 pos = ResolveSpawnPosition(spawnPosition);

        // NavMesh 표면에 스냅.
        // NavMesh는 복셀화 때문에 실제 바닥(y=0)보다 살짝 위(예: y≈0.08)에 생성됩니다.
        // 스냅하지 않고 바닥 좌표로 스폰하면 NavMeshAgent가
        // "not close enough to the NavMesh" 오류로 생성에 실패하고 이동하지 못합니다.
        if (NavMesh.SamplePosition(pos, out NavMeshHit navHit, navMeshSnapRadius, NavMesh.AllAreas))
        {
            pos = navHit.position;
        }
        else
        {
            Debug.LogWarning($"[TerritoryWanderSpawner] 소환 위치 {pos} 주변 {navMeshSnapRadius}m 내에 NavMesh가 없습니다. " +
                             "NavMesh가 구워졌는지(TerritoryTestNavMeshBaker 등) 확인하세요. 소환을 취소합니다.");
            return null;
        }

        // 스폰 (MemAI.Initialize → IdleState → 2~5초 후 WanderState 자동 진입)
        Mem mem = memPool.Spawn(memData, pos);
        if (mem == null)
        {
            Debug.LogWarning($"[TerritoryWanderSpawner] '{memData.memName}' 스폰 실패.");
            return null;
        }

        // 영지 소환 크기 적용 (풀 재사용 시를 대비해 매 소환마다 명시적으로 설정)
        mem.transform.localScale = Vector3.one * memScale;

        // 배회 경계 설정 (BoxCollider가 있는 경우)
        ApplyWanderBoundsIfNeeded(mem);

        // 레지스트리 등록
        wandererRegistry[memData.memId] = mem;

        Debug.Log($"[TerritoryWanderSpawner] '{memData.memName}' 영지 소환 완료. 현재 배회 멤: {wandererRegistry.Count}/{maxWanderers}");
        return mem;
    }

    /// <summary>
    /// 영지에서 배회 중인 멤을 회수(디스폰)합니다.
    /// </summary>
    /// <param name="memData">회수할 멤의 MemData</param>
    /// <returns>회수 성공 여부.</returns>
    public bool RecallWanderer(MemData memData)
    {
        if (memData == null) return false;

        if (!wandererRegistry.TryGetValue(memData.memId, out Mem mem))
        {
            Debug.LogWarning($"[TerritoryWanderSpawner] '{memData.memName}'이(가) 영지에 없습니다.");
            return false;
        }

        wandererRegistry.Remove(memData.memId);

        if (mem != null)
        {
            // 배회 경계 해제 + 크기 원복 후 풀로 반환 (다른 시스템의 풀 재사용 대비)
            mem.Movement?.ClearWanderBounds();
            mem.transform.localScale = Vector3.one;
            memPool.Despawn(mem);
        }

        Debug.Log($"[TerritoryWanderSpawner] '{memData.memName}' 영지에서 회수. 현재 배회 멤: {wandererRegistry.Count}/{maxWanderers}");
        return true;
    }

    /// <summary>
    /// 영지에 소환된 배회 멤 전원을 회수합니다. (씬 전환 시 호출)
    /// </summary>
    public void RecallAllWanderers()
    {
        var keys = new List<string>(wandererRegistry.Keys);
        foreach (string id in keys)
        {
            if (wandererRegistry.TryGetValue(id, out Mem mem) && mem != null)
            {
                mem.Movement?.ClearWanderBounds();
                mem.transform.localScale = Vector3.one;
                memPool.Despawn(mem);
            }
        }
        wandererRegistry.Clear();
        Debug.Log("[TerritoryWanderSpawner] 영지 배회 멤 전원 회수 완료.");
    }

    // =================================================================
    // 내부 유틸리티
    // =================================================================

    /// <summary>소환 위치를 결정합니다. 유효하지 않으면 defaultSpawnPoint 또는 본체 위치를 사용.</summary>
    private Vector3 ResolveSpawnPosition(Vector3 requested)
    {
        if (requested != default && requested != Vector3.zero)
            return requested;

        if (defaultSpawnPoint != null)
            return defaultSpawnPoint.position;

        if (territoryBounds != null)
            return territoryBounds.bounds.center;

        return transform.position;
    }

    /// <summary>BoxCollider가 있으면 해당 Bounds를 배회 경계로 멤에 설정합니다.</summary>
    private void ApplyWanderBoundsIfNeeded(Mem mem)
    {
        if (mem == null || mem.Movement == null) return;

        if (territoryBounds != null)
        {
            mem.Movement.SetWanderBounds(territoryBounds.bounds);
        }
    }

    // =================================================================
    // 에디터 디버그 시각화
    // =================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // BoxCollider 경계 시각화 (초록색 반투명 박스)
        if (territoryBounds != null)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.25f);
            Gizmos.DrawCube(territoryBounds.bounds.center, territoryBounds.bounds.size);
            Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.9f);
            Gizmos.DrawWireCube(territoryBounds.bounds.center, territoryBounds.bounds.size);
        }

        // 기본 소환 지점 표시 (하늘색 구)
        if (defaultSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(defaultSpawnPoint.position, 0.5f);
        }
    }

    private void OnGUI()
    {
        if (!Application.isPlaying) return;

        int y = 40;
        GUI.Label(new Rect(10, y, 400, 20), $"[TerritoryWanderSpawner] 배회 멤: {wandererRegistry.Count}/{maxWanderers}");
        y += 20;
        foreach (var pair in wandererRegistry)
        {
            string memName = pair.Value != null ? pair.Value.Stats?.MemName ?? "?" : "(null)";
            GUI.Label(new Rect(10, y, 400, 20), $"  · {memName}  (배회 중)");
            y += 18;
        }
    }
#endif
}
