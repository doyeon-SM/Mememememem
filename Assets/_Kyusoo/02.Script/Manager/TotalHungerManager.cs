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
        ProductionCraftRuntime.FacilityStarted += OnFacilityStartedHandler;
    }

    private void OnDisable()
    {
        ProductionFacilityRuntime.FacilityStarted -= OnFacilityStartedHandler;
        ProductionCraftRuntime.FacilityStarted -= OnFacilityStartedHandler;
    }

    private void Start()
    {
        RecalculateTotalHunger();
    }

    private void OnFacilityStartedHandler(BuildingType type, List<MemData> mems) => RecalculateTotalHunger();
    private void OnFacilityStartedHandler(BuildingType type) => RecalculateTotalHunger();

    /// <summary>
    /// 실제 생산/제작 가동 중(isProducing == true)인 시설에 배치된 멤의 허기값만 합산
    /// </summary>
    public void RecalculateTotalHunger()
    {
        int newTotalHunger = 0;

        // 1. 일반 생산 시설 스캔
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        Debug.Log($"<color=cyan>[TotalHunger] 스캔 시작: 생산 시설 {productionFacilities.Length}개 발견</color>");

        foreach (var facility in productionFacilities)
        {
            if (facility == null || facility.DeployedMems == null || facility.DeployedMems.Count == 0) continue;
            if (!facility.isProducing) continue; 

            foreach (MemData mem in facility.DeployedMems)
            {
                if (mem == null) continue;

                newTotalHunger += mem.maxHunger;
                Debug.Log($" - [생산 가동 중] 시설: {facility.name} | 멤: {mem.memName} | 허기값: {mem.maxHunger} | 누적합: {newTotalHunger}");
            }
        }

        // 2. 제작 시설 스캔
        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        Debug.Log($"<color=yellow>[TotalHunger] 스캔 시작: 제작 시설 {craftingFacilities.Length}개 발견</color>");

        foreach (var craft in craftingFacilities)
        {
            if (craft == null || craft.DeployedMems == null || craft.DeployedMems.Count == 0) continue;
            if (!craft.isProducing) continue; // 가동 중이 아니면 합산 스킵

            foreach (MemData mem in craft.DeployedMems)
            {
                if (mem == null) continue;

                newTotalHunger += mem.maxHunger;
                Debug.Log($" - [제작 가동 중] 시설: {craft.name} | 멤: {mem.memName} | 허기값: {mem.maxHunger} | 누적합: {newTotalHunger}");
            }
        }

        totalHungerPerMinute = newTotalHunger;
        Debug.Log($"<color=green>[TotalHunger] 최종 합산 완료 (실제 가동 중인 멤 기준): {totalHungerPerMinute}</color>");
        OnTotalHungerChanged?.Invoke(totalHungerPerMinute);
    }
}