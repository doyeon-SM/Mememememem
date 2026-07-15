using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KMS
{
    public class PlayerInput : MonoBehaviour
    {
        [Header("Actions")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Mouse")]
        [SerializeField] private float mouseLookScale = 1f;

        [Header("Gamepad")]
        [SerializeField] private float gamepadLookScale = 12f;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAiming { get; private set; }
        public bool IsCursorReleased { get; private set; }

        public event Action<Vector2> MoveChanged;
        public event Action<Vector2> LookChanged;
        public event Action JumpPressed;
        public event Action JumpReleased;
        public event Action<bool> SprintChanged;
        public event Action PrimaryActionPressed;
        public event Action PrimaryActionReleased;
        public event Action SecondaryActionPressed;
        public event Action SecondaryActionReleased;
        public event Action ReloadPressed;
        public event Action InteractPressed;
        public event Action NextPressed;
        public event Action PreviousPressed;
        public event Action MenuPressed;
        public event Action InventoryPressed;
        public event Action CollectionPressed;
        public event Action MapPressed;
        public event Action<int> QuickSlotPressed;
        public event Action<int> QuickSlotScrolled;

        public bool IsGameplayInputBlocked => isGameplayInputBlocked;

        private const float QuickSlotScrollStepValue = 1f;

        private InputActionAsset runtimeInputActions;
        private InputActionMap gameplayActions;
        private InputActionMap globalActions;
        private InputAction moveAction;
        private InputAction mouseLookAction;
        private InputAction gamepadLookAction;
        private InputAction sprintAction;
        private bool isGameplayInputBlocked;
        private float quickSlotScrollAmount;

        private bool CanProcessGameplayInput =>
            isActiveAndEnabled && !isGameplayInputBlocked && !IsCursorReleased;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerInput] KMSPlayerControls InputActionAsset is not assigned.", this);
                enabled = false;
                return;
            }

            runtimeInputActions = Instantiate(inputActions);
            gameplayActions = runtimeInputActions.FindActionMap("Gameplay", true);
            globalActions = runtimeInputActions.FindActionMap("Global", true);
            moveAction = gameplayActions.FindAction("Move", true);
            mouseLookAction = gameplayActions.FindAction("MouseLook", true);
            gamepadLookAction = gameplayActions.FindAction("GamepadLook", true);
            sprintAction = gameplayActions.FindAction("Sprint", true);
            BindActions();
        }

        private void OnEnable()
        {
            if (globalActions == null) return;

            globalActions.Enable();
            RefreshGameplayActionMap();
        }

        private void OnDisable()
        {
            gameplayActions?.Disable();
            globalActions?.Disable();
            ClearGameplayState();
            IsCursorReleased = false;
        }

        private void OnDestroy()
        {
            if (runtimeInputActions == null) return;

            UnbindActions();
            Destroy(runtimeInputActions);
            runtimeInputActions = null;
        }

        private void Update()
        {
            if (!CanProcessGameplayInput)
            {
                SetMove(Vector2.zero);
                SetLook(Vector2.zero);
                SetSprint(false);
                return;
            }

            Vector2 move = moveAction.ReadValue<Vector2>();
            Vector2 look = mouseLookAction.ReadValue<Vector2>() * mouseLookScale;
            look += gamepadLookAction.ReadValue<Vector2>() * gamepadLookScale;

            SetMove(Vector2.ClampMagnitude(move, 1f));
            SetLook(look);
            SetSprint(sprintAction.IsPressed());
        }

        public void SetGameplayInputBlocked(bool isBlocked)
        {
            if (isGameplayInputBlocked == isBlocked) return;

            isGameplayInputBlocked = isBlocked;
            RefreshGameplayActionMap();

            if (isGameplayInputBlocked)
            {
                ClearGameplayState();
            }
        }

        public void SetCursorReleased(bool isReleased)
        {
            if (IsCursorReleased == isReleased) return;

            IsCursorReleased = isReleased;
            RefreshGameplayActionMap();

            if (IsCursorReleased)
            {
                ClearGameplayState();
            }
        }

        private void BindActions()
        {
            gameplayActions["Jump"].performed += HandleJumpPerformed;
            gameplayActions["Jump"].canceled += HandleJumpCanceled;
            gameplayActions["PrimaryAction"].performed += HandlePrimaryActionPerformed;
            gameplayActions["PrimaryAction"].canceled += HandlePrimaryActionCanceled;
            gameplayActions["SecondaryAction"].performed += HandleSecondaryActionPerformed;
            gameplayActions["SecondaryAction"].canceled += HandleSecondaryActionCanceled;
            gameplayActions["Reload"].performed += HandleReloadPerformed;
            gameplayActions["Interact"].performed += HandleInteractPerformed;
            gameplayActions["Next"].performed += HandleNextPerformed;
            gameplayActions["Previous"].performed += HandlePreviousPerformed;
            gameplayActions["QuickSlot1"].performed += HandleQuickSlot1Performed;
            gameplayActions["QuickSlot2"].performed += HandleQuickSlot2Performed;
            gameplayActions["QuickSlot3"].performed += HandleQuickSlot3Performed;
            gameplayActions["QuickSlot4"].performed += HandleQuickSlot4Performed;
            gameplayActions["QuickSlot5"].performed += HandleQuickSlot5Performed;
            gameplayActions["QuickSlot6"].performed += HandleQuickSlot6Performed;
            gameplayActions["QuickSlot7"].performed += HandleQuickSlot7Performed;
            gameplayActions["QuickSlot8"].performed += HandleQuickSlot8Performed;
            gameplayActions["QuickSlot9"].performed += HandleQuickSlot9Performed;
            gameplayActions["QuickSlot10"].performed += HandleQuickSlot10Performed;
            gameplayActions["QuickSlotScroll"].performed += HandleQuickSlotScrollPerformed;

            globalActions["ToggleCursor"].performed += HandleToggleCursorPerformed;
            globalActions["Menu"].performed += HandleMenuPerformed;
            globalActions["Inventory"].performed += HandleInventoryPerformed;
            globalActions["Collection"].performed += HandleCollectionPerformed;
            globalActions["Map"].performed += HandleMapPerformed;
        }

        private void UnbindActions()
        {
            gameplayActions["Jump"].performed -= HandleJumpPerformed;
            gameplayActions["Jump"].canceled -= HandleJumpCanceled;
            gameplayActions["PrimaryAction"].performed -= HandlePrimaryActionPerformed;
            gameplayActions["PrimaryAction"].canceled -= HandlePrimaryActionCanceled;
            gameplayActions["SecondaryAction"].performed -= HandleSecondaryActionPerformed;
            gameplayActions["SecondaryAction"].canceled -= HandleSecondaryActionCanceled;
            gameplayActions["Reload"].performed -= HandleReloadPerformed;
            gameplayActions["Interact"].performed -= HandleInteractPerformed;
            gameplayActions["Next"].performed -= HandleNextPerformed;
            gameplayActions["Previous"].performed -= HandlePreviousPerformed;
            gameplayActions["QuickSlot1"].performed -= HandleQuickSlot1Performed;
            gameplayActions["QuickSlot2"].performed -= HandleQuickSlot2Performed;
            gameplayActions["QuickSlot3"].performed -= HandleQuickSlot3Performed;
            gameplayActions["QuickSlot4"].performed -= HandleQuickSlot4Performed;
            gameplayActions["QuickSlot5"].performed -= HandleQuickSlot5Performed;
            gameplayActions["QuickSlot6"].performed -= HandleQuickSlot6Performed;
            gameplayActions["QuickSlot7"].performed -= HandleQuickSlot7Performed;
            gameplayActions["QuickSlot8"].performed -= HandleQuickSlot8Performed;
            gameplayActions["QuickSlot9"].performed -= HandleQuickSlot9Performed;
            gameplayActions["QuickSlot10"].performed -= HandleQuickSlot10Performed;
            gameplayActions["QuickSlotScroll"].performed -= HandleQuickSlotScrollPerformed;

            globalActions["ToggleCursor"].performed -= HandleToggleCursorPerformed;
            globalActions["Menu"].performed -= HandleMenuPerformed;
            globalActions["Inventory"].performed -= HandleInventoryPerformed;
            globalActions["Collection"].performed -= HandleCollectionPerformed;
            globalActions["Map"].performed -= HandleMapPerformed;
        }

        private void RefreshGameplayActionMap()
        {
            if (gameplayActions == null || !isActiveAndEnabled) return;

            if (isGameplayInputBlocked || IsCursorReleased)
            {
                gameplayActions.Disable();
            }
            else
            {
                gameplayActions.Enable();
            }
        }

        private void ClearGameplayState()
        {
            SetMove(Vector2.zero);
            SetLook(Vector2.zero);
            SetSprint(false);
            IsAiming = false;
            quickSlotScrollAmount = 0f;
        }

        private void HandleJumpPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) JumpPressed?.Invoke();
        }

        private void HandleJumpCanceled(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) JumpReleased?.Invoke();
        }

        private void HandlePrimaryActionPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) PrimaryActionPressed?.Invoke();
        }

        private void HandlePrimaryActionCanceled(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) PrimaryActionReleased?.Invoke();
        }

        private void HandleSecondaryActionPerformed(InputAction.CallbackContext _)
        {
            if (!CanProcessGameplayInput) return;

            IsAiming = true;
            SecondaryActionPressed?.Invoke();
        }

        private void HandleSecondaryActionCanceled(InputAction.CallbackContext _)
        {
            if (!CanProcessGameplayInput) return;

            IsAiming = false;
            SecondaryActionReleased?.Invoke();
        }

        private void HandleReloadPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) ReloadPressed?.Invoke();
        }

        private void HandleInteractPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) InteractPressed?.Invoke();
        }

        private void HandleNextPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) NextPressed?.Invoke();
        }

        private void HandlePreviousPerformed(InputAction.CallbackContext _)
        {
            if (CanProcessGameplayInput) PreviousPressed?.Invoke();
        }

        private void HandleQuickSlot1Performed(InputAction.CallbackContext _) => InvokeQuickSlot(0);
        private void HandleQuickSlot2Performed(InputAction.CallbackContext _) => InvokeQuickSlot(1);
        private void HandleQuickSlot3Performed(InputAction.CallbackContext _) => InvokeQuickSlot(2);
        private void HandleQuickSlot4Performed(InputAction.CallbackContext _) => InvokeQuickSlot(3);
        private void HandleQuickSlot5Performed(InputAction.CallbackContext _) => InvokeQuickSlot(4);
        private void HandleQuickSlot6Performed(InputAction.CallbackContext _) => InvokeQuickSlot(5);
        private void HandleQuickSlot7Performed(InputAction.CallbackContext _) => InvokeQuickSlot(6);
        private void HandleQuickSlot8Performed(InputAction.CallbackContext _) => InvokeQuickSlot(7);
        private void HandleQuickSlot9Performed(InputAction.CallbackContext _) => InvokeQuickSlot(8);
        private void HandleQuickSlot10Performed(InputAction.CallbackContext _) => InvokeQuickSlot(9);

        private void HandleQuickSlotScrollPerformed(InputAction.CallbackContext context)
        {
            if (!CanProcessGameplayInput) return;
            ApplyQuickSlotScroll(context.ReadValue<Vector2>().y);
        }

        private void HandleToggleCursorPerformed(InputAction.CallbackContext _)
        {
            if (isGameplayInputBlocked) return;
            SetCursorReleased(!IsCursorReleased);
        }

        private void HandleMenuPerformed(InputAction.CallbackContext _)
        {
            if (isGameplayInputBlocked || IsCursorReleased) return;
            MenuPressed?.Invoke();
        }

        private void HandleInventoryPerformed(InputAction.CallbackContext _)
        {
            InventoryPressed?.Invoke();
        }

        private void HandleCollectionPerformed(InputAction.CallbackContext _)
        {
            CollectionPressed?.Invoke();
        }

        private void HandleMapPerformed(InputAction.CallbackContext _)
        {
            MapPressed?.Invoke();
        }

        private void InvokeQuickSlot(int index)
        {
            if (CanProcessGameplayInput) QuickSlotPressed?.Invoke(index);
        }

        private void ApplyQuickSlotScroll(float scrollY)
        {
            if (Mathf.Approximately(scrollY, 0f)) return;

            quickSlotScrollAmount += scrollY;
            if (Mathf.Abs(quickSlotScrollAmount) < QuickSlotScrollStepValue) return;

            int direction = quickSlotScrollAmount > 0f ? -1 : 1;
            quickSlotScrollAmount = 0f;
            QuickSlotScrolled?.Invoke(direction);
        }

        private void SetMove(Vector2 move)
        {
            if (Move == move) return;
            Move = move;
            MoveChanged?.Invoke(Move);
        }

        private void SetLook(Vector2 look)
        {
            Look = look;
            LookChanged?.Invoke(Look);
        }

        private void SetSprint(bool isSprinting)
        {
            if (IsSprinting == isSprinting) return;
            IsSprinting = isSprinting;
            SprintChanged?.Invoke(IsSprinting);
        }
    }
}
