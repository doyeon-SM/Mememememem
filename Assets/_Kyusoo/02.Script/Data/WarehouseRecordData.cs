using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HDY.Inventory;
using KMS.InventoryDuped;

public class WarehouseRecordData : MonoBehaviour, IRecord
{
    private WarehouseInventory liveWarehouse;

    private void OnEnable()
    {
        RefreshWarehouseReference();
    }

    private void OnDisable()
    {
        UnsubscribeWarehouse();
    }

    private void RefreshWarehouseReference()
    {
        UnsubscribeWarehouse();
        liveWarehouse = FindFirstObjectByType<WarehouseInventory>();

        if (liveWarehouse != null)
        {
            liveWarehouse.OnStorageChanged += OnWarehouseDataChangedHandler;
            liveWarehouse.OnRowCountChanged += OnWarehouseDataChangedHandler;
        }
    }

    private void UnsubscribeWarehouse()
    {
        if (liveWarehouse != null)
        {
            liveWarehouse.OnStorageChanged -= OnWarehouseDataChangedHandler;
            liveWarehouse.OnRowCountChanged -= OnWarehouseDataChangedHandler;
        }

        liveWarehouse = null;
    }

    private void OnWarehouseDataChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        // 일반 창고: 기본 10x2 = 20칸 규격 (startingRows = 2)
        saveData.warehouseStorageData = new ContainerData { width = 10, height = 2 };
        saveData.warehouseStorageData.slots = new List<ItemStackData>();

        for (int i = 0; i < 20; i++)
        {
            saveData.warehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }
    }

    public void SaveData(string saveFilePath)
    {
        if (liveWarehouse == null) RefreshWarehouseReference();
        if (liveWarehouse == null || liveWarehouse.storage == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.warehouseStorageData = RecordManager.Instance.PackContainerData(liveWarehouse.storage);

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[WarehouseRecordData]</color> 📦 일반 창고 데이터 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration) return;

        RefreshWarehouseReference();

        if (liveWarehouse == null || liveWarehouse.storage == null || saveData.warehouseStorageData == null ||
            saveData.warehouseStorageData.width <= 0 || saveData.warehouseStorageData.height <= 0)
        {
            Debug.LogWarning("[WarehouseRecordData] ⚠️ 세이브 파일의 창고 규격이 비어있거나 비정상적입니다.");
            return;
        }

        // 업그레이드로 늘어난 가로/세로 규격 및 슬롯 전체 복원
        RecordManager.Instance.UnpackContainerData(saveData.warehouseStorageData, liveWarehouse.storage);

        liveWarehouse.PublishWarehouseChanged();

        Debug.Log($"<color=cyan>[WarehouseRecordData]</color> 📦 일반 창고 완전 복구 완료! ({liveWarehouse.storage.height}행 / 총 {liveWarehouse.storage.slots.Length}슬롯)");
    }
}