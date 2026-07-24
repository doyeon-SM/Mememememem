using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MemSystem.Data;
using HDY.Capture;
using HDY.UI;
using HDY.Mem;
using TMPro;

public class MemSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
{
    [Header("슬롯 UI 요소 참조")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image stat;
    [SerializeField] private TextMeshProUGUI statText;
    [SerializeField] private Button slotButton;

    [Header("스탯 아이콘 매핑")]
    [SerializeField] private Sprite craftingStatIcon;
    [SerializeField] private Sprite loggingStatIcon;
    [SerializeField] private Sprite miningStatIcon;
    [SerializeField] private Sprite transportStatIcon;
    [SerializeField] private Sprite farmingStatIcon;

    public int SlotIndex { get; private set; }
    private bool isUnlocked = false;

    private MemData currentPlacedMem = null;
    private CapturedMemEntry currentPlacedEntry = null;

    public void InitializeSlot(int index)
    {
        SlotIndex = index;
        if (slotButton == null) slotButton = GetComponent<Button>();

        if (TryGetComponent<MemSlotUI>(out var duplicateComp))
        {
            if (duplicateComp != this) Destroy(duplicateComp);
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnClickSlot);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            ExecuteSlotReleaseProcess();
        }
    }

    private void ExecuteSlotReleaseProcess()
    {
        if (currentPlacedMem == null) return;

        MonoBehaviour activePanel = GetCurrentActivePanel();

        if (activePanel is ProductionPanelUI prodPanel)
        {
            prodPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
        else if (activePanel is CraftingPanelUI craftPanel)
        {
            craftPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
        else if (activePanel is RanchPanelUI ranchPanel)
        {
            ranchPanel.TryRemoveMemFromUI(currentPlacedMem);
        }
    }

    public void RefreshStatus(bool unlocked, MemData memData, CapturedMemEntry entryData)
    {
        isUnlocked = unlocked;
        currentPlacedMem = memData;
        currentPlacedEntry = entryData;

        if (slotButton != null) slotButton.interactable = isUnlocked;

        if (!isUnlocked)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.color = Color.black;
            }
            ApplyStatDisplay(null, string.Empty);
        }
        else
        {
            if (iconImage != null)
            {
                if (currentPlacedMem != null)
                {
                    Sprite sprite = (MemIconRenderer.Instance != null)
                            ? MemIconRenderer.Instance.GetIcon(currentPlacedMem.memId)
                            : null;

                    iconImage.sprite = sprite;
                    iconImage.color = Color.white;
                    iconImage.gameObject.SetActive(sprite != null);

                    UpdateStatDisplay();
                }
                else
                {
                    iconImage.sprite = null;
                    iconImage.color = Color.white;
                    ApplyStatDisplay(null, string.Empty);
                }
            }
        }
    }

    private void UpdateStatDisplay()
    {
        if (currentPlacedMem == null)
        {
            ApplyStatDisplay(null, string.Empty);
            return;
        }

        BuildingType? buildingType = null;
        MonoBehaviour activePanel = GetCurrentActivePanel();

        if (activePanel is ProductionPanelUI prodPanel && prodPanel.TargetFacility != null && prodPanel.TargetFacility.buildingData != null)
        {
            buildingType = prodPanel.TargetFacility.buildingData.buildingType;
        }
        else if (activePanel is CraftingPanelUI craftPanel && craftPanel.TargetFacility != null && craftPanel.TargetFacility.buildingData != null)
        {
            buildingType = craftPanel.TargetFacility.buildingData.buildingType;
        }
        else if (activePanel is RanchPanelUI ranchPanel && ranchPanel.TargetFacility != null && ranchPanel.TargetFacility.buildingData != null)
        {
            buildingType = ranchPanel.TargetFacility.buildingData.buildingType;
        }

        if (buildingType.HasValue)
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingType.Value);
            int statValue = currentPlacedMem.productionStats.GetStat(requiredStat);
            Sprite statIcon = GetStatIcon(requiredStat);

