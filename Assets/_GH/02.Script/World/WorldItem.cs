using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;

/// <summary>
/// 월드에 배치된 아이템의 등급 VFX와 플레이어 자동 습득을 처리합니다.
/// 색상과 발광 강도는 <see cref="WorldItemRarityVfxManager"/> 한 곳에서 관리합니다.
/// </summary>
public class WorldItem : MonoBehaviour
{
    private const string LegacySpriteRootName = "World Item Sprite Visual";

    [Header("Ref")]
    [SerializeField] private string itemId;
    [SerializeField] private int amount = 1;

    [Header("Rarity VFX Visual")]
    [Tooltip("등급 불빛 전체 크기입니다. 색상과 강도는 전역 VFX 관리자에서 관리합니다.")]
    [Min(0.01f)]
    [SerializeField] private float visualMaxSize = 0.8f;

    [Tooltip("등급 불빛과 바닥 사이의 간격입니다.")]
    [Min(0f)]
    [SerializeField] private float groundClearance = 0.03f;

    [Header("Visual Motion")]
    [Tooltip("활성화하면 등급 불빛이 위아래로 부유하고 천천히 회전합니다.")]
    [SerializeField] private bool animateVisual = true;

    [Min(0f)]
    [SerializeField] private float bobHeight = 0.06f;

    [Min(0f)]
    [SerializeField] private float bobFrequency = 1.5f;

    [Min(0f)]
    [SerializeField] private float rotationSpeed = 35f;

    [Header("Pickup Collider")]
    [Tooltip("비어 있으면 같은 오브젝트의 BoxCollider를 자동으로 사용합니다.")]
    [SerializeField] private BoxCollider pickupCollider;

    [Min(0f)]
    [SerializeField] private float colliderPadding = 0.05f;

    [Min(0.01f)]
    [SerializeField] private float colliderDepth = 0.16f;

    private int initialAmount;
    private Transform visualRoot;
    private MeshRenderer[] legacyMeshRenderers;
    private bool[] legacyRendererInitialStates;
    private Vector3 initialColliderCenter;
    private Vector3 initialColliderSize;
    private float animationTime;
    private Vector3 visualBaseLocalPosition;
    private float nextVisualResolveTime;

    /// <summary>현재 월드 아이템에 설정된 카탈로그 ID입니다.</summary>
    public string ItemId => itemId;

    /// <summary>현재 월드에 남아 있는 수량입니다.</summary>
    public int Amount => amount;

    /// <summary>
    /// 공용 풀에서 꺼낸 오브젝트에 아이템 ID와 수량을 주입합니다.
    /// 비활성 상태에서 호출하면 다음 OnEnable에서 해당 값으로 VFX와 습득 상태가 초기화됩니다.
    /// </summary>
    public void Configure(string newItemId, int newAmount)
    {
        itemId = string.IsNullOrWhiteSpace(newItemId) ? string.Empty : newItemId.Trim();
        amount = Mathf.Max(1, newAmount);
        initialAmount = amount;

        if (isActiveAndEnabled)
        {
            RefreshVisual();
        }
    }

    private void Awake()
    {
        initialAmount = amount;
        CacheLegacyComponents();
        RefreshVisual();
    }

    private void OnEnable()
    {
        // 풀에서 다시 사용될 때 프리팹에 설정한 원래 수량으로 복구합니다.
        amount = initialAmount;
        animationTime = Random.Range(0f, Mathf.PI * 2f);
        RefreshVisual();
    }

    private void LateUpdate()
    {
        if (visualRoot == null || !visualRoot.gameObject.activeSelf)
        {
            // ItemCatalogManager의 Awake가 더 늦게 실행된 경우 카탈로그 준비 후 다시 연결합니다.
            if (Time.unscaledTime >= nextVisualResolveTime)
            {
                nextVisualResolveTime = Time.unscaledTime + 0.5f;
                RefreshVisual();
            }

            return;
        }

        if (animateVisual)
        {
            animationTime += Time.deltaTime;
            float bobOffset = Mathf.Sin(animationTime * bobFrequency * Mathf.PI * 2f) * bobHeight;
            visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bobOffset;

            if (rotationSpeed > 0f)
            {
                visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }
        }
        else
        {
            visualRoot.localPosition = visualBaseLocalPosition;
        }
    }

    /// <summary>
    /// ItemData의 등급을 전역 VFX 관리자에 전달하고 기존 스프라이트/메시 표현을 숨깁니다.
    /// </summary>
    private void RefreshVisual()
    {
        CacheLegacyComponents();

        ItemData itemData = ResolveItemData();
        if (itemData == null)
        {
            SetLegacyVisualVisible(true);

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }

            RestoreInitialCollider();
            return;
        }

