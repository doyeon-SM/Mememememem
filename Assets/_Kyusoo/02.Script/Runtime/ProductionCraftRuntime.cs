using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using HDY.Recipe;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ProductionCraftRuntime : MonoBehaviour
{
    [Header("제작 시설 기반 정보")]
    public BuildingData buildingData;

    [Header("제작 상태 여부")]
    public bool isProducing = false;

    public string currentCraftingItem;

    public float totalRequiredTime;
    public float currentProgressTime = 0f;

    [Header("제작 수량 데이터: 목표 설정 수량, 남은 수량")]
    public int targetQuantity = 1;
    public int remainingQuantity = 0;

    [Header("제작 완료 데이터: 제작 완료된 수량, 최대 수량")]
    public int currentStorageCount = 0;
    public int maxStorageCount;

    [Header("제작대에 배치된 멤 정보")]
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
        if (!isProducing) return;

        if (currentStorageCount >= maxStorageCount)
        {
            isProducing = false;
            return;
        }

        currentProgressTime += Time.deltaTime;

        if (currentProgressTime >= totalRequiredTime)
        {
            CompleteCraftingUnit();
        }
    }

    public void SelectAndStartCrafting(string targetItemId, int quantity)
    {
        if (string.IsNullOrEmpty(targetItemId) || addMems.Count == 0) return;

        currentCraftingItem = targetItemId;
        targetQuantity = quantity;
        remainingQuantity = quantity;
        currentProgressTime = 0f;

        RecipeData recipe = FindRecipeDataInCatalog(currentCraftingItem);
        float baseDuration = recipe != null ? recipe.time : 20f;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            SetProducingActive(true);
        }
        else
        {
            isProducing = false;
        }
    }

    public void SelectAndStartCrafting(ItemData targetItem, int quantity)
    {
        if (targetItem == null) return;
        SelectAndStartCrafting(targetItem.Item_ID, quantity);
    }

    private void RecalculateCraftingTimer()
    {
        if (addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f;
            currentCraftingItem = null;
            remainingQuantity = 0;
            targetQuantity = 1;
            return;
        }

        if (!string.IsNullOrEmpty(currentCraftingItem))
        {
            float currentProgressPercent = totalRequiredTime > 0f ? (currentProgressTime / totalRequiredTime) : 0f;

            RecipeData recipe = FindRecipeDataInCatalog(currentCraftingItem);
            float baseDuration = recipe != null ? recipe.time : 20f;

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
    }

    // 🌟 [목장 기반 복사 및 커스텀]: 제작대 멤 배치 로직
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        EnsureBuildingData();

        if (targetEntry == null)
        {
            Debug.LogWarning("[제작대] ❌ CapturedMemEntry 인자가 null입니다.");
            return false;
        }

        if (buildingData == null)
        {
            Debug.LogError("[제작대] ❌ BuildingData가 할당되어 있지 않아 배치를 진행할 수 없습니다.");
            return false;
        }

        MemData realMemData = targetMem;
        if ((realMemData == null || string.IsNullOrEmpty(realMemData.memId)) && MemCatalogManager.Instance != null && !string.IsNullOrEmpty(targetEntry.MemId))
        {
            realMemData = MemCatalogManager.Instance.FindMemData(targetEntry.MemId);
        }

        if (realMemData == null)
        {
            Debug.LogError($"[제작대] ❌ targetEntry의 MemId('{targetEntry.MemId}')에 해당되는 MemData SO가 존재하지 않습니다.");
            return false;
        }

        if (addMemEntries.Exists(e => e != null && e.KeyId == targetEntry.KeyId))
        {
            Debug.LogWarning($"[제작대] ⚠️ 동일한 멤 개체(KeyID: {targetEntry.KeyId})가 이미 배치되어 있습니다.");
            return false;
        }

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"[제작대] ⚠️ {realMemData.memName}(KeyID: {targetEntry.KeyId})은/는 이미 IsActive == true 상태(다른 시설/탐험대 근무 중)입니다.");
            return false;
        }

        ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
        int currentStatVal = realMemData.productionStats.GetStat(requiredStat);

        if (!ProductionCalculator.CanDeployToFacility(realMemData, buildingData.buildingType))
        {
            Debug.LogWarning($"[제작대] ⚠️ {realMemData.memName}의 {requiredStat} 스탯이 {currentStatVal}단계입니다. ({buildingData.buildingName} 배치 요구 조건: 1단계 이상)");
            return false;
        }

        if (addMems.Count >= 1 && addMemEntries.Count > 0)
        {
            Debug.Log($"[제작대] 🔄 기존 멤({addMems[0].memName})을 자동 해제하고 새 멤({realMemData.memName})으로 교체합니다.");
            RemoveMem(addMemEntries[0]);
        }

        addMems.Add(realMemData);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;
        Debug.Log($"<color=lime>[제작대]</color> ✅ {realMemData.memName} 배치 성공! (스탯: {requiredStat} Lv.{currentStatVal})");

        RecalculateCraftingTimer();

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

            RecalculateCraftingTimer();

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

    private void CompleteCraftingUnit()
    {
        currentStorageCount++;
        remainingQuantity--;

        currentProgressTime = 0f;

        if (remainingQuantity > 0)
        {
            RecipeData recipe = FindRecipeDataInCatalog(currentCraftingItem);
            float baseDuration = recipe != null ? recipe.time : 20f;

            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, addMems);
        }
        else
        {
            isProducing = false;

            if (buildingData != null)
            {
                FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CompleteCrafting);
            }
        }

        FacilityCollectManager.Instance?.NotifyFacilityChanged(this);
    }

    public void CancelCrafting()
    {
        if (!isProducing && string.IsNullOrEmpty(currentCraftingItem)) return;

        bool wasWorking = isProducing;

        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();

        if (currentStorageCount > 0)
        {
            ItemData itemData = FindItemDataInCatalog(currentCraftingItem);
            if (inventory != null && itemData != null)
            {
                inventory.AddItem(itemData, currentStorageCount);
            }
        }

        if (remainingQuantity > 0)
        {
            RecipeData matchedRecipe = FindRecipeDataInCatalog(currentCraftingItem);

            if (matchedRecipe != null && matchedRecipe.Requset_Items_ID != null)
            {
                foreach (var req in matchedRecipe.Requset_Items_ID)
                {
                    if (req == null || string.IsNullOrEmpty(req.Item_ID)) continue;

                    int refundAmount = req.Amount * remainingQuantity;
                    if (refundAmount <= 0) continue;

                    if (inventory != null)
                    {
                        refundAmount = inventory.AddItem(req.Item_ID, refundAmount);
                    }
                    if (refundAmount > 0 && warehouse != null)
                    {
                        warehouse.AddItem(req.Item_ID, refundAmount);
                    }
                }
            }
        }

        isProducing = false;
        currentStorageCount = 0;
        remainingQuantity = 0;
        targetQuantity = 1;
        currentProgressTime = 0f;
        currentCraftingItem = null;

        if (wasWorking && buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CancelCrafting);
        }
    }

    public bool CollectCraftedItems()
    {
        if (currentStorageCount <= 0) return false;

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        ItemData itemData = FindItemDataInCatalog(currentCraftingItem);

        if (inventory != null && itemData != null)
        {
            int remaining = inventory.AddItem(itemData, currentStorageCount);
            currentStorageCount = remaining;
        }

        if (currentStorageCount > 0) return false;

        if (remainingQuantity <= 0 && !isProducing)
        {
            currentCraftingItem = null;
            targetQuantity = 1;
            return true;
        }

        return false;
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

    private RecipeData FindRecipeDataInCatalog(string recipeItemId)
    {
        if (string.IsNullOrEmpty(recipeItemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 레시피 '{recipeItemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        return ItemCatalogManager.Instance.FindRecipeData(recipeItemId);
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

    public void ResumeWorkAfterStarvation()
    {
        if (!string.IsNullOrEmpty(currentCraftingItem) && addMems.Count > 0)
        {
            SetProducingActive(true);
        }
    }
}