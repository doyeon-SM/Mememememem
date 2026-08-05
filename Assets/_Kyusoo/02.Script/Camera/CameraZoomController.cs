using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem; 

public class CameraZoomController : MonoBehaviour
{
    private Camera targetCamera;

    [Header("¡‹ º≥¡§: ¡‹¿Œ, ¡‹æ∆øÙ, »Ÿ Ω∫≈©∑— ¡§µµ, ¡‹¿Œ/¡‹æ∆øÙ Ω√∞£")]
    [SerializeField] private float minSize = 3f;        
    [SerializeField] private float maxSize = 8f;       
    [SerializeField] private float zoomSensitivity = 0.15f; 
    [SerializeField] private float smoothTime = 0.1f;

    [Header("√÷¥Î ¡‹ ≈©±‚ ¡ı∞°∑Æ")]
    [SerializeField] private float increaseAmount = 1f;

    [Header("UI ø¨µø º≥¡§")]
    [SerializeField] private Slider zoomSlider;

    private float targetOrthoSize;
    private float zoomVelocity;

    void Start()
    {
        targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
        {
            targetOrthoSize = targetCamera.orthographicSize;
        }

        InitSlider();
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        HandleZoom();

        UpdateSliderUI();
    }

    private void OnEnable()
    {
        GridManager.GridExpanded += IncreaseMaxSize;
    }

    private void OnDisable()
    {
        GridManager.GridExpanded -= IncreaseMaxSize;
    }

    private void InitSlider()
    {
        if (zoomSlider == null) return;

        zoomSlider.minValue = 0f;
        zoomSlider.maxValue = 1f;

        UpdateSliderUI();

        zoomSlider.onValueChanged.AddListener(OnSliderValueChanged);
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
    public void IncreaseMaxSize()
    {
        maxSize += increaseAmount;
        targetOrthoSize = Mathf.Clamp(targetOrthoSize, minSize, maxSize);
    }

    private void OnSliderValueChanged(float value)
    {
        // ΩΩ∂Û¿Ã¥ı 1(ø¿∏•¬ ) -> minSize (¡‹¿Œ)
        // ΩΩ∂Û¿Ã¥ı 0(øﬁ¬ )   -> maxSize (¡‹æ∆øÙ)
        targetOrthoSize = Mathf.Lerp(maxSize, minSize, value);
    }

    private void UpdateSliderUI()
    {
        if (zoomSlider == null || targetCamera == null) return;

        float normalizedZoom = Mathf.InverseLerp(maxSize, minSize, targetCamera.orthographicSize);

        zoomSlider.SetValueWithoutNotify(normalizedZoom);
    }
}