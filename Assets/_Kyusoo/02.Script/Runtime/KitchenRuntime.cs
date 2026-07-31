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

/// <summary>
/// 주방 시설의 런타임 로직을 담당합니다.
/// 레시피 진행, 멤 배치, 요리 취소/수령 트랜잭션 및 발전기 전력 선불 소모(순차 차감)를 관리합니다.
/// </summary>
public class KitchenRuntime : MonoBehaviour
{
    [Header("시설 기본 정보")]
    public BuildingData buildingData;
    public CookingFacilityData cookingFacilityData;

    [Header("요리 가동 상태")]
    public bool isCooking = false;
    public bool isPowerPaused = false; // 전력 부족으로 일시 정지된 상태 여부
    public string currentCookingItem;  // 현재 요리 중인 Result_Item_ID

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("요리 수량 데이터")]
    public int targetQuantity = 1;
    public int remainingQuantity = 0;

    [Header("요리 완료 데이터")]
    public int currentStorageCount = 0;
    public int maxStorageCount = 10;

    [Header("전력 소모 설정 (발전기 연동)")]
    [Tooltip("전력 소모 주기 (초)")]
    public float powerConsumeInterval = 20f;
    [Tooltip("1회 소모 주기당 선불 차감할 전력량 (Watt)")]
    public int powerConsumeAmount = 10;

    private float powerTimer = 0f;

    [Header("시설에 배치된 멤 정보 (최대 1마리)")]
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

    private void Update()
    {
        if (!isCooking) return;

        // 보관함이 가득 차면 가동 중지
        if (currentStorageCount >= maxStorageCount)
        {
            isCooking = false;
            return;
        }

        // 1. 정상 가동 상태 (전력 선불 지불 완료)
        if (!isPowerPaused)
        {
            currentProgressTime += Time.deltaTime;
            powerTimer += Time.deltaTime;

            // 선불 지불한 20초 기간이 만료되면 다음 20초분 전력 선불 차감 시도
            if (powerTimer >= powerConsumeInterval)
            {
                powerTimer = 0f;

                if (!ConsumeTerritoryPower(powerConsumeAmount))
                {
                    isPowerPaused = true;
                    Debug.LogWarning($"<color=yellow>[주방]</color> 연장 전력 부족: '{buildingData?.buildingName}' 요리가 일시 정지됩니다.");
                }
                else
                {
                    Debug.Log($"<color=lime>[주방]</color> 추가 전력({powerConsumeAmount}W) 선불 차감 성공. 요리를 계속합니다.");
                }
            }

            // 요리 1단위 완성 체크 (전력이 정지되지 않은 경우만)
            if (!isPowerPaused && currentProgressTime >= totalRequiredTime)
            {
                CompleteCookingUnit();
            }
        }
        // 2. 전력 부족 일시 정지 상태: 전력 충전을 감지하여 선불 차감 재시도
        else
        {
            if (GetTotalTerritoryPower() >= powerConsumeAmount)
            {
                if (ConsumeTerritoryPower(powerConsumeAmount))
                {
                    isPowerPaused = false;
                    powerTimer = 0f;
                    Debug.Log($"<color=lime>[주방]</color> 전력 충전 감지 및 선불 차감 성공! 요리를 자동 재개합니다.");
                }
            }
        }
    }

    /// <summary>
    /// 영지 내에 배치된 발전기(GeneratorRuntime)들의 축적된 전력을 A -> B -> C 순으로 순차 차감합니다.
    /// </summary>
    public bool ConsumeTerritoryPower(int amount)
    {
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        int remainingToConsume = amount;

        // 영지 전체 총 전력이 요구량보다 적으면 차감 불가능
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

    /// <summary>
    /// 영지 내 모든 발전기들에 축적되어 있는 총 전력량(Watt)을 합산하여 반환합니다.
    /// </summary>
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

    /// <summary>
    /// 선택한 요리와 수량으로 요리를 시작합니다.
    /// </summary>
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
                Debug.LogWarning($"<color=yellow>[주방]</color> 시작 전력 부족: '{buildingData?.buildingName}' 요리가 일시 정지 상태로 시작합니다.");
            }
            else
            {
                isPowerPaused = false;
                Debug.Log($"<color=lime>[주방]</color> 시작 전력({powerConsumeAmount}W) 선불 차감 성공. 요리를 시작합니다.");
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

            RecalculateCookingTimer();

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
                FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CompleteCrafting);
            }
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    /// <summary>
    /// 요리를 취소합니다. 이미 조리 완료된 음식은 인벤토리로 수령하고, 미완성 수량만큼 재료를 환불합니다.
    /// </summary>
    public void CancelCooking()
    {
        if (!isCooking && string.IsNullOrEmpty(currentCookingItem)) return;

        bool wasWorking = isCooking;
        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();

        // 1. 완성분 음식 수령
        if (currentStorageCount > 0)
        {
            ItemData dishItem = FindItemDataInCatalog(currentCookingItem);
            if (inventory != null && dishItem != null)
            {
                inventory.AddItem(dishItem, currentStorageCount);
            }
        }

        // 2. 미완성분 수량(remainingQuantity)에 대한 재료 환불
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
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CancelCrafting);
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
            FacilityStarted?.Invoke(buildingData.buildingType, addMems);
        }
    }

    public void StopWorkDueToStarvation()
    {
        if (!isCooking) return;
        isCooking = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation);
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