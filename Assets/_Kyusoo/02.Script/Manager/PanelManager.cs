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
    [SerializeField] private GameObject kitchenPanel;
    [SerializeField] private GameObject foodWarehousePanel;
    [SerializeField] private GameObject exploreMapPanel;
    [SerializeField] private GameObject UIPanel;

    [Header("공용 멤 창고 팝업 (Left 계열 패널 전용 - HDY 요청)")]
    [Tooltip("(멤) Crafting/Production/CampFire(Left_*_Panel) 중 하나라도 열려 있을 때만 함께 보여야 하는 공용 멤 선택 UI(P_MemUI). Center 계열(Ranch/Generator/Transport/Kitchen)은 자체 Mem_Slot_Area를 쓰므로 관여하지 않는다.")]
    [SerializeField] private GameObject memStoragePanel;


    [Header("시설 UI 컴포넌트")]
    [SerializeField] private CraftingPanelUI craftingPanelUI;
    [SerializeField] private ProductionPanelUI productionPanelUI;
    [SerializeField] private RanchPanelUI ranchPanelUI;
    [SerializeField] private GeneratorPanelUI generatorPanelUI;
    [SerializeField] private TransportPanelUI transportPanelUI;
    [SerializeField] private CampFirePanelUI campFirePanelUI;
    [SerializeField] private KitchenPanelUI kitchenPanelUI;

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
    public bool IsKitchenPanelActive => kitchenPanel != null && kitchenPanel.activeSelf;

private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        RegisterFacilityPanels();
        CloseAllPanels();
        RefreshMemStoragePanelVisibility();
    }

/// <summary>
    /// [PanelManager 흡수] 시설 패널들을 SceneUIManager의 관리 대상으로 정식 등록한다.
    /// 이제 열기/닫기/배타적 처리/ESC는 모두 SceneUIManager가 담당하고, PanelManager는 각 패널의
    /// 데이터 바인딩(OpenPanel)과 공통 부가효과(UIPanel/배치버튼그룹/카메라)만 담당한다.
    /// </summary>
    private void RegisterFacilityPanels()
    {
        RegisterPanel("Crafting", craftingPanel);
        RegisterPanel("Production", productionPanel);
        RegisterPanel("Ranch", ranchPanel);
        RegisterPanel("Generator", generatorPanel);
        RegisterPanel("Transport", transportPanel);
        RegisterPanel("CampFire", campFirePanel);
        RegisterPanel("Kitchen", kitchenPanel);
        RegisterPanel("FoodWarehouse", foodWarehousePanel);
        RegisterPanel("ExploreMap", exploreMapPanel);
    }

    private static void RegisterPanel(string id, GameObject panel)
    {
        if (panel == null) return;
        SceneUIManager.TryRegisterManagedUI(id, panel);
    }

    private void OnEnable()
    {
        if (SceneUIManager.Instance != null)
        {
            SceneUIManager.Instance.ManagedUIPanelOpened += HandleManagedUIPanelOpened;
            SceneUIManager.Instance.ManagedUIPanelClosed += HandleManagedUIPanelClosed;
            SceneUIManager.Instance.ManagedUIVisibilityChanged += HandleManagedUIVisibilityChanged;
        }
    }

    private void OnDisable()
    {
        if (SceneUIManager.Instance != null)
        {
            SceneUIManager.Instance.ManagedUIPanelOpened -= HandleManagedUIPanelOpened;
            SceneUIManager.Instance.ManagedUIPanelClosed -= HandleManagedUIPanelClosed;
            SceneUIManager.Instance.ManagedUIVisibilityChanged -= HandleManagedUIVisibilityChanged;
        }
    }

    /// <summary>
    /// (멤) [PanelManager 흡수] 예전 NotifyHUDPanelOpened()가 하던 일 중 "HUD 패널이 열릴 때마다 탐험 UI
    /// 정렬 필터를 새로고침"하는 부분만 남긴 것. 이제는 HUD든 시설 패널이든 SceneUIManager에 등록된
    /// 어떤 UI가 열려도 동일하게 호출된다.
    /// </summary>
private void HandleManagedUIPanelOpened(string id)
    {
        var activeExplorationUI = FindFirstObjectByType<HDY.UI.ExplorationPanelUI>();
        if (activeExplorationUI != null && activeExplorationUI.gameObject.activeInHierarchy)
        {
            SortButtonManagement.Instance?.UpdateSortFilters(activeExplorationUI.gameObject);
        }

        RefreshMemStoragePanelVisibility();
    }

    /// <summary>
    /// (멤) SceneUIManager가 시설 패널 하나를 닫아줌을 때(ESC, 다른 UI로 전환 등 어떤 경로로 닫힌이든)
    /// 그 패널의 내부 상태(ClosePanel)와 공용 배경(UIPanel)을 정리한다.
    /// </summary>
private void HandleManagedUIPanelClosed(string id)
    {
        switch (id)
        {
            case "Crafting": craftingPanelUI?.ClosePanel(); break;
            case "Production": productionPanelUI?.ClosePanel(); break;
            case "Ranch": ranchPanelUI?.ClosePanel(); break;
            case "Generator": generatorPanelUI?.ClosePanel(); break;
            case "Transport": transportPanelUI?.ClosePanel(); break;
            case "CampFire": campFirePanelUI?.ClosePanel(); break;
            case "Kitchen": kitchenPanelUI?.ClosePanel(); break;
            default:
                RefreshMemStoragePanelVisibility();
                return;
        }

        if (UIPanel != null) UIPanel.SetActive(false);
        RefreshMemStoragePanelVisibility();
    }

    /// <summary>
    /// (멤) "열려 있는 Managed UI가 하나라도 있는가"에 맞춰 공통 배치 버튼그룹과 카메라 컨트롤러를 갱신한다.
    /// 예전에는 이 토글을 Open*Panel/NotifyHUDPanelOpened/CloseAllPanels 전부가 각자 따로 호출했다.
    /// </summary>
    private void HandleManagedUIVisibilityChanged(bool anyManagedUIOpen)
    {
        SetCommonGroupActive(anyManagedUIOpen);
        SetCameraControllersEnabled(!anyManagedUIOpen);
    }

