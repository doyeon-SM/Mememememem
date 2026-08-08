using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HDY.Item;

namespace KMS.InventoryDuped
{

/// <summary>
/// 인벤토리/퀵슬롯/창고 공용 슬롯 UI. [HDY 요청] owner를 IInventorySlotOwner 인터페이스로 일반화하고
/// isQuickSlot(bool) 대신 SlotGroup(enum)을 사용해서, InventoryUI(플레이어 전용)와 WarehouseUI(창고+인벤토리
/// 통합) 양쪽 모두에서 이 컴포넌트를 그대로 재사용할 수 있게 했다.
///
/// [HDY 요청 - KMS 크로스 승인 - 내구도] 내구도가 있는 아이템(ItemData.MaxDurability > 0)을 표시할 때
/// 아이콘 아래에 Slider(durabilityBarRoot)를 내구도 비율만큼 채워서 보여준다. Slider의 min/max/wholeNumbers는
/// 인스펙터 설정에 의존하지 않도록 매번 코드에서 강제로 0~1 범위로 맞춘다. Fill 색상은 비율에 따라
/// 연두(40~100%) -> 주황(10~40%) -> 빨강(0~10%)으로 바뀐다. 이 컴포넌트는 플레이어 인벤토리/퀵슬롯/영지
/// 창고/트래시 등 모든 슬롯에서 공용으로 재사용되므로, 여기 한 곳만 수정하면 모든 곳에 자동으로 반영된다.
/// durabilityBarRoot(Slider)/durabilityFillImage(Slider의 Fill Image)는 프리팹에서 직접 배선해야 한다
/// (Inspector 작업, 도연 담당) - 배선 전까지는 null 체크로 안전하게 무시된다.
/// </summary>
public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    public Image itemIcon;
    public TMP_Text amountText;
    public TMP_Text keyText;
    public GameObject selectedFrame;

    public SlotGroup group;
    public int slotIndex;

    [SerializeField] private GameObject emptyPlaceholder;

    // [HDY 요청 - KMS 크로스 승인 - 내구도] 내구도 있는 아이템만 표시되는 Slider 바.
    // durabilityBarRoot: Slider 자체(활성/비활성 토글 + value로 내구도 비율 표시 겸용).
    // durabilityFillImage: Slider의 Fill Image - 비율에 따라 색상(연두/주황/빨강)만 여기서 바꾼다.
    [Header("내구도 바 (내구도 있는 아이템만 표시, Slider)")]
    [SerializeField] private Slider durabilityBarRoot;
    [SerializeField] private Image durabilityFillImage;

    // [HDY 요청 - KMS 크로스 승인 - 내구도] 내구도 비율 구간별 Fill 색상.
    // 40%~100% = 연두색, 10%~40% = 주황색, 0~10% = 빨간색.
    private const float DurabilityGreenThreshold = 0.4f;
    private const float DurabilityOrangeThreshold = 0.1f;
    private static readonly Color DurabilityColorGreen = new Color(0.62f, 0.85f, 0.27f);
    private static readonly Color DurabilityColorOrange = new Color(1f, 0.6f, 0.1f);
    private static readonly Color DurabilityColorRed = new Color(0.9f, 0.2f, 0.2f);

    // [HDY 요청] ItemStack.itemId(string)로 실제 ItemData(아이콘 등)를 조회하기 위한 참조.
    [SerializeField] private ItemCatalogManager catalogManager;

    private IInventorySlotOwner owner;
    private ItemStack currentStack;
    private ScrollRect activeScrollRect;

    private void Awake()
    {
        catalogManager = ItemCatalogManager.Resolve(catalogManager);

        // [HDY 요청 - KMS 크로스 승인 - 내구도] 순수 표시용 바이므로 플레이어가 드래그해서 값을 바꾸지
        // 못하도록 막는다. 실제 데이터는 항상 SetStack()에서 다시 덮어쓰므로 드래그해도 즉시 원복되지만,
        // 애초에 조작 가능한 것처럼 보이지 않도록 처리한다.
        if (durabilityBarRoot != null) durabilityBarRoot.interactable = false;
    }

