using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
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

    // 🌟 [수정]: ItemData 대신 아이템 ID(string)로 저장
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

    // 시설에 실제 멤 UI배치와 관련하여 멤 배치/해제, 시설 가동, 가동 중단에 대한 이벤트 발행
    public static event Action<BuildingType, MemData, bool> MemAdded;
    public static event Action<BuildingType, List<MemData>> FacilityStarted;
    public static event Action<BuildingType, List<MemData>, FacilityStopReason> FacilityStopped;

    private void Start()
    {
        maxStorageCount = 10;
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

    /// <summary>
    /// 제작 버튼 클릭시 동작되는 함수 (string ID 기반)
    /// </summary>
    public void SelectAndStartCrafting(string targetItemId, int quantity)
    {
        if (string.IsNullOrEmpty(targetItemId) || addMems.Count == 0) return;

        currentCraftingItem = targetItemId;
        targetQuantity = quantity;
        remainingQuantity = quantity;
        currentProgressTime = 0f;

        // 🌟 [수정]: ItemCatalogManager에서 RecipeData를 조회하여 time 기반 소요시간 산출
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

    /// <summary>
    /// ItemData 오버로드 버전 (호환성 유지)
    /// </summary>
    public void SelectAndStartCrafting(ItemData targetItem, int quantity)
    {
        if (targetItem == null) return;
        SelectAndStartCrafting(targetItem.Item_ID, quantity);
    }

    /// <summary>
    /// 제작 도중 멤 슬롯 상태 변경(교체, 제거)시 진행 비율을 보존 or 제거 + 시간 재 계산처리
    /// </summary>
    private void RecalculateCraftingTimer()
    {
        if (addMems.Count == 0)
        {
            isProducing = false;
            currentProgressTime = 0f;
            currentCraftingItem = null;
            remainingQuantity = 0;
            targetQuantity = 1;
            Debug.LogWarning("가동 중이던 제작 공정이 취소되었습니다.");
            return;
        }

        if (!string.IsNullOrEmpty(currentCraftingItem))
        {
            float currentProgressPercent = totalRequiredTime > 0f ? (currentProgressTime / totalRequiredTime) : 0f;

            // 🌟 [수정]: RecipeData.time 동적 적용
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

    /// <summary>
    /// Drop으로 멤배치하였을 때 동작하는 함수  
    /// </summary>
    public bool TryAddMem(MemData targetMem, CapturedMemEntry targetEntry)
    {
        if (targetMem == null || buildingData == null) return false;

        if (addMems.Count >= 1)
        {
            Debug.LogWarning("[ProductionCraftRuntime] 제작대의 멤 배치 슬롯이 이미 가득 찼습니다.");
            return false;
        }

        if (addMems.Contains(targetMem)) return false;

        if (targetEntry.IsActive)
        {
            Debug.LogWarning($"{targetMem.memName}(은/는) 이미 다른 시설이나 탐험대에 배치되어 있습니다.");
            return false;
        }

        if (!ProductionCalculator.CanDeployToFacility(targetMem, buildingData.buildingType))
        {
            ProductionStatType requiredStat = ProductionCalculator.GetRequiredStatType(buildingData.buildingType);
            return false;
        }

        addMems.Add(targetMem);
        addMemEntries.Add(targetEntry);
        targetEntry.IsActive = true;

        RecalculateCraftingTimer();

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

        OnMemDeploymentChanged?.Invoke();

        if (buildingData != null)
        {
            MemAdded?.Invoke(buildingData.buildingType, targetMem, true);
        }

        return true;
    }

    /// <summary>
    /// 멤 제거 + 시간 재조정처리
    /// </summary>
    public void RemoveMem(MemData targetMem)
    {
        if (addMems.Contains(targetMem))
        {
            int index = addMems.IndexOf(targetMem);
            if (index >= 0 && index < addMemEntries.Count)
            {
                addMemEntries[index].IsActive = false;
                addMemEntries.RemoveAt(index);
            }
            addMems.RemoveAt(index);

            RecalculateCraftingTimer();

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

            OnMemDeploymentChanged?.Invoke();

            if (buildingData != null)
            {
                MemAdded?.Invoke(buildingData.buildingType, targetMem, false);
            }
        }
    }

    /// <summary>
    /// 제작 물품이 100%되어 완료되었을 때 생산 물품 수량 증가 + 다음 생산 진행처리
    /// </summary>
    private void CompleteCraftingUnit()
    {
        currentStorageCount++;
        remainingQuantity--;

        currentProgressTime = 0f;

        if (remainingQuantity > 0)
        {
            // 🌟 [수정]: RecipeData.time 동적 적용
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
    }

    /// <summary>
    /// 제작 도중 취소 버튼 클릭 시 누적 완성 개수를 판별하여 환불 처리
    /// </summary>
    public void CancelCrafting()
    {
        if (!isProducing && string.IsNullOrEmpty(currentCraftingItem)) return;

        bool wasWorking = isProducing;

        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();

        if (currentStorageCount > 0)
        {
            // 🌟 [수정]: ItemCatalogManager에서 ItemData 검색
            ItemData itemData = FindItemDataInCatalog(currentCraftingItem);
            if (inventory != null && itemData != null)
            {
                inventory.AddItem(itemData, currentStorageCount);
            }
        }

        if (remainingQuantity > 0)
        {
            // 🌟 [수정]: ItemCatalogManager를 통해서만 RecipeData 탐색
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

    /// <summary>
    /// 수령 보상 처리
    /// </summary>
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

    /// <summary>
    /// ItemCatalogManager 전용 ItemData 탐색 (없을 시 에러 로그)
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
    /// ItemCatalogManager 전용 RecipeData 탐색 (없을 시 에러 로그)
    /// </summary>
    private RecipeData FindRecipeDataInCatalog(string recipeItemId)
    {
        if (string.IsNullOrEmpty(recipeItemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 레시피 '{recipeItemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        RecipeData targetRecipe = ItemCatalogManager.Instance.FindRecipeData(recipeItemId);
        if (targetRecipe == null)
        {
            Debug.LogError($"[ItemCatalogManager] 카탈로그에서 레시피 ID '{recipeItemId}'에 해당하는 RecipeData를 찾을 수 없습니다.");
        }

        return targetRecipe;
    }

    /// <summary>
    /// 시설 가동이 시작될 때 이벤트 발행용 함수
    /// </summary>
    private void SetProducingActive(bool value)
    {
        if (isProducing == value) return;
        isProducing = value;

        if (isProducing && buildingData != null)
        {
            FacilityStarted?.Invoke(buildingData.buildingType, addMems);
        }
    }

    /// <summary>
    /// 식량 부족으로 인해 가동 중지시 가동 중지 이벤트 발행
    /// </summary>
    public void StopWorkDueToStarvation()
    {
        if (!isProducing) return;
        isProducing = false;

        if (buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.Starvation);
        }
    }

    /// <summary>
    /// 식량 재공급 -> 시설가동 처리
    /// </summary>
    public void ResumeWorkAfterStarvation()
    {
        if (!string.IsNullOrEmpty(currentCraftingItem) && addMems.Count > 0)
        {
            SetProducingActive(true);
        }
    }
}