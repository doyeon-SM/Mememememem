using System;
using System.Collections.Generic;
using GH.Loading;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 모든 웨이포인트의 런타임 해금 상태, 지도 개방 조건, 스톤 등록과 이동 요청을 관리합니다.
/// 같은 씬의 목적지는 즉시 이동하고, 다른 씬의 목적지는 <see cref="LoadingManager"/>를 통해
/// 씬을 로드한 뒤 해당 ID의 스톤 위치로 이동합니다.
/// </summary>
public class WayPointManager : MonoBehaviour
{
    /// <summary>씬 전환 후에도 유지되는 현재 웨이포인트 매니저입니다.</summary>
    public static WayPointManager Instance { get; private set; }

    [Header("WayPoint Data")]
    [SerializeField] private List<WayPointMapDefinition> mapDefinitions = new List<WayPointMapDefinition>();
    [SerializeField] private List<WayPointDefinition> definitions = new List<WayPointDefinition>();

    [Header("Runtime")]
    [SerializeField] private Transform player;
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool logWayPointState = true;
    [SerializeField] private bool logUnlockStackTrace = true;

    private readonly Dictionary<string, WayPointRunTime> statesById = new Dictionary<string, WayPointRunTime>();
    private readonly Dictionary<string, WayPointStone> stonesById = new Dictionary<string, WayPointStone>();
    private WayPointRunTime pendingTravelState;

    /// <summary>잠긴 웨이포인트가 처음 해금될 때 발생합니다.</summary>
    public event Action<WayPointRunTime> OnWayPointUnlocked;

    /// <summary>웨이포인트 상태 또는 연결된 스톤 참조가 바뀔 때 발생합니다.</summary>
    public event Action<WayPointRunTime> OnWayPointStateChanged;

    /// <summary>이전 지도의 완료로 다음 지도 사용 가능 여부가 바뀔 수 있을 때 발생합니다.</summary>
    public event Action<WayPointMapDefinition> OnMapAvailabilityChanged;

    /// <summary>검증을 통과한 웨이포인트 이동이 시작될 때 발생합니다.</summary>
    public event Action<WayPointRunTime> OnWayPointTravelStarted;

    /// <summary>동일 씬 이동 또는 다른 씬 로드와 목적지 배치가 완료될 때 발생합니다.</summary>
    public event Action<WayPointRunTime> OnWayPointTravelCompleted;

    /// <summary>이동할 수 없을 때 대상 상태와 실패 사유를 전달합니다.</summary>
    public event Action<WayPointRunTime, string> OnWayPointTravelFailed;

