using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using HDY.Capture;

public class MemRecordData : MonoBehaviour, IRecord
{
    private MemCaptureManager liveMemManager;

    // 🌟 [핵심] 런타임 동안 종족별 최초 포획 시간을 보관할 딕셔너리 (MemId -> Timestamp)
    private static Dictionary<string, long> firstCaptureDict = new Dictionary<string, long>();

    /// <summary>
    /// 외부 UI(도감 등)에서 특정 MemId의 최초 포획 시간을 조회할 때 사용하는 정적 메서드
    /// </summary>
    public static long? GetFirstCapturedTimestamp(string memId)
    {
        if (!string.IsNullOrEmpty(memId) && firstCaptureDict.TryGetValue(memId, out long timestamp))
        {
            return timestamp;
        }
        return null;
    }

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
        liveMemManager = FindFirstObjectByType<MemCaptureManager>();
        if (liveMemManager != null)
        {
            liveMemManager.OnCapturedMemsChanged += OnCapturedMemsChangedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveMemManager != null)
        {
            liveMemManager.OnCapturedMemsChanged -= OnCapturedMemsChangedHandler;
            liveMemManager = null;
        }
    }

    /// <summary>
    /// 포획 멤 인벤토리 변동 감지 이벤트 핸들러
    /// </summary>
    private void OnCapturedMemsChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        // 🌟 1. 현재 포획된 멤 목록을 스캔하여 신규 MemId 발견 시 최초 포획 시간 기록
        CheckAndRegisterFirstCaptureTimestamps();

        // 2. 파일 저장
        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    /// <summary>
    /// 🌟 포획된 멤 목록에서 미등록된 MemId가 있으면 현재 시간(Timestamp)으로 최초 포획 기록 등록
    /// </summary>
    private void CheckAndRegisterFirstCaptureTimestamps()
    {
        if (liveMemManager == null || liveMemManager.CapturedMems == null) return;

        long currentUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var entry in liveMemManager.CapturedMems)
        {
            if (entry == null || string.IsNullOrEmpty(entry.MemId)) continue;

            // 딕셔너리에 아직 기록되지 않은 MemId라면 최초 포획으로 판정하고 시간 기록
            if (!firstCaptureDict.ContainsKey(entry.MemId))
            {
                firstCaptureDict[entry.MemId] = currentUnixTimestamp;
                Debug.Log($"<color=cyan>[MemRecordData]</color> 🎉 신규 종족 최초 포획 감지! MemId: {entry.MemId} | 포획 시각: {DateTimeOffset.FromUnixTimeSeconds(currentUnixTimestamp).ToLocalTime():yyyy-MM-dd HH:mm}");
            }
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.unlockedPageCount = 2;
        saveData.serializedCapturedMems = new List<CapturedMemEntry>();
        saveData.firstCapturedTimestamps = new List<MemFirstCapturedEntry>();

        int defaultMaxCapacity = 48 * 10;
        for (int i = 0; i < defaultMaxCapacity; i++)
        {
            saveData.serializedCapturedMems.Add(CapturedMemEntry.CreateEmpty());
        }
    }

    public void SaveData(string saveFilePath)
    {
        if (liveMemManager == null) RefreshManagerReference();
        if (liveMemManager == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        // 🌟 세이브 전 신규 최초 포획 건 재확인
        CheckAndRegisterFirstCaptureTimestamps();

        currentData.unlockedPageCount = liveMemManager.UnlockedPageCount;

        if (liveMemManager.CapturedMems != null)
        {
            currentData.serializedCapturedMems = new List<CapturedMemEntry>(liveMemManager.CapturedMems);
        }

        // 🌟 [핵심] 딕셔너리 -> SaveData 리스트로 직렬화 변환 저장
        currentData.firstCapturedTimestamps = firstCaptureDict.Select(kvp => new MemFirstCapturedEntry
        {
            memId = kvp.Key,
            firstCapturedTimestamp = kvp.Value
        }).ToList();

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[MemRecordData]</color> 🟩 포획 멤 인벤토리 및 최초 포획 타임스탬프 세이브 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveMemManager == null) return;

        firstCaptureDict.Clear();
        if (saveData.firstCapturedTimestamps != null)
        {
            foreach (var entry in saveData.firstCapturedTimestamps)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.memId))
                {
                    firstCaptureDict[entry.memId] = entry.firstCapturedTimestamp;
                }
            }
        }

        FieldInfo listField = typeof(MemCaptureManager).GetField("capturedMems", BindingFlags.NonPublic | BindingFlags.Instance);
        if (listField != null)
        {
            List<CapturedMemEntry> internalList = listField.GetValue(liveMemManager) as List<CapturedMemEntry>;
            if (internalList != null)
            {
                internalList.Clear();
                if (saveData.serializedCapturedMems != null && saveData.serializedCapturedMems.Count > 0)
                {
                    internalList.AddRange(saveData.serializedCapturedMems);
                }
            }
        }

        RecordManager.Instance.SetPrivateFieldSafely(liveMemManager, "unlockedPageCount", saveData.unlockedPageCount);

        MethodInfo ensureMethod = typeof(MemCaptureManager).GetMethod("EnsureCapacity", BindingFlags.NonPublic | BindingFlags.Instance);
        if (ensureMethod != null)
        {
            ensureMethod.Invoke(liveMemManager, null);
        }

        CheckAndRegisterFirstCaptureTimestamps();

        FieldInfo eventField = typeof(MemCaptureManager).GetField("OnCapturedMemsChanged", BindingFlags.NonPublic | BindingFlags.Instance);
        if (eventField != null)
        {
            MulticastDelegate eventDelegate = eventField.GetValue(liveMemManager) as MulticastDelegate;
            if (eventDelegate != null)
            {
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    handler.DynamicInvoke();
                }
            }
        }

        Debug.Log("<color=lime>[MemRecordData]</color> 👑 최초 포획 타임스탬프 및 멤 데이터 복구 성공!");
    }
}