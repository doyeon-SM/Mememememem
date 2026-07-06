using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace KMS
{
    public class PlayerInput : MonoBehaviour
    {
        [Header("Mouse")]
        [SerializeField] private float mouseLookScale = 1f;

        [Header("Gamepad")]
        [SerializeField] private float gamepadLookScale = 12f;

        [Header("Interaction")]
        [SerializeField] private Key interactKey = Key.E;
        [SerializeField] private bool primaryActionTriggersInteraction = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAiming { get; private set; }

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

        private void OnDisable()
        {
            SetMove(Vector2.zero);
            SetLook(Vector2.zero);
            SetSprint(false);
            IsAiming = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            Gamepad gamepad = Gamepad.current;

            UpdateMove(keyboard, gamepad);
            UpdateLook(mouse, gamepad);
            UpdateButtons(keyboard, mouse, gamepad);
        }

        private void UpdateMove(Keyboard keyboard, Gamepad gamepad)
        {
            Vector2 move = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x -= 1f;
            }

            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > move.sqrMagnitude)
                {
                    move = stick;
                }
            }

            SetMove(Vector2.ClampMagnitude(move, 1f));
        }

        private void UpdateLook(Mouse mouse, Gamepad gamepad)
        {
            Vector2 look = Vector2.zero;

            if (mouse != null)
            {
                look += mouse.delta.ReadValue() * mouseLookScale;
            }

            if (gamepad != null)
            {
                look += gamepad.rightStick.ReadValue() * gamepadLookScale;
            }

            SetLook(look);
        }

        private void UpdateButtons(Keyboard keyboard, Mouse mouse, Gamepad gamepad)
        {
            bool sprint = (keyboard != null && keyboard.leftShiftKey.isPressed) ||
                          (gamepad != null && gamepad.leftStickButton.isPressed);
            SetSprint(sprint);

            if (WasPressed(keyboard?.spaceKey, gamepad?.buttonSouth)) JumpPressed?.Invoke();
            if (WasReleased(keyboard?.spaceKey, gamepad?.buttonSouth)) JumpReleased?.Invoke();

            if (WasPressed(mouse?.leftButton, gamepad?.rightTrigger))
            {
                PrimaryActionPressed?.Invoke();
                if (primaryActionTriggersInteraction)
                {
                    InteractPressed?.Invoke();
                }
            }

            if (WasReleased(mouse?.leftButton, gamepad?.rightTrigger))
            {
                PrimaryActionReleased?.Invoke();
            }

            if (WasPressed(mouse?.rightButton, gamepad?.leftTrigger))
            {
                IsAiming = true;
                SecondaryActionPressed?.Invoke();
            }

            if (WasReleased(mouse?.rightButton, gamepad?.leftTrigger))
            {
                IsAiming = false;
                SecondaryActionReleased?.Invoke();
            }

            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) ReloadPressed?.Invoke();
                if (keyboard.digit2Key.wasPressedThisFrame) NextPressed?.Invoke();
                if (keyboard.digit1Key.wasPressedThisFrame) PreviousPressed?.Invoke();
                if (keyboard.escapeKey.wasPressedThisFrame) MenuPressed?.Invoke();

                KeyControl interactControl = keyboard[interactKey];
                if (interactControl != null && interactControl.wasPressedThisFrame)
                {
                    InteractPressed?.Invoke();
                }
            }

            if (gamepad != null)
            {
                if (gamepad.buttonWest.wasPressedThisFrame) ReloadPressed?.Invoke();
                if (gamepad.rightShoulder.wasPressedThisFrame) NextPressed?.Invoke();
                if (gamepad.leftShoulder.wasPressedThisFrame) PreviousPressed?.Invoke();
                if (gamepad.startButton.wasPressedThisFrame) MenuPressed?.Invoke();
            }
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

        private static bool WasPressed(ButtonControl first, ButtonControl second)
        {
            return (first != null && first.wasPressedThisFrame) ||
                   (second != null && second.wasPressedThisFrame);
        }

        private static bool WasReleased(ButtonControl first, ButtonControl second)
        {
            return (first != null && first.wasReleasedThisFrame) ||
                   (second != null && second.wasReleasedThisFrame);
        }
    }
}