    public void Initialize(IInventorySlotOwner newOwner, SlotGroup newGroup, int index)
    {
        owner = newOwner;
        group = newGroup;
        slotIndex = index;

        if (newGroup == SlotGroup.Trash && keyText != null)
        {
            keyText.gameObject.SetActive(false);
        }

        SetSelected(false);
    }

    public void SetStack(ItemStack stack)
    {
        currentStack = stack;

        bool hasItem = stack != null && !stack.IsEmpty;
        if (emptyPlaceholder != null) emptyPlaceholder.SetActive(!hasItem);

        if (hasItem && catalogManager == null)
        {
            catalogManager = ItemCatalogManager.Resolve(null);
        }

        ItemData data = (hasItem && catalogManager != null) ? catalogManager.FindItemData(stack.itemId) : null;

        if (itemIcon != null)
        {
            itemIcon.enabled = data != null && data.ItemIcon != null;
            itemIcon.sprite = data != null ? data.ItemIcon : null;
        }

        if (amountText != null)
        {
            amountText.gameObject.SetActive(hasItem && stack.amount > 1);
            amountText.text = hasItem ? stack.amount.ToString() : string.Empty;
        }

        // [HDY 요청 - KMS 크로스 승인 - 내구도] 내구도 있는 아이템일 때만 바를 켜고 비율만큼 채운다.
        // stack.durability가 아직 초기화되지 않았으면(-1, 구버전 세이브 등) 최대치로 표시한다(실제 값
        // 보정은 PlayerInventory.DamageQuickSlotToolDurability에서 처음 사용될 때 이루어진다).
        bool hasDurability = hasItem && data != null && data.MaxDurability > 0;

        if (durabilityBarRoot != null) durabilityBarRoot.gameObject.SetActive(hasDurability);

        if (hasDurability)
        {
            int current = stack.durability >= 0 ? stack.durability : data.MaxDurability;
            float ratio = data.MaxDurability > 0 ? Mathf.Clamp01((float)current / data.MaxDurability) : 0f;

            if (durabilityBarRoot != null)
            {
                // 인스펙터의 min/max 설정에 기대지 않도록 매번 0~1 범위로 강제한다.
                durabilityBarRoot.minValue = 0f;
                durabilityBarRoot.maxValue = 1f;
                durabilityBarRoot.wholeNumbers = false;
                durabilityBarRoot.value = ratio;
            }

            if (durabilityFillImage != null)
            {
                durabilityFillImage.color = GetDurabilityColor(ratio);
            }
        }
    }

    /// <summary>내구도 비율에 따른 바 색상을 결정한다. 40%~100%=연두, 10%~40%=주황, 0~10%=빨강.</summary>
    private static Color GetDurabilityColor(float ratio)
    {
        if (ratio >= DurabilityGreenThreshold) return DurabilityColorGreen;
        if (ratio >= DurabilityOrangeThreshold) return DurabilityColorOrange;
        return DurabilityColorRed;
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null) selectedFrame.SetActive(selected);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner)
        {
            activeScrollRect = GetComponentInParent<ScrollRect>();
            if (activeScrollRect != null)
            {
                activeScrollRect.OnInitializePotentialDrag(eventData);
                activeScrollRect.OnBeginDrag(eventData);
            }
            return;
        }
        owner?.BeginSlotDrag(this, currentStack, eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner)
        {
            activeScrollRect?.OnDrag(eventData);
            return;
        }
        owner?.MoveSlotDrag(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner)
        {
            activeScrollRect?.OnEndDrag(eventData);
            activeScrollRect = null;
            return;
        }

        InventorySlotUI target = eventData.pointerCurrentRaycast.gameObject != null ?
            eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlotUI>() : null;

        owner?.EndSlotDrag(target);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner is IInventorySlotClickOwner clickOwner)
        {
            clickOwner.ClickSlot(this, eventData.button, eventData.position);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowItemTooltip(currentStack, eventData.position);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        owner?.MoveItemTooltip(eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideItemTooltip();
    }
}

}