    /// <summary>웨이포인트 ID로 조회할 수 있는 전체 런타임 상태입니다.</summary>
    public IReadOnlyDictionary<string, WayPointRunTime> StatesById => statesById;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        InitializeStates();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        RegisterSceneStones();
    }

    private void OnDestroy()
    {
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadingCompleted -= HandleCrossSceneTravelCompleted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 씬에 이미 배치된 WayPointStone을 한 번 더 등록해서 실행 순서 문제를 줄인다.
    private void RegisterSceneStones()
    {
        WayPointStone[] stones = FindObjectsByType<WayPointStone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (WayPointStone stone in stones)
        {
            RegisterStone(stone);
        }
    }

    // Inspector에 등록된 ScriptableObject 목록으로 런타임 상태를 만든다.
    private void InitializeStates()
    {
        statesById.Clear();

        foreach (WayPointDefinition definition in definitions)
        {
            if (definition == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.id))
            {
                Debug.LogWarning("[WayPointManager] WayPointDefinition has empty id.", definition);
                continue;
            }

            if (statesById.ContainsKey(definition.id))
            {
                Debug.LogWarning($"[WayPointManager] Duplicate waypoint id: {definition.id}", definition);
                continue;
            }

            WayPointRunTime state = new WayPointRunTime(definition);
            state.IsActive = definition.IsUnlockedOnInitialize;
            statesById.Add(definition.id, state);

            LogState($"초기화: id={definition.id}, unlockType={definition.unlockType}, isActive={state.IsActive}", definition);
        }
    }

    // 웨이포인트 이동 목적지인 Stone을 매니저에 등록한다.
    /// <summary>
    /// 현재 씬에 존재하는 스톤을 해당 웨이포인트 ID의 런타임 상태에 연결합니다.
    /// 스톤의 <c>OnEnable</c> 또는 씬 로드 직후 호출됩니다.
    /// </summary>
    /// <param name="stone">등록할 씬 스톤입니다.</param>
    public void RegisterStone(WayPointStone stone)
    {
        if (stone == null)
        {
            return;
        }

        string id = stone.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            Debug.LogWarning("[WayPointManager] Tried to register stone with empty id.", stone);
            return;
        }

        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            Debug.LogWarning($"[WayPointManager] No WayPointDefinition found for stone id: {id}", stone);
            return;
        }

        if (stonesById.ContainsKey(id) && stonesById[id] != stone)
        {
            Debug.LogWarning($"[WayPointManager] Duplicate WayPointStone for id: {id}", stone);
        }

        stonesById[id] = stone;
        state.Stone = stone;

        stone.SetUnlockedState(state.IsActive);
        LogState($"스톤 등록: id={id}, isActive={state.IsActive}, stone={stone.name}", stone);
        OnWayPointStateChanged?.Invoke(state);
    }

    // Stone이 실제로 제거될 때 등록을 해제한다. 청크 비활성화만으로는 참조를 유지한다.
    /// <summary>파괴된 씬 스톤의 등록을 해제합니다. 청크 비활성화만으로는 호출되지 않습니다.</summary>
    /// <param name="stone">등록 해제할 스톤입니다.</param>
    public void UnregisterStone(WayPointStone stone)
    {
        if (stone == null)
        {
            return;
        }

        string id = stone.Id;
        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (stonesById.TryGetValue(id, out WayPointStone registeredStone) && registeredStone == stone)
        {
            stonesById.Remove(id);
        }

        if (statesById.TryGetValue(id, out WayPointRunTime state) && state.Stone == stone)
        {
            state.Stone = null;
            OnWayPointStateChanged?.Invoke(state);
        }
    }

    // 해당 ID의 웨이포인트가 해금되어 있는지 확인한다.
    /// <summary>지정한 ID의 웨이포인트가 해금되었는지 확인합니다.</summary>
    /// <param name="id">확인할 웨이포인트 고유 ID입니다.</param>
    public bool IsUnlocked(string id)
    {
        return statesById.TryGetValue(id, out WayPointRunTime state) && state.IsActive;
    }

    /// <summary>
    /// 지도 개방, 웨이포인트 해금, 현재 씬의 스톤 존재 여부를 기준으로 이동 가능성을 확인합니다.
    /// 다른 씬 목적지는 현재 씬에 스톤 참조가 없어도 이동 가능합니다.
    /// </summary>
    /// <param name="id">목적지 웨이포인트 ID입니다.</param>
    public bool CanTravel(string id)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state) ||
            !state.IsActive ||
            !IsMapAvailable(state.Definition.mapDefinition))
        {
            return false;
        }

        return !IsWayPointInCurrentScene(state.Definition) || state.Stone != null;
    }

    /// <summary>웨이포인트가 현재 활성 씬에 속하는지 Map Definition의 씬 이름으로 판별합니다.</summary>
    /// <param name="definition">판별할 웨이포인트 정의입니다.</param>
    public bool IsWayPointInCurrentScene(WayPointDefinition definition)
    {
        string targetSceneName = definition != null && definition.mapDefinition != null
            ? definition.mapDefinition.sceneName
            : string.Empty;

        return string.IsNullOrWhiteSpace(targetSceneName)
            || SceneManager.GetActiveScene().name == targetSceneName;
    }

    // 플레이어가 등록 오브젝트와 상호작용했을 때 웨이포인트를 해금한다.
    /// <summary>상호작용 방식 웨이포인트를 ID로 해금합니다.</summary>
    /// <returns>이번 호출에서 새로 해금되었으면 참입니다.</returns>
    public bool Unlock(string id)
    {
        return UnlockByInteraction(id);
    }

    /// <summary>상호작용 방식 웨이포인트를 정의로 해금합니다.</summary>
    /// <returns>이번 호출에서 새로 해금되었으면 참입니다.</returns>
    public bool Unlock(WayPointDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[WayPointManager] Cannot unlock null waypoint definition.");
            return false;
        }

        return Unlock(definition.id);
    }

    // 등록 오브젝트와 직접 상호작용했을 때 Interact 타입 웨이포인트를 해금한다.
    /// <summary>등록 오브젝트와 직접 상호작용한 웨이포인트를 해금합니다.</summary>
    public bool UnlockByInteraction(string id)
    {
        return SetUnlocked(id, false, WayPointUnlockType.Interact, "상호작용으로 해금됨");
    }

    /// <summary>등록 오브젝트와 직접 상호작용한 웨이포인트를 해금합니다.</summary>
    public bool UnlockByInteraction(WayPointDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[WayPointManager] Cannot unlock null waypoint definition by interaction.");
            return false;
        }

        return UnlockByInteraction(definition.id);
    }

    /// <summary>해당 ID가 현재 상호작용 방식으로 해금 가능한지 확인합니다.</summary>
    public bool CanUnlockByInteraction(string id)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            return false;
        }

        return !state.IsActive && state.Definition.CanUnlockByInteraction;
    }

    /// <summary>해당 정의가 현재 상호작용 방식으로 해금 가능한지 확인합니다.</summary>
    public bool CanUnlockByInteraction(WayPointDefinition definition)
    {
        return definition != null && CanUnlockByInteraction(definition.id);
    }

    // NPC, 퀘스트, 재화 지불 같은 외부 조건을 만족했을 때 웨이포인트를 해금한다.
    /// <summary>
    /// 퀘스트, 재화 지불, 테스트 등 외부 시스템의 조건 완료로 웨이포인트를 해금합니다.
    /// </summary>
    /// <param name="id">해금할 웨이포인트 ID입니다.</param>
    /// <param name="requireMapAvailable">참이면 웨이포인트가 속한 지도가 먼저 개방되어야 합니다.</param>
    public bool UnlockByExternalAction(string id, bool requireMapAvailable = true)
    {
        return SetUnlocked(id, requireMapAvailable, WayPointUnlockType.ExternalAction, "외부 동작으로 해금됨");
    }

    /// <summary>외부 시스템의 조건 완료로 웨이포인트를 해금합니다.</summary>
    public bool UnlockByExternalAction(WayPointDefinition definition, bool requireMapAvailable = true)
    {
        if (definition == null)
        {
            Debug.LogWarning("[WayPointManager] Cannot unlock null waypoint definition by external action.");
            return false;
        }

        return UnlockByExternalAction(definition.id, requireMapAvailable);
    }

    /// <summary>외부 시스템에서 지정 ID를 해금할 수 있는 현재 상태인지 확인합니다.</summary>
    public bool CanUnlockByExternalAction(string id, bool requireMapAvailable = true)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            return false;
        }

        if (state.IsActive)
        {
            return false;
        }

        return state.Definition.CanUnlockByExternalAction
            && (!requireMapAvailable || IsMapAvailable(state.Definition.mapDefinition));
    }

    /// <summary>외부 시스템에서 지정 정의를 해금할 수 있는 현재 상태인지 확인합니다.</summary>
    public bool CanUnlockByExternalAction(WayPointDefinition definition, bool requireMapAvailable = true)
    {
        return definition != null && CanUnlockByExternalAction(definition.id, requireMapAvailable);
    }

    private bool SetUnlocked(string id, bool requireMapAvailable, WayPointUnlockType requiredUnlockType, string logMessage)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            Debug.LogWarning($"[WayPointManager] Cannot unlock unknown waypoint id: {id}");
            return false;
        }

        if (state.IsActive)
        {
            LogState($"해금 요청 무시: 이미 활성화됨 id={id}", state.Definition);
            return false;
        }

        if (requireMapAvailable && !IsMapAvailable(state.Definition.mapDefinition))
        {
            Debug.LogWarning($"[WayPointManager] Cannot unlock waypoint before map is available. id={id}");
            return false;
        }

        if (state.Definition.unlockType != requiredUnlockType)
        {
            Debug.LogWarning($"[WayPointManager] Cannot unlock waypoint with {requiredUnlockType}. id={id}, unlockType={state.Definition.unlockType}", state.Definition);
            return false;
        }

        state.IsActive = true;
        LogState($"{logMessage}: id={id}", state.Definition, logUnlockStackTrace);

        if (state.Stone != null)
        {
            state.Stone.SetUnlockedState(true);
        }

        OnWayPointUnlocked?.Invoke(state);
        OnWayPointStateChanged?.Invoke(state);
        NotifyMapAvailabilityChanged(state.Definition.mapDefinition);

        return true;
    }

    // 특정 맵이 현재 열려 있는지 확인한다.
    /// <summary>
    /// 지도가 현재 선택 및 이동에 사용 가능한지 확인합니다.
    /// 첫 지도는 초기 설정을 따르고, 이후 지도는 이전 지도의 모든 웨이포인트 해금을 요구합니다.
    /// </summary>
    public bool IsMapAvailable(WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null)
        {
            return true;
        }

        if (mapDefinition.requiredPreviousMap == null)
        {
            return mapDefinition.unlockedOnStart;
        }

        return AreAllWayPointsUnlockedInMap(mapDefinition.requiredPreviousMap);
    }

    // 특정 맵에 포함된 모든 웨이포인트가 해금되었는지 확인한다.
    /// <summary>해당 지도에 등록된 웨이포인트가 하나 이상 존재하고 모두 해금됐는지 확인합니다.</summary>
    public bool AreAllWayPointsUnlockedInMap(WayPointMapDefinition mapDefinition)
    {
        bool hasAny = false;

        foreach (WayPointRunTime state in statesById.Values)
        {
            if (state.Definition == null || state.Definition.mapDefinition != mapDefinition)
            {
                continue;
            }

            hasAny = true;
            if (!state.IsActive)
            {
                return false;
            }
        }

        return hasAny;
    }

    // 지도 UI가 특정 맵에 표시할 웨이포인트 목록을 가져온다.
    /// <summary>지정 지도에 속한 웨이포인트 런타임 상태 목록을 반환합니다.</summary>
    public List<WayPointRunTime> GetStatesByMap(WayPointMapDefinition mapDefinition)
    {
        List<WayPointRunTime> result = new List<WayPointRunTime>();

        foreach (WayPointRunTime state in statesById.Values)
        {
            if (state.Definition == null)
            {
                continue;
            }

            if (mapDefinition == null || state.Definition.mapDefinition == mapDefinition)
            {
                result.Add(state);
            }
        }

        return result;
    }

    // 인스펙터에 등록된 맵과 웨이포인트에서 사용 중인 맵 목록을 중복 없이 가져온다.
    /// <summary>인스펙터 목록과 웨이포인트 정의에서 사용 중인 모든 지도 정의를 중복 없이 반환합니다.</summary>
    public List<WayPointMapDefinition> GetAllMaps()
    {
        List<WayPointMapDefinition> result = new List<WayPointMapDefinition>();

        foreach (WayPointMapDefinition mapDefinition in mapDefinitions)
        {
            if (mapDefinition != null && !result.Contains(mapDefinition))
            {
                result.Add(mapDefinition);
            }
        }

        foreach (WayPointRunTime state in statesById.Values)
        {
            WayPointMapDefinition map = state.Definition != null ? state.Definition.mapDefinition : null;
            if (map != null && !result.Contains(map))
            {
                result.Add(map);
            }
        }

        return result;
    }

    // 지도 UI에서 활성화된 아이콘을 클릭했을 때 이동을 시도한다.
    /// <summary>
    /// ID의 웨이포인트로 이동을 요청합니다. 플레이어는 태그를 통해 자동으로 찾습니다.
    /// </summary>
    /// <returns>즉시 이동했거나 씬 로딩 요청이 정상 시작되었으면 참입니다.</returns>
    public bool TryTravel(string id)
    {
        return TryTravel(id, ResolvePlayer());
    }

    // 해금된 웨이포인트라면 등록된 Stone의 SpawnPosition으로 플레이어를 이동시킨다.
    /// <summary>
    /// 지정 플레이어를 웨이포인트로 이동합니다. 같은 씬이면 즉시 이동하고,
    /// 다른 씬이면 플레이어 인자는 무시하고 새 씬의 Player 태그 오브젝트를 배치합니다.
    /// </summary>
    /// <returns>즉시 이동했거나 씬 로딩 요청이 정상 시작되었으면 참입니다.</returns>
    public bool TryTravel(string id, Transform targetPlayer)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            NotifyTravelFailed(null, $"Unknown waypoint id: {id}");
            return false;
        }

        if (!IsMapAvailable(state.Definition.mapDefinition))
        {
            NotifyTravelFailed(state, "This map is locked.");
            return false;
        }

        if (!state.IsActive)
        {
            string message = string.IsNullOrWhiteSpace(state.Definition.lockedMessage)
                ? "This waypoint is locked."
                : state.Definition.lockedMessage;

            NotifyTravelFailed(state, message);
            return false;
        }

        if (IsWayPointInCurrentScene(state.Definition))
        {
            if (state.Stone == null)
            {
                NotifyTravelFailed(state, "Waypoint stone is not registered in the current scene.");
                return false;
            }

            if (targetPlayer == null)
            {
                NotifyTravelFailed(state, "Player target is missing.");
                return false;
            }

            OnWayPointTravelStarted?.Invoke(state);
            MovePlayer(targetPlayer, state.Stone.SpawnPosition);
            RefreshWorldChunks();
            OnWayPointTravelCompleted?.Invoke(state);
            return true;
        }

        string targetSceneName = state.Definition.mapDefinition.sceneName;
        if (LoadingManager.Instance == null)
        {
            NotifyTravelFailed(state, "LoadingManager is missing.");
            return false;
        }

        if (!LoadingManager.Instance.LoadScene(targetSceneName, state.Id))
        {
            NotifyTravelFailed(state, $"Scene loading request failed: {targetSceneName}");
            return false;
        }

        pendingTravelState = state;
        LoadingManager.Instance.LoadingCompleted -= HandleCrossSceneTravelCompleted;
        LoadingManager.Instance.LoadingCompleted += HandleCrossSceneTravelCompleted;
        OnWayPointTravelStarted?.Invoke(state);

        return true;
    }

    // ID로 런타임 상태를 가져온다.
    /// <summary>ID에 해당하는 런타임 상태를 반환하며, 없으면 null을 반환합니다.</summary>
    public WayPointRunTime GetState(string id)
    {
        statesById.TryGetValue(id, out WayPointRunTime state);
        return state;
    }

    // 모든 런타임 상태를 가져온다.
    /// <summary>현재 관리 중인 모든 웨이포인트 런타임 상태를 반환합니다.</summary>
    public IReadOnlyCollection<WayPointRunTime> GetAllStates()
    {
        return statesById.Values;
    }

    // ID로 등록된 Stone을 찾는다.
    /// <summary>현재 씬에 등록된 스톤을 ID로 찾습니다.</summary>
    public bool TryGetStone(string id, out WayPointStone stone)
    {
        return stonesById.TryGetValue(id, out stone);
    }

    // 런타임에 플레이어가 생성되거나 교체되었을 때 이동 대상을 다시 지정한다.
    /// <summary>동일 씬 순간이동에 사용할 현재 플레이어 Transform을 갱신합니다.</summary>
    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    // 인스펙터에 Player가 비어 있으면 Player 태그로 이동 대상을 자동 탐색한다.
    private Transform ResolvePlayer()
    {
        if (player != null)
        {
            return player;
        }

        if (!autoFindPlayerByTag || string.IsNullOrWhiteSpace(playerTag))
        {
            return null;
        }

        GameObject playerObject = null;
        try
        {
            playerObject = GameObject.FindGameObjectWithTag(playerTag);
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[WayPointManager] Player tag is not defined: {playerTag}");
        }

        if (playerObject == null)
        {
            return null;
        }

        player = playerObject.transform;
        return player;
    }

    // 다음 맵의 잠금 상태가 바뀔 수 있으니 UI에 다시 확인하라고 알린다.
    private void NotifyMapAvailabilityChanged(WayPointMapDefinition changedMap)
    {
        foreach (WayPointMapDefinition map in GetAllMaps())
        {
            if (map != null && map.requiredPreviousMap == changedMap)
            {
                OnMapAvailabilityChanged?.Invoke(map);
            }
        }
    }

    // CharacterController가 있으면 잠시 꺼서 순간이동 위치가 밀리지 않도록 한다.
    private void MovePlayer(Transform targetPlayer, Vector3 destination)
    {
        CharacterController controller = targetPlayer.GetComponent<CharacterController>();

        if (controller != null)
        {
            controller.enabled = false;
            targetPlayer.position = destination;
            controller.enabled = true;
            return;
        }

        targetPlayer.position = destination;
    }

    // 순간이동 직후 플레이어 위치 기준으로 청크 활성 상태를 즉시 맞춘다.
    private void RefreshWorldChunks()
    {
        WorldChunkManager chunkManager = FindFirstObjectByType<WorldChunkManager>(FindObjectsInactive.Include);
        if (chunkManager != null)
        {
            chunkManager.RefreshActiveChunks(true);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        player = null;
        RegisterSceneStones();
    }

    private void HandleCrossSceneTravelCompleted()
    {
        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadingCompleted -= HandleCrossSceneTravelCompleted;
        }

        if (pendingTravelState != null)
        {
            OnWayPointTravelCompleted?.Invoke(pendingTravelState);
            pendingTravelState = null;
        }
    }

    // 이동 실패 이벤트와 경고 로그를 한 곳에서 처리한다.
    private void NotifyTravelFailed(WayPointRunTime state, string reason)
    {
        OnWayPointTravelFailed?.Invoke(state, reason);
        Debug.LogWarning($"[WayPointManager] Travel failed. {reason}");
    }

    // 웨이포인트 상태가 언제 바뀌는지 추적하기 위한 디버그 로그를 남긴다.
    private void LogState(string message, UnityEngine.Object context = null, bool includeStackTrace = false)
    {
        if (!logWayPointState)
        {
            return;
        }

        if (includeStackTrace && logUnlockStackTrace)
        {
            Debug.Log($"[WayPointManager] {message}\n{Environment.StackTrace}", context != null ? context : this);
            return;
        }

        Debug.Log($"[WayPointManager] {message}", context != null ? context : this);
    }

    /// 지도 열린 후 해금 테스트 함수
    [ContextMenu("Function: WayPoint Open")]
    public void TestWayPoint()
    {
        UnlockByExternalAction("red_forest");
    }
}
