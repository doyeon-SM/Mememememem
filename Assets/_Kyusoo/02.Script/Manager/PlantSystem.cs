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
            LoadAllPlantData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

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
    /// 씬 이동이나 게임 종료 시, 맵에 배치된 모든 건물의 실시간 현황 저장.
    /// </summary>
    public void SaveAllFacilitiesRealtimeSync()
    {
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null || facility.buildingData == null) continue;
            var br = facility.GetComponent<BuildingRuntime>();
            string uniqueId = br != null ? $"{facility.buildingData.buildingName}_{br.gridX}_{br.gridZ}" : facility.buildingData.buildingId;

            PlantJSONSaveData data = GetFacilityData(uniqueId);
            data.isActive = facility.isProducing;
            data.currentCraftingItemId = facility.craftingItem != null ? facility.craftingItem.Item_ID : "";
            data.currentProgressTime = facility.currentProgressTime;
            data.currentStorageCount = facility.currentStorageCount;

            if (facilityDatabase.ContainsKey(uniqueId)) facilityDatabase[uniqueId] = data;
        }

        
        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null || craft.buildingData == null) continue;
            var br = craft.GetComponent<BuildingRuntime>();
            string uniqueId = br != null ? $"{craft.buildingData.buildingName}_{br.gridX}_{br.gridZ}" : craft.buildingData.buildingId;

            PlantJSONSaveData data = GetFacilityData(uniqueId);
            data.isActive = craft.isProducing;
            data.currentCraftingItemId = craft.currentCraftingItem != null ? craft.currentCraftingItem.Item_ID : "";
            data.targetQuantity = craft.targetQuantity;
            data.remainingQuantity = craft.remainingQuantity;
            data.currentProgressTime = craft.currentProgressTime;
            data.currentStorageCount = craft.currentStorageCount;

            if (facilityDatabase.ContainsKey(uniqueId)) facilityDatabase[uniqueId] = data;
        }

        
        SaveAllPlantData();
    }

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
            Debug.Log($"[PlantSystem] 영지 전체 시설 최종 동기화 JSON 세이브 성공! 경로: {saveFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlantSystem] 세이브 도중 오류 발생: {e.Message}");
        }
    }

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

                    Debug.Log($"[PlantSystem] 오프라인 방치 시간 감지: 약 {offlineSeconds:F1}초 백그라운드 연산 가동.");
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

    private void OnApplicationPause(bool pause) { if (pause) SaveAllFacilitiesRealtimeSync(); }
    private void OnApplicationQuit() { SaveAllFacilitiesRealtimeSync(); }
    private void OnDestroy() { SaveAllFacilitiesRealtimeSync(); } 
}