using System.Collections.Generic;
using KMS.InventoryDuped;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 현재 씬의 ESC UI 동작을 관리합니다.
/// 씬 사이에서 유지되지 않지만 Instance를 통해 다른 스크립트에서 직접 참조 없이 접근할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class SceneUIManager : MonoBehaviour
{
    public static SceneUIManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("ESC로 열고 닫을 설정 UI의 루트 오브젝트입니다.")]
    [SerializeField] private GameObject settingsUI;

    [Tooltip("ESC로 닫을 UI 루트 오브젝트들입니다. 설정 UI는 넣지 않습니다.")]
    [SerializeField] private List<GameObject> managedUIObjects = new List<GameObject>();

    [Header("Closed Cursor Fallback")]
    [Tooltip("플레이 중 정상 커서 상태를 아직 기억하지 못했을 때 사용할 잠금 상태입니다.")]
    [SerializeField] private CursorLockMode fallbackClosedCursorLockMode = CursorLockMode.Locked;

    [Tooltip("플레이 중 정상 커서 상태를 아직 기억하지 못했을 때 사용할 표시 상태입니다.")]
    [SerializeField] private bool fallbackClosedCursorVisible;

    [Header("Player Input")]
    [Tooltip("설정 UI가 열려 있는 동안 KMS InputManager의 시스템 메뉴 상태도 함께 변경합니다.")]
    [SerializeField] private bool notifyInputManager = true;

    [Tooltip("KMS 플레이어를 찾을 때 사용할 태그입니다.")]
    [SerializeField] private string playerTag = PlayerReferenceResolver.DefaultPlayerTag;

    [Tooltip("KMS 플레이어를 찾을 때 사용할 레이어입니다.")]
    [SerializeField] private string playerLayerName = PlayerReferenceResolver.DefaultPlayerLayerName;

    private float timeScaleBeforeSettings = 1f;
    private CursorLockMode cursorLockModeBeforeSettings;
    private bool cursorVisibleBeforeSettings;
    private bool systemMenuWasOpenBeforeSettings;
    private bool settingsStateApplied;

    private CursorLockMode normalCursorLockMode;
    private bool normalCursorVisible;
    private bool hasNormalCursorState;

    private readonly List<KmsPlayerInputState> kmsPlayerInputStates =
        new List<KmsPlayerInputState>();
    private readonly List<KmsPlayerInputState> normalKmsPlayerInputStates =
        new List<KmsPlayerInputState>();
    private readonly List<KMS.PlayerCameraController> kmsCameraControllers =
        new List<KMS.PlayerCameraController>();

    private sealed class KmsPlayerInputState
    {
        public KMS.PlayerInput Input;
        public bool WasGameplayInputBlocked;
        public bool WasCursorReleased;
    }

    public bool IsSettingsOpen => IsOpen(settingsUI);
    public bool HasOpenManagedUI => FindOpenManagedUI() != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SceneUIManager] 씬에는 하나의 SceneUIManager만 둘 수 있습니다.", this);
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (settingsUI != null)
        {
            settingsUI.SetActive(false);
        }

        CaptureNormalCursorState();
    }

    private void Update()
    {
        SynchronizeSettingsState();

        if (WasEscapePressedThisFrame())
        {
            HandleEscape();
        }

        if (!IsSettingsOpen && !HasOpenManagedUI)
        {
            CaptureNormalCursorState();
        }
    }

    private void OnDisable()
    {
        if (settingsStateApplied)
        {
            RestoreSettingsState();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        if (settingsStateApplied)
        {
            RestoreSettingsState();
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// ESC 우선순위를 실행합니다.
    /// 설정 UI 닫기, 열린 일반 UI 모두 닫기, 설정 UI 열기 순서입니다.
    /// </summary>
    public void HandleEscape()
    {
        if (IsSettingsOpen)
        {
            CloseSettingsUI();
            return;
        }

        if (CloseManagedUIObjects())
        {
            return;
        }

        OpenSettingsUI();
    }

    /// <summary>설정 UI를 열고 게임 시간 및 플레이어 입력을 멈춥니다.</summary>
    public void OpenSettingsUI()
    {
        if (settingsUI == null)
        {
            Debug.LogWarning("[SceneUIManager] Settings UI가 지정되지 않았습니다.", this);
            return;
        }

        if (IsSettingsOpen)
        {
            ApplySettingsOpenState();
            return;
        }

        CloseManagedUIObjects();

        CacheSettingsState();
        settingsStateApplied = true;
        settingsUI.SetActive(true);

        if (!IsSettingsOpen)
        {
            settingsStateApplied = false;
            Debug.LogWarning(
                "[SceneUIManager] Settings UI의 상위 오브젝트가 비활성 상태라 UI를 열 수 없습니다.",
                settingsUI);
            return;
        }

        ApplySettingsOpenState();
    }

    /// <summary>설정 UI를 닫고 열기 전의 시간, 커서, 입력 상태를 복구합니다.</summary>
    public void CloseSettingsUI()
    {
        if (settingsUI != null)
        {
            settingsUI.SetActive(false);
        }

        RestoreSettingsState();
    }

    /// <summary>
    /// 등록된 UI 중 현재 열려 있는 오브젝트를 모두 닫습니다.
    /// 하나 이상 닫혔으면 true를 반환합니다.
    /// </summary>
    public bool CloseManagedUIObjects()
    {
        bool closedAny = false;

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (target == null || target == settingsUI || !IsOpen(target))
            {
                continue;
            }

            closedAny = true;

            if (!TryCloseKmsInventory(target) && !TryCloseWayPointMap(target))
            {
                target.SetActive(false);
            }
        }

        if (!closedAny)
        {
            return false;
        }

        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(false);
        }

        RestoreNormalKmsPlayerState();
        RestoreNormalCursorState();
        return true;
    }

    /// <summary>해당 오브젝트가 ESC 닫기 목록에 등록되어 있는지 확인합니다.</summary>
    public bool IsManagedUI(GameObject target)
    {
        return target != null && managedUIObjects.Contains(target);
    }

    /// <summary>씬의 SceneUIManager를 직접 참조하지 않고 설정 UI 열기를 요청합니다.</summary>
    public static bool TryOpenSettings()
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.OpenSettingsUI();
        return Instance.IsSettingsOpen;
    }

    /// <summary>씬의 SceneUIManager를 직접 참조하지 않고 설정 UI 닫기를 요청합니다.</summary>
    public static bool TryCloseSettings()
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.CloseSettingsUI();
        return true;
    }

    private void ApplySettingsOpenState()
    {
        if (!settingsStateApplied)
        {
            CacheSettingsState();
            settingsStateApplied = true;
        }

        Time.timeScale = 0f;
        ApplyKmsPlayerSettingsState();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(true);
        }
    }

    private void CacheSettingsState()
    {
        timeScaleBeforeSettings = Time.timeScale;
        cursorLockModeBeforeSettings = Cursor.lockState;
        cursorVisibleBeforeSettings = Cursor.visible;
        systemMenuWasOpenBeforeSettings =
            InputManager.Instance != null && InputManager.Instance.IsSystemMenuOpen;
    }

    private void ApplyKmsPlayerSettingsState()
    {
        KMS.PlayerInput[] playerInputs =
            PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerInput>(
                playerTag,
                playerLayerName);

        for (int i = 0; i < playerInputs.Length; i++)
        {
            KMS.PlayerInput playerInput = playerInputs[i];
            if (playerInput == null)
            {
                continue;
            }

            if (!ContainsKmsPlayerInput(playerInput))
            {
                kmsPlayerInputStates.Add(new KmsPlayerInputState
                {
                    Input = playerInput,
                    WasGameplayInputBlocked = playerInput.IsGameplayInputBlocked,
                    WasCursorReleased = playerInput.IsCursorReleased
                });
            }

            playerInput.SetGameplayInputBlocked(true);
            playerInput.SetCursorReleased(true);
        }

        KMS.PlayerCameraController[] cameraControllers =
            PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerCameraController>(
                playerTag,
                playerLayerName);

        for (int i = 0; i < cameraControllers.Length; i++)
        {
            KMS.PlayerCameraController cameraController = cameraControllers[i];
            if (cameraController == null)
            {
                continue;
            }

            if (!kmsCameraControllers.Contains(cameraController))
            {
                kmsCameraControllers.Add(cameraController);
            }

            cameraController.SetCursorLocked(false);
        }
    }

    private void RestoreKmsPlayerState()
    {
        for (int i = 0; i < kmsPlayerInputStates.Count; i++)
        {
            KmsPlayerInputState state = kmsPlayerInputStates[i];
            if (state.Input == null)
            {
                continue;
            }

            state.Input.SetCursorReleased(state.WasCursorReleased);
            state.Input.SetGameplayInputBlocked(state.WasGameplayInputBlocked);
        }

        bool shouldLockCursor = cursorLockModeBeforeSettings == CursorLockMode.Locked;
        for (int i = 0; i < kmsCameraControllers.Count; i++)
        {
            KMS.PlayerCameraController cameraController = kmsCameraControllers[i];
            if (cameraController != null)
            {
                cameraController.SetCursorLocked(shouldLockCursor);
            }
        }

        kmsPlayerInputStates.Clear();
        kmsCameraControllers.Clear();
    }

    private bool ContainsKmsPlayerInput(KMS.PlayerInput target)
    {
        for (int i = 0; i < kmsPlayerInputStates.Count; i++)
        {
            if (kmsPlayerInputStates[i].Input == target)
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureNormalKmsPlayerState()
    {
        for (int i = 0; i < normalKmsPlayerInputStates.Count; i++)
        {
            if (normalKmsPlayerInputStates[i].Input != null)
            {
                return;
            }
        }

        normalKmsPlayerInputStates.Clear();

        KMS.PlayerInput[] playerInputs =
            PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerInput>(
                playerTag,
                playerLayerName);

        for (int i = 0; i < playerInputs.Length; i++)
        {
            KMS.PlayerInput playerInput = playerInputs[i];
            if (playerInput == null || ContainsPlayerInput(normalKmsPlayerInputStates, playerInput))
            {
                continue;
            }

            normalKmsPlayerInputStates.Add(new KmsPlayerInputState
            {
                Input = playerInput,
                WasGameplayInputBlocked = playerInput.IsGameplayInputBlocked,
                WasCursorReleased = playerInput.IsCursorReleased
            });
        }
    }

    private void RestoreNormalKmsPlayerState()
    {
        for (int i = 0; i < normalKmsPlayerInputStates.Count; i++)
        {
            KmsPlayerInputState state = normalKmsPlayerInputStates[i];
            if (state.Input == null)
            {
                continue;
            }

            state.Input.SetCursorReleased(state.WasCursorReleased);
            state.Input.SetGameplayInputBlocked(state.WasGameplayInputBlocked);
        }

        bool shouldLockCursor = hasNormalCursorState
            ? normalCursorLockMode == CursorLockMode.Locked
            : fallbackClosedCursorLockMode == CursorLockMode.Locked;

        KMS.PlayerCameraController[] cameraControllers =
            PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerCameraController>(
                playerTag,
                playerLayerName);

        for (int i = 0; i < cameraControllers.Length; i++)
        {
            if (cameraControllers[i] != null)
            {
                cameraControllers[i].SetCursorLocked(shouldLockCursor);
            }
        }
    }

    private static bool ContainsPlayerInput(
        List<KmsPlayerInputState> states,
        KMS.PlayerInput target)
    {
        for (int i = 0; i < states.Count; i++)
        {
            if (states[i].Input == target)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreSettingsState()
    {
        if (!settingsStateApplied)
        {
            return;
        }

        Time.timeScale = timeScaleBeforeSettings;
        RestoreKmsPlayerState();
        Cursor.lockState = cursorLockModeBeforeSettings;
        Cursor.visible = cursorVisibleBeforeSettings;

        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(systemMenuWasOpenBeforeSettings);
        }

        settingsStateApplied = false;
    }

    private void SynchronizeSettingsState()
    {
        if (IsSettingsOpen)
        {
            ApplySettingsOpenState();
        }
        else if (settingsStateApplied)
        {
            RestoreSettingsState();
        }
    }

    private GameObject FindOpenManagedUI()
    {
        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (target != null && target != settingsUI && IsOpen(target))
            {
                return target;
            }
        }

        return null;
    }

    private static bool TryCloseKmsInventory(GameObject target)
    {
        InventoryUI[] inventoryUIs = FindObjectsByType<InventoryUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < inventoryUIs.Length; i++)
        {
            InventoryUI inventoryUI = inventoryUIs[i];
            GameObject inventoryPanel = inventoryUI != null
                ? inventoryUI.inventoryPanel
                : null;

            if (inventoryPanel == null || !AreInSameHierarchy(target, inventoryPanel))
            {
                continue;
            }

            inventoryUI.Close();
            return true;
        }

        return false;
    }

    private bool TryCloseWayPointMap(GameObject target)
    {
        WayPointManager wayPointManager = WayPointManager.Instance;
        WayPointMapUI mapUI = WayPointMapUI.Instance;

        if (wayPointManager == null || mapUI == null || !wayPointManager.IsMapOpen)
        {
            return false;
        }

        GameObject mapRoot = mapUI.VisibilityTarget;
        if (mapRoot == null || !AreInSameHierarchy(target, mapRoot))
        {
            return false;
        }

        wayPointManager.CloseMap();
        return true;
    }

    private static bool AreInSameHierarchy(GameObject first, GameObject second)
    {
        return first == second
            || first.transform.IsChildOf(second.transform)
            || second.transform.IsChildOf(first.transform);
    }

    private void CaptureNormalCursorState()
    {
        normalCursorLockMode = Cursor.lockState;
        normalCursorVisible = Cursor.visible;
        hasNormalCursorState = true;
        CaptureNormalKmsPlayerState();
    }

    private void RestoreNormalCursorState()
    {
        Cursor.lockState = hasNormalCursorState
            ? normalCursorLockMode
            : fallbackClosedCursorLockMode;
        Cursor.visible = hasNormalCursorState
            ? normalCursorVisible
            : fallbackClosedCursorVisible;
    }

    private static bool IsOpen(GameObject target)
    {
        return target != null && target.activeInHierarchy;
    }

    private static bool WasEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        for (int i = managedUIObjects.Count - 1; i >= 0; i--)
        {
            if (managedUIObjects[i] == null)
            {
                continue;
            }

            for (int duplicateIndex = 0; duplicateIndex < i; duplicateIndex++)
            {
                if (managedUIObjects[duplicateIndex] == managedUIObjects[i])
                {
                    managedUIObjects.RemoveAt(i);
                    break;
                }
            }
        }
    }
#endif
}
