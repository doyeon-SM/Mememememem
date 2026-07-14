using HDY;
using HDY.Item;
using KGH.Data;
using KMS.Harvesting;
using KMS.InventoryDuped;
using UnityEngine;

/// <summary>현재 장착 아이템으로 월드 오브젝트와 상호작용할 수 있는지 나타냅니다.</summary>
public enum WorldObjectInteractionState
{
    Available,
    NoToolEquipped,
    WrongToolType,
    InsufficientToolGrade,
    Depleted
}

/// <summary>
/// 도구 종류와 요구 등급을 검증하고 HP가 0이 되면 각 드롭 항목을 독립 추첨하는 채집 오브젝트입니다.
/// 고갈 중에는 Renderer와 Collider만 끄며, 청크 재활성화 시 절대 리스폰 시각으로 상태를 복구합니다.
/// </summary>
public class WorldObject : MonoBehaviour, KMS.IInteractable
{
    [Header("Setting")]
    [Tooltip("UI에 표시할 이름입니다. 비워 두면 GameObject 이름을 사용합니다.")]
    [SerializeField] private string displayName;
    [SerializeField] private string interactionPrompt = "채집";
    [Tooltip("활성화하면 PlayerInteraction의 상호작용 키로도 채집합니다. 꺼져 있어도 포커스 감지와 정보 UI는 동작합니다.")]
    [SerializeField] private bool harvestWithInteractInput;
    [SerializeField] private ObjectType myType;
    [SerializeField] private ObjectDropItem[] dropItems;
    [SerializeField] private int maxObjectHp = 1;
    [SerializeField] private int currentObjectHp;
    [Min(0f)] [SerializeField] private float respawnTime = 30f;
    [SerializeField] private CommonClass needGrade = CommonClass.Rare;

    [Header("Depletion Visual And Collision")]
    [Tooltip("비워 두면 이 오브젝트와 자식의 모든 Renderer를 자동으로 사용합니다.")]
    [SerializeField] private Renderer[] resourceRenderers;
    [Tooltip("비워 두면 이 오브젝트와 자식의 모든 Collider를 자동으로 사용합니다.")]
    [SerializeField] private Collider[] resourceColliders;

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

    /// <summary>이름, 체력 또는 상호작용 상태가 바뀌었을 때 발생합니다.</summary>
    public event System.Action<WorldObject> StateChanged;

    /// <summary>UI에 표시할 오브젝트 이름입니다.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;

    /// <inheritdoc />
    public string InteractionPrompt => interactionPrompt;

    /// <summary>현재 남은 체력입니다.</summary>
    public int CurrentHp => currentObjectHp;

    /// <summary>최대 체력입니다.</summary>
    public int MaxHp => maxObjectHp;

    /// <summary>상호작용에 필요한 도구 타입입니다.</summary>
    public ObjectType RequiredToolType => myType;

    /// <summary>상호작용에 필요한 최소 도구 등급입니다.</summary>
    public CommonClass RequiredToolGrade => needGrade;

    /// <summary>현재 고갈되어 상호작용할 수 없는지 나타냅니다.</summary>
    public bool IsDepleted => currentObjectHp <= 0;

    private bool IsDead => IsDepleted;
    private float debugTime;
    private float respawnAtTime = float.PositiveInfinity;
    private bool[] rendererInitialStates;
    private bool[] colliderInitialStates;

    private void Awake()
    {
        maxObjectHp = Mathf.Max(1, maxObjectHp);
        currentObjectHp = maxObjectHp;
        CacheResourceComponents();
        SetResourceAvailable(true);
        PrewarmDropPools();
    }

    private void OnEnable()
    {
        RefreshRespawnState();
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
        if (IsDead && Time.time >= respawnAtTime)
        {
            Respawn();
        }
    }

    /// <summary>
    /// 플레이어의 근접/시선 감지에 포함되도록 고갈 전에는 참을 반환합니다.
    /// 도구 적합성은 정보 UI에 실패 사유를 보여줘야 하므로 여기서 제외하고 EvaluateInteraction에서 판정합니다.
    /// </summary>
    public bool CanInteract(KMS.PlayerInteraction interactor)
    {
        return !IsDead;
    }