            ApplyStatDisplay(statIcon, statValue.ToString());
        }
        else
        {
            ApplyStatDisplay(null, string.Empty);
        }
    }

    private Sprite GetStatIcon(ProductionStatType statType)
    {
        switch (statType)
        {
            case ProductionStatType.Crafting: return craftingStatIcon;
            case ProductionStatType.Logging: return loggingStatIcon;
            case ProductionStatType.Mining: return miningStatIcon;
            case ProductionStatType.Transport: return transportStatIcon;
            case ProductionStatType.Farming: return farmingStatIcon;
            default: return null;
        }
    }

    private void ApplyStatDisplay(Sprite statSprite, string textValue)
    {
        if (stat != null)
        {
            stat.sprite = statSprite;
            stat.color = Color.white;
            stat.gameObject.SetActive(statSprite != null);
        }

        if (statText != null)
        {
            statText.text = textValue;
            statText.gameObject.SetActive(!string.IsNullOrEmpty(textValue));
        }
    }

    private MonoBehaviour GetCurrentActivePanel()
    {
        if (PanelManager.Instance != null)
        {
            if (PanelManager.Instance.IsCraftingPanelActive && CraftingPanelUI.Instance != null)
            {
                return CraftingPanelUI.Instance;
            }

            if (PanelManager.Instance.IsProductionPanelActive && ProductionPanelUI.Instance != null)
            {
                return ProductionPanelUI.Instance;
            }

            if (PanelManager.Instance.IsRanchPanelActive && RanchPanelUI.Instance != null)
            {
                return RanchPanelUI.Instance;
            }
        }

        return null;
    }

    private void OnClickSlot()
    {
        ExecuteSlotReleaseProcess();
    }

    public void OnDrop(PointerEventData eventData)
    {
        MonoBehaviour activePanel = GetCurrentActivePanel();
        if (activePanel == null) return;

        // 패널로부터 부착된 Runtime 컴포넌트를 직접 추출
        Object targetRuntime = GetRuntimeFromPanel(activePanel);

        // 목장 패널이고 슬롯이 잠겨있는지 확인
        if (targetRuntime is RanchFacilityRuntime && !isUnlocked)
        {
            Debug.LogWarning($"[MemSlotUI] 잠겨있는 목장 슬롯입니다. (인덱스: {SlotIndex})");
            return;
        }

        if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent<HDY.UI.MemSlotUI>(out HDY.UI.MemSlotUI draggedSlot))
        {
            var type = typeof(HDY.UI.MemSlotUI);
            var fieldEntry = type.GetField("cachedEntry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldData = type.GetField("cachedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldEntry != null)
            {
                CapturedMemEntry warehouseEntry = fieldEntry.GetValue(draggedSlot) as CapturedMemEntry;
                MemData warehouseData = fieldData != null ? fieldData.GetValue(draggedSlot) as MemData : null;

                if (warehouseEntry != null)
                {
                    if ((warehouseData == null || string.IsNullOrEmpty(warehouseData.memId)) && MemCatalogManager.Instance != null)
                    {
                        warehouseData = MemCatalogManager.Instance.FindMemData(warehouseEntry.MemId);
                    }

                    bool isDeployedSuccess = false;

                    // 🌟 [핵심]: UI 패널 클래스가 아니라, 부착된 Runtime 타입에 따라 직접 분기 및 처리
                    if (targetRuntime is ProductionFacilityRuntime prodRuntime)
                    {
                        isDeployedSuccess = prodRuntime.TryAddMem(warehouseData, warehouseEntry);
                        if (isDeployedSuccess && activePanel is ProductionPanelUI prodPanel) prodPanel.RefreshStaticUI();
                    }
                    else if (targetRuntime is ProductionCraftRuntime craftRuntime)
                    {
                        isDeployedSuccess = craftRuntime.TryAddMem(warehouseData, warehouseEntry);
                        if (isDeployedSuccess && activePanel is CraftingPanelUI craftPanel) craftPanel.RefreshStaticUI();
                    }
                    else if (targetRuntime is RanchFacilityRuntime ranchRuntime)
                    {
                        isDeployedSuccess = ranchRuntime.TryAddMemToSlot(SlotIndex, warehouseData, warehouseEntry);
                        if (isDeployedSuccess && activePanel is RanchPanelUI ranchPanel) ranchPanel.RefreshStaticUI();
                    }

                    if (isDeployedSuccess)
                    {
                        Debug.Log($"<color=lime>[MemSlotUI]</color> 런타임 직접 배치 성공: {warehouseEntry.MemId}");
                    }
                    else
                    {
                        Debug.LogWarning($"<color=orange>[MemSlotUI]</color> 런타임 배치 조건 불충족으로 취소됨");
                    }
                }
            }
        }
    }

    // 패널 인스턴스로부터 런타임 컴포넌트를 안전하게 가져오는 헬퍼 메서드
    private Object GetRuntimeFromPanel(MonoBehaviour panel)
    {
        if (panel is ProductionPanelUI prod) return prod.TargetFacility;
        if (panel is CraftingPanelUI craft) return craft.TargetFacility;
        if (panel is RanchPanelUI ranch) return ranch.TargetFacility;
        Debug.Log($"[Panel 확인하기 {panel}]");
        return null;
    }

}