using DG.Tweening;
using HDY.Capture;
using MemSystem.Data;
using UnityEngine;
using UnityEngine.EventSystems;

public class RanchWarningIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private CapturedMemEntry currentMemEntry;
    private bool isIconShowing = false;
    private bool isHovered = false;

    private void Awake()
    {
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 개별 멤의 허기 상태 실시간 감지
    /// </summary>
    public void UpdateWarningStatus(CapturedMemEntry entry)
    {
        currentMemEntry = entry;

        // 허기량이 0 이하이거나 IsStarving인 경우만 배고픔으로 판단
        bool isStarving = currentMemEntry != null && (currentMemEntry.IsStarving || currentMemEntry.CurrentHunger <= 0);

        // 🌟 요구사항 5번: 허기량이 0이 된 경우 경고 아이콘 등장
        if (isStarving)
        {
            if (!isIconShowing)
            {
                isIconShowing = true;
                gameObject.SetActive(true);

                transform.DOKill();
                transform.localScale = Vector3.zero;
                transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
            }

            // 호버링 중인 동안 실시간으로 밥통 상태 반영해서 툴팁 갱신
            if (isHovered)
            {
                UpdateTooltipState();
            }
        }
        // 🌟 요구사항 4번: 허기 회복 / 정상 가동 중일 때는 둘 다 즉시 사라짐!
        else
        {
            if (isIconShowing)
            {
                isIconShowing = false;
                isHovered = false;

                if (RanchStatusTooltipUI.Instance != null)
                {
                    RanchStatusTooltipUI.Instance.HideTooltip();
                }

                transform.DOKill();
                transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    gameObject.SetActive(false);
                });
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isIconShowing || currentMemEntry == null) return;
        isHovered = true;
        UpdateTooltipState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (RanchStatusTooltipUI.Instance != null)
        {
            RanchStatusTooltipUI.Instance.HideTooltip();
        }
    }

    private void UpdateTooltipState()
    {
        int currentSatiety = ConsumeFoodSystem.Instance != null ? ConsumeFoodSystem.Instance.CurrentSatiety : 0;
        bool hasFood = currentSatiety > 0;

        if (RanchStatusTooltipUI.Instance != null)
        {
            RanchStatusTooltipUI.Instance.ShowTooltip(hasFood, transform.position);
        }
    }

    private void OnDisable()
    {
        transform.DOKill();
        isIconShowing = false;
        isHovered = false;
        if (RanchStatusTooltipUI.Instance != null)
        {
            RanchStatusTooltipUI.Instance.HideTooltip();
        }
    }
}