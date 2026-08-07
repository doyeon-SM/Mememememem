using DG.Tweening;
using TMPro;
using UnityEngine;

public class RanchStatusTooltipUI : MonoBehaviour
{
    public static RanchStatusTooltipUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Sequence dotsSequence;
    private bool isAnimatingDots = false;
    private string currentPrefix = "";

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 🌟 툴팁이 마우스 레이캐스트를 가로채서 호버링이 풀리는 현상 방지
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        HideTooltip();
    }

    /// <summary>
    /// 🌟 요구사항 5번: 밥통 상태에 따라 툴팁 실시간 출력
    /// </summary>
    public void ShowTooltip(bool hasFood, Vector3 targetWorldPos)
    {
        // 아이콘 위쪽 40px 위치에 배치
        transform.position = targetWorldPos + new Vector3(0f, 40f, 0f);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
        }
        gameObject.SetActive(true);

        // 밥통에 음식이 있으면 "음식 보충중 . . ."
        if (hasFood)
        {
            StartDotsAnimation("음식 보충중", Color.white);
        }
        // 밥통에 음식이 없으면 "식량이 부족합니다"
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
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        StopDotsAnimation();
    }
}