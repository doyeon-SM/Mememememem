using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RanchSlotRuntime
{
    [Header("슬롯 인덱스 및 해금 여부")]
    public int slotIndex;
    public bool isUnlocked = false;

    [Header("배치된 멤 데이터")]
    public MemData deployedMem;
    public CapturedMemEntry deployedMemEntry;

    [Header("생산 상태")]
    public string craftingItemId;
    public bool isProducing = false;
    public float currentProgressTime = 0f;
    public float totalRequiredTime = 30f;

    [Header("보관함 수량")]
    public int currentStorageCount = 0;
    public const int maxStorage = 100;

    public void ClearMem()
    {
        if (deployedMemEntry != null)
        {
            deployedMemEntry.IsActive = false;
        }
        deployedMem = null;
        deployedMemEntry = null;
        craftingItemId = string.Empty;
        isProducing = false;
        currentProgressTime = 0f;
    }
}

public class RanchFacilityRuntime : MonoBehaviour
{
    [Header("시설 기반 데이터")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("기본 생산 주기 (초)")]
    public float baseProductionTime = 30f;

    [Header("슬롯 데이터 (최대 5개)")]
    [SerializeField] private List<RanchSlotRuntime> slots = new List<RanchSlotRuntime>();
    public IReadOnlyList<RanchSlotRuntime> Slots => slots;

    public bool isProducing = false;

    // 🌟 MemPos 트랜스폼 캐싱 리스트
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

    private void Awake()
    {
        InitializeSlots();
    }

