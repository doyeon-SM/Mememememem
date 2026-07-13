using HDY.Inventory;
using HDY.Item;
using KMS.InventoryDuped;
using System;
using System.Collections.Generic;
using UnityEngine;

public class ConsumeFoodSystem : MonoBehaviour
{
    public static ConsumeFoodSystem Instance { get; private set; }

    [SerializeField] private FoodWarehouseUI foodWarehouseUI;

    [Header("소모 주기 설정 (초 단위)")]
    [SerializeField] private float consumeInterval = 60f;

    private float timer = 0f;
    private bool isWorkStoppedDueToStarvation = false;

    private bool isWaitingForMissedMeal = false;

    private int maxSatiety = 0;
    private int currentSatiety = 0;

    /// <summary>
    /// 현재 음식이 부족하여 영지 전체가 중지되었는지 여부 반환
    /// </summary>
    public bool IsWorkStoppedDueToStarvation => isWorkStoppedDueToStarvation;
    public int MaxSatiety => maxSatiety;
    public int CurrentSatiety => currentSatiety;

    public event Action<int, int> OnFoodAmountChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        if (foodWarehouseUI == null) foodWarehouseUI = GetComponent<FoodWarehouseUI>();
    }

    private void Start()
    {
        int totalSatiety = CalculateTotalStorageSatiety(out _);
        maxSatiety = totalSatiety;
        currentSatiety = totalSatiety;
        NotifyFoodStatusChanged();
    }

    private void Update()
    {
        if (!isWorkStoppedDueToStarvation)
        {
            timer += Time.deltaTime;
            if (timer >= consumeInterval)
            {
                timer = 0f;
                ProcessTimerTick(); 
            }
        }
    }

    private void ProcessTimerTick()
    {
        if (foodWarehouseUI == null) return;

        int totalHunger = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        int totalSatietyAvailable = CalculateTotalStorageSatiety(out List<int> validFoodIndices);

        if (totalHunger > totalSatietyAvailable)
        {
            isWorkStoppedDueToStarvation = true;
            isWaitingForMissedMeal = true;

            SetAllFacilitiesWorkingState(false);
            Debug.LogWarning("<color=red><b>[영지 경보]</b></color> 1분 주기 도래: 음식 부족으로 모든 시설 가동이 정지됩니다.");

            currentSatiety = totalSatietyAvailable;
        }
        else
        {
            if (totalHunger > 0)
            {
                ConsumeFoodFromStorage(totalHunger, validFoodIndices);
            }

            currentSatiety = CalculateTotalStorageSatiety(out _);
        }

        NotifyFoodStatusChanged();
    }

    /// <summary>
    /// 음식 창고 영역 내부에서 슬롯 교환 및 이동시 처리
    /// </summary>
    public void OnStorageToStorageMove()
    {
        int totalSatiety = CalculateTotalStorageSatiety(out _);
        currentSatiety = totalSatiety;

        NotifyFoodStatusChanged();
    }

    /// <summary>
    /// 인벤토리 -> 음식 창고 드랍시 동작
    /// </summary>
    public void OnRightToLeftMove()
    {
        if (foodWarehouseUI == null) return;

        int totalHunger = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        int totalSatietyAvailable = CalculateTotalStorageSatiety(out List<int> validFoodIndices);

        maxSatiety = totalSatietyAvailable;
        currentSatiety = totalSatietyAvailable;

        if (isWorkStoppedDueToStarvation)
        {
            if (totalSatietyAvailable >= totalHunger)
            {
                if (isWaitingForMissedMeal)
                {
                    if (totalHunger > 0)
                    {
                        ConsumeFoodFromStorage(totalHunger, validFoodIndices);
                    }
                    isWaitingForMissedMeal = false; 

                    timer = 0f;
                }

                isWorkStoppedDueToStarvation = false;
                SetAllFacilitiesWorkingState(true);

                int remaining = CalculateTotalStorageSatiety(out _);
                //maxSatiety = remaining;
                currentSatiety = remaining;
            }
        }

        NotifyFoodStatusChanged();
    }

    /// <summary>
    /// 좌측 음식 창고 -> 우측 인벤토리로 회수 이동했을 때
    /// </summary>
    public void OnLeftToRightMove()
    {
        int totalHunger = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        int totalSatietyAvailable = CalculateTotalStorageSatiety(out _);

        maxSatiety = totalSatietyAvailable;
        currentSatiety = totalSatietyAvailable;

        if (totalHunger > totalSatietyAvailable && !isWorkStoppedDueToStarvation)
        {
            isWorkStoppedDueToStarvation = true;
            isWaitingForMissedMeal = false;

            SetAllFacilitiesWorkingState(false);
            Debug.LogWarning("<color=red><b>[영지 경보]</b></color> 창고 음식 회수로 보관량이 허기량보다 부족해져 즉시 작업이 정지됩니다.");
        }

        NotifyFoodStatusChanged();
    }

    /// <summary>
    /// 변경된 포만감 상태를 갱신
    /// </summary>
    private void NotifyFoodStatusChanged()
    {
        OnFoodAmountChanged?.Invoke(currentSatiety, maxSatiety);

        var persistentUI = FindFirstObjectByType<FoodAmountUI>();
        if (persistentUI != null)
        {
            persistentUI.RefreshUI(currentSatiety, maxSatiety);
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

            if (slot.amount <= 0) slot.Clear();
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
            if (!isWorking) facility.isProducing = false;
            else facility.CheckProductionCondition();
        }

        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null) continue;
            if (!isWorking) craft.isProducing = false;
            else
            {
                if (craft.currentCraftingItem != null && craft.DeployedMems.Count > 0) craft.isProducing = true;
            }
        }
    }
    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}