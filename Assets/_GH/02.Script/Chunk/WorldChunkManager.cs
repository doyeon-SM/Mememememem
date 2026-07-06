using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
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

    public Vector2Int CurrentPlayerChunkCoord => GetChunkCoord(player != null ? player.position : Vector3.zero);

    private void Awake()
    {
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

    public void RefreshActiveChunks(bool force)
    {
        if (player == null)
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

    public Vector2Int GetChunkCoord(Vector3 worldPosition)
    {
        float size = Mathf.Max(0.01f, chunkSize);
        Vector3 localPosition = worldPosition - worldOrigin;
        int x = Mathf.FloorToInt(localPosition.x / size);
        int z = Mathf.FloorToInt(localPosition.z / size);
        return new Vector2Int(x, z);
    }

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
