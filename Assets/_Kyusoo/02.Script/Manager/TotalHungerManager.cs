using HDY.Inventory;
using MemSystem.Data;
using System;
using UnityEngine;


public class TotalHungerManager : MonoBehaviour
{
    public static TotalHungerManager Instance { get; private set; }

    [Header("실시간 통계 데이터")]
    [SerializeField] private int totalHungerPerMinute = 0;

    // 영지 내 모든 시설에 배치된 멤들의 분당 총 허기량
    public int TotalHungerPerMinute => totalHungerPerMinute;

    // 시설에 멤이 배치/제거되어 총 허기량이 바뀔 때마다 발행됩니다.
    public event Action<int> OnTotalHungerChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        RecalculateTotalHunger();
    }

    /// <summary>
    /// 필드 내 모든 ProductionFacilityRuntime 및 ProductionCraftRuntime을 찾아 
    /// 배치된 모든 멤들의 분당 허기량을 합산 후, 이벤트 발행
    /// </summary>
    public void RecalculateTotalHunger()
    {
        int newTotalHunger = 0;

        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null || facility.DeployedMems == null) continue;

            foreach (MemData mem in facility.DeployedMems)
            {
                if (mem == null) continue;

                newTotalHunger += mem.maxHunger;
            }
        }

        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null || craft.DeployedMems == null) continue;

            foreach (MemData mem in craft.DeployedMems)
            {
                if (mem == null) continue;

                newTotalHunger += mem.maxHunger;
            }
        }

        totalHungerPerMinute = newTotalHunger;
        Debug.Log($"허기량 업데이트: {totalHungerPerMinute}");
        OnTotalHungerChanged?.Invoke(totalHungerPerMinute);

    }
}
