using HDY.UI;
using HDY.Upgrade;
using System.Reflection;
using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    [Header("시설 Panel GameObject")]
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject productionPanel;
    [SerializeField] private GameObject ranchPanel;
    [SerializeField] private GameObject generatorPanel;
    [SerializeField] private GameObject transportPanel;
    [SerializeField] private GameObject campFirePanel;
    [SerializeField] private GameObject foodWarehousePanel;
    [SerializeField] private GameObject exploreMapPanel;
    [SerializeField] private GameObject UIPanel;

    [Header("시설 UI 컴포넌트")]
    [SerializeField] private CraftingPanelUI craftingPanelUI;
    [SerializeField] private ProductionPanelUI productionPanelUI;
    [SerializeField] private RanchPanelUI ranchPanelUI;
    [SerializeField] private GeneratorPanelUI generatorPanelUI;
    [SerializeField] private TransportPanelUI transportPanelUI;
    [SerializeField] private CampFirePanelUI campFirePanelUI; 

    [Header("공통 UI 버튼 그룹")]
    [SerializeField] private GameObject closeButtonGroup;
    [SerializeField] private GameObject placeButtonGroup;

    private GridManager cachedGridManager;
    private FieldInfo placementModeFieldInfo;

    public bool IsCraftingPanelActive => craftingPanel != null && craftingPanel.activeSelf;
    public bool IsProductionPanelActive => productionPanel != null && productionPanel.activeSelf;
    public bool IsRanchPanelActive => ranchPanel != null && ranchPanel.activeSelf;
    public bool IsGeneratorPanelActive => generatorPanel != null && generatorPanel.activeSelf;
    public bool IsTransportPanelActive => transportPanel != null && transportPanel.activeSelf;
    public bool IsCampFirePanelActive => campFirePanel != null && campFirePanel.activeSelf; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        CloseAllPanels();
    }

    private void Start()
    {
        cachedGridManager = FindFirstObjectByType<GridManager>();
        if (cachedGridManager != null)
        {
            placementModeFieldInfo = typeof(GridManager).GetField("isPlacementMode",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        }
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (IsAnyPanelOpen())
            {
                CloseAllPanels();
            }
            else
            {
                if (CheckIsGridPlacementModeActive())
                {
                    Debug.Log("<color=yellow><b>[PanelManager]</b></color> 배치 모드 중 ESC 입력 - GridManager.CancelPlacement() 실행.");
                    cachedGridManager.CancelPlacement();
                }
            }
        }
    }

    private bool CheckIsGridPlacementModeActive()
    {
        if (cachedGridManager == null)
        {
            cachedGridManager = FindFirstObjectByType<GridManager>();
            if (cachedGridManager != null)
            {
                placementModeFieldInfo = typeof(GridManager).GetField("isPlacementMode",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            }
        }
        if (cachedGridManager != null && placementModeFieldInfo != null)
        {
            return (bool)placementModeFieldInfo.GetValue(cachedGridManager);
        }
        return false;
    }

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
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
        }
    }

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
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
        }
    }

    public void OpenRanchPanel(RanchFacilityRuntime facility)
    {
        if (facility == null) return;
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (ranchPanel != null && ranchPanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);
            UIPanel.SetActive(true);
            ranchPanel.SetActive(true);
            ranchPanelUI.OpenPanel(facility);
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
        }
    }

    public void OpenTransportPanel(TransportRuntime facility)
    {
        if (facility == null) return;
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (transportPanel != null && transportPanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);
            UIPanel.SetActive(true);
            transportPanel.SetActive(true);
            transportPanelUI.OpenPanel(facility);
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
        }
    }

    public void OpenGeneratorPanel(GeneratorRuntime facility)
    {
        if (facility == null) return;
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (generatorPanel != null && generatorPanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);
            UIPanel.SetActive(true);
            generatorPanel.SetActive(true);
            generatorPanelUI.OpenPanel(facility);
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
        }
    }

    public void OpenCampFirePanel(CampFireRuntime facility)
    {
        if (facility == null) return;
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        CloseAllPanels();

        if (campFirePanel != null && campFirePanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);
            UIPanel.SetActive(true);
            campFirePanel.SetActive(true);
            campFirePanelUI.OpenPanel(facility);
            SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
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
            if (WayPointManager.Instance != null)
            {
                WayPointManager.Instance.OpenTravelMap();
            }
        }
    }

    public void NotifyHUDPanelOpened()
    {
        if (craftingPanelUI != null) craftingPanelUI.ClosePanel();
        if (productionPanelUI != null) productionPanelUI.ClosePanel();
        if (ranchPanelUI != null) ranchPanelUI.ClosePanel();
        if (generatorPanelUI != null) generatorPanelUI.ClosePanel();
        if (transportPanelUI != null) transportPanelUI.ClosePanel();
        if (campFirePanelUI != null) campFirePanelUI.ClosePanel(); 

        if (foodWarehousePanel != null) foodWarehousePanel.SetActive(false);
        if (exploreMapPanel != null) exploreMapPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);
        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (productionPanel != null) productionPanel.SetActive(false);
        if (ranchPanel != null) ranchPanel.SetActive(false);
        if (generatorPanel != null) generatorPanel.SetActive(false);
        if (transportPanel != null) transportPanel.SetActive(false);
        if (campFirePanel != null) campFirePanel.SetActive(false); 

        SetCommonGroupActive(true);
        SetCameraControllersEnabled(false);

        var activeExplorationUI = FindFirstObjectByType<HDY.UI.ExplorationPanelUI>();
        if (activeExplorationUI != null && activeExplorationUI.gameObject.activeInHierarchy)
        {
            if (SortButtonManagement.Instance != null)
            {
                SortButtonManagement.Instance.UpdateSortFilters(activeExplorationUI.gameObject);
            }
        }
    }

    public void CloseAllPanels()
    {
        if (UIManager.Instance != null) UIManager.Instance.CloseCurrent();
        if (craftingPanelUI != null) craftingPanelUI.ClosePanel();
        if (productionPanelUI != null) productionPanelUI.ClosePanel();
        if (ranchPanelUI != null) ranchPanelUI.ClosePanel();
        if (generatorPanelUI != null) generatorPanelUI.ClosePanel();
        if (transportPanelUI != null) transportPanelUI.ClosePanel();
        if (campFirePanelUI != null) campFirePanelUI.ClosePanel(); 

        if (foodWarehousePanel != null) foodWarehousePanel.SetActive(false);
        if (exploreMapPanel != null) exploreMapPanel.SetActive(false);
        if (UIPanel != null) UIPanel.SetActive(false);
        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (productionPanel != null) productionPanel.SetActive(false);
        if (ranchPanel != null) ranchPanel.SetActive(false);
        if (generatorPanel != null) generatorPanel.SetActive(false);
        if (transportPanel != null) transportPanel.SetActive(false);
        if (campFirePanel != null) campFirePanel.SetActive(false); 

        SetCommonGroupActive(false);
        SetCameraControllersEnabled(true);
    }

    private bool IsAnyPanelOpen()
    {
        bool isCraftActive = craftingPanel != null && craftingPanel.activeSelf;
        bool isProductActive = productionPanel != null && productionPanel.activeSelf;
        bool isRanchActive = ranchPanel != null && ranchPanel.activeSelf;
        bool isGenActive = generatorPanel != null && generatorPanel.activeSelf;
        bool isTransActive = transportPanel != null && transportPanel.activeSelf;
        bool isCampFireActive = campFirePanel != null && campFirePanel.activeSelf;
        bool isInventoryActive = foodWarehousePanel != null && foodWarehousePanel.activeSelf;
        bool isHUDActive = UIManager.Instance != null && UIManager.Instance.HasActivePanel();
        bool isMapActive = exploreMapPanel != null && exploreMapPanel.activeSelf;

        return isCraftActive || isProductActive || isRanchActive || isGenActive || isInventoryActive || isHUDActive || isMapActive || isTransActive || isCampFireActive;
    }

    private void SetCommonGroupActive(bool isPanelOpen)
    {
        if (placeButtonGroup != null) placeButtonGroup.SetActive(!isPanelOpen);
    }

    public void SetCameraControllersEnabled(bool isEnable)
    {
        CameraMoveController moveController = Object.FindFirstObjectByType<CameraMoveController>();
        if (moveController != null) moveController.enabled = isEnable;

        CameraZoomController zoomController = Object.FindFirstObjectByType<CameraZoomController>();
        if (zoomController != null) zoomController.enabled = isEnable;
    }
}