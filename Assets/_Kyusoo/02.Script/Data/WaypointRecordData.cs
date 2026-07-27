using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

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
            liveWayPointManager.OnWayPointStateChanged += OnWayPointDataChangedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveWayPointManager != null)
        {
            liveWayPointManager.OnWayPointStateChanged -= OnWayPointDataChangedHandler;
            liveWayPointManager = null;
        }
    }

    private void OnWayPointDataChangedHandler(WayPointRunTime state)
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    /// <summary>
    /// 세이브 파일이 없을 때 초기 구조 생성
    /// </summary>
    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.waypointInfo = new List<WaypointInfo>();
    }

    /// <summary>
    /// 현재 WayPointManager에 존재하는 모든 웨이포인트의 해금 상태를 저장
    /// </summary>
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

    /// <summary>
    /// 씬 전환/게임 재시작 시 저정된 해금 상태를 WayPointManager에 적용
    /// </summary>
    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveWayPointManager == null) return;

        if (saveData.waypointInfo == null || saveData.waypointInfo.Count == 0) return;

        foreach (var info in saveData.waypointInfo)
        {
            if (string.IsNullOrEmpty(info.wayPointId)) continue;

            if (liveWayPointManager.StatesById.TryGetValue(info.wayPointId, out WayPointRunTime state))
            {
                state.IsActive = info.isUnlocked;

                if (state.Stone != null)
                {
                    state.Stone.SetUnlockedState(info.isUnlocked);
                }
            }
        }

        Debug.Log("<color=cyan>[WaypointRecordData]</color> 웨이포인트 해금 상태 데이터 복구 완료!");
    }
}