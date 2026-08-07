using System;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;
using HDY.Capture;

/// <summary>
/// [HDY 요청 - 영지 배고픔 시스템 전면 개편]
/// 예전에는 이 클래스가 "가동 중인 멤들의 MaxHunger 합"만 계산해서 들고 있고(TotalHungerPerMinute),
/// 실제 소비/정지 판단은 ConsumeFoodSystem이 60초마다 그 합계를 밥통에서 통째로 차감하는 방식으로 했다.
///
/// 이제는 두 가지 역할을 한다.
/// 1) TotalHungerPerMinute: UI 표시 전용 - 가동 중인 멤들의 Consumption(분당 소비량) 합. 시설
///    시작/정지 이벤트가 있을 때마다 RecalculateTotalHunger()로 재계산된다(기존과 동일한 트리거 방식).
/// 2) ProcessPerMinuteConsumption(): 실제 매분 틱 처리 - ConsumeFoodSystem이 60초 타이머에서 호출한다.
///    가동 중인 시설에 배치된 멤마다 CurrentHunger를 Consumption만큼 깎고, 0이 되면 그 멤 하나만
///    밥통에서 급식을 시도한다. 급식에 실패하면 그 멤이 배치된 시설(목장은 슬롯)만 개별적으로 정지시키고,
///    이후 급식에 성공하면 그 시설(슬롯)만 개별적으로 재개시킨다 - 예전처럼 영지 전체를 일괄 정지시키지
///    않는다.
/// </summary>
public class TotalHungerManager : MonoBehaviour
{
    public static TotalHungerManager Instance { get; private set; }

    [SerializeField] private int totalHungerPerMinute;

    /// <summary>[UI 표시 전용] 가동 중인 멤들의 분당 소비량(Consumption) 합. 실제 소비/급식 로직에는
    /// 쓰이지 않는다 - ProcessPerMinuteConsumption()이 멤마다 개별로 처리한다.</summary>
    public int TotalHungerPerMinute => totalHungerPerMinute;

    public event Action<int> OnTotalHungerChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        ProductionFacilityRuntime.FacilityStarted += OnFacilityStartedHandler;
        ProductionFacilityRuntime.FacilityStopped += OnFacilityStoppedHandler;

        ProductionCraftRuntime.FacilityStarted += OnFacilityStartedHandler;
        ProductionCraftRuntime.FacilityStopped += OnFacilityStoppedHandler;

        GeneratorRuntime.FacilityStarted += OnFacilityStartedHandler;
        GeneratorRuntime.FacilityStopped += OnFacilityStoppedHandler;

        TransportRuntime.FacilityStarted += OnFacilityStartedHandler;
        TransportRuntime.FacilityStopped += OnFacilityStoppedHandler;

        RanchFacilityRuntime.FacilityStarted += OnFacilityStartedHandler;
        RanchFacilityRuntime.FacilityStopped += OnFacilityStoppedHandler;

        CampFireRuntime.FacilityStarted += OnFacilityStartedHandler;
        CampFireRuntime.FacilityStopped += OnFacilityStoppedHandler;

