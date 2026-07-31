using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using HDY.Recipe;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ProductionFacilityRuntime : MonoBehaviour
{
    [Header("시설 기본 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("생산 가동 상태")]
    public bool isProducing = false;
    public string craftingItem;
    public float totalRequiredTime;
    public float currentProgressTime = 0f;
    public float baseProductionTime = 30f;

    [Header("보관 수량")]
    public int currentStorageCount = 0;
    public int maxStorageCount = 100;

    [Header("배치된 멤 정보")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

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
        UpdateMaxStorage();
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

    public void LevelUp()
    {
        currentLevel++;
        UpdateMaxStorage();
        CheckProductionCondition();
        OnMemDeploymentChanged?.Invoke();
    }

    public void UpdateMaxStorage()
    {
        maxStorageCount = currentLevel * 100;
    }

    private void Update()
    {
        if (!isProducing) return;

        if (currentStorageCount >= maxStorageCount)
        {
            return;
        }

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
            isProducing = false;
            currentProgressTime = 0f;
            return;
        }

        float baseDuration = baseProductionTime;

        if (currentProgressTime > 0f && totalRequiredTime > 0f)
        {
            float currentProgressPercent = currentProgressTime / totalRequiredTime;
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
            currentProgressTime = totalRequiredTime * currentProgressPercent;

            if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
            {
                SetProducingActive(true);
            }
            else
            {
                isProducing = false;
            }
        }
        else
        {
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
            if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
            {
                SetProducingActive(true);
                currentProgressTime = 0f;
            }
            else
            {
                isProducing = false;
                currentProgressTime = 0f;
            }
        }
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

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);

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
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null) return null;

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    private void SetProducingActive(bool value)
    {
        if (isProducing == value) return;
        isProducing = value;

        if (isProducing && buildingData != null)
        {
            FacilityStarted?.Invoke(buildingData.buildingType, addMems, MemPositions);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isProducing) return;
        isProducing = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation, MemPositions);
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }
}