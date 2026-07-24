using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HDY.Capture;
using MemSystem.Data;
using HDY.Mem;
using HDY.Item;

public class FacilityRecordData : MonoBehaviour, IRecord
{
    private void OnEnable()
    {
        GridManager.OnGridDataChanged += OnFacilityDataChanged;
        ProductionFacilityRuntime.OnMemDeploymentChanged += OnFacilityDataChanged;
        ProductionCraftRuntime.OnMemDeploymentChanged += OnFacilityDataChanged;
        RanchFacilityRuntime.OnMemDeploymentChanged += OnFacilityDataChanged; 

        ProductionFacilityRuntime.FacilityStarted += OnFacilityStartedHandler;
        ProductionFacilityRuntime.FacilityStopped += OnFacilityStoppedHandler;
        ProductionCraftRuntime.FacilityStarted += OnFacilityStartedHandler;
        ProductionCraftRuntime.FacilityStopped += OnFacilityStoppedHandler;
        RanchFacilityRuntime.FacilityStarted += OnFacilityStartedHandler;     
        RanchFacilityRuntime.FacilityStopped += OnFacilityStoppedHandler; 
    }

    private void OnDisable()
    {
        GridManager.OnGridDataChanged -= OnFacilityDataChanged;
        ProductionFacilityRuntime.OnMemDeploymentChanged -= OnFacilityDataChanged;
        ProductionCraftRuntime.OnMemDeploymentChanged -= OnFacilityDataChanged;
        RanchFacilityRuntime.OnMemDeploymentChanged -= OnFacilityDataChanged;

        ProductionFacilityRuntime.FacilityStarted -= OnFacilityStartedHandler;
        ProductionFacilityRuntime.FacilityStopped -= OnFacilityStoppedHandler;
        ProductionCraftRuntime.FacilityStarted -= OnFacilityStartedHandler;
        ProductionCraftRuntime.FacilityStopped -= OnFacilityStoppedHandler;
        RanchFacilityRuntime.FacilityStarted -= OnFacilityStartedHandler;
        RanchFacilityRuntime.FacilityStopped -= OnFacilityStoppedHandler;
    }

    private void OnFacilityStartedHandler(BuildingType type, List<MemData> mems) => OnFacilityDataChanged();
    private void OnFacilityStartedHandler(BuildingType type) => OnFacilityDataChanged();
    private void OnFacilityStoppedHandler(BuildingType type, List<MemData> mems, FacilityStopReason reason) => OnFacilityDataChanged();
    private void OnFacilityStoppedHandler(BuildingType type, FacilityStopReason reason) => OnFacilityDataChanged();

    private void OnFacilityDataChanged()
    {
        if (RecordManager.IsLoadingData) return;

        if (RecordManager.Instance != null)
        {
            SaveData(RecordManager.Instance.SaveFilePath);
        }
    }

    public void InitDefaultData(ref SaveData saveData)
    {
        saveData.placedBuildings = new List<PlacedBuildingData>();
    }

    public void SaveData(string saveFilePath)
    {
        SaveData currentData = RecordManager.Instance.ReadRawSaveFileOnly();
        if (currentData == null) currentData = new SaveData();

        var activeBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);
        currentData.placedBuildings = new List<PlacedBuildingData>();

        HashSet<string> allDeployedMemIDs = new HashSet<string>();

