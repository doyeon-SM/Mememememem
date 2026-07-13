using System.Collections.Generic;
using UnityEngine;
using KMS.InventoryDuped;
using HDY.Item;
using HDY.Inventory;

public class ConsumeFoodSystem : MonoBehaviour
{
    public static ConsumeFoodSystem Instance { get; private set; }

    [SerializeField] private FoodWarehouseUI foodWarehouseUI;

    [Header("소모 주기 설정 (초 단위)")]
    [SerializeField] private float consumeInterval = 60f;

    private float timer = 0f;
    private bool isWorkStoppedDueToStarvation = false;

    /// <summary>
    /// 현재 음식이 부족하여 영지 전체가 중지되었는지 여부 반환
    /// </summary>
    public bool IsWorkStoppedDueToStarvation => isWorkStoppedDueToStarvation;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (foodWarehouseUI == null) foodWarehouseUI = GetComponent<FoodWarehouseUI>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= consumeInterval)
        {
            timer = 0f;
            ProcessFoodConsumption();
        }
    }

    /// <summary>
    /// 1분 주기로 가동되는 함수.
    /// 멤의 허기량을 1분마다 채워주기.
    /// 분당 총 허기량 파악, 좌측에 배치된 음식 아이템의 총 포만도 계산
    /// 총 허기량 > 포만감 => 작업 중지처리 / 포만감 > 총 허기량 => 음식 감산 처리
    /// 1분 주기로 가동되거나, 음식을 좌측 창고로 드래그앤드롭하여 보관 상태가 바뀔 때 호출하여 사전 검증할 수 있습니다.
    /// </summary>
    public void ProcessFoodConsumption()
    {
        if (foodWarehouseUI == null) return;

        int totalHunger = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;

        int totalSatietyAvailable = CalculateTotalStorageSatiety(out List<int> validFoodIndices);

        Debug.Log($"[ConsumeFoodSystem] 소모 주기 도래 -> 현재 총 허기량: {totalHunger} / 창고 잔여 포만감: {totalSatietyAvailable}");

        if (totalHunger > totalSatietyAvailable)
        {
            if (!isWorkStoppedDueToStarvation)
            {
                isWorkStoppedDueToStarvation = true;
                SetAllFacilitiesWorkingState(false); 
                Debug.LogWarning("<color=red><b>[영지 경보]</b></color> 잔여 음식의 총 포만감이 허기량보다 부족합니다! 모든 시설의 작업이 일시 중지됩니다. (기존 진행도 보존)");
            }
            return;
        }

        if (isWorkStoppedDueToStarvation)
        {
            isWorkStoppedDueToStarvation = false;
            SetAllFacilitiesWorkingState(true); 
            Debug.Log("<color=lime><b>[영지 정상화]</b></color> 음식을 충분히 확보했습니다. 모든 시설이 다시 가동을 시작합니다.");

            timer = 0f;
        }

        if (totalHunger > 0)
        {
            ConsumeFoodFromStorage(totalHunger, validFoodIndices);
        }
    }

    /// <summary>
    /// 좌측 창고에 추가한 모든 아이템의 포만감 계산
    /// </summary>
    private int CalculateTotalStorageSatiety(out List<int> validFoodIndices)
    {
        validFoodIndices = new List<int>();
        int sumSatiety = 0;

        InventoryContainer storageContainer = foodWarehouseUI.FoodStorageContainer;
        if (storageContainer == null || storageContainer.slots == null) return 0;

        for (int i = 0; i < storageContainer.slots.Length; i++)
        {
            ItemStack slot = storageContainer.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData itemData = foodWarehouseUI.CatalogManager.FindItemData(slot.itemId);
            if (itemData == null || itemData.EatEffects == null) continue;

            foreach (ItemEffect effect in itemData.EatEffects)
            {
                if (effect != null && effect.Effect == EffectType.Satiety && effect.Value > 0)
                {
                    sumSatiety += ((int)effect.Value * slot.amount);
                    validFoodIndices.Add(i);
                    break;
                }
            }
        }

        return sumSatiety;
    }

    /// <summary>
    /// 왼쪽 위부터 순서대로 포만감을 정량 계산하며 실물 수량을 차감합니다.
    /// </summary>
    private void ConsumeFoodFromStorage(int hungerToConsume, List<int> foodIndices)
    {
        InventoryContainer storageContainer = foodWarehouseUI.FoodStorageContainer;
        int remainingHunger = hungerToConsume;

        foreach (int index in foodIndices)
        {
            ItemStack slot = storageContainer.slots[index];
            ItemData itemData = foodWarehouseUI.CatalogManager.FindItemData(slot.itemId);

            int singleSatiety = 0;
            foreach (ItemEffect effect in itemData.EatEffects)
            {
                if (effect != null && effect.Effect == EffectType.Satiety)
                {
                    singleSatiety = (int)effect.Value;
                    break;
                }
            }

            if (singleSatiety <= 0) continue;

            while (slot.amount > 0 && remainingHunger > 0)
            {
                slot.amount--;           
                remainingHunger -= singleSatiety; 
            }

            if (slot.amount <= 0)
            {
                slot.Clear();
            }

            if (remainingHunger <= 0) break;
        }

        foodWarehouseUI.RefreshAllPanelsAndSlots();
    }

    /// <summary>
    /// 허기량을 충족 여부에 따른 영지 전체의 생산/제작 시설 작동 변수(isProducing)를 켜고 끕니다.
    /// </summary>
    private void SetAllFacilitiesWorkingState(bool isWorking)
    {
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null) continue;

            if (!isWorking)
            {
                facility.isProducing = false; 
            }
            else
            {
                facility.CheckProductionCondition();
            }
        }

        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null) continue;

            if (!isWorking)
            {
                craft.isProducing = false; 
            }
            else
            {
                if (craft.currentCraftingItem != null && craft.DeployedMems.Count > 0)
                {
                    craft.isProducing = true;
                }
            }
        }
    }
}