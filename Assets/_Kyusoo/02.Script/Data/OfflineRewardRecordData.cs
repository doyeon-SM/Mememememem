using HDY.Capture;
using HDY.Cook;
using HDY.Item;
using HDY.Recipe;
using HDY.Territory;
using MemSystem.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public class OfflineRewardRecordData : MonoBehaviour, IRecord
{
    public void InitDefaultData(ref SaveData saveData) { }

    public void SaveData(string saveFilePath) { }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration) return;

        string lastTimeStr = saveData.timeData != null ? saveData.timeData.lastSaveRealTimeKst : null;
        if (string.IsNullOrEmpty(lastTimeStr)) lastTimeStr = saveData.lastSaveTime;
        if (string.IsNullOrEmpty(lastTimeStr)) return;

        DateTime lastSaveUtc;

        // 🌟 [수정] DateTimeStyles.RoundtripKind 단독 사용 (AdjustToUniversal과 중복 사용 시 예외 발생 방지)
        if (!DateTime.TryParse(lastTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out lastSaveUtc))
        {
            if (!DateTime.TryParseExact(lastTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out lastSaveUtc))
            {
                if (!DateTime.TryParse(lastTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out lastSaveUtc))
                {
                    Debug.LogWarning($"[OfflineRewardRecordData] ⚠️ 시각 파싱 실패: {lastTimeStr}");
                    return;
                }
            }
        }

        lastSaveUtc = lastSaveUtc.ToUniversalTime();
        TimeSpan offlineDuration = DateTime.UtcNow - lastSaveUtc;
        float offlineSeconds = (float)offlineDuration.TotalSeconds;

        if (offlineSeconds <= 5f) return; // 5초 미만 오프라인 스킵

        Debug.Log($"<color=yellow>[OfflineRewardRecordData]</color> ⏳ 오프라인 경과 시간 감지: {offlineSeconds:F1}초");

        // 1. 오프라인 허기율 계산
        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.RecalculateTotalHunger();
        }

        int totalHungerPerMin = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        float totalHungerPerSec = totalHungerPerMin / 60f;

        float effectiveWorkSeconds = offlineSeconds;
        bool isStarved = false;

        if (ConsumeFoodSystem.Instance != null && totalHungerPerSec > 0f)
        {
            int currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
            float satietyRunoutSeconds = currentSatiety / totalHungerPerSec;

            if (satietyRunoutSeconds < offlineSeconds)
            {
                effectiveWorkSeconds = satietyRunoutSeconds;
                isStarved = true;
                ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(currentSatiety);
                ConsumeFoodSystem.Instance.ForceSyncManualState(0, ConsumeFoodSystem.Instance.MaxSatiety, true);
                Debug.LogWarning("<color=red>[OfflineRewardRecordData]</color> ⚠️ 오프라인 도중 식량이 고갈되어 작업이 정지되었습니다.");
            }
            else
            {
                int consumedSatiety = Mathf.FloorToInt(totalHungerPerSec * offlineSeconds);
                ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(consumedSatiety);
            }
        }

        if (effectiveWorkSeconds > 0f)
        {
            // 2. 7종 시설 오프라인 생산 시뮬레이션
            SimulateAllFacilitiesProgress(effectiveWorkSeconds, isStarved);
        }

        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

        Debug.Log($"<color=lime>[OfflineRewardRecordData]</color> 🎁 유효 오프라인 작업 ({effectiveWorkSeconds:F1}초) 보상 정산 완료!");
    }

    private void SimulateAllFacilitiesProgress(float workSeconds, bool isStarved)
    {
        // 1) 일반 생산 시설 (호박석 채석장 포함)
        var prodFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var prod in prodFacilities)
        {
            if (prod == null || !prod.isProducing || string.IsNullOrEmpty(prod.craftingItem) || prod.DeployedMems.Count == 0) continue;

            float unitTime = ProductionCalculator.CalculateFinalProductionTime(prod.baseProductionTime, prod.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = prod.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);

            prod.currentStorageCount = Mathf.Min(prod.maxStorageCount, prod.currentStorageCount + units);
            if (prod.currentStorageCount >= prod.maxStorageCount || isStarved)
            {
                prod.isProducing = false;
                prod.currentProgressTime = 0f;
            }
            else
            {
                prod.currentProgressTime = totalProgress % unitTime;
            }
        }

        // 2) 제작대 시설
        var craftFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftFacilities)
        {
            if (craft == null || !craft.isProducing || string.IsNullOrEmpty(craft.currentCraftingItem) || craft.DeployedMems.Count == 0) continue;

            RecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindRecipeData(craft.currentCraftingItem) : null;
            float baseDuration = recipe != null ? recipe.time : 20f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, craft.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = craft.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int actualProduced = Mathf.Min(units, craft.remainingQuantity);

            craft.currentStorageCount = Mathf.Min(craft.maxStorageCount, craft.currentStorageCount + actualProduced);
            craft.remainingQuantity -= actualProduced;

            if (craft.remainingQuantity <= 0 || isStarved)
            {
                craft.isProducing = false;
                craft.currentProgressTime = 0f;
            }
            else
            {
                craft.currentProgressTime = totalProgress % unitTime;
            }
        }

        // 3) 모닥불 시설
        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        foreach (var cf in campFires)
        {
            if (cf == null || !cf.isCooking || string.IsNullOrEmpty(cf.currentCookingItem) || cf.DeployedMems.Count == 0) continue;

            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(cf.currentCookingItem) : null;
            float baseDuration = recipe != null ? recipe.Time : 15f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, cf.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = cf.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int actualProduced = Mathf.Min(units, cf.remainingQuantity);

            cf.currentStorageCount = Mathf.Min(cf.maxStorageCount, cf.currentStorageCount + actualProduced);
            cf.remainingQuantity -= actualProduced;

            if (cf.remainingQuantity <= 0 || isStarved)
            {
                cf.isCooking = false;
                cf.currentProgressTime = 0f;
            }
            else
            {
                cf.currentProgressTime = totalProgress % unitTime;
            }
        }

        // 4) 주방 시설
        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        foreach (var k in kitchens)
        {
            if (k == null || !k.isCooking || string.IsNullOrEmpty(k.currentCookingItem) || k.DeployedMems.Count == 0) continue;

            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(k.currentCookingItem) : null;
            float baseDuration = recipe != null ? recipe.Time : 15f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, k.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = k.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int actualProduced = Mathf.Min(units, k.remainingQuantity);

            k.currentStorageCount = Mathf.Min(k.maxStorageCount, k.currentStorageCount + actualProduced);
            k.remainingQuantity -= actualProduced;

            if (k.remainingQuantity <= 0 || isStarved)
            {
                k.isCooking = false;
                k.currentProgressTime = 0f;
            }
            else
            {
                k.currentProgressTime = totalProgress % unitTime;
            }
        }

        // 5) 발전기 시설
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (gen == null || !gen.isPowerGenerating || gen.DeployedMems.Count == 0) continue;

            float unitTime = ProductionCalculator.CalculatePowerGenerationTime(gen.basePowerGenerationTime, gen.DeployedMems[0]);
            if (unitTime <= 0f) continue;

            float totalProgress = gen.currentPowerProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);

            gen.currentPowerStorage = Mathf.Min(gen.maxPowerStorage, gen.currentPowerStorage + (units * gen.powerPerUnit));

            if (gen.currentPowerStorage >= gen.maxPowerStorage || isStarved)
            {
                gen.isPowerGenerating = false;
                gen.currentPowerProgressTime = 0f;
            }
            else
            {
                gen.currentPowerProgressTime = totalProgress % unitTime;
            }
        }

        // 6) 목장 시설
        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var ranch in ranches)
        {
            if (ranch == null || !ranch.isProducing) continue;

            foreach (var slot in ranch.Slots)
            {
                if (!slot.isUnlocked || !slot.isProducing || slot.deployedMem == null) continue;

                float targetBaseTime = ranch.baseProductionTime;
                if (ranch.TryGetRanchProduceData(slot.deployedMem, out _, out float customBaseTime))
                {
                    targetBaseTime = customBaseTime;
                }

                float unitTime = ProductionCalculator.CalculateFinalProductionTime(targetBaseTime, new List<MemData> { slot.deployedMem });
                if (unitTime <= 0f) continue;

                float totalProgress = slot.currentProgressTime + workSeconds;
                int units = Mathf.FloorToInt(totalProgress / unitTime);

                slot.currentStorageCount = Mathf.Min(RanchSlotRuntime.maxStorage, slot.currentStorageCount + units);

                if (slot.currentStorageCount >= RanchSlotRuntime.maxStorage || isStarved)
                {
                    slot.isProducing = false;
                    slot.currentProgressTime = 0f;
                }
                else
                {
                    slot.currentProgressTime = totalProgress % unitTime;
                }
            }
        }
    }
}