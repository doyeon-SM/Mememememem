using HDY.Capture;
using HDY.Upgrade;
using MemSystem.Data;
using System.Collections.Generic;
using System.Linq;
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
    [SerializeField] private TextMeshProUGUI levelUpBtnText;

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
    private string currentStatusPrefix = "";

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

    /// <summary>
    /// 해금된 슬롯 중 멤이 배치된 데이터가 1개라도 있는지 확인
    /// </summary>
    private bool HasAnyMemDeployed()
    {
        if (targetFacility == null || targetFacility.Slots == null) return false;
        return targetFacility.Slots.Any(s => s != null && s.isUnlocked && s.deployedMemEntry != null);
    }

    /// <summary>
    /// 배치된 모든 멤의 배고픔량이 0 이하(또는 IsStarving)인지 검사
    /// </summary>
    private bool IsAllDeployedMemsStarving()
    {
        if (targetFacility == null || targetFacility.Slots == null) return false;

        var deployedEntries = targetFacility.Slots
            .Where(s => s != null && s.isUnlocked && s.deployedMemEntry != null)
            .Select(s => s.deployedMemEntry)
            .ToList();

        if (deployedEntries.Count == 0) return false;

        return deployedEntries.All(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
    }

    private void Update()
    {
        if (targetFacility == null) return;

        // 1. 개별 슬롯 진행도 & WarningIcon 상태 실시간 전달
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

        // 🌟 2. ProductionPanelUI와 100% 동일한 분기 로직 적용
        bool isNoMem = !HasAnyMemDeployed();
        bool isAllStarving = !isNoMem && IsAllDeployedMemsStarving();
        int currentSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;

        // 2-1. 멤 미배치
        if (isNoMem)
        {
            StopDotsAnimation();
            if (overallStatusText != null)
            {
                overallStatusText.color = Color.white;
                overallStatusText.text = "멤을 배치하세요!";
            }
        }
        // 2-2. 가동 중지 (배치된 멤 모두 허기량 0)
        else if (isAllStarving)
        {
            // 🌟 밥통에 음식을 채워 넣은 경우 (급식 진행/대기)
            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
            // 🌟 밥통에 음식도 완전히 없는 경우
            else
            {
                StopDotsAnimation();
                if (overallStatusText != null)
                {
                    overallStatusText.color = Color.red;
                    overallStatusText.text = "식량이 부족합니다";
                }
            }
        }
        // 2-3. 정상 생산 진행 중
        else
        {
            StartDotsAnimation("생산중");
        }
    }

    private void StartDotsAnimation(string customPrefix = "생산중")
    {
        string prefix = string.IsNullOrEmpty(customPrefix) ? "생산중" : customPrefix;

        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (overallStatusText != null) overallStatusText.color = Color.white;

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetStatusText($"{currentStatusPrefix} ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{currentStatusPrefix} .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{currentStatusPrefix} ..."); })
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
        currentStatusPrefix = "";
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

        if (levelUpBtn != null)
        {
            bool isMax = targetFacility.currentLevel >= 5;
            levelUpBtn.interactable = !isMax;

            if (levelUpBtnText != null)
            {
                levelUpBtnText.text = isMax ? "Lv.Max" : "레벨업";
            }
        }
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