        SetLegacyVisualVisible(false);

        Transform legacySpriteRoot = transform.Find(LegacySpriteRootName);
        if (legacySpriteRoot != null)
        {
            legacySpriteRoot.gameObject.SetActive(false);
        }

        visualRoot = WorldItemRarityVfxManager.Instance.ApplyTo(
            transform,
            itemData.ItemClass,
            visualMaxSize,
            groundClearance);

        if (visualRoot == null)
        {
            RestoreInitialCollider();
            return;
        }

        visualBaseLocalPosition = visualRoot.localPosition;
        ResizePickupCollider(visualMaxSize, visualMaxSize);
    }

    /// <summary>itemId로 ItemCatalogManager에서 ItemData를 조회합니다.</summary>
    private ItemData ResolveItemData()
    {
        if (string.IsNullOrEmpty(itemId))
        {
            return null;
        }

        ItemCatalogManager catalogManager = ItemCatalogManager.Instance;
        if (catalogManager == null)
        {
            catalogManager = FindFirstObjectByType<ItemCatalogManager>();
        }

        return catalogManager != null ? catalogManager.FindItemData(itemId) : null;
    }

    private void CacheLegacyComponents()
    {
        if (pickupCollider == null)
        {
            pickupCollider = GetComponent<BoxCollider>();
        }

        if (pickupCollider != null && initialColliderSize == Vector3.zero)
        {
            initialColliderCenter = pickupCollider.center;
            initialColliderSize = pickupCollider.size;
        }

        // 등급 VFX가 생성되기 전에 기존 프리팹의 메시만 한 번 보관합니다.
        if (legacyMeshRenderers != null)
        {
            return;
        }

        legacyMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        legacyRendererInitialStates = new bool[legacyMeshRenderers.Length];

        for (int i = 0; i < legacyMeshRenderers.Length; i++)
        {
            legacyRendererInitialStates[i] =
                legacyMeshRenderers[i] != null && legacyMeshRenderers[i].enabled;
        }
    }

    private void ResizePickupCollider(float width, float height)
    {
        if (pickupCollider == null)
        {
            return;
        }

        pickupCollider.center = new Vector3(0f, groundClearance + height * 0.5f, 0f);
        pickupCollider.size = new Vector3(
            width + colliderPadding * 2f,
            height + colliderPadding * 2f,
            colliderDepth);
    }

    private void RestoreInitialCollider()
    {
        if (pickupCollider == null || initialColliderSize == Vector3.zero)
        {
            return;
        }

        pickupCollider.center = initialColliderCenter;
        pickupCollider.size = initialColliderSize;
    }

    private void SetLegacyVisualVisible(bool visible)
    {
        if (legacyMeshRenderers == null || legacyRendererInitialStates == null)
        {
            return;
        }

        for (int i = 0; i < legacyMeshRenderers.Length; i++)
        {
            if (legacyMeshRenderers[i] != null)
            {
                legacyMeshRenderers[i].enabled =
                    visible && legacyRendererInitialStates[i];
            }
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (string.IsNullOrEmpty(itemId) || amount <= 0)
        {
            return;
        }

        if (!PlayerReferenceResolver.IsInPlayerHierarchy(collision.gameObject))
        {
            return;
        }

        PlayerInventory inventory = PlayerReferenceResolver.FindComponentInPlayerHierarchy<PlayerInventory>(
            collision.gameObject);
        if (inventory == null)
        {
            inventory = PlayerReferenceResolver.FindPlayerComponent<PlayerInventory>();
        }

        if (inventory == null)
        {
            return;
        }

        int remaining = inventory.AddItem(itemId, amount);
        if (remaining > 0)
        {
            // 일부만 들어갔다면 남은 수량은 월드에 유지합니다.
            amount = remaining;
            return;
        }

        PooledWorldDrop pooledDrop = GetComponent<PooledWorldDrop>();
        if (pooledDrop == null || !pooledDrop.ReturnToPool())
        {
            // 풀에서 생성되지 않은 씬 배치 아이템만 직접 제거합니다.
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        visualMaxSize = Mathf.Max(0.01f, visualMaxSize);
        groundClearance = Mathf.Max(0f, groundClearance);
        bobHeight = Mathf.Max(0f, bobHeight);
        bobFrequency = Mathf.Max(0f, bobFrequency);
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        colliderPadding = Mathf.Max(0f, colliderPadding);
        colliderDepth = Mathf.Max(0.01f, colliderDepth);

        if (!Application.isPlaying || UnityEditor.EditorUtility.IsPersistent(gameObject))
        {
            return;
        }

        RefreshVisual();
    }
#endif
}
