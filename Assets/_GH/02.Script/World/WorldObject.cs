using HDY;
using HDY.Forge;
using HDY.Item;
using KGH.Data;
using KMS.Harvesting;
using KMS.InventoryDuped;
using System.Collections.Generic;
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
    private static readonly HashSet<WorldObject> ActiveWorldObjects = new HashSet<WorldObject>();

    /// <summary>활성화된 월드 오브젝트가 나타날 때 균열 등 보조 시스템에 알립니다.</summary>
    public static event System.Action<WorldObject> InstanceEnabled;

    /// <summary>현재 활성화된 월드 오브젝트입니다. 주기적인 전체 씬 검색을 대체합니다.</summary>
    public static IReadOnlyCollection<WorldObject> ActiveInstances => ActiveWorldObjects;

    private const string DamageIncreaseOptionType = "DamageIncrease";
    private const string GatherIncreaseOptionType = "GatherIncrease";

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

    [Header("Tree Depletion Motion")]
    [Tooltip("나무가 쓰러질 때 타격 반대 방향으로 주는 순간 속도입니다.")]
    [Min(0f)] [SerializeField] private float treeFallPushSpeed = 1.2f;
    [Tooltip("나무가 쓰러지도록 회전축에 주는 순간 각속도입니다.")]
    [Min(0f)] [SerializeField] private float treeFallAngularSpeed = 2.4f;
    [Tooltip("쓰러지기 시작한 직후 기존 바닥 접촉을 착지로 오인하지 않는 시간입니다.")]
    [Min(0f)] [SerializeField] private float treeFallLandingGraceSeconds = 0.25f;
    [Tooltip("처음 자세에서 이 각도 이상 기울고 바닥과 접촉하면 쓰러진 것으로 판정합니다.")]
    [Range(10f, 89f)] [SerializeField] private float treeFallLandedAngle = 55f;
    [Tooltip("충돌 판정을 받지 못한 나무도 이 시간이 지나면 제거하고 아이템을 생성합니다.")]
    [Min(0.5f)] [SerializeField] private float treeFallTimeoutSeconds = 5f;

    [Header("Depletion Visual And Collision")]
    [Tooltip("비워 두면 이 오브젝트와 자식의 모든 Renderer를 자동으로 사용합니다.")]
    [SerializeField] private Renderer[] resourceRenderers;
    [Tooltip("비워 두면 이 오브젝트와 자식의 모든 Collider를 자동으로 사용합니다.")]
    [SerializeField] private Collider[] resourceColliders;

    [Header("Drop Spawn")]
    [SerializeField] private Transform dropSpawnPoint;
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float dropSpawnHeight = 0.02f;
    [Tooltip("Drop Spawn Point 기준 로컬 위치 보정값입니다. X/Z로 중심을 옮기고 Y로 높이를 조절합니다.")]
    [SerializeField] private Vector3 dropAreaOffset;
    [Tooltip("드롭 타원 전체 크기입니다. X는 월드 가로축, Y는 월드 Z축 크기로 사용합니다.")]
    [SerializeField] private Vector2 dropAreaSize = new Vector2(2.2f, 2.2f);
    [Tooltip("유효한 바닥 위치를 찾기 위해 시도할 횟수입니다.")]
    [Min(1)] [SerializeField] private int dropPositionAttempts = 12;
    [Tooltip("드롭 위치 주변에 다른 오브젝트가 없어야 하는 반경입니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceRadius = 0.25f;
    [Tooltip("바닥부터 이 높이까지 다른 오브젝트가 있으면 해당 위치를 사용하지 않습니다.")]
    [Min(0.01f)] [SerializeField] private float dropClearanceHeight = 0.9f;
    [Tooltip("드롭을 놓을 수 있는 바닥의 최대 경사각입니다.")]
    [Range(0f, 89f)] [SerializeField] private float maxGroundSlope = 50f;

    [Header("Drop Launch Motion")]
    [Tooltip("활성화하면 Chest처럼 아이템이 오브젝트에서 Drop Area의 착지점까지 포물선으로 날아갑니다.")]
    [SerializeField] private bool launchDrops = true;
    [Tooltip("지정하면 이 위치에서 아이템이 발사됩니다. 비워 두면 오브젝트 로컬 오프셋을 사용합니다.")]
    [SerializeField] private Transform dropEjectPoint;
    [SerializeField] private Vector3 dropEjectLocalOffset = new Vector3(0f, 0.8f, 0f);
    [Min(0.01f)] [SerializeField] private float itemFlightDuration = 0.55f;
    [Min(0f)] [SerializeField] private float itemFlightArcHeight = 0.85f;
    [Min(0f)] [SerializeField] private float itemSpinSpeed = 320f;
    [Min(0f)] [SerializeField] private float itemStartJitterRadius = 0.12f;

    [Header("Drop Spawn Gizmo")]
    [Tooltip("오브젝트를 선택했을 때 실제 드롭 타원과 중심 위치를 Scene 뷰에 표시합니다.")]
    [SerializeField] private bool showDropSpawnGizmo = true;
    [SerializeField] private Color dropSpawnGizmoColor = new Color(1f, 0.72f, 0.1f, 0.9f);

    [Header("Drop Pool")]
    [SerializeField] private int poolPrewarmCount;
    [SerializeField] private float autoReturnToPoolSeconds = 10f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugAutoDrop;
    [SerializeField] private float debugAutoDropInterval = 2f;

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

    /// <summary>
    /// 아직 고갈되지 않은 오브젝트의 체력을 최대치로 회복합니다.
    /// 외부의 피격 복구 연출이 기존 드롭/리스폰 흐름을 건드리지 않고 사용할 수 있는 전용 진입점입니다.
    /// </summary>
    /// <returns>실제로 체력이 변경되었으면 참입니다.</returns>
    public bool RestoreHealthToMaximum()
    {
        if (IsDepleted || currentObjectHp >= maxObjectHp)
        {
            return false;
        }

        currentObjectHp = maxObjectHp;
        NotifyStateChanged();
        return true;
    }

    /// <summary>씬 매니저가 활성화된 뒤 타입별 고정 규칙을 현재 상태에 반영합니다.</summary>
    internal void ApplyTypeSpecificRules()
    {
        if (myType != ObjectType.Bush)
        {
            return;
        }

        maxObjectHp = 1;
        if (!IsDepleted)
        {
            currentObjectHp = 1;
        }

        NotifyStateChanged();
    }

    private bool IsDead => IsDepleted;
    private float debugTime;
    private float respawnAtTime = float.PositiveInfinity;
    private bool[] rendererInitialStates;
    private bool[] colliderInitialStates;
    private bool isTreeFalling;
    private float treeFallCanLandAtTime;
    private float treeFallTimeoutAtTime;
    private Vector3 treeFallInitialUp;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private ItemData pendingTreeDropTool;
    private Rigidbody treeRigidbody;
    private bool addedTreeRigidbody;
    private bool treeRigidbodyStateCached;
    private bool treeRigidbodyInitialKinematic;
    private bool treeRigidbodyInitialUseGravity;
    private RigidbodyConstraints treeRigidbodyInitialConstraints;
    private CollisionDetectionMode treeRigidbodyInitialCollisionDetection;

    private void Awake()
    {
        maxObjectHp = Mathf.Max(1, maxObjectHp);
        if (UsesTypeSpecificDepletion() && myType == ObjectType.Bush)
        {
            maxObjectHp = 1;
        }

        currentObjectHp = maxObjectHp;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        CacheResourceComponents();
        SetResourceAvailable(true);
        PrewarmDropPools();
    }

    private void OnEnable()
    {
        ActiveWorldObjects.Add(this);
        InstanceEnabled?.Invoke(this);
        RefreshRespawnState();
    }

    private void OnDisable()
    {
        ActiveWorldObjects.Remove(this);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveRegistry()
    {
        ActiveWorldObjects.Clear();
        InstanceEnabled = null;
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

        if (isTreeFalling && Time.time >= treeFallTimeoutAtTime)
        {
            CompleteTreeFall();
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
        return ObjectInteract(inventory, data, transform.position, transform.position - transform.forward);
    }

    /// <summary>
    /// 도구로 채집 피해를 적용하고, 타격 위치와 공격자 위치를 타입별 파괴 연출에 전달합니다.
    /// </summary>
    public bool ObjectInteract(
        PlayerInventory inventory,
        ItemData data,
        Vector3 hitPoint,
        Vector3 attackerPosition)
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

        int refinementDamage = GetWholeRefinementBonus(activeTool, DamageIncreaseOptionType);
        int finalDamage = Mathf.Max(1, activeTool.Value + refinementDamage);
        currentObjectHp = Mathf.Max(0, currentObjectHp - finalDamage);
        Debug.Log(
            $"감지 성공 : 기본/강화 피해 {activeTool.Value}, 연마 피해 +{refinementDamage}, " +
            $"최종 피해 {finalDamage}, 현재 체력 {currentObjectHp}",
            this);
        if (currentObjectHp <= 0)
        {
            if (UsesTypeSpecificDepletion() && myType == ObjectType.Tree)
            {
                BeginTreeFall(activeTool, hitPoint, attackerPosition);
                NotifyStateChanged();
                return true;
            }

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

        int gatherBonus = GetWholeRefinementBonus(tool, GatherIncreaseOptionType);
        Dictionary<string, int> amountsByItemId = new Dictionary<string, int>();
        for (int dropIndex = 0; dropIndex < dropItems.Length; dropIndex++)
        {
            string dropItemId = dropItems[dropIndex].itemId;
            if (string.IsNullOrWhiteSpace(dropItemId))
            {
                Debug.LogWarning($"[{name}] Drop Items의 {dropIndex}번 Item Id가 비어 있어 생성을 건너뜁니다.", this);
                continue;
            }

            // 드롭 항목마다 도구의 개수 확률을 독립적으로 추첨한다.
            int baseDropCount = ToolDropManager.Instance != null
                ? ToolDropManager.Instance.RollDropCount(tool)
                : 1;
            int dropCount = baseDropCount + gatherBonus;

            if (dropCount <= 0)
            {
                continue;
            }

            string normalizedItemId = dropItemId.Trim();
            amountsByItemId.TryGetValue(normalizedItemId, out int currentAmount);
            amountsByItemId[normalizedItemId] = currentAmount + dropCount;

            Debug.Log(
                $"[{name}] 드롭 계산: {normalizedItemId}, 기본 {baseDropCount}, " +
                $"연마 채집량 +{gatherBonus}, 최종 {dropCount}",
                this);
        }

        WorldItemDropLaunchSettings launchSettings = CreateDropLaunchSettings();

        foreach (KeyValuePair<string, int> drop in amountsByItemId)
        {
            WorldItemDropSpawner.SpawnIndividualItems(
                drop.Key,
                drop.Value,
                transform,
                dropSpawnPoint,
                dropAreaOffset,
                dropAreaSize,
                groundLayer,
                dropSpawnHeight,
                dropPositionAttempts,
                dropClearanceRadius,
                dropClearanceHeight,
                maxGroundSlope,
                autoReturnToPoolSeconds,
                resourceColliders,
                launchSettings);
        }
    }

    private WorldItemDropLaunchSettings CreateDropLaunchSettings()
    {
        if (!launchDrops)
        {
            return default;
        }

        Vector3 startPosition = dropEjectPoint != null
            ? dropEjectPoint.position
            : transform.TransformPoint(dropEjectLocalOffset);

        return new WorldItemDropLaunchSettings
        {
            enabled = true,
            startPosition = startPosition,
            duration = Mathf.Max(0.01f, itemFlightDuration),
            arcHeight = Mathf.Max(0f, itemFlightArcHeight),
            spinSpeed = Mathf.Max(0f, itemSpinSpeed),
            startJitterRadius = Mathf.Max(0f, itemStartJitterRadius)
        };
    }

    /// <summary>
    /// 강화/연마 도구의 합성 ID로 인스턴스를 찾아 같은 옵션의 수치를 합산합니다.
    /// 일반 도구이거나 레지스트리 데이터가 없으면 기존 동작을 유지하도록 0을 반환합니다.
    /// 현재 피해와 드롭이 정수 단위이므로 소수 옵션 값은 가장 가까운 정수로 반올림합니다.
    /// </summary>
    private static int GetWholeRefinementBonus(ItemData tool, string optionType)
    {
        if (tool == null
            || string.IsNullOrEmpty(optionType)
            || !ForgeInstanceRegistry.TryParseCompositeId(tool.Item_ID, out _, out string instanceId))
        {
            return 0;
        }

        ForgeInstanceRegistry registry = ForgeInstanceRegistry.Instance;
        ForgeInstanceData instance = registry != null ? registry.GetInstance(instanceId) : null;
        if (instance?.RefinementSlots == null)
        {
            return 0;
        }

        float total = 0f;
        foreach (ForgeRefinementSlotData slot in instance.RefinementSlots)
        {
            if (slot != null
                && string.Equals(slot.OptionType, optionType, System.StringComparison.Ordinal))
            {
                total += slot.Value;
            }
        }

        return Mathf.Max(0, Mathf.RoundToInt(total));
    }

    private void PrewarmDropPools()
    {
        WorldDropPool.Prewarm(poolPrewarmCount);
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

    private bool UsesTypeSpecificDepletion()
    {
        return GH.World.GHWorldObjectDamageRecoveryManager
            .IsTypeSpecificDepletionEnabledFor(this);
    }

    private void BeginTreeFall(ItemData tool, Vector3 hitPoint, Vector3 attackerPosition)
    {
        currentObjectHp = 0;
        respawnAtTime = float.PositiveInfinity;
        isTreeFalling = true;
        pendingTreeDropTool = tool;
        treeFallInitialUp = transform.up;
        treeFallCanLandAtTime = Time.time + Mathf.Max(0f, treeFallLandingGraceSeconds);
        treeFallTimeoutAtTime = Time.time + Mathf.Max(0.5f, treeFallTimeoutSeconds);

        Vector3 fallDirection = transform.position - attackerPosition;
        fallDirection = Vector3.ProjectOnPlane(fallDirection, treeFallInitialUp);
        if (fallDirection.sqrMagnitude < 0.0001f)
        {
            fallDirection = transform.position - hitPoint;
            fallDirection = Vector3.ProjectOnPlane(fallDirection, treeFallInitialUp);
        }

        if (fallDirection.sqrMagnitude < 0.0001f)
        {
            fallDirection = transform.forward;
        }

        fallDirection.Normalize();
        Rigidbody body = PrepareTreeRigidbody();
        body.AddForce(fallDirection * Mathf.Max(0f, treeFallPushSpeed), ForceMode.VelocityChange);

        Vector3 fallAxis = Vector3.Cross(treeFallInitialUp, fallDirection).normalized;
        body.AddTorque(
            fallAxis * Mathf.Max(0f, treeFallAngularSpeed),
            ForceMode.VelocityChange);
    }

    private Rigidbody PrepareTreeRigidbody()
    {
        treeRigidbody = GetComponent<Rigidbody>();
        if (treeRigidbody == null)
        {
            treeRigidbody = gameObject.AddComponent<Rigidbody>();
            addedTreeRigidbody = true;
        }
        else if (!treeRigidbodyStateCached)
        {
            treeRigidbodyInitialKinematic = treeRigidbody.isKinematic;
            treeRigidbodyInitialUseGravity = treeRigidbody.useGravity;
            treeRigidbodyInitialConstraints = treeRigidbody.constraints;
            treeRigidbodyInitialCollisionDetection = treeRigidbody.collisionDetectionMode;
            treeRigidbodyStateCached = true;
        }

        treeRigidbody.isKinematic = false;
        treeRigidbody.useGravity = true;
        treeRigidbody.constraints = RigidbodyConstraints.None;
        treeRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        treeRigidbody.linearVelocity = Vector3.zero;
        treeRigidbody.angularVelocity = Vector3.zero;
        treeRigidbody.WakeUp();
        return treeRigidbody;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!isTreeFalling
            || collision == null
            || Time.time < treeFallCanLandAtTime
            || !IsLayerInMask(collision.gameObject.layer, groundLayer))
        {
            return;
        }

        float tiltAngle = Vector3.Angle(treeFallInitialUp, transform.up);
        if (tiltAngle >= treeFallLandedAngle && HasGroundLikeContact(collision))
        {
            CompleteTreeFall();
        }
    }

    private bool HasGroundLikeContact(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (Vector3.Dot(contact.normal, treeFallInitialUp) >= 0.45f)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void CompleteTreeFall()
    {
        if (!isTreeFalling)
        {
            return;
        }

        isTreeFalling = false;
        StopTreePhysics();
        BeginRespawnCooldown();
        Physics.SyncTransforms();
        ItemDrops(pendingTreeDropTool);
        pendingTreeDropTool = null;
        NotifyStateChanged();
    }

    private void StopTreePhysics()
    {
        if (treeRigidbody == null)
        {
            return;
        }

        treeRigidbody.linearVelocity = Vector3.zero;
        treeRigidbody.angularVelocity = Vector3.zero;
        treeRigidbody.isKinematic = true;
        treeRigidbody.useGravity = false;
    }

    private void RestoreTreeTransformAndPhysics()
    {
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;

        if (treeRigidbody == null)
        {
            return;
        }

        treeRigidbody.linearVelocity = Vector3.zero;
        treeRigidbody.angularVelocity = Vector3.zero;

        if (addedTreeRigidbody)
        {
            Destroy(treeRigidbody);
            treeRigidbody = null;
            addedTreeRigidbody = false;
            return;
        }

        if (treeRigidbodyStateCached)
        {
            treeRigidbody.isKinematic = treeRigidbodyInitialKinematic;
            treeRigidbody.useGravity = treeRigidbodyInitialUseGravity;
            treeRigidbody.constraints = treeRigidbodyInitialConstraints;
            treeRigidbody.collisionDetectionMode = treeRigidbodyInitialCollisionDetection;
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
        isTreeFalling = false;
        pendingTreeDropTool = null;
        RestoreTreeTransformAndPhysics();
        Physics.SyncTransforms();
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

        WorldItemDropSpawner.DrawDropAreaGizmo(
            dropSpawnPoint,
            transform,
            dropAreaOffset,
            dropAreaSize,
            dropSpawnHeight,
            dropSpawnGizmoColor);
    }
#endif

}
