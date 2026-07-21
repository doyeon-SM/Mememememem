using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;
using HDY.Territory;
using HDY.Item;
using KMS.InventoryDuped;

public class OfflineRewardRecordData : MonoBehaviour, IRecord
{
    public void InitDefaultData(ref SaveData saveData) { }
    public void SaveData(string saveFilePath) { }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration) return;

        string lastKstString = saveData.timeData.lastSaveRealTimeKst;
        if (string.IsNullOrEmpty(lastKstString)) return;

        if (!DateTime.TryParseExact(lastKstString, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime lastKstTime))
        {
            Debug.LogWarning($"[OfflineReward] 저장된 시간 포맷이 올바르지 않습니다: {lastKstString}");
            return;
        }

        DateTime currentKstTime = DateTime.UtcNow.AddHours(9);
        TimeSpan timeDiff = currentKstTime - lastKstTime;
        double realElapsedSeconds = timeDiff.TotalSeconds;

        if (realElapsedSeconds < 5.0)
        {
            Debug.Log($"[OfflineReward] 오프라인 경과 시간이 미비하여 정산을 스킵합니다. ({realElapsedSeconds:F1}초)");
            return;
        }

        Debug.Log($"<color=yellow>[OfflineReward] 영지 이탈 후 오프라인 경과 시간: {realElapsedSeconds:F1}초 ({timeDiff.TotalMinutes:F1}분)</color>");

        if (TotalHungerManager.Instance != null)
        {
            TotalHungerManager.Instance.RecalculateTotalHunger();
        }

        int totalHungerPerMinute = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;

        int elapsedMinutes = Mathf.FloorToInt((float)realElapsedSeconds / 60f);
        float remainingSecondsFraction = (float)realElapsedSeconds % 60f;

        int activeWorkingMinutes = 0;
        bool isStarved = false;

        for (int m = 0; m < elapsedMinutes; m++)
        {
            if (totalHungerPerMinute <= 0)
            {
                activeWorkingMinutes++;
                continue;
            }

            int currentTotalSatiety = GetTotalStorageSatiety();

            if (totalHungerPerMinute > currentTotalSatiety)
            {
                isStarved = true;
                Debug.LogWarning($"[OfflineReward] 오프라인 {m + 1}분째에 음식이 부족하여 가동이 정지되었습니다.");
                break;
            }

            Deduct1MinuteFoodSatiety(totalHungerPerMinute);
            activeWorkingMinutes++;
        }

        // 유효 가동 시간 계산 (정상 가동된 분 수 * 60초 + 자투리 초)
        double effectiveWorkSeconds = (activeWorkingMinutes * 60.0);
        if (!isStarved)
        {
            effectiveWorkSeconds += remainingSecondsFraction;
        }

        Debug.Log($"[OfflineReward] 최종 유효 가동 시간: {effectiveWorkSeconds:F1}초 | 최종 남은 포만감: {GetTotalStorageSatiety()} | 기근 여부: {isStarved}");

        // 1. 보급고 및 기근 상태 동기화
        if (ConsumeFoodSystem.Instance != null)
        {
            ConsumeFoodSystem.Instance.ForceSyncManualState(GetTotalStorageSatiety(), ConsumeFoodSystem.Instance.MaxSatiety, isStarved);
        }

        // 2. 제작 시설 오프라인 생산 정산
        SimulateCraftFacilities(effectiveWorkSeconds, isStarved);

        // 3. 생산 시설 오프라인 생산 정산
        SimulateProductionFacilities(effectiveWorkSeconds, isStarved);

        // 4. UI 갱신
        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

        // 5. 정산 완료 후 세이브 파일 즉시 재기록
        RecordManager.Instance.SetPrivateFieldSafely(RecordManager.Instance, "IsLoadingData", false);

        var inventoryRecord = FindFirstObjectByType<PlayerInventoryRecord>();
        inventoryRecord?.SaveData(RecordManager.Instance.SaveFilePath);

        var consumeFoodRecord = FindFirstObjectByType<ConsumeFoodRecordData>();
        consumeFoodRecord?.SaveData(RecordManager.Instance.SaveFilePath);

        var facilityRecord = FindFirstObjectByType<FacilityRecordData>();
        facilityRecord?.SaveData(RecordManager.Instance.SaveFilePath);

        Debug.Log("<color=lime>[OfflineReward] 오프라인 1분 단위 동기화 시뮬레이션 완료!</color>");
    }

    /// <summary>
    /// 실시간 ConsumeFoodSystem과 동일하게 0번 슬롯(왼쪽 위)부터 1분 분량 허기를 올림 연산으로 차감합니다.
    /// </summary>
    private void Deduct1MinuteFoodSatiety(int hungerToConsume)
    {
        if (ConsumeFoodSystem.Instance == null || ConsumeFoodSystem.Instance.FoodStorageContainer == null) return;
        var container = ConsumeFoodSystem.Instance.FoodStorageContainer;
        if (container.slots == null) return;

        int neededHunger = hungerToConsume;

        for (int i = 0; i < container.slots.Length; i++)
        {
            if (neededHunger <= 0) break;

            ItemStack slot = container.slots[i];
            if (slot == null || slot.IsEmpty) continue;

            ItemData data = RecordManager.Instance != null ? RecordManager.Instance.FindItemDataInProject(slot.itemId) : null;
            int singleSatiety = GetItemSatietyValue(data);
            if (singleSatiety <= 0) continue;

            int itemsNeeded = Mathf.CeilToInt((float)neededHunger / singleSatiety);
            int itemsToConsume = Mathf.Min(slot.amount, itemsNeeded);

            slot.amount -= itemsToConsume;
            neededHunger -= (itemsToConsume * singleSatiety);

            if (slot.amount <= 0)
            {
                slot.Clear();
            }
        }
    }

    private int GetTotalStorageSatiety()
    {
        if (ConsumeFoodSystem.Instance == null || ConsumeFoodSystem.Instance.FoodStorageContainer == null) return 0;
        int totalSatiety = 0;

        foreach (var slot in ConsumeFoodSystem.Instance.FoodStorageContainer.slots)
        {
            if (slot == null || slot.IsEmpty) continue;
            ItemData data = RecordManager.Instance != null ? RecordManager.Instance.FindItemDataInProject(slot.itemId) : null;
            int singleSatiety = GetItemSatietyValue(data);
            totalSatiety += singleSatiety * slot.amount;
        }
        return totalSatiety;
    }

    private int GetItemSatietyValue(ItemData data)
    {
        if (data == null || data.EatEffects == null) return 0;
        foreach (var effect in data.EatEffects)
        {
            if (effect != null && effect.Effect == EffectType.Satiety) return (int)effect.Value;
        }
        return 0;
    }

    private void SimulateCraftFacilities(double workSeconds, bool isStarved)
    {
        var crafts = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in crafts)
        {
            if (craft == null || !craft.isProducing || craft.currentCraftingItem == null) continue;

            float craftDuration = GetCraftingDuration(craft);
            if (craftDuration <= 0f) craftDuration = 20f;

            double totalTime = workSeconds + craft.currentProgressTime;
            int producedCount = Mathf.FloorToInt((float)(totalTime / craftDuration));
            int actualToProduce = Math.Min(producedCount, craft.remainingQuantity);

            if (actualToProduce > 0)
            {
                craft.currentStorageCount += actualToProduce;
                craft.remainingQuantity -= actualToProduce;
                craft.currentProgressTime = (float)(totalTime - (actualToProduce * craftDuration));
            }
            else
            {
                craft.currentProgressTime = (float)totalTime;
            }

            if (craft.remainingQuantity <= 0)
            {
                craft.remainingQuantity = 0;
                craft.currentProgressTime = 0f;
                craft.isProducing = false;
            }

            if (isStarved)
            {
                craft.isProducing = false;
            }
        }
    }

    private void SimulateProductionFacilities(double workSeconds, bool isStarved)
    {
        var facilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in facilities)
        {
            if (facility == null || !facility.isProducing || facility.craftingItem == null) continue;

            float prodDuration = GetProductionDuration(facility);
            if (prodDuration <= 0f) prodDuration = 30f;

            int maxStorage = GetMaxStorage(facility);
            int availableStorageSpace = maxStorage - facility.currentStorageCount;

            if (availableStorageSpace <= 0)
            {
                facility.isProducing = false;
                continue;
            }

            double totalTime = workSeconds + facility.currentProgressTime;
            int producedCount = Mathf.FloorToInt((float)(totalTime / prodDuration));

            int actualToProduce = Math.Min(producedCount, availableStorageSpace);

            if (actualToProduce > 0)
            {
                facility.currentStorageCount += actualToProduce;
                facility.currentProgressTime = (float)(totalTime - (actualToProduce * prodDuration));
            }
            else
            {
                facility.currentProgressTime = (float)totalTime;
            }

            if (facility.currentStorageCount >= maxStorage || isStarved)
            {
                facility.isProducing = false;
            }
        }
    }

    private float GetCraftingDuration(ProductionCraftRuntime craft)
    {
        if (craft == null) return 20f;
        var field = typeof(ProductionCraftRuntime).GetField("craftingDuration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return Convert.ToSingle(field.GetValue(craft));
        return 20f;
    }

    private float GetProductionDuration(ProductionFacilityRuntime facility)
    {
        if (facility == null) return 30f;
        var field = typeof(ProductionFacilityRuntime).GetField("baseProductionTime", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return Convert.ToSingle(field.GetValue(facility));
        return 30f;
    }

    private int GetMaxStorage(ProductionFacilityRuntime facility)
    {
        if (facility == null) return 100;
        var field = typeof(ProductionFacilityRuntime).GetField("maxStorageCount", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null) return Convert.ToInt32(field.GetValue(facility));
        return 100;
    }
}