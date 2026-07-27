using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class ChestRecordData : MonoBehaviour, IRecord
{
    private HashSet<string> openedChestIds = new HashSet<string>();

    private void OnEnable()
    {
        SubscribeSceneChests();
    }

    private void OnDisable()
    {
        UnsubscribeSceneChests();
    }

    /// <summary>
    /// 현재 씬에 배치된 모든 Chest 오브젝트의 OpenChestId 이벤트를 탐색하여 구독
    /// </summary>
    private void SubscribeSceneChests()
    {
        UnsubscribeSceneChests();
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest != null)
            {
                chest.OpenChestId += OnChestOpenedHandler;
            }
        }
    }

    private void UnsubscribeSceneChests()
    {
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest != null)
            {
                chest.OpenChestId -= OnChestOpenedHandler;
            }
        }
    }

    /// <summary>
    /// 상자 개방 시 이벤트 수신 처리
    /// </summary>
    private void OnChestOpenedHandler(string chestId)
    {
        if (RecordManager.IsLoadingData || string.IsNullOrEmpty(chestId)) return;

        if (!openedChestIds.Contains(chestId))
        {
            openedChestIds.Add(chestId);
        }

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.chestInfo = new List<ChestInfo>();
    }

    public void SaveData(string saveFilePath)
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.chestInfo = new List<ChestInfo>();

        foreach (string id in openedChestIds)
        {
            currentData.chestInfo.Add(new ChestInfo
            {
                chestId = id,
                isOpen = true
            });
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[ChestRecordData]</color> 상자 개방 상태 데이터 저장 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        openedChestIds.Clear();

        if (saveData.chestInfo != null && saveData.chestInfo.Count > 0)
        {
            foreach (var info in saveData.chestInfo)
            {
                if (!string.IsNullOrEmpty(info.chestId) && info.isOpen)
                {
                    openedChestIds.Add(info.chestId);
                }
            }
        }

        // 씬 내 상자들 중 이미 열린(openedChestIds에 포함된) 상자는 즉시 파괴
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest == null) continue;

            string cId = GetChestIdReflectively(chest);

            if (!string.IsNullOrEmpty(cId) && openedChestIds.Contains(cId))
            {
                Destroy(chest.gameObject);
            }
        }

        // 파괴되고 남은 상자들에 대해 이벤트 재연결
        SubscribeSceneChests();

        Debug.Log("<color=cyan>[ChestRecordData]</color> 이미 개방된 상자 제거 및 상태 동기화 완료!");
    }

    /// <summary>
    /// Chest 스크립트의 chestId 필드를 안전하게 가져옵니다.
    /// </summary>
    private string GetChestIdReflectively(Chest chest)
    {
        if (chest == null) return string.Empty;
        var field = typeof(Chest).GetField("chestId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return field?.GetValue(chest) as string ?? string.Empty;
    }
}