using HDY.Capture;
using HDY.Forge;
using HDY.Item;
using HDY.Mem;
using HDY.Tutorial;
using KMS.InventoryDuped;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance { get; private set; }

    // [멤] "저장중" 표시에 쓸 폰트. 비워두면 SaveIndicatorUI가 현재 씬 HUD 텍스트에서
    // 한글을 그릴 수 있는 폰트를 자동으로 찾아 쓴다. (예: Jalnan2 SDF, MaruMinyaHangul SDF)
    [Header("[멤] 저장중 표시 폰트 (선택)")]
    [SerializeField] private TMP_FontAsset saveIndicatorFont;

    private string saveFilePath;
    public string SaveFilePath => saveFilePath;

    private Dictionary<string, FacilityData> facilityDatabase = new Dictionary<string, FacilityData>();
    public bool IsBlueprintGiven { get; private set; }

    public static bool IsLoadingData { get; private set; } = false;

    public static bool IsSceneUnloading { get; private set; } = false;

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

        // [멤] 저장중 표시기를 여기에 붙여둔다. RecordManager가 DontDestroyOnLoad이므로
        // 캐릭터 씬(월드)든 영지 씬이든 같은 표시기가 그대로 따라다닌다.
        SaveIndicatorUI.EnsureAttached(gameObject, saveIndicatorFont);

        Debug.Log($"[세이브 파일 실물 위치] : {Application.persistentDataPath}");
    }

    private void Start()
    {
        // [멤] 5분 자동저장 시작. Instance가 아닌 중복 오브젝트는 Awake에서 이미 파괴된다.
        if (Instance != this) return;
        StartAutoSaveRoutine();
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
        IsSceneUnloading = false;

        try
        {
            string sceneName = scene.name.ToLower();

            if (sceneName.Contains("territory"))
            {
                LoadAndBroadcastTerritoryData(SceneType.Territory);
            }
            else if (sceneName.Contains("main_world"))
            {
                LoadAndBroadcastTerritoryData(SceneType.Exploration);
            }
        }
        finally
        {
            IsLoadingData = false;
        }
    }

    /// <summary>
    /// 씬 컴포넌트 유무와 관계없이 100% 완전한 기본 SaveData 구조를 생성합니다.
    /// </summary>
    public SaveData CreateFullDefaultSaveData(string startScene = "Main_World_3")
    {
        SaveData data = new SaveData
        {
            lastSaveTime = DateTime.UtcNow.ToString("o"),
            lastPlayScene = startScene,
            territoryLevel = 1,
            currentExp = 0,
            requiredExp = 100,
            gold = 0,
            satisfaction = 0,
            isBlueprintGiven = false,
            currentGridSize = 5,
            expansionExpandedStates = new List<bool>(),
            // [HDY 요청 - 여신상 저장 버그 수정] recipeUnlockedStates(List<bool>, 인덱스 기반) ->
            // unlockedRecipeItemIds(List<string>, Item_ID 기반)로 교체. 자세한 이유는 SaveData.cs 주석 참고.
            unlockedRecipeItemIds = new List<string>(),
            cookRecipeUnlockedStates = new List<string>(),
            maxSatiety = 0,
            currentSatiety = 0,
            isWorkStoppedDueToStarvation = false,
            unlockedPageCount = 2,
            serializedCapturedMems = new List<CapturedMemEntry>(),
            firstCapturedTimestamps = new List<MemFirstCapturedEntry>(),
            placedBuildings = new List<PlacedBuildingData>(),
            waypointInfo = new List<WaypointInfo>(),
            chestInfo = new List<ChestInfo>(),
            forgeInstanceDataList = new List<ForgeInstanceData>(),
            equipmentInstanceDataList = new List<KMS.Equipment.EquipmentInstanceData>(),
            tutorialData = new TutorialProgressSnapshot(),
            playerPosDataList = new List<ScenePlayerPosData>
            {
                new ScenePlayerPosData { sceneName = "Main_World_3", lastPlayerPos = null, hasSavedPlayerPos = false },
                new ScenePlayerPosData { sceneName = "Main_World_Cave", lastPlayerPos = null, hasSavedPlayerPos = false }
            },
        };

        // 1. 인벤토리 기본 구조 (10x6 = 60 슬롯)
        data.playerInventoryData = new ContainerData { width = 10, height = 6, slots = new List<ItemStackData>() };
        for (int i = 0; i < 60; i++)
        {
            data.playerInventoryData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        // 2. 퀵슬롯 기본 구조 (10x1 = 10 슬롯)
        data.playerQuickSlotsData = new ContainerData { width = 10, height = 1, slots = new List<ItemStackData>() };
        for (int i = 0; i < 10; i++)
        {
            data.playerQuickSlotsData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        // 3. [멤] 장비 장착창 기본 구조 (12칸: 방어구 4 + 장신구 8)
        data.playerEquipmentData = new ContainerData { width = KMS.Equipment.EquipmentSlotLayout.TotalSlotCount, height = 1, slots = new List<ItemStackData>() };
        for (int i = 0; i < KMS.Equipment.EquipmentSlotLayout.TotalSlotCount; i++)
        {
            data.playerEquipmentData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }
        data.selectedQuickSlotIndex = 0;
        data.unlockedInventorySlotCount = 10;

        // 3. 일반 창고 기본 구조 (10x2 = 20 슬롯)
        data.warehouseStorageData = new ContainerData { width = 10, height = 2, slots = new List<ItemStackData>() };
        for (int i = 0; i < 20; i++)
        {
            data.warehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        // 4. 음식 창고 기본 구조 (10x1 = 5 슬롯)
        data.foodWarehouseStorageData = new ContainerData { width = 5, height = 1, slots = new List<ItemStackData>() };
        for (int i = 0; i < 5; i++)
        {
            data.foodWarehouseStorageData.slots.Add(new ItemStackData { itemId = "", amount = 0 });
        }

        // 5. 시간 및 플레이어 스탯 기본값
        data.timeData = new GameTimeSaveData
        {
            elapsedTime = 300f,
            lastSaveRealTimeKst = DateTime.UtcNow.ToString("o")
        };

        data.playerInfo = new PlayerInfo
        {
            maxHealth = 100f,
            maxHunger = 100f,
            currentHealth = 100f,
            currentHunger = 100f
        };

        return data;
    }

    /// <summary>
    /// [멤] 씬 전환 등 "무조건 지금 저장해야 하는" 상황용. 쿨다운을 무시하고 즉시 전체 저장한다.
    /// (기존 호출부 호환용 - LoadingManager가 이 시그니처로 부른다.)
    /// </summary>
    public void SaveAllData()
    {
        ExecuteSaveInternal(SaveReason.SceneChange, true);
    }

    // ==================================================================================
    // [멤] 저장 빈도 감축 시스템
    // ----------------------------------------------------------------------------------
    // 예전에는 각 RecordData가 이벤트(인벤토리 변경, 창고 변경, 시설 가동, 멤 포획, 장비 변경 등)를
    // 받을 때마다 세이브 파일 전체를 읽고 다시 쓰는 구조였다. 이벤트가 초 단위로 쏟아지는 시스템이
    // 많아서 디스크 쓰기가 과도했고, 그만큼 프레임 스파이크와 파일 오염 위험도 커졌다.
    //
    // 이제는 저장 시점을 아래 3종류로만 좁힌다.
    //   1) 5분 자동저장       - AutoSaveRoutine (변경사항이 있을 때만)
    //   2) 중요행동 즉시저장   - NotifyCriticalAction(SaveReason)
    //   3) 안전장치           - 씬 전환 / 게임 종료 / 일시정지(백그라운드 전환)
    // 그 외 모든 변경은 NotifyDataChanged()로 "변경됨(dirty)" 표시만 남기고 디스크를 건드리지 않는다.
    // ==================================================================================

    /// <summary>[멤] 저장이 일어난 이유. 로그와 "저장중" UI 표시에 쓰인다.</summary>
    public enum SaveReason
    {
        AutoSave,          // 5분 자동저장
        RecipeUnlock,      // 여신상 / 요리 제작법 해금 (레벨업 포함)
        WaypointRegister,  // 월드 웨이포인트 등록
        TerritoryLevelUp,  // 영지 레벨업 / 영지 확장
        FacilityChanged,   // 시설 신축 / 철거
        PlayerLifecycle,   // 플레이어 사망 / 부활
        MemFirstCapture,   // 멤 최초 포획 (도감 신규 등록)
        TutorialStep,      // 튜토리얼 단계 완료
        SceneChange,       // 씬 전환
        ApplicationQuit,   // 게임 종료 / 백그라운드 전환
        Manual             // 수동 호출 (디버그 등)
    }

    /// <summary>[멤] 디스크 쓰기 직전에 발생. SaveIndicatorUI가 "저장중" 표시에 사용한다.</summary>
    public static event Action<SaveReason> OnSaveStarted;

    /// <summary>[멤] 디스크 쓰기가 끝난 직후 발생.</summary>
    public static event Action<SaveReason> OnSaveCompleted;

    [Header("[멤] 저장 빈도 설정")]
    [Tooltip("변경사항이 있을 때 이 주기마다 한 번씩 자동저장한다.")]
    [SerializeField, Min(10f)] private float autoSaveInterval = 300f;

    [Tooltip("중요행동이 연달아 발생해도 이 시간 안에는 두 번 쓰지 않는다. 밀린 저장은 곧바로 예약 실행된다.")]
    [SerializeField, Min(0f)] private float minSaveCooldown = 1f;

    private bool isDirty = false;
    private float lastSaveRealtime = -999f;
    private Coroutine autoSaveRoutine;
    private Coroutine pendingSaveRoutine;

    /// <summary>[멤] 마지막 저장 이후 바뀐 내용이 있는지.</summary>
    public bool IsDirty => isDirty;

    /// <summary>
    /// [멤] "데이터가 바뀌었다"고만 알린다. 디스크는 건드리지 않는다.
    /// 고빈도 이벤트(인벤토리/창고/시설 가동/장비/스킬 등)는 전부 이 메서드를 쓴다.
    /// </summary>
    public static void NotifyDataChanged()
    {
        if (Instance == null) return;
        if (IsLoadingData || IsSceneUnloading) return;

        Instance.isDirty = true;
    }

    /// <summary>
    /// [멤] 중요행동이 일어났으니 지금 저장한다. (여신상 해금, 웨이포인트 등록, 영지 레벨업,
    /// 시설 신축/철거, 사망/부활, 멤 최초 포획, 튜토리얼 단계 완료)
    /// </summary>
    public static void NotifyCriticalAction(SaveReason reason)
    {
        if (Instance == null) return;

        Instance.isDirty = true;
        Instance.ExecuteSaveInternal(reason, false);
    }

    private void StartAutoSaveRoutine()
    {
        StopAutoSaveRoutine();
        autoSaveRoutine = StartCoroutine(AutoSaveRoutine());
    }

    private void StopAutoSaveRoutine()
    {
        if (autoSaveRoutine != null)
        {
            StopCoroutine(autoSaveRoutine);
            autoSaveRoutine = null;
        }
    }

    private IEnumerator AutoSaveRoutine()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(autoSaveInterval);

            if (isDirty)
            {
                ExecuteSaveInternal(SaveReason.AutoSave, true);
            }
        }
    }

    /// <summary>
    /// [멤] 쿨다운에 걸려 미뤄진 저장을 잠시 뒤에 반드시 실행해준다.
    /// (여신상을 연속으로 해금하는 등 중요행동이 몰려도 마지막 상태가 유실되지 않게 한다.)
    /// </summary>
    private IEnumerator DeferredSaveRoutine(SaveReason reason, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        pendingSaveRoutine = null;

        if (isDirty)
        {
            ExecuteSaveInternal(reason, true);
        }
    }

    /// <summary>
    /// [멤] 실제 디스크 쓰기. 모든 저장 경로는 결국 여기로 모인다.
    /// </summary>
    private void ExecuteSaveInternal(SaveReason reason, bool ignoreCooldown)
    {
        if (IsLoadingData || IsSceneUnloading) return;

        string currentScene = SceneManager.GetActiveScene().name.ToLower();
        if (currentScene.Contains("title"))
        {
            Debug.LogWarning("<color=yellow>[RecordManager]</color> ⚠️ 타이틀 씬에서는 세이브 파일 덮어쓰기를 방지합니다.");
            return;
        }

        float elapsed = Time.realtimeSinceStartup - lastSaveRealtime;
        if (!ignoreCooldown && elapsed < minSaveCooldown)
        {
            isDirty = true;
            if (pendingSaveRoutine == null && isActiveAndEnabled)
            {
                pendingSaveRoutine = StartCoroutine(DeferredSaveRoutine(reason, minSaveCooldown - elapsed));
            }
            return;
        }

        OnSaveStarted?.Invoke(reason);

        List<IRecord> subRecords = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                                      .OfType<IRecord>()
                                      .ToList();

        foreach (var record in subRecords)
        {
            try
            {
                record.SaveData(saveFilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RecordManager] {record.GetType().Name}.SaveData 예외: {e.Message}");
            }
        }

        lastSaveRealtime = Time.realtimeSinceStartup;
        isDirty = false;

        OnSaveCompleted?.Invoke(reason);
        Debug.Log($"<color=lime>[RecordManager]</color> 💾 전체 데이터 저장 완료! (사유: {reason})");
    }

    // [멤] 안전장치 - 게임을 정상 종료하거나 창을 이탈할 때는 변경사항을 무조건 밀어 넣는다.
    private void OnApplicationQuit()
    {
        if (Instance != this) return;
        ExecuteSaveInternal(SaveReason.ApplicationQuit, true);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (Instance != this) return;
        if (!pauseStatus || !isDirty) return;

        ExecuteSaveInternal(SaveReason.ApplicationQuit, true);
    }

    /// <summary>
    /// 씬 언로드 시작 시 호출하여 오브젝트 파괴 이벤트에 의한 세이브 파일 오염을 차단
    /// </summary>
    public void SetSceneUnloading(bool unloading)
    {
        IsSceneUnloading = unloading;
    }

    /// <summary>
    /// 씬 이동 전에 완전한 구조의 신규 세이브 파일만 미리 디스크에 생성합니다.
    /// </summary>
    public void PrepareNewGameFile(string defaultStartScene = "Main_World_3")
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
            }

            SaveData defaultData = CreateFullDefaultSaveData(defaultStartScene);

            List<IRecord> subRecords = GetAllRecordsInSceneAndPersistent();
            foreach (var record in subRecords)
            {
                try
                {
                    record.InitDefaultData(ref defaultData);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RecordManager] {record.GetType().Name}.InitDefaultData 예외 무시: {ex.Message}");
                }
            }

            File.WriteAllText(saveFilePath, JsonUtility.ToJson(defaultData, true));
            Debug.Log($"<color=lime>[RecordManager]</color> ✨ 완전한 기본 구조 세이브 파일 사전 생성 완료 ({defaultStartScene})");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 새 게임 파일 생성 중 오류: {e.Message}");
        }
    }

    /// <summary>
    /// [새로하기] 세이브 파일 삭제 ➡️ 완전한 기본 파일 생성 ➡️ Direct 씬 로딩
    /// </summary>
    public void StartNewGame(string defaultStartScene = "Main_World_3")
    {
        PrepareNewGameFile(defaultStartScene);
        SceneManager.LoadScene(defaultStartScene);
    }

    /// <summary>
    /// [이어하기] 세이브 데이터의 lastPlayScene을 읽어와 이동
    /// </summary>
    public void ContinueGame(string fallbackScene = "Main_World_3")
    {
        if (!File.Exists(saveFilePath))
        {
            StartNewGame(fallbackScene);
            return;
        }

        try
        {
            SaveData saveData = ReadRawSaveFileOnly();
            string targetScene = (saveData != null && !string.IsNullOrEmpty(saveData.lastPlayScene))
                ? saveData.lastPlayScene
                : fallbackScene;

            Debug.Log($"<color=lime>[RecordManager]</color> 🚀 이어하기 성공 ➡️ 저장된 씬으로 이동: <color=yellow>{targetScene}</color>");
            SceneManager.LoadScene(targetScene);
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 이어하기 중 오류: {e.Message}");
            SceneManager.LoadScene(fallbackScene);
        }
    }

    public void LoadAndBroadcastTerritoryData(SceneType sceneType)
    {
        List<IRecord> subRecords = GetAllRecordsInSceneAndPersistent();

        if (!File.Exists(saveFilePath))
        {
            Debug.Log("<color=cyan>[RecordManager]</color> 세이브 파일이 없어 초기 장부를 자동 생성합니다.");
            SaveData defaultData = CreateFullDefaultSaveData();

            foreach (var record in subRecords)
            {
                try
                {
                    record.InitDefaultData(ref defaultData);
                }
                catch { }
            }

            File.WriteAllText(saveFilePath, JsonUtility.ToJson(defaultData, true));
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonString);
            if (saveData == null) return;

            IsBlueprintGiven = saveData.isBlueprintGiven;

            var sceneRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "SceneRecordData");
            sceneRecord?.ApplyData(saveData, sceneType);

            var territoryRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "TerritoryRecordData");
            territoryRecord?.ApplyData(saveData, sceneType);

            var memRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "MemRecordData");
            memRecord?.ApplyData(saveData, sceneType);

            var inventoryRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "PlayerInventoryRecord");
            inventoryRecord?.ApplyData(saveData, sceneType);

            var warehouseRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "WarehouseRecordData");
            warehouseRecord?.ApplyData(saveData, sceneType);

            var foodRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "ConsumeFoodRecordData");
            foodRecord?.ApplyData(saveData, sceneType);

            var facilityRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "FacilityRecordData");
            facilityRecord?.ApplyData(saveData, sceneType);

            var timeRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "TimeRecordData");
            timeRecord?.ApplyData(saveData, sceneType);

            var cookRecipeRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "CookRecipeRecordData");
            cookRecipeRecord?.ApplyData(saveData, sceneType);

            var skillRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "SkillRecordData");
            skillRecord?.ApplyData(saveData, sceneType);

            var waypointRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "WaypointRecordData");
            waypointRecord?.ApplyData(saveData, sceneType);

            var chestRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "ChestRecordData");
            chestRecord?.ApplyData(saveData, sceneType);

            var forgeRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "ForgeRecordData");
            forgeRecord?.ApplyData(saveData, sceneType);

            // [멤] 장비 시스템 - 이 목록에 등록하지 않으면 EquipmentRecordData.ApplyData가 아예 호출되지 않는다.
            var equipmentRecord = subRecords.FirstOrDefault(rec => rec.GetType().Name == "EquipmentRecordData");
            equipmentRecord?.ApplyData(saveData, sceneType);

            var playerStatsRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "PlayerStatsRecordData");
            playerStatsRecord?.ApplyData(saveData, sceneType);

            var playerPosRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "PlayerPosRecordData");
            playerPosRecord?.ApplyData(saveData, sceneType);

            var tutorialRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "TutorialRecordData");
            tutorialRecord?.ApplyData(saveData, sceneType);

            var offlineRecord = subRecords.FirstOrDefault(r => r.GetType().Name == "OfflineRewardRecordData") as OfflineRewardRecordData;
            if (offlineRecord != null)
            {
                offlineRecord.ProcessOfflineReward(saveData);
            }

            if (sceneType == SceneType.Territory)
            {
                StartCoroutine(SpawnWarehouseWanderersWithDelayRoutine());
            }

            //ResynchronizeLoadedSceneState(subRecords, saveData);

            Debug.Log($"<color=lime>[RecordManager]</color> {sceneType} 환경 맞춤 데이터 완벽 복구 및 정산 완료!");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RecordManager] 로드 및 분배 중 치명적 예외 발생:\n{e.ToString()}");
        }
    }

    private List<IRecord> GetAllRecordsInSceneAndPersistent()
    {
        return FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OfType<IRecord>()
                .ToList();
    }

    private void ResynchronizeLoadedSceneState(List<IRecord> records, SaveData currentData)
    {
        foreach (var record in records)
        {
            try
            {
                record.SaveData(saveFilePath);
            }
            catch { }
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

    public Dictionary<string, FacilityData> GetFacilityDatabaseClone()
    {
        var cloneDict = new Dictionary<string, FacilityData>();
        foreach (var pair in facilityDatabase)
        {
            if (pair.Value == null) continue;
            cloneDict[pair.Key] = CloneFacilityData(pair.Value);
        }
        return cloneDict;
    }

    public void RestoreFacilityDatabase(Dictionary<string, FacilityData> backupDict)
    {
        facilityDatabase.Clear();
        if (backupDict != null)
        {
            foreach (var pair in backupDict)
            {
                if (pair.Value == null) continue;
                facilityDatabase[pair.Key] = CloneFacilityData(pair.Value);
            }
        }
    }

    private FacilityData CloneFacilityData(FacilityData source)
    {
        if (source == null) return null;
        FacilityData clone = new FacilityData
        {
            Building_ID = source.Building_ID,
            currentLevel = source.currentLevel,
            isActive = source.isActive,
            currentCraftingItemId = source.currentCraftingItemId,
            targetQuantity = source.targetQuantity,
            remainingQuantity = source.remainingQuantity,
            currentProgressTime = source.currentProgressTime,
            currentStorageCount = source.currentStorageCount,
            DeployedMemIDs = source.DeployedMemIDs != null ? new List<string>(source.DeployedMemIDs) : new List<string>(),
            ranchSlots = new List<RanchSlotSaveData>()
        };

        if (source.ranchSlots != null)
        {
            foreach (var slot in source.ranchSlots)
            {
                if (slot == null) continue;
                clone.ranchSlots.Add(new RanchSlotSaveData
                {
                    slotIndex = slot.slotIndex,
                    isUnlocked = slot.isUnlocked,
                    deployedMemKeyId = slot.deployedMemKeyId,
                    craftingItemId = slot.craftingItemId,
                    isProducing = slot.isProducing,
                    currentProgressTime = slot.currentProgressTime,
                    currentStorageCount = slot.currentStorageCount
                });
            }
        }
        return clone;
    }

    public ContainerData PackContainerData(InventoryContainer container)
    {
        if (container == null) return null;

        var data = new ContainerData { width = container.width, height = container.height };
        if (container.slots != null)
        {
            foreach (var slot in container.slots)
            {
                data.slots.Add(new ItemStackData
                {
                    itemId = slot != null ? slot.itemId : "",
                    amount = slot != null ? slot.amount : 0,
                    durability = slot != null ? slot.durability : -1
                });
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
                target.slots[i].Set(source.slots[i].itemId, source.slots[i].amount, source.slots[i].durability);
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
        foreach (var g in FindObjectsByType<GeneratorPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (g.gameObject.activeInHierarchy) g.RefreshUI();
        foreach (var t in FindObjectsByType<TransportPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (t.gameObject.activeInHierarchy) t.RefreshUI();
        foreach (var cf in FindObjectsByType<CampFirePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (cf.gameObject.activeInHierarchy) cf.RefreshUI();
        foreach (var k in FindObjectsByType<KitchenPanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) if (k.gameObject.activeInHierarchy) k.RefreshUI();
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