    private void Start()
    {
        EnsureBuildingData();
        CacheMemPositions();
        UpdateSlotCapacity();
        CheckAllSlotsProductionCondition();

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

    private void InitializeSlots()
    {
        if (slots.Count == 0)
        {
            for (int i = 0; i < 5; i++)
            {
                slots.Add(new RanchSlotRuntime
                {
                    slotIndex = i,
                    isUnlocked = false
                });
            }
        }
    }

    public void LevelUp()
    {
        currentLevel++;
        UpdateSlotCapacity();
        CheckAllSlotsProductionCondition();
        OnMemDeploymentChanged?.Invoke();
    }

    public void UpdateSlotCapacity()
    {
        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].isUnlocked = (i < maxCapacity);
        }
    }

    private void Update()
    {
        bool anyProducing = false;

        for (int i = 0; i < slots.Count; i++)
        {
            RanchSlotRuntime slot = slots[i];
            if (!slot.isUnlocked || !slot.isProducing || slot.deployedMem == null) continue;

            if (slot.currentStorageCount >= RanchSlotRuntime.maxStorage)
            {
                slot.isProducing = false;
                continue;
            }

            anyProducing = true;
            slot.currentProgressTime += Time.deltaTime;

            if (slot.currentProgressTime >= slot.totalRequiredTime)
            {
                CompleteSlotProduction(slot);
            }
        }

        isProducing = anyProducing;
    }

    private void CompleteSlotProduction(RanchSlotRuntime slot)
    {
        slot.currentStorageCount++;
        slot.currentProgressTime = 0f;

        if (slot.currentStorageCount >= RanchSlotRuntime.maxStorage)
        {
            slot.isProducing = false;
        }
        else
        {
            slot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
                baseProductionTime,
                new List<MemData> { slot.deployedMem }
            );
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    public void CheckAllSlotsProductionCondition()
    {
        bool isStarving = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        foreach (var slot in slots)
        {
            if (!slot.isUnlocked || slot.deployedMem == null || string.IsNullOrEmpty(slot.craftingItemId))
            {
                slot.isProducing = false;
                continue;
            }

            if (slot.currentStorageCount >= RanchSlotRuntime.maxStorage)
            {
                slot.isProducing = false;
                continue;
            }

            slot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
                baseProductionTime,
                new List<MemData> { slot.deployedMem }
            );

            slot.isProducing = !isStarving;
        }

        UpdateOverallProducingState();
    }

    public bool TryAddMemToSlot(int slotIndex, MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();

        if (targetEntry == null || buildingData == null) return false;
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;

        RanchSlotRuntime targetSlot = slots[slotIndex];
        if (!targetSlot.isUnlocked) return false;

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null)
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }

        if (realMemData == null) return false;

        foreach (var slot in slots)
        {
            if (slot != targetSlot && slot.deployedMemEntry != null && slot.deployedMemEntry.KeyId == targetEntry.KeyId)
            {
                return false;
            }
        }

        if (targetEntry.IsActive && (targetSlot.deployedMemEntry == null || targetSlot.deployedMemEntry.KeyId != targetEntry.KeyId))
        {
            return false;
        }

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType)) return false;

        if (targetSlot.deployedMemEntry != null && targetSlot.deployedMemEntry.KeyId != targetEntry.KeyId)
        {
            targetSlot.ClearMem();
        }

        targetSlot.deployedMem = realMemData;
        targetSlot.deployedMemEntry = targetEntry;
        targetEntry.IsActive = true;

        targetSlot.craftingItemId = GetRanchProduceItemId(realMemData);
        targetSlot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
            baseProductionTime,
            new List<MemData> { realMemData }
        );
        targetSlot.currentProgressTime = 0f;

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            targetSlot.isProducing = true;
        }

        UpdateOverallProducingState();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, realMemData, true, MemPositions);
        }

        return true;
    }

    public string GetRanchProduceItemId(MemData memData)
    {
        if (memData == null) return "item_rough_fur";
        switch (memData.memId)
        {
            case "Mem_Rare_01": return "item_rough_fur";
            case "Mem_Epic_01": return "item_rough_fur";
            case "Mem_Unique_01": return "item_diamond";
            default: return "item_rough_fur";
        }
    }

    public void RemoveMem(CapturedMemEntry targetEntry)
    {
        if (targetEntry == null) return;

        RanchSlotRuntime targetSlot = slots.Find(s => s.deployedMemEntry != null && s.deployedMemEntry.KeyId == targetEntry.KeyId);
        if (targetSlot != null)
        {
            MemData removedMem = targetSlot.deployedMem;
            targetSlot.ClearMem();

            UpdateOverallProducingState();

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

        RanchSlotRuntime targetSlot = slots.Find(s => s.deployedMem == targetMem);
        if (targetSlot != null)
        {
            RemoveMem(targetSlot.deployedMemEntry);
        }
    }

    public bool HasAnyCollectableItem()
    {
        foreach (var slot in slots)
        {
            if (slot.currentStorageCount > 0) return true;
        }
        return false;
    }

    public void CollectAllItems()
    {
        WarehouseInventory warehouse = FindFirstObjectByType<WarehouseInventory>();
        if (warehouse == null) return;

        foreach (var slot in slots)
        {
            if (slot.currentStorageCount <= 0 || string.IsNullOrEmpty(slot.craftingItemId)) continue;

            ItemData itemData = FindItemDataInCatalog(slot.craftingItemId);
            if (itemData != null)
            {
                int remaining = warehouse.AddItem(itemData, slot.currentStorageCount);
                slot.currentStorageCount = remaining;

                if (slot.currentStorageCount < RanchSlotRuntime.maxStorage && slot.deployedMem != null)
                {
                    if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
                    {
                        slot.isProducing = true;
                    }
                }
            }
        }

        UpdateOverallProducingState();
    }

    private void UpdateOverallProducingState()
    {
        bool anyActive = slots.Exists(s => s.isProducing);
        if (isProducing != anyActive)
        {
            isProducing = anyActive;
            List<MemData> activeMems = new List<MemData>();
            foreach (var s in slots) if (s.deployedMem != null) activeMems.Add(s.deployedMem);

            if (isProducing && buildingData != null)
            {
                FacilityStarted?.Invoke(buildingData.buildingType, activeMems, MemPositions);
            }
            else if (!isProducing && buildingData != null)
            {
                FacilityStopped?.Invoke(buildingData.buildingType, activeMems, FacilityStopReason.CompleteCrafting, MemPositions);
            }
        }
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        if (ItemCatalogManager.Instance == null) return null;

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    public void StopWorkDueToStarvation()
    {
        List<MemData> activeMems = new List<MemData>();
        foreach (var s in slots) if (s.deployedMem != null) activeMems.Add(s.deployedMem);

        foreach (var slot in slots)
        {
            slot.isProducing = false;
        }
        isProducing = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, activeMems, FacilityStopReason.Starvation, MemPositions);
        }
    }
}