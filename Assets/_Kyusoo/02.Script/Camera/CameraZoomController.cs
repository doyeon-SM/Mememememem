using UnityEngine;
using UnityEngine.InputSystem; 

public class CameraZoomController : MonoBehaviour
{
    private Camera targetCamera;

    [Header("¡‹ º≥¡§: ¡‹¿Œ, ¡‹æ∆øÙ, »Ÿ Ω∫≈©∑— ¡§µµ, ¡‹¿Œ/¡‹æ∆øÙ Ω√∞£")]
    [SerializeField] private float minSize = 1f;        
    [SerializeField] private float maxSize = 5f;       
    [SerializeField] private float zoomSensitivity = 0.15f; 
    [SerializeField] private float smoothTime = 0.1f;   

    private float targetOrthoSize;
    private float zoomVelocity;

    void Start()
    {
        targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
        {
            targetOrthoSize = targetCamera.orthographicSize;
        }
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        HandleZoom();
    }

    /// <summary>
    /// ∏∂øÏΩ∫ »Ÿ ¿‘∑¬¿ª ±‚π›¿∏∑Œ ∫ŒµÂ∑¥∞‘ ¡‹¿Œ/¡‹æ∆øÙ √≥∏Æ
    /// </summary>
    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scrollDelta = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            targetOrthoSize -= scrollDelta * zoomSensitivity;

            targetOrthoSize = Mathf.Clamp(targetOrthoSize, minSize, maxSize);
        }

        targetCamera.orthographicSize = Mathf.SmoothDamp(
            targetCamera.orthographicSize,
            targetOrthoSize,
            ref zoomVelocity,
            smoothTime
        );
    }
}