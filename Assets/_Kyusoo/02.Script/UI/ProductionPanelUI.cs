using HDY.Capture;
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

    //[Header("우측 패널 - 멤 생산 Stat 아이콘 레퍼런스 가방")]
    //[SerializeField] private Sprite craftingStatIcon;
    //[SerializeField] private Sprite loggingStatIcon;
    //[SerializeField] private Sprite miningStatIcon;
    //[SerializeField] private Sprite transportStatIcon;
    //[SerializeField] private Sprite farmingStatIcon;

    // 현재 UI 창이 조준하고 있는 타겟 시설 스크립트 캐싱
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

    /// <summary>
    /// 아이템 생산시 슬라이더 변화처리
    /// </summary>
    private void Update()
    {
        if (targetFacility == null) return;

        if (targetFacility.isProducing && targetFacility.totalRequiredTime > 0f)
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

            if (productionSpeed != null)
            {
                productionSpeed.text = (targetFacility.craftingItem != null)
                    ? $"생산속도: {targetFacility.baseProductionTime:F1}초(개당)"
                    : "생산속도: - 초(개당)";
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

        RefreshStaticUI();
        DisplayProduction();
    }

    /// <summary>
    /// 패널이 열릴 때 시설의 이름, 레벨, 멤 슬롯 상태 등의 정보 받아오기
    /// </summary>
    public void RefreshStaticUI()
    {
        if (targetFacility == null)
        {
            Debug.LogError("<color=red>[ProductionPanelUI]</color> RefreshStaticUI를 실행하려 했으나 targetFacility가 null입니다.");
            return;
        }
        Debug.Log($"<color=lime>[ProductionPanelUI]</color> RefreshStaticUI 수신 성공. 대상 시설: {targetFacility.buildingData.buildingName}");

        buildingName.text = targetFacility.buildingData.buildingName;
        buildingLevel.text = $"Lv {targetFacility.currentLevel}";

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

    /// <summary>
    /// 코드 변경. 고정 매칭된 아이템의 이미지, 이름을 노출하는 함수
    /// </summary>
    private void DisplayProduction()
    {
        if (targetFacility == null) return;

        if (defaultMode != null) defaultMode.SetActive(true);

        if (targetFacility.craftingItem != null)
        {
            if (creatingItem != null)
            {
                creatingItem.sprite = targetFacility.craftingItem.ItemIcon;
                creatingItem.gameObject.SetActive(true);
            }
            if (creatingItemName != null)
            {
                creatingItemName.text = targetFacility.craftingItem.ItemName;
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
    /// 드롭 이벤트를 수신하여 멤 배치 및 펫정보 수신
    /// </summary>
    public void TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);

        if (isSuccess)
        {
            RefreshStaticUI();
        }
    }

    /// <summary>
    /// 시설 내 슬롯 클릭 시 슬로 배치 해제 처리
    /// </summary>
    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);

        RefreshStaticUI();
    }

    /// <summary>
    /// 배치된 멤의 스탯값을 가져와서 적용하기
    /// </summary>
    //private void LoadAndCacheMemStats(MemData memData)
    //{
    //    if (memData == null) return;

    //    int craftLvl = memData.productionStats.crafting;
    //    int logLvl = memData.productionStats.logging;
    //    int mineLvl = memData.productionStats.mining;
    //    int transLvl = memData.productionStats.transport;
    //    int farmLvl = memData.productionStats.farming;

    //}

    /// <summary>
    /// 시설 내 저장된 수량 텍스트 업데이트
    /// </summary>
    private void UpdateStorageText()
    {
        if (targetFacility == null || completeCreateCount == null) return;

        completeCreateCount.text = targetFacility.currentStorageCount.ToString();
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
        targetFacility = null;
    }
}