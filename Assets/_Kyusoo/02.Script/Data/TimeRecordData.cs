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

    private void OnDisable()
    {
        // 🌟 [교정]: 씬 해제 도중 OnDestroy에서 시간을 저장할 수 있도록 OnDisable에서 reference를 null로 만들지 않습니다.
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
            lastSaveRealTimeKst = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss")
        };
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
                currentData.timeData.lastSaveRealTimeKst = liveTimeManager.CurrentRealTimeKst.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                // 🌟 [폴백 안전 장치]: 씬 파괴 타이밍에 GameTimeManager가 먼저 Unload되었을 경우 KST 실시간 백업
                currentData.timeData.lastSaveRealTimeKst = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[TimeRecordData]</color> 🟩 시간 및 일자 데이터 안전 세이브 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();

        if (liveTimeManager != null)
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

            Debug.Log($"<color=cyan>[TimeRecordData]</color> 🟦 인게임 누적 시간 복구 완료: {targetElapsedTime:F1}초");
        }
    }
}