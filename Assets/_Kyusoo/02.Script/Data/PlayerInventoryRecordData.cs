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

        // 🌟 [수정]: 세이브 파일 읽기 실패 시 데이터 초기화로 인한 기존 데이터 유실 방지
        if (currentData == null)
        {
            Debug.LogWarning("[PlayerInventoryRecord] 기존 세이브 데이터를 읽어오지 못해 인벤토리 단독 저장을 중단합니다.");
            return;
        }

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
        if (liveInventory == null)
        {
            Debug.LogWarning("[PlayerInventoryRecord] 씬에서 PlayerInventory 참조를 찾을 수 없습니다.");
            return;
        }

        // 🌟 [수정]: PlayerInventory 내부 컨테이너 생성 여부 방어막
        if (liveInventory.inventory != null && saveData.playerInventoryData != null)
        {
            RecordManager.Instance.UnpackContainerData(saveData.playerInventoryData, liveInventory.inventory);
        }

        if (liveInventory.quickSlots != null && saveData.playerQuickSlotsData != null)
        {
            RecordManager.Instance.UnpackContainerData(saveData.playerQuickSlotsData, liveInventory.quickSlots);

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