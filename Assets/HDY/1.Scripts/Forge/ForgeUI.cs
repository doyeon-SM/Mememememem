using System.Collections.Generic;
using System.Linq;
using HDY.Item;
using HDY.Inventory;
using HDY.UI;
using KMS.InventoryDuped;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HDY.Forge
{
    /// <summary>대장간 UI의 현재 탭.</summary>
    public enum ForgeUITab
    {
        Enhance,
        Promotion,
        Refinement,
        Inheritance
    }

    /// <summary>
    /// 대장간(Forge) UI 컨트롤러.
    ///
    /// [슬롯 개념 - 중요] 하단 목록과 가운데 "선택 슬롯"은 전부 표시(view)일 뿐이다. 도구를 고른다고 해서
    /// 그 ItemStack이 인벤토리/창고에서 실제로 빠져나오지 않는다 - 원본 참조(selectedStack)를 그대로 들고
    /// 있다가, 강화/승급 시도 시 ForgeManager가 "그 자리에서" itemId만 갱신한다. 그래서 이 UI를 닫아도
    /// 잃어버릴 아이템이 없다(옮긴 적이 없으므로).
    ///
    /// [탭] 강화 탭은 CanEnhance=true인 도구만, 승급 탭은 지금 승급 가능한 상태(EligibleForPromotionNow)인
    /// 도구만 하단 목록에 보여준다. 정렬은 티어 내림차순 -> 강화레벨 내림차순.
    /// 연마/전승 탭은 각각 ForgeUI_RefinementPanel/ForgeUI_InheritancePanel이 전담한다 - 이 클래스는
    /// 탭 전환에 따라 두 패널의 GameObject를 켜고 끄는 라우팅만 담당한다(MemStorageUI_Grid/Info와 동일한 분리 패턴).
    ///
    /// [자동 전환] 강화로 10강을 찍으면 자동으로 승급 탭으로 전환하고 같은 아이템을 그대로 선택 상태로 둔다.
    /// 승급에 성공하면 선택을 해제한다(아이템 자체는 그 자리에서 다음 티어로 바뀐 채 남아있음).
    /// </summary>
    public class ForgeUI : MonoBehaviour
    {
        [Header("탭")]
        [SerializeField] private Button enhanceTabButton;
        [SerializeField] private Button promotionTabButton;
        [SerializeField] private Button refinementTabButton;
        [SerializeField] private Button inheritanceTabButton;
        [SerializeField] private GameObject enhanceTabSelectedMark;
        [SerializeField] private GameObject promotionTabSelectedMark;
        [SerializeField] private GameObject refinementTabSelectedMark;
        [SerializeField] private GameObject inheritanceTabSelectedMark;

        [Header("닫기 (선택)")]
        [SerializeField] private Button closeButton;

        [Header("강화/승급 전용 - 하단 목록 (10 x n 스크롤)")]
        [SerializeField] private Transform slotListContent;
        [SerializeField] private ForgeToolSlotUI slotPrefab;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private WarehouseInventory warehouseInventory;

        [Header("강화/승급 전용 - 가운데 패널 루트 (탭 전환 시 이 루트를 통째로 켜고 끔)")]
        [SerializeField] private GameObject enhancePromotionPanelRoot;

        [Header("가운데 - 선택 슬롯")]
        [SerializeField] private ForgeToolSlotUI selectedSlotDisplay;
        [SerializeField] private GameObject selectedEmptyHint;

        [Tooltip("가운데에 현재 강화/승급 대상 도구의 이름을 표시하는 텍스트 (선택 없으면 비움)")]
        [SerializeField] private TMP_Text selectedItemNameText;

        [Header("가운데 - 모루 과열")]
        [SerializeField] private Slider overheatSlider;
        [SerializeField] private TMP_Text overheatPercentText;

        [Header("가운데 - 확률/재료/골드")]
        [SerializeField] private TMP_Text successRateText;
        [SerializeField] private Image materialIconImage;
        [SerializeField] private TMP_Text materialCountText;
        [SerializeField] private TMP_Text goldCostText;

        [Header("가운데 - 실행 버튼")]
        [SerializeField] private Button actionButton;
        [SerializeField] private TMP_Text actionButtonLabel;
        [SerializeField] private CanvasGroup actionButtonGroup;
        [Range(0f, 1f)]
        [SerializeField] private float disabledButtonAlpha = 0.5f;

        [Header("부족 표시 색상")]
        [SerializeField] private Color normalTextColor = Color.white;
        [SerializeField] private Color shortageTextColor = Color.red;

        [Header("연마/전승 전용 - 패널 (탭 전환 시 GameObject를 켜고 끔)")]
        [SerializeField] private GameObject refinementPanelRoot;
        [SerializeField] private ForgeUI_RefinementPanel refinementPanel;
        [SerializeField] private GameObject inheritancePanelRoot;
        [SerializeField] private ForgeUI_InheritancePanel inheritancePanel;

        [Header("참조")]
        [SerializeField] private ForgeManager forgeManager;
        [SerializeField] private ItemCatalogManager catalogManager;

        private ForgeUITab currentTab = ForgeUITab.Enhance;
        private ItemStack selectedStack;
        private readonly List<ForgeToolSlotUI> spawnedSlots = new List<ForgeToolSlotUI>();

        private void Awake()
        {
            if (forgeManager == null) forgeManager = ForgeManager.Instance;
            catalogManager = ItemCatalogManager.Resolve(catalogManager);

            if (playerInventory == null) playerInventory = FindFirstObjectByType<PlayerInventory>();
            if (warehouseInventory == null) warehouseInventory = FindFirstObjectByType<WarehouseInventory>();

            if (enhanceTabButton != null) enhanceTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Enhance));
            if (promotionTabButton != null) promotionTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Promotion));
            if (refinementTabButton != null) refinementTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Refinement));
            if (inheritanceTabButton != null) inheritanceTabButton.onClick.AddListener(() => SwitchTab(ForgeUITab.Inheritance));
            if (actionButton != null) actionButton.onClick.AddListener(HandleActionButtonClicked);
            if (closeButton != null) closeButton.onClick.AddListener(() => UIManager.Instance?.CloseCurrent());

            if (selectedSlotDisplay != null) selectedSlotDisplay.Clicked += _ => ClearSelection();
        }

        private void OnEnable()
        {
            SubscribeInventoryEvents(true);
            SwitchTab(ForgeUITab.Enhance);
        }

        private void OnDisable()
        {
            SubscribeInventoryEvents(false);
        }

        private void SubscribeInventoryEvents(bool subscribe)
        {
            if (playerInventory != null)
            {
                if (subscribe) playerInventory.OnInventoryChanged += HandleContainersChanged;
                else playerInventory.OnInventoryChanged -= HandleContainersChanged;
            }

            if (warehouseInventory != null)
            {
                if (subscribe) warehouseInventory.OnStorageChanged += HandleContainersChanged;
                else warehouseInventory.OnStorageChanged -= HandleContainersChanged;
            }
        }

        private void HandleContainersChanged()
        {
            if (currentTab != ForgeUITab.Enhance && currentTab != ForgeUITab.Promotion) return;

            RefreshList();
            RefreshMiddlePanel();
        }

        private void SwitchTab(ForgeUITab tab)
        {
            currentTab = tab;

            if (enhanceTabSelectedMark != null) enhanceTabSelectedMark.SetActive(tab == ForgeUITab.Enhance);
            if (promotionTabSelectedMark != null) promotionTabSelectedMark.SetActive(tab == ForgeUITab.Promotion);
            if (refinementTabSelectedMark != null) refinementTabSelectedMark.SetActive(tab == ForgeUITab.Refinement);
            if (inheritanceTabSelectedMark != null) inheritanceTabSelectedMark.SetActive(tab == ForgeUITab.Inheritance);

            bool isEnhanceOrPromotion = tab == ForgeUITab.Enhance || tab == ForgeUITab.Promotion;

            // 강화/승급 전용 하단 목록 + 가운데 패널은 두 탭이 공유하므로 같이 켜고 끈다.
            if (slotListContent != null) slotListContent.gameObject.SetActive(isEnhanceOrPromotion);
            if (enhancePromotionPanelRoot != null) enhancePromotionPanelRoot.SetActive(isEnhanceOrPromotion);

            if (refinementPanelRoot != null) refinementPanelRoot.SetActive(tab == ForgeUITab.Refinement);
            if (inheritancePanelRoot != null) inheritancePanelRoot.SetActive(tab == ForgeUITab.Inheritance);

            if (isEnhanceOrPromotion)
            {
                if (actionButtonLabel != null) actionButtonLabel.text = tab == ForgeUITab.Enhance ? "강화하기" : "승급하기";
                RefreshList();
                RefreshMiddlePanel();
            }
        }

        /// <summary>하단 목록을 다시 스캔·필터링·정렬해서 그린다. 인벤토리/창고 변경 이벤트에서도 호출된다.</summary>
        private void RefreshList()
        {
            var entries = CollectForgeableTools();

            for (int i = 0; i < entries.Count; i++)
            {
                var slot = GetOrCreateSlot(i);
                var displayData = catalogManager != null ? catalogManager.FindItemData(entries[i].stack.itemId) : null;
                slot.Bind(entries[i].stack, displayData);
                slot.gameObject.SetActive(true);
            }

            for (int i = entries.Count; i < spawnedSlots.Count; i++)
            {
                spawnedSlots[i].Clear();
                spawnedSlots[i].gameObject.SetActive(false);
            }
        }

        private ForgeToolSlotUI GetOrCreateSlot(int index)
        {
            if (index < spawnedSlots.Count) return spawnedSlots[index];

            var slot = Instantiate(slotPrefab, slotListContent);
            slot.Clicked += HandleListSlotClicked;
            spawnedSlots.Add(slot);
            return slot;
        }

        /// <summary>인벤토리(일반+퀵슬롯) + 창고에서 대장간 대상 도구만 모아, 현재 탭 조건으로 필터링하고 정렬한다.</summary>
        private List<(ItemStack stack, ForgeItemDescriptor descriptor)> CollectForgeableTools()
        {
            var results = new List<(ItemStack, ForgeItemDescriptor)>();
            if (forgeManager == null) return results;

            void CollectFrom(InventoryContainer container)
            {
                if (container?.slots == null) return;

                foreach (var slot in container.slots)
                {
                    if (slot == null || slot.IsEmpty) continue;
                    if (!forgeManager.IsForgeableItem(slot.itemId)) continue;

                    var descriptor = forgeManager.Describe(slot);
                    if (!descriptor.IsForgeable) continue;

                    bool matchesTab = currentTab == ForgeUITab.Enhance
                        ? descriptor.CanEnhance
                        : descriptor.EligibleForPromotionNow;

                    if (!matchesTab) continue;

                    results.Add((slot, descriptor));
                }
            }

            if (playerInventory != null)
            {
                CollectFrom(playerInventory.inventory);
                CollectFrom(playerInventory.quickSlots);
            }

            if (warehouseInventory != null)
            {
                CollectFrom(warehouseInventory.storage);
            }

            // 높은 티어 > 강화순으로 정렬.
            return results
                .OrderByDescending(e => e.Item2.TierIndex)
                .ThenByDescending(e => e.Item2.EnhanceLevel)
                .ToList();
        }

        private void HandleListSlotClicked(ForgeToolSlotUI slot)
        {
            if (slot == null || slot.BoundStack == null) return;

            selectedStack = slot.BoundStack;
            RefreshMiddlePanel();
        }

        private void ClearSelection()
        {
            selectedStack = null;
            RefreshMiddlePanel();
        }

        /// <summary>가운데 패널(선택 슬롯/이름/과열/확률/재료/골드/버튼)을 현재 선택·탭 기준으로 다시 그린다.</summary>
        private void RefreshMiddlePanel()
        {
            bool hasSelection = selectedStack != null && !selectedStack.IsEmpty;

            if (selectedEmptyHint != null) selectedEmptyHint.SetActive(!hasSelection);

            if (!hasSelection)
            {
                selectedSlotDisplay?.Clear();
                if (selectedItemNameText != null) selectedItemNameText.text = string.Empty;
                SetActionButtonInteractable(false);

                if (overheatSlider != null) overheatSlider.value = 0f;
                if (overheatPercentText != null) overheatPercentText.text = "0%";
                if (successRateText != null) successRateText.text = "-";
                if (materialIconImage != null) materialIconImage.enabled = false;
                if (materialCountText != null) materialCountText.text = "-";
                if (goldCostText != null) goldCostText.text = "-";
                return;
            }

            var displayData = catalogManager != null ? catalogManager.FindItemData(selectedStack.itemId) : null;
            selectedSlotDisplay?.Bind(selectedStack, displayData);

            if (selectedItemNameText != null)
            {
                selectedItemNameText.text = displayData != null ? displayData.ItemName : string.Empty;
            }

            var actionType = currentTab == ForgeUITab.Enhance ? ForgeActionType.Enhance : ForgeActionType.Promotion;
            var preview = forgeManager.GetPreview(selectedStack, actionType);

            if (overheatSlider != null) overheatSlider.value = preview.OverheatPercent;
            if (overheatPercentText != null) overheatPercentText.text = FormatPercent(preview.OverheatPercent);

            if (successRateText != null)
            {
                successRateText.text = preview.IsGuaranteed ? "100% (보장)" : FormatPercent(preview.SuccessRate);
            }

            var materialItemData = !string.IsNullOrEmpty(preview.MaterialItemId) && catalogManager != null
                ? catalogManager.FindItemData(preview.MaterialItemId)
                : null;

            if (materialIconImage != null)
            {
                materialIconImage.sprite = materialItemData != null ? materialItemData.ItemIcon : null;
                materialIconImage.enabled = materialItemData != null && materialItemData.ItemIcon != null;
            }

            bool materialShortage = preview.MaterialOwned < preview.MaterialCost;
            if (materialCountText != null)
            {
                materialCountText.text = $"{preview.MaterialOwned} / {preview.MaterialCost}";
                materialCountText.color = materialShortage ? shortageTextColor : normalTextColor;
            }

            bool goldShortage = preview.GoldOwned < preview.GoldCost;
            if (goldCostText != null)
            {
                goldCostText.text = $"{preview.GoldOwned} / {preview.GoldCost}";
                goldCostText.color = goldShortage ? shortageTextColor : normalTextColor;
            }

            bool canExecute = preview.BlockReason == ForgeFailReason.None && !materialShortage && !goldShortage;
            SetActionButtonInteractable(canExecute);
        }

        private void SetActionButtonInteractable(bool enabled)
        {
            if (actionButton != null) actionButton.interactable = enabled;

            if (actionButtonGroup != null)
            {
                actionButtonGroup.alpha = enabled ? 1f : disabledButtonAlpha;
                actionButtonGroup.interactable = enabled;
                actionButtonGroup.blocksRaycasts = enabled;
            }
        }

        private void HandleActionButtonClicked()
        {
            if (selectedStack == null || selectedStack.IsEmpty || forgeManager == null) return;

            var outcome = currentTab == ForgeUITab.Enhance
                ? forgeManager.TryEnhance(selectedStack)
                : forgeManager.TryPromote(selectedStack);

            if (!outcome.Attempted) return;

            if (currentTab == ForgeUITab.Enhance)
            {
                // 강화 성공/실패와 무관하게 아이템은 슬롯(=원래 있던 인벤토리/창고 칸)에 그대로 유지된다.
                if (outcome.Result == ForgeAttemptResult.Success && outcome.ReachedMaxEnhanceLevel)
                {
                    // 10강 달성 - 승급 탭으로 자동 전환하고 같은 아이템을 그대로 선택 상태로 유지한다.
                    SwitchTab(ForgeUITab.Promotion);
                    return;
                }
            }
            else
            {
                if (outcome.Result == ForgeAttemptResult.Success)
                {
                    // 승급 성공 - 아이템 자체가 다음 티어로 바뀌었으므로 선택을 해제한다.
                    selectedStack = null;
                }
            }

            RefreshList();
            RefreshMiddlePanel();
        }

        private static string FormatPercent(float value01)
        {
            return $"{value01 * 100f:0.#}%";
        }
    }
}