        KitchenRuntime.FacilityStarted += OnFacilityStartedHandler;
        KitchenRuntime.FacilityStopped += OnFacilityStoppedHandler;
    }

    private void OnDisable()
    {
        ProductionFacilityRuntime.FacilityStarted -= OnFacilityStartedHandler;
        ProductionFacilityRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        ProductionCraftRuntime.FacilityStarted -= OnFacilityStartedHandler;
        ProductionCraftRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        GeneratorRuntime.FacilityStarted -= OnFacilityStartedHandler;
        GeneratorRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        TransportRuntime.FacilityStarted -= OnFacilityStartedHandler;
        TransportRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        RanchFacilityRuntime.FacilityStarted -= OnFacilityStartedHandler;
        RanchFacilityRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        CampFireRuntime.FacilityStarted -= OnFacilityStartedHandler;
        CampFireRuntime.FacilityStopped -= OnFacilityStoppedHandler;

        KitchenRuntime.FacilityStarted -= OnFacilityStartedHandler;
        KitchenRuntime.FacilityStopped -= OnFacilityStoppedHandler;
    }

    private void Start()
    {
        RecalculateTotalHunger();
    }

    private void OnFacilityStartedHandler(BuildingType type, List<MemData> mems, List<Transform> positions) => RecalculateTotalHunger();
    private void OnFacilityStoppedHandler(BuildingType type, List<MemData> mems, FacilityStopReason reason, List<Transform> positions) => RecalculateTotalHunger();

    /// <summary>
    /// [UI 표시 전용] 가동 중인 시설에 배치된 멤들의 Consumption(분당 소비량) 합을 다시 계산한다.
    /// 실제 배고픔 소비/급식과는 무관하다 - 화면에 "분당 소비량"을 보여주기 위한 값이다.
    /// </summary>
    public void RecalculateTotalHunger()
    {
        int newTotalHunger = 0;

        // 1. 일반 생산 시설
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null || facility.DeployedMems == null || facility.DeployedMems.Count == 0) continue;
            if (!facility.isProducing) continue;

            foreach (MemData mem in facility.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        // 2. 제작대 시설
        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null || craft.DeployedMems == null || craft.DeployedMems.Count == 0) continue;
            if (!craft.isProducing) continue;

            foreach (MemData mem in craft.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        // 3. 발전기 시설
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (gen == null || gen.DeployedMems == null || gen.DeployedMems.Count == 0) continue;
            if (!gen.isPowerGenerating) continue;

            foreach (MemData mem in gen.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        // 4. 운송 시설
        var transportFacilities = FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None);
        foreach (var trans in transportFacilities)
        {
            if (trans == null || trans.DeployedMems == null || trans.DeployedMems.Count == 0) continue;
            if (!trans.isWorking) continue;

            foreach (MemData mem in trans.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        // 5. 목장 시설
        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var ranch in ranches)
        {
            if (ranch == null || ranch.Slots == null) continue;
            foreach (var slot in ranch.Slots)
            {
                if (slot == null || !slot.isUnlocked || !slot.isProducing || slot.deployedMem == null) continue;
                newTotalHunger += slot.deployedMem.consumption;
            }
        }

        // 6. 모닥불 시설
        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        foreach (var cf in campFires)
        {
            if (cf == null || cf.DeployedMems == null || cf.DeployedMems.Count == 0) continue;
            if (!cf.isCooking) continue;

            foreach (MemData mem in cf.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        // 7. 주방 시설
        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        foreach (var k in kitchens)
        {
            if (k == null || k.DeployedMems == null || k.DeployedMems.Count == 0) continue;
            if (!k.isCooking) continue;

            foreach (MemData mem in k.DeployedMems)
            {
                if (mem == null) continue;
                newTotalHunger += mem.consumption;
            }
        }

        totalHungerPerMinute = newTotalHunger;
        OnTotalHungerChanged?.Invoke(totalHungerPerMinute);
    }

    /// <summary>
    /// [HDY 요청 - 영지 배고픔 시스템] 매분 실제 소비/급식을 처리한다. ConsumeFoodSystem의 60초 타이머가
    /// 호출한다. 가동 중인 시설에 배치된 멤마다 CurrentHunger를 Consumption만큼 깎고, 0 이하가 되면 그
    /// 멤 하나만 밥통에서 급식을 시도한다. 급식 성공/실패에 따라 그 멤이 배치된 시설(목장은 슬롯)만
    /// 개별적으로 재개/정지시킨다.
    /// </summary>
    public void ProcessPerMinuteConsumption()
    {
        // 1. 일반 생산 시설
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null || facility.DeployedMems == null || facility.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(facility.DeployedMems, facility.DeployedMemEntries,
                facility.StopWorkDueToStarvation, facility.CheckProductionCondition);
        }

        // 2. 제작대 시설
        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null || craft.DeployedMems == null || craft.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(craft.DeployedMems, craft.DeployedMemEntries,
                craft.StopWorkDueToStarvation, craft.ResumeWorkAfterStarvation);
        }

        // 3. 발전기 시설
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (gen == null || gen.DeployedMems == null || gen.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(gen.DeployedMems, gen.DeployedMemEntries,
                gen.StopWorkDueToStarvation, gen.CheckPowerCondition);
        }

        // 4. 운송 시설
        var transportFacilities = FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None);
        foreach (var trans in transportFacilities)
        {
            if (trans == null || trans.DeployedMems == null || trans.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(trans.DeployedMems, trans.DeployedMemEntries,
                trans.StopWorkDueToStarvation, trans.CheckProductionCondition);
        }

        // 5. 목장 시설
        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var ranch in ranches)
        {
            if (ranch == null || ranch.Slots == null) continue;

            foreach (var slot in ranch.Slots)
            {
                if (slot == null || !slot.isUnlocked) continue;
                if (slot.deployedMem == null || slot.deployedMemEntry == null) continue;

                bool changed = UpdateMemHunger(slot.deployedMem, slot.deployedMemEntry);
                if (changed)
                {
                    ranch.SetSlotStarvationState(slot.deployedMemEntry, slot.deployedMemEntry.IsStarving);
                }
            }
        }

        // 6. 모닥불 시설
        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        foreach (var cf in campFires)
        {
            if (cf == null || cf.DeployedMems == null || cf.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(cf.DeployedMems, cf.DeployedMemEntries,
                cf.StopWorkDueToStarvation, cf.ResumeWorkAfterStarvation);
        }

        // 7. 주방 시설
        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        foreach (var k in kitchens)
        {
            if (k == null || k.DeployedMems == null || k.DeployedMems.Count == 0) continue;
            ProcessFacilityMems(k.DeployedMems, k.DeployedMemEntries,
                k.StopWorkDueToStarvation, k.ResumeWorkAfterStarvation);
        }
    }

    /// <summary>
    /// [버그 수정 - 다중 배치 시설] 한 시설에 배치된 멤 목록(DeployedMems/DeployedMemEntries, 같은
    /// 인덱스끼리 짝) 전체를 순회하며 개별 소비/급식을 처리한다.
    ///
    /// 채석장/벌목장/밭 등(ProductionFacilityRuntime 계열)은 멤을 여러 마리 배치할 수 있는데, 멤을
    /// 하나 처리할 때마다 즉시 정지/재개를 호출하면 처리 순서에 따라 결과가 꼬일 수 있다(예: 방금 굶은
    /// 멤 A 처리 → 정지 호출, 이어서 방금 급식에 성공한 멤 B 처리 → 재개 호출 → 최종적으로 시설이
    /// "재개" 상태로 끝나버리는데, 실제로는 A가 여전히 굶고 있으므로 잘못된 결과다).
    ///
    /// 그래서 이 시설에 배치된 멤 전체를 먼저 다 처리한 뒤, "한 마리라도 여전히 굶고 있는가"를 최종
    /// 판단해서 그 결과로 딱 한 번만 정지/재개를 호출한다. 상태가 실제로 바뀐 경우(정지↔재개 전환)에만
    /// 콜백을 호출해 불필요한 중복 호출도 피한다.
    /// </summary>
    private void ProcessFacilityMems(List<MemData> mems, List<CapturedMemEntry> entries, Action stopFacility, Action resumeFacility)
    {
        if (mems == null || entries == null) return;

        int count = Mathf.Min(mems.Count, entries.Count);
        bool anyStillStarving = false;
        bool anyChanged = false;

        for (int i = 0; i < count; i++)
        {
            MemData mem = mems[i];
            CapturedMemEntry entry = entries[i];
            if (mem == null || entry == null) continue;

            bool changed = UpdateMemHunger(mem, entry);
            if (changed) anyChanged = true;
            if (entry.IsStarving) anyStillStarving = true;
        }

        // 이번 틱에 어떤 멤도 굶는 상태가 바뀌지 않았으면(계속 정상이거나 계속 굶는 중) 시설 상태를
        // 다시 건드릴 필요가 없다 - 이미 맞는 상태로 가동/정지 중이다.
        //if (!anyChanged) return;

        if (anyStillStarving)
        {
            stopFacility?.Invoke();
        }
        else
        {
            resumeFacility?.Invoke();
        }
    }

    /// <summary>
    /// 멤 1마리의 배고픔을 Consumption만큼 깎고, 0 이하가 되면 급식을 시도해서 entry.IsStarving을
    /// 갱신한다. 급식 전후로 IsStarving이 실제로 바뀌었으면(정지↔재개 전환) true를 반환한다 -
    /// 호출부가 이 값으로 시설/슬롯의 정지·재개 콜백을 부를지 말지 결정한다.
    /// </summary>
    private bool UpdateMemHunger(MemData mem, CapturedMemEntry entry)
    {
        if (mem == null || entry == null) return false;

        bool wasStarving = entry.IsStarving;

        entry.CurrentHunger = Mathf.Max(0, entry.CurrentHunger - mem.consumption);

        if (entry.CurrentHunger <= 0)
        {
            bool fed = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.TryFeedMem(entry, mem.maxHunger);
            entry.IsStarving = !fed;
        }

        return wasStarving != entry.IsStarving;
    }

    public void RetryStarvingMemsFeeding()
    {
        if (ConsumeFoodSystem.Instance == null) return;

        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null) continue;
            RetryFeedingForFacility(facility.DeployedMems, facility.DeployedMemEntries, facility.CheckProductionCondition);
        }

        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null) continue;
            RetryFeedingForFacility(craft.DeployedMems, craft.DeployedMemEntries, craft.ResumeWorkAfterStarvation);
        }

        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (gen == null) continue;
            RetryFeedingForFacility(gen.DeployedMems, gen.DeployedMemEntries, gen.CheckPowerCondition);
        }

        var transportFacilities = FindObjectsByType<TransportRuntime>(FindObjectsSortMode.None);
        foreach (var trans in transportFacilities)
        {
            if (trans == null) continue;
            RetryFeedingForFacility(trans.DeployedMems, trans.DeployedMemEntries, trans.CheckProductionCondition);
        }

        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var ranch in ranches)
        {
            if (ranch == null || ranch.Slots == null) continue;
            foreach (var slot in ranch.Slots)
            {
                if (slot != null && slot.isUnlocked && slot.deployedMem != null && slot.deployedMemEntry != null && slot.deployedMemEntry.IsStarving)
                {
                    bool fed = ConsumeFoodSystem.Instance.TryFeedMem(slot.deployedMemEntry, slot.deployedMem.maxHunger);
                    if (fed)
                    {
                        slot.deployedMemEntry.IsStarving = false;
                        ranch.SetSlotStarvationState(slot.deployedMemEntry, false);
                    }
                }
            }
        }

        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        foreach (var cf in campFires)
        {
            if (cf == null) continue;
            RetryFeedingForFacility(cf.DeployedMems, cf.DeployedMemEntries, cf.ResumeWorkAfterStarvation);
        }

        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        foreach (var k in kitchens)
        {
            if (k == null) continue;
            RetryFeedingForFacility(k.DeployedMems, k.DeployedMemEntries, k.ResumeWorkAfterStarvation);
        }
    }

    private void RetryFeedingForFacility(List<MemData> mems, List<CapturedMemEntry> entries, Action resumeCallback)
    {
        if (mems == null || entries == null) return;

        int count = Mathf.Min(mems.Count, entries.Count);
        bool anyResumed = false;

        for (int i = 0; i < count; i++)
        {
            MemData mem = mems[i];
            CapturedMemEntry entry = entries[i];

            if (mem != null && entry != null && entry.IsStarving)
            {
                bool fed = ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.TryFeedMem(entry, mem.maxHunger);
                if (fed)
                {
                    entry.IsStarving = false;
                    anyResumed = true;
                }
            }
        }

        if (anyResumed)
        {
            resumeCallback?.Invoke();
        }
    }
}

