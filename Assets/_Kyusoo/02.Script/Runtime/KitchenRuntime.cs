using HDY.Capture;
using HDY.Cook;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

public class KitchenRuntime : MonoBehaviour
{
    [Header("시설 기본 정보")]
    public BuildingData buildingData;
    public CookingFacilityData cookingFacilityData;

    [Header("요리 가동 상태")]
    public bool isCooking = false;
    public bool isPowerPaused = false;
    public string currentCookingItem;

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("요리 수량 데이터")]
    public int targetQuantity = 1;
    public int remainingQuantity = 0;

    [Header("요리 완료 데이터")]
    public int currentStorageCount = 0;
    public int maxStorageCount = 10;

    [Header("전력 소모 설정 (발전기 연동)")]
    public float powerConsumeInterval = 20f;
    public int powerConsumeAmount = 10;

    private float powerTimer = 0f;

    [Header("시설에 배치된 멤 정보 (최대 1마리)")]
    [SerializeField] private List<MemData> addMems = new List<MemData>();
    [SerializeField] private List<CapturedMemEntry> addMemEntries = new List<CapturedMemEntry>();

    public List<MemData> DeployedMems => addMems;
    public List<CapturedMemEntry> DeployedMemEntries => addMemEntries;

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

    private void Start()
    {
        EnsureBuildingData();
        CacheMemPositions();
        maxStorageCount = 10;

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

    private void Update()
    {
        if (!isCooking) return;

        if (currentStorageCount >= maxStorageCount)
        {
            isCooking = false;
            return;
        }

        if (!isPowerPaused)
        {
            currentProgressTime += Time.deltaTime;
            powerTimer += Time.deltaTime;

            if (powerTimer >= powerConsumeInterval)
            {
                powerTimer = 0f;

                if (!ConsumeTerritoryPower(powerConsumeAmount))
                {
                    isPowerPaused = true;
                }
            }

            if (!isPowerPaused && currentProgressTime >= totalRequiredTime)
            {
                CompleteCookingUnit();
            }
        }
        else
        {
            if (GetTotalTerritoryPower() >= powerConsumeAmount)
            {
                if (ConsumeTerritoryPower(powerConsumeAmount))
                {
                    isPowerPaused = false;
                    powerTimer = 0f;
                }
            }
        }
    }

    public bool ConsumeTerritoryPower(int amount)
    {
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        int remainingToConsume = amount;

        if (GetTotalTerritoryPower() < amount) return false;

        foreach (var gen in generators)
        {
            if (gen == null || gen.currentPowerStorage <= 0) continue;

            int consumed = gen.ConsumePower(remainingToConsume);
            remainingToConsume -= consumed;

            if (remainingToConsume <= 0) break;
        }

        return remainingToConsume <= 0;
    }

    public int GetTotalTerritoryPower()
    {
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        int totalPower = 0;

        foreach (var gen in generators)
        {
            if (gen != null)
            {
                totalPower += gen.currentPowerStorage;
            }
        }

        return totalPower;
    }

    public void SelectAndStartCooking(string targetItemId, int quantity)
    {
        if (string.IsNullOrEmpty(targetItemId) || addMems.Count == 0) return;

        currentCookingItem = targetItemId;
        targetQuantity = quantity;
        remainingQuantity = quantity;
        currentProgressTime = 0f;
        powerTimer = 0f;

        CookRecipeData recipe = FindCookRecipeDataInCatalog(currentCookingItem);
        float baseDuration = recipe != null ? recipe.Time : 15f;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            SetCookingActive(true);

            if (!ConsumeTerritoryPower(powerConsumeAmount))
            {
                isPowerPaused = true;
            }
            else
            {
                isPowerPaused = false;
            }
        }
        else
        {
            isCooking = false;
            isPowerPaused = false;
        }
    }

    public void SelectAndStartCooking(ItemData targetItem, int quantity)
    {
        if (targetItem == null) return;
        SelectAndStartCooking(targetItem.Item_ID, quantity);
    }

    private void RecalculateCookingTimer()
    {
        if (addMems.Count == 0)
        {
            isCooking = false;
            isPowerPaused = false;
            currentProgressTime = 0f;
            currentCookingItem = null;
            remainingQuantity = 0;
            targetQuantity = 1;
            return;
        }

        if (!string.IsNullOrEmpty(currentCookingItem))
        {
            float currentProgressPercent = totalRequiredTime > 0f ? (currentProgressTime / totalRequiredTime) : 0f;

            CookRecipeData recipe = FindCookRecipeDataInCatalog(currentCookingItem);
            float baseDuration = recipe != null ? recipe.Time : 15f;

            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
            currentProgressTime = totalRequiredTime * currentProgressPercent;

            if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
            {
                SetCookingActive(true);
            }
            else
            {
                isCooking = false;
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

        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId)) return false;
        if (targetEntry.IsActive) return false;

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType)) return false;

        if (addMems.Count >= 1 && addMemEntries.Count > 0)
        {
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;

        RecalculateCookingTimer();

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

            RecalculateCookingTimer();

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

    private void CompleteCookingUnit()
    {
        currentStorageCount++;
        remainingQuantity--;
        currentProgressTime = 0f;

        if (remainingQuantity > 0)
        {
            CookRecipeData recipe = FindCookRecipeDataInCatalog(currentCookingItem);
            float baseDuration = recipe != null ? recipe.Time : 15f;
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
        }
        else
        {
            isCooking = false;
            isPowerPaused = false;
            powerTimer = 0f;

            if (buildingData != null)
            {
                FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CompleteCrafting, MemPositions);
            }
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    public void CancelCooking()
    {
        if (!isCooking && string.IsNullOrEmpty(currentCookingItem)) return;

        bool wasWorking = isCooking;
        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();

        if (currentStorageCount > 0)
        {
            ItemData dishItem = FindItemDataInCatalog(currentCookingItem);
            if (inventory != null && dishItem != null)
            {
                inventory.AddItem(dishItem, currentStorageCount);
            }
        }

        if (remainingQuantity > 0)
        {
            List<string> ingredientIds = GetIngredientIdsForCooking(currentCookingItem);

            foreach (string matId in ingredientIds)
            {
                if (string.IsNullOrEmpty(matId)) continue;

                int refundAmount = remainingQuantity;
                if (refundAmount <= 0) continue;

                if (inventory != null)
                {
                    refundAmount = inventory.AddItem(matId, refundAmount);
                }
                if (refundAmount > 0 && warehouse != null)
                {
                    warehouse.AddItem(matId, refundAmount);
                }
            }
        }

        isCooking = false;
        isPowerPaused = false;
        currentStorageCount = 0;
        remainingQuantity = 0;
        targetQuantity = 1;
        currentProgressTime = 0f;
        powerTimer = 0f;
        currentCookingItem = null;

        if (wasWorking && buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CancelCrafting, MemPositions);
        }
    }

    public bool CollectCookedItems()
    {
        if (currentStorageCount <= 0) return false;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        ItemData dishItem = FindItemDataInCatalog(currentCookingItem);

        if (inventory != null && dishItem != null)
        {
            int remaining = inventory.AddItem(dishItem, currentStorageCount);
            currentStorageCount = remaining;
        }

        if (currentStorageCount > 0) return false;

        if (remainingQuantity <= 0 && !isCooking)
        {
            currentCookingItem = null;
            targetQuantity = 1;
            return true;
        }

        return false;
    }

    public List<string> GetIngredientIdsForCooking(string resultItemId)
    {
        if (string.IsNullOrEmpty(resultItemId)) return new List<string>();

        CookRecipeData cookRecipe = FindCookRecipeDataInCatalog(resultItemId);
        if (cookRecipe != null && cookRecipe.Ingredient_Item_IDs != null)
        {
            return cookRecipe.Ingredient_Item_IDs;
        }

        return new List<string>();
    }

    private ItemData FindItemDataInCatalog(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        var catalog = ItemCatalogManager.Resolve(null);
        return catalog != null ? catalog.FindItemData(itemId) : null;
    }

    private CookRecipeData FindCookRecipeDataInCatalog(string resultItemId)
    {
        if (string.IsNullOrEmpty(resultItemId)) return null;
        var catalog = ItemCatalogManager.Resolve(null);
        return catalog != null ? catalog.FindCookRecipeData(resultItemId) : null;
    }

    private void SetCookingActive(bool value)
    {
        if (isCooking == value) return;
        isCooking = value;

        if (isCooking && buildingData != null)
        {
            FacilityStarted?.Invoke(buildingData.buildingType, addMems, MemPositions);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isCooking) return;
        isCooking = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation, MemPositions);
        }
    }

    public void ResumeWorkAfterStarvation()
    {
        if (!string.IsNullOrEmpty(currentCookingItem) && addMems.Count > 0)
        {
            SetCookingActive(true);

            if (!ConsumeTerritoryPower(powerConsumeAmount))
            {
                isPowerPaused = true;
            }
            else
            {
                isPowerPaused = false;
                powerTimer = 0f;
            }
        }
    }
}