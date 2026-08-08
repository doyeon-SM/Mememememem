using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HDY.Tutorial;

public class TutorialRecordData : MonoBehaviour, IRecord
{
    private TutorialManager liveTutorialManager;

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
        liveTutorialManager = TutorialManager.Resolve(null);

        if (liveTutorialManager != null)
        {
            liveTutorialManager.OnTutorialProgressChanged += OnTutorialProgressChangedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveTutorialManager != null)
        {
            liveTutorialManager.OnTutorialProgressChanged -= OnTutorialProgressChangedHandler;
            liveTutorialManager = null;
        }
    }

    /// <summary>
    /// 튜토리얼 스텝 변경/목표 진행/스텝 완료 시 실시간 세이브 실행
    /// </summary>
    private void OnTutorialProgressChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.tutorialData = new TutorialProgressSnapshot();
    }

    public void SaveData(string saveFilePath)
    {
        if (liveTutorialManager == null) RefreshManagerReference();
        if (liveTutorialManager == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        // TutorialManager로부터 진행 스냅샷 캡처
        currentData.tutorialData = liveTutorialManager.CaptureSnapshot();

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();

        if (liveTutorialManager == null) return;

        if (saveData.tutorialData != null)
        {
            // 불러온 스냅샷을 TutorialManager에 복원
            liveTutorialManager.ApplySnapshot(saveData.tutorialData);
        }
    }
}