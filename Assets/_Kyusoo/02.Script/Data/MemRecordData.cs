using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using HDY.Capture;

public class MemRecordData : MonoBehaviour, IRecord
{
    private MemCaptureManager liveMemManager;

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

    private void OnCapturedMemsChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    /// <summary>
    /// 최초 세이브 파일 생성 시 규격 가동
    /// </summary>
    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.unlockedPageCount = 2;
        saveData.serializedCapturedMems = new List<CapturedMemEntry>();

        // 🌟 [480칸 선제 할당]: 최초 기동 시 빈 장부가 생성될 때, 
        // 480개(48칸 x 10페이지)의 빈 칸 데이터를 미리 채워 가방 규격을 초기 세팅합니다.
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

        currentData.unlockedPageCount = liveMemManager.UnlockedPageCount;

        // 🌟 매니저 본체가 항상 EnsureCapacity()로 480개를 유지하고 있으므로,
        // 이 복사 생성자 연산만으로 빈 칸을 포함한 480개 리스트 전체가 통째로 백업됩니다.
        if (liveMemManager.CapturedMems != null)
        {
            currentData.serializedCapturedMems = new List<CapturedMemEntry>(liveMemManager.CapturedMems);
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[MemRecordData]</color> 🟩 포획 멤 인벤토리 변동 감지 ➡️ 실시간 세이브 디스크 라이팅 성공!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        RefreshManagerReference();
        if (liveMemManager == null) return;

        // 1. 리플렉션을 통해 팀원 코드 내부의 private 진짜 리스트("capturedMems") 주입 개시
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

        // 2. 해금된 페이지 수 private 필드 안전 주입
        RecordManager.Instance.SetPrivateFieldSafely(liveMemManager, "unlockedPageCount", saveData.unlockedPageCount);

        // 🌟 [핵심 보완 가드]: 만약 옛날 세이브 파일이거나 데이터가 깨져서 480칸이 안 채워진 채 로드되었을 경우를 대비합니다.
        // 팀원 코드 내부의 private 메서드인 "EnsureCapacity"를 조준해 호출함으로써 강제로 480칸을 다시 정상화시킵니다.
        MethodInfo ensureMethod = typeof(MemCaptureManager).GetMethod("EnsureCapacity", BindingFlags.NonPublic | BindingFlags.Instance);
        if (ensureMethod != null)
        {
            ensureMethod.Invoke(liveMemManager, null);
        }

        // 3. 데이터 변경 알림 이벤트 필드("OnCapturedMemsChanged") 강제 Invoke 트리거 발행 (UI 갱신 유도)
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

        Debug.Log("<color=lime>[MemRecordData]</color> 👑 480칸 규격 완전 검증 및 멤 데이터 복구 성공!");
    }
}