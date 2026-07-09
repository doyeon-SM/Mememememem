using HDY.Capture;
using HDY.Recipe;
using HDY.UI;
using HDY.Item; 
using MemSystem.Data;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftingPanelUI : MonoBehaviour
{
    public static CraftingPanelUI Instance { get; private set; }

    private enum CraftingUIState { Default, SelectProduct, Crafting }
    private CraftingUIState currentUIState = CraftingUIState.Default;

    //[Header("최상위 패널 오브젝트: 패널, 닫기버튼")]
    //[SerializeField] private GameObject craftingPanelRoot;
    //[SerializeField] private GameObject centerCraftingPanel;
    //[SerializeField] private GameObject CloseButtonGroup;
    //[SerializeField] private Button closeBtn;
    //[SerializeField] private GameObject PlaceButtonGroup;

    [Header("중앙 패널 - Top 빌딩 이름")]
    [SerializeField] private TextMeshProUGUI buildingName;

    [Header("중앙 패널 - Center 멤슬롯")]
    [SerializeField] private CraftingMemSlotUI singleMemSlot;

    [Header("중앙 패널 - Center: Default_Mode 오브젝트, 레시피 생성될 영역, 레시피 프리팹 ")]
    [SerializeField] private GameObject defaultModeObject;
    [SerializeField] private Transform recipeGridParent;
    [SerializeField] private GameObject recipeSlotPrefab;

    [Header("중앙 패널 - Center: Select_Product 오브젝트, 선택 아이템 정보, 요구 재료 위치 및 프리팹, 요구수량, 수량 조절 버튼 및 슬라이드")]
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

    [Header("중앙 패널 - Center: Crafting 오브젝트, 제작 아이템 정보, 수령 버튼, 완성수량")]
    [SerializeField] private GameObject craftingModeObject;
    [SerializeField] private Image craftingItemIcon;
    [SerializeField] private TextMeshProUGUI craftingItemName;
    [SerializeField] private Button collectRewardBtn; 
    [SerializeField] private TextMeshProUGUI completeCountText;  

    [Header("중앙 패널 - Bottom: Default_Mode 오브젝트, Desc연결")]
    [SerializeField] private GameObject bottomDefaultModeObject;
    [SerializeField] private TextMeshProUGUI selectGuideText;

    [Header("중앙 패널 - Bottom: Select_Product 오브젝트, 제작 예상 시간, 다시 선택/제작 버튼")]
    [SerializeField] private GameObject bottomSelectProductModeObject;
    [SerializeField] private TextMeshProUGUI craftingDurationText; 
    [SerializeField] private Button reSelectBtn; 
    [SerializeField] private Button craftBtn;    

    [Header("중앙 패널 - Bottom: Crafting 오브젝트, 진행도, 완성품 취소/수령 버튼")]
    [SerializeField] private GameObject bottomCraftingModeObject;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText; 
    [SerializeField] private Button cancelBtn;
    [SerializeField] private Button getBtn;

    private ProductionCraftRuntime targetFacility;

    private ItemData activeSelectedRecipe;
    private HDY.Recipe.RecipeData activeSelectedRecipeData;

    private int selectedQuantity = 1;
    private int maxCraftableQuantity = 1; 

    private bool isUpdatingQuantitySystem = false;
    private Coroutine errorFeedbackCoroutine;

    private const float VIRTUAL_BASE_CRAFT_TIME = 20f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        //if (craftingPanelRoot != null) craftingPanelRoot.SetActive(false);
        //if (centerCraftingPanel != null) centerCraftingPanel.SetActive(false);

        //if (closeBtn != null) closeBtn.onClick.AddListener(ClosePanel);

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

    private void Update()
    {
        //if (!craftingPanelRoot.activeSelf) CloseButtonGroup.SetActive(false);
        //if (craftingPanelRoot != null && craftingPanelRoot.activeSelf)
        //{
        //    if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        //    {
        //        ClosePanel();
        //        return;
        //    }
        //}

        if (targetFacility == null) return;

        if (currentUIState == CraftingUIState.Crafting && targetFacility.isProducing && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";
        }

        bool canGet = (targetFacility.currentStorageCount > 0);
        if (getBtn != null) getBtn.interactable = canGet;
        if (collectRewardBtn != null) collectRewardBtn.interactable = canGet;

        UpdateStorageText();
    }

    public void OpenPanel(ProductionCraftRuntime facility)
    {
        if (facility == null) return;

        targetFacility = facility;

        RefreshStaticUI();

        if (targetFacility.isProducing || targetFacility.currentCraftingItem != null)
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

    private void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        buildingName.text = targetFacility.buildingData.buildingName;

        MemData placedMemData = targetFacility.DeployedMems.Count > 0 ? targetFacility.DeployedMems[0] : null;
        CapturedMemEntry placedEntryData = targetFacility.DeployedMemEntries.Count > 0 ? targetFacility.DeployedMemEntries[0] : null;

        if (singleMemSlot != null)
        {
            singleMemSlot.RefreshStatus(true, placedMemData, placedEntryData);
        }
    }

    /// <summary>
    /// 제작의 3단계 흐름에 따라 Center, Bottom에 분리한 3가지 모드를 세트로 전환처리
    /// </summary>
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

        if (currentUIState == CraftingUIState.Crafting && targetFacility.currentCraftingItem != null)
        {
            if (craftingItemIcon != null) craftingItemIcon.sprite = targetFacility.currentCraftingItem.ItemIcon;
            if (craftingItemName != null) craftingItemName.text = targetFacility.currentCraftingItem.ItemName;
        }
    }

    /// <summary>
    /// 멤 배치를 통해 예상 소요 시간을 실시간으로 연산하여 표시
    /// </summary>
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
                float singleTime = ProductionCalculator.CalculateFinalProductionTime(VIRTUAL_BASE_CRAFT_TIME, targetFacility.DeployedMems);
                float totalEstimatedTime = singleTime * selectedQuantity;
                craftingDurationText.text = $"제작 예상시간: {totalEstimatedTime:F1}초 (개당 {singleTime:F1}초)";
            }
        }
    }

    private void OnSliderQuantityChanged(float value)
    {
        if (isUpdatingQuantitySystem) return;

        selectedQuantity = Mathf.Clamp(Mathf.RoundToInt(value), 1, maxCraftableQuantity);
        selectedQuantity = Mathf.RoundToInt(value);
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

    /// <summary>
    /// 인벤토리 or 창고 내 원자재 한계치를 기반으로 최대 제작 가능 수량 계산
    /// 최소 제작에 필요한 재료도 부족한 경우 0을 반환
    /// </summary>
    private int CalculateMaxCraftableLimitAmount(ItemData recipe)
    {
        if (recipe == null || activeSelectedRecipeData == null) return 1;

        int finalCalculatedMax = int.MaxValue;
        bool hasMaterialsAtAll = false;

        foreach (Recipe_Requset_Item_Data reqItem in activeSelectedRecipeData.Requset_Items_ID)
        {
            if (reqItem == null || string.IsNullOrEmpty(reqItem.Item_ID)) continue;

            hasMaterialsAtAll = true;

            // Mock데이터
            int inventoryOwned = 100;
            if (reqItem.Item_ID == "item_irongemstone") inventoryOwned = 62;
            if (reqItem.Item_ID == "item_wood") inventoryOwned = 39;

            if (reqItem.Amount <= 0) continue;

            int possibleMaxByThisMaterial = inventoryOwned / reqItem.Amount;
            if (possibleMaxByThisMaterial < finalCalculatedMax)
            {
                finalCalculatedMax = possibleMaxByThisMaterial;
            }
        }

        if (!hasMaterialsAtAll) return 0;

        return Mathf.Max(0, finalCalculatedMax);
    }

    /// <summary>
    /// 수량 조절을 통해 필요한 재료 갯수를 업데이트(+, -, 최대, 최소, 슬라이더 조절)에 맞춰서 처리
    /// </summary>
    private void GenerateRequiredMaterialListUI()
    {
        foreach (Transform child in requiredListParent) Destroy(child.gameObject);

        if (activeSelectedRecipe == null || activeSelectedRecipeData == null) return;

        foreach (Recipe_Requset_Item_Data requestData in activeSelectedRecipeData.Requset_Items_ID)
        {
            if (requestData == null || string.IsNullOrEmpty(requestData.Item_ID)) continue;

            RecipeUnlockManager recipeManager = Object.FindFirstObjectByType<RecipeUnlockManager>();
            ItemData materialItemData = null;

            if (recipeManager != null)
            {
                materialItemData = recipeManager.FindRecipeItemData(requestData.Item_ID);
            }

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

    // 해금된 제작 레시피들 UI에 표시
    private void GenerateAvailableRecipeList()
    {
        foreach (Transform child in recipeGridParent) Destroy(child.gameObject);

        RecipeUnlockManager recipeManager = Object.FindFirstObjectByType<RecipeUnlockManager>();

        if (recipeManager == null)
        {
            return;
        }

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
        else
        {
            Debug.Log("[공방] RecipeUnlockManager 내부에 등록된 레시피 엔트리가 존재하지 않습니다.");
        }
    }

    /// <summary>
    /// 해금된 레시피중 하나를 클릭할때, ItemCatalogManager의 recipeId
    /// ItemCatalogManager 산하의 중앙 카탈로그(recipeDataList)를 직접 실시간 쿼리하여 매칭되는 RecipeData를 가져옵니다.
    /// </summary>
    public void OnSelectItemRecipe(ItemData selectedItem)
    {
        if (targetFacility == null || selectedItem == null) return;

        activeSelectedRecipe = selectedItem;
        activeSelectedRecipeData = null;

        RecipeData[] allRecipesInProject = Resources.FindObjectsOfTypeAll<RecipeData>();
        foreach (RecipeData recipe in allRecipesInProject)
        {
            if (recipe != null && recipe.Recipe_Item_ID == selectedItem.Item_ID)
            {
                activeSelectedRecipeData = recipe; 
                break;
            }
        }

        if (activeSelectedRecipeData == null)
        {
            Debug.LogWarning($"RecipeData SO 제작법을 프로젝트 내부에서 탐색해내지 못했습니다.");
        }

        maxCraftableQuantity = CalculateMaxCraftableLimitAmount(selectedItem);

        selectedQuantity = (maxCraftableQuantity == 0) ? 0 : 1;

        if (quantitySlider != null)
        {
            quantitySlider.minValue = (maxCraftableQuantity == 0) ? 0 : 1;
            quantitySlider.maxValue = maxCraftableQuantity;
            quantitySlider.wholeNumbers = true;
        }

        currentUIState = CraftingUIState.SelectProduct;
        RefreshCraftingModeUI();
    }

    /// <summary>
    /// 다시선택 클릭 시 제작 가능 리스트 목록으로 복귀
    /// </summary>
    private void OnClickReSelect()
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = null;

        activeSelectedRecipe = null;
        selectedQuantity = 1;

        currentUIState = CraftingUIState.Default;
        RefreshCraftingModeUI();
    }

    /// <summary>
    /// 제작 조건 미충족 상태를 판별하여 조건 미충족시 시각적 피드백 제공
    /// 멤 배치, 제작에 필요한 재료 보유 여부를 조건으로 판별
    /// 이상없으면 Crafting_Mode로 전환
    /// </summary>
    private void OnClickCraftStart()
    {
        if (targetFacility == null || activeSelectedRecipe == null) return;

        if (targetFacility.DeployedMems.Count == 0)
        {
            TriggerErrorFeedbackAlert("멤이 배치되지 않았습니다");
            return;
        }

        bool isMaterialEnough = true;

        if (activeSelectedRecipeData != null && activeSelectedRecipeData.Requset_Items_ID != null)
        {
            foreach (Recipe_Requset_Item_Data req in activeSelectedRecipeData.Requset_Items_ID)
            {
                if (req == null || string.IsNullOrEmpty(req.Item_ID)) continue;

                int ownedCount = 50;
                if (req.Item_ID == "Item_Wood") ownedCount = 62;
                if (req.Item_ID == "Item_Irongemstone") ownedCount = 39;

                int totalRequired = req.Amount * selectedQuantity;
                if (ownedCount < totalRequired)
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

        targetFacility.SelectAndStartCrafting(activeSelectedRecipe, selectedQuantity);

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

    // 제작 아이템 수령 분기에 따른 UI 갱신
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

    /// <summary>
    /// 멤 창고에서 드래그하여 슬롯에 드롭할 때 이벤트 수신
    /// </summary>
    public void TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return;

        // 실질적인 배치 가동은 런타임 스크립트에 지시
        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);

        if (isSuccess)
        {
            RefreshStaticUI();

            // 👥 만약 수량 선택 대기 중인 상태라면, 멤이 추가됨에 따라 버프를 재연산하여 예상 시간을 실시간으로 깎아줍니다.
            if (currentUIState == CraftingUIState.SelectProduct)
            {
                UpdateSelectProductCalculatedUI();
            }
        }
    }

    /// <summary>
    /// 슬롯에 배치된 멤 제거처리
    /// </summary>
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

        targetFacility = null;
        
    }
}