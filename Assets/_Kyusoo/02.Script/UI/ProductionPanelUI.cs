using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MemSystem.Data;

public class ProductionPanelUI : MonoBehaviour
{
    public static ProductionPanelUI Instance { get; private set; }

    [Header("최상위 패널 오브젝트")]
    [SerializeField] private GameObject productionPanelRoot; 

    [Header("중앙 패널 - Top")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUp;

    [Header("중앙 패널 - Center")]
    [SerializeField] private ProductionMemSlotUI[] memSlotImages = new ProductionMemSlotUI[5];
    [SerializeField] private GameObject defaultMode;   
    [SerializeField] private GameObject creatingMode;  
    [SerializeField] private Image creatingItem;       
    [SerializeField] private TextMeshProUGUI completeCreateCount; 
    [SerializeField] private Button diamondBGBtn;    

    [Header("중앙 패널 - Bottom")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;

    [Header("제작할 아이템 관련 정보: 프리팹, 생성 위치, SO리스트 전체")]
    [SerializeField] private GameObject craftingSlotPrefab;
    [SerializeField] private Transform craftingSlotParent;
    [SerializeField] private List<ProductItemData> allProductItems = new List<ProductItemData>();

    private List<GameObject> activeCraftingSlots = new List<GameObject>();

    [Header("우측 패널 - 멤 생산 Stat 아이콘 레퍼런스 가방")]
    [SerializeField] private Sprite craftingStatIcon;
    [SerializeField] private Sprite loggingStatIcon;
    [SerializeField] private Sprite miningStatIcon;
    [SerializeField] private Sprite transportStatIcon;
    [SerializeField] private Sprite farmingStatIcon;

    // 현재 UI 창이 조준하고 있는 타겟 시설 스크립트 캐싱
    private ProductionFacilityRuntime targetFacility;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (productionPanelRoot != null) productionPanelRoot.SetActive(false);

        if (diamondBGBtn != null)
        {
            diamondBGBtn.onClick.AddListener(OnClickCollectReward);
        }

        for (int i = 0; i < memSlotImages.Length; i++)
        {
            if (memSlotImages[i] != null) memSlotImages[i].InitializeSlot(i);
        }
    }

    /// <summary>
    /// 아이템 생산시 슬라이더 변화처리
    /// </summary>
    private void Update()
    {
        if (targetFacility == null || !targetFacility.isProducing) return;

        if (progressBar != null && targetFacility.totalRequiredTime > 0)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            progressBar.value = progressNormalized;

            if (durationText != null)
            {
                durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";
            }
        }

        UpdateStorageText();
    }

    /// <summary>
    /// 기본 모드에서 시설물 클릭 시 패널 UI를 활성화
    /// </summary>
    public void OpenPanel(ProductionFacilityRuntime facility)
    {
        if (facility == null) return;

        targetFacility = facility;
        productionPanelRoot.SetActive(true);

        RefreshStaticUI();
        RefreshProductionMode();
    }

    /// <summary>
    /// 패널이 열릴 때 시설의 이름, 레벨, 멤 슬롯 상태 등의 정보 받아오기
    /// </summary>
    private void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        buildingName.text = targetFacility.buildingData.buildingName;
        buildingLevel.text = $"Lv {targetFacility.currentLevel}";

        int maxCapacity = ProductionCalculator.GetMaxMemCount(targetFacility.currentLevel);
        
        for (int i = 0; i < memSlotImages.Length; i++)
        {
            if (memSlotImages[i] == null) continue;

            bool isUnlocked = (i < maxCapacity);
            MemData placedMemData = null;

            if (isUnlocked && i < targetFacility.DeployedMems.Count)
            {
                placedMemData = targetFacility.DeployedMems[i];
            }

            memSlotImages[i].RefreshStatus(isUnlocked, placedMemData);
        }
    }

    /// <summary>
    /// 드롭 이벤트를 수신하여 멤 배치 및 펫정보 수신
    /// </summary>
    public void TryDeployMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        bool isSuccess = targetFacility.TryAddMem(targetMem);

        if (isSuccess)
        {
            //LoadAndCacheMemStats(targetMem);

            RefreshStaticUI();
        }
    }

    /// <summary>
    /// 배치된 멤의 스탯값을 가져와서 적용하기
    /// </summary>
    private void LoadAndCacheMemStats(MemData memData)
    {
        if (memData == null) return;

        int craftLvl = memData.productionStats.crafting;
        int logLvl = memData.productionStats.logging;
        int mineLvl = memData.productionStats.mining;
        int transLvl = memData.productionStats.transport;
        int farmLvl = memData.productionStats.farming;

    }

    /// <summary>
    /// 제작중일때 아닐때 전환처리
    /// </summary>
    private void RefreshProductionMode()
    {
        if (targetFacility == null) return;

        bool isWorking = targetFacility.isProducing;

        defaultMode.SetActive(isWorking);
        creatingMode.SetActive(!isWorking);

        if (isWorking && targetFacility.currentProductItem != null)
        {
            creatingItem.sprite = targetFacility.currentProductItem.itemIcon;
            creatingItem.gameObject.SetActive(true);

            ClearCraftingSlots();
        }
        else
        {
            creatingItem.gameObject.SetActive(false);

            GenerateFacilityCraftingSlots();
        }

        UpdateStorageText();
    }

    /// <summary>
    /// 현재 시설에 매칭되는 아이템들만 추출하여 UI에 표시
    /// </summary>
    private void GenerateFacilityCraftingSlots()
    {
        ClearCraftingSlots();

        if (targetFacility == null || craftingSlotPrefab == null || craftingSlotParent == null) return;

        BuildingType currentFacilityType = targetFacility.buildingData.buildingType;

        foreach (ProductItemData item in allProductItems)
        {
            if (item == null) continue;

            
            if (item.matchBuildingType == currentFacilityType)
            {
                GameObject slotInstance = Instantiate(craftingSlotPrefab, craftingSlotParent);

                if (slotInstance.TryGetComponent<CraftingSlotUI>(out CraftingSlotUI slotUI))
                {
                    slotUI.Setup(item);
                }
                activeCraftingSlots.Add(slotInstance);
            }
        }
    }

    /// <summary>
    /// 오류 방지용 비우기
    /// </summary>
    private void ClearCraftingSlots()
    {
        foreach (GameObject slot in activeCraftingSlots)
        {
            if (slot != null) Destroy(slot);
        }
        activeCraftingSlots.Clear();
    }

    /// <summary>
    /// 시설 내 저장된 수량 텍스트 업데이트
    /// </summary>
    private void UpdateStorageText()
    {
        if (targetFacility == null || completeCreateCount == null) return;

        completeCreateCount.text = targetFacility.currentStorageCount.ToString();
    }

    /// <summary>
    /// 특정 아이템 제작을 위해 슬롯을 클릭했을 때 호출할 함수
    /// </summary>
    public void OnSelectItemProduce(ProductItemData itemData)
    {
        if (targetFacility == null || itemData == null) return;

        targetFacility.SelectAndStartProduction(itemData);

        RefreshProductionMode();
    }

    /// <summary>
    /// 생산중인 아이템 버튼 클릭 시 수령 처리 연동
    /// </summary>
    private void OnClickCollectReward()
    {
        if (targetFacility == null) return;

        targetFacility.StoredItems();

        UpdateStorageText();
    }

    /// <summary>
    /// UI 닫기 버튼용
    /// </summary>
    public void ClosePanel()
    {
        ClearCraftingSlots();
        targetFacility = null;
        productionPanelRoot.SetActive(false);
    }
}