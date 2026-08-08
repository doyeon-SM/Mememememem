using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KMS.InventoryDuped;

public class PlayerInventoryRecord : MonoBehaviour, IRecord
{
    private PlayerInventory liveInventory;

    private void OnEnable()
    {
        RefreshReference();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void RefreshReference()
    {
        Unsubscribe();
        liveInventory = FindFirstObjectByType<PlayerInventory>();
        if (liveInventory != null)
        {
            liveInventory.OnInventoryChanged += OnInventoryDataChangedHandler;
            liveInventory.OnInventorySlotCountChanged += OnInventoryDataChangedHandler;
            liveInventory.OnSelectedQuickSlotChanged += OnQuickSlotSelectionChangedHandler;
            liveInventory.OnQuickSlotChanged += OnQuickSlotDataChangedHandler;
        }
    }

    private void Unsubscribe()
    {
        if (liveInventory != null)
        {
            liveInventory.OnInventoryChanged -= OnInventoryDataChangedHandler;
            liveInventory.OnInventorySlotCountChanged -= OnInventoryDataChangedHandler;
            liveInventory.OnSelectedQuickSlotChanged -= OnQuickSlotSelectionChangedHandler;
            liveInventory.OnQuickSlotChanged -= OnQuickSlotDataChangedHandler;
            liveInventory = null;
        }
    }

    private void OnInventoryDataChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    private void OnQuickSlotSelectionChangedHandler(int selectedIndex)
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    private void OnQuickSlotDataChangedHandler(int slotIndex)
    {
        if (RecordManager.IsLoadingData) return;
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        // 1. 인벤토리: 기본 10x6 = 60칸 규격
        saveData.playerInventoryData = new ContainerData { width = 10, height = 6 };
        saveData.playerInventoryData.slots = new List<ItemStackData>();
        for (int i = 0; i < 60; i++)
        {
            saveData.playerInventoryData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        // 2. 퀵슬롯: 기본 10x1 = 10칸 규격
        saveData.playerQuickSlotsData = new ContainerData { width = 10, height = 1 };
        saveData.playerQuickSlotsData.slots = new List<ItemStackData>();
        for (int i = 0; i < 10; i++)
        {
            saveData.playerQuickSlotsData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        saveData.selectedQuickSlotIndex = 0;
        saveData.unlockedInventorySlotCount = 10; // 초기 언락 슬롯 10개
    }

    public void SaveData(string saveFilePath)
    {
        if (liveInventory == null) RefreshReference();
        if (liveInventory == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.playerInventoryData = RecordManager.Instance.PackContainerData(liveInventory.inventory);
        currentData.playerQuickSlotsData = RecordManager.Instance.PackContainerData(liveInventory.quickSlots);
        currentData.selectedQuickSlotIndex = liveInventory.selectedQuickSlotIndex;
        currentData.unlockedInventorySlotCount = liveInventory.UnlockedInventorySlotCount;

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[PlayerInventoryRecord]</color> 🎒 플레이어 인벤토리 및 퀵슬롯 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshReference();
        if (liveInventory == null) return;

        // 1. 업그레이드 언락 슬롯 수 복원
        int unlockedCount = saveData.unlockedInventorySlotCount > 0 ? saveData.unlockedInventorySlotCount : liveInventory.StartingInventorySlotCount;
        RecordManager.Instance.SetPrivateFieldSafely(liveInventory, "unlockedInventorySlotCount", unlockedCount);

        // 2. 인벤토리 실물 슬롯 데이터 복원
        if (saveData.playerInventoryData != null)
        {
            RecordManager.Instance.UnpackContainerData(saveData.playerInventoryData, liveInventory.inventory);
        }

        // 3. 퀵슬롯 데이터 복원
        if (saveData.playerQuickSlotsData != null)
        {
            RecordManager.Instance.UnpackContainerData(saveData.playerQuickSlotsData, liveInventory.quickSlots);
        }

        // 4. 선택된 퀵슬롯 인덱스 복원
        liveInventory.selectedQuickSlotIndex = liveInventory.quickSlots.IsValidIndex(saveData.selectedQuickSlotIndex) ? saveData.selectedQuickSlotIndex : 0;

        liveInventory.PublishInventoryChanged();

        Debug.Log($"<color=cyan>[PlayerInventoryRecord]</color> 🎒 플레이어 인벤토리 완전 복구 완료! (언락 슬롯: {unlockedCount}칸)");
    }
}