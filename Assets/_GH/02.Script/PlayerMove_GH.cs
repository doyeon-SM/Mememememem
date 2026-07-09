using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerMove_GH : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -35f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Camera")]
    [SerializeField] private Camera followCamera;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 2.1f, -4.5f);
    [SerializeField] private float cameraFollowSharpness = 18f;

    private CharacterController controller;
    private float verticalVelocity;
    private float yaw;
    private float pitch = 15f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        followCamera = followCamera != null ? followCamera : Camera.main;
        yaw = transform.eulerAngles.y;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (IsSystemMenuOpen())
        {
            return;
        }

        UpdateLook();
        UpdateMovement();
        UpdateCursorLock();
    }

    private void LateUpdate()
    {
        UpdateCameraFollow();
    }

    private void UpdateLook()
    {
        Vector2 look = ReadLookInput();

        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    private void UpdateMovement()
    {
        Vector2 moveInput = ReadMoveInput();
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        if (controller.isGrounded && ReadJumpPressed())
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        verticalVelocity += gravity * Time.deltaTime;

        float speed = ReadSprintHeld() ? sprintSpeed : moveSpeed;
        Vector3 velocity = move * speed;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    private void UpdateCameraFollow()
    {
        if (followCamera == null)
        {
            followCamera = Camera.main;
        }

        if (followCamera == null)
        {
            return;
        }

        Quaternion bodyRotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 targetPosition = transform.position + bodyRotation * cameraOffset;

        followCamera.transform.position = Vector3.Lerp(
            followCamera.transform.position,
            targetPosition,
            1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime));

        followCamera.transform.rotation = cameraRotation;
    }

    private void UpdateCursorLock()
    {
        if (ReadUnlockCursorPressed())
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (ReadLockCursorPressed())
        {
            if (IsPointerOverUI())
            {
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private bool IsSystemMenuOpen()
    {
        return InputManager.Instance != null && InputManager.Instance.IsSystemMenuOpen;
    }

    private bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            return Vector2.ClampMagnitude(input, 1f);
        }
#endif
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }
#endif
        return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
    }

    private bool ReadJumpPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.spaceKey.wasPressedThisFrame;
        }
#endif
        return Input.GetButtonDown("Jump");
    }

    private bool ReadSprintHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }
#endif
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool ReadUnlockCursorPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }
#endif
        return Input.GetKeyDown(KeyCode.Escape);
    }

    private bool ReadLockCursorPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }
#endif
        return Input.GetMouseButtonDown(0);
    }
}
