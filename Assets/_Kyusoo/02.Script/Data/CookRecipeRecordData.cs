using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HDY.Cook;

public class CookRecipeRecordData : MonoBehaviour, IRecord
{
    private CookRecipeUnlockManager liveCookRecipeManager;

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
        liveCookRecipeManager = CookRecipeUnlockManager.Resolve(null);

        if (liveCookRecipeManager != null)
        {
            liveCookRecipeManager.OnRecipeUnlocked += OnRecipeUnlockedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveCookRecipeManager != null)
        {
            liveCookRecipeManager.OnRecipeUnlocked -= OnRecipeUnlockedHandler;
            liveCookRecipeManager = null;
        }
    }

    /// <summary>
    /// 새로운 요리 레시피 해금 이벤트 발생 시 실시간 세이브 진행
    /// </summary>
    private void OnRecipeUnlockedHandler(CookRecipeData unlockedRecipe)
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.cookRecipeUnlockedStates = new List<string>();
    }

    public void SaveData(string saveFilePath)
    {
        if (liveCookRecipeManager == null) RefreshManagerReference();
        if (liveCookRecipeManager == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.cookRecipeUnlockedStates = liveCookRecipeManager.UnlockedRecipeIds != null
            ? new List<string>(liveCookRecipeManager.UnlockedRecipeIds)
            : new List<string>();

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();

        if (liveCookRecipeManager == null)
        {
            return;
        }

        if (saveData.cookRecipeUnlockedStates != null)
        {
            liveCookRecipeManager.LoadUnlockedRecipeIds(saveData.cookRecipeUnlockedStates);
        }
    }
}