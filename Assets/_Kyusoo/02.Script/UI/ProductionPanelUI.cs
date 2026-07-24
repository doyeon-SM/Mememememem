using HDY.Capture;
using HDY.Item;
using MemSystem.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProductionPanelUI : MonoBehaviour
{
    public static ProductionPanelUI Instance { get; private set; }

    [Header("중앙 패널 - Top")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUp; // 🌟 레벨업 버튼

    [Header("중앙 패널 - Center")]
    [SerializeField] private MemSlotUI[] memSlotImages = new MemSlotUI[5];
    [SerializeField] private GameObject defaultMode;
    [SerializeField] private GameObject creatingMode;
    [SerializeField] private Image creatingItem;
    [SerializeField] private TextMeshProUGUI completeCreateCount;
    [SerializeField] private Button diamondBGBtn;

    [Header("중앙 패널 - Bottom")]
    [SerializeField] private TextMeshProUGUI creatingItemName;
    [SerializeField] private TextMeshProUGUI productionSpeed;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;

    [Header("제작할 아이템 관련 정보: 프리팹, 생성 위치, SO리스트 전체")]
    [SerializeField] private GameObject craftingSlotPrefab;
    [SerializeField] private Transform craftingSlotParent;

    public ProductionFacilityRuntime TargetFacility => targetFacility;
    private ProductionFacilityRuntime targetFacility;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (diamondBGBtn != null)
        {
            diamondBGBtn.onClick.AddListener(OnClickCollectReward);
        }

        if (levelUp != null)
        {
            levelUp.onClick.AddListener(OnClickLevelUp); // 🌟 레벨업 버튼 이벤트 연동
        }
    }

    private void Update()
    {
        if (targetFacility == null) return;

        if (!string.IsNullOrEmpty(targetFacility.craftingItem) && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";

            if (productionSpeed != null) productionSpeed.text = $"생산속도: {targetFacility.totalRequiredTime:F1}초(개당)";
        }
        else
        {
            if (progressBar != null) progressBar.value = 0f;
            if (durationText != null) durationText.text = "0%";
            if (productionSpeed != null) productionSpeed.text = "생산속도: - 초(개당)";
        }

        UpdateStorageText();
    }

    public void OpenPanel(ProductionFacilityRuntime facility)
    {
        if (facility == null) return;
        targetFacility = facility;

        RefreshStaticUI();
        DisplayProduction();
    }

    public void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        bodyNameTextModify();

        // 🌟 레벨에 따른 슬롯 해금 계산
        int maxCapacity = ProductionCalculator.GetMaxMemCount(targetFacility.currentLevel);

        for (int i = 0; i < memSlotImages.Length; i++)
        {
            if (memSlotImages[i] == null) continue;

            bool isUnlocked = (i < maxCapacity);
            MemData placedMemData = null;
            CapturedMemEntry placedEntryData = null;

            if (isUnlocked)
            {
                if (i < targetFacility.DeployedMems.Count) placedMemData = targetFacility.DeployedMems[i];
                if (i < targetFacility.DeployedMemEntries.Count) placedEntryData = targetFacility.DeployedMemEntries[i];
            }

            memSlotImages[i].RefreshStatus(isUnlocked, placedMemData, placedEntryData);
        }
    }

    /// <summary>
    /// 🌟 [레벨업 버튼 클릭 핸들러]
    /// </summary>
    private void OnClickLevelUp()
    {
        if (targetFacility == null) return;

        targetFacility.LevelUp();
        RefreshStaticUI(); // UI 레벨 텍스트 및 슬롯 해금 상태 즉시 리프레시
    }

    private void bodyNameTextModify()
    {
        if (buildingName != null) buildingName.text = targetFacility.buildingData.buildingName;
        if (buildingLevel != null) buildingLevel.text = $"Lv {targetFacility.currentLevel}";
    }

    private void DisplayProduction()
    {
        if (targetFacility == null) return;

        if (defaultMode != null) defaultMode.SetActive(true);

        if (!string.IsNullOrEmpty(targetFacility.craftingItem))
        {
            ItemData targetItemData = FindItemDataInCatalog(targetFacility.craftingItem);

            if (targetItemData != null)
            {
                if (creatingItem != null)
                {
                    creatingItem.sprite = targetItemData.ItemIcon;
                    creatingItem.gameObject.SetActive(true);
                }
                if (creatingItemName != null)
                {
                    creatingItemName.text = targetItemData.ItemName;
                }
            }
            else
            {
                if (creatingItem != null) creatingItem.gameObject.SetActive(false);
                if (creatingItemName != null) creatingItemName.text = "아이템 정보 없음";
            }
        }
        else
        {
            if (creatingItem != null) creatingItem.gameObject.SetActive(false);
            if (creatingItemName != null) creatingItemName.text = "생산 품목 없음";
        }

        UpdateStorageText();
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    public void TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);

        if (isSuccess)
        {
            RefreshStaticUI();
        }
    }

    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);

        RefreshStaticUI();
    }

    private void UpdateStorageText()
    {
        if (targetFacility == null || completeCreateCount == null) return;

        completeCreateCount.text = targetFacility.currentStorageCount.ToString();
    }

    private void OnClickCollectReward()
    {
        if (targetFacility == null) return;

        targetFacility.StoredItems();

        UpdateStorageText();
    }

    public void ClosePanel()
    {
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        DisplayProduction();
        UpdateStorageText();
    }
}