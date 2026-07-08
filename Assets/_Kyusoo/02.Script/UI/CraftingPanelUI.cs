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

    [Header("최상위 패널 오브젝트: 패널, 닫기버튼")]
    [SerializeField] private GameObject craftingPanelRoot;
    [SerializeField] private GameObject centerCraftingPanel;
    [SerializeField] private GameObject CloseButtonGroup;
    [SerializeField] private Button closeBtn;
    [SerializeField] private GameObject PlaceButtonGroup;

    [Header("중앙 패널 - Top 빌딩 이름")]
    [SerializeField] private TextMeshProUGUI buildingName;

    [Header("중앙 패널 - Center 멤슬롯")]
    [SerializeField] private ProductionMemSlotUI singleMemSlot;

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

    private int selectedQuantity = 1;
    private int maxCraftableQuantity = 1; 

    private bool isUpdatingQuantitySystem = false;
    private Coroutine errorFeedbackCoroutine;

    private const float VIRTUAL_BASE_CRAFT_TIME = 20f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (craftingPanelRoot != null) craftingPanelRoot.SetActive(false);
        if (centerCraftingPanel != null) centerCraftingPanel.SetActive(false);

        if (closeBtn != null) closeBtn.onClick.AddListener(ClosePanel);

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
        if (craftingPanelRoot != null && craftingPanelRoot.activeSelf)
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
                return;
            }
        }

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

        CloseButtonGroup.SetActive(true);
        centerCraftingPanel.SetActive(true);
        PlaceButtonGroup.SetActive(false);
        craftingPanelRoot.SetActive(true);
        SetCameraControllersEnabled(false);

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

        if (errorFeedbackCoroutine == null && craftingDurationText != null)
        {
            float singleTime = ProductionCalculator.CalculateFinalProductionTime(VIRTUAL_BASE_CRAFT_TIME, targetFacility.DeployedMems);
            float totalEstimatedTime = singleTime * selectedQuantity;
            craftingDurationText.text = $"제작 예상시간: {totalEstimatedTime:F1}초 (개당 {singleTime:F1}초 × {selectedQuantity}개)";
        }
    }

    private void OnSliderQuantityChanged(float value)
    {
        if (isUpdatingQuantitySystem) return;

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
    /// </summary>
    private int CalculateMaxCraftableLimitAmount(ItemData recipe)
    {
        // TODO :: recipe를 기반으로 요구재료를 받아오고, 재료 보유량을 기반으로 최대 제작 가능 수량 계산하여 반환
        
        return 3; 
    }

    private void GenerateRequiredMaterialListUI()
    {
        foreach (Transform child in requiredListParent) Destroy(child.gameObject);

        // TODO :: 요구 재료관련 프리팹 생성
        // TODO :: 보유량 / 필요수량 매핑처리
    }

    // 해금된 제작 레시피들 UI에 표시
    private void GenerateAvailableRecipeList()
    {
        foreach (Transform child in recipeGridParent) Destroy(child.gameObject);
    }

    /// <summary>
    /// 목록판 리스트에서 임의 아이템 클릭시 Select_Product모드 전환
    /// </summary>
    public void OnSelectItemRecipe(ItemData selectedItem)
    {
        if (targetFacility == null || selectedItem == null) return;

        activeSelectedRecipe = selectedItem;
        selectedQuantity = 1;

        maxCraftableQuantity = CalculateMaxCraftableLimitAmount(selectedItem);
        if (quantitySlider != null)
        {
            quantitySlider.minValue = 1;
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

        // TODO :: 제작갯수 * 요구수량 < 보유 수량 인지 확인 후, 아닐경우 isMaterialEnough = FALSE처리

        if (!isMaterialEnough)
        {
            TriggerErrorFeedbackAlert("제작 요구수량이 부족합니다");
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

    public void ClosePanel()
    {
        if (errorFeedbackCoroutine != null) StopCoroutine(errorFeedbackCoroutine);
        errorFeedbackCoroutine = null;

        targetFacility = null;

        CloseButtonGroup.SetActive(true);
        PlaceButtonGroup.SetActive(false);
        SetCameraControllersEnabled(true);
        craftingPanelRoot.SetActive(false);
        centerCraftingPanel.SetActive(false);
    }

    private void SetCameraControllersEnabled(bool isEnable)
    {
        CameraMoveController moveController = Object.FindFirstObjectByType<CameraMoveController>();
        if (moveController != null) moveController.enabled = isEnable;

        CameraZoomController zoomController = Object.FindFirstObjectByType<CameraZoomController>();
        if (zoomController != null) zoomController.enabled = isEnable;
    }
}