    /// <summary>옵션이 켜져 있으면 플레이어 상호작용 키로 현재 퀵슬롯 도구를 사용합니다.</summary>
    public void Interact(KMS.PlayerInteraction interactor)
    {
        if (!harvestWithInteractInput || interactor == null)
        {
            return;
        }

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();
        ItemData selectedTool = ResolveSelectedTool(inventory);
        ObjectInteract(inventory, selectedTool);
    }
    /// <summary>
    /// 도구로 채집 피해를 적용합니다. 종류·등급·고갈 상태 검증에 실패하면 상태를 변경하지 않습니다.
    /// </summary>
    /// <returns>이번 상호작용이 유효하게 적용되었으면 참입니다.</returns>
    public bool ObjectInteract(PlayerInventory inventory, ItemData data)
    {
        WorldObjectInteractionState interactionState = EvaluateInteraction(data);
        if (interactionState != WorldObjectInteractionState.Available)
        {
            Debug.Log($"{name} 상호작용 불가: {interactionState}", this);
            return false;
        }

        currentObjectHp = Mathf.Max(0, currentObjectHp - data.Value);
        Debug.Log($"감지 성공 : 현재 체력 {currentObjectHp}");
        if (currentObjectHp <= 0)
        {
            ItemDrops(data);
            BeginRespawnCooldown();

            if (IsDead)
            {
                NotifyStateChanged();
            }

            return true;
        }

        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// 현재 장착 아이템으로 상호작용 가능한지 실제 채집과 동일한 순서로 판정합니다.
    /// UI는 이 결과를 사용해 불가능 사유를 표시할 수 있습니다.
    /// </summary>
    public WorldObjectInteractionState EvaluateInteraction(ItemData tool)
    {
        if (IsDead)
        {
            return WorldObjectInteractionState.Depleted;
        }

        if (tool == null)
        {
            return WorldObjectInteractionState.NoToolEquipped;
        }

        if (tool.Category != ItemCategory.Tool || myType != tool.ObjectType)
        {
            return WorldObjectInteractionState.WrongToolType;
        }

        if (tool.ItemClass < needGrade)
        {
            return WorldObjectInteractionState.InsufficientToolGrade;
        }

        return WorldObjectInteractionState.Available;
    }

    private static ItemData ResolveSelectedTool(PlayerInventory inventory)
    {
        if (inventory == null)
        {
            return null;
        }

        ItemStack selectedSlot = inventory.GetSelectedQuickSlot();
        if (selectedSlot == null || selectedSlot.IsEmpty)
        {
            return null;
        }

        ItemCatalogManager catalogManager = ItemCatalogManager.Instance;
        if (catalogManager == null)
        {
            catalogManager = FindFirstObjectByType<ItemCatalogManager>();
        }

        return catalogManager != null
            ? catalogManager.FindItemData(selectedSlot.itemId)
            : null;
    }

    private void ItemDrops(ItemData tool)
    {
        SpawnDropObjects(tool);
    }

    private void SpawnDropObjects(ItemData tool)
    {
        if (dropItems == null || dropItems.Length == 0) return;

        for (int dropIndex = 0; dropIndex < dropItems.Length; dropIndex++)
        {
            GameObject dropPrefab = dropItems[dropIndex].dropPrefab;
            if (dropPrefab == null) continue;

            // 드롭 항목마다 도구의 개수 확률을 독립적으로 추첨한다.
            int dropCount = ToolDropManager.Instance != null
                ? ToolDropManager.Instance.RollDropCount(tool)
                : 1;
            int visualCount = Mathf.Min(Mathf.Max(1, dropCount), MaxDropVisualCount);

            for (int i = 0; i < visualCount; i++)
            {
                Vector3 spawnPosition = GetDropSpawnPosition();
                Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                WorldDropPool.Spawn(dropPrefab, spawnPosition, spawnRotation, autoReturnToPoolSeconds);
            }
        }
    }

    private void PrewarmDropPools()
    {
        if (dropItems == null) return;

        for (int i = 0; i < dropItems.Length; i++)
        {
            WorldDropPool.Prewarm(dropItems[i].dropPrefab, poolPrewarmCount);
        }
    }

    private Vector3 GetDropSpawnPosition()
    {
        Vector3 origin = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * dropSpreadRadius;
        Vector3 dropPosition = origin + new Vector3(randomCircle.x, 0f, randomCircle.y);
        Vector3 rayStart = dropPosition + Vector3.up * GroundRaycastHeight;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, GroundRaycastDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            return hit.point + Vector3.up * dropSpawnHeight;
        }

        return dropPosition + Vector3.up * dropSpawnHeight;
    }

    private void BeginRespawnCooldown()
    {
        currentObjectHp = 0;
        respawnAtTime = Time.time + Mathf.Max(0f, respawnTime);
        SetResourceAvailable(false);

        if (respawnTime <= 0f)
        {
            Respawn();
        }
    }

    // 청크 비활성화 중 Update가 멈췄더라도 절대 리스폰 시각으로 경과 여부를 복구한다.
    private void RefreshRespawnState()
    {
        if (!IsDead)
        {
            SetResourceAvailable(true);
            return;
        }

        if (Time.time >= respawnAtTime)
        {
            Respawn();
            return;
        }

        SetResourceAvailable(false);
    }

    private void Respawn()
    {
        currentObjectHp = maxObjectHp;
        respawnAtTime = float.PositiveInfinity;
        SetResourceAvailable(true);
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this);
    }

    private void CacheResourceComponents()
    {
        if (resourceRenderers == null || resourceRenderers.Length == 0)
        {
            resourceRenderers = GetComponentsInChildren<Renderer>(true);
        }

        if (resourceColliders == null || resourceColliders.Length == 0)
        {
            resourceColliders = GetComponentsInChildren<Collider>(true);
        }

        rendererInitialStates = new bool[resourceRenderers.Length];
        for (int i = 0; i < resourceRenderers.Length; i++)
        {
            rendererInitialStates[i] = resourceRenderers[i] != null && resourceRenderers[i].enabled;
        }

        colliderInitialStates = new bool[resourceColliders.Length];
        for (int i = 0; i < resourceColliders.Length; i++)
        {
            colliderInitialStates[i] = resourceColliders[i] != null && resourceColliders[i].enabled;
        }
    }

    private void SetResourceAvailable(bool available)
    {
        if (rendererInitialStates != null)
        {
            for (int i = 0; i < resourceRenderers.Length; i++)
            {
                if (resourceRenderers[i] != null)
                {
                    resourceRenderers[i].enabled = available && rendererInitialStates[i];
                }
            }
        }

        if (colliderInitialStates != null)
        {
            for (int i = 0; i < resourceColliders.Length; i++)
            {
                if (resourceColliders[i] != null)
                {
                    resourceColliders[i].enabled = available && colliderInitialStates[i];
                }
            }
        }
    }

}
