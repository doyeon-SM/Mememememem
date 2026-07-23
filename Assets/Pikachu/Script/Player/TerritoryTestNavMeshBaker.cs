// ============================================================================
// TerritoryTestNavMeshBaker.cs
// 영지 테스트 씬 전용 — 런타임에 그리드 위 NavMesh를 만드는 컴포넌트 (멤 담당자 테스트용)
//
// [배경]
// - 영지(_Kyusoo) 시스템은 NavMesh를 전혀 사용하지 않습니다. (멤 = UI/데이터)
// - 우리 3D 멤은 NavMeshAgent 기반이라 이동하려면 NavMesh가 반드시 필요합니다.
//
// [방식 — NavMeshSurface 대신 저수준 NavMeshBuilder]
// - 이 씬/패키지 버전에서는 NavMeshSurface(콜라이더/렌더러 수집) 베이크가 결과 0개로
//   실패하는 문제가 있어, 콜라이더/레이어에 의존하지 않고 저수준 API로 굽습니다.
// - 그리드 전체를 덮는 "절차적 박스" 하나를 걷기 가능 지형으로 직접 넣어 NavMesh를 만듭니다.
//   → 콜라이더/메쉬/레이어/tripo 모델과 무관하게 항상 안정적으로 생성됩니다.
//
// [씬 설정]
// 1. 빈 GameObject 생성 → 이 컴포넌트 부착
// 2. Play → 그리드 위 NavMesh 자동 생성 (그리드 확장/변경 시 F5로 재생성)
// ============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using MemSystem.Movement; // MemMovement.FacilityNavMeshArea

namespace Pikachu.Test
{
    /// <summary>
    /// 런타임에 영지 그리드 위 NavMesh를 저수준 NavMeshBuilder로 생성합니다.
    /// 영지 원본을 수정하지 않는 비침투(add-only) 방식입니다.
    /// </summary>
    public class TerritoryTestNavMeshBaker : MonoBehaviour
    {
        // =================================================================
        // Inspector 설정
        // =================================================================

        [Header("베이크 타이밍")]
        [Tooltip("그리드 타일이 런타임 생성되므로, Play 후 이 시간(초)만큼 대기 후 첫 생성을 실행합니다.")]
        [SerializeField] private float initialBakeDelay = 0.5f;

        [Tooltip("그리드 생성 지연 대비 재시도 횟수.")]
        [SerializeField] private int maxBakeRetries = 5;

        [Tooltip("재시도 간격(초).")]
        [SerializeField] private float retryInterval = 0.5f;

        [Header("영지 크기 (그리드 자동 감지 실패 시 사용)")]
        [Tooltip("기본 영지 한 변 크기(m). 그리드를 못 찾으면 이 정사각형으로 NavMesh를 만듭니다.")]
        [SerializeField] private float fallbackGridSize = 5f;

        [Tooltip("NavMesh 영역을 그리드보다 이만큼(m) 넉넉히 잡습니다.")]
        [SerializeField] private float areaMargin = 1f;

        [Header("바닥 높이 (타일 윗면)")]
        [Tooltip("타일 윗면(멤이 걷는 바닥) 높이를 타일 렌더러에서 자동 감지합니다. 감지 실패 시 이 값을 사용합니다. " +
                 "구버전 평면 타일=0, 신버전 두께 있는 타일=0.5")]
        [SerializeField] private float fallbackGroundY = 0.5f;

        [Header("에이전트 설정")]
        [SerializeField] private float agentRadius = 0.3f;
        [SerializeField] private float agentHeight = 1.5f;
        [SerializeField] private float agentClimb  = 0.4f;
        [SerializeField] private float agentSlope  = 45f;

        [Header("재생성 키")]
        [SerializeField] private Key rebakeKey = Key.F5;

        // =================================================================
        // 내부 상태
        // =================================================================

        private NavMeshData navData;
        private NavMeshDataInstance navInstance;
        private bool baked = false;

        /// <summary>
        /// 시설이 설치된 칸의 중심 좌표들. 이 칸들은 시설 Area(MemMovement.FacilityNavMeshArea)로 구워져
        /// 순찰 멤의 areaMask에서 제외됩니다(=순찰이 시설 칸을 통과하지 못함). 배치 멤만 밟을 수 있습니다.
        /// TerritoryFacilityTestDriver가 시설 생성 후 SetFacilityCells()로 채워줍니다.
        /// </summary>
        private readonly List<Vector3> facilityCellCenters = new List<Vector3>();

