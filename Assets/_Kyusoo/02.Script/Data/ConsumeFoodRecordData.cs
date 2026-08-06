using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KMS.InventoryDuped;

public class ConsumeFoodRecordData : MonoBehaviour, IRecord
{
    private void OnEnable()
    {
        FoodWarehouseUI.OnFoodDataChanged += OnFoodDataChangedHandler;
    }

    private void OnDisable()
    {
        FoodWarehouseUI.OnFoodDataChanged -= OnFoodDataChangedHandler;
    }

    private void OnFoodDataChangedHandler()
    {
        if (RecordManager.IsLoadingData || RecordManager.IsSceneUnloading) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.foodWarehouseStorageData = new ContainerData { width = 5, height = 1 };
        saveData.foodWarehouseStorageData.slots = new List<ItemStackData>();
        for (int i = 0; i < 5; i++)
        {
            saveData.foodWarehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }
        saveData.maxSatiety = 0;
        saveData.currentSatiety = 0;
        saveData.isWorkStoppedDueToStarvation = false;
    }

    public void SaveData(string saveFilePath)
    {
        if (RecordManager.IsLoadingData || RecordManager.IsSceneUnloading) return;
        if (ConsumeFoodSystem.Instance == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        var container = ConsumeFoodSystem.Instance.FoodStorageContainer;
        currentData.foodWarehouseStorageData = RecordManager.Instance.PackContainerData(container);
        currentData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
        currentData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
        currentData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[ConsumeFoodRecordData]</color> 🍚 음식 창고 데이터 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (ConsumeFoodSystem.Instance == null || saveData.foodWarehouseStorageData == null) return;

        var container = ConsumeFoodSystem.Instance.FoodStorageContainer;

        RecordManager.Instance.UnpackContainerData(saveData.foodWarehouseStorageData, container);

        int totalSlots = container.slots != null ? container.slots.Length : 5;
        var foodUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (foodUI != null)
        {
            int extraSlots = Mathf.Max(0, totalSlots - 5);
            RecordManager.Instance.SetPrivateFieldSafely(foodUI, "extraUpgradedSlotCount", extraSlots);
            foodUI.RefreshAllPanelsAndSlots();
        }

        int actualCalculatedSatiety = ConsumeFoodSystem.Instance.CalculateTotalStorageSatiety(out _);

        ConsumeFoodSystem.Instance.ForceSyncManualState(
            actualCalculatedSatiety,
            Mathf.Max(saveData.maxSatiety, actualCalculatedSatiety),
            saveData.isWorkStoppedDueToStarvation
        );

        Debug.Log($"<color=cyan>[ConsumeFoodRecordData]</color> 🍚 음식 복구 완료 (슬롯 수: {totalSlots}, 실측 포만감: {actualCalculatedSatiety})");
    }
}