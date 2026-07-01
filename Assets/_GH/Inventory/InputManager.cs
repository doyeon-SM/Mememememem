using System;
using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    private GameInputControl _controls;   
    // 프레임 단위로 알아야하는건 이벤트 쓰지 말고 바로 값 접근
    public Vector2 LookInput { get
        {
            if (_controls == null) return Vector2.zero;
            return _controls.Gameplay.Look.ReadValue<Vector2>();
        }
    }
    public Vector2 AimPosition { get
        {
            if (_controls == null) return Vector2.zero;
            return _controls.Gameplay.Aim.ReadValue<Vector2>();
        }
    }

    public Vector2 moveInput { get; private set; }
    public bool isRunHold { get; private set; }
    public bool isFarmGridViewHold { get; private set; }
    public bool isKickHold { get; private set; }

    public event Action OnFarmGridViewStarted;
    public event Action OnFarmGridViewCanceled;

    public event Action OnInteractStarted;
    public event Action OnInteractCanceled;

    public event Action OnUsePressed;
    public event Action OnKickPressed;
    public event Action OnKickStarted;
    public event Action OnKickCanceled;
    public event Action OnInventoryPressed;
    public event Action OnRunStarted;
    public event Action OnRollPressed;

    public event Action OnPausePressed;
    public event Action OnCraftPressed;
    public event Action OnBuildPressed;

    private bool isSystemMenuOpen;
    private bool isOnlyMouseInputAllowed;
    private bool isGameplayInputBlocked;

    public event Action<int> OnQuickSlotPressed;
    public event Action<int> OnQuickSlotScroll;

    private float quickSlotScrollAmount;

    private const float QuickSlotScrollStepValue = 1f;

    #region Life Cycle

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Initialize();
    }

    void OnEnable()
    {
        if (_controls != null && !isGameplayInputBlocked) _controls.Gameplay.Enable();
    }

    void OnDisable()
    {
        if (_controls != null) _controls.Gameplay.Disable();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_controls != null) _controls.Dispose();
    }

    #endregion

    private void Initialize()
    {
        _controls = new GameInputControl();

        // 이동
        _controls.Gameplay.Move.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed)
            {
                moveInput = Vector2.zero;
                return;
            }
            moveInput = ctx.ReadValue<Vector2>();
        };
        _controls.Gameplay.Move.canceled += ctx => moveInput = Vector2.zero;
        
        // 달리기 (+스탭)
        _controls.Gameplay.Run.started += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            isRunHold = true;
            OnRunStarted?.Invoke();
        };
        _controls.Gameplay.Run.canceled += ctx => isRunHold = false;

        // 구르기
        _controls.Gameplay.Roll.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            OnRollPressed?.Invoke();
        };

        // 상호작용
        _controls.Gameplay.Interact.started += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;
            OnInteractStarted?.Invoke();
        };
        _controls.Gameplay.Interact.canceled += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;
            OnInteractCanceled?.Invoke();
        };

        // 장착 장비 사용
        _controls.Gameplay.Use.performed += ctx =>
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;
            
            OnUsePressed?.Invoke(); 
        };

        // 발차기 (+전투 중 막기)
        _controls.Gameplay.Kick.started += ctx =>
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            isKickHold = true;
            OnKickStarted?.Invoke();
        };
        _controls.Gameplay.Kick.canceled += ctx =>
        {
            isKickHold = false;
            OnKickCanceled?.Invoke();
        };
        _controls.Gameplay.Kick.performed += ctx =>
        {
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            OnKickPressed?.Invoke();
        };

        // 일시정지 메뉴 호출
        _controls.Gameplay.Pause.performed += ctx =>
        {
            if (isOnlyMouseInputAllowed) return;

            OnPausePressed?.Invoke();
        };

        // 인벤토리 호출
        _controls.Gameplay.Inventory.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            OnInventoryPressed?.Invoke();
        };

        // 제작메뉴 호출
        _controls.Gameplay.Craft.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            OnCraftPressed?.Invoke();
        };

        // 건설메뉴 호출
        _controls.Gameplay.Build.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            OnBuildPressed?.Invoke();
        };

        // 퀵슬롯
        _controls.Gameplay.QuickSlot_1.performed += ctx => InvokeQuickSlot(0);
        _controls.Gameplay.QuickSlot_2.performed += ctx => InvokeQuickSlot(1);
        _controls.Gameplay.QuickSlot_3.performed += ctx => InvokeQuickSlot(2);
        _controls.Gameplay.QuickSlot_4.performed += ctx => InvokeQuickSlot(3);
        _controls.Gameplay.QuickSlot_5.performed += ctx => InvokeQuickSlot(4);
        _controls.Gameplay.QuickSlot_6.performed += ctx => InvokeQuickSlot(5);
        _controls.Gameplay.QuickSlot_7.performed += ctx => InvokeQuickSlot(6);
        _controls.Gameplay.QuickSlot_8.performed += ctx => InvokeQuickSlot(7);
        _controls.Gameplay.QuickSlot_9.performed += ctx => InvokeQuickSlot(8);
        _controls.Gameplay.QuickSlot_10.performed += ctx => InvokeQuickSlot(9);

        // 마우스 휠로 퀵슬롯 선택 이동
        _controls.Gameplay.QuickSlot_Scroll.performed += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

            if (QuickSlotScrollBlockerUI.isPointerOver)
            {
                quickSlotScrollAmount = 0f;
                return;
            }

            Vector2 scroll = ctx.ReadValue<Vector2>();
            ApplyQuickSlotScroll(scroll.y);
        };

        // 추가: Alt 누르기 시작
        _controls.Gameplay.FarmGridView.started += ctx =>
        {
            if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;
            if (isGameplayInputBlocked) return;

            isFarmGridViewHold = true;
            OnFarmGridViewStarted?.Invoke();
        };

        // 추가: Alt 떼기
        _controls.Gameplay.FarmGridView.canceled += ctx =>
        {
            isFarmGridViewHold = false;
            OnFarmGridViewCanceled?.Invoke();
        };
    }

    private void ApplyQuickSlotScroll(float scrollY)
    {
        if (Mathf.Approximately(scrollY, 0f)) return;

        quickSlotScrollAmount += scrollY;

        if (Mathf.Abs(quickSlotScrollAmount) < QuickSlotScrollStepValue) return;

        int direction = quickSlotScrollAmount > 0f ? -1 : 1;

        quickSlotScrollAmount -= Mathf.Sign(quickSlotScrollAmount) * QuickSlotScrollStepValue;

        OnQuickSlotScroll?.Invoke(direction);
    }

    private void InvokeQuickSlot(int index)
    {
        if (isSystemMenuOpen || isOnlyMouseInputAllowed) return;

        OnQuickSlotPressed?.Invoke(index);
    }

    private void ClearGamePlayInputState()
    {
        moveInput = Vector2.zero;
        isRunHold = false;
        isKickHold = false;
        isFarmGridViewHold = false;
        quickSlotScrollAmount = 0f;
        OnFarmGridViewCanceled?.Invoke();
        OnKickCanceled?.Invoke();
    }

    // 각종 메뉴입력 잠금용 함수
    public void SetSystemMenuOpen(bool isOpen)
    {
        isSystemMenuOpen = isOpen;

        // 이동관련 입력 도중 메뉴를 호출하면 해당 입력이 유지되기 때문에 초기화해줌 (상호작용 누른상태에서 메뉴 호출도 취소시킴)
        if (isSystemMenuOpen)
        {
            ClearGamePlayInputState();
            OnInteractCanceled?.Invoke();
        } 
    }

    // 오직 마우스만 작동하는 함수
    public void SetOnlyMouseInputAllowed(bool isAllowed)
    {
        isOnlyMouseInputAllowed = isAllowed;

        if (isOnlyMouseInputAllowed)
        {
            ClearGamePlayInputState();
            OnInteractCanceled?.Invoke();
        }
    }

    // Gameplay Input 자체를 블락 / 해제
    public void SetGameplayInputBlocked(bool isBlocked)
    {
        isGameplayInputBlocked = isBlocked;

        ClearGamePlayInputState();
        OnInteractCanceled?.Invoke();

        if (_controls == null) return;

        if (isGameplayInputBlocked) _controls.Gameplay.Disable();
        else _controls.Gameplay.Enable();
    }
}