using UnityEngine;

public class PanelManager : MonoBehaviour
{
    public static PanelManager Instance { get; private set; }

    [Header("시설별 Panel GameObject")]
    [SerializeField] private GameObject craftingPanel;
    [SerializeField] private GameObject productionPanel;
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

        CloseAllPanels(); // 다른 패널은 정리

        if (craftingPanel != null && craftingPanelUI != null)
        {
            SetCommonGroupActive(true);         
            SetCameraControllersEnabled(false); 

            UIPanel.SetActive(true);
            craftingPanel.SetActive(true); 
            craftingPanelUI.OpenPanel(facility); 
        }
    }

    /// <summary>
    /// 생산 패널 활성화
    /// </summary>
    public void OpenProductionPanel(ProductionFacilityRuntime facility)
    {
        if (facility == null) return;

        CloseAllPanels(); // 다른 패널은 정리

        if (productionPanel != null && productionPanelUI != null)
        {
            SetCommonGroupActive(true);
            SetCameraControllersEnabled(false);

            UIPanel.SetActive(true);
            productionPanel.SetActive(true); 
            productionPanelUI.OpenPanel(facility); 
        }
    }

    /// <summary>
    /// 영지관련 패널과 Close버튼 닫기 및 Place버튼 활성화
    /// </summary>
    public void CloseAllPanels()
    {
        if (craftingPanelUI != null) craftingPanelUI.ClosePanel();
        if (productionPanelUI != null) productionPanelUI.ClosePanel();
        if(UIPanel != null) UIPanel.SetActive(false);

        if (craftingPanel != null) craftingPanel.SetActive(false);
        if (productionPanel != null) productionPanel.SetActive(false);

        SetCommonGroupActive(false);
        SetCameraControllersEnabled(true);
    }

    private bool IsAnyPanelOpen()
    {
        bool isCraftActive = craftingPanel != null && craftingPanel.activeSelf;
        bool isProductActive = productionPanel != null && productionPanel.activeSelf;
        return isCraftActive || isProductActive;
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
}