using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using HDY.Territory;
using HDY.Inventory;
using HDY.Item;
using KMS.InventoryDuped;
using MemSystem.Data;

// =================================================================
// 📦 [데이터 세이브 및 직렬화 전용 규격 구조체 정의]
// =================================================================
[Serializable]
public class SerializableItemStack
{
    public string itemId;
    public int amount;
}

[Serializable]
public class SerializableContainerData
{
    public int width;
    public int height;
    public List<SerializableItemStack> slots = new List<SerializableItemStack>();
}

[Serializable]
public class PlacedBuildingSaveData
{
    public string buildingName;
    public int gridX;
    public int gridZ;
    public float rotationY;
    public PlantJSONSaveData runtimeData;
}

[Serializable]
public class TerritorySaveData
{
    public string lastSaveTime;

    [Header("영지 기초 성장 데이터")]
    public int territoryLevel = 1;
    public int currentExp = 0;
    public int requiredExp = 100;
    public int gold = 0;
    public int satisfaction = 0;
    public float elapsedTime = 0f;

    [Header("영지 타일 확장 데이터")]
    public int currentGridSize = 5;
    public List<bool> expansionExpandedStates = new List<bool>();

    [Header("창고 및 인벤토리 실물 데이터")]
    public SerializableContainerData playerInventoryData;
    public SerializableContainerData warehouseStorageData;
    public SerializableContainerData foodWarehouseStorageData;

    [Header("음식 소모 시뮬레이션 데이터")]
    public int maxSatiety;
    public int currentSatiety;
    public bool isWorkStoppedDueToStarvation;

    [Header("배치된 시설 레이아웃 청사진 및 일꾼 마스터 데이터")]
    public List<PlacedBuildingSaveData> placedBuildings = new List<PlacedBuildingSaveData>();
}

