using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HDY.Forge;

public class ForgeRecordData : MonoBehaviour, IRecord
{
    private void OnEnable()
    {
        ForgeManager.OnForgeDataChanged += OnForgeDataChangedHandler;
    }

    private void OnDisable()
    {
        ForgeManager.OnForgeDataChanged -= OnForgeDataChangedHandler;
    }

    private void OnForgeDataChangedHandler()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.forgeInstanceDataList = new List<ForgeInstanceData>();
    }

    public void SaveData(string saveFilePath)
    {
        var registry = ForgeInstanceRegistry.Instance != null
            ? ForgeInstanceRegistry.Instance
            : FindFirstObjectByType<ForgeInstanceRegistry>();

        if (registry == null) return;

        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.forgeInstanceDataList = new List<ForgeInstanceData>(registry.AllInstances);
        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[ForgeRecordData]</color> 대장간 도구 인스턴스 데이터 백업 완료!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        var registry = ForgeInstanceRegistry.Instance != null
            ? ForgeInstanceRegistry.Instance
            : FindFirstObjectByType<ForgeInstanceRegistry>();

        if (registry == null) return;

        // 1. 메모리 레지스트리에 저장된 ForgeInstanceData 리스트 복원
        if (saveData.forgeInstanceDataList != null)
        {
            registry.RestoreInstances(saveData.forgeInstanceDataList);
        }

        // 2. 런타임 ItemData 캐시 갱신
        var itemDataProvider = ForgeInstanceItemDataProvider.Instance != null
            ? ForgeInstanceItemDataProvider.Instance
            : FindFirstObjectByType<ForgeInstanceItemDataProvider>();

        if (itemDataProvider != null && saveData.forgeInstanceDataList != null)
        {
            foreach (var instance in saveData.forgeInstanceDataList)
            {
                if (instance != null)
                {
                    itemDataProvider.RefreshRuntimeItemData(instance.BuildCompositeId());
                }
            }
        }

        Debug.Log("<color=cyan>[ForgeRecordData]</color> 대장간 도구 인스턴스 데이터 복구 완료!");
    }
}