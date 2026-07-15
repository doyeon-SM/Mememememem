using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
/// <summary>
/// 플레이어의 현재 청크 좌표를 기준으로 주변 <see cref="WorldChunk"/> 루트의 활성 상태를 관리합니다.
/// 청크 비활성화 중 자식 MonoBehaviour의 Update는 실행되지 않으므로 시간 기반 상태는 절대시간으로 복구해야 합니다.
/// </summary>
public class WorldChunkManager : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private Transform player;
    [SerializeField] private Transform worldChunksRoot;

    [Header("Chunk Settings")]
    [SerializeField] private float chunkSize = 50f;
    [SerializeField] private int activeRange = 1;
    [SerializeField] private Vector3 worldOrigin;
    [SerializeField] private bool updateInEditMode;

    [Header("Gizmos")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private Color inactiveColor = new Color(0.45f, 0.45f, 0.45f, 0.35f);
    [SerializeField] private Color activeColor = new Color(0.1f, 0.85f, 0.25f, 0.8f);
    [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color errorColor = new Color(1f, 0.1f, 0.1f, 1f);

    private readonly Dictionary<Vector2Int, WorldChunk> chunksByCoord = new Dictionary<Vector2Int, WorldChunk>();
    private readonly List<WorldChunk> chunks = new List<WorldChunk>();
    private Vector2Int lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

    /// <summary>현재 플레이어 위치를 기준으로 계산한 청크 좌표입니다.</summary>
    public Vector2Int CurrentPlayerChunkCoord => GetChunkCoord(player != null ? player.position : Vector3.zero);

    private void Awake()
    {
        ResolvePlayerReference();
        CollectChunks();
    }

    private void Start()
    {
        RefreshActiveChunks(true);
    }

    private void Update()
    {
        if (!Application.isPlaying && !updateInEditMode)
        {
            return;
        }

        RefreshActiveChunks(false);
    }

    /// <summary>설정된 루트 아래의 모든 청크를 다시 수집하고 좌표별 조회 테이블을 구성합니다.</summary>
    public void CollectChunks()
    {
        chunks.Clear();
        chunksByCoord.Clear();

        Transform searchRoot = worldChunksRoot != null ? worldChunksRoot : transform;
        searchRoot.GetComponentsInChildren<WorldChunk>(true, chunks);

        foreach (WorldChunk chunk in chunks)
        {
            if (chunk == null)
            {
                continue;
            }

            chunk.Configure(chunkSize, worldOrigin);

            if (chunksByCoord.ContainsKey(chunk.Coord))
            {
                Debug.LogWarning($"[WorldChunkManager] Duplicate chunk coord {chunk.Coord}: {chunk.name}", chunk);
                continue;
            }

            chunksByCoord.Add(chunk.Coord, chunk);
        }
    }

    /// <summary>플레이어 주변 활성 범위를 즉시 다시 계산합니다.</summary>
    /// <param name="force">참이면 플레이어 청크가 바뀌지 않았어도 모든 청크 상태를 다시 적용합니다.</param>
    public void RefreshActiveChunks(bool force)
    {
        if (!ResolvePlayerReference())
        {
            return;
        }

        if (chunks.Count == 0)
        {
            CollectChunks();
        }

        Vector2Int currentCoord = GetChunkCoord(player.position);
        if (!force && currentCoord == lastPlayerChunkCoord)
        {
            return;
        }

        lastPlayerChunkCoord = currentCoord;

        foreach (WorldChunk chunk in chunks)
        {
            if (chunk == null)
            {
                continue;
            }

            bool shouldBeActive = IsWithinActiveRange(currentCoord, chunk.Coord);
            if (chunk.gameObject.activeSelf != shouldBeActive)
            {
                chunk.gameObject.SetActive(shouldBeActive);
            }
        }
    }

    /// <summary>런타임에 교체된 플레이어를 명시적으로 연결하고 청크를 즉시 갱신합니다.</summary>
    public void SetPlayer(Transform newPlayer, bool refreshImmediately = true)
    {
        player = newPlayer;
        lastPlayerChunkCoord = new Vector2Int(int.MinValue, int.MinValue);

        if (refreshImmediately && player != null)
        {
            RefreshActiveChunks(true);
        }
    }

    private bool ResolvePlayerReference()
    {
        player = PlayerReferenceResolver.ResolveTransform(player);
        return player != null;
    }

    /// <summary>월드 좌표를 청크 좌표로 변환합니다.</summary>
    public Vector2Int GetChunkCoord(Vector3 worldPosition)
    {
        float size = Mathf.Max(0.01f, chunkSize);
        Vector3 localPosition = worldPosition - worldOrigin;
        int x = Mathf.FloorToInt(localPosition.x / size);
        int z = Mathf.FloorToInt(localPosition.z / size);
        return new Vector2Int(x, z);
    }

    /// <summary>대상 청크가 중심 청크의 설정된 활성 반경 안에 있는지 확인합니다.</summary>
    public bool IsWithinActiveRange(Vector2Int centerCoord, Vector2Int targetCoord)
    {
        int distanceX = Mathf.Abs(targetCoord.x - centerCoord.x);
        int distanceZ = Mathf.Abs(targetCoord.y - centerCoord.y);
        return Mathf.Max(distanceX, distanceZ) <= activeRange;
    }

    private void OnValidate()
    {
        chunkSize = Mathf.Max(0.01f, chunkSize);
        activeRange = Mathf.Max(0, activeRange);

        if (!Application.isPlaying)
        {
            CollectChunks();
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos)
        {
            return;
        }

        if (chunks.Count == 0)
        {
            CollectChunks();
        }

        Vector2Int currentCoord = player != null ? GetChunkCoord(player.position) : lastPlayerChunkCoord;

        foreach (WorldChunk chunk in chunks)
        {
            if (chunk == null)
            {
                continue;
            }

            Color color = inactiveColor;

            if (chunk.HasOutOfBoundsChildren)
            {
                color = errorColor;
            }
            else if (player != null && chunk.Coord == currentCoord)
            {
                color = currentColor;
            }
            else if (player != null && IsWithinActiveRange(currentCoord, chunk.Coord))
            {
                color = activeColor;
            }

            chunk.Configure(chunkSize, worldOrigin);
            chunk.DrawChunkGizmos(color, false);
        }

        if (player != null)
        {
            DrawActiveRangeGizmo(currentCoord);
        }
    }

    private void DrawActiveRangeGizmo(Vector2Int currentCoord)
    {
        int rangeSize = activeRange * 2 + 1;
        float size = Mathf.Max(0.01f, chunkSize);
        Vector2Int minCoord = currentCoord - new Vector2Int(activeRange, activeRange);
        Vector3 min = worldOrigin + new Vector3(minCoord.x * size, 0.08f, minCoord.y * size);
        Vector3 center = min + new Vector3(rangeSize * size * 0.5f, 0f, rangeSize * size * 0.5f);

        Gizmos.color = currentColor;
        Gizmos.DrawWireCube(center, new Vector3(rangeSize * size, 0.08f, rangeSize * size));

#if UNITY_EDITOR
        Handles.color = currentColor;
        Handles.Label(center + Vector3.up * 0.5f, $"Active Range {activeRange}\nCurrent {currentCoord}");
#endif
    }
}
