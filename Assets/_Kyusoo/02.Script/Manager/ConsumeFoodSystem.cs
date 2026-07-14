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

    // RecordManager가 세이브/로드 시 직접 접근할 수 있도록 음식 창고 사이즈 정의
    private InventoryContainer foodStorageContainer = new InventoryContainer { width = 5, height = 2 };

    /// <summary>현재 음식이 부족하여 영지 전체가 중지되었는지 여부 반환</summary>
    public bool IsWorkStoppedDueToStarvation => isWorkStoppedDueToStarvation;
    public int MaxSatiety => maxSatiety;
    public int CurrentSatiety => currentSatiety;

    // RecordManager가 들여다볼 수 있도록 통로 개방
    public InventoryContainer FoodStorageContainer => foodStorageContainer;

    public event Action<int, int> OnFoodAmountChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); 
            foodStorageContainer.Initialize();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

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
                // 1분 주기 타이머 호출 시에는 수동 변경이 아니므로 false 전달
                ProcessFoodConsumption(false);
            }
        }
    }

    /// <summary>
    /// RecordManager 및 외부 UI 드래그앤드롭 이벤트가 
    /// 수동 제어 분기(isManualChange)를 명시하여 호출할 수 있도록 매개변수를 완벽하게 탑재했습니다!
    /// </summary>
    public void ProcessFoodConsumption(bool isManualChange = false)
    {
        if (foodWarehouseUI == null) return;

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

    /// <summary>음식 창고 영역 내부에서 슬롯 교환 및 이동시 처리</summary>
    public void OnStorageToStorageMove()
    {
        int totalSatiety = CalculateTotalStorageSatiety(out _);
        currentSatiety = totalSatiety;
        NotifyFoodStatusChanged();
    }

    /// <summary>인벤토리 -> 음식 창고 드랍시 동작</summary>
    public void OnRightToLeftMove()
    {
        ProcessFoodConsumption(true);
    }

    /// <summary>좌측 음식 창고 -> 우측 인벤토리로 회수 이동했을 때</summary>
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
    /// 외부 로드 시스템이나 데이터 강제 주입 후, 강제 수치 리프레시를 제어하기 위한 개방형 통로
    /// </summary>
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

        for (int i = 0; i < foodStorageContainer.slots.Length; i++)
        {
            ItemStack slot = foodStorageContainer.slots[i];
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

    private void ConsumeFoodFromStorage(int hungerToConsume, List<int> foodIndices)
    {
        int remainingHunger = hungerToConsume;

        foreach (int index in foodIndices)
        {
            ItemStack slot = foodStorageContainer.slots[index];
            ItemData itemData = foodWarehouseUI.CatalogManager.FindItemData(slot.itemId);

            int singleSatiety = 0;
            if (itemData != null && itemData.EatEffects != null)
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
        foodWarehouseUI.RefreshAllPanelsAndSlots();
    }

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
}