        foreach (var br in activeBuildings)
        {
            if (br == null || br.buildingData == null) continue;

            PlacedBuildingData bSave = new PlacedBuildingData
            {
                buildingName = br.buildingData.buildingName,
                gridX = br.gridX,
                gridZ = br.gridZ,
                rotationY = br.transform.eulerAngles.y
            };

            string uniqueId = $"{br.buildingData.buildingName}_{br.gridX}_{br.gridZ}";
            FacilityData rData = RecordManager.Instance.GetFacilityData(uniqueId);

            if (br.TryGetComponent<ProductionFacilityRuntime>(out var facility))
            {
                rData.isActive = facility.isProducing;
                rData.currentCraftingItemId = facility.craftingItem ?? "";
                rData.currentProgressTime = facility.currentProgressTime;
                rData.currentStorageCount = facility.currentStorageCount;

                if (facility.DeployedMemEntries != null)
                {
                    var ids = facility.DeployedMemEntries.Where(e => e != null && !string.IsNullOrEmpty(e.KeyId)).Select(e => e.KeyId).ToList();
                    rData.DeployedMemIDs = ids;

                    foreach (var id in ids) allDeployedMemIDs.Add(id);
                }
            }
            else if (br.TryGetComponent<ProductionCraftRuntime>(out var craft))
            {
                rData.isActive = craft.isProducing;
                rData.currentCraftingItemId = craft.currentCraftingItem ?? "";
                rData.targetQuantity = craft.targetQuantity;
                rData.remainingQuantity = craft.remainingQuantity;
                rData.currentProgressTime = craft.currentProgressTime;
                rData.currentStorageCount = craft.currentStorageCount;

                if (craft.DeployedMemEntries != null)
                {
                    var ids = craft.DeployedMemEntries.Where(e => e != null && !string.IsNullOrEmpty(e.KeyId)).Select(e => e.KeyId).ToList();
                    rData.DeployedMemIDs = ids;

                    foreach (var id in ids) allDeployedMemIDs.Add(id);
                }
            }
            else if (br.TryGetComponent<RanchFacilityRuntime>(out var ranch)) // 🌟 [추가]: 목장 저장 로직
            {
                rData.isActive = ranch.isProducing;
                rData.ranchSlots = new List<RanchSlotSaveData>();

                if (ranch.Slots != null)
                {
                    foreach (var slot in ranch.Slots)
                    {
                        var slotSave = new RanchSlotSaveData
                        {
                            slotIndex = slot.slotIndex,
                            isUnlocked = slot.isUnlocked,
                            deployedMemKeyId = slot.deployedMemEntry != null ? slot.deployedMemEntry.KeyId : "",
                            craftingItemId = slot.craftingItemId ?? "",
                            isProducing = slot.isProducing,
                            currentProgressTime = slot.currentProgressTime,
                            currentStorageCount = slot.currentStorageCount
                        };
                        rData.ranchSlots.Add(slotSave);

                        if (!string.IsNullOrEmpty(slotSave.deployedMemKeyId))
                        {
                            allDeployedMemIDs.Add(slotSave.deployedMemKeyId);
                        }
                    }
                }
            }

            bSave.runtimeData = rData;
            currentData.placedBuildings.Add(bSave);
        }

        if (currentData.serializedCapturedMems != null)
        {
            foreach (var memEntry in currentData.serializedCapturedMems)
            {
                if (memEntry != null && !string.IsNullOrEmpty(memEntry.KeyId))
                {
                    if (allDeployedMemIDs.Contains(memEntry.KeyId))
                    {
                        memEntry.IsActive = true;
                    }
                    else
                    {
                        memEntry.IsActive = false;
                    }
                }
            }
        }

