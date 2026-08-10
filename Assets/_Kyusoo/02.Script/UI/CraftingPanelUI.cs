using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Recipe;
using HDY.UI;
using KMS.InventoryDuped;
using MemSystem.Data;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class CraftingPanelUI : MonoBehaviour
{
    public static CraftingPanelUI Instance { get; private set; }

    private enum CraftingUIState { Default, SelectProduct, Crafting }
    private CraftingUIState currentUIState = CraftingUIState.Default;

    [Header("중앙 패널 - Top 빌딩 이름")]
    [SerializeField] private TextMeshProUGUI buildingName;

    [Header("중앙 패널 - Center 멤슬롯")]
    [SerializeField] private MemSlotUI singleMemSlot;

    [Header("중앙 패널 - Center: Default_Mode")]
    [SerializeField] private GameObject defaultModeObject;
    [SerializeField] private Transform recipeGridParent;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("중앙 패널 - Center: Select_Product")]
    [SerializeField] private GameObject selectProductModeObject;
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

    [Header("중앙 패널 - Center: Crafting")]
    [SerializeField] private GameObject craftingModeObject;
    [SerializeField] private Image craftingItemIcon;
    [SerializeField] private TextMeshProUGUI craftingItemName;
    [SerializeField] private Button collectRewardBtn;
    [SerializeField] private TextMeshProUGUI completeCountText;

    [Header("중앙 패널 - Bottom: Default_Mode")]
    [SerializeField] private GameObject bottomDefaultModeObject;
    [SerializeField] private TextMeshProUGUI selectGuideText;

    [Header("중앙 패널 - Bottom: Select_Product")]
    [SerializeField] private GameObject bottomSelectProductModeObject;
    [SerializeField] private TextMeshProUGUI craftingDurationText;
    [SerializeField] private Button reSelectBtn;
    [SerializeField] private Button craftBtn;

    [Header("중앙 패널 - Bottom: Crafting")]
    [SerializeField] private GameObject bottomCraftingModeObject;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI craftingStatusText;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button getBtn;

    private ProductionCraftRuntime targetFacility;
    public ProductionCraftRuntime TargetFacility => targetFacility;

    private ItemData activeSelectedRecipe;
    private HDY.Recipe.RecipeData activeSelectedRecipeData;

    private int selectedQuantity = 1;
    private int maxCraftableQuantity = 1;

    private bool isUpdatingQuantitySystem = false;
    private Coroutine errorFeedbackCoroutine;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = "";

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
        if (craftBtn != null) craftBtn.onClick.AddListener(OnClickCraftStart);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(OnClickCancelCrafting);
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

        if (currentUIState == CraftingUIState.Crafting && !string.IsNullOrEmpty(targetFacility.currentCraftingItem) && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";

            UpdateCraftingStatusUI();
        }

        bool canGet = (targetFacility.currentStorageCount > 0);
        if (getBtn != null) getBtn.interactable = canGet;
        if (collectRewardBtn != null) collectRewardBtn.interactable = canGet;

        UpdateStorageText();
    }

    private bool IsAllDeployedMemsStarving()
    {
        if (targetFacility == null || targetFacility.DeployedMemEntries == null || targetFacility.DeployedMemEntries.Count == 0)
            return false;

        return targetFacility.DeployedMemEntries.All(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
    }

    private void UpdateCraftingStatusUI()
    {
        if (targetFacility == null) return;

        bool isNoMem = targetFacility.DeployedMems.Count == 0 || targetFacility.DeployedMemEntries.Count == 0;
        bool isAllStarving = !isNoMem && IsAllDeployedMemsStarving();
        int currentSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;

        // 1. 멤 미배치
        if (isNoMem)
        {
            StopDotsAnimation();
            if (craftingStatusText != null)
            {
                craftingStatusText.color = Color.white;
                craftingStatusText.text = "멤을 배치해야 합니다";
            }
        }
        // 2. 가동 중지 (배치된 멤 모두 허기량 0)
        else if (isAllStarving)
        {
            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
            else
            {
                StopDotsAnimation();
                if (craftingStatusText != null)
                {
                    craftingStatusText.color = Color.red;
                    craftingStatusText.text = "식량이 부족합니다";
                }
            }
        }
        // 3. 정상 제작 중
        else if (targetFacility.isProducing)
        {
            StartDotsAnimation();
        }
        else
        {
            StopDotsAnimation();
            if (craftingStatusText != null)
            {
                craftingStatusText.color = Color.white;
                craftingStatusText.text = "제작 대기 중";
            }
        }
    }

    private void StartDotsAnimation(string customPrefix = null)
    {
        string prefix = customPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            string itemName = "";
            if (targetFacility != null && !string.IsNullOrEmpty(targetFacility.currentCraftingItem))
            {
                ItemData targetItemData = FindItemDataInCatalog(targetFacility.currentCraftingItem);
                if (targetItemData != null) itemName = targetItemData.ItemName;
            }
            prefix = string.IsNullOrEmpty(itemName) ? "제작중" : $"{itemName} 제작중";
        }

        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (craftingStatusText != null) craftingStatusText.color = Color.white;

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetStatusText($"{currentStatusPrefix} ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{currentStatusPrefix} .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{currentStatusPrefix} ..."); })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
    }

    private void SetStatusText(string text)
    {
        if (craftingStatusText != null) craftingStatusText.text = text;
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

    public void OpenPanel(ProductionCraftRuntime facility)
    {
        if (facility == null) return;

        targetFacility = facility;

        RefreshStaticUI();

        if (targetFacility.isProducing || !string.IsNullOrEmpty(targetFacility.currentCraftingItem))
        {
            currentUIState = CraftingUIState.Crafting;
        }
        else
        {
            currentUIState = CraftingUIState.Default;
            activeSelectedRecipe = null;
            selectedQuantity = 1;
        }

        RefreshCraftingModeUI();
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

    private void RefreshCraftingModeUI()
    {
        if (targetFacility == null) return;

        if (defaultModeObject != null) defaultModeObject.SetActive(currentUIState == CraftingUIState.Default);
        if (selectProductModeObject != null) selectProductModeObject.SetActive(currentUIState == CraftingUIState.SelectProduct);
        if (craftingModeObject != null) craftingModeObject.SetActive(currentUIState == CraftingUIState.Crafting);

        if (bottomDefaultModeObject != null) bottomDefaultModeObject.SetActive(currentUIState == CraftingUIState.Default);
        if (bottomSelectProductModeObject != null) bottomSelectProductModeObject.SetActive(currentUIState == CraftingUIState.SelectProduct);
        if (bottomCraftingModeObject != null) bottomCraftingModeObject.SetActive(currentUIState == CraftingUIState.Crafting);

        if (currentUIState == CraftingUIState.SelectProduct && activeSelectedRecipe != null)
        {
            if (selectionImage != null) selectionImage.sprite = activeSelectedRecipe.ItemIcon;
            if (selectionName != null) selectionName.text = activeSelectedRecipe.ItemName;

            UpdateSelectProductCalculatedUI();
            GenerateRequiredMaterialListUI();
        }

        if (currentUIState == CraftingUIState.Crafting && !string.IsNullOrEmpty(targetFacility.currentCraftingItem))
        {
            ItemData currentItem = FindItemDataInCatalog(targetFacility.currentCraftingItem);
            if (currentItem != null)
            {
                if (craftingItemIcon != null) craftingItemIcon.sprite = currentItem.ItemIcon;
                if (craftingItemName != null) craftingItemName.text = currentItem.ItemName;
            }

            UpdateCraftingStatusUI();
        }
        else
        {
            StopDotsAnimation();
        }
    }

    private void UpdateSelectProductCalculatedUI()
    {
        if (activeSelectedRecipe == null) return;

        isUpdatingQuantitySystem = true;
        if (productAmountText != null) productAmountText.text = selectedQuantity.ToString();
        if (quantitySlider != null) quantitySlider.value = selectedQuantity;
        isUpdatingQuantitySystem = false;

        if (btnMin != null) btnMin.interactable = (selectedQuantity > 1);
        if (btnMinus != null) btnMinus.interactable = (selectedQuantity > 1);

        if (btnPlus != null) btnPlus.interactable = (maxCraftableQuantity > 0 && selectedQuantity < maxCraftableQuantity);
        if (btnMax != null) btnMax.interactable = (maxCraftableQuantity > 0 && selectedQuantity < maxCraftableQuantity);

        if (craftBtn != null)
        {
            craftBtn.interactable = (maxCraftableQuantity > 0 && selectedQuantity > 0);
        }

        if (errorFeedbackCoroutine == null && craftingDurationText != null)
        {
            if (maxCraftableQuantity == 0)
            {
                craftingDurationText.text = "<color=red>제작에 필요한 원자재 수량이 부족합니다.</color>";
            }
            else
            {
                float baseDuration = activeSelectedRecipeData != null ? activeSelectedRecipeData.time : 20f;
                float singleTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, targetFacility.DeployedMems);
                float totalEstimatedTime = singleTime * selectedQuantity;
                craftingDurationText.text = $"제작 예상시간: {totalEstimatedTime:F1}초 (개당 {singleTime:F1}초)";
            }
        }
    }

    private void OnSliderQuantityChanged(float value)
    {
        if (isUpdatingQuantitySystem) return;

        int maxLimit = Mathf.Max(1, maxCraftableQuantity);
        selectedQuantity = Mathf.Clamp(Mathf.RoundToInt(value), 1, maxLimit);

        UpdateSelectProductCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void ModifyQuantity(int amount)
    {
        selectedQuantity = Mathf.Clamp(selectedQuantity + amount, 1, maxCraftableQuantity);
        UpdateSelectProductCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void SetMinQuantity()
    {
        selectedQuantity = 1;
        UpdateSelectProductCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private void SetMaxQuantity()
    {
        selectedQuantity = maxCraftableQuantity;
        UpdateSelectProductCalculatedUI();
        GenerateRequiredMaterialListUI();
    }

    private int CalculateMaxCraftableLimitAmount(ItemData recipe)
    {
        if (recipe == null || activeSelectedRecipeData == null) return 1;

        int finalCalculatedMax = int.MaxValue;
        bool hasMateria = false;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        foreach (Recipe_Requset_Item_Data reqItem in activeSelectedRecipeData.Requset_Items_ID)
        {
            if (reqItem == null || string.IsNullOrEmpty(reqItem.Item_ID)) continue;

            hasMateria = true;

            int totalOwnedAmount = 0;
            if (inventory != null) totalOwnedAmount += inventory.GetItemAmount(reqItem.Item_ID);
            if (warehouse != null) totalOwnedAmount += warehouse.GetItemAmount(reqItem.Item_ID);

            if (reqItem.Amount <= 0) continue;

            int possibleMaxByThisMaterial = totalOwnedAmount / reqItem.Amount;
            if (possibleMaxByThisMaterial < finalCalculatedMax)
            {
                finalCalculatedMax = possibleMaxByThisMaterial;
            }
        }

        if (!hasMateria) return 0;

        return netMaximumCheck(finalCalculatedMax);
    }

    private int netMaximumCheck(int finalCalculatedMax)
    {
        return Mathf.Max(0, finalCalculatedMax);
    }

    private void GenerateRequiredMaterialListUI()
    {
        foreach (Transform child in requiredListParent) Destroy(child.gameObject);

        if (activeSelectedRecipe == null || activeSelectedRecipeData == null) return;

        foreach (Recipe_Requset_Item_Data requestData in activeSelectedRecipeData.Requset_Items_ID)
        {
            if (requestData == null || string.IsNullOrEmpty(requestData.Item_ID)) continue;

            ItemData materialItemData = FindItemDataInCatalog(requestData.Item_ID);

            if (materialItemData != null)
            {
                GameObject materialSlotInstance = Instantiate(requireMaterialPrefab, requiredListParent);

                if (materialSlotInstance.TryGetComponent<RequireMaterialItemUI>(out RequireMaterialItemUI materialUI))
                {
                    materialUI.SetupMaterialSlot(materialItemData, requestData.Amount, selectedQuantity);
                }
            }
        }
    }

    private void GenerateAvailableRecipeList()
    {
        foreach (Transform child in recipeGridParent) Destroy(child.gameObject);

        RecipeUnlockManager recipeManager = Object.FindFirstObjectByType<RecipeUnlockManager>();

        if (recipeManager == null) return;

        if (recipeManager.RecipeUnlocks != null && recipeManager.RecipeUnlocks.Count > 0)
        {
            for (int i = 0; i < recipeManager.RecipeUnlocks.Count; i++)
            {
                RecipeUnlockEntry entry = recipeManager.RecipeUnlocks[i];

                if (entry == null || !entry.IsUnlocked) continue;

                ItemData matchedItemData = recipeManager.FindRecipeItemData(entry.Item_ID);

                if (matchedItemData != null)
                {
                    GameObject slotInstance = Instantiate(recipeSlotPrefab, recipeGridParent);

                    if (slotInstance.TryGetComponent<RecipeSlotUI>(out RecipeSlotUI recipeSlot))
                    {
                        recipeSlot.SetupSlot(matchedItemData, () => OnSelectItemRecipe(matchedItemData));
                    }
                }
            }
        }
    }

    public void OnSelectItemRecipe(ItemData selectedItem)
    {
        if (targetFacility == null || selectedItem == null) return;

        activeSelectedRecipe = selectedItem;
        activeSelectedRecipeData = FindRecipeDataInCatalog(selectedItem.Item_ID);

        maxCraftableQuantity = CalculateMaxCraftableLimitAmount(selectedItem);
        selectedQuantity = 1;

        if (quantitySlider != null)
        {
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = Mathf.Max(1, maxCraftableQuantity);
            quantitySlider.wholeNumbers = true;
        }

        currentUIState = CraftingUIState.SelectProduct;
        RefreshCraftingModeUI();
    }

    private void OnClickReSelect()
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = null;

        activeSelectedRecipe = null;
        selectedQuantity = 1;

        currentUIState = CraftingUIState.Default;
        RefreshCraftingModeUI();
    }

    private void OnClickCraftStart()
    {
        if (targetFacility == null || activeSelectedRecipe == null) return;

        if (targetFacility.DeployedMems.Count == 0)
        {
            TriggerErrorFeedbackAlert("멤이 배치되지 않았습니다");
            return;
        }

        bool isMaterialEnough = true;
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();

        if (activeSelectedRecipeData != null && activeSelectedRecipeData.Requset_Items_ID != null)
        {
            foreach (Recipe_Requset_Item_Data req in activeSelectedRecipeData.Requset_Items_ID)
            {
                if (req == null || string.IsNullOrEmpty(req.Item_ID)) continue;

                int totalOwnedAmount = 0;
                if (inventory != null) totalOwnedAmount += inventory.GetItemAmount(req.Item_ID);
                if (warehouse != null) totalOwnedAmount += warehouse.GetItemAmount(req.Item_ID);

                int totalRequired = req.Amount * selectedQuantity;
                if (totalOwnedAmount < totalRequired)
                {
                    isMaterialEnough = false;
                    break;
                }
            }
        }

        if (!isMaterialEnough)
        {
            TriggerErrorFeedbackAlert("제작에 필요한 재료가 부족합니다");
            return;
        }

        if (activeSelectedRecipeData != null && activeSelectedRecipeData.Requset_Items_ID != null)
        {
            foreach (Recipe_Requset_Item_Data req in activeSelectedRecipeData.Requset_Items_ID)
            {
                if (req == null || string.IsNullOrEmpty(req.Item_ID)) continue;

                int totalRequired = req.Amount * selectedQuantity;
                int inventoryHas = inventory != null ? inventory.GetItemAmount(req.Item_ID) : 0;

                if (inventoryHas >= totalRequired)
                {
                    inventory.RemoveItem(req.Item_ID, totalRequired);
                }
                else
                {
                    if (inventoryHas > 0)
                    {
                        inventory.RemoveItem(req.Item_ID, inventoryHas);
                    }

                    int remainingNeed = totalRequired - inventoryHas;
                    if (warehouse != null)
                    {
                        warehouse.RemoveItem(req.Item_ID, remainingNeed);
                    }
                }
            }
        }

        targetFacility.SelectAndStartCrafting(activeSelectedRecipe.Item_ID, selectedQuantity);
        currentUIState = CraftingUIState.Crafting;
        RefreshCraftingModeUI();
    }

    private void TriggerErrorFeedbackAlert(string errorMsg)
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = StartCoroutine(ErrorFeedbackRoutine(errorMsg));
    }

    private IEnumerator ErrorFeedbackRoutine(string msg)
    {
        if (craftingDurationText != null)
        {
            Color originColor = craftingDurationText.color;
            craftingDurationText.color = Color.red;
            craftingDurationText.text = msg;

            yield return new WaitForSeconds(2f);

            craftingDurationText.color = originColor;
        }

        errorFeedbackCoroutine = null;
        UpdateSelectProductCalculatedUI();
    }

    private void OnClickCancelCrafting()
    {
        if (targetFacility == null) return;

        targetFacility.CancelCrafting();

        currentUIState = CraftingUIState.Default;
        RefreshCraftingModeUI();
    }

    private void OnClickCollectReward()
    {
        if (targetFacility == null) return;

        bool isLineCleared = targetFacility.CollectCraftedItems();

        if (isLineCleared)
        {
            currentUIState = CraftingUIState.Default;
        }

        RefreshCraftingModeUI();
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
            targetFacility = FindFirstObjectByType<ProductionCraftRuntime>();
            if (targetFacility == null) return false;
        }

        if (targetEntry == null) return false;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);

        if (isSuccess)
        {
            RefreshStaticUI();

            if (currentUIState == CraftingUIState.SelectProduct)
            {
                UpdateSelectProductCalculatedUI();
            }
        }

        return isSuccess;
    }

    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);

        RefreshStaticUI();
        RefreshCraftingModeUI();
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
        RefreshCraftingModeUI();
        UpdateStorageText();
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (ItemCatalogManager.Instance == null) return null;
        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    private HDY.Recipe.RecipeData FindRecipeDataInCatalog(string recipeItemId)
    {
        if (string.IsNullOrEmpty(recipeItemId)) return null;
        if (ItemCatalogManager.Instance == null) return null;
        return ItemCatalogManager.Instance.FindRecipeData(recipeItemId);
    }
}