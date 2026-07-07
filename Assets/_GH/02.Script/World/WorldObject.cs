using KGH.Data;
using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField] private ObjectType myType;
    [SerializeField] private ObjectDropItem dropItem;
    [SerializeField] private int maxObjectHp = 1;
    [SerializeField] private int currentObjectHp;

    [Header("Drop Spawn")]
    [SerializeField] private Transform dropSpawnPoint;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float dropSpawnHeight = 0.02f;
    [SerializeField] private float dropSpreadRadius = 1.1f;
    [SerializeField] private float groundRaycastHeight = 3f;
    [SerializeField] private float groundRaycastDistance = 8f;
    [SerializeField] private int maxDropVisualCount = 8;
    [SerializeField] private bool destroyObjectWhenDepleted = true;

    [Header("Drop Pool")]
    [SerializeField] private int poolPrewarmCount;
    [SerializeField] private float autoReturnToPoolSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugAutoDrop;
    [SerializeField] private float debugAutoDropInterval = 2f;

    private float debugTime;

    private void Awake()
    {
        currentObjectHp = maxObjectHp;
        WorldDropPool.Prewarm(dropItem.dropPrefab, poolPrewarmCount);
    }

    private void Update()
    {
        if (!enableDebugAutoDrop) return;

        debugTime += Time.deltaTime;
        if (debugTime >= debugAutoDropInterval)
        {
            debugTime = 0f;
            SpawnDropObjects();
        }
    }

    public bool ObjectInteract(ObjectType toolTargetType, PlayerInventory inventory, int damage)
    {
        if (myType != toolTargetType) return false;
        if (currentObjectHp <= 0) return false;

        currentObjectHp = Mathf.Max(0, currentObjectHp - damage);

        if (currentObjectHp <= 0)
        {
            ItemDrops(inventory);

            if (destroyObjectWhenDepleted)
            {
                Destroy(gameObject);
            }
        }

        return true;
    }

    private void ItemDrops(PlayerInventory inventory)
    {
        SpawnDropObjects();

        // inventory.AddItem(dropItem.itemData, Random.Range(dropItem.minDrop, dropItem.maxDrop + 1));
    }

    private void SpawnDropObjects()
    {
        if (dropItem.dropPrefab == null) return;

        int minDrop = Mathf.Max(0, dropItem.minDrop);
        int maxDrop = Mathf.Max(minDrop, dropItem.maxDrop);
        int dropCount = Random.Range(minDrop, maxDrop + 1);
        int visualCount = Mathf.Min(dropCount, Mathf.Max(1, maxDropVisualCount));

        for (int i = 0; i < visualCount; i++)
        {
            Vector3 spawnPosition = GetDropSpawnPosition();
            Quaternion spawnRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            WorldDropPool.Spawn(dropItem.dropPrefab, spawnPosition, spawnRotation, autoReturnToPoolSeconds);
        }
    }

    private Vector3 GetDropSpawnPosition()
    {
        Vector3 origin = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        Vector2 randomCircle = Random.insideUnitCircle * dropSpreadRadius;
        Vector3 dropPosition = origin + new Vector3(randomCircle.x, 0f, randomCircle.y);
        Vector3 rayStart = dropPosition + Vector3.up * groundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundRaycastDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * dropSpawnHeight;
        }

        return dropPosition + Vector3.up * dropSpawnHeight;
    }
}
