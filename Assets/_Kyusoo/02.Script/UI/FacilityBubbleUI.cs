using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FacilityBubbleUI : MonoBehaviour
{
    [Header("UI 컴포넌트 연결")]
    [SerializeField] private Image itemIconImage;
    [SerializeField] private Button clickButton;

    private Action onClickCallback;

    private void Awake()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClickBubble);
        }
    }

    public void Setup(Sprite icon, Action clickCallback)
    {
        onClickCallback = clickCallback;

        if (itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.gameObject.SetActive(icon != null);
        }
    }

    /// <summary>
    /// 🌟 생성/노출 연출: 0에서 1로 튀어나오는 Pop-up 애니메이션
    /// </summary>
    public void PlayPopShowAnimation()
    {
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
    }

    /// <summary>
    /// 🌟 수령 시 DOTween 연출: 순간적으로 1.25배 커진 후 빠르게 축소되며 사라짐
    /// </summary>
    public void PlayCollectAnimation(Action onComplete)
    {
        transform.DOKill();
        transform.DOScale(1.25f, 0.12f).OnComplete(() =>
        {
            transform.DOScale(0f, 0.12f).SetEase(Ease.InBack).OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        });
    }

    private void OnClickBubble()
    {
        onClickCallback?.Invoke();
    }
}