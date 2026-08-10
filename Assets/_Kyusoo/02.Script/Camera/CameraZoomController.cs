using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

public class CameraZoomController : MonoBehaviour
{
    private Camera targetCamera;

    [Header("줌 설정")]
    [SerializeField] private float minSize = 3f;
    [SerializeField] private float maxSize = 8f;
    [SerializeField] private float zoomSensitivity = 0.15f;
    [SerializeField] private float smoothTime = 0.1f;

    [Header("최대 줌 크기 증가량")]
    [SerializeField] private float increaseAmount = 1f;

    [Header("UI 연동 설정")]
    [SerializeField] private Slider zoomSlider;          // 슬라이더 UI
    [SerializeField] private CanvasGroup zoomCanvasGroup; // UI 페이드인/아웃용 그룹
    [SerializeField] private TMP_Text zoomText;          // (선택) 줌 퍼센트/타이틀 텍스트
    [SerializeField] private float fadeDuration = 0.2f;    // 페이드 시간
    [SerializeField] private float displayDuration = 2f;  // 화면 노출 유지 시간 (2초)

    private float targetOrthoSize;
    private float zoomVelocity;
    private Sequence zoomUISequence;
    private bool isHovered = false; // 마우스가 UI 위에 있는지 여부

    void Start()
    {
        targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
        {
            targetOrthoSize = targetCamera.orthographicSize;
        }

        InitSlider();
        InitHoverDetector();

        // 시작 시 UI 투명하게 숨김
        if (zoomCanvasGroup != null)
        {
            zoomCanvasGroup.alpha = 0f;
        }
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

        if (zoomUISequence != null && zoomUISequence.IsActive())
        {
            zoomUISequence.Kill();
        }
    }

    private void InitSlider()
    {
        if (zoomSlider == null) return;

        zoomSlider.minValue = 0f;
        zoomSlider.maxValue = 1f;

        UpdateSliderUI();

        // 슬라이더를 직접 조작할 때도 줌 적용 및 UI 페이드인 트리거
        zoomSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    /// <summary>
    /// UI 패널 영역 감지를 위한 이벤트 헬퍼 세팅
    /// </summary>
    private void InitHoverDetector()
    {
        GameObject targetUIObject = zoomCanvasGroup != null ? zoomCanvasGroup.gameObject : (zoomSlider != null ? zoomSlider.gameObject : null);
        if (targetUIObject == null) return;

        // UI 호버 감지기 컴포넌트 자동 추가
        UIHoverDetector detector = targetUIObject.GetComponent<UIHoverDetector>();
        if (detector == null)
        {
            detector = targetUIObject.AddComponent<UIHoverDetector>();
        }

        detector.OnEnter = OnPointerEnterUI;
        detector.OnExit = OnPointerExitUI;
    }

    /// <summary>
    /// 마우스 휠 입력을 기반으로 부드럽게 줌인/줌아웃 처리
    /// </summary>
    private void HandleZoom()
    {
        if (Mouse.current == null) return;

        float scrollDelta = Mouse.current.scroll.ReadValue().y;

        if (Mathf.Abs(scrollDelta) > 0.01f)
        {
            targetOrthoSize -= scrollDelta * zoomSensitivity;
            targetOrthoSize = Mathf.Clamp(targetOrthoSize, minSize, maxSize);

            TriggerZoomUI();
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
        targetOrthoSize = Mathf.Lerp(maxSize, minSize, value);
        TriggerZoomUI();
    }

    private void UpdateSliderUI()
    {
        if (zoomSlider == null || targetCamera == null) return;

        float normalizedZoom = Mathf.InverseLerp(maxSize, minSize, targetCamera.orthographicSize);
        zoomSlider.SetValueWithoutNotify(normalizedZoom);

        if (zoomText != null)
        {
            int zoomPercent = Mathf.RoundToInt(normalizedZoom * 100f);
            zoomText.text = $"{zoomPercent}%";
        }
    }

    /// <summary>
    /// UI 페이드인 및 2초 카운트다운 타이머 시작
    /// </summary>
    private void TriggerZoomUI()
    {
        if (zoomCanvasGroup == null) return;

        if (zoomUISequence != null && zoomUISequence.IsActive())
        {
            zoomUISequence.Kill();
        }

        zoomUISequence = DOTween.Sequence();
        zoomUISequence.Append(zoomCanvasGroup.DOFade(1f, fadeDuration))
                      .AppendInterval(displayDuration)
                      .AppendCallback(TryFadeOut);
    }

    /// <summary>
    /// 2초 타이머가 끝났을 때 마우스가 UI 위에 없을 때만 페이드아웃
    /// </summary>
    private void TryFadeOut()
    {
        if (isHovered || zoomCanvasGroup == null) return;

        zoomCanvasGroup.DOFade(0f, fadeDuration);
    }

    #region 마우스 호버 콜백

    private void OnPointerEnterUI()
    {
        isHovered = true;

        // 마우스가 들어오면 기존 페이드아웃 타이머 취소하고 알파를 1로 유지
        if (zoomUISequence != null && zoomUISequence.IsActive())
        {
            zoomUISequence.Kill();
        }

        if (zoomCanvasGroup != null)
        {
            zoomCanvasGroup.DOFade(1f, fadeDuration);
        }
    }

    private void OnPointerExitUI()
    {
        isHovered = false;

        TriggerZoomUI();
    }

    #endregion
}

/// <summary>
/// UI 개체에 대한 마우스 진입/이탈을 감지하는 경량 헬퍼 클래스
/// </summary>
public class UIHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public System.Action OnEnter;
    public System.Action OnExit;

    public void OnPointerEnter(PointerEventData eventData) => OnEnter?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => OnExit?.Invoke();
}