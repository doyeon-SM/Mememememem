using System;
using System.Collections;
using System.Collections.Generic;
using GH.Loading;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

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

    [Header("Scene Map UI")]
    [Tooltip("활성 씬에서 비활성 오브젝트까지 포함해 자동으로 찾습니다.")]
    [SerializeField] private WayPointMapUI mapUI;
    [SerializeField] private bool hideMapOnSceneLoad = true;

    [Header("Shortcut Map")]
    [SerializeField] private WayPointMapOpenMode shortcutOpenMode = WayPointMapOpenMode.PreviewOnly;
    [SerializeField] private WayPointMapDefinition shortcutMap;
    [Tooltip("등록된 씬에서는 단축키 지도를 웨이포인트 이동 모드로 엽니다.")]
    [SerializeField] private List<string> shortcutTravelSceneNames = new List<string> { "Territory" };

    [Header("Territory Travel")]
    [Tooltip("활성화된 웨이포인트 스톤에서 연 지도의 전용 버튼으로만 진입할 수 있는 영지 씬 이름입니다.")]
    [SerializeField] private string territorySceneName = "Territory";

    [Header("Map Input And Cursor")]
    [SerializeField] private bool notifyInputManager = true;
    [SerializeField] private bool blockKmsPlayerInput = true;
    [Tooltip("일반 씬에서 지도를 열면 커서를 표시하고, 닫으면 다시 숨기고 잠급니다.")]
    [SerializeField] private bool unlockCursorWhileOpen = true;
    [Tooltip("등록된 씬에 진입하면 커서를 항상 표시하고 잠금을 해제합니다. 지도 열기/닫기 후에도 이 상태를 유지합니다.")]
    [SerializeField] private List<string> alwaysVisibleCursorSceneNames = new List<string> { "Territory" };

    [Header("Runtime")]
    [SerializeField] private Transform player;
    [FormerlySerializedAs("autoFindPlayerByTag")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private string playerLayerName = "Player";
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("Debug")]
    [SerializeField] private bool logWayPointState = true;
    [SerializeField] private bool logUnlockStackTrace = true;

    private readonly Dictionary<string, WayPointRunTime> statesById = new Dictionary<string, WayPointRunTime>();
    private readonly Dictionary<string, WayPointStone> stonesById = new Dictionary<string, WayPointStone>();
    private WayPointRunTime pendingTravelState;
    private GameObject targetUI;
    private bool isMapOpen;
    private bool manageCursorForOpenMap;
    private string openedFromWayPointId;
    private bool authorizingTerritoryLoad;
    private int lastShortcutToggleFrame = -1;
    private Coroutine cursorPolicyRefreshCoroutine;

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

    /// <summary>현재 활성 씬의 지도 UI가 매니저를 통해 열려 있는지 나타냅니다.</summary>
    public bool IsMapOpen => isMapOpen;

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
        ResolveSceneMapUI();

        if (hideMapOnSceneLoad)
        {
            SetSceneMapVisible(false);
        }

        RefreshSceneCursorPolicy(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        if (isMapOpen)
        {
            ApplyMapInputState(false, manageCursorForOpenMap);
        }

        if (LoadingManager.Instance != null)
        {
            LoadingManager.Instance.LoadingCompleted -= HandleCrossSceneTravelCompleted;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>현재 씬 정책에 맞는 모드로 단축키 지도를 열거나 닫습니다.</summary>
    public void ToggleShortcutMap()
    {
        // 같은 씬에 이전 Toggle 컴포넌트가 둘 이상 남아 있어도 한 프레임에는 한 번만 처리한다.
        if (lastShortcutToggleFrame == Time.frameCount)
        {
            return;
        }

        lastShortcutToggleFrame = Time.frameCount;

        if (isMapOpen)
        {
            CloseMap();
            return;
        }

        OpenMap(ResolveShortcutOpenMode(), shortcutMap);
    }

    /// <summary>
    /// 탐험 출발 버튼처럼 UI Button의 OnClick에서 호출할 웨이포인트 이동 지도 진입점입니다.
    /// 단축키의 보기 전용 설정과 관계없이 항상 웨이포인트 이동 모드로 지도를 엽니다.
    /// </summary>
    public void OpenTravelMap()
    {
        if (!OpenMap(WayPointMapOpenMode.Travel, shortcutMap))
        {
            Debug.LogWarning("[WayPointManager] Travel map could not be opened because the active scene map UI is missing.", this);
        }
    }

    /// <summary>
    /// 지도 보기 버튼처럼 UI Button의 OnClick에서 호출할 보기 전용 지도 진입점입니다.
    /// 씬의 단축키 설정과 관계없이 웨이포인트 이동이 불가능한 보기 모드로 지도를 엽니다.
    /// </summary>
    public void OpenPreviewMap()
    {
        if (!OpenMap(WayPointMapOpenMode.PreviewOnly, shortcutMap))
        {
            Debug.LogWarning("[WayPointManager] Preview map could not be opened because the active scene map UI is missing.", this);
        }
    }

    /// <summary>보기 전용 지도가 닫혀 있으면 열고, 지도가 열려 있으면 닫습니다.</summary>
    public void TogglePreviewMap()
    {
        if (isMapOpen)
        {
            CloseMap();
            return;
        }

        OpenPreviewMap();
    }

    /// <summary>지정한 모드와 초기 지도로 현재 씬의 지도 UI를 엽니다.</summary>
    public bool OpenMap(WayPointMapOpenMode openMode, WayPointMapDefinition mapOverride = null)
    {
        ResolveSceneMapUI();
        if (mapUI == null || targetUI == null)
        {
            return false;
        }

        // 단축키나 일반 지도 버튼으로 연 경우에는 스톤 출발 권한을 남기지 않는다.
        openedFromWayPointId = string.Empty;
        manageCursorForOpenMap = !IsAlwaysVisibleCursorScene();
        isMapOpen = true;
        SetSceneMapVisible(true);
        mapUI.PrepareOpen(openMode, mapOverride);
        ApplyMapInputState(true, manageCursorForOpenMap);
        return true;
    }

    /// <summary>웨이포인트 스톤 상호작용으로 이동 모드 지도를 엽니다.</summary>
    public bool OpenMapFromStone(WayPointDefinition sourceWayPoint)
    {
        WayPointMapDefinition targetMap = sourceWayPoint != null ? sourceWayPoint.mapDefinition : null;
        bool opened = OpenMap(WayPointMapOpenMode.Travel, targetMap);
        if (opened && mapUI != null)
        {
            openedFromWayPointId = sourceWayPoint != null ? sourceWayPoint.id : string.Empty;
            mapUI.SetOpenedFromWayPoint(sourceWayPoint);
        }

        return opened;
    }

    /// <summary>현재 지도 UI를 닫고 일반 씬에서 플레이 커서를 다시 숨기고 잠급니다.</summary>
    public void CloseMap()
    {
        if (mapUI != null)
        {
            mapUI.PrepareClose();
        }

        SetSceneMapVisible(false);
        bool wasOpen = isMapOpen;
        bool shouldManageCursor = manageCursorForOpenMap;
        isMapOpen = false;
        manageCursorForOpenMap = false;
        openedFromWayPointId = string.Empty;

        if (wasOpen)
        {
            ApplyMapInputState(false, shouldManageCursor);
        }

        // 상시 커서 씬은 표시 상태를 유지하고, 그 외 씬은 닫기 요청과 함께
        // PlayerInput/카메라 내부 상태까지 잠금 상태로 다시 맞춘다.
        RefreshSceneCursorPolicy(SceneManager.GetActiveScene().name);
    }

    private WayPointMapOpenMode ResolveShortcutOpenMode()
    {
        if (shortcutOpenMode == WayPointMapOpenMode.Travel)
        {
            return WayPointMapOpenMode.Travel;
        }

        return IsShortcutTravelScene()
            ? WayPointMapOpenMode.Travel
            : shortcutOpenMode;
    }

    private bool IsShortcutTravelScene()
    {
        return ContainsSceneName(shortcutTravelSceneNames, SceneManager.GetActiveScene().name);
    }

    private bool IsAlwaysVisibleCursorScene()
    {
        return ContainsSceneName(alwaysVisibleCursorSceneNames, SceneManager.GetActiveScene().name);
    }

    private static bool ContainsSceneName(List<string> sceneNames, string targetSceneName)
    {
        if (sceneNames == null)
        {
            return false;
        }

        foreach (string sceneName in sceneNames)
        {
            if (!string.IsNullOrWhiteSpace(sceneName)
                && string.Equals(sceneName.Trim(), targetSceneName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ResolveSceneMapUI()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (mapUI != null && mapUI.gameObject.scene == activeScene)
        {
            targetUI = mapUI.VisibilityTarget;
            return;
        }

        mapUI = null;
        targetUI = null;

        if (WayPointMapUI.Instance != null && WayPointMapUI.Instance.gameObject.scene == activeScene)
        {
            mapUI = WayPointMapUI.Instance;
        }
        else
        {
            WayPointMapUI[] sceneMapUIs = FindObjectsByType<WayPointMapUI>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (WayPointMapUI candidate in sceneMapUIs)
            {
                if (candidate != null && candidate.gameObject.scene == activeScene)
                {
                    mapUI = candidate;
                    break;
                }
            }
        }

        if (mapUI != null)
        {
            targetUI = mapUI.VisibilityTarget;
        }
    }

    private void SetSceneMapVisible(bool visible)
    {
        if (targetUI != null)
        {
            targetUI.SetActive(visible);
        }
    }

    private void ApplyMapInputState(bool open, bool manageCursor)
    {
        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(open);
        }

        SetKmsPlayerInputBlocked(open);

        // 영지처럼 상시 커서를 사용하는 이동 허용 씬에서는 커서 상태를 절대 변경하지 않는다.
        if (!unlockCursorWhileOpen || !manageCursor)
        {
            return;
        }

        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void RefreshSceneCursorPolicy(string sceneName)
    {
        ApplySceneCursorPolicy(sceneName);

        if (cursorPolicyRefreshCoroutine != null)
        {
            StopCoroutine(cursorPolicyRefreshCoroutine);
        }

        cursorPolicyRefreshCoroutine = StartCoroutine(ReapplySceneCursorPolicyNextFrame(sceneName));
    }

    private IEnumerator ReapplySceneCursorPolicyNextFrame(string sceneName)
    {
        yield return null;

        if (string.Equals(SceneManager.GetActiveScene().name, sceneName, StringComparison.Ordinal))
        {
            ApplySceneCursorPolicy(sceneName);
        }

        cursorPolicyRefreshCoroutine = null;
    }

    private void ApplySceneCursorPolicy(string sceneName)
    {
        bool keepCursorVisible = ContainsSceneName(alwaysVisibleCursorSceneNames, sceneName);
        bool showCursorForOpenMap = isMapOpen && unlockCursorWhileOpen && manageCursorForOpenMap;
        bool releaseCursor = keepCursorVisible || showCursorForOpenMap;

        KMS.PlayerInput[] playerInputs = PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerInput>(
            playerTag,
            playerLayerName);

        foreach (KMS.PlayerInput playerInputComponent in playerInputs)
        {
            if (playerInputComponent != null)
            {
                playerInputComponent.SetCursorReleased(releaseCursor);
            }
        }

        KMS.PlayerCameraController[] cameraControllers =
            PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerCameraController>(
                playerTag,
                playerLayerName);

        foreach (KMS.PlayerCameraController cameraController in cameraControllers)
        {
            if (cameraController != null)
            {
                cameraController.SetCursorLocked(!releaseCursor);
            }
        }

        Cursor.lockState = releaseCursor ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = releaseCursor;
    }

    private void SetKmsPlayerInputBlocked(bool blocked)
    {
        if (!blockKmsPlayerInput)
        {
            return;
        }

        KMS.PlayerInput[] playerInputs = PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerInput>(
            playerTag,
            playerLayerName);

        foreach (KMS.PlayerInput playerInputComponent in playerInputs)
        {
            if (playerInputComponent != null)
            {
                playerInputComponent.SetGameplayInputBlocked(blocked);
            }
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

    /// <summary>현재까지 하나 이상의 웨이포인트가 활성화되었는지 확인합니다.</summary>
    public bool HasAnyActiveWayPoint()
    {
        foreach (WayPointRunTime state in statesById.Values)
        {
            if (state != null && state.IsActive)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>지정한 씬 이름이 영지 씬인지 확인합니다.</summary>
    public bool IsTerritorySceneName(string sceneName)
    {
        return !string.IsNullOrWhiteSpace(sceneName)
            && !string.IsNullOrWhiteSpace(territorySceneName)
            && string.Equals(sceneName.Trim(), territorySceneName.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 현재 지도가 실제 스톤에서 열렸고 그 스톤이 활성 상태일 때만 영지 이동을 허용합니다.
    /// 일반 지도 열기, 잠긴 스톤, 활성 웨이포인트가 하나도 없는 상태에서는 거짓입니다.
    /// </summary>
    public bool CanTravelToTerritoryFromOpenedStone()
    {
        if (!isMapOpen
            || IsTerritorySceneName(SceneManager.GetActiveScene().name)
            || string.IsNullOrWhiteSpace(openedFromWayPointId)
            || !HasAnyActiveWayPoint()
            || LoadingManager.Instance == null
            || LoadingManager.Instance.IsLoading)
        {
            return false;
        }

        return statesById.TryGetValue(openedFromWayPointId, out WayPointRunTime sourceState)
            && sourceState != null
            && sourceState.IsActive
            && sourceState.Stone != null
            && sourceState.Stone.IsUnlocked;
    }

    /// <summary>활성 웨이포인트 스톤에서 연 지도의 영지 이동 버튼 요청을 처리합니다.</summary>
    public bool TryTravelToTerritory()
    {
        statesById.TryGetValue(openedFromWayPointId, out WayPointRunTime sourceState);

        if (!HasAnyActiveWayPoint())
        {
            NotifyTravelFailed(sourceState, "활성화된 웨이포인트가 없어 영지로 이동할 수 없습니다.");
            return false;
        }

        if (!CanTravelToTerritoryFromOpenedStone())
        {
            NotifyTravelFailed(sourceState, "영지는 활성화된 웨이포인트 스톤에서 연 지도를 통해서만 이동할 수 있습니다.");
            return false;
        }

        authorizingTerritoryLoad = true;
        bool started;
        try
        {
            started = LoadingManager.Instance.LoadScene(territorySceneName.Trim(), string.Empty);
        }
        finally
        {
            authorizingTerritoryLoad = false;
        }

        if (!started)
        {
            NotifyTravelFailed(sourceState, $"영지 씬 로딩 요청에 실패했습니다: {territorySceneName}");
            return false;
        }

        OnWayPointTravelStarted?.Invoke(sourceState);
        CloseMap();
        return true;
    }

    /// <summary>
    /// LoadingManager가 영지 씬 직접 로딩 요청을 받았을 때 전용 스톤 이동 흐름에서 시작된 요청인지 검증합니다.
    /// 영지 외 씬은 기존 로딩 흐름을 그대로 허용합니다.
    /// </summary>
    public bool IsSceneLoadAuthorized(string sceneName)
    {
        return !IsTerritorySceneName(sceneName) || authorizingTerritoryLoad;
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
        if (IsTerritorySceneName(targetSceneName))
        {
            NotifyTravelFailed(state, "지도 웨이포인트 아이콘으로는 영지로 이동할 수 없습니다.");
            return false;
        }

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
        RefreshSceneCursorPolicy(SceneManager.GetActiveScene().name);
    }

    // 인스펙터에 Player가 비어 있으면 Player 태그를 우선하고, 없으면 Player 레이어로 찾는다.
    private Transform ResolvePlayer()
    {
        if (player != null)
        {
            return player;
        }

        if (!autoFindPlayer)
        {
            return null;
        }

        player = PlayerReferenceResolver.ResolveTransform(player, playerTag, playerLayerName);
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
        CharacterController controller = PlayerReferenceResolver
            .FindComponentInPlayerHierarchy<CharacterController>(
                targetPlayer.gameObject,
                playerTag,
                playerLayerName);

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
            chunkManager.SetPlayer(ResolvePlayer());
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isMapOpen)
        {
            ApplyMapInputState(false, manageCursorForOpenMap);
        }

        isMapOpen = false;
        manageCursorForOpenMap = false;
        openedFromWayPointId = string.Empty;
        mapUI = null;
        targetUI = null;
        player = null;
        RegisterSceneStones();
        ResolveSceneMapUI();

        if (hideMapOnSceneLoad)
        {
            SetSceneMapVisible(false);
        }

        RefreshSceneCursorPolicy(scene.name);
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
