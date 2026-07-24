using HDY.Capture;
using MemSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanchPanelUI : MonoBehaviour
{
    public static RanchPanelUI Instance { get; private set; }

    [Header("상단 시설 정보")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;

    [Header("1대1 매칭 슬롯 배열 (5개 고정)")]
    [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[5];
    [SerializeField] private RanchProductionSlotUI[] productionSlots = new RanchProductionSlotUI[5];

    [Header("버튼 연동")]
    [SerializeField] private Button collectAllBtn;

    public RanchFacilityRuntime TargetFacility => targetFacility;
    private RanchFacilityRuntime targetFacility;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (collectAllBtn != null)
        {
            collectAllBtn.onClick.AddListener(OnClickCollectAll);
        }

        InitializeSlotIndexes();
    }

    private void InitializeSlotIndexes()
    {
        for (int i = 0; i < memSlots.Length; i++)
        {
            if (memSlots[i] != null)
            {
                memSlots[i].InitializeSlot(i);
            }
        }
    }

    private void Update()
    {
        if (targetFacility == null) return;

        // 가동 중인 슬롯들의 진행도 및 수량 실시간 UI 업데이트
        for (int i = 0; i < productionSlots.Length; i++)
        {
            if (i < targetFacility.Slots.Count && productionSlots[i] != null)
            {
                productionSlots[i].UpdateDynamicProgress(targetFacility.Slots[i]);
            }
        }
    }

    /// <summary>
    /// 목장 선택 시 패널 오픈
    /// </summary>
    public void OpenPanel(RanchFacilityRuntime ranch)
    {
        if (ranch == null) return;
        targetFacility = ranch;

        RefreshStaticUI();
    }

    /// <summary>
    /// 정적 정보 및 1대1 매칭 슬롯 전체 Refresh
    /// </summary>
    public void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        if (buildingName != null && targetFacility.buildingData != null)
            buildingName.text = targetFacility.buildingData.buildingName;

        if (buildingLevel != null)
            buildingLevel.text = $"Lv {targetFacility.currentLevel}";

        var slots = targetFacility.Slots;

        for (int i = 0; i < 5; i++)
        {
            bool isUnlocked = (i < slots.Count) && slots[i].isUnlocked;
            MemData placedMem = isUnlocked ? slots[i].deployedMem : null;
            CapturedMemEntry placedEntry = isUnlocked ? slots[i].deployedMemEntry : null;

            // 1. 상단 멤 슬롯 UI 갱신
            if (i < memSlots.Length && memSlots[i] != null)
            {
                memSlots[i].RefreshStatus(isUnlocked, placedMem, placedEntry);
            }

            // 2. 하단 생산 슬롯 UI 갱신 (1대1 매칭)
            if (i < productionSlots.Length && productionSlots[i] != null)
            {
                if (i < slots.Count)
                {
                    productionSlots[i].RefreshSlot(slots[i]);
                }
            }
        }
    }

    /// <summary>
    /// UI에서 특정 인덱스 슬롯으로 드롭하여 멤 배치 시 호출
    /// </summary>
    public void TryDeployMemFromUI(int slotIndex, MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return;

        bool isSuccess = targetFacility.TryAddMemToSlot(slotIndex, targetMem, targetEntry);
        if (isSuccess)
        {
            RefreshStaticUI();
        }
    }

    /// <summary>
    /// UI에서 멤 제거 시 호출
    /// </summary>
    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);
        RefreshStaticUI();
    }

    /// <summary>
    /// [전체 수령] 버튼 클릭 시 동작
    /// </summary>
    private void OnClickCollectAll()
    {
        if (targetFacility == null) return;

        targetFacility.CollectAllItems();
        RefreshStaticUI();
    }

    public void ClosePanel()
    {
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
    }
}