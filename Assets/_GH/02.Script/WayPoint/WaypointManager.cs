using System;
using System.Collections.Generic;
using UnityEngine;

public class WayPointManager : MonoBehaviour
{
    public static WayPointManager Instance { get; private set; }

    [Header("WayPoint Data")]
    [SerializeField] private List<WayPointDefinition> definitions = new List<WayPointDefinition>();

    [Header("Runtime")]
    [SerializeField] private Transform player;

    private readonly Dictionary<string, WayPointRunTime> statesById = new Dictionary<string, WayPointRunTime>();
    private readonly Dictionary<string, WayPointStone> stonesById = new Dictionary<string, WayPointStone>();

    public event Action<WayPointRunTime> OnWayPointUnlocked;
    public event Action<WayPointRunTime> OnWayPointStateChanged;
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        RegisterSceneStones();
    }

    private void RegisterSceneStones()
    {
        WayPointStone[] stones = FindObjectsByType<WayPointStone>(FindObjectsSortMode.None);

        foreach (WayPointStone stone in stones)
        {
            RegisterStone(stone);
        }
    }

    // Build runtime states from the ScriptableObject list assigned in the Inspector.
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
            statesById.Add(definition.id, state);
        }
    }

    // Register the scene Stone that acts as this waypoint's teleport destination.
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

    // Remove a Stone when it leaves the scene or is disabled.
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

    public bool IsUnlocked(string id)
    {
        return statesById.TryGetValue(id, out WayPointRunTime state) && state.IsActive;
    }

    // Called by WayPointObject after player interaction to unlock that waypoint id.
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

        return true;
    }

    // Called by map UI when the player clicks an active waypoint icon.
    public bool TryTravel(string id)
    {
        return TryTravel(id, player);
    }

    // Move the player to the registered Stone spawn position when the waypoint is unlocked.
    public bool TryTravel(string id, Transform targetPlayer)
    {
        if (!statesById.TryGetValue(id, out WayPointRunTime state))
        {
            NotifyTravelFailed(null, $"Unknown waypoint id: {id}");
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

    public WayPointRunTime GetState(string id)
    {
        statesById.TryGetValue(id, out WayPointRunTime state);
        return state;
    }

    public IReadOnlyCollection<WayPointRunTime> GetAllStates()
    {
        return statesById.Values;
    }

    public bool TryGetStone(string id, out WayPointStone stone)
    {
        return stonesById.TryGetValue(id, out stone);
    }

    // Reassign the player transform when the player is spawned or replaced at runtime.
    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
    }

    // Temporarily disable CharacterController so teleporting does not get corrected away.
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

    private void NotifyTravelFailed(WayPointRunTime state, string reason)
    {
        OnWayPointTravelFailed?.Invoke(state, reason);
        Debug.LogWarning($"[WayPointManager] Travel failed. {reason}");
    }
}
