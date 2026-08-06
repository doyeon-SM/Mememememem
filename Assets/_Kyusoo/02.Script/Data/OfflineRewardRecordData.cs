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
        Debug.Log($"<color=cyan>[오프라인 보상]</color> 🚀 ApplyData() 실행됨! (씬 타입: {sceneType})");

        if (sceneType == SceneType.Exploration)
        {
            Debug.Log("[오프라인 보상] 탐험 씬이므로 오프라인 보상 정산 스킵.");
            return;
        }

        ProcessOfflineReward(saveData);
    }

    public void ProcessOfflineReward(SaveData saveData)
    {
        Debug.Log("<color=cyan>[오프라인 보상]</color> 📌 ProcessOfflineReward() 정산 연산 진입.");

        if (saveData == null)
        {
            Debug.LogError("<color=red>[오프라인 보상]</color> ❌ 전달받은 saveData가 null입니다. 정산 중단.");
            return;
        }

        string lastTimeStr = saveData.timeData != null ? saveData.timeData.lastSaveRealTimeKst : null;
        if (string.IsNullOrEmpty(lastTimeStr)) lastTimeStr = saveData.lastSaveTime;

        if (string.IsNullOrEmpty(lastTimeStr))
        {
            Debug.LogWarning("<color=red>[오프라인 보상]</color> ❌ 저장된 KST 시각 정보(timeData/lastSaveTime)가 비어있습니다. 정산 중단.");
            return;
        }

        Debug.Log($"[오프라인 보상] 📄 세이브 파일의 마지막 저장 시각 텍스트: '{lastTimeStr}'");

        // 1. 저장 시각 파싱 및 현재 KST 시각 비교
        DateTime currentKst = DateTime.UtcNow.AddHours(9);
        DateTime lastSaveKst;

        bool parseSuccess = DateTime.TryParseExact(lastTimeStr, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out lastSaveKst);
        if (!parseSuccess)
        {
            parseSuccess = DateTime.TryParse(lastTimeStr, out lastSaveKst);
        }

        if (!parseSuccess)
        {
            Debug.LogError($"<color=red>[오프라인 보상]</color> ❌ 저장 시각 파싱 실패! 문자열: '{lastTimeStr}'");
            return;
        }

        double offlineSeconds = (currentKst - lastSaveKst).TotalSeconds;

        Debug.Log($"[오프라인 보상] 🕒 현재 KST 시각: {currentKst:yyyy-MM-dd HH:mm:ss}");
        Debug.Log($"[오프라인 보상] 🕒 저장 KST 시각: {lastSaveKst:yyyy-MM-dd HH:mm:ss}");
        Debug.Log($"[오프라인 보상] ⏱️ 계산된 오프라인 경과 시간: {offlineSeconds:F2}초 ({offlineSeconds / 60.0:F1}분)");

        if (offlineSeconds <= 5.0)
        {
            Debug.LogWarning($"<color=yellow>[오프라인 보상]</color> ⚠️ 경과 시간이 5초 이하({offlineSeconds:F2}초)이므로 정산을 스킵합니다.");
            return;
        }

        // 2. 허기량 대비 가동 가능 시간 산출
        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.RecalculateTotalHunger();
            Debug.Log($"[오프라인 보상] TotalHungerManager 허기 재계산 완료.");
        }
        else
        {
            Debug.LogWarning("[오프라인 보상] TotalHungerManager.Instance 가 씬에 존재하지 않습니다.");
        }

        int totalHungerPerMin = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        float totalHungerPerSec = totalHungerPerMin / 60f;

        float effectiveWorkSeconds = (float)offlineSeconds;
        bool isStarved = false;

        Debug.Log($"[오프라인 보상] 🍖 영지 분당 총 허기 소모량: {totalHungerPerMin} (초당: {totalHungerPerSec:F4})");

        if (ConsumeFoodSystem.Instance != null)
        {
            int currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
            Debug.Log($"[오프라인 보상] 🍚 ConsumeFoodSystem 인스턴스 확인됨. 현재 창고 포만감: {currentSatiety}");

            if (totalHungerPerSec > 0f)
            {
                float maxWorkSecondsByFood = currentSatiety / totalHungerPerSec;
                Debug.Log($"[오프라인 보상] 📊 포만감 기준 최대 지속 가능 시간: {maxWorkSecondsByFood:F1}초");

                if (maxWorkSecondsByFood < offlineSeconds)
                {
                    effectiveWorkSeconds = maxWorkSecondsByFood;
                    isStarved = true;

                    ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(currentSatiety);
                    ConsumeFoodSystem.Instance.ForceSyncManualState(0, ConsumeFoodSystem.Instance.MaxSatiety, true);
                    Debug.LogWarning($"<color=red>[오프라인 보상]</color> ⚠️ 포만감 소진 발생! 실제 반영 시간 단축: {offlineSeconds:F1}초 ➡️ {effectiveWorkSeconds:F1}초");
                }
                else
                {
                    int consumedSatiety = Mathf.FloorToInt(totalHungerPerSec * effectiveWorkSeconds);
                    ConsumeFoodSystem.Instance.ConsumeSatietyFromWarehouse(consumedSatiety);
                    Debug.Log($"[오프라인 보상] 🍴 음식 차감 완료! 차감된 포만감 수치: {consumedSatiety}");
                }
            }
            else
            {
                Debug.Log("[오프라인 보상] 초당 허기 소모량이 0이므로 음식 차감 없이 오프라인 전체 시간을 적용합니다.");
            }
        }
        else
        {
            Debug.LogWarning("[오프라인 보상] ConsumeFoodSystem.Instance 가 null입니다! 음식 차감 없이 오프라인 시간이 계산됩니다.");
        }

        // 3. 실제 가동시간만큼 모든 시설에 생산량 대입
        if (effectiveWorkSeconds > 0f)
        {
            Debug.Log($"<color=green>[오프라인 보상]</color> 🏭 시설 시뮬레이션 개시 (반영 가동시간: {effectiveWorkSeconds:F1}초, 기근여부: {isStarved})");
            SimulateAllFacilitiesProgress(effectiveWorkSeconds, isStarved);
        }
        else
        {
            Debug.LogWarning("<color=red>[오프라인 보상]</color> ❌ 유효 가동 시간(effectiveWorkSeconds)이 0초 이하이므로 시설 시뮬레이션을 스킵합니다.");
        }

        // UI 및 버블 리프레시
        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

        if (FacilityCollectManager.Instance != null)
        {
            FacilityCollectManager.Instance.RefreshAllFacilitiesStatus();
            Debug.Log("[오프라인 보상] FacilityCollectManager 버블 상태 리프레시 완료.");
        }

        // 4. 오프라인 보상 정산 후 파일 저장
        SaveUpdatedRecordAfterReward();

        Debug.Log($"<color=lime>[오프라인 보상]</color> 🎉 오프라인 보상 정산 최종 완 완료! (적용 가동시간: {effectiveWorkSeconds:F1}초)");
    }

    private void SaveUpdatedRecordAfterReward()
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData != null)
        {
            string kstNow = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            if (currentData.timeData == null) currentData.timeData = new GameTimeSaveData();

            //currentData.timeData.lastSaveRealTimeKst = kstNow;
            currentData.lastSaveTime = kstNow;

            System.IO.File.WriteAllText(RecordManager.Instance.SaveFilePath, JsonUtility.ToJson(currentData, true));
            Debug.Log($"[오프라인 보상] 💾 정산 완료 후 타임스탬프 최신화 저장 완료: {kstNow}");
        }
    }

    private void SimulateAllFacilitiesProgress(float workSeconds, bool isStarved)
    {
        // 1) 일반 생산 시설
        var prodFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 1. [생산 시설] 씬 내 검색된 시설 개수: {prodFacilities.Length}개");

        foreach (var prod in prodFacilities)
        {
            if (prod == null) continue;

            if (!prod.isProducing)
            {
                Debug.Log($"[오프라인 보상] [생산 시설] '{prod.gameObject.name}' 스킵 🛑 : isProducing 이 false 입니다.");
                continue;
            }
            if (string.IsNullOrEmpty(prod.craftingItem))
            {
                Debug.Log($"[오프라인 보상] [생산 시설] '{prod.gameObject.name}' 스킵 🛑 : craftingItem(생산 아이템) 이 비어있습니다.");
                continue;
            }
            if (prod.DeployedMems.Count == 0)
            {
                Debug.Log($"[오프라인 보상] [생산 시설] '{prod.gameObject.name}' 스킵 🛑 : DeployedMems(배치된 멤) 이 0마리입니다.");
                continue;
            }

            float unitTime = ProductionCalculator.CalculateFinalProductionTime(prod.baseProductionTime, prod.DeployedMems);
            if (unitTime <= 0f)
            {
                Debug.LogWarning($"[오프라인 보상] [생산 시설] '{prod.gameObject.name}' 스킵 🛑 : 계산된 단위 생산시간(unitTime)이 0 이하 ({unitTime}초)");
                continue;
            }

            float totalProgress = prod.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int prevCount = prod.currentStorageCount;

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

            Debug.Log($"<color=cyan>[오프라인 보상] [생산 시설 성공]</color> '{prod.gameObject.name}' ({prod.craftingItem}) ➡️ +{units}개 생산 (보관량: {prevCount} -> {prod.currentStorageCount}/{prod.maxStorageCount})");
        }

        // 2) 제작대 시설
        var craftFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 2. [제작대 시설] 씬 내 검색된 시설 개수: {craftFacilities.Length}개");

        foreach (var craft in craftFacilities)
        {
            if (craft == null) continue;

            if (!craft.isProducing)
            {
                Debug.Log($"[오프라인 보상] [제작대 시설] '{craft.gameObject.name}' 스킵 🛑 : isProducing 이 false 입니다.");
                continue;
            }
            if (string.IsNullOrEmpty(craft.currentCraftingItem))
            {
                Debug.Log($"[오프라인 보상] [제작대 시설] '{craft.gameObject.name}' 스킵 🛑 : currentCraftingItem 이 비어있습니다.");
                continue;
            }
            if (craft.DeployedMems.Count == 0)
            {
                Debug.Log($"[오프라인 보상] [제작대 시설] '{craft.gameObject.name}' 스킵 🛑 : DeployedMems(배치된 멤) 이 0마리입니다.");
                continue;
            }
            if (craft.remainingQuantity <= 0)
            {
                Debug.Log($"[오프라인 보상] [제작대 시설] '{craft.gameObject.name}' 스킵 🛑 : remainingQuantity(남은 목표량)가 0 이하입니다.");
                craft.isProducing = false;
                craft.currentProgressTime = 0f;
                continue;
            }

            // maxStorageCount 0 이하 방어 코드 (기본값 10)
            int effectiveMaxStorage = craft.maxStorageCount > 0 ? craft.maxStorageCount : 100;
            craft.maxStorageCount = effectiveMaxStorage;

            if (craft.currentStorageCount >= effectiveMaxStorage)
            {
                Debug.Log($"[오프라인 보상] [제작대 시설] '{craft.gameObject.name}' 스킵 🛑 : 보관함이 이미 가득 찼습니다 ({craft.currentStorageCount}/{effectiveMaxStorage}).");
                craft.isProducing = false;
                continue;
            }

            // 카탈로그 안전 참조
            var catalog = ItemCatalogManager.Resolve(null);
            RecipeData recipe = catalog != null ? catalog.FindRecipeData(craft.currentCraftingItem) : null;
            float baseDuration = recipe != null ? recipe.time : 20f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, craft.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = craft.currentProgressTime + workSeconds;
            int unitsPossible = Mathf.FloorToInt(totalProgress / unitTime);

            // 남은 수량과 보관함 여유 공간 중 최소값 계산
            int availableSpace = effectiveMaxStorage - craft.currentStorageCount;
            int maxCanProduce = Mathf.Min(craft.remainingQuantity, availableSpace);
            int actualProduced = Mathf.Min(unitsPossible, maxCanProduce);

            int prevCount = craft.currentStorageCount;

            craft.currentStorageCount += actualProduced;
            craft.remainingQuantity -= actualProduced;

            if (craft.remainingQuantity <= 0 || craft.currentStorageCount >= effectiveMaxStorage || isStarved)
            {
                craft.isProducing = false;
                craft.currentProgressTime = (craft.remainingQuantity <= 0 || isStarved) ? 0f : (totalProgress % unitTime);
            }
            else
            {
                craft.currentProgressTime = totalProgress % unitTime;
            }

            Debug.Log($"<color=cyan>[오프라인 보상] [제작대 시설 성공]</color> '{craft.gameObject.name}' ({craft.currentCraftingItem}) ➡️ +{actualProduced}개 제작 (보관량: {prevCount} -> {craft.currentStorageCount}/{effectiveMaxStorage}, 남은 목표량: {craft.remainingQuantity})");
        }

        // 3) 모닥불
        var campFires = FindObjectsByType<CampFireRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 3. [모닥불 시설] 씬 내 검색된 시설 개수: {campFires.Length}개");

        foreach (var cf in campFires)
        {
            if (cf == null) continue;

            if (!cf.isCooking)
            {
                Debug.Log($"[오프라인 보상] [모닥불] '{cf.gameObject.name}' 스킵 🛑 : isCooking 이 false 입니다.");
                continue;
            }
            if (string.IsNullOrEmpty(cf.currentCookingItem))
            {
                Debug.Log($"[오프라인 보상] [모닥불] '{cf.gameObject.name}' 스킵 🛑 : currentCookingItem 이 비어있습니다.");
                continue;
            }
            if (cf.DeployedMems.Count == 0)
            {
                Debug.Log($"[오프라인 보상] [모닥불] '{cf.gameObject.name}' 스킵 🛑 : DeployedMems(배치된 멤) 이 0마리입니다.");
                continue;
            }

            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(cf.currentCookingItem) : null;
            float baseDuration = recipe != null ? recipe.Time : 15f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, cf.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = cf.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int actualProduced = Mathf.Min(units, cf.remainingQuantity);
            int prevCount = cf.currentStorageCount;

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

            Debug.Log($"<color=cyan>[오프라인 보상] [모닥불 성공]</color> '{cf.gameObject.name}' ({cf.currentCookingItem}) ➡️ +{actualProduced}개 요리 (보관량: {prevCount} -> {cf.currentStorageCount}/{cf.maxStorageCount})");
        }

        // 4) 주방
        var kitchens = FindObjectsByType<KitchenRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 4. [주방 시설] 씬 내 검색된 시설 개수: {kitchens.Length}개");

        foreach (var k in kitchens)
        {
            if (k == null) continue;

            if (!k.isCooking)
            {
                Debug.Log($"[오프라인 보상] [주방] '{k.gameObject.name}' 스킵 🛑 : isCooking 이 false 입니다.");
                continue;
            }
            if (string.IsNullOrEmpty(k.currentCookingItem))
            {
                Debug.Log($"[오프라인 보상] [주방] '{k.gameObject.name}' 스킵 🛑 : currentCookingItem 이 비어있습니다.");
                continue;
            }
            if (k.DeployedMems.Count == 0)
            {
                Debug.Log($"[오프라인 보상] [주방] '{k.gameObject.name}' 스킵 🛑 : DeployedMems(배치된 멤) 이 0마리입니다.");
                continue;
            }

            CookRecipeData recipe = ItemCatalogManager.Instance != null ? ItemCatalogManager.Instance.FindCookRecipeData(k.currentCookingItem) : null;
            float baseDuration = recipe != null ? recipe.Time : 15f;
            float unitTime = ProductionCalculator.CalculateFinalProductionTime(baseDuration, k.DeployedMems);
            if (unitTime <= 0f) continue;

            float totalProgress = k.currentProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int actualProduced = Mathf.Min(units, k.remainingQuantity);
            int prevCount = k.currentStorageCount;

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

            Debug.Log($"<color=cyan>[오프라인 보상] [주방 성공]</color> '{k.gameObject.name}' ({k.currentCookingItem}) ➡️ +{actualProduced}개 요리 (보관량: {prevCount} -> {k.currentStorageCount}/{k.maxStorageCount})");
        }

        // 5) 발전기
        var generators = FindObjectsByType<GeneratorRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 5. [발전기 시설] 씬 내 검색된 시설 개수: {generators.Length}개");

        foreach (var gen in generators)
        {
            if (gen == null) continue;

            if (!gen.isPowerGenerating)
            {
                Debug.Log($"[오프라인 보상] [발전기] '{gen.gameObject.name}' 스킵 🛑 : isPowerGenerating 이 false 입니다.");
                continue;
            }
            if (gen.DeployedMems.Count == 0)
            {
                Debug.Log($"[오프라인 보상] [발전기] '{gen.gameObject.name}' 스킵 🛑 : DeployedMems(배치된 멤) 이 0마리입니다.");
                continue;
            }

            float unitTime = ProductionCalculator.CalculatePowerGenerationTime(gen.basePowerGenerationTime, gen.DeployedMems[0]);
            if (unitTime <= 0f) continue;

            float totalProgress = gen.currentPowerProgressTime + workSeconds;
            int units = Mathf.FloorToInt(totalProgress / unitTime);
            int prevPower = gen.currentPowerStorage;

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

            Debug.Log($"<color=cyan>[오프라인 보상] [발전기 성공]</color> '{gen.gameObject.name}' ➡️ +{units * gen.powerPerUnit}W 전력 충전 (저장량: {prevPower} -> {gen.currentPowerStorage}/{gen.maxPowerStorage}W)");
        }

        // 6) 목장
        var ranches = FindObjectsByType<RanchFacilityRuntime>(FindObjectsSortMode.None);
        Debug.Log($"[오프라인 보상] 6. [목장 시설] 씬 내 검색된 시설 개수: {ranches.Length}개");

        foreach (var ranch in ranches)
        {
            if (ranch == null) continue;

            if (!ranch.isProducing)
            {
                Debug.Log($"[오프라인 보상] [목장] '{ranch.gameObject.name}' 스킵 🛑 : isProducing 이 false 입니다.");
                continue;
            }

            for (int i = 0; i < ranch.Slots.Count; i++)
            {
                var slot = ranch.Slots[i];
                if (!slot.isUnlocked) continue;

                if (!slot.isProducing)
                {
                    Debug.Log($"[오프라인 보상] [목장] '{ranch.gameObject.name}' 슬롯[{i}] 스킵 🛑 : slot.isProducing 이 false 입니다.");
                    continue;
                }
                if (slot.deployedMem == null)
                {
                    Debug.Log($"[오프라인 보상] [목장] '{ranch.gameObject.name}' 슬롯[{i}] 스킵 🛑 : deployedMem 이 null입니다.");
                    continue;
                }

                float targetBaseTime = ranch.baseProductionTime;
                if (ranch.TryGetRanchProduceData(slot.deployedMem, out _, out float customBaseTime))
                {
                    targetBaseTime = customBaseTime;
                }

                float unitTime = ProductionCalculator.CalculateFinalProductionTime(targetBaseTime, new List<MemData> { slot.deployedMem });
                if (unitTime <= 0f) continue;

                float totalProgress = slot.currentProgressTime + workSeconds;
                int units = Mathf.FloorToInt(totalProgress / unitTime);
                int prevCount = slot.currentStorageCount;

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

                Debug.Log($"<color=cyan>[오프라인 보상] [목장 성공]</color> '{ranch.gameObject.name}' 슬롯[{i}] ({slot.craftingItemId}) ➡️ +{units}개 생산 (보관량: {prevCount} -> {slot.currentStorageCount}/{RanchSlotRuntime.maxStorage})");
            }
        }
    }
}