        currentData.lastSaveTime = DateTime.UtcNow.ToString("o");
        File.WriteAllText(saveFilePath, JsonUtility.ToJson(currentData, true));
        Debug.Log("<color=lime>[FacilityLayoutRecord]</color> 시설 정보 및 멤 창고 IsActive 상태 데이터 업데이트");
    }

    public void ApplyData(SaveData saveData, SceneType sceneType)
    {
        if (sceneType == SceneType.Exploration)
        {
            return;
        }

        var gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null) return;

        var bTemplateField = typeof(GridManager).GetField("buildings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var floorContainerField = typeof(GridManager).GetField("floorContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var occupiedField = typeof(GridManager).GetField("occupiedCells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var objGridField = typeof(GridManager).GetField("buildingObjectsGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        var dataGridField = typeof(GridManager).GetField("buildingDataGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        List<BuildingData> buildingTemplates = bTemplateField?.GetValue(gridManager) as List<BuildingData>;
        Transform floorContainer = floorContainerField?.GetValue(gridManager) as Transform;
        bool[,] occupiedCells = occupiedField?.GetValue(gridManager) as bool[,];
        GameObject[,] buildingObjectsGrid = objGridField?.GetValue(gridManager) as GameObject[,];
        BuildingData[,] buildingDataGrid = dataGridField?.GetValue(gridManager) as BuildingData[,];

        if (occupiedCells == null)
        {
            int s = saveData.currentGridSize;
            occupiedCells = new bool[s, s];
            buildingObjectsGrid = new GameObject[s, s];
            buildingDataGrid = new BuildingData[s, s];

            RecordManager.Instance.SetPrivateFieldSafely(gridManager, "currentWidth", s);
            RecordManager.Instance.SetPrivateFieldSafely(gridManager, "currentHeight", s);
        }

        int w = occupiedCells.GetLength(0);
        int h = occupiedCells.GetLength(1);

        if (saveData.placedBuildings != null && buildingTemplates != null)
        {
            foreach (var bSave in saveData.placedBuildings)
            {
                BuildingData matchData = buildingTemplates.Find(b => b.buildingName == bSave.buildingName);
                if (matchData == null || matchData.buildingPrefab == null) continue;

                int currentRotationIndex = Mathf.RoundToInt(bSave.rotationY / 90f) % 4;
                bool isRotated = (currentRotationIndex == 1 || currentRotationIndex == 3);
                int bWidth = isRotated ? matchData.height : matchData.width;
                int bHeight = isRotated ? matchData.width : matchData.height;

                Vector3 spawnPos = new Vector3(bSave.gridX + (bWidth / 2.0f), 0.5f, bSave.gridZ + (bHeight / 2.0f));
                GameObject spawnedObj = Instantiate(matchData.buildingPrefab, spawnPos, Quaternion.Euler(0f, bSave.rotationY, 0f), floorContainer);

                if (spawnedObj.TryGetComponent<BuildingRuntime>(out BuildingRuntime br))
                {
                    br.enabled = true;
                    br.Initialize(matchData, bSave.gridX, bSave.gridZ);
                }

                var entry = bSave.runtimeData ?? new FacilityData { Building_ID = $"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}" };

                if (spawnedObj.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                {
                    facility.buildingData = matchData;
                    facility.isProducing = entry.isActive;
                    facility.currentProgressTime = entry.currentProgressTime;
                    facility.currentStorageCount = entry.currentStorageCount;
                    facility.craftingItem = entry.currentCraftingItemId;
                    facility.UpdateMaxStorage();

                    if (facility.DeployedMems != null) facility.DeployedMems.Clear();
                    if (facility.DeployedMemEntries != null) facility.DeployedMemEntries.Clear();

                    var memManager = FindFirstObjectByType<MemCaptureManager>();
                    if (memManager != null && entry.DeployedMemIDs != null)
                    {
                        int maxCapacity = ProductionCalculator.GetMaxMemCount(facility.currentLevel);
                        var safeMemIDs = entry.DeployedMemIDs.Distinct().Take(maxCapacity).ToList();
                        foreach (var savedKeyId in safeMemIDs)
                        {
                            var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                            if (match != null)
                            {
                                MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                if (realMemData != null) facility.TryAddMem(realMemData, match);
                            }
                        }
                    }
                    facility.CheckProductionCondition();
                }
                else if (spawnedObj.TryGetComponent<ProductionCraftRuntime>(out var craft))
                {
                    craft.buildingData = matchData;
                    craft.isProducing = entry.isActive;
                    craft.targetQuantity = entry.targetQuantity;
                    craft.remainingQuantity = entry.remainingQuantity;
                    craft.currentProgressTime = entry.currentProgressTime;
                    craft.currentStorageCount = entry.currentStorageCount;
                    craft.currentCraftingItem = entry.currentCraftingItemId;

                    if (craft.DeployedMems != null) craft.DeployedMems.Clear();
                    if (craft.DeployedMemEntries != null) craft.DeployedMemEntries.Clear();

                    var memManager = FindFirstObjectByType<MemCaptureManager>();
                    if (memManager != null && entry.DeployedMemIDs != null)
                    {
                        foreach (var savedKeyId in entry.DeployedMemIDs)
                        {
                            var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);
                            if (match != null)
                            {
                                MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                if (realMemData != null) craft.TryAddMem(realMemData, match);
                            }
                        }
                    }
                }
                else if (spawnedObj.TryGetComponent<RanchFacilityRuntime>(out var ranch)) // 🌟 [추가]: 목장 복원 로직
                {
                    ranch.buildingData = matchData;
                    ranch.UpdateSlotCapacity();

                    var memManager = FindFirstObjectByType<MemCaptureManager>();

                    if (entry.ranchSlots != null && entry.ranchSlots.Count > 0)
                    {
                        foreach (var slotSave in entry.ranchSlots)
                        {
                            if (slotSave.slotIndex >= 0 && slotSave.slotIndex < ranch.Slots.Count)
                            {
                                var slotRuntime = ranch.Slots[slotSave.slotIndex];
                                slotRuntime.isUnlocked = slotSave.isUnlocked;

                                if (!string.IsNullOrEmpty(slotSave.deployedMemKeyId) && memManager != null)
                                {
                                    var match = memManager.CapturedMems.FirstOrDefault(m => m != null && m.KeyId == slotSave.deployedMemKeyId);
                                    if (match != null)
                                    {
                                        MemData realMemData = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(match.MemId) : null;
                                        if (realMemData != null)
                                        {
                                            ranch.TryAddMemToSlot(slotSave.slotIndex, realMemData, match);
                                        }
                                    }
                                }

                                // 멤 추가 함수 호출 후 개별 진행 상태 복원
                                slotRuntime.craftingItemId = slotSave.craftingItemId;
                                slotRuntime.currentProgressTime = slotSave.currentProgressTime;
                                slotRuntime.currentStorageCount = slotSave.currentStorageCount;
                            }
                        }
                    }
                    ranch.CheckAllSlotsProductionCondition();
                }

                for (int x = bSave.gridX; x < bSave.gridX + bWidth; x++)
                {
                    for (int z = bSave.gridZ; z < bSave.gridZ + bHeight; z++)
                    {
                        if (x >= 0 && x < w && z >= 0 && z < h)
                        {
                            occupiedCells[x, z] = true;
                            buildingObjectsGrid[x, z] = spawnedObj;
                            buildingDataGrid[x, z] = matchData;
                        }
                    }
                }
                RecordManager.Instance.UpdateFacilityData($"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}", entry);
            }
        }

        RecordManager.Instance.SetPrivateFieldSafely(gridManager, "occupiedCells", occupiedCells);
        RecordManager.Instance.SetPrivateFieldSafely(gridManager, "buildingObjectsGrid", buildingObjectsGrid);
        RecordManager.Instance.SetPrivateFieldSafely(gridManager, "buildingDataGrid", buildingDataGrid);
        RecordManager.Instance.RefreshActivePanelMemSlotsRealtime();
    }

    private ItemData FindItemDataInProject(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance == null)
        {
            Debug.LogError($"[ItemCatalogManager] 인스턴스가 존재하지 않아 아이템 '{itemId}'을(를) 탐색할 수 없습니다.");
            return null;
        }

        ItemData targetItem = ItemCatalogManager.Instance.FindItemData(itemId);
        if (targetItem == null)
        {
            Debug.LogError($"[ItemCatalogManager] 카탈로그에서 아이템 ID '{itemId}'에 해당하는 ItemData를 찾을 수 없습니다.");
        }

        return targetItem;
    }
}