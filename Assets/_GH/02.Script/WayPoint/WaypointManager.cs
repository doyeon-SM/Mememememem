using System;
using System.Collections.Generic;
using UnityEngine;

public class WayPointManager : MonoBehaviour
{
    public static WayPointManager Instance { get; private set; }

    [Header("WayPoint Data")]
    [SerializeField] private List<WayPointMapDefinition> mapDefinitions = new List<WayPointMapDefinition>();
    [SerializeField] private List<WayPointDefinition> definitions = new List<WayPointDefinition>();

    [Header("Runtime")]
    [SerializeField] private Transform player;
    [SerializeField] private bool autoFindPlayerByTag = true;
    [SerializeField] private string playerTag = "Player";

    private readonly Dictionary<string, WayPointRunTime> statesById = new Dictionary<string, WayPointRunTime>();
    private readonly Dictionary<string, WayPointStone> stonesById = new Dictionary<string, WayPointStone>();

    public event Action<WayPointRunTime> OnWayPointUnlocked;
    public event Action<WayPointRunTime> OnWayPointStateChanged;
    public event Action<WayPointMapDefinition> OnMapAvailabilityChanged;
    public event Action<WayPointRunTime> OnWayPointTravelStarted;
    public event Action<WayPointRunTime> OnWayPointTravelCompleted;
    public event Action<WayPointRunTime, string> OnWayPointTravelFailed;

    public IReadOnlyDictionary<string, WayPointRunTime> StatesById => statesById;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeStates();
    }

    private void Start()
    {
        RegisterSceneStones();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 씬에 이미 배치된 WayPointStone을 한 번 더 등록해서 실행 순서 문제를 줄인다.
    private void RegisterSceneStones()
    {
        WayPointStone[] stones = FindObjectsByType<WayPointStone>(FindObjectsSortMode.None);
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
            state.IsActive = definition.unlockedOnStart;
            statesById.Add(definition.id, state);
        }
    }

    // 웨이포인트 이동 목적지인 Stone을 매니저에 등록한다.
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

        stone.SetUnlockedVisual(state.IsActive);
        OnWayPointStateChanged?.Invoke(state);
    }

    // Stone이 비활성화되거나 제거될 때 등록을 해제한다.
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
    public bool IsUnlocked(string id)
    {
        return statesById.TryGetValue(id, out WayPointRunTime state) && state.IsActive;
    }

    // 플레이어가 등록 오브젝트와 상호작용했을 때 웨이포인트를 해금한다.
    public bool Unlock(string id)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            Debug.LogWarning($"[WayPointManager] Cannot unlock unknown waypoint id: {id}");
            return false;
        }

        if (state.IsActive)
        {
            return false;
        }

        state.IsActive = true;

        if (state.Stone != null)
        {
            state.Stone.SetUnlockedVisual(true);
        }

        OnWayPointUnlocked?.Invoke(state);
        OnWayPointStateChanged?.Invoke(state);
        NotifyMapAvailabilityChanged(state.Definition.mapDefinition);

        return true;
    }

    // 특정 맵이 현재 열려 있는지 확인한다.
    public bool IsMapAvailable(WayPointMapDefinition mapDefinition)
    {
        if (mapDefinition == null)
        {
            return true;
        }

        if (mapDefinition.unlockedOnStart)
        {
            return true;
        }

        if (mapDefinition.requiredPreviousMap == null)
        {
            return false;
        }

        return AreAllWayPointsUnlockedInMap(mapDefinition.requiredPreviousMap);
    }

    // 특정 맵에 포함된 모든 웨이포인트가 해금되었는지 확인한다.
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
    public bool TryTravel(string id)
    {
        return TryTravel(id, ResolvePlayer());
    }

    // 해금된 웨이포인트라면 등록된 Stone의 SpawnPosition으로 플레이어를 이동시킨다.
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

        if (state.Stone == null)
        {
            NotifyTravelFailed(state, "Waypoint stone is not registered.");
            return false;
        }

        if (targetPlayer == null)
        {
            NotifyTravelFailed(state, "Player target is missing.");
            return false;
        }

        OnWayPointTravelStarted?.Invoke(state);
        MovePlayer(targetPlayer, state.Stone.SpawnPosition);
        OnWayPointTravelCompleted?.Invoke(state);

        return true;
    }

    // ID로 런타임 상태를 가져온다.
    public WayPointRunTime GetState(string id)
    {
        statesById.TryGetValue(id, out WayPointRunTime state);
        return state;
    }

    // 모든 런타임 상태를 가져온다.
    public IReadOnlyCollection<WayPointRunTime> GetAllStates()
    {
        return statesById.Values;
    }

    // ID로 등록된 Stone을 찾는다.
    public bool TryGetStone(string id, out WayPointStone stone)
    {
        return stonesById.TryGetValue(id, out stone);
    }

    // 런타임에 플레이어가 생성되거나 교체되었을 때 이동 대상을 다시 지정한다.
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

    // 이동 실패 이벤트와 경고 로그를 한 곳에서 처리한다.
    private void NotifyTravelFailed(WayPointRunTime state, string reason)
    {
        OnWayPointTravelFailed?.Invoke(state, reason);
        Debug.LogWarning($"[WayPointManager] Travel failed. {reason}");
    }
}
