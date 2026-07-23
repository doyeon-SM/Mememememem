using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using KMS.InventoryDuped;

public class PlayerInventoryRecord : MonoBehaviour, IRecord
{
    private PlayerInventory liveInventory;

    private void OnEnable()
    {
        RefreshInventoryReference();
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
    }

    private void RefreshInventoryReference()
    {
        UnsubscribeInventory();
        liveInventory = FindFirstObjectByType<PlayerInventory>();
        if (liveInventory != null)
        {
            liveInventory.OnInventoryChanged += OnInventoryChangedHandler;
        }
    }

    private void UnsubscribeInventory()
    {
        if (liveInventory != null)
        {
            liveInventory.OnInventoryChanged -= OnInventoryChangedHandler;
            liveInventory = null;
        }
    }

    private void OnInventoryChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.playerInventoryData = new ContainerData { width = 10, height = 6 };
        saveData.playerQuickSlotsData = new ContainerData { width = 10, height = 1 };
        saveData.selectedQuickSlotIndex = 0;

        for (int i = 0; i < 60; i++) saveData.playerInventoryData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        for (int i = 0; i < 10; i++) saveData.playerQuickSlotsData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
    }

    public void SaveData(string saveFilePath)
    {
        if (liveInventory == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (liveInventory.inventory != null) currentData.playerInventoryData = RecordManager.Instance.PackContainerData(liveInventory.inventory);
        if (liveInventory.quickSlots != null) currentData.playerQuickSlotsData = RecordManager.Instance.PackContainerData(liveInventory.quickSlots);
        currentData.selectedQuickSlotIndex = liveInventory.selectedQuickSlotIndex;

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

        string outputJson = JsonUtility.ToJson(currentData, true);
        File.WriteAllText(saveFilePath, outputJson);
        Debug.Log("<color=lime>[PlayerInventoryRecord]</color> 인벤토리 데이터 변경 감지 및 데이터 업데이트");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshInventoryReference();
        if (liveInventory == null) return;

        if (saveData.playerInventoryData != null)
            RecordManager.Instance.UnpackContainerData(saveData.playerInventoryData, liveInventory.inventory);

        if (saveData.playerQuickSlotsData != null)
            RecordManager.Instance.UnpackContainerData(saveData.playerQuickSlotsData, liveInventory.quickSlots);

        if (liveInventory.quickSlots != null)
        {
            liveInventory.selectedQuickSlotIndex = liveInventory.quickSlots.IsValidIndex(saveData.selectedQuickSlotIndex)
                ? saveData.selectedQuickSlotIndex : 0;
        }

        var onInventoryChangedField = typeof(PlayerInventory).GetField("OnInventoryChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (onInventoryChangedField != null)
        {
            System.Action onInventoryChanged = onInventoryChangedField.GetValue(liveInventory) as System.Action;
            onInventoryChanged?.Invoke();
        }

        var notifyAllQuickSlotsMethod = typeof(PlayerInventory).GetMethod("NotifyAllQuickSlotsChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (notifyAllQuickSlotsMethod != null) notifyAllQuickSlotsMethod.Invoke(liveInventory, null);
    }
}