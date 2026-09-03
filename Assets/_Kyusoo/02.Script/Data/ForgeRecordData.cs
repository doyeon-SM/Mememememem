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
                        // [멤] 저장 빈도 감축 - 대장간 진행 변동은 고빈도라 변경 표시만 한다.
            RecordManager.NotifyDataChanged();
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
        Debug.Log("<color=lime>[ForgeRecordData]</color> ���尣 ���� �ν��Ͻ� ������ ��� �Ϸ�!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        var registry = ForgeInstanceRegistry.Instance != null
            ? ForgeInstanceRegistry.Instance
            : FindFirstObjectByType<ForgeInstanceRegistry>();

        if (registry == null) return;

        // 1. �޸� ������Ʈ���� ����� ForgeInstanceData ����Ʈ ����
        if (saveData.forgeInstanceDataList != null)
        {
            registry.RestoreInstances(saveData.forgeInstanceDataList);
        }

        // 2. ��Ÿ�� ItemData ĳ�� ����
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

        Debug.Log("<color=cyan>[ForgeRecordData]</color> ���尣 ���� �ν��Ͻ� ������ ���� �Ϸ�!");
    }
}