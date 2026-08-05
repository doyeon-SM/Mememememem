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
        string kstNow = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
        saveData.timeData = new GameTimeSaveData
        {
            elapsedTime = 0f,
            lastSaveRealTimeKst = kstNow
        };
        saveData.lastSaveTime = kstNow;
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

        string activeSceneName = SceneManager.GetActiveScene().name.ToLower();

        // 🌟 [핵심 방어] 타이틀 씬이거나 인게임 씬(Territory/Main_World)이 아닌 경우 저장 시각(lastSaveRealTimeKst)을 갱신하지 않고 이전 값 유지!
        if (!activeSceneName.Contains("title"))
        {
            string kstNow = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            if (currentData.timeData == null) currentData.timeData = new GameTimeSaveData();

            currentData.timeData.lastSaveRealTimeKst = kstNow;
            currentData.lastSaveTime = kstNow;
            Debug.Log($"<color=lime>[TimeRecordData]</color> ⏰ KST 인게임 저장 시각 최신화: {kstNow}");
        }
        else
        {
            Debug.Log("<color=yellow>[TimeRecordData]</color> ⚠️ 타이틀 씬 저장 요청 감지: lastSaveRealTimeKst 보존됨");
        }

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
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