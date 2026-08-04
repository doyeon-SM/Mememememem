using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using KMS.InventoryDuped;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [HDY 요청 - 영지 배고픔 시스템 전면 개편]
/// 예전에는 "영지 전체 분당 소비량(TotalHungerManager.TotalHungerPerMinute = 가동 중인 멤들의 MaxHunger 합)"을
/// 60초마다 밥통에서 한꺼번에 차감하고, 부족하면 모든 시설을 일괄 정지시키는 방식이었다.
///
/// 이제는 멤 1마리 단위로 배고픔을 관리한다(CapturedMemEntry.CurrentHunger). 실제 매분 소비/급식 처리는
/// TotalHungerManager.ProcessPerMinuteConsumption()이 각 시설의 가동 중인 멤을 순회하며 담당하고, 이
/// 클래스는 그 요청을 받아 밥통에서 실제로 음식을 소비하는 역할(TryFeedMem)만 한다.
///
/// [레거시 플래그] IsWorkStoppedDueToStarvation은 기존에 여러 시설 코드(ProductionFacilityRuntime 등)가
/// "생산을 시작해도 되는지"의 보조 조건으로 참조하고 있어 완전히 제거하지 않았다. 다만 의미가 "밥통이
/// 완전히 바닥났는지" 정도의 비상 신호로 축소되었고, 더 이상 이 플래그로 모든 시설을 일괄 정지시키지
/// 않는다 - 개별 시설 정지/재개는 TotalHungerManager가 각 시설의 StopWorkDueToStarvation/재개 메서드를
/// 그 멤이 배치된 시설에 대해서만 개별 호출해서 처리한다.
/// </summary>
public class ConsumeFoodSystem : MonoBehaviour
{
    public static ConsumeFoodSystem Instance { get; private set; }

    [SerializeField] private FoodWarehouseUI foodWarehouseUI;

    [Header("소모 주기 설정 (초 단위)")]
    [SerializeField] private float consumeInterval = 60f;

    private float timer = 0f;

