using HDY.Item;
using HDY.Mem;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance { get; private set; }

    private string saveFilePath;
    public string SaveFilePath => saveFilePath;

    private Dictionary<string, FacilityData> facilityDatabase = new Dictionary<string, FacilityData>();
    public bool IsBlueprintGiven { get; private set; }

    public static bool IsLoadingData { get; private set; } = false;

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
        Debug.Log($"[세이브 파일 실물 위치] : {Application.persistentDataPath}");
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoadedTrigger;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedTrigger;
    }

    private void OnSceneLoadedTrigger(Scene scene, LoadSceneMode mode)
    {
        IsLoadingData = true;

        try
        {
            string sceneName = scene.name.ToLower();

            if (sceneName.Contains("territory"))
            {
                Debug.Log("<color=cyan>[RecordManager]</color> 영지 씬 로드 감지 ➡️ 즉시 데이터 완전 복구 개시");
                LoadAndBroadcastTerritoryData(SceneType.Territory);
            }
            else if (sceneName.Contains("main_world"))
            {
                Debug.Log("<color=yellow>[RecordManager]</color> 탐험 씬(Main_World) 로드 감지 ➡️ 플레이어 귀속 데이터 한정 복구 개시");
                LoadAndBroadcastTerritoryData(SceneType.Exploration);
            }
        }
        finally
        {
            IsLoadingData = false;
        }
    }

    public void LoadAndBroadcastTerritoryData(SceneType sceneType)
    {
        List<IRecord> subRecords = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                      .OfType<IRecord>()
                                      .ToList();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[RecordManager]</color> 세이브 파일이 없어 초기 장부를 생성합니다.");
            SaveData defaultData = new SaveData();

            foreach (var record in subRecords)
            {
                record.InitDefaultData(ref defaultData);
            }

            defaultData.lastSaveTime = DateTime.UtcNow.ToString("o");
            File.WriteAllText(saveFilePath, JsonUtility.ToJson(defaultData, true));
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonString);
            if (saveData == null) return;

            IsBlueprintGiven = saveData.isBlueprintGiven;

            var territoryRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "TerritoryRecordData");
            territoryRecord?.ApplyData(saveData, sceneType);

            var memRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "MemRecordData");
            memRecord?.ApplyData(saveData, sceneType);

            var inventoryRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "PlayerInventoryRecord");
            inventoryRecord?.ApplyData(saveData, sceneType);

            var facilityRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "FacilityRecordData");
            facilityRecord?.ApplyData(saveData, sceneType);

            var foodRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "ConsumeFoodRecordData");
            foodRecord?.ApplyData(saveData, sceneType);

            var timeRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "TimeRecordData");
            timeRecord?.ApplyData(saveData, sceneType);

            var offlineRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "OfflineRewardRecordData");
            offlineRecord?.ApplyData(saveData, sceneType);

            foreach (var record in subRecords)
            {
                if (record == territoryRecord || record == memRecord || record == inventoryRecord ||
                    record == facilityRecord || record == foodRecord || record == timeRecord || record == offlineRecord)
                    continue;

                record.ApplyData(saveData, sceneType);
            }

            if (sceneType == SceneType.Territory)
            {
                StartCoroutine(SpawnWarehouseWanderersWithDelayRoutine());
            }

            Debug.Log($"<color=lime>[RecordManager]</color> {sceneType} 환경 맞춤 데이터 복구 및 정산 완료!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 로드 및 분배 중 치명적 예외 발생:\n{e.ToString()}");
        }
    }

    public SaveData ReadRawSaveFileOnly()
    {
        if (!File.Exists(saveFilePath)) return null;
        try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(saveFilePath)); }
        catch { return null; }
    }

    public FacilityData GetFacilityData(string buildingId)
    {
        if (string.IsNullOrEmpty(buildingId)) return null;
        if (!facilityDatabase.ContainsKey(buildingId))
        {
            facilityDatabase.Add(buildingId, new FacilityData
            {
                Building_ID = buildingId,
                isActive = false,
                currentCraftingItemId = "",
                targetQuantity = 1
            });
        }
        return facilityDatabase[buildingId];
    }

    public void UpdateFacilityData(string buildingId, FacilityData updatedData)
    {
        if (facilityDatabase.ContainsKey(buildingId)) facilityDatabase[buildingId] = updatedData;
        else facilityDatabase.Add(buildingId, updatedData);
    }

    // 🌟 [수정 위치]: 배치 취소/철거 시 씬에 없는 건물의 딕셔너리 캐시 데이터를 완전히 동기화 정제하는 메서드 추가
    public void SynchronizeFacilityDatabase(Dictionary<string, FacilityData> activeFacilities)
    {
        facilityDatabase.Clear();
        if (activeFacilities != null)
        {
            foreach (var pair in activeFacilities)
            {
                facilityDatabase[pair.Key] = pair.Value;
            }
        }
    }

    public ContainerData PackContainerData(InventoryContainer container)
    {
        if (container == null) return null;

        var data = new ContainerData { width = container.width, height = container.height };
        if (container.slots != null)
        {
            foreach (var slot in container.slots)
            {
                data.slots.Add(new ItemStackData { itemId = slot != null ? slot.itemId : "", amount = slot != null ? slot.amount : 0 });
            }
        }
        return data;
    }

    public void UnpackContainerData(ContainerData source, InventoryContainer target)
    {
        if (source == null || target == null) return;

        if (source.slots == null)
        {
            Debug.LogWarning("[RecordManager] ⚠️ 세이브 데이터의 slots 리스트가 null입니다. 연산을 방어적으로 취소합니다.");
            return;
        }

        target.width = source.width;
        target.height = source.height;

        int requiredCapacity = source.width * source.height;
        target.slots = new ItemStack[requiredCapacity];

        for (int i = 0; i < requiredCapacity; i++)
        {
            target.slots[i] = new ItemStack();

            if (i < source.slots.Count && source.slots[i] != null && !string.IsNullOrEmpty(source.slots[i].itemId) && source.slots[i].amount > 0)
            {
                target.slots[i].Set(source.slots[i].itemId, source.slots[i].amount);
            }
            else
            {
                target.slots[i].Clear();
            }
        }
    }

    public ItemData FindItemDataInProject(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;

        if (ItemCatalogManager.Instance != null)
        {
            ItemData catalogItem = ItemCatalogManager.Instance.FindItemData(itemId);
            if (catalogItem != null) return catalogItem;
        }

        Debug.LogError($"[RecordManager] ItemCatalogManager에서 '{itemId}' 아이템을 찾을 수 없습니다.");
        return null;
    }

    public void SetPrivateFieldSafely(object targetObject, string fieldName, object valueToSet)
    {
        if (targetObject == null || valueToSet == null) return;
        try
        {
            var fieldInfo = targetObject.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (fieldInfo != null) fieldInfo.SetValue(targetObject, valueToSet);
        }
        catch (Exception e) { Debug.LogWarning($"[RecordManager] 리플렉션 오류: {e.Message}"); }
    }

    public void RefreshActivePanelMemSlotsRealtime()
    {
        foreach (var p in FindObjectsByType<ProductionPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (p.gameObject.activeInHierarchy) p.RefreshUI();
        foreach (var c in FindObjectsByType<CraftingPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (c.gameObject.activeInHierarchy) c.RefreshUI();
        foreach (var r in FindObjectsByType<RanchPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (r.gameObject.activeInHierarchy) r.RefreshUI();
    }

    private IEnumerator SpawnWarehouseWanderersWithDelayRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        if (TerritoryWanderSpawner.Instance == null)
        {
            Debug.LogWarning("[RecordManager] 씬에 TerritoryWanderSpawner 인스턴스가 존재하지 않습니다.");
            yield break;
        }

        var memManager = FindFirstObjectByType<HDY.Capture.MemCaptureManager>();
        if (memManager != null && memManager.CapturedMems != null)
        {
            foreach (var entry in memManager.CapturedMems)
            {
                if (entry == null || entry.IsEmpty || entry.IsActive) continue;

                MemData realMemData = MemCatalogManager.Instance != null
                    ? MemCatalogManager.Instance.FindMemData(entry.MemId)
                    : null;

                if (realMemData != null)
                {
                    TerritoryWanderSpawner.Instance.SpawnWanderer(realMemData, new Vector3(0f, 1f, 0f));
                }
            }
        }
    }
}