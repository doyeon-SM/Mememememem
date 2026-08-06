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
            elapsedTime = 300f,
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

        // 1. 인게임 환경 시간(elapsedTime) 저장은 항상 수행
        if (liveTimeManager != null)
        {
            if (currentData.timeData == null) currentData.timeData = new GameTimeSaveData();
            currentData.timeData.elapsedTime = liveTimeManager.ElapsedTime;
        }

        string activeSceneName = SceneManager.GetActiveScene().name.ToLower();

        // 🌟 [수정 조건 1] 영지 씬(territory)에서만 lastSaveRealTimeKst 시각을 최신화하여 저장!
        // 영지에서 종료/강제종료 하거나, 영지 -> 탐험/동굴 씬으로 이동하기 직전(현재 씬이 영지일 때)에만 동작합니다.
        if (activeSceneName.Contains("territory"))
        {
            string kstNow = DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
            if (currentData.timeData == null) currentData.timeData = new GameTimeSaveData();

            currentData.timeData.lastSaveRealTimeKst = kstNow;
            currentData.lastSaveTime = kstNow;
            Debug.Log($"<color=lime>[TimeRecordData]</color> ⏰ [영지 씬] KST 저장 시각 최신화 완료: {kstNow}");
        }
        else
        {
            Debug.Log($"<color=yellow>[TimeRecordData]</color> ⚠️ [영지 씬 아님: {activeSceneName}] lastSaveRealTimeKst 저장 스킵 (기존 시각 보존)");
        }

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();

        // 🌟 [수정 조건 2] lastSaveRealTimeKst는 로드하지 않음!
        // 오직 인게임 환경 시간(elapsedTime)만 복구
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
            Debug.Log($"<color=cyan>[TimeRecordData]</color> ⏰ 경과 시간 복구 완료: {targetElapsedTime:F1}초");
        }
    }
}