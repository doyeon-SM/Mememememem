using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KMS.InventoryDuped;

public class PlayerInventoryRecord : MonoBehaviour, IRecord
{
    private PlayerInventory liveInventory;

    [Header("자동 저장 설정")]
    [SerializeField] private float autoSaveInterval = 60f; // 1분 단위 저장

    private bool isDirty = false; // 데이터 변경 여부 플래그
    private Coroutine autoSaveCoroutine;

    private void OnEnable()
    {
        RefreshReference();

        // [멤] 저장 빈도 감축 - 자체 1분 자동저장 루틴을 돌리지 않는다.
        // 이제 자동저장은 RecordManager가 5분 주기로 전체 데이터를 한 번에 생성한다.
        // (StartAutoSaveRoutine/AutoSaveRoutine은 디버그용으로 남겨둔다.)
    }

    private void OnDisable()
    {
        // 씬 이동 및 비활성화 시 데이터가 변경된 상태라면 즉시 저장
        if (isDirty)
        {
            SaveData(RecordManager.Instance != null ? RecordManager.Instance.SaveFilePath : null);
        }

        StopAutoSaveRoutine();
        Unsubscribe();
    }

    private void OnApplicationQuit()
    {
        // 게임 정상 종료 시 즉시 저장
        if (isDirty)
        {
            SaveData(RecordManager.Instance != null ? RecordManager.Instance.SaveFilePath : null);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // 앱이 백그라운드로 내려가거나 창 이탈 시 데이터 유실 방지 저장
        if (pauseStatus && isDirty)
        {
            SaveData(RecordManager.Instance != null ? RecordManager.Instance.SaveFilePath : null);
        }
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

    // 🌟 이벤트 발생 시 디스크에 직접 쓰지 않고 Dirty 플래그만 세팅
    // 🌟 이벤트 발생 시 디스크에 직접 쓰지 않고 Dirty 플래그만 세팅
    private void OnInventoryDataChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;
        isDirty = true;
        RecordManager.NotifyDataChanged();
    }

    private void OnQuickSlotSelectionChangedHandler(int selectedIndex)
    {
        if (RecordManager.IsLoadingData) return;
        isDirty = true;
        RecordManager.NotifyDataChanged();
    }

    private void OnQuickSlotDataChangedHandler(int slotIndex)
    {
        if (RecordManager.IsLoadingData) return;
        isDirty = true;
        RecordManager.NotifyDataChanged();
    }

    private void StartAutoSaveRoutine()
    {
        StopAutoSaveRoutine();
        autoSaveCoroutine = StartCoroutine(AutoSaveRoutine());
    }

    private void StopAutoSaveRoutine()
    {
        if (autoSaveCoroutine != null)
        {
            StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }
    }

    private IEnumerator AutoSaveRoutine()
    {
        var wait = new WaitForSeconds(autoSaveInterval);
        while (true)
        {
            yield return wait;
            // 1분이 지났을 때 데이터 변경사항이 있을 때만 디스크 저장 실행
            if (isDirty && RecordManager.Instance != null)
            {
                SaveData(RecordManager.Instance.SaveFilePath);
            }
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
        if (string.IsNullOrEmpty(saveFilePath)) return;

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

        isDirty = false; // 디스크 저장 완료 후 Dirty 플래그 해제
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

        isDirty = false; // 복구 직후에는 저장할 필요 없으므로 false 세팅
        Debug.Log($"<color=cyan>[PlayerInventoryRecord]</color> 🎒 플레이어 인벤토리 완전 복구 완료! (언락 슬롯: {unlockedCount}칸)");
    }
}