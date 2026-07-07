using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

public static class ProductionCalculator
{
    /// <summary>
    /// 시설 종류에 매치되는 멤의 생산 스탯 종류 매칭함수
    /// </summary>
    public static ProductionStatType GetRequiredStatType(BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.Workshop => ProductionStatType.Crafting,
            BuildingType.LoggingCamp => ProductionStatType.Logging,
            BuildingType.MiningCamp => ProductionStatType.Mining,
            BuildingType.Farm or BuildingType.Ranch => ProductionStatType.Farming,
            BuildingType.TransportFacility or BuildingType.Generator => ProductionStatType.Transport,
            _ => ProductionStatType.Crafting
        };
    }

    /// <summary>
    /// 시설 레벨에 따른 최대 배치 가능 멤 숫자 계산 (1, 3, 5, 7, 9레벨에서 확장)
    /// </summary>
    public static int GetMaxMemCount(int facilityLevel)
    {
        int maxCount = 1 + ((facilityLevel - 1) / 2);
        return Mathf.Clamp(maxCount, 1, 5);
    }

    /// <summary>
    /// 특정 시설에 멤을 배치할 수 있는지 자격 검증하는 함수
    /// GetStat 메소드를 활용하여 최소 1단계이상 매치되는지 확인
    /// </summary>
    public static bool CanDeployToFacility(MemData memData, BuildingType buildingType)
    {
        if (memData == null) return false;

        ProductionStatType requiredStat = GetRequiredStatType(buildingType);

        return memData.productionStats.GetStat(requiredStat) >= 1;
    }

    /// <summary>
    /// 멤 배치수, 멤 등급을 기반으로 최종 생산 소요 시간을 계산하는 마스터 공식
    /// 멤 배치별 2초씩 감소(1마리 = 0초, 5마리 = 10초)
    /// 멤 등급별 2초씩 감소(Rare = 0초, Mythic = 10초)
    /// </summary>
    public static float CalculateFinalProductionTime(float baseItemTime, List<MemData> assignedMems)
    {
        if (assignedMems == null || assignedMems.Count == 0) return baseItemTime;

        int memCount = assignedMems.Count;
        float totalReduction = 0f;

        if (memCount >= 5) totalReduction += 10f; 
        else if (memCount >= 2) totalReduction += (memCount - 1) * 2f;

        foreach (MemData mem in assignedMems)
        {
            if (mem == null) continue;

            switch (mem.tier)
            {
                case MemTier.Rare: totalReduction += 0f; break;
                case MemTier.Epic: totalReduction += 2f; break;
                case MemTier.Unique: totalReduction += 4f; break;
                case MemTier.Legendary: totalReduction += 6f; break;
                case MemTier.Mythic: totalReduction += 10f; break; 
            }
        }

        return Mathf.Max(baseItemTime - totalReduction, 2f);
    }
}