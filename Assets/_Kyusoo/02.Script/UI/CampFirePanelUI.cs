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

    [Header("중앙 - 멤 슬롯 (단일 1마리)")]
    [SerializeField] private MemSlotUI singleMemSlot;

    [Header("중앙 - Default_Mode (레시피 선택 그리드)")]
    [SerializeField] private GameObject defaultModeObject;
    [SerializeField] private Transform recipeGridParent;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("중앙 - Select_Mode (요리 및 재료 정보)")]
    [SerializeField] private GameObject selectFoodModeObject;
    [SerializeField] private Image selectionImage;
    [SerializeField] private TextMeshProUGUI selectionName;
    [SerializeField] private Transform requiredListParent;
    [SerializeField] private GameObject requireMaterialPrefab;
    [SerializeField] private TextMeshProUGUI productAmountText;
    [SerializeField] private Button btnMin;
    [SerializeField] private Button btnMinus;
    [SerializeField] private Button btnPlus;
    [SerializeField] private Button btnMax;
    [SerializeField] private Slider quantitySlider;

    [Header("중앙 - Cooking_Mode (요리 진행 중 정보)")]
    [SerializeField] private GameObject cookingModeObject;
    [SerializeField] private Image cookingItemIcon;
    [SerializeField] private TextMeshProUGUI cookingItemName;
    [SerializeField] private Button collectRewardBtn;
    [SerializeField] private TextMeshProUGUI completeCountText;

    [Header("하단 - Default_Mode")]
    [SerializeField] private GameObject bottomDefaultModeObject;
    [SerializeField] private TextMeshProUGUI selectGuideText;

    [Header("하단 - Select_Mode")]
    [SerializeField] private GameObject bottomSelectFoodModeObject;
    [SerializeField] private TextMeshProUGUI cookingDurationText;
    [SerializeField] private Button reSelectBtn;
    [SerializeField] private Button cookBtn;

    [Header("하단 - Cooking_Mode")]
    [SerializeField] private GameObject bottomCookingModeObject;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI cookingStatusText; // 🌟 상태 및 애니메이션 텍스트
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button getBtn;

    private CampFireRuntime targetFacility;
    public CampFireRuntime TargetFacility => targetFacility;

    private ItemData activeSelectedFood;
    private CookRecipeData activeSelectedRecipeData;

    private int selectedQuantity = 1;
    private int maxCookableQuantity = 1;

    private bool isUpdatingQuantitySystem = false;
    private Coroutine errorFeedbackCoroutine;

    // 🌟 DOTween 애니메이션 관련 변수
    private Sequence dotsSequence;
    private bool isAnimatingDots = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (btnMin != null) btnMin.onClick.AddListener(SetMinQuantity);
        if (btnMax != null) btnMax.onClick.AddListener(SetMaxQuantity);
        if (btnMinus != null) btnMinus.onClick.AddListener(() => ModifyQuantity(-1));
        if (btnPlus != null) btnPlus.onClick.AddListener(() => ModifyQuantity(1));
        if (quantitySlider != null) quantitySlider.onValueChanged.AddListener(OnSliderQuantityChanged);

        if (reSelectBtn != null) reSelectBtn.onClick.AddListener(OnClickReSelect);
        if (cookBtn != null) cookBtn.onClick.AddListener(OnClickCookStart);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(OnClickCancelCooking);
        if (getBtn != null) getBtn.onClick.AddListener(OnClickCollectReward);
        if (collectRewardBtn != null) collectRewardBtn.onClick.AddListener(OnClickCollectReward);

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
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";

            UpdateCookingStatusUI();
        }

        bool canGet = (targetFacility.currentStorageCount > 0);
        if (getBtn != null) getBtn.interactable = canGet;
        if (collectRewardBtn != null) collectRewardBtn.interactable = canGet;

        UpdateStorageText();
    }

    /// <summary>
    /// 🌟 상태 텍스트 갱신 및 중지 사유 처리
    /// </summary>
    private void UpdateCookingStatusUI()
    {
        if (targetFacility == null) return;

        bool isStarving = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        if (isStarving)
        {
            StopDotsAnimation();
            if (cookingStatusText != null)
            {
                cookingStatusText.color = Color.red;
                cookingStatusText.text = "식량이 부족합니다";
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

    private void StartDotsAnimation()
    {
        if (isAnimatingDots) return;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();

        if (cookingStatusText != null)
        {
            cookingStatusText.color = Color.white;
        }

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetCookingStatusText("요리중 ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetCookingStatusText("요리중 .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetCookingStatusText("요리중 ..."); })
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

        if (defaultModeObject != null) defaultModeObject.SetActive(currentUIState == CookingUIState.Default);
        if (selectFoodModeObject != null) selectFoodModeObject.SetActive(currentUIState == CookingUIState.SelectFood);
        if (cookingModeObject != null) cookingModeObject.SetActive(currentUIState == CookingUIState.Cooking);

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
        }
        else
        {
            StopDotsAnimation();
        }
    }

    private void UpdateSelectFoodCalculatedUI()
    {
        if (activeSelectedFood == null) return;

        isUpdatingQuantitySystem = true;
        if (productAmountText != null) productAmountText.text = selectedQuantity.ToString();
        if (quantitySlider != null) quantitySlider.value = selectedQuantity;
        isUpdatingQuantitySystem = false;

        if (btnMin != null) btnMin.interactable = (selectedQuantity > 1);
        if (btnMinus != null) btnMinus.interactable = (selectedQuantity > 1);

        if (btnPlus != null) btnPlus.interactable = (maxCookableQuantity > 0 && selectedQuantity < maxCookableQuantity);
        if (btnMax != null) btnMax.interactable = (maxCookableQuantity > 0 && selectedQuantity < maxCookableQuantity);

        if (cookBtn != null)
        {
            cookBtn.interactable = (maxCookableQuantity > 0 && selectedQuantity > 0);
        }

        if (errorFeedbackCoroutine == null && cookingDurationText != null)
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

    private void OnSliderQuantityChanged(float value)
    {
        if (isUpdatingQuantitySystem) return;

        int maxLimit = Mathf.Max(1, maxCookableQuantity);
        selectedQuantity = Mathf.Clamp(Mathf.RoundToInt(value), 1, maxLimit);

        UpdateSelectFoodCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void ModifyQuantity(int amount)
    {
        selectedQuantity = Mathf.Clamp(selectedQuantity + amount, 1, maxCookableQuantity);
        UpdateSelectFoodCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void SetMinQuantity()
    {
        selectedQuantity = 1;
        UpdateSelectFoodCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void SetMaxQuantity()
    {
        selectedQuantity = maxCookableQuantity;
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

    private void GenerateRequiredMaterialListUI()
    {
        if (requiredListParent == null) return;

        foreach (Transform child in requiredListParent) Destroy(child.gameObject);

        if (activeSelectedFood == null || targetFacility == null) return;

        List<string> ingredientIds = targetFacility.GetIngredientIdsForCooking(activeSelectedFood.Item_ID);
        if (ingredientIds == null || ingredientIds.Count == 0) return;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        int displayCount = Mathf.Min(4, ingredientIds.Count);

        for (int i = 0; i < displayCount; i++)
        {
            string matId = ingredientIds[i];
            if (string.IsNullOrEmpty(matId)) continue;

            ItemData materialItemData = FindItemDataInCatalog(matId);

            if (materialItemData != null && requireMaterialPrefab != null)
            {
                GameObject materialSlotInstance = Instantiate(requireMaterialPrefab, requiredListParent);

                int owned = 0;
                if (inventory != null) owned += inventory.GetItemAmount(matId);
                if (warehouse != null) owned += warehouse.GetItemAmount(matId);
                int requiredTotal = 1 * selectedQuantity;

                if (materialSlotInstance.TryGetComponent<CookingRecipeSlotIconUI>(out var iconUI))
                {
                    iconUI.SetupSlot(materialItemData, owned, requiredTotal);
                }
                else if (materialSlotInstance.TryGetComponent<RequireMaterialItemUI>(out var materialUI))
                {
                    materialUI.SetupMaterialSlot(materialItemData, 1, selectedQuantity);
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

        // 기존 슬롯 UI 오브젝트 초기화
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

        if (quantitySlider != null)
        {
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = Mathf.Max(1, maxCookableQuantity);
            quantitySlider.wholeNumbers = true;
        }

        currentUIState = CookingUIState.SelectFood;
        RefreshCookingModeUI();
    }

    private void OnClickReSelect()
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = null;

        activeSelectedFood = null;
        selectedQuantity = 1;

        currentUIState = CookingUIState.Default;
        RefreshCookingModeUI();
    }

    private void OnClickCookStart()
    {
        if (targetFacility == null || activeSelectedFood == null) return;

        if (targetFacility.DeployedMems.Count == 0)
        {
            TriggerErrorFeedbackAlert("멤이 배치되지 않았습니다");
            return;
        }

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

        if (!isMaterialEnough)
        {
            TriggerErrorFeedbackAlert("요리에 필요한 식재료가 부족합니다");
            return;
        }

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

    private void TriggerErrorFeedbackAlert(string errorMsg)
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = StartCoroutine(ErrorFeedbackRoutine(errorMsg));
    }

    private IEnumerator ErrorFeedbackRoutine(string msg)
    {
        if (cookingDurationText != null)
        {
            Color originColor = cookingDurationText.color;
            cookingDurationText.color = Color.red;
            cookingDurationText.text = msg;

            yield return new WaitForSeconds(2f);

            cookingDurationText.color = originColor;
        }

        errorFeedbackCoroutine = null;
        UpdateSelectFoodCalculatedUI();
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

    private void UpdateStorageText()
    {
        if (targetFacility == null || completeCountText == null) return;
        completeCountText.text = targetFacility.currentStorageCount.ToString();
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
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = null;

        StopDotsAnimation();
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        RefreshCookingModeUI();
        UpdateStorageText();
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