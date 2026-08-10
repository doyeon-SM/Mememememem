using System.Collections.Generic;
using UnityEngine;
using MemSystem.Data;

public static class ProductionCalculator
{
    public static ProductionStatType GetRequiredStatType(BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.Workshop:
            case BuildingType.CampFire:
            case BuildingType.Kitchen:
                return ProductionStatType.Crafting;
            case BuildingType.LoggingCamp:
                return ProductionStatType.Logging;
            case BuildingType.MiningCamp:
                return ProductionStatType.Mining;
            case BuildingType.TransportFacility:
            case BuildingType.Generator:
                return ProductionStatType.Transport;
            case BuildingType.Farm:
            case BuildingType.Ranch:
                return ProductionStatType.Farming;
            default:
                return ProductionStatType.Crafting;
        }
    }

    /// <summary>
    /// 일반 시설(생산, 목장 등)의 레벨당 최대 멤 배치 수 (1레벨 1개, 레벨업 시 +2개, 최대 5개)
    /// </summary>
    public static int GetMaxMemCount(int facilityLevel)
    {
        int count = 3 + (facilityLevel - 1) * 2;
        return Mathf.Clamp(count, 1, 5);
    }

    /// <summary>
    /// 운송 시설의 레벨당 최대 멤 배치 수 (1레벨당 1슬롯, 최대 3개)
    /// </summary>
    public static int GetTransportMaxMemCount(int facilityLevel)
    {
        return Mathf.Clamp(facilityLevel, 1, 3);
    }

    public static bool CanDeployToFacility(MemData memData, BuildingType buildingType)
    {
        if (memData == null) return false;
        ProductionStatType requiredStat = GetRequiredStatType(buildingType);
        return memData.productionStats.GetStat(requiredStat) >= 1;
    }

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

    public static float CalculatePowerGenerationTime(float baseTime, MemData mem)
    {
        if (mem == null) return baseTime;
        float reduction = 0f;
        switch (mem.tier)
        {
            case MemTier.Rare: reduction = 0f; break;
            case MemTier.Epic: reduction = 2f; break;
            case MemTier.Unique: reduction = 4f; break;
            case MemTier.Legendary: reduction = 6f; break;
            case MemTier.Mythic: reduction = 10f; break;
        }
        return Mathf.Max(baseTime - reduction, 2f);
    }
}