        /// <summary>시설 칸 한 변 크기(m). 그리드 셀 = 1m.</summary>
        private const float FacilityCellSize = 1f;

        /// <summary>NavMesh 생성 성공 여부.</summary>
        public bool IsReady => baked;

        // =================================================================
        // Unity Lifecycle
        // =================================================================

        private IEnumerator Start()
        {
            yield return new WaitForSeconds(initialBakeDelay);

            for (int attempt = 0; attempt <= maxBakeRetries; attempt++)
            {
                if (Bake()) yield break;
                if (attempt < maxBakeRetries)
                    yield return new WaitForSeconds(retryInterval);
            }

            Debug.LogError($"[TerritoryTestNavMeshBaker] {maxBakeRetries + 1}회 시도했지만 NavMesh 생성 실패.");
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[rebakeKey].wasPressedThisFrame)
            {
                Debug.Log("[TerritoryTestNavMeshBaker] 수동 재생성 요청.");
                Bake();
            }
        }

        private void OnDestroy()
        {
            if (navInstance.valid) navInstance.Remove();
        }

        // =================================================================
        // NavMesh 생성 (저수준 NavMeshBuilder)
        // =================================================================

        /// <summary>그리드 전체를 덮는 절차적 박스로 NavMesh를 만듭니다. 성공 시 true.</summary>
        public bool Bake()
        {
            Bounds gb = ComputeGridBounds();

            // 타일 윗면(멤이 걷는 바닥) 높이에 맞춰 굽는다.
            // 신버전 영지 타일은 두께가 있어 바닥이 y≈0.5 → y=0에 구우면 멤이 바닥에 파묻혀 보인다.
            float surfaceY = ComputeGroundSurfaceY();

            Vector3 boxSize = new Vector3(
                Mathf.Max(gb.size.x, 1f), 0.2f, Mathf.Max(gb.size.z, 1f));

            // 박스의 "윗면"이 타일 윗면과 일치하도록 중심을 내린다.
            Vector3 center = new Vector3(gb.center.x, surfaceY - boxSize.y * 0.5f, gb.center.z);

            // 걷기 가능 지형 = 그리드를 덮는 얇은 박스 하나 (콜라이더/메쉬 불필요)
            var sources = new List<NavMeshBuildSource>
            {
                new NavMeshBuildSource
                {
                    shape     = NavMeshBuildSourceShape.Box,
                    size      = boxSize,
                    transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one),
                    area      = 0 // Walkable
                }
            };

