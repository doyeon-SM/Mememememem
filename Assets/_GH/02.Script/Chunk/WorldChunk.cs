using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class WorldChunk : MonoBehaviour
{
    
    [Header("Chunk")]
    [SerializeField] private Vector2Int coord;
    [SerializeField] private float chunkSize = 50f;
    [SerializeField] private Vector3 worldOrigin;

    [Header("Validation")]
    [SerializeField] private bool validateChildren = true;
    [SerializeField] private bool showGizmos = true;
    [Tooltip("청크를 선택하지 않아도 범위를 벗어난 자식이 있으면 오류 기즈모를 표시합니다.")]
    [SerializeField] private bool showErrorGizmosWhenUnselected = true;
    [SerializeField] private Color normalColor = new Color(0.25f, 0.85f, 1f, 0.8f);
    [SerializeField] private Color errorColor = new Color(1f, 0.15f, 0.1f, 0.9f);

    public Vector2Int Coord => coord;
    public float ChunkSize => chunkSize;
    public Bounds Bounds => CalculateBounds(coord, chunkSize, worldOrigin);
    
    public bool HasOutOfBoundsChildren => GetOutOfBoundsChildren(s_CachedOutOfBounds) > 0;

    private static readonly List<Transform> s_CachedOutOfBounds = new List<Transform>();
    private static readonly List<Transform> s_CachedChildren = new List<Transform>();

    public void Configure(float newChunkSize, Vector3 newWorldOrigin)
    {
        chunkSize = Mathf.Max(0.01f, newChunkSize);
        worldOrigin = newWorldOrigin;
    }

    public void SetCoord(Vector2Int newCoord)
    {
        coord = newCoord;
    }
    /// <summary>
    /// 청크 밖으로 오브젝트가 나갔는지 ( 배치 오류 방지용 )
    /// </summary>
    public int GetOutOfBoundsChildren(List<Transform> results)
    {
        if (results == null)
        {
            return 0;
        }

        results.Clear();

        if (!validateChildren)
        {
            return 0;
        }

        Bounds chunkBounds = Bounds;
        s_CachedChildren.Clear();
        GetComponentsInChildren(true, s_CachedChildren);

        foreach (Transform child in s_CachedChildren)
        {
            if (child == transform)
            {
                continue;
            }

            Vector3 position = child.position;
            bool insideX = position.x >= chunkBounds.min.x && position.x <= chunkBounds.max.x;
            bool insideZ = position.z >= chunkBounds.min.z && position.z <= chunkBounds.max.z;

            if (!insideX || !insideZ)
            {
                results.Add(child);
            }
        }

        return results.Count;
    }
    /// <summary>
    /// 실제 월드 영역 계산
    /// </summary>
    public static Bounds CalculateBounds(Vector2Int chunkCoord, float size, Vector3 origin)
    {
        size = Mathf.Max(0.01f, size);
        Vector3 min = origin + new Vector3(chunkCoord.x * size, 0f, chunkCoord.y * size);
        Vector3 center = min + new Vector3(size * 0.5f, 0f, size * 0.5f);
        return new Bounds(center, new Vector3(size, 0.05f, size));
    }

    private void OnValidate()
    {
        chunkSize = Mathf.Max(0.01f, chunkSize);
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos || !showErrorGizmosWhenUnselected || !HasOutOfBoundsChildren)
        {
            return;
        }

        // 정상 청크는 선택했을 때만 보이고, 배치 오류가 있는 청크만 항상 표시합니다.
        DrawChunkGizmos(errorColor, true);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        bool hasOutOfBoundsChildren = HasOutOfBoundsChildren;

        // 오류 청크는 OnDrawGizmos에서 이미 표시했으므로 선택 시 중복으로 그리지 않습니다.
        if (showErrorGizmosWhenUnselected && hasOutOfBoundsChildren)
        {
            return;
        }

        DrawChunkGizmos(hasOutOfBoundsChildren ? errorColor : normalColor, true);
    }

    public void DrawChunkGizmos(Color color, bool drawLabel)
    {
        Bounds bounds = Bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        Gizmos.color = color;
        Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
        if (drawLabel)
        {
            Handles.color = color;
            Handles.Label(center + Vector3.up * 0.25f, $"{name}\n{coord}");
        }
#endif

        if (!validateChildren)
        {
            return;
        }

        s_CachedOutOfBounds.Clear();
        GetOutOfBoundsChildren(s_CachedOutOfBounds);

        if (s_CachedOutOfBounds.Count == 0)
        {
            return;
        }

        Gizmos.color = errorColor;

        foreach (Transform child in s_CachedOutOfBounds)
        {
            if (child == null)
            {
                continue;
            }

            Gizmos.DrawLine(center, child.position);
            Gizmos.DrawWireSphere(child.position, Mathf.Max(0.25f, chunkSize * 0.015f));

#if UNITY_EDITOR
            Handles.color = errorColor;
            Handles.Label(child.position + Vector3.up * 0.35f, $"Out of chunk\n{child.name}");
#endif
        }
    }
}
