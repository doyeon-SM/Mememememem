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
    [SerializeField] private GameObject levelUpArrowIcon; // 🌟 레벨업 버튼 내부의 화살표/세모 아이콘

    [Header("중앙 패널 - Center")]
    [SerializeField] private MemSlotUI[] memSlotImages = new MemSlotUI[5];

    [Header("중앙 패널 - Bottom")]
    [SerializeField] private Image creatingItem;
    [SerializeField] private TextMeshProUGUI creatingItemName;
    [SerializeField] private TextMeshProUGUI productionSpeed;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI completeCreateCount;
    [SerializeField] private Button getBtn;

    public ProductionFacilityRuntime TargetFacility => targetFacility;
    private ProductionFacilityRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentStatusPrefix = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (getBtn != null) getBtn.onClick.AddListener(OnClickCollectReward);
        if (levelUp != null) levelUp.onClick.AddListener(OnClickLevelUp);

        InitializeSlotIndexes();
    }

    private void OnEnable()
    {
        ProductionFacilityRuntime.OnMemDeploymentChanged += RefreshStaticUI;
    }

    private void OnDisable()
    {
        ProductionFacilityRuntime.OnMemDeploymentChanged -= RefreshStaticUI;
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

        if (getBtn != null)
        {
            getBtn.interactable = targetFacility.currentStorageCount > 0;
        }

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
            if (productionSpeed != null) productionSpeed.text = "- 초 (개당)";
        }
        else if (isAllStarving)
        {
            if (progressBar != null) progressBar.value = 0f;
            if (durationText != null) durationText.text = "0%";
            if (productionSpeed != null) productionSpeed.text = "- 초 (개당)";

            if (currentSatiety > 0)
            {
                StartDotsAnimation("음식 보충중");
            }
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
            if (productionSpeed != null) productionSpeed.text = $"{targetFacility.totalRequiredTime:F1}초 (개당)";
        }
        else
        {
            float progressNormalized = targetFacility.totalRequiredTime > 0f ? Mathf.Clamp01(targetFacility.currentProgressTime / targetFacility.totalRequiredTime) : 0f;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{progressNormalized * 100f:F0}%";
            if (productionSpeed != null) productionSpeed.text = $"{targetFacility.totalRequiredTime:F1}초 (개당)";

            StartDotsAnimation();
        }

        UpdateStorageText();
    }

    private void StartDotsAnimation(string customPrefix = null)
    {
        string prefix = customPrefix;

        if (string.IsNullOrEmpty(prefix))
        {
            prefix = "생산중";
        }

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

        // 🌟 [핵심 수정] 최대 레벨을 3으로 변경 및 화살표 세모 제거
        if (levelUp != null)
        {
            bool isMax = targetFacility.currentLevel >= 3;
            levelUp.interactable = !isMax;

            if (levelUpArrowIcon != null)
            {
                levelUpArrowIcon.SetActive(!isMax);
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