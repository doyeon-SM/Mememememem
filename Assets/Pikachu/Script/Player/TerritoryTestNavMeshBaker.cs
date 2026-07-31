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

        [Tooltip("0 이상이면 자동 감지를 쓰지 않고 이 높이에 NavMesh를 굽습니다. (-1 = 자동 감지)\n" +
                 "자동 감지가 시설/장식 모델 높이에 끌려 올라갈 때 여기에 실제 바닥 높이를 넣으세요.")]
        [SerializeField] private float groundYOverride = -1f;

        [Header("에이전트 설정")]
        [SerializeField] private float agentRadius = 0.3f;
        [SerializeField] private float agentHeight = 1.5f;
        [SerializeField] private float agentClimb  = 0.4f;
        [SerializeField] private float agentSlope  = 45f;

        [Header("재생성 키")]
        [SerializeField] private Key rebakeKey = Key.F5;

        [Header("그리드 변경 자동 감지")]
        [Tooltip("타일 추가/삭제/높이 변경, 시설 설치/철거를 감지해 NavMesh를 자동으로 다시 굽습니다. " +
                 "영지 타일 위치가 런타임에 바뀌는 동안 켜 두세요.")]
        [SerializeField] private bool autoRebakeOnGridChange = true;

        [Tooltip("변경 감지 주기(초).")]
        [SerializeField] private float gridCheckInterval = 1f;

        [Tooltip("시설 칸 목록을 씬에서 매 베이크마다 자동 수집합니다. " +
                 "끄면 SetFacilityCells()로 주입한 목록만 사용합니다.")]
        [SerializeField] private bool autoCollectFacilityCells = true;

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

        /// <summary>FloorContainer 캐시. (매 프레임 GameObject.Find를 피하기 위함)</summary>
        private Transform floorContainerCache;

        /// <summary>그리드 변경 감지 타이머 / 마지막으로 구운 그리드 상태 서명.</summary>
        private float gridCheckTimer;
        private string lastBakedSignature;

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

            if (!autoRebakeOnGridChange || !baked) return;

            gridCheckTimer += Time.deltaTime;
            if (gridCheckTimer < gridCheckInterval) return;
            gridCheckTimer = 0f;

            string signature = ComputeGridSignature();
            if (signature == lastBakedSignature) return;

            Debug.Log($"[TerritoryTestNavMeshBaker] 그리드 변경 감지 → NavMesh 재생성.\n" +
                      $"  이전: {lastBakedSignature}\n  현재: {signature}");
            Bake();
        }

        /// <summary>
        /// 그리드의 현재 상태를 한 줄 문자열로 요약합니다. 이 값이 바뀌면 NavMesh를 다시 굽습니다.
        /// (타일 수 / 범위 / 바닥 높이 / 시설 칸 수 - 이 중 하나라도 변하면 NavMesh가 어긋납니다)
        /// </summary>
        private string ComputeGridSignature()
        {
            // [비용 주의] 이 함수는 gridCheckInterval마다 호출된다.
            // 타일 렌더러 스캔이나 FindObjectsByType은 절대 넣지 말 것.
            //
            // GridManager는 타일과 시설을 모두 FloorContainer의 자식으로 만든다.
            // (GridManager.SpawnTile / 건물 Instantiate 모두 floorContainer를 부모로 지정)
            // → 타일 확장도, 시설 설치·철거도 childCount 변화로 감지된다.
            Transform floor = GetFloorContainer();
            int childCount = floor != null ? floor.childCount : 0;

            Bounds gb = ComputeGridBounds(); // 자식 Transform 위치만 읽는다(렌더러 접근 없음)

            return $"children={childCount} " +
                   $"center=({gb.center.x:0.##},{gb.center.z:0.##}) size=({gb.size.x:0.##},{gb.size.z:0.##})";
        }

        /// <summary>FloorContainer를 캐시해서 반환합니다. (파괴/재생성 시 다시 찾음)</summary>
        private Transform GetFloorContainer()
        {
            if (floorContainerCache == null)
            {
                var floor = GameObject.Find("FloorContainer");
                floorContainerCache = floor != null ? floor.transform : null;
            }
            return floorContainerCache;
        }

        /// <summary>씬의 생산 시설 7종 위치를 시설 칸 중심으로 수집합니다.</summary>
        private List<Vector3> CollectFacilityCells()
        {
            var centers = new List<Vector3>();

            foreach (var f in FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None))
                if (f != null) centers.Add(f.transform.position);
            foreach (var c in FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None))
                if (c != null) centers.Add(c.transform.position);
            foreach (var k in FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None))
                if (k != null) centers.Add(k.transform.position);
            foreach (var cf in FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None))
                if (cf != null) centers.Add(cf.transform.position);
            foreach (var g in FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None))
                if (g != null) centers.Add(g.transform.position);
            foreach (var r in FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None))
                if (r != null) centers.Add(r.transform.position);
            foreach (var t in FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None))
                if (t != null) centers.Add(t.transform.position);

            return centers;
        }

        /// <summary>
        /// NavMesh가 다시 구워지면 이미 소환된 멤들을 새 NavMesh 표면 위로 다시 올립니다.
        /// (바닥 높이나 시설 칸이 바뀌면 기존 멤이 NavMesh 밖으로 밀려나 이동 불능이 됩니다)
        /// </summary>
        private void ResnapActiveMems()
        {
            int n = 0;
            foreach (var movement in FindObjectsByType<MemMovement>(FindObjectsSortMode.None))
            {
                if (movement == null || !movement.isActiveAndEnabled) continue;
                if (movement.Warp(movement.transform.position)) n++;
            }

            if (n > 0) Debug.Log($"[TerritoryTestNavMeshBaker] 소환된 멤 {n}마리를 새 NavMesh 위로 재배치.");
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
            // 시설이 런타임에 설치/철거되므로 매 베이크마다 현재 시설 목록을 다시 수집한다.
            if (autoCollectFacilityCells)
            {
                facilityCellCenters.Clear();
                facilityCellCenters.AddRange(CollectFacilityCells());
            }

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
            //
            // [중요] 윗면 높이를 지형 박스와 "정확히" 맞춘다.
            // 예전엔 Area가 확실히 칠해지도록 0.05m 띄웠는데, 그 높이가 복셀 한 칸과 비슷해
            // 시설 칸 경계에 미세한 단차가 생겼다. 순찰 멤이 그 경계를 지날 때 위/아래 폴리곤에
            // 번갈아 매핑되면서(위쪽은 areaMask에서 빠진 시설 Area) 제자리에서 덜덜 떨렸다.
            // → 이제 윗면을 같게 하고 두께만 아래로 늘려, 같은 복셀 열에서 나중 소스가
            //   Area를 덮어쓰게 한다. 단차가 없으므로 경계에서 떨리지 않는다.
            const float FacilityThickness = 0.4f;
            foreach (var c in facilityCellCenters)
            {
                sources.Add(new NavMeshBuildSource
                {
                    shape     = NavMeshBuildSourceShape.Box,
                    size      = new Vector3(FacilityCellSize, FacilityThickness, FacilityCellSize),
                    transform = Matrix4x4.TRS(
                        new Vector3(c.x, surfaceY - FacilityThickness * 0.5f, c.z),
                        Quaternion.identity, Vector3.one),
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
                bool wasBaked = baked;
                baked = true;
                lastBakedSignature = ComputeGridSignature();

                Debug.Log($"[TerritoryTestNavMeshBaker] ✅ NavMesh 생성 완료. " +
                          $"바닥높이 y={surfaceY:0.###}, 영역 center={center} size=({boxSize.x:0.#}×{boxSize.z:0.#}), " +
                          $"시설 칸 {facilityCellCenters.Count}개, 정점 {verts}개, 기준 {hit.position}");

                // 재생성이면 이미 소환된 멤들을 새 표면 위로 올려준다.
                if (wasBaked) ResnapActiveMems();

                return true;
            }

            Debug.LogWarning($"[TerritoryTestNavMeshBaker] NavMesh 생성 결과 부족: 정점 {verts}개, onGrid={onGrid}");
            return false;
        }

        /// <summary>
        /// 시설이 설치된 칸 중심 좌표들을 갱신하고 NavMesh를 다시 굽습니다.
        /// (TerritoryFacilityTestDriver가 시설 생성 후 호출)
        ///
        /// [주의] autoCollectFacilityCells가 켜져 있으면 Bake()가 씬에서 다시 수집하므로
        ///        여기서 넘긴 목록은 덮어써집니다. 직접 지정한 목록만 쓰려면 그 옵션을 끄세요.
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
        ///
        /// [주의] 타일 전체 렌더러의 max Y를 쓰면 안 된다.
        /// 타일 위에 올라간 시설/장식 모델(높이 2m 이상)까지 포함되어 NavMesh가 공중에 떠버린다.
        /// → 타일 1개당 윗면 높이를 구한 뒤 그 "중앙값"을 쓴다. 대부분의 타일은 비어 있으므로
        ///   시설이 올라간 소수의 타일에 휘둘리지 않는다.
        /// </summary>
        private float ComputeGroundSurfaceY()
        {
            if (groundYOverride >= 0f) return groundYOverride;

            Transform floor = GetFloorContainer();
            if (floor == null || floor.childCount == 0) return fallbackGroundY;

            // 1순위: 시설 오브젝트의 y.
            //   GridManager가 시설을 정확히 innerPlaneY(= 타일 윗면)에 배치하므로 가장 정확한 값이다.
            //   타일 프리팹이 바뀌어도(두께 변경) 시설 배치 높이가 곧 바닥이라 자동으로 따라간다.
            var buildingYs = new List<float>();
            foreach (var building in floor.GetComponentsInChildren<BuildingRuntime>(true))
                if (building != null) buildingYs.Add(building.transform.position.y);

            if (buildingYs.Count > 0)
            {
                buildingYs.Sort();
                return buildingYs[buildingYs.Count / 2];
            }

            // 2순위: 시설이 하나도 없으면 타일 렌더러의 윗면.
            //   타일 1개당 윗면을 구해 중앙값을 쓴다. (타일 위에 올라간 모델에 휘둘리지 않도록)
            var tileTops = new List<float>(floor.childCount);

            for (int i = 0; i < floor.childCount; i++)
            {
                Transform child = floor.GetChild(i);

                // 시설/그리드 오버레이는 바닥이 아니다.
                if (child.GetComponent<BuildingRuntime>() != null) continue;
                if (child.name == "GlobalGridOverlay") continue;

                float top = float.NegativeInfinity;
                foreach (var r in child.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    top = Mathf.Max(top, r.bounds.max.y);
                }

                if (!float.IsNegativeInfinity(top)) tileTops.Add(top);
            }

            if (tileTops.Count == 0) return fallbackGroundY;

            tileTops.Sort();
            return tileTops[tileTops.Count / 2];
        }

        /// <summary>
        /// 그리드 범위를 계산합니다. FloorContainer의 타일들에서 구하고, 없으면 기본 정사각형.
        /// </summary>
        private Bounds ComputeGridBounds()
        {
            Transform floor = GetFloorContainer();
            if (floor != null && floor.childCount > 0)
            {
                Bounds b = new Bounds(floor.GetChild(0).position, Vector3.zero);
                for (int i = 1; i < floor.childCount; i++)
                    b.Encapsulate(floor.GetChild(i).position);
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
