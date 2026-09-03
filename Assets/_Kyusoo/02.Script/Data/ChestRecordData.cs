using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class ChestRecordData : MonoBehaviour, IRecord
{
    private HashSet<string> openedChestIds = new HashSet<string>();

    private void OnEnable()
    {
        SubscribeSceneChests();
    }

    private void OnDisable()
    {
        UnsubscribeSceneChests();
    }

    /// <summary>
    /// ���� ���� ��ġ�� ��� Chest ������Ʈ�� OpenChestId �̺�Ʈ�� Ž���Ͽ� ����
    /// </summary>
    private void SubscribeSceneChests()
    {
        UnsubscribeSceneChests();
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest != null)
            {
                chest.OpenChestId += OnChestOpenedHandler;
            }
        }
    }

    private void UnsubscribeSceneChests()
    {
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest != null)
            {
                chest.OpenChestId -= OnChestOpenedHandler;
            }
        }
    }

    /// <summary>
    /// ���� ���� �� �̺�Ʈ ���� ó��
    /// </summary>
    private void OnChestOpenedHandler(string chestId)
    {
        if (RecordManager.IsLoadingData || string.IsNullOrEmpty(chestId)) return;

        if (!openedChestIds.Contains(chestId))
        {
            openedChestIds.Add(chestId);
        }

        if (RecordManager.Instance != null)
        {
                        // [멤] 저장 빈도 감축 - 즉시 디스크 쓰기 대신 변경 표시만 한다.
            RecordManager.NotifyDataChanged();
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.chestInfo = new List<ChestInfo>();
    }

    public void SaveData(string saveFilePath)
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        currentData.chestInfo = new List<ChestInfo>();

        foreach (string id in openedChestIds)
        {
            currentData.chestInfo.Add(new ChestInfo
            {
                chestId = id,
                isOpen = true
            });
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[ChestRecordData]</color> ���� ���� ���� ������ ���� �Ϸ�!");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        openedChestIds.Clear();

        if (saveData.chestInfo != null && saveData.chestInfo.Count > 0)
        {
            foreach (var info in saveData.chestInfo)
            {
                if (!string.IsNullOrEmpty(info.chestId) && info.isOpen)
                {
                    openedChestIds.Add(info.chestId);
                }
            }
        }

        // �� �� ���ڵ� �� �̹� ����(openedChestIds�� ���Ե�) ���ڴ� ��� �ı�
        var sceneChests = FindObjectsByType<Chest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var chest in sceneChests)
        {
            if (chest == null) continue;

            string cId = GetChestIdReflectively(chest);

            if (!string.IsNullOrEmpty(cId) && openedChestIds.Contains(cId))
            {
                Destroy(chest.gameObject);
            }
        }

        // �ı��ǰ� ���� ���ڵ鿡 ���� �̺�Ʈ �翬��
        SubscribeSceneChests();

        Debug.Log("<color=cyan>[ChestRecordData]</color> �̹� ����� ���� ���� �� ���� ����ȭ �Ϸ�!");
    }

    /// <summary>
    /// Chest ��ũ��Ʈ�� chestId �ʵ带 �����ϰ� �����ɴϴ�.
    /// </summary>
    private string GetChestIdReflectively(Chest chest)
    {
        if (chest == null) return string.Empty;
        var field = typeof(Chest).GetField("chestId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        return field?.GetValue(chest) as string ?? string.Empty;
    }
}