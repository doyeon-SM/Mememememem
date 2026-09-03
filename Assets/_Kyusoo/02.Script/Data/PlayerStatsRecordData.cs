using System;
using System.IO;
using UnityEngine;
using KMS;
using KMS.Persistence;

public class PlayerStatsRecordData : MonoBehaviour, IRecord
{
    private PlayerStats targetPlayerStats;
    private PlayerCombatStats targetCombatStats; // [멤] 캐릭터 스탯 시스템 저장/복원용

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
        targetCombatStats = targetPlayerStats != null ? targetPlayerStats.GetComponent<PlayerCombatStats>() : null;

        if (targetPlayerStats != null)
        {
            // 🌟 PlayerStats가 파괴될 때(씬 이동 시) 세이브 트리거 구독
            targetPlayerStats.PlayerStatsDestroyed += OnPlayerStatsDestroyedHandler;

            // [멤] 중요행동 - 사망/부활. 굶주림 사망 시 도구를 제외한 아이템이 전손되므로,
            // 이 순간을 저장해두지 않으면 되감기로 손실을 무효화하거나 반대로 억울한 손해가 생긴다.
            targetPlayerStats.Died += OnPlayerLifecycleChangedHandler;
            targetPlayerStats.Revived += OnPlayerLifecycleChangedHandler;
        }
    }

    private void OnPlayerLifecycleChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;
        RecordManager.NotifyCriticalAction(RecordManager.SaveReason.PlayerLifecycle);
    }

    private void UnbindPlayerStats()
    {
        if (targetPlayerStats != null)
        {
            targetPlayerStats.PlayerStatsDestroyed -= OnPlayerStatsDestroyedHandler;
            targetPlayerStats.Died -= OnPlayerLifecycleChangedHandler;
            targetPlayerStats.Revived -= OnPlayerLifecycleChangedHandler;
            targetPlayerStats = null;
        }

        targetCombatStats = null;
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

            // [멤] 캐릭터 스탯 시스템 저장. targetCombatStats는 BindPlayerStats에서 같이 갱신되지만, stats 파라미터가 바인된
            // targetPlayerStats와 다를 수 있으므로(SaveDataWithStats가 외부 stats를 받는 경우) 여기서 다시 조회한다.
            PlayerCombatStats combatStats = stats.GetComponent<PlayerCombatStats>();
            if (combatStats != null)
            {
                currentData.playerInfo.combatStats = combatStats.CaptureSaveData();
            }
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

            // [멤] 캐릭터 스탯 시스템 복원. 세이브가 이전 버전(combatStats 필드 없음)이면 null이므로 RestoreSaveData 자체가 안전하게 무시한다.
            PlayerCombatStats combatStats = targetPlayerStats.GetComponent<PlayerCombatStats>();
            if (combatStats != null && saveData.playerInfo.combatStats != null)
            {
                combatStats.RestoreSaveData(saveData.playerInfo.combatStats);
            }
        }
    }
}