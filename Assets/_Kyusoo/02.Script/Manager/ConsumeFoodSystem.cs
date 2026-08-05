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

    private InventoryContainer foodStorageContainer = new InventoryContainer { width = 10, height = 1 };
    private InventoryContainer foodBagContainer = new InventoryContainer { width = 10, height = 7 };

    public bool IsWorkStoppedDueToStarvation => isWorkStoppedDueToStarvation;
    public int MaxSatiety => maxSatiety;
    public int CurrentSatiety => currentSatiety;

    public InventoryContainer FoodStorageContainer => foodStorageContainer;
    public InventoryContainer FoodBagContainer => foodBagContainer;

    public event Action<int, int> OnFoodAmountChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            foodStorageContainer.width = 10;
            foodStorageContainer.height = 1;
            foodStorageContainer.slots = new ItemStack[5];
            for (int i = 0; i < 5; i++)
            {
                foodStorageContainer.slots[i] = new ItemStack();
            }

            foodBagContainer.Initialize();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
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

    public int CalculateTotalStorageSatiety(out List<int> validFoodIndices)
    {
        validFoodIndices = new List<int>();
        int sumSatiety = 0;

        if (foodStorageContainer == null || foodStorageContainer.slots == null) return 0;

        ItemCatalogManager catalog = foodWarehouseUI != null ? foodWarehouseUI.CatalogManager : FindFirstObjectByType<ItemCatalogManager>();

        for (int i = 0; i < foodStorageContainer.slots.Length; i++)
        {
            ItemStack slot = foodStorageContainer.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData itemData = catalog != null ? catalog.FindItemData(slot.itemId) : null;

            if (itemData == null && RecordManager.Instance != null)
            {
                itemData = RecordManager.Instance.FindItemDataInProject(slot.itemId);
            }

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
        if (foodStorageContainer == null || foodStorageContainer.slots == null) return;

        ItemCatalogManager catalog = foodWarehouseUI != null ? foodWarehouseUI.CatalogManager : FindFirstObjectByType<ItemCatalogManager>();
        int neededHunger = hungerToConsume;

        for (int i = 0; i < foodStorageContainer.slots.Length; i++)
        {
            if (neededHunger <= 0) break;

            ItemStack slot = foodStorageContainer.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData itemData = catalog != null ? catalog.FindItemData(slot.itemId) : null;
            if (itemData == null && RecordManager.Instance != null)
            {
                itemData = RecordManager.Instance.FindItemDataInProject(slot.itemId);
            }

            int singleSatiety = GetSatietyValue(itemData);
            if (singleSatiety <= 0) continue;

            int itemsNeeded = Mathf.CeilToInt((float)neededHunger / singleSatiety);

            int itemsToConsume = Mathf.Min(slot.amount, itemsNeeded);

            slot.amount -= itemsToConsume;
            neededHunger -= (itemsToConsume * singleSatiety);

            if (slot.amount <= 0)
            {
                slot.Clear();
            }
        }

        currentSatiety = CalculateTotalStorageSatiety(out _);
        NotifyFoodStatusChanged();

        if (foodWarehouseUI != null) foodWarehouseUI.RefreshAllPanelsAndSlots();
    }

    private void SetAllFacilitiesWorkingState(bool isWorking)
    {
        // 1. 일반 생산 시설
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null) continue;
            if (!isWorking) facility.StopWorkDueToStarvation();
            else facility.CheckProductionCondition();
        }

        // 2. 제작대 시설
        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null) continue;
            if (!isWorking) craft.StopWorkDueToStarvation();
            else craft.ResumeWorkAfterStarvation();
        }

        // 3. 발전기 시설
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (gen == null) continue;
            if (!isWorking) gen.StopWorkDueToStarvation();
            else gen.CheckPowerCondition();
        }

        // 4. 목장 시설
        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var ranch in ranches)
        {
            if (ranch == null) continue;
            if (!isWorking) ranch.StopWorkDueToStarvation();
            else ranch.CheckAllSlotsProductionCondition();
        }

        // 5. 운송 시설
        var transportFacilities = FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None);
        foreach (var trans in transportFacilities)
        {
            if (trans == null) continue;
            if (!isWorking) trans.StopWorkDueToStarvation();
            else trans.CheckProductionCondition();
        }

        // 6. 모닥불 시설
        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        foreach (var cf in campFires)
        {
            if (cf == null) continue;
            if (!isWorking) cf.StopWorkDueToStarvation();
            else cf.ResumeWorkAfterStarvation();
        }

        // 7. 주방 시설
        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        foreach (var k in kitchens)
        {
            if (k == null) continue;
            if (!isWorking) k.StopWorkDueToStarvation();
            else k.ResumeWorkAfterStarvation();
        }
    }

    public void ConsumeSatietyFromWarehouse(int satietyToConsume)
    {
        int remainingSatiety = satietyToConsume;


        if (foodStorageContainer == null || foodStorageContainer.slots == null) return;

        ItemCatalogManager catalog = null;
        if (foodWarehouseUI != null && foodWarehouseUI.CatalogManager != null)
        {
            catalog = foodWarehouseUI.CatalogManager;
        }
        else
        {
            catalog = FindFirstObjectByType<ItemCatalogManager>();
        }

        foreach (var slot in foodStorageContainer.slots)
        {
            if (slot == null || slot.IsEmpty || string.IsNullOrEmpty(slot.itemId)) continue;

            ItemData itemData = catalog != null ? catalog.FindItemData(slot.itemId) : null;

            if (itemData == null && RecordManager.Instance != null)
            {
                itemData = RecordManager.Instance.FindItemDataInProject(slot.itemId);
            }

            if (itemData == null) continue;

            int itemSatiety = GetSatietyValue(itemData);
            if (itemSatiety <= 0) continue;

            while (slot.amount > 0 && remainingSatiety >= itemSatiety)
            {
                slot.amount--;
                remainingSatiety -= itemSatiety;
            }

            if (slot.amount <= 0) slot.Clear();
            if (remainingSatiety <= 0) break;
        }

        currentSatiety = CalculateTotalStorageSatiety(out _);
        NotifyFoodStatusChanged();
    }

    private int GetSatietyValue(ItemData data)
    {
        if (data == null || data.EatEffects == null || data.EatEffects.Count == 0) return 0;
        foreach (var effect in data.EatEffects)
        {
            if (effect.Effect == EffectType.Satiety) return (int)effect.Value;
        }
        return 0;
    }
}