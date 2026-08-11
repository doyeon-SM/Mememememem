using System;
using System.IO;
using System.Collections;
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

    [Header("자동 저장 설정")]
    [SerializeField] private float autoSaveInterval = 30f; // 30초마다 위치 자동 저장

    private bool isDirty = false; // 위치 변동 여부 플래그
    private Coroutine autoSaveRoutine;

    // 🌟 위치 저장 대상 씬 목록 (Territory는 제외)
    private readonly List<string> targetScenes = new List<string> { "Main_World_3", "Main_World_Cave" };

    private void OnEnable()
    {
        StartAutoSaveRoutine();
    }

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
                // 위치가 일정 거리 이상 변했을 때만 Dirty 플래그 세팅
                if (Vector3.SqrMagnitude(cachedController.transform.position - lastKnownPosition) > 0.01f)
                {
                    lastKnownPosition = cachedController.transform.position;
                    hasValidKnownPosition = true;
                    isDirty = true;
                }
            }
        }
    }

    private void OnDisable()
    {
        StopAutoSaveRoutine();

        // 씬 전환/비활성화 시점에 변경된 위치가 있다면 즉시 저장
        if (isDirty && hasValidKnownPosition && IsValidTargetScene(SceneManager.GetActiveScene().name))
        {
            SaveData(RecordManager.Instance?.SaveFilePath);
        }
    }

    private void OnApplicationQuit()
    {
        // 정상 종료 시 저장
        if (isDirty && hasValidKnownPosition && IsValidTargetScene(SceneManager.GetActiveScene().name))
        {
            SaveData(RecordManager.Instance?.SaveFilePath);
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        // 모바일/PC 창 이탈 및 백그라운드 전환 시 강제 종료 대비 즉시 저장
        if (pauseStatus && isDirty && hasValidKnownPosition && IsValidTargetScene(SceneManager.GetActiveScene().name))
        {
            SaveData(RecordManager.Instance?.SaveFilePath);
        }
    }

    private void StartAutoSaveRoutine()
    {
        StopAutoSaveRoutine();
        autoSaveRoutine = StartCoroutine(AutoSaveRoutine());
    }

    private void StopAutoSaveRoutine()
    {
        if (autoSaveRoutine != null)
        {
            StopCoroutine(autoSaveRoutine);
            autoSaveRoutine = null;
        }
    }

    /// <summary>
    /// 강제 종료 손실을 방지하기 위한 주기적 위치 자동 저장
    /// </summary>
    private IEnumerator AutoSaveRoutine()
    {
        var wait = new WaitForSeconds(autoSaveInterval);
        while (true)
        {
            yield return wait;

            if (isDirty && hasValidKnownPosition && IsValidTargetScene(SceneManager.GetActiveScene().name))
            {
                SaveData(RecordManager.Instance?.SaveFilePath);
            }
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
        isDirty = false; // 저장 완료 시 Dirty 해제
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
            isDirty = false;

            Debug.Log($"<color=cyan>[PlayerPosRecordData]</color> 📍 [{currentSceneName}] 플레이어 위치 복구 완료: {targetPosition}");
        }
    }

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