/// <summary>
    /// (멤) HDY 요청: Crafting/Production/CampFire(Left_*_Panel) 중 하나라도 열려 있으면(activeSelf)
    /// 공용 멤 선택 UI(P_MemUI)를 함께 켜고, 셋 다 닫혀 있으면 함께 끈다. 매번 현재 activeSelf 상태를
    /// 그대로 다시 읽어서 결정하므로(증분 토글이 아님), 여러 Left 패널이 같은 프레임에 열림/닫힘을
    /// 주고받아도(배타적 전환) 항상 정확한 최종 상태로 수렴한다. Awake에서도 호출해서 씬 시작 시
    /// 실수로 켜진 채 저장된 경우까지 강제로 꺼준다.
    /// </summary>
    private void RefreshMemStoragePanelVisibility()
    {
        if (memStoragePanel == null) return;

        bool anyLeftPanelOpen = (craftingPanel != null && craftingPanel.activeSelf)
            || (productionPanel != null && productionPanel.activeSelf)
            || (campFirePanel != null && campFirePanel.activeSelf);

        memStoragePanel.SetActive(anyLeftPanelOpen);
    }









public void OpenCraftingPanel(ProductionCraftRuntime facility)
    {
        if (facility == null || craftingPanel == null || craftingPanelUI == null) return;

        // (멤) UIPanel(공용 배경)을 먼저 켜야 한다 - 시설 패널들이 이 UIPanel의 자식이라, TryOpenManagedUI를
        // 먼저 부르면 부모가 아직 꺼진 상태라 activeInHierarchy가 false로 판정되어 ManagedUIPanelOpened
        // 이벤트가 발행되지 않는 버그가 있었다(RefreshMemStoragePanelVisibility 등이 스킵됨 - P_MemUI가
        // 한 번 꺼지면 다시 안 켜지는 문제의 원인).
        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Crafting");
        // (멤) 안전망 - 다른 시설 패널에서 이 패널로 곧바로 전환될 때, 위 TryOpenManagedUI 내부에서
        // 이전 패널을 닫는 과정(HandleManagedUIPanelClosed)이 공용 UIPanel을 순간적으로 다시 꺼버릴 수 있다.
        // 그 시점엔 이 패널이 아직 활성화되기 전이라 SceneUIManager의 열림 판정(activeInHierarchy)이 실패해서
        // '열림' 이벤트 자체가 발행되지 않는 경우가 있었다(P_MemUI가 한 번 꺼지면 다시 안 켜지는 문제의 원인).
        // 그 이벤트에 의존하지 않고 여기서 직접 최종 상태를 다시 강제한다.
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        craftingPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenProductionPanel(ProductionFacilityRuntime facility)
    {
        if (facility == null || productionPanel == null || productionPanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Production");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        productionPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenRanchPanel(RanchFacilityRuntime facility)
    {
        if (facility == null || ranchPanel == null || ranchPanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Ranch");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        ranchPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenTransportPanel(TransportRuntime facility)
    {
        if (facility == null || transportPanel == null || transportPanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Transport");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        transportPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenGeneratorPanel(GeneratorRuntime facility)
    {
        if (facility == null || generatorPanel == null || generatorPanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Generator");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        generatorPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenCampFirePanel(CampFireRuntime facility)
    {
        if (facility == null || campFirePanel == null || campFirePanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("CampFire");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        campFirePanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenKitchenPanel(KitchenRuntime facility)
    {
        if (facility == null || kitchenPanel == null || kitchenPanelUI == null) return;

        if (UIPanel != null) UIPanel.SetActive(true);
        SceneUIManager.TryOpenManagedUI("Kitchen");
        if (UIPanel != null) UIPanel.SetActive(true);
        RefreshMemStoragePanelVisibility();
        kitchenPanelUI.OpenPanel(facility);
        SortButtonManagement.Instance?.UpdateSortFilters(facility.gameObject);
    }

public void OpenFoodWareHousePanel()
    {
        if (foodWarehousePanel == null) return;

        SceneUIManager.TryOpenManagedUI("FoodWarehouse");
    }

public void OpenExploreMapPanel()
    {
        if (exploreMapPanel == null) return;

        SceneUIManager.TryOpenManagedUI("ExploreMap");

        if (WayPointManager.Instance != null)
        {
            WayPointManager.Instance.OpenTravelMap();
        }
    }



public void CloseAllPanels()
    {
        // (멤) 예전에는 여기서 모든 패널을 일일이 SetActive(false)+ClosePanel()했다. 이제는 모두
        // SceneUIManager에 등록되어 있으므로, 열려 있는 Managed UI를 전부 닫아달라고 요청하면
        // HandleManagedUIPanelClosed/HandleManagedUIVisibilityChanged가 나머지(ClosePanel 호출, UIPanel/
        // placeButtonGroup/카메라 컨트롤러 복구)를 자동으로 처리한다.
        SceneUIManager.Instance?.CloseManagedUIObjects();
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