using KMS.InventoryDuped;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WayPointUIToggle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject targetUI;
    [SerializeField] private WayPointMapUI mapUI;
    [SerializeField] private bool hideOnStart = true;

    [Header("Input")]
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.M;
    [SerializeField] private WayPointMapOpenMode shortcutOpenMode = WayPointMapOpenMode.PreviewOnly;
    [SerializeField] private WayPointMapDefinition shortcutMap;

    [Header("Gameplay")]
    [SerializeField] private bool notifyInputManager = true;
    [SerializeField] private bool blockKmsPlayerInput = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private bool isOpen;

    private void Awake()
    {
        ResolveReferences();

        if (hideOnStart && targetUI != null)
        {
            SetOpen(false);
        }
    }

    private void Start()
    {
        ResolveReferences();

        if (hideOnStart)
        {
            SetOpen(false);
        }
        else
        {
            isOpen = targetUI.activeSelf;
            ApplyInputState();
        }
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            Toggle();
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            SetKmsPlayerInputBlocked(false);
        }
    }

    // M 키로 지도 UI를 열고 닫는다. M 키는 기본적으로 보기 전용 모드다.
    public void Toggle()
    {
        if (isOpen)
        {
            Close();
            return;
        }

        Open(shortcutOpenMode, shortcutMap);
    }

    // 버튼 등에서 기본 모드로 지도 UI를 열 때 사용한다.
    public void Open()
    {
        Open(shortcutOpenMode, shortcutMap);
    }

    // Stone 상호작용처럼 특정 모드와 맵으로 지도 UI를 열 때 사용한다.
    public void Open(WayPointMapOpenMode openMode, WayPointMapDefinition mapOverride = null)
    {
        ResolveReferences();

        if (mapUI != null)
        {
            mapUI.PrepareOpen(openMode, mapOverride);
        }

        SetOpen(true);
    }

    // 닫기 버튼이나 이동 성공 후 지도 UI를 닫을 때 사용한다.
    public void Close()
    {
        SetOpen(false);
    }

    // UI 활성화와 입력 잠금, 커서 상태를 한 번에 적용한다.
    public void SetOpen(bool open)
    {
        ResolveReferences();
        isOpen = open;

        if (!isOpen && mapUI != null)
        {
            mapUI.HideTooltip();
        }

        if (targetUI != null)
        {
            targetUI.SetActive(isOpen);
        }

        ApplyInputState();
    }

    // 인스펙터 연결이 비어 있어도 씬의 지도 UI를 찾아서 초기 닫힘과 열기 동작이 안정적으로 되게 한다.
    private void ResolveReferences()
    {
        if (mapUI == null)
        {
            mapUI = GetComponent<WayPointMapUI>();
        }

        if (mapUI == null)
        {
            mapUI = GetComponentInChildren<WayPointMapUI>(true);
        }

        if (mapUI == null)
        {
            WayPointMapUI[] mapUIs = FindObjectsByType<WayPointMapUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (mapUIs.Length > 0)
            {
                mapUI = mapUIs[0];
            }
        }

        if (targetUI == null && mapUI != null)
        {
            targetUI = mapUI.gameObject;
        }
    }

    // 현재 프레임에 지도 단축키가 눌렸는지 확인한다.
    private bool WasTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.mKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(legacyToggleKey);
    }

    // 지도 UI가 열렸을 때 플레이 입력과 마우스 커서 상태를 바꾼다.
    private void ApplyInputState()
    {
        if (notifyInputManager && InputManager.Instance != null)
        {
            InputManager.Instance.SetSystemMenuOpen(isOpen);
        }

        SetKmsPlayerInputBlocked(isOpen);

        if (!unlockCursorWhileOpen)
        {
            return;
        }

        Cursor.visible = isOpen;
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // KMS 플레이어가 추가된 씬에서도 지도 UI 위 마우스 입력이 월드 조작과 겹치지 않게 막는다.
    private void SetKmsPlayerInputBlocked(bool blocked)
    {
        if (!blockKmsPlayerInput)
        {
            return;
        }

        KMS.PlayerInput[] playerInputs = FindObjectsByType<KMS.PlayerInput>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (KMS.PlayerInput playerInput in playerInputs)
        {
            if (playerInput != null)
            {
                playerInput.SetGameplayInputBlocked(blocked);
            }
        }
    }
}
