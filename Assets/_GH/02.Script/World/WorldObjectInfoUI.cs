using HDY.Item;
using KMS;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerInteraction이 감지한 상자 또는 월드 오브젝트의 정보를 화면에 표시합니다.
/// 상자는 별도의 툴팁 프리팹을 사용하고, 월드 오브젝트는 대상 상단에 이름과 체력만 표시합니다.
/// </summary>
[DisallowMultipleComponent]
public class WorldObjectInfoUI : MonoBehaviour
{
    [Header("World Object UI")]
    [Tooltip("월드 오브젝트의 이름과 체력바가 들어 있는 기존 패널입니다.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text objectNameText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthValueText;
    [Tooltip("이전 설명용 텍스트입니다. 새 구조에서는 항상 숨깁니다.")]
    [SerializeField] private TMP_Text interactionStatusText;
    [SerializeField] private bool showHealthValueText;

    [Header("Chest Tooltip")]
    [Tooltip("상자를 감지했을 때 생성할 UI 프리팹입니다.")]
    [SerializeField] private GameObject chestTooltipPrefab;
    [Tooltip("상자 툴팁 프리팹의 하위 Icon Image에 전달할 Sprite입니다.")]
    [SerializeField] private Sprite chestInteractionSprite;
    [Tooltip("비워 두면 기존 월드 오브젝트 패널과 같은 부모 아래에 생성합니다.")]
    [SerializeField] private Transform tooltipParent;
    [Tooltip("Chest Tooltip Prefab을 생성할 때 적용할 Canvas 기준 Anchored Position입니다.")]
    [SerializeField] private Vector2 chestTooltipAnchoredPosition;

    [Header("Target Position")]
    [Tooltip("Renderer/Collider의 최상단을 기준으로 추가할 월드 좌표 오프셋입니다.")]
    [SerializeField] private Vector3 worldAnchorOffset = new Vector3(0f, 0.25f, 0f);
    [Tooltip("월드 좌표를 화면 좌표로 변환할 카메라입니다. 비워 두면 Main Camera를 사용합니다.")]
    [SerializeField] private Camera worldCamera;
    [SerializeField] private bool hideWhenBehindCamera = true;

    [Header("Player References")]
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ItemCatalogManager itemCatalogManager;

    [Header("Name Color")]
    [SerializeField] private Color availableColor = Color.white;
    [SerializeField] private Color unavailableColor = new Color(1f, 0.35f, 0.35f, 1f);

    private WorldObject currentTarget;
    private Chest currentChest;
    private ItemData currentTool;
    private PlayerInteraction subscribedPlayerInteraction;
    private GameObject chestTooltipInstance;
    private RectTransform chestTooltipRect;
    private Image chestTooltipIcon;
    private TMP_Text chestTooltipNameText;
    private float nextPlayerReferenceResolveTime;

    private const float PlayerReferenceRetryInterval = 0.5f;

    /// <summary>현재 UI에 표시 중인 월드 오브젝트입니다.</summary>
    public WorldObject CurrentTarget => currentTarget;

    private void Awake()
    {
        ResolveRuntimeReferences(true);
        BindPlayerInteraction();
        HideLegacyDescription();
        SetWorldPanelVisible(false);
        SetChestTooltipVisible(false);
    }

    private void OnEnable()
    {
        ResolveRuntimeReferences(true);
        BindPlayerInteraction();
        HideLegacyDescription();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnbindPlayerInteraction();
        ChangeTarget(null);
        SetWorldPanelVisible(false);
        SetChestTooltipVisible(false);
    }

    private void OnDestroy()
    {
        if (chestTooltipInstance != null)
        {
            Destroy(chestTooltipInstance);
        }
    }

    private void Update()
    {
        ResolveRuntimeReferences();
        BindPlayerInteraction();

        ItemData selectedTool = ResolveSelectedTool();
        if (selectedTool != currentTool)
        {
            currentTool = selectedTool;
            RefreshUI();
        }
    }

    private void LateUpdate()
    {
        UpdateFocusedUIPosition();
    }

    /// <summary>현재 대상과 장착 도구를 다시 읽어 UI를 즉시 갱신합니다.</summary>
    public void RefreshUI()
    {
        HideLegacyDescription();

        if (currentTarget == null && currentChest == null)
        {
            SetWorldPanelVisible(false);
            SetChestTooltipVisible(false);
            return;
        }

        if (currentChest != null)
        {
            RefreshChestUI();
            return;
        }

        SetChestTooltipVisible(false);
        SetWorldPanelVisible(true);
        SetHealthUIVisible(true);

        if (objectNameText != null)
        {
            objectNameText.gameObject.SetActive(true);
            objectNameText.text = currentTarget.DisplayName;
        }

        int maxHp = Mathf.Max(1, currentTarget.MaxHp);
        int currentHp = Mathf.Clamp(currentTarget.CurrentHp, 0, maxHp);

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = maxHp;
            healthSlider.value = currentHp;
            healthSlider.wholeNumbers = true;
        }

        if (healthValueText != null)
        {
            healthValueText.text = $"{currentHp} / {maxHp}";
        }

        RefreshWorldObjectAvailability();
        UpdateFocusedUIPosition();
    }

    private void ChangeTarget(WorldObject newTarget, Chest newChest = null)
    {
        if (ReferenceEquals(currentTarget, newTarget) && ReferenceEquals(currentChest, newChest))
        {
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.StateChanged -= HandleTargetStateChanged;
        }

        currentTarget = newTarget;
        currentChest = newChest;

        if (currentTarget != null)
        {
            currentTarget.StateChanged += HandleTargetStateChanged;
        }

        currentTool = ResolveSelectedTool();
        RefreshUI();
    }

    private void HandleTargetStateChanged(WorldObject changedTarget)
    {
        if (changedTarget == currentTarget)
        {
            RefreshUI();
        }
    }

    private ItemData ResolveSelectedTool()
    {
        if (playerInventory == null || itemCatalogManager == null)
        {
            return null;
        }

        ItemStack selectedSlot = playerInventory.GetSelectedQuickSlot();
        if (selectedSlot == null || selectedSlot.IsEmpty)
        {
            return null;
        }

        return itemCatalogManager.FindItemData(selectedSlot.itemId);
    }

    private void ResolveRuntimeReferences(bool forcePlayerSearch = false)
    {
        bool needsPlayerReference = playerInteraction == null || playerInventory == null;
        if (needsPlayerReference
            && (forcePlayerSearch || Time.unscaledTime >= nextPlayerReferenceResolveTime))
        {
            nextPlayerReferenceResolveTime = Time.unscaledTime + PlayerReferenceRetryInterval;
            GameObject playerObject = playerInteraction != null
                ? playerInteraction.gameObject
                : PlayerReferenceResolver.FindPlayerObject();

            playerInteraction = PlayerReferenceResolver.ResolveComponent(playerInteraction, playerObject);
            playerInventory = PlayerReferenceResolver.ResolveComponent(playerInventory, playerObject);
        }

        if (itemCatalogManager == null)
        {
            itemCatalogManager = ItemCatalogManager.Instance;
        }

        if (itemCatalogManager == null)
        {
            itemCatalogManager = FindFirstObjectByType<ItemCatalogManager>();
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }
    }

    private void BindPlayerInteraction()
    {
        if (ReferenceEquals(subscribedPlayerInteraction, playerInteraction))
        {
            return;
        }

        UnbindPlayerInteraction();
        subscribedPlayerInteraction = playerInteraction;

        if (subscribedPlayerInteraction == null)
        {
            ChangeTarget(null);
            return;
        }

        subscribedPlayerInteraction.FocusChanged += HandleFocusChanged;
        HandleFocusChanged(subscribedPlayerInteraction.CurrentInteractable);
    }

    private void UnbindPlayerInteraction()
    {
        if (subscribedPlayerInteraction != null)
        {
            subscribedPlayerInteraction.FocusChanged -= HandleFocusChanged;
        }

        subscribedPlayerInteraction = null;
    }

    private void HandleFocusChanged(IInteractable interactable)
    {
        ChangeTarget(interactable as WorldObject, interactable as Chest);
    }

    private void RefreshChestUI()
    {
        if (EnsureChestTooltip())
        {
            SetWorldPanelVisible(false);

            if (chestTooltipIcon != null)
            {
                chestTooltipIcon.sprite = chestInteractionSprite;
                chestTooltipIcon.enabled = chestInteractionSprite != null;
            }

            if (chestTooltipNameText != null)
            {
                chestTooltipNameText.text = currentChest.DisplayName;
            }

            SetChestTooltipVisible(true);
            return;
        }

        // 프리팹이 아직 연결되지 않은 동안에는 기존 패널로 상자 이름만 표시합니다.
        SetChestTooltipVisible(false);
        SetWorldPanelVisible(true);
        SetHealthUIVisible(false);

        if (objectNameText != null)
        {
            objectNameText.gameObject.SetActive(true);
            objectNameText.text = currentChest.DisplayName;
            objectNameText.color = availableColor;
        }
    }

    private bool EnsureChestTooltip()
    {
        if (chestTooltipInstance != null)
        {
            return true;
        }

        if (chestTooltipPrefab == null)
        {
            return false;
        }

        Transform parent = ResolveTooltipParent();
        chestTooltipInstance = Instantiate(chestTooltipPrefab, parent, false);
        chestTooltipInstance.name = $"{chestTooltipPrefab.name} (Runtime)";
        chestTooltipRect = chestTooltipInstance.GetComponent<RectTransform>();

        if (chestTooltipRect != null)
        {
            chestTooltipRect.anchoredPosition = chestTooltipAnchoredPosition;
        }

        CacheChestTooltipReferences(chestTooltipInstance);
        chestTooltipInstance.SetActive(false);
        return true;
    }

    private void CacheChestTooltipReferences(GameObject tooltipObject)
    {
        if (tooltipObject == null)
        {
            return;
        }

        chestTooltipIcon = FindChestIcon(tooltipObject);
        chestTooltipNameText = FindChestNameText(tooltipObject);
    }

    private Transform ResolveTooltipParent()
    {
        if (tooltipParent != null)
        {
            return tooltipParent;
        }

        if (panelRoot != null && panelRoot.transform.parent != null)
        {
            return panelRoot.transform.parent;
        }

        return transform;
    }

    private static Image FindChestIcon(GameObject tooltipRoot)
    {
        Image[] images = tooltipRoot.GetComponentsInChildren<Image>(true);
        Image fallback = null;
        Image emptySpriteFallback = null;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image.gameObject == tooltipRoot)
            {
                continue;
            }

            fallback ??= image;
            if (image.sprite == null)
            {
                emptySpriteFallback ??= image;
            }

            string lowerName = image.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("icon")
                || lowerName.Contains("key")
                || lowerName.Contains("input")
                || lowerName.Contains("button"))
            {
                return image;
            }
        }

        return emptySpriteFallback != null ? emptySpriteFallback : fallback;
    }

    private static TMP_Text FindChestNameText(GameObject tooltipRoot)
    {
        TMP_Text[] texts = tooltipRoot.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text fallback = null;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
            {
                continue;
            }

            fallback ??= text;
            string lowerName = text.gameObject.name.ToLowerInvariant();
            if (lowerName.Contains("name")
                || lowerName.Contains("title")
                || lowerName.Contains("label"))
            {
                return text;
            }
        }

        return fallback;
    }

    private void RefreshWorldObjectAvailability()
    {
        if (objectNameText == null || currentTarget == null)
        {
            return;
        }

        WorldObjectInteractionState state = currentTarget.EvaluateInteraction(playerInventory);
        objectNameText.color = state == WorldObjectInteractionState.Available
            ? availableColor
            : unavailableColor;
    }

    private void SetHealthUIVisible(bool visible)
    {
        if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(visible);
        }

        if (healthValueText != null)
        {
            healthValueText.gameObject.SetActive(visible && showHealthValueText);
        }
    }

    private void HideLegacyDescription()
    {
        if (interactionStatusText != null)
        {
            interactionStatusText.gameObject.SetActive(false);
        }
    }

    private void SetWorldPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
        }
    }

    private void SetChestTooltipVisible(bool visible)
    {
        if (chestTooltipInstance != null)
        {
            chestTooltipInstance.SetActive(visible);
        }
    }

    private void UpdateFocusedUIPosition()
    {
        // 상자 툴팁은 Canvas의 고정 좌표를 사용하므로 이 위치 갱신에서 제외합니다.
        if (currentTarget == null)
        {
            return;
        }

        RectTransform displayRect = panelRoot != null
            ? panelRoot.transform as RectTransform
            : null;

        if (displayRect == null)
        {
            return;
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
        {
            return;
        }

        Vector3 worldPosition = CalculateTargetTop(currentTarget) + worldAnchorOffset;
        Vector3 screenPosition = cameraToUse.WorldToScreenPoint(worldPosition);
        bool isBehindCamera = screenPosition.z <= 0f;

        if (hideWhenBehindCamera)
        {
            displayRect.gameObject.SetActive(!isBehindCamera);
        }

        if (isBehindCamera)
        {
            return;
        }

        RectTransform parentRect = displayRect.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        Canvas canvas = displayRect.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            displayRect.position = parentRect.TransformPoint(localPoint);
        }
    }

    private static Vector3 CalculateTargetTop(Component target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds combinedBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            return target.transform.position;
        }

        return new Vector3(
            combinedBounds.center.x,
            combinedBounds.max.y,
            combinedBounds.center.z);
    }
}
