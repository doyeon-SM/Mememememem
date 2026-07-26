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
    [Tooltip("유효한 바닥 위치를 찾기 위해 시도할 횟수입니다.")]
    [Min(1)] [SerializeField] private int dropPositionAttempts = 12;
    [Tooltip("드롭 위치 주변에 다른 오브젝트가 없어야 하는 반경입니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceRadius = 0.25f;
    [Tooltip("바닥부터 이 높이까지 다른 오브젝트가 있으면 해당 위치를 사용하지 않습니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceHeight = 0.9f;
    [Tooltip("드롭을 놓을 수 있는 바닥의 최대 경사각입니다.")]
    [Range(0f, 89f)] [SerializeField] private float maxGroundSlope = 50f;

    [Header("Drop Spawn Gizmo")]
    [Tooltip("오브젝트를 선택했을 때 드롭 중심과 확산 반경을 Scene 뷰에 표시합니다.")]
    [SerializeField] private bool showDropSpawnGizmo = true;
    [SerializeField] private Color dropSpawnGizmoColor = new Color(1f, 0.72f, 0.1f, 0.9f);

    [Header("Drop Pool")]
    [SerializeField] private int poolPrewarmCount;
    [SerializeField] private float autoReturnToPoolSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugAutoDrop;
    [SerializeField] private float debugAutoDropInterval = 2f;

    private const float GroundRaycastHeight = 3f;
    private const float GroundRaycastDistance = 8f;
    private const int MaxDropVisualCount = 8;
    private const int MaxClearanceHits = 32;

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
    private readonly Collider[] clearanceHits = new Collider[MaxClearanceHits];

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

        PlayerInventory inventory = PlayerReferenceResolver.FindComponentInPlayerHierarchy<PlayerInventory>(
            interactor.gameObject);
        if (inventory == null)
        {
            inventory = PlayerReferenceResolver.FindPlayerComponent<PlayerInventory>();
        }

        ItemData selectedTool = ResolveSelectedTool(inventory);
        ObjectInteract(inventory, selectedTool);
    }
    /// <summary>
    /// 도구로 채집 피해를 적용합니다. 종류·등급·고갈 상태 검증에 실패하면 상태를 변경하지 않습니다.
    /// </summary>
    /// <returns>이번 상호작용이 유효하게 적용되었으면 참입니다.</returns>
    public bool ObjectInteract(PlayerInventory inventory, ItemData data)
    {
        // 호출자가 미리 조회한 ItemData와 UI가 따로 조회한 ItemData가 엇갈리지 않도록
        // 실제 상호작용 시점의 선택 퀵슬롯을 이 오브젝트에서 다시 한 번만 해석한다.
        ItemData activeTool = inventory != null ? ResolveSelectedTool(inventory) : data;
        WorldObjectInteractionState interactionState = EvaluateInteraction(activeTool);
        if (interactionState != WorldObjectInteractionState.Available)
        {
            Debug.Log($"{name} 상호작용 불가: {interactionState}", this);
            return false;
        }

        currentObjectHp = Mathf.Max(0, currentObjectHp - activeTool.Value);
        Debug.Log($"감지 성공 : 현재 체력 {currentObjectHp}");
        if (currentObjectHp <= 0)
        {
            // 자원 콜라이더 위를 바닥으로 잘못 인식하지 않도록 먼저 자원을 숨긴 뒤 드롭 위치를 계산합니다.
            BeginRespawnCooldown();
            Physics.SyncTransforms();
            ItemDrops(activeTool);

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

    /// <summary>
    /// 현재 플레이어 인벤토리의 선택 퀵슬롯을 실제 채집과 동일한 경로로 해석해 판정합니다.
    /// 정보 UI가 별도의 ItemData 캐시를 사용하지 않도록 제공하는 공용 진입점입니다.
    /// </summary>
    public WorldObjectInteractionState EvaluateInteraction(PlayerInventory inventory)
    {
        return EvaluateInteraction(ResolveSelectedTool(inventory));
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
            string dropItemId = dropItems[dropIndex].itemId;
            if (string.IsNullOrWhiteSpace(dropItemId))
            {
                Debug.LogWarning($"[{name}] Drop Items의 {dropIndex}번 Item Id가 비어 있어 생성을 건너뜁니다.", this);
                continue;
            }

            // 드롭 항목마다 도구의 개수 확률을 독립적으로 추첨한다.
            int dropCount = ToolDropManager.Instance != null
                ? ToolDropManager.Instance.RollDropCount(tool)
                : 1;
            int visualCount = Mathf.Min(Mathf.Max(1, dropCount), MaxDropVisualCount);
            int baseAmount = dropCount / visualCount;
            int remainder = dropCount % visualCount;

            for (int i = 0; i < visualCount; i++)
            {
                if (!TryGetDropSpawnPosition(out Vector3 spawnPosition))
                {
                    Debug.LogWarning($"[{name}] 주변에서 안전한 바닥 드롭 위치를 찾지 못해 드롭 생성을 건너뜁니다.", this);
                    continue;
                }

                Quaternion spawnRotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                int itemAmount = baseAmount + (i < remainder ? 1 : 0);

                WorldDropPool.Spawn(
                    dropItemId,
                    itemAmount,
                    spawnPosition,
                    spawnRotation,
                    autoReturnToPoolSeconds);
            }
        }
    }

    private void PrewarmDropPools()
    {
        WorldDropPool.Prewarm(poolPrewarmCount);
    }

    private bool TryGetDropSpawnPosition(out Vector3 spawnPosition)
    {
        Vector3 origin = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        int attempts = Mathf.Max(1, dropPositionAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * Mathf.Max(0f, dropSpreadRadius);
            Vector3 samplePosition = origin + new Vector3(randomCircle.x, 0f, randomCircle.y);
            Vector3 rayStart = samplePosition + Vector3.up * GroundRaycastHeight;

            if (!TryFindGround(rayStart, out RaycastHit groundHit))
            {
                continue;
            }

            Vector3 groundPosition = groundHit.point;
            if (!IsDropSpaceClear(groundPosition, groundHit.collider))
            {
                continue;
            }

            // 경사면에서도 월드 아이템 루트가 표면에 붙도록 표면 노멀 방향으로 최소 간격만 둡니다.
            spawnPosition = groundPosition + groundHit.normal * Mathf.Max(0f, dropSpawnHeight);
            return true;
        }

        spawnPosition = default;
        return false;
    }

    /// <summary>
    /// 자원·플레이어·기존 월드 아이템을 제외하고 현재 위치 바로 아래의 첫 유효 표면을 선택합니다.
    /// groundLayer를 바닥 전용 레이어로 설정하면 해당 레이어 안에서만 탐색합니다.
    /// </summary>
    private bool TryFindGround(Vector3 rayStart, out RaycastHit groundHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            rayStart,
            Vector3.down,
            GroundRaycastDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);

        groundHit = default;
        bool found = false;

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (!IsValidGroundHit(hit))
            {
                continue;
            }

            // 위에서 아래로 가장 먼저 만나는 유효 표면을 사용해 멀리 떨어진 아래층으로 내려가지 않게 합니다.
            if (!found || hit.distance < groundHit.distance)
            {
                groundHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool IsValidGroundHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;
        if (hitCollider == null
            || Vector3.Angle(hit.normal, Vector3.up) > maxGroundSlope
            || PlayerReferenceResolver.IsInPlayerHierarchy(hitCollider.gameObject)
            || hitCollider.GetComponentInParent<WorldItem>() != null
            || hitCollider.GetComponentInParent<WorldObject>() != null
            || IsOwnResourceCollider(hitCollider))
        {
            return false;
        }

        Rigidbody attachedBody = hitCollider.attachedRigidbody;
        return attachedBody == null || attachedBody.isKinematic;
    }

    private bool IsDropSpaceClear(Vector3 groundPosition, Collider groundCollider)
    {
        float radius = Mathf.Max(0.01f, dropClearanceRadius);
        float height = Mathf.Max(radius * 2f, dropClearanceHeight);
        Vector3 bottom = groundPosition + Vector3.up * (radius + 0.01f);
        Vector3 top = groundPosition + Vector3.up * Mathf.Max(radius + 0.01f, height - radius);
        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            radius,
            clearanceHits,
            ~0,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = clearanceHits[i];
            clearanceHits[i] = null;
            if (hitCollider == null || hitCollider == groundCollider || IsOwnResourceCollider(hitCollider))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsOwnResourceCollider(Collider candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.GetComponentInParent<WorldObject>() == this)
        {
            return true;
        }

        if (resourceColliders == null)
        {
            return false;
        }

        for (int i = 0; i < resourceColliders.Length; i++)
        {
            if (resourceColliders[i] == candidate)
            {
                return true;
            }
        }

        return false;
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDropSpawnGizmo)
        {
            return;
        }

        Vector3 origin = dropSpawnPoint != null ? dropSpawnPoint.position : transform.position;
        Vector3 center = origin + Vector3.up * dropSpawnHeight;
        float radius = Mathf.Max(0f, dropSpreadRadius);

        UnityEditor.Handles.color = dropSpawnGizmoColor;
        UnityEditor.Handles.DrawWireDisc(center, Vector3.up, radius);
        UnityEditor.Handles.DrawLine(origin, center);

        Gizmos.color = dropSpawnGizmoColor;
        Gizmos.DrawSphere(center, Mathf.Max(0.04f, radius * 0.025f));
    }
#endif

}
