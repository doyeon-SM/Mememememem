using KGH.Data;
using KMS.Harvesting;
using KMS.InventoryDuped;
using UnityEngine;

public class WorldObject : MonoBehaviour
{
    [Header("Setting")]
    [SerializeField] private ObjectType myType;
    [SerializeField] private ObjectDropItem dropItem;
    [SerializeField] private int maxObjectHp = 1;
    [SerializeField] private int currentObjectHp;
    [SerializeField] private int respawnTime = 30;
    [SerializeField] private bool destroyObjectWhenDepleted = true;

    [Header("Drop Spawn")]
    [SerializeField] private Transform dropSpawnPoint;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float dropSpawnHeight = 0.02f;
    [SerializeField] private float dropSpreadRadius = 1.1f;

    [Header("Drop Pool")]
    [SerializeField] private int poolPrewarmCount;
    [SerializeField] private float autoReturnToPoolSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugAutoDrop;
    [SerializeField] private float debugAutoDropInterval = 2f;

    private const float GroundRaycastHeight = 3f;
    private const float GroundRaycastDistance = 8f;
    private const int MaxDropVisualCount = 8;
    private bool IsDead => currentObjectHp <= 0;
    private float debugTime;
    private float deadTime = -999f;

    private void Awake()
    {
        currentObjectHp = maxObjectHp;
        WorldDropPool.Prewarm(dropItem.dropPrefab, poolPrewarmCount);
    }

    private void Update()
    {
/*        if (!enableDebugAutoDrop) return;

        debugTime += Time.deltaTime;
        if (debugTime >= debugAutoDropInterval)
        {
            debugTime = 0f;
            SpawnDropObjects();
        }*/
        if(IsDead)
        {
            if(deadTime + respawnTime >= Time.deltaTime)
            {
                currentObjectHp = maxObjectHp;
            }
        }
    }
    //TODO :
    public bool ObjectInteract(ObjectType toolTargetType, PlayerInventory inventory, int damage)
    {
        if (IsDead)
        {
            Debug.Log($"{this.name} IsDead");
            return false;
        }
        if (myType != toolTargetType) 
        {
            Debug.Log($"{this.name} myType != toolTargetType");
            return false;
        }

        currentObjectHp = Mathf.Max(0, currentObjectHp - damage);
        Debug.Log($"감지 성공 : 현재 체력 {currentObjectHp}");
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
    }

    private void SpawnDropObjects()
    {
        if (dropItem.dropPrefab == null) return;

        int minDrop = Mathf.Max(0, dropItem.minDrop);
        int maxDrop = Mathf.Max(minDrop, dropItem.maxDrop);
        int dropCount = Random.Range(minDrop, maxDrop + 1);
        int visualCount = Mathf.Min(dropCount, MaxDropVisualCount);

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
        Vector3 rayStart = dropPosition + Vector3.up * GroundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GroundRaycastDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * dropSpawnHeight;
        }

        return dropPosition + Vector3.up * dropSpawnHeight;
    }

}
