using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class WayPointUIToggle : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject targetUI;
    [SerializeField] private bool hideOnStart = true;

    [Header("Input")]
    [SerializeField] private KeyCode legacyToggleKey = KeyCode.M;

    [Header("Gameplay")]
    [SerializeField] private bool notifyInputManager = true;
    [SerializeField] private bool unlockCursorWhileOpen = true;

    private bool isOpen;

    private void Start()
    {
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

    // M 키 입력이나 버튼에서 호출해 지도 UI를 열고 닫는다.
    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    // 외부 버튼에서 지도 UI를 강제로 열 때 사용한다.
    public void Open()
    {
        SetOpen(true);
    }

    // 이동 완료나 닫기 버튼에서 지도 UI를 닫을 때 사용한다.
    public void Close()
    {
        SetOpen(false);
    }

    // UI 활성화와 입력 잠금, 커서 상태를 한 번에 적용한다.
    public void SetOpen(bool open)
    {
        isOpen = open;

        if (targetUI != null)
        {
            targetUI.SetActive(isOpen);
        }

        ApplyInputState();
    }

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
