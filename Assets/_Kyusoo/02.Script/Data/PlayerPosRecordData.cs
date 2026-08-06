using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 지정된 씬(Main_World_3, Main_World_Cave 등)별 플레이어(CharacterController) 위치 좌표를 저장 및 복구
/// </summary>
public class PlayerPosRecordData : MonoBehaviour, IRecord
{
    private Vector3 lastKnownPosition;
    private bool hasValidKnownPosition = false;
    private CharacterController cachedController;

    // 🌟 위치 저장 대상 씬 목록 (Territory는 제외)
    private readonly List<string> targetScenes = new List<string> { "Main_World_3", "Main_World_Cave" };

    private void Update()
    {
        // 위치 저장 대상 씬일 때만 플레이어 좌표 실시간 추적
        if (IsValidTargetScene(SceneManager.GetActiveScene().name))
        {
            if (cachedController == null)
            {
                cachedController = FindFirstObjectByType<CharacterController>();
            }

            if (cachedController != null)
            {
                lastKnownPosition = cachedController.transform.position;
                hasValidKnownPosition = true;
            }
        }
    }

    private void OnApplicationQuit()
    {
        // 앱 종료 시점에 저장 대상 씬이면 위치 저장 수행
        if (IsValidTargetScene(SceneManager.GetActiveScene().name) && hasValidKnownPosition)
        {
            SaveData(RecordManager.Instance?.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.playerPosDataList = new List<ScenePlayerPosData>();

        foreach (string sceneName in targetScenes)
        {
            saveData.playerPosDataList.Add(new ScenePlayerPosData
            {
                sceneName = sceneName,
                lastPlayerPos = null,
                hasSavedPlayerPos = false
            });
        }
    }

    public void SaveData(string saveFilePath)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrEmpty(saveFilePath) || !IsValidTargetScene(currentSceneName)) return;

        if (cachedController == null)
        {
            cachedController = FindFirstObjectByType<CharacterController>();
        }

        if (cachedController != null)
        {
            lastKnownPosition = cachedController.transform.position;
            hasValidKnownPosition = true;
        }

        if (!hasValidKnownPosition) return;

        SaveData currentData = RecordManager.Instance?.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        if (currentData.playerPosDataList == null)
        {
            currentData.playerPosDataList = new List<ScenePlayerPosData>();
        }

        ScenePlayerPosData targetPosData = currentData.playerPosDataList.Find(
            x => string.Equals(x.sceneName, currentSceneName, StringComparison.OrdinalIgnoreCase));

        if (targetPosData == null)
        {
            targetPosData = new ScenePlayerPosData { sceneName = currentSceneName };
            currentData.playerPosDataList.Add(targetPosData);
        }

        targetPosData.lastPlayerPos = new Vector3Data(lastKnownPosition);
        targetPosData.hasSavedPlayerPos = true;
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log($"<color=lime>[PlayerPosRecordData]</color> 📍 [{currentSceneName}] 플레이어 좌표 저장 완료: {lastKnownPosition}");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (saveData == null || saveData.playerPosDataList == null) return;

        string currentSceneName = SceneManager.GetActiveScene().name;

        ScenePlayerPosData targetPosData = saveData.playerPosDataList.Find(
            x => string.Equals(x.sceneName, currentSceneName, StringComparison.OrdinalIgnoreCase));

        if (targetPosData == null || !targetPosData.hasSavedPlayerPos || targetPosData.lastPlayerPos == null)
        {
            Debug.Log($"[PlayerPosRecordData] [{currentSceneName}] 저장된 위치 데이터가 없어 좌표 복구를 건너땁니다.");
            return;
        }

        CharacterController controller = FindFirstObjectByType<CharacterController>();
        if (controller != null)
        {
            Vector3 targetPosition = targetPosData.lastPlayerPos.ToVector3();

            controller.enabled = false;
            controller.transform.position = targetPosition;
            controller.enabled = true;

            lastKnownPosition = targetPosition;
            hasValidKnownPosition = true;
            cachedController = controller;

            Debug.Log($"<color=cyan>[PlayerPosRecordData]</color> 📍 [{currentSceneName}] 플레이어 위치 복구 완료: {targetPosition}");
        }
    }

    /// <summary>
    /// 저장 대상 씬(Main_World_3, Main_World_Cave 등)인지 검사 (Territory 등은 false)
    /// </summary>
    private bool IsValidTargetScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;

        foreach (string target in targetScenes)
        {
            if (string.Equals(sceneName, target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}