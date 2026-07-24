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
    [Header("시설 기반 정보 (배치 시 데이터 주입됨)")]
    public BuildingData buildingData;
    public int currentLevel = 1;

    [Header("실시간 생산 상태 변수")]
    public bool isProducing = false;

    [Tooltip("생산할 아이템 또는 레시피 ID (Item_ID)")]
    public string craftingItem;

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Tooltip("레시피 시간(time)이 별도로 없을 경우 적용될 기본 생산 시간(초)")]
    public float baseProductionTime = 30f;

    [Header("시설 내 자원 축적 현황")]
    public int currentStorageCount = 0;
    public int maxStorageCount = 100;

    [Header("현재 시설에 배치된 멤 리스트")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

    public static event Action OnMemDeploymentChanged;

    public static event Action<BuildingType, MemData, bool> MemAdded;
    public static event Action<BuildingType, List<MemData>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;

    private void Start()
    {
        EnsureBuildingData();
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

    public void LevelUp()
    {
        currentLevel++;
        UpdateMaxStorage();
        CheckProductionCondition();

        OnMemDeploymentChanged?.Invoke();
        Debug.Log($"<color=lime>[생산시설 레벨업]</color> {buildingData?.buildingName} 시설 레벨이 {currentLevel}로 증가했습니다.");
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

    private float GetBaseProductionTime()
    {
        return baseProductionTime;
    }

    public void CheckProductionCondition()
    {
        if (string.IsNullOrEmpty(craftingItem) || addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f;
            return;
        }

        float baseDuration = GetBaseProductionTime();

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

    // 🌟 [목장 기반 복사 및 커스텀]: 생산시설 멤 배치 핵심 로직
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();

        if (targetEntry == null)
        {
            Debug.LogWarning("[생산시설] ❌ CapturedMemEntry 인자가 null입니다.");
            return false;
        }

        if (buildingData == null)
        {
            Debug.LogError("[생산시설] ❌ BuildingData가 할당되어 있지 않아 배치를 진행할 수 없습니다.");
            return false;
        }

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null && !string.IsNullOrEmpty(targetEntry.MemId))
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }

        if (realMemData == null)
        {
            Debug.LogError($"[생산시설] ❌ targetEntry의 MemId('{targetEntry.MemId}')에 해당되는 MemData SO가 존재하지 않습니다.");
            return false;
        }

        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);

        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId))
        {
            Debug.LogWarning($"[생산시설] ⚠️ 동일한 멤 개체(KeyID: {targetEntry.KeyId})가 이미 이 시설에 투입되어 있습니다.");
            return false;
        }

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"[생산시설] ⚠️ {realMemData.memName}(KeyID: {targetEntry.KeyId})은/는 이미 IsActive == true 상태(다른 시설/탐험대 근무 중)입니다.");
            return false;
        }

        // 스탯 수치와 필요 스탯 종류 로그 세부 출력
        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
        int currentStatVal = realMemData.productionStats.GetStat(requiredStat);

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType))
        {
            Debug.LogWarning($"[생산시설] ⚠️ {realMemData.memName}의 {requiredStat} 스탯이 {currentStatVal}단계입니다. ({buildingData.buildingName} 배치 요구 조건: 1단계 이상)");
            return false;
        }

        if (addMems.Count >= maxCapacity && addMemEntries.Count > 0)
        {
            Debug.Log($"[생산시설] 🔄 최대 수용량({maxCapacity}) 도달로 기존 멤({addMems[0].memName})을 해제하고 새 멤({realMemData.memName})으로 교체합니다.");
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        Debug.Log($"<color=lime>[생산시설]</color> ✅ {realMemData.memName} 배치 성공! (스탯: {requiredStat} Lv.{currentStatVal})");

        CheckProductionCondition();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, realMemData, true);
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
                MemAdded?.Invoke(buildingData.buildingType, removedMem, false);
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
            float baseDuration = GetBaseProductionTime();
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    public void StoredItems()
    {
        if (currentStorageCount <= 0) return;
        if (string.IsNullOrEmpty(craftingItem)) return;

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

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindItemData(itemId);
    }

    private void SetProducingActive(bool value)
    {
        if (isProducing == value) return;
        isProducing = value;

        if (isProducing && buildingData != null)
        {
            FacilityStarted?.Invoke(buildingData.buildingType, addMems);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isProducing) return;
        isProducing = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation);
        }
        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }
}