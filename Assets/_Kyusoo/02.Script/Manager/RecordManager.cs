using HDY.Capture;
using HDY.Inventory;
using HDY.Item;
using HDY.Mem;
using HDY.Territory;
using HDY.Recipe;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    public SerializableContainerData foodBagStorageData;

    [Header("음식 소모 시뮬레이션 데이터")]
    public int maxSatiety;
    public int currentSatiety;
    public bool isWorkStoppedDueToStarvation;

    [Header("멤 창고 데이터")]
    public int unlockedPageCount = 2;
    public List<CapturedMemEntry> serializedCapturedMems = new List<CapturedMemEntry>();

    [Header("배치된 시설 레이아웃 청사진 및 일꾼 마스터 데이터")]
    public List<PlacedBuildingSaveData> placedBuildings = new List<PlacedBuildingSaveData>();
}

public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance { get; private set; }

    private string saveFilePath;
    private Dictionary<string, PlantJSONSaveData> facilityDatabase = new Dictionary<string, PlantJSONSaveData>();
    private bool isApplicationQuitting = false;

    private const string LastPlayTimeKey = "OfflineLastPlayTime";

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

    private void Update()
    {
        MaintainHealthyFacilityCache();
    }

    private IEnumerator DelayedLoadRoutine()
    {
        yield return null;
        // 오직 영지 씬 계열일 때만 최초 자동 복구 로드를 가동해 격자 레이아웃을 형성합니다.
        if (SceneManager.GetActiveScene().name.ToLower().Contains("territory"))
        {
            LoadTerritoryRecordData();
        }
    }

    public bool IsSaveFileExists()
    {
        return File.Exists(saveFilePath);
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

    public TerritorySaveData ReadRawSaveFileOnly()
    {
        if (!File.Exists(saveFilePath)) return null;
        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            return JsonUtility.FromJson<TerritorySaveData>(jsonString);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 순수 장부 파일 로드 실패: {e.Message}");
            return null;
        }
    }

    /// <summary>
    /// 최초 실행 시 영지 기본 뼈대 구조 데이터 오브젝트를 디스크에 강제 형성 개설합니다.
    /// </summary>
    public void CreateDefaultTerritoryRecord()
    {
        try
        {
            TerritorySaveData defaultData = new TerritorySaveData
            {
                lastSaveTime = DateTime.UtcNow.ToString("o"),
                territoryLevel = 1,
                currentExp = 0,
                requiredExp = 100,
                gold = 0,
                satisfaction = 0,
                elapsedTime = 0f,
                currentGridSize = 5,
                expansionExpandedStates = new List<bool> { false, false, false, false, false },
                playerInventoryData = new SerializableContainerData { width = 10, height = 6 },
                warehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                foodWarehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                foodBagStorageData = new SerializableContainerData { width = 10, height = 6 },
                maxSatiety = 100,
                currentSatiety = 100,
                isWorkStoppedDueToStarvation = false,
                unlockedPageCount = 2,
                serializedCapturedMems = new List<CapturedMemEntry>(),
                placedBuildings = new List<PlacedBuildingSaveData>()
            };

            string jsonString = JsonUtility.ToJson(defaultData, true);
            File.WriteAllText(saveFilePath, jsonString);
            Debug.Log("<color=lime>[RecordManager]</color> 최초 세이브 무결성 기본 구조 파일 생성 완료!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 기본 구조 파일 생성 도중 예외: {e.Message}");
        }
    }

    /// <summary>
    /// 🌟 [전면 교정]: 하드디스크 입출력 지연 버그를 완벽하게 배제하고, 메모리 상에서 구조를 완전 융합한 뒤
    /// 단 한 번에 파일 스트림을 마감하는 완벽한 2차 동기화 업데이트 연산식입니다.
    /// </summary>
    public void ExecutePartialSaveForAdventure()
    {
        try
        {
            TerritorySaveData currentData = null;

            // 파일이 존재한다면 원본 데이터를 파싱해오고, 없다면 메모리 상에서 기본 구조를 즉시 완전히 동적 생성합니다.
            if (IsSaveFileExists())
            {
                currentData = ReadRawSaveFileOnly();
            }

            if (currentData == null)
            {
                currentData = new TerritorySaveData
                {
                    territoryLevel = 1,
                    currentGridSize = 5,
                    requiredExp = 100,
                    gold = 0,
                    satisfaction = 0,
                    elapsedTime = 0f,
                    expansionExpandedStates = new List<bool> { false, false, false, false, false },
                    playerInventoryData = new SerializableContainerData { width = 10, height = 6 },
                    warehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodWarehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodBagStorageData = new SerializableContainerData { width = 10, height = 6 },
                    maxSatiety = 100,
                    currentSatiety = 100,
                    isWorkStoppedDueToStarvation = false,
                    unlockedPageCount = 2,
                    serializedCapturedMems = new List<CapturedMemEntry>(),
                    placedBuildings = new List<PlacedBuildingSaveData>()
                };
            }

            currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

            // 🌟 [안전 예방선]: 비활성화되거나 숨겨진 컴포넌트 장부까지 완벽히 긁어오기 위해 Include 옵션을 바인딩합니다.
            var pInventory = FindObjectsByType<PlayerInventory>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (pInventory != null && pInventory.inventory != null)
            {
                currentData.playerInventoryData = PackContainerData(pInventory.inventory);
            }
            else
            {
                Debug.LogWarning("[RecordManager] 탐험 씬 내부에서 가방(PlayerInventory) 컴포넌트를 탐색하지 못해 업데이트를 유보했습니다.");
            }

            // 포획 멤 대장 추출 이식
            var memCaptureManager = FindObjectsByType<MemCaptureManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (memCaptureManager != null && memCaptureManager.CapturedMems != null)
            {
                currentData.unlockedPageCount = memCaptureManager.UnlockedPageCount;
                if (currentData.serializedCapturedMems == null)
                    currentData.serializedCapturedMems = new List<CapturedMemEntry>();
                else
                    currentData.serializedCapturedMems.Clear();

                foreach (var entry in memCaptureManager.CapturedMems)
                {
                    if (entry != null) currentData.serializedCapturedMems.Add(entry);
                }
            }

            // 영지 시설 청사진 안전 가드
            if (currentData.placedBuildings == null)
                currentData.placedBuildings = new List<PlacedBuildingSaveData>();

            // 메모리 상에서 동기화가 완벽하게 끝난 최종 장부 객체를 단 한 번에 파일로 작성하여 I/O 병목을 소멸시킵니다.
            string jsonString = JsonUtility.ToJson(currentData, true);
            File.WriteAllText(saveFilePath, jsonString);

            PlayerPrefs.SetString(LastPlayTimeKey, currentData.lastSaveTime);
            PlayerPrefs.Save();

            Debug.Log("<color=lime><b>[RecordManager]</b></color> 탐험 씬 인벤토리 및 포획 데이터 융합 세이브 파일 최종 쓰기 완수!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 부분 백업 세이브 도중 치명적 예외: {e.Message}");
        }
    }

    public void ExecuteBulkSaveProcess(bool isTeardown = false)
    {
        try
        {
            if (isApplicationQuitting && (ConsumeFoodSystem.Instance == null || FindFirstObjectByType<PlayerInventory>() == null))
            {
                Debug.LogWarning("<color=orange>[RecordManager]</color> 어플리케이션 종료 연쇄 파괴 단계를 감지하여 세이브 파일 오염 방지를 위해 쓰기를 유보합니다.");
                return;
            }

            if (SceneManager.GetActiveScene().name.ToLower().Contains("adventure") || !SceneManager.GetActiveScene().name.ToLower().Contains("territory"))
            {
                ExecutePartialSaveForAdventure();
                return;
            }

            var activeBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);

            if (isTeardown && activeBuildings.Length == 0 && File.Exists(saveFilePath))
            {
                Debug.LogWarning("<color=red>[RecordManager]</color> 씬 해제 또는 종료 중 오브젝트 소실을 감지하여 데이터 오염 방지를 위해 세이브를 유보합니다.");
                return;
            }

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

            if (MemCaptureManager.Instance != null)
            {
                saveData.unlockedPageCount = MemCaptureManager.Instance.UnlockedPageCount;
                if (MemCaptureManager.Instance.CapturedMems != null)
                {
                    foreach (var entry in MemCaptureManager.Instance.CapturedMems)
                    {
                        saveData.serializedCapturedMems.Add(entry);
                    }
                }
            }

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

                    if (!isTeardown && facility.DeployedMemEntries != null)
                    {
                        List<string> liveIDs = new List<string>();
                        bool isClean = true;
                        foreach (var entry in facility.DeployedMemEntries)
                        {
                            if (entry == null) { isClean = false; break; }
                            if (!string.IsNullOrEmpty(entry.KeyId)) liveIDs.Add(entry.KeyId);
                        }
                        if (isClean) rData.DeployedMemIDs = liveIDs;
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

                    if (!isTeardown && craft.DeployedMemEntries != null)
                    {
                        List<string> liveIDs = new List<string>();
                        bool isClean = true;
                        foreach (var entry in craft.DeployedMemEntries)
                        {
                            if (entry == null) { isClean = false; break; }
                            if (!string.IsNullOrEmpty(entry.KeyId)) liveIDs.Add(entry.KeyId);
                        }
                        if (isClean) rData.DeployedMemIDs = liveIDs;
                    }
                }

                bSave.runtimeData = rData;
                saveData.placedBuildings.Add(bSave);
            }

            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && pInventory.inventory != null) saveData.playerInventoryData = PackContainerData(pInventory.inventory);

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && wInventory.storage != null) saveData.warehouseStorageData = PackContainerData(wInventory.storage);

            if (ConsumeFoodSystem.Instance != null)
            {
                if (ConsumeFoodSystem.Instance.FoodStorageContainer != null)
                    saveData.foodWarehouseStorageData = PackContainerData(ConsumeFoodSystem.Instance.FoodStorageContainer);

                if (ConsumeFoodSystem.Instance.FoodBagContainer != null)
                    saveData.foodBagStorageData = PackContainerData(ConsumeFoodSystem.Instance.FoodBagContainer);

                saveData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
                saveData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
                saveData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;
            }

            string jsonString = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(saveFilePath, jsonString);

            PlayerPrefs.SetString(LastPlayTimeKey, saveData.lastSaveTime);
            PlayerPrefs.Save();

            Debug.Log($"<color=lime><b>[RecordManager]</b></color> 영지 전체 데이터 백업 일괄 정산 세이브 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 일괄 마감 세이브 도중 치명적 예외: {e.Message}");
        }
    }

    public void LoadTerritoryRecordData()
    {
        facilityDatabase.Clear();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[RecordManager]</color> 최초 파일이 없어 디폴트 뼈대를 자동 개설합니다.");
            CreateDefaultTerritoryRecord(); // 기본 파일 즉시 생성
            LoadTerritoryRecordData();       // 재귀 호출하여 데이터 복구 진행
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

            if (MemCaptureManager.Instance != null && saveData.serializedCapturedMems != null && saveData.serializedCapturedMems.Count > 0)
            {
                SetPrivateFieldSafely(MemCaptureManager.Instance, "unlockedPageCount", saveData.unlockedPageCount);

                var capMemsField = typeof(MemCaptureManager).GetField("capturedMems", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                List<CapturedMemEntry> runtimeMemsList = capMemsField?.GetValue(MemCaptureManager.Instance) as List<CapturedMemEntry>;

                if (runtimeMemsList != null)
                {
                    runtimeMemsList.Clear();
                    foreach (var savedMem in saveData.serializedCapturedMems)
                    {
                        runtimeMemsList.Add(savedMem);
                    }
                }

                var changeEvent = typeof(MemCaptureManager).GetField("OnCapturedMemsChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)?.GetValue(MemCaptureManager.Instance) as Action;
                changeEvent?.Invoke();
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

                    var entry = bSave.runtimeData;
                    if (entry == null)
                    {
                        entry = new PlantJSONSaveData { Building_ID = $"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}" };
                    }

                    List<CapturedMemEntry> matchedEntries = new List<CapturedMemEntry>();
                    List<MemData> restoredMems = new List<MemData>();

                    if (MemCaptureManager.Instance != null && entry.DeployedMemIDs != null)
                    {
                        var warehouseList = MemCaptureManager.Instance.CapturedMems;
                        foreach (var savedKeyId in entry.DeployedMemIDs)
                        {
                            var warehouseMatch = warehouseList.FirstOrDefault(m => m != null && m.KeyId == savedKeyId);

                            if (warehouseMatch != null)
                            {
                                warehouseMatch.IsActive = true;
                                matchedEntries.Add(warehouseMatch);

                                MemData mData = new MemData();
                                mData.memName = warehouseMatch.MemId;

                                var template = MemCatalogManager.Instance != null ? MemCatalogManager.Instance.FindMemData(warehouseMatch.MemId) : null;
                                mData.maxHunger = (template != null) ? template.maxHunger : 10;

                                restoredMems.Add(mData);
                            }
                        }
                    }

                    if (spawnedObj.TryGetComponent<ProductionFacilityRuntime>(out var facility))
                    {
                        facility.buildingData = matchData;
                        facility.isProducing = entry.isActive;
                        facility.currentProgressTime = entry.currentProgressTime;
                        facility.currentStorageCount = entry.currentStorageCount;

                        facility.craftingItem = FindItemDataInProject(entry.currentCraftingItemId);
                        facility.UpdateMaxStorage();

                        if (facility.DeployedMems != null && facility.DeployedMemEntries != null)
                        {
                            facility.DeployedMems.Clear();
                            facility.DeployedMemEntries.Clear();
                            facility.DeployedMems.AddRange(restoredMems);
                            facility.DeployedMemEntries.AddRange(matchedEntries);
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

                        craft.currentCraftingItem = FindItemDataInProject(entry.currentCraftingItemId);

                        if (craft.DeployedMems != null && craft.DeployedMemEntries != null)
                        {
                            craft.DeployedMems.Clear();
                            craft.DeployedMemEntries.Clear();
                            craft.DeployedMems.AddRange(restoredMems);
                            craft.DeployedMemEntries.AddRange(matchedEntries);
                        }

                        if (craft.currentCraftingItem != null && craft.DeployedMems.Count > 0)
                        {
                            craft.totalRequiredTime = ProductionCalculator.CalculateFinalProductionTime(20f, craft.DeployedMems);
                            craft.isProducing = (ConsumeFoodSystem.Instance == null || !ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation) ? entry.isActive : false;
                        }
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

                    string uniqueId = $"{matchData.buildingName}_{bSave.gridX}_{bSave.gridZ}";
                    facilityDatabase[uniqueId] = entry;
                }
            }

            SetPrivateFieldSafely(gridManager, "occupiedCells", occupiedCells);
            SetPrivateFieldSafely(gridManager, "buildingObjectsGrid", buildingObjectsGrid);
            SetPrivateFieldSafely(gridManager, "buildingDataGrid", buildingDataGrid);

            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && saveData.playerInventoryData != null) UnpackContainerData(saveData.playerInventoryData, pInventory.inventory);

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && saveData.warehouseStorageData != null) UnpackContainerData(saveData.warehouseStorageData, wInventory.storage);

            if (ConsumeFoodSystem.Instance != null)
            {
                if (saveData.foodWarehouseStorageData != null)
                    UnpackContainerData(saveData.foodWarehouseStorageData, ConsumeFoodSystem.Instance.FoodStorageContainer);

                if (saveData.foodBagStorageData != null)
                    UnpackContainerData(saveData.foodBagStorageData, ConsumeFoodSystem.Instance.FoodBagContainer);

                ConsumeFoodSystem.Instance.ForceSyncManualState(saveData.currentSatiety, saveData.maxSatiety, saveData.isWorkStoppedDueToStarvation);
                ConsumeFoodSystem.Instance.ProcessFoodConsumption(true);
            }

            if (TotalHungerManager.Instance != null) TotalHungerManager.Instance.RecalculateTotalHunger();

            ProcessOfflineRewards();

            var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
            if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

            RefreshActivePanelMemSlotsRealtime();

            SatisFactoryUI satisfactionUI = FindFirstObjectByType<SatisFactoryUI>();
            if (satisfactionUI != null)
            {
                satisfactionUI.RecalculateSatisfaction();
            }

            Debug.Log($"<color=cyan><b>[RecordManager]</b></color> 저장 기록으로부터 무결성 완전 로드 성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 데이터 복원(Load) 중 치명적 예외 발생: {e.Message}");
        }
    }

    private void MaintainHealthyFacilityCache()
    {
        if (isApplicationQuitting) return;

        var activeBuildings = FindObjectsByType<BuildingRuntime>(FindObjectsSortMode.None);
        if (activeBuildings == null || activeBuildings.Length == 0) return;

        foreach (var br in activeBuildings)
        {
            if (br == null || br.buildingData == null) continue;

            string uniqueId = $"{br.buildingData.buildingName}_{br.gridX}_{br.gridZ}";
            PlantJSONSaveData rData = GetFacilityData(uniqueId);

            if (br.TryGetComponent<ProductionFacilityRuntime>(out var facility))
            {
                if (facility.DeployedMemEntries != null)
                {
                    List<string> currentIDs = new List<string>();
                    bool containsDestroyedNull = false;

                    foreach (var entry in facility.DeployedMemEntries)
                    {
                        if (entry == null) { containsDestroyedNull = true; break; }
                        if (!string.IsNullOrEmpty(entry.KeyId)) currentIDs.Add(entry.KeyId);
                    }

                    if (!containsDestroyedNull)
                    {
                        rData.DeployedMemIDs = currentIDs;
                    }
                }
            }
            else if (br.TryGetComponent<ProductionCraftRuntime>(out var craft))
            {
                if (craft.DeployedMemEntries != null)
                {
                    List<string> currentIDs = new List<string>();
                    bool containsDestroyedNull = false;

                    foreach (var entry in craft.DeployedMemEntries)
                    {
                        if (entry == null) { containsDestroyedNull = true; break; }
                        if (!string.IsNullOrEmpty(entry.KeyId)) currentIDs.Add(entry.KeyId);
                    }

                    if (!containsDestroyedNull)
                    {
                        rData.DeployedMemIDs = currentIDs;
                    }
                }
            }
        }
    }

    private ItemData PackItemData(string itemId)
    {
        return FindItemDataInProject(itemId);
    }

    private ItemData FindItemDataInProject(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        ItemData[] allItems = Resources.FindObjectsOfTypeAll<ItemData>();
        return allItems.FirstOrDefault(item => item != null && item.Item_ID == itemId);
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

    private void OnApplicationQuit()
    {
        if (isApplicationQuitting) return;
        isApplicationQuitting = true;
        ExecuteBulkSaveProcess(true);
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            ExecuteBulkSaveProcess(false);
        }
    }

    private void OnSceneUnloadedTrigger(Scene currentScene)
    {
        if (isApplicationQuitting) return;
        ExecuteBulkProcess();
    }

    private void ExecuteBulkProcess()
    {
        ExecuteBulkSaveProcess(true);
    }

    public void SaveCurrentTime()
    {
        PlayerPrefs.SetString(LastPlayTimeKey, DateTime.UtcNow.ToString("o"));
        PlayerPrefs.Save();
    }

    public void ProcessOfflineRewards()
    {
        if (!PlayerPrefs.HasKey(LastPlayTimeKey)) return;

        string lastTimeStr = PlayerPrefs.GetString(LastPlayTimeKey);
        if (!DateTime.TryParse(lastTimeStr, out DateTime lastPlayTime)) return;

        DateTime nowTime = DateTime.UtcNow;
        TimeSpan offlineSpan = nowTime - lastPlayTime;
        double totalOfflineSeconds = offlineSpan.TotalSeconds;

        if (totalOfflineSeconds <= 0) return;

        int totalHungerCostPerMinute = TotalHungerManager.Instance != null ? TotalHungerManager.Instance.TotalHungerPerMinute : 0;
        int totalFoodSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;

        double actualRewardSeconds = 0;

        if (totalHungerCostPerMinute > 0)
        {
            int foodMinutes = totalFoodSatiety / totalHungerCostPerMinute;
            int activeProductionMinutes = foodMinutes + 1;
            double maxProductionSeconds = activeProductionMinutes * 60.0;

            actualRewardSeconds = Math.Min(totalOfflineSeconds, maxProductionSeconds);
        }
        else
        {
            actualRewardSeconds = totalOfflineSeconds;
        }

        if (actualRewardSeconds > 0)
        {
            ApplyOfflineProductionToFacilities(actualRewardSeconds);

            if (totalHungerCostPerMinute > 0)
            {
                int activeMinutes = Mathf.CeilToInt((float)actualRewardSeconds / 60f);
                int consumedSatiety = activeMinutes * totalHungerCostPerMinute;

                if (ConsumeFoodSystem.Instance != null)
                {
                    int finalSatiety = Mathf.Max(0, totalFoodSatiety - consumedSatiety);
                    ConsumeFoodSystem.Instance.ProcessFoodConsumption(false);
                    ConsumeFoodSystem.Instance.ForceSyncManualState(finalSatiety, ConsumeFoodSystem.Instance.MaxSatiety, ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation);
                }
            }
        }

        SaveCurrentTime();
    }

    private void ApplyOfflineProductionToFacilities(double activeSeconds)
    {
        var productionFacilities = FindObjectsByType<ProductionFacilityRuntime>(FindObjectsSortMode.None);
        foreach (var facility in productionFacilities)
        {
            if (facility == null || !facility.isProducing) continue;

            float craftInterval = 30f;
            double totalAccumulatedSeconds = facility.currentProgressTime + activeSeconds;

            int rewardCount = Mathf.FloorToInt((float)totalAccumulatedSeconds / craftInterval);
            float remainingProgressTime = (float)(totalAccumulatedSeconds % craftInterval);

            if (rewardCount > 0)
            {
                facility.currentStorageCount += rewardCount;
                facility.UpdateMaxStorage();
            }

            facility.currentProgressTime = remainingProgressTime;

            string uniqueId = $"{facility.buildingData.buildingName}_{facility.GetComponent<BuildingRuntime>().gridX}_{facility.GetComponent<BuildingRuntime>().gridZ}";
            if (facilityDatabase.ContainsKey(uniqueId))
            {
                facilityDatabase[uniqueId].currentStorageCount = facility.currentStorageCount;
                facilityDatabase[uniqueId].currentProgressTime = facility.currentProgressTime;
            }
        }

        var craftingFacilities = FindObjectsByType<ProductionCraftRuntime>(FindObjectsSortMode.None);
        foreach (var craft in craftingFacilities)
        {
            if (craft == null || !craft.isProducing) continue;

            float craftInterval = 30f;
            double totalAccumulatedSeconds = craft.currentProgressTime + activeSeconds;

            int rewardCount = Mathf.FloorToInt((float)totalAccumulatedSeconds / craftInterval);
            float remainingProgressTime = (float)(totalAccumulatedSeconds % craftInterval);

            if (rewardCount > 0)
            {
                if (craft.remainingQuantity > 0)
                {
                    int realLimit = Mathf.Min(rewardCount, craft.remainingQuantity);
                    craft.currentStorageCount += realLimit;
                    craft.remainingQuantity -= realLimit;

                    if (craft.remainingQuantity <= 0)
                    {
                        craft.isProducing = false;
                        remainingProgressTime = 0f;
                    }
                }
                else
                {
                    craft.currentStorageCount += rewardCount;
                }
            }

            craft.currentProgressTime = remainingProgressTime;

            string uniqueId = $"{craft.buildingData.buildingName}_{craft.GetComponent<BuildingRuntime>().gridX}_{craft.GetComponent<BuildingRuntime>().gridZ}";
            if (facilityDatabase.ContainsKey(uniqueId))
            {
                facilityDatabase[uniqueId].currentStorageCount = craft.currentStorageCount;
                facilityDatabase[uniqueId].remainingQuantity = craft.remainingQuantity;
                facilityDatabase[uniqueId].isActive = craft.isProducing;
                facilityDatabase[uniqueId].currentProgressTime = craft.currentProgressTime;
            }
        }
    }

    private void RefreshActivePanelMemSlotsRealtime()
    {
        var prodPanels = FindObjectsByType<ProductionPanelUI>(FindObjectsSortMode.None);
        foreach (var panel in prodPanels)
        {
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                panel.RefreshUI();
                Debug.Log($"[RecordManager] Production UI Forced Refreshed.");
            }
        }

        var craftPanels = FindObjectsByType<CraftingPanelUI>(FindObjectsSortMode.None);
        foreach (var panel in craftPanels)
        {
            if (panel != null && panel.gameObject.activeInHierarchy)
            {
                panel.RefreshUI();
                Debug.Log($"[RecordManager] Crafting UI Forced Refreshed.");
            }
        }
    }
}