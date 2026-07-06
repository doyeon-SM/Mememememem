using UnityEngine;
using UnityEngine.Serialization;

public class WayPointStone : MonoBehaviour
{
    [Header("WayPoint")]
    [SerializeField] private WayPointDefinition definition;

    [Header("Spawn")]
    [Tooltip("If assigned, the player is moved to this transform position.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Used only when Spawn Point is not assigned.")]
    [FormerlySerializedAs("spawnPoistion")]
    [SerializeField] private Vector3 fallbackSpawnPosition;
    [SerializeField] private bool useFallbackSpawnPosition;

    [Header("Visual")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material unlockedMaterial;
    [SerializeField] private GameObject lockedVisual;
    [SerializeField] private GameObject unlockedVisual;

    [Header("State")]
    [SerializeField] private bool isUnlocked;

    public WayPointDefinition Definition => definition;
    public string Id => definition != null ? definition.id : string.Empty;
    public bool IsUnlocked => isUnlocked;
    private void OnEnable()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.RegisterStone(this);
        }
    }

    private void OnDisable()
    {
        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.UnregisterStone(this);
        }
    }
    public Vector3 SpawnPosition
    {
        get
        {
            if (spawnPoint != null)
            {
                return spawnPoint.position;
            }

            if (useFallbackSpawnPosition)
            {
                return fallbackSpawnPosition;
            }

            return transform.position;
        }
    }

    private void Awake()
    {
        RefreshVisual();
    }

    private void OnValidate()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        RefreshVisual();
    }

    public void SetUnlockedVisual(bool unlocked)
    {
        isUnlocked = unlocked;
        RefreshVisual();
    }

    public void SetActive(bool active)
    {
        SetUnlockedVisual(active);
    }

    public void SetDefinition(WayPointDefinition newDefinition)
    {
        definition = newDefinition;
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }

    private void RefreshVisual()
    {
        if (targetRenderer != null)
        {
            Material targetMaterial = isUnlocked ? unlockedMaterial : lockedMaterial;
            if (targetMaterial != null)
            {
                targetRenderer.sharedMaterial = targetMaterial;
            }
        }

        if (lockedVisual != null)
        {
            lockedVisual.SetActive(!isUnlocked);
        }

        if (unlockedVisual != null)
        {
            unlockedVisual.SetActive(isUnlocked);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 position = SpawnPosition;

        Gizmos.color = isUnlocked ? Color.cyan : Color.gray;
        Gizmos.DrawWireSphere(position, 0.35f);
        Gizmos.DrawLine(transform.position, position);
    }
}
