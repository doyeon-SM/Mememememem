using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RanchSlotRuntime
{
    [Header("슬롯 상태 정보")]
    public int slotIndex;
    public bool isUnlocked = false;

    [Header("배치된 멤 정보")]
    public MemData deployedMem;
    public CapturedMemEntry deployedMemEntry;

    [Header("생산 상태 정보")]
    public string craftingItemId;
    public bool isProducing = false;
    public float currentProgressTime = 0f;
    public float totalRequiredTime = 30f;

    [Header("자원 축적 현황")]
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
    [Header("시설 기반 정보")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("기본 생산 소요 시간 (초)")]
    public float baseProductionTime = 30f;

    [Header("목장 슬롯 리스트 (최대 5개 관리)")]
    [SerializeField] private List<RanchSlotRuntime> slots = new List<RanchSlotRuntime>();
    public IReadOnlyList<RanchSlotRuntime> Slots => slots;

    public bool isProducing = false;

    public static event Action OnMemDeploymentChanged;

    public static event Action<BuildingType, MemData, bool> MemAdded;
    public static event Action<BuildingType, List<MemData>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;

    private void Awake()
    {
        InitializeSlots();
    }

    private void Start()
    {
        UpdateSlotCapacity();
        CheckAllSlotsProductionCondition();
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
        Debug.Log($"<color=lime>[목장 레벨업]</color> {buildingData?.buildingName} 시설 레벨이 {currentLevel}로 증가했습니다.");
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
        if (targetMem == null || targetEntry == null || buildingData == null) return false;
        if (slotIndex < 0 || slotIndex >= slots.Count) return false;

        RanchSlotRuntime targetSlot = slots[slotIndex];
        if (!targetSlot.isUnlocked)
        {
            Debug.LogWarning("[목장] 잠겨있는 슬롯에는 멤을 배치할 수 없습니다.");
            return false;
        }

        // 🌟 [수정]: MemData/MemId 중복 검사 제거 -> KeyId 동일 개체만 중복 검사
        foreach (var slot in slots)
        {
            if (slot.deployedMemEntry != null && slot.deployedMemEntry.KeyId == targetEntry.KeyId)
            {
                Debug.LogWarning($"해당 멤 개체(KeyID: {targetEntry.KeyId})는 이미 이 목장의 다른 슬롯에 배치되어 있습니다.");
                return false;
            }
        }

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"{targetMem.memName}(은/는) 이미 다른 시설이나 탐험대에 배치되어 있습니다.");
            return false;
        }

        if (!ProductionCalculator.CanDeployToFacility(targetMem, buildingData.buildingType))
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
            Debug.LogWarning($"{targetMem.memName}이 {requiredStat} 스탯이 없어 목장에 배치할 수 없습니다.");
            return false;
        }

        targetSlot.deployedMem = targetMem;
        targetSlot.deployedMemEntry = targetEntry;
        targetEntry.IsActive = true;

        string produceItemId = "item_wood"; // 임시 설정
        targetSlot.craftingItemId = produceItemId;

        targetSlot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
            baseProductionTime,
            new List<MemData> { targetMem }
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
            MemAdded?.Invoke(buildingData.buildingType, targetMem, true);
        }

        return true;
    }

    /// <summary>
    /// 🌟 [추가]: CapturedMemEntry (KeyId) 기준 슬롯 해제
    /// </summary>
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
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false);
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
            if (isProducing && buildingData != null)
            {
                List<MemData> activeMems = new List<MemData>();
                foreach (var s in slots) if (s.deployedMem != null) activeMems.Add(s.deployedMem);
                FacilityStarted?.Invoke(buildingData.buildingType, activeMems);
            }
        }
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

    public void StopWorkDueToStarvation()
    {
        foreach (var slot in slots)
        {
            slot.isProducing = false;
        }
        isProducing = false;

        if (buildingData != null)
        {
            List<MemData> activeMems = new List<MemData>();
            foreach (var s in slots) if (s.deployedMem != null) activeMems.Add(s.deployedMem);
            FacilityStopped?.Invoke(buildingData.buildingType, activeMems, FacilityStopReason.Starvation);
        }
    }
}