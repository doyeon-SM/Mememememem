// ============================================================================
// TerritoryGridRuntimeDrawer.cs
// 영지 테스트 씬 전용 — 런타임 생성되는 그리드를 "플레이 중 Game 뷰"에 실제로 그려줍니다.
//
// [왜 필요한가]
// TerritoryGridGizmo는 에디터 Scene 뷰에만 보이는 기즈모라, 플레이(Game 뷰) 화면에는
// 안 나옵니다. 멤이 시설이 설치된 그리드 칸 "안"으로 들어가 작업하는지 정확히 눈으로
// 확인하려면, 플레이 중에도 그리드가 화면에 보여야 합니다.
// 이 컴포넌트는 LineRenderer(URP에서 Game 뷰 렌더링됨)로 그리드 라인을 그립니다.
//
// [사용법]
// 1. 영지 테스트 씬에 빈 GameObject 생성 → 이 컴포넌트 부착 (position 무관).
// 2. 플레이하면 런타임 그리드(FloorContainer 타일 기준)가 화면에 그려집니다.
// 3. 타일을 못 찾으면 fallbackGridSize(기본 5×5)로 그립니다.
// 4. 그리드가 확장(레벨업)되면 autoRefreshSeconds 주기로 자동 갱신됩니다.
//
// ※ 순수 시각화 도구입니다. 게임 로직에 영향을 주지 않으며 삭제해도 무방합니다.
//    (다른 담당자 파일 수정 없음 — FloorContainer는 읽기만 함)
// ============================================================================

using System.Collections;
using UnityEngine;

namespace Pikachu.Test
{
    /// <summary>
    /// 영지 런타임 그리드를 플레이 중 Game 뷰에 LineRenderer로 그려주는 시각화 전용 컴포넌트.
    /// </summary>
    [AddComponentMenu("Pikachu/Test/Territory Grid Runtime Drawer")]
    public class TerritoryGridRuntimeDrawer : MonoBehaviour
    {
        [Header("그리드 규격")]
        [Tooltip("타일 한 칸의 크기(m). GridManager 기준 1m.")]
        [SerializeField] private float cellSize = 1f;

        [Header("자동 감지")]
        [Tooltip("FloorContainer의 런타임 타일에서 그리드 범위를 자동으로 읽습니다.")]
        [SerializeField] private bool autoDetectFromTiles = true;

        [Tooltip("타일 자동 감지 실패 시 사용할 한 변 칸 수 (기본 영지=5).")]
        [SerializeField] private int fallbackGridSize = 5;

        [Tooltip("자동 감지 실패 시 사용할 그리드 원점(좌하단 코너).")]
        [SerializeField] private Vector3 fallbackOrigin = Vector3.zero;

        [Tooltip("씬 시작 후 타일이 생성될 때까지 최대 대기 시간(초).")]
        [SerializeField] private float detectTimeout = 5f;

        [Tooltip("그리드 확장(레벨업)을 반영해 이 주기(초)로 다시 그립니다. 0이면 안 함.")]
        [SerializeField] private float autoRefreshSeconds = 2f;

        [Header("외형")]
        [Tooltip("그리드 라인 색상.")]
        [SerializeField] private Color lineColor = new Color(0.2f, 0.9f, 1f, 0.9f);

        [Tooltip("라인 두께(m).")]
        [SerializeField] private float lineWidth = 0.03f;

        [Tooltip("바닥보다 살짝 띄워 라인이 묻히지 않게 합니다.")]
        [SerializeField] private float yOffset = 0.03f;

        [Tooltip("각 칸 중심에 십자 마커를 표시 (시설/멤 위치 확인용).")]
        [SerializeField] private bool showCellCenters = true;

        [Tooltip("칸 중심 마커 색상.")]
        [SerializeField] private Color cellCenterColor = new Color(1f, 0.9f, 0.2f, 1f);

        private Transform container;
        private Material lineMat;

        // 마지막으로 그린 그리드 상태 (변화 감지용)
        private int lastCountX = -1;
        private int lastCountZ = -1;
        private Vector3 lastOrigin = new Vector3(float.NaN, 0f, 0f);

        private void OnEnable()
        {
            StartCoroutine(RunLoop());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (container != null) Destroy(container.gameObject);
            if (lineMat != null) Destroy(lineMat);
            container = null;
            lineMat = null;
        }

