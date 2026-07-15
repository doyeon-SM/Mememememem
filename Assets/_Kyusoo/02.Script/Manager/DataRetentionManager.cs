using UnityEngine;
using UnityEngine.SceneManagement;
using KMS.InventoryDuped;
using HDY.Capture;
using MemSystem.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class DataRetentionManager : MonoBehaviour
{
    // 👑 영구 상주형 싱글톤 인스턴스 전역 개방
    public static DataRetentionManager Instance { get; private set; }

    private string saveFilePath;

    // 🌟 [무결성 핵심 가드]: 씬 전환/종료 시 유니티 파괴 순서로 인한 가방 데이터 증발을 막기 위해 
    // 씬 시작 시점의 건강한 컴포넌트 참조 주소를 원천 백업합니다.
    private PlayerInventory cachedInventory;

    private void Awake()
    {
        // [싱글톤 보장 프로세스 가동]
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveFilePath = Path.Combine(Application.persistentDataPath, "TerritoryRecord.json");
            Debug.Log($"<color=orange><b>[DataRetentionManager]</b></color> 🟠 [Awake] 싱글톤 매니저 생성 완료. 세이브 경로: {saveFilePath}");
        }
        else
        {
            Debug.Log($"<color=red><b>[DataRetentionManager]</b></color> 🔴 [Awake] 중복 매니저 가동 감지 ➡️ 오브젝트 파괴.");
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 최초 시작 시 씬에 가방 컴포넌트가 있다면 안전 바인딩을 진행합니다.
        RefreshAndSubscribeInventoryReference();

        // 파일 정사 개설 혹은 기존 데이터 복구 분기를 가동합니다.
        StartCoroutine(DelayedLoadRoutine());
    }

    private void OnEnable()
    {
        // 라이프사이클 및 멤 포획 최종 이벤트 리스너 결속
        SceneManager.sceneLoaded += OnSceneLoadedTrigger;
        SceneManager.sceneUnloaded += OnSceneUnloadedTrigger;

        // 멤 포획 성공 시 발행되는 전역 이벤트를 수신하도록 정밀 구독합니다.
        MemSystem.Events.MemEvents.OnMemCaptured += OnMemCapturedHandler;
        Debug.Log("<color=orange><b>[DataRetentionManager]</b></color> 🟠 [OnEnable] OnMemCaptured 포획 최종 이벤트 전역 감시선 작동 개시!");
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoadedTrigger;
        SceneManager.sceneUnloaded -= OnSceneUnloadedTrigger;

        // 이벤트 해제 안전장치
        MemSystem.Events.MemEvents.OnMemCaptured -= OnMemCapturedHandler;
        UnsubscribeInventoryEvent();
    }

    /// <summary>
    /// 🌟 [이벤트 엔지니어링]: 기존 가방의 연결을 끊고 현재 활성화된 씬 속의 가방을 새로 잡아 실시간 변경 이벤트를 바인딩합니다.
    /// </summary>
    private void RefreshAndSubscribeInventoryReference()
    {
        // A. 메모리 누수 및 중복 호출을 차단하기 위해 기존 구독을 확실하게 해제합니다.
        UnsubscribeInventoryEvent();

        // B. 현재 씬에서 가장 생생하게 깨어나 가동 중인 플레이어 인벤토리 주소를 포착합니다.
        cachedInventory = FindFirstObjectByType<PlayerInventory>();

        // C. 새로운 가방이 발견되었다면 실시간 감시 자석을 다시 연결합니다.
        if (cachedInventory != null)
        {
            cachedInventory.OnInventoryChanged += OnInventoryChangedTriggerHandler;
            Debug.Log("<color=orange><b>[DataRetentionManager]</b></color> 🟠 [이벤트 바인딩 완료] PlayerInventory.OnInventoryChanged 실시간 감시 추적 작동 개시!");
        }
        else
        {
            Debug.Log($"<color=orange><b>[DataRetentionManager]</b></color> 🟧 [참조 알림] 현재 활성화된 씬 내부에서 가방(PlayerInventory) 스크립트를 발견하지 못했습니다.");
        }
    }

    private void UnsubscribeInventoryEvent()
    {
        if (cachedInventory != null)
        {
            cachedInventory.OnInventoryChanged -= OnInventoryChangedTriggerHandler;
            cachedInventory = null;
        }
    }

    // =================================================================
    // 👑 [실시간 변화 처리 이벤트 핸들러 엔진]
    // =================================================================

    /// <summary>
    /// 🌟 [요구사항 1번 구현 부위]: 탐험 씬에서 아이템 습득/소모 등으로 인벤토리에 변화가 감지되는 즉시 실시간 디스크 백업 저장을 집행합니다.
    /// </summary>
    private void OnInventoryChangedTriggerHandler()
    {
        Debug.Log("<color=yellow><b>[DataRetentionManager]</b></color> 🟨 <b>[실시간 가방 변화 감지]</b> PlayerInventory.OnInventoryChanged 이벤트 발생! 즉시 세이브 파일 갱신을 시작합니다.");
        SaveAdventureDataFully();
    }

    /// <summary>
    /// 🎯 탐험 씬에서 일꾼 포획 성공 신호가 떨어지는 그 즉시 장부를 강제로 열어 실시간 가산 백업을 완료합니다.
    /// </summary>
    private void OnMemCapturedHandler(MemSystem.Core.Mem mem, MemSnapshot snapshot)
    {
        if (mem == null || snapshot == null) return;

        Debug.Log($"<color=yellow><b>[DataRetentionManager]</b></color> 🎯 <b>[포획 성공 이벤트 실시간 수신]</b> 이름: {snapshot.memName} | 장부 가산 정산을 개시합니다.");

        try
        {
            TerritorySaveData currentData = null;

            if (File.Exists(saveFilePath))
            {
                string jsonString = File.ReadAllText(saveFilePath);
                currentData = JsonUtility.FromJson<TerritorySaveData>(jsonString);
            }

            // 파일이 없는 최초 런타임 환경 상태라면 뼈대 구조를 먼저 형성합니다.
            if (currentData == null)
            {
                currentData = new TerritorySaveData
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
                    warehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodWarehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodBagStorageData = new SerializableContainerData { width = 10, height = 6 },
                    playerQuickSlotsData = new SerializableContainerData { width = 10, height = 1 },
                    selectedQuickSlotIndex = 0,
                    maxSatiety = 100,
                    currentSatiety = 100,
                    isWorkStoppedDueToStarvation = false,
                    unlockedPageCount = 2,
                    placedBuildings = new List<PlacedBuildingSaveData>(),
                    serializedCapturedMems = new List<CapturedMemEntry>()
                };

                for (int i = 0; i < 60; i++)
                {
                    currentData.warehouseStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                    currentData.foodWarehouseStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                    currentData.foodBagStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                }

                for (int i = 0; i < 10; i++)
                {
                    currentData.playerQuickSlotsData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                }
            }

            if (currentData.serializedCapturedMems == null)
            {
                currentData.serializedCapturedMems = new List<CapturedMemEntry>();
            }

            CapturedMemEntry newEntry = new CapturedMemEntry();
            newEntry.KeyId = Guid.NewGuid().ToString();
            newEntry.MemId = snapshot.memId;
            newEntry.IsActive = false;

            currentData.serializedCapturedMems.Add(newEntry);
            Debug.Log($"<color=lime><b>[DataRetentionManager]</b></color> ✅ [실시간 이식 성공] serializedCapturedMems 대장에 신규 일꾼 추가 완료! (총 창고 일꾼 수: {currentData.serializedCapturedMems.Count}마리)");

            // 포획된 순간 가방 및 퀵슬롯의 최신 상태도 함께 동기화 연동하여 장부에 합치 저장해 둡니다.
            if (cachedInventory != null)
            {
                if (cachedInventory.inventory != null)
                    currentData.playerInventoryData = PackContainerData(cachedInventory.inventory);

                if (cachedInventory.quickSlots != null)
                    currentData.playerQuickSlotsData = PackContainerData(cachedInventory.quickSlots);

                currentData.selectedQuickSlotIndex = cachedInventory.selectedQuickSlotIndex;
            }

            currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

            // 하드디스크 최종 강제 물리 라이팅 쓰기 실행
            string outputJson = JsonUtility.ToJson(currentData, true);
            File.WriteAllText(saveFilePath, outputJson);

            PlayerPrefs.SetString("OfflineLastPlayTime", currentData.lastSaveTime);
            PlayerPrefs.Save();

            Debug.Log("<color=lime><b>[DataRetentionManager]</b></color> 👑 포획 즉시 물리 파일 동기화 덮어쓰기 최종 대성공!");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red><b>[DataRetentionManager] 포획 성공 이벤트 처리 도중 예외 발생:</b></color> {e.Message}");
        }
    }

    // =================================================================
    // 👑 [라이프사이클 트리거 기반 가방 및 퀵슬롯 전담 세이브 부위]
    // =================================================================

    public void SaveCurrentProgressBeforeLeaveScene()
    {
        Debug.Log("<color=yellow><b>[DataRetentionManager]</b></color> 🟨 <b>[버튼 클릭 트리거 발생]</b> 영지 복귀 버튼 연동 감지! 즉시 인벤토리/퀵슬롯 저장 절차에 돌입합니다.");
        SaveAdventureDataFully();
    }

    private void OnSceneLoadedTrigger(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"<color=cyan><b>[DataRetentionManager]</b></color> 🟦 <b>[진입 로그]</b> 신규 씬 로드 마감: {scene.name} ➡️ 인벤토리 및 퀵슬롯 주소 갱신 및 언팩 복구를 실행합니다.");

        // 🌟 신규 씬에 진입했으므로 가방 컴포넌트를 재수색하고 실시간 감시 체계를 새로 붙여줍니다.
        RefreshAndSubscribeInventoryReference();
        ExecuteRetentionLoadProcess();
    }

    private void OnSceneUnloadedTrigger(Scene currentScene)
    {
        if (currentScene.name.ToLower().Contains("adventure") || currentScene.name.ToLower().Contains("exploration"))
        {
            Debug.Log($"<color=yellow><b>[DataRetentionManager]</b></color> 🟨 <b>[이동 로그]</b> 탐험 씬 탈출 확인({currentScene.name}) ➡️ 전체 인벤토리 데이터 최종 저장을 실행합니다.");
            SaveAdventureDataFully();
        }
    }

    private void OnApplicationQuit()
    {
        Debug.Log("<color=red><b>[DataRetentionManager]</b></color> 🟥 <b>[종료 로그]</b> 게임 프로세스 종료 감지 ➡️ 마감 인벤토리 세이브 처리.");
        SaveAdventureDataFully();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
        {
            Debug.Log("<color=yellow><b>[DataRetentionManager]</b></color> 🟨 <b>[정지 로그]</b> 백그라운드 전환 다운 감지 ➡️ 실시간 백업 가방/퀵슬롯 세이브 처리.");
            SaveAdventureDataFully();
        }
    }

    public void SaveAdventureDataFully()
    {
        Debug.Log("<color=lime><b>[DataRetentionManager]</b></color> 🟩 🚀 <b>[인벤토리 전담 세이브 시작]</b> ==========================================");
        try
        {
            TerritorySaveData currentData = null;

            if (File.Exists(saveFilePath))
            {
                string jsonString = File.ReadAllText(saveFilePath);
                currentData = JsonUtility.FromJson<TerritorySaveData>(jsonString);
            }

            if (currentData == null)
            {
                currentData = new TerritorySaveData
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
                    warehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodWarehouseStorageData = new SerializableContainerData { width = 10, height = 6 },
                    foodBagStorageData = new SerializableContainerData { width = 10, height = 6 },
                    playerQuickSlotsData = new SerializableContainerData { width = 10, height = 1 },
                    selectedQuickSlotIndex = 0,
                    maxSatiety = 100,
                    currentSatiety = 100,
                    isWorkStoppedDueToStarvation = false,
                    unlockedPageCount = 2,
                    placedBuildings = new List<PlacedBuildingSaveData>(),
                    serializedCapturedMems = new List<CapturedMemEntry>()
                };

                for (int i = 0; i < 60; i++)
                {
                    currentData.warehouseStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                    currentData.foodWarehouseStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                    currentData.foodBagStorageData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                }

                for (int i = 0; i < 10; i++)
                {
                    currentData.playerQuickSlotsData.slots.Add(new SerializableItemStack { itemId = "", amount = 0 });
                }
            }

            currentData.lastSaveTime = DateTime.UtcNow.ToString("o");

            // 일반 인벤토리 가방 + 퀵슬롯 데이터 통합 패킹 직렬화 처리 부위
            if (cachedInventory != null)
            {
                if (cachedInventory.inventory != null)
                {
                    currentData.playerInventoryData = PackContainerData(cachedInventory.inventory);
                    int filledSlot = currentData.playerInventoryData.slots.Count(s => !string.IsNullOrEmpty(s.itemId));
                    Debug.Log($"<color=lime><b>[DataRetentionManager]</b></color> 🟩 [일반 가방 정사 완료] 유효 적재 슬롯 수: {filledSlot} / 60");
                }

                if (cachedInventory.quickSlots != null)
                {
                    currentData.playerQuickSlotsData = PackContainerData(cachedInventory.quickSlots);
                    int filledQuickSlot = currentData.playerQuickSlotsData.slots.Count(s => !string.IsNullOrEmpty(s.itemId));
                    Debug.Log($"<color=lime><b>[DataRetentionManager]</b></color> 🟩 [퀵슬롯 대장 정사 완료] 유효 적재 슬롯 수: {filledQuickSlot} / 10");
                }

                currentData.selectedQuickSlotIndex = cachedInventory.selectedQuickSlotIndex;
            }
            else
            {
                Debug.LogWarning("<color=orange><b>[DataRetentionManager]</b></color> 🟧 인벤토리 참조가 유효하지 않아 기존 인벤토리/퀵슬롯 데이터를 유지합니다.");
            }

            if (currentData.serializedCapturedMems == null)
                currentData.serializedCapturedMems = new List<CapturedMemEntry>();

            if (currentData.placedBuildings == null)
                currentData.placedBuildings = new List<PlacedBuildingSaveData>();

            string outputJson = JsonUtility.ToJson(currentData, true);
            File.WriteAllText(saveFilePath, outputJson);

            PlayerPrefs.SetString("OfflineLastPlayTime", currentData.lastSaveTime);
            PlayerPrefs.Save();

            Debug.Log($"<color=lime><b>[DataRetentionManager] 👑 [통합 인벤토리 마감 세이브 완료]</b></color> JSON 물리 저장 성공! 크기: {outputJson.Length} bytes");
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red><b>[DataRetentionManager] 세이브 처리 도중 예외:</b></color> {e.Message}");
        }
        Debug.Log("<color=lime><b>[DataRetentionManager]</b></color> 🟩 =========================================================");
    }

    private IEnumerator DelayedLoadRoutine()
    {
        yield return null;
        ExecuteRetentionLoadProcess();
    }

    private void ExecuteRetentionLoadProcess()
    {
        Debug.Log("<color=cyan><b>[DataRetentionManager]</b></color> 🟦 🚀 <b>[인벤토리/퀵슬롯 동기화 로드 개시]</b> ==================================");
        if (!File.Exists(saveFilePath))
        {
            SaveAdventureDataFully();
            return;
        }

        try
        {
            string jsonString = File.ReadAllText(saveFilePath);
            TerritorySaveData saveData = JsonUtility.FromJson<TerritorySaveData>(jsonString);
            if (saveData == null) return;

            if (cachedInventory != null)
            {
                // 1. 일반 인벤토리 가방 복구 대입
                if (saveData.playerInventoryData != null)
                {
                    UnpackContainerData(saveData.playerInventoryData, cachedInventory.inventory);
                }

                // 2. 퀵슬롯 복구 대입
                if (saveData.playerQuickSlotsData != null)
                {
                    UnpackContainerData(saveData.playerQuickSlotsData, cachedInventory.quickSlots);
                }

                // 3. 현재 선택된 퀵슬롯 번호 주입 및 유효성 검증
                if (cachedInventory.quickSlots != null)
                {
                    cachedInventory.selectedQuickSlotIndex = cachedInventory.quickSlots.IsValidIndex(saveData.selectedQuickSlotIndex)
                        ? saveData.selectedQuickSlotIndex
                        : 0;
                }

                // 일반 가방 변경 시스템 이벤트 통지 발행
                var onInventoryChangedField = typeof(PlayerInventory).GetField("OnInventoryChanged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (onInventoryChangedField != null)
                {
                    Action onInventoryChanged = onInventoryChangedField.GetValue(cachedInventory) as Action;
                    onInventoryChanged?.Invoke();
                }

                // 퀵단축바 리프레시 동적 가동
                var notifyAllQuickSlotsMethod = typeof(PlayerInventory).GetMethod("NotifyAllQuickSlotsChanged",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                if (notifyAllQuickSlotsMethod != null)
                {
                    notifyAllQuickSlotsMethod.Invoke(cachedInventory, null);
                    Debug.Log("<color=cyan><b>[DataRetentionManager]</b></color> 🟦 [인벤토리 복구 완료] NotifyAllQuickSlotsChanged 시스템 새로고침 정산 성공!");
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"<color=red><b>[DataRetentionManager] 로드 동기화 도중 예외:</b></color> {e.Message}");
        }
        Debug.Log("<color=cyan><b>[DataRetentionManager]</b></color> 🟦 =========================================================");
    }

    // =================================================================
    // 👑 [유틸리티 및 RecordManager 원본 복사 이식 부위]
    // =================================================================

    public SerializableContainerData PackContainerData(InventoryContainer container)
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
}