using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 탐험 씬(Main_World2 등)에서 플레이어(CharacterController)의 위치 좌표를 저장 및 복구
/// </summary>
public class PlayerPosRecordData : MonoBehaviour, IRecord
{
    private Vector3 lastKnownPosition;
    private bool hasValidKnownPosition = false;
    private CharacterController cachedController;

    //private void OnEnable()
    //{
    //    // 씬 전환 감지 이벤트 등록
    //    SceneManager.activeSceneChanged += OnActiveSceneChanged;
    //}

    //private void OnDisable()
    //{
    //    SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    //}

    private void Update()
    {
        // 탐험 씬일 때 플레이어의 좌표를 실시간 추적 및 캐싱
        if (IsExplorationScene())
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

    ///// <summary>
    ///// 씬이 변경되는 순간 호출 (이전 씬이 탐험 씬이었다면 캐싱된 위치 저장)
    ///// </summary>
    //private void OnActiveSceneChanged(Scene current, Scene next)
    //{
    //    if (current.name.ToLower().Contains("main_world") && hasValidKnownPosition)
    //    {
    //        SaveDataWithCachedPosition(RecordManager.Instance?.SaveFilePath);
    //        cachedController = null;
    //        hasValidKnownPosition = false;
    //    }
    //}

    private void OnApplicationQuit()
    {
        if (IsExplorationScene() && hasValidKnownPosition)
        {
            SaveDataWithCachedPosition(RecordManager.Instance?.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.lastPlayerPos = null;
        saveData.hasSavedPlayerPos = false;
    }

    public void SaveData(string saveFilePath)
    {
        if (string.IsNullOrEmpty(saveFilePath) || !IsExplorationScene()) return;

        if (cachedController == null)
        {
            cachedController = FindFirstObjectByType<CharacterController>();
        }

        if (cachedController != null)
        {
            lastKnownPosition = cachedController.transform.position;
            hasValidKnownPosition = true;
        }

        if (hasValidKnownPosition)
        {
            SaveDataWithCachedPosition(saveFilePath);
        }
    }

    private void SaveDataWithCachedPosition(string saveFilePath)
    {
        if (string.IsNullOrEmpty(saveFilePath)) return;

        SaveData currentData = RecordManager.Instance?.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.lastPlayerPos = new Vector3Data(lastKnownPosition);
        currentData.hasSavedPlayerPos = true;
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log($"<color=lime>[PlayerPosRecordData]</color> 📍 탐험 씬 플레이어 좌표 저장 완료: {lastKnownPosition}");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType != SceneType.Exploration) return;
        if (saveData == null || !saveData.hasSavedPlayerPos || saveData.lastPlayerPos == null) return;

        CharacterController controller = FindFirstObjectByType<CharacterController>();
        if (controller != null)
        {
            Vector3 targetPosition = saveData.lastPlayerPos.ToVector3();

            controller.enabled = false;
            controller.transform.position = targetPosition;
            controller.enabled = true;

            lastKnownPosition = targetPosition;
            hasValidKnownPosition = true;
            cachedController = controller;

        }
        else
        {
        }
    }

    private bool IsExplorationScene()
    {
        string sceneName = SceneManager.GetActiveScene().name.ToLower();
        return sceneName.Contains("main_world");
    }
}