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

    // 시설에 실제 멤 UI배치와 관련하여 멤 배치/해제, 시설 가동, 가동 중단에 대한 이벤트 발행
    // 해당 이벤트를 받아서 멤의 실제 UI배치 처리 및 Animation 동작진행 예정
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
            SetProducingActive(true);
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

        // 🌟 [제작대 슬롯 용량 제한 가드 정식 추가]: 제작대는 1레벨 1칸 고정이므로 기존 배치가 있다면 추가 배입을 완전히 차단합니다.
        if (addMems.Count >= 1)
        {
            Debug.LogWarning("[ProductionCraftRuntime] 제작대의 멤 배치 슬롯이 이미 가득 찼습니다.");
            return false;
        }

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
            totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(craftingDuration, addMems);
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
        if(!isProducing && currentCraftingItem == null) return;

        bool wasWorking = isProducing;

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

        if (wasWorking && buildingData != null)
        {
            FacilityStopped?.Invoke(buildingData.buildingType, addMems, FacilityStopReason.CancelCrafting);
        }
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
    ///  식량 재공급 -> 시설가동 처리
    /// </summary>
    public void ResumeWorkAfterStarvation()
    {
        if (currentCraftingItem != null && addMems.Count > 0)
        {
            SetProducingActive(true);
        }
    }
}