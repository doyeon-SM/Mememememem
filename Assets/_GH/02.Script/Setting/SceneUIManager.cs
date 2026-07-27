using System.Collections.Generic;
using KMS.InventoryDuped;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// 현재 씬의 설정 UI와 등록된 일반 패널 UI를 관리합니다.
/// 씬 사이에서 유지되지 않지만 Instance를 통해 직접 참조 없이 접근할 수 있습니다.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class SceneUIManager : MonoBehaviour
{
    public static SceneUIManager Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("ESC로 열고 닫을 설정 UI의 루트 오브젝트입니다.")]
    [SerializeField] private GameObject settingsUI;

    [Tooltip("SceneUIManager가 활성 상태, 입력, 커서를 관리할 UI 루트들입니다.")]
    [SerializeField] private List<GameObject> managedUIObjects = new List<GameObject>();

    [Tooltip("Managed UI Objects와 같은 순서로 대응하는 고유 ID입니다.")]
    [SerializeField] private List<string> managedUIIds = new List<string>();

    [Tooltip("체크하면 여러 Managed UI를 동시에 열 수 있습니다. 체크를 해제하면 마지막으로 열린 UI만 유지합니다.")]
    [SerializeField] private bool allowMultipleManagedUIs = true;

    [Header("Closed Cursor Fallback")]
    [Tooltip("정상 커서 상태를 아직 기억하지 못했을 때 사용할 잠금 상태입니다.")]
    [SerializeField] private CursorLockMode fallbackClosedCursorLockMode = CursorLockMode.Locked;

    [Tooltip("정상 커서 상태를 아직 기억하지 못했을 때 사용할 표시 상태입니다.")]
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
    private bool managedUIStateApplied;

    private CursorLockMode normalCursorLockMode;
    private bool normalCursorVisible;
    private bool hasNormalCursorState;

    private GameObject lastRequestedManagedUI;
    private readonly Dictionary<string, GameObject> managedUIById =
        new Dictionary<string, GameObject>(System.StringComparer.OrdinalIgnoreCase);
    private readonly List<bool> previousManagedOpenStates = new List<bool>();
    private readonly List<KmsPlayerInputState> settingsKmsPlayerInputStates =
        new List<KmsPlayerInputState>();
    private readonly List<KmsPlayerInputState> normalKmsPlayerInputStates =
        new List<KmsPlayerInputState>();
    private readonly List<KMS.PlayerCameraController> settingsKmsCameraControllers =
        new List<KMS.PlayerCameraController>();

    private sealed class KmsPlayerInputState
    {
        public KMS.PlayerInput Input;
        public bool WasGameplayInputBlocked;
        public bool WasCursorReleased;
    }

    public bool IsSettingsOpen => IsOpen(settingsUI);
    public bool HasOpenManagedUI => FindOpenManagedUI() != null;
    public bool AllowMultipleManagedUIs => allowMultipleManagedUIs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SceneUIManager] 씬에는 하나의 SceneUIManager만 둘 수 있습니다.", this);
            Destroy(this);
            return;
        }

        Instance = this;
        RebuildManagedUILookup();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        RebuildManagedUILookup();
    }

    private void Start()
    {
        if (settingsUI != null)
        {
            settingsUI.SetActive(false);
        }

        CaptureNormalState();
        EnforceExclusiveManagedUIs();
        SynchronizeManagedUIState();
        UpdateManagedOpenSnapshot();
    }

    private void Update()
    {
        if (IsSettingsOpen)
        {
            if (HasOpenManagedUI)
            {
                CloseManagedUIObjects();
            }

            ApplySettingsOpenState();
        }
        else
        {
            if (settingsStateApplied)
            {
                RestoreSettingsState();
            }

            EnforceExclusiveManagedUIs();
            SynchronizeManagedUIState();
        }

        if (WasEscapePressedThisFrame())
        {
            HandleEscape();
        }

        UpdateManagedOpenSnapshot();

        if (!IsSettingsOpen && !HasOpenManagedUI)
        {
            CaptureNormalState();
        }
    }

    private void OnDisable()
    {
        RestoreRuntimeState();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDestroy()
    {
        RestoreRuntimeState();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// ESC 우선순위를 실행합니다.
    /// 설정 UI 닫기, 열린 Managed UI 모두 닫기, 설정 UI 열기 순서입니다.
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
    /// 등록된 UI를 엽니다. 동시 열림이 비활성화되어 있으면 다른 등록 UI를 먼저 닫습니다.
    /// Unity Button의 OnClick에서도 GameObject 인자를 지정해 호출할 수 있습니다.
    /// </summary>
    public void OpenManagedUI(GameObject target)
    {
        if (!TryResolveManagedUI(target, out GameObject managedTarget))
        {
            return;
        }

        if (IsSettingsOpen)
        {
            CloseSettingsUI();
        }

        if (!HasOpenManagedUI)
        {
            CaptureNormalState();
        }

        lastRequestedManagedUI = managedTarget;

        if (!allowMultipleManagedUIs)
        {
            CloseOtherManagedUIObjects(managedTarget);
        }

        if (!IsOpen(managedTarget) && !TryOpenKmsInventory(managedTarget))
        {
            managedTarget.SetActive(true);
        }

        if (!IsOpen(managedTarget))
        {
            Debug.LogWarning(
                $"[SceneUIManager] '{managedTarget.name}'의 상위 오브젝트가 비활성 상태라 UI를 열 수 없습니다.",
                managedTarget);
        }

        SynchronizeManagedUIState();
        UpdateManagedOpenSnapshot();
    }

    /// <summary>등록된 UI 하나를 닫습니다.</summary>
    public void CloseManagedUI(GameObject target)
    {
        if (!TryResolveManagedUI(target, out GameObject managedTarget))
        {
            return;
        }

        CloseSingleManagedUI(managedTarget);
        SynchronizeManagedUIState();
        UpdateManagedOpenSnapshot();
    }

    /// <summary>등록된 UI 하나를 현재 활성 상태의 반대로 전환합니다.</summary>
    public void ToggleManagedUI(GameObject target)
    {
        if (!TryResolveManagedUI(target, out GameObject managedTarget))
        {
            return;
        }

        if (IsOpen(managedTarget))
        {
            CloseManagedUI(managedTarget);
        }
        else
        {
            OpenManagedUI(managedTarget);
        }
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
            if (!IsValidManagedUI(target) || !IsOpen(target))
            {
                continue;
            }

            closedAny = true;
            CloseSingleManagedUI(target);
        }

        SynchronizeManagedUIState();
        UpdateManagedOpenSnapshot();
        return closedAny;
    }

    /// <summary>해당 오브젝트가 Managed UI 목록에 등록되어 있는지 확인합니다.</summary>
    public bool IsManagedUI(GameObject target)
    {
        return target != null && managedUIObjects.Contains(target);
    }

    /// <summary>현재 씬에 등록된 ID에 대응하는 Managed UI를 찾습니다.</summary>
    public bool TryGetManagedUI(string managedUIId, out GameObject target)
    {
        target = null;

        string normalizedId = NormalizeManagedUIId(managedUIId);
        if (normalizedId.Length == 0)
        {
            return false;
        }

        if (!managedUIById.TryGetValue(normalizedId, out GameObject registeredTarget)
            || !IsValidManagedUI(registeredTarget))
        {
            return false;
        }

        target = registeredTarget;
        return true;
    }

    /// <summary>현재 씬에 해당 ID가 등록되어 있는지 확인합니다.</summary>
    public bool IsManagedUI(string managedUIId)
    {
        return TryGetManagedUI(managedUIId, out _);
    }

    /// <summary>ID로 현재 씬의 Managed UI를 엽니다.</summary>
    public void OpenManagedUI(string managedUIId)
    {
        if (TryResolveManagedUI(managedUIId, out GameObject managedTarget))
        {
            OpenManagedUI(managedTarget);
        }
    }

    /// <summary>ID로 현재 씬의 Managed UI를 닫습니다.</summary>
    public void CloseManagedUI(string managedUIId)
    {
        if (TryResolveManagedUI(managedUIId, out GameObject managedTarget))
        {
            CloseManagedUI(managedTarget);
        }
    }

    /// <summary>ID로 현재 씬의 Managed UI 열림 상태를 전환합니다.</summary>
    public void ToggleManagedUI(string managedUIId)
    {
        if (TryResolveManagedUI(managedUIId, out GameObject managedTarget))
        {
            ToggleManagedUI(managedTarget);
        }
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

    /// <summary>직접 참조 없이 등록 UI 열기를 요청합니다.</summary>
    public static bool TryOpenManagedUI(GameObject target)
    {
        if (Instance == null)
        {
            return false;
        }

        Instance.OpenManagedUI(target);
        return Instance.IsManagedUI(target) && IsOpen(target);
    }

    /// <summary>직접 참조 없이 등록 UI 닫기를 요청합니다.</summary>
    public static bool TryCloseManagedUI(GameObject target)
    {
        if (Instance == null || !Instance.IsManagedUI(target))
        {
            return false;
        }

        Instance.CloseManagedUI(target);
        return !IsOpen(target);
    }

    /// <summary>직접 참조 없이 등록 UI 토글을 요청합니다.</summary>
    public static bool TryToggleManagedUI(GameObject target)
    {
        if (Instance == null || !Instance.IsManagedUI(target))
        {
            return false;
        }

        Instance.ToggleManagedUI(target);
        return true;
    }

    /// <summary>현재 씬의 SceneUIManager에서 ID로 Managed UI 열기를 요청합니다.</summary>
    public static bool TryOpenManagedUI(string managedUIId)
    {
        if (Instance == null
            || !Instance.TryGetManagedUI(managedUIId, out GameObject target))
        {
            return false;
        }

        Instance.OpenManagedUI(target);
        return IsOpen(target);
    }

    /// <summary>현재 씬의 SceneUIManager에서 ID로 Managed UI 닫기를 요청합니다.</summary>
    public static bool TryCloseManagedUI(string managedUIId)
    {
        if (Instance == null
            || !Instance.TryGetManagedUI(managedUIId, out GameObject target))
        {
            return false;
        }

        Instance.CloseManagedUI(target);
        return !IsOpen(target);
    }

    /// <summary>현재 씬의 SceneUIManager에서 ID로 Managed UI 상태 전환을 요청합니다.</summary>
    public static bool TryToggleManagedUI(string managedUIId)
    {
        if (Instance == null
            || !Instance.TryGetManagedUI(managedUIId, out GameObject target))
        {
            return false;
        }

        Instance.ToggleManagedUI(target);
        return true;
    }

    private void SynchronizeManagedUIState()
    {
        if (HasOpenManagedUI)
        {
            ApplyManagedUIOpenState();
        }
        else
        {
            RestoreManagedUIState();
        }
    }

    private void ApplyManagedUIOpenState()
    {
        managedUIStateApplied = true;
        ApplyKmsManagedUIState();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreManagedUIState()
    {
        if (!managedUIStateApplied)
        {
            return;
        }

        if (WasCursorReleasedBeforeManagedUI())
        {
            ExitCursorReleaseToGameplayState();
        }
        else
        {
            RestoreNormalKmsPlayerState();
            RestoreNormalCursorState();
        }

        managedUIStateApplied = false;
    }

    private bool WasCursorReleasedBeforeManagedUI()
    {
        bool foundPlayerInputState = false;

        for (int i = 0; i < normalKmsPlayerInputStates.Count; i++)
        {
            KmsPlayerInputState state = normalKmsPlayerInputStates[i];
            if (state.Input == null)
            {
                continue;
            }

            foundPlayerInputState = true;

            // Alt의 ToggleCursor 상태는 커서만 해제되고 강제 입력 차단은 적용되지 않습니다.
            if (state.WasCursorReleased && !state.WasGameplayInputBlocked)
            {
                return true;
            }
        }

        // KMS PlayerInput을 아직 찾지 못한 경우에는 저장된 Unity 커서 상태를 사용합니다.
        return !foundPlayerInputState
            && hasNormalCursorState
            && (normalCursorLockMode != CursorLockMode.Locked || normalCursorVisible);
    }

    private void ExitCursorReleaseToGameplayState()
    {
        KMS.PlayerInput[] playerInputs = FindKmsPlayerInputs();
        for (int i = 0; i < playerInputs.Length; i++)
        {
            KMS.PlayerInput playerInput = playerInputs[i];
            if (playerInput == null)
            {
                continue;
            }

            playerInput.SetCursorReleased(false);
            playerInput.SetGameplayInputBlocked(false);
        }

        KMS.PlayerCameraController[] cameraControllers = FindKmsCameraControllers();
        for (int i = 0; i < cameraControllers.Length; i++)
        {
            if (cameraControllers[i] != null)
            {
                cameraControllers[i].SetCursorLocked(true);
            }
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 다음 Managed UI가 열릴 때 Alt 상태가 다시 복원되지 않도록 기준 상태도 갱신합니다.
        CaptureNormalState();
    }

    private void EnforceExclusiveManagedUIs()
    {
        if (allowMultipleManagedUIs)
        {
            return;
        }

        GameObject keepOpen = ResolveExclusiveUIToKeepOpen();
        if (keepOpen == null)
        {
            return;
        }

        lastRequestedManagedUI = keepOpen;
        CloseOtherManagedUIObjects(keepOpen);
    }

    private GameObject ResolveExclusiveUIToKeepOpen()
    {
        GameObject firstOpen = null;
        GameObject newlyOpened = null;

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (!IsValidManagedUI(target) || !IsOpen(target))
            {
                continue;
            }

            if (firstOpen == null)
            {
                firstOpen = target;
            }

            bool wasOpen = i < previousManagedOpenStates.Count
                && previousManagedOpenStates[i];
            if (!wasOpen)
            {
                newlyOpened = target;
            }
        }

        if (newlyOpened != null)
        {
            return newlyOpened;
        }

        if (lastRequestedManagedUI != null && IsOpen(lastRequestedManagedUI))
        {
            return lastRequestedManagedUI;
        }

        return firstOpen;
    }

    private void CloseOtherManagedUIObjects(GameObject keepOpen)
    {
        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (!IsValidManagedUI(target) || target == keepOpen || !IsOpen(target))
            {
                continue;
            }

            CloseSingleManagedUI(target);
        }
    }

    private void CloseSingleManagedUI(GameObject target)
    {
        if (!IsValidManagedUI(target))
        {
            return;
        }

        if (!TryCloseKmsInventory(target) && !TryCloseWayPointMap(target))
        {
            target.SetActive(false);
        }
    }

    private bool TryResolveManagedUI(GameObject target, out GameObject managedTarget)
    {
        managedTarget = null;

        if (target == null)
        {
            Debug.LogWarning("[SceneUIManager] 관리할 UI가 지정되지 않았습니다.", this);
            return false;
        }

        if (target == settingsUI)
        {
            Debug.LogWarning(
                "[SceneUIManager] Settings UI는 Managed UI API가 아닌 설정 UI API를 사용해야 합니다.",
                target);
            return false;
        }

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            if (managedUIObjects[i] == target)
            {
                managedTarget = target;
                return true;
            }
        }

        Debug.LogWarning(
            $"[SceneUIManager] '{target.name}'은 Managed UI Objects에 등록되지 않았습니다.",
            target);
        return false;
    }

    private bool TryResolveManagedUI(string managedUIId, out GameObject managedTarget)
    {
        if (TryGetManagedUI(managedUIId, out managedTarget))
        {
            return true;
        }

        string normalizedId = NormalizeManagedUIId(managedUIId);
        if (normalizedId.Length == 0)
        {
            Debug.LogWarning("[SceneUIManager] Managed UI ID가 비어 있습니다.", this);
        }
        else
        {
            Debug.LogWarning(
                $"[SceneUIManager] 현재 씬에 '{normalizedId}' ID가 등록되어 있지 않습니다.",
                this);
        }

        return false;
    }

    private void RebuildManagedUILookup()
    {
        SynchronizeManagedUIIdCount();
        managedUIById.Clear();

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            string id = NormalizeManagedUIId(managedUIIds[i]);

            if (!IsValidManagedUI(target) || id.Length == 0)
            {
                continue;
            }

            if (managedUIById.ContainsKey(id))
            {
                Debug.LogWarning(
                    $"[SceneUIManager] Managed UI ID '{id}'가 중복되었습니다. 첫 번째 항목을 사용합니다.",
                    this);
                continue;
            }

            managedUIById.Add(id, target);
        }
    }

    private void SynchronizeManagedUIIdCount()
    {
        while (managedUIIds.Count < managedUIObjects.Count)
        {
            int index = managedUIIds.Count;
            GameObject target = managedUIObjects[index];
            managedUIIds.Add(target != null ? target.name : string.Empty);
        }

        while (managedUIIds.Count > managedUIObjects.Count)
        {
            managedUIIds.RemoveAt(managedUIIds.Count - 1);
        }
    }

    private static string NormalizeManagedUIId(string managedUIId)
    {
        return string.IsNullOrWhiteSpace(managedUIId)
            ? string.Empty
            : managedUIId.Trim();
    }

    private void UpdateManagedOpenSnapshot()
    {
        while (previousManagedOpenStates.Count < managedUIObjects.Count)
        {
            previousManagedOpenStates.Add(false);
        }

        while (previousManagedOpenStates.Count > managedUIObjects.Count)
        {
            previousManagedOpenStates.RemoveAt(previousManagedOpenStates.Count - 1);
        }

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            previousManagedOpenStates[i] =
                IsValidManagedUI(managedUIObjects[i]) && IsOpen(managedUIObjects[i]);
        }
    }

    private void ApplySettingsOpenState()
    {
        if (!settingsStateApplied)
        {
            CacheSettingsState();
            settingsStateApplied = true;
        }

        Time.timeScale = 0f;
        ApplyKmsSettingsState();
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

    private void ApplyKmsSettingsState()
    {
        KMS.PlayerInput[] playerInputs = FindKmsPlayerInputs();
        for (int i = 0; i < playerInputs.Length; i++)
        {
            KMS.PlayerInput playerInput = playerInputs[i];
            if (playerInput == null)
            {
                continue;
            }

            if (!ContainsPlayerInput(settingsKmsPlayerInputStates, playerInput))
            {
                settingsKmsPlayerInputStates.Add(new KmsPlayerInputState
                {
                    Input = playerInput,
                    WasGameplayInputBlocked = playerInput.IsGameplayInputBlocked,
                    WasCursorReleased = playerInput.IsCursorReleased
                });
            }

            playerInput.SetGameplayInputBlocked(true);
            playerInput.SetCursorReleased(true);
        }

        KMS.PlayerCameraController[] cameraControllers = FindKmsCameraControllers();
        for (int i = 0; i < cameraControllers.Length; i++)
        {
            KMS.PlayerCameraController cameraController = cameraControllers[i];
            if (cameraController == null)
            {
                continue;
            }

            if (!settingsKmsCameraControllers.Contains(cameraController))
            {
                settingsKmsCameraControllers.Add(cameraController);
            }

            cameraController.SetCursorLocked(false);
        }
    }

    private void ApplyKmsManagedUIState()
    {
        KMS.PlayerInput[] playerInputs = FindKmsPlayerInputs();
        for (int i = 0; i < playerInputs.Length; i++)
        {
            if (playerInputs[i] == null)
            {
                continue;
            }

            playerInputs[i].SetGameplayInputBlocked(true);
            playerInputs[i].SetCursorReleased(true);
        }

        KMS.PlayerCameraController[] cameraControllers = FindKmsCameraControllers();
        for (int i = 0; i < cameraControllers.Length; i++)
        {
            if (cameraControllers[i] != null)
            {
                cameraControllers[i].SetCursorLocked(false);
            }
        }
    }

    private void RestoreSettingsState()
    {
        if (!settingsStateApplied)
        {
            return;
        }

        Time.timeScale = timeScaleBeforeSettings;

        for (int i = 0; i < settingsKmsPlayerInputStates.Count; i++)
        {
            KmsPlayerInputState state = settingsKmsPlayerInputStates[i];
            if (state.Input == null)
            {
                continue;
            }

            state.Input.SetCursorReleased(state.WasCursorReleased);
            state.Input.SetGameplayInputBlocked(state.WasGameplayInputBlocked);
        }

        bool shouldLockCursor = cursorLockModeBeforeSettings == CursorLockMode.Locked;
        for (int i = 0; i < settingsKmsCameraControllers.Count; i++)
        {
            if (settingsKmsCameraControllers[i] != null)
            {
                settingsKmsCameraControllers[i].SetCursorLocked(shouldLockCursor);
            }
        }

        Cursor.lockState = cursorLockModeBeforeSettings;
        Cursor.visible = cursorVisibleBeforeSettings;

        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(systemMenuWasOpenBeforeSettings);
        }

        settingsKmsPlayerInputStates.Clear();
        settingsKmsCameraControllers.Clear();
        settingsStateApplied = false;
    }

    private void CaptureNormalState()
    {
        normalCursorLockMode = Cursor.lockState;
        normalCursorVisible = Cursor.visible;
        hasNormalCursorState = true;
        CaptureNormalKmsPlayerState();
    }

    private void CaptureNormalKmsPlayerState()
    {
        bool hasLiveState = false;

        for (int i = normalKmsPlayerInputStates.Count - 1; i >= 0; i--)
        {
            KmsPlayerInputState state = normalKmsPlayerInputStates[i];
            if (state.Input == null)
            {
                normalKmsPlayerInputStates.RemoveAt(i);
                continue;
            }

            state.WasGameplayInputBlocked = state.Input.IsGameplayInputBlocked;
            state.WasCursorReleased = state.Input.IsCursorReleased;
            hasLiveState = true;
        }

        if (hasLiveState)
        {
            return;
        }

        KMS.PlayerInput[] playerInputs = FindKmsPlayerInputs();
        for (int i = 0; i < playerInputs.Length; i++)
        {
            KMS.PlayerInput playerInput = playerInputs[i];
            if (playerInput == null)
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
        bool restoredInput = false;

        for (int i = 0; i < normalKmsPlayerInputStates.Count; i++)
        {
            KmsPlayerInputState state = normalKmsPlayerInputStates[i];
            if (state.Input == null)
            {
                continue;
            }

            state.Input.SetCursorReleased(state.WasCursorReleased);
            state.Input.SetGameplayInputBlocked(state.WasGameplayInputBlocked);
            restoredInput = true;
        }

        bool shouldLockCursor = hasNormalCursorState
            ? normalCursorLockMode == CursorLockMode.Locked
            : fallbackClosedCursorLockMode == CursorLockMode.Locked;

        if (!restoredInput)
        {
            KMS.PlayerInput[] playerInputs = FindKmsPlayerInputs();
            for (int i = 0; i < playerInputs.Length; i++)
            {
                if (playerInputs[i] == null)
                {
                    continue;
                }

                playerInputs[i].SetCursorReleased(!shouldLockCursor);
                playerInputs[i].SetGameplayInputBlocked(false);
            }
        }

        KMS.PlayerCameraController[] cameraControllers = FindKmsCameraControllers();
        for (int i = 0; i < cameraControllers.Length; i++)
        {
            if (cameraControllers[i] != null)
            {
                cameraControllers[i].SetCursorLocked(shouldLockCursor);
            }
        }
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

    private void RestoreRuntimeState()
    {
        if (settingsStateApplied)
        {
            RestoreSettingsState();
        }

        if (managedUIStateApplied)
        {
            RestoreManagedUIState();
        }
    }

    private KMS.PlayerInput[] FindKmsPlayerInputs()
    {
        return PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerInput>(
            playerTag,
            playerLayerName);
    }

    private KMS.PlayerCameraController[] FindKmsCameraControllers()
    {
        return PlayerReferenceResolver.FindPlayerComponents<KMS.PlayerCameraController>(
            playerTag,
            playerLayerName);
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

    private GameObject FindOpenManagedUI()
    {
        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (IsValidManagedUI(target) && IsOpen(target))
            {
                return target;
            }
        }

        return null;
    }

    private static bool TryOpenKmsInventory(GameObject target)
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

            inventoryUI.Open();
            return true;
        }

        return false;
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

    private bool IsValidManagedUI(GameObject target)
    {
        return target != null && target != settingsUI;
    }

    private static bool AreInSameHierarchy(GameObject first, GameObject second)
    {
        return first == second
            || first.transform.IsChildOf(second.transform)
            || second.transform.IsChildOf(first.transform);
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
        SynchronizeManagedUIIdCount();

        for (int i = 0; i < managedUIObjects.Count; i++)
        {
            GameObject target = managedUIObjects[i];
            if (target == null)
            {
                managedUIIds[i] = NormalizeManagedUIId(managedUIIds[i]);
                continue;
            }

            if (target == settingsUI)
            {
                managedUIObjects[i] = null;
                continue;
            }

            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                if (managedUIObjects[previousIndex] == target)
                {
                    // Unity의 기본 + 버튼은 마지막 항목을 복제하므로 칸은 유지하고 값만 비웁니다.
                    managedUIObjects[i] = null;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(managedUIIds[i]) && managedUIObjects[i] != null)
            {
                managedUIIds[i] = managedUIObjects[i].name;
            }
            else
            {
                managedUIIds[i] = NormalizeManagedUIId(managedUIIds[i]);
            }
        }
    }
#endif
}
