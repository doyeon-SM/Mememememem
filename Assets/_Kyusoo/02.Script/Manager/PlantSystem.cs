using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HDY.Item;
using MemSystem.Data;

public class PlantSystem : MonoBehaviour
{
    public static PlantSystem Instance { get; private set; }

    private Dictionary<string, PlantJSONSaveData> facilityDatabase = new Dictionary<string, PlantJSONSaveData>();

    private string saveFilePath;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            saveFilePath = Path.Combine(Application.persistentDataPath, "TerritoryPlantData.json");
            System.Diagnostics.Process.Start(Application.persistentDataPath);
            LoadAllPlantData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 시설 UI(생산/제작)가 열릴 때, 해당 시설의 고유 ID 장부를 건네주는 통로 함수
    /// </summary>
    public PlantJSONSaveData GetFacilityData(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId)) return null;

        if (!facilityDatabase.ContainsKey(buildingId))
        {
            PlantJSONSaveData newEntry = new PlantJSONSaveData
            {
                Building_ID = buildingId,
                isActive = false,
                currentCraftingItemId = "",
                targetQuantity = 1,
                remainingQuantity = 0,
                currentProgressTime = 0f,
                currentStorageCount = 0
            };
            facilityDatabase.Add(buildingId, newEntry);
        }

        return facilityDatabase[buildingId];
    }

    /// <summary>
    /// 특정 시설에서 멤 슬롯 배치, 제작 시작, 수령 완료 등의 변동이 일어났을 때 실시간 장부를 동기화하는 함수
    /// </summary>
    public void UpdateFacilityData(string buildingId, PlantJSONSaveData updatedData)
    {
        if (string.IsNullOrEmpty(buildingId) || updatedData == null) return;

        if (facilityDatabase.ContainsKey(buildingId))
        {
            facilityDatabase[buildingId] = updatedData;
        }
        else
        {
            facilityDatabase.Add(buildingId, updatedData);
        }

        SaveAllPlantData();
    }

    /// <summary>
    /// 현재 영지 메모리 상의 전체 딕셔너리 데이터를 JSON 포맷 문자열로 변환하여 로컬 디스크에 저장
    /// </summary>
    public void SaveAllPlantData()
    {
        try
        {
            PlantSaveWrapper wrapper = new PlantSaveWrapper();
            wrapper.lastSaveTime = DateTime.UtcNow.ToString("o");
            foreach (KeyValuePair<string, PlantJSONSaveData> facility in facilityDatabase)
            {
                wrapper.SaveDataList.Add(facility.Value);
            }

            string jsonString = JsonUtility.ToJson(wrapper, true);
            File.WriteAllText(saveFilePath, jsonString);

            Debug.Log($"[PlantSystem] 영지 시설 가동 데이터 JSON 세이브 성공! 경로: {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlantSystem] 세이브 도중 치명적인 오류 발생: {e.Message}");
        }
    }

    /// <summary>
    /// 게임 기동 시 로컬 디스크에서 JSON 파일을 안전하게 읽어와 딕셔너리 메모리를 복원
    /// </summary>
    public void LoadAllPlantData()
    {
        facilityDatabase.Clear();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("[PlantSystem] 세이브 데이터 파일이 존재하지 않아 신규 장부를 개설합니다.");
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            PlantSaveWrapper wrapper = JsonUtility.FromJson<PlantSaveWrapper>(jsonString);

            if (wrapper != null)
            {
                
                float offlineSeconds = 0f;
                if (!string.IsNullOrEmpty(wrapper.lastSaveTime))
                {
                    DateTime lastSave = DateTime.Parse(wrapper.lastSaveTime);
                    TimeSpan offlineSpan = DateTime.UtcNow - lastSave;
                    offlineSeconds = (float)offlineSpan.TotalSeconds;

                    Debug.Log($"[PlantSystem] 오프라인 방치 시간 감지: 약 {offlineSeconds:F1}초 동안 자원이 백그라운드에서 가공되었습니다.");
                }

                if (wrapper.SaveDataList != null)
                {
                    foreach (var entry in wrapper.SaveDataList)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.Building_ID)) continue;

                        if (entry.isActive && offlineSeconds > 0f)
                        {
                            float unitCraftTime = 30f;

                            int offlineProducedCount = Mathf.FloorToInt(offlineSeconds / unitCraftTime);

                            if (offlineProducedCount > 0)
                            {
                                if (entry.remainingQuantity > 0)
                                {
                                    int realProduceLimit = Mathf.Min(offlineProducedCount, entry.remainingQuantity);
                                    entry.currentStorageCount += realProduceLimit;
                                    entry.remainingQuantity -= realProduceLimit;

                                    if (entry.remainingQuantity <= 0)
                                    {
                                        entry.isActive = false; 
                                        entry.currentProgressTime = 0f;
                                    }
                                }
                                else
                                {
                                    entry.currentStorageCount += offlineProducedCount;
                                }
                            }
                        }

                        facilityDatabase.Add(entry.Building_ID, entry);
                    }
                    Debug.Log($"[PlantSystem] 총 {facilityDatabase.Count}개 시설의 가동 정보 및 방치 보상 정산 완료.");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlantSystem] 로드 및 방치 정산 오류: {e.Message}");
        }
    }

    private void OnApplicationPause(bool pause) { if (pause) SaveAllPlantData(); }
    private void OnApplicationQuit() { SaveAllPlantData(); }
}