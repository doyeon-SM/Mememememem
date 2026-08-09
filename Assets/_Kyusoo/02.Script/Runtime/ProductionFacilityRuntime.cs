using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using HDY.Recipe;
using KMS.Audio;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ProductionFacilityRuntime : MonoBehaviour
{
    [Header("기본 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("생산 관련 정보")]
    public bool isProducing = false;
    public string craftingItem;
    public float totalRequiredTime;
    public float currentProgressTime = 0f;
    public float baseProductionTime = 30f;

    [Header("보관함 정보")]
    public int currentStorageCount = 0;
    public int maxStorageCount = 100;

    [Header("배치된 멤 데이터")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    private Coroutine soundRoutine;

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    [SerializeField] private List<Transform> memPositions = new List<Transform>();
    public List<Transform> MemPositions
    {
        get
        {
            if (memPositions == null || memPositions.Count == 0) CacheMemPositions();
            return memPositions;
        }
    }

    public static event Action OnMemDeploymentChanged;
    public static event Action<BuildingType, MemData, bool, List<Transform>> MemAdded;
    public static event Action<BuildingType, List<MemData>, List<Transform>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason, List<Transform>> FacilityStopped;

    private void Start()
    {
        EnsureBuildingData();
        CacheMemPositions();
        CheckProductionCondition();
        if (FacilityCollectManager.Instance != null)
            FacilityCollectManager.Instance.RegisterFacility(this);
    }

    private void OnDestroy()
    {
        if (FacilityCollectManager.Instance != null)
            FacilityCollectManager.Instance.UnregisterFacility(this);
    }

    private void EnsureBuildingData()
    {
        if (buildingData == null && TryGetComponent<BuildingRuntime>(out var br))
        {
            buildingData = br.buildingData;
        }
    }

    private void CacheMemPositions()
    {
        memPositions.Clear();
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name.StartsWith("MemPos"))
            {
                memPositions.Add(child);
            }
        }
    }

    /// <summary>
    /// 레벨업 시 멤 슬롯 한 칸 추가 해금 (최대 5레벨)
    /// </summary>
    public void LevelUp()
    {
        if (currentLevel < 5)
        {
            currentLevel++;
            CheckProductionCondition();
            OnMemDeploymentChanged?.Invoke();

            if (ProductionPanelUI.Instance != null && ProductionPanelUI.Instance.TargetFacility == this)
            {
                ProductionPanelUI.Instance.RefreshStaticUI();
            }
        }
    }

    private void Update()
    {
        if (!isProducing)
        {
            SetProducingActive(false);
            return;
        }
        if (currentStorageCount >= maxStorageCount) return;

        currentProgressTime += Time.deltaTime;
        if (currentProgressTime >= totalRequiredTime)
        {
            CompleteProductionUnit();
        }
    }

    public void CheckProductionCondition()
    {
        if (string.IsNullOrEmpty(craftingItem) || addMems.Count == 0)
        {
            SetProducingActive(false);
            currentProgressTime = 0f;
            return;
        }

        float baseDuration = baseProductionTime;
        float newTotalTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);

        // 🌟 [수정] 진행 소요 시간을 먼저 구한 뒤 기존 진행 시간을 보존
        if (totalRequiredTime > 0f && currentProgressTime > 0f)
        {
            float currentProgressPercent = currentProgressTime / totalRequiredTime;
            totalRequiredTime = newTotalTime;
            currentProgressTime = totalRequiredTime * currentProgressPercent;
        }
        else
        {
            totalRequiredTime = newTotalTime;
            if (currentProgressTime > totalRequiredTime)
            {
                currentProgressTime = 0f;
            }
        }

        bool isAnyMemStarving = DeployedMemEntries.Any(e => e != null && (e.IsStarving || e.CurrentHunger <= 0));

        if (!isAnyMemStarving)
        {
            SetProducingActive(true);
        }
        else
        {
            SetProducingActive(false);
        }
        SetProducingActive(!isAnyMemStarving);
    }

    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();
        if (targetEntry == null || buildingData == null) return false;

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null && !string.IsNullOrEmpty(targetEntry.MemId))
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }
        if (realMemData == null) return false;

        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);
        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId)) return false;
        if (targetEntry.IsActive) return false;

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType)) return false;

        if (addMems.Count >= maxCapacity && addMemEntries.Count > 0)
        {
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        CheckProductionCondition();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, realMemData, true, MemPositions);
        }
        return true;
    }

    public void RemoveMem(CapturedMemEntry targetEntry)
    {
        if (targetEntry == null) return;
        int index = addMemEntries.FindIndex(e => e != null && e.KeyId == targetEntry.KeyId);
        if (index >= 0)
        {
            MemData removedMem = (index < addMems.Count) ? addMems[index] : null;
            addMemEntries[index].IsActive = false;
            addMemEntries.RemoveAt(index);
            if (index < addMems.Count) addMems.RemoveAt(index);
            CheckProductionCondition();

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
            OnMemDeploymentChanged?.Invoke();

            if (buildingData != null && removedMem != null)
            {
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false, MemPositions);
            }
        }

        if(addMemEntries.Count == 0)
        {
            SetProducingActive(false);
        }
    }

    public void RemoveMem(MemData targetMem)
    {
        if (targetMem == null) return;
        int index = addMems.IndexOf(targetMem);
        if (index >= 0 && index < addMemEntries.Count)
        {
            RemoveMem(addMemEntries[index]);
        }
    }

    private void CompleteProductionUnit()
    {
        currentStorageCount++;
        currentProgressTime = 0f;
        if (!string.IsNullOrEmpty(craftingItem))
        {
            float baseDuration = baseProductionTime;
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
        }
        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    public void StoredItems()
    {
        if (currentStorageCount <= 0 || string.IsNullOrEmpty(craftingItem)) return;
        ItemData targetItemData = FindItemDataInCatalog(craftingItem);
        if (targetItemData == null) return;

        int amountToCollect = currentStorageCount;
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();
        if (warehouse != null)
        {
            int remaining = warehouse.AddItem(targetItemData, amountToCollect);
            currentStorageCount = remaining;
        }
        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (ItemCatalogManager.Instance == null) return null;
        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    private void SetProducingActive(bool value)
    {
        if (isProducing == value && (value && soundRoutine != null)) return;
        isProducing = value;
        string objName = gameObject.name;

        if (isProducing)
        {
            if (soundRoutine != null) StopCoroutine(soundRoutine);
            soundRoutine = StartCoroutine(FacilitySoundRoutine());

            if (buildingData != null)
                FacilityStarted?.Invoke(buildingData.buildingType, addMems, MemPositions);
        }
        else
        {
            if (soundRoutine != null)
            {
                StopCoroutine(soundRoutine);
                soundRoutine = null;
            }

            if (objName.Contains("Logging")) KMS.Audio.KMSAudioService.StopSfx(GameSfxId.Logging);
            else if (objName.Contains("Mining")) KMS.Audio.KMSAudioService.StopSfx(GameSfxId.Mining);
            else if (objName.Contains("Berry")) KMS.Audio.KMSAudioService.StopSfx(GameSfxId.Farm);
            else if (objName.Contains("Wheat")) KMS.Audio.KMSAudioService.StopSfx(GameSfxId.WheatFarm);
        }
    }

    private IEnumerator FacilitySoundRoutine()
    {
        string objName = gameObject.name;
        float interval = objName.Contains("Mining") ? 1.5f : 2.0f;

        while (isProducing)
        {
            PlayFacilitySfx();
            yield return new WaitForSeconds(interval);
        }
    }

    private void PlayFacilitySfx()
    {
        string objName = gameObject.name;
        if (objName.Contains("Logging")) KMSAudioService.PlayAt(GameSfxId.Logging, transform.position);
        else if (objName.Contains("Mining")) KMSAudioService.PlayAt(GameSfxId.Mining, transform.position);
        else if (objName.Contains("Berry")) KMSAudioService.PlayAt(GameSfxId.Farm, transform.position);
        else if (objName.Contains("Wheat")) KMSAudioService.PlayAt(GameSfxId.WheatFarm, transform.position);
    }

    private void StopFacilitySfx()
    {
        string objName = gameObject.name;
        if (objName.Contains("Logging")) KMSAudioService.StopSfx(GameSfxId.Logging);
        else if (objName.Contains("Mining")) KMSAudioService.StopSfx(GameSfxId.Mining);
        else if (objName.Contains("Berry")) KMSAudioService.StopSfx(GameSfxId.Farm);
        else if (objName.Contains("Wheat")) KMSAudioService.StopSfx(GameSfxId.WheatFarm);
    }

    public void StopWorkDueToStarvation()
    {
        if (!isProducing) return;
        SetProducingActive(false);
        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation, MemPositions);
        }
        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }
}