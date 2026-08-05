using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WaypointRecordData : MonoBehaviour, IRecord
{
    private WayPointManager liveWayPointManager;
    private List<WaypointInfo> cachedLoadedWaypointInfo = new List<WaypointInfo>();
    private bool isDataApplied = false;
    private float loadProtectionTimer = 0f;
    private const float SAVE_PROTECTION_DURATION = 3.0f; // 🌟 3초로 넉넉하게 늘려 탐험 씬 청크 로딩 완벽 방어

    private void OnEnable()
    {
        RefreshManagerReference();
    }

    private void OnDisable()
    {
        UnsubscribeManager();
    }

    private void Update()
    {
        if (loadProtectionTimer > 0f)
        {
            loadProtectionTimer -= Time.unscaledDeltaTime;
        }
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
        if (RecordManager.IsLoadingData || loadProtectionTimer > 0f || !isDataApplied) return;

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
        // 🌟 [핵심 방어] 데이터가 아직 적용되지 않았거나 로딩/보호 시간 내에는 절대 덮어쓰기(유실) 방지
        if (RecordManager.IsLoadingData || RecordManager.IsSceneUnloading || loadProtectionTimer > 0f || !isDataApplied)
        {
            return;
        }

        if (liveWayPointManager == null) RefreshManagerReference();
        if (liveWayPointManager == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        // 탐험 씬에서 매니저 데이터가 아직 덜 불려왔다면 기존 세이브를 보호하기 위해 저장 스킵
        if (liveWayPointManager.StatesById == null || liveWayPointManager.StatesById.Count == 0)
        {
            return;
        }

        List<WaypointInfo> updatedWaypoints = new List<WaypointInfo>();

        // 1. 현재 런타임 상태 반영
        foreach (var pair in liveWayPointManager.StatesById)
        {
            if (pair.Value == null) continue;

            updatedWaypoints.Add(new WaypointInfo
            {
                wayPointId = pair.Key,
                isUnlocked = pair.Value.IsActive
            });
        }

        // 🌟 [안전망] 만약 기존 세이브 파일에 해금되어 있던(true) 정보가 있는데, 
        // 현재 탐험 씬 로딩 직후라 스톤이 덜 생성되어 갯수가 적거나 전부 false로 잡히는 현상 방어
        if (currentData.waypointInfo != null && currentData.waypointInfo.Count > 0)
        {
            foreach (var oldInfo in currentData.waypointInfo)
            {
                if (oldInfo.isUnlocked)
                {
                    var match = updatedWaypoints.Find(w => w.wayPointId == oldInfo.wayPointId);
                    if (match != null)
                    {
                        match.isUnlocked = true; // 기존에 해금된 적이 있다면 무조건 유지
                    }
                    else
                    {
                        updatedWaypoints.Add(new WaypointInfo { wayPointId = oldInfo.wayPointId, isUnlocked = true });
                    }
                }
            }
        }

        currentData.waypointInfo = updatedWaypoints;
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[WaypointRecordData]</color> 웨이포인트 해금 상태 데이터 안전 저장 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();

        // 3초간 세이브 덮어쓰기 완전 차단 보호 가동
        loadProtectionTimer = SAVE_PROTECTION_DURATION;
        isDataApplied = false;

        if (saveData.waypointInfo != null)
        {
            cachedLoadedWaypointInfo = new List<WaypointInfo>(saveData.waypointInfo);
        }

        StartCoroutine(ApplyDataPersistentRoutine());

        var mapUI = WayPointMapUI.Instance != null
            ? WayPointMapUI.Instance
            : FindFirstObjectByType<WayPointMapUI>(FindObjectsInactive.Include);

        if (mapUI != null && liveWayPointManager != null)
        {
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

        Debug.Log("<color=cyan>[WaypointRecordData]</color> 탐험/영지 씬 웨이포인트 데이터 적용 루틴 가동!");
    }

    private IEnumerator ApplyDataPersistentRoutine()
    {
        float timer = 0f;
        // 탐험 씬의 월드 청크 및 스톤 생성 대기 시간을 고려하여 2초 동안 매 프레임 강제 주입
        while (timer < 2.0f)
        {
            timer += Time.unscaledDeltaTime;

            if (liveWayPointManager == null)
            {
                RefreshManagerReference();
            }

            if (liveWayPointManager != null && cachedLoadedWaypointInfo != null)
            {
                foreach (var info in cachedLoadedWaypointInfo)
                {
                    if (string.IsNullOrEmpty(info.wayPointId)) continue;
                    if (info.isUnlocked)
                    {
                        liveWayPointManager.ApplySavedUnlockedState(info.wayPointId, true);
                    }
                }
            }
            yield return null;
        }

        isDataApplied = true; 
    }
}