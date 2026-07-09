using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class WayPointMapIconUI : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;

    private WayPointMapUI owner;
    private WayPointRunTime state;
    private Graphic rootRaycastGraphic;

    public string Id => state != null ? state.Id : string.Empty;
    // 지도 UI가 아이콘을 만들 때 웨이포인트 상태와 소유 UI를 연결한다.
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
    public void SetMapPosition(Vector2 anchoredPosition)
    {
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }
    }

    // 해금 상태에 따라 Unlock Map Icon 또는 Active Map Icon으로 이미지를 교체한다.
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

    // 아이콘 클릭 시 지도 UI에 이동 요청을 전달한다.
    private void HandleClick()
    {
        if (owner == null || state == null)
        {
            return;
        }

        if (!owner.CanTravelByClick(state))
        {
            return;
        }

        owner.TravelTo(state.Id);
    }

    // 마우스가 아이콘에 올라오면 툴팁을 보여준다.
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null || state == null)
        {
            return;
        }

        owner.ShowTooltip(state, eventData.position);
    }

    // 마우스 이동에 맞춰 툴팁 위치를 갱신한다.
    public void OnPointerMove(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        owner.MoveTooltip(eventData.position);
    }

    // 마우스가 아이콘에서 벗어나면 툴팁을 숨긴다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (owner == null)
        {
            return;
        }

        owner.HideTooltip();
    }

}
