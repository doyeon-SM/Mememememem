using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유니티 인스펙터 노출 및 직렬화를 위한 단일 목장 슬롯 런타임 데이터 클래스
/// </summary>
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
    public const int MAX_STORAGE_PER_SLOT = 100;

    /// <summary>
    /// 슬롯의 멤 배치 정보를 초기화합니다.
    /// </summary>
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

    /// <summary>
    /// 최대 5개의 내부 슬롯 데이터 구조체를 기본 생성합니다.
    /// </summary>
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

    /// <summary>
    /// 목장 레벨에 맞추어 슬롯 해금(Unlock) 상태를 갱신합니다.
    /// </summary>
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

            // 슬롯당 최대 100개 제한
            if (slot.currentStorageCount >= RanchSlotRuntime.MAX_STORAGE_PER_SLOT)
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

    /// <summary>
    /// 단일 슬롯 생산 완료 처리
    /// </summary>
    private void CompleteSlotProduction(RanchSlotRuntime slot)
    {
        slot.currentStorageCount++;
        slot.currentProgressTime = 0f;

        if (slot.currentStorageCount >= RanchSlotRuntime.MAX_STORAGE_PER_SLOT)
        {
            slot.isProducing = false;
        }
        else
        {
            // 멤 단일 속도 재계산
            slot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
                baseProductionTime,
                new List<MemData> { slot.deployedMem }
            );
        }
    }

    /// <summary>
    /// 전체 슬롯의 가동 조건을 체크합니다.
    /// </summary>
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

            if (slot.currentStorageCount >= RanchSlotRuntime.MAX_STORAGE_PER_SLOT)
            {
                slot.isProducing = false;
                continue;
            }

            // 개별 멤 등급 기반 시간 산출
            slot.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(
                baseProductionTime,
                new List<MemData> { slot.deployedMem }
            );

            slot.isProducing = !isStarving;
        }

        UpdateOverallProducingState();
    }

    /// <summary>
    /// UI 또는 시스템에서 특정 인덱스의 슬롯에 멤을 배치할 때 호출
    /// </summary>
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

        // 이미 다른 슬롯에 배치되어 있는지 검사
        foreach (var slot in slots)
        {
            if (slot.deployedMem == targetMem)
            {
                Debug.LogWarning($"{targetMem.memName}은 이미 이 목장의 다른 슬롯에 배치되어 있습니다.");
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

        // 슬롯 데이터 할당
        targetSlot.deployedMem = targetMem;
        targetSlot.deployedMemEntry = targetEntry;
        targetEntry.IsActive = true;

        // 멤 고유 생산 아이템 ID 바인딩 (ranchProduceItemId 우선 참조, 없을 시 memId)
        //string produceItemId = !string.IsNullOrEmpty(targetMem.ranchProduceItemId) ? targetMem.ranchProduceItemId : targetMem.memId;
        // 임시 코드
        string produceItemId = "item_wood";
        targetSlot.craftingItemId = produceItemId;

        // 단일 멤 기준 생산시간 계산 (기본 30초 기반)
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
    /// 특정 멤을 목장 슬롯에서 해제할 때 호출
    /// </summary>
    public void RemoveMem(MemData targetMem)
    {
        if (targetMem == null) return;

        RanchSlotRuntime targetSlot = slots.Find(s => s.deployedMem == targetMem);
        if (targetSlot != null)
        {
            targetSlot.ClearMem();

            UpdateOverallProducingState();

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
            OnMemDeploymentChanged?.Invoke();

            if (buildingData != null)
            {
                MemAdded?.Invoke(buildingData.buildingType, targetMem, false);
            }
        }
    }

    /// <summary>
    /// 모든 슬롯의 생산품을 창고로 일괄 수령
    /// </summary>
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

                // 수량에 여유가 생기면 재가동
                if (slot.currentStorageCount < RanchSlotRuntime.MAX_STORAGE_PER_SLOT && slot.deployedMem != null)
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

    /// <summary>
    /// 전체 슬롯 가동 여부에 따라 시설 상태 및 이벤트 발행
    /// </summary>
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

    /// <summary>
    /// ItemCatalogManager 전용 탐색
    /// </summary>
    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        ItemData targetItem = ItemCatalogManager.Instance.FindItemData(itemId);
        if (targetItem == null)
        {
            Debug.LogError($"[ItemCatalogManager] 카탈로그에서 아이템 ID '{itemId}'에 해당하는 ItemData를 찾을 수 없습니다.");
        }

        return targetItem;
    }

    /// <summary>
    /// 굶주림으로 작업 중단 시 호출
    /// </summary>
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