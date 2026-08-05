using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaypointRecordData : MonoBehaviour, IRecord
{
    private WayPointManager liveWayPointManager;

    private void OnEnable()
    {
        RefreshManagerReference();
    }

    private void OnDisable()
    {
        UnsubscribeManager();
    }

    private void RefreshManagerReference()
    {
        UnsubscribeManager();
        liveWayPointManager = WayPointManager.Instance != null
            ? WayPointManager.Instance
            : FindFirstObjectByType<WayPointManager>();

        if (liveWayPointManager != null)
        {
            liveWayPointManager.OnWayPointUnlocked += OnWayPointUnlockedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveWayPointManager != null)
        {
            liveWayPointManager.OnWayPointUnlocked -= OnWayPointUnlockedHandler;
            liveWayPointManager = null;
        }
    }

    private void OnWayPointUnlockedHandler(WayPointRunTime state)
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.waypointInfo = new List<WaypointInfo>();
    }

    public void SaveData(string saveFilePath)
    {
        if (liveWayPointManager == null) RefreshManagerReference();
        if (liveWayPointManager == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.waypointInfo = new List<WaypointInfo>();

        foreach (var pair in liveWayPointManager.StatesById)
        {
            if (pair.Value == null) continue;

            currentData.waypointInfo.Add(new WaypointInfo
            {
                wayPointId = pair.Key,
                isUnlocked = pair.Value.IsActive
            });
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[WaypointRecordData]</color> 웨이포인트 해금 상태 데이터 저장 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveWayPointManager == null) return;

        // 1. 웨이포인트 해금 상태 복구
        if (saveData.waypointInfo != null && saveData.waypointInfo.Count > 0)
        {
            foreach (var info in saveData.waypointInfo)
            {
                if (string.IsNullOrEmpty(info.wayPointId)) continue;

                liveWayPointManager.ApplySavedUnlockedState(info.wayPointId, info.isUnlocked);
            }
        }

        // 🌟 2. WayPointMapUI 수정 없이 외부에서 Travel 모드로 사전 보정
        var mapUI = WayPointMapUI.Instance != null
            ? WayPointMapUI.Instance
            : FindFirstObjectByType<WayPointMapUI>(FindObjectsInactive.Include);

        if (mapUI != null)
        {
            // 영지 씬이거나 이동 가능 환경인 경우
            if (sceneType == SceneType.Territory || liveWayPointManager.IsTerritorySceneName(SceneManager.GetActiveScene().name))
            {
                bool wasVisible = mapUI.IsVisible;

                mapUI.PrepareOpen(WayPointMapOpenMode.Travel, mapUI.CurrentMap);

                if (!wasVisible)
                {
                    mapUI.PrepareClose();
                }
            }
        }

        Debug.Log("<color=cyan>[WaypointRecordData]</color> 웨이포인트 해금 상태 데이터 및 지도 모드 보정 완료!");
    }
}
