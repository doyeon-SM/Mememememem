using HDY;
using HDY.Item;
using KGH.Data;
using KMS;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어의 <see cref="PlayerInteraction"/> 포커스와 현재 선택 퀵슬롯 도구를 기준으로
/// 화면 UI의 이름, 체력, 상호작용 가능 여부를 갱신합니다.
/// </summary>
[DisallowMultipleComponent]
public class WorldObjectInfoUI : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("대상이 없을 때 숨길 UI 패널입니다. 이 컴포넌트가 붙은 오브젝트의 자식으로 지정하세요.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text objectNameText;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text interactionStatusText;

    [Header("Player References")]
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private ItemCatalogManager itemCatalogManager;

    [Header("Status Text")]
    [SerializeField] private string availableText = "상호작용 가능";
    [SerializeField] private string noToolText = "상호작용 불가: 도구를 장착해야 합니다.";
    [SerializeField] private string wrongToolTypeFormat = "상호작용 불가: {0} 채집 도구가 필요합니다.";
    [SerializeField] private string insufficientGradeFormat = "상호작용 불가: {0} 등급 이상의 도구가 필요합니다.";
    [SerializeField] private string depletedText = "상호작용 불가: 현재 고갈된 오브젝트입니다.";

    [Header("Chest Text")]
    [Tooltip("모든 Chest에 공통으로 표시할 한 줄 설명입니다.")]
    [SerializeField] private string chestTooltipText = "상자를 열어 아이템을 획득할 수 있습니다.";

    [Header("Status Color")]
    [SerializeField] private Color availableColor = new Color(0.35f, 1f, 0.45f, 1f);
    [SerializeField] private Color unavailableColor = new Color(1f, 0.35f, 0.35f, 1f);

    private WorldObject currentTarget;
    private Chest currentChest;
    private ItemData currentTool;
    private PlayerInteraction subscribedPlayerInteraction;

    /// <summary>현재 UI에 표시 중인 월드 오브젝트입니다.</summary>
    public WorldObject CurrentTarget => currentTarget;

    private void Awake()
    {
        ResolveRuntimeReferences();
        BindPlayerInteraction();
        SetPanelVisible(false);
    }

    private void OnEnable()
    {
        ResolveRuntimeReferences();
        BindPlayerInteraction();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnbindPlayerInteraction();
        ChangeTarget(null);
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

    /// <summary>현재 대상과 장착 도구를 다시 읽어 UI를 즉시 갱신합니다.</summary>
    public void RefreshUI()
    {
        if (currentTarget == null && currentChest == null)
        {
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);

        if (currentChest != null)
        {
            RefreshChestUI();
            return;
        }

        SetHealthUIVisible(true);

        if (objectNameText != null)
        {
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

        RefreshInteractionStatus();
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

    private void ResolveRuntimeReferences()
    {
        if (playerInteraction == null)
        {
            playerInteraction = FindFirstObjectByType<PlayerInteraction>();
        }

        if (playerInventory == null)
        {
            playerInventory = playerInteraction != null
                ? playerInteraction.GetComponentInParent<PlayerInventory>()
                : FindFirstObjectByType<PlayerInventory>();
        }

        if (itemCatalogManager == null)
        {
            itemCatalogManager = ItemCatalogManager.Instance;
        }

        if (itemCatalogManager == null)
        {
            itemCatalogManager = FindFirstObjectByType<ItemCatalogManager>();
        }
    }

    private void BindPlayerInteraction()
    {
        if (object.ReferenceEquals(subscribedPlayerInteraction, playerInteraction))
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
        SetHealthUIVisible(false);

        if (objectNameText != null)
        {
            objectNameText.text = currentChest.DisplayName;
        }

        if (interactionStatusText != null)
        {
            interactionStatusText.text = chestTooltipText;
            interactionStatusText.color = availableColor;
        }
    }

    private void SetHealthUIVisible(bool visible)
    {
        if (healthSlider != null)
        {
            healthSlider.gameObject.SetActive(visible);
        }

        if (healthValueText != null)
        {
            healthValueText.gameObject.SetActive(visible);
        }
    }

    private void RefreshInteractionStatus()
    {
        if (interactionStatusText == null || currentTarget == null)
        {
            return;
        }

        WorldObjectInteractionState state = currentTarget.EvaluateInteraction(currentTool);
        interactionStatusText.color = state == WorldObjectInteractionState.Available
            ? availableColor
            : unavailableColor;

        switch (state)
        {
            case WorldObjectInteractionState.Available:
                interactionStatusText.text = availableText;
                break;
            case WorldObjectInteractionState.NoToolEquipped:
                interactionStatusText.text = noToolText;
                break;
            case WorldObjectInteractionState.WrongToolType:
                interactionStatusText.text = string.Format(
                    wrongToolTypeFormat,
                    GetObjectTypeDisplayName(currentTarget.RequiredToolType));
                break;
            case WorldObjectInteractionState.InsufficientToolGrade:
                interactionStatusText.text = string.Format(
                    insufficientGradeFormat,
                    GetGradeDisplayName(currentTarget.RequiredToolGrade));
                break;
            case WorldObjectInteractionState.Depleted:
                interactionStatusText.text = depletedText;
                break;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot != null && panelRoot != gameObject)
        {
            panelRoot.SetActive(visible);
        }
    }

    private static string GetObjectTypeDisplayName(ObjectType objectType)
    {
        switch (objectType)
        {
            case ObjectType.Tree:
                return "나무";
            case ObjectType.Stone:
                return "광석";
            case ObjectType.Bush:
                return "수풀";
            default:
                return "지정된 타입";
        }
    }

    private static string GetGradeDisplayName(CommonClass grade)
    {
        switch (grade)
        {
            case CommonClass.Rare:
                return "레어";
            case CommonClass.Epic:
                return "에픽";
            case CommonClass.Unique:
                return "유니크";
            case CommonClass.Legendary:
                return "레전더리";
            case CommonClass.Myth:
                return "신화";
            default:
                return grade.ToString();
        }
    }
}
