using HDY.Capture;
using HDY.Item;
using MemSystem.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

public class ProductionPanelUI : MonoBehaviour
{
    public static ProductionPanelUI Instance { get; private set; }

    [Header("중앙 패널 - Top")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUp;

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
    }

    private void Update()
    {
        if (targetFacility == null) return;

        // 🌟 [수정]: string ID 검사
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
        if (targetFacility == null)
        {
            Debug.LogError("<color=red>[ProductionPanelUI]</color> RefreshStaticUI를 실행하려 했으나 targetFacility가 null입니다.");
            return;
        }
        Debug.Log($"<color=lime>[ProductionPanelUI]</color> RefreshStaticUI 수신 성공. 대상 시설: {targetFacility.buildingData.buildingName}");

        bodyNameTextModify();

        int maxCapacity = ProductionCalculator.GetMaxMemCount(targetFacility.currentLevel);
        Debug.Log($"[ProductionPanelUI] 현재 시설 최대 배치 수용량: {maxCapacity}마리 / 현재 DeployedMems 수: {targetFacility.DeployedMems.Count}");

        for (int i = 0; i < memSlotImages.Length; i++)
        {
            if (memSlotImages[i] == null)
            {
                Debug.LogWarning($"[ProductionPanelUI] 인스펙터의 memSlotImages[{i}] 슬롯 컴포넌트 참조가 비어있습니다(Null).");
                continue;
            }

            bool isUnlocked = (i < maxCapacity);
            MemData placedMemData = null;
            CapturedMemEntry placedEntryData = null;

            if (isUnlocked)
            {
                if (i < targetFacility.DeployedMems.Count) placedMemData = targetFacility.DeployedMems[i];
                if (i < targetFacility.DeployedMemEntries.Count) placedEntryData = targetFacility.DeployedMemEntries[i];
            }

            Debug.Log($"[ProductionPanelUI -> MemSlotUI] 슬롯 인덱스 [{i}] 갱신 시도 - Unlocked: {isUnlocked}, PlacedMem: {(placedMemData != null ? placedMemData.memName : "Null(비어있음)")}");
            memSlotImages[i].RefreshStatus(isUnlocked, placedMemData, placedEntryData);
        }
    }

    private void bodyNameTextModify()
    {
        buildingName.text = targetFacility.buildingData.buildingName;
        buildingLevel.text = $"Lv {targetFacility.currentLevel}";
    }

    /// <summary>
    /// 고정 매칭된 아이템의 이미지, 이름을 노출하는 함수
    /// </summary>
    private void DisplayProduction()
    {
        if (targetFacility == null) return;

        if (defaultMode != null) defaultMode.SetActive(true);

        // 🌟 [수정]: string 아이템 ID 기반으로 ItemCatalogManager에서 아이템 정보 검색
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

    /// <summary>
    /// ItemCatalogManager 전용 탐색 헬퍼 메서드
    /// </summary>
    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        ItemData targetItem = ItemCatalogManager.Instance.FindItemData(itemId);
        if (targetItem == null)
        {
            Debug.LogError($"[ItemCatalogManager] 카탈로그에서 아이템 ID '{itemId}'에 해당하는 ItemData를 찾을 수 없습니다.");
        }

        return targetItem;
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