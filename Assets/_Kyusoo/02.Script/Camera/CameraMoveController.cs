using UnityEngine;
using UnityEngine.InputSystem; 

public class CameraMoveController : MonoBehaviour
{
    [Header("화면 움직임 드래그 설정: 드래그 민감도, 드래그 사용 여부")]
    [SerializeField] private float dragSensitivity = 0.01f; 
    [SerializeField] private bool useLeftClickToDrag = true;

    [Header("카메라가 이동하는 최대 범위(무한 이동 방지)")]
    [SerializeField] private float minX = -12f;            
    [SerializeField] private float maxX = -8f;            
    [SerializeField] private float minZ = -12f;            
    [SerializeField] private float maxZ = -8f;            

    private Transform cameraTransform;

    void Start()
    {
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    void Update()
    {
        HandleDragScroll();
    }

    /// <summary>
    /// 마우스가 화면 가장자리에 닿았을 때 CameraRig 이동시키는 로직
    /// </summary>
    private void HandleDragScroll()
    {
        if (Mouse.current == null || cameraTransform == null) return;

        bool isDrawing = useLeftClickToDrag ?
            Mouse.current.leftButton.isPressed :
            Mouse.current.rightButton.isPressed;

        if (isDrawing)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            if (mouseDelta.sqrMagnitude > 0.01f)
            {
                Vector3 camRight = cameraTransform.right;
                Vector3 camForward = cameraTransform.forward;

                camRight.y = 0f;
                camForward.y = 0f;
                camRight.Normalize();
                camForward.Normalize();

                // mouseDelta.x(좌측이동), mouseDelta.y(하단이동) 값에 따라 카메라 이동 방향 결정
                // Grab 방식을 위해 음수처리(화면을 잡고 끌어당기는 듯한 느낌)
                Vector3 moveVector = (camRight * -mouseDelta.x + camForward * -mouseDelta.y) * dragSensitivity;

                transform.position += moveVector;

                Vector3 clampedPosition = transform.position;
                clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
                clampedPosition.z = Mathf.Clamp(clampedPosition.z, minZ, maxZ);

                transform.position = clampedPosition;
            }
        }
    }
}