    /// <summary>
    /// [의미 축소 - 영지 배고픔 시스템 개편] 이제 "밥통이 완전히 바닥났는지"만 나타내는 비상 신호다.
    /// 더 이상 이 값을 true로 만든다고 해서 모든 시설이 일괄 정지되지 않는다(개별 시설이 각자의 배치된
    /// 멤이 실제로 급식에 실패했는지로 스스로 정지/재개한다). 다만 일부 시설 코드가 여전히 "생산 시작
    /// 가능 여부"의 보조 조건으로 이 플래그를 참조하므로 하위 호환을 위해 계속 계산해서 제공한다.
    /// </summary>
    private bool isWorkStoppedDueToStarvation = false;

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
        // [HDY 요청 - 영지 배고픔 시스템] 굶고 있는 멤도 매 틱 급식을 재시도해야 하므로, 예전과 달리
        // 전역 정지 플래그와 무관하게 타이머는 항상 진행된다.
        timer += Time.deltaTime;
        if (timer >= consumeInterval)
        {
            timer = 0f;
            TotalHungerManager.Instance?.ProcessPerMinuteConsumption();
            RefreshLegacyStarvationFlag();
        }
    }

    /// <summary>
    /// [HDY 요청 - 영지 배고픔 시스템] 밥통이 완전히 바닥났는지 여부만 갱신한다(비상 신호).
    /// TotalHungerManager.ProcessPerMinuteConsumption()이 개별 급식을 전부 처리한 뒤 호출된다.
    /// </summary>
    private void RefreshLegacyStarvationFlag()
    {
        currentSatiety = CalculateTotalStorageSatiety(out _);
        isWorkStoppedDueToStarvation = currentSatiety <= 0;
        NotifyFoodStatusChanged();
    }

    /// <summary>
    /// [HDY 요청 - 영지 배고픔 시스템] 멤 1마리를 먹인다. entry.CurrentHunger가 maxHunger에 도달할 때까지
    /// 밥통에서 음식을 "아이템 단위"로 소비한다 - 마지막 한 아이템으로 목표치를 넘기더라도 그 아이템은
    /// 통째로 소비되고 초과분은 버려진다(예: MaxHunger=15, 포만감 10짜리 음식이 10개 있으면 2개를 소비해서
    /// CurrentHunger를 15로 채움). 밥통에 있는 음식으로 목표치까지 다 채우지 못하면(재고 부족) 가진 만큼만
    /// 채우고 false를 반환한다 - 호출부(TotalHungerManager)가 이 결과로 해당 멤이 배치된 시설/슬롯을
    /// 정지시킬지 판단한다.
    /// </summary>
    public bool TryFeedMem(CapturedMemEntry entry, int maxHunger)
    {
        if (entry == null) return false;

        int needed = maxHunger - entry.CurrentHunger;
        if (needed <= 0)
        {
            entry.CurrentHunger = maxHunger;
            return true;
        }

        int obtained = ConsumeItemsForHunger(needed);
        entry.CurrentHunger = Mathf.Min(maxHunger, entry.CurrentHunger + obtained);

        currentSatiety = CalculateTotalStorageSatiety(out _);
        NotifyFoodStatusChanged();
        if (foodWarehouseUI != null) foodWarehouseUI.RefreshAllPanelsAndSlots();

        return entry.CurrentHunger >= maxHunger;
    }

    public void OnStorageToStorageMove()
    {
        int totalSatiety = CalculateTotalStorageSatiety(out _);
        currentSatiety = totalSatiety;
        NotifyFoodStatusChanged();
    }

    /// <summary>플레이어가 음식을 가방에서 창고(밥통)로 옮겼을 때 호출된다. 표시값만 갱신한다 - 실제
    /// 급식/정지 판단은 다음 매분 틱에서 TotalHungerManager가 개별 멤 단위로 다시 시도한다.</summary>
    public void OnRightToLeftMove()
    {
        RefreshLegacyStarvationFlag();
    }

    /// <summary>플레이어가 음식을 창고(밥통)에서 가방으로 옮겼을 때 호출된다. 표시값만 갱신한다 - 이제
    /// 이 시점에 모든 시설을 일괄 정지시키지 않는다(개별 시설은 다음 매분 틱에서 자기 멤의 급식 성공
    /// 여부로 스스로 정지/재개한다).</summary>
    public void OnLeftToRightMove()
    {
        RefreshLegacyStarvationFlag();
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

    /// <summary>
    /// [HDY 요청 - 영지 배고픔 시스템] neededSatiety만큼 채우기 위해 필요한 아이템 개수를 올림 계산해서
    /// 밥통에서 소비한다(마지막 아이템으로 목표를 넘길 수 있음 - 호출부인 TryFeedMem이 최종 캡핑 처리).
    /// 실제로 확보한 satiety를 반환한다. 재고가 부족하면 있는 만큼만 소비하고 그만큼만 반환한다(요청량 미만).
    /// </summary>
    private int ConsumeItemsForHunger(int neededSatiety)
    {
        if (foodStorageContainer == null || foodStorageContainer.slots == null) return 0;

        ItemCatalogManager catalog = foodWarehouseUI != null ? foodWarehouseUI.CatalogManager : FindFirstObjectByType<ItemCatalogManager>();
        int remaining = neededSatiety;
        int obtained = 0;

        for (int i = 0; i < foodStorageContainer.slots.Length; i++)
        {
            if (remaining <= 0) break;

            ItemStack slot = foodStorageContainer.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData itemData = catalog != null ? catalog.FindItemData(slot.itemId) : null;
            if (itemData == null && RecordManager.Instance != null)
            {
                itemData = RecordManager.Instance.FindItemDataInProject(slot.itemId);
            }

            int singleSatiety = GetSatietyValue(itemData);
            if (singleSatiety <= 0) continue;

            int itemsNeeded = Mathf.CeilToInt((float)remaining / singleSatiety);
            int itemsToConsume = Mathf.Min(slot.amount, itemsNeeded);

            slot.amount -= itemsToConsume;
            int satietyFromThis = itemsToConsume * singleSatiety;
            obtained += satietyFromThis;
            remaining -= satietyFromThis;

            if (slot.amount <= 0)
            {
                slot.Clear();
            }
        }

        return obtained;
    }

    /// <summary>[오프라인 보상 등에서 사용] 지정한 satiety를 밥통에서 그대로 소비한다(아이템 단위, 초과분 버림 없이 딱 맞춰 소비 시도).</summary>
    public void ConsumeSatietyFromWarehouse(int satietyToConsume)
    {
        int remainingSatiety = satietyToConsume;

        foreach (var slot in foodStorageContainer.slots)
        {
            if (slot == null || slot.IsEmpty) continue;

            var itemData = foodWarehouseUI.CatalogManager.FindItemData(slot.itemId);
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
        if (data == null || data.EatEffects == null) return 0;
        foreach (var effect in data.EatEffects)
        {
            if (effect.Effect == EffectType.Satiety) return (int)effect.Value;
        }
        return 0;
    }
}
