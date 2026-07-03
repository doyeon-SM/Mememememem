using UnityEngine;
using UnityEngine.InputSystem; 

public class CameraMoveController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 12f;       
    [SerializeField] private float edgeSize = 30f;

    [Header("카메라가 이동하는 최대 범위(무한 이동 방지)")]
    [SerializeField] private float minX = -14f;            
    [SerializeField] private float maxX = -10f;            
    [SerializeField] private float minZ = -14f;            
    [SerializeField] private float maxZ = -10f;            

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
        HandleEdgeScroll();
    }

    /// <summary>
    /// 마우스가 화면 가장자리에 닿았을 때 CameraRig 이동시키는 로직
    /// </summary>
    private void HandleEdgeScroll()
    {
        if (Mouse.current == null || cameraTransform == null) return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Vector3 moveDirection = Vector3.zero;

        if (mousePosition.x < edgeSize)
        {
            moveDirection += -cameraTransform.right;
        }
        else if (mousePosition.x > Screen.width - edgeSize)
        {
            moveDirection += cameraTransform.right;
        }

        if (mousePosition.y < edgeSize)
        {
            moveDirection += -cameraTransform.forward;
        }
        else if (mousePosition.y > Screen.height - edgeSize)
        {
            moveDirection += cameraTransform.forward;
        }

        moveDirection.y = 0f;
        moveDirection.Normalize();

        transform.Translate(moveDirection * moveSpeed * Time.deltaTime, Space.World);

        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
        clampedPosition.z = Mathf.Clamp(clampedPosition.z, minZ, maxZ);

        transform.transform.position = clampedPosition;
    }
}