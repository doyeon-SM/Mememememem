using HDY.Capture;
using HDY.Item;
using HDY.Upgrade;
using MemSystem.Data;
using System.Collections.Generic;
using System.Linq; // 🌟 LINQ 추가
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TransportPanelUI : MonoBehaviour
{
    public static TransportPanelUI Instance { get; private set; }

    [Header("상단 정보")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUpBtn;
    [SerializeField] private TextMeshProUGUI levelUpBtnText;

    [Header("중앙 - 멤 슬롯 (최대 3개)")]
    [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[3];

    [Header("하단 - 진행도 및 텍스트")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI percentText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI durationText;

    public TransportRuntime TargetFacility => targetFacility;
    private TransportRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (levelUpBtn != null) levelUpBtn.onClick.AddListener(OnClickLevelUp);

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

    private void OnDisable()
    {
        StopDotsAnimation();
    }

    private void Update()
    {
        if (targetFacility == null) return;
        UpdateDynamicUI();
    }

    public void OpenPanel(TransportRuntime facility)
    {
        if (facility == null) return;
        targetFacility = facility;

        RefreshStaticUI();
        RefreshUI();
    }

    public void RefreshStaticUI()
    {
        if (targetFacility == null) return;

        if (buildingName != null && targetFacility.buildingData != null)
            buildingName.text = targetFacility.buildingData.buildingName;

        if (buildingLevel != null)
            buildingLevel.text = $"Lv {targetFacility.currentLevel}";

        int maxCapacity = ProductionCalculator.GetTransportMaxMemCount(targetFacility.currentLevel);

        for (int i = 0; i < memSlots.Length; i++)
        {
            if (memSlots[i] == null) continue;

            bool isUnlocked = (i < maxCapacity);
            MemData placedMemData = null;
            CapturedMemEntry placedEntryData = null;

            if (isUnlocked)
            {
                if (i < targetFacility.DeployedMems.Count) placedMemData = targetFacility.DeployedMems[i];
                if (i < targetFacility.DeployedMemEntries.Count) placedEntryData = targetFacility.DeployedMemEntries[i];
            }

            memSlots[i].RefreshStatus(isUnlocked, placedMemData, placedEntryData);
        }

        if (levelUpBtn != null)
        {
            bool isMax = targetFacility.currentLevel >= 3;
            levelUpBtn.interactable = !isMax;

            if (levelUpBtnText != null)
            {
                levelUpBtnText.text = isMax ? "Lv.Max" : "레벨업";
            }
        }
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        UpdateDynamicUI();
    }

    /// <summary>
    /// 🌟 배치된 모든 멤의 배고픔량이 0 이하인지 검사
    /// </summary>
    private bool IsAllDeployedMemsStarving()
    {
        if (targetFacility == null || targetFacility.DeployedMemEntries == null || targetFacility.DeployedMemEntries.Count == 0)
            return false;

        return targetFacility.DeployedMemEntries.All(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
    }

    /// <summary>
    /// 🌟 실시간 상태 텍스트 및 UI 갱신 (식량 상태 분기 연동)
    /// </summary>
    private void UpdateDynamicUI()
    {
        if (targetFacility == null) return;

        bool isNoMem = targetFacility.DeployedMems.Count == 0 || targetFacility.DeployedMemEntries.Count == 0;
        bool isAllStarving = !isNoMem && IsAllDeployedMemsStarving();
        int currentSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;

        // 1. 멤 미배치
        if (isNoMem)
        {
            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.white;
                statusText.text = "멤을 배치하세요!";
            }
            if (progressBar != null) progressBar.value = 0f;
            if (percentText != null) percentText.text = "0%";
            if (durationText != null) durationText.text = "운반 대기 중";
        }
        // 2. 가동 중지 (배치된 멤 모두 허기량 0)
        else if (isAllStarving)
        {
            if (progressBar != null) progressBar.value = 0f;
            if (percentText != null) percentText.text = "0%";
            if (durationText != null) durationText.text = "운반 대기 중";

            // 2-1. 굶고 있지만 음식창고에 음식을 채워 넣은 경우 (급식 진행/대기)
            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
            // 2-2. 창고에 음식도 완전히 없는 경우
            else
            {
                StopDotsAnimation();
                if (statusText != null)
                {
                    statusText.color = Color.red;
                    statusText.text = "식량이 부족합니다";
                }
            }
        }
        // 3. 정상 운송 진행 중 (기존 Transport 시설 전용 연출 유지)
        else if (targetFacility.isWorking && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            float percent = Mathf.Clamp(progressNormalized * 100f, 0f, 100f);

            if (progressBar != null) progressBar.value = progressNormalized;
            if (percentText != null) percentText.text = $"{percent:F0}%";

            if (durationText != null)
            {
                durationText.text = $"운반 주기: {targetFacility.totalRequiredTime:F1}초";
            }

            if (targetFacility.isCollecting)
            {
                string targetItemName = targetFacility.GetTargetItemName();
                string prefix = string.IsNullOrEmpty(targetItemName) ? "아이템 수거중" : $"{targetItemName} 수거중";
                StartDotsAnimation(prefix);
            }
            else if (percent >= 100f || targetFacility.currentProgressTime >= targetFacility.totalRequiredTime)
            {
                StopDotsAnimation();
                if (statusText != null)
                {
                    statusText.color = Color.white;
                    statusText.text = "운반 대기중";
                }
            }
            else
            {
                StartDotsAnimation("운송 준비중");
            }
        }
        else
        {
            if (progressBar != null) progressBar.value = 0f;
            if (percentText != null) percentText.text = "0%";

            if (durationText != null)
            {
                durationText.text = "운반 대기중";
            }

            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.white;
                statusText.text = "운반 대기중";
            }
        }
    }

    private void StartDotsAnimation(string prefix)
    {
        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (statusText != null) statusText.color = Color.white; 

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { if (statusText != null) statusText.text = $"{currentStatusPrefix} ."; })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { if (statusText != null) statusText.text = $"{currentStatusPrefix} .."; })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { if (statusText != null) statusText.text = $"{currentStatusPrefix} ..."; })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
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

    public bool TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetEntry == null) return false;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);
        if (isSuccess) RefreshStaticUI();
        return isSuccess;
    }

    public void TryRemoveMemFromUI(MemData targetMem)
    {
        if (targetFacility == null || targetMem == null) return;

        targetFacility.RemoveMem(targetMem);
        RefreshStaticUI();
    }

    public void ClosePanel()
    {
        StopDotsAnimation();
        targetFacility = null;
    }
}