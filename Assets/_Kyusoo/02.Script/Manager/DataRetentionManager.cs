using UnityEngine;
using SceneManagement = UnityEngine.SceneManagement;
using KMS.InventoryDuped;
using HDY.Capture;
using System.Collections.Generic;
using System.Linq;

public class DataRetentionManager : MonoBehaviour
{
    private void Start()
    {
        // 🌟 탐험 씬이 시작되자마자 세이브 장부를 대조하여 인벤토리 복구 연산을 시도합니다.
        ExecuteRetentionLoadProcess();
    }

    private void OnDisable()
    {
        // 🌟 [안전 구제책]: 유저가 포탈 상호작용 등에서 세이브 함수 호출을 누락하더라도, 
        // 탐험 씬을 떠나면서 이 컴포넌트가 파괴되기 직전에 자동으로 가방 데이터를 강제 저장하여 데이터 증발을 원천 차단합니다.
        if (RecordManager.Instance != null && SceneManagement.SceneManager.GetActiveScene().name.ToLower().Contains("adventure"))
        {
            RecordManager.Instance.ExecutePartialSaveForAdventure();
        }
    }

    /// <summary>
    /// 탐험 씬 시작 시 파일 장부를 대조하여 인벤토리를 비우고 정밀 복구 및 멤 상태를 바인딩합니다.
    /// </summary>
    private void ExecuteRetentionLoadProcess()
    {
        if (RecordManager.Instance == null)
        {
            Debug.LogWarning("[DataRetentionManager] RecordManager가 씬에 존재하지 않아 데이터 복구를 유보합니다.");
            return;
        }

        // 세이브 파일 자체가 아예 존재하지 않는 태초의 게임 시작 상태라면 복구 단계를 건너뛰어 탐험 씬 기획 초기 가방 상태를 보존합니다.
        if (!RecordManager.Instance.IsSaveFileExists())
        {
            Debug.Log("<color=cyan>[DataRetentionManager]</color> 최초 실행 상태(세이브 파일 없음)이므로 복구를 생략하고 초기 배치 상태를 유지합니다.");
            return;
        }

        // 파일이 존재한다면 최신 세이브 장부 데이터를 파싱해옵니다.
        TerritorySaveData latestData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (latestData == null || string.IsNullOrEmpty(latestData.lastSaveTime)) return;

        // 현재 탐험 씬의 플레이어 인벤토리 오브젝트 동기화
        // 🌟 비활성화 컴포넌트까지 추적하도록 보정
        PlayerInventory currentInventory = Object.FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (currentInventory != null)
        {
            RebuildInventoryFromSaveData(currentInventory, latestData);
        }

        // 현재 탐험 씬의 멤 창고 IsActive 활성화 여부 결속 바인딩
        MemCaptureManager currentCaptureManager = Object.FindObjectsByType<MemCaptureManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
        if (currentCaptureManager != null && latestData.serializedCapturedMems != null)
        {
            BindMemCaptureActiveStates(currentCaptureManager, latestData.serializedCapturedMems);
        }
    }

    /// <summary>
    /// 가방 슬롯 대장을 완전 공백(Clear) 상태로 밀어버린 후 장부 내용물 기반으로 재적재 복구합니다.
    /// </summary>
    private void RebuildInventoryFromSaveData(PlayerInventory targetInventory, TerritorySaveData saveData)
    {
        if (targetInventory == null || targetInventory.inventory == null || saveData == null) return;

        // A. 인벤토리 가방 내부 슬롯을 깔끔하게 전부 비워버립니다.
        if (targetInventory.inventory.slots != null)
        {
            for (int i = 0; i < targetInventory.inventory.slots.Length; i++)
            {
                if (targetInventory.inventory.slots[i] != null)
                    targetInventory.inventory.slots[i].Clear();
            }
        }

        // 퀵슬롯 초기화 방어선 가동
        if (targetInventory.quickSlots != null && targetInventory.quickSlots.slots != null)
        {
            for (int i = 0; i < targetInventory.quickSlots.slots.Length; i++)
            {
                if (targetInventory.quickSlots.slots[i] != null)
                    targetInventory.quickSlots.slots[i].Clear();
            }
        }

        // B. 파일 장부에 기록되어 있던 수량 그대로 AddItem 입고를 전개합니다.
        if (saveData.playerInventoryData != null && saveData.playerInventoryData.slots != null)
        {
            foreach (var savedItem in saveData.playerInventoryData.slots)
            {
                if (savedItem != null && !string.IsNullOrEmpty(savedItem.itemId) && savedItem.amount > 0)
                {
                    targetInventory.AddItem(savedItem.itemId, savedItem.amount);
                }
            }
        }

        // 인벤토리 변경 시스템 이벤트 강제 통지
        var onInventoryChangedField = typeof(PlayerInventory).GetField("OnInventoryChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var onInventoryChanged = onInventoryChangedField?.GetValue(targetInventory) as System.Action;
        onInventoryChanged?.Invoke();

        Debug.Log("<color=lime>[DataRetentionManager]</color> 탐험 씬 인벤토리 완전 밀어내기 후 장부 동기화 정산 완료!");
    }

    /// <summary>
    /// 멤 창고의 IsActive 활성화 수치만 온전하게 매칭 결속합니다.
    /// </summary>
    private void BindMemCaptureActiveStates(MemCaptureManager manager, List<CapturedMemEntry> savedMems)
    {
        if (manager == null || savedMems == null) return;

        var capMemsField = typeof(MemCaptureManager).GetField("capturedMems",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        List<CapturedMemEntry> runtimeMemsList = capMemsField?.GetValue(manager) as List<CapturedMemEntry>;

        if (runtimeMemsList == null) return;

        foreach (var savedMem in savedMems)
        {
            if (savedMem == null) continue;

            var runtimeMatch = runtimeMemsList.FirstOrDefault(m => m != null && m.KeyId == savedMem.KeyId);
            if (runtimeMatch != null)
            {
                runtimeMatch.IsActive = savedMem.IsActive; // 활성화 여부 정보 결속
            }
        }

        var changeEvent = typeof(MemCaptureManager).GetField("OnCapturedMemsChanged",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(manager) as System.Action;
        changeEvent?.Invoke();

        Debug.Log("<color=lime>[DataRetentionManager]</color> 탐험 씬 멤 리스트 IsActive 바인딩 완수!");
    }

    /// <summary>
    /// 🌟 [포탈 인터페이스 단방향 연동]: 영지나 다음 씬으로 넘어가기 직전 포탈 상호작용 지점에서 이 함수를 호출해줍니다.
    /// </summary>
    public void SaveCurrentProgressBeforeLeaveScene()
    {
        if (RecordManager.Instance != null)
        {
            RecordManager.Instance.ExecutePartialSaveForAdventure();
        }
    }
}