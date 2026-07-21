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
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
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

        currentData.warehouseStorageData = new ContainerData
        {
            width = liveWarehouse.storage.width,
            height = liveWarehouse.storage.height
        };
        currentData.warehouseStorageData.slots = new List<ItemStackData>();

        if (liveWarehouse.storage.slots != null)
        {
            foreach (var slot in liveWarehouse.storage.slots)
            {
                currentData.warehouseStorageData.slots.Add(new ItemStackData
                {
                    itemId = (slot != null && !slot.IsEmpty) ? slot.itemId : "",
                    amount = slot != null ? slot.amount : 0
                });
            }
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[WarehouseRecordData]</color> 창고 자원 변동 감지를 통한 데이터 업데이트");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration)
        {
            Debug.Log("[WarehouseRecordData] 탐험 씬이므로 실물 창고 복구를 스킵합니다.");
            return;
        }

        RefreshWarehouseReference();

        if (liveWarehouse == null || liveWarehouse.storage == null || saveData.warehouseStorageData == null ||
            saveData.warehouseStorageData.width <= 0 || saveData.warehouseStorageData.height <= 0)
        {
            Debug.LogWarning("[WarehouseRecordData] ⚠️ 세이브 파일의 창고 규격이 비어있거나 비정상적입니다. 인스펙터 초기 기본 설정을 유지합니다.");
            return;
        }

        liveWarehouse.storage.width = saveData.warehouseStorageData.width;
        liveWarehouse.storage.height = saveData.warehouseStorageData.height;

        int totalSlots = saveData.warehouseStorageData.slots.Count;
        liveWarehouse.storage.slots = new ItemStack[totalSlots];

        for (int i = 0; i < totalSlots; i++)
        {
            liveWarehouse.storage.slots[i] = new ItemStack();
            var savedStack = saveData.warehouseStorageData.slots[i];

            if (savedStack != null && !string.IsNullOrEmpty(savedStack.itemId) && savedStack.amount > 0)
            {
                liveWarehouse.storage.slots[i].Set(savedStack.itemId, savedStack.amount);
            }
            else
            {
                liveWarehouse.storage.slots[i].Clear();
            }
        }

        var storageChangedField = typeof(WarehouseInventory).GetField("OnStorageChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (storageChangedField != null)
        {
            Action onStorageChanged = storageChangedField.GetValue(liveWarehouse) as Action;
            onStorageChanged?.Invoke();
        }

        var rowCountChangedField = typeof(WarehouseInventory).GetField("OnRowCountChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (rowCountChangedField != null)
        {
            Action onRowCountChanged = rowCountChangedField.GetValue(liveWarehouse) as Action;
            onRowCountChanged?.Invoke();
        }

        Debug.Log("<color=cyan>[WarehouseRecordData]</color> 🟦 안전 규격 검증 통과 ➡️ 창고 실물 오브젝트 완전 복구 완료!");
    }
}