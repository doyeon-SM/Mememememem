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
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        // 🌟 음식 창고: ConsumeFoodSystem의 선언에 맞춘 초기 5칸 규격 (width 10, height 1, 5 슬롯)
        saveData.foodWarehouseStorageData = new ContainerData { width = 10, height = 1 };
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
        Debug.Log("<color=lime>[ConsumeFoodRecordData]</color> 🍲 음식 창고 및 시뮬레이션 데이터 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (ConsumeFoodSystem.Instance == null || saveData.foodWarehouseStorageData == null) return;

        var container = ConsumeFoodSystem.Instance.FoodStorageContainer;

        // 1. 음식 창고 슬롯 및 아이템 리스트 복원
        RecordManager.Instance.UnpackContainerData(saveData.foodWarehouseStorageData, container);

        // 2. FoodWarehouseUI의 업그레이드 카운트(extraUpgradedSlotCount) 수치 동기화
        int totalSlots = container.slots != null ? container.slots.Length : 5;
        var foodUI = FindFirstObjectByType<FoodWarehouseUI>();
        if (foodUI != null)
        {
            int extraSlots = Mathf.Max(0, totalSlots - 5);
            RecordManager.Instance.SetPrivateFieldSafely(foodUI, "extraUpgradedSlotCount", extraSlots);
            foodUI.RefreshAllPanelsAndSlots();
        }

        // 3. 음식 소모 시스템 상태 복원
        ConsumeFoodSystem.Instance.ForceSyncManualState(
            saveData.currentSatiety,
            saveData.maxSatiety,
            saveData.isWorkStoppedDueToStarvation
        );

        Debug.Log($"<color=cyan>[ConsumeFoodRecordData]</color> 🍲 음식 창고 완전 복구 완료! (총 {totalSlots}슬롯 / 현재 포만감: {saveData.currentSatiety})");
    }
}