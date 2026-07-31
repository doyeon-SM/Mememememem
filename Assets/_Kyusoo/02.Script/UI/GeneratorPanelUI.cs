using HDY.Capture;
using HDY.Item;
using MemSystem.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class GeneratorPanelUI : MonoBehaviour
{
    public static GeneratorPanelUI Instance { get; private set; }

    [Header("상단 - 기본 정보")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUpBtn;

    [Header("우측 상단 - 실시간 전력 생산 정보")]
    [SerializeField] private TextMeshProUGUI powerGenerationRateText;

    [Header("좌측 상단 - 전력 축적량 UI 컴포넌트")]
    [SerializeField] private TotalPowerStorageUI totalPowerStorageUI;

    [Header("중앙 - 멤 슬롯 (단일 1마리)")]
    [SerializeField] private MemSlotUI singleMemSlot;

    [Header("하단 - 발전 진행도 및 시간")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText; // 슬라이더 우측 퍼센트(%) 표시용 [TMP]
    [SerializeField] private TextMeshProUGUI statusText;   // 🌟 새로 추가: "전력 생산중 ..." 상태 표시용 [TMP]

    public GeneratorRuntime TargetFacility => targetFacility;
    private GeneratorRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (levelUpBtn != null)
        {
            levelUpBtn.onClick.AddListener(OnClickLevelUp);
        }

        if (singleMemSlot != null)
        {
            singleMemSlot.InitializeSlot(0);
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

    public void OpenPanel(GeneratorRuntime facility)
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

        MemData placedMemData = targetFacility.DeployedMems.Count > 0 ? targetFacility.DeployedMems[0] : null;
        CapturedMemEntry placedEntryData = targetFacility.DeployedMemEntries.Count > 0 ? targetFacility.DeployedMemEntries[0] : null;

        if (singleMemSlot != null)
        {
            singleMemSlot.RefreshStatus(true, placedMemData, placedEntryData);
        }

        RefreshTopRightInfo();
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

        // 1. 하단 슬라이더 및 % 텍스트 표기
        if (targetFacility.isPowerGenerating && targetFacility.totalPowerRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentPowerProgressTime / targetFacility.totalPowerRequiredTime;
            float percent = Mathf.Clamp(progressNormalized * 100f, 0f, 100f);

            if (progressBar != null) progressBar.value = progressNormalized;

            // 슬라이더 옆 퍼센트 표기
            if (durationText != null) durationText.text = $"{percent:F0}%";

            StartDotsAnimation();
        }
        else
        {
            if (progressBar != null) progressBar.value = 0f;
            if (durationText != null) durationText.text = "0%";

            StopDotsAnimation();
            if (statusText != null) statusText.text = "발전 대기 중";
        }

        // 2. 좌측 상단 축적량 UI 갱신
        if (totalPowerStorageUI != null)
        {
            totalPowerStorageUI.RefreshUI(targetFacility);
        }

        // 3. 우측 상단 생산량 정보 갱신
        RefreshTopRightInfo();
    }

    private void StartDotsAnimation()
    {
        if (isAnimatingDots) return;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();

        // 🌟 "전력 생산중 ." -> "전력 생산중 .." -> "전력 생산중 ..." 루프
        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetProductionStatusText("전력 생산중 ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetProductionStatusText("전력 생산중 .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetProductionStatusText("전력 생산중 ..."); })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
    }

    private void SetProductionStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
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

    private void RefreshTopRightInfo()
    {
        if (targetFacility == null || powerGenerationRateText == null) return;

        float currentTime = targetFacility.totalPowerRequiredTime > 0f
            ? targetFacility.totalPowerRequiredTime
            : targetFacility.basePowerGenerationTime;

        powerGenerationRateText.text = $"+{targetFacility.powerPerUnit} Watt / {currentTime:F1}s";
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