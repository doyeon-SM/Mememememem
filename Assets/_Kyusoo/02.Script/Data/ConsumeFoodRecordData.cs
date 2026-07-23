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
        // 🌟 좌측 음식 창고 초기값만 설정 (5x2 크기)
        saveData.foodWarehouseStorageData = new ContainerData { width = 5, height = 2 };
        saveData.maxSatiety = 100;
        saveData.currentSatiety = 100;
        saveData.isWorkStoppedDueToStarvation = false;

        for (int i = 0; i < 10; i++)
            saveData.foodWarehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
    }

    public void SaveData(string saveFilePath)
    {
        if (ConsumeFoodSystem.Instance == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        // 1. 🌟 좌측 음식 창고 데이터 저장
        if (ConsumeFoodSystem.Instance.FoodStorageContainer != null)
        {
            currentData.foodWarehouseStorageData = RecordManager.Instance.PackContainerData(ConsumeFoodSystem.Instance.FoodStorageContainer);
        }

        // 2. 🌟 포만감 및 아사 상태 데이터 저장
        currentData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
        currentData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
        currentData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;

        // 3. 우측 영역에 연결된 인벤토리/일반 창고 최신 데이터 동기화 보장
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

        // 1. 🌟 좌측 음식 창고 데이터 복원
        if (saveData.foodWarehouseStorageData != null && ConsumeFoodSystem.Instance.FoodStorageContainer != null)
        {
            RecordManager.Instance.UnpackContainerData(saveData.foodWarehouseStorageData, ConsumeFoodSystem.Instance.FoodStorageContainer);
        }

        // 2. 🌟 포만감 및 상태 복원
        ConsumeFoodSystem.Instance.ForceSyncManualState(saveData.currentSatiety, saveData.maxSatiety, saveData.isWorkStoppedDueToStarvation);

        if (TotalHungerManager.Instance != null)
            TotalHungerManager.Instance.RecalculateTotalHunger();

        // 3. 🌟 UI 갱신 (우측 인벤토리/창고 및 좌측 음식창고 슬롯들 일괄 갱신)
        var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (warehouseUI != null)
            warehouseUI.RefreshAllPanelsAndSlots();
    }
}