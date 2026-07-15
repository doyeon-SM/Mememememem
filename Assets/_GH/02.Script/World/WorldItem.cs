using HDY.Item;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.Rendering;

public class WorldItem : MonoBehaviour
{
    private const string VisualRootName = "World Item Sprite Visual";
    private const string SpriteObjectName = "Double Sided Sprite";

    [Header("Ref")]
    [SerializeField] private ItemData itemdata;
    [SerializeField] private int amount = 1;

    [Header("Sprite Visual")]
    [Tooltip("아이콘의 가로/세로 중 긴 쪽이 월드에서 차지할 최대 크기입니다.")]
    [Min(0.01f)] [SerializeField] private float visualMaxSize = 0.8f;
    [Tooltip("스프라이트 하단과 바닥 사이의 간격입니다.")]
    [Min(0f)] [SerializeField] private float groundClearance = 0.03f;

    [Header("Visual Motion")]
    [SerializeField] private bool animateVisual = true;
    [SerializeField] private float rotationSpeed = 45f;
    [Min(0f)] [SerializeField] private float bobHeight = 0.06f;
    [Min(0f)] [SerializeField] private float bobFrequency = 1.5f;

    [Header("Pickup Collider")]
    [Tooltip("비워 두면 이 오브젝트의 BoxCollider를 자동으로 사용합니다.")]
    [SerializeField] private BoxCollider pickupCollider;
    [Min(0f)] [SerializeField] private float colliderPadding = 0.05f;
    [Min(0.01f)] [SerializeField] private float colliderDepth = 0.16f;

    private int initialAmount;
    private Transform visualRoot;
    private SpriteRenderer spriteRenderer;
    private MeshRenderer[] legacyMeshRenderers;
    private bool[] legacyRendererInitialStates;
    private Vector3 initialColliderCenter;
    private Vector3 initialColliderSize;
    private float animationTime;
    private Vector3 visualBaseLocalPosition;

    private void Awake()
    {
        initialAmount = amount;
        CacheLegacyComponents();
        RefreshVisual();
    }

    private void OnEnable()
    {
        // 풀에서 다시 사용될 때 프리팹에 설정된 원래 수량으로 복구한다.
        amount = initialAmount;
        animationTime = Random.Range(0f, Mathf.PI * 2f);
        RefreshVisual();
    }

    private void Update()
    {
        if (!animateVisual || visualRoot == null || !visualRoot.gameObject.activeSelf)
        {
            return;
        }

        animationTime += Time.deltaTime;
        float bobOffset = Mathf.Sin(animationTime * bobFrequency * Mathf.PI * 2f) * bobHeight;
        visualRoot.localPosition = visualBaseLocalPosition + Vector3.up * bobOffset;
        visualRoot.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
    }

    /// <summary>
    /// ItemData 아이콘으로 양면 월드 스프라이트를 만들고 표시 비율에 맞춰 픽업 콜라이더를 조정합니다.
    /// SpriteRenderer 기본 머티리얼은 Cull Off이므로 한 장의 평면이 앞뒤 모두 표시됩니다.
    /// </summary>
    private void RefreshVisual()
    {
        CacheLegacyComponents();

        Sprite itemSprite = itemdata != null ? itemdata.ItemIcon : null;
        if (itemSprite == null)
        {
            SetLegacyVisualVisible(true);

            if (visualRoot != null)
            {
                visualRoot.gameObject.SetActive(false);
            }

            RestoreInitialCollider();
            return;
        }

        EnsureSpriteVisual();
        SetLegacyVisualVisible(false);

        Bounds spriteBounds = itemSprite.bounds;
        float longestSide = Mathf.Max(spriteBounds.size.x, spriteBounds.size.y);
        float spriteScale = longestSide > Mathf.Epsilon ? visualMaxSize / longestSide : 1f;
        float width = Mathf.Max(0.01f, spriteBounds.size.x * spriteScale);
        float height = Mathf.Max(0.01f, spriteBounds.size.y * spriteScale);

        spriteRenderer.sprite = itemSprite;
        spriteRenderer.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);

        // 피벗이 제각각이어도 실제 스프라이트 바운드가 X축 중앙과 바닥 위에 오도록 보정한다.
        spriteRenderer.transform.localPosition = new Vector3(
            -spriteBounds.center.x * spriteScale,
            groundClearance - spriteBounds.min.y * spriteScale,
            0f);
        spriteRenderer.transform.localRotation = Quaternion.identity;

        visualBaseLocalPosition = Vector3.zero;
        visualRoot.localPosition = visualBaseLocalPosition;
        visualRoot.gameObject.SetActive(true);

        ResizePickupCollider(width, height);
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

        if (legacyMeshRenderers != null)
        {
            return;
        }

        legacyMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        legacyRendererInitialStates = new bool[legacyMeshRenderers.Length];

        for (int i = 0; i < legacyMeshRenderers.Length; i++)
        {
            legacyRendererInitialStates[i] = legacyMeshRenderers[i] != null && legacyMeshRenderers[i].enabled;
        }
    }

    private void EnsureSpriteVisual()
    {
        if (visualRoot == null)
        {
            Transform existingVisual = transform.Find(VisualRootName);
            if (existingVisual != null)
            {
                visualRoot = existingVisual;
            }
            else
            {
                GameObject visualObject = new GameObject(VisualRootName);
                visualRoot = visualObject.transform;
                visualRoot.SetParent(transform, false);
            }
        }

        if (spriteRenderer == null)
        {
            Transform existingSprite = visualRoot.Find(SpriteObjectName);
            if (existingSprite == null)
            {
                GameObject spriteObject = new GameObject(SpriteObjectName);
                existingSprite = spriteObject.transform;
                existingSprite.SetParent(visualRoot, false);
            }

            spriteRenderer = existingSprite.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = existingSprite.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        spriteRenderer.shadowCastingMode = ShadowCastingMode.Off;
        spriteRenderer.receiveShadows = false;
        spriteRenderer.lightProbeUsage = LightProbeUsage.Off;
        spriteRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
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
                legacyMeshRenderers[i].enabled = visible && legacyRendererInitialStates[i];
            }
        }
    }

    private void OnTriggerEnter(Collider collision)
    {
        if (itemdata == null || amount <= 0)
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

        int remaining = inventory.AddItem(itemdata.Item_ID, amount);
        if (remaining > 0)
        {
            // 일부만 들어갔다면 남은 수량은 월드에 유지한다.
            amount = remaining;
            return;
        }

        PooledWorldDrop pooledDrop = GetComponent<PooledWorldDrop>();
        if (pooledDrop == null || !pooledDrop.ReturnToPool())
        {
            // 풀에서 생성되지 않은 씬 배치 아이템만 예외적으로 제거한다.
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
