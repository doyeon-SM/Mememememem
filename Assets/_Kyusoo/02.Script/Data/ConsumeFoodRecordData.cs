using HDY.Inventory;
using System;
using System.IO;
using UnityEngine;

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
        if (RecordManager.Instance != null && !RecordManager.IsLoadingData)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.foodWarehouseStorageData = new ContainerData { width = 5, height = 2 };
        saveData.foodBagStorageData = new ContainerData { width = 10, height = 7 };
        saveData.maxSatiety = 100;
        saveData.currentSatiety = 100;
        saveData.isWorkStoppedDueToStarvation = false;

        for (int i = 0; i < 10; i++) saveData.foodWarehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        for (int i = 0; i < 70; i++) saveData.foodBagStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
    }

    public void SaveData(string saveFilePath)
    {
        if (ConsumeFoodSystem.Instance == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (ConsumeFoodSystem.Instance.FoodStorageContainer != null)
            currentData.foodWarehouseStorageData = RecordManager.Instance.PackContainerData(ConsumeFoodSystem.Instance.FoodStorageContainer);

        if (ConsumeFoodSystem.Instance.FoodBagContainer != null)
            currentData.foodBagStorageData = RecordManager.Instance.PackContainerData(ConsumeFoodSystem.Instance.FoodBagContainer);

        currentData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
        currentData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
        currentData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        var livePlayerInventory = FindFirstObjectByType<KMS.InventoryDuped.PlayerInventory>();
        if (livePlayerInventory != null && livePlayerInventory.inventory != null)
        {
            currentData.playerInventoryData = RecordManager.Instance.PackContainerData(livePlayerInventory.inventory);
        }

        var liveWarehouseInventory = FindFirstObjectByType<WarehouseInventory>();
        if (liveWarehouseInventory != null && liveWarehouseInventory.storage != null)
        {
            currentData.warehouseStorageData = RecordManager.Instance.PackContainerData(liveWarehouseInventory.storage);
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration) return;
        if (ConsumeFoodSystem.Instance == null) return;

        if (saveData.foodWarehouseStorageData != null)
            RecordManager.Instance.UnpackContainerData(saveData.foodWarehouseStorageData, ConsumeFoodSystem.Instance.FoodStorageContainer);

        if (saveData.foodBagStorageData != null)
            RecordManager.Instance.UnpackContainerData(saveData.foodBagStorageData, ConsumeFoodSystem.Instance.FoodBagContainer);

        ConsumeFoodSystem.Instance.ForceSyncManualState(saveData.currentSatiety, saveData.maxSatiety, saveData.isWorkStoppedDueToStarvation);

        if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();
    }
}