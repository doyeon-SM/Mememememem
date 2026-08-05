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
    public void ApplyData(SaveData saveData, SceneType sceneType) { }

    /// <summary>
    /// 🌟 FacilityRecordData에서 건물/멤 복원이 완전히 종료된 후 호출됩니다.
    /// </summary>
    public void ProcessOfflineReward(SaveData saveData)
    {
        if (saveData == null) return;

        string lastTimeStr = saveData.timeData != null ? saveData.timeData.lastSaveRealTimeKst : null;
        if (string.IsNullOrEmpty(lastTimeStr)) lastTimeStr = saveData.lastSaveTime;
        if (string.IsNullOrEmpty(lastTimeStr)) return;

        // 1. 저장되어 있던 과거 KST 시각과 현재 KST 시각 비교
        DateTime currentKst = DateTime.UtcNow.AddHours(9);
        DateTime lastSaveKst;

        if (!DateTime.TryParseExact(lastTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out lastSaveKst))
        {
            if (!DateTime.TryParse(lastTimeStr, out lastSaveKst))
            {
                Debug.LogWarning($"[OfflineRewardRecordData] 저장 시각 파싱 실패: {lastTimeStr}");
                return;
            }
        }

        double offlineSeconds = (currentKst - lastSaveKst).TotalSeconds;

        if (offlineSeconds <= 5.0)
        {
            Debug.Log($"[OfflineRewardRecordData] KST 차이가 짧아 정산을 스킵합니다. ({offlineSeconds:F1}초 차이)");
            return;
        }

        Debug.Log($"<color=yellow>[OfflineRewardRecordData]</color> ⏰ 정상 감지된 KST 오프라인 경과 시간: {offlineSeconds:F1}초");

        // 2. 허기량 대비 가동 가능 시간 산출
        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.RecalculateTotalHunger();
        }

        int totalHungerPerMin = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        float totalHungerPerSec = totalHungerPerMin / 60f;

        float effectiveWorkSeconds = (float)offlineSeconds;
        bool isStarved = false;

        if (ConsumeFoodSystem.Instance != null && totalHungerPerSec > 0f)
        {
            int currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
            float maxWorkSecondsByFood = currentSatiety / totalHungerPerSec;

            // Min(실제 경과 시간, 포만감 보유량 대비 가동 가능 시간)
            if (maxWorkSecondsByFood < offlineSeconds)
            {
                effectiveWorkSeconds = maxWorkSecondsByFood;
                isStarved = true;

                ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(currentSatiety);
                ConsumeFoodSystem.Instance.ForceSyncManualState(0, ConsumeFoodSystem.Instance.MaxSatiety, true);
                Debug.LogWarning("<color=red>[OfflineRewardRecordData]</color> ⚠️ 포만감이 한계에 도달하여 일부 시간만 가동된 후 작업이 중단되었습니다.");
            }
            else
            {
                int consumedSatiety = Mathf.FloorToInt(totalHungerPerSec * effectiveWorkSeconds);
                ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(consumedSatiety);
            }
        }

        // 3. 실제 가동시간만큼 모든 시설에 생산량 대입
        if (effectiveWorkSeconds > 0f)
        {
            SimulateAllFacilitiesProgress(effectiveWorkSeconds, isStarved);
        }

        // UI 및 버블 리프레시
        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

        if (FacilityCollectManager.Instance != null)
        {
            FacilityCollectManager.Instance.RefreshAllFacilitiesStatus();
        }

        // 4. 🌟 오프라인 보상 대입이 모두 끝난 '지금' 비로소 저장 시각을 현재 KST 시각으로 갱신하여 파일 저장!
        SaveUpdatedRecordAfterReward();

        Debug.Log($"<color=lime>[OfflineRewardRecordData]</color> 🎁 오프라인 보상 정산 완! (가동: {effectiveWorkSeconds:F1}초) & TerritoryRecord.json 최신화 완료!");
    }

    private void SaveUpdatedRecordAfterReward()
    {
        // 정산이 끝났으므로 저장 시각을 현재 KST 시각으로 변경
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData != null)
        {
            string kstNow = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            if (currentData.timeData == null) currentData.timeData = new GameTimeSaveData();

            currentData.timeData.lastSaveRealTimeKst = kstNow;
            currentData.lastSaveTime = kstNow;

            System.IO.File.WriteAllText(RecordManager.Instance.SaveFilePath, JsonUtility.ToJson(currentData, true));
        }

        var facilityRecord = FindFirstObjectByType<FacilityRecordData>();
        if (facilityRecord != null && RecordManager.Instance != null)
        {
            facilityRecord.SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    private void SimulateAllFacilitiesProgress(float workSeconds, bool isStarved)
    {
        // 1) 일반 생산 시설
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

        // 3) 모닥불
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

        // 4) 주방
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

        // 5) 발전기
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

        // 6) 목장
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