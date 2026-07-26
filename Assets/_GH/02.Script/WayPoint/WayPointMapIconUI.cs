using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 하나의 웨이포인트 런타임 상태를 지도 아이콘으로 표시하고 클릭 이동 및 툴팁 입력을 전달합니다.
/// </summary>
public class WayPointMapIconUI : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    private WayPointMapUI owner;
    private WayPointRunTime state;
    private Graphic rootRaycastGraphic;

    public string Id => state != null ? state.Id : string.Empty;
    // 지도 UI가 아이콘을 만들 때 웨이포인트 상태와 소유 UI를 연결한다.
    /// <summary>소유 지도 UI와 표시할 웨이포인트 상태를 연결합니다.</summary>
    public void Initialize(WayPointMapUI newOwner, WayPointRunTime newState)
    {
        owner = newOwner;
        state = newState;

        if (iconImage == null)
        {
            iconImage = GetComponent<Image>();
        }

        if (iconImage == null)
        {
            iconImage = GetComponentInChildren<Image>(true);
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        EnsureRaycastTarget();

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        Refresh();
    }

    // ScriptableObject에 설정한 Map Position을 지도 이미지 기준 UI 좌표로 적용한다.
    /// <summary>지도 정의에 저장된 좌표를 아이콘의 Anchored Position으로 적용합니다.</summary>
    public void SetMapPosition(Vector2 anchoredPosition)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    // 해금 상태에 따라 Unlock Map Icon 또는 Active Map Icon으로 이미지를 교체한다.
    /// <summary>현재 해금 및 이동 상태에 맞춰 이미지와 버튼 상호작용 상태를 갱신합니다.</summary>
    public void Refresh()
    {
        if (state == null || state.Definition == null || iconImage == null)
        {
            return;
        }

        Sprite sprite = state.IsActive
            ? state.Definition.activeMapIcon
            : state.Definition.unlockMapIcon;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
        iconImage.raycastTarget = true;
        EnsureRaycastTarget();

        if (button != null)
        {
            button.interactable = owner != null && state != null;
        }
    }

    // 프리팹 구조와 상관없이 마우스 오버와 클릭 이벤트를 받을 수 있게 루트에 Raycast 대상을 보장한다.
    private void EnsureRaycastTarget()
    {
        if (rootRaycastGraphic == null)
        {
            rootRaycastGraphic = GetComponent<Graphic>();
        }

        if (rootRaycastGraphic == null)
        {
            Image raycastImage = gameObject.AddComponent<Image>();
            raycastImage.color = Color.clear;
            rootRaycastGraphic = raycastImage;
        }

        rootRaycastGraphic.raycastTarget = true;

        if (iconImage != null)
        {
            iconImage.raycastTarget = true;
        }
    }

    // 이동 가능한 활성 웨이포인트는 아이콘 클릭으로 즉시 이동한다.
    // 프리뷰 모드 또는 잠금 상태에서는 정보 툴팁만 유지한다.
    private void HandleClick()
    {
        if (owner == null || state == null)
        {
            return;
        }

        if (owner.CanTravelByClick(state))
        {
            owner.TravelTo(state.Id);
            return;
        }

        owner.NotifyWayPointPointerEnter(state, transform as RectTransform);
    }

    // 마우스가 아이콘에 올라오면 툴팁을 보여준다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || state == null)
        {
            return;
        }

        owner.NotifyWayPointPointerEnter(state, transform as RectTransform);
    }

    // 마우스 이동에 맞춰 툴팁 위치를 갱신한다.
    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        owner.MoveTooltip(transform as RectTransform);
    }

    // 아이콘을 벗어나면 툴팁 숨김을 요청한다. Fill_BG로 진입하면 요청이 취소된다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner != null)
        {
            owner.NotifyWayPointPointerExit();
        }
    }

}
