using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class FlyingItemUI : MonoBehaviour
{
    [SerializeField] private Image itemImage;

    /// <summary>
    /// 시작 위치(버블)에서 목표 위치(가방)로 날아가는 연출을 실행합니다.
    /// </summary>
    public void PlayFlyAnimation(Sprite icon, Vector3 startScreenPos, Vector3 targetScreenPos, Action onComplete)
    {
        if (itemImage != null && icon != null)
        {
            itemImage.sprite = icon;
        }

        transform.position = startScreenPos;
        transform.localScale = Vector3.one; 

        Sequence flySequence = DOTween.Sequence();

        // 1. 살짝 커지면서 위로 살짝 튀어 오르는 효과 (0.15초)
        flySequence.Append(transform.DOScale(1.2f, 0.15f).SetEase(Ease.OutBack))
                   .Join(transform.DOMoveY(startScreenPos.y + 30f, 0.15f));

        // 2. 가방 위치로 곡선 이동하며 축소 + 투명화 (1.5초)
        flySequence.Append(transform.DOMove(targetScreenPos, 1.5f).SetEase(Ease.InQuad))
                   .Join(transform.DOScale(0.2f, 1.5f))
                   .Join(itemImage.DOFade(0.3f, 1.5f));

        // 3. 도착 완료 시 콜백 호출 후 자기 자신 파괴
        flySequence.OnComplete(() =>
        {
            onComplete?.Invoke();
            Destroy(gameObject);
        });
    }
}