using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
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
        UpdateMaxStorage();
        CheckProductionCondition();
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

    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetMem == null || targetEntry == null || buildingData == null) return false;

        int maxCapacity = ProductionCalculator.GetMaxMemCount(currentLevel);

        // 🌟 [수정]: MemData/MemId 기준 검사 제거 -> KeyId 동일 개체만 중복 검사
        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId))
        {
            Debug.LogWarning($"동일한 멤 개체(KeyID: {targetEntry.KeyId})가 이미 이 시설에 투입되어 있습니다.");
            return false;
        }

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"{targetMem.memName}(은/는) 이미 다른 시설이나 탐험대에 배치되어 있습니다.");
            return false;
        }

        if (addMems.Count >= maxCapacity)
        {
            Debug.LogWarning($"배치 인원이 가득 찼증니다.");
            return false;
        }

        if (!ProductionCalculator.CanDeployToFacility(targetMem, buildingData.buildingType))
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
            Debug.LogWarning($"{targetMem.memName}이 {requiredStat} 스탯이 없어 시설에 배치할 수 없습니다.");
            return false;
        }

        addMems.Add(targetMem);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        Debug.Log($"[생산] {targetMem.memName} 배치 성공!");

        CheckProductionCondition();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();
        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, targetMem, true);
        }

        return true;
    }

    /// <summary>
    /// 🌟 [추가]: CapturedMemEntry (KeyId) 기준 멤 제거
    /// </summary>
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

            Debug.Log($"[생산 해제] KeyID '{targetEntry.KeyId}' 시설에서 제외 완료.");

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
    }
}