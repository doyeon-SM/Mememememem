using HDY.UI;
using UnityEngine;
using HDY.Upgrade;

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    [Header("시설별 Panel GameObject")]
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject productionPanel;
    [SerializeField] private GameObject foodWarehousePanel;
    [SerializeField] private GameObject exploreMapPanel;
    [SerializeField] private GameObject UIPanel;

    [Header("시설별 UI 패널 컴포넌트")]
    [SerializeField] private CraftingPanelUI craftingPanelUI;
    [SerializeField] private ProductionPanelUI productionPanelUI;

    [Header("영지 UI 공통 제어 오브젝트: 닫기 버튼, 배치모드 버튼")]
    [SerializeField] private GameObject closeButtonGroup;
    [SerializeField] private GameObject placeButtonGroup;


    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CloseAllPanels();
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsAnyPanelOpen())
            {
                CloseAllPanels();
            }
        }
    }

    /// <summary>
    /// 제작 패널 활성화
    /// </summary>
    public void OpenCraftingPanel(ProductionCraftRuntime facility)
    {
        if (facility == null) return;

        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (craftingPanel != null && craftingPanelUI != null)
        {
            SetCommonGroupActive(true);         
            SetCameraControllersEnabled(false); 

            UIPanel.SetActive(true);
            craftingPanel.SetActive(true); 
            craftingPanelUI.OpenPanel(facility);

            if (SortButtonManagement.Instance != null)
            {
                SortButtonManagement.Instance.UpdateSortFiltersByFacility(facility.gameObject);
            }
        }
    }

    /// <summary>
    /// 생산 패널 활성화
    /// </summary>
    public void OpenProductionPanel(ProductionFacilityRuntime facility)
    {
        if (facility == null) return;

        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (productionPanel != null && productionPanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);

            UIPanel.SetActive(true);
            productionPanel.SetActive(true); 
            productionPanelUI.OpenPanel(facility); 

            if (SortButtonManagement.Instance != null)
            {
                SortButtonManagement.Instance.UpdateSortFiltersByFacility(facility.gameObject);
            }
        }
    }


    public void OpenFoodWareHousePanel()
    {
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (foodWarehousePanel != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);

            foodWarehousePanel.SetActive(true);
        }
    }

    public void OpenExploreMapPanel()
    {
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (exploreMapPanel != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);

            exploreMapPanel.SetActive(true);
            if(WayPointManager.Instance != null)
            {
                WayPointManager.Instance.OpenTravelMap();
            }
            
        }
    }


    /// <summary>
    /// UIManager를 통해 패널 활성화시 기존 시설물 창들을 세이브 후 클리어 처리, 공통 UI 닫기 버튼 On 및 카메라 차단
    /// </summary>
    public void NotifyHUDPanelOpened()
    {
        //SaveActivePanelData();

        if (craftingPanelUI != null) craftingPanelUI.ClosePanel();
        if (productionPanelUI != null) productionPanelUI.ClosePanel();
        if (foodWarehousePanel != null) foodWarehousePanel.SetActive(false);
        if (exploreMapPanel != null) exploreMapPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);

        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (productionPanel != null) productionPanel.SetActive(false);

        SetCommonGroupActive(true);
        SetCameraControllersEnabled(false);
    }

    /// <summary>
    /// 영지관련 패널과 Close버튼 닫기 및 Place버튼 활성화
    /// </summary>
    public void CloseAllPanels()
    {
        //SaveActivePanelData();

        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();

        if (craftingPanelUI != null) craftingPanelUI.ClosePanel();
        if (productionPanelUI != null) productionPanelUI.ClosePanel();
        if (foodWarehousePanel != null) foodWarehousePanel.SetActive(false);
        if (exploreMapPanel != null) exploreMapPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);

        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (productionPanel != null) productionPanel.SetActive(false);


        SetCommonGroupActive(false);
        SetCameraControllersEnabled(true);
    }

    private bool IsAnyPanelOpen()
    {
        bool isCraftActive = craftingPanel != null && craftingPanel.activeSelf;
        bool isProductActive = productionPanel != null && productionPanel.activeSelf;
        bool isInventoryActive = foodWarehousePanel != null && foodWarehousePanel.activeSelf;
        bool isHUDActive = UIManager.Instance != null && UIManager.Instance.HasActivePanel();
        return isCraftActive || isProductActive || isInventoryActive;
    }

    private void SetCommonGroupActive(bool isPanelOpen)
    {
        if (closeButtonGroup != null) closeButtonGroup.SetActive(isPanelOpen);
        if (placeButtonGroup != null) placeButtonGroup.SetActive(!isPanelOpen);
    }

    public void SetCameraControllersEnabled(bool isEnable)
    {
        CameraMoveController moveController = Object.FindFirstObjectByType<CameraMoveController>();
        if (moveController != null) moveController.enabled = isEnable;

        CameraZoomController zoomController = Object.FindFirstObjectByType<CameraZoomController>();
        if (zoomController != null) zoomController.enabled = isEnable;
    }

    ///// <summary>
    ///// 현재 켜져 있는 생산/제작 패널의 실시간 데이터를 패널을 닫기 전에 저장.
    ///// </summary>
    //private void SaveActivePanelData()
    //{
    //    if (productionPanel != null && productionPanel.activeSelf && productionPanelUI != null)
    //    {
    //        var facility = productionPanelUI.TargetFacility;
    //        if (facility != null && facility.buildingData != null && PlantSystem.Instance != null)
    //        {
    //            var br = facility.GetComponent<BuildingRuntime>();
    //            string uniqueId = br != null ? $"{facility.buildingData.buildingName}_{br.gridX}_{br.gridZ}" : facility.buildingData.buildingId;

    //            var data = PlantSystem.Instance.GetFacilityData(uniqueId);
    //            data.isActive = facility.isProducing;
    //            data.currentCraftingItemId = facility.craftingItem != null ? facility.craftingItem.Item_ID : "";
    //            data.currentProgressTime = facility.currentProgressTime;
    //            data.currentStorageCount = facility.currentStorageCount;

    //            PlantSystem.Instance.UpdateFacilityData(uniqueId, data);
    //        }
    //    }

    //    if (craftingPanel != null && craftingPanel.activeSelf && craftingPanelUI != null)
    //    {
    //        var craft = craftingPanelUI.TargetFacility;
    //        if (craft != null && craft.buildingData != null && PlantSystem.Instance != null)
    //        {
    //            var br = craft.GetComponent<BuildingRuntime>();
    //            string uniqueId = br != null ? $"{craft.buildingData.buildingName}_{br.gridX}_{br.gridZ}" : craft.buildingData.buildingId;

    //            var data = PlantSystem.Instance.GetFacilityData(uniqueId);
    //            data.isActive = craft.isProducing;
    //            data.currentCraftingItemId = craft.currentCraftingItem != null ? craft.currentCraftingItem.Item_ID : "";
    //            data.targetQuantity = craft.targetQuantity;
    //            data.remainingQuantity = craft.remainingQuantity;
    //            data.currentProgressTime = craft.currentProgressTime;
    //            data.currentStorageCount = craft.currentStorageCount;

    //            PlantSystem.Instance.UpdateFacilityData(uniqueId, data);
    //        }
    //    }
    //}
}