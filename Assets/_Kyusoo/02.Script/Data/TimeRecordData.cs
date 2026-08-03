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
        if (RecordManager.Instance != null && !RecordManager.IsLoadingData)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.timeData = new GameTimeSaveData
        {
            elapsedTime = 0f,
            lastSaveRealTimeKst = DateTime.UtcNow.AddHours(9).ToString("o")
        };
        saveData.lastSaveTime = DateTime.UtcNow.ToString("o");
    }

    public void SaveData(string saveFilePath)
    {
        RefreshManagerReference();
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (liveTimeManager != null)
        {
            currentData.timeData.elapsedTime = liveTimeManager.ElapsedTime;
        }

        string activeSceneName = SceneManager.GetActiveScene().name.ToLower();
        if (activeSceneName.Contains("territory"))
        {
            if (liveTimeManager != null)
            {
                currentData.timeData.lastSaveRealTimeKst = liveTimeManager.CurrentRealTimeKst.ToString("o");
            }
            else
            {
                currentData.timeData.lastSaveRealTimeKst = DateTime.UtcNow.AddHours(9).ToString("o");
            }
        }

        // 🌟 종료/일시정지 전용 타임스탬프 갱신
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[TimeRecordData]</color> ⏰ 게임 종료/일시정지 시점 시각 및 진행 시간 세이브 성공!");
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
            Debug.Log($"<color=cyan>[TimeRecordData]</color> ⏰ 플레이 시간 데이터 복구 완료: {targetElapsedTime:F1}초");
        }
    }
}