/// <summary>
/// 👑 [영지 데이터 영구 저장/복원 및 월드 레이아웃 건축 마스터 매니저]
/// </summary>
public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance { get; private set; }

    private string saveFilePath;
    private Dictionary<string, PlantJSONSaveData> facilityDatabase = new Dictionary<string, PlantJSONSaveData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "TerritoryRecord.json");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 🌟 GridManager.Start()의 리셋 연산이 끝날 시간을 벌어주기 위해 1프레임 대기 후 로드 실행
        StartCoroutine(DelayedLoadRoutine());
    }

    private void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneUnloadedTrigger;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloadedTrigger;
    }

    private IEnumerator DelayedLoadRoutine()
    {
        yield return null; // 씬의 모든 오브젝트가 완벽히 깨어날 때까지 정확히 1프레임 대기
        LoadTerritoryRecordData();
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
            facilityDatabase[buildingId] = updatedData;
        else
            facilityDatabase.Add(buildingId, updatedData);
    }

    // =================================================================
    // 🛡️ [리플렉션 안전장치 헬퍼 함수]
    // =================================================================
    private void SetPrivateFieldSafely(object targetObject, string fieldName, object valueToSet)
    {
        if (targetObject == null || valueToSet == null) return;

        try
        {
            var fieldInfo = targetObject.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Instance);

            if (fieldInfo == null) return;

            if (fieldInfo.FieldType.IsAssignableFrom(valueToSet.GetType()))
            {
                fieldInfo.SetValue(targetObject, valueToSet);
            }
            else
            {
                object convertedValue = Convert.ChangeType(valueToSet, fieldInfo.FieldType);
                fieldInfo.SetValue(targetObject, convertedValue);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RecordManager] 리플렉션 안전 장치 가동 우회 ({fieldName}): {e.Message}");
        }
    }

    // =================================================================
    // 💾 [마스터 데이터 일괄 취합 및 파일 쓰기 핵심 세이브 엔진]
    // =================================================================
    public void ExecuteBulkSaveProcess()
    {
        try
        {
            TerritorySaveData saveData = new TerritorySaveData();
            saveData.lastSaveTime = DateTime.UtcNow.ToString("o");

            var territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                saveData.territoryLevel = territoryData.Level;
                saveData.currentExp = territoryData.CurrentExp;
                saveData.gold = territoryData.Gold;
                saveData.satisfaction = territoryData.Satisfaction;
                saveData.elapsedTime = territoryData.ElapsedTime;
                saveData.requiredExp = 100;
            }

            var expansion = FindFirstObjectByType<TerritoryExpansionManager>();
            if (expansion != null)
            {
                var fieldGridSize = typeof(TerritoryExpansionManager).GetField("currentGridSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldGridSize != null) saveData.currentGridSize = (int)fieldGridSize.GetValue(expansion);

                foreach (var step in expansion.ExpansionSteps) saveData.expansionExpandedStates.Add(step.IsExpanded);
            }

            var activeBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);
            foreach (var br in activeBuildings)
            {
                if (br == null || br.buildingData == null) continue;

                PlacedBuildingSaveData bSave = new PlacedBuildingSaveData();
                bSave.buildingName = br.buildingData.buildingName;
                bSave.gridX = br.gridX;
                bSave.gridZ = br.gridZ;
                bSave.rotationY = br.transform.eulerAngles.y;

                string uniqueId = $"{br.buildingData.buildingName}_{br.gridX}_{br.gridZ}";
                PlantJSONSaveData rData = GetFacilityData(uniqueId);

                if (br.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                {
                    rData.isActive = facility.isProducing;
                    rData.currentCraftingItemId = facility.craftingItem != null ? facility.craftingItem.Item_ID : "";
                    rData.currentProgressTime = facility.currentProgressTime;
                    rData.currentStorageCount = facility.currentStorageCount;

                    rData.DeployedMemIDs.Clear();
                    if (facility.DeployedMems != null)
                    {
                        foreach (var mem in facility.DeployedMems) if (mem != null) rData.DeployedMemIDs.Add(mem.memName);
                    }
                }
                else if (br.TryGetComponent<ProductionCraftRuntime>(out var craft))
                {
                    rData.isActive = craft.isProducing;
                    rData.currentCraftingItemId = craft.currentCraftingItem != null ? craft.currentCraftingItem.Item_ID : "";
                    rData.targetQuantity = craft.targetQuantity;
                    rData.remainingQuantity = craft.remainingQuantity;
                    rData.currentProgressTime = craft.currentProgressTime;
                    rData.currentStorageCount = craft.currentStorageCount;

                    rData.DeployedMemIDs.Clear();
                    if (craft.DeployedMems != null)
                    {
                        foreach (var mem in craft.DeployedMems) if (mem != null) rData.DeployedMemIDs.Add(mem.memName);
                    }
                }

                bSave.runtimeData = rData;
                saveData.placedBuildings.Add(bSave);
            }

            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && pInventory.inventory != null) saveData.playerInventoryData = PackContainerData(pInventory.inventory);

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && wInventory.storage != null) saveData.warehouseStorageData = PackContainerData(wInventory.storage);

            if (ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.FoodStorageContainer != null)
            {
                saveData.foodWarehouseStorageData = PackContainerData(ConsumeFoodSystem.Instance.FoodStorageContainer);
                saveData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
                saveData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
                saveData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;
            }

            string jsonString = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(saveFilePath, jsonString);

            Debug.Log($"<color=lime><b>[RecordManager]</b></color> 레이아웃 포함 전체 세이브 성공! 경로: <color=yellow>{saveFilePath}</color>");

            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{saveFilePath.Replace("/", "\\")}\"");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 일괄 마감 세이브 도중 치명적 예외: {e.Message}");
        }
    }

    // =================================================================
    // 📥 [역직렬화 데이터 복원 및 월드 3D 실물 그리드 재건축 로드 엔진]
    // =================================================================
    public void LoadTerritoryRecordData()
    {
        facilityDatabase.Clear();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[RecordManager]</color> 최초 파일이 없어 디폴트 뼈대를 자동 개설합니다.");
            ExecuteBulkSaveProcess();
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            TerritorySaveData saveData = JsonUtility.FromJson<TerritorySaveData>(jsonString);
            if (saveData == null) return;

            var territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                SetPrivateFieldSafely(territoryData, "level", saveData.territoryLevel);
                SetPrivateFieldSafely(territoryData, "currentExp", saveData.currentExp);
                SetPrivateFieldSafely(territoryData, "gold", saveData.gold);
                SetPrivateFieldSafely(territoryData, "satisfaction", saveData.satisfaction);
                SetPrivateFieldSafely(territoryData, "elapsedTime", saveData.elapsedTime);
                // 🌟 [타입 에러 박멸]: 인펙터 설정 리스트 테이블인 requiredExp는 세이브 강제 주입 대상에서 안전하게 예외 패스 처리합니다.
            }

            var expansion = FindFirstObjectByType<TerritoryExpansionManager>();
            var gridManager = FindFirstObjectByType<GridManager>();

            if (expansion != null) SetPrivateFieldSafely(expansion, "currentGridSize", saveData.currentGridSize);

            if (gridManager != null)
            {
                gridManager.ExpandGrid(saveData.currentGridSize, saveData.currentGridSize);

                var oldBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);
                foreach (var oldB in oldBuildings) Destroy(oldB.gameObject);
            }

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
            }

            int w = occupiedCells.GetLength(0);
            int h = occupiedCells.GetLength(1);

            float offlineSeconds = 0f;
            if (!string.IsNullOrEmpty(saveData.lastSaveTime))
            {
                DateTime lastSave = DateTime.Parse(saveData.lastSaveTime);
                offlineSeconds = (float)(DateTime.UtcNow - lastSave).TotalSeconds;
            }

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

                    float offsetX = bSave.gridX + (bWidth / 2.0f);
                    float offsetZ = bSave.gridZ + (bHeight / 2.0f);
                    Vector3 spawnPos = new Vector3(offsetX, 0f, offsetZ);

                    GameObject spawnedObj = Instantiate(matchData.buildingPrefab, spawnPos, Quaternion.Euler(0f, bSave.rotationY, 0f), floorContainer);

                    if (spawnedObj.TryGetComponent<BuildingRuntime>(out BuildingRuntime buildingRuntime))
                    {
                        buildingRuntime.enabled = true;
                        buildingRuntime.Initialize(matchData, bSave.gridX, bSave.gridZ);
                    }

                    // 🌟 [NullReference 에러 원천 차단 방어막]: 
                    // JSON 파일 내부에 runtimeData 세이브 내용이 누락되었거나 null이더라도, 
                    // 시스템 가동이 멈추지 않도록 가상 디폴트 객체를 즉시 개설하여 완벽하게 방어합니다.
                    var entry = bSave.runtimeData;
                    if (entry == null)
                    {
                        entry = new PlantJSONSaveData
                        {
                            Building_ID = $"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}",
                            isActive = false,
                            currentCraftingItemId = "",
                            targetQuantity = 1,
                            remainingQuantity = 0,
                            currentProgressTime = 0f,
                            currentStorageCount = 0
                        };
                    }

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
                                if (entry.remainingQuantity <= 0) { entry.isActive = false; entry.currentProgressTime = 0f; }
                            }
                            else entry.currentStorageCount += offlineProducedCount;
                        }
                    }

                    if (spawnedObj.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                    {
                        facility.buildingData = matchData;
                        facility.isProducing = entry.isActive;
                        facility.currentProgressTime = entry.currentProgressTime;
                        facility.currentStorageCount = entry.currentStorageCount;
                        facility.UpdateMaxStorage();

                        if (facility.DeployedMems != null && entry.DeployedMemIDs != null)
                        {
                            facility.DeployedMems.Clear();
                            foreach (var id in entry.DeployedMemIDs) facility.DeployedMems.Add(new MemData { memName = id, maxHunger = 5 });
                        }
                    }
                    else if (spawnedObj.TryGetComponent<ProductionCraftRuntime>(out var craft))
                    {
                        craft.buildingData = matchData;
                        craft.isProducing = entry.isActive;
                        craft.targetQuantity = entry.targetQuantity;
                        craft.remainingQuantity = entry.remainingQuantity;
                        craft.currentProgressTime = entry.currentProgressTime;
                        craft.currentStorageCount = entry.currentStorageCount;

                        if (craft.DeployedMems != null && entry.DeployedMemIDs != null)
                        {
                            craft.DeployedMems.Clear();
                            foreach (var id in entry.DeployedMemIDs) craft.DeployedMems.Add(new MemData { memName = id, maxHunger = 5 });
                        }
                    }

                    // 🌟 [상호작용 정상화 완수]: 실시간 각인 주입
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

                    string uniqueId = $"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}";
                    facilityDatabase[uniqueId] = entry;
                }
            }

            // 🌟 예외 없이 루프가 완수되었으므로, 최종 정산된 클릭 장부가 GridManager 본체에 정상 귀환 안착합니다.
            SetPrivateFieldSafely(gridManager, "occupiedCells", occupiedCells);
            SetPrivateFieldSafely(gridManager, "buildingObjectsGrid", buildingObjectsGrid);
            SetPrivateFieldSafely(gridManager, "buildingDataGrid", buildingDataGrid);

            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && saveData.playerInventoryData != null) UnpackContainerData(saveData.playerInventoryData, pInventory.inventory);

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && saveData.warehouseStorageData != null) UnpackContainerData(saveData.warehouseStorageData, wInventory.storage);

            if (ConsumeFoodSystem.Instance != null && saveData.foodWarehouseStorageData != null) UnpackContainerData(saveData.foodWarehouseStorageData, ConsumeFoodSystem.Instance.FoodStorageContainer);

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

            if (ConsumeFoodSystem.Instance != null)
            {
                ConsumeFoodSystem.Instance.ForceSyncManualState(saveData.currentSatiety, saveData.maxSatiety, saveData.isWorkStoppedDueToStarvation);
                ConsumeFoodSystem.Instance.ProcessFoodConsumption(true);
            }

            var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
            if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

            Debug.Log($"<color=cyan><b>[RecordManager]</b></color> 레이아웃 및 클릭 상호작용 데이터 무결성 100% 동기화 복원 대성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 데이터 복원(Load) 중 치명적 예외 발생: {e.Message}");
        }
    }

    private SerializableContainerData PackContainerData(InventoryContainer container)
    {
        var data = new SerializableContainerData { width = container.width, height = container.height };
        if (container.slots != null)
        {
            foreach (var slot in container.slots)
            {
                data.slots.Add(new SerializableItemStack { itemId = slot != null ? slot.itemId : "", amount = slot != null ? slot.amount : 0 });
            }
        }
        return data;
    }

    private void UnpackContainerData(SerializableContainerData source, InventoryContainer target)
    {
        if (source == null || target == null) return;
        target.width = source.width;
        target.height = source.height;
        target.slots = new ItemStack[source.slots.Count];

        for (int i = 0; i < source.slots.Count; i++)
        {
            target.slots[i] = new ItemStack();
            if (!string.IsNullOrEmpty(source.slots[i].itemId) && source.slots[i].amount > 0)
                target.slots[i].Set(source.slots[i].itemId, source.slots[i].amount);
            else
                target.slots[i].Clear();
        }
    }

    private void OnApplicationQuit() { ExecuteBulkSaveProcess(); }
    private void OnApplicationPause(bool pause) { if (pause) ExecuteBulkSaveProcess(); }
    private void OnSceneUnloadedTrigger(Scene currentScene) { ExecuteBulkSaveProcess(); }
}