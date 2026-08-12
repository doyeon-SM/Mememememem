using System;
using System.IO;
using UnityEngine;
using KMS;
using KMS.Persistence;

public class PlayerStatsRecordData : MonoBehaviour, IRecord
{
    private PlayerStats targetPlayerStats;

    private void OnEnable()
    {
        BindPlayerStats();
    }

    private void OnDisable()
    {
        UnbindPlayerStats();
    }

    private void BindPlayerStats()
    {
        UnbindPlayerStats();
        targetPlayerStats = FindFirstObjectByType<PlayerStats>();

        if (targetPlayerStats != null)
        {
            // 🌟 PlayerStats가 파괴될 때(씬 이동 시) 세이브 트리거 구독
            targetPlayerStats.PlayerStatsDestroyed += OnPlayerStatsDestroyedHandler;
        }
    }

    private void UnbindPlayerStats()
    {
        if (targetPlayerStats != null)
        {
            targetPlayerStats.PlayerStatsDestroyed -= OnPlayerStatsDestroyedHandler;
            targetPlayerStats = null;
        }
    }

    // 1. 씬 이동 시 PlayerStats 파괴 직전에 트리거
    private void OnPlayerStatsDestroyedHandler(PlayerStats stats)
    {
        if (RecordManager.IsLoadingData) return;

        if (stats != null && RecordManager.Instance != null)
        {
            SaveDataWithStats(RecordManager.Instance.SaveFilePath, stats);
        }
    }

    // 2. 게임 정상 종료 시 트리거
    private void OnApplicationQuit()
    {
        TriggerSave();
    }

    // 3. 백그라운드 전환 및 강제 종료 준비 시 트리거
    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            TriggerSave();
        }
    }

    private void TriggerSave()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.playerInfo = new PlayerInfo();
    }

    public void SaveData(string saveFilePath)
    {
        if (targetPlayerStats == null) BindPlayerStats();
        SaveDataWithStats(saveFilePath, targetPlayerStats);
    }

    private void SaveDataWithStats(string saveFilePath, PlayerStats stats)
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (currentData.playerInfo == null) currentData.playerInfo = new PlayerInfo();

        if (stats != null)
        {
            PlayerStatsSaveData capturedStats = stats.CaptureSaveData();

            currentData.playerInfo.maxHealth = stats.MaxHealth;
            currentData.playerInfo.maxHunger = stats.MaxHunger;
            currentData.playerInfo.currentHealth = capturedStats.currentHealth;
            currentData.playerInfo.currentHunger = capturedStats.currentHunger;
            currentData.playerInfo.foodEffects = capturedStats.foodEffects;
        }

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        BindPlayerStats();

        if (saveData.playerInfo == null) return;

        if (targetPlayerStats != null)
        {
            targetPlayerStats.RestoreSaveData(new PlayerStatsSaveData
            {
                currentHealth = saveData.playerInfo.currentHealth,
                currentHunger = saveData.playerInfo.currentHunger,
                foodEffects = saveData.playerInfo.foodEffects
            });
        }
    }
}