            // 시설 칸: 같은 자리에 시설 Area 박스를 겹쳐 얹는다.
            // → 이 칸들은 순찰 멤 areaMask에서 제외되어 통과 불가, 배치 멤만 진입 가능.
            //   구멍을 뚫지 않으므로 navmesh는 계속 연결됨(배회 멤이 갇히지 않음).
            // Area가 확실히 시설 Area로 칠해지도록 두 가지를 함께 보장한다:
            //   (1) 지형 박스보다 뒤에 추가(겹칠 때 나중 소스가 Area를 덮어씀),
            //   (2) 살짝 띄워(FacilityLift) 이 칸에서 "가장 위 표면"이 되게 함(복셀화가 위 표면 Area 채택).
            //   0.05m 띄움은 지형 박스와 Y로 겹쳐(두께 0.2) 하나의 연결된 표면으로 합쳐지고,
            //   agentClimb(0.4) 안이라 단차 없이 이어진다.
            const float FacilityLift = 0.05f;
            foreach (var c in facilityCellCenters)
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape     = NavMeshBuildSourceShape.Box,
                    size      = new Vector3(FacilityCellSize, boxSize.y, FacilityCellSize),
                    transform = Matrix4x4.TRS(new Vector3(c.x, center.y + FacilityLift, c.z), Quaternion.identity, Vector3.one),
                    area      = MemMovement.FacilityNavMeshArea
                });
            }

            var settings = new NavMeshBuildSettings
            {
                agentTypeID   = 0,           // Humanoid (멤 NavMeshAgent와 동일)
                agentRadius   = agentRadius,
                agentHeight   = agentHeight,
                agentClimb    = agentClimb,
                agentSlope    = agentSlope,
                minRegionArea = 0.5f,
                overrideVoxelSize = false,
                overrideTileSize  = false,
            };

            var buildBounds = new Bounds(
                center,
                new Vector3(boxSize.x + areaMargin * 2f, 6f, boxSize.z + areaMargin * 2f));

            if (navData == null) navData = new NavMeshData(settings.agentTypeID);
            NavMeshBuilder.UpdateNavMeshData(navData, settings, sources, buildBounds);

            if (!navInstance.valid)
                navInstance = NavMesh.AddNavMeshData(navData);

            // 검증
            var tri = NavMesh.CalculateTriangulation();
            int verts = tri.vertices != null ? tri.vertices.Length : 0;
            bool onGrid = NavMesh.SamplePosition(center, out NavMeshHit hit, 6f, NavMesh.AllAreas);

            if (verts > 0 && onGrid)
            {
                baked = true;
                Debug.Log($"[TerritoryTestNavMeshBaker] ✅ NavMesh 생성 완료. " +
                          $"바닥높이 y={surfaceY:0.###}, 영역 center={center} size=({boxSize.x:0.#}×{boxSize.z:0.#}), " +
                          $"정점 {verts}개, 기준 {hit.position}");
                return true;
            }

            Debug.LogWarning($"[TerritoryTestNavMeshBaker] NavMesh 생성 결과 부족: 정점 {verts}개, onGrid={onGrid}");
            return false;
        }

        /// <summary>
        /// 시설이 설치된 칸 중심 좌표들을 갱신하고 NavMesh를 다시 굽습니다.
        /// (TerritoryFacilityTestDriver가 시설 생성 후 호출)
        /// </summary>
        public void SetFacilityCells(IEnumerable<Vector3> centers)
        {
            facilityCellCenters.Clear();
            if (centers != null) facilityCellCenters.AddRange(centers);

            if (Application.isPlaying)
                Bake();
        }

        /// <summary>현재 그리드의 월드 범위(중심/크기). 소환·배회 경계 정렬에 사용. (그리드는 월드 원점 기준 생성)</summary>
        public Bounds GridWorldBounds => ComputeGridBounds();

        /// <summary>멤이 걷는 바닥(타일 윗면) 높이. 소환 위치 y 보정에 사용.</summary>
        public float GroundSurfaceY => ComputeGroundSurfaceY();

        /// <summary>
        /// 타일 윗면(걷는 바닥) 높이를 FloorContainer 타일들의 렌더러에서 자동 감지합니다.
        /// 구버전 평면 타일이면 ≈0, 신버전 두께 있는 타일이면 ≈0.5. 감지 실패 시 fallbackGroundY.
        /// </summary>
        private float ComputeGroundSurfaceY()
        {
            var floor = GameObject.Find("FloorContainer");
            if (floor != null && floor.transform.childCount > 0)
            {
                float maxY = float.NegativeInfinity;

                var renderers = floor.GetComponentsInChildren<Renderer>();
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    maxY = Mathf.Max(maxY, r.bounds.max.y);
                }

                if (!float.IsNegativeInfinity(maxY))
                    return maxY;
            }

            return fallbackGroundY;
        }

        /// <summary>
        /// 그리드 범위를 계산합니다. FloorContainer의 타일들에서 구하고, 없으면 기본 정사각형.
        /// </summary>
        private Bounds ComputeGridBounds()
        {
            var floor = GameObject.Find("FloorContainer");
            if (floor != null && floor.transform.childCount > 0)
            {
                Bounds b = new Bounds(floor.transform.GetChild(0).position, Vector3.zero);
                for (int i = 1; i < floor.transform.childCount; i++)
                    b.Encapsulate(floor.transform.GetChild(i).position);
                b.Expand(new Vector3(1f, 0f, 1f)); // 타일 중심 기준이라 양쪽 0.5칸 확장
                return b;
            }
            return new Bounds(
                new Vector3(fallbackGridSize * 0.5f, 0f, fallbackGridSize * 0.5f),
                new Vector3(fallbackGridSize, 0f, fallbackGridSize));
        }

        // =================================================================
        // 에디터 디버그
        // =================================================================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = baked ? Color.green : Color.yellow;
            Bounds gb = Application.isPlaying ? ComputeGridBounds()
                      : new Bounds(new Vector3(fallbackGridSize * 0.5f, 0f, fallbackGridSize * 0.5f),
                                   new Vector3(fallbackGridSize, 0.1f, fallbackGridSize));
            Gizmos.DrawWireCube(new Vector3(gb.center.x, 0.05f, gb.center.z),
                                new Vector3(gb.size.x, 0.1f, gb.size.z));
        }
#endif
    }
}
