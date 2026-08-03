using System;
using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

public class TotalHungerManager : MonoBehaviour
{
    public static TotalHungerManager Instance { get; private set; }

    [SerializeField] private int totalHungerPerMinute;
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
                newTotalHunger += mem.maxHunger;
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
                newTotalHunger += mem.maxHunger;
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
                newTotalHunger += mem.maxHunger;
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
                newTotalHunger += mem.maxHunger;
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
                newTotalHunger += slot.deployedMem.maxHunger;
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
                newTotalHunger += mem.maxHunger;
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
                newTotalHunger += mem.maxHunger;
            }
        }

        totalHungerPerMinute = newTotalHunger;
        OnTotalHungerChanged?.Invoke(totalHungerPerMinute);
    }
}