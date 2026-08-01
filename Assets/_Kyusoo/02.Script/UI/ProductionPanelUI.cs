using HDY.Capture;
using HDY.Item;
using MemSystem.Data;
using System.Collections.Generic;
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

    [Header("제작할 아이템 관련 정보: 프리팹, 생성 위치, SO리스트 전체")]
    [SerializeField] private GameObject craftingSlotPrefab;
    [SerializeField] private Transform craftingSlotParent;

    public ProductionFacilityRuntime TargetFacility => targetFacility;
    private ProductionFacilityRuntime targetFacility;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (diamondBGBtn != null)
        {
            diamondBGBtn.onClick.AddListener(OnClickCollectReward);
        }

        if (levelUp != null)
        {
            levelUp.onClick.AddListener(OnClickLevelUp);
        }

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

    private void Update()
    {
        if (targetFacility == null) return;

        bool isStarving = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        if (isStarving)
        {
            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.red;
                statusText.text = "식량이 부족합니다";
            }
        }
        else if (targetFacility.isProducing && !string.IsNullOrEmpty(targetFacility.craftingItem) && targetFacility.totalRequiredTime > 0f)
        {
            float progressNormalized = targetFacility.currentProgressTime / targetFacility.totalRequiredTime;
            if (progressBar != null) progressBar.value = progressNormalized;
            if (durationText != null) durationText.text = $"{Mathf.Clamp(progressNormalized * 100f, 0f, 100f):F0}%";
            if (productionSpeed != null) productionSpeed.text = $"생산속도: {targetFacility.totalRequiredTime:F1}초(개당)";

            StartDotsAnimation();
        }
        else
        {
            if (progressBar != null) progressBar.value = 0f;
            if (durationText != null) durationText.text = "0%";
            if (productionSpeed != null) productionSpeed.text = "생산속도: - 초(개당)";

            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.white;
                if (targetFacility.currentStorageCount >= targetFacility.maxStorageCount)
                    statusText.text = "보관함 가득 참";
                else if (targetFacility.DeployedMems.Count == 0)
                    statusText.text = "멤 미배치";
                else
                    statusText.text = "생산 대기 중";
            }
        }

        UpdateStorageText();
    }

    /// <summary>
    /// 🌟 아이템 이름 + "생산중 . . ." 애니메이션 적용
    /// </summary>
    private void StartDotsAnimation()
    {
        if (isAnimatingDots) return;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();

        if (statusText != null) statusText.color = Color.white;

        // 아이템 이름 추출
        string itemName = "";
        if (targetFacility != null && !string.IsNullOrEmpty(targetFacility.craftingItem))
        {
            ItemData targetItemData = FindItemDataInCatalog(targetFacility.craftingItem);
            if (targetItemData != null)
            {
                itemName = targetItemData.ItemName;
            }
        }

        string prefix = string.IsNullOrEmpty(itemName) ? "생산중" : $"{itemName} 생산중";

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { SetStatusText($"{prefix} ."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{prefix} .."); })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { SetStatusText($"{prefix} ..."); })
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
    }

    private void OnClickLevelUp()
    {
        if (targetFacility == null) return;

        targetFacility.LevelUp();
        RefreshStaticUI();
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
                if (creatingItemName != null)
                {
                    creatingItemName.text = targetItemData.ItemName;
                }
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

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    public bool TryDeployMemFromUI(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetFacility == null)
        {
            Debug.LogError($"[{GetType().Name}] ❌ targetFacility 참조가 null입니다. OpenPanel()이 정상적으로 호출되었는지 확인하세요.");
            return false;
        }

        if (targetEntry == null)
        {
            Debug.LogError($"[{GetType().Name}] ❌ targetEntry 인자가 null입니다.");
            return false;
        }

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