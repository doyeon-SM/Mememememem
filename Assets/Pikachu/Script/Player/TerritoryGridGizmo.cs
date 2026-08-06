// ============================================================================
// TerritoryGridGizmo.cs
// 영지 테스트 씬 전용 — 런타임 생성되는 그리드를 에디터 Scene 뷰에 미리 그려주는 기즈모
//
// [목적]
// 영지 그리드 타일은 GridManager.Start()에서 런타임 생성되므로 에디터에서는 안 보입니다.
// 그래서 시설 프리팹을 어디에 놓아야 할지 좌표를 가늠하기 어렵습니다.
// 이 컴포넌트는 그리드의 위치/타일 중심/좌표를 Scene 뷰에 그려줘서
// 시설 프리팹을 정확한 타일 위에 올려놓을 수 있게 도와줍니다.
//
// [사용법]
// 1. 빈 GameObject 생성 → "TerritoryGridGizmo" → 이 컴포넌트 부착 (position은 무관).
// 2. Scene 뷰에 5×5 그리드와 각 타일 중심(노란 점), 좌표 라벨이 표시됩니다.
// 3. 시설 프리팹을 씬에 드래그한 뒤, Inspector의 Transform Position을
//    원하는 타일 중심 좌표(예: 2.5, 0, 2.5)로 입력하세요.
// 4. 그리드가 확장(레벨업)됐다면 gridSize 값을 실제 currentGridSize에 맞추세요.
//
// ※ 순수 에디터 시각화 도구입니다. 게임 로직에 전혀 영향을 주지 않으며, 삭제해도 무방합니다.
// ============================================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pikachu.Test
{
    /// <summary>
    /// 영지 그리드(원점 기준 N×N, 셀 1m)를 에디터 Scene 뷰에 그려주는 시각화 전용 컴포넌트.
    /// </summary>
    [ExecuteAlways]
    public class TerritoryGridGizmo : MonoBehaviour
    {
        [Header("그리드 규격 (GridManager와 일치시키세요)")]
        [Tooltip("그리드 원점 (GridManager는 월드 원점 0,0,0 사용).")]
        [SerializeField] private Vector3 origin = Vector3.zero;

        [Tooltip("한 변의 타일 수. 기본 영지=5, 확장 시 currentGridSize에 맞추세요.")]
        [SerializeField] private int gridSize = 5;

        [Tooltip("타일 한 칸의 크기(m). GridManager 기준 1m.")]
        [SerializeField] private float cellSize = 1f;

        [Header("표시 옵션")]
        [Tooltip("각 타일 중심에 점을 표시 (시설 배치 좌표).")]
        [SerializeField] private bool showCellCenters = true;

        [Tooltip("타일 중심 좌표 라벨 표시.")]
        [SerializeField] private bool showCoordinateLabels = true;

        [Tooltip("NavMesh 실제 보행 영역(agentRadius만큼 안쪽) 표시.")]
        [SerializeField] private bool showWalkableArea = true;

        [Tooltip("NavMesh 베이크 agentRadius (기본 0.5m). 보행 영역 계산용.")]
        [SerializeField] private float agentRadius = 0.5f;

        private void OnDrawGizmos()
        {
            if (gridSize <= 0 || cellSize <= 0f) return;

            float span = gridSize * cellSize;
            Vector3 o = origin;

            // 1) 그리드 셀 라인 (연한 청록)
            Gizmos.color = new Color(0.3f, 0.8f, 0.9f, 0.6f);
            for (int i = 0; i <= gridSize; i++)
            {
                float off = i * cellSize;
                // Z 방향 라인 (X 고정)
                Gizmos.DrawLine(o + new Vector3(off, 0, 0), o + new Vector3(off, 0, span));
                // X 방향 라인 (Z 고정)
                Gizmos.DrawLine(o + new Vector3(0, 0, off), o + new Vector3(span, 0, off));
            }

            // 2) 외곽 테두리 강조 (진한 청록)
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 1f);
            Vector3 c = o + new Vector3(span * 0.5f, 0, span * 0.5f);
            Gizmos.DrawWireCube(c, new Vector3(span, 0.02f, span));

            // 3) NavMesh 보행 영역 (agentRadius 안쪽) — 실제로 멤이 밟는 범위
            if (showWalkableArea && span - 2f * agentRadius > 0f)
            {
                Gizmos.color = new Color(0.3f, 1f, 0.4f, 0.9f);
                Gizmos.DrawWireCube(c, new Vector3(span - 2f * agentRadius, 0.02f, span - 2f * agentRadius));
            }

            // 4) 타일 중심점 + 좌표 라벨
            if (showCellCenters)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.2f, 1f);
                for (int x = 0; x < gridSize; x++)
                {
                    for (int z = 0; z < gridSize; z++)
                    {
                        Vector3 center = o + new Vector3((x + 0.5f) * cellSize, 0, (z + 0.5f) * cellSize);
                        Gizmos.DrawSphere(center, cellSize * 0.06f);

#if UNITY_EDITOR
                        if (showCoordinateLabels)
                        {
                            Handles.color = Color.white;
                            Handles.Label(center + new Vector3(0, 0.05f, 0),
                                          $"({center.x:0.#},{center.z:0.#})");
                        }
#endif
                    }
                }
            }
        }
    }
}
