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
            targetPlayerStats.HealthChanged += OnPlayerStatsChangedHandler;
            targetPlayerStats.HungerChanged += OnPlayerStatsChangedHandler;
        }
    }

    private void UnbindPlayerStats()
    {
        if (targetPlayerStats != null)
        {
            targetPlayerStats.HealthChanged -= OnPlayerStatsChangedHandler;
            targetPlayerStats.HungerChanged -= OnPlayerStatsChangedHandler;
            targetPlayerStats = null;
        }
    }

    private void OnPlayerStatsChangedHandler(float current, float max)
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

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (currentData.playerInfo == null) currentData.playerInfo = new PlayerInfo();

        if (targetPlayerStats != null)
        {
            currentData.playerInfo.maxHealth = targetPlayerStats.MaxHealth;
            currentData.playerInfo.maxHunger = targetPlayerStats.MaxHunger;
            currentData.playerInfo.currentHealth = targetPlayerStats.CurrentHealth;
            currentData.playerInfo.currentHunger = targetPlayerStats.CurrentHunger;
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[PlayerStatsRecordData]</color> 플레이어 스탯 데이터 저장 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        BindPlayerStats();

        if (saveData.playerInfo == null) return;

        if (targetPlayerStats != null)
        {
            // PlayerStats 기존 RestoreSaveData 사용
            targetPlayerStats.RestoreSaveData(new PlayerStatsSaveData
            {
                currentHealth = saveData.playerInfo.currentHealth,
                currentHunger = saveData.playerInfo.currentHunger
            });
        }

        Debug.Log("<color=cyan>[PlayerStatsRecordData]</color> 플레이어 스탯 데이터 복구 완료!");
    }
}