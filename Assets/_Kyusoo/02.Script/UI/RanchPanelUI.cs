using HDY.Capture;
using HDY.Upgrade;
using MemSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class RanchPanelUI : MonoBehaviour
{
    public static RanchPanelUI Instance { get; private set; }

    [Header("상단 시설 정보 및 레벨업")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUpBtn;
    [SerializeField] private TextMeshProUGUI levelUpBtnText; // 레벨업 버튼 텍스트

    [Header("1대1 매칭 슬롯 배열 (5개 고정)")]
    [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[5];
    [SerializeField] private RanchProductionSlotUI[] productionSlots = new RanchProductionSlotUI[5];

    [Header("하단 상태 및 수령 버튼")]
    [SerializeField] private Button collectAllBtn;
    [SerializeField] private TextMeshProUGUI overallStatusText;

    public RanchFacilityRuntime TargetFacility => targetFacility;
    private RanchFacilityRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (collectAllBtn != null) collectAllBtn.onClick.AddListener(OnClickCollectAll);
        if (levelUpBtn != null) levelUpBtn.onClick.AddListener(OnClickLevelUp);

        InitializeSlotIndexes();
    }

    private void OnDisable()
    {
        StopDotsAnimation();
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

        UpdateOverallStatusUI();
    }

    private void UpdateOverallStatusUI()
    {
        if (targetFacility == null) return;

        bool isStarving = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        if (isStarving)
        {
            StopDotsAnimation();
            if (overallStatusText != null)
            {
                overallStatusText.color = Color.red;
                overallStatusText.text = "식량이 부족합니다";
            }
        }
        else if (targetFacility.isProducing)
        {
            StartDotsAnimation();
        }
        else
        {
            StopDotsAnimation();
            if (overallStatusText != null)
            {
                overallStatusText.color = Color.white;
                overallStatusText.text = "생산 대기 중";
            }
        }
    }

    private void StartDotsAnimation()
    {
        if (isAnimatingDots) return;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (overallStatusText != null) overallStatusText.color = Color.white;

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetStatusText("생산중 ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText("생산중 .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText("생산중 ..."); })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
    }

    private void SetStatusText(string text)
    {
        if (overallStatusText != null) overallStatusText.text = text;
    }

    private void StopDotsAnimation()
    {
        if (!isAnimatingDots && dotsSequence == null) return;

        isAnimatingDots = false;
        if (dotsSequence != null)
        {
            dotsSequence.Kill();
            dotsSequence = null;
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

            if (i < memSlots.Length && memSlots[i] != null)
            {
                memSlots[i].RefreshStatus(isUnlocked, placedMem, placedEntry);
            }

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

        // 레벨업 버튼 Max 상태 처리 (5레벨 도달 시)
        if (levelUpBtn != null)
        {
            bool isMax = targetFacility.currentLevel >= 5;
            levelUpBtn.interactable = !isMax;

            if (levelUpBtnText != null)
            {
                levelUpBtnText.text = isMax ? "Lv.Max" : "레벨업";
            }
        }

        UpdateOverallStatusUI();
    }

    private void OnClickLevelUp()
    {
        if (targetFacility == null) return;

        if (targetFacility.TryGetComponent<FacilityUpgrade>(out var upgradeAdapter))
        {
            if (UpgradePopupUI.Instance != null)
            {
                UpgradePopupUI.Instance.Show(upgradeAdapter);
            }
        }
        else
        {
            Debug.LogWarning($"[PanelUI] {targetFacility.name} 건물 프리팹에 FacilityUpgrade 컴포넌트가 부착되어 있지 않습니다.");
        }
    }

    public bool TryDeployMemFromUI(int slotIndex, MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetMem == null || targetEntry == null) return false;

        bool isSuccess = targetFacility.TryAddMemToSlot(slotIndex, targetMem, targetEntry);
        if (isSuccess) RefreshStaticUI();
        return isSuccess;
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
        StopDotsAnimation();
        targetFacility = null;
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
    }
}