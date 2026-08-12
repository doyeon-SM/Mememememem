using DG.Tweening;
using HDY.Capture;
using HDY.Cook;
using HDY.Inventory;
using HDY.Item;
using HDY.Recipe;
using KMS.InventoryDuped;
using MemSystem.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CampFirePanelUI : MonoBehaviour
{
    public static CampFirePanelUI Instance { get; private set; }

    private enum CookingUIState { Default, SelectFood, Cooking }
    private CookingUIState currentUIState = CookingUIState.Default;

    [Header("상단 - 건물 이름")]
    [SerializeField] private TextMeshProUGUI buildingName;

    [Header("Center - 2단 모드 설정")]
    [SerializeField] private GameObject centerMemModeObject;            // [1] 멤 배치모드 (Default, Cooking 시 활성화)
    [SerializeField] private MemSlotUI singleMemSlot;

    [SerializeField] private GameObject centerSelectedFoodModeObject;   // [2] 선택한 요리모드 (SelectFood 시 활성화)
    [SerializeField] private Image selectionImage;
    [SerializeField] private TextMeshProUGUI selectionName;

    [Header("Bottom - [1] Default Mode (기본모드)")]
    [SerializeField] private GameObject bottomDefaultModeObject;
    [SerializeField] private Transform recipeGridParent;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("Bottom - [2] Select Mode (선택모드)")]
    [SerializeField] private GameObject bottomSelectFoodModeObject;
    [SerializeField] private Transform requiredListParent;
    [SerializeField] private GameObject requireMaterialPrefab;
    [SerializeField] private TextMeshProUGUI productAmountText;
    [SerializeField] private Button btnMinus;
    [SerializeField] private Button btnPlus;
    [SerializeField] private TextMeshProUGUI cookingDurationText;
    [SerializeField] private Button reSelectBtn;
    [SerializeField] private Button cookBtn;

    [Header("Bottom - [3] Cooking Mode (요리모드)")]
    [SerializeField] private GameObject bottomCookingModeObject;
    [SerializeField] private Image cookingItemIcon;
    [SerializeField] private Button cookingItemIconButton;         // 요리 아이콘 클릭 수령용 버튼
    [SerializeField] private TextMeshProUGUI cookingItemName;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI cookingStatusText;    // 상태 텍스트 ("요리중 .", "요리 완료!", "식량이 부족합니다" 등)
    [SerializeField] private TextMeshProUGUI cookingSpeedText;     // 요리 속도 (예: "15.0초 (개당)")
    [SerializeField] private TextMeshProUGUI cookingQuantityText;  // 완성된 수량 (0부터 시작하여 완성 시 +1)
    [SerializeField] private Button cancelBtn;

    private CampFireRuntime targetFacility;
    public CampFireRuntime TargetFacility => targetFacility;

    private ItemData activeSelectedFood;
    private CookRecipeData activeSelectedRecipeData;

    private int selectedQuantity = 1;
    private int maxCookableQuantity = 1;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (btnMinus != null) btnMinus.onClick.AddListener(() => ModifyQuantity(-1));
        if (btnPlus != null) btnPlus.onClick.AddListener(() => ModifyQuantity(1));

        if (reSelectBtn != null) reSelectBtn.onClick.AddListener(OnClickReSelect);
        if (cookBtn != null) cookBtn.onClick.AddListener(OnClickCookStart);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(OnClickCancelCooking);

        // 요리 아이콘 클릭 시 수령 연동
        if (cookingItemIconButton != null)
        {
            cookingItemIconButton.onClick.AddListener(OnClickCollectReward);
        }
        else if (cookingItemIcon != null && cookingItemIcon.TryGetComponent<Button>(out var iconBtn))
        {
            iconBtn.onClick.AddListener(OnClickCollectReward);
        }

        if (singleMemSlot != null) singleMemSlot.InitializeSlot(0);
    }

    private void OnDisable()
    {
        StopDotsAnimation();
    }

    private void Update()
    {
        if (targetFacility == null) return;

        if (currentUIState == CookingUIState.Cooking && !string.IsNullOrEmpty(targetFacility.currentCookingItem) && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = Mathf.Clamp01(targetFacility.currentProgressTime / targetFacility.totalRequiredTime);
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{progressNormalized * 100f:F0}%";

            UpdateCookingStatusUI();
            UpdateCookingInfoText();
        }

        bool canGet = (targetFacility.currentStorageCount > 0);
        if (cookingItemIconButton != null) cookingItemIconButton.interactable = canGet;
    }

    private bool IsAllDeployedMemsStarving()
    {
        if (targetFacility == null || targetFacility.DeployedMemEntries == null || targetFacility.DeployedMemEntries.Count == 0)
            return false;

        return targetFacility.DeployedMemEntries.All(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
    }

    private void UpdateCookingStatusUI()
    {
        if (targetFacility == null) return;

        bool isNoMem = targetFacility.DeployedMems.Count == 0 || targetFacility.DeployedMemEntries.Count == 0;
        bool isAllStarving = !isNoMem && IsAllDeployedMemsStarving();
        int currentSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;

        bool isCookingComplete = (targetFacility.remainingQuantity <= 0 && targetFacility.currentStorageCount > 0)
                              || (targetFacility.targetQuantity > 0 && targetFacility.currentStorageCount >= targetFacility.targetQuantity);

        if (isCookingComplete)
        {
            StopDotsAnimation();
            if (cookingStatusText != null)
            {
                cookingStatusText.color = Color.white;
                cookingStatusText.text = "요리 완료!";
            }
        }
        else if (isNoMem)
        {
            StopDotsAnimation();
            if (cookingStatusText != null)
            {
                cookingStatusText.color = Color.white;
                cookingStatusText.text = "멤을 배치해야 합니다";
            }
        }
        else if (isAllStarving)
        {
            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
            else
            {
                StopDotsAnimation();
                if (cookingStatusText != null)
                {
                    cookingStatusText.color = Color.red;
                    cookingStatusText.text = "식량이 부족합니다";
                }
            }
        }
        else if (targetFacility.isCooking)
        {
            StartDotsAnimation();
        }
        else
        {
            StopDotsAnimation();
            if (cookingStatusText != null)
            {
                cookingStatusText.color = Color.white;
                cookingStatusText.text = "요리 대기 중";
            }
        }
    }

    private void UpdateCookingInfoText()
    {
        if (targetFacility == null) return;

        if (cookingSpeedText != null)
        {
            cookingSpeedText.text = $"{targetFacility.totalRequiredTime:F1}초 (개당)";
        }

        if (cookingQuantityText != null)
        {
            cookingQuantityText.text = targetFacility.currentStorageCount.ToString();
        }
    }

    private void StartDotsAnimation(string customPrefix = null)
    {
        string prefix = customPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            prefix = "요리중";
        }

        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (cookingStatusText != null) cookingStatusText.color = Color.white;

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetCookingStatusText($"{currentStatusPrefix} ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetCookingStatusText($"{currentStatusPrefix} .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetCookingStatusText($"{currentStatusPrefix} ..."); })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
    }

    private void SetCookingStatusText(string text)
    {
        if (cookingStatusText != null)
        {
            cookingStatusText.text = text;
        }
    }

    private void StopDotsAnimation()
    {
        if (!isAnimatingDots && dotsSequence == null) return;

        isAnimatingDots = false;
        currentStatusPrefix = "";
        if (dotsSequence != null)
        {
            dotsSequence.Kill();
            dotsSequence = null;
        }
    }

    public void OpenPanel(CampFireRuntime facility)
    {
        if (facility == null) return;
        targetFacility = facility;

        RefreshStaticUI();

        if (targetFacility.isCooking || !string.IsNullOrEmpty(targetFacility.currentCookingItem))
        {
            currentUIState = CookingUIState.Cooking;
        }
        else
        {
            currentUIState = CookingUIState.Default;
            activeSelectedFood = null;
            selectedQuantity = 1;
        }

        RefreshCookingModeUI();
        GenerateAvailableRecipeList();
    }

    public void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        if (buildingName != null && targetFacility.buildingData != null)
            buildingName.text = targetFacility.buildingData.buildingName;

        MemData placedMemData = targetFacility.DeployedMems.Count > 0 ? targetFacility.DeployedMems[0] : null;
        CapturedMemEntry placedEntryData = targetFacility.DeployedMemEntries.Count > 0 ? targetFacility.DeployedMemEntries[0] : null;

        if (singleMemSlot != null)
        {
            singleMemSlot.RefreshStatus(true, placedMemData, placedEntryData);
        }
    }

    private void RefreshCookingModeUI()
    {
        if (targetFacility == null) return;

        bool isMemMode = (currentUIState == CookingUIState.Default || currentUIState == CookingUIState.Cooking);
        if (centerMemModeObject != null) centerMemModeObject.SetActive(isMemMode);
        if (centerSelectedFoodModeObject != null) centerSelectedFoodModeObject.SetActive(currentUIState == CookingUIState.SelectFood);

        if (bottomDefaultModeObject != null) bottomDefaultModeObject.SetActive(currentUIState == CookingUIState.Default);
        if (bottomSelectFoodModeObject != null) bottomSelectFoodModeObject.SetActive(currentUIState == CookingUIState.SelectFood);
        if (bottomCookingModeObject != null) bottomCookingModeObject.SetActive(currentUIState == CookingUIState.Cooking);

        if (currentUIState == CookingUIState.SelectFood && activeSelectedFood != null)
        {
            if (selectionImage != null) selectionImage.sprite = activeSelectedFood.ItemIcon;
            if (selectionName != null) selectionName.text = activeSelectedFood.ItemName;

            UpdateSelectFoodCalculatedUI();
            GenerateRequiredMaterialListUI();
        }

        if (currentUIState == CookingUIState.Cooking && !string.IsNullOrEmpty(targetFacility.currentCookingItem))
        {
            ItemData currentItem = FindItemDataInCatalog(targetFacility.currentCookingItem);
            if (currentItem != null)
            {
                if (cookingItemIcon != null) cookingItemIcon.sprite = currentItem.ItemIcon;
                if (cookingItemName != null) cookingItemName.text = currentItem.ItemName;
            }

            UpdateCookingStatusUI();
            UpdateCookingInfoText();
        }
        else
        {
            StopDotsAnimation();
        }
    }

    private void UpdateSelectFoodCalculatedUI()
    {
        if (activeSelectedFood == null) return;

        if (productAmountText != null) productAmountText.text = selectedQuantity.ToString();

        if (btnMinus != null) btnMinus.interactable = (selectedQuantity > 1);
        if (btnPlus != null) btnPlus.interactable = (maxCookableQuantity > 0 && selectedQuantity < maxCookableQuantity);

        if (cookBtn != null)
        {
            cookBtn.interactable = (maxCookableQuantity > 0 && selectedQuantity > 0);
        }

        if (cookingDurationText != null)
        {
            if (maxCookableQuantity == 0)
            {
                cookingDurationText.text = "<color=red>요리에 필요한 식재료 수량이 부족합니다.</color>";
            }
            else
            {
                float baseDuration = activeSelectedRecipeData != null ? activeSelectedRecipeData.Time : 15f;
                float singleTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, targetFacility.DeployedMems);
                float totalEstimatedTime = singleTime * selectedQuantity;
                cookingDurationText.text = $"요리 예상시간: {totalEstimatedTime:F1}초 (개당 {singleTime:F1}초)";
            }
        }
    }

    private void ModifyQuantity(int amount)
    {
        selectedQuantity = Mathf.Clamp(selectedQuantity + amount, 1, maxCookableQuantity);
        UpdateSelectFoodCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private int CalculateMaxCookableLimitAmount(string recipeOrItemId)
    {
        if (targetFacility == null) return 0;

        List<string> ingredientIds = targetFacility.GetIngredientIdsForCooking(recipeOrItemId);
        if (ingredientIds == null || ingredientIds.Count == 0) return 0;

        int finalCalculatedMax = int.MaxValue;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        foreach (string matId in ingredientIds)
        {
            if (string.IsNullOrEmpty(matId)) continue;

            int totalOwnedAmount = 0;
            if (inventory != null) totalOwnedAmount += inventory.GetItemAmount(matId);
            if (warehouse != null) totalOwnedAmount += warehouse.GetItemAmount(matId);

            if (totalOwnedAmount < finalCalculatedMax)
            {
                finalCalculatedMax = totalOwnedAmount;
            }
        }

        return finalCalculatedMax == int.MaxValue ? 0 : Mathf.Max(0, finalCalculatedMax);
    }

    /// <summary>
    /// 🌟 [수정] RequireMaterialItemUI를 우선적으로 찾아서 재료 이미지, 이름, 수량이 CraftingPanelUI처럼 모두 표시되도록 보완
    /// </summary>
    private void GenerateRequiredMaterialListUI()
    {
        if (requiredListParent == null) return;

        foreach (Transform child in requiredListParent) Destroy(child.gameObject);

        if (activeSelectedFood == null || targetFacility == null) return;

        List<string> ingredientIds = targetFacility.GetIngredientIdsForCooking(activeSelectedFood.Item_ID);
        if (ingredientIds == null || ingredientIds.Count == 0) return;

        foreach (string matId in ingredientIds)
        {
            if (string.IsNullOrEmpty(matId)) continue;

            ItemData materialItemData = FindItemDataInCatalog(matId);

            if (materialItemData != null && requireMaterialPrefab != null)
            {
                GameObject materialSlotInstance = Instantiate(requireMaterialPrefab, requiredListParent);

                // 🌟 RequireMaterialItemUI를 1순위로 바인딩 (이름, 아이콘, 수량이 모두 표시됨)
                if (materialSlotInstance.TryGetComponent<RequireMaterialItemUI>(out var materialUI))
                {
                    materialUI.SetupMaterialSlot(materialItemData, 1, selectedQuantity);
                }
                else if (materialSlotInstance.TryGetComponent<CookingRecipeSlotIconUI>(out var iconUI))
                {
                    PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
                    WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();
                    int owned = 0;
                    if (inventory != null) owned += inventory.GetItemAmount(matId);
                    if (warehouse != null) owned += warehouse.GetItemAmount(matId);
                    int requiredTotal = 1 * selectedQuantity;

                    iconUI.SetupSlot(materialItemData, owned, requiredTotal);
                }
                else if (materialSlotInstance.TryGetComponent<Image>(out var img))
                {
                    img.sprite = materialItemData.ItemIcon;
                }
            }
        }
    }

    private void GenerateAvailableRecipeList()
    {
        if (recipeGridParent == null) return;

        foreach (Transform child in recipeGridParent) Destroy(child.gameObject);

        if (targetFacility == null || targetFacility.cookingFacilityData == null)
        {
            Debug.LogWarning("[요리 시설] CookingFacilityData가 가리키는 데이터 자산이 비어있습니다.");
            return;
        }

        List<string> facilityRecipeIds = targetFacility.cookingFacilityData.RecipeIds;
        if (facilityRecipeIds == null || facilityRecipeIds.Count == 0) return;

        CookRecipeUnlockManager unlockManager = CookRecipeUnlockManager.Resolve(null);
        if (unlockManager == null) unlockManager = FindFirstObjectByType<CookRecipeUnlockManager>();

        foreach (string recipeId in facilityRecipeIds)
        {
            if (string.IsNullOrEmpty(recipeId)) continue;

            bool isUnlocked = false;
            if (unlockManager != null && unlockManager.UnlockedRecipeIds != null)
            {
                isUnlocked = unlockManager.UnlockedRecipeIds.Contains(recipeId);
            }

            if (!isUnlocked) continue;

            ItemData matchedItemData = FindItemDataInCatalog(recipeId);

            if (matchedItemData != null && recipeSlotPrefab != null)
            {
                GameObject slotInstance = Instantiate(recipeSlotPrefab, recipeGridParent);

                if (slotInstance.TryGetComponent<RecipeSlotUI>(out RecipeSlotUI recipeSlot))
                {
                    recipeSlot.SetupSlot(matchedItemData, () => OnSelectFoodRecipe(matchedItemData));
                }
            }
        }
    }

    public void OnSelectFoodRecipe(ItemData selectedFood)
    {
        if (targetFacility == null || selectedFood == null) return;

        activeSelectedFood = selectedFood;
        activeSelectedRecipeData = FindCookRecipeDataInCatalog(selectedFood.Item_ID);

        maxCookableQuantity = CalculateMaxCookableLimitAmount(selectedFood.Item_ID);
        selectedQuantity = 1;

        currentUIState = CookingUIState.SelectFood;
        RefreshCookingModeUI();
    }

    private void OnClickReSelect()
    {
        activeSelectedFood = null;
        selectedQuantity = 1;

        currentUIState = CookingUIState.Default;
        RefreshCookingModeUI();
    }

    private void OnClickCookStart()
    {
        if (targetFacility == null || activeSelectedFood == null) return;

        if (targetFacility.DeployedMems.Count == 0) return;

        List<string> ingredientIds = targetFacility.GetIngredientIdsForCooking(activeSelectedFood.Item_ID);
        bool isMaterialEnough = true;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        foreach (string matId in ingredientIds)
        {
            if (string.IsNullOrEmpty(matId)) continue;

            int totalOwnedAmount = 0;
            if (inventory != null) totalOwnedAmount += inventory.GetItemAmount(matId);
            if (warehouse != null) totalOwnedAmount += warehouse.GetItemAmount(matId);

            int totalRequired = 1 * selectedQuantity;
            if (totalOwnedAmount < totalRequired)
            {
                isMaterialEnough = false;
                break;
            }
        }

        if (!isMaterialEnough) return;

        foreach (string matId in ingredientIds)
        {
            if (string.IsNullOrEmpty(matId)) continue;

            int totalRequired = 1 * selectedQuantity;
            int inventoryHas = inventory != null ? inventory.GetItemAmount(matId) : 0;

            if (inventoryHas >= totalRequired)
            {
                inventory.RemoveItem(matId, totalRequired);
            }
            else
            {
                if (inventoryHas > 0)
                {
                    inventory.RemoveItem(matId, inventoryHas);
                }

                int remainingNeed = totalRequired - inventoryHas;
                if (warehouse != null)
                {
                    warehouse.RemoveItem(matId, remainingNeed);
                }
            }
        }

        targetFacility.SelectAndStartCooking(activeSelectedFood.Item_ID, selectedQuantity);
        currentUIState = CookingUIState.Cooking;
        RefreshCookingModeUI();
    }

    private void OnClickCancelCooking()
    {
        if (targetFacility == null) return;

        targetFacility.CancelCooking();

        currentUIState = CookingUIState.Default;
        RefreshCookingModeUI();
    }

    private void OnClickCollectReward()
    {
        if (targetFacility == null) return;

        bool isLineCleared = targetFacility.CollectCookedItems();

        if (isLineCleared)
        {
            currentUIState = CookingUIState.Default;
        }

        RefreshCookingModeUI();
    }

    public bool TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null)
        {
            targetFacility = FindFirstObjectByType<CampFireRuntime>();
            if (targetFacility == null) return false;
        }

        if (targetEntry == null) return false;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);

        if (isSuccess)
        {
            RefreshStaticUI();

            if (currentUIState == CookingUIState.SelectFood)
            {
                UpdateSelectFoodCalculatedUI();
            }
        }

        return isSuccess;
    }

    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);

        RefreshStaticUI();
        RefreshCookingModeUI();
    }

    public void ClosePanel()
    {
        StopDotsAnimation();
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        RefreshCookingModeUI();
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        var catalog = ItemCatalogManager.Resolve(null);
        return catalog != null ? catalog.FindItemData(itemId) : null;
    }

    private CookRecipeData FindCookRecipeDataInCatalog(string resultItemId)
    {
        if (string.IsNullOrEmpty(resultItemId)) return null;
        var catalog = ItemCatalogManager.Resolve(null);
        return catalog != null ? catalog.FindCookRecipeData(resultItemId) : null;
    }
}