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
    /// Ʃ�丮�� ���� ����/��ǥ ����/���� �Ϸ� �� �ǽð� ���̺� ����
    /// </summary>
    private void OnTutorialProgressChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
                        // [멤] 중요행동 - 튜토리얼 단계 진행은 되돌아가면 체감 손실이 커서 즉시 저장한다.
            RecordManager.NotifyCriticalAction(RecordManager.SaveReason.TutorialStep);
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

        // TutorialManager�κ��� ���� ������ ĸó
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
            // �ҷ��� �������� TutorialManager�� ����
            liveTutorialManager.ApplySnapshot(saveData.tutorialData);
        }
    }
}