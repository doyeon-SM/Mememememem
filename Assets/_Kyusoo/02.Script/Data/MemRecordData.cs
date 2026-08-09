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
    private MemDexRecordManager liveDexRecordManager;

    private static Dictionary<string, long> firstCaptureDict = new Dictionary<string, long>();

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
        liveDexRecordManager = MemDexRecordManager.Resolve(liveDexRecordManager);

        if (liveMemManager != null)
        {
            liveMemManager.OnCapturedMemsChanged += OnCapturedDataChangedHandler;
            liveMemManager.OnStorageCapacityChanged += OnCapturedDataChangedHandler;
        }

        if (liveDexRecordManager != null)
        {
            liveDexRecordManager.OnFirstCaptureRecorded += OnFirstCaptureRecordedHandler;
        }

        if (ConsumeFoodSystem.Instance != null)
        {
            ConsumeFoodSystem.Instance.OnFoodAmountChanged += OnHungerDataChangedHandler;
        }
    }

    private void UnsubscribeManager()
    {
        if (liveMemManager != null)
        {
            liveMemManager.OnCapturedMemsChanged -= OnCapturedDataChangedHandler;
            liveMemManager.OnStorageCapacityChanged -= OnCapturedDataChangedHandler;
            liveMemManager = null;
        }

        if (liveDexRecordManager != null)
        {
            liveDexRecordManager.OnFirstCaptureRecorded -= OnFirstCaptureRecordedHandler;
            liveDexRecordManager = null;
        }

        if (ConsumeFoodSystem.Instance != null)
        {
            ConsumeFoodSystem.Instance.OnFoodAmountChanged -= OnHungerDataChangedHandler;
        }
    }

    private void OnCapturedDataChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;
        CheckAndRegisterFirstCaptureTimestamps();

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    private void OnHungerDataChangedHandler(int currentSatiety, int maxSatiety)
    {
        OnCapturedDataChangedHandler();
    }

    private void OnFirstCaptureRecordedHandler(string memId, long timestamp)
    {
        if (!string.IsNullOrEmpty(memId))
        {
            firstCaptureDict[memId] = timestamp;
        }
        OnCapturedDataChangedHandler();
    }

    private void CheckAndRegisterFirstCaptureTimestamps()
    {
        if (liveMemManager == null || liveMemManager.CapturedMems == null) return;

        long currentUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        foreach (var entry in liveMemManager.CapturedMems)
        {
            if (entry == null || entry.IsEmpty || string.IsNullOrEmpty(entry.MemId)) continue;

            if (!firstCaptureDict.ContainsKey(entry.MemId))
            {
                firstCaptureDict[entry.MemId] = currentUnixTimestamp;
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

        CheckAndRegisterFirstCaptureTimestamps();

        currentData.unlockedPageCount = liveMemManager.UnlockedPageCount;

        if (liveMemManager.CapturedMems != null)
        {
            currentData.serializedCapturedMems = new List<CapturedMemEntry>(liveMemManager.CapturedMems);
        }

        currentData.firstCapturedTimestamps = firstCaptureDict.Select(kvp => new MemFirstCapturedEntry
        {
            memId = kvp.Key,
            firstCapturedTimestamp = kvp.Value
        }).ToList();

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveMemManager == null) return;

        firstCaptureDict.Clear();
        List<MemDexRecord> dexRecordsForManager = new List<MemDexRecord>();

        if (saveData.firstCapturedTimestamps != null)
        {
            foreach (var entry in saveData.firstCapturedTimestamps)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.memId))
                {
                    firstCaptureDict[entry.memId] = entry.firstCapturedTimestamp;
                    dexRecordsForManager.Add(new MemDexRecord
                    {
                        MemId = entry.memId,
                        FirstCapturedTimestamp = entry.firstCapturedTimestamp
                    });
                }
            }
        }

        if (liveDexRecordManager != null)
        {
            liveDexRecordManager.LoadRecords(dexRecordsForManager);
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
        ensureMethod?.Invoke(liveMemManager, null);

        CheckAndRegisterFirstCaptureTimestamps();
    }
}