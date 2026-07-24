using HDY.Capture;
using MemSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RanchPanelUI : MonoBehaviour
{
    public static RanchPanelUI Instance { get; private set; }

    [Header("상단 시설 정보 및 레벨업")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUpBtn; // 🌟 레벨업 버튼 추가

    [Header("1대1 매칭 슬롯 배열 (5개 고정)")]
    [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[5];
    [SerializeField] private RanchProductionSlotUI[] productionSlots = new RanchProductionSlotUI[5];

    [Header("버튼 연동 (Bottom/Get)")]
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

        if (levelUpBtn != null)
        {
            levelUpBtn.onClick.AddListener(OnClickLevelUp); // 🌟 레벨업 버튼 이벤트 연동
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

        for (int i = 0; i < productionSlots.Length; i++)
        {
            if (i < targetFacility.Slots.Count && productionSlots[i] != null)
            {
                productionSlots[i].UpdateDynamicProgress(targetFacility.Slots[i]);
            }
        }

        if (collectAllBtn != null)
        {
            collectAllBtn.interactable = targetFacility.HasAnyCollectableItem();
        }
    }

    public void OpenPanel(RanchFacilityRuntime ranch)
    {
        if (ranch == null) return;
        targetFacility = ranch;

        RefreshStaticUI();
    }

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

            // 상단 멤 슬롯 UI 갱신 (해금 시 검은색 -> 흰색)
            if (i < memSlots.Length && memSlots[i] != null)
            {
                memSlots[i].RefreshStatus(isUnlocked, placedMem, placedEntry);
            }

            // 하단 생산 슬롯 UI 갱신 (해금 시 검은색 -> 흰색)
            if (i < productionSlots.Length && productionSlots[i] != null)
            {
                if (i < slots.Count)
                {
                    productionSlots[i].RefreshSlot(slots[i]);
                }
            }
        }

        if (collectAllBtn != null)
        {
            collectAllBtn.interactable = targetFacility.HasAnyCollectableItem();
        }
    }

    /// <summary>
    /// 🌟 [레벨업 버튼 클릭 핸들러]
    /// </summary>
    private void OnClickLevelUp()
    {
        if (targetFacility == null) return;

        targetFacility.LevelUp();
        RefreshStaticUI(); // UI 레벨 텍스트 및 해금 슬롯 상태 개편
    }

    public void TryDeployMemFromUI(int slotIndex, MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return;

        bool isSuccess = targetFacility.TryAddMemToSlot(slotIndex, targetMem, targetEntry);
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