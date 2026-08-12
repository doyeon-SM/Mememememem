using DG.Tweening;
using TMPro;
using UnityEngine;

public class RanchStatusTooltipUI : MonoBehaviour
{
    private static RanchStatusTooltipUI instance;
    public static RanchStatusTooltipUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<RanchStatusTooltipUI>(FindObjectsInactive.Include);
            }
            return instance;
        }
    }

    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentPrefix = "";

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) Destroy(gameObject);

        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

        HideTooltip();
    }

    public void ShowTooltip(bool hasFood, Vector3 targetWorldPos)
    {
        // 툴팁 위치 이동 (아이콘 위쪽 40px)
        transform.position = targetWorldPos + new Vector3(0f, 40f, 0f);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (hasFood)
        {
            StartDotsAnimation("음식 보충중", Color.white);
        }
        else
        {
            StopDotsAnimation();
            if (statusText != null)
            {
                statusText.color = Color.red;
                statusText.text = "식량이 부족합니다";
            }
        }
    }

    private void StartDotsAnimation(string prefix, Color color)
    {
        if (isAnimatingDots && currentPrefix == prefix) return;

        currentPrefix = prefix;
        isAnimatingDots = true;

        if (dotsSequence != null) dotsSequence.Kill();
        if (statusText != null) statusText.color = color;

        dotsSequence = DOTween.Sequence();
        dotsSequence.AppendCallback(() => { if (statusText != null) statusText.text = $"{currentPrefix} ."; })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { if (statusText != null) statusText.text = $"{currentPrefix} .."; })
                    .AppendInterval(0.4f)
                    .AppendCallback(() => { if (statusText != null) statusText.text = $"{currentPrefix} ..."; })
                    .AppendInterval(0.4f)
                    .SetLoops(-1, LoopType.Restart);
    }

    private void StopDotsAnimation()
    {
        if (!isAnimatingDots && dotsSequence == null) return;
        isAnimatingDots = false;
        currentPrefix = "";
        if (dotsSequence != null)
        {
            dotsSequence.Kill();
            dotsSequence = null;
        }
    }

    public void HideTooltip()
    {
        StopDotsAnimation();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void OnDisable()
    {
        StopDotsAnimation();
    }
}