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

    [SerializeField] private int maxSatiety = 0;
    [SerializeField] private int currentSatiety = 0;

    private InventoryContainer foodStorageContainer = new InventoryContainer { width = 5, height = 2 };
    private InventoryContainer foodBagContainer = new InventoryContainer { width = 10, height = 7 };

    /// <summary>현재 음식이 부족하여 영지 전체가 중지되었는지 여부 반환</summary>
    public bool IsWorkStoppedDueToStarvation => isWorkStoppedDueToStarvation;
    public int MaxSatiety => maxSatiety;
    public int CurrentSatiety => currentSatiety;

    // RecordManager 및 FoodWarehouseUI가 직접 공유하여 링크할 프로퍼티 통로 개방
    public InventoryContainer FoodStorageContainer => foodStorageContainer;
    public InventoryContainer FoodBagContainer => foodBagContainer;

    public event Action<int, int> OnFoodAmountChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 장부 슬롯 배정 구조 초기화 완수
            foodStorageContainer.Initialize();
            foodBagContainer.Initialize();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (foodWarehouseUI == null) foodWarehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
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
                ProcessFoodConsumption(false);
            }
        }
    }

    /// <summary>
    /// 음식 소모 차감 연산 및 포만감 강제 실시간 정산 통제 총괄 엔진
    /// </summary>
    public void ProcessFoodConsumption(bool isManualChange = false)
    {
        if (foodWarehouseUI == null) foodWarehouseUI = FindFirstObjectByType<FoodWarehouseUI>();

        int totalHunger = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        int totalSatietyAvailable = CalculateTotalStorageSatiety(out List<int> validFoodIndices);

        if (isManualChange)
        {
            maxSatiety = totalSatietyAvailable;
            currentSatiety = totalSatietyAvailable;
        }

        if (totalHunger > totalSatietyAvailable)
        {
            if (!isWorkStoppedDueToStarvation)
            {
                isWorkStoppedDueToStarvation = true;
                isWaitingForMissedMeal = true;

                SetAllFacilitiesWorkingState(false);
                Debug.LogWarning("<color=red><b>[영지 경보]</b></color> 음식 부족으로 모든 시설 가동이 정지됩니다.");
            }
            currentSatiety = totalSatietyAvailable;
            NotifyFoodStatusChanged();
            return;
        }

        if (isWorkStoppedDueToStarvation)
        {
            isWorkStoppedDueToStarvation = false;
            SetAllFacilitiesWorkingState(true);
            Debug.Log("<color=lime><b>[영지 정상화]</b></color> 음식을 충분히 확보했습니다. 모든 시설이 다시 가동을 시작합니다.");
            timer = 0f;
        }

        if (!isManualChange || (isManualChange && isWaitingForMissedMeal))
        {
            if (totalHunger > 0)
            {
                ConsumeFoodFromStorage(totalHunger, validFoodIndices);
                isWaitingForMissedMeal = false;
            }
        }

        currentSatiety = CalculateTotalStorageSatiety(out _);
        NotifyFoodStatusChanged();
    }

    public void OnStorageToStorageMove()
    {
        int totalSatiety = CalculateTotalStorageSatiety(out _);
        currentSatiety = totalSatiety;
        NotifyFoodStatusChanged();
    }

    public void OnRightToLeftMove()
    {
        ProcessFoodConsumption(true);
    }

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

    public void ForceSyncManualState(int loadedCurrent, int loadedMax, bool loadedStarvation)
    {
        maxSatiety = loadedMax;
        currentSatiety = loadedCurrent;
        isWorkStoppedDueToStarvation = loadedStarvation;
        NotifyFoodStatusChanged();
    }

    public void NotifyFoodStatusChanged()
    {
        OnFoodAmountChanged?.Invoke(currentSatiety, maxSatiety);

        var persistentUI = FindFirstObjectByType<FoodAmountUI>();
        if (persistentUI != null)
        {
            persistentUI.RefreshUI(currentSatiety, maxSatiety);
        }
    }

    private int CalculateTotalStorageSatiety(out List<int> validFoodIndices)
    {
        validFoodIndices = new List<int>();
        int sumSatiety = 0;

        if (foodStorageContainer == null || foodStorageContainer.slots == null) return 0;

        // UI창이 꺼져있을 때에도 에러 없이 백과사전 스캔 에셋 데이터를 찾아올 수 있도록 안전 방어 분기를 설계합니다.
        ItemCatalogManager catalog = foodWarehouseUI != null ? foodWarehouseUI.CatalogManager : FindFirstObjectByType<ItemCatalogManager>();
        if (catalog == null) return 0;

        for (int i = 0; i < foodStorageContainer.slots.Length; i++)
        {
            ItemStack slot = foodStorageContainer.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData itemData = catalog.FindItemData(slot.itemId);
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

    private void ConsumeFoodFromStorage(int hungerToConsume, List<int> foodIndices)
    {
        int remainingHunger = hungerToConsume;
        ItemCatalogManager catalog = foodWarehouseUI != null ? foodWarehouseUI.CatalogManager : FindFirstObjectByType<ItemCatalogManager>();

        foreach (int index in foodIndices)
        {
            ItemStack slot = foodStorageContainer.slots[index];
            ItemData itemData = catalog != null ? catalog.FindItemData(slot.itemId) : null;
            if (itemData == null) continue;

            int singleSatiety = 0;
            if (itemData.EatEffects != null)
            {
                foreach (ItemEffect effect in itemData.EatEffects)
                {
                    if (effect != null && effect.Effect == EffectType.Satiety)
                    {
                        singleSatiety = (int)effect.Value;
                        break;
                    }
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

        if (foodWarehouseUI != null) foodWarehouseUI.RefreshAllPanelsAndSlots();
    }

    private void SetAllFacilitiesWorkingState(bool isWorking)
    {
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null) continue;
            if (!isWorking)
            {
                facility.StopWorkDueToStarvation();
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
                craft.StopWorkDueToStarvation();
            }
            else
            {
                craft.ResumeWorkAfterStarvation();
            }
        }
    }

    public void ConsumeSatietyFromWarehouse(int satietyToConsume)
    {
        int remainingSatiety = satietyToConsume;

        // 창고 슬롯을 순회하며 음식 소모
        foreach (var slot in foodStorageContainer.slots)
        {
            if (slot == null || slot.IsEmpty) continue;

            // 아이템 정보 조회 (아이템 카탈로그 연동)
            var itemData = foodWarehouseUI.CatalogManager.FindItemData(slot.itemId);
            int itemSatiety = GetSatietyValue(itemData); // 아이템 데이터에서 포만감 수치 추출하는 함수

            if (itemSatiety <= 0) continue;

            while (slot.amount > 0 && remainingSatiety >= itemSatiety)
            {
                slot.amount--;
                remainingSatiety -= itemSatiety;
            }

            if (slot.amount <= 0) slot.Clear();
            if (remainingSatiety <= 0) break;
        }

        // UI 갱신 및 상태 동기화
        currentSatiety = CalculateTotalStorageSatiety(out _);
        NotifyFoodStatusChanged();
    }

    private int GetSatietyValue(ItemData data)
    {
        if (data == null || data.EatEffects == null) return 0;
        foreach (var effect in data.EatEffects)
        {
            if (effect.Effect == EffectType.Satiety) return (int)effect.Value;
        }
        return 0;
    }
}