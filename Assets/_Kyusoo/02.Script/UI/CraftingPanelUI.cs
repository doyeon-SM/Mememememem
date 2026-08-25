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

    [Header("Top - 빌딩 이름")]
    [SerializeField] private TextMeshProUGUI buildingName;

    [Header("Center - 2단 모드 설정")]
    [SerializeField] private GameObject centerMemModeObject;            // [1] 멤 배치모드 (Default, Crafting 시 활성화)
    [SerializeField] private MemSlotUI singleMemSlot;

    [SerializeField] private GameObject centerSelectedRecipeModeObject; // [2] 선택한 레시피모드 (SelectProduct 시 활성화)
    [SerializeField] private Image selectionImage;
    [SerializeField] private TextMeshProUGUI selectionName;

    [Header("Bottom - [1] Default Mode (기본모드)")]
    [SerializeField] private GameObject bottomDefaultModeObject;
    [SerializeField] private Transform recipeGridParent;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("Bottom - [2] Select Mode (선택모드)")]
    [SerializeField] private GameObject bottomSelectProductModeObject;
    [SerializeField] private Transform requiredListParent;
    [SerializeField] private GameObject requireMaterialPrefab;
    [SerializeField] private TextMeshProUGUI productAmountText;
    [SerializeField] private Button btnMinus;
    [SerializeField] private Button btnPlus;
    [SerializeField] private Button reSelectBtn;
    [SerializeField] private Button craftBtn;

    [Header("Bottom - [3] Crafting Mode (제작모드)")]
    [SerializeField] private GameObject bottomCraftingModeObject;
    [SerializeField] private Image craftingItemIcon;
    [SerializeField] private Button craftingItemIconButton;         // 제작 아이콘 클릭 수령용 버튼
    [SerializeField] private TextMeshProUGUI craftingItemName;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI craftingStatusText;    // 상태 텍스트 ("제작중 .", "제작 완료!", "식량이 부족합니다" 등)
    [SerializeField] private TextMeshProUGUI craftingSpeedText;     // 제작 속도 (예: "30.0초 (개당)")
    [SerializeField] private TextMeshProUGUI craftingQuantityText;  // 완성된 수량 (0부터 시작하여 완성 시 +1)
    [SerializeField] private Button cancelBtn;

    private ProductionCraftRuntime targetFacility;
    public ProductionCraftRuntime TargetFacility => targetFacility;

    private ItemData activeSelectedRecipe;
    private HDY.Recipe.RecipeData activeSelectedRecipeData;

    private int selectedQuantity = 1;
    private int maxCraftableQuantity = 1;

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
        if (craftBtn != null) craftBtn.onClick.AddListener(OnClickCraftStart);
        if (cancelBtn != null) cancelBtn.onClick.AddListener(OnClickCancelCrafting);

        // 제작 아이콘 클릭 시 수령 연동
        if (craftingItemIconButton != null)
        {
            craftingItemIconButton.onClick.AddListener(OnClickCollectReward);
        }
        else if (craftingItemIcon != null && craftingItemIcon.TryGetComponent<Button>(out var iconBtn))
        {
            iconBtn.onClick.AddListener(OnClickCollectReward);
        }

        if (singleMemSlot != null) singleMemSlot.InitializeSlot(0);

        // [멤] 수량 숫자/증감 버튼 위에서 마우스 휠로도 수량을 조절할 수 있게 한다.
        AttachWheelStep(btnMinus != null ? btnMinus.gameObject : null);
        AttachWheelStep(btnPlus != null ? btnPlus.gameObject : null);
        AttachWheelStep(productAmountText != null ? productAmountText.gameObject : null);
