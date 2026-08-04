using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using HDY.Territory;

public class TimeRecordData : MonoBehaviour, IRecord
{
    private GameTimeManager liveTimeManager;

    private void OnEnable()
    {
        RefreshManagerReference();
    }

    private void RefreshManagerReference()
    {
        if (liveTimeManager == null)
        {
            liveTimeManager = GameTimeManager.Resolve(null);
        }
    }

    private void OnDestroy()
    {
        TrySaveTimeData();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            TrySaveTimeData();
        }
    }

    private void OnApplicationQuit()
    {
        TrySaveTimeData();
    }

    private void TrySaveTimeData()
    {
        if (RecordManager.Instance != null && !RecordManager.IsLoadingData && !RecordManager.IsSceneUnloading)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.timeData = new GameTimeSaveData
        {
            elapsedTime = 0f,
            lastSaveRealTimeKst = DateTime.UtcNow.ToString("o")
        };
        saveData.lastSaveTime = DateTime.UtcNow.ToString("o");
    }

    public void SaveData(string saveFilePath)
    {
        if (RecordManager.IsLoadingData || RecordManager.IsSceneUnloading) return;
        RefreshManagerReference();

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (liveTimeManager != null)
        {
            currentData.timeData.elapsedTime = liveTimeManager.ElapsedTime;
        }

        // 🌟 [수정] 오프라인 계산용 시각을 표준 ISO 8601 UTC로 작성
        string utcNowIso = DateTime.UtcNow.ToString("o");
        currentData.timeData.lastSaveRealTimeKst = utcNowIso;
        currentData.lastSaveTime = utcNowIso;

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[TimeRecordData]</color> ⏰ 시간 데이터 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveTimeManager != null && saveData.timeData != null)
        {
            float targetElapsedTime = saveData.timeData.elapsedTime;
            RecordManager.Instance.SetPrivateFieldSafely(liveTimeManager, "elapsedTime", targetElapsedTime);

            var territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                territoryData.SyncElapsedTimeFromGameTimeManager(targetElapsedTime);
            }

            MethodInfo syncMethod = typeof(GameTimeManager).GetMethod("SyncInitialState", BindingFlags.NonPublic | BindingFlags.Instance);
            syncMethod?.Invoke(liveTimeManager, null);
            Debug.Log($"<color=cyan>[TimeRecordData]</color> ⏰ 경과 시간 복구: {targetElapsedTime:F1}초");
        }
    }
}