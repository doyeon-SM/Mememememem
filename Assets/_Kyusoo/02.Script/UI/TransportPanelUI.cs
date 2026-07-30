using HDY.Capture;
using HDY.Item;
using MemSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 운반 시설 전용 패널 UI 스크립트입니다.
/// </summary>
public class TransportPanelUI : MonoBehaviour
{
    public static TransportPanelUI Instance { get; private set; }

    [Header("상단 정보")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUpBtn;

    [Header("중앙 - 멤 슬롯 (최대 3개)")]
    [SerializeField] private MemSlotUI[] memSlots = new MemSlotUI[3];

    [Header("하단 - 진행도 및 텍스트")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI percentText;  // Slider 내 % 표시 [TMP]
    [SerializeField] private TextMeshProUGUI statusText;   // 상태 메시지 표시 [TMP]
    [SerializeField] private TextMeshProUGUI durationText; // 운반 주기 표시 [TMP]

    public TransportRuntime TargetFacility => targetFacility;
    private TransportRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (levelUpBtn != null)
        {
            levelUpBtn.onClick.AddListener(OnClickLevelUp);
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
    }

    public void RefreshUI()
    {
        if (targetFacility == null) return;
        RefreshStaticUI();
        UpdateDynamicUI();
    }

    private void UpdateDynamicUI()
    {
        if (targetFacility == null) return;

        if (targetFacility.isWorking && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            float percent = Mathf.Clamp(progressNormalized * 100f, 0f, 100f);

            if (progressBar != null) progressBar.value = progressNormalized;
            if (percentText != null) percentText.text = $"{percent:F0}%";

            if (durationText != null)
            {
                durationText.text = $"운반 주기: {targetFacility.totalRequiredTime:F1}초";
            }

            // 1. 5초 수거 작업 진행 중일 때
            if (targetFacility.isCollecting)
            {
                string targetItemName = targetFacility.GetTargetItemName();
                string prefix = string.IsNullOrEmpty(targetItemName) ? "아이템 수거중" : $"{targetItemName} 수거중";
                StartDotsAnimation(prefix);
            }
            // 2. 타이머 100% 도달했으나 조건(10개 이상) 충족 시설이 없을 때
            else if (percent >= 100f || targetFacility.currentProgressTime >= targetFacility.totalRequiredTime)
            {
                StopDotsAnimation();
                if (statusText != null) statusText.text = "운반 대기중";
            }
            // 3. 0% ~ 99% 정상 타이머 진행 중일 때
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
                durationText.text = "운반 대기 중";
            }

            StopDotsAnimation();
            if (statusText != null) statusText.text = "운반 대기중";
        }
    }

    private void StartDotsAnimation(string prefix)
    {
        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();

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
        targetFacility.LevelUp();
        RefreshStaticUI();
    }

    public bool TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null || targetEntry == null) return false;

        bool isSuccess = targetFacility.TryAddMem(targetMem, targetEntry);
        if (isSuccess)
        {
            RefreshStaticUI();
        }
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