        private IEnumerator RunLoop()
        {
            EnsureMaterial();

            // 그리드 타일은 GridManager.Start()에서 런타임 생성되므로, 생길 때까지 잠깐 대기.
            float t = 0f;
            while (autoDetectFromTiles && !TilesReady() && t < detectTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }

            Redraw();

            // 그리드 확장 등으로 규격이 바뀌면 다시 그린다.
            while (autoRefreshSeconds > 0f)
            {
                yield return new WaitForSeconds(autoRefreshSeconds);
                if (GridChanged()) Redraw();
            }
        }

        // ---------------------------------------------------------------
        // 그리드 범위 계산 (NavMesh 베이커와 동일하게 FloorContainer 타일에서 감지)
        // ---------------------------------------------------------------

        private bool TilesReady()
        {
            var floor = GameObject.Find("FloorContainer");
            return floor != null && floor.transform.childCount > 0;
        }

        private void GetGrid(out Vector3 origin, out int countX, out int countZ)
        {
            if (autoDetectFromTiles)
            {
                var floor = GameObject.Find("FloorContainer");
                if (floor != null && floor.transform.childCount > 0)
                {
                    Bounds b = new Bounds(floor.transform.GetChild(0).position, Vector3.zero);
                    for (int i = 1; i < floor.transform.childCount; i++)
                        b.Encapsulate(floor.transform.GetChild(i).position);

                    // 타일 "중심" 기준이라 양쪽으로 반 칸씩 확장 → 실제 칸 경계와 일치.
                    b.Expand(new Vector3(cellSize, 0f, cellSize));

                    origin = new Vector3(b.min.x, 0f, b.min.z);
                    countX = Mathf.Max(1, Mathf.RoundToInt(b.size.x / cellSize));
                    countZ = Mathf.Max(1, Mathf.RoundToInt(b.size.z / cellSize));
                    return;
                }
            }

            origin = fallbackOrigin;
            countX = countZ = Mathf.Max(1, fallbackGridSize);
        }

        private bool GridChanged()
        {
            GetGrid(out Vector3 origin, out int cx, out int cz);
            return cx != lastCountX || cz != lastCountZ
                || (origin - lastOrigin).sqrMagnitude > 0.0001f;
        }

        // ---------------------------------------------------------------
        // 그리기
        // ---------------------------------------------------------------

        private void Redraw()
        {
            EnsureContainer();

            // 기존 라인 제거
            for (int i = container.childCount - 1; i >= 0; i--)
                Destroy(container.GetChild(i).gameObject);

            GetGrid(out Vector3 origin, out int countX, out int countZ);
            lastCountX = countX;
            lastCountZ = countZ;
            lastOrigin = origin;

            float y = yOffset;
            float spanX = countX * cellSize;
            float spanZ = countZ * cellSize;

            // 세로선 (Z 방향으로 뻗음, X 고정)
            for (int i = 0; i <= countX; i++)
            {
                float x = origin.x + i * cellSize;
                AddLine(new Vector3(x, y, origin.z),
                        new Vector3(x, y, origin.z + spanZ),
                        lineColor, lineWidth);
            }

            // 가로선 (X 방향으로 뻗음, Z 고정)
            for (int j = 0; j <= countZ; j++)
            {
                float z = origin.z + j * cellSize;
                AddLine(new Vector3(origin.x, y, z),
                        new Vector3(origin.x + spanX, y, z),
                        lineColor, lineWidth);
            }

            // 각 칸 중심 십자 마커
            if (showCellCenters)
            {
                float arm = cellSize * 0.15f;
                for (int x = 0; x < countX; x++)
                {
                    for (int z = 0; z < countZ; z++)
                    {
                        Vector3 c = new Vector3(
                            origin.x + (x + 0.5f) * cellSize, y,
                            origin.z + (z + 0.5f) * cellSize);
                        AddLine(c + new Vector3(-arm, 0, 0), c + new Vector3(arm, 0, 0),
                                cellCenterColor, lineWidth);
                        AddLine(c + new Vector3(0, 0, -arm), c + new Vector3(0, 0, arm),
                                cellCenterColor, lineWidth);
                    }
                }
            }
        }

        private void AddLine(Vector3 a, Vector3 b, Color col, float width)
        {
            var go = new GameObject("gridline");
            go.transform.SetParent(container, false);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = lineMat;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = col;
            lr.endColor = col;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }

        private void EnsureContainer()
        {
            if (container != null) return;
            var go = new GameObject("GridLines");
            go.transform.SetParent(transform, false);
            container = go.transform;
        }

        private void EnsureMaterial()
        {
            if (lineMat != null) return;

            // 파이프라인 무관하게 라인이 보이는 셰이더 선택 (URP/빌트인 모두 커버).
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");

            lineMat = new Material(sh) { name = "TerritoryGridLineMat" };
        }
    }
}
