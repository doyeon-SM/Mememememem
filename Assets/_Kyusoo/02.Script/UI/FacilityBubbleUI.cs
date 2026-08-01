using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FacilityBubbleUI : MonoBehaviour
{
    [Header("기본 UI 연결")]
    [SerializeField] private Image itemIconImage;
    [SerializeField] private Button clickButton;

    [Header("플라잉 연출 설정")]
    [SerializeField] private GameObject flyingItemPrefab; // FlyingItemUI 부착된 프리팹
    [SerializeField] private RectTransform bagTargetRect;  // 우측 상단 가방 UI RectTransform (자동 탐색)

    private MonoBehaviour ownerFacility;
    private Action<MonoBehaviour> onClickCallback;

    private void Awake()
    {
        if (clickButton != null)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(OnClickBubble);
        }
    }

    public void Setup(MonoBehaviour facility, Sprite icon, Action<MonoBehaviour> clickCallback)
    {
        ownerFacility = facility;
        onClickCallback = clickCallback;

        if (itemIconImage != null)
        {
            itemIconImage.sprite = icon;
            itemIconImage.gameObject.SetActive(icon != null);
        }
    }

    public void PlayPopShowAnimation()
    {
        transform.DOKill();
        transform.localScale = Vector3.zero;
        transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
    }

    public void PlayCollectAnimation(Action onComplete)
    {
        // 🌟 1. bagTargetRect가 안 비어있다면 씬에서 B_Inventory 자동 탐색
        EnsureBagTargetReference();

        // 2. 기존 버블 팝업 축소 연출
        transform.DOKill();
        transform.DOScale(1.25f, 0.12f).OnComplete(() =>
        {
            transform.DOScale(0f, 0.12f).SetEase(Ease.InBack).OnComplete(() =>
            {
                onComplete?.Invoke();
            });
        });

        // 3. 가방으로 아이콘 날아가는 연출 쏘기
        if (flyingItemPrefab != null && bagTargetRect != null && itemIconImage != null && itemIconImage.sprite != null)
        {
            Canvas rootCanvas = GetComponentInParent<Canvas>();
            GameObject flyingObj = Instantiate(flyingItemPrefab, rootCanvas.transform);

            if (flyingObj.TryGetComponent<FlyingItemUI>(out var flyingUI))
            {
                Vector3 startPos = transform.position;
                Vector3 targetPos = bagTargetRect.position;

                flyingUI.PlayFlyAnimation(itemIconImage.sprite, startPos, targetPos, () =>
                {
                    // 가방 둠칫 튀어나오는 연출 (Punch Scale)
                    bagTargetRect.DOKill();
                    bagTargetRect.localScale = Vector3.one;
                    bagTargetRect.DOPunchScale(Vector3.one * 0.25f, 0.2f, 5, 1f);
                });
            }
        }
    }

    /// <summary>
    /// 🌟 씬 내에 있는 B_Inventory 오브젝트를 찾아 RectTransform을 자동 연결합니다.
    /// </summary>
    private void EnsureBagTargetReference()
    {
        if (bagTargetRect != null) return;

        // 1. "B_Inventory"라는 이름의 오브젝트 직접 탐색
        GameObject bagObj = GameObject.Find("B_Inventory");

        // 2. 만약 이름을 못 찾으면 씬 내부를 비활성화 오브젝트 포함하여 전수 탐색
        if (bagObj == null)
        {
            var allTransforms = Resources.FindObjectsOfTypeAll<RectTransform>();
            foreach (var rect in allTransforms)
            {
                if (rect.gameObject.hideFlags == HideFlags.None && rect.name.Equals("B_Inventory"))
                {
                    bagObj = rect.gameObject;
                    break;
                }
            }
        }

        if (bagObj != null)
        {
            bagTargetRect = bagObj.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogWarning("[FacilityBubbleUI] 씬에서 'B_Inventory' 오브젝트를 찾을 수 없습니다. 오브젝트 이름을 확인해주세요.");
        }
    }

    private void OnClickBubble()
    {
        onClickCallback?.Invoke(ownerFacility);
    }
}