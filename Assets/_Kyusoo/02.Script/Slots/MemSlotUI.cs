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
    [Header("슬롯 UI 요소 참조 (미리 배치될 프리팹의 컴포넌트들)")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image stat;
    [SerializeField] private TextMeshProUGUI statText;
    [SerializeField] private Button slotButton;

    [Header("스탯 아이콘 매핑 (시설 필요 스탯 표시용)")]
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

        if (slotButton != null)
        {
            slotButton.interactable = isUnlocked;
        }

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
                    Sprite sprite = (isUnlocked && currentPlacedMem != null && MemIconRenderer.Instance != null)
                            ? MemIconRenderer.Instance.GetIcon(currentPlacedMem.memId)
                            : null;
                    if (currentPlacedMem.modelPrefab != null)
                    {
                        iconImage.sprite = sprite;
                        iconImage.color = Color.white;
                        iconImage.gameObject.SetActive(sprite != null);
                    }

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

    /// <summary>
    /// 🌟 [수정]: 오브젝트 이름 대신 부모 계층 및 실제 활성화 상태 기반 패널 탐색
    /// </summary>
    private MonoBehaviour GetCurrentActivePanel()
    {
        var ranchInParent = GetComponentInParent<RanchPanelUI>();
        if (ranchInParent != null && ranchInParent.gameObject.activeInHierarchy) return ranchInParent;

        var prodInParent = GetComponentInParent<ProductionPanelUI>();
        if (prodInParent != null && prodInParent.gameObject.activeInHierarchy) return prodInParent;

        var craftInParent = GetComponentInParent<CraftingPanelUI>();
        if (craftInParent != null && craftInParent.gameObject.activeInHierarchy) return craftInParent;

        if (RanchPanelUI.Instance != null && RanchPanelUI.Instance.gameObject.activeInHierarchy) return RanchPanelUI.Instance;
        if (ProductionPanelUI.Instance != null && ProductionPanelUI.Instance.gameObject.activeInHierarchy) return ProductionPanelUI.Instance;
        if (CraftingPanelUI.Instance != null && CraftingPanelUI.Instance.gameObject.activeInHierarchy) return CraftingPanelUI.Instance;

        return null;
    }

    private void OnClickSlot()
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

    public void OnDrop(PointerEventData eventData)
    {
        if (!isUnlocked)
        {
            Debug.LogWarning($"시설 레벨 조건이 충족되지 않아 잠겨있는 슬롯 칸입니다. (인덱스: {SlotIndex})");
            return;
        }

        if (eventData.pointerDrag != null && eventData.pointerDrag.TryGetComponent<HDY.UI.MemSlotUI>(out HDY.UI.MemSlotUI draggedSlot))
        {
            var type = typeof(HDY.UI.MemSlotUI);
            var fieldEntry = type.GetField("cachedEntry", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var fieldData = type.GetField("cachedData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (fieldEntry != null && fieldData != null)
            {
                CapturedMemEntry warehouseEntry = fieldEntry.GetValue(draggedSlot) as CapturedMemEntry;
                MemData warehouseData = fieldData.GetValue(draggedSlot) as MemData;

                if (warehouseEntry != null && warehouseData != null)
                {
                    MonoBehaviour activePanel = GetCurrentActivePanel();

                    if (activePanel is ProductionPanelUI prodPanel)
                    {
                        prodPanel.TryDeployMemFromUI(warehouseData, warehouseEntry);
                    }
                    else if (activePanel is CraftingPanelUI craftPanel)
                    {
                        craftPanel.TryDeployMemFromUI(warehouseData, warehouseEntry);
                    }
                    else if (activePanel is RanchPanelUI ranchPanel)
                    {
                        ranchPanel.TryDeployMemFromUI(SlotIndex, warehouseData, warehouseEntry);
                    }

                    Debug.Log($"포획 멤 데이터 추출 및 배치 요청 완료: {warehouseData.memName}.");
                }
                else
                {
                    Debug.LogWarning("[OnDrop 경고] 슬롯에 정상적인 멤 데이터가 존재하지 않아 배치를 취소합니다.");
                }
            }
        }
    }
}