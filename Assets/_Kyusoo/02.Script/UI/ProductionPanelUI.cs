using HDY.Capture;
using HDY.Item;
using HDY.Upgrade;
using MemSystem.Data;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ProductionPanelUI : MonoBehaviour
{
    public static ProductionPanelUI Instance { get; private set; }

    [Header("중앙 패널 - Top")]
    [SerializeField] private TextMeshProUGUI buildingName;
    [SerializeField] private TextMeshProUGUI buildingLevel;
    [SerializeField] private Button levelUp;
    [SerializeField] private TextMeshProUGUI levelUpBtnText;

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
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("제작할 아이템 관련 정보")]
    [SerializeField] private GameObject craftingSlotPrefab;
    [SerializeField] private Transform craftingSlotParent;

    public ProductionFacilityRuntime TargetFacility => targetFacility;
    private ProductionFacilityRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = ""; // 🌟 상태 프리픽스 추적 변수

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (diamondBGBtn != null) diamondBGBtn.onClick.AddListener(OnClickCollectReward);
        if (levelUp != null) levelUp.onClick.AddListener(OnClickLevelUp);

        InitializeSlotIndexes();
    }

    private void OnDisable()
    {
        StopDotsAnimation();
    }

    private void InitializeSlotIndexes()
    {
        for (int i = 0; i < memSlotImages.Length; i++)
        {
            if (memSlotImages[i] != null)
            {
                memSlotImages[i].InitializeSlot(i);
            }
        }
    }

    /// <summary>
    /// 배치된 모든 멤의 배고픔량이 0 이하(또는 IsStarving)인지 검사
    /// </summary>
    private bool IsAllDeployedMemsStarving()
    {
        if (targetFacility == null || targetFacility.DeployedMemEntries == null || targetFacility.DeployedMemEntries.Count == 0)
            return false;

        return targetFacility.DeployedMemEntries.All(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));
    }

    private void Update()
    {
        if (targetFacility == null) return;

        bool isNoMem = targetFacility.DeployedMems.Count == 0 || targetFacility.DeployedMemEntries.Count == 0;
        bool isAllStarving = !isNoMem && IsAllDeployedMemsStarving();
        bool isStorageFull = targetFacility.currentStorageCount >= targetFacility.maxStorageCount;
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
            if (durationText != null) durationText.text = "0%";
            if (productionSpeed != null) productionSpeed.text = "생산속도: - 초(개당)";
        }
        // 2. 가동 중지 (배치된 멤 모두 허기량 0)
        else if (isAllStarving)
        {
            if (progressBar != null) progressBar.value = 0f;
            if (durationText != null) durationText.text = "0%";
            if (productionSpeed != null) productionSpeed.text = "생산속도: - 초(개당)";

            // 🌟 2-1. 굶고 있지만 음식창고에 음식을 채워 넣은 경우 (급식 진행/대기)
            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
            // 🌟 2-2. 창고에 음식도 완전히 없는 경우
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
        // 3. 보관함 가득 참
        else if (isStorageFull)
        {
            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.white;
                statusText.text = "보관함이 가득 찼습니다";
            }
            if (progressBar != null) progressBar.value = 1f;
            if (durationText != null) durationText.text = "100%";
        }
        // 4. 정상 생산 진행 중 (허기 상태 해제됨)
        else
        {
            float progressNormalized = targetFacility.totalRequiredTime > 0f ? (targetFacility.currentProgressTime / targetFacility.totalRequiredTime) : 0f;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";
            if (productionSpeed != null) productionSpeed.text = $"생산속도: {targetFacility.totalRequiredTime:F1}초(개당)";

            StartDotsAnimation(); // 기본 아이템이름 + "생산중"
        }

        UpdateStorageText();
    }

    /// <summary>
    /// 🌟 customPrefix 인자를 받아 "음식 보충중" 또는 "[아이템명] 생산중" 다변화 애니메이션 지원
    /// </summary>
    private void StartDotsAnimation(string customPrefix = null)
    {
        string prefix = customPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            string itemName = "";
            if (targetFacility != null && !string.IsNullOrEmpty(targetFacility.craftingItem))
            {
                ItemData targetItemData = FindItemDataInCatalog(targetFacility.craftingItem);
                if (targetItemData != null) itemName = targetItemData.ItemName;
            }
            prefix = string.IsNullOrEmpty(itemName) ? "생산중" : $"{itemName} 생산중";
        }

        // 이미 동일한 접두사로 애니메이션 재생 중이면 재시작 방지
        if (isAnimatingDots && currentStatusPrefix == prefix) return;

        currentStatusPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (statusText != null) statusText.color = Color.white;

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
        if (statusText != null) statusText.text = text;
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

        if (levelUp != null)
        {
            bool isMax = targetFacility.currentLevel >= 5;
            levelUp.interactable = !isMax;

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
                if (creatingItemName != null) creatingItemName.text = targetItemData.ItemName;
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
        if (ItemCatalogManager.Instance == null) return null;
        return ItemCatalogManager.Instance.FindItemData(itemId);
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
        StopDotsAnimation();
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