if (singleMemSlot != null) singleMemSlot.InitializeSlot(0);
    }

    /// <summary>[멤] 숫자 표시/버튼 위에서 마우스 휠로 수량을 조절할 수 있도록 ScrollWheelStepInput을 붙이고 연결한다.</summary>
    private void AttachWheelStep(GameObject target)
    {
        if (target == null) return;

        var wheelInput = target.GetComponent<ScrollWheelStepInput>();
        if (wheelInput == null) wheelInput = target.AddComponent<ScrollWheelStepInput>();

        wheelInput.OnWheelStep += HandleWheelStep;
    }

    /// <summary>[멤] 휠 한 칸당 최대 제작 가능 수량의 5%(최소 1개)만큼 증가/감소시킨다. +/- 버튼 클릭(1개 단위)과는 별개의 조절 폭이다.</summary>
    private void HandleWheelStep(int direction)
    {
        if (maxCraftableQuantity <= 0) return;

        int step = Mathf.Max(1, Mathf.RoundToInt(maxCraftableQuantity * 0.05f)) * direction;
        ModifyQuantity(step);
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
            float progressNormalized = Mathf.Clamp01(targetFacility.currentProgressTime / targetFacility.totalRequiredTime);
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{progressNormalized * 100f:F0}%";

            UpdateCraftingStatusUI();
            UpdateCraftingInfoText();
        }

        bool canGet = (targetFacility.currentStorageCount > 0);
        if (craftingItemIconButton != null) craftingItemIconButton.interactable = canGet;
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

        bool isCraftingComplete = (targetFacility.remainingQuantity <= 0 && targetFacility.currentStorageCount > 0)
                               || (targetFacility.targetQuantity > 0 && targetFacility.currentStorageCount >= targetFacility.targetQuantity);

        if (isCraftingComplete)
        {
            StopDotsAnimation();
            if (craftingStatusText != null)
            {
                craftingStatusText.color = Color.white;
                craftingStatusText.text = "제작 완료!";
            }
        }
        else if (isNoMem)
        {
            StopDotsAnimation();
            if (craftingStatusText != null)
            {
                craftingStatusText.color = Color.white;
                craftingStatusText.text = "멤을 배치해야 합니다";
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
                if (craftingStatusText != null)
                {
                    craftingStatusText.color = Color.red;
                    craftingStatusText.text = "식량이 부족합니다";
                }
            }
        }
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

    private void UpdateCraftingInfoText()
    {
        if (targetFacility == null) return;

        if (craftingSpeedText != null)
        {
            craftingSpeedText.text = $"{targetFacility.totalRequiredTime:F1}초 (개당)";
        }

        if (craftingQuantityText != null)
        {
            craftingQuantityText.text = targetFacility.currentStorageCount.ToString();
        }
    }

    private void StartDotsAnimation(string customPrefix = null)
    {
        string prefix = customPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            prefix = "제작중";
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

        bool isMemMode = (currentUIState == CraftingUIState.Default || currentUIState == CraftingUIState.Crafting);
        if (centerMemModeObject != null) centerMemModeObject.SetActive(isMemMode);
        if (centerSelectedRecipeModeObject != null) centerSelectedRecipeModeObject.SetActive(currentUIState == CraftingUIState.SelectProduct);

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
            UpdateCraftingInfoText();
        }
        else
        {
            StopDotsAnimation();
        }
    }

    private void UpdateSelectProductCalculatedUI()
    {
        if (activeSelectedRecipe == null) return;

        if (productAmountText != null) productAmountText.text = selectedQuantity.ToString();

        if (btnMinus != null) btnMinus.interactable = (selectedQuantity > 1);
        if (btnPlus != null) btnPlus.interactable = (maxCraftableQuantity > 0 && selectedQuantity < maxCraftableQuantity);

        if (craftBtn != null)
        {
            craftBtn.interactable = (maxCraftableQuantity > 0 && selectedQuantity > 0);
        }
    }

    private void ModifyQuantity(int amount)
    {
        selectedQuantity = Mathf.Clamp(selectedQuantity + amount, 1, maxCraftableQuantity);
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

        currentUIState = CraftingUIState.SelectProduct;
        RefreshCraftingModeUI();
    }

    private void OnClickReSelect()
    {
        activeSelectedRecipe = null;
        selectedQuantity = 1;

        currentUIState = CraftingUIState.Default;
        RefreshCraftingModeUI();
    }

    private void OnClickCraftStart()
    {
        if (targetFacility == null || activeSelectedRecipe == null) return;

        if (targetFacility.DeployedMems.Count == 0) return;

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

        if (!isMaterialEnough) return;

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
        StopDotsAnimation();
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        RefreshCraftingModeUI();
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