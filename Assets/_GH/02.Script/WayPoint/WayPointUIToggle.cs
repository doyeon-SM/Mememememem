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
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private bool isOpen;

    private void Awake()
    {
        if (mapUI == null)
        {
            mapUI = GetComponentInChildren<WayPointMapUI>(true);
        }
    }

    private void Start()
    {
        if (targetUI == null && mapUI != null)
        {
            targetUI = mapUI.gameObject;
        }

        if (targetUI == null)
        {
            targetUI = gameObject;
        }

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

        if (!unlockCursorWhileOpen)
        {
            return;
        }

        Cursor.visible = isOpen;
        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
    }
}
