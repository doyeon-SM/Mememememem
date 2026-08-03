using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어의 최종 위치 씬(LastPlayScene)을 감지 및 직렬화하고,
/// 강제 종료 시점(OnApplicationQuit)에 안전하게 씬 데이터를 기록하는 IRecord 구현 스크립트.
/// </summary>
public class SceneRecordData : MonoBehaviour, IRecord
{
    private void OnApplicationQuit()
    {
        // 강제 종료 또는 게임 종료 버튼 클릭 시 현재 위치 씬 데이터를 기록
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
            Debug.Log("<color=yellow>[SceneRecordData]</color> 🛑 게임 종료 감지 ➡️ 현재 씬 정보 실시간 세이브 기록 완료");
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.lastPlayScene = "Main_World2";
    }

    public void SaveData(string saveFilePath)
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        string currentSceneName = SceneManager.GetActiveScene().name;

        if (!string.IsNullOrEmpty(currentSceneName) && !currentSceneName.ToLower().Contains("title"))
        {
            currentData.lastPlayScene = currentSceneName;
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (!string.IsNullOrEmpty(saveData.lastPlayScene))
        {
            Debug.Log($"<color=cyan>[SceneRecordData]</color> 🎬 최근 플레이 씬 불러오기 완료: {saveData.lastPlayScene}");
        }
    }
}