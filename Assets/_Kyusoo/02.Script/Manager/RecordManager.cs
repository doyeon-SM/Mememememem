using System;
using System.IO;
using System.Collections.Generic;
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

    [Header("배치된 런타임 시설 및 일꾼 데이터")]
    public List<PlantJSONSaveData> facilitySaveList = new List<PlantJSONSaveData>();
}

/// <summary>
/// 👑 [영지 데이터 영구 저장/복원 및 오프라인 방치 정산 마스터 매니저]
/// UI 기능이 완전히 배제된 순수 데이터 제어 스크립트입니다.
/// 사라진 PlantSystem의 모든 장부 데이터베이스 및 오프라인 대기 연산 역할을 단독 수행합니다.
/// 게임 종료, 강제 종료(일시정지), 씬 이동 직전 "오직 단 한 번" 전체 백업 세이브를 수행합니다.
/// </summary>
public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance { get; private set; }

    private string saveFilePath;

    // 🌟 [PlantSystem 완벽 흡수]: 전역 시설 장부 데이터베이스 캐싱 딕셔너리
    private Dictionary<string, PlantJSONSaveData> facilityDatabase = new Dictionary<string, PlantJSONSaveData>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 도중 세이브 파괴 방지
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
        // 🚀 게임 기동 또는 영지 씬 진입 시 전체 복원 가동
        LoadTerritoryRecordData();
    }

    private void OnEnable()
    {
        // 씬 이탈(전투 씬 이동 등) 직전 트리거를 잡기 위해 유니티 이벤트 바인딩
        SceneManager.sceneUnloaded += OnSceneUnloadedTrigger;
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloadedTrigger;
    }

    // =================================================================
    // 🧬 [PlantSystem 흡수 통합: 시설 데이터베이스 관리 API 구역]
    // =================================================================
    /// <summary>기존 PlantSystem.Instance.GetFacilityData를 대체합니다.</summary>
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

    /// <summary>기존 PlantSystem.Instance.UpdateFacilityData를 대체합니다.</summary>
    public void UpdateFacilityData(string buildingId, PlantJSONSaveData updatedData)
    {
        if (string.IsNullOrEmpty(buildingId) || updatedData == null) return;

        if (facilityDatabase.ContainsKey(buildingId))
            facilityDatabase[buildingId] = updatedData;
        else
            facilityDatabase.Add(buildingId, updatedData);
    }

    /// <summary>
    /// 필드의 모든 생산 기지와 제작 공방을 실시간 전수 조사하여 런타임 수치를 메모리 장부에 동기화합니다.
    /// </summary>
    private void SynchronizeAllFacilitiesRealtime()
    {
        // 1. 일반 생산 시설(채광, 벌목 등) 전수조사 스캔
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

            // 투입되어 일하던 멤(Mem) 이름 리스트 백업
            data.DeployedMemIDs.Clear();
            if (facility.DeployedMems != null)
            {
                foreach (var mem in facility.DeployedMems)
                {
                    if (mem != null) data.DeployedMemIDs.Add(mem.memName);
                }
            }

            if (facilityDatabase.ContainsKey(uniqueId)) facilityDatabase[uniqueId] = data;
        }

        // 2. 제작 공방 시설(제작대 등) 전수조사 스캔
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

            data.DeployedMemIDs.Clear();
            if (craft.DeployedMems != null)
            {
                foreach (var mem in craft.DeployedMems)
                {
                    if (mem != null) data.DeployedMemIDs.Add(mem.memName);
                }
            }

            if (facilityDatabase.ContainsKey(uniqueId)) facilityDatabase[uniqueId] = data;
        }
    }

    // =================================================================
    // 💾 [마스터 데이터 일괄 취합 및 파일 쓰기 핵심 세이브 엔진]
    // =================================================================
    /// <summary>
    /// 🌟 [핵심 사양]: 종료 / 강제종료 / 씬 이동 직전에 단 한번 무겁게 호출되는 마감 직렬화 세이브 함수
    /// </summary>
    public void ExecuteBulkSaveProcess()
    {
        try
        {
            // 필드 내 배치된 모든 생산/제작 월드 시설물 데이터 전수 스캔 주입
            SynchronizeAllFacilitiesRealtime();

            TerritorySaveData saveData = new TerritorySaveData();
            saveData.lastSaveTime = DateTime.UtcNow.ToString("o"); // ISO 8601 표준 포맷 시간 기록

            // 1. 영지 기초 재화 및 레벨 데이터 취합 복사
            var territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                saveData.territoryLevel = territoryData.Level;
                saveData.currentExp = territoryData.CurrentExp;
                saveData.requiredExp = territoryData.RequiredExp;
                saveData.gold = territoryData.Gold;
                saveData.satisfaction = territoryData.Satisfaction;
                saveData.elapsedTime = territoryData.ElapsedTime;
            }

            // 2. 타일 확장 그리드 크기 및 단계 정보 취합 복사
            var expansion = FindFirstObjectByType<TerritoryExpansionManager>();
            if (expansion != null)
            {
                // 리플렉션을 사용해 private 필드인 currentGridSize 강제 추출 백업
                var fieldGridSize = typeof(TerritoryExpansionManager).GetField("currentGridSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (fieldGridSize != null)
                {
                    saveData.currentGridSize = (int)fieldGridSize.GetValue(expansion);
                }

                foreach (var step in expansion.ExpansionSteps)
                {
                    saveData.expansionExpandedStates.Add(step.IsExpanded);
                }
            }

            // 3. 가방 / 일반 창고 / 음식 창고 실물 장부 슬롯 팩킹 직렬화
            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && pInventory.inventory != null)
            {
                saveData.playerInventoryData = PackContainerData(pInventory.inventory);
            }

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && wInventory.storage != null)
            {
                saveData.warehouseStorageData = PackContainerData(wInventory.storage);
            }

            if (ConsumeFoodSystem.Instance != null && ConsumeFoodSystem.Instance.FoodStorageContainer != null)
            {
                saveData.foodWarehouseStorageData = PackContainerData(ConsumeFoodSystem.Instance.FoodStorageContainer);

                // 음식 보급망 내부 핵심 수치 장부 백업
                saveData.maxSatiety = ConsumeFoodSystem.Instance.MaxSatiety;
                saveData.currentSatiety = ConsumeFoodSystem.Instance.CurrentSatiety;
                saveData.isWorkStoppedDueToStarvation = ConsumeFoodSystem.Instance.IsWorkStoppedDueToStarvation;
            }

            // 4. 메모리 내 통합 관리 중인 모든 시설 리스트 마스터 세이브 데이터에 최종 편입
            foreach (var kvp in facilityDatabase)
            {
                saveData.facilitySaveList.Add(kvp.Value);
            }

            // 5. 로컬 JSON 파일 최종 디스크 기록
            string jsonString = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(saveFilePath, jsonString);

            Debug.Log($"<color=lime><b>[RecordManager]</b></color> 전체 데이터 일괄 백업 세이브 대성공! 경로: <color=yellow>{saveFilePath}</color>");

            // 🌟 [기획 사양]: 파일 저장이 끝나는 즉시 로컬 파일 위치를 유저 컴퓨터의 탐색기 창으로 강제 활성화 노출
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{saveFilePath.Replace("/", "\\")}\"");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 일괄 마감 세이브 처리 중 예외 발생: {e.Message}");
        }
    }

    // =================================================================
    // 📥 [역직렬화 데이터 복원 및 오프라인 방치 보상 연산 로드 엔진]
    // =================================================================
    public void LoadTerritoryRecordData()
    {
        facilityDatabase.Clear();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[RecordManager]</color> 최초 기동으로 확인되어 디폴트 JSON 구조를 초기 개설합니다.");
            ExecuteBulkSaveProcess();
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            TerritorySaveData saveData = JsonUtility.FromJson<TerritorySaveData>(jsonString);
            if (saveData == null) return;

            // 1. 기초 재화 및 경험치 장부 복구 복원
            var territoryData = FindFirstObjectByType<TerritoryData>();
            if (territoryData != null)
            {
                // 리플렉션을 통해 private 변수 강제 주입 우회 연산 시전
                typeof(TerritoryData).GetField("level", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.territoryLevel);
                typeof(TerritoryData).GetField("currentExp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.currentExp);
                typeof(TerritoryData).GetField("requiredExp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.requiredExp);
                typeof(TerritoryData).GetField("gold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.gold);
                typeof(TerritoryData).GetField("satisfaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.satisfaction);
                typeof(TerritoryData).GetField("elapsedTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(territoryData, saveData.elapsedTime);
            }

            // 2. 타일 확장 매니저 및 실물 타일 격자 크기 복구 복원
            var expansion = FindFirstObjectByType<TerritoryExpansionManager>();
            if (expansion != null)
            {
                typeof(TerritoryExpansionManager).GetField("currentGridSize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(expansion, saveData.currentGridSize);

                if (saveData.expansionExpandedStates != null)
                {
                    for (int i = 0; i < expansion.ExpansionSteps.Count; i++)
                    {
                        if (i < saveData.expansionExpandedStates.Count)
                        {
                            expansion.ExpansionSteps[i].IsExpanded = saveData.expansionExpandedStates[i];
                        }
                    }
                }

                // 타일 맵을 다루는 GridManager를 찾아 저장된 크기로 강제 확장 동기화 명령 하달
                var gridManager = FindFirstObjectByType<GridManager>();
                if (gridManager != null)
                {
                    gridManager.ExpandGrid(saveData.currentGridSize, saveData.currentGridSize);
                }
            }

            // 3. 가방 / 일반 창고 / 음식 창고 실물 ItemStack 데이터 장부 동기화 원복
            var pInventory = FindFirstObjectByType<PlayerInventory>();
            if (pInventory != null && saveData.playerInventoryData != null)
            {
                UnpackContainerData(saveData.playerInventoryData, pInventory.inventory);
            }

            var wInventory = FindFirstObjectByType<WarehouseInventory>();
            if (wInventory != null && saveData.warehouseStorageData != null)
            {
                UnpackContainerData(saveData.warehouseStorageData, wInventory.storage);
            }

            if (ConsumeFoodSystem.Instance != null && saveData.foodWarehouseStorageData != null)
            {
                UnpackContainerData(saveData.foodWarehouseStorageData, ConsumeFoodSystem.Instance.FoodStorageContainer);
            }

            // 4. 🌟 [사라진 PlantSystem 완벽 통합]: 오프라인 방치 시간 계산 및 시설 보상 누적 정산 연산
            float offlineSeconds = 0f;
            if (!string.IsNullOrEmpty(saveData.lastSaveTime))
            {
                DateTime lastSave = DateTime.Parse(saveData.lastSaveTime);
                TimeSpan offlineSpan = DateTime.UtcNow - lastSave;
                offlineSeconds = (float)offlineSpan.TotalSeconds;
                Debug.Log($"[RecordManager] 오프라인 누적 시간 감지: 약 {offlineSeconds:F1}초 방치 보상 연산 개시.");
            }

            if (saveData.facilitySaveList != null)
            {
                foreach (var entry in saveData.facilitySaveList)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Building_ID)) continue;

                    // 가동 중이던 시설물이었고 오프라인 방치 시간이 유효하다면 보상 누적
                    if (entry.isActive && offlineSeconds > 0f)
                    {
                        float unitCraftTime = 30f; // 기본 단위 생산 시간 30초 고정
                        int offlineProducedCount = Mathf.FloorToInt(offlineSeconds / unitCraftTime);

                        if (offlineProducedCount > 0)
                        {
                            if (entry.remainingQuantity > 0) // 목표 수량이 존재하는 공방 제작 시설
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
                            else // 한정 수량이 없는 무제한 수집 생산 기지
                            {
                                entry.currentStorageCount += offlineProducedCount;
                            }
                        }
                    }
                    facilityDatabase.Add(entry.Building_ID, entry);
                }
            }

            // 5. 음식 보급망 수치 장부 강제 동기화 리프레시 요청
            if (ConsumeFoodSystem.Instance != null)
            {
                ConsumeFoodSystem.Instance.ForceSyncManualState(saveData.currentSatiety, saveData.maxSatiety, saveData.isWorkStoppedDueToStarvation);
                ConsumeFoodSystem.Instance.ProcessFoodConsumption(true);
            }

            var warehouseUI = FindFirstObjectByType<FoodWarehouseUI>();
            if (warehouseUI != null) warehouseUI.RefreshAllPanelsAndSlots();

            Debug.Log($"<color=cyan><b>[RecordManager]</b></color> 로컬 JSON으로부터 전체 인프라 완전 복원 완료. 경로: {saveFilePath}");
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
                data.slots.Add(new SerializableItemStack
                {
                    itemId = slot != null ? slot.itemId : "",
                    amount = slot != null ? slot.amount : 0
                });
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
            {
                target.slots[i].Set(source.slots[i].itemId, source.slots[i].amount);
            }
            else
            {
                target.slots[i].Clear();
            }
        }
    }

    // =================================================================
    // 🚪 [종료 / 강제종료 / 씬 이동 단일 시점 마감 연쇄 트리거 접점]
    // =================================================================
    private void OnApplicationQuit()
    {
        ExecuteBulkSaveProcess();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            ExecuteBulkSaveProcess();
        }
    }

    private void OnSceneUnloadedTrigger(Scene currentScene)
    {
        ExecuteBulkSaveProcess();
    }
}