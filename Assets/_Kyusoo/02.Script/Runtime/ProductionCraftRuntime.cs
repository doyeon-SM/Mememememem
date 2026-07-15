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

    public ItemData currentCraftingItem;

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

    // 임시. 생산 소요 시간
    private float craftingDuration = 20f;

    public static event Action OnMemDeploymentChanged;

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
    /// 제작 버튼 클릭시 동작되는 함수
    /// 총 소요시간(1개당 제작 시간 - 멤 등급으로인해 감속되는 시간)을 기반으로 제작을 진행
    /// 작업상태를 True로 변경
    /// </summary>
    public void SelectAndStartCrafting(ItemData targetItem, int quantity)
    {
        if (targetItem == null || addMems.Count == 0) return;

        currentCraftingItem = targetItem;
        targetQuantity = quantity;
        remainingQuantity = quantity;
        currentProgressTime = 0f;

        totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);

        if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
        {
            isProducing = true;
        }
        else
        {
            isProducing = false;
        }
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

        if (currentCraftingItem != null)
        {
            float currentProgressPercent = totalRequiredTime > 0f ? (currentProgressTime / totalRequiredTime) : 0f;
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
            currentProgressTime = totalRequiredTime * currentProgressPercent;

            if (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation)
            {
                isProducing = true;
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

        if (addMems.Contains(targetMem)) return false;

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
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
        }
        else
        {
            isProducing = false;
        }
    }

    /// <summary>
    /// 제작 도중 취소 버튼 클릭 시 누적 완성 개수를 판별하여 환불 처리
    /// </summary>
    public void CancelCrafting()
    {
        if(!isProducing && currentCraftingItem == null) return;

        var inventory = FindFirstObjectByType<PlayerInventory>();
        var warehouse = FindFirstObjectByType<WarehouseInventory>();
        
        if (currentStorageCount > 0)
        {
            if (inventory != null)
            {
                inventory.AddItem(currentCraftingItem, currentStorageCount);
            }
        }

        if (remainingQuantity > 0)
        {
            RecipeData matchedRecipe = null;
            RecipeData[] allRecipes = Resources.FindObjectsOfTypeAll<RecipeData>();
            foreach (RecipeData recipe in allRecipes)
            {
                if (recipe != null && recipe.Recipe_Item_ID == currentCraftingItem.Item_ID)
                {
                    matchedRecipe = recipe;
                    break;
                }
            }

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
    }

    /// <summary>
    /// 수령 보상 처리
    /// 0개일 때, return;
    /// 전체 제작 예정 수량보다 적은상태에서 수령할 때, 수령 + 화면유지
    /// 전체 제작 수량을 다 제작한 상태에서 수령하면 수령 + 초기화면 전환
    /// </summary>
    public bool CollectCraftedItems()
    {
        if (currentStorageCount <= 0) return false;

        //
        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
        if(inventory != null)
        {
            int remaining = inventory.AddItem(currentCraftingItem, currentStorageCount);
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
}